using System.Collections.Generic;
using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Pont de lianes au-dessus de la gorge (stage 10) : une suite de tronçons (planches et cordes, image de l'utilisateur)
    /// tendue entre deux poteaux. Il pend un peu, se balance doucement, et s'enfonce davantage là où Yao marche. Certaines
    /// planches sont pourries (plus sombres) : elles craquent sous les pieds de Yao, cèdent un instant après et tombent
    /// dans la gorge, puis reviennent quelques secondes plus tard.
    /// Chaque tronçon a son corps cinématique et son dessus traversable par en dessous ; ce composant les déplace.
    /// </summary>
    public class RopeBridge : MonoBehaviour
    {
        [Tooltip("Extrémités du pont (x) et hauteur du tablier aux poteaux.")]
        public float x0, x1, deckY;
        [Tooltip("Flèche au repos (cases), flèche ajoutée sous Yao, et balancement.")]
        public float sag = 0.5f, weight = 0.6f, sway = 0.12f;
        public Rigidbody2D[] planks;
        public bool[] rotten;
        public SpriteRenderer[] images;

        const float CrackTime = 0.45f, FallTime = 1.2f, RespawnTime = 4f;
        float[] crack, fallen;
        float load, loadX;
        static HeroController heroCache;

        void Awake()
        {
            int n = planks != null ? planks.Length : 0;
            crack = new float[n];
            fallen = new float[n];
            for (int i = 0; i < n; i++) { crack[i] = -1f; fallen[i] = -1f; }
        }

        void FixedUpdate()
        {
            if (planks == null) return;
            float dt = Time.fixedDeltaTime, t = Time.time;
            if (heroCache == null) heroCache = FindAnyObjectByType<HeroController>();
            var hero = heroCache;
            // Yao est-il sur le pont ? Le poids creuse le pont autour de lui.
            bool onBridge = false;
            if (hero != null && !hero.IsDying && hero.Grounded)
            {
                var hp = hero.transform.position;
                onBridge = hp.x > x0 && hp.x < x1 && Mathf.Abs(hp.y - DeckAt(hp.x, 0f)) < 1.2f;
                if (onBridge) loadX = hp.x;
            }
            load = Mathf.MoveTowards(load, onBridge ? 1f : 0f, dt * 2.5f);
            for (int i = 0; i < planks.Length; i++)
            {
                var b = planks[i];
                if (b == null) continue;
                float x = b.position.x;
                if (fallen[i] >= 0f)
                {
                    // Planche tombée : elle chute, puis revient à sa place.
                    fallen[i] += dt;
                    if (fallen[i] < FallTime) { b.MovePosition(b.position + Vector2.down * (8f + 20f * fallen[i]) * dt); continue; }
                    if (fallen[i] < RespawnTime) { if (images[i] != null) images[i].enabled = false; continue; }
                    fallen[i] = -1f;
                    crack[i] = -1f;
                    SetSolid(i, true);
                    if (images[i] != null) { images[i].enabled = true; images[i].color = RottenColor; }
                    b.position = new Vector2(x, DeckAt(x, t));
                    continue;
                }
                b.MovePosition(new Vector2(x, DeckAt(x, t)));
                // Planche pourrie sous les pieds de Yao : elle craque (elle tremble), puis cède.
                if (rotten != null && rotten[i] && onBridge && Mathf.Abs(loadX - x) < 0.6f && crack[i] < 0f) crack[i] = 0f;
                if (crack[i] >= 0f)
                {
                    crack[i] += dt;
                    if (images[i] != null) images[i].transform.localPosition = new Vector3(Mathf.Sin(crack[i] * 70f) * 0.04f, images[i].transform.localPosition.y, 0f);
                    if (crack[i] > CrackTime)
                    {
                        fallen[i] = 0f;
                        SetSolid(i, false);
                        SoundFx.Play("break");
                    }
                }
            }
        }

        static readonly Color RottenColor = new Color(0.62f, 0.5f, 0.4f);

        void SetSolid(int i, bool on)
        {
            foreach (var c in planks[i].GetComponents<Collider2D>()) c.enabled = on;
        }

        /// <summary>Hauteur du tablier en x : flèche en parabole, creux sous Yao, petit balancement.</summary>
        float DeckAt(float x, float t)
        {
            float u = Mathf.InverseLerp(x0, x1, x);
            float sagNow = sag * 4f * u * (1f - u);
            float dent = load * weight * Mathf.Exp(-Mathf.Pow((x - loadX) / 2.5f, 2f)) * 4f * u * (1f - u) * 1.6f;
            float swing = sway * Mathf.Sin(t * 1.7f + u * 3f) * 4f * u * (1f - u);
            return deckY - sagNow - dent + swing;
        }
    }
}
