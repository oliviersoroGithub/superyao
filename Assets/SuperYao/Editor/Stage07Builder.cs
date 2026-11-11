using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Construit le stage 7, « Yamoussoukro » (menu Super Yao > Construire le stage 7), en plein jour, au bord des lacs :
    /// les jardins de l'hôtel Président (premier lac à traverser à la nage, poissons-chats, jets d'eau), l'avenue, le
    /// palais présidentiel et son lac aux caïmans (traversé en pirogue), ses jardins (singe sur une branche), puis la
    /// basilique Notre-Dame de la Paix : un lac profond (poissons-tigres, diamant au fond) et les marches de l'arrivée.
    /// Nouveautés : la nage (SwimWater, HeroController), les poissons et les caïmans (Enemy), les jets d'eau (Fountain).
    /// Reprend les stages précédents : ciel, nuages, oiseaux, pirogue et pavé du stage 1, asphalte, cocotiers et arbre du
    /// stage 2, branches et singes du stage 6, Yao à pied et son skate, CourseKit. Pas de boss, comme sur le téléphone.
    /// </summary>
    public static class Stage07Builder
    {
        const string Art = "Assets/SuperYao/Backgrounds/Stage7/";
        const string Art6 = "Assets/SuperYao/Backgrounds/Stage6/";
        const string Art2 = "Assets/SuperYao/Backgrounds/Stage2/";
        const string Art1 = "Assets/SuperYao/Backgrounds/Stage1/";
        const string PrefabDir = "Assets/SuperYao/Prefabs/Stage7";
        const string ScenePath = "Assets/SuperYao/Scenes/Stage07_Yamoussoukro.unity";
        const string HeroPrefab = "Assets/SuperYao/Prefabs/Heros/Heros_Yao.prefab";
        const string GameplayDir = "Assets/SuperYao/Gameplay/";
        const string FrictionlessPath = "Assets/SuperYao/Physics/SansFrottement.physicsMaterial2D";
        const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string Tagline = "Bienvenue dans la capitale ! Nage dans les lacs de Yamoussoukro, gare-toi des caïmans du palais et grimpe jusqu'à la basilique !";

        const float FSky = 0f, FClouds = 0.05f, FHorizon = 0.15f, FMid = 0.65f, FStreet = 0.9f, FFront = 1.1f;
        const float VisibleRows = 20f;
        const float HalfViewMin = VisibleRows / 2f * 4f / 3f, HalfViewMax = VisibleRows / 2f * 2.4f;

        // Parcours (350 cases) : jardins de l'hôtel Président | palais présidentiel | basilique.
        // Parcours de 420 cases (à partir du stage 7, les stages sont plus longs que les 350 cases des premiers). Les
        // parties d'origine sont décalées par S() ; trois passages nouveaux y sont insérés (voir BuildCourse).
        const float LevelLength = 420f, PalaceStart = 130f, BasilicaStart = 275f, GoalX = 415f;
        /// <summary>Position dans le parcours d'origine (350 cases) → position dans le parcours long : +20 après le
        /// premier lac (arbre et échelle), +25 après le lac aux caïmans (étang aux lianes), +25 avant le parvis (marché).</summary>
        static float S(float x) => x < 76f ? x : x <= 200f ? x + 20f : x < 290f ? x + 45f : x + 70f;
        static readonly float[] ZoneEnds = { PalaceStart, BasilicaStart, float.MaxValue };
        static readonly string[] ZoneNames = { "Z1_Hotel_President", "Z2_Palais", "Z3_Basilique" };
        const float GardenTop = 0.05f, /* terre rouge du stage 6 */ AsphaltTop = 0.2f, PavedTop = 0.2f + 5f / 160f, BackOfStreet = 0.15f;
        const float AvenueX0 = 104f, AvenueX1 = 132f, ParvisX0 = 370f;
        /// <summary>Surface des lacs (sous le bord des berges).</summary>
        const float Surface = -0.35f;
        // Les lacs : (début, fin, profondeur du fond). Le premier s'apprend, le deuxième a ses caïmans (pirogue), le troisième
        // est profond (poissons-tigres, diamant au fond).
        static readonly (float x0, float x1, float depth)[] Lakes = { (38f, 70f, 6f), (180f, 220f, 5f), (226f, 238f, 4f), (281f, 321f, 9f) };
        const float KillY = -16f;
        // Les marches de la basilique : 6 marches de 0,8 case, puis le parvis haut jusqu'à l'arrivée.
        const float StepsX = 388f, StepW = 1.5f, StepH = 0.8f, TentH = 3.6f;
        const int Steps = 6;
        const float TopY = Steps * StepH;
        // Pirogue (images du stage 1) : coque de 62 px de long sur 87, bord à 28 % de la hauteur de l'image.
        const float PirogueLength = 4f, PirogueHull = 62f / 87f, PirogueDeck = 0.28f;

        static Material spriteMaterial;
        static PhysicsMaterial2D frictionless;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        [MenuItem("Super Yao/Construire le stage 7 (Yamoussoukro)")]
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
                EditorUtility.DisplayDialog("Super Yao", "Lance d'abord « Construire le stage 1 » : il fabrique Yao, que le stage 7 réutilise.", "OK");
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
            BuildHorizon(Layer(decor, "03_Horizon_Yamoussoukro", FHorizon, 1f));
            BuildMid(Layer(decor, "05a_Hotel_Palais_Basilique", FMid, 1f));
            BuildStreet(Layer(decor, "05b_Palmiers", FStreet, 1f));

            var world = new GameObject("06_Monde_Jouable").transform;
            BuildGround(world);
            BuildProps(world);
            BuildCourse(world);
            var yao = (GameObject)PrefabUtility.InstantiatePrefab(hero, world);
            yao.transform.localPosition = new Vector3(4f, 0.02f, 0f);
            yao.AddComponent<HeroRespawn>().killY = KillY;
            Stage02Builder.PutOnSkate(yao);
            var anim = yao.GetComponentInChildren<HeroAnimator>();
            anim.hang = CourseKit.LoadSprites("Assets/SuperYao/Heroes/YaoLiane/hang_", 8);
            anim.climb = CourseKit.LoadSprites("Assets/SuperYao/Heroes/YaoEchelle/climb_", 8);
            anim.swim = CourseKit.LoadSprites("Assets/SuperYao/Heroes/YaoNage/swim_", 8);
            anim.tread = CourseKit.LoadSprites("Assets/SuperYao/Heroes/YaoNage/tread_", 4);
            BuildForeground(Layer(new GameObject("07_Avant_Plan").transform, "Vegetation_Avant_Plan", FFront, 1f));

            BuildCamera(yao.transform);
            BuildSettings();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log($"Super Yao : stage 7 construit ({LevelLength:0} cases) dans {ScenePath}.");
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

        /// <summary>Horizon : la ville basse et le lac, répétés bord à bord. Le dôme de la basilique (découpé de la même
        /// image) n'y est posé qu'une fois, au début : on le voit au loin depuis l'hôtel et le palais, et il est sorti de
        /// l'écran quand on arrive devant la vraie basilique (sinon il revenait à chaque répétition de la bande).</summary>
        static void BuildHorizon(Transform layer)
        {
            var (min, max) = LocalRange(FHorizon);
            const float baseY = -1.2f, height = 6.5f, k = height / 418f; // 418 px : hauteur de la bande
            Band(layer, "HorizonYakro", min, max, baseY, height, 0);
            // Dôme : 255 px du haut de la bande ; en x local 8, il quitte l'écran avant la case 213 (avant la basilique).
            Place(layer, "DomeLointain", 8f, baseY + (418f - 255f) * k, 255f * k, 1);
        }

        /// <summary>Plan intermédiaire (x local) : la tour de l'hôtel Président, le palais présidentiel, la basilique, et
        /// des cocotiers entre eux.</summary>
        static void BuildMid(Transform layer)
        {
            const string sort = "PlanIntermediaire";
            var items = new (string prefab, float wx, float h, bool flip, int order)[]
            {
                ("Hotel", 22f, 15f, false, 0), ("Cocotier_02", 8f, 9f, false, 1), ("Cocotier_01", 44f, 9.5f, true, 1), ("Cocotier_04", 92f, 6f, false, 1),
                ("Palais", 140f, 8.5f, false, 0), ("Cocotier_03", 112f, 10f, false, 1), ("Arbre", 178f, 7f, true, 1), ("Cocotier_02", 214f, 9f, true, 1),
                ("Basilique", 312f, 15f, false, 0), ("Cocotier_01", 262f, 9.5f, false, 1), ("Cocotier_04", 345f, 6f, true, 1),
            };
            foreach (var i in items)
            {
                float lx = FMid * S(i.wx);
                Place(Zone(layer, FMid, lx), i.prefab, lx, 0f, i.h, i.order, i.flip, null, sort);
            }
        }

        static void BuildStreet(Transform layer)
        {
            var palms = new (string prefab, float wx, float h, bool flip)[]
                { ("Cocotier_04", 26f, 6f, false), ("Cocotier_01", 120f, 10.5f, true), ("Cocotier_03", 222f, 10f, false), ("Cocotier_04", 292f, 6f, true) };
            foreach (var p in palms) Place(Zone(layer, FStreet, FStreet * S(p.wx)), p.prefab, FStreet * S(p.wx), 0f, p.h, 1, p.flip);
        }

        // ---------------------------------------------------------------- monde jouable

        static void BuildGround(Transform world)
        {
            var ground = new GameObject("Sol").transform;
            ground.SetParent(world, false);
            // Terre ferme entre les lacs : jardin, avenue d'asphalte, parvis pavé ; berges profondes jusqu'au fond des lacs.
            var land = new List<(float a, float b)>();
            float from = -2f;
            foreach (var l in Lakes) { land.Add((from, l.x0)); from = l.x1; }
            land.Add((from, LevelLength + 2f));
            foreach (var (a, b) in land)
            {
                SolidBox(ground, $"Collision_x{a:0}-{b:0}", Mathf.Max(a, -1f), Mathf.Min(b, LevelLength + 1f), 0f, -KillY);
                foreach (var (s0, s1, kind) in Surfaces(a, b))
                {
                    if (kind == 0) Garden(ground, s0, s1);
                    else GroundSpan(ground, kind == 1 ? "SolAsphalte" : "SolPave", s0, s1, kind == 1 ? AsphaltTop : PavedTop);
                }
                Soil(ground, "SolTerreRouge", a, b, -3.2f, KillY + 2f, Color.white);
            }
            foreach (var l in Lakes) Lake(ground, l.x0, l.x1, l.depth);
            Wall(ground, "Mur_Debut", -0.5f);
            Wall(ground, "Mur_Fin", LevelLength + 0.5f);
        }

        /// <summary>Morceaux de sol entre a et b : 0 = jardin, 1 = asphalte de l'avenue, 2 = pavé du parvis.</summary>
        static IEnumerable<(float, float, int)> Surfaces(float a, float b)
        {
            var cuts = new (float x, int kind)[] { (-10f, 0), (AvenueX0, 1), (AvenueX1, 0), (ParvisX0, 2), (999f, 2) };
            for (int i = 0; i < cuts.Length - 1; i++)
            {
                float s0 = Mathf.Max(a, cuts[i].x), s1 = Mathf.Min(b, cuts[i + 1].x);
                if (s1 > s0) yield return (s0 - (s0 > a ? 0.6f : 0f), s1, cuts[i].kind);
            }
        }

        /// <summary>Gazon du jardin : terre et bordure d'herbe.</summary>
        static void Garden(Transform ground, float x0, float x1)
        {
            // Terre rouge et herbe du stage 6 (la terre du stage 7 est réservée au fond des lacs).
            GroundSpan(ground, "SolTerreRouge", x0, x1, GardenTop);
            var grass = Band(ground, "SolHerbe", x0, x1, GardenTop - 14f / 160f, 55f / 160f, 1);
            grass.name = $"Herbe_x{x0:0}-{x1:0}";
        }

        /// <summary>Terre en profondeur (berges et fond des lacs) : bandes empilées de top à bottom.</summary>
        static void Soil(Transform ground, string prefab, float x0, float x1, float top, float bottom, Color tint)
        {
            float h = prefabs[prefab].GetComponent<SpriteRenderer>().sprite.bounds.size.y;
            for (float t = top; t > bottom; t -= h - 0.1f)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab], ground);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.size = new Vector2(x1 - x0, h);
                sr.color = tint;
                sr.sortingOrder = -1;
                go.transform.localPosition = new Vector3((x0 + x1) / 2f, t - h, 0f);
                go.name = $"Terre_x{x0:0}-{x1:0}_y{t:0.#}";
            }
        }

        /// <summary>Un lac où l'on nage : fond de terre, eau vue de dessous derrière Yao, voile d'eau devant lui, surface
        /// ondulée, et la zone de nage (SwimWater).</summary>
        static void Lake(Transform ground, float x0, float x1, float depth)
        {
            var lake = new GameObject($"Lac_x{x0:0}-{x1:0}").transform;
            lake.SetParent(ground, false);
            SolidBox(lake, "Fond", x0, x1, -depth, 4f);
            // Le fond du lac : la terre moussue du stage 7, un peu bleutée par l'eau.
            Soil(lake, "SolJardin", x0, x1, -depth + 0.05f, KillY + 2f, new Color(0.8f, 0.9f, 0.88f));
            var moss = Band(lake, "SolJardinHerbe", x0, x1, -depth + 0.05f - 14f / 160f, 49f / 160f, 0);
            moss.color = new Color(0.8f, 0.9f, 0.88f);
            moss.name = "Fond_Mousse";
            float h = depth + Surface;
            var back = Band(lake, "SousLEau", x0 - 0.1f, x1 + 0.1f, -depth, h, -6);
            back.name = "Eau_Fond";
            var veil = Band(lake, "SousLEau", x0 - 0.1f, x1 + 0.1f, -depth, h, 60);
            veil.name = "Eau_Voile";
            veil.color = new Color(1f, 1f, 1f, 0.28f);
            var top = Band(lake, "Mer", x0 - 0.1f, x1 + 0.1f, Surface - 0.4f, 0.4f, 61);
            top.name = "Eau_Surface";
            top.color = new Color(0.7f, 0.95f, 0.95f, 0.8f);
            var fx = top.gameObject.AddComponent<WaterFx>();
            fx.drift = 0.5f; fx.glintSize = 0.25f; fx.glintDepth = 0.3f; fx.casesPerGlint = 3f;
            var zone = new GameObject("Zone_De_Nage");
            zone.transform.SetParent(lake, false);
            var box = zone.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.offset = new Vector2((x0 + x1) / 2f, (-depth + Surface) / 2f);
            box.size = new Vector2(x1 - x0, depth + Surface);
            zone.AddComponent<SwimWater>();
        }

        /// <summary>Décor au fond : murs, grilles et portail du palais, lampadaires de l'avenue, plantes.</summary>
        static void BuildProps(Transform world)
        {
            var props = new GameObject("Rue_Et_Palais").transform;
            props.SetParent(world, false);
            // Clôture du palais : (image, x, hauteur).
            var fence = new (string prefab, float x, float h)[]
            {
                ("Pilier", 113f, 2.2f), ("Grille", 115.4f, 2.2f), ("Grille", 118.2f, 2.2f), ("Pilier", 120.6f, 2.2f), ("Mur", 123.4f, 2f),
                ("Portail", 129f, 2.4f), ("Mur", 134.6f, 2f), ("Pilier", 137.2f, 2.2f), ("Grille", 139.6f, 2.2f), ("Grille", 142.4f, 2.2f),
                ("Grille", 145.2f, 2.2f), ("Pilier", 147.6f, 2.2f), ("Mur", 151f, 2f), ("Pilier", 154f, 2.2f),
                ("Pilier", 203f, 2.2f), ("Grille", 205.4f, 2.2f), ("Grille", 208.2f, 2.2f), ("Pilier", 210.6f, 2.2f), ("Mur", 224f, 2f), ("Pilier", 226.6f, 2.2f),
            };
            foreach (var f in fence) Place(Zone(props, 1f, S(f.x)), f.prefab, S(f.x), BackOfStreet, f.h, 1);
            var items = new (string prefab, float x, float h, bool flip, int order)[]
            {
                ("Lampadaire", 86f, 4.4f, false, 1), ("Lampadaire", 98f, 4.4f, false, 1), ("Lampadaire", 110f, 4.4f, false, 1),
                ("Plante_Buisson", 30f, 1.2f, false, 5), ("Plante_Touffes", 74f, 1.1f, true, 5), ("Plante_Buisson", 158f, 1.2f, true, 5),
                ("Plante_Herbes", 232f, 1.1f, false, 5), ("Plante_Buisson", 280f, 1.2f, false, 5),
                ("Lampadaire", 304f, 4.4f, false, 1), ("Lampadaire", 316f, 4.4f, true, 1),
            };
            foreach (var i in items) Place(Zone(props, 1f, S(i.x)), i.prefab, S(i.x), BackOfStreet - (i.order == 5 ? 0.25f : 0f), i.h, i.order, i.flip);
            // Étals de bananes du marché des pèlerins (stage 6), derrière les bâches.
            foreach (var x in new[] { 338f, 347f, 356f })
            {
                var st = Place(Zone(props, 1f, x), "EtalBananes", x, BackOfStreet, 3f, 1, x > 350f);
                st.sortingLayerName = "PlanIntermediaire";
                st.sortingOrder = 9001;
            }
        }

        /// <summary>
        /// Le parcours (420 cases ; passages ajoutés : arbre et échelle, étang aux lianes, marché des pèlerins). Jardins de l'hôtel : le premier lac s'apprend (poissons-chats, deux jets d'eau qui
        /// propulsent vers des pièces), puis l'avenue (taxi). Palais : charrette, le lac aux caïmans traversé en pirogue
        /// (ils bondissent hors de l'eau ; tombé à l'eau, il faut nager vite), les jardins et le singe sur sa branche.
        /// Basilique : lac profond (poissons-tigres qui foncent, diamant au fond, jets d'eau), gbaka sur le parvis, les
        /// marches et l'arrivée en haut. Danger des ennemis 32,5 (poisson-chat et agame 1, mouette 1,5, poisson-tigre et
        /// taxi 2, caïman 2,25, singe et charrette 3, gbaka 4) + 5 dans les passages ajoutés (tortue, poisson-chat, agame 1, vautour 2) =
        /// 37,5 pour 420 cases. Un seul point de contrôle, au milieu.
        /// </summary>
        static void BuildCourse(Transform world)
        {
            var course = new GameObject("Parcours").transform;
            course.SetParent(world, false);
            Transform In(float x) => Zone(course, 1f, x);
            var items = new List<(Collectible.Kind kind, float x, float y)>();
            // Line : positions d'origine (décalées par S) ; LineN : positions du parcours long (passages nouveaux).
            void Line(Collectible.Kind kind, float x0, float x1, float step, float y)
            {
                for (float x = x0; x <= x1 + 0.01f; x += step) items.Add((kind, S(x), y));
            }
            void LineN(Collectible.Kind kind, float x0, float x1, float step, float y)
            {
                for (float x = x0; x <= x1 + 0.01f; x += step) items.Add((kind, x, y));
            }
            var C = Collectible.Kind.Coin;
            var B = Collectible.Kind.Banana;
            var D = Collectible.Kind.Diamond;

            // --- Jardins de l'hôtel Président
            Line(C, 20f, 24f, 1f, 1.2f);
            Line(C, 42f, 66f, 2f, -2.6f);                   // sous l'eau du premier lac
            Fountain(In(46f), 46f, 5f);
            Fountain(In(61f), 61f, 5.5f);
            Line(B, 45f, 47f, 1f, 6.6f);
            Line(C, 60f, 62f, 1f, 7.2f);
            // Nouveau (76-96) : un arbre, sa plate-forme et son échelle de corde (stage 6), une tortue.
            var tree = In(84f);
            Place(tree, "Arbre", 82f, BackOfStreet - 0.05f, 8f, -2);
            Shelf(tree, 84f, 4.4f, 3f);
            Ladder(tree, 85.1f, 4.4f);
            LineN(C, 83f, 85f, 1f, 5.6f);
            LineN(B, 85f, 87f, 1f, 8.4f);
            Line(C, 88f, 92f, 1f, 1.2f);
            CourseKit.Plank(In(S(102f)), S(102f), 3.4f, 1);
            Line(B, 103f, 105f, 1f, 4.6f);

            // --- Palais présidentiel
            Line(C, 126f, 130f, 1f, 1.2f);
            Pirogue(In(S(160f)), S(160f), S(200f), 2.6f);
            Line(C, 168f, 192f, 3f, 4f);                    // au-dessus de la pirogue (gare aux caïmans)
            // Nouveau (220-245) : l'étang du palais et ses deux lianes (stage 6), pour qui ne veut pas nager.
            var pond = In(232f);
            Place(pond, "Arbre", 224.6f, BackOfStreet - 0.05f, 9f, -2);
            Branch(pond, 224.9f, 231.5f, 8.8f);
            Place(pond, "Arbre", 239.4f, BackOfStreet - 0.05f, 9f, -2, true);
            Branch(pond, 231.8f, 239.1f, 9f, true);
            Liana(pond, 229.6f, 8.5f, 5f);
            Liana(pond, 234.4f, 8.7f, 5.2f);
            LineN(B, 229f, 235f, 1.5f, 6.4f);
            LineN(C, 228f, 236f, 2f, -2.4f);
            var garden = In(S(215f));
            Place(garden, "Arbre", S(211f), BackOfStreet - 0.05f, 7.5f, -2);
            Branch(garden, S(211.3f), S(218.5f), 3.8f);
            Line(C, 213f, 217f, 1f, 5f);

            // --- Basilique
            Line(C, 240f, 272f, 2f, -3f);
            items.Add((D, S(256f), -8.1f));                 // au fond du lac profond
            Fountain(In(S(244f)), S(244f), 5f);
            Fountain(In(S(268f)), S(268f), 6f);
            Line(B, 267f, 269f, 1f, 7.8f);
            Line(C, 282f, 286f, 1f, 1.2f);
            // Nouveau (335-360) : le marché des pèlerins, ses bâches qui font rebondir (stage 5).
            var market = In(346f);
            foreach (var (tx, art) in new[] { (341f, 1), (352f, 3) })
            {
                Tent(market, tx, art);
                LineN(C, tx - 1f, tx + 1f, 1f, TentH * 0.75f + 4f);
            }
            LineN(B, 345f, 348f, 1f, 1.2f);
            // Les marches de la basilique (pierre claire), puis le parvis haut jusqu'à l'arrivée.
            var steps = In(StepsX);
            for (int k = 0; k < Steps; k++) Step(steps, StepsX + k * StepW, (k + 1) * StepH, k == Steps - 1 ? LevelLength + 1f - (StepsX + k * StepW) : StepW);
            LineN(C, 400f, 406f, 1f, TopY + 1.2f);

            var blocks = new (BumpBlock.Kind kind, float x, float y)[]
            {
                (BumpBlock.Kind.CoinBox, 10f, CourseKit.BlockY), (BumpBlock.Kind.Brick, 11f, CourseKit.BlockY), (BumpBlock.Kind.PowerBox, 12f, CourseKit.BlockY), (BumpBlock.Kind.Brick, 13f, CourseKit.BlockY),
                (BumpBlock.Kind.CoinBox, 93f, CourseKit.BlockY), (BumpBlock.Kind.Brick, 94f, CourseKit.BlockY), (BumpBlock.Kind.CoinBox, 95f, CourseKit.BlockY),
                (BumpBlock.Kind.CoinSack, 146f, CourseKit.BlockY),
                (BumpBlock.Kind.Brick, 222f, CourseKit.BlockY), (BumpBlock.Kind.PowerBox, 223f, CourseKit.BlockY), (BumpBlock.Kind.Brick, 224f, CourseKit.BlockY),
                (BumpBlock.Kind.CoinBox, 296f, CourseKit.BlockY), (BumpBlock.Kind.Brick, 297f, CourseKit.BlockY), (BumpBlock.Kind.CoinBox, 298f, CourseKit.BlockY),
            };
            foreach (var b in blocks) CourseKit.Block(In(S(b.x)), b.kind, S(b.x), b.y);
            foreach (var it in items) CourseKit.Item(In(it.x), it.kind, it.x, it.y);

            var E = new (Enemy.Kind kind, float x, float y)[]
            {
                // Jardins de l'hôtel (6,5)
                (Enemy.Kind.Catfish, 48f, -4.2f), (Enemy.Kind.Catfish, 62f, -2.4f), (Enemy.Kind.Gull, 76f, 4f), (Enemy.Kind.Agama, 85f, 0f), (Enemy.Kind.Taxi, 100f, 0f),
                // Palais (11,5)
                (Enemy.Kind.Agama, 125f, 0f), (Enemy.Kind.Cart, 140f, 0f), (Enemy.Kind.Caiman, 172f, Surface), (Enemy.Kind.Caiman, 189f, Surface),
                (Enemy.Kind.Monkey, 215f, 3.8f),
                // Basilique (14,5)
                (Enemy.Kind.Tigerfish, 248f, -4.5f), (Enemy.Kind.Catfish, 258f, -2.8f), (Enemy.Kind.Tigerfish, 266f, -7f),
                (Enemy.Kind.Gbaka, 290f, 0f), (Enemy.Kind.Gull, 302f, 4.5f), (Enemy.Kind.Monkey, 312f, 0f), (Enemy.Kind.Agama, 338f, TopY),
            };
            foreach (var e in E) CourseKit.EnemyAt(In(S(e.x)), e.kind, S(e.x), e.y);
            // Passages nouveaux (5) : tortue sous l'arbre, poisson-chat de l'étang, agame et vautour au marché.
            var N = new (Enemy.Kind kind, float x, float y)[]
                { (Enemy.Kind.Turtle, 90f, 0f), (Enemy.Kind.Catfish, 232f, -3.2f), (Enemy.Kind.Agama, 346f, 0f), (Enemy.Kind.Vulture, 350f, 7f) };
            foreach (var e in N) CourseKit.EnemyAt(In(e.x), e.kind, e.x, e.y);

            CourseKit.Flag(In(223f), 223f, 3.5f, false); // un seul point de contrôle, au milieu (entre le lac aux caïmans et l'étang)
            CourseKit.Flag(In(GoalX), GoalX, 5.5f, true, TopY);

            var catalog = MenuBuilder.Catalog();
            var stage = catalog.stages[6];
            stage.scene = System.IO.Path.GetFileNameWithoutExtension(ScenePath);
            stage.tagline = Tagline;
            stage.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SuperYao/UI/Backgrounds/thumb_yakro.jpg");
            stage.coins = items.Count(i => i.kind == C) + blocks.Count(k => k.kind == BumpBlock.Kind.CoinBox)
                          + blocks.Count(k => k.kind == BumpBlock.Kind.CoinSack) * 8;
            stage.bananas = items.Count(i => i.kind == B);
            stage.diamonds = items.Count(i => i.kind == D);
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Jet d'eau qui jaillit de la surface du lac en x.</summary>
        static void Fountain(Transform parent, float x, float height)
        {
            var go = new GameObject($"Jet_D_Eau_x{x:0}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, Surface, 0f);
            go.AddComponent<BoxCollider2D>();
            var f = go.AddComponent<SuperYao.Fountain>();
            f.height = height;
            f.material = spriteMaterial;
        }

        /// <summary>Marche de la basilique : un bloc de pierre plein (le mur clair du palais) de x à x + width, dessus en top.</summary>
        static void Step(Transform parent, float x, float top, float width)
        {
            var go = new GameObject($"Marche_x{x:0.#}");
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = new Vector2(x + width / 2f, top / 2f);
            box.size = new Vector2(width, top);
            box.sharedMaterial = frictionless;
            var sr = Band(go.transform, "MarchePierre", x, x + width, 0f, top, 3);
            sr.name = "Pierre";
        }

        /// <summary>Branche (stage 6, image 1, bout épais contre le tronc) de x0 à x1, dessus en top, à sens unique.</summary>
        static void Branch(Transform parent, float x0, float x1, float top, bool leftward = false)
        {
            float len = x1 - x0, mid = (x0 + x1) / 2f;
            var sr = Place(parent, "Branche", mid, 0f, 1f, 2, leftward);
            float s = len / sr.sprite.bounds.size.x;
            sr.transform.localScale = new Vector3(s, s, 1f);
            float h = sr.sprite.bounds.size.y * s;
            sr.transform.localPosition = new Vector3(mid, top - h * 0.82f, 0f);
            var go = new GameObject($"Branche_x{x0:0.#}-{x1:0.#}");
            go.transform.SetParent(parent, false);
            CourseKit.OneWay(go, mid, top, len * 0.86f);
        }

        /// <summary>Plate-forme en bois (stage 6) dont le dessus est en top.</summary>
        static void Shelf(Transform parent, float x, float top, float width)
        {
            var sr = Place(parent, "PlancheCourte", x, 0f, 1f, 3);
            float s = width / sr.sprite.bounds.size.x;
            sr.transform.localScale = new Vector3(s, s, 1f);
            float h = sr.sprite.bounds.size.y * s;
            sr.transform.localPosition = new Vector3(x, top - h * 0.97f, 0f);
            var go = new GameObject($"Plateforme_x{x:0.#}");
            go.transform.SetParent(parent, false);
            CourseKit.OneWay(go, x, top, width * 0.92f);
        }

        /// <summary>Échelle de corde (stage 6) du sol jusqu'à top : on y grimpe avec le bouton de saut.</summary>
        static void Ladder(Transform parent, float x, float top)
        {
            Place(parent, "EchelleCorde", x, 0f, top + 0.15f, 2);
            var go = new GameObject($"Echelle_x{x:0.#}");
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.8f, top + 0.2f);
            box.offset = new Vector2(x, (top + 0.2f) / 2f);
            go.AddComponent<Climbable>().top = top;
        }

        /// <summary>Liane (stage 6) pendue en (x, anchorY) : la corde teintée de vert tourne autour du point d'attache.</summary>
        static void Liana(Transform parent, float x, float anchorY, float length)
        {
            var go = new GameObject($"Liane_x{x:0.#}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, anchorY, 0f);
            var liana = go.AddComponent<SuperYao.Liana>();
            liana.length = length;
            var sr = Place(go.transform, "Corde", 0f, 0f, length + 0.3f, 3, false, new Color(0.72f, 0.85f, 0.5f));
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            liana.visual = sr.transform;
        }

        /// <summary>Tente d'étal (stage 5) : Yao passe dessous ; sur la bâche, on se pose par au-dessus et on rebondit.</summary>
        static void Tent(Transform parent, float x, int art)
        {
            var sr = Place(parent, $"Tente_0{art}", x, BackOfStreet, TentH, 1);
            float w = sr.sprite.bounds.size.x * sr.transform.localScale.x;
            var go = new GameObject($"Bache_Rebond_x{x:0}");
            go.transform.SetParent(parent, false);
            CourseKit.OneWay(go, x, BackOfStreet + TentH * 0.75f, w * 0.6f);
            go.AddComponent<Bouncer>().visual = sr.transform;
        }

        /// <summary>Pirogue (stage 1) qui fait la navette sur le lac entre les deux rives (bord à y = 0).</summary>
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
            visual.sortingOrder = 62;
            float s = PirogueLength / (frames[0].bounds.size.x * PirogueHull);
            visual.transform.localScale = new Vector3(s, s, 1f);
            visual.transform.localPosition = new Vector3(0f, -PirogueDeck * frames[0].bounds.size.y * s, 0f);
            moving.visual = visual;
            moving.frames = frames;
            CourseKit.OneWay(go, 0f, 0f, PirogueLength * 0.9f);
        }

        static void BuildForeground(Transform layer)
        {
            var tint = new Color(0.72f, 0.78f, 0.72f);
            var plants = new (string prefab, float wx, float h, bool flip)[]
                { ("Plante_Herbes", 12f, 1.3f, false), ("Plante_Touffes", 96f, 1.2f, false), ("Plante_Herbes", 150f, 1.3f, true), ("Plante_Touffes", 225f, 1.2f, true), ("Plante_Herbes", 290f, 1.3f, false) };
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
            settings.AddComponent<StageState>().LevelNumber = 7;
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
            box.offset = new Vector2(x, 0f);
            box.size = new Vector2(1f, 40f);
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
            SpritePrefab("SolPave", Art1 + "Ground/ground_paved.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("Lampadaire", Art1 + "Props/lamp_01.png", "Monde");
            SpritePrefab("SolAsphalte", Art2 + "Ground/ground_asphalt.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("Mer", Art2 + "Water/sea.jpg", "Monde", SpriteDrawMode.Tiled);
            for (int i = 1; i <= 4; i++) SpritePrefab($"Cocotier_0{i}", Art2 + $"Vegetation/palm_0{i}.png", "Rue");
            SpritePrefab("Arbre", Art2 + "Vegetation/tree_01.png", "Monde");
            SpritePrefab("Plante_Herbes", Art2 + "Plants/plant_01.png", "Monde");
            SpritePrefab("Plante_Buisson", Art2 + "Plants/plant_03.png", "Monde");
            SpritePrefab("Plante_Touffes", Art2 + "Plants/plant_06.png", "Monde");
            SpritePrefab("Branche", Art6 + "Trees/branch_01.png", "Monde");
            SpritePrefab("SolTerreRouge", Art6 + "Ground/ground_red.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("PlancheCourte", Art6 + "Wood/shelf_small.png", "Monde");
            SpritePrefab("EchelleCorde", Art6 + "Wood/ladder_rope.png", "Monde");
            SpritePrefab("Corde", Art6 + "Wood/rope.png", "Monde");
            SpritePrefab("EtalBananes", Art6 + "Village/stall_bananas.png", "Monde");
            for (int i = 1; i <= 3; i += 2) SpritePrefab($"Tente_0{i}", "Assets/SuperYao/Backgrounds/Stage5/Tents/tent_0" + i + ".png", "Monde");
            SpritePrefab("SolHerbe", Art6 + "Ground/ground_red_grass.png", "Monde", SpriteDrawMode.Tiled);
            // Stage 7
            SpritePrefab("HorizonYakro", Art + "Horizon/horizon_yakro.png", "Horizon", SpriteDrawMode.Tiled);
            SpritePrefab("DomeLointain", Art + "Horizon/horizon_dome.png", "Horizon");
            SpritePrefab("SolJardin", Art + "Ground/ground_garden.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SolJardinHerbe", Art + "Ground/ground_garden_grass.png", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SousLEau", Art + "Water/underwater.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("Hotel", Art + "Buildings/hotel.png", "PlanIntermediaire");
            SpritePrefab("Palais", Art + "Buildings/palace.png", "PlanIntermediaire");
            SpritePrefab("Basilique", Art + "Buildings/basilica.png", "PlanIntermediaire");
            SpritePrefab("Mur", Art + "Walls/wall.png", "Monde");
            SpritePrefab("Grille", Art + "Walls/fence.png", "Monde");
            SpritePrefab("Portail", Art + "Walls/gate.png", "Monde");
            SpritePrefab("Pilier", Art + "Walls/pillar.png", "Monde");
            SpritePrefab("MarchePierre", Art + "Walls/wall.png", "Monde", SpriteDrawMode.Tiled);
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
