using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Vie sur la lagune lointaine : bateaux du téléphone (pinasse, voiliers, bac, hors-bord) qui glissent lentement,
    /// chacun à sa vitesse et dans son sens, en tanguant un peu. Ils sont enfants de la couche du plan intermédiaire :
    /// le parallaxe s'applique tout seul. Un bateau sorti d'un bout de la lagune revient par l'autre (recyclé).
    /// Positions en x local de la couche (voir ParallaxLayer).
    /// </summary>
    public class LagoonBoats : MonoBehaviour
    {
        [System.Serializable]
        public class Boat
        {
            public SpriteRenderer renderer;
            public Sprite[] frames;
            public float speed;          // cases/s, signe = sens
            public bool facesRight = true;
            [HideInInspector] public float phase;
        }

        public Boat[] boats;
        [Tooltip("Étendue de la lagune, en x local.")]
        public float fromX, toX;

        void Start()
        {
            foreach (var b in boats) b.phase = Random.value * 10f;
        }

        void Update()
        {
            float dt = Time.deltaTime, t = Time.time;
            foreach (var b in boats)
            {
                var tr = b.renderer.transform;
                var p = tr.localPosition;
                p.x += b.speed * dt;
                float half = b.renderer.bounds.size.x / 2f;
                if (b.speed > 0f && p.x - half > toX) p.x = fromX - half;
                else if (b.speed < 0f && p.x + half < fromX) p.x = toX + half;
                tr.localPosition = p;
                // Tangage léger.
                tr.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 1.3f + b.phase) * 1.5f);
                b.renderer.flipX = b.facesRight ? b.speed < 0f : b.speed > 0f;
                if (b.frames != null && b.frames.Length > 1)
                    b.renderer.sprite = b.frames[Mathf.FloorToInt(t * 4f + b.phase) % b.frames.Length];
            }
        }
    }
}
