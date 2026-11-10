using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Balançoire (stage 6) : un siège en bois pendu à deux cordes sous une branche, qui se balance seul au-dessus d'une
    /// saignée inondée. Le siège reste à plat (deux cordes parallèles) ; Yao posé dessus est emporté (ICarrier).
    /// Le composant est sur le siège (corps cinématique, dessus traversable par en dessous) ; les cordes sont tournées
    /// autour de leurs points d'attache.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SwingSeat : MonoBehaviour, ICarrier
    {
        [Tooltip("Point d'attache (milieu des deux cordes), en cases.")]
        public Vector2 anchor;
        [Tooltip("Longueur des cordes, en cases.")]
        public float length = 6f;
        [Tooltip("Amplitude du balancement, en degrés, et durée d'un aller-retour, en secondes.")]
        public float amplitude = 32f, period = 3.2f;
        [Tooltip("Cordes (pivot en bas de l'image, tournées de 180° pour pendre) et leur écart horizontal au milieu.")]
        public Transform[] ropes;
        public float[] ropeOffsets;

        public Vector2 Velocity { get; private set; }

        Rigidbody2D body;
        float time;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.position = Seat(0f);
            transform.position = body.position;
        }

        float Angle(float t) => amplitude * Mathf.Deg2Rad * Mathf.Sin(t * 2f * Mathf.PI / period);
        Vector2 Seat(float t)
        {
            float a = Angle(t);
            return anchor + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * length;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            time += dt;
            var p = Seat(time);
            Velocity = (p - body.position) / dt;
            body.MovePosition(p);
        }

        void LateUpdate()
        {
            if (ropes == null) return;
            float a = Angle(time);
            for (int i = 0; i < ropes.Length; i++)
            {
                if (ropes[i] == null) continue;
                ropes[i].position = new Vector3(anchor.x + ropeOffsets[i], anchor.y, ropes[i].position.z);
                ropes[i].rotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 180f);
            }
        }
    }
}
