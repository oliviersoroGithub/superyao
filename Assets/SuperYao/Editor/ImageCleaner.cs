using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Supprime les images inutilisées du jeu (menu Super Yao > Nettoyer : supprimer les images inutilisées).
    /// 1. Reconstruit tous les stages, puis l'accueil et la carte, pour que les scènes soient à jour.
    /// 2. Relève tout ce dont dépendent les scènes du jeu, le catalogue (dossier Resources) et l'icône de l'application.
    /// 3. Les images (png, jpg) de Assets/SuperYao qui n'y figurent pas sont listées, puis, après confirmation, envoyées à
    ///    la corbeille de Windows (récupérables). La liste est écrite dans Builds/images_supprimees.txt.
    /// </summary>
    public static class ImageCleaner
    {
        const string Root = "Assets/SuperYao";
        const string IconPath = "Assets/SuperYao/UI/app_icon.png";
        const string Report = "Builds/images_supprimees.txt";

        [MenuItem("Super Yao/Nettoyer : supprimer les images inutilisées")]
        public static void Clean()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Super Yao", "Arrête d'abord le jeu (bouton Play).", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Super Yao",
                    "Tous les stages, l'accueil et la carte vont être reconstruits, puis les images que le jeu n'utilise pas " +
                    "seront listées. Tu confirmeras avant la suppression (elles iront dans la corbeille de Windows).",
                    "Continuer", "Annuler")) return;

            Stage01Builder.Build();
            Stage02Builder.Build();
            Stage03Builder.Build();
            Stage04Builder.Build();
            Stage05Builder.Build();
            Stage06Builder.Build();
            Stage07Builder.Build();
            Stage08Builder.Build();
            Stage09Builder.Build();
            MenuBuilder.Build();

            var roots = new List<string>();
            roots.AddRange(EditorBuildSettings.scenes.Select(s => s.path).Where(p => p.EndsWith(".unity") && File.Exists(p)));
            roots.AddRange(AssetDatabase.FindAssets("", new[] { Root + "/Resources" }).Select(AssetDatabase.GUIDToAssetPath));
            roots.Add(IconPath);
            var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.Distinct().ToArray(), true));

            var images = AssetDatabase.FindAssets("t:Texture2D", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Where(p => p.EndsWith(".png") || p.EndsWith(".jpg") || p.EndsWith(".jpeg"))
                .ToList();
            var unused = images.Where(p => !used.Contains(p) && p != IconPath).OrderBy(p => p).ToList();
            if (unused.Count == 0)
            {
                EditorUtility.DisplayDialog("Super Yao", $"Aucune image inutilisée : les {images.Count} images servent toutes.", "OK");
                return;
            }
            long bytes = unused.Sum(p => new FileInfo(p).Length);
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllLines(Report, unused);
            Debug.Log($"Super Yao : {unused.Count} images inutilisées (liste dans {Report}) :\n" + string.Join("\n", unused));
            if (!EditorUtility.DisplayDialog("Super Yao",
                    $"{unused.Count} images sur {images.Count} ne servent pas ({bytes / (1024f * 1024f):0.0} Mo).\n" +
                    $"La liste est dans {Report} et dans la Console.\n\nLes envoyer à la corbeille ?", "Supprimer", "Annuler")) return;

            var failed = new List<string>();
            AssetDatabase.MoveAssetsToTrash(unused.ToArray(), failed);
            AssetDatabase.Refresh();
            Debug.Log($"Super Yao : {unused.Count - failed.Count} images envoyées à la corbeille" +
                      (failed.Count > 0 ? $", {failed.Count} non supprimées :\n" + string.Join("\n", failed) : "."));
            EditorUtility.DisplayDialog("Super Yao", $"{unused.Count - failed.Count} images supprimées (corbeille de Windows).", "OK");
        }
    }
}
