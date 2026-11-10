using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Pouvoir du feu (orbe de feu) : avec le bouton « feu », Yao lance une boule de feu devant lui, à mi-hauteur
    /// (au ras du sol s'il est accroupi). Règles du téléphone : 2 boules au plus à l'écran, une toutes les 12 images.
    /// Le pouvoir se perd quand Yao est touché (il redevient petit).
    /// </summary>
    [RequireComponent(typeof(HeroController), typeof(HeroInput))]
    public class HeroFire : MonoBehaviour
    {
        public Fireball[] fireballs;
        public const float Cooldown = 12f / 60f;

        public bool HasFire { get; private set; }
        /// <summary>Temps depuis le dernier tir, en secondes (pour la pose « coup de poing »).</summary>
        public float ShotTime { get; private set; } = 99f;

        HeroController hero;
        HeroInput input;

        void Awake()
        {
            hero = GetComponent<HeroController>();
            input = GetComponent<HeroInput>();
            foreach (var f in fireballs) f.transform.SetParent(null, true); // elles ne suivent pas le héros
        }

        public void SetFire(bool fire) => HasFire = fire;

        void Start()
        {
            // Yao commence le stage comme il a fini le précédent (ou avec ce qu'il a acheté au marché).
            if (!GameProgress.HasSave) return;
            if (GameProgress.Big || GameProgress.Fire) hero.SetBig(true);
            if (GameProgress.Fire) SetFire(true);
        }

        void Update()
        {
            ShotTime += Time.deltaTime;
            if (!input.ConsumeFirePressed()) return;
            var stage = StageState.Instance;
            if (!HasFire || hero.IsDying || !hero.IsBig || ShotTime < Cooldown || (stage != null && (stage.Won || stage.Paused))) return;
            foreach (var f in fireballs)
            {
                if (f.Active) continue;
                float h = hero.standHeight;
                var pos = (Vector2)transform.position + new Vector2(hero.Facing * (hero.width / 2f + 0.3f), hero.Crouching ? h * 0.25f : h * 0.55f);
                f.Launch(pos, hero.Facing, hero.Velocity.x);
                ShotTime = 0f;
                SoundFx.Play("shoot");
                return;
            }
        }
    }
}
