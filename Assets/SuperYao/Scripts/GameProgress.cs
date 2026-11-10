using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuperYao
{
    /// <summary>
    /// Partie enregistrée (sur l'appareil, PlayerPrefs) : stages terminés, vies, pièces, bananes, diamants, son.
    /// Comme sur le téléphone : on peut rejouer tout stage atteint et jouer le suivant ; les compteurs suivent le
    /// joueur d'un stage à l'autre. Enchaîne aussi les écrans : accueil → carte → stage → carte (ou accueil si la
    /// partie est perdue).
    /// </summary>
    public static class GameProgress
    {
        public const string TitleScene = "Accueil", MapScene = "Carte";
        const string P = "superyao.";

        /// <summary>Mode essai : sur la carte, tous les stages construits sont ouverts (pour tester le stage 2 sans
        /// finir le stage 1). À remettre à false avant de publier le jeu.</summary>
        public static readonly bool TestMode = false; // coupé le 2026-11-10 (demandé) : on suit l'ordre des stages

        public static bool HasSave => PlayerPrefs.GetInt(P + "started", 0) == 1;
        /// <summary>Nombre de stages terminés (le suivant, d'indice Furthest, peut être joué).</summary>
        public static int Furthest => PlayerPrefs.GetInt(P + "furthest", 0);
        /// <summary>Stage où se tient Yao sur la carte.</summary>
        public static int Selected => PlayerPrefs.GetInt(P + "selected", 0);
        public static int Lives => PlayerPrefs.GetInt(P + "lives", StageState.StartLives);
        public static int Coins => PlayerPrefs.GetInt(P + "coins", 0);
        public static int Bananas => PlayerPrefs.GetInt(P + "bananas", 0);
        public static int Diamonds => PlayerPrefs.GetInt(P + "diamonds", 0);
        public static bool Muted
        {
            get => PlayerPrefs.GetInt(P + "muted", 0) == 1;
            set { PlayerPrefs.SetInt(P + "muted", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Pouvoirs déjà trouvés dans un stage : ils peuvent ensuite s'acheter au marché.</summary>
        public static bool UnlockedGrow => PlayerPrefs.GetInt(P + "unlocked.grow", 0) == 1;
        public static bool UnlockedFire => PlayerPrefs.GetInt(P + "unlocked.fire", 0) == 1;
        /// <summary>Yao commence le prochain stage grand / avec le feu (gardé d'un stage à l'autre, ou acheté).</summary>
        public static bool Big => PlayerPrefs.GetInt(P + "big", 0) == 1;
        public static bool Fire => PlayerPrefs.GetInt(P + "fire", 0) == 1;

        public static void Unlock(bool fire)
        {
            PlayerPrefs.SetInt(P + (fire ? "unlocked.fire" : "unlocked.grow"), 1);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ marché (market.ts du téléphone)

        public const int BananaPrice = 2;                         // une banane vendue = 2 pièces
        public const int LifePrice = 400, GrowPrice = 100, FirePrice = 200;

        /// <summary>Vend des bananes contre des pièces ; renvoie le nombre de pièces gagnées.</summary>
        public static int SellBananas(int count)
        {
            int sold = Mathf.Clamp(count, 0, Bananas);
            if (sold <= 0) return 0;
            PlayerPrefs.SetInt(P + "bananas", Bananas - sold);
            PlayerPrefs.SetInt(P + "coins", Coins + sold * BananaPrice);
            PlayerPrefs.Save();
            return sold * BananaPrice;
        }

        public enum Offer { Life, Grow, Fire }

        /// <summary>Achète une vie ou un pouvoir déjà trouvé ; renvoie la phrase à afficher, et vrai si c'est fait.</summary>
        public static (bool ok, string note) Buy(Offer offer)
        {
            int price = offer == Offer.Life ? LifePrice : offer == Offer.Grow ? GrowPrice : FirePrice;
            if (offer == Offer.Grow && !UnlockedGrow || offer == Offer.Fire && !UnlockedFire) return (false, "Ce pouvoir n'a pas encore été trouvé.");
            if (offer == Offer.Grow && Big) return (false, "Tu as déjà ton skate.");
            if (offer == Offer.Fire && Fire) return (false, "Tu as déjà le feu.");
            if (Coins < price) return (false, offer == Offer.Life ? $"Il faut {LifePrice} pièces pour une vie." : $"Il faut {price} pièces.");
            PlayerPrefs.SetInt(P + "coins", Coins - price);
            switch (offer)
            {
                case Offer.Life: PlayerPrefs.SetInt(P + "lives", Lives + 1); break;
                case Offer.Grow: PlayerPrefs.SetInt(P + "big", 1); break;
                case Offer.Fire: PlayerPrefs.SetInt(P + "big", 1); PlayerPrefs.SetInt(P + "fire", 1); break;
            }
            PlayerPrefs.Save();
            return (true, offer == Offer.Life ? "Une vie en plus !" : offer == Offer.Grow ? "Le skate est prêt pour le prochain stage." : "L'orbe de feu est prêt pour le prochain stage.");
        }

        // ------------------------------------------------------------------ cadeau vidéo du marché

        /// <summary>Cadeaux par jour en regardant une vidéo au marché.</summary>
        public const int VideoGiftsPerDay = 5;
        static string Today => System.DateTime.Now.ToString("yyyyMMdd");
        public static int VideoGiftsLeft =>
            PlayerPrefs.GetString(P + "gift.day", "") == Today ? Mathf.Max(0, VideoGiftsPerDay - PlayerPrefs.GetInt(P + "gift.count", 0)) : VideoGiftsPerDay;

        /// <summary>Vidéo regardée au marché : un cadeau au hasard (pièces le plus souvent, parfois des bananes, un diamant
        /// ou une vie). Renvoie la phrase à afficher.</summary>
        public enum GiftKind { Coins, Bananas, Diamond, Life }
        public struct Gift { public GiftKind kind; public int amount; public string text; }

        public static Gift VideoGift()
        {
            int used = PlayerPrefs.GetString(P + "gift.day", "") == Today ? PlayerPrefs.GetInt(P + "gift.count", 0) : 0;
            PlayerPrefs.SetString(P + "gift.day", Today);
            PlayerPrefs.SetInt(P + "gift.count", used + 1);
            float roll = Random.value;
            Gift gift;
            int lives = Lives, coins = Coins, bananas = Bananas, diamonds = Diamonds;
            if (roll < 0.5f) { int n = Random.Range(30, 81); coins += n; gift = new Gift { kind = GiftKind.Coins, amount = n, text = $"Cadeau : {n} pièces !" }; }
            else if (roll < 0.7f) { int n = Random.Range(5, 11); bananas += n; gift = new Gift { kind = GiftKind.Bananas, amount = n, text = $"Cadeau : {n} bananes !" }; }
            else if (roll < 0.85f) { diamonds++; gift = new Gift { kind = GiftKind.Diamond, amount = 1, text = "Cadeau : un diamant !" }; }
            else { lives++; gift = new Gift { kind = GiftKind.Life, amount = 1, text = "Cadeau : une vie en plus !" }; }
            PlayerPrefs.SetInt(P + "started", 1);
            SaveCounters(lives, coins, bananas, diamonds);
            Saved?.Invoke();
            return gift;
        }

        /// <summary>Stage en cours (indice dans le catalogue).</summary>
        public static int Playing { get; private set; }

        /// <summary>Stage en cours d'après la scène chargée (stage lancé directement depuis l'éditeur).</summary>
        public static void SyncPlaying(int stage) => Playing = Mathf.Max(0, stage);
        /// <summary>Bilan du stage qui vient d'être gagné, affiché sur la carte (-1 : aucun).</summary>
        public static int JustCompleted { get; private set; } = -1;
        public static int GotCoins, GotBananas, GotDiamonds;

        public static void NewGame()
        {
            PlayerPrefs.SetInt(P + "started", 1);
            PlayerPrefs.SetInt(P + "furthest", 0);
            PlayerPrefs.SetInt(P + "selected", 0);
            PlayerPrefs.SetInt(P + "big", 0);
            PlayerPrefs.SetInt(P + "fire", 0);
            PlayerPrefs.SetInt(P + "unlocked.grow", 0);
            PlayerPrefs.SetInt(P + "unlocked.fire", 0);
            SaveCounters(StageState.StartLives, 0, 0, 0);
            JustCompleted = -1;
        }

        public static void ClearSave()
        {
            // Seulement la partie : pas tout PlayerPrefs, qui garde aussi la connexion au compte en ligne.
            foreach (var k in new[] { "started", "furthest", "selected", "lives", "coins", "bananas", "diamonds", "unlocked.grow", "unlocked.fire", "big", "fire" })
                PlayerPrefs.DeleteKey(P + k);
            for (int i = 0; i < MaxStages; i++) PlayerPrefs.DeleteKey(P + "best." + i);
            PlayerPrefs.Save();
            JustCompleted = -1;
            Saved?.Invoke();
        }

        public static void Select(int stage)
        {
            PlayerPrefs.SetInt(P + "selected", stage);
            PlayerPrefs.Save();
        }

        /// <summary>Lance un stage depuis la carte.</summary>
        public static void Play(int stage, string scene)
        {
            Playing = stage;
            JustCompleted = -1;
            Select(stage);
            SceneManager.LoadScene(scene);
        }

        /// <summary>Stage gagné : on enregistre, Yao avancera jusqu'au stage suivant sur la carte.</summary>
        public static void CompleteStage(int lives, int coins, int bananas, int diamonds, int gotCoins, int gotBananas, int gotDiamonds,
            bool big, bool fire, int score = 0)
        {
            if (score > BestScore(Playing)) PlayerPrefs.SetInt(P + "best." + Playing, score);
            PlayerPrefs.SetInt(P + "big", big ? 1 : 0);
            PlayerPrefs.SetInt(P + "fire", fire ? 1 : 0);
            PlayerPrefs.SetInt(P + "started", 1);
            PlayerPrefs.SetInt(P + "furthest", Mathf.Max(Furthest, Playing + 1));
            PlayerPrefs.SetInt(P + "selected", Playing + 1);
            SaveCounters(lives, coins, bananas, diamonds);
            JustCompleted = Playing;
            GotCoins = gotCoins;
            GotBananas = gotBananas;
            GotDiamonds = gotDiamonds;
            Saved?.Invoke();
            Load(MapScene);
        }

        /// <summary>Plus de vies : retour à l'accueil, la partie repart avec 3 vies (stages terminés conservés).</summary>
        public static void GameOver()
        {
            PlayerPrefs.SetInt(P + "lives", StageState.StartLives);
            PlayerPrefs.SetInt(P + "big", 0);
            PlayerPrefs.SetInt(P + "fire", 0);
            PlayerPrefs.Save();
            Saved?.Invoke();
            Load(TitleScene);
        }

        // ------------------------------------------------------------------ scores et sauvegarde en ligne

        /// <summary>Nombre de stages prévus (80, comme sur le téléphone) : un meilleur score par stage.</summary>
        public const int MaxStages = 80;
        /// <summary>Meilleur score obtenu en gagnant ce stage.</summary>
        public static int BestScore(int stage) => PlayerPrefs.GetInt(P + "best." + stage, 0);
        /// <summary>Score du classement : somme des meilleurs scores de chaque stage gagné.</summary>
        public static int TotalScore
        {
            get
            {
                int total = 0;
                for (int i = 0; i < MaxStages; i++) total += BestScore(i);
                return total;
            }
        }
        /// <summary>La partie vient d'être enregistrée (stage gagné, partie perdue) : le compte en ligne l'envoie.</summary>
        public static event System.Action Saved;

        /// <summary>Toute la partie, pour la sauvegarde en ligne (le son reste propre à l'appareil).</summary>
        [System.Serializable]
        public class Snapshot
        {
            public int started, furthest, selected, lives, coins, bananas, diamonds, unlockedGrow, unlockedFire, big, fire;
            public int[] best;
            public int Total { get { int t = 0; if (best != null) foreach (var b in best) t += b; return t; } }
        }

        public static Snapshot Export()
        {
            var best = new int[MaxStages];
            for (int i = 0; i < MaxStages; i++) best[i] = BestScore(i);
            return new Snapshot
            {
                started = HasSave ? 1 : 0, furthest = Furthest, selected = Selected, lives = Lives, coins = Coins, bananas = Bananas,
                diamonds = Diamonds, unlockedGrow = UnlockedGrow ? 1 : 0, unlockedFire = UnlockedFire ? 1 : 0, big = Big ? 1 : 0,
                fire = Fire ? 1 : 0, best = best,
            };
        }

        /// <summary>Remplace la partie de l'appareil par celle du compte.</summary>
        public static void Import(Snapshot s)
        {
            if (s == null) return;
            PlayerPrefs.SetInt(P + "started", s.started);
            PlayerPrefs.SetInt(P + "furthest", s.furthest);
            PlayerPrefs.SetInt(P + "selected", s.selected);
            PlayerPrefs.SetInt(P + "unlocked.grow", s.unlockedGrow);
            PlayerPrefs.SetInt(P + "unlocked.fire", s.unlockedFire);
            PlayerPrefs.SetInt(P + "big", s.big);
            PlayerPrefs.SetInt(P + "fire", s.fire);
            for (int i = 0; i < MaxStages; i++) PlayerPrefs.SetInt(P + "best." + i, s.best != null && i < s.best.Length ? s.best[i] : 0);
            SaveCounters(s.lives, s.coins, s.bananas, s.diamonds);
        }

        /// <summary>Quelle partie garder entre deux : la plus avancée (stages terminés, puis score, puis pièces).</summary>
        public static bool IsAhead(Snapshot a, Snapshot b)
        {
            if (b == null) return true;
            if (a == null) return false;
            if (a.furthest != b.furthest) return a.furthest > b.furthest;
            if (a.Total != b.Total) return a.Total > b.Total;
            return a.coins >= b.coins;
        }

        static void SaveCounters(int lives, int coins, int bananas, int diamonds)
        {
            PlayerPrefs.SetInt(P + "lives", lives);
            PlayerPrefs.SetInt(P + "coins", coins);
            PlayerPrefs.SetInt(P + "bananas", bananas);
            PlayerPrefs.SetInt(P + "diamonds", diamonds);
            PlayerPrefs.Save();
        }

        /// <summary>Charge un écran s'il fait partie du jeu (sinon, essai d'un stage seul : on le recommence).</summary>
        public static void Load(string scene)
        {
            if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
            else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
