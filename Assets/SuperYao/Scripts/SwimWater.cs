using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Lac où l'on nage (stage 7) : zone (déclencheur) qui couvre l'eau, de la surface au fond. Yao qui y plonge nage
    /// (HeroController.StepSwimming) ; les poissons et les caïmans y patrouillent. Le dessus de la boîte est la surface.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class SwimWater : MonoBehaviour
    {
        [Tooltip("Courant de la rivière (cases/s, positif vers la droite) : il emporte Yao qui nage (stage 9).")]
        public float current;
        BoxCollider2D box;

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
        }

        /// <summary>Hauteur de la surface de l'eau (cases).</summary>
        public float SurfaceY => (box != null ? box : box = GetComponent<BoxCollider2D>()).bounds.max.y;
        public float BottomY => (box != null ? box : box = GetComponent<BoxCollider2D>()).bounds.min.y;
        public float MinX => box.bounds.min.x;
        public float MaxX => box.bounds.max.x;

        void OnTriggerStay2D(Collider2D other)
        {
            var hero = other.GetComponentInParent<HeroController>();
            if (hero != null) hero.InWater(this);
        }

        /// <summary>Le lac qui contient ce point (null si aucun).</summary>
        public static SwimWater At(Vector2 p)
        {
            foreach (var w in FindObjectsByType<SwimWater>(FindObjectsSortMode.None))
                if (p.x >= w.MinX && p.x <= w.MaxX && p.y <= w.SurfaceY + 0.01f && p.y >= w.BottomY) return w;
            return null;
        }
    }
}
