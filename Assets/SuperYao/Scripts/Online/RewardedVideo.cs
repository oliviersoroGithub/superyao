using System;
using UnityEngine;
using UnityEngine.UI;
#if SUPERYAO_LEVELPLAY
using Unity.Services.LevelPlay;
#endif

namespace SuperYao
{
    /// <summary>
    /// Vidéos récompensées (publicité regardée jusqu'au bout = un cadeau), via Unity LevelPlay (SUPERYAO_LEVELPLAY, posé
    /// tout seul quand le paquet est installé) : une vie pour continuer après la fin de partie, un cadeau au marché.
    /// Tant que LevelPlay n'est pas installé ou configuré (clés ci-dessous vides), une fausse vidéo de 5 secondes la
    /// remplace dans l'éditeur et en mode essai (GameProgress.TestMode), pour tester le parcours ; sur un vrai téléphone
    /// sans configuration, aucune vidéo n'est proposée.
    /// </summary>
    public class RewardedVideo : MonoBehaviour
    {
        // À remplir avec les identifiants du tableau de bord LevelPlay (Unity Cloud > Monetization) : clé de l'application
        // et identifiant du bloc « Rewarded », pour Android et pour iPhone.
        const string AndroidAppKey = "", AndroidRewardedUnit = "";
        const string IosAppKey = "", IosRewardedUnit = "";

        static RewardedVideo instance;
        public static RewardedVideo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GameObject("Videos_Recompensees").AddComponent<RewardedVideo>();
                    DontDestroyOnLoad(instance.gameObject);
                }
                return instance;
            }
        }

        static string AppKey => Application.platform == RuntimePlatform.IPhonePlayer ? IosAppKey : AndroidAppKey;
        static string UnitId => Application.platform == RuntimePlatform.IPhonePlayer ? IosRewardedUnit : AndroidRewardedUnit;
        static bool Configured => !string.IsNullOrEmpty(AppKey) && !string.IsNullOrEmpty(UnitId);
        /// <summary>Fausse vidéo : dans l'éditeur, ou en mode essai, tant que LevelPlay n'est pas prêt.</summary>
        static bool Simulated => Application.isEditor || GameProgress.TestMode;

        /// <summary>Une vidéo peut être proposée (vraie et chargée, ou fausse pour les essais).</summary>
        public bool CanShow
        {
            get
            {
#if SUPERYAO_LEVELPLAY
                if (Configured && !Application.isEditor) return ad != null && ad.IsAdReady();
#endif
                return Simulated;
            }
        }

        Action<bool> pending;
        bool rewarded, finished;

#if SUPERYAO_LEVELPLAY
        LevelPlayRewardedAd ad;
        bool started;

        void Start()
        {
            if (!Configured || Application.isEditor || started) return;
            started = true;
            LevelPlay.OnInitSuccess += _ =>
            {
                ad = new LevelPlayRewardedAd(UnitId);
                ad.OnAdRewarded += (info, reward) => rewarded = true;
                ad.OnAdClosed += info => finished = true;
                ad.OnAdDisplayFailed += (info, error) => { Debug.LogWarning("Super Yao : vidéo non affichée : " + error.ErrorMessage); finished = true; };
                ad.OnAdLoadFailed += error => { Debug.LogWarning("Super Yao : pas de vidéo chargée : " + error.ErrorMessage); retryAt = Time.unscaledTime + 30f; };
                ad.LoadAd();
            };
            LevelPlay.OnInitFailed += error => Debug.LogWarning("Super Yao : LevelPlay indisponible : " + error.ErrorMessage);
            LevelPlay.Init(AppKey);
        }

        float retryAt = -1f;
#endif

        /// <summary>Prépare les vidéos dès le lancement (la première met quelques secondes à se charger).</summary>
        public void Warm() { }

        /// <summary>Montre une vidéo ; <paramref name="done"/> reçoit vrai si elle a été regardée jusqu'au bout.</summary>
        public void Show(Action<bool> done)
        {
            if (pending != null) return;
            rewarded = finished = false;
#if SUPERYAO_LEVELPLAY
            if (Configured && !Application.isEditor)
            {
                if (ad == null || !ad.IsAdReady()) { done?.Invoke(false); return; }
                pending = done;
                ad.ShowAd();
                return;
            }
#endif
            if (!Simulated) { done?.Invoke(false); return; }
            pending = done;
            ShowFake();
        }

        void Update()
        {
#if SUPERYAO_LEVELPLAY
            if (retryAt > 0f && Time.unscaledTime > retryAt && ad != null) { retryAt = -1f; ad.LoadAd(); }
#endif
            if (!finished || pending == null) return;
            // Réponse de la vidéo, rendue ici sur le fil principal du jeu.
            var done = pending;
            pending = null;
            finished = false;
            done(rewarded);
#if SUPERYAO_LEVELPLAY
            if (ad != null) ad.LoadAd(); // la suivante
#endif
        }

        // ------------------------------------------------------------------ fausse vidéo (essais)

        void ShowFake()
        {
            var (full, s) = UiKit.Screen(transform, "Video_Essai", 900);
            var veil = UiKit.Stretch(UiKit.Rect(full, "Noir"));
            veil.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.96f);
            veil.SetAsFirstSibling();
            var title = UiKit.Label(UiKit.At(UiKit.Rect(s, "Titre"), 0, 300, 1672, 80), "Vidéo d'essai", 54, Color.white);
            var sub = UiKit.Label(UiKit.At(UiKit.Rect(s, "Texte"), 0, 400, 1672, 60), "", 30, UiKit.Hex("#FFD23F"));
            var skip = UiKit.Button(s, "Passer", 1672 / 2 - 400, 520, 360, 84, UiKit.Hex("#5D4037"), UiKit.Hex("#3A2213"), 3, 4, 30, null);
            UiKit.Label((RectTransform)skip.transform.Find("Face"), "Fermer sans cadeau", 26, Color.white);
            var take = UiKit.Button(s, "Cadeau", 1672 / 2 + 40, 520, 360, 84, UiKit.Hex("#2EAA3F"), UiKit.Hex("#3A2213"), 3, 4, 30, null);
            var takeLabel = UiKit.Label((RectTransform)take.transform.Find("Face"), "Récupérer", 30, Color.white);
            take.interactable = false;
            var clock = full.gameObject.AddComponent<FakeClock>();
            clock.Init(5f, left => sub.text = left > 0f ? $"La vidéo se termine dans {Mathf.CeilToInt(left)} s" : "Vidéo terminée !", () => take.interactable = true);
            void End(bool ok)
            {
                rewarded = ok;
                finished = true;
                Destroy(full.gameObject);
            }
            skip.onClick.AddListener(() => End(false));
            take.onClick.AddListener(() => End(true));
            _ = title;
            _ = takeLabel;
        }

        /// <summary>Compte à rebours de la fausse vidéo, en temps réel (le jeu peut être figé).</summary>
        class FakeClock : MonoBehaviour
        {
            float left;
            Action<float> tick;
            Action end;
            public void Init(float seconds, Action<float> onTick, Action onEnd) { left = seconds; tick = onTick; end = onEnd; tick(left); }
            void Update()
            {
                if (end == null) return;
                left -= Time.unscaledDeltaTime;
                tick(Mathf.Max(0f, left));
                if (left <= 0f) { end(); end = null; }
            }
        }
    }
}
