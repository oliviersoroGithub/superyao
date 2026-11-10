using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Nuages vivants : chaque nuage enfant dérive seul vers la gauche, à sa propre vitesse (les plus petits, plus
    /// lointains, vont moins vite). Quand un nuage quitte l'écran, il est recyclé de l'autre côté à une nouvelle
    /// hauteur : quelques nuages suffisent pour tout le stage, sans rien créer pendant le jeu.
    /// À poser sur la couche des nuages (avec ParallaxLayer, facteur ≈ 0,05 et facteur vertical 0).
    /// Les hauteurs sont relatives au centre de l'écran.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public class CloudDrift : MonoBehaviour
    {
        [Tooltip("Vitesse de dérive du plus gros nuage, en cases par seconde.")]
        public float windSpeed = 0.35f;
        [Tooltip("Hauteurs possibles, en cases par rapport au centre de l'écran.")]
        public float minY = 1.8f, maxY = 5.6f;
        [Tooltip("Marge hors de l'écran avant recyclage, en cases.")]
        public float margin = 1.5f;
        public Camera targetCamera;

        Transform[] clouds;
        float[] speeds;
        float largest;

        void Start()
        {
            int n = transform.childCount;
            clouds = new Transform[n];
            speeds = new float[n];
            for (int i = 0; i < n; i++)
            {
                clouds[i] = transform.GetChild(i);
                largest = Mathf.Max(largest, Width(clouds[i]));
            }
            for (int i = 0; i < n; i++) speeds[i] = windSpeed * Mathf.Lerp(0.45f, 1f, Width(clouds[i]) / largest);
        }

        void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null || clouds == null) return;
            float camX = targetCamera.transform.position.x;
            float half = targetCamera.orthographicSize * targetCamera.aspect + margin;
            for (int i = 0; i < clouds.Length; i++)
            {
                var t = clouds[i];
                t.localPosition += Vector3.left * (speeds[i] * Time.deltaTime);
                float w = Width(t) / 2f, x = t.position.x;
                if (x + w < camX - half) Recycle(t, camX + half + w);
                else if (x - w > camX + half) Recycle(t, camX - half - w);
            }
        }

        void Recycle(Transform t, float worldX)
        {
            var p = t.localPosition;
            p.x += worldX - t.position.x;
            p.y = Random.Range(minY, maxY);
            t.localPosition = p;
        }

        static float Width(Transform t)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            return sr != null && sr.sprite != null ? sr.sprite.bounds.size.x * Mathf.Abs(t.lossyScale.x) : 1f;
        }
    }
}
