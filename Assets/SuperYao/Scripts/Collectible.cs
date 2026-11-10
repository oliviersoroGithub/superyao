using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Objet à ramasser en passant dessus : pièce (elle tourne), banane (elle se balance), diamant (il scintille).
    /// Au contact du héros, il disparaît dans une étincelle (préparée d'avance, aucun objet créé en jeu).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class Collectible : MonoBehaviour
    {
        public enum Kind { Coin, Banana, Diamond }

        public Kind kind;
        public Sprite[] frames;
        public float framesPerSecond = 8f;
        [Tooltip("Étincelle jouée à la prise (enfant désactivé).")]
        public SpriteRenderer sparkle;
        public Sprite[] sparkleFrames;

        SpriteRenderer sr;
        Vector3 basePosition;
        float time, sparkleTime = -1f;
        bool taken;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            GetComponent<CircleCollider2D>().isTrigger = true;
            basePosition = transform.localPosition;
            time = Random.value * 3f; // pas toutes en même temps
        }

        void Update()
        {
            time += Time.deltaTime;
            if (!taken)
            {
                if (frames != null && frames.Length > 0) sr.sprite = frames[Mathf.FloorToInt(time * framesPerSecond) % frames.Length];
                if (kind == Kind.Banana) transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(time * 3f) * 0.06f);
            }
            else if (sparkle != null && sparkleTime >= 0f)
            {
                sparkleTime += Time.deltaTime;
                int f = Mathf.FloorToInt(sparkleTime * 20f);
                if (sparkleFrames == null || f >= sparkleFrames.Length) { sparkle.enabled = false; enabled = false; }
                else sparkle.sprite = sparkleFrames[f];
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (taken || other.GetComponentInParent<HeroController>() == null) return;
            taken = true;
            sr.enabled = false;
            GetComponent<Collider2D>().enabled = false;
            var state = StageState.Instance;
            if (state != null)
            {
                if (kind == Kind.Coin) state.AddCoin();
                else if (kind == Kind.Banana) state.AddBanana();
                else state.AddDiamond();
            }
            if (sparkle != null)
            {
                sparkle.enabled = true;
                sparkleTime = 0f;
            }
            else enabled = false;
        }
    }
}
