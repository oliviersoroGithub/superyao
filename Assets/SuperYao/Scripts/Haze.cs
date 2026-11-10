using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Brume de profondeur (tous les stages) : deux voiles de couleur posés sur l'écran, l'un par-dessus les plans
    /// lointains (horizon et bâtiments lointains), l'autre, plus léger, par-dessus le plan intermédiaire. Le décor
    /// s'efface un peu, et Yao, les ennemis, les pièces et les blocs (au premier plan) ressortent. La couleur de la
    /// brume suit l'heure : bleu clair le jour, prise par le coucher de soleil (DayNight), bleu nuit la nuit.
    /// Ajoutée par la caméra (CameraFollow2D) : aucun stage n'a besoin d'être reconstruit.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class Haze : MonoBehaviour
    {
        [Tooltip("Opacité du voile sur les plans lointains, puis sur le plan intermédiaire.")]
        public float farAmount = 0.3f, midAmount = 0.16f;
        public Color dayColor = new Color(0.74f, 0.84f, 0.96f);
        public Color nightColor = new Color(0.14f, 0.18f, 0.36f);

        Camera cam;
        SpriteRenderer far, mid;
        bool night;

        void Start()
        {
            cam = GetComponent<Camera>();
            var bg = cam.backgroundColor;
            night = bg.r * 0.3f + bg.g * 0.59f + bg.b * 0.11f < 0.3f; // stage tout de nuit (fond sombre)
            var any = FindAnyObjectByType<SpriteRenderer>();
            var material = any != null ? any.sharedMaterial : null;
            far = Veil("Brume_Lointains", "BatimentsLointains", material);
            mid = Veil("Brume_Plan_Intermediaire", "PlanIntermediaire", material);
        }

        SpriteRenderer Veil(string name, string sortingLayer, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 9f);
            var sr = go.AddComponent<SpriteRenderer>();
            var tex = Texture2D.whiteTexture;
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            if (material != null) sr.sharedMaterial = material;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = 10000; // par-dessus tout ce calque
            return sr;
        }

        void LateUpdate()
        {
            if (far == null) return;
            // Le voile couvre tout l'écran, quel que soit son format.
            float h = cam.orthographicSize * 2f + 2f, w = h * cam.aspect + 2f;
            foreach (var sr in new[] { far, mid }) sr.transform.localScale = new Vector3(w, h, 1f);
            var c = (night ? nightColor : dayColor) * DayNight.CurrentTint;
            far.color = new Color(c.r, c.g, c.b, farAmount);
            mid.color = new Color(c.r, c.g, c.b, midAmount);
        }
    }
}
