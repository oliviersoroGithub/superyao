using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Cascade (stage 10) : l'eau tombe (4 images en boucle) dans le bassin, avec une gerbe d'écume en bas (4 images). Yao
    /// qui passe dans la chute est plaqué vers le bas : il ne peut pas la traverser en sautant, et dans l'eau il est poussé
    /// vers le fond. Derrière la cascade (dessinée devant Yao), un passage secret mène à un diamant.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Waterfall : MonoBehaviour
    {
        public SpriteRenderer fall, splash;
        public Sprite[] fallFrames, splashFrames;
        [Tooltip("Vitesse de chute imposée à Yao dans la cascade (cases/s).")]
        public float push = 9f;

        void Update()
        {
            float t = Time.time;
            if (fall != null && fallFrames != null && fallFrames.Length > 0) fall.sprite = fallFrames[Mathf.FloorToInt(t * 10f) % fallFrames.Length];
            if (splash != null && splashFrames != null && splashFrames.Length > 0) splash.sprite = splashFrames[Mathf.FloorToInt(t * 9f) % splashFrames.Length];
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var hero = other.GetComponentInParent<HeroController>();
            if (hero == null || hero.IsDying) return;
            var body = hero.GetComponent<Rigidbody2D>();
            var v = body.linearVelocity;
            // Plaqué vers le bas ; il avance moins vite sous l'eau qui tombe.
            if (v.y > -push) v.y = Mathf.Min(v.y, -push * (hero.Swimming ? 0.45f : 1f));
            v.x *= 0.9f;
            body.linearVelocity = v;
        }
    }
}
