using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace SuperYao
{
    /// <summary>
    /// Outils communs aux écrans (accueil, carte) : comme sur le téléphone, tout est placé dans un repère de
    /// 1672 × 941 (la maquette), mis à l'échelle pour tenir dans l'écran et centré ; le fond, lui, couvre tout l'écran.
    /// Boîtes à coins arrondis et bordure, textes en Lilita One, images, boutons « en relief » (bord bas épais).
    /// </summary>
    public static class UiKit
    {
        public const float MockW = 1672f, MockH = 941f;
        public static Font Font;

        // Rectangle arrondi blanc découpable en 9, teinté par la couleur de l'image.
        const int TexRadius = 64;
        static Sprite round;
        public static Sprite Round => round != null ? round : round = MakeRound();

        /// <summary>Crée un écran : toile mise à l'échelle (le repère tient dans l'écran) et la « scène » centrée.</summary>
        public static (RectTransform full, RectTransform stage) Screen(Transform parent, string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder; // écrans superposés : pause au-dessus du jeu, marché au-dessus de la carte
            canvas.pixelPerfect = true;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(MockW, MockH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // échelle = min(largeur, hauteur)
            var full = (RectTransform)go.transform;
            var stage = Rect(full, "Maquette");
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(MockW, MockH);
            EnsureEventSystem();
            return (full, stage);
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
#if ENABLE_INPUT_SYSTEM
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Place un rectangle en (x, y) depuis le coin haut gauche du parent, comme `at()` sur le téléphone.</summary>
        public static RectTransform At(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>Image arrondie de couleur unie (rayon des coins en unités du repère).</summary>
        public static Image Rounded(RectTransform rt, Color color, float radius)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Round;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = (TexRadius + 2f) / Mathf.Max(0.5f, radius);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Boîte à coins arrondis, avec bordure (fond de la couleur de la bordure, puis intérieur).</summary>
        public static RectTransform Box(RectTransform parent, string name, float x, float y, float w, float h,
            Color fill, Color border, float borderWidth, float radius)
        {
            var outer = At(Rect(parent, name), x, y, w, h);
            Rounded(outer, border, radius);
            var inner = Stretch(Rect(outer, "Fond"), borderWidth);
            Rounded(inner, fill, Mathf.Max(0.5f, radius - borderWidth));
            // Le fond n'est pas un élément de la boîte : sans cela, une boîte rangée en ligne (compteurs, « À RAMASSER »)
            // le plaçait comme une case noire au début de la rangée.
            inner.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return outer;
        }

        public static Text Label(RectTransform parent, string text, float size, Color color, TextAnchor align = TextAnchor.MiddleCenter,
            Color? shadow = null, float shadowY = 2f)
        {
            var t = Rect(parent, "Texte").gameObject.AddComponent<Text>();
            Stretch(t.rectTransform);
            t.font = Font;
            t.text = text;
            t.fontSize = Mathf.RoundToInt(size);
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (shadow.HasValue)
            {
                var s = t.gameObject.AddComponent<Shadow>();
                s.effectColor = shadow.Value;
                s.effectDistance = new Vector2(0f, -shadowY);
            }
            return t;
        }

        public static Image Picture(RectTransform parent, Sprite sprite, float x, float y, float w, float h, bool preserveAspect = true)
        {
            var img = At(Rect(parent, sprite != null ? sprite.name : "Image"), x, y, w, h).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Image qui couvre tout son parent sans être déformée (débordement rogné par le parent).</summary>
        public static Image Cover(RectTransform parent, Sprite sprite)
        {
            var rt = Rect(parent, "Fond");
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            var fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite != null ? sprite.rect.width / sprite.rect.height : 16f / 9f;
            return img;
        }

        /// <summary>Bouton en relief, comme sur le téléphone : bordure sombre, bord bas plus épais qui s'aplatit à l'appui.</summary>
        public static Button Button(RectTransform parent, string name, float x, float y, float w, float h, Color fill, Color border,
            float borderWidth, float bottom, float radius, UnityEngine.Events.UnityAction onClick)
        {
            var outer = At(Rect(parent, name), x, y, w, h);
            var hit = Rounded(outer, border, radius);
            hit.raycastTarget = true;
            var face = Rect(outer, "Face");
            Stretch(face);
            face.offsetMin = new Vector2(borderWidth, bottom);
            face.offsetMax = new Vector2(-borderWidth, -borderWidth);
            var faceImg = Rounded(face, fill, Mathf.Max(0.5f, radius - borderWidth));
            var button = outer.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = faceImg;
            var colors = button.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            outer.gameObject.AddComponent<PressedBottom>().Init(face, borderWidth, bottom);
            return button;
        }

        /// <summary>Le bord bas du bouton s'aplatit pendant l'appui (le bouton s'enfonce).</summary>
        public class PressedBottom : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            RectTransform face;
            float side, bottom;
            public void Init(RectTransform f, float s, float b) { face = f; side = s; bottom = b; }
            public void OnPointerDown(PointerEventData e) => face.offsetMin = new Vector2(side, side);
            public void OnPointerUp(PointerEventData e) => face.offsetMin = new Vector2(side, bottom);
            public void OnPointerExit(PointerEventData e) => face.offsetMin = new Vector2(side, bottom);
        }

        public static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = alpha;
            return c;
        }

        static Sprite MakeRound()
        {
            const int r = TexRadius, n = 2 * r + 8;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - r;
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }
            tex.SetPixels(px);
            tex.Apply(true);
            float b = r + 2;
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }
    }
}
