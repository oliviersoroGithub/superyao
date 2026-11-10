using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Plateforme qui fait la navette entre deux points (la pirogue de la lagune) : elle part, s'arrête un instant à
    /// chaque bout, puis repart. Le héros posé dessus est emporté (voir HeroController). Le pêcheur pagaie pendant
    /// le trajet, et la pirogue se retourne dans le sens de la marche.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class MovingPlatform : MonoBehaviour, ICarrier
    {
        [Tooltip("Bouts du trajet (x du centre), en cases.")]
        public float fromX, toX;
        [Tooltip("Vitesse, en cases par seconde.")]
        public float speed = 2.5f;
        [Tooltip("Arrêt à chaque bout, en secondes.")]
        public float pause = 1f;

        [Header("Image")]
        public SpriteRenderer visual;
        public Sprite[] frames;
        public float framesPerSecond = 6f;

        /// <summary>Vitesse actuelle, transmise au héros posé dessus.</summary>
        public Vector2 Velocity { get; private set; }

        Rigidbody2D body;
        int direction = 1;
        float wait, animTime;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            wait = pause;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            var p = body.position;
            if (wait > 0f)
            {
                wait -= dt;
                Velocity = Vector2.zero;
                return;
            }
            float target = direction > 0 ? toX : fromX;
            float x = Mathf.MoveTowards(p.x, target, speed * dt);
            Velocity = new Vector2((x - p.x) / dt, 0f);
            body.MovePosition(new Vector2(x, p.y));
            if (Mathf.Approximately(x, target))
            {
                direction = -direction;
                wait = pause;
            }
        }

        void Update()
        {
            if (visual == null) return;
            visual.flipX = direction < 0; // l'image regarde vers la droite
            if (frames == null || frames.Length == 0) return;
            if (Mathf.Abs(Velocity.x) > 0.01f) animTime += Time.deltaTime; // le pêcheur ne pagaie qu'en route
            visual.sprite = frames[Mathf.FloorToInt(animTime * framesPerSecond) % frames.Length];
        }
    }
}
