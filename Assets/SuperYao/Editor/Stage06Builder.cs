using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Construit le stage 6, « La route du Nord » (menu Super Yao > Construire le stage 6), en plein jour : Yao quitte
    /// Abidjan par l'autoroute (route inondée par un marigot, traversée sur le toit des camions du convoi), longe la
    /// palmeraie, traverse les cacaoyers (pont de liane au-dessus d'un marigot, plate-forme dans les arbres et son
    /// échelle), les hévéas (lianes et balançoire au-dessus des saignées inondées, corde à nœuds), puis le village et
    /// ses étals de bananes. Nouveautés : ConvoyTruck, Liana, SwingSeat, Climbable, le singe (Enemy) et ses noix de
    /// coco. Pas de boss, comme sur le téléphone.
    /// Reprend les stages précédents : ciel, nuages, oiseaux, asphalte, glissières et panneau du stage 2, eau, plantes,
    /// cocotiers, Yao à pied et son skate, CourseKit.
    /// </summary>
    public static class Stage06Builder
    {
        const string Art = "Assets/SuperYao/Backgrounds/Stage6/";
        const string Art2 = "Assets/SuperYao/Backgrounds/Stage2/";
        const string Art1 = "Assets/SuperYao/Backgrounds/Stage1/";
        const string PrefabDir = "Assets/SuperYao/Prefabs/Stage6";
        const string ScenePath = "Assets/SuperYao/Scenes/Stage06_RouteDuNord.unity";
        const string HeroPrefab = "Assets/SuperYao/Prefabs/Heros/Heros_Yao.prefab";
        const string GameplayDir = "Assets/SuperYao/Gameplay/";
        const string FrictionlessPath = "Assets/SuperYao/Physics/SansFrottement.physicsMaterial2D";
        const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string Tagline = "Quitte Abidjan sur le toit des camions, file à travers les plantations de cacao et d'hévéas de liane en liane, et gare-toi des singes !";

        const float FSky = 0f, FClouds = 0.05f, FHorizon = 0.15f, FFar = 0.35f, FMid = 0.65f, FStreet = 0.9f, FFront = 1.1f;
        const float VisibleRows = 20f;
        const float HalfViewMin = VisibleRows / 2f * 4f / 3f, HalfViewMax = VisibleRows / 2f * 2.4f;

        // Parcours (350 cases) : autoroute et palmeraie | cacaoyers | hévéas | village.
        const float LevelLength = 350f, CacaoStart = 90f, HeveaStart = 170f, VillageStart = 260f, GoalX = 347f;
        static readonly float[] ZoneEnds = { CacaoStart, HeveaStart, VillageStart, float.MaxValue };
        static readonly string[] ZoneNames = { "Z1_Autoroute_Palmeraie", "Z2_Cacaoyers", "Z3_Heveas", "Z4_Village" };
        const float AsphaltTop = 0.2f, RedTop = 0.05f, BackOfStreet = 0.15f, PisteStart = 78f;
        // La route inondée (le marigot a débordé) : seuls les camions du convoi passent. Passé ConvoyTo (l'autre rive), un
        // camion qui porte Yao s'arrête pour le déposer, les autres s'en vont ; sortis de l'écran, ils reviennent hors de
        // l'écran par la gauche, au plus tard en ConvoyFrom (avant l'eau).
        const float FloodX0 = 40f, FloodX1 = 64f, ConvoyFrom = 30f, ConvoyTo = 72f, ConvoySpeed = 4.5f;
        // Les trous d'eau de la campagne : (début, fin, eau du marigot ou des saignées).
        static readonly (float x0, float x1, bool marigot)[] Pits =
        {
            (127f, 137f, true),   // marigot des cacaoyers : pont de liane
            (186f, 193f, false),  // saignée inondée : une liane
            (202f, 214f, false),  // grande saignée : deux lianes
            (238f, 248f, false),  // saignée de la balançoire
            (275f, 285f, true),   // marigot du village : banc-passerelle
        };
        // Liane du pont (image 1237 × 500 px) : dessus de la corde, en px depuis le bas, y = 63 + 0,00138 (x - 640)².
        const float VineW = 11.5f, VinePxW = 1237f, VinePxH = 500f;
        // La liane est aplatie (70 % de sa hauteur) : pente de 35° au plus aux bouts, au lieu de 45° (on y avançait à peine).
        const float VineSquash = 0.7f;

        static Material spriteMaterial;
        static PhysicsMaterial2D frictionless;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        [MenuItem("Super Yao/Construire le stage 6 (La route du Nord)")]
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
                EditorUtility.DisplayDialog("Super Yao", "Lance d'abord « Construire le stage 1 » : il fabrique Yao, que le stage 6 réutilise.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            prefabs.Clear();
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            frictionless = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(FrictionlessPath);
            EnsureFolder(PrefabDir);
            CourseKit.Init(spriteMaterial, frictionless);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreatePrefabs();

            var decor = new GameObject("Decor").transform;
            var skyLayer = Layer(decor, "01_Ciel", FSky, 0f);
            Place(skyLayer, "Ciel", 0f, -7f, 14f, 0).gameObject.AddComponent<FitToCamera>();
            BuildClouds(Layer(decor, "02_Nuages", FClouds, 0f));
            BuildSkyLife(decor);
            BuildHorizon(Layer(decor, "03_Horizon_Palmeraie", FHorizon, 1f));
            BuildFar(Layer(decor, "04_Foret", FFar, 1f));
            BuildMid(Layer(decor, "05a_Plantations_Village", FMid, 1f));
            BuildStreet(Layer(decor, "05b_Palmiers", FStreet, 1f));

            var world = new GameObject("06_Monde_Jouable").transform;
            BuildGround(world);
            BuildProps(world);
            BuildCourse(world);
            var yao = (GameObject)PrefabUtility.InstantiatePrefab(hero, world);
            yao.transform.localPosition = new Vector3(4f, 0.02f, 0f);
            yao.AddComponent<HeroRespawn>();
            Stage02Builder.PutOnSkate(yao);
            // Planches de l'utilisateur : Yao pendu à la liane, Yao de dos sur l'échelle.
            var anim = yao.GetComponentInChildren<HeroAnimator>();
            anim.hang = CourseKit.LoadSprites("Assets/SuperYao/Heroes/YaoLiane/hang_", 8);
            anim.climb = CourseKit.LoadSprites("Assets/SuperYao/Heroes/YaoEchelle/climb_", 8);
            BuildForeground(Layer(new GameObject("07_Avant_Plan").transform, "Vegetation_Avant_Plan", FFront, 1f));

            BuildCamera(yao.transform);
            BuildSettings();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log($"Super Yao : stage 6 construit ({LevelLength:0} cases) dans {ScenePath}.");
        }

        // ---------------------------------------------------------------- décor

        static void BuildClouds(Transform layer)
        {
            layer.gameObject.AddComponent<CloudDrift>();
            var clouds = new (int sprite, float x, float y, float h)[] { (2, -8f, 4.6f, 1.6f), (1, 0f, 3.2f, 1.1f), (3, 8f, 5f, 2f), (1, 16f, 2.8f, 1f), (2, 24f, 4.2f, 1.4f) };
            foreach (var c in clouds) Place(layer, $"Nuage_0{c.sprite}", c.x, c.y, c.h);
        }

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

        /// <summary>Horizon : rangées de palmiers à huile et collines bleutées, répétées bord à bord.</summary>
        static void BuildHorizon(Transform layer)
        {
            var (min, max) = LocalRange(FHorizon);
            Band(layer, "HorizonPalmeraie", min, max, 0f, 7.5f, 0);
        }

        /// <summary>Lisière de la forêt, devant la palmeraie (ses palmiers dépassent au-dessus).</summary>
        static void BuildFar(Transform layer)
        {
            var (min, max) = LocalRange(FFar);
            var sr = Band(layer, "HorizonForet", min, max, 0f, 5.2f, 0);
            sr.sortingLayerName = "BatimentsLointains";
        }

        /// <summary>Plan intermédiaire (x local) : palmiers à huile le long de l'autoroute, cacaoyers, hévéas, puis les
        /// cases du village, entre quelques palmiers.</summary>
        static void BuildMid(Transform layer)
        {
            const string sort = "PlanIntermediaire";
            var items = new List<(string prefab, float wx, float h, bool flip)>();
            // Palmeraie (0 à 90).
            float[] palmH = { 8f, 6.5f, 9f, 7f, 8.5f };
            for (int k = 0; k * 6f < CacaoStart + 6f; k++) items.Add(($"PalmierHuile_0{k % 3 + 1}", k * 6f + (k % 2) * 1.5f, palmH[k % palmH.Length], k % 4 == 3));
            // Cacaoyers (90 à 170).
            for (int k = 0; CacaoStart + k * 5f < HeveaStart; k++) items.Add(($"Cacaoyer_0{k % 3 + 1}", CacaoStart + 2f + k * 5f, 4.2f + (k % 3) * 0.5f, k % 2 == 1));
            // Hévéas (170 à 260).
            for (int k = 0; HeveaStart + k * 6f < VillageStart; k++) items.Add(($"Hevea_0{k % 3 + 1}", HeveaStart + 3f + k * 6f, 8f + (k % 2) * 1.2f, k % 2 == 0));
            // Village (260 à 350) : cases et palmiers.
            for (int k = 0; VillageStart + k * 11f < LevelLength + 20f; k++)
            {
                float wx = VillageStart + 4f + k * 11f;
                items.Add(($"Case_0{k % 6 + 1}", wx, 3.4f, k % 2 == 1));
                if (k % 2 == 0) items.Add(($"PalmierHuile_0{k % 3 + 1}", wx + 6f, 7.5f, false));
            }
            foreach (var i in items)
            {
                float lx = FMid * i.wx;
                Place(Zone(layer, FMid, lx), i.prefab, lx, 0f, i.h, i.prefab.StartsWith("Case") ? 1 : 0, i.flip, null, sort);
            }
        }

        static void BuildStreet(Transform layer)
        {
            var palms = new (string prefab, float wx, float h, bool flip)[]
            {
                ("PalmierHuile_02", 26f, 8f, false), ("Cocotier_01", 70f, 10f, true), ("PalmierHuile_03", 112f, 9f, true),
                ("PalmierHuile_01", 178f, 8.5f, false), ("Cocotier_03", 262f, 10f, false), ("PalmierHuile_02", 312f, 8f, true),
            };
            foreach (var p in palms) Place(Zone(layer, FStreet, FStreet * p.wx), p.prefab, FStreet * p.wx, 0f, p.h, 1, p.flip);
        }

        // ---------------------------------------------------------------- monde jouable

        static void BuildGround(Transform world)
        {
            var ground = new GameObject("Sol").transform;
            ground.SetParent(world, false);
            // Autoroute (coupée par la route inondée), puis la piste de terre rouge (coupée par les trous d'eau).
            GroundSpan(ground, "SolAsphalte", -2f, FloodX0, AsphaltTop);
            GroundSpan(ground, "SolAsphalte", FloodX1, PisteStart + 0.6f, AsphaltTop);
            SolidBox(ground, "Collision_Autoroute", -1f, FloodX0, 0f, 3f);
            Water(ground, "Route_Inondee", FloodX0, FloodX1, new Color(0.78f, 0.64f, 0.48f));
            float from = PisteStart;
            foreach (var p in Pits)
            {
                RedSpan(ground, from, p.x0);
                SolidBox(ground, $"Collision_x{from:0}-{p.x0:0}", from == PisteStart ? FloodX1 : from, p.x0, 0f, 3f);
                Water(ground, p.marigot ? $"Marigot_x{p.x0:0}" : $"Saignee_x{p.x0:0}", p.x0, p.x1,
                    p.marigot ? new Color(0.6f, 0.7f, 0.55f) : new Color(0.8f, 0.68f, 0.52f));
                from = p.x1;
            }
            RedSpan(ground, from, LevelLength + 2f);
            SolidBox(ground, $"Collision_x{from:0}-fin", from, LevelLength + 1f, 0f, 3f);
            Wall(ground, "Mur_Debut", -0.5f);
            Wall(ground, "Mur_Fin", LevelLength + 0.5f);
        }

        /// <summary>Terre rouge et sa bordure d'herbe entre x0 et x1.</summary>
        static void RedSpan(Transform ground, float x0, float x1)
        {
            GroundSpan(ground, "SolTerreRouge", x0, x1, RedTop);
            var grass = Band(ground, "SolHerbe", x0, x1, RedTop - 14f / 160f, 55f / 160f, 1);
            grass.name = $"Herbe_x{x0:0}-{x1:0}";
        }

        /// <summary>Eau d'un trou (marigot, saignée, route inondée) : surface 0,35 case sous le sol ; Yao qui y tombe se noie.</summary>
        static void Water(Transform ground, string name, float x0, float x1, Color tint)
        {
            var water = Band(ground, "Mer", x0 - 0.4f, x1 + 0.4f, -2.8f, 2.45f, 6);
            water.name = name;
            water.color = tint;
            var fx = water.gameObject.AddComponent<WaterFx>();
            fx.drift = 0.5f; fx.glintSize = 0.3f; fx.glintDepth = 0.4f; fx.casesPerGlint = 3.5f;
        }

        /// <summary>Décor au fond de la route : glissières et panneau de l'autoroute, étals de bananes, camion de bananes
        /// garé, bancs du village.</summary>
        static void BuildProps(Transform world)
        {
            var props = new GameObject("Rue_Et_Village").transform;
            props.SetParent(world, false);
            // Glissières le long de l'autoroute, interrompues par la route inondée.
            foreach (var (a, b) in new[] { (-2f, FloodX0 - 2f), (FloodX1 + 2f, PisteStart - 1f) })
            {
                Band(Zone(props, 1f, (a + b) / 2f), "Glissiere", a, b - 0.5f, BackOfStreet, 0.75f, 1);
                Place(Zone(props, 1f, b), "GlissiereEmbout", b, BackOfStreet, 0.75f, 1);
                if (a > 0f) Place(Zone(props, 1f, a), "GlissiereEmbout", a - 0.5f, BackOfStreet, 0.75f, 1, true);
            }
            var items = new (string prefab, float x, float y, float h, bool flip, int order)[]
            {
                ("Panneau_80", 22f, 0f, 2.4f, false, 3), ("Plante_Buisson", PisteStart + 1f, -0.1f, 1.2f, false, 5),
                ("Plante_Herbes", 120f, -0.05f, 1.1f, false, 5), ("Plante_Touffes", 165f, -0.05f, 1f, true, 5),
                ("EtalBananes", 266f, 0f, 3.2f, false, 1), ("EtalBananes", 293f, 0f, 3.2f, true, 1), ("CamionBananes", 307f, 0f, 3f, false, 1),
                ("EtalBananes", 330f, 0f, 3.2f, false, 1), ("BancBas", 340f, 0f, 0.9f, false, 2),
            };
            foreach (var i in items)
            {
                var sr = Place(Zone(props, 1f, i.x), i.prefab, i.x, BackOfStreet + i.y, i.h, i.order, i.flip);
                // Décor du village (étals, camion garé, banc) : derrière la brume du plan intermédiaire, comme les
                // personnages du décor du stage 5, pour ne pas se confondre avec ce qui se joue.
                if (i.prefab == "EtalBananes" || i.prefab == "CamionBananes" || i.prefab == "BancBas")
                {
                    sr.sortingLayerName = "PlanIntermediaire";
                    sr.sortingOrder = 9000 + i.order;
                }
            }
        }

        /// <summary>
        /// Le parcours (350 cases). Autoroute : la route inondée se traverse sur le toit des camions du convoi (bus, grumier,
        /// camion de bananes) qui roulent sans arrêt. Cacaoyers : singe sur une branche, plate-forme dans un arbre et son
        /// échelle de corde, pont de liane au-dessus du marigot (un singe lance des noix de coco depuis sa branche).
        /// Hévéas : lianes au-dessus des saignées inondées (s'y accrocher en sautant, ◄ ► pour se balancer, saut pour
        /// lâcher), corde à nœuds vers une branche et son diamant, balançoire. Village : banc-passerelle sur le marigot,
        /// gbaka, étals. Danger des ennemis 32 (agame et tortue 1, vautour 2, singe 3, gbaka 4 ; téléphone : 30,5 ± 20 %).
        /// Un seul point de contrôle, au milieu ; pas de boss.
        /// </summary>
        static void BuildCourse(Transform world)
        {
            var course = new GameObject("Parcours").transform;
            course.SetParent(world, false);
            Transform In(float x) => Zone(course, 1f, x);
            var items = new List<(Collectible.Kind kind, float x, float y)>();
            void Line(Collectible.Kind kind, float x0, float x1, float step, float y)
            {
                for (float x = x0; x <= x1 + 0.01f; x += step) items.Add((kind, x, y));
            }
            var C = Collectible.Kind.Coin;
            var B = Collectible.Kind.Banana;
            var D = Collectible.Kind.Diamond;

            // --- Autoroute : le convoi. Les coffres sont posés plus haut que les toits des camions.
            BuildConvoy(course);
            Line(C, 20f, 24f, 1f, 1.2f);
            Line(C, 44f, 60f, 2f, 5.2f);                  // au-dessus de la route inondée (depuis le toit des camions)
            Line(B, 68f, 72f, 1f, 1.2f);

            // --- Cacaoyers
            var cacao = In(100f);
            TreeAt(cacao, "Cacaoyer_03", 95f, 6.5f, false);
            Branch(cacao, 2, 95.3f, 102.5f, 3.6f, false);
            Line(C, 97f, 101f, 1f, 4.8f);
            // Plate-forme dans un arbre, son échelle de corde, et un diamant au-dessus.
            TreeAt(cacao, "Cacaoyer_03", 113.5f, 6.8f, true);
            Shelf(cacao, "PlancheCourte", 115.5f, 4.4f, 3f);
            Ladder(cacao, "EchelleCorde", 116.6f, 0f, 4.4f);
            Line(C, 114.5f, 116.5f, 1f, 5.6f);
            items.Add((D, 117f, 8.6f));
            // Pont de liane au-dessus du marigot, entre deux cacaoyers ; le singe guette depuis sa branche.
            var marigot = In(132f);
            TreeAt(marigot, "Cacaoyer_03", 126.2f, 6.5f, false);
            TreeAt(marigot, "Cacaoyer_01", 137.9f, 6f, true);
            VineBridge(marigot, 132f, 0.4f);
            Branch(marigot, 1, 131.6f, 138.2f, 6.2f, true);
            Line(C, 130f, 134f, 1f, 2f);
            CourseKit.Stack(In(154f), 154f, 2, 0);
            CourseKit.Stack(In(155f), 155f, 3, 1);
            Line(C, 144f, 148f, 1f, 1.2f);
            items.Add((C, 155f, 4.3f));

            // --- Hévéas : lianes, corde à nœuds, balançoire.
            var h1 = In(189f);
            TreeAt(h1, "Hevea_02", 184.8f, 12f, false);
            Branch(h1, 1, 185.1f, 192.3f, 9.1f, false);
            Liana(h1, 189.6f, 8.8f, 5.4f);
            Line(B, 188f, 191f, 1f, 6.6f);
            var h2 = In(208f);
            TreeAt(h2, "Hevea_01", 200.8f, 12f, false);
            Branch(h2, 3, 201.1f, 208.3f, 9.1f, false);
            TreeAt(h2, "Hevea_02", 215.3f, 12.5f, true);
            Branch(h2, 1, 208.6f, 215f, 9.4f, true);
            Liana(h2, 205f, 8.8f, 5.4f);
            Liana(h2, 210.6f, 9.1f, 5.6f);
            Line(C, 206.5f, 209f, 1.25f, 6.8f);
            Line(C, 212f, 214.5f, 1.25f, 6.8f);
            // Corde à nœuds : on grimpe jusqu'à la branche du diamant.
            var h3 = In(223f);
            TreeAt(h3, "Hevea_03", 219.5f, 11f, false);
            Branch(h3, 2, 219.8f, 228.3f, 8f, false);
            Ladder(h3, "CordeNoeuds", 222.6f, 0.3f, 8f);
            Line(C, 224f, 227f, 1f, 9.2f);
            items.Add((D, 227.4f, 10.4f));
            // Balançoire au-dessus de la dernière saignée.
            var h4 = In(243f);
            TreeAt(h4, "Hevea_01", 237f, 12f, false);
            Branch(h4, 3, 237.3f, 246.5f, 9.1f, false);
            Swing(h4, new Vector2(243f, 8.3f), 5.5f, 30f, 3.4f);
            Line(B, 241f, 245f, 1f, 5.6f);

            // --- Village : banc-passerelle sur le marigot, branche du singe.
            Shelf(In(280f), "PlancheLongue", 280f, 0.5f, 5f, -0.7f);
            Line(C, 278f, 282f, 1f, 1.7f);
            var v = In(322f);
            TreeAt(v, "Cacaoyer_03", 319.3f, 6.5f, false);
            Branch(v, 3, 319.6f, 326f, 3.8f, false);
            Line(C, 336f, 341f, 1f, 1.2f);
            Line(B, 262f, 266f, 1f, 1.2f);

            var blocks = new (BumpBlock.Kind kind, float x, float y)[]
            {
                (BumpBlock.Kind.CoinBox, 10f, 4.2f), (BumpBlock.Kind.Brick, 11f, 4.2f), (BumpBlock.Kind.PowerBox, 12f, 4.2f), (BumpBlock.Kind.Brick, 13f, 4.2f),
                (BumpBlock.Kind.CoinBox, 104f, CourseKit.BlockY), (BumpBlock.Kind.Brick, 105f, CourseKit.BlockY), (BumpBlock.Kind.PowerBox, 106f, CourseKit.BlockY), (BumpBlock.Kind.Brick, 107f, CourseKit.BlockY),
                (BumpBlock.Kind.CoinSack, 160f, CourseKit.BlockY),
                (BumpBlock.Kind.CoinBox, 300f, CourseKit.BlockY), (BumpBlock.Kind.Brick, 301f, CourseKit.BlockY), (BumpBlock.Kind.PowerBox, 302f, CourseKit.BlockY), (BumpBlock.Kind.Brick, 303f, CourseKit.BlockY),
            };
            foreach (var b in blocks) CourseKit.Block(In(b.x), b.kind, b.x, b.y);
            foreach (var it in items) CourseKit.Item(In(it.x), it.kind, it.x, it.y);

            var E = new (Enemy.Kind kind, float x, float y)[]
            {
                // Autoroute et palmeraie (9)
                (Enemy.Kind.Agama, 16f, 0f), (Enemy.Kind.Vulture, 30f, 5.5f), (Enemy.Kind.Vulture, 52f, 8f), (Enemy.Kind.Gbaka, 85f, 0f),
                // Cacaoyers (8)
                (Enemy.Kind.Monkey, 99.5f, 3.6f), (Enemy.Kind.Turtle, 110f, 0f), (Enemy.Kind.Monkey, 134f, 6.2f), (Enemy.Kind.Agama, 150f, 0f),
                // Hévéas (7)
                (Enemy.Kind.Agama, 180f, 0f), (Enemy.Kind.Vulture, 197f, 6.5f), (Enemy.Kind.Monkey, 232f, 0f), (Enemy.Kind.Turtle, 252f, 0f),
                // Village (8)
                (Enemy.Kind.Gbaka, 296f, 0f), (Enemy.Kind.Monkey, 323f, 3.8f), (Enemy.Kind.Agama, 335f, 0f),
            };
            foreach (var e in E) CourseKit.EnemyAt(In(e.x), e.kind, e.x, e.y);

            CourseKit.Flag(In(175f), 175f, 3.5f, false); // un seul point de contrôle, au milieu
            CourseKit.Flag(In(GoalX), GoalX, 5.5f, true);

            var catalog = MenuBuilder.Catalog();
            var stage = catalog.stages[5];
            stage.scene = System.IO.Path.GetFileNameWithoutExtension(ScenePath);
            stage.tagline = Tagline;
            stage.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SuperYao/UI/Backgrounds/thumb_route_nord.jpg");
            stage.coins = items.Count(i => i.kind == C) + blocks.Count(k => k.kind == BumpBlock.Kind.CoinBox)
                          + blocks.Count(k => k.kind == BumpBlock.Kind.CoinSack) * 8;
            stage.bananas = items.Count(i => i.kind == B);
            stage.diamonds = items.Count(i => i.kind == D);
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Le convoi : quatre camions espacés de 27 cases au départ. Leur toit (ou leur chargement) porte Yao ; on
        /// passe à travers par en dessous. Ils ne disparaissent jamais à l'écran (voir ConvoyTruck).</summary>
        static void BuildConvoy(Transform course)
        {
            var group = new GameObject("Convoi").transform;
            group.SetParent(course, false);
            // (image, hauteur, dessus en part de la hauteur, début et fin du dessus en part de la largeur)
            var trucks = new (string prefab, float h, float top, float x0, float x1)[]
            {
                ("Car", 3.1f, 0.96f, 0.04f, 0.9f), ("Grumier", 3.3f, 0.92f, 0.02f, 0.95f),
                ("CamionBananes", 3f, 0.85f, 0.02f, 0.95f), ("Grumier", 3.3f, 0.92f, 0.02f, 0.95f),
            };
            for (int i = 0; i < trucks.Length; i++)
            {
                var t = trucks[i];
                var go = new GameObject($"Camion_{i + 1}_{t.prefab}");
                go.transform.SetParent(group, false);
                go.transform.localPosition = new Vector3(ConvoyFrom, 0f, 0f);
                go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                var sr = Place(go.transform, t.prefab, 0f, 0f, t.h, 2);
                float w = sr.sprite.bounds.size.x * sr.transform.localScale.x;
                CourseKit.OneWay(go, ((t.x0 + t.x1) / 2f - 0.5f) * w, t.top * t.h, (t.x1 - t.x0) * w);
                var truck = go.AddComponent<ConvoyTruck>();
                truck.fromX = ConvoyFrom;
                truck.toX = ConvoyTo;
                truck.speed = ConvoySpeed;
                truck.offset = (-12f + i * 27f) - ConvoyFrom; // départ en -12, 15, 42 (dans l'eau) et 69
                truck.visual = sr;
            }
        }

        /// <summary>Arbre posé au sol, derrière le parcours (ses branches s'ajoutent à part).</summary>
        static void TreeAt(Transform parent, string prefab, float x, float h, bool flip) =>
            Place(parent, prefab, x, BackOfStreet - 0.05f, h, -2, flip);

        /// <summary>Branche (bout épais contre le tronc) de x0 à x1, dessus en <paramref name="top"/> : on y marche, on la
        /// traverse par en dessous. <paramref name="leftward"/> : elle part vers la gauche.</summary>
        static void Branch(Transform parent, int art, float x0, float x1, float top, bool leftward)
        {
            float len = x1 - x0, mid = (x0 + x1) / 2f;
            var sr = Place(parent, $"Branche_0{art}", mid, 0f, 1f, 2, leftward);
            float s = len / sr.sprite.bounds.size.x;
            sr.transform.localScale = new Vector3(s, s, 1f);
            float h = sr.sprite.bounds.size.y * s;
            sr.transform.localPosition = new Vector3(mid, top - h * 0.86f, 0f);
            var go = new GameObject($"Branche_x{x0:0.#}-{x1:0.#}");
            go.transform.SetParent(parent, false);
            CourseKit.OneWay(go, mid, top, len * 0.88f);
        }

        /// <summary>Plate-forme en bois (planche sur pieds) de largeur <paramref name="width"/>, dessus en
        /// <paramref name="top"/> ; <paramref name="baseY"/> sinon calculé (pieds dans l'eau pour le banc-passerelle).</summary>
        static void Shelf(Transform parent, string prefab, float x, float top, float width, float baseY = float.NaN)
        {
            var sr = Place(parent, prefab, x, 0f, 1f, 3);
            float s = width / sr.sprite.bounds.size.x;
            sr.transform.localScale = new Vector3(s, s, 1f);
            float h = sr.sprite.bounds.size.y * s;
            sr.transform.localPosition = new Vector3(x, float.IsNaN(baseY) ? top - h * 0.97f : baseY, 0f);
            if (!float.IsNaN(baseY)) top = baseY + h * 0.97f;
            var go = new GameObject($"Plateforme_x{x:0.#}");
            go.transform.SetParent(parent, false);
            CourseKit.OneWay(go, x, top, width * 0.92f);
        }

        /// <summary>Échelle (ou corde à nœuds) de <paramref name="bottom"/> à <paramref name="top"/> : on y grimpe avec le
        /// bouton de saut.</summary>
        static void Ladder(Transform parent, string prefab, float x, float bottom, float top)
        {
            Place(parent, prefab, x, bottom, top - bottom + 0.15f, 2);
            var go = new GameObject($"Grimper_{prefab}_x{x:0.#}");
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.8f, top + 0.2f);
            box.offset = new Vector2(x, (top + 0.2f) / 2f);
            go.AddComponent<Climbable>().top = top;
        }

        /// <summary>Liane pendue en (x, anchorY) : la corde, teintée de vert, tourne autour du point d'attache.</summary>
        static void Liana(Transform parent, float x, float anchorY, float length)
        {
            var go = new GameObject($"Liane_x{x:0.#}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, anchorY, 0f);
            var liana = go.AddComponent<Liana>();
            liana.length = length;
            var sr = Place(go.transform, "Corde", 0f, 0f, length + 0.3f, 3, false, new Color(0.72f, 0.85f, 0.5f));
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            liana.visual = sr.transform;
        }

        /// <summary>Balançoire : siège pendu à deux cordes sous <paramref name="anchor"/>.</summary>
        static void Swing(Transform parent, Vector2 anchor, float length, float amplitude, float period)
        {
            var group = new GameObject($"Balancoire_x{anchor.x:0}").transform;
            group.SetParent(parent, false);
            var seat = new GameObject("Siege");
            seat.transform.SetParent(group, false);
            seat.transform.localPosition = anchor + Vector2.down * length;
            seat.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var sr = Place(seat.transform, "Siege", 0f, 0f, 1f, 3);
            const float width = 2.8f;
            float s = width / sr.sprite.bounds.size.x;
            sr.transform.localScale = new Vector3(s, s, 1f);
            float h = sr.sprite.bounds.size.y * s;
            sr.transform.localPosition = new Vector3(0f, -h * 0.85f, 0f);
            CourseKit.OneWay(seat, 0f, 0f, width * 0.9f);
            var swing = seat.AddComponent<SwingSeat>();
            swing.anchor = anchor;
            swing.length = length;
            swing.amplitude = amplitude;
            swing.period = period;
            var ropes = new List<Transform>();
            var offsets = new[] { -width / 2f + 0.3f, width / 2f - 0.3f };
            foreach (var o in offsets)
            {
                var rope = Place(group, "Corde", anchor.x + o, anchor.y, length + 0.1f, 2);
                rope.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                ropes.Add(rope.transform);
            }
            swing.ropes = ropes.ToArray();
            swing.ropeOffsets = offsets;
        }

        /// <summary>Pont de liane : la liane pend entre deux arbres (centre en x, bas de l'image en baseY) ; on marche sur sa
        /// partie basse (pente de moins de 45°), traversable par en dessous.</summary>
        static void VineBridge(Transform parent, float x, float baseY)
        {
            var sr = Place(parent, "LianePont", x, baseY, 1f, 3);
            float s = VineW / sr.sprite.bounds.size.x;
            sr.transform.localScale = new Vector3(s, s * VineSquash, 1f);
            var edge = sr.gameObject.AddComponent<EdgeCollider2D>();
            var pts = new List<Vector2>();
            // Partie praticable : jusqu'à 1,5 case des rives (on y saute depuis la rive, on en redescend sur l'autre).
            for (float px = 640f - 370f; px <= 640f + 370.1f; px += 37f)
                pts.Add(new Vector2((px - VinePxW / 2f) / 160f, (63f + 0.00138f * (px - 640f) * (px - 640f) - 6f) / 160f));
            edge.points = pts.ToArray();
            edge.sharedMaterial = frictionless;
            edge.usedByEffector = true;
            var effector = sr.gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
            sr.name = "Pont_De_Liane";
        }

        static void BuildForeground(Transform layer)
        {
            var tint = new Color(0.72f, 0.78f, 0.72f);
            var plants = new (string prefab, float wx, float h, bool flip)[]
                { ("Plante_Herbes", 12f, 1.3f, false), ("Plante_Touffes", 96f, 1.2f, false), ("Plante_Herbes", 168f, 1.3f, true), ("Plante_Touffes", 255f, 1.2f, true), ("Plante_Herbes", 318f, 1.3f, false) };
            foreach (var p in plants) Place(Zone(layer, FFront, FFront * p.wx), p.prefab, FFront * p.wx, -2.1f, p.h, 0, p.flip, tint, "AvantPlan");
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

        static void BuildSettings()
        {
            var settings = new GameObject("Reglages_Stage");
            settings.AddComponent<StageSettings>();
            settings.AddComponent<TouchControls>().fireIcon = CourseKit.LoadSprite($"{GameplayDir}Items/fireball.png");
            settings.AddComponent<StageState>().LevelNumber = 6;
            var hud = settings.AddComponent<Hud>();
            hud.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/SuperYao/UI/Fonts/LilitaOne-Regular.ttf");
            hud.portrait = CourseKit.LoadSprite("Assets/SuperYao/UI/portrait_yao.png");
            hud.coin = CourseKit.LoadSprite($"{GameplayDir}Items/coin_0.png");
            hud.banana = CourseKit.LoadSprite($"{GameplayDir}Items/banana.png");
            hud.diamond = CourseKit.LoadSprite($"{GameplayDir}Items/diamond_0.png");
            var pause = settings.AddComponent<PauseScreen>();
            pause.font = hud.font;
            pause.hero = CourseKit.LoadSprite("Assets/SuperYao/UI/hero_yao.png");
            pause.leaf = CourseKit.LoadSprite($"{Art1}Plants/plant_07.png");
            pause.stoneGrey = CourseKit.LoadSprite("Assets/SuperYao/UI/stone_grey.png");
            pause.stoneEarth = CourseKit.LoadSprite("Assets/SuperYao/UI/stone_earth.png");
            pause.playIcon = CourseKit.LoadSprite("Assets/SuperYao/UI/Icons/play.png");
            pause.restartIcon = CourseKit.LoadSprite("Assets/SuperYao/UI/Icons/restart.png");
            pause.homeIcon = CourseKit.LoadSprite("Assets/SuperYao/UI/Icons/home.png");
            settings.AddComponent<AudioSource>();
            var sfx = settings.AddComponent<SoundFx>();
            sfx.sounds = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/SuperYao/Audio" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => new SoundFx.Entry { name = System.IO.Path.GetFileNameWithoutExtension(p), clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p) })
                .ToArray();
        }

        static void AddSceneToBuild()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Any(sc => sc.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
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

        static SpriteRenderer Band(Transform parent, string prefab, float x0, float x1, float baseY, float height, int order)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab], parent);
            var sr = go.GetComponent<SpriteRenderer>();
            var size = sr.sprite.bounds.size;
            float s = height / size.y;
            sr.size = new Vector2((x1 - x0) / s, size.y);
            sr.sortingOrder = order;
            go.transform.localScale = new Vector3(s, s, 1f);
            go.transform.localPosition = new Vector3((x0 + x1) / 2f, baseY, 0f);
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

        static void SolidBox(Transform parent, string name, float x0, float x1, float top, float depth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = new Vector2((x0 + x1) / 2f, top - depth / 2f);
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

        static Sprite[] Frames(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new Sprite[0];
            return AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
        }

        static void CreatePrefabs()
        {
            // Stages précédents
            SpritePrefab("Ciel", Art1 + "Sky/sky.jpg", "Ciel");
            for (int i = 1; i <= 3; i++) SpritePrefab($"Nuage_0{i}", Art1 + $"Clouds/cloud_0{i}.png", "Nuages");
            SpritePrefab("SolAsphalte", Art2 + "Ground/ground_asphalt.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("Mer", Art2 + "Water/sea.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("Glissiere", Art2 + "Rails/rail_01.png", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("GlissiereEmbout", Art2 + "Rails/rail_04.png", "Monde");
            SpritePrefab("Panneau_80", Art2 + "Signs/sign_04.png", "Monde");
            foreach (int i in new[] { 1, 3 }) SpritePrefab($"Cocotier_0{i}", Art2 + $"Vegetation/palm_0{i}.png", "Rue");
            SpritePrefab("Plante_Herbes", Art2 + "Plants/plant_01.png", "Monde");
            SpritePrefab("Plante_Buisson", Art2 + "Plants/plant_03.png", "Monde");
            SpritePrefab("Plante_Touffes", Art2 + "Plants/plant_06.png", "Monde");
            // Stage 6
            SpritePrefab("HorizonPalmeraie", Art + "Horizon/horizon_palms.png", "Horizon", SpriteDrawMode.Tiled);
            SpritePrefab("HorizonForet", Art + "Horizon/horizon_forest.png", "BatimentsLointains", SpriteDrawMode.Tiled);
            SpritePrefab("SolTerreRouge", Art + "Ground/ground_red.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SolHerbe", Art + "Ground/ground_red_grass.png", "Monde", SpriteDrawMode.Tiled);
            for (int i = 1; i <= 3; i++)
            {
                SpritePrefab($"PalmierHuile_0{i}", Art + $"Trees/oilpalm_0{i}.png", "Rue");
                SpritePrefab($"Cacaoyer_0{i}", Art + $"Trees/cacao_0{i}.png", "Monde");
                SpritePrefab($"Hevea_0{i}", Art + $"Trees/hevea_0{i}.png", "Monde");
                SpritePrefab($"Branche_0{i}", Art + $"Trees/branch_0{i}.png", "Monde");
            }
            for (int i = 1; i <= 6; i++) SpritePrefab($"Case_0{i}", Art + $"Village/house_0{i}.png", "PlanIntermediaire");
            SpritePrefab("EtalBananes", Art + "Village/stall_bananas.png", "Monde");
            SpritePrefab("Car", Art + "Vehicles/bus.png", "Monde");
            SpritePrefab("Grumier", Art + "Vehicles/logtruck.png", "Monde");
            SpritePrefab("CamionBananes", Art + "Vehicles/bananatruck.png", "Monde");
            SpritePrefab("EchelleCorde", Art + "Wood/ladder_rope.png", "Monde");
            SpritePrefab("Corde", Art + "Wood/rope.png", "Monde");
            SpritePrefab("CordeNoeuds", Art + "Wood/rope_knots.png", "Monde");
            SpritePrefab("BancBas", Art + "Wood/bench_low.png", "Monde");
            SpritePrefab("PlancheCourte", Art + "Wood/shelf_small.png", "Monde");
            SpritePrefab("PlancheLongue", Art + "Wood/shelf_long.png", "Monde");
            SpritePrefab("Siege", Art + "Wood/swing_seat.png", "Monde");
            SpritePrefab("LianePont", Art + "Wood/vine.png", "Monde");
        }

        static void SpritePrefab(string name, string spritePath, string sortingLayer, SpriteDrawMode mode = SpriteDrawMode.Simple)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
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
