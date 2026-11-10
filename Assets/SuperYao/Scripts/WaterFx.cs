using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Anime une bande d'eau de la lagune (comme les vagues et reflets du téléphone, drawWaterFoam) :
    /// - une seconde couche de la même eau, retournée et à demi transparente, glisse lentement d'avant en arrière et
    ///   ondule : les vaguelettes semblent bouger et se croiser ;
    /// - de petits reflets de soleil scintillent près de la surface, apparaissent et s'éteignent un peu partout.
    /// Tout est créé au lancement (une couche, quelques reflets recyclés) : rien n'est créé pendant le jeu.
    /// À poser sur l'objet de la bande d'eau (SpriteRenderer en mode Tiled, pivot en bas).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class WaterFx : MonoBehaviour
    {
        [Tooltip("Course de la couche de vagues, en cases (de chaque côté).")]
        public float drift = 0.7f;
        [Tooltip("Durée d'un aller-retour des vagues, en secondes.")]
        public float period = 7f;
        [Range(0f, 1f)] public float overlayAlpha = 0.35f;
        [Tooltip("Hauteur, sous le haut de l'eau, où brillent les reflets, en cases.")]
        public float glintDepth = 0.7f;
        [Tooltip("Taille des reflets, en cases (plus petits pour l'eau lointaine).")]
        public float glintSize = 0.35f;
        [Tooltip("Un reflet pour tant de cases de largeur.")]
        public float casesPerGlint = 3.5f;

        SpriteRenderer water, overlay;
        Transform[] glints;
        SpriteRenderer[] glintRenderers;
        float[] glintTime, glintLife;
        Vector3 overlayBase;
        float scale, left, right, top;
        static Sprite glintSprite;

        void Start()
        {
            water = GetComponent<SpriteRenderer>();
            scale = Mathf.Abs(transform.localScale.x);
            float width = water.size.x * scale, height = water.size.y * scale;
            var p = transform.localPosition;
            left = p.x - width / 2f;
            right = p.x + width / 2f;
            top = p.y + height;

            // Couche de vagues : même eau retournée, plus étroite de la course pour ne jamais dépasser les bords.
            var go = new GameObject("Vagues");
            go.transform.SetParent(transform, false);
            overlay = go.AddComponent<SpriteRenderer>();
            overlay.sprite = water.sprite;
            overlay.sharedMaterial = water.sharedMaterial;
            overlay.drawMode = SpriteDrawMode.Tiled;
            overlay.tileMode = SpriteTileMode.Continuous;
            overlay.size = new Vector2(Mathf.Max(0.1f, water.size.x - 2f * drift / scale), water.size.y);
            overlay.flipX = true;
            overlay.color = new Color(1f, 1f, 1f, overlayAlpha);
            overlay.sortingLayerID = water.sortingLayerID;
            overlay.sortingOrder = water.sortingOrder;
            overlay.maskInteraction = water.maskInteraction;
            overlayBase = go.transform.localPosition;

            // Reflets de soleil.
            if (glintSprite == null) glintSprite = MakeGlint();
            int n = Mathf.Clamp(Mathf.RoundToInt(width / casesPerGlint), 2, 40);
            glints = new Transform[n];
            glintRenderers = new SpriteRenderer[n];
            glintTime = new float[n];
            glintLife = new float[n];
            for (int i = 0; i < n; i++)
            {
                var g = new GameObject("Reflet");
                g.transform.SetParent(transform.parent, false);
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = glintSprite;
                sr.sharedMaterial = water.sharedMaterial;
                sr.sortingLayerID = water.sortingLayerID;
                sr.sortingOrder = water.sortingOrder + 1;
                glints[i] = g.transform;
                glintRenderers[i] = sr;
                Respawn(i);
                glintTime[i] = Random.value * glintLife[i]; // pas tous en même temps
            }
        }

        void Respawn(int i)
        {
            glintLife[i] = Random.Range(0.6f, 1.4f);
            glintTime[i] = 0f;
            glints[i].localPosition = new Vector3(Random.Range(left + 0.5f, right - 0.5f), top - Random.Range(0.08f, glintDepth), 0f);
            float s = glintSize * Random.Range(0.6f, 1.2f);
            glints[i].localScale = new Vector3(s * 1.8f, s, 1f); // reflet étiré à l'horizontale
        }

        void Update()
        {
            float t = Time.time;
            // Les vagues vont et viennent et montent un peu, en décalé.
            float phase = t * Mathf.PI * 2f / period;
            overlay.transform.localPosition = overlayBase + new Vector3(Mathf.Sin(phase) * drift / scale, Mathf.Sin(phase * 1.7f) * 0.04f / scale, 0f);

            for (int i = 0; i < glints.Length; i++)
            {
                glintTime[i] += Time.deltaTime;
                float k = glintTime[i] / glintLife[i];
                if (k >= 1f) { Respawn(i); k = 0f; }
                glintRenderers[i].color = new Color(1f, 0.97f, 0.85f, Mathf.Sin(k * Mathf.PI) * 0.9f);
            }
        }

        /// <summary>Petit reflet : un éclat clair, doux sur les bords, avec une croix fine (une case de large).</summary>
        static Sprite MakeGlint()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float blob = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v * 4f));
                    float cross = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(v) * 10f) * (1f - Mathf.Abs(u)), Mathf.Clamp01(1f - Mathf.Abs(u) * 10f) * (1f - Mathf.Abs(v)));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(blob * blob + cross * 0.8f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
