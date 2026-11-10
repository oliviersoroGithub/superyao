using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Vie du ciel : de temps en temps un petit vol d'oiseaux traverse l'écran en battant des ailes, et un avion
    /// passe au loin, derrière les nuages. Tout est créé d'avance par le constructeur du stage (aucun objet créé
    /// pendant le jeu) : les oiseaux et l'avion sont recyclés à chaque passage.
    ///
    /// Positions en « écran » : x par rapport au centre de la caméra, y par rapport au centre de l'écran (ils restent
    /// dans le ciel quand la caméra monte). Comme les autres couches lointaines, ils défilent un peu avec la caméra
    /// (vitesse apparente <see cref="factor"/>, voir ParallaxLayer), en plus de leur propre vol vers la droite.
    /// </summary>
    [DefaultExecutionOrder(220)]
    public class SkyTraffic : MonoBehaviour
    {
        public Camera targetCamera;
        [Tooltip("Vitesse apparente avec la caméra (0 = fixe à l'écran, comme le ciel ; les nuages sont à 0,05).")]
        public float factor = 0.08f;

        [Header("Oiseaux")]
        public SpriteRenderer[] birds;
        public Sprite[] birdFrames;
        public float birdFps = 14f;
        [Tooltip("Vitesse de vol, en cases par seconde.")]
        public float birdSpeed = 3.2f;
        [Tooltip("Attente entre deux vols, en secondes (min, max).")]
        public Vector2 birdDelay = new Vector2(5f, 14f);
        public float birdMinY = 1.5f, birdMaxY = 4.5f;

        [Header("Avions")]
        public SpriteRenderer plane;
        public Sprite[] planeSprites;
        [Tooltip("Largeur à l'écran de chaque avion, en cases (ils sont loin).")]
        public float[] planeWidths = { 3.6f, 2.4f, 1.5f };
        [Tooltip("Vitesse de chaque avion, en cases par seconde.")]
        public float[] planeSpeeds = { 1.6f, 2.0f, 1.2f };
        [Tooltip("Attente entre deux avions, en secondes (min, max).")]
        public Vector2 planeDelay = new Vector2(12f, 30f);
        public float planeMinY = 3.8f, planeMaxY = 5.8f;

        const float Margin = 2f;
        Vector2[] birdOffsets;
        float[] birdPhase;
        Vector2 flock, planePos;
        float flockWait, planeWait, planeSpeed, lastCamX, time;
        bool flockFlying, planeFlying, started;

        void Start()
        {
            birdOffsets = new Vector2[birds.Length];
            birdPhase = new float[birds.Length];
            for (int i = 0; i < birds.Length; i++)
            {
                // Vol en V lâche, chaque oiseau avec son propre battement.
                birdOffsets[i] = new Vector2(-0.9f * i, (i % 2 == 0 ? 0.25f : -0.3f) * i);
                birdPhase[i] = Random.value * birdFrames.Length;
                birds[i].enabled = false;
            }
            if (plane != null) plane.enabled = false;
            flockWait = Random.Range(1f, 4f);
            planeWait = Random.Range(planeDelay.x * 0.5f, planeDelay.y * 0.5f);
        }

        void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;
            var cam = targetCamera.transform.position;
            float dt = Time.deltaTime;
            float halfW = targetCamera.orthographicSize * targetCamera.aspect;
            if (!started) { lastCamX = cam.x; started = true; }
            float drift = -(cam.x - lastCamX) * factor; // défilement apparent avec la caméra
            lastCamX = cam.x;
            time += dt;

            UpdateBirds(cam, dt, drift, halfW);
            UpdatePlane(cam, dt, drift, halfW);
        }

        void UpdateBirds(Vector3 cam, float dt, float drift, float halfW)
        {
            if (birds == null || birds.Length == 0 || birdFrames == null || birdFrames.Length == 0) return;
            if (!flockFlying)
            {
                flockWait -= dt;
                if (flockWait > 0f) return;
                flockFlying = true;
                flock = new Vector2(-halfW - Margin, Random.Range(birdMinY, birdMaxY));
                foreach (var b in birds) b.enabled = true;
            }
            flock.x += birdSpeed * dt + drift;
            for (int i = 0; i < birds.Length; i++)
            {
                var p = flock + birdOffsets[i];
                p.y += Mathf.Sin(time * 2.2f + i * 1.7f) * 0.15f; // léger roulis
                birds[i].transform.position = new Vector3(cam.x + p.x, cam.y + p.y, 0f);
                int frame = Mathf.FloorToInt(time * birdFps + birdPhase[i]) % birdFrames.Length;
                birds[i].sprite = birdFrames[frame];
            }
            // Tout le vol est sorti à droite (le dernier oiseau est le plus à gauche).
            if (flock.x + birdOffsets[birds.Length - 1].x > halfW + Margin)
            {
                flockFlying = false;
                flockWait = Random.Range(birdDelay.x, birdDelay.y);
                foreach (var b in birds) b.enabled = false;
            }
        }

        void UpdatePlane(Vector3 cam, float dt, float drift, float halfW)
        {
            if (plane == null || planeSprites == null || planeSprites.Length == 0) return;
            if (!planeFlying)
            {
                planeWait -= dt;
                if (planeWait > 0f) return;
                int k = Random.Range(0, planeSprites.Length);
                plane.sprite = planeSprites[k];
                float width = k < planeWidths.Length ? planeWidths[k] : 2f;
                float s = width / planeSprites[k].bounds.size.x;
                plane.transform.localScale = new Vector3(s, s, 1f);
                planeSpeed = k < planeSpeeds.Length ? planeSpeeds[k] : 1.5f;
                planePos = new Vector2(-halfW - width, Random.Range(planeMinY, planeMaxY));
                plane.enabled = planeFlying = true;
            }
            planePos.x += planeSpeed * dt + drift;
            plane.transform.position = new Vector3(cam.x + planePos.x, cam.y + planePos.y, 0f);
            if (planePos.x - plane.bounds.size.x > halfW + Margin)
            {
                plane.enabled = planeFlying = false;
                planeWait = Random.Range(planeDelay.x, planeDelay.y);
            }
        }
    }
}
