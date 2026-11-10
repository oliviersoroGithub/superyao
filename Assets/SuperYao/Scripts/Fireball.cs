using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Boule de feu lancée par Yao (version téléphone, spells.ts) : elle file tout droit, s'éteint contre un obstacle
    /// ou au bout de 70 images, et éjecte l'ennemi qu'elle touche. Deux boules préparées d'avance, réutilisées.
    /// </summary>
    public class Fireball : MonoBehaviour
    {
        public const float Speed = 5.2f * 3.75f, Life = 70f / 60f, Radius = 0.25f;

        public bool Active { get; private set; }
        SpriteRenderer sr;
        float vx, life;
        readonly Collider2D[] hits = new Collider2D[8];
        ContactFilter2D filter;

        void Awake()
        {
            // L'image est un enfant décalé pour que la boule (pas la traînée) soit au centre de l'objet.
            sr = GetComponentInChildren<SpriteRenderer>();
            sr.enabled = false;
            filter = new ContactFilter2D { useTriggers = true };
        }

        public void Launch(Vector2 position, int direction, float heroSpeed)
        {
            transform.position = position;
            vx = direction * Speed + heroSpeed * 0.4f;
            transform.localScale = new Vector3(direction < 0 ? -1f : 1f, 1f, 1f); // l'image file vers la droite
            life = Life;
            Active = sr.enabled = true;
        }

        void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            transform.position += Vector3.right * (vx * dt);
            life -= dt;
            var cam = Camera.main;
            bool offScreen = cam != null && Mathf.Abs(transform.position.x - cam.transform.position.x) > cam.orthographicSize * cam.aspect + 2f;
            if (life <= 0f || offScreen) { Stop(); return; }

            int n = Physics2D.OverlapCircle(transform.position, Radius, filter, hits);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i];
                if (c.GetComponentInParent<HeroController>() != null) continue;
                // Le boss encaisse la boule de feu (un point de vie) sauf pendant son clignotement.
                var boss = c.GetComponentInParent<TollBoss>();
                if (boss != null)
                {
                    if (boss.FireHit()) { Stop(); return; }
                    continue;
                }
                var gnambro = c.GetComponentInParent<GnambroBoss>();
                if (gnambro != null)
                {
                    if (gnambro.FireHit()) { Stop(); return; }
                    continue;
                }
                var enemy = c.GetComponentInParent<Enemy>();
                if (enemy != null)
                {
                    if (enemy.Knock(vx > 0f ? 1 : -1)) { Stop(); return; }
                    continue;
                }
                // Obstacle plein (sol, pierre, bloc) ; les pièces, plateformes à sens unique et pirogues ne l'arrêtent pas.
                if (!c.isTrigger && !c.usedByEffector) { SoundFx.Play("bump"); Stop(); return; }
            }
        }

        void Stop() => Active = sr.enabled = false;
    }
}
