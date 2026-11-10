using System.Collections.Generic;
using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Le racketteur du péage du pont HKB, boss du stage 2, règles de la version téléphone (boss.ts, « toll ») :
    /// il attend sous l'auvent ; quand Yao entre dans l'arène, les barrières se ferment et la caméra se fixe.
    /// Il marche vers Yao, lève la main puis, en alternance, lance une volée de tickets en cloche (deux, trois quand il
    /// est énervé) ou charge jusqu'à la barrière, où il reste sonné. Un saut sur sa tête lui retire 2 points de vie,
    /// une boule de feu 1 ; touché, il recule d'un bond et clignote. Sous la moitié de sa vie, il est plus rapide.
    /// Vaincu, il s'écroule, les barrières s'ouvrent et l'arrivée est libre. Si Yao perd une vie pendant le combat et
    /// repart du point de contrôle (hors de l'arène), le combat recommence à zéro.
    /// Durées et vitesses du téléphone converties : 60 images/s, 1 px/image = 3,75 cases/s.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public class TollBoss : MonoBehaviour
    {
        public Sprite[] idle, prep, walk, throwing, charge, impact, stunned, defeat, ticket;
        [Tooltip("Bords de l'arène, en x (cases).")]
        public float arenaX0, arenaX1;
        public int maxHp = 6;
        [Tooltip("Barrières invisibles de l'arène, actives pendant le combat.")]
        public Collider2D[] walls;
        public float ticketHeight = 0.7f;
        public string bossName = "LE RACKETTEUR DU PÉAGE";

        public static TollBoss Current { get; private set; }
        public int Hp => hp;
        public bool Fighting => mode != Mode.Wait && mode != Mode.Dead;
        public bool Defeated => mode == Mode.Dead;

        enum Mode { Wait, Idle, Walk, Alert, Cast, Charge, Stunned, Recoil, Dead }
        const float Unit = 3.75f, Gravity = 139.5f, F = 1f / 60f;

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
        readonly List<BossTicket> tickets = new List<BossTicket>();

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            start = transform.position;
            groundY = start.y;
            hp = maxHp;
            Current = this;
            SetWalls(false);
        }

        void Start()
        {
            follow = FindAnyObjectByType<CameraFollow2D>();
            if (follow != null) { camMin = follow.levelMinX; camMax = follow.levelMaxX; }
        }

        void OnDestroy() { if (Current == this) Current = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // pause
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
            // Yao a perdu une vie et repart du point de contrôle, hors de l'arène : tout recommence.
            if (hero == null || hero.transform.position.x < arenaX0 - 0.5f) { ResetFight(); return; }

            float hx = hero.transform.position.x, bx = transform.position.x;
            int toward = hx < bx ? -1 : 1;
            float distance = Mathf.Abs(hx - bx);
            bool angry = hp <= maxHp / 2f;
            timer -= dt;
            switch (mode)
            {
                case Mode.Idle:
                    vx = 0f;
                    facing = toward;
                    if (timer <= 0f)
                    {
                        if (distance > 6.9f && Random.value < 0.5f) Set(Mode.Walk, 70 * F);
                        else Set(Mode.Alert, (angry ? 22 : 36) * F);
                    }
                    break;
                case Mode.Walk:
                    facing = toward;
                    vx = toward * (angry ? 1.1f : 0.75f) * Unit;
                    if (timer <= 0f || distance < 3.75f) Set(Mode.Alert, (angry ? 22 : 36) * F);
                    break;
                case Mode.Alert:
                    vx = 0f;
                    facing = toward;
                    if (timer <= 0f)
                    {
                        // Il alterne : une volée de tickets, puis une charge à la barrière.
                        sinceCast++;
                        if (onGround && (sinceCast >= 2 || Random.value < (angry ? 0.5f : 0.4f)))
                        {
                            sinceCast = 0;
                            Set(Mode.Cast, (angry ? 34 : 44) * F);
                        }
                        else
                        {
                            Set(Mode.Charge, 99f);
                            vx = facing * (angry ? 3.3f : 2.6f) * Unit;
                        }
                    }
                    break;
                case Mode.Cast:
                    vx = 0f;
                    facing = toward;
                    if (timer <= 0f)
                    {
                        Throw(angry);
                        Set(Mode.Idle, (angry ? 26 : 40) * F);
                    }
                    break;
                case Mode.Stunned:
                    vx = 0f;
                    if (timer <= 0f) Set(Mode.Idle, (angry ? 30 : 55) * F);
                    break;
            }

            // Déplacement dans l'arène (sol plat), saut de recul.
            var p = transform.position;
            p.x += vx * dt;
            if (!onGround)
            {
                vy -= Gravity * dt;
                p.y += vy * dt;
                if (p.y <= groundY) { p.y = groundY; vy = 0f; onGround = true; }
            }
            float half = box.size.x * Mathf.Abs(transform.lossyScale.x) / 2f;
            bool atEdge = false;
            if (p.x < arenaX0 + half) { p.x = arenaX0 + half; atEdge = true; }
            else if (p.x > arenaX1 - half) { p.x = arenaX1 - half; atEdge = true; }
            transform.position = p;

            if (mode == Mode.Charge && atEdge)
            {
                // Il fonce dans la barrière et reste sonné : c'est le moment de lui sauter dessus.
                Set(Mode.Stunned, (angry ? 50 : 75) * F);
                vx = 0f;
                SoundFx.Play("bump");
            }
            else if (mode == Mode.Recoil)
            {
                if (atEdge) vx = 0f;
                if (onGround) { vx = 0f; Set(Mode.Idle, (angry ? 30 : 45) * F); }
            }
            Animate();
        }

        void Set(Mode m, float t)
        {
            mode = m;
            timer = t;
            since = 0f;
        }

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
            foreach (var t in tickets) t.gameObject.SetActive(false);
        }

        void RestoreCamera()
        {
            if (follow != null) { follow.levelMinX = camMin; follow.levelMaxX = camMax; }
        }

        void SetWalls(bool on)
        {
            if (walls == null) return;
            foreach (var w in walls) if (w != null) w.enabled = on;
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (mode == Mode.Wait || mode == Mode.Dead) return;
            var h = other.GetComponentInParent<HeroController>();
            if (h == null || h.IsDying || stompGuard > 0f) return;
            // Saut sur la tête (même règle que les ennemis) : Yao rebondit ; sinon il est blessé.
            float top = box.bounds.max.y;
            bool falling = h.PreviousVelocityY < 0.5f || h.Velocity.y < 0f;
            if (falling && h.PreviousFeetY >= top - 0.6f)
            {
                h.Bounce();
                stompGuard = 0.35f;
                if (flash <= 0f) Hit(2, 50 * F);
            }
            // Yao qui remonte après un rebond, pieds au-dessus du milieu : pas un coup de côté.
            else if (!(h.Velocity.y > 0f && h.transform.position.y > box.bounds.center.y)) h.Hurt();
        }

        /// <summary>Juste après un rebond sur sa tête, Yao le touche encore un instant en remontant : ignoré.</summary>
        float stompGuard;

        /// <summary>Touché par une boule de feu : 1 point de vie. Faux s'il ne peut pas être touché maintenant.</summary>
        public bool FireHit()
        {
            if (!Fighting) return false;
            if (flash <= 0f) Hit(1, 40 * F);
            return true;
        }

        void Hit(int damage, float flashTime)
        {
            hp = Mathf.Max(0, hp - damage);
            flash = flashTime;
            if (hp <= 0) { Defeat(); return; }
            StageState.Instance?.AddBossHit();
            // Touché, il recule d'un bond loin de Yao.
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
            foreach (var t in tickets) t.gameObject.SetActive(false);
            StageState.Instance?.AddBossDown();
        }

        /// <summary>Volée de tickets lancés en cloche autour de Yao.</summary>
        void Throw(bool angry)
        {
            if (hero == null || ticket == null || ticket.Length == 0) return;
            int n = angry ? 3 : 2;
            var hand = transform.position + new Vector3(facing * 0.75f, 2.2f, 0f);
            for (int i = 0; i < n; i++)
            {
                float target = hero.transform.position.x + (i - (n - 1) / 2f) * 1.875f;
                const float flight = 46f / 60f;
                Ticket().Launch(hand, (target - hand.x) / flight, (5.2f + 0.5f * i) * Unit, groundY);
            }
            SoundFx.Play("shoot");
        }

        BossTicket Ticket()
        {
            foreach (var t in tickets) if (!t.gameObject.activeSelf) return t;
            var go = new GameObject($"Ticket_{tickets.Count + 1}");
            var tsr = go.AddComponent<SpriteRenderer>();
            tsr.sharedMaterial = sr.sharedMaterial;
            tsr.sortingLayerName = sr.sortingLayerName;
            tsr.sortingOrder = sr.sortingOrder + 2;
            tsr.sprite = ticket[0];
            float s = ticketHeight / ticket[0].bounds.size.y;
            go.transform.localScale = new Vector3(s, s, 1f);
            var circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.3f / s;
            circle.offset = new Vector2(0f, ticketHeight / 2f / s);
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var bt = go.AddComponent<BossTicket>();
            bt.frames = ticket;
            tickets.Add(bt);
            return bt;
        }

        void Animate()
        {
            Sprite[] set = idle;
            int frame = Mathf.FloorToInt(clock * 2.5f);
            switch (mode)
            {
                case Mode.Walk: set = walk; frame = Mathf.FloorToInt(clock * 10f); break;
                case Mode.Alert: set = prep; frame = since < 18 * F ? 0 : 1; break;
                case Mode.Cast: set = throwing; frame = Mathf.FloorToInt(since / (10 * F)); break;
                case Mode.Charge:
                    set = charge;
                    frame = since < 8 * F ? Mathf.FloorToInt(since / (4 * F)) : 2 + Mathf.FloorToInt(clock * 12f) % 2;
                    break;
                case Mode.Stunned:
                    if (since < 14 * F && impact != null && impact.Length > 0) { set = impact; frame = 0; }
                    else { set = stunned; frame = Mathf.FloorToInt(clock * 3f); }
                    break;
                case Mode.Recoil: set = stunned; frame = 0; break;
                case Mode.Dead: set = defeat; frame = Mathf.FloorToInt(since / (14 * F)); break;
            }
            if (set == null || set.Length == 0) set = idle;
            if (set == null || set.Length == 0) return;
            bool clampLast = mode == Mode.Cast || mode == Mode.Dead || mode == Mode.Charge;
            frame = clampLast ? Mathf.Min(frame, set.Length - 1) : frame % set.Length;
            if (mode == Mode.Charge && frame >= set.Length) frame = set.Length - 1;
            sr.sprite = set[frame];
            sr.flipX = facing < 0;
            // Touché : il clignote.
            sr.color = flash > 0f && Mathf.FloorToInt(flash * 15f) % 2 == 0 ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }
    }
}
