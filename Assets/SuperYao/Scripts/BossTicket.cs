using UnityEngine;

namespace SuperYao
{
    /// <summary>Ticket du racketteur : lancé en cloche, il blesse Yao au contact et disparaît en touchant le sol.</summary>
    public class BossTicket : MonoBehaviour
    {
        public Sprite[] frames;
        const float Gravity = 49.5f; // 0,22 px/image² du téléphone (plus léger qu'une calebasse)
        float vx, vy, groundY, life, clock;
        SpriteRenderer sr;

        public void Launch(Vector3 from, float speedX, float speedY, float ground)
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            transform.position = from;
            vx = speedX;
            vy = speedY;
            groundY = ground;
            life = 200f / 60f;
            clock = 0f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            clock += dt;
            life -= dt;
            vy -= Gravity * dt;
            transform.position += new Vector3(vx * dt, vy * dt, 0f);
            if (frames != null && frames.Length > 0) sr.sprite = frames[Mathf.FloorToInt(clock * 12f) % frames.Length];
            if (life <= 0f || (vy < 0f && transform.position.y <= groundY)) gameObject.SetActive(false);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var h = other.GetComponentInParent<HeroController>();
            if (h == null || h.IsDying) return;
            h.Hurt();
            gameObject.SetActive(false);
        }
    }
}
