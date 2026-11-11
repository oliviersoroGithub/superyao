using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Grosse pierre qui dévale la pente (stage 10) : lâchée en haut de la montée par un BoulderSpawner, elle roule vers
    /// Yao en suivant le sol, accélère dans la descente, tourne sur elle-même. Elle blesse au contact ; on rebondit dessus
    /// en lui sautant dessus. Une boule de feu la casse ; arrivée en bas (ou arrêtée), elle se fend et éclate en morceaux
    /// et en poussière (images de l'utilisateur).
    /// </summary>
    public class Boulder : MonoBehaviour
    {
        public SpriteRenderer image;
        [Tooltip("Centre de la pierre (l'image y est accrochée) : c'est lui qui tourne.")]
        public Transform rotor;
        public Sprite[] breakFrames, debris, dust;
        public float radius = 0.8f, speed = 3f, maxSpeed = 9f, endX = -999f;
        public Material material;

        const float Gravity = 40f, BreakTime = 0.35f;
        float vy, broken = -1f, stuck, guard;
        bool grounded;
        static readonly RaycastHit2D[] hits = new RaycastHit2D[6];
        static ContactFilter2D solid = new ContactFilter2D { useTriggers = false };
        static HeroController heroCache;
        float lastY;

        void Start() => lastY = transform.position.y;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (broken >= 0f) { StepBreak(dt); return; }
            var p = transform.position;
            // Le sol sous la pierre (sans Yao, les ennemis ni les déclencheurs).
            float groundY = float.NegativeInfinity;
            int n = Physics2D.Raycast(new Vector2(p.x, p.y + radius + 0.6f), Vector2.down, solid, hits, radius + 1.6f);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c.usedByEffector || c.GetComponentInParent<HeroController>() != null || c.GetComponentInParent<Enemy>() != null || c.GetComponentInParent<Boulder>() != null) continue;
                groundY = hits[i].point.y;
                break;
            }
            p.x -= speed * dt; // elle dévale vers la gauche (vers Yao qui monte)
            if (groundY > float.NegativeInfinity && p.y <= groundY + 0.15f && vy <= 0f)
            {
                // Pente : elle accélère quand le sol descend, ralentit un peu sur le plat.
                float drop = lastY - groundY;
                speed = drop > 0.001f ? Mathf.Min(maxSpeed, speed + 9f * dt) : Mathf.Max(2.5f, speed - 1.5f * dt);
                p.y = groundY;
                vy = 0f;
                grounded = true;
            }
            else
            {
                vy -= Gravity * dt;
                p.y += vy * dt;
                grounded = false;
                if (p.y < -20f) { Destroy(gameObject); return; }
            }
            lastY = p.y;
            transform.position = p;
            if (rotor != null) rotor.Rotate(0f, 0f, speed * dt / radius * Mathf.Rad2Deg);
            stuck = speed < 0.4f && grounded ? stuck + dt : 0f;
            if (p.x < endX || stuck > 0.5f) { Break(); return; }
            HitHero(dt);
        }

        void HitHero(float dt)
        {
            if (guard > 0f) { guard -= dt; return; }
            if (heroCache == null) heroCache = FindAnyObjectByType<HeroController>();
            var hero = heroCache;
            if (hero == null || hero.IsDying) return;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var c = (Vector2)transform.position + Vector2.up * radius;
            var closest = new Vector2(Mathf.Clamp(c.x, b.min.x, b.max.x), Mathf.Clamp(c.y, b.min.y, b.max.y));
            if ((closest - c).sqrMagnitude > radius * radius * 0.8f) return;
            // Saut sur la pierre : Yao rebondit ; sinon elle l'écrase.
            bool falling = hero.PreviousVelocityY < 0.5f || hero.Velocity.y < 0f;
            if (falling && hero.PreviousFeetY >= c.y + radius * 0.4f) { hero.Bounce(); guard = 0.3f; }
            else { hero.Hurt(); guard = 0.6f; }
        }

        /// <summary>La pierre se fend et éclate (boule de feu, bas de la pente).</summary>
        public void Break()
        {
            if (broken >= 0f) return;
            broken = 0f;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            if (rotor != null) rotor.localRotation = Quaternion.identity;
            SoundFx.Play("break");
            // Morceaux qui volent et poussière.
            if (debris != null)
                for (int i = 0; i < 4 && debris.Length > 0; i++)
                {
                    var d = Piece(debris[(i * 3 + Random.Range(0, debris.Length)) % debris.Length], radius * 0.7f);
                    var flyer = d.gameObject.AddComponent<Debris>();
                    flyer.velocity = new Vector2(Random.Range(-5f, 5f), Random.Range(8f, 14f));
                    flyer.spin = Random.Range(-500f, 500f);
                }
            if (dust != null && dust.Length > 0)
            {
                var puff = Piece(dust[0], radius * 2.2f);
                puff.gameObject.AddComponent<Puff>().frames = dust;
            }
        }

        SpriteRenderer Piece(Sprite sprite, float size)
        {
            var go = new GameObject("Eclat");
            go.transform.position = transform.position + Vector3.up * radius * 0.6f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingLayerName = "Monde";
            sr.sortingOrder = 8;
            float s = size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            go.transform.localScale = new Vector3(s, s, 1f);
            return sr;
        }

        void StepBreak(float dt)
        {
            broken += dt;
            if (image != null && breakFrames != null && breakFrames.Length > 0)
            {
                image.sprite = breakFrames[Mathf.Min(breakFrames.Length - 1, Mathf.FloorToInt(broken / BreakTime * breakFrames.Length))];
                var c = image.color;
                c.a = Mathf.Clamp01(1f - (broken - BreakTime) / 0.2f);
                image.color = c;
            }
            if (broken > BreakTime + 0.25f) Destroy(gameObject);
        }

        /// <summary>Morceau de pierre qui vole, tourne, retombe et s'efface.</summary>
        class Debris : MonoBehaviour
        {
            public Vector2 velocity;
            public float spin;
            float age;
            SpriteRenderer sr;
            void Awake() => sr = GetComponent<SpriteRenderer>();
            void Update()
            {
                float dt = Time.deltaTime;
                age += dt;
                velocity.y -= 40f * dt;
                transform.position += (Vector3)(velocity * dt);
                transform.Rotate(0f, 0f, spin * dt);
                var c = sr.color;
                c.a = Mathf.Clamp01(1.2f - age);
                sr.color = c;
                if (age > 1.2f) Destroy(gameObject);
            }
        }

        /// <summary>Nuage de poussière (images de l'utilisateur) qui grossit et s'efface.</summary>
        class Puff : MonoBehaviour
        {
            public Sprite[] frames;
            float age;
            SpriteRenderer sr;
            void Awake() => sr = GetComponent<SpriteRenderer>();
            void Update()
            {
                age += Time.deltaTime;
                if (frames != null && frames.Length > 0) sr.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(age * 8f))];
                var c = sr.color;
                c.a = Mathf.Clamp01(1f - age / 0.6f);
                sr.color = c;
                if (age > 0.6f) Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// En haut de la montée (stage 10), lâche une grosse pierre de temps en temps (seulement quand Yao est assez près).
    /// </summary>
    public class BoulderSpawner : MonoBehaviour
    {
        public Sprite[] boulders, breakFrames, debris, dust;
        public Material material;
        [Tooltip("Délai moyen entre deux pierres (s), et x où elles éclatent (bas de la pente).")]
        public float interval = 3.4f, endX = -999f;
        float timer = 1f;

        void Update()
        {
            var cam = Camera.main;
            if (cam == null || boulders == null || boulders.Length == 0) return;
            float dx = transform.position.x - cam.transform.position.x;
            if (dx > cam.orthographicSize * cam.aspect + 14f || dx < -cam.orthographicSize * cam.aspect - 4f) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = interval * Random.Range(0.75f, 1.3f);
            Spawn();
        }

        void Spawn()
        {
            int k = Random.Range(0, boulders.Length);
            float radius = 0.55f + 0.25f * k; // trois tailles
            var go = new GameObject("Pierre_Qui_Roule");
            go.transform.position = transform.position;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = radius * 0.9f;
            col.offset = new Vector2(0f, radius);
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var img = new GameObject("Image").AddComponent<SpriteRenderer>();
            img.transform.SetParent(go.transform, false);
            img.transform.localPosition = new Vector3(0f, radius, 0f);
            img.sprite = boulders[k];
            if (material != null) img.sharedMaterial = material;
            img.sortingLayerName = "Monde";
            img.sortingOrder = 6;
            float s = radius * 2f / boulders[k].bounds.size.x;
            img.transform.localScale = new Vector3(s, s, 1f);
            // Pivot des images en bas : l'image est accrochée au centre de la pierre, qui tourne.
            var pivot = new GameObject("Centre").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(0f, radius, 0f);
            img.transform.SetParent(pivot, false);
            img.transform.localPosition = new Vector3(0f, -radius, 0f);
            var b = go.AddComponent<Boulder>();
            b.image = img;
            b.radius = radius;
            b.breakFrames = breakFrames;
            b.debris = debris;
            b.dust = dust;
            b.material = material;
            b.endX = endX;
            b.speed = 2.5f;
            b.rotor = pivot;
        }
    }
}
