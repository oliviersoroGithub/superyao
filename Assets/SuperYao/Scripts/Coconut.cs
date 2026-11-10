using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Noix de coco lancée par un singe (téléphone, spells.ts) : elle part en cloche vers Yao, tourne sur elle-même,
    /// blesse Yao si elle le touche et éclate en touchant le sol (ou au bout de 3,7 s). Valeurs du téléphone converties :
    /// pesanteur 0,26 px/image² (58,5 cases/s²).
    /// </summary>
    public class Coconut : MonoBehaviour
    {
        public Vector2 velocity;
        public Sprite[] frames;

        const float Gravity = 58.5f, MaxFall = 41f, Life = 220f / 60f, Radius = 0.3f, BreakTime = 0.25f;
        SpriteRenderer sr;
        float age, broken = -1f;
        static HeroController heroCache;
        static readonly RaycastHit2D[] hits = new RaycastHit2D[4];
        static ContactFilter2D solid = new ContactFilter2D { useTriggers = false };

        void Awake() => sr = GetComponentInChildren<SpriteRenderer>(); // l'image est un enfant centré : elle tourne autour de son milieu

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (broken >= 0f)
            {
                // Éclats : les deux dernières images, puis elle disparaît.
                broken += dt;
                if (frames != null && frames.Length >= 4) sr.sprite = frames[broken < BreakTime / 2f ? 2 : 3];
                transform.rotation = Quaternion.identity;
                if (broken > BreakTime) Destroy(gameObject);
                return;
            }
            if (age > Life) { Destroy(gameObject); return; }
            velocity.y = Mathf.Max(-MaxFall, velocity.y - Gravity * dt);
            var step = velocity * dt;
            var p = (Vector2)transform.position;
            // Sol (pas les plates-formes traversables quand elle monte) : elle éclate.
            if (velocity.y < 0f)
            {
                int n = Physics2D.CircleCast(p, Radius * 0.6f, step.normalized, solid, hits, step.magnitude);
                for (int i = 0; i < n; i++)
                {
                    var c = hits[i].collider;
                    if (c.GetComponentInParent<HeroController>() != null || c.GetComponentInParent<Enemy>() != null) continue;
                    if (hits[i].normal.y < 0.5f) continue;
                    transform.position = hits[i].centroid;
                    Break();
                    return;
                }
            }
            transform.position = p + step;
            transform.Rotate(0f, 0f, -Mathf.Sign(velocity.x) * 540f * dt);
            if (frames != null && frames.Length > 0) sr.sprite = frames[0];
            if (heroCache == null) heroCache = FindAnyObjectByType<HeroController>();
            var hero = heroCache;
            if (hero == null || hero.IsDying) return;
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            box.Expand(Radius * 2f);
            if (box.Contains(new Vector3(transform.position.x, transform.position.y, box.center.z)))
            {
                hero.Hurt();
                Break();
            }
        }

        void Break()
        {
            broken = 0f;
            SoundFx.Play("bump");
        }
    }
}
