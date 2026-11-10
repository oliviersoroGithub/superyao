using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Étire un sprite pour couvrir tout l'écran, quel que soit le format du téléphone (16:9, 19,5:9, 4:3…),
    /// sans le déformer. Pour le ciel, posé dans une couche fixe à l'écran (ParallaxLayer facteur 0).
    /// Le sprite a son pivot en bas au centre : son bas est calé sur le bas de l'écran.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FitToCamera : MonoBehaviour
    {
        public Camera targetCamera;
        float lastAspect, lastSize;

        void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;
            if (Mathf.Approximately(lastAspect, targetCamera.aspect) && Mathf.Approximately(lastSize, targetCamera.orthographicSize)) return;
            lastAspect = targetCamera.aspect;
            lastSize = targetCamera.orthographicSize;

            var size = GetComponent<SpriteRenderer>().sprite.bounds.size;
            float viewH = 2f * lastSize, viewW = viewH * lastAspect;
            float s = Mathf.Max(viewW / size.x, viewH / size.y) * 1.01f;
            transform.localScale = new Vector3(s, s, 1f);
            transform.localPosition = new Vector3(0f, -lastSize, transform.localPosition.z);
        }
    }
}
