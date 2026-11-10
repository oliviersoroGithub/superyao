using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuperYao
{
    /// <summary>
    /// État du stage en cours : score, pièces, bananes, diamants, vies et temps. Valeurs de la version téléphone
    /// (constants.ts) : 3 vies, 300 unités de temps (une toutes les 24 images, soit 0,4 s). Le panneau (Hud) les
    /// affiche ; les événements serviront aussi aux sons.
    /// </summary>
    public class StageState : MonoBehaviour
    {
        public const int CoinScore = 200, BananaScore = 100, PowerScore = 1000, BrickScore = 50, DiamondScore = 5000;
        // Temps : le téléphone donne 300 unités (2 minutes) pour un stage 1 de 204 cases ; le nôtre en fait 350, avec des
        // traversées en pirogue à attendre. À 300, le temps s'épuisait et Yao mourait « tout seul » : 500 (3 min 20 s).
        public const int StartLives = 3, StartTime = 500;
        const float TimeUnit = 24f / 60f;

        public static StageState Instance { get; private set; }

        public int LevelNumber = 1;
        /// <summary>Nom du stage (catalogue), affiché dans la pause.</summary>
        public string LevelName { get; private set; } = "";
        public int Score { get; private set; }
        public int Coins { get; private set; }
        public int Bananas { get; private set; }
        public int Diamonds { get; private set; }
        public int Lives { get; private set; } = StartLives;
        public int TimeLeft { get; private set; } = StartTime;
        public bool Paused { get; private set; }
        public bool Muted { get; private set; }
        /// <summary>Où le héros repart après une vie perdue : départ, puis dernier point de contrôle atteint.</summary>
        public float RespawnX { get; private set; } = 4f;
        public bool Won { get; private set; }
        public bool GameOver { get; private set; }

        public const int TimeBonus = 50; // points par unité de temps restante, à l'arrivée
        const float EndDelay = 3.5f;     // fin de partie ou stage gagné : délai avant de recommencer (provisoire)
        float endClock;

        /// <summary>Quelque chose a été gagné : « coin », « banana », « diamond », « power », « break », « stomp ».</summary>
        public event System.Action<string> Gained;

        float clock;

        /// <summary>Ramassé pendant ce stage (bilan affiché sur la carte).</summary>
        int gotCoins, gotBananas, gotDiamonds;
        bool ended;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            // Les compteurs et les vies suivent le joueur d'un stage à l'autre (partie enregistrée).
            if (GameProgress.HasSave)
            {
                Lives = GameProgress.Lives;
                Coins = GameProgress.Coins;
                Bananas = GameProgress.Bananas;
                Diamonds = GameProgress.Diamonds;
            }
            Muted = GameProgress.Muted;
            // Fenêtre « Continuer avec une vidéo » (cachée), et chargement de la première vidéo.
            gameObject.AddComponent<ContinueScreen>();
            _ = RewardedVideo.Instance;
            // Numéro et nom du stage d'après la scène chargée (catalogue) : juste aussi quand on lance un stage
            // directement depuis l'éditeur, sans passer par la carte. La victoire est alors enregistrée pour ce stage.
            int index = GameProgress.Playing;
            var catalog = StageCatalog.Load();
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (catalog != null)
                for (int i = 0; i < catalog.stages.Count; i++)
                    if (catalog.stages[i].scene == scene) { index = i; break; }
            GameProgress.SyncPlaying(index);
            LevelNumber = index + 1;
            if (catalog != null && index < catalog.stages.Count) LevelName = catalog.stages[index].name ?? "";
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (Won || GameOver)
            {
                // Arrivée : le temps restant défile en points (3 unités par image, comme le téléphone).
                if (Won && TimeLeft > 0)
                {
                    int chunk = Mathf.Min(TimeLeft, 3);
                    TimeLeft -= chunk;
                    Score += chunk * TimeBonus;
                    return;
                }
                // Puis : victoire -> la carte (Yao avance d'une étape) ; fin de partie -> l'accueil.
                endClock += Time.unscaledDeltaTime;
                if (endClock > EndDelay && !ended)
                {
                    ended = true;
                    var hero = FindAnyObjectByType<HeroController>();
                    var fire = hero != null ? hero.GetComponent<HeroFire>() : null;
                    if (Won) GameProgress.CompleteStage(Lives, Coins, Bananas, Diamonds, gotCoins, gotBananas, gotDiamonds,
                        hero != null && hero.IsBig, fire != null && fire.HasFire, Score);
                    else GameProgress.GameOver();
                }
                return;
            }
            if (Paused || TimeLeft <= 0) return;
            clock += Time.deltaTime;
            while (clock >= TimeUnit && TimeLeft > 0)
            {
                clock -= TimeUnit;
                if (--TimeLeft == 0) FindAnyObjectByType<HeroController>()?.Die(false); // temps écoulé : une vie perdue
            }
        }

        public void AddCoin() { Coins++; gotCoins++; Score += CoinScore; Gained?.Invoke("coin"); }
        public void AddBanana() { Bananas++; gotBananas++; Score += BananaScore; Gained?.Invoke("banana"); }
        public void AddDiamond() { Diamonds++; gotDiamonds++; Score += DiamondScore; Gained?.Invoke("diamond"); }
        public void AddPower() { Score += PowerScore; Gained?.Invoke("power"); }
        public void AddBrick() { Score += BrickScore; Gained?.Invoke("break"); }
        public void AddStomp() { Score += Enemy.StompScore; Gained?.Invoke("stomp"); }
        public void AddKnock() { Score += Enemy.StompScore * 2; Gained?.Invoke("stomp"); }
        /// <summary>Boss touché (400 points par coup, comme le téléphone) ; vaincu : 5000 points.</summary>
        public void AddBossHit() { Score += Enemy.StompScore * 4; Gained?.Invoke("bossHit"); }
        public void AddBossDown() { Score += 5000; Gained?.Invoke("bossDown"); }

        /// <summary>Une vie perdue (à la fin de l'animation de mort) : le temps repart de 300. Plus de vies : fin de
        /// partie. Renvoie faux si la partie est finie (le héros ne doit pas réapparaître).</summary>
        public bool LoseLife()
        {
            if (GameOver || OfferingContinue) return false;
            Lives--;
            TimeLeft = StartTime;
            clock = 0f;
            if (Lives > 0) return true;
            // Plus de vies : une fois par stage, on propose de regarder une vidéo pour continuer (ContinueScreen). Le jeu
            // est figé en attendant la réponse.
            if (!continueUsed && RewardedVideo.Instance.CanShow)
            {
                OfferingContinue = true;
                Time.timeScale = 0f;
                return false;
            }
            GameOver = true;
            return false;
        }

        /// <summary>Plus de vies, et une vidéo est proposée pour continuer.</summary>
        public bool OfferingContinue { get; private set; }
        bool continueUsed;

        /// <summary>Vidéo regardée : une vie, Yao repart du dernier point de contrôle.</summary>
        public void AcceptContinue()
        {
            if (!OfferingContinue) return;
            OfferingContinue = false;
            continueUsed = true;
            Lives = 1;
            Time.timeScale = 1f;
            FindAnyObjectByType<HeroController>()?.Revive();
            SoundFx.Play("life");
        }

        /// <summary>Le joueur abandonne : fin de partie, comme avant.</summary>
        public void DeclineContinue()
        {
            if (!OfferingContinue) return;
            OfferingContinue = false;
            Time.timeScale = 1f;
            GameOver = true;
        }

        public void ReachCheckpoint(float respawnX) => RespawnX = Mathf.Max(RespawnX, respawnX);

        /// <summary>Le héros a atteint l'arrivée.</summary>
        public void Win()
        {
            if (Won || GameOver) return;
            Won = true;
            SoundFx.Play("win");
        }

        public void TogglePause()
        {
            if (Won || GameOver) return;
            Paused = !Paused;
            Time.timeScale = Paused ? 0f : 1f;
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            GameProgress.Muted = Muted; // le réglage du son vaut pour tout le jeu
        }
    }
}
