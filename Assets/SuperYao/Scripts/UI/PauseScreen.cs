using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SuperYao
{
    /// <summary>
    /// Écran de pause, d'après la maquette de la version téléphone (PauseScreen.tsx, repère 1672 × 941) : cadre en bois
    /// et parchemin cloués, panneau « PAUSE », le niveau, Yao qui se repose près d'un muret de pierres, des feuillages,
    /// et trois gros boutons : Reprendre, Recommencer, Menu principal. S'ouvre avec le bouton Pause du panneau,
    /// la touche Échap ou le bouton Retour d'Android.
    /// </summary>
    public class PauseScreen : MonoBehaviour
    {
        public Font font;
        public Sprite hero, leaf, stoneGrey, stoneEarth, playIcon, restartIcon, homeIcon;
        public string levelName = "Grand-Bassam";

        RectTransform root;

        void Start()
        {
            UiKit.Font = font;
            var (full, s) = UiKit.Screen(transform, "Pause", 120);
            root = full;
            var veil = UiKit.Stretch(UiKit.Rect(full, "Voile"));
            veil.SetAsFirstSibling();
            veil.gameObject.AddComponent<Image>().color = UiKit.Hex("#0A1423", 0.35f);

            // Feuillages derrière le panneau.
            Leaf(s, 395, 150, 160, 140, -30f, false);
            Leaf(s, 1120, 150, 160, 140, -30f, true);
            Leaf(s, 365, 640, 140, 130, 0f, false);
            Leaf(s, 1185, 630, 140, 130, 0f, true);

            // Cadre en bois, parchemin et clous.
            var frame = UiKit.Box(s, "Cadre", 420, 235, 835, 535, UiKit.Hex("#B5702F"), UiKit.Hex("#7A4519"), 14, 34);
            var parchment = UiKit.Stretch(UiKit.Rect(frame, "Parchemin"), 20);
            UiKit.Rounded(parchment, UiKit.Hex("#E2C99A"), 22);
            UiKit.Rounded(UiKit.Stretch(UiKit.Rect(parchment, "Papier"), 3), UiKit.Hex("#FBF1DC"), 19);
            foreach (var (x, y) in new[] { (445f, 262f), (1200f, 262f), (445f, 718f), (1200f, 718f) })
                UiKit.Box(s, "Clou", x, y, 22, 22, UiKit.Hex("#9AA1A8"), UiKit.Hex("#4E545A"), 3, 11);

            // Panneau « PAUSE » et le niveau.
            var sign = UiKit.Box(s, "Titre", 595, 140, 485, 130, UiKit.Hex("#A4612A"), UiKit.Hex("#5A3012"), 6, 26);
            var title = UiKit.Label(sign, "PAUSE", 104, UiKit.Hex("#FFC629"), TextAnchor.MiddleCenter, UiKit.Hex("#5A3012"), 6f);
            title.rectTransform.offsetMin = new Vector2(0, 6);
            var kicker = UiKit.At(UiKit.Rect(s, "Niveau"), 470, 288, 735, 36);
            UiKit.Label(kicker, $"NIVEAU {(StageState.Instance != null ? StageState.Instance.LevelNumber : 1)} · {(StageState.Instance != null && StageState.Instance.LevelName != "" ? StageState.Instance.LevelName : levelName).ToUpperInvariant()}", 24, UiKit.Hex("#9A5A22"));

            // Yao fait une pause près d'un muret de pierres (deux rangées décalées, comme une vraie maçonnerie).
            var wall = UiKit.At(UiKit.Rect(s, "Muret"), 474, 610, 212, 112);
            wall.gameObject.AddComponent<RectMask2D>();
            for (int r = 0; r < 2; r++)
                for (int b = 0; b < 5; b++)
                {
                    float x = b * 64 - (r == 1 ? 32 : 0);
                    if (x + 64 <= 0 || x >= 212) continue;
                    UiKit.Picture(wall, r == 0 && b % 2 == 1 ? stoneEarth : stoneGrey, x, r * 58, 64, 62, false);
                }
            UiKit.Picture(s, hero, 440, 337, 280, 280);
            Leaf(s, 590, 680, 120, 90, 0f, false);

            // Les trois boutons.
            Button(s, "Reprendre", playIcon, 342, "#2EAA3F", "#14602A", Color.white, Resume);
            Button(s, "Recommencer", restartIcon, 470, "#FFC629", "#9A6A00", UiKit.Hex("#3A2208"), Restart);
            Button(s, "Menu principal", homeIcon, 598, "#E8343A", "#7E1717", Color.white, Quit);

            root.gameObject.SetActive(false);
        }

        void Leaf(RectTransform s, float x, float y, float w, float h, float angle, bool mirror)
        {
            var img = UiKit.Picture(s, leaf, x, y, w, h);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            img.rectTransform.anchoredPosition = new Vector2(x + w / 2f, -y - h / 2f);
            img.rectTransform.localEulerAngles = new Vector3(0f, 0f, mirror ? -angle : angle);
            img.rectTransform.localScale = new Vector3(mirror ? -1f : 1f, 1f, 1f);
        }

        void Button(RectTransform s, string label, Sprite icon, float y, string fill, string edge, Color text, UnityEngine.Events.UnityAction onClick)
        {
            var b = UiKit.Button(s, label, 725, y, 455, 104, UiKit.Hex(fill), UiKit.Hex(edge), 4, 11, 30, onClick);
            var face = (RectTransform)b.transform.Find("Face");
            var i = UiKit.Picture(face, icon, 40, 18, 56, 56);
            i.color = text;
            var t = UiKit.Label(face, label, 44, text, TextAnchor.MiddleCenter, text == Color.white ? new Color(0, 0, 0, 0.45f) : (Color?)null);
            t.rectTransform.offsetMin = new Vector2(100, 0);
            t.rectTransform.offsetMax = new Vector2(-30, 0);
        }

        void Update()
        {
            var state = StageState.Instance;
            if (state == null || root == null) return;
            // Échap (ordinateur) ou bouton Retour (Android) : pause / reprise.
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) state.TogglePause();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) state.TogglePause();
#endif
            if (root.gameObject.activeSelf != state.Paused) root.gameObject.SetActive(state.Paused);
        }

        void Resume()
        {
            if (StageState.Instance != null && StageState.Instance.Paused) StageState.Instance.TogglePause();
        }

        void Restart()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        void Quit()
        {
            Time.timeScale = 1f;
            GameProgress.Load(GameProgress.TitleScene);
        }
    }
}
