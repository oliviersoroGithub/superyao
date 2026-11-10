using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Le skate (à la place de la noix de karité du téléphone) : il sort lentement du coffre, puis roule vers la droite
    /// et fait demi-tour contre un obstacle. Attrapé, Yao monte dessus (il le perd s'il est touché). Si Yao l'a déjà,
    /// c'est l'orbe de feu qui sort.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class PowerItem : MonoBehaviour
    {
        public float speed = 3f;
        public float emergeTime = 0.6f;
        [Tooltip("Orbe de feu : ce qui sort du coffre si Yao est déjà grand (comme sur le téléphone).")]
        public Sprite fireOrbSprite;
        [Tooltip("Taille de l'orbe de feu, en cases.")]
        public float fireOrbSize = 1f;

        bool isFireOrb;

        Rigidbody2D body;
        SpriteRenderer sr;
        HeroController hero;
        Vector3 start;
        float emerge = -1f;
        int direction = 1;
        float stuck;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            sr = GetComponent<SpriteRenderer>();
            body.simulated = false;
            if (sr != null) sr.enabled = false;
        }

        /// <summary>Sort du coffre (appelé par BumpBlock).</summary>
        public void Emerge(bool fireOrb = false)
        {
            isFireOrb = fireOrb && fireOrbSprite != null;
            if (isFireOrb && sr != null)
            {
                // L'orbe remplace l'image du skate : elle reprend sa propre taille, et sa boule de collision aussi.
                sr.sprite = fireOrbSprite;
                float s = fireOrbSize / fireOrbSprite.bounds.size.y;
                transform.localScale = new Vector3(s, s, 1f);
                var circle = GetComponent<CircleCollider2D>();
                circle.radius = 0.4f / s;
                circle.offset = new Vector2(0f, 0.42f / s);
            }
            transform.SetParent(null, true); // ne suit plus le coffre qui saute
            start = transform.position;
            emerge = 0f;
            if (sr != null) sr.enabled = true;
            hero = FindAnyObjectByType<HeroController>();
            if (hero != null)
                foreach (var c in hero.GetComponents<Collider2D>()) Physics2D.IgnoreCollision(GetComponent<Collider2D>(), c);
        }

        void Update()
        {
            if (emerge < 0f) return;
            if (emerge < emergeTime)
            {
                emerge += Time.deltaTime;
                transform.position = start + Vector3.up * Mathf.Min(1f, emerge / emergeTime);
                if (emerge >= emergeTime) body.simulated = true;
            }
            if (hero != null && Vector2.Distance(hero.transform.position + Vector3.up * 0.7f, transform.position + Vector3.up * 0.4f) < 0.9f)
            {
                StageState.Instance?.AddPower();
                GameProgress.Unlock(isFireOrb); // trouvé : il pourra s'acheter au marché
                hero.SetBig(true); // sur le skate (l'orbe de feu le donne aussi)
                if (isFireOrb) hero.GetComponent<HeroFire>()?.SetFire(true);
                gameObject.SetActive(false);
            }
        }

        void FixedUpdate()
        {
            if (!body.simulated) return;
            // Demi-tour quand un obstacle l'arrête.
            if (Mathf.Abs(body.linearVelocity.x) < 0.5f) stuck += Time.fixedDeltaTime; else stuck = 0f;
            if (stuck > 0.1f) { direction = -direction; stuck = 0f; }
            body.linearVelocity = new Vector2(direction * speed, body.linearVelocity.y);
            if (transform.position.y < -4f) gameObject.SetActive(false); // tombée dans la lagune
        }
    }
}
