using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Construit le stage 3, « De Cocody au Plateau » (menu Super Yao > Construire le stage 3) : la promenade de la baie
    /// de Cocody devant l'Hôtel Ivoire, le pont Alassane-Ouattara en travaux (des trous dans le tablier), puis les toits
    /// du Plateau, de nuit, d'immeuble en immeuble. Le jour tombe pendant la traversée du pont (DayNight).
    /// Reprend au maximum les stages 1 et 2 : ciel, nuages, oiseaux, avions, lampadaires, bancs, plantes et pavé du
    /// stage 1 ; mer, asphalte, cocotiers, arbre, cônes et barrières du stage 2 ; Yao en skate, blocs, objets, ennemis
    /// (CourseKit). Mêmes conventions : une unité = une case, sol (collision) en y = 0.
    /// </summary>
    public static class Stage03Builder
    {
        const string Art = "Assets/SuperYao/Backgrounds/Stage3/";
        const string Art2 = "Assets/SuperYao/Backgrounds/Stage2/";
        const string Art1 = "Assets/SuperYao/Backgrounds/Stage1/";
        const string PrefabDir = "Assets/SuperYao/Prefabs/Stage3";
        const string ScenePath = "Assets/SuperYao/Scenes/Stage03_CocodyPlateau.unity";
        const string HeroPrefab = "Assets/SuperYao/Prefabs/Heros/Heros_Yao.prefab";
        const string GameplayDir = "Assets/SuperYao/Gameplay/";
        const string FrictionlessPath = "Assets/SuperYao/Physics/SansFrottement.physicsMaterial2D";
        const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string Tagline = "Longe la baie de Cocody, traverse le pont Alassane-Ouattara en travaux et file sur les toits du Plateau, à la nuit tombée !";

        const float FSky = 0f, FClouds = 0.05f, FHorizon = 0.15f, FFar = 0.35f, FMid = 0.65f, FStreet = 0.9f, FFront = 1.1f;
        const float VisibleRows = 20f;
        const float HalfViewMin = VisibleRows / 2f * 4f / 3f, HalfViewMax = VisibleRows / 2f * 2.4f;

        // Parcours (350 cases, comme les stages 1 et 2) : baie | pont en travaux | toits du Plateau | rue de l'arrivée.
        const float LevelLength = 350f, BridgeStart = 110f, BridgeEnd = 200f, FirstRoof = 208f, GoalX = 342f;
        // Le pont est surélevé : une rampe monte de la promenade (y = 0) au tablier (y = 3), une autre redescend vers la
        // rue du Plateau. Pente d'environ 11° (3 cases sur 15). Trous du tablier en travaux sur la partie haute.
        const float RampUpEnd = 125f, RampDownStart = 185f, DeckRise = 3f;
        static readonly (float x0, float x1)[] Holes = { (135f, 138f), (158f, 161.5f), (176f, 179f) };
        /// <summary>Hauteur de la chaussée en x (0 sur la promenade et dans la rue, 3 sur le pont, en pente sur les rampes).</summary>
        static float DeckY(float x) =>
            x <= BridgeStart || x >= BridgeEnd ? 0f
            : x < RampUpEnd ? DeckRise * (x - BridgeStart) / (RampUpEnd - BridgeStart)
            : x <= RampDownStart ? DeckRise
            : DeckRise * (BridgeEnd - x) / (BridgeEnd - RampDownStart);
        static readonly float[] ZoneEnds = { BridgeStart, BridgeEnd, float.MaxValue };
        static readonly string[] ZoneNames = { "Z1_Baie_Cocody", "Z2_Pont_Alassane_Ouattara", "Z3_Toits_Plateau" };

        const float PavedTop = 0.2f + 5f / 160f, AsphaltTop = 0.2f, BackOfStreet = 0.18f, BuildingBase = 0f;
        // Tablier du pont (deck.png, 297 px de haut, taille d'origine) : la chaussée, où roule Yao, à 118 px du haut.
        const float DeckHeight = 297f / 160f, DeckFeet = 118f / 160f;
        const float DuskFrom = 95f, DuskTo = 150f, NightFrom = 160f, NightTo = 205f;

        // Toits (roof_01 … 06, 160 px par unité) : taille de l'image, ligne du toit (px du haut : la dalle pleine, sous la
        // rambarde du toit 3), bords du toit (px).
        // Une seule échelle pour tous (un étage ≈ 2,4 cases) ; puis l'ordre de passage, la hauteur du toit et l'écart.
        const float RoofScale = 2.4f / 70f;
        static readonly (int w, int h, int roof, int x0, int x1)[] RoofArt =
            { (418, 607, 131, 18, 416), (746, 475, 100, 3, 743), (343, 832, 126, 14, 327), (456, 525, 72, 1, 454), (533, 784, 105, 1, 531), (451, 478, 103, 1, 449) };
        static readonly (int art, float top, float gap)[] RoofPath =
            { (1, 3f, 0f), (3, 4.5f, 2.5f), (2, 3.5f, 2.5f), (4, 5f, 2.5f), (6, 4f, 3f), (5, 5.5f, 3f) };

        static readonly Color FrontTint = new Color(0.72f, 0.78f, 0.72f);
        static readonly Color HorizonTint = new Color(0.92f, 0.94f, 1f);

        static Material spriteMaterial;
        static PhysicsMaterial2D frictionless;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly List<string> missing = new List<string>();
        static readonly List<(float x0, float x1, float top)> roofs = new List<(float x0, float x1, float top)>();

        [MenuItem("Super Yao/Construire le stage 3 (Cocody - Plateau)")]
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
                EditorUtility.DisplayDialog("Super Yao", "Lance d'abord « Construire le stage 1 » : il fabrique Yao, que le stage 3 réutilise.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            prefabs.Clear();
            missing.Clear();
            roofs.Clear();
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            frictionless = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(FrictionlessPath);
            EnsureFolder(PrefabDir);
            CourseKit.Init(spriteMaterial, frictionless);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreatePrefabs();
            if (missing.Count > 0)
            {
                Debug.LogError("Super Yao : images manquantes, stage 3 non construit :\n" + string.Join("\n", missing));
                return;
            }
            ComputeRoofs();

            var decor = new GameObject("Decor").transform;
            var skyLayer = Layer(decor, "01_Ciel", FSky, 0f);
            var (sunset, night, moon) = BuildSky(skyLayer);
            BuildClouds(Layer(decor, "02_Nuages", FClouds, 0f));
            BuildSkyLife(decor);
            BuildHorizon(Layer(decor, "03_Horizon_Plateau", FHorizon, 1f));
            BuildFar(Layer(decor, "04_Lagune_Monuments", FFar, 1f));
            BuildMid(Layer(decor, "05a_Baie_Pont_Plateau", FMid, 1f));
            BuildStreet(Layer(decor, "05b_Lampadaires_Cocotiers", FStreet, 1f));

            var world = new GameObject("06_Monde_Jouable").transform;
            var ground = BuildGround(world);
            var bridge = BuildBridge(world);
            BuildRoofs(world);
            var props = BuildProps(world);
            BuildCourse(world);
            var yao = (GameObject)PrefabUtility.InstantiatePrefab(hero, world);
            yao.transform.localPosition = new Vector3(4f, 0.02f, 0f);
            yao.AddComponent<HeroRespawn>();
            Stage02Builder.PutOnSkate(yao);
            BuildForeground(Layer(new GameObject("07_Avant_Plan").transform, "Vegetation_Avant_Plan", FFront, 1f));

            // Le jour tombe pendant la traversée du pont.
            var dn = new GameObject("Jour_Nuit").AddComponent<DayNight>();
            dn.sunsetSky = sunset;
            dn.nightSky = night;
            dn.moon = moon;
            dn.duskFrom = DuskFrom; dn.duskTo = DuskTo; dn.nightFrom = NightFrom; dn.nightTo = NightTo;
            dn.tintRoots = new[] { decor, ground, bridge, props };

            BuildCamera(yao.transform);
            BuildSettings();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log($"Super Yao : stage 3 construit ({LevelLength:0} cases) dans {ScenePath}.");
        }

        // ---------------------------------------------------------------- ciel et lointains

        static (SpriteRenderer sunset, SpriteRenderer night, SpriteRenderer moon) BuildSky(Transform layer)
        {
            // Trois ciels superposés, étirés sur l'écran : jour (stage 1), coucher de soleil, nuit ; et la lune.
            Place(layer, "Ciel", 0f, -7f, 14f, 0).gameObject.AddComponent<FitToCamera>();
            var sunset = Place(layer, "CielCoucher", 0f, -7f, 14f, 1);
            sunset.gameObject.AddComponent<FitToCamera>();
            var night = Place(layer, "CielNuit", 0f, -7f, 14f, 2);
            night.gameObject.AddComponent<FitToCamera>();
            var moon = Place(layer, "Lune", 9f, 3.2f, 2.6f, 3);
            foreach (var r in new[] { sunset, night, moon }) { r.color = new Color(1f, 1f, 1f, 0f); r.enabled = false; }
            return (sunset, night, moon);
        }

        static void BuildClouds(Transform layer)
        {
            layer.gameObject.AddComponent<CloudDrift>();
            var clouds = new (int sprite, float x, float y, float h)[]
            {
                (1, -8f, 5f, 1.1f), (3, 0f, 3.4f, 2.0f), (2, 8f, 4.8f, 1.6f), (1, 15f, 2.8f, 1.0f), (2, 23f, 4.2f, 1.4f),
            };
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

        /// <summary>Horizon : le Plateau vu de l'autre côté de la baie, de jour, puis le même quartier la nuit (la bande de
        /// nuit commence par une copie dont le bord gauche s'efface en fondu).</summary>
        static void BuildHorizon(Transform layer)
        {
            var (min, max) = LocalRange(FHorizon);
            float nightStart = FHorizon * 165f;
            const float dayHeight = 3.6f, nightHeight = 4.2f;
            var start = Place(layer, "HorizonNuitDebut", 0f, BuildingBase + 0.1f, nightHeight, 1);
            start.name = "Nuit_Horizon_Debut";
            float sw = start.sprite.bounds.size.x * start.transform.localScale.x;
            start.transform.localPosition = new Vector3(nightStart + sw / 2f, BuildingBase + 0.1f, 0f);
            Band(layer, "HorizonJour", min, nightStart + sw * 0.4f, BuildingBase + 0.1f, dayHeight, 0, HorizonTint);
            Band(layer, "HorizonNuit", nightStart + sw, max, BuildingBase + 0.1f, nightHeight, 1, null, "Nuit_Horizon");
        }

        /// <summary>Lointains : la lagune jusqu'au Plateau, où se dressent la Pyramide, la cathédrale Saint-Paul et une
        /// grande tour, puis d'autres immeubles de nuit.</summary>
        static void BuildFar(Transform layer)
        {
            const string sort = "BatimentsLointains";
            var (min, max) = LocalRange(FFar);
            var monuments = new (string prefab, float wx, float h)[] { ("Monument_01", 210f, 6.5f), ("Monument_02", 240f, 7f), ("Monument_03", 268f, 13f) };
            foreach (var m in monuments)
                Place(Zone(layer, FFar, FFar * m.wx), m.prefab, FFar * m.wx, BuildingBase, m.h, 0, false, null, sort).name = $"Nuit_{m.prefab}";
            float seaEnd = FFar * monuments[0].wx; // la Pyramide cache le bout de la lagune
            Band(Zone(layer, FFar, 0f), "Mer", min, seaEnd, -0.3f, 2.2f, -1, null, "Lagune_Lointaine", sort);
            // Immeubles de nuit au loin, plus petits et plus sombres.
            float x = FFar * 282f;
            int[] order = { 5, 2, 1, 4, 6, 3, 5, 2 };
            const float farScale = RoofScale * 0.5f;
            foreach (int a in order)
            {
                if (x > max) break;
                var art = RoofArt[a - 1];
                float w = art.w * farScale;
                var sr = Place(Zone(layer, FFar, x + w / 2f), $"Toit_0{a}", x + w / 2f, BuildingBase, art.h * farScale, -1, false, new Color(0.7f, 0.72f, 0.85f), sort);
                sr.name = $"Nuit_Immeuble_Lointain_{a}";
                x += w + 0.4f;
            }
        }

        /// <summary>Plan intermédiaire : l'Hôtel Ivoire et les immeubles du bord de la baie, la lagune (le pylône à haubans, unique, est sur le pont jouable)
        /// au loin, puis les immeubles du Plateau la nuit.</summary>
        static void BuildMid(Transform layer)
        {
            const string sort = "PlanIntermediaire";
            // Baie de Cocody (x local).
            var bay = new (string prefab, float x, float h, int order)[]
            {
                ("HotelTour", 6f, 20f, 0), ("HotelBas", 20f, 4.5f, 1), ("Baie_01", 44f, 11f, 0), ("Baie_03", 54f, 7f, 0), ("Baie_02", 67f, 14f, 0),
                ("Cocotier_01", 33f, 9.5f, 1), ("Cocotier_03", 37.5f, 9f, -1), ("Arbre", 61f, 7f, 1), ("Cocotier_02", 74f, 9.5f, -1),
            };
            foreach (var b in bay) Place(Zone(layer, FMid, b.x), b.prefab, b.x, BuildingBase, b.h, b.order, false, null, sort);

            // La lagune sous le pont, des bateaux, un pylône au loin ; la rive du Plateau la cache à droite.
            float seaStart = 70f;
            float x = FMid * 196f;
            float seaEnd = 0f;
            int[] order = { 5, 2, 4, 1, 6, 3, 5, 2, 4, 1 };
            const float midScale = RoofScale * 0.75f;
            var max = LocalRange(FMid).max;
            foreach (int a in order)
            {
                if (x > max) break;
                var art = RoofArt[a - 1];
                float w = art.w * midScale;
                if (seaEnd == 0f) seaEnd = x + w / 2f;
                var sr = Place(Zone(layer, FMid, x + w / 2f), $"Toit_0{a}", x + w / 2f, BuildingBase, art.h * midScale, 0, false, new Color(0.78f, 0.8f, 0.9f), sort);
                sr.name = $"Nuit_Immeuble_{a}";
                x += w + 0.3f;
            }
            var sea = Band(Zone(layer, FMid, seaStart), "Mer", seaStart, seaEnd, -0.5f, 2.2f, -2, null, "Lagune", sort);
            var fx = sea.gameObject.AddComponent<WaterFx>();
            fx.drift = 0.45f; fx.glintSize = 0.2f; fx.glintDepth = 1.2f; fx.casesPerGlint = 2.5f;
            LagoonTraffic(layer, seaStart + 2f, seaEnd - 3f);
        }

        /// <summary>Bateaux du stage 1 sur la lagune (x local), recyclés d'un bout à l'autre.</summary>
        static void LagoonTraffic(Transform layer, float fromX, float toX)
        {
            var go = new GameObject("Bateaux");
            go.transform.SetParent(layer, false);
            var lb = go.AddComponent<LagoonBoats>();
            lb.fromX = fromX;
            lb.toX = toX;
            Sprite S(string n) => CourseKit.LoadSprite($"{GameplayDir}Lagoon/{n}.png");
            var list = new (Sprite[] frames, float width, float y, float speed, bool right)[]
            {
                (new[] { S("ferry_0"), S("ferry_1"), S("ferry_2") }, 3.4f, 0.55f, 0.6f, true),
                (new[] { S("pinasse_0") }, 2.6f, 1.05f, -0.45f, true),
                (new[] { S("sail_0") }, 1.0f, 1.75f, 0.3f, true),
            };
            var boats = new List<LagoonBoats.Boat>();
            for (int i = 0; i < list.Length; i++)
            {
                var d = list[i];
                var sr = CourseKit.Renderer(go.transform, $"Bateau_{i + 1}", d.frames[0], -1, d.width, false);
                sr.sortingLayerName = "PlanIntermediaire";
                sr.transform.localPosition = new Vector3(fromX + (toX - fromX) * (0.15f + 0.7f * i / list.Length), d.y, 0f);
                boats.Add(new LagoonBoats.Boat { renderer = sr, frames = d.frames, speed = d.speed, facesRight = d.right });
            }
            lb.boats = boats.ToArray();
        }

        static void BuildStreet(Transform layer)
        {
            // Lampadaires du stage 1 et cocotiers le long de la promenade (rien sur le pont ni sur les toits).
            for (float wx = 10f; wx <= 100f; wx += 15f)
                Place(Zone(layer, FStreet, FStreet * wx), ((int)wx / 15) % 2 == 0 ? "Lampadaire_A" : "Lampadaire_B", FStreet * wx, BuildingBase, 8f);
            var palms = new (string prefab, float wx, float h, bool flip)[]
                { ("Cocotier_04", 25f, 6f, false), ("Cocotier_01", 58f, 10.5f, true), ("Cocotier_04", 88f, 6f, true), ("Cocotier_02", 336f, 10f, false) };
            foreach (var p in palms) Place(Zone(layer, FStreet, FStreet * p.wx), p.prefab, FStreet * p.wx, BuildingBase, p.h, 1, p.flip);
        }

        // ---------------------------------------------------------------- monde jouable

        /// <summary>Sols et collisions : le pavé de la promenade, le pont (rampes et tablier, troué), la rue du Plateau
        /// jusqu'au premier toit et celle de l'arrivée. Chaque tronçon plein descend jusqu'à 3 cases sous le sol : les
        /// trous sont des puits où l'on tombe dans la lagune.</summary>
        static Transform BuildGround(Transform world)
        {
            var ground = new GameObject("Sol").transform;
            ground.SetParent(world, false);
            GroundSpan(ground, "SolPave", -2f, BridgeStart + 0.6f, PavedTop);
            GroundSpan(ground, "SolAsphalte", BridgeEnd - 0.6f, FirstRoof + 0.5f, AsphaltTop);
            float lastRoof = roofs[roofs.Count - 1].x1;
            GroundSpan(ground, "SolAsphalte", lastRoof - 0.5f, LevelLength + 2f, AsphaltTop);

            // Profil de la chaussée (points du dessus), coupé aux trous.
            var profile = new List<Vector2> { new Vector2(-1f, 0f), new Vector2(BridgeStart, 0f), new Vector2(RampUpEnd, DeckRise),
                new Vector2(RampDownStart, DeckRise), new Vector2(BridgeEnd, 0f), new Vector2(FirstRoof + 0.5f, 0f) };
            float from = -1f;
            foreach (var h in Holes.Concat(new[] { (x0: FirstRoof + 0.5f, x1: FirstRoof + 0.5f) }))
            {
                var top = new List<Vector2> { new Vector2(from, DeckY(from)) };
                top.AddRange(profile.Where(p => p.x > from && p.x < h.x0));
                top.Add(new Vector2(h.x0, DeckY(h.x0)));
                var poly = new List<Vector2>(top) { new Vector2(h.x0, -3f), new Vector2(from, -3f) };
                var go = new GameObject($"Collision_x{from:0.#}-{h.x0:0.#}");
                go.transform.SetParent(ground, false);
                var pc = go.AddComponent<PolygonCollider2D>();
                pc.points = poly.ToArray();
                pc.sharedMaterial = frictionless;
                from = h.x1;
            }
            SolidBox(ground, "Collision_Rue_Arrivee", lastRoof - 0.5f, LevelLength + 1f);
            Wall(ground, "Mur_Debut", -0.5f);
            Wall(ground, "Mur_Fin", LevelLength + 0.5f);
            return ground;
        }

        /// <summary>Le pont Alassane-Ouattara : rampes et tablier (ton image répétée, inclinée sur les rampes), troué par
        /// les travaux ; piles dans la lagune, pylône à haubans derrière la chaussée.</summary>
        static Transform BuildBridge(Transform world)
        {
            var group = new GameObject("Pont_Alassane_Ouattara").transform;
            group.SetParent(world, false);
            // Tronçons de tablier : (début, fin) de chaussée, plats ou en pente.
            var pieces = new List<(float x0, float x1)> { (BridgeStart, RampUpEnd) };
            float x = RampUpEnd;
            foreach (var h in Holes) { pieces.Add((x, h.x0)); x = h.x1; }
            pieces.Add((x, RampDownStart));
            pieces.Add((RampDownStart, BridgeEnd));
            foreach (var p in pieces) DeckPiece(group, p.x0, p.x1);

            float deckBottom = DeckRise + DeckFeet - DeckHeight;
            var sea = Band(group, "Mer", BridgeStart - 2f, BridgeEnd + 2f, -2.8f, 2.5f, -1, null, "Lagune_Sous_Pont", "Monde");
            var fx = sea.gameObject.AddComponent<WaterFx>();
            fx.drift = 0.7f; fx.glintSize = 0.35f; fx.glintDepth = 0.5f; fx.casesPerGlint = 3.5f;
            const float pileWidth = 3f, pileHeight = pileWidth * 933f / 561f;
            foreach (float px in new[] { 130f, 148f, 168f, 186f })
            {
                var pile = Place(group, "Pile", px, deckBottom - pileHeight, pileHeight, 0);
                pile.name = $"Pile_x{px:0}";
            }
            // Pylône à haubans, posé sur le tablier derrière Yao : sa base est cachée par la rambarde.
            Place(group, "Pylone", 150f, DeckRise, 12f, 0);
            return group;
        }

        /// <summary>Un tronçon de tablier entre x0 et x1, incliné si la chaussée y monte ou descend ; il dépasse un peu
        /// aux deux bouts pour couvrir les cassures de pente.</summary>
        static void DeckPiece(Transform parent, float x0, float x1)
        {
            var a = new Vector2(x0, DeckY(x0));
            var b = new Vector2(x1, DeckY(x1));
            var d = b - a;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            bool sloped = Mathf.Abs(angle) > 0.1f;
            float len = d.magnitude + (sloped ? 0.4f : 0f);
            var sr = Band(parent, "Tablier", -len / 2f, len / 2f, 0f, DeckHeight, 1, null, $"Tablier_x{x0:0.#}-{x1:0.#}", "Monde");
            // Le pivot est en bas au centre : on place la ligne de la chaussée (DeckFeet sous le haut) au milieu du tronçon.
            var rot = Quaternion.Euler(0f, 0f, angle);
            var mid = (a + b) / 2f;
            sr.transform.localRotation = rot;
            sr.transform.localPosition = (Vector3)mid - rot * new Vector3(0f, DeckHeight - DeckFeet, 0f);
        }

        static void ComputeRoofs()
        {
            float cursor = FirstRoof;
            foreach (var r in RoofPath)
            {
                var a = RoofArt[r.art - 1];
                cursor += r.gap;
                roofs.Add((cursor + a.x0 * RoofScale, cursor + (a.x1 + 1) * RoofScale, r.top));
                cursor += a.w * RoofScale;
            }
        }

        /// <summary>Les toits du Plateau, de nuit : chaque immeuble descend hors de l'écran ; on saute de toit en toit,
        /// et tomber entre deux immeubles coûte une vie.</summary>
        static void BuildRoofs(Transform world)
        {
            var group = new GameObject("Toits_Plateau").transform;
            group.SetParent(Zone(world, 1f, FirstRoof + 1f), false);
            float cursor = FirstRoof;
            for (int i = 0; i < RoofPath.Length; i++)
            {
                var r = RoofPath[i];
                var a = RoofArt[r.art - 1];
                cursor += r.gap;
                float w = a.w * RoofScale, h = a.h * RoofScale;
                var sr = Place(group, $"Toit_0{r.art}", cursor + w / 2f, r.top - (a.h - a.roof) * RoofScale, h, 0);
                sr.name = $"Nuit_Toit_{i + 1}";
                var roof = roofs[i];
                var box = sr.gameObject.AddComponent<BoxCollider2D>();
                float s = sr.transform.localScale.x;
                // Boîte en coordonnées de l'image (pivot en bas au centre) : du rebord du toit jusqu'à 16 cases plus bas.
                box.size = new Vector2((roof.x1 - roof.x0) / s, 16f / s);
                box.offset = new Vector2((((roof.x0 + roof.x1) / 2f) - (cursor + w / 2f)) / s, ((a.h - a.roof) * RoofScale - 8f) / s);
                box.sharedMaterial = frictionless;
                cursor += w;
            }
        }

        static Transform BuildProps(Transform world)
        {
            var props = new GameObject("Promenade_Et_Rue").transform;
            props.SetParent(world, false);
            var items = new (string prefab, float x, float h, bool flip, int order)[]
            {
                // Promenade de la baie : bancs, pots et massifs du stage 1.
                ("Mobilier_BancDossier", 12f, 1.0f, false, 2), ("Plante_Hibiscus", 20f, 1.5f, false, 2), ("Mobilier_Banc", 34f, 0.8f, false, 2),
                ("Mobilier_Pots", 46f, 0.8f, false, 2), ("Plante_Bougainvillier", 60f, 1.5f, false, 2), ("Mobilier_BancDossier", 76f, 1.0f, true, 2),
                ("Mobilier_BacBeton", 84f, 0.9f, false, 2), ("Plante_Herbes", 92f, 0.9f, false, 2), ("Plante_Hibiscus", 104f, 1.5f, true, 2),
                // Entrée du pont, travaux au bord des trous (cônes et barrières du stage 2).
                ("Route_Balise", 108f, 0.9f, false, 3), ("Route_Balise", 109.2f, 0.9f, false, 3),
                // Rue de l'arrivée.
                ("Route_Balise", 326f, 0.9f, false, 3), ("Plante_Herbes", 345f, 0.9f, false, 2),
            };
            foreach (var i in items) Place(Zone(props, 1f, i.x), i.prefab, i.x, BackOfStreet, i.h, i.order, i.flip);
            foreach (var h in Holes)
            {
                Place(Zone(props, 1f, h.x0), "Route_Barriere", h.x0 - 0.8f, DeckY(h.x0) + BackOfStreet, 1.0f, 3);
                Place(Zone(props, 1f, h.x0), "Route_Cone", h.x0 - 2f, DeckY(h.x0) + BackOfStreet, 0.7f, 3);
                Place(Zone(props, 1f, h.x1), "Route_Barriere", h.x1 + 0.8f, DeckY(h.x1) + BackOfStreet, 1.0f, 3, true);
            }
            // Obstacles dans la descente seulement : des blocs de béton du chantier, posés sur la pente, à sauter (la
            // descente donne de la vitesse : il faut sauter au bon moment).
            foreach (float bx in new[] { 190f, 195.5f })
            {
                float y = DeckY(bx);
                float slope = bx < RampUpEnd ? DeckRise / (RampUpEnd - BridgeStart) : -DeckRise / (BridgeEnd - RampDownStart);
                var block = Place(Zone(props, 1f, bx), "Route_BlocBeton", bx, y - 0.05f, 0.8f, 4);
                block.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan(slope) * Mathf.Rad2Deg);
                block.name = $"Obstacle_Beton_x{bx:0.#}";
                var box = block.gameObject.AddComponent<BoxCollider2D>();
                var size = block.sprite.bounds.size;
                box.size = new Vector2(size.x * 0.9f, size.y * 0.85f);
                box.offset = new Vector2(0f, size.y * 0.85f / 2f);
                box.sharedMaterial = frictionless;
                block.gameObject.AddComponent<Obstacle>(); // percuté en skate : Yao tombe et perd son skate
            }
            return props;
        }

        /// <summary>
        /// Le parcours, d'après le stage 3 du téléphone étiré sur 350 cases. Danger des ennemis 28 (agame et crabe 1,
        /// mouette 1,5, taxi 2 ; cible du téléphone pour le stage 3 : 26 ± 20 %), en montée : la baie (8), le pont (8,
        /// avec ses rampes et les blocs de béton de la descente), les toits (12). Pas d'images de vautour : des mouettes à leur place. Un seul point de contrôle, au milieu
        /// (case 175, sur le pont).
        /// </summary>
        static void BuildCourse(Transform world)
        {
            var course = new GameObject("Parcours").transform;
            course.SetParent(world, false);
            Transform In(float x) => Zone(course, 1f, x);
            var R = roofs;

            var stacks = new (float x, int h)[] { (26f, 1), (27f, 2) };
            int variant = 0;
            foreach (var st in stacks) CourseKit.Stack(In(st.x), st.x, st.h, variant++);
            var planks = new (float x, float y, int n)[] { (54f, 3f, 2), (146f, DeckRise + 2.6f, 1), (150.2f, DeckRise + 5f, 1) };
            foreach (var p in planks) CourseKit.Plank(In(p.x), p.x, p.y, p.n);

            var blocks = new List<(BumpBlock.Kind kind, float x, float y)>
            {
                (BumpBlock.Kind.CoinBox, 8f, CourseKit.BlockY),
                (BumpBlock.Kind.Brick, 13f, CourseKit.BlockY), (BumpBlock.Kind.CoinBox, 14f, CourseKit.BlockY),
                (BumpBlock.Kind.PowerBox, 15f, CourseKit.BlockY), (BumpBlock.Kind.Brick, 16f, CourseKit.BlockY),
                (BumpBlock.Kind.CoinBox, 100f, CourseKit.BlockY), (BumpBlock.Kind.PowerBox, 101f, CourseKit.BlockY), (BumpBlock.Kind.CoinSack, 102f, CourseKit.BlockY),
                (BumpBlock.Kind.CoinBox, 127f, DeckRise + CourseKit.BlockY), (BumpBlock.Kind.Brick, 128f, DeckRise + CourseKit.BlockY), (BumpBlock.Kind.CoinSack, 129f, DeckRise + CourseKit.BlockY),
                (BumpBlock.Kind.CoinBox, R[2].x0 + 13f, R[2].top + CourseKit.BlockY),   // sur le grand toit
            };
            foreach (var b in blocks) CourseKit.Block(In(b.x), b.kind, b.x, b.y);

            var items = new List<(Collectible.Kind kind, float x, float y)>();
            void Line(Collectible.Kind kind, float x0, float x1, float step, float y)
            {
                for (float x = x0; x <= x1 + 0.01f; x += step) items.Add((kind, x, y));
            }
            var C = Collectible.Kind.Coin;
            var B = Collectible.Kind.Banana;
            items.Add((C, 14f, 5.6f));
            Line(C, 30f, 33f, 1f, 1.2f);
            Line(C, 41f, 43f, 1f, 3.2f);
            Line(C, 55f, 58f, 1f, 4f);
            Line(C, 70f, 73f, 1f, 1.2f);
            foreach (var h in Holes) Line(C, h.x0 + 0.5f, h.x1 - 0.5f, 1f, DeckRise + 2.6f); // au-dessus des trous du tablier
            Line(C, 147f, 148f, 1f, DeckRise + 3.6f);
            Line(C, 151.2f, 152.2f, 1f, DeckRise + 6f);
            Line(C, 112f, 116f, 1f, 0f); // le long de la rampe qui monte (hauteurs corrigées plus bas)
            Line(B, 187f, 189f, 1f, 0f); // au-dessus de la descente
            for (int i = 0; i < items.Count; i++)
                if (items[i].y == 0f) items[i] = (items[i].kind, items[i].x, DeckY(items[i].x) + 1.2f);
            Line(C, R[0].x0 + 3f, R[0].x1 - 2f, 1.5f, R[0].top + 1.2f);
            Line(B, R[2].x0 + 2f, R[2].x0 + 6f, 1f, R[2].top + 1.2f);
            items.Add((Collectible.Kind.Diamond, (R[3].x0 + R[3].x1) / 2f, R[3].top + 4.5f)); // au-dessus du plus haut toit
            Line(C, R[4].x0 + 3f, R[4].x1 - 3f, 2f, R[4].top + 1.2f);
            Line(C, R[5].x0 + 3f, R[5].x1 - 3f, 2f, R[5].top + 1.2f);
            Line(B, 330f, 334f, 1f, 1.2f);
            foreach (var it in items) CourseKit.Item(In(it.x), it.kind, it.x, it.y);

            var E = new (Enemy.Kind kind, float x, float y)[]
            {
                // La baie (8)
                (Enemy.Kind.Agama, 20f, 0f), (Enemy.Kind.Gull, 36f, 3.4f), (Enemy.Kind.Crab, 50f, 0f), (Enemy.Kind.Agama, 66f, 0f),
                (Enemy.Kind.Gull, 80f, 3.2f), (Enemy.Kind.Taxi, 95f, 0f),
                // Le pont en travaux (9) : les taxis font demi-tour au bord des trous ; des mouettes au-dessus de deux trous.
                (Enemy.Kind.Taxi, 130f, DeckRise), (Enemy.Kind.Agama, 145f, DeckRise), (Enemy.Kind.Gull, 159.75f, DeckRise + 3.4f),
                (Enemy.Kind.Taxi, 168f, DeckRise), (Enemy.Kind.Gull, 177.5f, DeckRise + 3.4f),
                // Les toits (12)
                (Enemy.Kind.Agama, R[0].x0 + 8f, R[0].top), (Enemy.Kind.Crab, R[1].x0 + 6f, R[1].top), (Enemy.Kind.Gull, R[1].x1 + 1.2f, R[1].top + 2.5f),
                (Enemy.Kind.Agama, R[2].x0 + 8f, R[2].top), (Enemy.Kind.Agama, R[2].x0 + 19f, R[2].top), (Enemy.Kind.Crab, R[3].x0 + 9f, R[3].top),
                (Enemy.Kind.Agama, R[4].x0 + 8f, R[4].top), (Enemy.Kind.Gull, R[4].x1 + 1.5f, R[5].top + 2f), (Enemy.Kind.Agama, R[5].x0 + 10f, R[5].top),
                (Enemy.Kind.Taxi, 338f, 0f),
            };
            foreach (var e in E) CourseKit.EnemyAt(In(e.x), e.kind, e.x, e.y);

            CourseKit.Flag(In(172f), 172f, 3.5f, false, DeckRise); // un seul point de contrôle, au milieu, sur le pont
            CourseKit.Flag(In(GoalX), GoalX, 5.5f, true);

            var catalog = MenuBuilder.Catalog();
            var stage = catalog.stages[2];
            stage.scene = System.IO.Path.GetFileNameWithoutExtension(ScenePath);
            stage.tagline = Tagline;
            stage.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SuperYao/UI/Backgrounds/thumb_cocody.jpg");
            stage.coins = items.Count(i => i.kind == C) + blocks.Count(k => k.kind == BumpBlock.Kind.CoinBox)
                          + blocks.Count(k => k.kind == BumpBlock.Kind.CoinSack) * 8;
            stage.bananas = items.Count(i => i.kind == B);
            stage.diamonds = items.Count(i => i.kind == Collectible.Kind.Diamond);
            EditorUtility.SetDirty(catalog);
        }

        static void BuildForeground(Transform layer)
        {
            var plants = new (string prefab, float wx, float h, bool flip)[]
                { ("Plante_Herbes", 10f, 1.3f, false), ("Plante_BuissonRond", 45f, 1.4f, false), ("Plante_Herbes", 85f, 1.3f, true), ("Plante_Herbes", 340f, 1.3f, false) };
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

        static void BuildSettings()
        {
            var settings = new GameObject("Reglages_Stage");
            settings.AddComponent<StageSettings>();
            settings.AddComponent<TouchControls>().fireIcon = CourseKit.LoadSprite($"{GameplayDir}Items/fireball.png");
            settings.AddComponent<StageState>().LevelNumber = 3;
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

        static Sprite[] Frames(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new Sprite[0];
            return AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
        }

        static void CreatePrefabs()
        {
            // Stage 1
            SpritePrefab("Ciel", Art1 + "Sky/sky.jpg", "Ciel");
            for (int i = 1; i <= 3; i++) SpritePrefab($"Nuage_0{i}", Art1 + $"Clouds/cloud_0{i}.png", "Nuages");
            SpritePrefab("Lampadaire_A", Art1 + "Props/lamp_01.png", "Rue");
            SpritePrefab("Lampadaire_B", Art1 + "Props/lamp_03.png", "Rue");
            SpritePrefab("SolPave", Art1 + "Ground/ground_paved.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("Mobilier_Banc", Art1 + "Furniture/furniture_01.png", "Monde");
            SpritePrefab("Mobilier_BancDossier", Art1 + "Furniture/furniture_02.png", "Monde");
            SpritePrefab("Mobilier_Pots", Art1 + "Furniture/furniture_05.png", "Monde");
            SpritePrefab("Mobilier_BacBeton", Art1 + "Furniture/furniture_06.png", "Monde");
            SpritePrefab("Plante_BuissonRond", Art1 + "Plants/plant_01.png", "Monde");
            SpritePrefab("Plante_Hibiscus", Art1 + "Plants/plant_04.png", "Monde");
            SpritePrefab("Plante_Bougainvillier", Art1 + "Plants/plant_06.png", "Monde");
            SpritePrefab("Plante_Herbes", Art1 + "Plants/plant_08.png", "Monde");
            // Stage 2
            SpritePrefab("Mer", Art2 + "Water/sea.jpg", "PlanIntermediaire", SpriteDrawMode.Tiled);
            SpritePrefab("SolAsphalte", Art2 + "Ground/ground_asphalt.jpg", "Monde", SpriteDrawMode.Tiled);
            for (int i = 1; i <= 4; i++) SpritePrefab($"Cocotier_0{i}", Art2 + $"Vegetation/palm_0{i}.png", "Rue");
            SpritePrefab("Arbre", Art2 + "Vegetation/tree_01.png", "Rue");
            SpritePrefab("Route_Cone", Art2 + "Road/road_01.png", "Monde");
            SpritePrefab("Route_Barriere", Art2 + "Road/road_03.png", "Monde");
            SpritePrefab("Route_Balise", Art2 + "Road/road_05.png", "Monde");
            SpritePrefab("Route_BlocBeton", Art2 + "Road/road_06.png", "Monde");
            // Stage 3
            SpritePrefab("CielCoucher", Art + "Sky/sky_sunset.jpg", "Ciel");
            SpritePrefab("CielNuit", Art + "Sky/sky_night.jpg", "Ciel");
            SpritePrefab("Lune", Art + "Sky/moon.png", "Ciel");
            SpritePrefab("HorizonJour", Art + "Horizon/horizon_plateau_jour.png", "Horizon", SpriteDrawMode.Tiled);
            SpritePrefab("HorizonNuit", Art + "Horizon/horizon_plateau_nuit.png", "Horizon", SpriteDrawMode.Tiled);
            SpritePrefab("HorizonNuitDebut", Art + "Horizon/horizon_plateau_nuit_start.png", "Horizon");
            SpritePrefab("HotelTour", Art + "Buildings/hotel_ivoire_tower.png", "PlanIntermediaire");
            SpritePrefab("HotelBas", Art + "Buildings/hotel_ivoire_low.png", "PlanIntermediaire");
            for (int i = 1; i <= 3; i++)
            {
                SpritePrefab($"Baie_0{i}", Art + $"Buildings/bay_0{i}.png", "PlanIntermediaire");
                SpritePrefab($"Monument_0{i}", Art + $"Buildings/landmark_0{i}.png", "BatimentsLointains");
            }
            for (int i = 1; i <= 6; i++) SpritePrefab($"Toit_0{i}", Art + $"Roofs/roof_0{i}.png", "Monde");
            SpritePrefab("Tablier", Art + "Bridge/deck.png", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("Pile", Art + "Bridge/pillar_big.png", "Monde");
            SpritePrefab("Pylone", Art + "Bridge/pylon.png", "Monde");
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
