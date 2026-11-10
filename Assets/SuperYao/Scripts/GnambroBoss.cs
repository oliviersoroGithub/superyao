using System.Collections.Generic;
using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Le gnambro du corridor de Gesco, chef des rabatteurs et boss du stage 5, règles de la version téléphone (boss.ts,
    /// « gnambro ») : quand Yao entre dans l'arène, les barrières se ferment et la caméra se fixe. Le gnambro crie
    /// (« Yopougon ! »), puis alterne une volée de sacs de voyage lancés en cloche (un, deux quand il est énervé) et
    /// l'appel d'un gbaka qui traverse l'arène à toute allure depuis la barrière la plus éloignée de Yao et s'écrase contre
    /// l'autre (on peut rebondir sur son toit ; le toucher de côté blesse). Le gnambro court alors se disputer avec le
    /// chauffeur : c'est le moment de lui sauter dessus (2 points de vie par saut, 1 par boule de feu). Il a 12 points de
    /// vie (troisième boss du téléphone) ; touché, il recule d'un bond et clignote. Vaincu, il s'écroule, les barrières
    /// s'ouvrent. Si Yao perd une vie et repart hors de l'arène, le combat recommence à zéro.
    /// Durées et vitesses du téléphone converties : 60 images/s, 1 px/image = 3,75 cases/s.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public class GnambroBoss : MonoBehaviour
    {
        public Sprite[] idle, shout, argue, run, hit, defeat, bag, bus;
        [Tooltip("Bords de l'arène, en x (cases).")]
        public float arenaX0, arenaX1;
        public int maxHp = 12;
        [Tooltip("Barrières invisibles de l'arène, actives pendant le combat.")]
        public Collider2D[] walls;
        [Tooltip("Le gbaka appelé par le gnambro (enfant, caché entre deux passages).")]
        public SpriteRenderer busRenderer;
        public BoxCollider2D busBox;
        public float bagHeight = 0.7f;

        enum Mode { Wait, Idle, Alert, Cast, Walk, Stunned, Recoil, Dead }
        const float Unit = 3.75f, Gravity = 139.5f, F = 1f / 60f;
        const float BusSpeed = 3.4f * Unit, BusWreckTime = 160 * F, BusFlashTime = 24 * F, BusWidth = 3.4f;

        SpriteRenderer sr;
        BoxCollider2D box;
        HeroController hero;
        CameraFollow2D follow;
        float camMin, camMax, groundY;
        Vector3 start;
        Mode mode = Mode.Wait;
        float timer, since, flash, vx, vy, clock;
        int hp, facing = -1, sinceCast;
        bool onGround = true;
        // Le gbaka : en route (crash < 0), puis écrasé contre la barrière (crash = temps écoulé depuis le choc).
        bool busActive;
        int busDir;
        float busCrash = -1f;
        readonly List<BossTicket> bags = new List<BossTicket>();
        /// <summary>Juste après un rebond (sur sa tête ou sur le toit du gbaka), Yao touche encore le boss un instant en
        /// remontant : ce contact ne doit pas compter comme un coup de côté (il perdait une vie).</summary>
        float stompGuard;
        const float StompGuardTime = 0.35f;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            start = transform.position;
            groundY = start.y;
            hp = maxHp;
            SetWalls(false);
            HideBus();
        }

        void Start()
        {
            follow = FindAnyObjectByType<CameraFollow2D>();
            if (follow != null) { camMin = follow.levelMinX; camMax = follow.levelMaxX; }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (hero == null) hero = FindAnyObjectByType<HeroController>();
            clock += dt;
            since += dt;
            if (flash > 0f) flash -= dt;
            if (stompGuard > 0f) stompGuard -= dt;

            if (mode == Mode.Dead) { Animate(); return; }
            if (mode == Mode.Wait)
            {
                Animate();
                if (hero != null && !hero.IsDying && hero.transform.position.x > arenaX0 + 1.5f) BeginFight();
                return;
            }
            if (hero == null || hero.transform.position.x < arenaX0 - 0.5f) { ResetFight(); return; }

            bool angry = hp <= maxHp / 2f;
            int toward = hero.transform.position.x < transform.position.x ? -1 : 1;
            StepBus(dt);
            // Le gbaka s'est écrasé : dès qu'il a les mains libres, il court se disputer avec le chauffeur.
            if (busActive && busCrash >= 0f && (mode == Mode.Idle || mode == Mode.Alert)) Set(Mode.Walk, 99f);
            timer -= dt;
            switch (mode)
            {
                case Mode.Idle:
                    vx = 0f;
                    facing = toward;
                    if (timer <= 0f) Set(Mode.Alert, (angry ? 26 : 38) * F);
                    break;
                case Mode.Alert:
                    vx = 0f;
                    facing = toward;
                    if (timer <= 0f)
                    {
                        // Il alterne : une volée de sacs, puis un gbaka.
                        if (!busActive && sinceCast >= 1)
                        {
                            CallBus();
                            sinceCast = 0;
                            Set(Mode.Idle, 90 * F);
                        }
                        else
                        {
                            sinceCast++;
                            Set(Mode.Cast, (angry ? 26 : 34) * F);
                        }
                    }
                    break;
                case Mode.Cast:
                    vx = 0f;
                    facing = toward;
                    if (timer <= 0f)
                    {
                        ThrowBags(angry);
                        Set(Mode.Idle, (angry ? 40 : 60) * F);
                    }
                    break;
                case Mode.Walk:
                    if (!busActive) { Set(Mode.Idle, 30 * F); break; }
                    float dx = busRenderer.transform.position.x - transform.position.x;
                    facing = dx < 0f ? -1 : 1;
                    vx = facing * 2.4f * Unit;
                    if (Mathf.Abs(dx) < BusWidth / 2f + 0.75f) { vx = 0f; Set(Mode.Stunned, (angry ? 100 : 130) * F); }
                    break;
                case Mode.Stunned:
                    // Il crie sur le chauffeur et ne regarde plus Yao ; puis le gbaka repart.
                    vx = 0f;
                    if (timer <= 0f) { HideBus(); Set(Mode.Idle, 40 * F); }
                    break;
                case Mode.Recoil:
                    if (busActive && busCrash >= 0f) HideBus();
                    if (onGround && since > 6 * F) Set(Mode.Idle, (angry ? 30 : 45) * F);
                    break;
            }

            var p = transform.position;
            p.x += vx * dt;
            if (!onGround)
            {
                vy -= Gravity * dt;
                p.y += vy * dt;
                if (p.y <= groundY) { p.y = groundY; vy = 0f; onGround = true; if (mode == Mode.Recoil) vx = 0f; }
            }
            float half = box.size.x * Mathf.Abs(transform.lossyScale.x) / 2f;
            p.x = Mathf.Clamp(p.x, arenaX0 + half, arenaX1 - half);
            transform.position = p;
            Animate();
        }

        void Set(Mode m, float t) { mode = m; timer = t; since = 0f; }

        void BeginFight()
        {
            Set(Mode.Alert, 50 * F);
            SetWalls(true);
            if (follow != null) { follow.levelMinX = arenaX0 - 1f; follow.levelMaxX = arenaX1 + 1f; }
            SoundFx.Play("bump");
        }

        void ResetFight()
        {
            mode = Mode.Wait;
            hp = maxHp;
            flash = vx = vy = 0f;
            onGround = true;
            sinceCast = 0;
            facing = -1;
            transform.position = start;
            SetWalls(false);
            RestoreCamera();
            HideBus();
            foreach (var b in bags) b.gameObject.SetActive(false);
        }

        void RestoreCamera() { if (follow != null) { follow.levelMinX = camMin; follow.levelMaxX = camMax; } }

        void SetWalls(bool on)
        {
            if (walls == null) return;
            foreach (var w in walls) if (w != null) w.enabled = on;
        }

        // ---------------------------------------------------------------- le gbaka

        /// <summary>Le gbaka part de la barrière la plus éloignée de Yao et fonce vers lui.</summary>
        void CallBus()
        {
            if (busRenderer == null) return;
            busDir = hero.transform.position.x > (arenaX0 + arenaX1) / 2f ? 1 : -1;
            float x = busDir > 0 ? arenaX0 + BusWidth / 2f : arenaX1 - BusWidth / 2f;
            busRenderer.transform.position = new Vector3(x, groundY, 0f);
            busRenderer.flipX = busDir < 0; // dessiné tourné vers la droite
            busRenderer.enabled = true;
            if (busBox != null) busBox.enabled = true;
            busActive = true;
            busCrash = -1f;
            SoundFx.Play("bump");
        }

        void HideBus()
        {
            busActive = false;
            busCrash = -1f;
            if (busRenderer != null) busRenderer.enabled = false;
            if (busBox != null) busBox.enabled = false;
        }

        void StepBus(float dt)
        {
            if (!busActive) return;
            var t = busRenderer.transform;
            if (busCrash < 0f)
            {
                var p = t.position;
                p.x += busDir * BusSpeed * dt;
                float edge = busDir > 0 ? arenaX1 - BusWidth / 2f : arenaX0 + BusWidth / 2f;
                if (busDir > 0 ? p.x >= edge : p.x <= edge)
                {
                    // Il s'écrase contre la barrière : le gnambro accourt dès qu'il a les mains libres.
                    p.x = edge;
                    busCrash = 0f;
                    SoundFx.Play("break");
                }
                t.position = p;
                busRenderer.sprite = bus[1 + Mathf.FloorToInt(clock * 15f) % 2];
            }
            else
            {
                busCrash += dt;
                busRenderer.sprite = busCrash < BusFlashTime ? bus[3] : bus[0];
                if (busCrash > BusWreckTime && mode != Mode.Walk && mode != Mode.Stunned) HideBus();
            }
        }

        /// <summary>Contact avec le gbaka (appelé par GnambroBus) : on rebondit sur son toit ; de côté, en route, il blesse.</summary>
        public void BusContact(HeroController h)
        {
            if (!busActive || h.IsDying || stompGuard > 0f) return;
            float top = busBox.bounds.max.y;
            bool falling = h.PreviousVelocityY < 0.5f || h.Velocity.y < 0f;
            if (falling && h.PreviousFeetY >= top - 0.5f) { h.Bounce(); stompGuard = StompGuardTime; }
            else if (busCrash < 0f && !Above(h, busBox)) h.Hurt();
        }

        // ---------------------------------------------------------------- coups

        void OnTriggerStay2D(Collider2D other)
        {
            if (mode == Mode.Wait || mode == Mode.Dead) return;
            var h = other.GetComponentInParent<HeroController>();
            if (h == null || h.IsDying || stompGuard > 0f) return;
            float top = box.bounds.max.y;
            bool falling = h.PreviousVelocityY < 0.5f || h.Velocity.y < 0f;
            if (falling && h.PreviousFeetY >= top - 0.6f)
            {
                h.Bounce();
                stompGuard = StompGuardTime;
                if (flash <= 0f) Hit(2, 50 * F);
            }
            else if (!Above(h, box)) h.Hurt();
        }

        /// <summary>Yao remonte avec les pieds au-dessus du milieu de la boîte : il vient de rebondir, ce n'est pas un coup.</summary>
        static bool Above(HeroController h, Collider2D c) => h.Velocity.y > 0f && h.transform.position.y > c.bounds.center.y;

        /// <summary>Touché par une boule de feu : 1 point de vie. Faux s'il ne peut pas être touché maintenant.</summary>
        public bool FireHit()
        {
            if (mode == Mode.Wait || mode == Mode.Dead) return false;
            if (flash <= 0f) Hit(1, 40 * F);
            return true;
        }

        void Hit(int damage, float flashTime)
        {
            hp = Mathf.Max(0, hp - damage);
            flash = flashTime;
            if (hp <= 0) { Defeat(); return; }
            StageState.Instance?.AddBossHit();
            int away = hero != null && transform.position.x < hero.transform.position.x ? -1 : 1;
            Set(Mode.Recoil, 99f);
            vx = away * 2.2f * Unit;
            vy = 5.5f * Unit;
            onGround = false;
        }

        void Defeat()
        {
            Set(Mode.Dead, 0f);
            box.enabled = false;
            flash = vx = vy = 0f;
            var p = transform.position;
            p.y = groundY;
            transform.position = p;
            SetWalls(false);
            RestoreCamera();
            HideBus();
            foreach (var b in bags) b.gameObject.SetActive(false);
            StageState.Instance?.AddBossDown();
        }

        /// <summary>Sacs de voyage lancés en cloche autour de Yao.</summary>
        void ThrowBags(bool angry)
        {
            if (hero == null || bag == null || bag.Length == 0) return;
            int n = angry ? 2 : 1;
            var hand = transform.position + new Vector3(facing * 0.9f, 2.4f, 0f);
            for (int i = 0; i < n; i++)
            {
                float target = hero.transform.position.x + (i - (n - 1) / 2f) * 2.125f;
                const float flight = 48f / 60f;
                Bag().Launch(hand, (target - hand.x) / flight, (5.4f + 0.6f * i) * Unit, groundY);
            }
            SoundFx.Play("shoot");
        }

        BossTicket Bag()
        {
            foreach (var b in bags) if (!b.gameObject.activeSelf) return b;
            var go = new GameObject($"Sac_{bags.Count + 1}");
            var bsr = go.AddComponent<SpriteRenderer>();
            bsr.sharedMaterial = sr.sharedMaterial;
            bsr.sortingLayerName = sr.sortingLayerName;
            bsr.sortingOrder = sr.sortingOrder + 2;
            bsr.sprite = bag[0];
            float s = bagHeight / bag[0].bounds.size.y;
            go.transform.localScale = new Vector3(s, s, 1f);
            var circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.35f / s;
            circle.offset = new Vector2(0f, bagHeight / 2f / s);
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var bt = go.AddComponent<BossTicket>();
            bt.frames = bag;
            bags.Add(bt);
            return bt;
        }

        void Animate()
        {
            Sprite[] set = idle;
            int frame = Mathf.FloorToInt(clock * 2f);
            switch (mode)
            {
                case Mode.Alert: set = shout; frame = Mathf.FloorToInt(since / (10 * F)); break; // « Yopougon ! Yopougon ! »
                case Mode.Cast: set = run; frame = since < 16 * F ? 1 : 0; break;
                case Mode.Walk: set = run; frame = Mathf.FloorToInt(clock * 12f); break;
                case Mode.Stunned: set = argue; frame = Mathf.FloorToInt(clock * 5f); break;
                case Mode.Recoil: set = hit; frame = 1; break;
                case Mode.Dead:
                    if (since < 20 * F) { set = hit; frame = 0; }
                    else { set = defeat; frame = Mathf.Min(defeat.Length - 1, Mathf.FloorToInt((since - 20 * F) / (24 * F))); }
                    break;
            }
            if (set == null || set.Length == 0) set = idle;
            if (set == null || set.Length == 0) return;
            sr.sprite = set[Mathf.Abs(frame) % set.Length];
            sr.flipX = facing < 0;
            sr.color = flash > 0f && Mathf.FloorToInt(flash * 15f) % 2 == 0 ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }
    }

}
