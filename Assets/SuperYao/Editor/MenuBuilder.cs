using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Construit l'accueil et la carte (menu Super Yao > Construire l'accueil et la carte) : le catalogue des 12
    /// stages du chapitre 1 (positions sur la carte reprises du téléphone), les scènes « Accueil » et « Carte », et
    /// l'ordre des scènes du jeu : Accueil, Carte, puis les stages.
    /// </summary>
    public static class MenuBuilder
    {
        public const string CatalogPath = "Assets/SuperYao/Resources/StageCatalog.asset";
        const string UiDir = "Assets/SuperYao/UI/";
        const string ScenesDir = "Assets/SuperYao/Scenes/";
        const string Stage1Scene = "Stage01_GrandBassam";

        /// <summary>Les étapes de la carte, en pixels de l'image map_ci (MapScreen.tsx du téléphone).</summary>
        static readonly (string name, float x, float y, Vector2[] via)[] Nodes =
        {
            ("Grand-Bassam", 1139, 978, null),
            ("Bassam → Cocody", 1014, 847, new[] { new Vector2(1040, 982) }),
            ("Cocody → Plateau", 737, 827, null),
            ("Plateau la nuit", 848, 788, null),
            ("Adjamé → Yopougon", 546, 724, new[] { new Vector2(838, 704), new Vector2(660, 760) }),
            ("La route du Nord", 610, 600, new[] { new Vector2(642, 738) }),
            ("Yamoussoukro", 691, 470, null),
            ("La forêt sacrée", 470, 545, null),
            ("Taï", 244, 683, new[] { new Vector2(340, 700) }),
            ("Man", 284, 385, new[] { new Vector2(160, 655), new Vector2(190, 450) }),
            ("Korhogo", 652, 213, null),
            ("Le village sénoufo", 835, 262, null),
        };

        [MenuItem("Super Yao/Construire l'accueil et la carte")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Super Yao", "Arrête d'abord le jeu (bouton Play), puis relance la construction.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var catalog = Catalog();
            var font = AssetDatabase.LoadAssetAtPath<Font>(UiDir + "Fonts/LilitaOne-Regular.ttf");

            // Accueil.
            var scene = NewScene("#0E3B5C");
            var title = new GameObject("Accueil").AddComponent<TitleScreen>();
            title.font = font;
            title.background = Sprite(UiDir + "Backgrounds/abidjan_day.jpg");
            title.logo = Sprite(UiDir + "logo.png");
            title.hero = Sprite(UiDir + "hero_yao.png");
            title.portrait = Sprite(UiDir + "portrait_yao.png");
            title.soundOn = Sprite(UiDir + "Icons/sound_on.png");
            title.soundOff = Sprite(UiDir + "Icons/sound_off.png");
            title.gear = Sprite(UiDir + "Icons/gear.png");
            // Vidéo d'introduction de la version Unity (dossier video du projet d'import), pas la bande-annonce React.
            title.intro = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>("Assets/SuperYao/Video/super_yao_intro.mp4");
            if (title.intro == null) Debug.LogWarning("Super Yao : vidéo d'introduction introuvable (Assets/SuperYao/Video/super_yao_intro.mp4).");
            EditorSceneManager.SaveScene(scene, ScenesDir + GameProgress.TitleScene + ".unity");

            // Carte.
            scene = NewScene("#0E3B5C");
            var map = new GameObject("Carte").AddComponent<MapScreen>();
            map.font = font;
            map.map = Sprite(UiDir + "Backgrounds/map_ci.jpg");
            map.mapBlur = Sprite(UiDir + "Backgrounds/map_ci_blur.jpg");
            map.hero = Sprite("Assets/SuperYao/Heroes/Yao/Idle/yao_idle_00.png");
            map.portrait = Sprite(UiDir + "portrait_yao.png");
            map.coin = Sprite("Assets/SuperYao/Gameplay/Items/coin_0.png");
            map.banana = Sprite("Assets/SuperYao/Gameplay/Items/banana.png");
            map.diamond = Sprite("Assets/SuperYao/Gameplay/Items/diamond_0.png");
            map.soundOn = Sprite(UiDir + "Icons/sound_on.png");
            map.soundOff = Sprite(UiDir + "Icons/sound_off.png");
            map.home = Sprite(UiDir + "Icons/home.png");
            // Le marché, ouvert depuis la carte.
            var market = map.gameObject.AddComponent<MarketScreen>();
            market.font = font;
            market.background = Sprite(UiDir + "Backgrounds/market.jpg");
            market.portrait = map.portrait;
            market.banana = map.banana;
            market.coin = map.coin;
            market.diamond = map.diamond;
            market.shea = Sprite("Assets/SuperYao/Gameplay/Items/skateboard.png");
            market.fireOrb = Sprite("Assets/SuperYao/Gameplay/Items/fire_orb.png");
            market.coinSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SuperYao/Audio/coin.wav");
            market.lifeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SuperYao/Audio/life.wav");
            market.powerSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SuperYao/Audio/power.wav");
            EditorSceneManager.SaveScene(scene, ScenesDir + GameProgress.MapScene + ".unity");

            // Ordre du jeu : Accueil, Carte, puis les stages construits.
            var order = new List<string> { ScenesDir + GameProgress.TitleScene + ".unity", ScenesDir + GameProgress.MapScene + ".unity" };
            order.AddRange(catalog.stages.Where(s => s.Built).Select(s => ScenesDir + s.scene + ".unity").Where(p => AssetDatabase.LoadMainAssetAtPath(p) != null));
            var others = EditorBuildSettings.scenes.Where(s => !order.Contains(s.path) && !s.path.EndsWith("SampleScene.unity"));
            EditorBuildSettings.scenes = order.Select(p => new EditorBuildSettingsScene(p, true)).Concat(others).ToArray();

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenesDir + GameProgress.TitleScene + ".unity");
            Debug.Log("Super Yao : accueil et carte construits. Lance Play depuis la scène Accueil.");
        }

        /// <summary>Le catalogue des stages (créé s'il manque ; les totaux déjà comptés sont conservés).</summary>
        public static StageCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
            if (catalog == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/SuperYao/Resources")) AssetDatabase.CreateFolder("Assets/SuperYao", "Resources");
                catalog = ScriptableObject.CreateInstance<StageCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            while (catalog.stages.Count < Nodes.Length) catalog.stages.Add(new StageCatalog.Stage());
            for (int i = 0; i < Nodes.Length; i++)
            {
                var s = catalog.stages[i];
                s.name = Nodes[i].name;
                s.mapPosition = new Vector2(Nodes[i].x, Nodes[i].y);
                s.via = Nodes[i].via ?? new Vector2[0];
            }
            var first = catalog.stages[0];
            first.scene = Stage1Scene;
            first.tagline = "Tout commence à Grand-Bassam : file à travers le Quartier France, traverse la lagune et rejoins la plage !";
            first.thumbnail = Sprite(UiDir + "Backgrounds/thumb_bassam.jpg");
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static UnityEngine.SceneManagement.Scene NewScene(string color)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            ColorUtility.TryParseHtmlString(color, out var c);
            cam.backgroundColor = c;
            cam.gameObject.AddComponent<AudioListener>();
            return scene;
        }

        static Sprite Sprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogError("Super Yao : image manquante " + path);
            return s;
        }
    }
}
