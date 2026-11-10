using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Échelle ou corde à nœuds (stage 6) : zone (déclencheur) où Yao grimpe. Contre l'échelle, le bouton de saut le fait
    /// monter (▼ descendre) ; en haut, il se hisse sur la plate-forme ou la branche. Voir HeroController.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Climbable : MonoBehaviour
    {
        /// <summary>Haut de l'échelle (dessus de la plate-forme où elle mène), en cases.</summary>
        public float top;

        public float X => transform.position.x + GetComponent<BoxCollider2D>().offset.x * transform.lossyScale.x;

        void OnTriggerStay2D(Collider2D other)
        {
            var hero = other.GetComponentInParent<HeroController>();
            if (hero != null) hero.InClimbable(this);
        }
    }
}
