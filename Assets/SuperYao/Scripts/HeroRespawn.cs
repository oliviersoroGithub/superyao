using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Chute dans la lagune (le héros passe sous <see cref="killY"/>) : il perd une vie, sans pause ni bond. Après une
    /// vie perdue, il réapparaît au dernier point de contrôle atteint (StageState.RespawnX), ou au départ.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class HeroRespawn : MonoBehaviour
    {
        [Tooltip("En dessous de cette hauteur (pieds, en cases), le héros est tombé à l'eau.")]
        public float killY = -2f;

        Rigidbody2D body;
        HeroController hero;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hero = GetComponent<HeroController>();
        }

        void FixedUpdate()
        {
            if (hero != null && hero.IsDying) return;
            if (body.position.y < killY)
            {
                if (hero != null) hero.Die(true); else Respawn();
            }
        }

        public void Respawn()
        {
            float x = StageState.Instance != null ? StageState.Instance.RespawnX : 4f;
            body.position = new Vector2(x, GroundY(x) + 0.05f);
            transform.position = body.position;
            body.linearVelocity = Vector2.zero;
            FindAnyObjectByType<CameraFollow2D>()?.ResetBacktrack();
        }

        /// <summary>Hauteur du sol en x (le plus haut obstacle plein sous le ciel), ou 0 s'il n'y en a pas.</summary>
        float GroundY(float x)
        {
            foreach (var hit in Physics2D.RaycastAll(new Vector2(x, 30f), Vector2.down, 40f))
            {
                if (hit.collider.isTrigger || hit.collider.attachedRigidbody == body) continue;
                if (hit.collider.GetComponentInParent<Enemy>() != null || hit.collider.GetComponentInParent<BumpBlock>() != null) continue;
                return hit.point.y;
            }
            return 0f;
        }
    }
}
