using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Point de contrôle ou arrivée, marqué par le cocotier au drapeau ivoirien (flagPalm, comme sur le téléphone).
    /// Point de contrôle : estompé tant que le héros ne l'a pas dépassé, puis éclatant ; après une vie perdue, le héros
    /// repart du dernier atteint. Arrivée : le stage est gagné.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Checkpoint : MonoBehaviour
    {
        public bool isGoal;
        [Tooltip("Où le héros repart, en x (cases).")]
        public float respawnX;

        SpriteRenderer sr;
        HeroController hero;
        bool reached;
        float pulse = -1f;
        Vector3 baseScale;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
            if (!isGoal) sr.color = new Color(1f, 1f, 1f, 0.5f);
        }

        void Update()
        {
            if (hero == null) hero = FindAnyObjectByType<HeroController>();
            if (!reached && hero != null && !hero.IsDying && hero.transform.position.x >= transform.position.x)
            {
                reached = true;
                var state = StageState.Instance;
                if (isGoal) state?.Win();
                else
                {
                    sr.color = Color.white;
                    pulse = 0f;
                    state?.ReachCheckpoint(respawnX);
                    SoundFx.Play("checkpoint");
                }
            }
            // Le drapeau atteint grossit un instant.
            if (pulse >= 0f)
            {
                pulse += Time.deltaTime;
                float k = 1f + 0.15f * Mathf.Sin(Mathf.Clamp01(pulse / 0.4f) * Mathf.PI);
                transform.localScale = baseScale * k;
                if (pulse > 0.4f) { transform.localScale = baseScale; pulse = -1f; }
            }
        }
    }
}
