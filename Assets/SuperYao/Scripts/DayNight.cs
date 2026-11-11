using System.Collections.Generic;
using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Le jour qui tombe au fil du stage (stage 3) : selon la position de la caméra, le ciel de coucher de soleil puis
    /// le ciel de nuit (et la lune) apparaissent en fondu par-dessus le ciel de jour, et les décors de jour prennent la
    /// lumière du soir puis de la nuit. Les images dessinées de nuit (nom commençant par « Nuit_ ») et les reflets de
    /// l'eau gardent leurs couleurs.
    /// </summary>
    public class DayNight : MonoBehaviour
    {
        public SpriteRenderer sunsetSky, nightSky, moon;
        [Tooltip("Positions de la caméra (x, en cases) : début et fin du coucher de soleil, puis de la nuit.")]
        public float duskFrom, duskTo, nightFrom, nightTo;
        public Color duskTint = new Color(1f, 0.8f, 0.68f);
        public Color nightTint = new Color(0.45f, 0.5f, 0.75f);
        [Tooltip("Groupes dont les images de jour prennent la lumière du soir.")]
        public Transform[] tintRoots;

        /// <summary>Lumière du moment (blanc le jour) : la brume de profondeur (Haze) la reprend.</summary>
        public static Color CurrentTint { get; private set; } = Color.white;
        /// <summary>Part de nuit du moment, de 0 (jour ou soir) à 1 (pleine nuit) : lucioles et champignons s'allument.</summary>
        public static float Night { get; private set; }

        readonly List<SpriteRenderer> tinted = new List<SpriteRenderer>();
        readonly List<Color> baseColors = new List<Color>();
        Camera cam;

        void OnDestroy() { CurrentTint = Color.white; Night = 0f; }

        void Start()
        {
            foreach (var root in tintRoots)
            {
                if (root == null) continue;
                foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (r == sunsetSky || r == nightSky || r == moon || r.name.StartsWith("Nuit_")) continue;
                    var water = r.GetComponentInParent<WaterFx>();
                    if (water != null && water.gameObject != r.gameObject) continue; // reflets et vagues animés
                    tinted.Add(r);
                    baseColors.Add(r.color);
                }
            }
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            float x = cam.transform.position.x;
            float dusk = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(duskFrom, duskTo, x));
            float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(nightFrom, nightTo, x));
            SetAlpha(sunsetSky, dusk);
            SetAlpha(nightSky, night);
            SetAlpha(moon, night);
            var tint = Color.Lerp(Color.Lerp(Color.white, duskTint, dusk), nightTint, night);
            CurrentTint = tint;
            Night = night;
            for (int i = 0; i < tinted.Count; i++)
            {
                var r = tinted[i];
                if (r == null) continue;
                var c = baseColors[i];
                r.color = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
            }
        }

        static void SetAlpha(SpriteRenderer r, float a)
        {
            if (r == null) return;
            var c = r.color;
            c.a = a;
            r.color = c;
            r.enabled = a > 0.001f;
        }
    }
}
