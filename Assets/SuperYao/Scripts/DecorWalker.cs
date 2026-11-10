using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Passant animé du décor (stage 5 : la vendeuse au plateau de fruits) : il fait des allers-retours entre deux
    /// abscisses, au pas, derrière Yao, sans collision.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class DecorWalker : MonoBehaviour
    {
        public Sprite[] frames;
        [Tooltip("Bornes de la promenade (x, en cases) et vitesse (cases/s).")]
        public float fromX, toX, speed = 1.1f;
        [Tooltip("Vrai si les images regardent vers la droite.")]
        public bool facesRight = true;
        [Tooltip("Images par seconde de la marche.")]
        public float fps = 8f;

        SpriteRenderer sr;
        int dir = 1;
        float clock;

        void Awake() => sr = GetComponent<SpriteRenderer>();

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            clock += dt;
            var p = transform.localPosition;
            p.x += dir * speed * dt;
            if (p.x > toX) { p.x = toX; dir = -1; }
            else if (p.x < fromX) { p.x = fromX; dir = 1; }
            transform.localPosition = p;
            if (frames != null && frames.Length > 0) sr.sprite = frames[Mathf.FloorToInt(clock * fps) % frames.Length];
            sr.flipX = facesRight ? dir < 0 : dir > 0;
        }
    }
}
