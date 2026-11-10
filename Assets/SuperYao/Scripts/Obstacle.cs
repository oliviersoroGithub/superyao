using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Obstacle sur la route (bloc de béton du chantier) : plein, on peut sauter dessus ou par-dessus. Le percuter de
    /// face en skate, lancé, fait chuter Yao : il perd son skate (comme touché par un ennemi). À pied, il est arrêté.
    /// Si Yao se retrouve malgré tout dans le bloc (choc très rapide), il est repoussé du côté d'où il venait.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Obstacle : MonoBehaviour
    {
        [Tooltip("Vitesse (cases/s) au-delà de laquelle le choc en skate le fait tomber.")]
        public float crashSpeed = 4f;

        BoxCollider2D box;

        void Awake() => box = GetComponent<BoxCollider2D>();

        void OnCollisionEnter2D(Collision2D collision)
        {
            var hero = collision.collider.GetComponentInParent<HeroController>();
            if (hero == null || hero.IsDying) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                // Contact de côté (et pas en se posant dessus).
                if (Mathf.Abs(collision.GetContact(i).normal.x) < 0.7f) continue;
                if (hero.IsBig && Mathf.Abs(collision.relativeVelocity.x) > crashSpeed)
                {
                    SoundFx.Play("bump");
                    hero.Hurt();
                }
                return;
            }
        }

        void FixedUpdate()
        {
            // Sécurité : Yao à l'intérieur du bloc (pieds sous le dessus, au milieu) est remis à côté.
            var hero = HeroCache();
            if (hero == null || hero.IsDying) return;
            var body = hero.GetComponent<Rigidbody2D>();
            Vector2 local = transform.InverseTransformPoint(body.position);
            Vector2 half = box.size / 2f;
            Vector2 c = local - box.offset;
            if (Mathf.Abs(c.x) < half.x * 0.8f && c.y < half.y * 0.6f && c.y > -half.y)
            {
                float side = c.x < 0f ? -1f : 1f;
                var outside = transform.TransformPoint(box.offset + new Vector2(side * (half.x + 0.6f / Mathf.Abs(transform.lossyScale.x)), -half.y));
                body.position = new Vector2(outside.x, body.position.y);
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            }
        }

        static HeroController cached;
        static HeroController HeroCache()
        {
            if (cached == null) cached = FindAnyObjectByType<HeroController>();
            return cached;
        }
    }
}
