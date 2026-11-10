using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SuperYao
{
    /// <summary>
    /// Le marché, entre deux stages, d'après la version téléphone (MarketScreen.tsx, market.ts ; repère 1672 × 941) :
    /// l'étal peint en fond ; une ardoise où l'on vend ses bananes (1 banane = 2 pièces) ; des planches où l'on achète
    /// une vie (400 pièces) ou un pouvoir déjà trouvé dans un stage (karité 100, orbe de feu 200), prêt pour le prochain
    /// stage. Ouvert depuis la carte, par-dessus elle.
    /// </summary>
    public class MarketScreen : MonoBehaviour
    {
        public Font font;
        public Sprite background, portrait, banana, shea, fireOrb, coin, diamond;
        public AudioClip coinSound, lifeSound, powerSound;
        AudioSource audioSource;
        /// <summary>Appelé à la fermeture (la carte met ses compteurs à jour).</summary>
        public System.Action Closed;

        static readonly Color Wood = UiKit.Hex("#C4843A"), WoodDark = UiKit.Hex("#6B3A16"), Cream = UiKit.Hex("#FFF6E4");
        static readonly Color Orange = UiKit.Hex("#F07A28"), Chalk = UiKit.Hex("#E7C98A");

        RectTransform root, stage, deals;
        Text wallet, note;
        Button sellOne, sellAll;

        public void Open()
        {
            if (root == null) Build();
            root.gameObject.SetActive(true);
            note.text = GameProgress.UnlockedGrow || GameProgress.UnlockedFire ? "Choisis une vie ou un pouvoir." : "Trouve un pouvoir dans un stage pour l'acheter ici.";
            Refresh();
        }

        void Build()
        {
            UiKit.Font = font;
            var (full, s) = UiKit.Screen(transform, "Marche", 50);
            root = full;
            stage = s;
            var bg = UiKit.Stretch(UiKit.Rect(full, "Fond"));
            bg.SetAsFirstSibling();
            bg.gameObject.AddComponent<Image>().color = UiKit.Hex("#7EC8F0"); // bloque aussi les touches vers la carte
            UiKit.Cover(bg, background);

            var back = UiKit.Button(s, "Retour", 28, 22, 250, 72, Wood, WoodDark, 4, 4, 16, Close);
            UiKit.Label((RectTransform)back.transform.Find("Face"), "< Carte", 26, Cream);

            // L'ardoise : compteurs, vente des bananes, message.
            var counter = UiKit.Box(s, "Ardoise", 24, 688, 470, 220, UiKit.Hex("#2C2418"), UiKit.Hex("#F0C36A"), 5, 16);
            var col = UiKit.Stretch(UiKit.Rect(counter, "Colonne"), 5);
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 10, 10);
            v.spacing = 6;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandHeight = false;
            Line(col, "LE MARCHÉ", 18, UiKit.Hex("#F0C36A"));
            wallet = Line(col, "", 20, Cream);
            Line(col, $"1 banane = {GameProgress.BananaPrice} pièces. Les pièces s'échangent ici.", 15, Chalk);
            var row = UiKit.Rect(col, "Vente");
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            sellOne = UiKit.Button(row, "Vendre1", 0, 0, 200, 44, Orange, WoodDark, 3, 3, 12, () => Sell(1));
            UiKit.Label((RectTransform)sellOne.transform.Find("Face"), "Vendre 1 banane", 18, Cream);
            sellAll = UiKit.Button(row, "ToutVendre", 210, 0, 170, 44, Orange, WoodDark, 3, 3, 12, () => Sell(int.MaxValue));
            UiKit.Label((RectTransform)sellAll.transform.Find("Face"), "Tout vendre", 18, Cream);
            note = Line(col, "", 16, Chalk);

            deals = UiKit.Stretch(UiKit.Rect(s, "Offres"));
        }

        Text Line(RectTransform col, string text, float size, Color color)
        {
            var t = UiKit.Label(UiKit.Rect(col, "Ligne"), text, size, color, TextAnchor.MiddleLeft);
            t.rectTransform.parent.gameObject.AddComponent<LayoutElement>().preferredHeight = size * 1.35f;
            return t;
        }

        void Refresh()
        {
            int coins = GameProgress.Coins, bananas = GameProgress.Bananas, lives = GameProgress.Lives;
            wallet.text = $"{coins} pièce{(coins > 1 ? "s" : "")}    {bananas} banane{(bananas > 1 ? "s" : "")}    {lives} vie{(lives > 1 ? "s" : "")}";
            sellOne.interactable = sellAll.interactable = bananas > 0;

            // Les planches : une vie, puis les pouvoirs déjà trouvés.
            for (int i = deals.childCount - 1; i >= 0; i--) Destroy(deals.GetChild(i).gameObject);
            var list = new List<(GameProgress.Offer id, string label, bool enabled, string detail, string accent, Sprite icon)>
            {
                (GameProgress.Offer.Life, "Une vie", coins >= GameProgress.LifePrice, $"{GameProgress.LifePrice} pièces", "#2E9E4B", portrait),
            };
            if (GameProgress.UnlockedGrow)
                list.Add((GameProgress.Offer.Grow, "Skate", !GameProgress.Big && coins >= GameProgress.GrowPrice,
                    GameProgress.Big ? "Déjà à toi" : $"{GameProgress.GrowPrice} pièces", "#F07A28", shea));
            if (GameProgress.UnlockedFire)
                list.Add((GameProgress.Offer.Fire, "Orbe de feu", !GameProgress.Fire && coins >= GameProgress.FirePrice,
                    GameProgress.Fire ? "Déjà à toi" : $"{GameProgress.FirePrice} pièces", "#E25B2A", fireOrb));
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                var plank = UiKit.Button(deals, d.label, 520 + i * 280, 700, 260, 196, UiKit.Hex(d.accent), WoodDark, 4, 10, 16, () => Buy(d.id));
                plank.interactable = d.enabled;
                var face = (RectTransform)plank.transform.Find("Face");
                face.gameObject.AddComponent<RectMask2D>();
                // Petit auvent rayé en haut de la planche.
                var awning = UiKit.At(UiKit.Rect(face, "Auvent"), 0, 0, 252, 14);
                for (int n = 0; n < 8; n++)
                    UiKit.At(UiKit.Rect(awning, "Bande"), n * 31.5f, 0, 31.5f, 14).gameObject.AddComponent<Image>().color = n % 2 == 1 ? Cream : UiKit.Hex("#C45010");
                UiKit.Picture(face, d.icon, 126 - 30, 28, 60, 60);
                UiKit.Label(UiKit.At(UiKit.Rect(face, "Nom"), 0, 92, 252, 36), d.label, 24, Cream);
                UiKit.Label(UiKit.At(UiKit.Rect(face, "Prix"), 0, 128, 252, 30), d.detail, 18, UiKit.Hex("#FFE7A8"));
                if (!d.enabled) foreach (var g in plank.GetComponentsInChildren<Graphic>()) { var c = g.color; c.a *= 0.45f; g.color = c; }
            }

            // Dernière planche : regarder une vidéo pour un cadeau au hasard (5 par jour).
            int left = GameProgress.VideoGiftsLeft;
            bool canVideo = left > 0 && RewardedVideo.Instance.CanShow && !watching;
            var video = UiKit.Button(deals, "Video", 520 + list.Count * 280, 700, 260, 196, UiKit.Hex("#7A3FB0"), WoodDark, 4, 10, 16, WatchVideo);
            video.interactable = canVideo;
            var vf = (RectTransform)video.transform.Find("Face");
            UiKit.Label(UiKit.At(UiKit.Rect(vf, "Icone"), 0, 18, 252, 70), "VIDÉO", 40, UiKit.Hex("#FFE7A8"));
            UiKit.Label(UiKit.At(UiKit.Rect(vf, "Nom"), 0, 92, 252, 36), "Cadeau surprise", 24, Cream);
            UiKit.Label(UiKit.At(UiKit.Rect(vf, "Prix"), 0, 128, 252, 30),
                left > 0 ? $"Encore {left} aujourd'hui" : "Reviens demain", 18, UiKit.Hex("#FFE7A8"));
            if (!canVideo) foreach (var g in video.GetComponentsInChildren<Graphic>()) { var c = g.color; c.a *= 0.45f; g.color = c; }
        }

        bool watching;

        void WatchVideo()
        {
            if (watching || GameProgress.VideoGiftsLeft <= 0) return;
            watching = true;
            note.text = "";
            RewardedVideo.Instance.Show(ok =>
            {
                watching = false;
                if (note == null) return;
                if (ok)
                {
                    var gift = GameProgress.VideoGift();
                    note.text = gift.text;
                    StartCoroutine(GiftReveal(gift));
                }
                else note.text = "Vidéo pas terminée : pas de cadeau cette fois.";
                Refresh();
            });
        }

        void Sell(int count)
        {
            int gained = GameProgress.SellBananas(count);
            if (gained <= 0) return;
            Play(coinSound);
            note.text = $"+{gained} pièces.";
            Refresh();
        }

        void Buy(GameProgress.Offer offer)
        {
            var (ok, message) = GameProgress.Buy(offer);
            if (ok) Play(offer == GameProgress.Offer.Life ? lifeSound : powerSound);
            note.text = message;
            Refresh();
        }

        // ------------------------------------------------------------------ ouverture du cadeau

        /// <summary>Le cadeau gagné : un paquet apparaît, tremble, s'ouvre dans un éclat de rayons et laisse sortir le
        /// cadeau (pièces, bananes, diamant ou vie) avec son nombre. Un appui (ou 4 secondes) le referme.</summary>
        System.Collections.IEnumerator GiftReveal(GameProgress.Gift gift)
        {
            if (coin == null || diamond == null)
            {
                var map = GetComponent<MapScreen>();
                if (map != null) { if (coin == null) coin = map.coin; if (diamond == null) diamond = map.diamond; }
            }
            var layer = UiKit.Stretch(UiKit.Rect(root, "Cadeau"));
            var veil = layer.gameObject.AddComponent<Image>();
            veil.color = new Color(0f, 0f, 0f, 0f);
            float t = 0f;
            bool closed = false;
            var tap = layer.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => { if (t > 1.3f) closed = true; });

            var center = UiKit.Rect(layer, "Centre");
            center.anchorMin = center.anchorMax = center.pivot = new Vector2(0.5f, 0.5f);
            center.sizeDelta = Vector2.zero;

            // Rayons de lumière, cachés jusqu'à l'ouverture.
            var rays = UiKit.Rect(center, "Rayons");
            rays.sizeDelta = Vector2.zero;
            for (int i = 0; i < 12; i++)
            {
                var r = UiKit.Rect(rays, "Rayon");
                r.pivot = new Vector2(0.5f, 0f);
                r.sizeDelta = new Vector2(34f, 330f);
                r.localRotation = Quaternion.Euler(0f, 0f, i * 30f);
                UiKit.Rounded(r, new Color(1f, 0.86f, 0.35f, i % 2 == 0 ? 0.55f : 0.3f), 17);
            }
            rays.localScale = Vector3.zero;

            // Le paquet : boîte et ruban, couvercle et nœud.
            var box = Part(center, "Paquet", new Vector2(220f, 180f), new Vector2(0f, -40f), UiKit.Hex("#D8343A"), 18);
            Part(box, "Ruban", new Vector2(44f, 180f), Vector2.zero, UiKit.Hex("#FFD23F"), 8);
            var lid = Part(center, "Couvercle", new Vector2(250f, 62f), new Vector2(0f, 76f), UiKit.Hex("#B8232A"), 16);
            Part(lid, "Ruban", new Vector2(44f, 62f), Vector2.zero, UiKit.Hex("#FFD23F"), 8);
            Part(lid, "Noeud", new Vector2(110f, 54f), new Vector2(0f, 46f), UiKit.Hex("#FFC21A"), 27);
            box.localScale = lid.localScale = Vector3.zero;

            // Le cadeau et son nombre, cachés jusqu'à l'ouverture.
            Sprite icon = gift.kind == GameProgress.GiftKind.Coins ? coin : gift.kind == GameProgress.GiftKind.Bananas ? banana
                : gift.kind == GameProgress.GiftKind.Diamond ? diamond : portrait;
            var prize = UiKit.Rect(center, "Cadeau");
            prize.sizeDelta = new Vector2(200f, 200f);
            var prizeImg = prize.gameObject.AddComponent<Image>();
            prizeImg.sprite = icon;
            prizeImg.preserveAspect = true;
            prizeImg.raycastTarget = false;
            prize.localScale = Vector3.zero;
            string label = gift.kind == GameProgress.GiftKind.Coins ? $"+{gift.amount} pièces"
                : gift.kind == GameProgress.GiftKind.Bananas ? $"+{gift.amount} bananes"
                : gift.kind == GameProgress.GiftKind.Diamond ? "+1 diamant" : "+1 vie";
            var textRt = UiKit.Rect(center, "Texte");
            textRt.sizeDelta = new Vector2(700f, 90f);
            textRt.anchoredPosition = new Vector2(0f, -200f);
            var text = UiKit.Label(textRt, label, 62, UiKit.Hex("#FFD23F"), TextAnchor.MiddleCenter, new Color(0f, 0f, 0f, 0.6f), 4f);
            var hintRt = UiKit.Rect(center, "Appui");
            hintRt.sizeDelta = new Vector2(700f, 50f);
            hintRt.anchoredPosition = new Vector2(0f, -268f);
            var hint = UiKit.Label(hintRt, "Touche l'écran pour continuer", 24, Color.white);
            var gold = text.color;
            text.color = new Color(gold.r, gold.g, gold.b, 0f);
            hint.color = new Color(1f, 1f, 1f, 0f);

            const float Appear = 0.35f, Shake = 1.0f, Open = 1.3f, AutoClose = 4f;
            bool opened = false;
            while (!closed && t < AutoClose)
            {
                t += Time.unscaledDeltaTime;
                veil.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / Appear) * 0.6f);
                if (t < Shake)
                {
                    // Le paquet surgit avec un petit rebond, puis tremble de plus en plus fort.
                    float a = Mathf.Clamp01(t / Appear);
                    box.localScale = lid.localScale = Vector3.one * (a < 1f ? EaseBack(a) : 1f);
                    float wiggle = t > Appear ? Mathf.Sin(t * 48f) * 9f * (t - Appear) / (Shake - Appear) : 0f;
                    box.localRotation = lid.localRotation = Quaternion.Euler(0f, 0f, wiggle);
                }
                else
                {
                    if (!opened)
                    {
                        opened = true;
                        box.localRotation = lid.localRotation = Quaternion.identity;
                        Play(gift.kind == GameProgress.GiftKind.Life ? lifeSound : gift.kind == GameProgress.GiftKind.Diamond ? powerSound : coinSound);
                    }
                    float o = t - Shake;
                    // Le couvercle s'envole en tournant, la boîte s'efface, le cadeau monte et grossit dans les rayons.
                    lid.anchoredPosition = new Vector2(o * 260f, 76f + o * 900f - o * o * 900f);
                    lid.localRotation = Quaternion.Euler(0f, 0f, -o * 260f);
                    float fade = Mathf.Clamp01(1f - o / 0.35f);
                    foreach (var g in box.GetComponentsInChildren<Image>()) { var c = g.color; c.a = fade; g.color = c; }
                    foreach (var g in lid.GetComponentsInChildren<Image>()) { var c = g.color; c.a = fade; g.color = c; }
                    float pop = Mathf.Clamp01(o / 0.3f);
                    prize.localScale = Vector3.one * (pop < 1f ? EaseBack(pop) * 1.15f : 1f + Mathf.Sin((o - 0.3f) * 4f) * 0.05f);
                    prize.anchoredPosition = new Vector2(0f, Mathf.Lerp(-40f, 20f, pop));
                    rays.localScale = Vector3.one * Mathf.Clamp01(o / 0.25f);
                    rays.localRotation = Quaternion.Euler(0f, 0f, -o * 40f);
                    text.color = new Color(gold.r, gold.g, gold.b, Mathf.Clamp01((o - 0.15f) / 0.3f));
                    if (t > Open) hint.color = new Color(1f, 1f, 1f, 0.55f + 0.35f * Mathf.Sin(t * 5f));
                }
                yield return null;
            }
            Destroy(layer.gameObject);
        }

        static RectTransform Part(RectTransform parent, string name, Vector2 size, Vector2 pos, Color color, float radius)
        {
            var rt = UiKit.Rect(parent, name);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            UiKit.Rounded(rt, color, radius);
            return rt;
        }

        /// <summary>Courbe qui dépasse un peu puis revient (0 → 1).</summary>
        static float EaseBack(float x)
        {
            const float c = 1.70158f;
            x -= 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }

        void Play(AudioClip clip)
        {
            if (clip == null || GameProgress.Muted) return;
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.PlayOneShot(clip, 0.8f);
        }

        void Close()
        {
            root.gameObject.SetActive(false);
            Closed?.Invoke();
        }
    }
}
