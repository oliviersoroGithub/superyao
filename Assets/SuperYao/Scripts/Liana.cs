using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Liane (stage 6), pendue à une branche au-dessus d'une saignée inondée : elle se balance doucement. Yao en l'air
    /// qui la touche s'y accroche ; ◄ ► le font se balancer plus fort, le bouton de saut le lâche, lancé dans le sens du
    /// balancement (plus haut et plus loin au bon moment). Pendule simple : l'objet est au point d'attache, l'image
    /// (pivot en bas, tournée de 180° pour pendre) est un enfant tourné de l'angle du balancement.
    /// </summary>
    public class Liana : MonoBehaviour
    {
        [Tooltip("Longueur, en cases.")]
        public float length = 5.5f;
        [Tooltip("Balancement à vide, en degrés.")]
        public float idleAmplitude = 12f;
        public Transform visual;

        // Pesanteur du pendule (plus faible que celle de Yao : un balancement lent, lisible), poussée de ◄ ►, angle
        // maximal, et bond donné en lâchant.
        const float Gravity = 42f, Pump = 4.5f, ReleasePush = 6.5f, MaxAngle = 75f * Mathf.Deg2Rad, ReleaseUp = 10f, GrabReach = 0.7f;

        float angle, omega, grip;
        bool started;
        HeroController held;
        static HeroController heroCache;

        /// <summary>Position de la main de Yao, à <paramref name="r"/> cases du point d'attache.</summary>
        Vector2 Point(float r) => (Vector2)transform.position + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * r;

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (held == null)
            {
                // À vide : pendule libre, ramené peu à peu à son petit balancement (après que Yao l'a lâchée, ou au
                // départ, où il est lancé).
                float w = Mathf.Sqrt(Gravity / length);
                if (!started) { started = true; omega = idleAmplitude * Mathf.Deg2Rad * w; }
                omega += -Gravity / length * Mathf.Sin(angle) * dt;
                angle += omega * dt;
                float amp = Mathf.Sqrt(angle * angle + (omega / w) * (omega / w));
                if (amp > 0.001f)
                {
                    float k = Mathf.Lerp(1f, idleAmplitude * Mathf.Deg2Rad / amp, 0.6f * dt);
                    angle *= k;
                    omega *= k;
                }
                TryGrab();
            }
        }

        void LateUpdate()
        {
            if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 180f);
        }

        void TryGrab()
        {
            if (heroCache == null) heroCache = FindAnyObjectByType<HeroController>();
            var hero = heroCache;
            if (hero == null || !hero.CanGrab(this)) return;
            // Main de Yao : un peu sous le haut de sa tête. Elle doit toucher la moitié basse de la liane.
            var hand = (Vector2)hero.transform.position + Vector2.up * hero.HandHeight;
            var a = (Vector2)transform.position;
            var dir = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
            float along = Mathf.Clamp(Vector2.Dot(hand - a, dir), length * 0.45f, length);
            if (Vector2.Distance(a + dir * along, hand) > GrabReach) return;
            held = hero;
            grip = along;
            // L'élan de Yao passe au balancement.
            var tangent = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            omega = Mathf.Clamp(Vector2.Dot(hero.Velocity, tangent) / grip, -2.5f, 2.5f);
            hero.GrabLiana(this);
            SoundFx.Play("bump");
        }

        /// <summary>Un pas de balancement avec Yao accroché ; renvoie où placer ses pieds.</summary>
        public Vector2 Step(float dt, float push, float handHeight)
        {
            // Prendre de l'élan : ◄ ou ► pousse seulement quand la liane va déjà dans ce sens (comme sur une balançoire) ;
            // en gardant la direction voulue, chaque aller est plus ample. À contresens, on ne freine pas.
            float drive = push != 0 && omega * push > -0.3f ? push * Pump : 0f;
            float accel = -Gravity / grip * Mathf.Sin(angle) + drive;
            omega += accel * dt;
            omega *= 1f - 0.15f * dt;
            angle += omega * dt;
            if (Mathf.Abs(angle) > MaxAngle) { angle = Mathf.Sign(angle) * MaxAngle; omega = 0f; }
            return Point(grip) - Vector2.up * handHeight;
        }

        /// <summary>Yao lâche la liane : sa vitesse au moment de lâcher, plus un bond vers le haut ; s'il pousse ◄ ou ►, il
        /// part au moins à 6,5 cases/s de ce côté (il se projette vers la liane suivante ou la rive).</summary>
        public Vector2 Release(int push = 0)
        {
            var v = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (omega * grip) + Vector2.up * ReleaseUp;
            if (push != 0 && v.x * push < ReleasePush) v.x = push * ReleasePush;
            held = null;
            return v;
        }
    }
}
