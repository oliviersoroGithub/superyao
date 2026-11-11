using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Jet d'eau d'une fontaine du lac (stage 7) : une colonne d'eau qui jaillit de la surface. Yao qui entre dedans (en
    /// nageant ou en tombant dessus) est projeté vers le haut, plus haut s'il garde le bouton de saut (comme les bâches
    /// du stage 5). Le jet est dessiné par programme : des gouttes qui montent et une gerbe en haut.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Fountain : MonoBehaviour
    {
        [Tooltip("Hauteur du jet au-dessus de la surface, en cases.")]
        public float height = 5f;
        [Tooltip("Vitesse donnée à Yao (et en gardant le bouton de saut), en cases/s.")]
        public float launch = 30f, launchHeld = 38f;
        public Material material;

        const int Drops = 26;
        SpriteRenderer[] drops;
        float[] phase;
        float cooldown;
        static Sprite dropSprite;

        void Awake()
        {
            var box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.1f, height + 1.5f);
            box.offset = new Vector2(0f, (height - 1.5f) / 2f);
            if (dropSprite == null) dropSprite = MakeDrop();
            drops = new SpriteRenderer[Drops];
            phase = new float[Drops];
            for (int i = 0; i < Drops; i++)
            {
                var go = new GameObject("Goutte");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = dropSprite;
                if (material != null) sr.sharedMaterial = material;
                sr.sortingLayerName = "Monde";
                sr.sortingOrder = 7;
                drops[i] = sr;
                phase[i] = (float)i / Drops;
            }
        }

        void Update()
        {
            float t = Time.time;
            cooldown -= Time.deltaTime;
            for (int i = 0; i < Drops; i++)
            {
                // Chaque goutte monte de la surface au sommet, s'écarte un peu, et grossit puis s'efface en haut.
                float f = Mathf.Repeat(phase[i] + t * 0.9f, 1f);
                float side = Mathf.Sin(i * 12.9898f) * 0.35f * f;
                float y = height * (1f - (1f - f) * (1f - f)); // ralentit en montant
                drops[i].transform.localPosition = new Vector3(side, y, 0f);
                float s = Mathf.Lerp(0.35f, 0.75f, f) * (i % 3 == 0 ? 1.3f : 1f);
                drops[i].transform.localScale = new Vector3(s * 0.6f, s, 1f);
                drops[i].color = new Color(0.85f, 0.95f, 1f, f < 0.85f ? 0.85f : (1f - f) / 0.15f * 0.85f);
            }
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (cooldown > 0f) return;
            var hero = other.GetComponentInParent<HeroController>();
            if (hero == null || hero.IsDying || hero.Velocity.y > launch * 0.6f) return;
            hero.Launch(launch, launchHeld);
            cooldown = 0.35f;
        }

        /// <summary>Goutte d'eau ronde, claire, aux bords doux (créée une fois).</summary>
        static Sprite MakeDrop()
        {
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * 3f);
                    float light = Mathf.Clamp01(1f - Mathf.Sqrt((dx + 0.3f) * (dx + 0.3f) + (dy - 0.3f) * (dy - 0.3f)) * 1.6f);
                    tex.SetPixel(x, y, new Color(0.75f + 0.25f * light, 0.9f + 0.1f * light, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        }
    }
}
