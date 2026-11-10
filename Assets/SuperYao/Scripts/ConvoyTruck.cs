using System.Collections.Generic;
using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Camion du convoi (stage 6) : il roule vers la droite et traverse la route inondée, où Yao à pied se noierait. On ne
    /// se cogne pas dedans : seul son toit (ou son chargement) porte Yao, qui est emporté (ICarrier).
    /// Il ne disparaît jamais sous les yeux du joueur :
    /// - passé <see cref="toX"/> (de l'autre côté de l'eau), s'il porte Yao, il freine et s'arrête pour le déposer ;
    /// - sans Yao, il accélère et s'éloigne ; une fois sorti de l'écran par la droite, il revient hors de l'écran par la
    ///   gauche (au plus tard en <see cref="fromX"/>) pour un nouveau passage ;
    /// - il ralentit derrière un camion arrêté au lieu de le traverser ;
    /// - arrivé à destination (il s'en va sans Yao), il passe derrière la brume du décor et ne porte plus personne ;
    ///   revenu par la gauche, il redevient un camion du convoi.
    /// Les roues tremblotent un peu sur la route.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class ConvoyTruck : MonoBehaviour, ICarrier
    {
        [Tooltip("Retour des camions (x du centre au plus tard), et fin du passage : au-delà, il dépose Yao ou s'en va.")]
        public float fromX, toX;
        [Tooltip("Vitesse sur la route, en cases par seconde.")]
        public float speed = 4.5f;
        [Tooltip("Avance au départ, en cases depuis fromX (pour espacer les camions).")]
        public float offset;
        public SpriteRenderer visual;

        public Vector2 Velocity { get; private set; }

        const float LeaveSpeed = 9f, Brake = 6f, Accel = 3f, Gap = 1.5f;
        static readonly List<ConvoyTruck> all = new List<ConvoyTruck>();
        Rigidbody2D body;
        Collider2D top;
        float baseY, current, halfWidth, dropTime;
        bool arrived;
        int sortingLayer, sortingOrder;
        static Collider2D heroBox;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            top = GetComponent<Collider2D>();
            baseY = body.position.y;
            body.position = new Vector2(fromX + offset, baseY);
            transform.position = body.position;
            current = speed;
            halfWidth = visual != null ? visual.bounds.extents.x : 4f;
            if (visual != null) { sortingLayer = visual.sortingLayerID; sortingOrder = visual.sortingOrder; }
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        bool CarriesHero()
        {
            if (heroBox == null)
            {
                var hero = FindAnyObjectByType<HeroController>();
                if (hero != null) heroBox = hero.GetComponent<Collider2D>();
            }
            return heroBox != null && top != null && top.IsTouching(heroBox);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float x = body.position.x;
            float target = speed;
            if (x >= toX)
            {
                // De l'autre côté : il dépose Yao (arrêt, puis un instant après qu'il est descendu), sinon il s'en va.
                bool carrying = CarriesHero();
                dropTime = carrying ? 0f : dropTime + dt;
                target = carrying || dropTime < 0.6f && current < 0.5f ? 0f : LeaveSpeed;
                if (target > 0f && !carrying && !arrived) SetArrived(true);
            }
            // Pas à travers le camion de devant (arrêté ou plus lent).
            foreach (var other in all)
            {
                if (other == this) continue;
                float ahead = other.body.position.x - x;
                if (ahead > 0f && ahead < halfWidth + other.halfWidth + Gap) target = Mathf.Min(target, other.current * 0.9f);
            }
            current = Mathf.MoveTowards(current, target, (target < current ? Brake : Accel) * dt);

            // Sorti de l'écran par la droite, sans Yao : il revient hors de l'écran par la gauche.
            var cam = Camera.main;
            if (cam != null && x > toX)
            {
                float half = cam.orthographicSize * cam.aspect;
                float camLeft = cam.transform.position.x - half, camRight = cam.transform.position.x + half;
                if (x - halfWidth > camRight + 1f && !CarriesHero())
                {
                    float back = Mathf.Min(fromX, camLeft - halfWidth - 2f);
                    foreach (var other in all) // pas sur un camion qui attend déjà son tour
                        if (other != this && Mathf.Abs(other.body.position.x - back) < halfWidth + other.halfWidth + Gap)
                            back = other.body.position.x - (halfWidth + other.halfWidth + Gap);
                    body.position = new Vector2(back, baseY);
                    transform.position = body.position;
                    current = speed;
                    dropTime = 0f;
                    SetArrived(false);
                    Velocity = Vector2.zero;
                    return;
                }
            }
            Velocity = new Vector2(current, 0f);
            body.MovePosition(new Vector2(x + current * dt, baseY));
        }

        /// <summary>Arrivé à destination : décor flou (calque du plan intermédiaire, sous la brume), plus de toit où se poser.</summary>
        void SetArrived(bool on)
        {
            arrived = on;
            if (top != null) top.enabled = !on;
            if (visual == null) return;
            if (on)
            {
                visual.sortingLayerName = "PlanIntermediaire";
                visual.sortingOrder = 9000 + sortingOrder;
            }
            else
            {
                visual.sortingLayerID = sortingLayer;
                visual.sortingOrder = sortingOrder;
            }
        }

        void Update()
        {
            if (visual == null) return;
            float bump = current > 0.2f ? Mathf.Abs(Mathf.Sin(Time.time * 9f + offset)) * 0.03f : 0f;
            visual.transform.localPosition = new Vector3(0f, bump, 0f);
        }
    }
}
