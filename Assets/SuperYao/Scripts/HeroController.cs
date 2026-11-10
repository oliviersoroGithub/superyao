using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Déplacements des héros, repris de la version téléphone (player.ts) : marcher, courir, prendre de l'élan,
    /// s'accroupir, sauter sur place ou vers l'avant. Toutes les valeurs par défaut sont celles du téléphone,
    /// converties en cases et en secondes (1 case = 16 px, 60 images/s : 1 px/image = 3,75 cases/s).
    ///
    /// - Marcher : 6 cases/s. Courir (bouton « courir ») : 10,3 cases/s. En gardant la même direction au sol, le
    ///   héros prend de l'élan et atteint peu à peu la vitesse de course, même sans le bouton.
    /// - Demi-tour : il freine plus fort qu'il n'accélère. Sans commande, il glisse et s'arrête (moins vite en l'air).
    /// - Sauter : sur place si le héros est immobile, vers l'avant s'il avance (il garde son élan, et plus il va
    ///   vite, plus il saute haut). Relâcher le bouton tôt donne un petit saut. Le saut part encore un court instant
    ///   après avoir quitté un bord, et un appui juste avant d'atterrir est retenu.
    /// - S'accroupir : au sol seulement ; il ne marche plus (il glisse sur son élan) mais peut se retourner ; il ne
    ///   se relève que s'il y a la place au-dessus. Sa boîte de collision rapetisse.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(HeroInput))]
    public class HeroController : MonoBehaviour
    {
        [Header("Vitesses au sol (cases/s et cases/s²)")]
        public float walkSpeed = 6f;
        [Tooltip("Vitesse de course (le téléphone : 10,3 cases/s, jugé trop rapide ; ramené à 8,5).")]
        public float runSpeed = 8.5f;
        public float walkAccel = 45f;
        public float runAccel = 76.5f;
        [Tooltip("Accélération de la prise d'élan, au-delà de la marche, en gardant la direction.")]
        public float elanAccel = 6.3f;
        public float friction = 54f;
        // Pentes (constantes : elles s'appliquent à tous les stages sans les reconstruire). La descente va plus vite que
        // le plat, à pied comme en skate : part de la gravité qui l'accélère en descente (et le freine en montée), et
        // vitesse maximale en descente, en multiple de la vitesse de course.
        const float FootSlopeBoost = 0.45f, FootDownhillMax = 1.3f;
        const float SkateSlopeBoost = 0.9f, SkateDownhillMax = 1.7f;
        /// <summary>En skate, freinage sur une pente quand on lâche les commandes (il roule).</summary>
        const float SkateSlopeFriction = 6f;

        [Header("Vol à l'hélice (stage 4)")]
        [Tooltip("Yao porte l'hélice : les courants ascendants le font monter, et en l'air il plane tant qu'on garde le bouton de saut.")]
        public bool canFly;
        /// <summary>Vitesse de chute maximale quand il plane (cases/s).</summary>
        const float GlideFall = 2.5f;
        /// <summary>En l'air avec l'hélice : il plane ou monte dans un courant.</summary>
        public bool Gliding { get; private set; }
        float updraftRise, updraftAccel;

        /// <summary>Appelé par un courant ascendant tant que le héros est dedans.</summary>
        public void InUpdraft(float rise, float accel)
        {
            updraftRise = Mathf.Max(updraftRise, rise);
            updraftAccel = Mathf.Max(updraftAccel, accel);
        }
        public float airFriction = 13.5f;

        // Lianes, échelles et cordes à nœuds (stage 6).
        /// <summary>Liane à laquelle Yao est accroché (null sinon).</summary>
        public Liana Hanging { get; private set; }
        /// <summary>Yao grimpe à une échelle ou à une corde.</summary>
        public bool Climbing { get; private set; }
        /// <summary>Yao peut attraper cette liane : en l'air, ni accroché ni en train de grimper, et pas celle qu'il vient de
        /// lâcher (la suivante, si).</summary>
        public bool CanGrab(Liana l) => Hanging == null && !Climbing && !Grounded && (l != lastLiana || grabCooldown <= 0f)
                                        && !IsDying && !(StageState.Instance != null && StageState.Instance.Won);
        Liana lastLiana;
        const float ClimbSpeed = 4.5f;
        float grabCooldown;
        Climbable nearLadder;

        HeroAnimator anim;
        /// <summary>Hauteur des mains de Yao pendu à une liane, au-dessus de ses pieds (selon ses images).</summary>
        public float HandHeight
        {
            get
            {
                if (anim == null) anim = GetComponentInChildren<HeroAnimator>();
                return anim != null ? anim.HangHandHeight : standHeight * 0.9f;
            }
        }

        /// <summary>Appelé par une échelle tant que le héros la touche.</summary>
        public void InClimbable(Climbable c) => nearLadder = c;

        /// <summary>Appelé par une liane quand Yao l'attrape.</summary>
        public void GrabLiana(Liana l)
        {
            Hanging = l;
            Climbing = false;
            buffer = 0f;
            input.ConsumeJumpPressed();
        }

        [Header("Saut")]
        // Le téléphone sautait à 40,9 cases/s (6 cases de haut, 7,8 en courant) : jugé trop haut, ramené à 4 cases.
        [Tooltip("Vitesse de départ du saut, en cases/s. Hauteur = vitesse² / (2 × 139,5) : 33,4 donne 4 cases.")]
        public float jumpVelocity = 33.4f;
        [Tooltip("Vitesse verticale ajoutée par case/s de vitesse horizontale : courir fait sauter plus haut " +
                 "(0,31 : environ 4,8 cases en pleine course).")]
        public float jumpSpeedBonus = 0.31f;
        [Tooltip("Vitesse de montée maximale quand on relâche le bouton tôt (petit saut).")]
        public float jumpCut = 12.75f;
        public float maxFallSpeed = 41.25f;
        [Tooltip("Délai pendant lequel on peut encore sauter après avoir quitté un bord, en secondes.")]
        public float coyoteTime = 7f / 60f;
        [Tooltip("Un appui sur « sauter » est retenu ce temps avant d'atterrir, en secondes.")]
        public float jumpBufferTime = 7f / 60f;

        [Header("Boîte de collision (cases)")]
        public float width = 0.75f;
        public float standHeight = 1.375f;
        public float crouchHeight = 0.875f;

        /// <summary>Le pouvoir ne fait plus grandir Yao (le téléphone : ×30/22) : il lui donne le skate (voir
        /// HeroAnimator.skateSet), sans changer sa taille ni sa boîte de collision.</summary>
        const float BigScale = 1f;

        [Header("Ennemis")]
        [Tooltip("Rebond après avoir écrasé un ennemi, en cases/s (plus haut si le bouton de saut reste enfoncé).")]
        public float stompBounce = 24f, stompBounceHeld = 34.5f;
        [Tooltip("Temps d'invincibilité après une blessure, en secondes (le héros clignote).")]
        public float hurtInvulnerable = 2f;

        public bool Invulnerable => invulnerable > 0f;
        public bool IsDying { get; private set; }
        /// <summary>Temps écoulé depuis la dernière blessure, en secondes (pour la pose « blessé »).</summary>
        public float HurtTime { get; private set; } = 99f;
        /// <summary>Hauteur des pieds et vitesse verticale avant le dernier pas de physique : un ennemi touché pendant ce
        /// pas sait ainsi si le héros arrivait d'en haut (sa chute peut dépasser une demi-case par pas).</summary>
        public float PreviousFeetY { get; private set; }
        public float PreviousVelocityY { get; private set; }
        float invulnerable, winTime, nextWinHop = 0.5f;

        public bool Grounded { get; private set; }
        public bool IsBig { get; private set; }
        public bool Crouching { get; private set; }
        public int Facing { get; private set; } = 1;
        public bool JumpedForward { get; private set; }
        /// <summary>Temps passé en l'air depuis le dernier contact avec le sol, en secondes.</summary>
        public float AirTime { get; private set; }
        /// <summary>Hauteur des pieds au dernier contact avec le sol (la caméra s'en sert pour ignorer les sauts).</summary>
        public float LastGroundY { get; private set; }
        /// <summary>Vitesse propre du héros (sans celle de la pirogue qui le porte) : sert aux animations.</summary>
        public Vector2 Velocity => body.linearVelocity - new Vector2(carried, 0f);

        public event System.Action Jumped, Landed;

        Rigidbody2D body;
        BoxCollider2D box;
        HeroInput input;
        ContactFilter2D groundFilter;
        ContactFilter2D solidFilter; // obstacles pleins seulement (pas les pièces ni les bananes)
        readonly RaycastHit2D[] hits = new RaycastHit2D[4];
        readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
        float coyote, buffer;
        /// <summary>Au sol sur une pente (ni plate ni trop raide), et sa direction vers la droite.</summary>
        bool onSlope;
        Vector2 slopeTangent = Vector2.right;
        /// <summary>Vitesse horizontale de la plateforme mobile qui porte le héros (ajoutée à la sienne).</summary>
        float carried;
        bool cutJump;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            input = GetComponent<HeroInput>();
            body.gravityScale = 1f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            // Jamais endormi : immobile sur une échelle, il doit continuer à sentir qu'il la touche.
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            // Sol = contact dont la normale pointe vers le haut (les murs ne comptent pas).
            groundFilter = new ContactFilter2D { useTriggers = false };
            groundFilter.SetNormalAngle(45f, 135f);
            solidFilter = new ContactFilter2D { useTriggers = false };
            SetBox(standHeight);
            LastGroundY = transform.position.y;
            // Petite ombre au sol sous Yao.
            if (GetComponent<GroundShadow>() == null) gameObject.AddComponent<GroundShadow>().width = 0.95f;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (IsDying) { StepDeath(dt); return; }
            KeepOnScreen();
            var v = body.linearVelocity;
            invulnerable -= dt;
            // À l'arrivée, le héros ne répond plus aux commandes : il sautille de joie.
            var stage = StageState.Instance;
            bool won = stage != null && stage.Won;
            if (won) winTime += dt;
            HurtTime += dt;
            PreviousFeetY = body.position.y;
            PreviousVelocityY = v.y;
            grabCooldown -= dt;
            if (Hanging != null) { StepHanging(dt, won); return; }
            var ladder = nearLadder;
            nearLadder = null;

            bool wasGrounded = Grounded;
            // Tolérance de 0,5 case/s : avec une gravité aussi forte, le moteur repousse le héros hors du sol à chaque
            // pas avec une petite vitesse vers le haut ; un seuil à zéro le croyait en l'air une image sur deux.
            Grounded = v.y <= 0.5f && body.IsTouching(groundFilter);
            // En haut d'une descente, le héros qui avançait au sol décollerait un instant : on le recolle à la pente.
            if (!Grounded && wasGrounded && v.y <= 0.5f && !IsDying) SnapToGround();
            var normal = Grounded ? GroundNormal() : Vector2.up;
            onSlope = normal.y > 0.7f && normal.y < 0.995f;
            slopeTangent = onSlope ? new Vector2(normal.y, -normal.x) : Vector2.right;
            AirTime = Grounded ? 0f : AirTime + dt;
            if (Grounded)
            {
                LastGroundY = body.position.y;
                cutJump = false;
                if (!wasGrounded) Landed?.Invoke();
            }

            // S'accroupir au sol ; se relever seulement s'il y a la place au-dessus.
            bool down = input.Down && !won;
            if (down && Grounded && !Crouching) SetCrouch(true);
            else if (!down && Crouching) SetCrouch(false);

            int wanted = won ? 0 : input.Horizontal > 0.3f ? 1 : input.Horizontal < -0.3f ? -1 : 0;
            int dir = Crouching && Grounded ? 0 : wanted;
            if (Crouching && wanted != 0) Facing = wanted;

            // Échelle ou corde : le bouton de saut contre elle le fait grimper (au lieu de sauter).
            if (ladder != null && !Climbing && !won && !Crouching && input.JumpHeld && body.position.y < ladder.top - 0.5f) Climbing = true;
            if (Climbing)
            {
                if (ladder == null || won) { Climbing = false; body.gravityScale = 1f; }
                else { StepClimbing(dt, ladder, wanted); return; }
            }

            // Sur une pirogue, les commandes jouent sur la vitesse par rapport à elle, puis on rajoute la sienne.
            // En la quittant (saut, chute), le héros garde l'élan qu'elle lui donnait.
            float platform = Grounded ? PlatformSpeed() : 0f;
            float vx = Grounded ? v.x - carried : v.x;
            if (dir != 0)
            {
                Facing = dir;
                float along = vx * dir; // négative si le héros part encore dans l'autre sens
                float max, accel;
                if (input.RunHeld) { max = runSpeed; accel = runAccel; }
                else if (along < walkSpeed) { max = walkSpeed; accel = walkAccel; }
                else { max = Grounded || Gliding ? runSpeed : along; accel = elanAccel; } // prise d'élan (en vol aussi)
                if (along < 0f) accel = Mathf.Max(accel, Grounded ? friction : airFriction) * 1.5f; // demi-tour
                if (along < max) vx = dir * Mathf.Min(max, along + accel * dt); // l'élan déjà pris est conservé
            }
            else vx = Mathf.MoveTowards(vx, 0f, (Grounded ? (IsBig && onSlope ? SkateSlopeFriction : friction) : airFriction) * dt);
            // La pente l'accélère en descente et le freine en montée (en skate plus fort) ; à pied et à l'arrêt, le
            // frottement le retient. Revenu sur du plat, l'élan retombe peu à peu.
            bool downhill = onSlope && (dir == 0 ? IsBig : dir * slopeTangent.y < 0f);
            if (onSlope && (IsBig || dir != 0)) vx += (IsBig ? SkateSlopeBoost : FootSlopeBoost) * -Physics2D.gravity.y * -slopeTangent.y * dt;
            else if (Grounded && !onSlope && Mathf.Abs(vx) > runSpeed) vx = Mathf.MoveTowards(vx, Mathf.Sign(vx) * runSpeed, airFriction * dt);
            // En montée, il avance toujours au moins à 70 % de la marche quand on le pousse : sur une forte pente (liane
            // du stage 6), le freinage de la pente l'arrêtait presque.
            if (onSlope && dir != 0 && !downhill && vx * dir < walkSpeed * 0.7f) vx = dir * walkSpeed * 0.7f;
            float cap = runSpeed * (downhill ? (IsBig ? SkateDownhillMax : FootDownhillMax) : IsBig ? SkateDownhillMax : 1f);
            vx = Mathf.Clamp(vx, -cap, cap);

            coyote = Grounded ? coyoteTime : coyote - dt;
            buffer = input.ConsumeJumpPressed() && !won ? jumpBufferTime : buffer - dt;

            float vy = v.y;
            bool jumping = false;
            if (won && Grounded && winTime > nextWinHop)
            {
                vy = 22.5f; // petit bond de joie (6 px/image sur le téléphone), toutes les 40 images
                nextWinHop = winTime + 40f / 60f;
                Grounded = false;
                jumping = true;
            }
            else if (buffer > 0f && coyote > 0f)
            {
                SoundFx.Play("jump");
                if (Crouching) SetCrouch(false);
                vy = jumpVelocity + Mathf.Abs(vx) * jumpSpeedBonus;
                coyote = buffer = 0f;
                cutJump = true;
                Grounded = false;
                jumping = true;
                JumpedForward = Mathf.Abs(vx) > 0.5f;
                Jumped?.Invoke();
            }
            // Vol à l'hélice : dans un courant ascendant, il monte (même depuis le sol) ; en l'air, bouton de saut tenu, il
            // plane (chute lente) ; ▼ : il pique.
            // Le courant (ou l'hélice) porte Yao : la gravité est alors coupée pour ce pas, sinon elle l'emportait sur la
            // poussée du courant (il retombait) et doublait la chute en vol plané. Lancé plus vite que le courant (saut),
            // il ralentit normalement jusqu'à sa vitesse.
            bool lifted = false, carriedByAir = false;
            if (canFly && !input.Down && !won && !IsDying)
            {
                if (updraftRise > 0f)
                {
                    if (vy < updraftRise)
                    {
                        vy = Mathf.MoveTowards(Mathf.Max(vy, -GlideFall), updraftRise, updraftAccel * dt);
                        carriedByAir = true;
                    }
                    lifted = true;
                    Grounded = false;
                    cutJump = false;
                }
                else if (!Grounded && input.JumpHeld && vy <= -GlideFall)
                {
                    vy = -GlideFall;
                    carriedByAir = true;
                }
            }
            Gliding = canFly && !Grounded && (lifted || input.JumpHeld) && !input.Down;
            updraftRise = updraftAccel = 0f;
            if (cutJump && !input.JumpHeld && vy > jumpCut) vy = jumpCut;
            if (vy < -maxFallSpeed) vy = -maxFallSpeed;
            carried = platform;
            // Sur une pente : la vitesse suit la pente (même vitesse horizontale que sur du plat) et la gravité est
            // coupée, pour qu'il ne glisse pas à l'arrêt et ne décolle pas en descente.
            if (onSlope && Grounded && !jumping && !lifted && platform == 0f)
            {
                body.gravityScale = 0f;
                body.linearVelocity = slopeTangent * (vx / slopeTangent.x);
                return;
            }
            body.gravityScale = carriedByAir ? 0f : 1f;
            body.linearVelocity = new Vector2(vx + platform, vy);
        }

        /// <summary>Accroché à une liane : il se balance (◄ ► pour prendre de l'élan) ; le bouton de saut le fait lâcher.</summary>
        void StepHanging(float dt, bool won)
        {
            int push = won ? 0 : input.Horizontal > 0.3f ? 1 : input.Horizontal < -0.3f ? -1 : 0;
            if (push != 0) Facing = push;
            var feet = Hanging.Step(dt, push, HandHeight);
            Grounded = false;
            AirTime += dt;
            carried = 0f;
            if (input.ConsumeJumpPressed() && !won)
            {
                var v = Hanging.Release(push);
                lastLiana = Hanging;
                Hanging = null;
                grabCooldown = 0.4f;
                body.gravityScale = 1f;
                body.linearVelocity = v;
                cutJump = false;
                JumpedForward = Mathf.Abs(v.x) > 0.5f;
                if (Mathf.Abs(v.x) > 0.5f) Facing = v.x > 0f ? 1 : -1;
                SoundFx.Play("jump");
                Jumped?.Invoke();
                return;
            }
            body.gravityScale = 0f;
            body.linearVelocity = (feet - body.position) / dt;
        }

        /// <summary>Sur une échelle : bouton de saut tenu, il monte ; ▼, il descend ; rien, il reste accroché. En haut il se
        /// hisse sur la plate-forme ; ◄ ► (sans monter ni descendre) le font lâcher l'échelle.</summary>
        void StepClimbing(float dt, Climbable ladder, int wanted)
        {
            input.ConsumeJumpPressed();
            buffer = 0f;
            carried = 0f;
            var p = body.position;
            float vy = input.JumpHeld ? ClimbSpeed : input.Down ? -ClimbSpeed : 0f;
            body.gravityScale = 1f;
            if (wanted != 0 && !input.JumpHeld && !input.Down)
            {
                Climbing = false;
                Facing = wanted;
                body.linearVelocity = new Vector2(wanted * walkSpeed * 0.6f, 0f);
                return;
            }
            if (vy > 0f && p.y >= ladder.top - 0.25f)
            {
                Climbing = false;
                body.linearVelocity = new Vector2(0f, 12f); // il se hisse sur la plate-forme
                return;
            }
            if (vy < 0f && Grounded) { Climbing = false; return; }
            Grounded = false;
            AirTime = 0f;
            body.gravityScale = 0f;
            body.linearVelocity = new Vector2(Mathf.Clamp((ladder.X - p.x) / dt, -4f, 4f), vy);
        }

        CameraFollow2D follow;
        bool followSearched;

        /// <summary>Le bord gauche de l'écran est un mur : Yao ne peut pas revenir loin en arrière (la caméra ne recule
        /// presque pas, voir CameraFollow2D.maxBacktrack).</summary>
        void KeepOnScreen()
        {
            if (!followSearched) { follow = FindAnyObjectByType<CameraFollow2D>(); followSearched = true; }
            if (follow == null) return;
            float min = follow.LeftEdge + width / 2f + 0.1f;
            var p = body.position;
            if (p.x >= min) return;
            body.position = new Vector2(min, p.y);
            var v = body.linearVelocity;
            if (v.x < 0f) body.linearVelocity = new Vector2(0f, v.y);
        }

        /// <summary>Normale moyenne des contacts de sol (vers le haut).</summary>
        Vector2 GroundNormal()
        {
            int n = body.GetContacts(groundFilter, contacts);
            var sum = Vector2.zero;
            for (int i = 0; i < n; i++) if (contacts[i].normal.y > 0.7f) sum += contacts[i].normal;
            return sum.sqrMagnitude > 0.0001f ? sum.normalized : Vector2.up;
        }

        /// <summary>Recolle le héros au sol juste en dessous (0,35 case au plus), si c'est un sol et pas un vide.</summary>
        void SnapToGround()
        {
            int n = box.Cast(Vector2.down, solidFilter, hits, 0.35f);
            for (int i = 0; i < n; i++)
            {
                if (hits[i].normal.y < 0.7f || hits[i].collider.isTrigger || hits[i].collider.usedByEffector) continue;
                body.position += Vector2.down * Mathf.Max(0f, hits[i].distance - 0.01f);
                Grounded = true;
                return;
            }
        }

        /// <summary>Vitesse horizontale de la plateforme mobile sous les pieds du héros (0 sur le sol fixe).</summary>
        float PlatformSpeed()
        {
            int n = body.GetContacts(groundFilter, contacts);
            for (int i = 0; i < n; i++)
            {
                var other = contacts[i].collider.attachedRigidbody == body ? contacts[i].otherCollider : contacts[i].collider;
                var mp = other != null ? other.GetComponentInParent<ICarrier>() : null;
                if (mp != null) return mp.Velocity.x;
            }
            return 0f;
        }

        /// <summary>Rebond sur un ennemi écrasé.</summary>
        public void Bounce()
        {
            var v = body.linearVelocity;
            v.y = input.JumpHeld ? stompBounceHeld : stompBounce;
            body.linearVelocity = v;
            cutJump = input.JumpHeld; // relâcher le bouton raccourcit le rebond, comme un saut
            Grounded = false;
        }

        /// <summary>Lancé vers le haut par un trampoline (bâche d'étal) : plus haut si l'on garde le bouton de saut.</summary>
        public void Launch(float speed, float heldSpeed)
        {
            if (IsDying) return;
            var v = body.linearVelocity;
            v.y = input.JumpHeld ? heldSpeed : speed;
            body.linearVelocity = v;
            body.gravityScale = 1f;
            cutJump = false;
            Grounded = false;
            SoundFx.Play("jump");
        }

        /// <summary>Touché par un ennemi : grand, il redevient petit et clignote un moment ; petit, il perd une vie.</summary>
        public void Hurt()
        {
            if (invulnerable > 0f || IsDying) return;
            if (IsBig)
            {
                SetBig(false);
                invulnerable = hurtInvulnerable;
                HurtTime = 0f;
                SoundFx.Play("hurt");
            }
            else Die();
        }

        // Mort (téléphone : 20 images de pause, bond de 8,2 px/image, gravité × 0,8, 110 images en tout).
        const float DeathPause = 20f / 60f, DeathHop = 30.75f, DeathDuration = 110f / 60f;
        float dyingTime, deathVelocity;
        /// <summary>Vrai pendant la chute de l'animation de mort (après la pause).</summary>
        public bool DeathFalling => IsDying && dyingTime > DeathPause;

        /// <summary>Perd une vie : le héros se fige, fait un petit bond et tombe hors de l'écran (tombé dans l'eau :
        /// pas de pause ni de bond), puis repart du dernier point de contrôle — ou c'est la fin de partie.</summary>
        public void Die(bool fromPit = false)
        {
            var state = StageState.Instance;
            if (IsDying || (state != null && state.Won)) return;
            // La cause est notée dans le journal (Editor.log, ou logcat sur Android) pour comprendre les morts surprises.
            string cause = fromPit ? "tombé dans l'eau" : state != null && state.TimeLeft <= 0 ? "temps écoulé" : "touché par un ennemi";
            Debug.Log($"Super Yao : mort ({cause}) en x = {transform.position.x:0.0}, y = {transform.position.y:0.0}, temps restant {(state != null ? state.TimeLeft : -1)}.");
            IsDying = true;
            if (Hanging != null) { Hanging.Release(); Hanging = null; }
            Climbing = false;
            dyingTime = fromPit ? DeathPause : 0f;
            deathVelocity = fromPit ? 0f : DeathHop;
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 1f;
            body.simulated = false; // il traverse le sol en tombant
            carried = 0f;
            SoundFx.Play("die");
        }

        void StepDeath(float dt)
        {
            dyingTime += dt;
            if (dyingTime > DeathPause)
            {
                deathVelocity -= -Physics2D.gravity.y * 0.8f * dt;
                transform.position += Vector3.up * (deathVelocity * dt);
            }
            if (dyingTime < DeathDuration) return;
            var state = StageState.Instance;
            if (state != null && !state.LoseLife()) return; // fin de partie (ou vidéo proposée) : il ne revient pas
            Revive();
        }

        /// <summary>Il revient au dernier point de contrôle (après une vie perdue, ou une vie gagnée en regardant une vidéo
        /// à la fin de partie), sans skate, un moment intouchable.</summary>
        public void Revive()
        {
            IsDying = false;
            SetBig(false);
            if (Crouching) SetCrouch(false);
            body.simulated = true;
            var respawn = GetComponent<HeroRespawn>();
            if (respawn != null) respawn.Respawn();
            invulnerable = 1.5f;
            HurtTime = 99f;
        }

        /// <summary>Grand (noix de karité) ou petit (touché) : la boîte de collision et l'image changent de taille
        /// (HeroAnimator suit standHeight).</summary>
        public void SetBig(bool big)
        {
            if (big == IsBig) return;
            if (!big) GetComponent<HeroFire>()?.SetFire(false); // redevenu petit : il perd aussi le feu
            float k = big ? BigScale : 1f / BigScale;
            IsBig = big;
            standHeight *= k;
            crouchHeight *= k;
            width *= k;
            SetBox(Crouching ? crouchHeight : standHeight);
        }

        /// <summary>Change de posture ; faux si le héros ne peut pas se relever (plafond trop bas).</summary>
        bool SetCrouch(bool crouch)
        {
            if (crouch == Crouching) return true;
            if (!crouch && box.Cast(Vector2.up, solidFilter, hits, standHeight - crouchHeight) > 0) return false;
            Crouching = crouch;
            SetBox(crouch ? crouchHeight : standHeight);
            return true;
        }

        /// <summary>Coins arrondis de la boîte de collision : sur une rangée de blocs, une boîte à angles vifs accroche la
        /// jointure entre deux blocs voisins (le héros restait bloqué) ; arrondie, elle glisse par-dessus.</summary>
        const float EdgeRadius = 0.08f;

        void SetBox(float height)
        {
            // La taille donnée est celle de la boîte avant arrondi : elle grandit de EdgeRadius de chaque côté.
            box.edgeRadius = EdgeRadius;
            box.size = new Vector2(width - 2f * EdgeRadius, height - 2f * EdgeRadius);
            box.offset = new Vector2(0f, height / 2f);
        }
    }
}
