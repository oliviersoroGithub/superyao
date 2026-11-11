using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Ennemis du stage 1, règles de la version téléphone (enemies.ts) :
    /// - crabe et tortue : ils marchent, font demi-tour contre un obstacle et au bord du vide (ils ne tombent pas) ;
    /// - mouette : elle fait des allers-retours en vol en ondulant ;
    /// - taxi (woro-woro, stage 2) : il roule vite, s'arrête le temps d'un demi-tour contre un obstacle ou au bord du
    ///   vide ; on rebondit sur son toit et il explose (une boule de feu le fait exploser aussi) ;
    /// - agame (stages 2 et 3) : un lézard qui marche comme le crabe ;
    /// - vautour (stage 4) : il vole comme la mouette, plus large et plus dangereux ;
    /// - gbaka et charrette (stage 5) : ils roulent lentement et foncent quand Yao est devant eux, à la même hauteur ;
    ///   un saut (ou une boule de feu) les sonne un moment, il en faut 3 pour le gbaka (il est éjecté en tournoyant, comme
    ///   sur le téléphone), 2 pour la charrette ;
    /// - singe (stage 6) : il marche (au sol ou sur une branche) et lance des noix de coco en cloche quand Yao passe à portée ;
    /// - chimpanzé (stage 9) : comme le singe, il lance des noix (il les casse d'abord avec une pierre) ; hippopotame nain
    ///   (stage 9) : il fonce sur Yao comme le gbaka, il faut deux coups ;
    /// - poisson-chat, poisson-tigre et caïman (stage 7, dans les lacs) : le poisson-chat patrouille, le poisson-tigre fonce
    ///   sur Yao qui nage ; le caïman nage en surface, bondit hors de l'eau sur Yao qui passe au-dessus (pirogue) et le
    ///   poursuit sous l'eau ; il faut deux coups.
    /// Ils ne s'éveillent qu'à l'approche de la caméra. Sauter dessus les écrase (le héros rebondit, plus haut s'il
    /// garde le bouton de saut enfoncé) ; les toucher autrement blesse le héros.
    /// Vitesses du téléphone converties (1 px/image = 3,75 cases/s).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public class Enemy : MonoBehaviour
    {
        public enum Kind { Crab, Turtle, Gull, Taxi, Agama, Vulture, Gbaka, Cart, Monkey, Catfish, Tigerfish, Caiman, Chimp, Hippo }
        /// <summary>Gbaka et charrette : ils foncent sur Yao quand il est devant eux, et encaissent plusieurs coups.</summary>
        bool IsCharger => kind == Kind.Gbaka || kind == Kind.Cart || kind == Kind.Hippo;
        /// <summary>Mouette et vautour volent (allers-retours en ondulant).</summary>
        bool IsFlyer => kind == Kind.Gull || kind == Kind.Vulture;
        /// <summary>Poissons et caïman : ils restent dans leur lac.</summary>
        bool IsSwimmer => kind == Kind.Catfish || kind == Kind.Tigerfish || kind == Kind.Caiman;

        public Kind kind;
        public Sprite[] walkFrames;
        [Tooltip("Image une fois écrasé (facultatif).")]
        public Sprite squashedSprite;
        [Tooltip("Vrai si les images regardent vers la droite.")]
        public bool facesRight = true;
        public float speed = 1.4f;
        [Tooltip("Mouette : demi-amplitude de l'aller-retour et de l'ondulation, en cases.")]
        public float flyRange = 2.75f, flyBob = 0.625f;
        [Tooltip("Taxi : demi-tour (6 images) et explosion (5 images).")]
        public Sprite[] turnFrames, boomFrames;

        // Taxi (téléphone) : demi-tour de 18 images à 60 i/s, explosion de 5 poses de 7 images, conduite à 15 i/s.
        const float TaxiTurnTime = 0.3f, TaxiBoomFrame = 7f / 60f, TaxiDriveFps = 15f;
        float turnTime;
        bool exploding;

        [Header("Fonceurs (gbaka, charrette)")]
        public int hp = 1;
        [Tooltip("Vitesse de la charge, et distance (cases) à laquelle il repère Yao devant lui.")]
        public float chargeSpeed = 8f, sight = 9f;
        public Sprite[] chargeFrames, hitFrames;
        [Tooltip("Agrandissement de l'explosion (images du taxi), pour un véhicule plus gros.")]
        public float boomScale = 1f;
        float dizzy, chargeCooldown, stompGuard, baseX;

        [Header("Singe")]
        public Sprite[] throwFrames, coconutFrames;
        // Téléphone : il repère Yao à 170 px de côté (10,6 cases) ; lancer de 30 images, la noix part à la 18e ; 120 images
        // d'attente. Les singes perchés visent aussi Yao en contrebas (7 cases).
        const float ThrowTime = 0.5f, ThrowRelease = 0.3f, ThrowCooldown = 2f, ThrowRange = 10.6f, ThrowHeight = 7f, CoconutUp = 18f;
        float throwTime = -1f, throwCooldown = 0.8f;
        [Tooltip("Taille de la noix lancée (cases) : noix de coco du singe, petite noix cassée du chimpanzé.")]
        public float nutSize = 0.55f;
        bool thrown;
        bool charging;
        static HeroController heroCache;

        public const int StompScore = 100;
        const float ActivateMargin = 2f, FramesPerSecond = 60f / 9f;

        SpriteRenderer sr;
        BoxCollider2D box;
        Vector2 basePos;
        float vx, time, deadTime = -1f, fallSpeed;
        bool awake, dead;
        readonly RaycastHit2D[] hits = new RaycastHit2D[4];
        ContactFilter2D solid;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            var body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            basePos = transform.position;
            vx = -speed; // ils partent vers le héros
            // Petite ombre au sol, de la largeur de l'ennemi.
            if (GetComponent<GroundShadow>() == null && !IsSwimmer)
            {
                var sh = gameObject.AddComponent<GroundShadow>();
                sh.width = Mathf.Clamp(box.size.x * Mathf.Abs(transform.lossyScale.x) * 0.85f, 0.6f, 3f);
                sh.order = 3;
            }
            solid = new ContactFilter2D { useTriggers = false };
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dead) { UpdateDead(dt); return; }
            if (!awake)
            {
                var cam = Camera.main;
                if (cam == null) return;
                float right = cam.transform.position.x + cam.orthographicSize * cam.aspect + ActivateMargin;
                if (transform.position.x > right) return;
                awake = true;
            }
            time += dt;
            if (stompGuard > 0f) stompGuard -= dt;
            if (IsCharger)
            {
                if (dizzy > 0f)
                {
                    // Sonné : il tremble sur place.
                    dizzy -= dt;
                    if (hitFrames != null && hitFrames.Length > 0) sr.sprite = hitFrames[Mathf.FloorToInt(time * 8f) % hitFrames.Length];
                    var p = transform.position;
                    if (baseX == 0f) baseX = p.x;
                    p.x = baseX + Mathf.Sin(time * 60f) * 0.06f;
                    transform.position = p;
                    if (dizzy <= 0f) { p.x = baseX; transform.position = p; baseX = 0f; }
                    return;
                }
                LookForHero(dt);
            }
            if ((kind == Kind.Monkey || kind == Kind.Chimp) && MonkeyThrow(dt)) return;
            if (IsSwimmer) { Swim(dt); return; }
            if (IsFlyer) Fly(dt); else Walk(dt);
            if (kind == Kind.Taxi && turnTime > 0f && turnFrames != null && turnFrames.Length > 0)
                sr.sprite = turnFrames[Mathf.Min(turnFrames.Length - 1, Mathf.FloorToInt((TaxiTurnTime - turnTime) / TaxiTurnTime * turnFrames.Length))];
            else if (charging && chargeFrames != null && chargeFrames.Length > 0)
                sr.sprite = chargeFrames[Mathf.FloorToInt(time * 12f) % chargeFrames.Length];
            else if (walkFrames != null && walkFrames.Length > 0)
                sr.sprite = walkFrames[Mathf.FloorToInt(time * (kind == Kind.Taxi ? TaxiDriveFps : FramesPerSecond)) % walkFrames.Length];
            sr.flipX = facesRight ? vx < 0f : vx > 0f;
        }

        void Walk(float dt)
        {
            // Le taxi s'arrête le temps de son demi-tour.
            if (turnTime > 0f) { turnTime -= dt; return; }
            float dir = Mathf.Sign(vx);
            var p = (Vector2)transform.position;
            float half = box.size.x * Mathf.Abs(transform.lossyScale.x) / 2f;
            // Obstacle devant (à mi-hauteur) ou vide devant (sous le pied avant) : demi-tour.
            bool wall = Blocked(p + new Vector2(0f, 0.3f), Vector2.right * dir, half + 0.05f);
            bool ground = Blocked(p + new Vector2(dir * (half + 0.1f), 0.2f), Vector2.down, 0.5f);
            if (wall || !ground)
            {
                vx = -vx;
                if (kind == Kind.Taxi) turnTime = TaxiTurnTime;
                if (charging)
                {
                    // Fin de la charge contre un obstacle ou au bord : le gbaka qui percute reste sonné.
                    charging = false;
                    chargeCooldown = 1.2f;
                    if (wall && (kind == Kind.Gbaka || kind == Kind.Hippo)) { dizzy = 1f; SoundFx.Play("bump"); }
                }
                return;
            }
            p += new Vector2((charging ? Mathf.Sign(vx) * chargeSpeed : vx) * dt, 0f);
            // Sur une pente : il suit la hauteur du sol sous lui.
            int n = Physics2D.Raycast(p + Vector2.up * 0.6f, Vector2.down, solid, hits, 1.2f);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c.GetComponentInParent<HeroController>() != null || c.GetComponentInParent<Enemy>() != null) continue;
                p.y = hits[i].point.y;
                break;
            }
            transform.position = p;
        }

        void Fly(float dt)
        {
            var p = transform.position;
            p.x += vx * dt;
            if (Mathf.Abs(p.x - basePos.x) > flyRange)
            {
                p.x = basePos.x + Mathf.Sign(p.x - basePos.x) * flyRange;
                vx = -vx;
            }
            p.y = basePos.y + Mathf.Sin(time * 3f) * flyBob;
            transform.position = p;
        }

        /// <summary>Gbaka, charrette : charge quand Yao est devant, assez près, à peu près à la même hauteur.</summary>
        void LookForHero(float dt)
        {
            if (chargeCooldown > 0f) { chargeCooldown -= dt; return; }
            if (charging) return;
            if (heroCache == null) heroCache = FindAnyObjectByType<HeroController>();
            if (heroCache == null || heroCache.IsDying) return;
            float dx = heroCache.transform.position.x - transform.position.x;
            if (dx * Mathf.Sign(vx) > 0f && Mathf.Abs(dx) < sight && Mathf.Abs(heroCache.transform.position.y - transform.position.y) < 1.5f)
            {
                charging = true;
                SoundFx.Play("bump");
            }
        }

        /// <summary>Singe : repère Yao, se tourne vers lui et lance une noix de coco ; vrai pendant le lancer (il ne marche pas).</summary>
        bool MonkeyThrow(float dt)
        {
            if (throwCooldown > 0f) throwCooldown -= dt;
            if (heroCache == null) heroCache = FindAnyObjectByType<HeroController>();
            var hero = heroCache;
            if (throwTime < 0f)
            {
                if (throwCooldown > 0f || hero == null || hero.IsDying || throwFrames == null || throwFrames.Length == 0) return false;
                var d = hero.transform.position - transform.position;
                if (Mathf.Abs(d.x) > ThrowRange || Mathf.Abs(d.y) > ThrowHeight) return false;
                vx = (d.x >= 0f ? 1f : -1f) * Mathf.Abs(vx);
                throwTime = 0f;
                thrown = false;
            }
            throwTime += dt;
            sr.sprite = throwFrames[Mathf.Min(throwFrames.Length - 1, Mathf.FloorToInt(throwTime / ThrowTime * throwFrames.Length))];
            sr.flipX = facesRight ? vx < 0f : vx > 0f;
            if (!thrown && throwTime >= ThrowRelease) { thrown = true; ThrowCoconut(hero); }
            if (throwTime >= ThrowTime) { throwTime = -1f; throwCooldown = ThrowCooldown; }
            return true;
        }

        /// <summary>Noix de coco en cloche, visée sur l'endroit où est Yao (elle part vers le haut à 18 cases/s).</summary>
        void ThrowCoconut(HeroController hero)
        {
            if (coconutFrames == null || coconutFrames.Length == 0) return;
            float dir = vx >= 0f ? 1f : -1f;
            var from = transform.position + new Vector3(dir * 0.35f, 1.2f, 0f);
            float vxc = dir * 3f;
            if (hero != null)
            {
                // Durée du vol jusqu'à la hauteur de Yao (pesanteur de la noix : 58,5 cases/s²), puis vitesse de côté.
                float drop = from.y - (hero.transform.position.y + 0.7f);
                float t = (CoconutUp + Mathf.Sqrt(CoconutUp * CoconutUp + 2f * 58.5f * Mathf.Max(-2f, drop))) / 58.5f;
                vxc = Mathf.Clamp((hero.transform.position.x - from.x) / Mathf.Max(0.3f, t), -12f, 12f);
                if (Mathf.Abs(vxc) < 3f) vxc = dir * 3f;
            }
            var go = new GameObject("Noix_De_Coco");
            go.transform.position = from;
            var image = new GameObject("Image").AddComponent<SpriteRenderer>();
            image.transform.SetParent(go.transform, false);
            image.sprite = coconutFrames[0];
            image.sharedMaterial = sr.sharedMaterial;
            image.sortingLayerID = sr.sortingLayerID;
            image.sortingOrder = sr.sortingOrder + 1;
            float s = nutSize / coconutFrames[0].bounds.size.y;
            image.transform.localScale = new Vector3(s, s, 1f);
            image.transform.localPosition = new Vector3(0f, -nutSize / 2f, 0f);
            var nut = go.AddComponent<Coconut>();
            nut.frames = coconutFrames;
            nut.velocity = new Vector2(vxc, CoconutUp);
            SoundFx.Play("jump");
        }

        // ------------------------------------------------------------------ poissons et caïmans (stage 7)

        [Header("Poissons et caïmans")]
        [Tooltip("Caïman : sortie de l'eau (3 images) et bond / nage sous l'eau (images d'attaque).")]
        public Sprite[] lungeFrames;
        // Poisson-tigre : il repère Yao qui nage (portée « sight », 2 cases de haut) et fonce 1,2 s vers lui.
        // Caïman : bond hors de l'eau quand Yao passe au-dessus (2,6 cases de côté), poursuite sous l'eau (6 cases).
        const float DashTime = 1.2f, DashCooldown = 1.6f, LungeUp = 17f, LungeGravity = 40f, LungeReach = 2.6f, ChaseRange = 6f, ChaseSpeed = 3.2f;
        SwimWater lake;
        float dashTime, lungeTime = -1f, lungeVy, stunned;
        Vector2 dashDir;

        void Swim(float dt)
        {
            if (lake == null) lake = SwimWater.At((Vector2)basePos + Vector2.up * (kind == Kind.Caiman ? -0.3f : 0.3f)); // le caïman est posé à la surface
            if (heroCache == null) heroCache = FindAnyObjectByType<HeroController>();
            var hero = heroCache;
            var p = (Vector2)transform.position;
            Vector2 toHero = hero != null ? (Vector2)hero.transform.position + Vector2.up * 0.4f - p : Vector2.zero;
            bool heroInLake = hero != null && hero.Swimming && lake != null && SwimWater.At(hero.transform.position + Vector3.up * 0.3f) == lake;
            if (stunned > 0f) stunned -= dt;
            if (chargeCooldown > 0f) chargeCooldown -= dt;
            Sprite[] frames = walkFrames;
            float fps = 8f;

            if (kind == Kind.Caiman)
            {
                float surfaceY = lake != null ? lake.SurfaceY : basePos.y;
                float floatY = surfaceY - 0.6f; // la ligne d'eau des images de nage est à 0,6 case du bas
                if (lungeTime >= 0f)
                {
                    // Bond hors de l'eau : il sort (3 images), saute gueule ouverte, retombe dans l'eau.
                    lungeTime += dt;
                    if (lungeTime > 0.2f)
                    {
                        lungeVy -= LungeGravity * dt;
                        p.y += lungeVy * dt;
                        frames = lungeFrames != null && lungeFrames.Length > 3 ? new[] { lungeFrames[3 + Mathf.Min(2, Mathf.FloorToInt((lungeTime - 0.2f) * 6f))] } : walkFrames;
                        if (p.y <= floatY && lungeVy < 0f) { p.y = floatY; lungeTime = -1f; chargeCooldown = 1.2f; }
                    }
                    else frames = lungeFrames != null && lungeFrames.Length >= 3 ? new[] { lungeFrames[Mathf.Min(2, Mathf.FloorToInt(lungeTime / 0.2f * 3f))] } : walkFrames;
                    transform.position = p;
                    SetSwimSprite(frames, 12f);
                    return;
                }
                if (stunned <= 0f && heroInLake && toHero.magnitude < ChaseRange)
                {
                    // Yao nage près de lui : il plonge et le poursuit (images d'attaque, de côté).
                    var step = toHero.normalized * ChaseSpeed * dt;
                    p += step;
                    if (Mathf.Abs(step.x) > 0.001f) vx = Mathf.Sign(step.x) * Mathf.Abs(vx);
                    frames = chargeFrames;
                    fps = 9f;
                }
                else
                {
                    // En surface : il patrouille et guette Yao qui passe au-dessus de l'eau.
                    p.x += (stunned > 0f ? 0f : vx) * dt;
                    if (Mathf.Abs(p.x - basePos.x) > flyRange) { p.x = basePos.x + Mathf.Sign(p.x - basePos.x) * flyRange; vx = -vx; }
                    p.y = Mathf.MoveTowards(p.y, floatY, 3f * dt);
                    if (hero != null && !hero.Swimming && !hero.IsDying && chargeCooldown <= 0f && stunned <= 0f && Mathf.Abs(p.y - floatY) < 0.1f
                        && Mathf.Abs(toHero.x) < LungeReach && hero.transform.position.y > surfaceY - 0.2f && hero.transform.position.y < surfaceY + 6f)
                    {
                        lungeTime = 0f;
                        lungeVy = LungeUp;
                        vx = Mathf.Sign(toHero.x) * Mathf.Abs(vx);
                        SoundFx.Play("bump");
                    }
                }
            }
            else
            {
                if (kind == Kind.Tigerfish && dashTime <= 0f && chargeCooldown <= 0f && heroInLake
                    && Mathf.Abs(toHero.x) < sight && Mathf.Abs(toHero.y) < 2.5f)
                {
                    dashTime = DashTime;
                    dashDir = toHero.normalized;
                    vx = Mathf.Sign(toHero.x) * Mathf.Abs(vx);
                    SoundFx.Play("bump");
                }
                if (dashTime > 0f)
                {
                    // Le poisson-tigre fonce, gueule ouverte.
                    dashTime -= dt;
                    p += dashDir * chargeSpeed * dt;
                    if (dashTime <= 0f) { chargeCooldown = DashCooldown; basePos = new Vector2(basePos.x, p.y); }
                    frames = chargeFrames != null && chargeFrames.Length > 0 ? chargeFrames : walkFrames;
                    fps = 14f;
                }
                else
                {
                    // Patrouille en ondulant ; il revient peu à peu à sa profondeur.
                    p.x += vx * dt;
                    if (Mathf.Abs(p.x - basePos.x) > flyRange) { p.x = basePos.x + Mathf.Sign(p.x - basePos.x) * flyRange; vx = -vx; }
                    p.y = Mathf.MoveTowards(p.y, basePos.y + Mathf.Sin(time * 2f) * flyBob, 1.5f * dt);
                }
            }
            // Il reste dans son lac.
            if (lake != null)
            {
                float halfW = box.size.x * Mathf.Abs(transform.lossyScale.x) / 2f, h = box.size.y * Mathf.Abs(transform.lossyScale.y);
                p.x = Mathf.Clamp(p.x, lake.MinX + halfW, lake.MaxX - halfW);
                p.y = Mathf.Clamp(p.y, lake.BottomY + 0.1f, kind == Kind.Caiman ? lake.SurfaceY : lake.SurfaceY - h - 0.1f);
            }
            transform.position = p;
            SetSwimSprite(frames, fps);
        }

        void SetSwimSprite(Sprite[] frames, float fps)
        {
            if (stunned > 0f && hitFrames != null && hitFrames.Length > 0) frames = hitFrames;
            if (frames != null && frames.Length > 0) sr.sprite = frames[Mathf.FloorToInt(time * fps) % frames.Length];
            sr.flipX = facesRight ? vx < 0f : vx > 0f;
        }

        /// <summary>Caïman touché sans être vaincu : sonné un moment (il ne mord pas pendant ce temps).</summary>
        void SwimmerHit()
        {
            hp--;
            stunned = 0.8f;
            stompGuard = 0.4f;
            lungeTime = -1f;
            SoundFx.Play("bump");
        }

        bool Blocked(Vector2 origin, Vector2 direction, float distance)
        {
            int n = Physics2D.Raycast(origin, direction, solid, hits, distance);
            for (int i = 0; i < n; i++)
                if (hits[i].collider.GetComponentInParent<HeroController>() == null && hits[i].collider.GetComponentInParent<Enemy>() == null) return true;
            return false;
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (dead || !awake) return;
            var hero = other.GetComponentInParent<HeroController>();
            if (hero == null || hero.IsDying) return;
            if (stompGuard > 0f) return; // il vient d'encaisser un saut : Yao rebondit, pas de second contact
            // Écrasé (règle du téléphone) : le héros tombait, et juste avant ce pas ses pieds étaient au-dessus du haut
            // de l'ennemi, à une demi-case près. Sinon c'est un contact de côté ou par-dessous.
            float top = box.bounds.max.y;
            bool falling = hero.PreviousVelocityY < 0.5f || hero.Velocity.y < 0f;
            if (falling && hero.PreviousFeetY >= top - 0.5f)
            {
                Stomp();
                hero.Bounce();
            }
            else hero.Hurt();
        }

        /// <summary>Touché par une boule de feu : éjecté, il retombe hors de l'écran à l'envers (200 points, comme le
        /// téléphone). Faux s'il était déjà hors jeu.</summary>
        public bool Knock(int direction)
        {
            if (dead || !awake) return false;
            if (kind == Kind.Caiman && hp > 1) { SwimmerHit(); return true; }
            if (IsCharger && hp > 1) { Hit(); return true; }
            if (kind == Kind.Taxi) { StageState.Instance?.AddKnock(); Explode(); return true; }
            if (kind == Kind.Gbaka || kind == Kind.Hippo) { StageState.Instance?.AddKnock(); SpinOut(direction); return true; }
            dead = knocked = true;
            deadTime = 0f;
            box.enabled = false;
            fallSpeed = -15f; // petit saut vers le haut
            knockDirection = direction;
            sr.flipY = true;
            StageState.Instance?.AddKnock();
            return true;
        }

        bool knocked;
        int knockDirection;

        /// <summary>Gbaka vaincu (téléphone) : éjecté d'un bond, il tournoie, sonné, et tombe hors de l'écran.</summary>
        void SpinOut(int direction)
        {
            dead = knocked = spinning = true;
            deadTime = 0f;
            box.enabled = false;
            fallSpeed = -15f;
            knockDirection = direction;
            if (hitFrames != null && hitFrames.Length > 0) sr.sprite = hitFrames[0];
            SoundFx.Play("break");
        }

        bool spinning;

        /// <summary>Taxi : il explose sur place puis disparaît.</summary>
        void Explode()
        {
            dead = exploding = true;
            deadTime = 0f;
            box.enabled = false;
            if (boomScale != 1f) transform.localScale *= boomScale;
            SoundFx.Play("break");
        }

        /// <summary>Fonceur touché sans être vaincu : sonné un moment.</summary>
        void Hit()
        {
            hp--;
            dizzy = kind == Kind.Gbaka ? 1.2f : 0.8f;
            charging = false;
            chargeCooldown = 0.5f;
            stompGuard = 0.3f;
            SoundFx.Play("bump");
        }

        void Stomp()
        {
            if (kind == Kind.Caiman && hp > 1) { SwimmerHit(); return; }
            if (IsCharger && hp > 1) { Hit(); return; }
            if (kind == Kind.Taxi) { StageState.Instance?.AddStomp(); Explode(); return; }
            if (kind == Kind.Gbaka || kind == Kind.Hippo) { StageState.Instance?.AddStomp(); SpinOut(vx > 0f ? -1 : 1); return; }
            dead = true;
            deadTime = 0f;
            box.enabled = false;
            StageState.Instance?.AddStomp();
            if (squashedSprite != null) sr.sprite = squashedSprite;
            else if (!IsFlyer) transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y * 0.45f, 1f);
        }

        void UpdateDead(float dt)
        {
            deadTime += dt;
            if (exploding)
            {
                if (boomFrames != null && boomFrames.Length > 0)
                    sr.sprite = boomFrames[Mathf.Min(boomFrames.Length - 1, Mathf.FloorToInt(deadTime / TaxiBoomFrame))];
                if (deadTime > TaxiBoomFrame * 5.2f) gameObject.SetActive(false);
                return;
            }
            if (knocked)
            {
                fallSpeed += 50f * dt;
                transform.position += new Vector3(knockDirection * 2f * dt, -fallSpeed * dt, 0f);
                if (spinning) transform.Rotate(0f, 0f, 360f * dt * -knockDirection);
                if (deadTime > 2f) gameObject.SetActive(false);
            }
            else if (IsSwimmer)
            {
                // Vaincu dans l'eau : il remonte le ventre en l'air et s'efface.
                sr.flipY = true;
                transform.position += Vector3.up * (1.2f * dt);
                var c = sr.color;
                c.a = Mathf.Clamp01(1f - (deadTime - 0.8f) / 0.7f);
                sr.color = c;
                if (deadTime > 1.5f) gameObject.SetActive(false);
            }
            else if (IsFlyer)
            {
                // L'oiseau écrasé tombe en vrille.
                fallSpeed += 40f * dt;
                transform.position += Vector3.down * (fallSpeed * dt);
                transform.Rotate(0f, 0f, 540f * dt);
                if (deadTime > 1.5f) gameObject.SetActive(false);
            }
            else if (deadTime > 0.4f) gameObject.SetActive(false);
        }
    }
}
