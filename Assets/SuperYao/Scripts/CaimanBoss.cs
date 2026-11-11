using System.Collections.Generic;
using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Le vieux caïman sacré, boss du stage 8, règles de la version téléphone (boss.ts, « caiman ») : il garde le bassin
    /// sacré de la clairière. Quand Yao entre dans l'arène, les barrières se ferment et la caméra se fixe. Il nage à la
    /// surface sous Yao, guette (seuls ses yeux dépassent), puis :
    /// - si Yao est proche, il surgit gueule ouverte (il mord), et reste un instant hors de l'eau, sonné : c'est le moment
    ///   de lui sauter sur la tête (2 points de vie) ;
    /// - sinon, il frappe l'eau de la queue : deux gerbes (trois quand il est énervé) partent en cloche vers Yao.
    /// Nouveauté : si Yao plonge dans le bassin, le caïman le poursuit sous l'eau (le toucher blesse ; lui tomber dessus
    /// en nageant le frappe). Lui sauter sur la tête quand il nage ou guette enlève 1 point (2 quand il est sonné hors de
    /// l'eau). 12 points de vie ; une
    /// boule de feu enlève 1 point quand il est hors de l'eau. Vaincu, il coule ; les barrières s'ouvrent. Si Yao perd
    /// une vie et repart hors de l'arène, le combat recommence à zéro.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public class CaimanBoss : MonoBehaviour
    {
        public Sprite[] swim, lurk, bite, tail, under, splash;
        public SpriteRenderer image;
        [Tooltip("Bords de l'arène et du bassin, en x (cases), et hauteur de la surface du bassin.")]
        public float arenaX0, arenaX1, poolX0, poolX1, surfaceY, poolBottom;
        public int maxHp = 12;
        public Collider2D[] walls;
        [Tooltip("Cases par pixel des images du téléphone (42 px de jeu pour 265 px d'image, 16 px par case).")]
        public float unit = 42f / 16f / 265f;

        enum Mode { Wait, Swim, Lurk, Bite, Stunned, Dive, Tail, Chase, Dead }
        // Durées du téléphone (60 images/s) et réglages.
        const float F = 1f / 60f, Reach = 3.2f, SwimSpeed = 3.6f, ChaseSpeed = 3.2f, RiseTime = 0.15f;
        Mode mode = Mode.Wait;
        float timer, since, flash, stompGuard, clock, rise;
        int hp, facing = -1, sinceCast;
        bool thrown;
        BoxCollider2D box;
        HeroController hero;
        CameraFollow2D follow;
        float camMin, camMax;
        Vector3 start;
        readonly List<BossTicket> splashes = new List<BossTicket>();

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            start = transform.position;
            hp = maxHp;
            SetWalls(false);
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
            timer -= dt;
            if (flash > 0f) flash -= dt;
            if (stompGuard > 0f) stompGuard -= dt;
            if (mode == Mode.Dead) { Sink(dt); return; }
            if (mode == Mode.Wait)
            {
                Pose(lurk, 4f, -56f);
                if (hero != null && !hero.IsDying && hero.transform.position.x > arenaX0 + 1.5f) Begin();
                return;
            }
            if (hero == null || hero.transform.position.x < arenaX0 - 0.5f) { ResetFight(); return; }

            bool angry = hp <= maxHp / 2;
            float hx = hero.transform.position.x;
            var p = transform.position;
            bool heroInPool = hero.Swimming && hx > poolX0 && hx < poolX1;
            if (heroInPool && mode != Mode.Chase && mode != Mode.Stunned && mode != Mode.Bite) Set(Mode.Chase, 99f);

            switch (mode)
            {
                case Mode.Swim:
                {
                    // Il nage sous Yao (dans les limites du bassin), puis guette.
                    float target = Mathf.Clamp(hx, poolX0 + 1.4f, poolX1 - 1.4f);
                    float step = Mathf.Clamp(target - p.x, -SwimSpeed * (angry ? 1.4f : 1f) * dt, SwimSpeed * (angry ? 1.4f : 1f) * dt);
                    p.x += step;
                    if (Mathf.Abs(step) > 0.001f) facing = step > 0f ? 1 : -1;
                    p.y = Mathf.MoveTowards(p.y, surfaceY, 4f * dt);
                    Pose(swim, 8f, -125f);
                    SetBox(new Vector2(2.4f, 0.7f), new Vector2(0.4f * facing, 0.05f));
                    if (timer <= 0f && Mathf.Abs(target - p.x) < 0.1f) Set(Mode.Lurk, (angry ? 26 : 36) * F);
                    break;
                }
                case Mode.Lurk:
                    // Seuls ses yeux dépassent : il guette.
                    facing = hx < p.x ? -1 : 1;
                    Pose(lurk, 5f, -56f);
                    SetBox(new Vector2(1.6f, 0.45f), new Vector2(0.3f * facing, 0.1f));
                    if (timer <= 0f)
                    {
                        if (Mathf.Abs(hx - p.x) > Reach && sinceCast >= 1) { sinceCast = 0; Set(Mode.Tail, 40 * F); thrown = false; }
                        else { sinceCast++; Set(Mode.Bite, 99f); rise = 0f; SoundFx.Play("bump"); }
                    }
                    break;
                case Mode.Bite:
                    // Il surgit d'un coup, gueule ouverte, puis reste là, sonné.
                    rise = Mathf.Min(1f, rise + dt / RiseTime);
                    PoseBite(0, rise);
                    if (rise >= 1f) Set(Mode.Stunned, (angry ? 55 : 75) * F);
                    break;
                case Mode.Stunned:
                    PoseBite(1 + Mathf.FloorToInt(clock * 6f) % 2, 1f);
                    if (timer <= 0f) Set(Mode.Dive, 0.3f);
                    break;
                case Mode.Dive:
                    // Il replonge (plus vite quand il vient d'être touché), puis repart à la nage.
                    rise = Mathf.Max(0f, rise - dt / 0.3f);
                    PoseBite(0, rise);
                    if (rise <= 0f) Set(Mode.Swim, (angry ? 50 : 80) * F);
                    break;
                case Mode.Tail:
                    // Coup de queue dans l'eau : des gerbes en cloche vers Yao.
                    Pose(tail, 1f, -35f);
                    SetBox(new Vector2(1.2f, 1.6f), new Vector2(0f, 0.8f));
                    if (!thrown && since > 18 * F) { thrown = true; Splash(angry); }
                    if (timer <= 0f) Set(Mode.Swim, (angry ? 50 : 80) * F);
                    break;
                case Mode.Chase:
                {
                    // Yao nage dans le bassin : le caïman le poursuit sous l'eau.
                    if (!heroInPool) { Set(Mode.Swim, 40 * F); break; }
                    var to = (Vector2)hero.transform.position + Vector2.up * 0.3f - (Vector2)p;
                    var step = Vector2.ClampMagnitude(to, ChaseSpeed * (angry ? 1.35f : 1f) * dt);
                    p += (Vector3)step;
                    if (Mathf.Abs(to.x) > 0.2f) facing = to.x > 0f ? 1 : -1;
                    p.x = Mathf.Clamp(p.x, poolX0 + 1.4f, poolX1 - 1.4f);
                    p.y = Mathf.Clamp(p.y, poolBottom + 0.2f, surfaceY);
                    Pose(under, 8f, 0f);
                    SetBox(new Vector2(2.2f, 0.7f), new Vector2(0.3f * facing, 0.45f));
                    break;
                }
            }
            transform.position = p;
            if (image != null)
            {
                image.flipX = facing < 0; // images tournées vers la droite
                image.color = flash > 0f && Mathf.FloorToInt(flash * 15f) % 2 == 0 ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            }
        }

        void Set(Mode m, float t) { mode = m; timer = t; since = 0f; }

        /// <summary>Image du moment : la ligne <paramref name="waterlinePx"/> (px sous le bas de l'image, négatif) est à la surface.</summary>
        void Pose(Sprite[] frames, float fps, float bottomPx)
        {
            if (image == null || frames == null || frames.Length == 0) return;
            image.sprite = frames[Mathf.FloorToInt(clock * fps) % frames.Length];
            float s = unit * frames[0].pixelsPerUnit;
            image.transform.localScale = new Vector3(s, s, 1f);
            // Sous l'eau (poursuite), l'image suit le caïman ; sinon elle est calée sur la surface.
            float y = mode == Mode.Chase ? 0f : surfaceY - transform.position.y + bottomPx * unit;
            image.transform.localPosition = new Vector3(0f, y, 0f);
        }

        /// <summary>Le caïman qui surgit : hors de l'eau à <paramref name="up"/> (0 = sous la surface, 1 = sorti).</summary>
        void PoseBite(int frame, float up)
        {
            if (image == null || bite == null || bite.Length == 0) return;
            image.sprite = bite[Mathf.Min(frame, bite.Length - 1)];
            float s = unit * bite[0].pixelsPerUnit;
            image.transform.localScale = new Vector3(s, s, 1f);
            float h = bite[0].rect.height * unit;
            float bottom = Mathf.Lerp(-h, -0.35f, up);
            image.transform.localPosition = new Vector3(0f, surfaceY - transform.position.y + bottom, 0f);
            SetBox(new Vector2(1.7f, 1.5f * up + 0.2f), new Vector2(0.5f * facing, surfaceY - transform.position.y + 0.1f + 0.75f * up));
        }

        void SetBox(Vector2 size, Vector2 offset)
        {
            box.size = size;
            box.offset = offset;
        }

        void Begin()
        {
            Set(Mode.Swim, 50 * F);
            SetWalls(true);
            if (follow != null) { follow.levelMinX = arenaX0 - 1f; follow.levelMaxX = arenaX1 + 1f; }
            SoundFx.Play("bump");
        }

        void ResetFight()
        {
            mode = Mode.Wait;
            hp = maxHp;
            flash = 0f;
            sinceCast = 0;
            facing = -1;
            transform.position = start;
            SetWalls(false);
            RestoreCamera();
            foreach (var sp in splashes) sp.gameObject.SetActive(false);
        }

        void RestoreCamera() { if (follow != null) { follow.levelMinX = camMin; follow.levelMaxX = camMax; } }

        void SetWalls(bool on)
        {
            if (walls == null) return;
            foreach (var w in walls) if (w != null) w.enabled = on;
        }

        // ---------------------------------------------------------------- coups

        void OnTriggerStay2D(Collider2D other)
        {
            if (mode == Mode.Wait || mode == Mode.Dead || stompGuard > 0f) return;
            var h = other.GetComponentInParent<HeroController>();
            if (h == null || h.IsDying) return;
            float top = box.bounds.max.y;
            bool falling = h.PreviousVelocityY < 0.5f || h.Velocity.y < 0f;
            bool fromAbove = falling && h.PreviousFeetY >= top - 0.6f;
            bool exposed = mode == Mode.Stunned || mode == Mode.Bite || mode == Mode.Chase || mode == Mode.Tail;
            if (fromAbove)
            {
                if (!h.Swimming) h.Bounce();
                stompGuard = 0.35f;
                // Hors de l'eau (sonné, qui mord, coup de queue) ou poursuivi sous l'eau : 2 points ; la tête à la surface
                // (il nage ou guette) : 1 point, et il plonge s'éloigner.
                if (flash <= 0f) Hit(exposed ? 2 : 1, 50 * F);
                return;
            }
            // Sur ses yeux ou sa tête à la surface, Yao est repoussé sans dommage ; la gueule ou la poursuite blessent.
            if (mode == Mode.Lurk || mode == Mode.Swim && !h.Swimming || mode == Mode.Tail) return;
            if (h.Velocity.y > 0f && h.transform.position.y > box.bounds.center.y) return;
            h.Hurt();
        }

        /// <summary>Touché par une boule de feu : 1 point de vie s'il est hors de l'eau. Faux s'il ne peut pas être touché.</summary>
        public bool FireHit()
        {
            if (mode != Mode.Bite && mode != Mode.Stunned && mode != Mode.Tail) return false;
            if (flash <= 0f) Hit(1, 40 * F);
            return true;
        }

        void Hit(int damage, float flashTime)
        {
            hp = Mathf.Max(0, hp - damage);
            flash = flashTime;
            if (hp <= 0) { Defeat(); return; }
            StageState.Instance?.AddBossHit();
            if (mode == Mode.Stunned || mode == Mode.Bite) Set(Mode.Dive, 0.2f);
            else if (mode == Mode.Lurk || mode == Mode.Swim) Set(Mode.Swim, 0.9f); // touché sur la tête : il repart nager avant de guetter
            else if (mode == Mode.Chase) { var p = transform.position; p.y = Mathf.Max(poolBottom + 0.2f, p.y - 1.2f); transform.position = p; }
        }

        void Defeat()
        {
            Set(Mode.Dead, 0f);
            box.enabled = false;
            SetWalls(false);
            RestoreCamera();
            foreach (var sp in splashes) sp.gameObject.SetActive(false);
            StageState.Instance?.AddBossDown();
        }

        /// <summary>Vaincu : il coule doucement, gueule ouverte, et s'efface.</summary>
        void Sink(float dt)
        {
            if (image == null) return;
            image.sprite = bite != null && bite.Length > 0 ? bite[0] : image.sprite;
            image.transform.localPosition += Vector3.down * (0.8f * dt);
            var c = image.color;
            c.a = Mathf.Clamp01(1f - since / 2.5f);
            image.color = c;
            if (since > 2.6f) gameObject.SetActive(false);
        }

        /// <summary>Gerbes d'eau lancées en cloche vers Yao (elles blessent au contact).</summary>
        void Splash(bool angry)
        {
            if (hero == null || splash == null || splash.Length == 0) return;
            int n = angry ? 3 : 2;
            var from = new Vector3(transform.position.x, surfaceY + 0.6f, 0f);
            for (int i = 0; i < n; i++)
            {
                float target = hero.transform.position.x + (i - (n - 1) / 2f) * 1.9f;
                const float flight = 44f / 60f;
                Drop().Launch(from, (target - from.x) / flight, (6.2f + 0.4f * i) * 3.75f, surfaceY - 0.3f);
            }
            SoundFx.Play("shoot");
        }

        BossTicket Drop()
        {
            foreach (var b in splashes) if (!b.gameObject.activeSelf) return b;
            var go = new GameObject($"Gerbe_{splashes.Count + 1}");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = image != null ? image.sharedMaterial : null;
            sr.sortingLayerName = "Monde";
            sr.sortingOrder = 8;
            sr.sprite = splash[0];
            const float size = 0.8f;
            float s = size / splash[0].bounds.size.y;
            go.transform.localScale = new Vector3(s, s, 1f);
            var circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.3f / s;
            circle.offset = new Vector2(0f, size / 2f / s);
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var bt = go.AddComponent<BossTicket>();
            bt.frames = splash;
            splashes.Add(bt);
            return bt;
        }
    }
}
