using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Une couche du décor qui défile moins vite (ou plus vite) que le monde où marche Yao.
    ///
    /// Convention : le facteur est la VITESSE APPARENTE de la couche à l'écran, rapportée à celle du monde jouable.
    ///   0   = fixe à l'écran (le ciel) ;
    ///   0,35 = défile à 35 % de la vitesse du sol (bâtiments lointains) ;
    ///   1   = défile exactement comme le sol (aucun décalage) ;
    ///   1,1 = défile 10 % plus vite que le sol (avant-plan).
    /// Pour cela, la couche entière est décalée de camera.x × (1 − facteur) ; ses enfants ne bougent jamais
    /// (un seul script par couche, rien à calculer par objet).
    ///
    /// Placement : un enfant posé en x = facteur × X passe au centre de l'écran quand la caméra est en X.
    /// C'est ce que fait le constructeur du stage : on raisonne en X du monde, il convertit.
    ///
    /// Verticalement, le facteur est séparé : 1 pour tout ce qui est posé au sol (les bâtiments restent collés au
    /// sol quand la caméra monte), 0 pour le ciel et les nuages qui restent dans le ciel.
    /// </summary>
    [DefaultExecutionOrder(200)] // après la caméra (CameraFollow2D), pour ne pas trembler d'une image
    public class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("Vitesse apparente horizontale : 0 = fixe à l'écran, 1 = comme le sol, > 1 = avant-plan.")]
        public float factor = 1f;
        [Tooltip("Vitesse apparente verticale : 1 pour ce qui est posé au sol, 0 pour ce qui reste dans le ciel.")]
        public float verticalFactor = 1f;
        [Tooltip("Caméra suivie ; la caméra principale si vide.")]
        public Camera targetCamera;

        void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;
            var c = targetCamera.transform.position;
            transform.position = new Vector3(c.x * (1f - factor), c.y * (1f - verticalFactor), transform.position.z);
        }

        /// <summary>Position locale à donner à un objet de cette couche pour qu'il passe au centre de l'écran
        /// quand la caméra est en <paramref name="worldX"/>.</summary>
        public static float LocalX(float factor, float worldX) => factor * worldX;
    }
}
