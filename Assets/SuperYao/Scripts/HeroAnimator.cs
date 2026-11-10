using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Choisit la pose du héros comme la version téléphone (scene.ts, yaoPose) :
    /// - au sol, immobile : animation de repos ;
    /// - au sol, en mouvement : course, dont la cadence suit la vitesse (marche lente, course rapide) ;
    /// - accroupi : pose fixe ;
    /// - en l'air : la pose dépend de la vitesse verticale (impulsion, montée, sommet, descente), puis bras en
    ///   l'air pendant une longue chute.
    /// Même échelle pour toutes les poses : le héros debout, cheveux compris, fait 1,25 fois sa boîte de collision.
    /// À poser sur l'enfant « Visuel » du héros (le parent porte la physique).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class HeroAnimator : MonoBehaviour
    {
        public HeroAnimationSet hero;
        [Tooltip("Poses en skate : affichées tant que le héros a le pouvoir (skate ramassé dans un coffre).")]
        public HeroAnimationSet skateSet;
        [Tooltip("Poses avec l'hélice : affichées quand le héros a ramassé le chapeau à hélice (HeroController.canFly).")]
        public HeroAnimationSet flySet;
        HeroAnimationSet onFoot;
        [Header("Liane et échelle (stage 6)")]
        [Tooltip("Yao pendu à la liane (poings en haut de l'image, alignés), et Yao de dos qui grimpe.")]
        public Sprite[] hang, climb;
        [Tooltip("Hauteur, sur ces planches, de Yao debout (px), et hauteur des poings au-dessus du bas de l'image de la liane (px).")]
        public float hangSourceHeight = 395f, climbSourceHeight = 500f, hangGripPx = 373f;

        /// <summary>Hauteur des mains de Yao pendu, au-dessus du bas de l'image (cases) : la liane les tient là.</summary>
        public float HangHandHeight => controller == null ? 1.2f
            : hang != null && hang.Length > 0 ? controller.standHeight * DrawnHeightRatio * hangGripPx / hangSourceHeight
            : controller.standHeight * 0.9f;

        // Seuils de la version téléphone, convertis de px/image en cases/s (× 3,75).
        const float TakeoffSpeed = 15f, RiseSpeed = 3.75f, ApexSpeed = -5.625f, LongFallSpeed = -26.25f;
        const float MoveThreshold = 0.5625f;
        const float PhoneCycle = Mathf.PI * 2f / 2.56f; // téléphone : 2,56 radians de foulée par case, soit 2,45 cases par cycle
        const float LongFallFps = 60f / 7f;
        const float LandTime = 0.1f, MinAirForLanding = 0.2f, SlideSpeed = 2f;
        const float DrawnHeightRatio = 1.25f;
        const float AirGrace = 3f / 60f;
        const float HurtPoseTime = 0.4f, ShotPoseTime = 0.2f;
        float climbPhase;
        HeroFire fire;

        HeroController controller;
        SpriteRenderer sr;
        float runPhase, time, landTimer, airTime;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            controller = GetComponentInParent<HeroController>();
            ApplyScale();
        }

        /// <summary>Change de héros (mêmes déplacements, autres poses).</summary>
        public void SetHero(HeroAnimationSet set)
        {
            hero = onFoot = set;
            ApplyScale();
        }

        void ApplyScale() => ApplyScale(hero != null ? hero.sourceHeight : 0f);

        /// <summary>Même taille à l'écran pour toutes les poses, même si leurs planches sont dessinées à des tailles
        /// différentes : <paramref name="sourceHeight"/> est la hauteur du héros debout sur la planche de la pose.</summary>
        void ApplyScale(float sourceHeight)
        {
            if (hero == null || controller == null || hero.FirstSprite == null || sourceHeight <= 0f) return;
            float s = controller.standHeight * DrawnHeightRatio * hero.FirstSprite.pixelsPerUnit / sourceHeight;
            transform.localScale = new Vector3(s, s, 1f);
        }

        void LateUpdate()
        {
            // À pied, ou sur le skate quand il a le pouvoir : il le perd s'il est touché.
            if (onFoot == null) onFoot = hero;
            if (controller != null)
                hero = controller.canFly && flySet != null ? flySet : controller.IsBig && skateSet != null ? skateSet : onFoot;
            if (hero == null || controller == null) return;
            float dt = Time.deltaTime;
            time += dt;
            var v = controller.Velocity;

            // En l'air : vraiment décollé (impulsion du saut), ou sans sol depuis plus de 3 images ; un contact
            // perdu un instant en courant ne doit pas afficher une pose de saut au milieu de la foulée.
            bool airborne = !controller.Grounded && (v.y > RiseSpeed || controller.AirTime > AirGrace);

            // Réception : après un vrai saut ou une chute, la pose accroupie de réception un court instant.
            if (airborne) airTime += dt;
            else
            {
                if (airTime > MinAirForLanding && hero.land != null && hero.land.Length > 0) landTimer = LandTime;
                airTime = 0f;
            }
            landTimer -= dt;

            Sprite pose;
            float source = hero.sourceHeight;
            bool back = false;
            if (controller.Hanging != null)
            {
                // Pendu à la liane : les jambes se balancent.
                if (hang != null && hang.Length > 0) { pose = hang[Mathf.FloorToInt(time * 7f) % hang.Length]; source = hangSourceHeight; }
                else pose = Frame(hero.jump, hero.jumpApex) ?? Frame(hero.idle, 0);
            }
            else if (controller.Climbing)
            {
                // Il grimpe, de dos : une pose de plus à chaque bout de montée (il reste figé à l'arrêt).
                climbPhase += Mathf.Abs(v.y) * dt * 2.2f;
                if (climb != null && climb.Length > 0) { pose = climb[Mathf.FloorToInt(climbPhase) % climb.Length]; source = climbSourceHeight; back = true; }
                else pose = Frame(hero.jump, Mathf.FloorToInt(climbPhase) % 2 == 0 ? hero.jumpRise : hero.jumpApex) ?? Frame(hero.idle, 0);
            }
            else if (controller.Crouching)
            {
                bool sliding = hero.crouchSlideFrame >= 0 && Mathf.Abs(v.x) > SlideSpeed;
                pose = Frame(hero.crouch, sliding ? hero.crouchSlideFrame : hero.crouchFrame) ?? Frame(hero.idle, 0);
            }
            else if (!airborne && landTimer > 0f) pose = Frame(hero.land, 0);
            else if (airborne && controller.Gliding && hero.hover != null && hero.hover.Length > 0)
            {
                // Vol à l'hélice : vers l'avant s'il avance, sur place sinon (l'hélice tourne).
                bool forward = Mathf.Abs(v.x) > 2.5f && hero.glide != null && hero.glide.Length > 0;
                var frames = forward ? hero.glide : hero.hover;
                pose = frames[Mathf.FloorToInt(time * (forward ? 10f : 12f)) % frames.Length];
            }
            else if (airborne)
            {
                if (v.y > TakeoffSpeed) pose = Frame(hero.jump, hero.jumpTakeoff);
                else if (v.y > RiseSpeed) pose = Frame(hero.jump, hero.jumpRise);
                else if (v.y > ApexSpeed) pose = Frame(hero.jump, hero.jumpApex);
                else if (v.y > LongFallSpeed || hero.fall == null || hero.fall.Length == 0) pose = Frame(hero.jump, hero.jumpFall);
                else pose = Frame(hero.fall, Mathf.FloorToInt(time * LongFallFps) % Mathf.Min(3, hero.fall.Length));
            }
            else if (Mathf.Abs(v.x) > MoveThreshold && hero.run != null && hero.run.Length > 0)
            {
                // Marche jusqu'à mi-chemin entre la vitesse de marche et celle de course, puis course. La phase
                // (fraction du cycle) continue d'une animation à l'autre : le pas en cours n'est pas interrompu.
                bool walking = hero.walk != null && hero.walk.Length > 0
                               && Mathf.Abs(v.x) < (controller.walkSpeed + controller.runSpeed) / 2f;
                var frames = walking ? hero.walk : hero.run;
                float cycle = walking ? hero.walkCycle : hero.runCycle;
                if (cycle <= 0f) cycle = PhoneCycle;
                runPhase = (runPhase + Mathf.Abs(v.x) * dt / cycle) % 1f;
                pose = frames[Mathf.FloorToInt(runPhase * frames.Length) % frames.Length];
                float clipSource = walking ? hero.walkSourceHeight : hero.runSourceHeight;
                if (clipSource > 0f) source = clipSource;
            }
            else
            {
                runPhase = 0f;
                float idleFps = hero.idleFps > 0f ? hero.idleFps : 60f / 9f;
                pose = Frame(hero.idle, hero.idle != null && hero.idle.Length > 0 ? Mathf.FloorToInt(time * idleFps) % hero.idle.Length : 0);
            }

            // Tir : coup de poing (planche Attack) pendant 0,2 s, debout ou en l'air.
            fire ??= controller.GetComponent<HeroFire>();
            if (fire != null && fire.ShotTime < ShotPoseTime && !controller.Crouching && hero.attack != null && hero.attack.Length > 0)
            {
                pose = hero.attack[Mathf.Min(hero.attack.Length - 1, 1 + Mathf.FloorToInt(fire.ShotTime / ShotPoseTime * (hero.attack.Length - 1)))];
                source = hero.sourceHeight;
            }
            // Mort : surpris pendant la pause, puis bras en l'air en tombant (planche Death) ; fin de partie : caché.
            if (controller.IsDying && hero.death != null && hero.death.Length > 0)
            {
                pose = hero.death[Mathf.Min(hero.death.Length - 1, controller.DeathFalling ? 1 : 0)];
                source = hero.sourceHeight;
            }
            // Juste après une blessure : la pose « blessé » (planche Hurt), puis il clignote tant qu'il est intouchable.
            if (controller.HurtTime < HurtPoseTime && hero.hurt != null && hero.hurt.Length > 0)
            {
                pose = hero.hurt[Mathf.Min(hero.hurt.Length - 1, Mathf.FloorToInt(controller.HurtTime / HurtPoseTime * Mathf.Min(4, hero.hurt.Length)))];
                source = hero.sourceHeight;
            }
            var stage = StageState.Instance;
            sr.enabled = (stage == null || !stage.GameOver)
                         && (controller.IsDying || !controller.Invulnerable || Mathf.FloorToInt(Time.time * 15f) % 2 == 0);

            if (pose != null) sr.sprite = pose;
            ApplyScale(source);
            sr.flipX = !back && controller.Facing < 0; // les planches regardent vers la droite (de dos : pas de miroir)
        }

        static Sprite Frame(Sprite[] frames, int i) =>
            frames == null || frames.Length == 0 ? null : frames[Mathf.Clamp(i, 0, frames.Length - 1)];
    }
}
