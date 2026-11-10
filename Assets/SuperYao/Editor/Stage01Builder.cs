using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Construit la scène du stage 1 (quartier France de Grand-Bassam) à partir des images du dossier
    /// Backgrounds/Stage1 : couches de parallaxe, sol et collisions, caméra, Yao, préfabriqués et atlas.
    /// Menu : Super Yao > Construire le stage 1 (Grand-Bassam). On peut relancer après chaque retouche :
    /// la scène et les préfabriqués sont refaits à l'identique.
    ///
    /// Unités : une unité = une case du jeu ; Yao mesure environ 1,5 case. Le dessus du sol (collision) est en y = 0.
    /// Placement : dans une couche de facteur f, un objet en x local = f × X passe au centre de l'écran quand la
    /// caméra est en X (voir ParallaxLayer). Les rangées de bâtiments sont décrites directement en x local.
    /// </summary>
    public static class Stage01Builder
    {
        const string Art = "Assets/SuperYao/Backgrounds/Stage1/";
        const string PrefabDir = "Assets/SuperYao/Prefabs/Stage1";
        const string ScenePath = "Assets/SuperYao/Scenes/Stage01_GrandBassam.unity";
        const string AtlasPath = "Assets/SuperYao/Atlases/Stage1_Decor.spriteatlasv2";
        const string FrictionlessPath = "Assets/SuperYao/Physics/SansFrottement.physicsMaterial2D";
        const string HeroesDir = "Assets/SuperYao/Heroes/";
        const string GameplayDir = "Assets/SuperYao/Gameplay/"; // blocs, planches, pontons, pirogue (images du téléphone)
        const string HeroPrefabDir = "Assets/SuperYao/Prefabs/Heros";

        /// <summary>Réglages des poses de chaque héros : hauteur du héros debout sur ses planches de repos/saut/chute
        /// (en px), numéros des poses du saut (impulsion, montée, sommet, descente), pose accroupie, pose de glissade
        /// (-1 = aucune), cadence du repos. Planches 6 × 6 (garde, saut en 6 temps…) : la hauteur de référence donne
        /// à la tête (haut des cheveux ou de la casquette jusqu'au bas du bandeau ou de la casquette) la même taille
        /// que sur la planche de marche du héros.</summary>
        struct HeroRig
        {
            public string name;
            public float sourceHeight, idleFps;
            public int takeoff, rise, apex, fall, crouch, slide;
        }

        static readonly HeroRig[] Heroes =
        {
            // Yao seul (Lou et Moussa retirés le 2026-10-28) : planche unique de 7 rangées (toutes les poses du même
            // dessin, 184 px debout comme sa marche). Saut : 0 élan, 1 impulsion, 2 montée, 3 sommet genoux repliés,
            // 4 descente, 5 appui, 6 réception accroupie, 7 debout.
            new HeroRig { name = "Yao", sourceHeight = 184f, takeoff = 1, rise = 2, apex = 3, fall = 4, crouch = 0, slide = 1, idleFps = 4f },
        };
        static readonly List<HeroAnimationSet> heroSets = new List<HeroAnimationSet>();
        const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        /// <summary>Calques de tri, de l'arrière vers l'avant.</summary>
        public static readonly string[] SortingLayerNames =
            { "Ciel", "Nuages", "Horizon", "BatimentsLointains", "PlanIntermediaire", "Rue", "Monde", "Joueur", "AvantPlan" };

        // Vitesses apparentes des couches (voir ParallaxLayer) ; le monde jouable est à 1.
        const float FSky = 0f, FClouds = 0.05f, FHorizon = 0.15f, FFar = 0.35f, FMid = 0.65f, FStreet = 0.9f, FFront = 1.1f;

        const float GroundTop = 0.2f;      // dessus de l'image du sol : les pieds de Yao (y = 0) sont au milieu du trottoir
        const float BackOfStreet = 0.18f;  // pied des objets posés au fond du trottoir, derrière Yao
        // Pied des bâtiments (et des cocotiers), enfoncé de 0,2 case derrière le bord du trottoir (dessus en 0,2) : à 0,15,
        // les socles en biais des bâtiments 9 à 12 laissaient voir un jour dessous, ils semblaient flotter.
        const float BuildingBase = 0f;
        const float PavedRaise = 5f / SuperYaoSpriteImport.PixelsPerUnit; // le pavé commence 5 px plus haut que la terre
        const int DirtTiles1 = 3, PavedTiles = 6, DirtTiles2 = 7;          // terre | pavé (rue historique et place) | terre

        // Après le pont : la lagune (pontons de pêcheurs, îlot, deux traversées en pirogue), puis l'arrivée.
        // Positions en cases, comptées depuis la fin du pont. Sauts : environ 2,9 cases en marchant, 4,4 en courant.
        const float DockScale = 1.4f;                       // ponton (dock.png, 420 × 86 px) : 3,675 cases de long
        const float DockDeck = 0.81f;                       // plancher du ponton, en fraction de la hauteur de l'image
        const float PlankDeck = 0.91f;                      // dessus de la planche (plank.png)
        // Pirogue (87 × 39 px, cadre commun aux 3 images) : coque de 62 px de long (5 cases : le pêcheur a à peu près
        // la taille de Yao). Le héros pose les pieds dans la coque, à 28 % de la hauteur de l'image (le rebord est à
        // 40 % : posé dessus, il semblait flotter).
        // Ramenée de 5 à 4 cases : l'image du téléphone (87 px) agrandie à 5 cases paraissait floue sur Android.
        const float PirogueLength = 4f, PirogueHull = 62f / 87f, PirogueDeck = 0.28f; // bord de la pirogue, en fraction de la hauteur de l'image
        const float QuayEnd = 6f;                           // quai en terre au bout du pont
        const float DockA = 8.5f, Pirogue1End = 29.85f;     // ponton A (2 longueurs), traversée 1 jusqu'au ponton B
        const float Island0 = 36f, Island1 = 44f;           // l'îlot (point de reprise)
        const float Pirogue2End = 62f;                      // traversée 2 jusqu'au ponton C
        const float BeachOffset = 67.7f, BeachLength = 30f; // l'arrivée (terre en attendant la plage)
        // Eau de la lagune devant le héros (il s'y enfonce) : son haut s'efface en fondu (lagoon_front.png) pour ne pas
        // tracer de ligne nette sous les pontons.
        const float WaterTop = -0.1f, WaterBottom = -2.6f;

        // Pont de la Victoire (bridge.png, 444 px de haut) : dessus du tablier à 154 px du bas, rambarde de 44 px.
        // On règle sa taille par la rambarde : 0,75 case, la moitié de Yao.
        const float BridgeDeckFromBottom = 154f / 444f, BridgeRailing = 44f / 444f, BridgeRailingHeight = 0.75f;
        const float BridgeDeckThickness = 55f / 444f;
        const float BridgeOverlap = 0.8f; // les culées du pont recouvrent le bout du sol de chaque côté
        const float VisibleRows = 20f;                                     // hauteur visible, en cases (CameraFollow2D)
        const float HalfViewMin = VisibleRows / 2f * 4f / 3f, HalfViewMax = VisibleRows / 2f * 2.4f; // demi-largeur : tablette 4:3 … téléphone 21:9

        // Hauteurs des bâtiments au plan intermédiaire, en cases (Yao ≈ 1,5) : un étage fait environ 3 cases.
        // 9 : boutique PAUL à étages, 10 : la Maison des Arts (un haut rez-de-chaussée), 11 : maison coloniale à véranda
        // (deux niveaux), 12 : maison aux volets verts (trois niveaux).
        static readonly float[] BuildingHeights = { 5.5f, 6.5f, 6.5f, 8f, 7f, 7.5f, 7f, 7f, 7.5f, 5.5f, 7f, 8f };
        const float FarScale = 0.55f;
        static readonly Color FarTint = new Color(0.80f, 0.86f, 0.94f);   // brume du matin sur les bâtiments lointains
        static readonly Color HorizonTint = new Color(0.90f, 0.94f, 1f);
        static readonly Color FrontTint = new Color(0.72f, 0.78f, 0.72f); // avant-plan un peu sombre, pour la profondeur
        static readonly Color CableColor = new Color(0.12f, 0.10f, 0.09f);

        static readonly string[] FurnitureNames =
            { "Banc", "BancDossier", "Tabouret", "Jarre", "Pots", "BacBeton", "Caisse", "CaissesEmpilees", "Panier", "PanneauPetit", "EnseigneSuspendue", "PanneauGrand" };
        static readonly string[] PlantNames =
            { "BuissonRond", "Bananier", "Ixora", "Hibiscus", "MassifFleuri", "Bougainvillier", "Fougere", "Herbes", "Croton", "LargesFeuilles", "Aloes", "Pothos" };
        static readonly string[] ZoneNames =
            { "Z1_Entree_Quartier_France", "Z2_Rue_Historique", "Z3_Petite_Place", "Z4_Rue_Residentielle", "Z5_Sortie_Pont_Victoire",
              "Z6_Pont_Victoire", "Z7_Lagune_Pirogues", "Z8_Arrivee_Plage" };

        static Material spriteMaterial;
        static PhysicsMaterial2D frictionless;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly List<string> missing = new List<string>();
        static float tileWidth, levelLength, streetEnd, bridgeLeft, bridgeRight, landingStart, beachStart;
        static bool hasBridge;
        static float[] zoneEnds;

        [MenuItem("Super Yao/Construire le stage 1 (Grand-Bassam)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Super Yao", "Arrête d'abord le jeu (bouton Play), puis relance la construction du stage.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            prefabs.Clear();
            missing.Clear();

            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            if (spriteMaterial == null) { Debug.LogError($"Super Yao : matériau introuvable ({SpriteMaterialPath})."); return; }
            foreach (var dir in new[] { PrefabDir, HeroPrefabDir, "Assets/SuperYao/Scenes", "Assets/SuperYao/Atlases", "Assets/SuperYao/Physics" }) EnsureFolder(dir);
            EnsureSortingLayers();
            frictionless = Frictionless();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreatePrefabs();
            if (missing.Count > 0)
            {
                Debug.LogError("Super Yao : images manquantes, stage non construit :\n" + string.Join("\n", missing));
                return;
            }

            // Parcours : rue (terre, pavé, terre), pont de la Victoire au niveau de la rue, puis arrivée.
            tileWidth = prefabs["SolTerre"].GetComponent<SpriteRenderer>().sprite.bounds.size.x;
            streetEnd = tileWidth * (DirtTiles1 + PavedTiles + DirtTiles2);
            hasBridge = prefabs.ContainsKey("PontVictoire") && prefabs.ContainsKey("Lagune");
            if (hasBridge)
            {
                var size = prefabs["PontVictoire"].GetComponent<SpriteRenderer>().sprite.bounds.size;
                float bridgeWidth = size.x * BridgeHeight() / size.y;
                bridgeLeft = streetEnd - BridgeOverlap;
                bridgeRight = bridgeLeft + bridgeWidth;
                landingStart = bridgeRight - BridgeOverlap;
            }
            else bridgeLeft = bridgeRight = landingStart = streetEnd;
            beachStart = landingStart + BeachOffset;
            levelLength = beachStart + BeachLength;
            zoneEnds = new[] { tileWidth * DirtTiles1, 100f, tileWidth * (DirtTiles1 + PavedTiles), 190f, bridgeLeft, landingStart, beachStart, float.MaxValue };

            var decor = new GameObject("Decor").transform;
            BuildSky(Layer(decor, "01_Ciel", FSky, 0f));
            BuildClouds(Layer(decor, "02_Nuages", FClouds, 0f));
            BuildSkyLife(decor);
            BuildHorizon(Layer(decor, "03_Horizon_Vegetal", FHorizon, 1f));
            BuildFarBuildings(Layer(decor, "04_Batiments_Lointains", FFar, 1f));
            BuildMidground(Layer(decor, "05a_Batiments_Cocotiers", FMid, 1f));
            BuildStreet(Layer(decor, "05b_Poteaux_Cocotiers_Proches", FStreet, 1f));

            var world = new GameObject("06_Monde_Jouable").transform;
            BuildGround(world);
            if (hasBridge) BuildBridge(world);
            BuildCourse(world);
            BuildStreetProps(world);
            var yao = BuildHeroes(world);
            BuildForeground(Layer(new GameObject("07_Avant_Plan").transform, "Vegetation_Avant_Plan", FFront, 1f));

            BuildCamera(yao);
            var settings = new GameObject("Reglages_Stage");
            settings.AddComponent<StageSettings>();
            settings.AddComponent<TouchControls>().fireIcon = LoadSprite($"{GameplayDir}Items/fireball.png");
            settings.AddComponent<StageState>();
            // Panneau d'affichage, comme sur le téléphone : police Lilita One, médaillon de Yao, pièce, banane, diamant.
            var hud = settings.AddComponent<Hud>();
            hud.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/SuperYao/UI/Fonts/LilitaOne-Regular.ttf");
            hud.portrait = LoadSprite("Assets/SuperYao/UI/portrait_yao.png");
            hud.coin = LoadSprite($"{GameplayDir}Items/coin_0.png");
            hud.banana = LoadSprite($"{GameplayDir}Items/banana.png");
            hud.diamond = LoadSprite($"{GameplayDir}Items/diamond_0.png");
            if (hud.font == null) Debug.LogError("Super Yao : police Lilita One introuvable.");
            // Écran de pause, comme sur le téléphone.
            var pause = settings.AddComponent<PauseScreen>();
            pause.font = hud.font;
            pause.hero = LoadSprite("Assets/SuperYao/UI/hero_yao.png");
            pause.leaf = LoadSprite($"{Art}Plants/plant_07.png"); // fougère
            pause.stoneGrey = LoadSprite("Assets/SuperYao/UI/stone_grey.png");
            pause.stoneEarth = LoadSprite("Assets/SuperYao/UI/stone_earth.png");
            pause.playIcon = LoadSprite("Assets/SuperYao/UI/Icons/play.png");
            pause.restartIcon = LoadSprite("Assets/SuperYao/UI/Icons/restart.png");
            pause.homeIcon = LoadSprite("Assets/SuperYao/UI/Icons/home.png");
            // Bruitages : les 16 sons du téléphone, par leur nom.
            settings.AddComponent<AudioSource>();
            var sfx = settings.AddComponent<SoundFx>();
            sfx.sounds = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/SuperYao/Audio" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => new SoundFx.Entry { name = System.IO.Path.GetFileNameWithoutExtension(p), clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p) })
                .ToArray();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            CreateAtlas();
            AssetDatabase.SaveAssets();
            Debug.Log($"Super Yao : stage 1 construit ({levelLength:0.#} cases de long) dans {ScenePath}.");
        }

        // ---------------------------------------------------------------- couches

        static void BuildSky(Transform layer)
        {
            // Fixe à l'écran : la couche suit la caméra, l'image est étirée pour couvrir tous les formats d'écran.
            var sr = Place(layer, "Ciel", 0f, -7f, 14f);
            sr.gameObject.AddComponent<FitToCamera>();
        }

        static void BuildClouds(Transform layer)
        {
            // Hauteurs par rapport au centre de l'écran ; CloudDrift les fait dériver et les recycle.
            layer.gameObject.AddComponent<CloudDrift>();
            var clouds = new (int sprite, float x, float y, float h)[]
            {
                (3, -10f, 4.4f, 2.2f), (1, -2f, 5.3f, 1.2f), (2, 5f, 3.2f, 1.8f),
                (1, 12f, 2.3f, 1.0f), (3, 19f, 4.8f, 2.0f), (2, 27f, 2.7f, 1.5f),
            };
            foreach (var c in clouds) Place(layer, $"Nuage_0{c.sprite}", c.x, c.y, c.h);
        }

        /// <summary>Oiseaux et avions qui traversent le ciel de temps en temps (SkyTraffic) : tout est créé ici, caché,
        /// puis recyclé en jeu. Avions derrière les nuages, oiseaux devant.</summary>
        static void BuildSkyLife(Transform decor)
        {
            var birdFrames = Frames(Art + "SkyLife/Birds");
            var planes = new[] { "airliner", "bizjet", "cessna" }
                .Select(n => AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"SkyLife/Planes/plane_{n}.png")).Where(s => s != null).ToArray();
            if (birdFrames.Length == 0 && planes.Length == 0) return; // facultatif

            var go = new GameObject("02b_Ciel_Vivant");
            go.transform.SetParent(decor, false);
            var traffic = go.AddComponent<SkyTraffic>();
            traffic.birdFrames = birdFrames;
            traffic.planeSprites = planes;

            const float birdWidth = 0.6f; // un petit oiseau lointain, à côté de Yao (1,7 case de haut)
            var birds = new List<SpriteRenderer>();
            for (int i = 0; birdFrames.Length > 0 && i < 3; i++)
            {
                var sr = SkyRenderer(go.transform, $"Oiseau_{i + 1}", birdFrames[0], 5);
                float s = birdWidth * (1f - 0.12f * i) / birdFrames[0].bounds.size.x; // les suivants un peu plus loin
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

        static void BuildHorizon(Transform layer)
        {
            // Une seule bande répétée (l'image se raccorde bord à bord), assez longue pour tout le stage.
            const float height = 4.6f;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["Horizon"], layer);
            var sr = go.GetComponent<SpriteRenderer>();
            var size = sr.sprite.bounds.size;
            float s = height / size.y;
            var (min, max) = LocalRange(FHorizon);
            int tiles = Mathf.CeilToInt((max - min) / (size.x * s));
            sr.size = new Vector2(size.x * tiles, size.y);
            sr.color = HorizonTint;
            go.transform.localScale = new Vector3(s, s, 1f);
            go.transform.localPosition = new Vector3((min + max) / 2f, BuildingBase + 0.1f, 0f);
        }

        static void BuildFarBuildings(Transform layer)
        {
            // Rangée lointaine, plus petite et voilée de brume ; dense dans la rue historique, ouverte vers la sortie.
            float x = -13f;
            x = Row(layer, FFar, x, FarScale, FarTint, "BatimentsLointains", (8, 0f, false), (1, 2f, false), (7, 3f, true), (2, 4f, false), (4, 1f, false));
            x = Row(layer, FFar, x, FarScale, FarTint, "BatimentsLointains", (5, 0.8f, false), (11, 0.3f, false), (3, 0.3f, false), (8, 0.3f, true), (4, 0.3f, false), (6, 0.3f, true));
            x = Row(layer, FFar, x, FarScale, FarTint, "BatimentsLointains", (7, 5f, false), (12, 0.5f, false), (8, 0.5f, false), (10, 0.5f, true), (5, 0.5f, true), (9, 0.5f, true), (1, 0.5f, false));
            Row(layer, FFar, x, FarScale, FarTint, "BatimentsLointains", (4, 4f, false), (11, 6f, true), (1, 4f, true));
        }

        static void BuildMidground(Transform layer)
        {
            var white = Color.white;
            // Z1 : entrée, la ruine à arcades et quelques maisons espacées (place pour les cocotiers).
            float x = Row(layer, FMid, -16f, 1f, white, "PlanIntermediaire", (7, 0f, false), (1, 3f, false), (8, 6f, false), (7, 4f, true));
            // Z2 : rue historique, façades coloniales serrées.
            // Les bâtiments 9 à 12, plus larges, remplacent des façades qui revenaient plusieurs fois : la rue historique
            // s'arrête toujours vers x = 103 (avant la place) et la dernière maison vers x = 198 (avant le pont).
            x = Row(layer, FMid, x, 1f, white, "PlanIntermediaire", (9, 5f, false), (3, 0.2f, false), (4, 0.2f, false), (10, 0.2f, false), (6, 0.2f, false));
            var balcony = lastRow[2]; // bâtiment à balcons de la rue historique : on y suspend l'enseigne
            // Z3 : petite place dégagée (aucun bâtiment, les lointains se voient). Z4 : rue résidentielle.
            x = Row(layer, FMid, x, 1f, white, "PlanIntermediaire", (2, 12f, false), (12, 1.5f, false), (1, 3f, true), (11, 2f, false));
            // Z5 : sortie, le bâti s'arrête au bord de la lagune, que Yao traverse ensuite par le pont (couche du monde).
            x = Row(layer, FMid, x, 1f, white, "PlanIntermediaire", (4, 6f, true));
            float waterStart = lastRow[0].x; // l'eau commence derrière le dernier bâtiment, qui cache son bord gauche
            bool lagoon = hasBridge;
            if (lagoon)
                // Le bas de l'eau descend sous le tablier du pont : aucun jour entre le pont et la lagune lointaine.
            {
                WaterBand(Zone(layer, FMid, waterStart + 10f), waterStart, LocalRange(FMid).max, -0.5f, 2.45f, "PlanIntermediaire", -2);
                LagoonTraffic(layer, waterStart, LocalRange(FMid).max);
            }
            else Row(layer, FMid, x, 1f, white, "PlanIntermediaire", (7, 12f, false));

            Place(Zone(layer, FMid, balcony.x), "Mobilier_EnseigneSuspendue", balcony.x + 0.6f, BuildingBase + 2.2f, 0.75f, 1, false, null, "PlanIntermediaire");

            // Cocotiers entre et derrière les bâtiments (ordre -1 : derrière, leurs palmes dépassent des toits).
            var palms = new (int sprite, float x, float h, bool flip, int order)[]
            {
                (1, -9f, 9f, false, -1), (3, 3.1f, 8.5f, false, -1), (6, 14.2f, 9f, false, 1), (1, 24f, 9.5f, true, 1),
                (5, 42.8f, 9.5f, false, -1),
                (6, 69f, 9.5f, false, 1), (2, 73.5f, 8.5f, false, 1), (1, 77.5f, 9f, true, 1),
                (3, 94.3f, 9f, false, -1), (5, 101f, 9.5f, false, -1), (2, 111.3f, 9f, true, 1),
                (1, 126f, 9.5f, false, 1), (6, 139f, 9.5f, true, 1), (3, 143f, 8.5f, false, 1), (5, 156f, 9f, false, 1),
            };
            foreach (var p in palms)
            {
                if (lagoon && p.x > waterStart) continue; // pas de cocotier planté dans la lagune
                Place(Zone(layer, FMid, p.x), $"Cocotier_0{p.sprite}", p.x, BuildingBase, p.h, p.order, p.flip, null, "PlanIntermediaire");
            }
        }

        /// <summary>Bateaux du téléphone sur la lagune lointaine (x local de la couche), recyclés d'un bout à l'autre.
        /// Les plus grands (bac) sont plus bas sur l'eau, donc plus près ; les voiliers plus haut, plus loin.</summary>
        static void LagoonTraffic(Transform layer, float fromX, float toX)
        {
            var go = new GameObject("Bateaux");
            go.transform.SetParent(layer, false);
            var lb = go.AddComponent<LagoonBoats>();
            lb.fromX = fromX;
            lb.toX = toX;
            Sprite S(string n) => LoadSprite($"{GameplayDir}Lagoon/{n}.png");
            float span = toX - fromX;
            // (images, largeur en cases, ligne de flottaison en y local, vitesse, regarde à droite, ordre)
            var list = new (Sprite[] frames, float width, float y, float speed, bool right, int order)[]
            {
                (new[] { S("ferry_0"), S("ferry_1"), S("ferry_2") }, 3.4f, 0.55f, 0.6f, true, -1),
                (new[] { S("pinasse_0") }, 2.6f, 1.05f, -0.45f, true, -1),
                (new[] { S("sail_0") }, 1.0f, 1.75f, 0.3f, true, -1),
                (new[] { S("sail_2") }, 0.9f, 1.95f, -0.25f, true, -1),
                (new[] { S("speedboat_0") }, 1.7f, 1.35f, -1.6f, false, -1),
            };
            var boats = new List<LagoonBoats.Boat>();
            for (int i = 0; i < list.Length; i++)
            {
                var d = list[i];
                var sr = Renderer(go.transform, $"Bateau_{i + 1}", d.frames[0], d.order, d.width, false);
                sr.sortingLayerName = "PlanIntermediaire";
                sr.transform.localPosition = new Vector3(fromX + span * (0.15f + 0.7f * i / list.Length), d.y, 0f);
                boats.Add(new LagoonBoats.Boat { renderer = sr, frames = d.frames, speed = d.speed, facesRight = d.right });
            }
            lb.boats = boats.ToArray();
        }

        static void BuildStreet(Transform layer)
        {
            // Positions en X du monde (converties en x local) : poteaux électriques reliés par des câbles,
            // lampadaires de la rue historique et de la place, cocotiers proches.
            var runA = new[] { 6f, 19f, 32f };
            var runB = new[] { 126f, 139f, 152f, 165f, 178f, 191f, 204f }; // s'arrête avant le pont
            foreach (var run in new[] { runA, runB })
            {
                var poles = new List<Transform>();
                foreach (var wx in run) poles.Add(Place(Zone(layer, FStreet, FStreet * wx), "PoteauElectrique", FStreet * wx, BuildingBase, 9f).transform);
                PowerLine(Zone(layer, FStreet, FStreet * run[0]), poles);
            }

            var lamps = new (float wx, bool lampOnRight)[] { (46f, false), (60f, true), (74f, false), (88f, true), (104f, false), (118f, true) };
            foreach (var l in lamps)
                Place(Zone(layer, FStreet, FStreet * l.wx), l.lampOnRight ? "Lampadaire_B" : "Lampadaire_A", FStreet * l.wx, BuildingBase, 8f);

            var palms = new (int sprite, float wx, float h, bool flip)[]
            {
                (4, 26f, 6f, false), (6, 98f, 10.5f, false), (2, 110f, 10f, true), (4, 146f, 6f, true),
                (3, 171f, 10.5f, false), (5, 198f, 10f, false),
                // L'îlot de la lagune et l'arrivée.
                (4, landingStart + (Island0 + Island1) / 2f, 6f, false),
                (1, beachStart + 4f, 10.5f, true), (6, beachStart + 21f, 10f, false),
            };
            foreach (var p in palms) Place(Zone(layer, FStreet, FStreet * p.wx), $"Cocotier_0{p.sprite}", FStreet * p.wx, BuildingBase, p.h, 1, p.flip);
        }

        static void BuildGround(Transform world)
        {
            var ground = new GameObject("Sol").transform;
            ground.SetParent(world, false);
            // Bandes répétées sans couture ; la bordure de pierres est au même niveau sur la terre et le pavé.
            GroundRun(ground, "SolTerre", 0f, DirtTiles1, GroundTop);
            GroundRun(ground, "SolPave", tileWidth * DirtTiles1, PavedTiles, GroundTop + PavedRaise);
            GroundRun(ground, "SolTerre", tileWidth * (DirtTiles1 + PavedTiles), DirtTiles2, GroundTop);
            // Après le pont : le quai, l'îlot de la lagune et l'arrivée.
            float quayEnd = landingStart + QuayEnd;
            GroundSpan(ground, "SolTerre", landingStart, quayEnd, GroundTop); // la culée du pont cache le raccord
            GroundSpan(ground, "SolTerre", landingStart + Island0, landingStart + Island1, GroundTop);
            GroundSpan(ground, "SolTerre", beachStart, levelLength, GroundTop);

            // Une seule boîte de collision pour la rue, le pont (son tablier est au niveau de la rue) et le quai :
            // aucune jointure où Yao pourrait accrocher. Puis l'îlot et l'arrivée, et deux murs aux bouts.
            SolidBox(ground, "Collision_Rue_Pont_Quai", 0f, quayEnd);
            SolidBox(ground, "Collision_Ilot", landingStart + Island0, landingStart + Island1);
            SolidBox(ground, "Collision_Arrivee", beachStart, levelLength);
            Wall(ground, "Mur_Debut", -0.5f);
            Wall(ground, "Mur_Fin", levelLength + 0.5f);
        }

        /// <summary>
        /// Le parcours jouable : dans la ville, des caisses et des blocs de latérite à franchir, des planches où l'on
        /// saute (traversables par en dessous) ; après le pont, la lagune avec ses pontons, son îlot et deux traversées
        /// en pirogue ; à l'arrivée, le grand escalier final. Seuls ces éléments ont des collisions.
        /// </summary>
        static void BuildCourse(Transform world)
        {
            var course = new GameObject("Parcours").transform;
            course.SetParent(world, false);
            Transform In(float x) => Zone(course, 1f, x);

            // Ville : piles de latérite (pierre pleine, comme sur le téléphone ; les caisses sont des briques cassables),
            // (x, hauteur en blocs).
            var stacks = new (float x, int h)[]
            {
                (28f, 1), (29f, 2),                       // Z1 : premiers sauts
                (52f, 1), (53f, 2), (54f, 3),             // Z2 : escalier vers la planche
                (72f, 2), (93f, 1), (94f, 1),
                (132f, 1), (133f, 2),                     // Z4
                (150f, 1), (151f, 2), (152f, 1),
                (182f, 2),
                (198f, 1), (199f, 2), (200f, 1),          // Z5, avant le pont
            };
            int variant = 0;
            foreach (var s in stacks) Stack(In(s.x), "Laterite", s.x, s.h, variant++);

            // Planches (x du bord gauche, hauteur du dessus, nombre de planches bout à bout).
            var planks = new (float x, float y, int n)[]
            {
                // Z2 : au bout de l'escalier de latérite, au niveau exact du dessus des blocs (leur collision est enfoncée
                // de BlockTopInset) : 0,12 case plus haut, le héros heurtait le côté de la planche, la traversait et tombait.
                (54.5f, 3f - BlockTopInset, 2),
                (104f, 2.6f, 1), (108.2f, 5f, 1), (112.4f, 2.6f, 1),   // Z3 : la place, une petite pyramide de planches
                (164f, 3f, 2),                     // Z4
                (landingStart + Pirogue1End + 0.5f, 2.6f, 1), // lagune : au-dessus du ponton B
            };
            foreach (var p in planks) Plank(In(p.x), p.x, p.y, p.n);

            // Lagune : pontons, traversées en pirogue, et l'eau devant le héros entre le quai et l'arrivée.
            float u = landingStart, dockLen = DockLength();
            float dockAEnd = u + DockA + 2 * dockLen;
            Dock(In(u + DockA), u + DockA, 2);
            Pirogue(In(dockAEnd + 1f), dockAEnd, u + Pirogue1End, 2.4f);
            Dock(In(u + Pirogue1End), u + Pirogue1End, 1);
            Pirogue(In(u + Island1 + 1f), u + Island1, u + Pirogue2End, 3f);
            Dock(In(u + Pirogue2End), u + Pirogue2End, 1);
            WaterBand(In(u + QuayEnd), u + QuayEnd, beachStart, WaterBottom, WaterTop, "AvantPlan", -5, "LaguneAvant");
            // Un poisson saute de temps en temps hors de l'eau (derrière l'eau de devant : il en sort et y replonge).
            var fish = Renderer(course, "Poisson", LoadSprite($"{GameplayDir}Lagoon/fish.png"), -6, 0.9f, false);
            fish.sortingLayerName = "AvantPlan";
            var jump = fish.gameObject.AddComponent<FishJump>();
            jump.fromX = u + QuayEnd;
            jump.toX = beachStart;
            jump.surfaceY = WaterTop;

            // Arrivée : le grand escalier final (6 marches de latérite), puis le replat de l'arrivée.
            for (int i = 0; i < 6; i++) Stack(In(beachStart + 12f + i), "Laterite", beachStart + 12f + i, i + 1, i);

            BuildItems(course, u);
            BuildEnemies(course, u);
            // Un seul point de contrôle (cocotier au drapeau ivoirien), à mi-parcours (case 178 sur 350), dans la rue
            // résidentielle, entre le coffre de la case 176 et la pile de latérite de la case 182 ;
            // puis le grand drapeau de l'arrivée après l'escalier final.
            var flag = LoadSprite($"{GameplayDir}Flags/flagPalm.png");
            Flag(In(178f), flag, 178f, 3.5f, false);
            Flag(In(beachStart + 26f), flag, beachStart + 26f, 5.5f, true);
        }

        static void Flag(Transform parent, Sprite sprite, float x, float height, bool goal)
        {
            var sr = Renderer(parent, goal ? "Arrivee" : $"Point_De_Controle_x{x:0.#}", sprite, 1, height);
            sr.transform.localPosition = new Vector3(x, 0f, 0f);
            var cp = sr.gameObject.AddComponent<Checkpoint>();
            cp.isGoal = goal;
            cp.respawnX = x;
        }

        /// <summary>Ennemis du stage 1, comme sur le téléphone : peu nombreux et bien espacés, pour apprendre à sauter
        /// dessus. Tortues (l'« escargot » du téléphone) et crabes au sol, mouettes en vol.</summary>
        static void BuildEnemies(Transform course, float u)
        {
            Transform In(float x) => Zone(course, 1f, x);
            float b = beachStart;
            var list = new (Enemy.Kind kind, float x, float y)[]
            {
                (Enemy.Kind.Turtle, 25f, 0f), (Enemy.Kind.Crab, 37f, 0f),
                (Enemy.Kind.Turtle, 62f, 0f), (Enemy.Kind.Gull, 80f, 3f), (Enemy.Kind.Crab, 98f, 0f),
                (Enemy.Kind.Turtle, 118f, 0f), (Enemy.Kind.Crab, 145f, 0f), (Enemy.Kind.Gull, 160f, 3f),
                (Enemy.Kind.Crab, 172f, 0f), (Enemy.Kind.Turtle, 188f, 0f),
                (Enemy.Kind.Crab, u + 40f, 0f),                        // sur l'îlot
                (Enemy.Kind.Gull, u + 53f, 3.4f),                      // au-dessus de la traversée en pirogue 2
                (Enemy.Kind.Crab, b + 9f, 0f), // plus de tortue sur la ligne d'arrivée
            };
            foreach (var e in list) EnemyAt(In(e.x), e.kind, e.x, e.y);
        }

        static void EnemyAt(Transform parent, Enemy.Kind kind, float x, float y)
        {
            string art = kind == Enemy.Kind.Crab ? "crab" : kind == Enemy.Kind.Gull ? "gull" : "turtle";
            int count = kind == Enemy.Kind.Turtle ? 4 : 3;
            var frames = LoadSprites($"{GameplayDir}Enemies/{art}_", count);
            float height = kind == Enemy.Kind.Gull ? 1.1f : 0.95f;
            var sr = Renderer(parent, $"Ennemi_{kind}_x{x:0.#}", frames[0], 4, height);
            sr.transform.localPosition = new Vector3(x, y, 0f);
            float s = sr.transform.localScale.x;
            var box = sr.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f / s, 0.75f / s);
            box.offset = new Vector2(0f, 0.375f / s);
            sr.gameObject.AddComponent<Rigidbody2D>();
            var enemy = sr.gameObject.AddComponent<Enemy>();
            enemy.kind = kind;
            // Tortue : 2 images de marche, puis rentrée dans sa carapace quand on l'écrase.
            enemy.walkFrames = kind == Enemy.Kind.Turtle ? new[] { frames[0], frames[1] } : frames;
            enemy.squashedSprite = kind == Enemy.Kind.Turtle ? frames[3] : null;
            enemy.facesRight = kind != Enemy.Kind.Turtle;
            enemy.speed = kind == Enemy.Kind.Crab ? 0.38f * 3.75f : kind == Enemy.Kind.Turtle ? 0.3f * 3.75f : 0.75f * 3.75f;
        }

        // Blocs en l'air : bas du bloc à 2,6 cases (Yao petit, 1,4 case, doit sauter un peu pour les frapper).
        const float BlockY = 2.6f;
        // Les blocs (latérite, caisses, coffres, sacs) sont dessinés un peu vus de dessus : la surface où les pieds
        // semblent se poser est sous le bord du dessin. Leur collision s'arrête donc 0,12 case plus bas, comme le sol
        // (dessin 0,2 case au-dessus de la collision) ; sinon Yao semblait flotter sur les blocs.
        const float BlockTopInset = 0.12f;

        /// <summary>Blocs à frapper et objets à ramasser, repris de l'esprit du stage 1 du téléphone : quelques
        /// coffres et caisses par quartier, des rangées de pièces et de bananes qui montrent le chemin (au-dessus des
        /// sauts, des planches, des traversées en pirogue), deux noix de karité, et le diamant au sommet du grand
        /// escalier final.</summary>
        static void BuildItems(Transform course, float u)
        {
            Transform In(float x) => Zone(course, 1f, x);
            float b = beachStart;
            var blocks = new (BumpBlock.Kind kind, float x)[]
            {
                (BumpBlock.Kind.CoinBox, 9f),
                (BumpBlock.Kind.Brick, 18f), (BumpBlock.Kind.CoinBox, 19f), (BumpBlock.Kind.Brick, 20f), (BumpBlock.Kind.PowerBox, 21f), (BumpBlock.Kind.Brick, 22f),
                (BumpBlock.Kind.CoinBox, 45f), (BumpBlock.Kind.Brick, 46f), (BumpBlock.Kind.CoinSack, 47f),
                (BumpBlock.Kind.CoinBox, 86f), (BumpBlock.Kind.Brick, 87f), (BumpBlock.Kind.CoinBox, 88f),
                (BumpBlock.Kind.CoinBox, 125f),
                (BumpBlock.Kind.Brick, 140f), (BumpBlock.Kind.Brick, 141f), (BumpBlock.Kind.CoinSack, 142f), (BumpBlock.Kind.Brick, 143f),
                (BumpBlock.Kind.CoinBox, 176f),
                (BumpBlock.Kind.CoinBox, u + 38.5f), (BumpBlock.Kind.PowerBox, u + 40f), (BumpBlock.Kind.Brick, u + 41.5f), // l'îlot
                (BumpBlock.Kind.CoinBox, b + 5f), (BumpBlock.Kind.Brick, b + 6f), (BumpBlock.Kind.CoinSack, b + 7f),
            };
            foreach (var k in blocks) Block(In(k.x), k.kind, k.x, BlockY);

            var items = new List<(Collectible.Kind kind, float x, float y)>();
            void Line(Collectible.Kind kind, float x0, float x1, float step, float y)
            {
                for (float x = x0; x <= x1 + 0.01f; x += step) items.Add((kind, x, y));
            }
            var C = Collectible.Kind.Coin;
            var B = Collectible.Kind.Banana;
            items.Add((C, 20f, 5.6f));                       // au-dessus de la rangée de blocs : il faut y monter
            Line(C, 13f, 15f, 1f, 1.2f);
            Line(C, 31f, 33f, 1f, 1.2f);
            Line(B, 55.5f, 58.5f, 1f, 4f);                  // sur la planche au bout de l'escalier
            items.Add((C, 72f, 3.2f));
            Line(C, 76f, 78f, 1f, 1.2f);
            Line(C, 105f, 106f, 1f, 3.6f);                  // la place : pyramide de planches
            Line(C, 109.2f, 111.2f, 1f, 6f);
            Line(C, 113.4f, 114.4f, 1f, 3.6f);
            items.Add((B, 150f, 2.5f)); items.Add((B, 151f, 3.5f)); items.Add((B, 152f, 2.5f));
            Line(C, 165f, 168f, 1f, 4f);
            items.Add((C, 182f, 3.2f));
            Line(C, streetEnd + 8f, streetEnd + 26f, 3f, 1.2f); // le long du pont
            items.Add((C, u + 7.25f, 2.2f));                // saut du quai au ponton
            Line(C, u + 18f, u + 27f, 1.5f, 2.4f);          // traversée en pirogue 1
            Line(B, u + 30.9f, u + 32.9f, 1f, 3.6f);        // planche du ponton B
            Line(C, u + 47f, u + 59f, 2f, 2.6f);            // traversée en pirogue 2
            items.Add((C, u + 66.7f, 2.2f));                // saut vers l'arrivée
            Line(B, b + 20f, b + 24f, 1f, 1.2f);
            items.Add((Collectible.Kind.Diamond, b + 17f, 7.8f)); // au sommet du grand escalier
            foreach (var it in items) Item(In(it.x), it.kind, it.x, it.y);

            // Totaux à ramasser, affichés sur la carte (pièces libres, coffres à pièce, sacs de 8 pièces).
            var stage = MenuBuilder.Catalog().stages[0];
            stage.coins = items.Count(i => i.kind == C)
                          + blocks.Count(k => k.kind == BumpBlock.Kind.CoinBox)
                          + blocks.Count(k => k.kind == BumpBlock.Kind.CoinSack) * 8;
            stage.bananas = items.Count(i => i.kind == B);
            stage.diamonds = items.Count(i => i.kind == Collectible.Kind.Diamond);
            EditorUtility.SetDirty(MenuBuilder.Catalog());
        }

        static Sprite LoadSprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogError("Super Yao : image manquante " + path);
            return s;
        }

        static Sprite[] LoadSprites(string pathPrefix, int count) =>
            Enumerable.Range(0, count).Select(i => LoadSprite($"{pathPrefix}{i}.png")).ToArray();

        static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, int order, float size, bool byHeight = true)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = sprite;
            sr.sharedMaterial = spriteMaterial;
            sr.sortingLayerName = "Monde";
            sr.sortingOrder = order;
            if (sprite != null)
            {
                float s = size / (byHeight ? sprite.bounds.size.y : sprite.bounds.size.x);
                sr.transform.localScale = new Vector3(s, s, 1f);
            }
            return sr;
        }

        /// <summary>Bloc d'une case (bas en y), frappable par en dessous.</summary>
        static void Block(Transform parent, BumpBlock.Kind kind, float x, float y)
        {
            string art = kind == BumpBlock.Kind.Brick ? "crate" : kind == BumpBlock.Kind.CoinSack ? "sack" : "chest";
            var go = new GameObject($"Bloc_{kind}_x{x:0.#}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            // Le dessus est enfoncé de BlockTopInset sous le haut du dessin (vu un peu de dessus) : les pieds y posent.
            box.size = new Vector2(1f, 1f - BlockTopInset);
            box.offset = new Vector2(0f, (1f - BlockTopInset) / 2f);
            box.sharedMaterial = frictionless;

            var block = go.AddComponent<BumpBlock>();
            block.kind = kind;
            var frames = LoadSprites($"{GameplayDir}Blocks/{art}_", 3);
            // Image (elle saute au coup de tête), étirée sur une case ; la pièce, la noix et les éclats sont ses voisins.
            var sr = Renderer(go.transform, "Image", frames[0], 3, 1f);
            sr.transform.localScale = new Vector3(1f / frames[0].bounds.size.x, 1f / frames[0].bounds.size.y, 1f);
            block.visual = sr;
            block.usedSprite = kind == BumpBlock.Kind.Brick ? null : frames[2];

            if (kind != BumpBlock.Kind.Brick && kind != BumpBlock.Kind.PowerBox)
            {
                block.coinFrames = LoadSprites($"{GameplayDir}Items/coin_", 4);
                block.coinPop = Renderer(go.transform, "Piece_Jaillit", block.coinFrames[0], 5, 0.8f);
                block.coinPop.enabled = false;
            }
            if (kind == BumpBlock.Kind.PowerBox)
            {
                var nut = Renderer(go.transform, "Skate", LoadSprite($"{GameplayDir}Items/skateboard.png"), 2, 1.3f, false);
                nut.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                var body = nut.gameObject.AddComponent<Rigidbody2D>();
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                var circle = nut.gameObject.AddComponent<CircleCollider2D>();
                float s = nut.transform.localScale.x;
                circle.radius = 0.4f / s;
                circle.offset = new Vector2(0f, 0.42f / s);
                circle.sharedMaterial = frictionless;
                block.power = nut.gameObject.AddComponent<PowerItem>();
                block.power.fireOrbSprite = LoadSprite($"{GameplayDir}Items/fire_orb.png");
            }
            if (kind == BumpBlock.Kind.Brick)
            {
                block.debris = new SpriteRenderer[4];
                for (int i = 0; i < 4; i++)
                {
                    var d = Renderer(go.transform, $"Eclat_{i}", frames[2], 5, 0.45f);
                    d.enabled = false;
                    block.debris[i] = d;
                }
            }
        }

        /// <summary>Pièce, banane ou diamant, centré en (x, y).</summary>
        static void Item(Transform parent, Collectible.Kind kind, float x, float y)
        {
            Sprite[] frames = kind == Collectible.Kind.Coin ? LoadSprites($"{GameplayDir}Items/coin_", 4)
                : kind == Collectible.Kind.Diamond ? LoadSprites($"{GameplayDir}Items/diamond_", 3)
                : new[] { LoadSprite($"{GameplayDir}Items/banana.png") };
            float size = kind == Collectible.Kind.Diamond ? 1.1f : 0.75f;
            var sr = Renderer(parent, $"{kind}_x{x:0.#}", frames[0], 4, size, kind != Collectible.Kind.Banana);
            // Les images ont leur pivot en bas : on remonte de la moitié pour centrer l'objet en y.
            sr.transform.localPosition = new Vector3(x, y - size / 2f, 0f);
            var circle = sr.gameObject.AddComponent<CircleCollider2D>();
            float s = sr.transform.localScale.x;
            circle.radius = 0.4f / s;
            circle.offset = new Vector2(0f, size / 2f / s);
            var item = sr.gameObject.AddComponent<Collectible>();
            item.kind = kind;
            item.frames = frames.Length > 1 ? frames : null;
            item.framesPerSecond = kind == Collectible.Kind.Diamond ? 6f : 8f;
            item.sparkleFrames = LoadSprites($"{GameplayDir}Items/sparkle_", 8);
            var sparkle = Renderer(sr.transform, "Etincelle", item.sparkleFrames[0], 6, 1.2f);
            sparkle.transform.localScale = new Vector3(sparkle.transform.localScale.x / s, sparkle.transform.localScale.y / s, 1f);
            sparkle.transform.localPosition = new Vector3(0f, size / 2f / s - 0.6f / s, 0f);
            sparkle.enabled = false;
            item.sparkle = sparkle;
        }

        /// <summary>Pile de blocs d'une case : une seule boîte de collision pour toute la pile.</summary>
        static void Stack(Transform parent, string kind, float x, int height, int variant)
        {
            var go = new GameObject($"{kind}_x{x:0.#}_h{height}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, 0f, 0f);
            for (int i = 0; i < height; i++)
            {
                var sr = Place(go.transform, $"{kind}_{(variant + i) % 3}", 0f, i, 1f, 3);
                sr.transform.localScale = new Vector3(1f / sr.sprite.bounds.size.x, 1f / sr.sprite.bounds.size.y, 1f);
            }
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, height - BlockTopInset);
            box.offset = new Vector2(0f, (height - BlockTopInset) / 2f);
            box.sharedMaterial = frictionless;
        }

        /// <summary>Planches bout à bout dont le dessus est à <paramref name="top"/> ; on peut sauter à travers par
        /// en dessous et se poser dessus.</summary>
        static void Plank(Transform parent, float x, float top, int count)
        {
            var go = new GameObject($"Planche_x{x:0.#}_y{top:0.#}");
            go.transform.SetParent(parent, false);
            const float h = 0.45f;
            float w = 0f;
            for (int i = 0; i < count; i++)
            {
                var sr = Place(go.transform, "Planche", 0f, top - PlankDeck * h, h, 3);
                float segment = sr.sprite.bounds.size.x * sr.transform.localScale.x;
                sr.transform.localPosition = new Vector3(x + segment * (i + 0.5f), top - PlankDeck * h, 0f);
                w = segment * count;
            }
            OneWay(go, x + w / 2f, top, w - 0.1f);
        }

        static float DockLength()
        {
            var size = prefabs["Ponton"].GetComponent<SpriteRenderer>().sprite.bounds.size;
            return size.x * DockScale;
        }

        /// <summary>Ponton de pêcheurs (plancher en y = 0), en <paramref name="count"/> longueurs à partir de x.</summary>
        static void Dock(Transform parent, float x, int count)
        {
            var go = new GameObject($"Ponton_x{x:0.#}");
            go.transform.SetParent(parent, false);
            var size = prefabs["Ponton"].GetComponent<SpriteRenderer>().sprite.bounds.size;
            float h = size.y * DockScale, len = DockLength();
            for (int i = 0; i < count; i++) Place(go.transform, "Ponton", x + len * (i + 0.5f), -DockDeck * h, h, 2);
            SolidBox(go.transform, "Collision", x, x + len * count, 0.4f);
        }

        /// <summary>Pirogue qui fait la navette sur la lagune entre deux pontons (bord à y = 0) ; le héros monte dessus
        /// et se laisse porter.</summary>
        static void Pirogue(Transform parent, float from, float to, float speed)
        {
            var go = new GameObject($"Pirogue_x{from:0.#}-{to:0.#}");
            go.transform.SetParent(parent, false);
            float half = PirogueLength / 2f + 0.05f;
            go.transform.localPosition = new Vector3(from + half, 0f, 0f);
            go.AddComponent<Rigidbody2D>();
            var moving = go.AddComponent<MovingPlatform>();
            moving.fromX = from + half;
            moving.toX = to - half;
            moving.speed = speed;
            moving.pause = 1.2f;

            var frames = Enumerable.Range(0, 3).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"{GameplayDir}Pirogue/pirogue_{i}.png")).Where(s => s != null).ToArray();
            var visual = new GameObject("Image").AddComponent<SpriteRenderer>();
            visual.transform.SetParent(go.transform, false);
            visual.sprite = frames[0];
            visual.sharedMaterial = spriteMaterial;
            visual.sortingLayerName = "Monde";
            visual.sortingOrder = 4;
            float s = PirogueLength / (frames[0].bounds.size.x * PirogueHull);
            visual.transform.localScale = new Vector3(s, s, 1f);
            visual.transform.localPosition = new Vector3(0f, -PirogueDeck * frames[0].bounds.size.y * s, 0f);
            moving.visual = visual;
            moving.frames = frames;
            OneWay(go, 0f, 0f, PirogueLength * 0.9f);
        }

        /// <summary>Surface où l'on se pose en venant du dessus, et qu'on traverse en sautant par en dessous
        /// (x et top dans le repère de l'objet).</summary>
        static void OneWay(GameObject go, float x, float top, float width)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(width, 0.25f);
            box.offset = new Vector2(x, top - 0.125f);
            box.sharedMaterial = frictionless;
            box.usedByEffector = true;
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
        }

        /// <summary>Boîte de collision pleine de <paramref name="x0"/> à <paramref name="x1"/>, dessus en y = 0.</summary>
        static void SolidBox(Transform parent, string name, float x0, float x1, float depth = 3f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = new Vector2((x0 + x1) / 2f, -depth / 2f);
            box.size = new Vector2(x1 - x0, depth);
            box.sharedMaterial = frictionless;
        }

        static void BuildStreetProps(Transform world)
        {
            var props = new GameObject("Mobilier_Et_Vegetation").transform;
            props.SetParent(world, false);
            // Au fond du trottoir, derrière Yao (calque Monde < Joueur), sans collision.
            var items = new (string prefab, float x, float h, bool flip)[]
            {
                // Z1 entrée
                ("Plante_Herbes", 2f, 0.9f, false), ("Plante_BuissonRond", 11f, 1.3f, false), ("Mobilier_PanneauGrand", 16f, 1.8f, false),
                ("Plante_Bougainvillier", 27f, 1.5f, false), ("Mobilier_Panier", 35f, 0.6f, false),
                // Z2 rue historique
                ("Mobilier_BancDossier", 48f, 1.0f, false), ("Mobilier_Jarre", 51f, 1.0f, false), ("Mobilier_Pots", 58f, 0.8f, false),
                ("Plante_Hibiscus", 62f, 1.5f, false), ("Mobilier_Banc", 70f, 0.8f, false), ("Mobilier_Caisse", 79f, 0.9f, false),
                ("Mobilier_CaissesEmpilees", 81f, 1.3f, false), ("Plante_LargesFeuilles", 88f, 1.4f, false), ("Mobilier_Jarre", 95f, 1.0f, true),
                // Z3 petite place
                ("Mobilier_BancDossier", 103f, 1.0f, true), ("Plante_Bougainvillier", 106f, 1.5f, true), ("Mobilier_BacBeton", 108.5f, 0.9f, false),
                ("Mobilier_Banc", 113f, 0.8f, true), ("Plante_Hibiscus", 116f, 1.5f, true), ("Mobilier_Tabouret", 119f, 0.6f, false),
                // Z4 rue résidentielle
                ("Plante_Croton", 126f, 1.3f, false), ("Mobilier_CaissesEmpilees", 133f, 1.3f, true), ("Mobilier_Panier", 136.5f, 0.6f, false),
                ("Plante_Ixora", 142f, 1.2f, false), ("Mobilier_Pots", 149f, 0.8f, true), ("Plante_Pothos", 156f, 1.0f, false),
                ("Mobilier_Tabouret", 161f, 0.6f, true), ("Plante_Fougere", 166f, 1.2f, false), ("Mobilier_Jarre", 172f, 1.0f, false),
                ("Plante_Bananier", 178f, 1.8f, false), ("Mobilier_Caisse", 185f, 0.9f, true),
                // Z5 sortie, jusqu'à l'entrée du pont
                ("Plante_Aloes", 194f, 1.0f, false), ("Plante_Herbes", 199f, 0.9f, true), ("Plante_Fougere", 204f, 1.2f, true),
                ("Mobilier_PanneauPetit", 209f, 1.6f, false), ("Plante_BuissonRond", 213f, 1.3f, true),
                // Z7 arrivée (provisoire, en attendant les images de la plage)
                ("Plante_Herbes", landingStart + 2f, 0.9f, false), ("Mobilier_Panier", landingStart + 4.5f, 0.6f, false),
                ("Plante_Aloes", landingStart + Island0 + 1.5f, 1.0f, true), ("Mobilier_Jarre", landingStart + Island1 - 1.5f, 1.0f, false),
                ("Plante_Herbes", beachStart + 2f, 0.9f, false), ("Mobilier_PanneauGrand", beachStart + 8f, 1.8f, false),
                ("Plante_Bougainvillier", beachStart + 24f, 1.5f, false),
            };
            foreach (var i in items) Place(Zone(props, 1f, i.x), i.prefab, i.x, BackOfStreet, i.h, 2, i.flip);

            // Transitions terre / pavé : un massif posé sur la jointure cache le changement de revêtement.
            Place(Zone(props, 1f, zoneEnds[0]), "Plante_MassifFleuri", tileWidth * DirtTiles1, -0.1f, 1.0f, 5).name = "Transition_Terre_Pave";
            Place(Zone(props, 1f, zoneEnds[2]), "Plante_BuissonRond", tileWidth * (DirtTiles1 + PavedTiles), -0.1f, 1.2f, 5).name = "Transition_Pave_Terre";
        }

        static void BuildForeground(Transform layer)
        {
            // Bas de l'écran, sous les pieds de Yao (le haut des plantes reste sous y = -0,5) : il n'est jamais masqué.
            var plants = new (string prefab, float wx, float h, bool flip)[]
            {
                ("Plante_Herbes", 9f, 1.3f, false), ("Plante_Fougere", 37f, 1.5f, false), ("Plante_Pothos", 66f, 1.2f, false),
                ("Plante_Croton", 140f, 1.4f, true), ("Plante_Fougere", 170f, 1.5f, true), ("Plante_Herbes", 200f, 1.3f, true),
                ("Plante_Aloes", 211f, 1.3f, false), ("Plante_Herbes", beachStart + 6f, 1.3f, false),
            };
            foreach (var p in plants) Place(Zone(layer, FFront, FFront * p.wx), p.prefab, FFront * p.wx, -2.1f, p.h, 0, p.flip, FrontTint, "AvantPlan");
        }

        /// <summary>Un préfabriqué par héros (mêmes déplacements, poses différentes) ; Yao est posé au départ.
        /// En jeu, les touches 1, 2, 3 changent de héros pour les essais.</summary>
        static Transform BuildHeroes(Transform world)
        {
            GameObject first = null;
            foreach (var set in heroSets)
            {
                // Racine : la physique (pieds au pivot). Enfant « Visuel » : l'image, mise à l'échelle par HeroAnimator.
                var go = new GameObject($"Heros_{set.heroName}");
                var body = go.AddComponent<Rigidbody2D>();
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                go.AddComponent<BoxCollider2D>().sharedMaterial = frictionless;
                go.AddComponent<HeroInput>();
                go.AddComponent<HeroController>();
                // Pouvoir du feu : deux boules préparées d'avance (image décalée pour centrer la boule, pas la traînée).
                var fireballSprite = LoadSprite($"{GameplayDir}Items/fireball.png");
                var heroFire = go.AddComponent<HeroFire>();
                heroFire.fireballs = new Fireball[2];
                for (int i = 0; i < 2; i++)
                {
                    var ball = new GameObject($"Boule_De_Feu_{i + 1}");
                    ball.transform.SetParent(go.transform, false);
                    var img = Renderer(ball.transform, "Image", fireballSprite, 6, 0.65f);
                    img.sortingLayerName = "Joueur";
                    var size = fireballSprite.bounds.size * img.transform.localScale.x;
                    // Image de 22 × 12 unités du téléphone, boule en x = 16/22 de la largeur, au milieu de la hauteur.
                    img.transform.localPosition = new Vector3(-(16f / 22f - 0.5f) * size.x, -size.y / 2f, 0f);
                    heroFire.fireballs[i] = ball.AddComponent<Fireball>();
                }
                go.AddComponent<HeroSwitcher>().heroes = heroSets.ToArray();

                var visual = new GameObject("Visuel");
                visual.transform.SetParent(go.transform, false);
                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sprite = set.FirstSprite;
                sr.sharedMaterial = spriteMaterial;
                sr.sortingLayerName = "Joueur";
                visual.AddComponent<HeroAnimator>().hero = set;

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{HeroPrefabDir}/Heros_{set.heroName}.prefab");
                Object.DestroyImmediate(go);
                if (first == null) first = prefab;
            }

            var hero = (GameObject)PrefabUtility.InstantiatePrefab(first, world);
            hero.transform.localPosition = new Vector3(4f, 0.02f, 0f);
            // Provisoire : chute dans la lagune -> retour au dernier point de reprise (départ, quai, îlot, arrivée).
            hero.AddComponent<HeroRespawn>(); // chute dans la lagune ; réapparition au dernier point de contrôle
            Stage02Builder.PutOnSkate(hero); // à pied ; sur le skate quand il en trouve un dans un coffre
            return hero.transform;
        }

        static HeroAnimationSet HeroSet(HeroRig rig)
        {
            string hero = rig.name;
            string dir = HeroesDir + hero;
            string path = $"{dir}/{hero}_Animations.asset";
            var set = AssetDatabase.LoadAssetAtPath<HeroAnimationSet>(path);
            if (set == null)
            {
                if (!AssetDatabase.IsValidFolder(dir)) { missing.Add(dir); return null; }
                set = ScriptableObject.CreateInstance<HeroAnimationSet>();
                AssetDatabase.CreateAsset(set, path);
            }
            set.heroName = hero;
            set.sourceHeight = rig.sourceHeight;
            set.idleFps = rig.idleFps;
            set.land = Frames(dir + "/Land");
            set.attack = Frames(dir + "/Attack");
            set.hurt = Frames(dir + "/Hurt");
            set.death = Frames(dir + "/Death");
            set.idle = Frames(dir + "/Idle");
            set.run = Frames(dir + "/Run");
            set.jump = Frames(dir + "/Jump");
            set.fall = Frames(dir + "/Fall");
            set.crouch = Frames(dir + "/Crouch");
            // Marche et course dessinées ensemble sur une planche plus grande : leur échelle se lit sur la marche
            // (hauteur de cellule = hauteur debout + 3 px de marge, voir le découpage). Cadence en cases par cycle,
            // pour 8 images : environ 16 images/s en marchant, 22 en courant.
            set.walk = Frames(dir + "/Walk");
            bool newCycle = set.walk.Length > 0;
            set.walkSourceHeight = set.runSourceHeight = newCycle ? set.walk[0].rect.height - 3f : 0f;
            set.walkCycle = newCycle ? 3f : 0f;
            set.runCycle = newCycle ? 3.75f : 0f;
            set.jumpTakeoff = rig.takeoff;
            set.jumpRise = rig.rise;
            set.jumpApex = rig.apex;
            set.jumpFall = rig.fall;
            set.crouchFrame = rig.crouch;
            set.crouchSlideFrame = rig.slide;
            EditorUtility.SetDirty(set);
            if (set.idle.Length == 0 || set.run.Length == 0 || set.jump.Length == 0) missing.Add(dir + " (Idle, Run, Jump)");
            return set;
        }

        /// <summary>Images d'un dossier, dans l'ordre de leur nom (hero_anim_00, _01…).</summary>
        static Sprite[] Frames(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new Sprite[0];
            return AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
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
            follow.levelMaxX = levelLength;
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

        /// <summary>Groupe de zone (Z1 … Z5) d'une couche, d'après le X du monde où l'objet passe à l'écran.</summary>
        static Transform Zone(Transform layer, float factor, float localX)
        {
            float wx = factor > 0f ? localX / factor : 0f;
            int i = 0;
            while (i < zoneEnds.Length - 1 && wx >= zoneEnds[i]) i++;
            var t = layer.Find(ZoneNames[i]);
            if (t != null) return t;
            t = new GameObject(ZoneNames[i]).transform;
            t.SetParent(layer, false);
            return t;
        }

        static readonly List<(int b, float x)> lastRow = new List<(int b, float x)>();

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
                // Les bâtiments 9 à 12 sont dessinés en perspective, posés sur un socle en biais : on enfonce ce socle
                // derrière le trottoir (5 % de la hauteur), sinon son bord avant semble flotter au-dessus de la rue.
                float sink = it.b >= 9 ? 0.05f * h : 0f;
                Place(Zone(layer, factor, x), name, x, BuildingBase - sink, h, 0, it.flip, tint, sorting);
                lastRow.Add((it.b, x));
                cursor += w;
            }
            return cursor;
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

        static void GroundRun(Transform parent, string prefab, float startX, int count, float top)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab], parent);
            var sr = go.GetComponent<SpriteRenderer>();
            var size = sr.sprite.bounds.size;
            sr.size = new Vector2(size.x * count, size.y);
            go.transform.localPosition = new Vector3(startX + size.x * count / 2f, top - size.y, 0f);
            go.name = $"{prefab}_x{startX:0}-{startX + size.x * count:0}";
        }

        /// <summary>Pont de la Victoire, jouable : tablier au niveau de la rue (y = 0), Yao passe devant la rambarde ;
        /// l'eau de la lagune se voit entre les piles.</summary>
        static void BuildBridge(Transform world)
        {
            var group = new GameObject("Pont_Victoire").transform;
            group.SetParent(world, false);
            float h = BridgeHeight();
            var bridge = Place(group, "PontVictoire", (bridgeLeft + bridgeRight) / 2f, -BridgeDeckFromBottom * h, h, 1);
            bridge.sortingLayerName = "Monde";
            // Eau sous le tablier, de culée à culée ; son haut est caché derrière le tablier.
            float deckBottom = -BridgeDeckThickness * h;
            WaterBand(group, bridgeLeft, bridgeRight, deckBottom - 2.2f, deckBottom + 0.15f, "Monde", -1);
        }

        static float BridgeHeight() => BridgeRailingHeight / BridgeRailing;

        /// <summary>Bande d'eau de la lagune répétée sans couture, de <paramref name="fromX"/> à <paramref name="toX"/>,
        /// entre les hauteurs <paramref name="bottom"/> et <paramref name="top"/>.</summary>
        static void WaterBand(Transform parent, float fromX, float toX, float bottom, float top, string sorting, int order,
            string prefab = "Lagune")
        {
            if (!prefabs.ContainsKey(prefab)) prefab = "Lagune";
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab], parent);
            var sr = go.GetComponent<SpriteRenderer>();
            var size = sr.sprite.bounds.size;
            float s = (top - bottom) / size.y;
            sr.size = new Vector2((toX - fromX) / s, size.y); // répétition continue : s'arrête pile en toX
            sr.sortingLayerName = sorting;
            sr.sortingOrder = order;
            go.transform.localScale = new Vector3(s, s, 1f);
            go.transform.localPosition = new Vector3((fromX + toX) / 2f, bottom, 0f);
            go.name = "Lagune";

            // Eau animée : vagues qui vont et viennent, reflets de soleil qui scintillent. L'eau lointaine (plan
            // intermédiaire) a des vagues plus courtes et des reflets plus petits ; l'eau devant le héros s'efface en
            // fondu sur son haut, ses reflets sont un peu plus bas.
            var fx = go.AddComponent<WaterFx>();
            bool far = sorting == "PlanIntermediaire";
            fx.drift = far ? 0.45f : 0.7f;
            fx.glintSize = far ? 0.2f : 0.35f;
            fx.glintDepth = far ? 1.2f : prefab == "LaguneAvant" ? 0.9f : 0.5f;
            fx.casesPerGlint = far ? 2.5f : 3.5f;
        }

        /// <summary>Bande de sol répétée de <paramref name="x0"/> à <paramref name="x1"/>, dessus en <paramref name="top"/>.</summary>
        static void GroundSpan(Transform parent, string prefab, float x0, float x1, float top)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab], parent);
            var sr = go.GetComponent<SpriteRenderer>();
            var size = sr.sprite.bounds.size;
            sr.size = new Vector2(x1 - x0, size.y);
            go.transform.localPosition = new Vector3((x0 + x1) / 2f, top - size.y, 0f);
            go.name = $"{prefab}_x{x0:0}-{x1:0}";
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

        /// <summary>Câbles entre poteaux voisins, accrochés aux deux isolateurs, avec une flèche (le câble pend).</summary>
        static void PowerLine(Transform parent, List<Transform> poles)
        {
            // Position des isolateurs sur l'image du poteau (mesurée sur lamp_02.png), en fraction de sa taille.
            float[] insulatorX = { 0.074f, 0.921f };
            const float insulatorY = 0.985f;
            const int points = 24;
            for (int i = 0; i < poles.Count - 1; i++)
                for (int k = 0; k < insulatorX.Length; k++)
                {
                    var a = Anchor(poles[i], insulatorX[k], insulatorY);
                    var b = Anchor(poles[i + 1], insulatorX[k], insulatorY);
                    var go = new GameObject($"Cable_{i + 1}_{(k == 0 ? "gauche" : "droite")}");
                    go.transform.SetParent(parent, false);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.useWorldSpace = false;
                    lr.sharedMaterial = spriteMaterial;
                    lr.widthMultiplier = 0.035f;
                    lr.startColor = lr.endColor = CableColor;
                    lr.numCapVertices = 0;
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lr.receiveShadows = false;
                    lr.sortingLayerName = "Rue";
                    lr.sortingOrder = 3;
                    lr.positionCount = points;
                    float sag = 0.35f + 0.03f * Vector3.Distance(a, b);
                    for (int p = 0; p < points; p++)
                    {
                        float t = p / (points - 1f);
                        var pos = Vector3.Lerp(a, b, t);
                        pos.y -= sag * 4f * t * (1f - t);
                        lr.SetPosition(p, pos);
                    }
                }
        }

        static Vector3 Anchor(Transform pole, float fx, float fy)
        {
            var sr = pole.GetComponent<SpriteRenderer>();
            var size = sr.sprite.bounds.size;
            float s = pole.localScale.x;
            float x = (sr.flipX ? 0.5f - fx : fx - 0.5f) * size.x * s;
            return pole.localPosition + new Vector3(x, fy * size.y * s, 0f);
        }

        /// <summary>Plage de x local qu'une couche doit couvrir pour qu'aucun bord ne se voie, du début à la fin.</summary>
        static (float min, float max) LocalRange(float factor)
        {
            float camMin = HalfViewMin, camMax = levelLength - HalfViewMin;
            return (factor * camMin - HalfViewMax - 1f, factor * camMax + HalfViewMax + 1f);
        }

        // ---------------------------------------------------------------- préfabriqués et réglages du projet

        static void CreatePrefabs()
        {
            SpritePrefab("Ciel", Art + "Sky/sky.jpg", "Ciel");
            for (int i = 1; i <= 3; i++) SpritePrefab($"Nuage_0{i}", Art + $"Clouds/cloud_0{i}.png", "Nuages");
            SpritePrefab("Horizon", Art + "Horizon/horizon.png", "Horizon", SpriteDrawMode.Tiled);
            SpritePrefab("SolTerre", Art + "Ground/ground_dirt.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SolPave", Art + "Ground/ground_paved.jpg", "Monde", SpriteDrawMode.Tiled);
            for (int i = 1; i <= 12; i++) SpritePrefab($"Batiment_{i:00}", Art + $"Buildings/building_{i:00}.png", "PlanIntermediaire");
            for (int i = 1; i <= 6; i++) SpritePrefab($"Cocotier_0{i}", Art + $"Vegetation/palm_0{i}.png", "Rue");
            for (int i = 0; i < 3; i++)
            {
                SpritePrefab($"Caisse_{i}", GameplayDir + $"Blocks/crate_{i}.png", "Monde");
                SpritePrefab($"Laterite_{i}", GameplayDir + $"Blocks/laterite_{i}.png", "Monde");
            }
            SpritePrefab("Planche", GameplayDir + "Platforms/plank.png", "Monde");
            SpritePrefab("Ponton", GameplayDir + "Platforms/dock.png", "Monde");
            if (AssetDatabase.LoadAssetAtPath<Sprite>(GameplayDir + "Pirogue/pirogue_0.png") == null) missing.Add(GameplayDir + "Pirogue/pirogue_0.png");
            SpritePrefab("Lampadaire_A", Art + "Props/lamp_01.png", "Rue");
            SpritePrefab("PoteauElectrique", Art + "Props/lamp_02.png", "Rue");
            SpritePrefab("Lampadaire_B", Art + "Props/lamp_03.png", "Rue");
            for (int i = 1; i <= 12; i++) SpritePrefab($"Mobilier_{FurnitureNames[i - 1]}", Art + $"Furniture/furniture_{i:00}.png", "Monde");
            for (int i = 1; i <= 12; i++) SpritePrefab($"Plante_{PlantNames[i - 1]}", Art + $"Plants/plant_{i:00}.png", "Monde");
            // Facultatifs : sans eux, la sortie reste une rue qui continue.
            if (AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Bridge/bridge.png") != null)
                SpritePrefab("PontVictoire", Art + "Bridge/bridge.png", "PlanIntermediaire");
            if (AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Water/lagoon.jpg") != null)
                SpritePrefab("Lagune", Art + "Water/lagoon.jpg", "PlanIntermediaire", SpriteDrawMode.Tiled);
            if (AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Water/lagoon_front.png") != null)
                SpritePrefab("LaguneAvant", Art + "Water/lagoon_front.png", "AvantPlan", SpriteDrawMode.Tiled);
            heroSets.Clear();
            foreach (var h in Heroes)
            {
                var set = HeroSet(h);
                if (set != null) heroSets.Add(set);
            }
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

        static void EnsureSortingLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");
            foreach (var name in SortingLayerNames)
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++)
                    if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) exists = true;
                if (exists) continue;
                layers.InsertArrayElementAtIndex(layers.arraySize);
                var e = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                e.FindPropertyRelative("name").stringValue = name;
                e.FindPropertyRelative("uniqueID").intValue = (name.GetHashCode() & 0x7FFFFFFF) | 1;
            }
            tagManager.ApplyModifiedProperties();
        }

        static PhysicsMaterial2D Frictionless()
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(FrictionlessPath);
            if (mat != null) return mat;
            mat = new PhysicsMaterial2D("SansFrottement") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(mat, FrictionlessPath);
            return mat;
        }

        static void AddSceneToBuild()
        {
            // Le stage vient après l'accueil et la carte (voir MenuBuilder) : on l'ajoute à la fin s'il manque.
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Any(s => s.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Atlas des petits sprites (mobilier, plantes, nuages) : un seul envoi de texture pour tous.
        /// Les grandes images (ciel, horizon, sol, bâtiments, cocotiers, poteaux) restent à part.</summary>
        static void CreateAtlas()
        {
            if (AssetDatabase.LoadMainAssetAtPath(AtlasPath) != null) return;
            try
            {
                var atlas = (SpriteAtlasAsset)System.Activator.CreateInstance(typeof(SpriteAtlasAsset), true);
                atlas.Add(new Object[]
                {
                    AssetDatabase.LoadMainAssetAtPath(Art + "Furniture"),
                    AssetDatabase.LoadMainAssetAtPath(Art + "Plants"),
                    AssetDatabase.LoadMainAssetAtPath(Art + "Clouds"),
                });
                SpriteAtlasAsset.Save(atlas, AtlasPath);
                AssetDatabase.ImportAsset(AtlasPath);
                var importer = AssetImporter.GetAtPath(AtlasPath) as SpriteAtlasImporter;
                if (importer != null)
                {
                    importer.includeInBuild = true;
                    importer.packingSettings = new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4 };
                    importer.textureSettings = new SpriteAtlasTextureSettings { generateMipMaps = true, filterMode = FilterMode.Trilinear, sRGB = true };
                    importer.SaveAndReimport();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Super Yao : atlas non créé automatiquement (" + e.Message + "). Le créer à la main : " +
                                 "Assets > Create > 2D > Sprite Atlas, y glisser Furniture, Plants et Clouds.");
            }
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
