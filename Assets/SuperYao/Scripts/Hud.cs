using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace SuperYao
{
    /// <summary>
    /// Panneau d'affichage, comme celui de la version téléphone (Hud.tsx) : une planche de bois sombre cerclée d'or,
    /// bordée en haut et en bas d'une bande de kente, avec le médaillon de Yao et ses vies, les pièces, les bananes,
    /// les diamants, le niveau et le score, puis le temps (rouge sous 60). À droite, les boutons « Bruits / Muet » et
    /// « Pause », dans le même style. Police Lilita One. Construit au lancement, dans la zone sûre de l'écran.
    /// Tailles : celles du téléphone (en points) × 2,4, sur un écran de référence de 1920 × 1080.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public Font font;
        public Sprite portrait, coin, banana, diamond;

        const float K = 2.4f; // points du téléphone -> pixels de l'écran de référence
        // Bois sombre presque opaque : à 90 % (comme le téléphone), le ciel bleu transparaissait et le rendait bleu-gris.
        static readonly Color Wood = new Color32(54, 29, 13, 248);
        static readonly Color GoldBorder = new Color32(0xE0, 0xA8, 0x2E, 255);
        static readonly Color Gold = new Color32(0xFF, 0xD2, 0x3F, 255);
        static readonly Color Hurry = new Color32(0xFF, 0x7A, 0x6E, 255);
        static readonly Color32[] Kente =
        {
            new Color32(0xD7, 0x37, 0x2F, 255), new Color32(0xF7, 0xC5, 0x31, 255), new Color32(0x2E, 0x9E, 0x4B, 255),
            new Color32(0x1A, 0x1A, 0x1A, 255), new Color32(0xF7, 0xC5, 0x31, 255), new Color32(0xF0, 0x8A, 0x24, 255),
        };

        Text lives, coins, bananas, diamonds, level, score, time, sound, pauseLabel;
        RectTransform safeArea;
        Rect lastSafeArea;
        Sprite rounded, roundedFill;

        void Awake()
        {
            rounded = RoundedSprite(true);
            roundedFill = RoundedSprite(false);

            var canvasGo = new GameObject("Panneau", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            canvas.pixelPerfect = true; // panneau calé sur les pixels de l'écran : traits nets
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            safeArea = Rect(canvasGo.transform, "Zone sûre");
            ApplySafeArea();

            float pad = 6f * K, margin = 10f * K;
            float soundW = 64f * K, buttonH = 40f * K, gap = 10f * K;

            // La planche : bois sombre cerclé d'or, bandes de kente en haut et en bas (coupées par les coins arrondis).
            var panel = Rect(safeArea, "Planche");
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.offsetMin = new Vector2(margin, -pad - 46f * K);
            panel.offsetMax = new Vector2(-(margin + soundW + gap + buttonH + gap), -pad);
            var frame = panel.gameObject.AddComponent<Image>();
            frame.sprite = rounded;
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = SS; // coins de 34 px
            frame.raycastTarget = false;
            var shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -3f * K);

            var inner = Rect(panel, "Intérieur");
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(2.5f * K, 2.5f * K);
            inner.offsetMax = new Vector2(-2.5f * K, -2.5f * K);
            var maskImage = inner.gameObject.AddComponent<Image>();
            maskImage.sprite = roundedFill;
            maskImage.type = Image.Type.Sliced;
            maskImage.pixelsPerUnitMultiplier = SS * 34f / 28f; // coins de 28 px : épousent l'intérieur de l'anneau
            maskImage.color = Wood;
            maskImage.raycastTarget = false;
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var column = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            KenteBand(inner);
            var row = Rect(inner, "Rangée");
            row.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset((int)(10f * K), (int)(10f * K), (int)(3f * K), (int)(3f * K));
            h.spacing = 8f * K;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            KenteBand(inner);

            lives = Group(row, portrait, 30f, "×3");
            coins = Group(row, coin, 22f, "×00");
            bananas = Group(row, banana, 22f, "×0");
            diamonds = Group(row, diamond, 24f, "×0");
            (level, score) = Stacked(row, "NIVEAU 1", "SCORE 000000");
            (_, time) = Stacked(row, "TEMPS", "300");

            // Boutons ronds à droite de la planche.
            var soundBtn = Button(safeArea, "Bruits", new Vector2(-(margin + buttonH + gap + soundW / 2f), -pad - buttonH / 2f), new Vector2(soundW, buttonH), ToggleSound);
            sound = Label(soundBtn, "Bruits", 13f, Color.white, TextAnchor.MiddleCenter);
            Stretch(sound.rectTransform);
            var pauseBtn = Button(safeArea, "Pause", new Vector2(-(margin + buttonH / 2f), -pad - buttonH / 2f), new Vector2(buttonH, buttonH), TogglePause);
            for (int i = 0; i < 2; i++)
            {
                var bar = Rect(pauseBtn, "Barre");
                bar.sizeDelta = new Vector2(5f * K, 15f * K);
                bar.anchoredPosition = new Vector2((i == 0 ? -5f : 5f) * K, 0f); // deux barres de 5 points, 5 points d'écart
                var img = bar.gameObject.AddComponent<Image>();
                img.sprite = roundedFill;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 24f; // coins de 6 px : barres arrondies
                img.raycastTarget = false;
            }

            // « PAUSE » au milieu de l'écran pendant la pause.
            pauseLabel = Label(canvasGo.transform as RectTransform, "PAUSE", 48f, Color.white, TextAnchor.MiddleCenter);
            Stretch(pauseLabel.rectTransform);
            var outline = pauseLabel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(0x3A, 0x22, 0x13, 255);
            outline.effectDistance = new Vector2(3f, -3f);
            pauseLabel.enabled = false;
            endLabel = Label(canvasGo.transform as RectTransform, "", 48f, Gold, TextAnchor.MiddleCenter);
            Stretch(endLabel.rectTransform);
            var endOutline = endLabel.gameObject.AddComponent<Outline>();
            endOutline.effectColor = new Color32(0x3A, 0x22, 0x13, 255);
            endOutline.effectDistance = new Vector2(4f, -4f);
            endLabel.enabled = false;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
#if ENABLE_INPUT_SYSTEM
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
            }
        }

        void Update()
        {
            if (Screen.safeArea != lastSafeArea) ApplySafeArea();
            var s = StageState.Instance;
            if (s == null) return;
            lives.text = $"×{Mathf.Max(0, s.Lives)}";
            coins.text = $"×{s.Coins:00}";
            bananas.text = $"×{s.Bananas}";
            diamonds.text = $"×{s.Diamonds}";
            level.text = $"NIVEAU {s.LevelNumber}";
            score.text = $"SCORE {Mathf.Max(0, s.Score):000000}";
            time.text = $"{Mathf.Max(0, s.TimeLeft):000}";
            // Sous 60, le temps passe au rouge et clignote : la fin du temps fait perdre une vie.
            bool hurry = s.TimeLeft <= 60 && !s.Paused && !s.Won;
            time.color = hurry ? (Mathf.FloorToInt(Time.unscaledTime * 3f) % 2 == 0 ? Hurry : Color.white) : Color.white;
            sound.text = s.Muted ? "Muet" : "Bruits";
            pauseLabel.enabled = false; // remplacé par l'écran de pause (PauseScreen)
            // Fin du stage : félicitations ou fin de partie, au milieu de l'écran.
            endLabel.enabled = s.Won || s.GameOver;
            if (s.Won) endLabel.text = $"BRAVO !\n<size={Mathf.RoundToInt(20f * K)}>Grand-Bassam terminé</size>";
            else if (s.GameOver) endLabel.text = "FIN DE PARTIE";
        }

        Text endLabel;

        void ToggleSound() => StageState.Instance?.ToggleMute();
        void TogglePause() => StageState.Instance?.TogglePause();

        // ------------------------------------------------------------------ construction

        Text Group(RectTransform row, Sprite icon, float iconSize, string value)
        {
            var g = Rect(row, "Groupe");
            g.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var layout = g.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f * K;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var iconRt = Rect(g, "Icône");
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var le = iconRt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = iconSize * K;
            return Label(g, value, 17f, Color.white, TextAnchor.MiddleLeft);
        }

        (Text label, Text value) Stacked(RectTransform row, string label, string value)
        {
            var g = Rect(row, "Groupe");
            g.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var layout = g.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = -2f * K;
            var l = Label(g, label, 11f, Gold, TextAnchor.MiddleCenter);
            var v = Label(g, value, 16f, Color.white, TextAnchor.MiddleCenter);
            return (l, v);
        }

        Text Label(RectTransform parent, string text, float points, Color color, TextAnchor anchor)
        {
            var t = Rect(parent, text).gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = Mathf.RoundToInt(points * K);
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        RectTransform Button(RectTransform parent, string name, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var rt = Rect(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = (34f + 2f) * SS / (size.y / 2f); // coins arrondis = demi-hauteur : pilule ou rond
            // Fond de bois sous l'anneau doré.
            var fill = Rect(rt, "Bois");
            Stretch(fill);
            fill.offsetMin = new Vector2(2f * K, 2f * K);
            fill.offsetMax = new Vector2(-2f * K, -2f * K);
            var wood = fill.gameObject.AddComponent<Image>();
            wood.sprite = roundedFill;
            wood.type = Image.Type.Sliced;
            wood.pixelsPerUnitMultiplier = (34f + 2f) * SS / (size.y / 2f - 2f * K);
            wood.color = Wood;
            wood.raycastTarget = false;
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            return rt;
        }

        void KenteBand(RectTransform parent)
        {
            var band = Rect(parent, "Kente");
            var le = band.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = 3f * K;
            kenteTexture ??= KenteTexture();
            var img = band.gameObject.AddComponent<RawImage>();
            img.texture = kenteTexture;
            img.raycastTarget = false;
        }

        Texture2D kenteTexture;

        static RectTransform Rect(Transform parent, string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            var size = new Vector2(Screen.width, Screen.height);
            safeArea.anchorMin = lastSafeArea.position / size;
            safeArea.anchorMax = (lastSafeArea.position + lastSafeArea.size) / size;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        /// <summary>Le rectangle arrondi est dessiné <see cref="SS"/> fois plus fin que son affichage (puis réduit par
        /// pixelsPerUnitMultiplier) : bords et coins lisses, sans escalier.</summary>
        const int SS = 4;

        /// <summary>Rectangle arrondi découpable en 9 (rayon 34 px ≈ 14 points × 2,4, à l'affichage) : anneau doré,
        /// ou plein blanc (masque, fond de bois, barres de pause).</summary>
        static Sprite RoundedSprite(bool bordered)
        {
            const int r = 34 * SS, n = 2 * r + 8 * SS;
            float border = 2.5f * K * SS;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Distance au bord du rectangle arrondi (négative dedans), adoucie sur un pixel d'affichage.
                    float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - r;
                    float a = Mathf.Clamp01(0.5f - d / SS);
                    if (bordered) a *= Mathf.Clamp01((d + border) / SS + 0.5f); // anneau : transparent au-delà de la bordure
                    var c = bordered ? GoldBorder : Color.white;
                    c.a *= a;
                    px[y * n + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            float b = r + 2 * SS;
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>Bande de kente en une seule image : 36 bandes nettes (filtrage au plus proche), sans liseré.</summary>
        static Texture2D KenteTexture()
        {
            const int stripes = 36, w = stripes * 8;
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < w; x++) tex.SetPixel(x, 0, Kente[x / 8 % Kente.Length]);
            tex.Apply();
            return tex;
        }
    }
}
