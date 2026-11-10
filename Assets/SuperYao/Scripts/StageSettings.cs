using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Réglages communs à tous les stages, appliqués au lancement : paysage, 60 images/s (30 par défaut sur
    /// mobile), écran toujours allumé, gravité de la version téléphone.
    /// </summary>
    public class StageSettings : MonoBehaviour
    {
        [Tooltip("Gravité en cases/s² : 0,62 px par frame² à 60 images/s dans la version téléphone.")]
        public float gravity = -139.5f;
        public int targetFrameRate = 60;

        void Awake()
        {
            Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Physics2D.gravity = new Vector2(0f, gravity);
            // Physique à 60 pas par seconde, comme la version téléphone (une image = un pas).
            Time.fixedDeltaTime = 1f / targetFrameRate;
        }
    }
}
