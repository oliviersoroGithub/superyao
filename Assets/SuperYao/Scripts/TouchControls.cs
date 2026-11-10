using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SuperYao
{
    /// <summary>
    /// Boutons tactiles de Super Yao (téléphone Android et iPhone), dans la zone sûre (hors encoche et coins arrondis) :
    /// à gauche ◀ ▼ ▶ (marcher, s'accroupir), à droite « sauter » (le plus gros, sous le pouce) et, quand Yao a le
    /// pouvoir du feu, « feu ». Pas de bouton « courir » (retiré à la demande) : en gardant ◀ ou ▶ appuyé, Yao prend
    /// de l'élan et passe tout seul de la marche à la course. Tous les doigts posés sont lus à chaque image, et l'on
    /// peut glisser le pouce d'un bouton à l'autre. Dans l'éditeur, la souris joue le rôle d'un doigt.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        /// <summary>-1 (gauche), 0 ou 1 (droite), selon les boutons enfoncés.</summary>
        public static float Horizontal { get; private set; }
        public static bool Down { get; private set; }
        public static bool Jump { get; private set; }
        /// <summary>Toujours faux : il n'y a plus de bouton « courir » (gardé pour HeroInput).</summary>
        public static bool Run { get; private set; }
        public static bool Fire { get; private set; }

        [Tooltip("Icône du bouton « feu » (la boule de feu) ; il n'apparaît que lorsque Yao a le pouvoir du feu.")]
        public Sprite fireIcon;

        [Tooltip("Diamètre des boutons ◀ ▶, en points de l'écran de référence (1920 x 1080).")]
        public float buttonSize = 215f;
        [Tooltip("Diamètre du bouton de saut (rouge).")]
        public float jumpSize = 270f;
        [Tooltip("Diamètre du bouton « feu ».")]
        public float actionSize = 170f;
        [Tooltip("Marge entre les boutons et le bord de la zone sûre.")]
        public float margin = 50f;

        RectTransform safeArea;
        Pad left, down, right, jump, fire;
        HeroFire heroFire;
        Rect lastSafeArea;

        struct Pad
        {
            public RectTransform rect;
            public Image image;
        }

        void Awake()
        {
            var canvasGo = new GameObject("Boutons tactiles", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            safeArea = new GameObject("Zone sûre", typeof(RectTransform)).GetComponent<RectTransform>();
            safeArea.SetParent(canvasGo.transform, false);
            ApplySafeArea();

            var circle = Disc(new Color32(40, 22, 10, 200));
            var red = Disc(new Color32(0xE8, 0x34, 0x3A, 230)); // bouton de saut, rouge comme sur le téléphone
            var arrow = Triangle();
            float s = buttonSize, a = actionSize, y = margin + a / 2f;
            // À gauche : ◀ et ▶ gros et presque collés, ▼ plus petit en dessous, au milieu des deux (les ronds ne se
            // touchent pas : centres de ◀ ▶ écartés de 1,08 bouton, ▼ de 0,55 bouton, abaissé de 0,6 bouton).
            float d = s * 0.55f, downCy = margin + d / 2f, lrCy = downCy + s * 0.6f;
            float leftCx = margin + s / 2f, rightCx = leftCx + s * 1.08f;
            left = MakeButton("Gauche", circle, arrow, 180f, new Vector2(0f, 0f), new Vector2(leftCx, lrCy), s);
            right = MakeButton("Droite", circle, arrow, 0f, new Vector2(0f, 0f), new Vector2(rightCx, lrCy), s);
            down = MakeButton("Bas", circle, arrow, -90f, new Vector2(0f, 0f), new Vector2((leftCx + rightCx) / 2f, downCy), d);
            // À droite : « sauter » (rouge, le plus gros) dans le coin, « feu » à sa gauche.
            jump = MakeButton("Sauter", red, arrow, 90f, new Vector2(1f, 0f), new Vector2(-margin - jumpSize / 2f, margin + jumpSize / 2f), jumpSize);
            s = a;
            // Feu : la boule de feu pour icône ; caché tant que Yao n'a pas le pouvoir.
            fire = MakeButton("Feu", circle, fireIcon != null ? fireIcon : arrow, 0f, new Vector2(1f, 0f),
                new Vector2(-margin - jumpSize - s * 0.65f, y), s);
            var icon = fire.rect.GetChild(0).GetComponent<RectTransform>();
            icon.sizeDelta = new Vector2(s * 0.75f, s * 0.42f);
            icon.GetComponent<Image>().preserveAspect = true;
            fire.rect.gameObject.SetActive(false);
        }

        // Le projet ne recharge pas le code à chaque Play : on relâche les boutons à l'arrêt, sinon le héros repartirait seul.
        void OnDisable()
        {
            Horizontal = 0f;
            Down = Jump = Run = Fire = false;
        }

        void Update()
        {
            if (Screen.safeArea != lastSafeArea) ApplySafeArea();
            bool l = false, r = false, d = false, j = false, f = false;
            if (heroFire == null) heroFire = FindAnyObjectByType<HeroFire>();
            bool hasFire = heroFire != null && heroFire.HasFire;
            if (fire.rect != null && fire.rect.gameObject.activeSelf != hasFire) fire.rect.gameObject.SetActive(hasFire);
            foreach (var p in Pointers())
            {
                f |= hasFire && Contains(fire, p);
                l |= Contains(left, p);
                r |= Contains(right, p);
                d |= Contains(down, p);
                j |= Contains(jump, p);
            }
            Horizontal = (r ? 1f : 0f) - (l ? 1f : 0f);
            Down = d;
            Jump = j;
            Run = false;
            Fire = f;
            Tint(fire, f);
            Tint(left, l);
            Tint(right, r);
            Tint(down, d);
            Tint(jump, j, 0.75f); // le rouge reste bien visible au repos
        }

        static bool Contains(Pad b, Vector2 p) => RectTransformUtility.RectangleContainsScreenPoint(b.rect, p);
        // Garde-fou : si Unity recompile les scripts pendant le jeu, les boutons (non sauvegardés) sont perdus.
        static void Tint(Pad b, bool pressed, float idle = 0.45f) { if (b.image != null) b.image.color = new Color(1f, 1f, 1f, pressed ? 0.95f : idle); }
        /// <summary>Positions de tous les doigts posés (et de la souris enfoncée, pour l'éditeur).</summary>
        static System.Collections.Generic.IEnumerable<Vector2> Pointers()
        {
#if ENABLE_INPUT_SYSTEM
            var screen = Touchscreen.current;
            if (screen != null)
                foreach (var t in screen.touches)
                    if (t.press.isPressed) yield return t.position.ReadValue();
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) yield return mouse.position.ReadValue();
#else
            for (int i = 0; i < Input.touchCount; i++) yield return Input.GetTouch(i).position;
            if (Input.GetMouseButton(0)) yield return Input.mousePosition;
#endif
        }

        void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            var size = new Vector2(Screen.width, Screen.height);
            safeArea.anchorMin = lastSafeArea.position / size;
            safeArea.anchorMax = (lastSafeArea.position + lastSafeArea.size) / size;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        Pad MakeButton(string name, Sprite circle, Sprite arrow, float arrowAngle, Vector2 anchor, Vector2 position, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(safeArea, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = position;
            var background = go.GetComponent<Image>();
            background.sprite = circle;
            background.raycastTarget = false;

            var icon = new GameObject("Flèche", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            icon.SetParent(rt, false);
            icon.sizeDelta = new Vector2(size * 0.42f, size * 0.42f);
            icon.localEulerAngles = new Vector3(0f, 0f, arrowAngle);
            var img = icon.GetComponent<Image>();
            img.sprite = arrow;
            img.raycastTarget = false;
            return new Pad { rect = rt, image = background };
        }

        /// <summary>Disque de la couleur donnée, bordé de blanc, dessiné sans aucune image.</summary>
        static Sprite Disc(Color32 fill) => Draw((u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            var c = d > 0.44f ? new Color32(255, 255, 255, 255) : fill;
            return (c, Mathf.Clamp01((0.5f - d) * 128f));
        });

        /// <summary>Flèche blanche pointant vers la droite (tournée selon le bouton).</summary>
        static Sprite Triangle() => Draw((u, v) =>
            (new Color32(255, 255, 255, 255), Mathf.Clamp01(Mathf.Min(u + 0.32f, 0.36f - u - Mathf.Abs(v) * 1.2f) * 128f)));

        static Sprite Draw(System.Func<float, float, (Color32 color, float alpha)> shade)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var (c, a) = shade((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f);
                    c.a = (byte)(c.a * a);
                    px[y * n + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
