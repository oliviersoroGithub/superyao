using UnityEngine;
using UnityEngine.UI;

namespace SuperYao
{
    /// <summary>
    /// Page d'accueil, d'après la maquette de la version téléphone (TitleScreen.tsx, repère 1672 × 941) : fond
    /// d'Abidjan, Yao qui court, logo, médaillon du héros, panneau en bois du niveau et sa phrase d'accroche, boutons
    /// « Continuer » / « Nouvelle partie » (ou « Jouer » sans partie enregistrée), son et réglages, rappel des commandes.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public Font font;
        public Sprite background, logo, hero, portrait;
        public Sprite soundOn, soundOff, gear;
        [Tooltip("Vidéo jouée au début d'une nouvelle partie (Video/super_yao_intro.mp4).")]
        public UnityEngine.Video.VideoClip intro;

        static readonly Color Ink = UiKit.Hex("#3A2213"), Gold = UiKit.Hex("#FFD23F"), Red = UiKit.Hex("#E8343A");
        static readonly Color Green = UiKit.Hex("#2EAA3F"), Wood = UiKit.Hex("#8A5428");

        Image soundIcon;
        Text settingsSound;
        RectTransform settings;

        void Start()
        {
            UiKit.Font = font;
            var catalog = StageCatalog.Load();
            int next = Mathf.Min(GameProgress.Furthest, catalog != null ? catalog.stages.Count - 1 : 0);
            var stage = catalog != null && catalog.stages.Count > 0 ? catalog.stages[next] : null;

            var (full, s) = UiKit.Screen(transform, "Accueil");
            UiKit.Cover(full, background).transform.SetAsFirstSibling();

            UiKit.Picture(s, hero, 25, 335, 460, 420);
            UiKit.Picture(s, logo, 530, 18, 610, 262);

            // Le héros (Yao, seul héros jouable) : médaillon doré et son nom, comme le héros choisi sur le téléphone.
            var ring = UiKit.Box(s, "Medaillon", 840 - 82, 265, 164, 164, UiKit.Hex("#FFE7A8"), Gold, 7, 82);
            UiKit.Picture(ring, portrait, 7, 7, 150, 150);
            var pill = UiKit.Box(s, "Nom", 840 - 63, 265 + 155, 126, 42, Gold, UiKit.Hex("#B8860B"), 3, 21);
            UiKit.Label(pill, "Yao", 26, Ink);

            // Panneau en bois du niveau, puis sa phrase d'accroche.
            int number = next + 1;
            var sign = UiKit.Box(s, "Niveau", 595, 480, 480, 62, Wood, UiKit.Hex("#4A2A16"), 3, 10);
            UiKit.Label(sign, $"NIVEAU {number} : {(stage != null ? stage.name.ToUpperInvariant() : "")}", 30, UiKit.Hex("#FFD86B"), TextAnchor.MiddleCenter, Ink);
            var panel = UiKit.Box(s, "Accroche", 510, 548, 650, 106, UiKit.Hex("#142030", 0.82f), new Color(1f, 1f, 1f, 0.25f), 2, 14);
            var tagline = UiKit.Label(panel, stage != null ? stage.tagline : "", 25, Color.white);
            tagline.rectTransform.offsetMin = new Vector2(30, 0);
            tagline.rectTransform.offsetMax = new Vector2(-30, 0);

            // Boutons : Continuer + Nouvelle partie, ou Jouer.
            if (GameProgress.HasSave)
            {
                var cont = UiKit.Button(s, "Continuer", 460, 668, 380, 94, Green, Ink, 4, 9, 47, () => GameProgress.Load(GameProgress.MapScene));
                UiKit.Label((RectTransform)cont.transform.Find("Face"), "Continuer", 40, Color.white, TextAnchor.MiddleCenter, new Color(0, 0, 0, 0.45f));
                var info = UiKit.Box(s, "Info", 490, 768, 320, 60, UiKit.Hex("#142030", 0.72f), Color.clear, 0, 10);
                UiKit.Label(info, $"Stage {number} · {GameProgress.Lives} vie{(GameProgress.Lives > 1 ? "s" : "")} · {GameProgress.Coins} pièces", 19, Color.white);
                var fresh = UiKit.Button(s, "Nouvelle", 870, 668, 352, 94, Red, Ink, 4, 9, 47, NewGame);
                UiKit.Label((RectTransform)fresh.transform.Find("Face"), "Nouvelle partie", 32, Color.white, TextAnchor.MiddleCenter, new Color(0, 0, 0, 0.45f));
            }
            else
            {
                var play = UiKit.Button(s, "Jouer", 646, 668, 380, 94, Green, Ink, 4, 9, 47, NewGame);
                UiKit.Label((RectTransform)play.transform.Find("Face"), "Jouer", 40, Color.white, TextAnchor.MiddleCenter, new Color(0, 0, 0, 0.45f));
            }

            // Son et réglages, en haut à droite.
            var sound = UiKit.Button(s, "Son", 1458, 25, 82, 78, UiKit.Hex("#3A2213"), Gold, 4, 4, 16, ToggleSound);
            soundIcon = UiKit.Picture((RectTransform)sound.transform.Find("Face"), soundOn, 14, 12, 46, 46);
            var gearBtn = UiKit.Button(s, "Reglages", 1563, 25, 82, 78, UiKit.Hex("#3A2213"), Gold, 4, 4, 16, () => settings.gameObject.SetActive(true));
            UiKit.Picture((RectTransform)gearBtn.transform.Find("Face"), gear, 14, 12, 46, 46);

            // Rappel des commandes : boutons à l'écran sur téléphone, clavier sur ordinateur.
            var hint = UiKit.Box(s, "Commandes", 200, 843, 1355, 64, UiKit.Hex("#142030", 0.8f), new Color(1f, 1f, 1f, 0.3f), 2, 32);
            UiKit.Label(hint, Application.isMobilePlatform
                ? "Boutons de gauche : avancer et s'accroupir · Gros bouton : sauter · Double flèche : courir · Boule de feu : tirer"
                : "Flèches pour avancer · Bas pour s'accroupir · Espace pour sauter · Maj pour courir · X pour lancer le feu", 19, Color.white);

            BuildSettings(full);
            Refresh();

            // Compte en ligne (pseudo, Google / Facebook / Apple) et classement, en haut à gauche. Si la partie du compte
            // remplace celle du téléphone (plus avancée), la page se reconstruit.
            gameObject.AddComponent<AccountScreen>().Build(full, s);
            OnlineAccount.Instance.ProgressReplaced += Reload;
        }

        void OnDestroy() { if (OnlineAccount.HasInstance) OnlineAccount.Instance.ProgressReplaced -= Reload; }

        void Reload() => GameProgress.Load(GameProgress.TitleScene);

        void BuildSettings(RectTransform full)
        {
            // Fenêtre des réglages (téléphone : son, effacer la partie, fermer), sur un voile sombre.
            settings = UiKit.Stretch(UiKit.Rect(full, "Reglages"));
            var veil = settings.gameObject.AddComponent<Image>();
            veil.color = new Color(0f, 0f, 0f, 0.45f);
            var box = UiKit.Box(settings, "Fenetre", 0, 0, 560, 400, UiKit.Hex("#FFF6E3"), Ink, 3, 18);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            var title = UiKit.At(UiKit.Rect(box, "Titre"), 0, 18, 560, 50);
            UiKit.Label(title, "Réglages", 38, Ink);
            var brown = UiKit.Hex("#5D4037");
            var b1 = UiKit.Button(box, "Son", 40, 90, 480, 70, brown, Ink, 3, 3, 30, ToggleSound);
            settingsSound = UiKit.Label((RectTransform)b1.transform.Find("Face"), "", 26, Color.white);
            var b2 = UiKit.Button(box, "Effacer", 40, 175, 480, 70, Red, Ink, 3, 3, 30, ClearSave);
            UiKit.Label((RectTransform)b2.transform.Find("Face"), "Effacer la partie enregistrée", 26, Color.white);
            b2.interactable = GameProgress.HasSave;
            var b3 = UiKit.Button(box, "Fermer", 40, 260, 480, 70, Green, Ink, 3, 3, 30, () => settings.gameObject.SetActive(false));
            UiKit.Label((RectTransform)b3.transform.Find("Face"), "Fermer", 26, Color.white);
            settings.gameObject.SetActive(false);
        }

        void NewGame()
        {
            GameProgress.NewGame();
            if (intro != null) PlayIntro();
            else GameProgress.Load(GameProgress.MapScene);
        }

        /// <summary>Bande-annonce au début d'une nouvelle partie (comme le téléphone, StoryVideo.tsx) : plein écran, sans
        /// déformation, se ferme à la fin ou avec « Passer », puis la carte.</summary>
        void PlayIntro()
        {
            var (full, _) = UiKit.Screen(transform, "Video", 200);
            var black = UiKit.Stretch(UiKit.Rect(full, "Noir"));
            black.gameObject.AddComponent<Image>().color = Color.black; // bloque aussi les touches vers l'accueil
            var screen = UiKit.Rect(black, "Image");
            screen.anchorMin = screen.anchorMax = screen.pivot = new Vector2(0.5f, 0.5f);
            var raw = screen.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var fit = screen.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = intro.height > 0 ? (float)intro.width / intro.height : 16f / 9f;

            var texture = new RenderTexture((int)Mathf.Max(16, intro.width), (int)Mathf.Max(16, intro.height), 0);
            raw.texture = texture;
            var player = full.gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
            player.playOnAwake = false;
            player.clip = intro;
            player.isLooping = false;
            player.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
            player.targetTexture = texture;
            player.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.Direct;
            player.SetDirectAudioMute(0, GameProgress.Muted);
            player.loopPointReached += _ => EndIntro();
            player.errorReceived += (_, message) => { Debug.LogWarning("Super Yao : vidéo illisible (" + message + ")."); EndIntro(); };
            player.Play();

            // « Passer », en haut à droite.
            var skip = UiKit.Button(full, "Passer", 0, 0, 190, 64, new Color(0f, 0f, 0f, 0.55f), new Color(1f, 1f, 1f, 0.6f), 3, 3, 30, EndIntro);
            var rt = (RectTransform)skip.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-30f, -24f);
            UiKit.Label((RectTransform)skip.transform.Find("Face"), "Passer", 30, Color.white);
        }

        bool introDone;

        void EndIntro()
        {
            if (introDone) return;
            introDone = true;
            GameProgress.Load(GameProgress.MapScene);
        }

        void ClearSave()
        {
            GameProgress.ClearSave();
            // On reconstruit la page : plus de « Continuer ».
            GameProgress.Load(GameProgress.TitleScene);
        }

        void ToggleSound()
        {
            GameProgress.Muted = !GameProgress.Muted;
            Refresh();
        }

        void Refresh()
        {
            soundIcon.sprite = GameProgress.Muted ? soundOff : soundOn;
            settingsSound.text = GameProgress.Muted ? "Son coupé" : "Son activé";
        }
    }
}
