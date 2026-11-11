using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Lumières de la forêt la nuit (stage 8), dessinées par programme : des lucioles qui volettent dans une zone, et des
    /// champignons qui luisent au pied des arbres. Elles s'allument avec la nuit (DayNight.Night) et ne prennent pas la
    /// teinte du soir (noms « Nuit_ »).
    /// </summary>
    public class NightLights : MonoBehaviour
    {
        [Tooltip("Zone des lucioles (x0, y0, largeur, hauteur), en cases.")]
        public Rect area = new Rect(0f, 0.5f, 100f, 7f);
        public int fireflies = 40;
        [Tooltip("Champignons lumineux : (x, y) de leur pied.")]
        public Vector2[] mushrooms;
        public Material material;

        SpriteRenderer[] flies, mushroomGlow;
        SpriteRenderer[] mushroomCaps;
        Vector2[] flyBase;
        float[] flySeed;
        static Sprite glow, cap;

        void Awake()
        {
            if (glow == null) glow = MakeGlow();
            if (cap == null) cap = MakeMushroom();
            flies = new SpriteRenderer[fireflies];
            flyBase = new Vector2[fireflies];
            flySeed = new float[fireflies];
            var rng = new System.Random(7);
            for (int i = 0; i < fireflies; i++)
            {
                flies[i] = Renderer($"Nuit_Luciole_{i}", glow, 72);
                flyBase[i] = new Vector2(area.x + (float)rng.NextDouble() * area.width, area.y + (float)rng.NextDouble() * area.height);
                flySeed[i] = (float)rng.NextDouble() * 100f;
                float s = 0.25f + 0.15f * (float)rng.NextDouble();
                flies[i].transform.localScale = new Vector3(s, s, 1f);
            }
            int n = mushrooms != null ? mushrooms.Length : 0;
            mushroomCaps = new SpriteRenderer[n];
            mushroomGlow = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var c = Renderer($"Nuit_Champignon_{i}", cap, 6);
                c.transform.position = new Vector3(mushrooms[i].x, mushrooms[i].y, 0f);
                float s = 0.45f + 0.25f * ((i * 37) % 10) / 10f;
                c.transform.localScale = new Vector3(s, s, 1f);
                mushroomCaps[i] = c;
                var g = Renderer($"Nuit_Halo_Champignon_{i}", glow, 5);
                g.transform.position = new Vector3(mushrooms[i].x, mushrooms[i].y + 0.35f * s, 0f);
                g.transform.localScale = new Vector3(2.4f * s, 1.6f * s, 1f);
                mushroomGlow[i] = g;
            }
        }

        SpriteRenderer Renderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingLayerName = "Monde";
            sr.sortingOrder = order;
            return sr;
        }

        void Update()
        {
            float night = DayNight.Night, t = Time.time;
            for (int i = 0; i < flies.Length; i++)
            {
                float k = flySeed[i];
                var p = flyBase[i] + new Vector2(Mathf.Sin(t * 0.37f + k) * 1.6f + Mathf.Sin(t * 0.9f + k * 2f) * 0.4f, Mathf.Sin(t * 0.53f + k * 1.3f) * 0.8f);
                flies[i].transform.position = new Vector3(p.x, p.y, 0f);
                float blink = 0.5f + 0.5f * Mathf.Sin(t * (2f + (k % 3f)) + k);
                flies[i].color = new Color(0.85f, 1f, 0.45f, night * blink);
                flies[i].enabled = night > 0.01f;
            }
            for (int i = 0; i < mushroomCaps.Length; i++)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(t * 1.5f + i);
                // Le jour, un champignon pâle ; la nuit, il luit en bleu-vert.
                mushroomCaps[i].color = Color.Lerp(new Color(0.75f, 0.72f, 0.65f), new Color(0.55f, 1f, 0.9f), night);
                mushroomGlow[i].color = new Color(0.4f, 1f, 0.85f, night * 0.6f * pulse);
                mushroomGlow[i].enabled = night > 0.01f;
            }
        }

        /// <summary>Halo rond et doux (blanc, teinté par la couleur).</summary>
        static Sprite MakeGlow()
        {
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        }

        /// <summary>Petit champignon (chapeau arrondi, pied clair), pivot au pied.</summary>
        static Sprite MakeMushroom()
        {
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float fx = (x + 0.5f) / N * 2f - 1f, fy = (y + 0.5f) / N;
                    Color c = Color.clear;
                    if (Mathf.Abs(fx) < 0.16f && fy < 0.55f) c = new Color(0.95f, 0.93f, 0.85f, 1f);       // pied
                    float capY = (fy - 0.5f) / 0.4f;
                    if (capY >= 0f && fx * fx + capY * capY < 1f) c = new Color(1f, 1f, 1f, 1f);           // chapeau
                    if (c.a > 0f && capY >= 0f && fx * fx + capY * capY < 1f && ((x * 7 + y * 3) % 11 == 0)) c = new Color(0.8f, 0.85f, 0.8f, 1f);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0f), N);
        }
    }
}
