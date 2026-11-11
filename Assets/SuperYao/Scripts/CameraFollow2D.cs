using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Caméra orthographique de Super Yao : suit Yao horizontalement avec un léger amortissement, regarde un peu
    /// devant lui, ne sort jamais des bornes du niveau, et garde l'horizon à hauteur fixe. Elle ne monte que si Yao
    /// grimpe nettement (plates-formes hautes), et alors lentement : les sauts ordinaires ne la font pas bouger.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public class CameraFollow2D : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Hauteur visible, en cases (14 dans la version téléphone ; 20 ici, la vue étant jugée trop proche).")]
        public float visibleRows = 20f;
        [Tooltip("Bord bas de l'écran, en cases (0 = dessus du sol).")]
        public float bottomY = -2f;

        [Header("Horizontal")]
        [Tooltip("Avance de la caméra dans le sens de la marche, en cases.")]
        public float lookAhead = 2.5f;
        [Tooltip("Temps pour que l'avance change de côté quand Yao se retourne, en secondes.")]
        public float lookAheadTime = 0.6f;
        [Tooltip("Amortissement horizontal, en secondes (0 = collée à Yao).")]
        public float smoothTime = 0.12f;
        [Tooltip("Bornes du niveau, en cases : la caméra ne montre jamais au-delà.")]
        public float levelMinX = 0f, levelMaxX = 230f;
        [Tooltip("Recul possible de la caméra, en cases, derrière le point le plus avancé : Yao ne peut pas revenir loin en " +
                 "arrière (le bord gauche de l'écran l'arrête, voir HeroController).")]
        public float maxBacktrack = 2f;
        float furthest = float.MinValue;

        /// <summary>Bord gauche de ce que montre la caméra (x en cases).</summary>
        public float LeftEdge => transform.position.x - cam.orthographicSize * cam.aspect;

        /// <summary>Après une vie perdue : Yao repart du drapeau, la caméra y revient d'un coup et le recul repart de là.</summary>
        public void ResetBacktrack()
        {
            furthest = float.MinValue;
            if (target == null) return;
            var p = Desired(target.position.x);
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            xVelocity = 0f;
            furthest = transform.position.x;
        }

        [Header("Vertical")]
        // Cadrage fixe : le sol reste toujours en bas de l'écran (à 1 case, elle montait sur chaque escalier ou planche
        // et cachait le bas). Avec 20 cases de haut, tout le stage 1 tient dans l'écran.
        [Tooltip("La caméra ne monte que lorsque le héros se pose plus haut que cette hauteur, en cases " +
                 "(on suit la hauteur du dernier sol touché, pas celle du saut). Très haut = cadrage fixe sur le sol.")]
        public float climbThreshold = 12f;
        [Tooltip("Marge sous le haut de l'écran : si le héros monte au-dessus, la caméra le suit quand même.")]
        public float topMargin = 1.5f;
        [Tooltip("Stage de vol : la caméra suit l'altitude du héros (sans descendre sous le cadrage du sol).")]
        public bool followAltitude;
        [Tooltip("Amortissement vertical, en secondes (lent, pour ne pas suivre chaque saut).")]
        public float verticalSmoothTime = 0.45f;

        Camera cam;
        HeroController hero;
        float aheadOffset, aheadVelocity, xVelocity, yVelocity, lastTargetX;

        [Tooltip("Hauteur visible sur téléphone, en cases : l'écran est petit, la caméra est plus proche de Yao.")]
        public float mobileVisibleRows = 15f;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (target != null) hero = target.GetComponent<HeroController>();
            cam.orthographic = true;
            cam.orthographicSize = (Application.isMobilePlatform ? mobileVisibleRows : visibleRows) / 2f;
            // Brume de profondeur sur les plans du fond : Yao et le jeu ressortent.
            if (GetComponent<Haze>() == null) gameObject.AddComponent<Haze>();
        }

        void Start()
        {
            if (target == null) return;
            lastTargetX = target.position.x;
            var p = Desired(target.position.x);
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            // Côté où regarder : celui où Yao avance (on garde le dernier tant qu'il est immobile).
            float dx = target.position.x - lastTargetX;
            lastTargetX = target.position.x;
            if (Mathf.Abs(dx) > 0.001f) aheadOffset = Mathf.SmoothDamp(aheadOffset, Mathf.Sign(dx) * lookAhead, ref aheadVelocity, lookAheadTime);

            var goal = Desired(target.position.x + aheadOffset);
            var pos = transform.position;
            pos.x = Mathf.SmoothDamp(pos.x, goal.x, ref xVelocity, smoothTime, Mathf.Infinity, dt);
            pos.y = Mathf.SmoothDamp(pos.y, goal.y, ref yVelocity, verticalSmoothTime, Mathf.Infinity, dt);
            pos.x = ClampX(Mathf.Max(pos.x, furthest - maxBacktrack));
            transform.position = pos;
            furthest = Mathf.Max(furthest, pos.x);
        }

        Vector2 Desired(float x)
        {
            float baseY = bottomY + cam.orthographicSize;
            // Hauteur de référence : le dernier sol touché ; un saut ordinaire ne fait donc pas bouger la caméra.
            float groundY = hero != null ? hero.LastGroundY : target.position.y;
            // Sous l'eau (stage 7), elle descend avec le nageur pour montrer le fond du lac.
            if (hero != null && hero.Swimming) groundY = Mathf.Min(groundY, target.position.y - 3f);
            // Elle descend avec le sol s'il passe sous le niveau de départ (descentes), monte s'il grimpe nettement.
            if (followAltitude)
                return new Vector2(ClampX(x), Mathf.Max(baseY + Mathf.Min(0f, groundY), target.position.y + cam.orthographicSize * 0.15f));
            float y = baseY + Mathf.Min(0f, groundY) + Mathf.Max(0f, groundY - climbThreshold);
            // Sauf si le héros s'approche du haut de l'écran.
            float headroom = target.position.y + 2f - (y + cam.orthographicSize - topMargin);
            if (headroom > 0f) y += headroom;
            return new Vector2(ClampX(x), y);
        }

        float ClampX(float x)
        {
            float half = cam.orthographicSize * cam.aspect;
            return levelMaxX - levelMinX <= 2f * half ? (levelMinX + levelMaxX) / 2f : Mathf.Clamp(x, levelMinX + half, levelMaxX - half);
        }
    }
}
