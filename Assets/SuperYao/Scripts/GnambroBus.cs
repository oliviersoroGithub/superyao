using UnityEngine;

namespace SuperYao
{
    /// <summary>Le gbaka appelé par le gnambro : transmet au boss le contact avec Yao (rebond sur le toit ou blessure).</summary>
    public class GnambroBus : MonoBehaviour
    {
        public GnambroBoss boss;

        void OnTriggerStay2D(Collider2D other)
        {
            var hero = other.GetComponentInParent<HeroController>();
            if (hero != null && boss != null) boss.BusContact(hero);
        }
    }
}
