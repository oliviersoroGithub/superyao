using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Fabrique l'APK Android de test (menu Super Yao > Construire l'APK Android) : nom et icône de Super Yao,
    /// identifiant distinct de la version téléphone (com.oliviersoro.superyao.unity, pour l'installer à côté),
    /// paysage seulement, IL2CPP 64 bits (tous les téléphones Android récents). L'APK est rangé dans
    /// Builds/Android/SuperYao.apk, à côté du dossier Assets.
    /// </summary>
    public static class AndroidBuilder
    {
        const string PackageId = "com.oliviersoro.superyao.unity";
        const string Output = "Builds/Android/SuperYao.apk";
        const string IconPath = "Assets/SuperYao/UI/app_icon.png";

        /// <summary>Vérifie que les images du jeu sont bien en ASTC pour Android ; sinon (importées avant le réglage, ou
        /// sans compression à l'import), on les réimporte en forçant la compression. Le bilan est écrit dans la Console.</summary>
        static void EnsureCompressedTextures()
        {
            bool compressOnImport = EditorPrefs.GetBool("kCompressTexturesOnImport", true);
            if (!compressOnImport) EditorPrefs.SetBool("kCompressTexturesOnImport", true);
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/SuperYao" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            int fixedCount = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var p in paths)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    if (tex == null || tex.format.ToString().StartsWith("ASTC")) continue;
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                    fixedCount++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            var sample = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SuperYao/Backgrounds/Stage1/Vegetation/palm_04.png");
            Debug.Log($"Super Yao : textures — compression à l'import {(compressOnImport ? "active" : "était DÉSACTIVÉE (réactivée)")}, " +
                      $"{fixedCount}/{paths.Length} réimportées ; exemple palm_04 : {(sample != null ? sample.format.ToString() : "?")}.");
        }

        [MenuItem("Super Yao/Construire l'APK Android")]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Super Yao",
                    "Le module Android n'est pas installé.\n\nDans Unity Hub : Installs > roue dentée de 6000.6.7f1 > Add modules > " +
                    "cocher « Android Build Support » avec « OpenJDK » et « Android SDK & NDK Tools », installer, puis rouvrir le projet.",
                    "OK");
                return;
            }
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || !scenes[0].EndsWith(GameProgress.TitleScene + ".unity"))
            {
                EditorUtility.DisplayDialog("Super Yao", "Lance d'abord « Construire le stage 1 » puis « Construire l'accueil et la carte » : le jeu doit commencer par l'accueil.", "OK");
                return;
            }

            // Réglages de l'application.
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Olivier Soro";
            PlayerSettings.productName = "Super Yao";
            PlayerSettings.SetApplicationIdentifier(android, PackageId);
            PlayerSettings.bundleVersion = "0.1";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Code plus léger : on retire le code Unity et C# jamais appelé, et on compile pour la taille.
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetIl2CppCodeGeneration(android, Il2CppCodeGeneration.OptimizeSize);
            // Paysage seulement, dans les deux sens (aussi sur l'accueil et la carte).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            EditorUserBuildSettings.buildAppBundle = false; // un APK à installer directement, pas un paquet pour le Play Store
            // Le profil de construction peut imposer « textures non compressées » (constaté : APK avec images brutes
            // malgré le format ASTC de chaque image). On le remet sur « pas de forçage » et sans limite de taille.
            EditorUserBuildSettings.overrideTextureCompression = OverrideTextureCompression.NoOverride;
            EditorUserBuildSettings.overrideMaxTextureSize = 0;
            Debug.Log("Super Yao : compression des textures forcée à « aucun forçage », taille max sans limite.");

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            // Formats de compression des images autorisés pour Android : la liste du projet était VIDE (réglage du
            // modèle). Sans ASTC dans cette liste, Unity laissait toutes les images brutes (RGBA 32 bits) dans l'APK,
            // quel que soit le réglage de chaque image. On la fixe à ASTC, puis ETC2 pour les très vieux téléphones.
            var formats = PlayerSettings.Android.textureCompressionFormats;
            bool hadAstc = formats != null && formats.Contains(TextureCompressionFormat.ASTC);
            PlayerSettings.Android.textureCompressionFormats = new[] { TextureCompressionFormat.ASTC, TextureCompressionFormat.ETC2 };
            Debug.Log($"Super Yao : formats de compression Android : avant [{(formats == null ? "" : string.Join(", ", formats))}], maintenant [ASTC, ETC2].");
            if (!hadAstc) AssetDatabase.Refresh();
            EnsureCompressedTextures();

            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Output,
                target = BuildTarget.Android,
                // Construction complète : sinon Unity réutilise ses données précédentes (images brutes comprises).
                options = BuildOptions.CleanBuildCache,
            });

            var summary = report.summary;
            // Unity peut annoncer un succès sans avoir écrit l'APK (module Android pas chargé) : on vérifie le fichier.
            bool written = File.Exists(Output) && File.GetLastWriteTime(Output) > System.DateTime.Now.AddMinutes(-60);
            if (summary.result == BuildResult.Succeeded && !written)
            {
                EditorUtility.DisplayDialog("Super Yao",
                    "Unity n'a pas pu fabriquer l'APK : le module Android n'est pas chargé.\n\nFerme complètement Unity, rouvre le projet " +
                    "depuis Unity Hub, puis relance « Construire l'APK Android ».", "OK");
                Debug.LogError("Super Yao : l'APK n'a pas été écrit (module Android pas chargé ? Redémarre Unity).");
                return;
            }
            if (summary.result == BuildResult.Succeeded)
            {
                WriteSizeReport(report);
                string full = Path.GetFullPath(Output);
                Debug.Log($"Super Yao : APK prêt ({new FileInfo(full).Length / (1024f * 1024f):0.0} Mo) : {full}");
                EditorUtility.RevealInFinder(full);
            }
            else Debug.LogError($"Super Yao : la construction de l'APK a échoué ({summary.result}, {summary.totalErrors} erreur(s)). Voir la Console.");
        }

        const string SizeReport = "Builds/taille_apk.txt";

        /// <summary>Ce qui pèse dans l'APK (données du jeu, avant la compression du fichier) : par dossier, puis les 60 plus
        /// gros fichiers, avec leur format d'image sur Android. Écrit dans Builds/taille_apk.txt et dans la Console.</summary>
        static void WriteSizeReport(UnityEditor.Build.Reporting.BuildReport report)
        {
            var sizes = new System.Collections.Generic.Dictionary<string, ulong>();
            foreach (var pack in report.packedAssets)
                foreach (var c in pack.contents)
                {
                    string path = string.IsNullOrEmpty(c.sourceAssetPath) ? "(moteur Unity)" : c.sourceAssetPath;
                    sizes.TryGetValue(path, out var v);
                    sizes[path] = v + c.packedSize;
                }
            string Folder(string p)
            {
                var parts = p.Split('/');
                return parts.Length > 3 && parts[0] == "Assets" ? string.Join("/", parts, 0, System.Math.Min(4, parts.Length - 1)) : parts[0];
            }
            string Mo(ulong b) => (b / (1024.0 * 1024.0)).ToString("0.0") + " Mo";
            ulong total = 0;
            foreach (var v in sizes.Values) total += v;
            var lines = new System.Collections.Generic.List<string>
            {
                $"APK : {Mo((ulong)new FileInfo(Output).Length)} ; données du jeu (avant compression du fichier) : {Mo(total)}",
                "", "Par dossier :",
            };
            var folders = new System.Collections.Generic.Dictionary<string, ulong>();
            foreach (var kv in sizes) { folders.TryGetValue(Folder(kv.Key), out var v); folders[Folder(kv.Key)] = v + kv.Value; }
            var byFolder = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, ulong>>(folders);
            byFolder.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < byFolder.Count && i < 30; i++) lines.Add($"  {Mo(byFolder[i].Value),9}  {byFolder[i].Key}");
            lines.Add("");
            lines.Add("Plus gros fichiers :");
            var files = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, ulong>>(sizes);
            files.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < files.Count && i < 60; i++)
            {
                string p = files[i].Key, fmt = "";
                if (AssetImporter.GetAtPath(p) is TextureImporter ti)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    fmt = $"  [{(tex != null ? tex.width + "x" + tex.height : "?")} {ti.GetPlatformTextureSettings("Android").format}]";
                }
                lines.Add($"  {Mo(files[i].Value),9}  {p}{fmt}");
            }
            File.WriteAllLines(SizeReport, lines);
            Debug.Log("Super Yao : ce qui pèse dans l'APK -> " + Path.GetFullPath(SizeReport) + "\n" + string.Join("\n", lines.GetRange(0, System.Math.Min(20, lines.Count))));
        }
    }
}
