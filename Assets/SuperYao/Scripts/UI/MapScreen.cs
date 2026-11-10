using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SuperYao
{
    /// <summary>
    /// Carte de la Côte d'Ivoire entre deux stages, d'après la version téléphone (MapScreen.tsx, repère 1672 × 941) :
    /// la carte à gauche avec ses 12 étapes, reliées par un chemin doré jusqu'au dernier stage débloqué puis en
    /// pointillés ; Yao se tient sur le stage à jouer (après une victoire, il y marche à petits bonds). À droite : le
    /// chapitre, le son et le retour à l'accueil, les compteurs, le bilan du stage gagné (ou l'avancement), et le
    /// panneau du stage choisi (vignette, nom, accroche, objets à ramasser, bouton Jouer). On peut toucher une étape
    /// déjà atteinte pour la rejouer. Le marché viendra plus tard.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        public Font font;
        public Sprite map, mapBlur, hero, portrait, coin, banana, diamond;
        public Sprite soundOn, soundOff, home;

        const float MapW = 1312f, MapH = 1199f, MapK = UiKit.MockH / MapH;
        const float ColX = MapW * MapK + 16f, ColW = UiKit.MockW - ColX - 12f;
        const float NodeW = 36f, HeroH = 78f;

        static readonly Color Gold = UiKit.Hex("#FFD23F"), Dark = UiKit.Hex("#1C1A1F");

        StageCatalog catalog;
        MarketScreen market;
        readonly List<Text> counters = new List<Text>();
        RectTransform stageRoot, heroRt, ribbon, panel, mapRoot;
        Image heroImg, soundIcon, glow;
        int selected, open;
        Coroutine walking;

        void Start()
        {
            UiKit.Font = font;
            catalog = StageCatalog.Load();
            var stages = catalog != null ? catalog.stages : new List<StageCatalog.Stage>();
            // On peut aller sur les stages atteints et sur le suivant ; s'il n'est pas encore construit, son bouton
            // affiche « Bientôt ».
            open = Mathf.Clamp(GameProgress.Furthest + 1, 1, Mathf.Max(1, stages.Count));
            // Mode essai (GameProgress.TestMode) : tous les stages déjà construits sont ouverts, sans jouer les précédents.
            if (GameProgress.TestMode) open = Mathf.Max(open, stages.FindLastIndex(st => st.Built) + 1);
            selected = Mathf.Clamp(GameProgress.Selected, 0, open - 1);

            var (full, s) = UiKit.Screen(transform, "Carte");
            stageRoot = s;
            var bg = UiKit.Stretch(UiKit.Rect(full, "Fond"));
            bg.SetAsFirstSibling();
            bg.gameObject.AddComponent<Image>().color = UiKit.Hex("#0E3B5C");
            UiKit.Cover(bg, mapBlur);
            UiKit.Stretch(UiKit.Rect(bg, "Voile")).gameObject.AddComponent<Image>().color = UiKit.Hex("#081422", 0.45f);

            // La carte, ses coins arrondis, son chemin et ses étapes.
            mapRoot = UiKit.At(UiKit.Rect(s, "Carte"), 0, 0, MapW * MapK, UiKit.MockH);
            UiKit.Rounded(mapRoot, Color.white, 18);
            mapRoot.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            UiKit.Picture(mapRoot, map, 0, 0, MapW * MapK, UiKit.MockH, false);
            DrawPath(stages);
            for (int i = 0; i < stages.Count; i++) DrawNode(stages, i);

            heroRt = UiKit.At(UiKit.Rect(mapRoot, "Yao"), 0, 0, HeroH, HeroH);
            heroImg = heroRt.gameObject.AddComponent<Image>();
            heroImg.sprite = hero;
            heroImg.preserveAspect = true;
            heroImg.raycastTarget = false;
            heroRt.pivot = new Vector2(0.5f, 0f); // les pieds sur l'étape

            BuildColumn(stages);

            // Le marché (bouton orange en bas à gauche de la carte, comme sur le téléphone).
            market = GetComponent<MarketScreen>();
            if (market != null)
            {
                var mb = UiKit.Button(s, "Marche", 36, 836, 340, 82, UiKit.Hex("#E07A2F"), UiKit.Hex("#6B2E0E"), 4, 9, 22, () => market.Open());
                var face = (RectTransform)mb.transform.Find("Face");
                UiKit.Picture(face, banana, 70, 14, 46, 46);
                var t = UiKit.Label(face, "Marché", 28, Color.white);
                t.rectTransform.offsetMin = new Vector2(60, 0);
                market.Closed = RefreshCounters;
            }

            // Après une victoire : Yao marche du stage gagné jusqu'au suivant.
            int from = GameProgress.JustCompleted;
            if (from >= 0 && from != selected) walking = StartCoroutine(Walk(stages, from, selected, 0.5f));
            else PlaceHero(Node(stages, selected));
            ShowStage(stages, selected);
        }

        // ------------------------------------------------------------------ carte

        Vector2 Node(List<StageCatalog.Stage> stages, int i) => stages[i].mapPosition * MapK;

        /// <summary>Points du chemin de l'étape a à l'étape b (dans un sens ou l'autre), points de passage compris.</summary>
        List<Vector2> Route(List<StageCatalog.Stage> stages, int a, int b)
        {
            if (a > b) { var r = Route(stages, b, a); r.Reverse(); return r; }
            var pts = new List<Vector2> { Node(stages, a) };
            for (int i = a + 1; i <= b; i++)
            {
                foreach (var v in stages[i].via) pts.Add(v * MapK);
                pts.Add(Node(stages, i));
            }
            return pts;
        }

        void DrawPath(List<StageCatalog.Stage> stages)
        {
            if (stages.Count < 2) return;
            int last = stages.Count - 1, done = Mathf.Min(open - 1, last);
            var root = UiKit.Stretch(UiKit.Rect(mapRoot, "Chemin"));
            // Ombre sombre sous tout le chemin, puis doré jusqu'au dernier stage débloqué, pointillés blancs ensuite.
            Polyline(root, Route(stages, 0, last), 11, UiKit.Hex("#281808", 0.55f), false);
            if (done > 0)
            {
                Polyline(root, Route(stages, 0, done), 10, UiKit.Hex("#7A4A0E"), false);
                Polyline(root, Route(stages, 0, done), 6, Gold, false);
            }
            if (done < last) Polyline(root, Route(stages, done, last), 6, Color.white, true);
        }

        static void Polyline(RectTransform parent, List<Vector2> pts, float width, Color color, bool dashed)
        {
            const float dash = 14f, gap = 11f;
            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                float len = Vector2.Distance(a, b);
                if (len < 0.01f) continue;
                if (!dashed) { Segment(parent, a, b, width, color); continue; }
                for (float t = 0f; t < len; t += dash + gap)
                    Segment(parent, Vector2.Lerp(a, b, t / len), Vector2.Lerp(a, b, Mathf.Min(len, t + dash) / len), width, color);
            }
        }

        /// <summary>Trait épais à bouts ronds de a à b (repère de la maquette, y vers le bas).</summary>
        static void Segment(RectTransform parent, Vector2 a, Vector2 b, float width, Color color)
        {
            var rt = UiKit.Rect(parent, "Trait");
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var mid = (a + b) / 2f;
            rt.anchoredPosition = new Vector2(mid.x, -mid.y);
            rt.sizeDelta = new Vector2(Vector2.Distance(a, b) + width, width);
            rt.localEulerAngles = new Vector3(0f, 0f, -Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            UiKit.Rounded(rt, color, width / 2f);
        }

        void DrawNode(List<StageCatalog.Stage> stages, int i)
        {
            bool gold = i < open;
            var p = Node(stages, i);
            float w = NodeW, h = w * 0.44f;
            var rt = UiKit.At(UiKit.Rect(mapRoot, $"Etape_{i + 1}"), p.x - w / 2f - 8f, p.y - h / 2f - 8f, w + 16f, h + 26f);
            if (gold)
            {
                var hit = rt.gameObject.AddComponent<Image>();
                hit.color = Color.clear; // zone de toucher
                int index = i;
                rt.gameObject.AddComponent<Button>().onClick.AddListener(() => Choose(stages, index));
            }
            var g = UiKit.At(UiKit.Rect(rt, "Halo"), 0, 0, w + 16f, h + 22f);
            var gi = UiKit.Rounded(g, UiKit.Hex("#FFE25C", 0.6f), (h + 22f) / 2f);
            g.gameObject.SetActive(false);
            g.name = $"Halo_{i}";
            var basis = UiKit.At(UiKit.Rect(rt, "Socle"), 8f, 8f + h * 0.3f, w, h);
            UiKit.Rounded(basis, UiKit.Hex(gold ? "#B86A10" : "#5E646B"), h / 2f);
            var face = UiKit.Box(rt, "Face", 8f, 8f, w, h, UiKit.Hex(gold ? "#F6B42A" : "#CDD2D8"), UiKit.Hex(gold ? "#FFE08A" : "#FFFFFF"), 2.5f, h / 2f);
            UiKit.Label(face, $"{i + 1}", 17, Color.white, TextAnchor.MiddleCenter, UiKit.Hex("#1E1E1E"), 1f);
        }

        void PlaceHero(Vector2 p, float hop = 0f, bool flip = false)
        {
            heroRt.anchoredPosition = new Vector2(p.x, -(p.y - 2f) + hop);
            heroRt.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
        }

        void Choose(List<StageCatalog.Stage> stages, int i)
        {
            if (i < 0 || i >= open || i == selected) return;
            int from = selected;
            selected = i;
            GameProgress.Select(i);
            if (walking != null) StopCoroutine(walking);
            walking = StartCoroutine(Walk(stages, from, i, 0f));
            ShowStage(stages, i);
        }

        /// <summary>Yao marche d'une étape à l'autre en suivant le chemin, à petits bonds.</summary>
        IEnumerator Walk(List<StageCatalog.Stage> stages, int from, int to, float delay)
        {
            var trip = Route(stages, from, to);
            var lengths = new List<float> { 0f };
            for (int i = 1; i < trip.Count; i++) lengths.Add(lengths[i - 1] + Vector2.Distance(trip[i - 1], trip[i]));
            float total = Mathf.Max(1f, lengths[lengths.Count - 1]);
            bool flip = trip[trip.Count - 1].x < trip[0].x;
            PlaceHero(trip[0], 0f, flip);
            yield return new WaitForSeconds(delay);
            float duration = Mathf.Min(3f, 0.9f + 0.45f * Mathf.Abs(to - from));
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration) * total;
                int seg = 1;
                while (seg < lengths.Count - 1 && lengths[seg] < k) seg++;
                float u = Mathf.InverseLerp(lengths[seg - 1], lengths[seg], k);
                PlaceHero(Vector2.Lerp(trip[seg - 1], trip[seg], u), Mathf.Abs(Mathf.Sin(t / 0.34f * Mathf.PI)) * 12f, flip);
                yield return null;
            }
            PlaceHero(trip[trip.Count - 1], 0f, flip);
            walking = null;
        }

        // ------------------------------------------------------------------ colonne de droite

        Text kicker, levelName, tagline, playLabel, ribbonA, ribbonB;
        Text collectCoins, collectBananas, collectDiamonds;
        Image thumb, progressFill;
        Button play;
        RectTransform doneRow, progress;
        Text doneCoins, doneBananas, doneDiamonds;

        void BuildColumn(List<StageCatalog.Stage> stages)
        {
            var s = stageRoot;
            var chapter = UiKit.Box(s, "Chapitre", ColX, 16, ColW - 196, 70, UiKit.Hex("#8A4F22"), UiKit.Hex("#4A2410"), 3, 18);
            var ck = UiKit.Label(chapter, "CHAPITRE 1", 17, UiKit.Hex("#FFE08A"), TextAnchor.UpperCenter);
            ck.rectTransform.offsetMax = new Vector2(0, -8);
            var ct = UiKit.Label(chapter, "CÔTE D'IVOIRE", 27, Color.white, TextAnchor.LowerCenter, UiKit.Hex("#2A1408"));
            ct.rectTransform.offsetMin = new Vector2(0, 6);

            var sound = UiKit.Button(s, "Son", UiKit.MockW - 12 - 184, 16, 86, 70, Dark, Gold, 4, 4, 18, ToggleSound);
            soundIcon = UiKit.Picture((RectTransform)sound.transform.Find("Face"), GameProgress.Muted ? soundOff : soundOn, 18, 10, 42, 42);
            var homeBtn = UiKit.Button(s, "Accueil", UiKit.MockW - 12 - 86, 16, 86, 70, Dark, Gold, 4, 4, 18, () => GameProgress.Load(GameProgress.TitleScene));
            UiKit.Picture((RectTransform)homeBtn.transform.Find("Face"), home, 18, 10, 42, 42);

            // Compteurs.
            var counters = UiKit.Box(s, "Compteurs", ColX, 100, ColW, 70, Dark, Gold, 3, 22);
            var row = counters.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(16, 16, 0, 0);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = true;
            Counter(counters, portrait, GameProgress.Lives);
            Counter(counters, coin, GameProgress.Coins);
            Counter(counters, banana, GameProgress.Bananas);
            Counter(counters, diamond, GameProgress.Diamonds);

            // Bilan du stage gagné, sinon l'avancement dans le chapitre.
            ribbon = UiKit.Box(s, "Bilan", ColX, 184, ColW, 132, UiKit.Hex("#1C1A1F", 0.94f), Gold, 3, 18);
            ribbonA = UiKit.Label(UiKit.At(UiKit.Rect(ribbon, "A"), 10, 10, ColW - 20, 40), "", 20, Color.white);
            ribbonB = UiKit.Label(UiKit.At(UiKit.Rect(ribbon, "B"), 10, 48, ColW - 20, 36), "", 26, Gold);
            progress = UiKit.At(UiKit.Rect(ribbon, "Avancement"), 30, 96, ColW - 60, 14);
            UiKit.Rounded(progress, new Color(1f, 1f, 1f, 0.2f), 7);
            var fill = UiKit.At(UiKit.Rect(progress, "Rempli"), 0, 0, 0, 14);
            progressFill = UiKit.Rounded(fill, Gold, 7);
            doneRow = UiKit.At(UiKit.Rect(ribbon, "Ramasse"), 10, 86, ColW - 20, 36);
            var dr = doneRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            dr.childAlignment = TextAnchor.MiddleCenter;
            dr.spacing = 18;
            dr.childControlWidth = dr.childControlHeight = true;
            dr.childForceExpandWidth = false;
            doneCoins = Small(doneRow, coin);
            doneBananas = Small(doneRow, banana);
            doneDiamonds = Small(doneRow, diamond);

            // Panneau du stage choisi.
            panel = UiKit.Box(s, "Stage", ColX, 330, ColW, 598, UiKit.Hex("#18161C", 0.96f), UiKit.Hex("#3A3640"), 3, 24);
            var frame = UiKit.Box(panel, "Vignette", 18, 18, ColW - 36, 196, UiKit.Hex("#E9D9B4"), UiKit.Hex("#E9D9B4"), 3, 14);
            var thumbMask = UiKit.Stretch(UiKit.Rect(frame, "Masque"), 3);
            UiKit.Rounded(thumbMask, Color.white, 11);
            thumbMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            thumb = UiKit.Cover(thumbMask, null);
            var texts = UiKit.At(UiKit.Rect(panel, "Textes"), 22, 224, ColW - 44, 184);
            kicker = UiKit.Label(UiKit.At(UiKit.Rect(texts, "Kicker"), 0, 0, ColW - 44, 30), "", 21, UiKit.Hex("#F6B42A"), TextAnchor.MiddleLeft);
            levelName = UiKit.Label(UiKit.At(UiKit.Rect(texts, "Nom"), 0, 28, ColW - 44, 44), "", 34, Gold, TextAnchor.MiddleLeft);
            levelName.resizeTextMaxSize = 34;
            levelName.resizeTextMinSize = 18;
            levelName.resizeTextForBestFit = true;
            tagline = UiKit.Label(UiKit.At(UiKit.Rect(texts, "Accroche"), 0, 78, ColW - 44, 100), "", 18, Color.white, TextAnchor.UpperLeft);
            tagline.lineSpacing = 1.15f;
            var collect = UiKit.Box(panel, "ARamasser", 18, 412, ColW - 36, 66, UiKit.Hex("#141217"), UiKit.Hex("#34303A"), 2, 18);
            var cr = collect.gameObject.AddComponent<HorizontalLayoutGroup>();
            cr.childAlignment = TextAnchor.MiddleCenter;
            cr.spacing = 22;
            cr.childControlWidth = cr.childControlHeight = true;
            cr.childForceExpandWidth = false;
            var label = UiKit.Label(UiKit.Rect(collect, "Titre"), "À RAMASSER", 17, UiKit.Hex("#F6B42A"));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            collectCoins = Small(collect, coin);
            collectBananas = Small(collect, banana);
            collectDiamonds = Small(collect, diamond);
            play = UiKit.Button(panel, "Jouer", 18, 492, ColW - 36, 88, UiKit.Hex("#FFB81C"), UiKit.Hex("#7A3E00"), 4, 10, 24, PlaySelected);
            playLabel = UiKit.Label((RectTransform)play.transform.Find("Face"), "Jouer", 44, UiKit.Hex("#2A1408"));
        }

        void Counter(RectTransform parent, Sprite icon, int value)
        {
            var g = UiKit.Rect(parent, "Compteur");
            var h = g.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 6;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            var img = UiKit.Rect(g, "Icone").gameObject.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 44;
            var t = UiKit.Label(g, $"× {value}", 26, Color.white);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            counters.Add(t);
        }

        /// <summary>Au retour du marché : vies, pièces, bananes, diamants.</summary>
        void RefreshCounters()
        {
            int[] values = { GameProgress.Lives, GameProgress.Coins, GameProgress.Bananas, GameProgress.Diamonds };
            for (int i = 0; i < counters.Count && i < values.Length; i++) counters[i].text = $"× {values[i]}";
        }

        Text Small(RectTransform parent, Sprite icon)
        {
            var g = UiKit.Rect(parent, "Petit");
            var h = g.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 5;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            var img = UiKit.Rect(g, "Icone").gameObject.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 34;
            var t = UiKit.Label(g, "", 21, Color.white);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        void ShowStage(List<StageCatalog.Stage> stages, int i)
        {
            if (stages.Count == 0) return;
            var st = stages[i];
            for (int n = 0; n < stages.Count; n++)
            {
                var halo = mapRoot.Find($"Etape_{n + 1}/Halo_{n}");
                if (halo != null) halo.gameObject.SetActive(n == i);
            }
            thumb.sprite = st.thumbnail;
            thumb.enabled = st.thumbnail != null;
            if (st.thumbnail != null) thumb.GetComponent<AspectRatioFitter>().aspectRatio = st.thumbnail.rect.width / st.thumbnail.rect.height;
            kicker.text = $"STAGE {i + 1}";
            levelName.text = st.name.ToUpperInvariant();
            tagline.text = st.tagline;
            collectCoins.text = st.coins.ToString();
            collectBananas.text = st.bananas.ToString();
            collectDiamonds.text = st.diamonds.ToString();
            bool replay = i < GameProgress.Furthest;
            play.interactable = st.Built;
            playLabel.text = !st.Built ? "Bientôt" : replay ? "Rejouer" : "Jouer";

            // Bilan du stage gagné (au retour de la victoire), sinon l'avancement.
            int done = GameProgress.JustCompleted;
            bool showDone = done >= 0 && i == Mathf.Min(done + 1, stages.Count - 1);
            doneRow.gameObject.SetActive(showDone);
            progress.gameObject.SetActive(!showDone);
            if (showDone)
            {
                var d = stages[done];
                ribbonA.text = "Bravo Yao !";
                ribbonA.color = Gold;
                ribbonA.fontSize = 28;
                ribbonB.text = $"Stage {done + 1} terminé";
                ribbonB.color = Color.white;
                ribbonB.fontSize = 19;
                doneCoins.text = $"{GameProgress.GotCoins}/{d.coins}";
                doneBananas.text = $"{GameProgress.GotBananas}/{d.bananas}";
                doneDiamonds.text = $"{GameProgress.GotDiamonds}/{d.diamonds}";
            }
            else
            {
                ribbonA.text = !st.Built ? "Ce stage arrive bientôt" : replay ? "Stage déjà terminé : tu peux le rejouer" : "Stage prêt : tu peux y aller";
                ribbonA.color = Color.white;
                ribbonA.fontSize = 20;
                ribbonB.text = st.name;
                ribbonB.color = Gold;
                ribbonB.fontSize = 26;
                var rt = progressFill.rectTransform;
                rt.sizeDelta = new Vector2((ColW - 60) * (i + 1) / stages.Count, 14);
            }
        }

        void PlaySelected()
        {
            var st = catalog.stages[selected];
            if (st.Built) GameProgress.Play(selected, st.scene);
        }

        void ToggleSound()
        {
            GameProgress.Muted = !GameProgress.Muted;
            soundIcon.sprite = GameProgress.Muted ? soundOff : soundOn;
        }
    }
}
