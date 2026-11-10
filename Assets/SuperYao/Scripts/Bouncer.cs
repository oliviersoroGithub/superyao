using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Bâche d'étal qui fait rebondir (stage 5) : posée sur le haut de la bâche, une surface où l'on se pose par
    /// au-dessus (on passe sous la bâche sans la toucher). Yao qui retombe dessus est relancé vers le haut, plus haut
    /// s'il garde le bouton de saut ; la bâche s'écrase un instant.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Bouncer : MonoBehaviour
    {
        [Tooltip("Vitesse du rebond, et avec le bouton de saut tenu (cases/s). Le saut normal fait 33,4.")]
        public float speed = 30f, heldSpeed = 40f;
        [Tooltip("Image de la tente, écrasée un instant au rebond.")]
        public Transform visual;

        BoxCollider2D box;
        float squash = -1f, cooldown;
        Vector3 baseScale;

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            if (visual != null) baseScale = visual.localScale;
        }

        void OnCollisionEnter2D(Collision2D c) => TryBounce(c);
        void OnCollisionStay2D(Collision2D c) => TryBounce(c);

        void TryBounce(Collision2D c)
        {
            if (cooldown > 0f) return;
            var hero = c.collider.GetComponentInParent<HeroController>();
            if (hero == null || hero.IsDying) return;
            // Seulement en se posant dessus (pieds au-dessus de la bâche, en descente).
            if (hero.PreviousFeetY < box.bounds.max.y - 0.35f || hero.PreviousVelocityY > 0.5f) return;
            hero.Launch(speed, heldSpeed);
            cooldown = 0.2f;
            squash = 0f;
        }

        void Update()
        {
            if (cooldown > 0f) cooldown -= Time.deltaTime;
            if (squash < 0f || visual == null) return;
            squash += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(squash / 0.3f) * Mathf.PI);
            visual.localScale = new Vector3(baseScale.x * (1f + 0.05f * k), baseScale.y * (1f - 0.12f * k), 1f);
            if (squash > 0.3f) { visual.localScale = baseScale; squash = -1f; }
        }
    }
}
