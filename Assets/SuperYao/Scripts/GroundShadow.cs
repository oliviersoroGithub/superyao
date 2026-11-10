using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Petite ombre ovale sous Yao et sous les ennemis (tous les stages) : posée sur le sol (ou la planche, le toit…)
    /// juste en dessous, plus petite et plus pâle quand il s'élève, cachée s'il n'y a rien dessous ou s'il est hors
    /// jeu. Elle les pose sur le sol et attire l'œil. Ajoutée au lancement par HeroController et Enemy.
    /// </summary>
    public class GroundShadow : MonoBehaviour
    {
        [Tooltip("Largeur de l'ombre au sol, en cases.")]
        public float width = 0.9f;
        [Tooltip("Ordre d'affichage (calque Monde) : sous le personnage, sur le sol.")]
        public int order = 6;
        const float MaxHeight = 6f, Alpha = 0.32f;

        SpriteRenderer shadow;
        Collider2D own;
        readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        ContactFilter2D filter;
        static Sprite oval;

        void Start()
        {
            own = GetComponent<Collider2D>();
            filter = new ContactFilter2D { useTriggers = false };
            if (oval == null) oval = MakeOval();
            var go = new GameObject("Ombre");
            shadow = go.AddComponent<SpriteRenderer>();
            shadow.sprite = oval;
            var mine = GetComponentInChildren<SpriteRenderer>();
            if (mine != null) shadow.sharedMaterial = mine.sharedMaterial;
            shadow.sortingLayerName = "Monde";
            shadow.sortingOrder = order;
        }

        void OnDisable() { if (shadow != null) shadow.enabled = false; }
        void OnDestroy() { if (shadow != null) Destroy(shadow.gameObject); }

        void LateUpdate()
        {
            if (shadow == null) return;
            var hero = GetComponent<HeroController>();
            bool hidden = (own != null && !own.enabled) || (hero != null && hero.IsDying);
            float ground = 0f, height = 0f;
            if (!hidden)
            {
                var p = (Vector2)transform.position;
                int n = Physics2D.Raycast(p + Vector2.up * 0.3f, Vector2.down, filter, hits, MaxHeight + 0.3f);
                bool found = false;
                for (int i = 0; i < n && !found; i++)
                {
                    var c = hits[i].collider;
                    if (c.transform.IsChildOf(transform) || c.GetComponentInParent<HeroController>() != null || c.GetComponentInParent<Enemy>() != null) continue;
                    ground = hits[i].point.y;
                    height = Mathf.Max(0f, p.y - ground);
                    found = true;
                }
                hidden = !found;
            }
            shadow.enabled = !hidden;
            if (hidden) return;
            float k = 1f - Mathf.Clamp01(height / MaxHeight);
            shadow.transform.position = new Vector3(transform.position.x, ground + 0.02f, 0f);
            float w = width * Mathf.Lerp(0.5f, 1f, k);
            shadow.transform.localScale = new Vector3(w, w * 0.22f, 1f);
            shadow.color = new Color(0f, 0f, 0f, Alpha * Mathf.Lerp(0.25f, 1f, k));
        }

        /// <summary>Ovale doux (une unité de large), plus sombre au centre.</summary>
        static Sprite MakeOval()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * s + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
