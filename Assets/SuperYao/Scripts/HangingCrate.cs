using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Caisse suspendue aux lianes (stage 8, comme sur le téléphone) : deux caisses côte à côte pendues à deux cordes, qui
    /// montent et descendent doucement au-dessus d'un marigot. On se pose dessus (dessus traversable par en dessous) ;
    /// elle emporte Yao (ICarrier). Les cordes s'allongent jusqu'au point d'attache, caché dans le feuillage.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class HangingCrate : MonoBehaviour, ICarrier
    {
        [Tooltip("Hauteurs du dessus de la caisse, en bas et en haut de sa course (cases).")]
        public float lowY = 1f, highY = 4f;
        [Tooltip("Durée d'un aller-retour (s) et décalage dans le cycle (0 à 1).")]
        public float period = 4f, phase;
        [Tooltip("Hauteur des points d'attache des cordes.")]
        public float anchorY = 10f;
        public Transform[] ropes;
        public float ropeSpriteHeight = 1f;

        public Vector2 Velocity { get; private set; }

        Rigidbody2D body;
        float time;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            time = phase * period;
            body.position = new Vector2(body.position.x, Height(time));
            transform.position = body.position;
        }

        float Height(float t) => Mathf.Lerp(lowY, highY, 0.5f - 0.5f * Mathf.Cos(t * 2f * Mathf.PI / period));

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            time += dt;
            var p = new Vector2(body.position.x, Height(time));
            Velocity = (p - body.position) / dt;
            body.MovePosition(p);
        }

        void LateUpdate()
        {
            if (ropes == null) return;
            float len = Mathf.Max(0.1f, anchorY - transform.position.y);
            foreach (var r in ropes)
            {
                if (r == null) continue;
                var s = r.localScale;
                s.y = len / ropeSpriteHeight;
                r.localScale = s;
            }
        }
    }
}
