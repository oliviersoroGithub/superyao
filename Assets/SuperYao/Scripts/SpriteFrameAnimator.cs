using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Anime un SpriteRenderer image par image, à partir d'une suite de sprites (les planches découpées de Super Yao).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFrameAnimator : MonoBehaviour
    {
        [Tooltip("Images de l'animation en cours, dans l'ordre.")]
        public Sprite[] frames;
        [Tooltip("Images par seconde.")]
        public float framesPerSecond = 24f;
        [Tooltip("Faux : l'animation reste figée sur sa première image.")]
        public bool playing = true;

        SpriteRenderer spriteRenderer;
        float time;

        void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

        /// <summary>Change d'animation ; ne repart du début que si c'est une autre suite d'images.</summary>
        public void Play(Sprite[] next, float fps)
        {
            if (next != frames) time = 0f;
            frames = next;
            framesPerSecond = fps;
            playing = true;
        }

        /// <summary>Fige l'animation sur l'image donnée (Yao à l'arrêt, en attendant son animation de repos).</summary>
        public void Hold(int frame)
        {
            playing = false;
            time = frame / Mathf.Max(1f, framesPerSecond);
            Show(frame);
        }

        void Update()
        {
            if (frames == null || frames.Length == 0) return;
            if (playing) time += Time.deltaTime;
            Show(Mathf.FloorToInt(time * framesPerSecond));
        }

        void Show(int frame)
        {
            if (frames == null || frames.Length == 0) return;
            spriteRenderer.sprite = frames[((frame % frames.Length) + frames.Length) % frames.Length];
        }
    }
}
