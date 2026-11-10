using UnityEditor;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Réglages d'import automatiques pour toutes les images de Super Yao : une image = un sprite, pivot au pied
    /// (bas-centre), 160 px par unité (une unité = une case du jeu ; Yao mesure environ 1,5 unité). Les personnages
    /// restent sans compression (contours nets) ; les grands fonds, rangés dans un dossier Backgrounds, sont
    /// compressés pour ne pas alourdir le jeu sur téléphone. Toutes les images ont des mipmaps : elles sont souvent
    /// affichées plus petites que leur taille d'origine et seraient crénelées ou scintilleraient en défilant sans elles.
    /// </summary>
    public class SuperYaoSpriteImport : AssetPostprocessor
    {
        const string Root = "Assets/SuperYao/";
        const string BackgroundsFolder = "/Backgrounds/";
        public const float PixelsPerUnit = 160f;

        // À augmenter à chaque changement de réglage : Unity réimporte alors les images concernées.
        public override uint GetVersion() => 5;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            bool background = assetPath.Contains(BackgroundsFolder);
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            // Mipmaps partout : presque toutes les images (planches, pontons, caisses, Yao…) sont affichées plus petites
            // que leur taille d'origine ; sans mipmaps, elles sont crénelées et scintillent en défilant.
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Trilinear;
            // Le filtrage trilinéaire choisit des niveaux de mipmap un peu trop flous : on vise un demi-niveau plus net
            // (assez pour retrouver du piqué, pas assez pour faire revenir le crénelage et le scintillement).
            importer.mipMapBias = -0.5f;
            importer.anisoLevel = 2;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = background
                ? TextureImporterCompression.CompressedHQ
                : TextureImporterCompression.Uncompressed;

            // Téléphones : format compressé imposé (ASTC, lu par tous les Android et iPhone récents). Sans ce réglage,
            // l'APK contenait des images brutes (RGBA 32 bits) : 89 Mo d'images pour le seul stage 1.
            // Décors : ASTC 6×6 (3,6 bits par pixel) ; Yao, objets, icônes : ASTC 4×4, plus net (8 bits par pixel).
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var ps = importer.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = 2048;
                ps.format = background ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4;
                ps.compressionQuality = 100;
                importer.SetPlatformTextureSettings(ps);
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
