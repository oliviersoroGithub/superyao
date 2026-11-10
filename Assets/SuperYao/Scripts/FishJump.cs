using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// De temps en temps, un poisson saute hors de l'eau devant le héros (comme sur le téléphone, drawWaterFoam) :
    /// il jaillit, décrit un arc en suivant sa trajectoire, et replonge. Il apparaît seulement à l'écran et au-dessus
    /// de la lagune (entre <see cref="fromX"/> et <see cref="toX"/>, x du monde). Un seul poisson, recyclé.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FishJump : MonoBehaviour
    {
        public float fromX, toX;
        [Tooltip("Hauteur de la surface de l'eau, en cases.")]
        public float surfaceY = -0.1f;
        public float jumpHeight = 1.6f, jumpLength = 2.2f, jumpTime = 0.9f;
        public Vector2 delay = new Vector2(3f, 8f);

        SpriteRenderer sr;
        float wait, time = -1f, startX;
        int direction;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            sr.enabled = false;
            wait = Random.Range(1f, delay.y);
        }

        void Update()
        {
            if (time < 0f)
            {
                wait -= Time.deltaTime;
                if (wait > 0f) return;
                var cam = Camera.main;
                if (cam == null) return;
                float half = cam.orthographicSize * cam.aspect;
                float x0 = Mathf.Max(fromX + 1f, cam.transform.position.x - half + 1f);
                float x1 = Mathf.Min(toX - 1f - jumpLength, cam.transform.position.x + half - 1f - jumpLength);
                if (x1 <= x0) { wait = 1f; return; } // la lagune n'est pas à l'écran
                startX = Random.Range(x0, x1);
                direction = Random.value < 0.5f ? 1 : -1;
                if (direction < 0) startX += jumpLength;
                time = 0f;
                sr.enabled = true;
            }
            time += Time.deltaTime;
            float k = time / jumpTime;
            if (k >= 1f)
            {
                sr.enabled = false;
                time = -1f;
                wait = Random.Range(delay.x, delay.y);
                return;
            }
            // Arc : part de sous la surface, monte, retombe ; le poisson suit sa trajectoire.
            float x = startX + direction * jumpLength * k;
            float y = surfaceY - 0.5f + 4f * (jumpHeight + 0.5f) * k * (1f - k);
            transform.position = new Vector3(x, y, 0f);
            float slope = 4f * (jumpHeight + 0.5f) * (1f - 2f * k) / jumpLength;
            transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan(slope) * Mathf.Rad2Deg * direction);
            sr.flipX = direction < 0;
        }
    }
}
