using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Construit le stage 2, « De Bassam à Cocody » (menu Super Yao > Construire le stage 2) : l'autoroute de Bassam,
    /// Port-Bouët, Marcory, le pont Henri-Konan-Bédié, puis le péage côté Cocody, où l'on affronte le racketteur.
    /// Parallaxe, sol, parcours (voir CourseKit), ennemis, boss (TollBoss), Yao, caméra, panneau d'affichage et pause.
    /// Mêmes conventions que le stage 1 (voir Stage01Builder) : une unité = une case, sol (collision) en y = 0,
    /// dans une couche de facteur f un objet en x local = f × X passe au centre de l'écran quand la caméra est en X.
    /// Le ciel, les nuages, les oiseaux, les avions et les lampadaires sont ceux du stage 1.
    /// </summary>
    public static class Stage02Builder
    {
        const string Art = "Assets/SuperYao/Backgrounds/Stage2/";
        const string Art1 = "Assets/SuperYao/Backgrounds/Stage1/";
        const string PrefabDir = "Assets/SuperYao/Prefabs/Stage2";
        const string ScenePath = "Assets/SuperYao/Scenes/Stage02_BassamCocody.unity";
        const string HeroPrefab = "Assets/SuperYao/Prefabs/Heros/Heros_Yao.prefab";
        const string GameplayDir = "Assets/SuperYao/Gameplay/";
        const string FrictionlessPath = "Assets/SuperYao/Physics/SansFrottement.physicsMaterial2D";
        const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        const float FSky = 0f, FClouds = 0.05f, FHorizon = 0.15f, FFar = 0.35f, FMid = 0.65f, FStreet = 0.9f, FFront = 1.1f;
        const float VisibleRows = 20f;
        const float HalfViewMin = VisibleRows / 2f * 4f / 3f, HalfViewMax = VisibleRows / 2f * 2.4f;

        // Parcours (en cases, aussi long que le stage 1 : 350) : autoroute | Port-Bouët | Marcory | pont HKB au-dessus
        // de la lagune | péage, à l'arrivée côté Cocody (là où se tiendra le combat).
        const float LevelLength = 350f;
        const float TownStart = 90f, MarcoryStart = 175f, BridgeStart = 250f;
        const float BridgeLength = BridgeHeight * 2076f / 334f;          // bridge_hkb.png : 2076 × 334 px, soit 37,3 cases
        const float BridgeEnd = BridgeStart + BridgeLength;
        const float TollCenter = 318f;
        // Ravines où l'on tombe (comme les trous du viaduc sur le téléphone), de l'eau au fond : (début, fin) en x.
        // Sauts : environ 2,9 cases en marchant, 4,4 en courant ; celle de Port-Bouët se franchit par la pyramide de planches.
        static readonly (float x0, float x1)[] Ravines = { (34f, 37f), (75f, 78.5f), (130.4f, 135.2f), (229f, 232.5f) };
        static readonly float[] ZoneEnds = { TownStart, MarcoryStart, BridgeStart, BridgeEnd, float.MaxValue };
        static readonly string[] ZoneNames = { "Z1_Autoroute_Bassam", "Z2_Port_Bouet", "Z3_Marcory", "Z4_Pont_HKB", "Z5_Peage_Cocody" };

        // Sols : dessus de l'image. Asphalte : Yao marche sur la chaussée ; latérite : au milieu de la bordure en pierre.
        const float AsphaltTop = 0.2f, LateriteTop = 0.15f;
        const float BackOfStreet = 0.18f;  // pied des objets posés au fond de la rue, derrière Yao
        const float BuildingBase = 0f;

        // Pont HKB (bridge_hkb.png, 334 px de haut) : rambarde de 20 px puis tablier jusqu'à 82 px. Haut : 6 cases.
        const float BridgeHeight = 6f, BridgeDeckFromTop = 22f / 334f, BridgeDeckBottomFromTop = 82f / 334f;
        // Auvent du péage (canopy.png) : poteaux à ces fractions de la largeur ; toit sur les 37 % du haut.
        static readonly float[] CanopyPillars = { 0.057f, 0.204f, 0.354f, 0.501f, 0.650f, 0.801f, 0.943f };
        const float CanopyHeight = 6.5f;

        // Bâtiments (building_01 … 16) : hauteur en cases au plan intermédiaire (un étage ≈ 2,5 à 3 cases).
        // 1 station-service, 2 garage, 3 boutique à véranda, 4 entrepôt, 5 maison à étage, 6 maison bleue,
        // 7 maison à 3 niveaux, 8-10 immeubles de 4 niveaux, 11-13 immeubles de Marcory, 14-16 tours.
        static readonly float[] BuildingHeights = { 4.5f, 4.2f, 4.2f, 4.2f, 6.5f, 3.8f, 9f, 10f, 11f, 10f, 11.5f, 12f, 9.5f, 19f, 15f, 16f };
        const float FarScale = 0.55f;
        static readonly Color FarTint = new Color(0.80f, 0.86f, 0.94f);
        static readonly Color HorizonTint = new Color(0.90f, 0.94f, 1f);
        static readonly Color FrontTint = new Color(0.72f, 0.78f, 0.72f);

        static Material spriteMaterial;
        static PhysicsMaterial2D frictionless;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly List<string> missing = new List<string>();
        static readonly List<(int b, float x)> lastRow = new List<(int b, float x)>();

        [MenuItem("Super Yao/Construire le stage 2 (Bassam - Cocody)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Super Yao", "Arrête d'abord le jeu (bouton Play), puis relance la construction du stage.", "OK");
                return;
            }
            var hero = AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefab);
            if (hero == null)
            {
                EditorUtility.DisplayDialog("Super Yao", "Lance d'abord « Construire le stage 1 » : il fabrique Yao, que le stage 2 réutilise.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            prefabs.Clear();
            missing.Clear();
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            frictionless = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(FrictionlessPath);
            if (frictionless == null)
            {
                frictionless = new PhysicsMaterial2D("SansFrottement") { friction = 0f, bounciness = 0f };
                AssetDatabase.CreateAsset(frictionless, FrictionlessPath);
            }
            EnsureFolder(PrefabDir);
            CourseKit.Init(spriteMaterial, frictionless);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreatePrefabs();
            if (missing.Count > 0)
            {
                Debug.LogError("Super Yao : images manquantes, stage 2 non construit :\n" + string.Join("\n", missing));
                return;
            }

            var decor = new GameObject("Decor").transform;
            BuildSky(Layer(decor, "01_Ciel", FSky, 0f));
            BuildClouds(Layer(decor, "02_Nuages", FClouds, 0f));
            BuildSkyLife(decor);
            BuildHorizon(Layer(decor, "03_Horizon_PortBouet_Cocody", FHorizon, 1f));
            BuildFar(Layer(decor, "04_Batiments_Lointains_Lagune", FFar, 1f));
            BuildMid(Layer(decor, "05a_Batiments_Arbres", FMid, 1f));
            BuildStreet(Layer(decor, "05b_Lampadaires_Cocotiers_Proches", FStreet, 1f));

            var world = new GameObject("06_Monde_Jouable").transform;
            BuildGround(world);
            BuildBridge(world);
            BuildRoadside(world);
            BuildToll(world);
            BuildCourse(world);
            BuildBoss(world);
            var yao = (GameObject)PrefabUtility.InstantiatePrefab(hero, world);
            yao.transform.localPosition = new Vector3(4f, 0.02f, 0f);
            yao.AddComponent<HeroRespawn>();
            PutOnSkate(yao);
            BuildForeground(Layer(new GameObject("07_Avant_Plan").transform, "Vegetation_Avant_Plan", FFront, 1f));

            BuildCamera(yao.transform);
            BuildSettings();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log($"Super Yao : stage 2 (décor) construit ({LevelLength:0} cases de long) dans {ScenePath}.");
        }

        // ---------------------------------------------------------------- couches lointaines

        static void BuildSky(Transform layer)
        {
            var sr = Place(layer, "Ciel", 0f, -7f, 14f);
            sr.gameObject.AddComponent<FitToCamera>();
        }

        static void BuildClouds(Transform layer)
        {
            layer.gameObject.AddComponent<CloudDrift>();
            var clouds = new (int sprite, float x, float y, float h)[]
            {
                (2, -9f, 4.6f, 1.8f), (3, -1f, 3.0f, 2.2f), (1, 6f, 5.4f, 1.1f),
                (2, 13f, 2.6f, 1.5f), (1, 20f, 4.2f, 1.0f), (3, 28f, 5.0f, 2.0f),
            };
            foreach (var c in clouds) Place(layer, $"Nuage_0{c.sprite}", c.x, c.y, c.h);
        }

        /// <summary>Oiseaux et avions du stage 1 (Port-Bouët : on est à côté de l'aéroport, les avions passent bas).</summary>
        static void BuildSkyLife(Transform decor)
        {
            var birdFrames = Frames(Art1 + "SkyLife/Birds");
            var planes = new[] { "airliner", "bizjet", "cessna" }
                .Select(n => AssetDatabase.LoadAssetAtPath<Sprite>(Art1 + $"SkyLife/Planes/plane_{n}.png")).Where(s => s != null).ToArray();
            if (birdFrames.Length == 0 && planes.Length == 0) return;
            var go = new GameObject("02b_Ciel_Vivant");
            go.transform.SetParent(decor, false);
            var traffic = go.AddComponent<SkyTraffic>();
            traffic.birdFrames = birdFrames;
            traffic.planeSprites = planes;
            var birds = new List<SpriteRenderer>();
            for (int i = 0; birdFrames.Length > 0 && i < 3; i++)
            {
                var sr = SkyRenderer(go.transform, $"Oiseau_{i + 1}", birdFrames[0], 5);
                float s = 0.6f * (1f - 0.12f * i) / birdFrames[0].bounds.size.x;
                sr.transform.localScale = new Vector3(s, s, 1f);
                birds.Add(sr);
            }
            traffic.birds = birds.ToArray();
            if (planes.Length > 0) traffic.plane = SkyRenderer(go.transform, "Avion", planes[0], -1);
        }

        static SpriteRenderer SkyRenderer(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = spriteMaterial;
            sr.sortingLayerName = "Nuages";
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        /// <summary>Horizon : la plage et la mer de Port-Bouët jusqu'au pont, puis Cocody au loin, dans les arbres (de
        /// l'autre côté de la lagune, puis derrière le péage). La bande de Cocody commence par une copie dont le bord
        /// gauche s'efface en fondu, posée devant la fin de Port-Bouët.</summary>
        static void BuildHorizon(Transform layer)
        {
            var (min, max) = LocalRange(FHorizon);
            float cocodyStart = FHorizon * (BridgeStart - 15f); // Cocody apparaît au loin un peu avant le pont
            const float portBouetHeight = 2.2f, cocodyHeight = 3.4f;

            var start = Place(layer, "HorizonCocodyDebut", 0f, BuildingBase + 0.1f, cocodyHeight, 1, false, HorizonTint);
            float startWidth = start.sprite.bounds.size.x * start.transform.localScale.x;
            start.transform.localPosition = new Vector3(cocodyStart + startWidth / 2f, BuildingBase + 0.1f, 0f);
            Band(layer, "HorizonPortBouet", min, cocodyStart + startWidth * 0.4f, BuildingBase + 0.1f, portBouetHeight, 0, HorizonTint);
            Band(layer, "HorizonCocody", cocodyStart + startWidth, max, BuildingBase + 0.1f, cocodyHeight, 1, HorizonTint);
        }

        /// <summary>Lointains : quelques maisons à Port-Bouët, les tours de Marcory, la lagune Ébrié derrière le pont,
        /// puis la rive de Cocody (des arbres seulement : derrière le péage, on voit l'horizon de Cocody au loin).</summary>
        static void BuildFar(Transform layer)
        {
            const string sort = "BatimentsLointains";
            float x = FFar * TownStart;
            x = Row(layer, FFar, x, FarScale, FarTint, sort, (6, 0f, false), (5, 1.5f, true), (7, 2f, false), (6, 2.5f, true), (5, 2f, false));
            Row(layer, FFar, x, FarScale, FarTint, sort, (14, 1f, false), (12, 0.4f, false), (16, 0.6f, false), (11, 0.4f, true), (15, 0.6f, false));
            float seaStart = lastRow[lastRow.Count - 1].x; // la dernière tour de Marcory cache le bord gauche de la lagune
            // Rive de Cocody, au-delà du pont : des buissons et un arbre cachent le bord droit de la lagune.
            float seaEnd = FFar * (BridgeEnd + 3f);
            Band(Zone(layer, FFar, seaStart), "Mer", seaStart, seaEnd + 0.5f, -0.3f, 1.9f, -1, null, "Lagune_Lointaine", sort);
            Shore(layer, FFar, seaEnd, FarScale, FarTint, sort);
            foreach (var (prefab, wx, flip) in new[] { ("Cocotier_02", 305f, true), ("Arbre", 326f, false), ("Cocotier_01", 341f, false) })
                Place(Zone(layer, FFar, FFar * wx), prefab, FFar * wx, BuildingBase, (prefab == "Arbre" ? 7f : 9f) * FarScale, 1, flip, FarTint, sort);
        }

        /// <summary>Plan intermédiaire : autoroute bordée de stations et d'entrepôts espacés, Port-Bouët (maisons et
        /// boutiques), Marcory (immeubles), la lagune Ébrié derrière le pont, puis la rive de Cocody, boisée et basse :
        /// derrière le péage, l'horizon de Cocody au loin reste visible.</summary>
        static void BuildMid(Transform layer)
        {
            var w = Color.white;
            const string sort = "PlanIntermediaire";
            // Z1 autoroute : dégagée, on voit la mer de Port-Bouët entre les bâtiments.
            float x = Row(layer, FMid, -16f, 1f, w, sort, (6, 2f, false), (1, 12f, false), (4, 9f, false), (2, 6f, false), (1, 10f, true));
            // Z2 Port-Bouët : maisons et boutiques serrées.
            x = Row(layer, FMid, x, 1f, w, sort, (5, 1f, false), (3, 0.5f, false), (6, 0.5f, true), (7, 0.5f, false), (5, 0.5f, true),
                (3, 0.5f, true), (6, 0.5f, false), (2, 0.5f, false));
            // Z3 Marcory : immeubles ; le dernier cache le bord gauche de la lagune.
            Row(layer, FMid, x, 1f, w, sort, (8, 1f, false), (12, 0.5f, false), (10, 0.5f, false), (13, 0.5f, false));
            float seaStart = lastRow[lastRow.Count - 1].x;
            // Rive de Cocody, après le pont : des buissons et un arbre cachent le bord droit de la lagune.
            float seaEnd = FMid * (BridgeEnd + 2f);
            var sea = Band(Zone(layer, FMid, seaStart), "Mer", seaStart, seaEnd + 0.8f, -0.5f, 2.2f, -2, null, "Lagune_Ebrie", sort);
            Shore(layer, FMid, seaEnd, 1f, Color.white, sort);
            var fx = sea.gameObject.AddComponent<WaterFx>();
            fx.drift = 0.45f; fx.glintSize = 0.2f; fx.glintDepth = 1.2f; fx.casesPerGlint = 2.5f;

            // Cocotiers et arbres (X du monde) ; ordre -1 : derrière les bâtiments, +1 : devant.
            var trees = new (string prefab, float wx, float h, bool flip, int order)[]
            {
                // Autoroute
                ("Cocotier_01", 3f, 9.5f, false, 1), ("Arbre", 14f, 7f, false, 1), ("Cocotier_03", 24f, 9f, true, 1),
                ("Cocotier_02", 38f, 9.5f, false, 1), ("Arbre", 47f, 7.5f, true, 1), ("Cocotier_01", 56f, 9f, true, 1),
                ("Arbre", 68f, 7f, false, 1), ("Cocotier_02", 80f, 9.5f, true, 1),
                // Port-Bouët et Marcory : derrière les bâtiments, leurs palmes dépassent des toits
                ("Cocotier_03", 100f, 9.5f, false, -1), ("Cocotier_02", 125f, 9f, true, -1), ("Arbre", 150f, 7f, false, -1),
                ("Cocotier_01", 168f, 10f, false, -1), ("Arbre", 185f, 8f, true, -1), ("Cocotier_03", 205f, 10f, false, -1),
                ("Cocotier_02", 228f, 10f, true, -1), ("Arbre", 242f, 8f, false, -1),
                // Cocody, après le pont
                ("Arbre", 300f, 7.5f, false, -1), ("Cocotier_03", 318f, 9.5f, true, -1), ("Cocotier_01", 336f, 9f, false, -1),
            };
            foreach (var t in trees)
                Place(Zone(layer, FMid, FMid * t.wx), t.prefab, FMid * t.wx, BuildingBase, t.h, t.order, t.flip, null, sort);
        }

        static void BuildStreet(Transform layer)
        {
            // Lampadaires du stage 1 le long des quartiers, sur l'autoroute et au péage (aucun sur le pont : ils seraient dans l'eau).
            var lamps = new (float wx, bool right)[]
            {
                (12f, false), (36f, true), (60f, false), (84f, true),                       // autoroute
                (100f, false), (116f, true), (132f, false), (148f, true), (164f, false),   // Port-Bouët
                (182f, true), (198f, false), (214f, true), (230f, false), (244f, true),    // Marcory
                (292f, false), (312f, true), (332f, false), (346f, true),                 // péage
            };
            foreach (var l in lamps)
                Place(Zone(layer, FStreet, FStreet * l.wx), l.right ? "Lampadaire_B" : "Lampadaire_A", FStreet * l.wx, BuildingBase, 8f);
            var palms = new (string prefab, float wx, float h, bool flip)[]
            {
                ("Cocotier_04", 22f, 6f, false), ("Cocotier_01", 46f, 10.5f, true), ("Cocotier_04", 70f, 6f, true),
                ("Cocotier_02", 110f, 10f, false), ("Cocotier_03", 140f, 10.5f, true), ("Cocotier_04", 170f, 6f, false),
                ("Cocotier_04", 200f, 6f, true), ("Cocotier_01", 226f, 10.5f, false),
                ("Cocotier_04", 296f, 6f, false), ("Cocotier_02", 340f, 10f, true),
            };
            foreach (var p in palms) Place(Zone(layer, FStreet, FStreet * p.wx), p.prefab, FStreet * p.wx, BuildingBase, p.h, 1, p.flip);
        }

        // ---------------------------------------------------------------- monde jouable

        static void BuildGround(Transform world)
        {
            var ground = new GameObject("Sol").transform;
            ground.SetParent(world, false);
            // Terre rouge et bordure en pierre dès le départ ; asphalte après le pont (le péage).
            // Les deux bouts du pont recouvrent la fin du sol de chaque côté.
            // Les ravines coupent la latérite (sol et collision) ; de l'eau au fond, derrière les bords du sol.
            float from = -2f;
            foreach (var r in Ravines)
            {
                GroundSpan(ground, "SolLaterite", from, r.x0, LateriteTop);
                SolidBox(ground, $"Collision_x{Mathf.Max(from, -1f):0}-{r.x0:0.#}", Mathf.Max(from, -1f), r.x0);
                var water = Band(ground, "Mer", r.x0 - 0.4f, r.x1 + 0.4f, -2.8f, 2.45f, -1, null, $"Ravine_x{r.x0:0.#}", "Monde");
                var wfx = water.gameObject.AddComponent<WaterFx>();
                wfx.drift = 0.7f; wfx.glintSize = 0.3f; wfx.glintDepth = 0.4f; wfx.casesPerGlint = 3.5f;
                from = r.x1;
            }
            GroundSpan(ground, "SolLaterite", from, BridgeStart + 0.6f, LateriteTop);
            GroundSpan(ground, "SolAsphalte", BridgeEnd - 0.6f, LevelLength + 2f, AsphaltTop);
            // Après la dernière ravine, une seule boîte (le tablier du pont est au niveau de la route) : aucune jointure.
            SolidBox(ground, "Collision_Marcory_Pont_Peage", from, LevelLength + 1f);
            Wall(ground, "Mur_Debut", -0.5f);
            Wall(ground, "Mur_Fin", LevelLength + 0.5f);
        }

        /// <summary>Le pont HKB, jouable, de Marcory à Cocody : tablier au niveau de la route (y = 0), la lagune entre les piles.</summary>
        static void BuildBridge(Transform world)
        {
            var group = new GameObject("Pont_HKB").transform;
            group.SetParent(world, false);
            var size = prefabs["PontHKB"].GetComponent<SpriteRenderer>().sprite.bounds.size;
            float width = size.x * BridgeHeight / size.y;
            var bridge = Place(group, "PontHKB", BridgeStart + width / 2f, -(1f - BridgeDeckFromTop) * BridgeHeight, BridgeHeight, 1);
            bridge.sortingLayerName = "Monde";
            float deckBottom = -(BridgeDeckBottomFromTop - BridgeDeckFromTop) * BridgeHeight;
            var sea = Band(group, "Mer", BridgeStart, BridgeStart + width, deckBottom - 3f, deckBottom + 0.2f, -1, null, "Lagune_Sous_Pont", "Monde");
            var fx = sea.gameObject.AddComponent<WaterFx>();
            fx.drift = 0.7f; fx.glintSize = 0.35f; fx.glintDepth = 0.5f; fx.casesPerGlint = 3.5f;
        }

        /// <summary>Bord de route, au fond, derrière Yao (sans collision) : glissières de l'autoroute, panneaux,
        /// marché de Port-Bouët, chantier, plantes.</summary>
        static void BuildRoadside(Transform world)
        {
            var props = new GameObject("Bord_De_Route").transform;
            props.SetParent(world, false);
            // Glissière de l'autoroute (répétée) et son embout ; glissière double à l'approche du pont.
            float railFrom = 1f;
            foreach (var r in Ravines)
            {
                if (r.x0 > 84f) break;
                Band(Zone(props, 1f, (railFrom + r.x0) / 2f), "Glissiere", railFrom, r.x0 - 1f, BackOfStreet, 0.75f, 1);
                Place(Zone(props, 1f, r.x0), "GlissiereEmbout", r.x0 - 0.5f, BackOfStreet, 0.75f, 1);
                Place(Zone(props, 1f, r.x1), "GlissiereEmbout", r.x1 + 0.5f, BackOfStreet, 0.75f, 1, true);
                railFrom = r.x1 + 1f;
            }
            Band(Zone(props, 1f, 82f), "Glissiere", railFrom, 84f, BackOfStreet, 0.75f, 1);
            Place(Zone(props, 1f, 84f), "GlissiereEmbout", 84.5f, BackOfStreet, 0.75f, 1);
            Band(Zone(props, 1f, BridgeStart - 4f), "GlissiereDouble", BridgeStart - 7f, BridgeStart - 2.5f, BackOfStreet, 0.9f, 1);

            var items = new (string prefab, float x, float h, bool flip, int order)[]
            {
                // Panneaux
                ("Panneau_Abidjan", 8f, 2.6f, false, 3), ("Panneau_80", 40f, 2.4f, false, 3), ("Panneau_PortBouet", 87f, 2.6f, false, 3),
                ("Panneau_Marcory", 172f, 2.6f, false, 3), ("Panneau_Virage", 238f, 2.4f, false, 3), ("Panneau_Peage", 270f, 2.6f, false, 3), // le second sur le pont
                // Chantier sur l'autoroute
                ("Route_Cones", 50f, 0.7f, false, 3), ("Route_Barriere", 53f, 1.0f, false, 3), ("Route_Cone", 55.5f, 0.7f, false, 3),
                // Marché de Port-Bouët : parasols derrière les tables, chariots
                ("Marche_ParasolCouleurs", 104.6f, 2.6f, false, 2), ("Marche_TableFruits", 105f, 1.3f, false, 3),
                ("Marche_Chariot", 114f, 2.2f, false, 3),
                ("Marche_ParasolOrange", 140.6f, 2.6f, false, 2), ("Marche_TableBassines", 141f, 1.3f, false, 3),
                ("Marche_ParasolCouleurs", 149.6f, 2.6f, true, 2), ("Marche_TableFruits", 150f, 1.3f, true, 3),
                ("Marche_Chariot", 160f, 2.2f, true, 3),
                // Marcory
                ("Route_BlocBeton", 190f, 0.7f, false, 3), ("Route_Balise", 210f, 0.9f, false, 3), ("Route_Balise", 211.2f, 0.9f, false, 3),
                // Entrée du pont
                ("Route_Balise", 248f, 0.9f, false, 3), ("Route_Balise", 249.2f, 0.9f, false, 3),
                // Sortie du pont, devant le péage ; après le péage
                ("Route_SeparateurPlastique", 289.5f, 0.9f, false, 3), ("Route_Cone", 291f, 0.7f, false, 3), ("Route_Cone", 292.2f, 0.7f, false, 3),
                ("Route_Balise", 336f, 0.9f, false, 3), ("Route_Balise", 337.2f, 0.9f, false, 3),
                // Plantes
                ("Plante_Herbes", 2f, 0.9f, false, 2), ("Plante_HerbesSeches", 17f, 1.0f, false, 2), ("Plante_Touffes", 30f, 0.8f, false, 2),
                ("Plante_HerbesSeches", 62f, 1.0f, true, 2), ("Plante_Buisson", 80f, 1.3f, false, 2), ("Plante_LargesFeuilles", 96f, 1.5f, false, 2),
                ("Plante_BuissonRouge", 120f, 1.3f, false, 2), ("Plante_Branches", 146f, 1.4f, false, 2), ("Plante_Herbes", 158f, 0.9f, true, 2),
                ("Plante_BuissonJaune", 180f, 1.3f, false, 2), ("Plante_Touffes", 195f, 0.8f, false, 2), ("Plante_LargesFeuilles", 226f, 1.5f, true, 2),
                ("Plante_Buisson", 236f, 1.3f, true, 2), ("Plante_Touffes", 296f, 0.8f, true, 2), ("Plante_HerbesSeches", 344f, 1.0f, false, 2),
            };
            foreach (var i in items) Place(Zone(props, 1f, i.x), i.prefab, i.x, BackOfStreet, i.h, i.order, i.flip);
        }

        /// <summary>Le péage du pont HKB, à l'arrivée côté Cocody, où se tiendra le combat : auvent sur sept poteaux, cabines, barrières levées
        /// (Yao passe), feux, borne de paiement et séparateurs. Tout est derrière Yao, sans collision.</summary>
        static void BuildToll(Transform world)
        {
            var toll = Zone(world, 1f, TollCenter);
            var group = new GameObject("Peage_HKB").transform;
            group.SetParent(toll, false);
            var canopy = Place(group, "Peage_Auvent", TollCenter, BackOfStreet, CanopyHeight, 0);
            float cw = canopy.sprite.bounds.size.x * canopy.transform.localScale.x;
            float P(int i) => TollCenter - cw / 2f + CanopyPillars[i] * cw;

            string[] booths = { "Peage_Cabine_1", "Peage_Cabine_2", "Peage_Cabine_3" };
            int[] boothPillars = { 1, 3, 5 };
            for (int k = 0; k < 3; k++)
            {
                float px = P(boothPillars[k]);
                Place(group, booths[k], px, BackOfStreet, 2.8f, 2);
                // Boîtier de barrière à droite de la cabine, sa lisse levée (verticale) : la voie est ouverte.
                Place(group, "Peage_Boitier", px + 1.9f, BackOfStreet, 1.4f, 3);
                // Lisse levée, pied caché derrière le moyeu du boîtier (au milieu, à 72 % de sa hauteur), même échelle que lui.
                Place(group, "Peage_LisseLevee", px + 1.9f, BackOfStreet + 0.9f, 492f * 1.4f / 395f, 2);
            }
            Place(group, "Peage_Feu", P(2) + 0.6f, BackOfStreet, 1.7f, 3);
            Place(group, "Peage_Feu", P(4) + 0.6f, BackOfStreet, 1.7f, 3);
            Place(group, "Peage_Borne", P(0) + 0.9f, BackOfStreet, 1.5f, 3);
            Place(group, "Peage_Separateur", P(6), BackOfStreet, 0.45f, 3);
            Place(group, "Peage_Separateur", P(2) - 1.2f, BackOfStreet, 0.45f, 3);
        }

        // ---------------------------------------------------------------- gameplay

        // Arène du combat, sous l'auvent du péage : barrières en x, racketteur au départ.
        const float ArenaX0 = 302f, ArenaX1 = 334f, BossX = 327f, GoalX = 342f;
        const string Tagline = "Le Patron te poursuit ! File sur l'autoroute, passe Port-Bouët et Marcory, traverse le pont HKB et force le péage, tenu par son racketteur !";

        /// <summary>
        /// Le parcours, d'après le stage 2 du téléphone étiré sur 350 cases : blocs et rangées de pièces par quartier,
        /// escaliers de latérite et planches, taxis (on rebondit sur leur toit), mouettes et agames, quatre ravines où l'on tombe. Un seul point de contrôle, à mi-parcours (entrée de Marcory),
        /// avant le combat ; l'arrivée est après l'arène, qui reste fermée tant que le racketteur n'est pas vaincu.
        /// </summary>
        static void BuildCourse(Transform world)
        {
            var course = new GameObject("Parcours").transform;
            course.SetParent(world, false);
            Transform In(float x) => Zone(course, 1f, x);

            // Piles de latérite (x, hauteur) et planches (bord gauche, dessus, nombre).
            var stacks = new (float x, int h)[] { (58f, 1), (59f, 2), (60f, 3), (150f, 2), (151f, 3), (152f, 4), (222f, 1), (223f, 2) };
            int variant = 0;
            foreach (var st in stacks) CourseKit.Stack(In(st.x), st.x, st.h, variant++);
            var planks = new (float x, float y, int n)[]
            {
                (60.5f, 3f - CourseKit.BlockTopInset, 2),                   // au bout de l'escalier de l'autoroute
                (127f, 2.6f, 1), (131.2f, 5f, 1), (135.4f, 2.6f, 1),        // Port-Bouët : pyramide de planches
                (206f, 3f, 2),                                              // Marcory
                (308f, 3f, 1), (319f, 3f, 1),                               // l'arène, pour sauter sur le racketteur
            };
            foreach (var p in planks) CourseKit.Plank(In(p.x), p.x, p.y, p.n);

            var blocks = new (BumpBlock.Kind kind, float x)[]
            {
                (BumpBlock.Kind.CoinBox, 10f),
                (BumpBlock.Kind.Brick, 20f), (BumpBlock.Kind.CoinBox, 21f), (BumpBlock.Kind.PowerBox, 22f), (BumpBlock.Kind.Brick, 23f),
                (BumpBlock.Kind.CoinBox, 87f),
                (BumpBlock.Kind.CoinBox, 100f), (BumpBlock.Kind.Brick, 101f), (BumpBlock.Kind.CoinSack, 102f), (BumpBlock.Kind.Brick, 103f),
                (BumpBlock.Kind.CoinBox, 186f), (BumpBlock.Kind.Brick, 187f), (BumpBlock.Kind.Brick, 188f), (BumpBlock.Kind.PowerBox, 189f),
                (BumpBlock.Kind.Brick, 190f), (BumpBlock.Kind.Brick, 191f),
                (BumpBlock.Kind.CoinSack, 244f),
                (BumpBlock.Kind.CoinBox, 296f), (BumpBlock.Kind.PowerBox, 297f), (BumpBlock.Kind.CoinBox, 298f), // dernier pouvoir avant le combat
            };
            foreach (var b in blocks) CourseKit.Block(In(b.x), b.kind, b.x);

            var items = new List<(Collectible.Kind kind, float x, float y)>();
            void Line(Collectible.Kind kind, float x0, float x1, float step, float y)
            {
                for (float x = x0; x <= x1 + 0.01f; x += step) items.Add((kind, x, y));
            }
            var C = Collectible.Kind.Coin;
            var B = Collectible.Kind.Banana;
            Line(C, 15f, 17f, 1f, 1.2f);
            items.Add((C, 21f, 5.6f));                       // au-dessus de la rangée de blocs
            Line(C, 43f, 45f, 1f, 3.6f);                     // au-dessus du premier taxi : on rebondit sur son toit
            Line(B, 61.5f, 64.5f, 1f, 4f);                   // sur la planche au bout de l'escalier
            Line(C, 82f, 84f, 1f, 1.2f);
            Line(C, 118f, 121f, 1f, 1.2f);
            Line(C, 128f, 129f, 1f, 3.6f);                   // la pyramide de planches
            Line(C, 132.2f, 134.2f, 1f, 6f);
            Line(C, 136.4f, 137.4f, 1f, 3.6f);
            items.Add((Collectible.Kind.Diamond, 133.2f, 8.2f)); // tout en haut de la pyramide
            items.Add((B, 150f, 3.2f)); items.Add((B, 151f, 4.2f)); items.Add((B, 152f, 5.2f));
            Line(C, 200f, 203f, 1f, 1.2f);
            Line(C, 207f, 210f, 1f, 4f);                     // sur la planche de Marcory
            Line(B, 225f, 228f, 1f, 3f);
            Line(C, 239f, 242f, 1f, 1.2f);
            Line(C, 254f, 284f, 3f, 1.2f);                   // le long du pont
            Line(B, 309f, 311f, 1f, 4.2f);                   // les planches de l'arène
            Line(B, 320f, 322f, 1f, 4.2f);
            foreach (var it in items) CourseKit.Item(In(it.x), it.kind, it.x, it.y);

            var E = new (Enemy.Kind kind, float x, float y)[]
            {
                // Danger total 26,5 (crabe 1, mouette 1,5, taxi 2 ; cible du téléphone pour le stage 2 sur 350 cases :
                // 23,8 ± 20 %), en montée : début calme, le plus dur à Marcory, pont calme avant le boss.
                // Autoroute (5) : un agame, le taxi d'apprentissage (pièces au-dessus), un agame sur la planche, un agame.
                // Les deux ravines de l'autoroute sont seules, sans mouette.
                (Enemy.Kind.Agama, 26f, 0f), (Enemy.Kind.Taxi, 46f, 0f),
                (Enemy.Kind.Agama, 63f, 3f - CourseKit.BlockTopInset), (Enemy.Kind.Agama, 86f, 0f),
                // Port-Bouët (7,5) : une mouette entre les planches de la pyramide.
                (Enemy.Kind.Taxi, 108f, 0f), (Enemy.Kind.Agama, 118f, 0f), (Enemy.Kind.Gull, 132.8f, 3.9f), (Enemy.Kind.Agama, 145f, 0f),
                (Enemy.Kind.Taxi, 164f, 0f),
                // Marcory (10,5), le passage le plus dur : deux taxis à la suite, une mouette au-dessus de la ravine.
                (Enemy.Kind.Agama, 183f, 0f), (Enemy.Kind.Taxi, 196f, 0f), (Enemy.Kind.Taxi, 202f, 0f), (Enemy.Kind.Agama, 208f, 3f),
                (Enemy.Kind.Taxi, 216f, 0f), (Enemy.Kind.Gull, 230.75f, 3.4f), (Enemy.Kind.Agama, 240f, 0f),
                // Le pont (3,5), plus calme : le boss est le sommet du stage.
                (Enemy.Kind.Taxi, 262f, 0f), (Enemy.Kind.Gull, 272f, 3.4f),
            };
            foreach (var e in E) CourseKit.EnemyAt(In(e.x), e.kind, e.x, e.y);

            // Un seul point de contrôle, à mi-parcours (case 175 sur 350) : l'entrée de Marcory.
            CourseKit.Flag(In(175f), 175f, 3.5f, false);
            CourseKit.Flag(In(GoalX), GoalX, 5.5f, true);

            // Le stage 2 dans le catalogue (carte) : sa scène, sa présentation et les totaux à ramasser.
            var catalog = MenuBuilder.Catalog();
            var stage = catalog.stages[1];
            stage.scene = System.IO.Path.GetFileNameWithoutExtension(ScenePath);
            stage.tagline = Tagline;
            stage.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SuperYao/UI/Backgrounds/thumb_hkb.jpg");
            stage.coins = items.Count(i => i.kind == C) + blocks.Count(k => k.kind == BumpBlock.Kind.CoinBox)
                          + blocks.Count(k => k.kind == BumpBlock.Kind.CoinSack) * 8;
            stage.bananas = items.Count(i => i.kind == B);
            stage.diamonds = items.Count(i => i.kind == Collectible.Kind.Diamond);
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Le racketteur du péage (planches du téléphone) et les deux barrières invisibles de l'arène.</summary>
        static void BuildBoss(Transform world)
        {
            var group = new GameObject("Combat_Racketteur").transform;
            group.SetParent(Zone(world, 1f, BossX), false);
            string dir = CourseKit.GameplayDir + "Boss/toll_";
            Sprite[] S(string n, int k) => CourseKit.LoadSprites($"{dir}{n}_", k);
            var idle = S("idle", 6);
            var sr = CourseKit.Renderer(group, "Racketteur", idle[0], 4, idle[0].rect.height * CourseKit.TollUnit);
            sr.transform.localPosition = new Vector3(BossX, 0f, 0f);
            float s = sr.transform.localScale.x;
            var box = sr.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.5f / s, 2.4f / s);
            box.offset = new Vector2(0f, 1.2f / s);
            sr.gameObject.AddComponent<Rigidbody2D>();
            var boss = sr.gameObject.AddComponent<TollBoss>();
            boss.idle = idle;
            boss.prep = S("prep", 2);
            boss.walk = S("walk", 7);
            boss.throwing = S("throw", 4);
            boss.charge = S("charge", 4);
            boss.impact = S("impact", 1);
            boss.stunned = S("stunned", 2);
            boss.defeat = S("defeat", 3);
            boss.ticket = S("ticket", 4);
            boss.arenaX0 = ArenaX0;
            boss.arenaX1 = ArenaX1;
            boss.maxHp = 6; // première apparition d'un boss sur le téléphone : 6 points de vie, soit 3 sauts
            var walls = new List<Collider2D>();
            foreach (var wx in new[] { ArenaX0 - 0.5f, ArenaX1 + 0.5f })
            {
                var w = new GameObject($"Barriere_Arene_x{wx:0.#}");
                w.transform.SetParent(group, false);
                var c = w.AddComponent<BoxCollider2D>();
                c.offset = new Vector2(wx, 10f);
                c.size = new Vector2(1f, 30f);
                c.sharedMaterial = frictionless;
                walls.Add(c);
            }
            boss.walls = walls.ToArray();
        }

        static void AddSceneToBuild()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Any(sc => sc.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void BuildForeground(Transform layer)
        {
            var plants = new (string prefab, float wx, float h, bool flip)[]
            {
                ("Plante_Herbes", 10f, 1.3f, false), ("Plante_HerbesSeches", 42f, 1.4f, false), ("Plante_Touffes", 72f, 1.2f, false),
                ("Plante_LargesFeuilles", 118f, 1.5f, true), ("Plante_Herbes", 160f, 1.3f, true), ("Plante_Touffes", 200f, 1.2f, true),
                ("Plante_HerbesSeches", 235f, 1.4f, false), ("Plante_Touffes", 298f, 1.2f, true), ("Plante_Herbes", 340f, 1.3f, false),
            };
            foreach (var p in plants) Place(Zone(layer, FFront, FFront * p.wx), p.prefab, FFront * p.wx, -2.1f, p.h, 0, p.flip, FrontTint, "AvantPlan");
        }

        static void BuildCamera(Transform yao)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = VisibleRows / 2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.42f, 0.76f, 0.96f);
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(12f, 5f, -10f);
            var follow = go.AddComponent<CameraFollow2D>();
            follow.target = yao;
            follow.visibleRows = VisibleRows;
            follow.levelMinX = 0f;
            follow.levelMaxX = LevelLength;
        }

        /// <summary>Réglages, commandes tactiles, état du stage, panneau d'affichage, pause et sons : comme le stage 1.</summary>
        static void BuildSettings()
        {
            var settings = new GameObject("Reglages_Stage");
            settings.AddComponent<StageSettings>();
            settings.AddComponent<TouchControls>().fireIcon = LoadSprite($"{GameplayDir}Items/fireball.png");
            settings.AddComponent<StageState>().LevelNumber = 2;
            var hud = settings.AddComponent<Hud>();
            hud.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/SuperYao/UI/Fonts/LilitaOne-Regular.ttf");
            hud.portrait = LoadSprite("Assets/SuperYao/UI/portrait_yao.png");
            hud.coin = LoadSprite($"{GameplayDir}Items/coin_0.png");
            hud.banana = LoadSprite($"{GameplayDir}Items/banana.png");
            hud.diamond = LoadSprite($"{GameplayDir}Items/diamond_0.png");
            var pause = settings.AddComponent<PauseScreen>();
            pause.font = hud.font;
            pause.hero = LoadSprite("Assets/SuperYao/UI/hero_yao.png");
            pause.leaf = LoadSprite($"{Art1}Plants/plant_07.png");
            pause.stoneGrey = LoadSprite("Assets/SuperYao/UI/stone_grey.png");
            pause.stoneEarth = LoadSprite("Assets/SuperYao/UI/stone_earth.png");
            pause.playIcon = LoadSprite("Assets/SuperYao/UI/Icons/play.png");
            pause.restartIcon = LoadSprite("Assets/SuperYao/UI/Icons/restart.png");
            pause.homeIcon = LoadSprite("Assets/SuperYao/UI/Icons/home.png");
            settings.AddComponent<AudioSource>();
            var sfx = settings.AddComponent<SoundFx>();
            sfx.sounds = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/SuperYao/Audio" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => new SoundFx.Entry { name = System.IO.Path.GetFileNameWithoutExtension(p), clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p) })
                .ToArray();
        }

        // ---------------------------------------------------------------- outils

        static Transform Layer(Transform parent, string name, float factor, float verticalFactor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<ParallaxLayer>();
            p.factor = factor;
            p.verticalFactor = verticalFactor;
            return go.transform;
        }

        /// <summary>Groupe de zone d'une couche, d'après le X du monde où l'objet passe à l'écran.</summary>
        static Transform Zone(Transform layer, float factor, float localX)
        {
            float wx = factor > 0f ? localX / factor : 0f;
            int i = 0;
            while (i < ZoneEnds.Length - 1 && wx >= ZoneEnds[i]) i++;
            var t = layer.Find(ZoneNames[i]);
            if (t != null) return t;
            t = new GameObject(ZoneNames[i]).transform;
            t.SetParent(layer, false);
            return t;
        }

        /// <summary>Aligne des bâtiments de gauche à droite en x local : (numéro, écart avant, miroir).</summary>
        static float Row(Transform layer, float factor, float cursor, float scale, Color tint, string sorting, params (int b, float gap, bool flip)[] items)
        {
            lastRow.Clear();
            foreach (var it in items)
            {
                string name = $"Batiment_{it.b:00}";
                float h = BuildingHeights[it.b - 1] * scale;
                var size = prefabs[name].GetComponent<SpriteRenderer>().sprite.bounds.size;
                float w = size.x * h / size.y;
                cursor += it.gap;
                float x = cursor + w / 2f;
                Place(Zone(layer, factor, x), name, x, BuildingBase, h, 0, it.flip, tint, sorting);
                lastRow.Add((it.b, x));
                cursor += w;
            }
            return cursor;
        }

        /// <summary>Rive boisée en x local <paramref name="x"/> : deux buissons et un arbre cachent le bout d'une bande d'eau.</summary>
        static void Shore(Transform layer, float factor, float x, float scale, Color tint, string sorting)
        {
            var zone = Zone(layer, factor, x);
            Place(zone, "Arbre", x + 1.2f * scale, BuildingBase, 7f * scale, 1, false, tint, sorting);
            Place(zone, "Plante_Buisson", x, BuildingBase - 0.2f * scale, 3f * scale, 2, false, tint, sorting);
            Place(zone, "Plante_BuissonJaune", x + 2.6f * scale, BuildingBase - 0.2f * scale, 2.5f * scale, 2, true, tint, sorting);
        }

        static SpriteRenderer Place(Transform parent, string prefab, float x, float baseY, float height,
            int order = 0, bool flip = false, Color? tint = null, string sortingLayer = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab], parent);
            var sr = go.GetComponent<SpriteRenderer>();
            float s = height / sr.sprite.bounds.size.y;
            go.transform.localPosition = new Vector3(x, baseY, 0f);
            go.transform.localScale = new Vector3(s, s, 1f);
            sr.flipX = flip;
            sr.sortingOrder = order;
            if (tint.HasValue) sr.color = tint.Value;
            if (sortingLayer != null) sr.sortingLayerName = sortingLayer;
            return sr;
        }

        /// <summary>Bande répétée sans couture de <paramref name="x0"/> à <paramref name="x1"/>, pied en
        /// <paramref name="baseY"/>, haute de <paramref name="height"/> (préfabriqué en mode répété).</summary>
        static SpriteRenderer Band(Transform parent, string prefab, float x0, float x1, float baseY, float height, int order,
            Color? tint = null, string name = null, string sortingLayer = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab], parent);
            var sr = go.GetComponent<SpriteRenderer>();
            var size = sr.sprite.bounds.size;
            float s = height / size.y;
            sr.size = new Vector2((x1 - x0) / s, size.y);
            sr.sortingOrder = order;
            if (tint.HasValue) sr.color = tint.Value;
            if (sortingLayer != null) sr.sortingLayerName = sortingLayer;
            go.transform.localScale = new Vector3(s, s, 1f);
            go.transform.localPosition = new Vector3((x0 + x1) / 2f, baseY, 0f);
            go.name = name ?? prefab;
            return sr;
        }

        static void GroundSpan(Transform parent, string prefab, float x0, float x1, float top)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab], parent);
            var sr = go.GetComponent<SpriteRenderer>();
            var size = sr.sprite.bounds.size;
            sr.size = new Vector2(x1 - x0, size.y);
            go.transform.localPosition = new Vector3((x0 + x1) / 2f, top - size.y, 0f);
            go.name = $"{prefab}_x{x0:0}-{x1:0}";
        }

        static void SolidBox(Transform parent, string name, float x0, float x1, float depth = 3f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = new Vector2((x0 + x1) / 2f, -depth / 2f);
            box.size = new Vector2(x1 - x0, depth);
            box.sharedMaterial = frictionless;
        }

        static void Wall(Transform parent, string name, float x)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = new Vector2(x, 10f);
            box.size = new Vector2(1f, 30f);
            box.sharedMaterial = frictionless;
        }

        static (float min, float max) LocalRange(float factor)
        {
            float camMin = HalfViewMin, camMax = LevelLength - HalfViewMin;
            return (factor * camMin - HalfViewMax - 1f, factor * camMax + HalfViewMax + 1f);
        }

        static Sprite LoadSprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogWarning($"Super Yao : image introuvable ({path}).");
            return s;
        }

        /// <summary>
        /// Poses de Yao en skateboard (planche 8 × 4 de l'utilisateur, Heroes/YaoSkate) : repos sur la planche, poussée
        /// (marche : rangée 1 ; course : rangée 4), ollie pour le saut (rangée 3), accroupi. Le coup de poing, la
        /// blessure et la mort reprennent les poses de Yao à pied. Yao mesure 175 px debout sur cette planche, sans le
        /// skate : il garde la même taille, la planche à roulettes en plus sous ses pieds.
        /// </summary>
        /// <summary>Yao à pied ; le pouvoir des coffres (à la place de la noix de karité) lui donne le skate, qu'il perd
        /// s'il est touché (stages 1, 2 et 3).</summary>
        internal static void PutOnSkate(GameObject yao)
        {
            var skate = SkateSet();
            if (skate == null) return;
            var animator = yao.GetComponentInChildren<HeroAnimator>();
            var walker = AssetDatabase.LoadAssetAtPath<HeroAnimationSet>("Assets/SuperYao/Heroes/Yao/Yao_Animations.asset");
            if (walker != null) animator.hero = walker;
            animator.skateSet = skate;
            var switcher = yao.GetComponent<HeroSwitcher>();
            if (switcher != null) switcher.heroes = new HeroAnimationSet[0]; // le skate dépend du pouvoir, plus des touches
        }

        static HeroAnimationSet SkateSet()
        {
            const string dir = "Assets/SuperYao/Heroes/YaoSkate";
            if (!AssetDatabase.IsValidFolder(dir)) { Debug.LogWarning("Super Yao : poses du skate introuvables (Heroes/YaoSkate)."); return null; }
            string path = dir + "/YaoSkate_Animations.asset";
            var set = AssetDatabase.LoadAssetAtPath<HeroAnimationSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<HeroAnimationSet>();
                AssetDatabase.CreateAsset(set, path);
            }
            var walker = AssetDatabase.LoadAssetAtPath<HeroAnimationSet>("Assets/SuperYao/Heroes/Yao/Yao_Animations.asset");
            set.heroName = "Yao (skate)";
            set.sourceHeight = 175f;
            set.idleFps = 2f;
            set.idle = Frames(dir + "/Idle");
            set.walk = Frames(dir + "/Walk");
            set.run = Frames(dir + "/Run");
            set.jump = Frames(dir + "/Jump");
            set.fall = Frames(dir + "/Fall");
            set.land = Frames(dir + "/Land");
            set.crouch = Frames(dir + "/Crouch");
            set.walkSourceHeight = set.runSourceHeight = 0f; // même échelle partout
            // Une poussée fait glisser plus loin qu'un pas : cycle plus long qu'à pied (3 et 3,75 cases).
            set.walkCycle = 4f;
            set.runCycle = 5.5f;
            // Saut : 2 accroupi (élan), 3 ollie (le nez du skate se lève), 4 en l'air, 5 redescente.
            set.jumpTakeoff = 2;
            set.jumpRise = 3;
            set.jumpApex = 4;
            set.jumpFall = 5;
            set.crouchFrame = 0;
            set.crouchSlideFrame = 0; // accroupi en roulant : la même pose, le skate glisse
            if (walker != null)
            {
                set.attack = walker.attack;
                set.hurt = walker.hurt;
                set.death = walker.death;
            }
            EditorUtility.SetDirty(set);
            return set;
        }

        static Sprite[] Frames(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new Sprite[0];
            return AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
        }

        // ---------------------------------------------------------------- préfabriqués

        static void CreatePrefabs()
        {
            // Repris du stage 1.
            SpritePrefab("Ciel", Art1 + "Sky/sky.jpg", "Ciel");
            for (int i = 1; i <= 3; i++) SpritePrefab($"Nuage_0{i}", Art1 + $"Clouds/cloud_0{i}.png", "Nuages");
            SpritePrefab("Lampadaire_A", Art1 + "Props/lamp_01.png", "Rue");
            SpritePrefab("Lampadaire_B", Art1 + "Props/lamp_03.png", "Rue");

            // Stage 2.
            SpritePrefab("HorizonPortBouet", Art + "Horizon/horizon_portbouet.png", "Horizon", SpriteDrawMode.Tiled);
            SpritePrefab("HorizonCocody", Art + "Horizon/horizon_cocody.png", "Horizon", SpriteDrawMode.Tiled);
            SpritePrefab("HorizonCocodyDebut", Art + "Horizon/horizon_cocody_start.png", "Horizon");
            SpritePrefab("Mer", Art + "Water/sea.jpg", "PlanIntermediaire", SpriteDrawMode.Tiled);
            SpritePrefab("SolAsphalte", Art + "Ground/ground_asphalt.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SolLaterite", Art + "Ground/ground_laterite.jpg", "Monde", SpriteDrawMode.Tiled);
            for (int i = 1; i <= 16; i++) SpritePrefab($"Batiment_{i:00}", Art + $"Buildings/building_{i:00}.png", "PlanIntermediaire");
            for (int i = 1; i <= 4; i++) SpritePrefab($"Cocotier_0{i}", Art + $"Vegetation/palm_0{i}.png", "Rue");
            SpritePrefab("Arbre", Art + "Vegetation/tree_01.png", "Rue");
            string[] plants = { "Herbes", "HerbesSeches", "Buisson", "BuissonJaune", "Branches", "Touffes", "LargesFeuilles", "BuissonRouge" };
            for (int i = 1; i <= plants.Length; i++) SpritePrefab($"Plante_{plants[i - 1]}", Art + $"Plants/plant_{i:00}.png", "Monde");
            string[] signs = { "Abidjan", "PortBouet", "Marcory", "80", "Virage", "Peage" };
            for (int i = 1; i <= signs.Length; i++) SpritePrefab($"Panneau_{signs[i - 1]}", Art + $"Signs/sign_{i:00}.png", "Monde");
            string[] road = { "Cone", "Cones", "Barriere", "SeparateurPlastique", "Balise", "BlocBeton" };
            for (int i = 1; i <= road.Length; i++) SpritePrefab($"Route_{road[i - 1]}", Art + $"Road/road_{i:00}.png", "Monde");
            SpritePrefab("Glissiere", Art + "Rails/rail_01.png", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("GlissiereDouble", Art + "Rails/rail_02.png", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("MuretBeton", Art + "Rails/rail_03.png", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("GlissiereEmbout", Art + "Rails/rail_04.png", "Monde");
            string[] market = { "TableFruits", "TableBassines", "ParasolCouleurs", "ParasolOrange", "Chariot" };
            for (int i = 1; i <= market.Length; i++) SpritePrefab($"Marche_{market[i - 1]}", Art + $"Market/stall_{i:00}.png", "Monde");
            SpritePrefab("Peage_Auvent", Art + "Toll/canopy.png", "Monde");
            for (int i = 1; i <= 3; i++) SpritePrefab($"Peage_Cabine_{i}", Art + $"Toll/booth_0{i}.png", "Monde");
            SpritePrefab("Peage_Boitier", Art + "Toll/gear_01.png", "Monde");
            SpritePrefab("Peage_LisseBaissee", Art + "Toll/gear_02.png", "Monde");
            SpritePrefab("Peage_LisseLevee", Art + "Toll/gear_03.png", "Monde");
            SpritePrefab("Peage_Borne", Art + "Toll/gear_04.png", "Monde");
            SpritePrefab("Peage_Feu", Art + "Toll/gear_05.png", "Monde");
            SpritePrefab("Peage_PanneauVert", Art + "Toll/gear_06.png", "Monde");
            SpritePrefab("Peage_PanneauRouge", Art + "Toll/gear_07.png", "Monde");
            SpritePrefab("Peage_Separateur", Art + "Toll/gear_08.png", "Monde");
            SpritePrefab("PontHKB", Art + "Bridge/bridge_hkb.png", "Monde");
            SpritePrefab("PontHKBLointain", Art + "Bridge/bridge_hkb_far.png", "BatimentsLointains");
        }

        static void SpritePrefab(string name, string spritePath, string sortingLayer, SpriteDrawMode mode = SpriteDrawMode.Simple)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            // Image absente (supprimée par le nettoyage car jamais posée dans un stage) : on l'ignore.
            if (sprite == null) { Debug.LogWarning($"Super Yao : image absente, ignorée ({spritePath})."); return; }
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = spriteMaterial;
            sr.sortingLayerName = sortingLayer;
            sr.drawMode = mode;
            if (mode == SpriteDrawMode.Tiled)
            {
                sr.tileMode = SpriteTileMode.Continuous;
                sr.size = sprite.bounds.size;
            }
            prefabs[name] = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
