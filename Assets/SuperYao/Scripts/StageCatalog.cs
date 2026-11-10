using System.Collections.Generic;
using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Les stages du chapitre 1 (Côte d'Ivoire), dans l'ordre de la carte : nom, phrase d'accroche, scène, vignette,
    /// position sur la carte (pixels de l'image map_ci, points de passage du chemin) et objets à ramasser.
    /// Une scène vide = stage pas encore construit (« Bientôt » sur la carte). Rempli par Super Yao > Construire
    /// l'accueil et la carte ; les totaux du stage 1 sont mis à jour par la construction du stage.
    /// </summary>
    [CreateAssetMenu(menuName = "Super Yao/Catalogue des stages")]
    public class StageCatalog : ScriptableObject
    {
        [System.Serializable]
        public class Stage
        {
            public string name;
            [TextArea] public string tagline;
            public string scene;
            public Sprite thumbnail;
            public Vector2 mapPosition;
            public Vector2[] via = new Vector2[0];
            public int coins, bananas, diamonds;
            public bool Built => !string.IsNullOrEmpty(scene);
        }

        public List<Stage> stages = new List<Stage>();

        public static StageCatalog Load() => Resources.Load<StageCatalog>("StageCatalog");
    }
}
