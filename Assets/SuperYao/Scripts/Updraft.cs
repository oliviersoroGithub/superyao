using System.Collections.Generic;
using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Courant d'air ascendant (stage 4) : une colonne d'air (boîte « déclencheur ») qui fait monter Yao quand il a
    /// l'hélice (HeroController.canFly), jusqu'en haut de la colonne. On le voit grâce à des filets d'air clairs qui
    /// montent, créés ici (aucune image nécessaire).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Updraft : MonoBehaviour
    {
        [Tooltip("Vitesse de montée visée dans le courant, en cases/s.")]
        public float rise = 9f;
        [Tooltip("Accélération vers cette vitesse, en cases/s².")]
        public float accel = 45f;
        [Tooltip("Nombre de filets d'air visibles.")]
        public int wisps = 14;
        [Tooltip("Matériau des filets (celui des sprites du jeu).")]
        public Material material;

        BoxCollider2D box;
        readonly List<(SpriteRenderer sr, float speed, float x, float t)> parts = new List<(SpriteRenderer, float, float, float)>();
        static Sprite wispSprite;

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            if (wispSprite == null) wispSprite = MakeWisp();
            var size = box.size;
            for (int i = 0; i < wisps; i++)
            {
                var go = new GameObject($"Nuit_Filet_{i}"); // « Nuit_ » : la lumière du soir (DayNight) ne les fige pas
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = wispSprite;
                if (material != null) sr.sharedMaterial = material;
                sr.sortingLayerName = "Monde";
                sr.sortingOrder = -2;
                float w = Random.Range(0.6f, 1.1f);
                go.transform.localScale = new Vector3(w, Random.Range(0.8f, 1.6f), 1f);
                parts.Add((sr, Random.Range(4f, 8f), Random.Range(-size.x / 2f + 0.3f, size.x / 2f - 0.3f), Random.value));
            }
        }

        void Update()
        {
            var size = box.size;
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                p.t += Time.deltaTime * p.speed / Mathf.Max(1f, size.y);
                if (p.t > 1f) { p.t -= 1f; p.x = Random.Range(-size.x / 2f + 0.3f, size.x / 2f - 0.3f); }
                parts[i] = p;
                float y = box.offset.y - size.y / 2f + p.t * size.y;
                p.sr.transform.localPosition = new Vector3(box.offset.x + p.x + Mathf.Sin((p.t + i) * 9f) * 0.12f, y, 0f);
                p.sr.color = new Color(0.82f, 0.95f, 1f, Mathf.Sin(p.t * Mathf.PI) * 0.45f);
            }
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var hero = other.GetComponentInParent<HeroController>();
            if (hero != null) hero.InUpdraft(rise, accel);
        }

        /// <summary>Un filet d'air : trait vertical doux, plus clair au centre.</summary>
        static Sprite MakeWisp()
        {
            const int w = 8, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                    float fy = Mathf.Sin((y + 0.5f) / h * Mathf.PI);
                    px[y * w + x] = new Color(1f, 1f, 1f, fx * fx * fy);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
