using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Construit le stage 9, « Taï » (menu Super Yao > Construire le stage 9), dans la grande forêt primaire du parc de
    /// Taï : l'entrée de la forêt (chimpanzés qui lancent des noix, hippopotame nain, ruisseau), la rivière et son courant
    /// (rochers, pirogue ; à la nage, le courant emporte Yao vers l'arrière), puis la canopée, un chemin en hauteur de
    /// branche en branche (corde à nœuds, caisses suspendues, lianes) au-dessus du sous-bois. 420 cases, pas de boss.
    /// Nouveautés : chimpanzé et hippopotame nain (Enemy), courant de la rivière (SwimWater.current), rochers.
    /// Reprend les stages précédents : ciel et oiseaux, pirogue (stage 1), plantes (stage 2), hévéas, branches, lianes,
    /// corde à nœuds (stage 6), nage, fond des lacs, poissons et caïmans (stage 7), fromagers, racines, sol de pierres,
    /// caisses suspendues (stage 8), vautours, CourseKit.
    /// </summary>
    public static class Stage09Builder
    {
        const string Art = "Assets/SuperYao/Backgrounds/Stage9/";
        const string Art8 = "Assets/SuperYao/Backgrounds/Stage8/";
        const string Art7 = "Assets/SuperYao/Backgrounds/Stage7/";
        const string Art6 = "Assets/SuperYao/Backgrounds/Stage6/";
        const string Art2 = "Assets/SuperYao/Backgrounds/Stage2/";
        const string Art1 = "Assets/SuperYao/Backgrounds/Stage1/";
        const string PrefabDir = "Assets/SuperYao/Prefabs/Stage9";
        const string ScenePath = "Assets/SuperYao/Scenes/Stage09_Tai.unity";
        const string HeroPrefab = "Assets/SuperYao/Prefabs/Heros/Heros_Yao.prefab";
        const string GameplayDir = "Assets/SuperYao/Gameplay/";
        const string FrictionlessPath = "Assets/SuperYao/Physics/SansFrottement.physicsMaterial2D";
        const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string Tagline = "Plonge dans la forêt de Taï : les chimpanzés y cassent des noix… et te les lancent depuis les branches !";

        const float FSky = 0f, FClouds = 0.05f, FHorizon = 0.15f, FFar = 0.35f, FMid = 0.65f, FStreet = 0.9f, FFront = 1.1f;
        const float VisibleRows = 20f;
        const float HalfViewMin = VisibleRows / 2f * 4f / 3f, HalfViewMax = VisibleRows / 2f * 2.4f;

        // Parcours (420 cases) : entrée de la forêt | la rivière | la canopée.
        const float LevelLength = 420f, RiverStart = 140f, CanopyStart = 280f, GoalX = 415f;
        static readonly float[] ZoneEnds = { RiverStart, CanopyStart, float.MaxValue };
        static readonly string[] ZoneNames = { "Z1_Entree_Foret", "Z2_Riviere", "Z3_Canopee" };
        const float StonesTop = 0.05f, BackOfStreet = 0.15f, Surface = -0.35f, KillY = -16f;
        // Le ruisseau et la rivière : (début, fin, profondeur, courant en cases/s — négatif : vers l'arrière).
        static readonly (float x0, float x1, float depth, float current)[] Lakes = { (70f, 82f, 4f, 0f), (148f, 238f, 6f, -2.2f) };
        // Grandes racines de fromager (stage 8, image 1240 × 859 px) : 6 cases de haut.
        const float RootsH = 6f, RootsW = RootsH * 1240f / 859f;

        static Material spriteMaterial;
        static PhysicsMaterial2D frictionless;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        [MenuItem("Super Yao/Construire le stage 9 (Taï)")]
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
                EditorUtility.DisplayDialog("Super Yao", "Lance d'abord « Construire le stage 1 » : il fabrique Yao, que le stage 9 réutilise.", "OK");
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
            BuildHorizon(Layer(decor, "03_Foret_Primaire", FHorizon, 1f), Layer(decor, "04_Canopee_Lointaine", FFar, 1f));
            BuildMid(Layer(decor, "05a_Fromagers", FMid, 1f));
            BuildStreet(Layer(decor, "05b_Fromagers_Proches", FStreet, 1f));

            var world = new GameObject("06_Monde_Jouable").transform;
            BuildGround(world);
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
            Debug.Log($"Super Yao : stage 9 construit ({LevelLength:0} cases) dans {ScenePath}.");
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

        /// <summary>La forêt primaire et sa brume au loin, puis la canopée plus proche.</summary>
        static void BuildHorizon(Transform horizon, Transform far)
        {
            var (min, max) = LocalRange(FHorizon);
            Band(horizon, "HorizonPrimaire", min, max, 0f, 8f, 0);
            var (fmin, fmax) = LocalRange(FFar);
            var canopy = Band(far, "HorizonCanopee", fmin, fmax, -0.5f, 6.5f, 0);
            canopy.sortingLayerName = "BatimentsLointains";
        }

        /// <summary>Plan intermédiaire (x local) : fromagers géants (stage 8) et hévéas.</summary>
        static void BuildMid(Transform layer)
        {
            const string sort = "PlanIntermediaire";
            for (int k = 0; k * 12f < LevelLength + 30f; k++)
            {
                float wx = 4f + k * 12f + (k % 3) * 3f;
                float lx = FMid * wx;
                bool fromager = k % 3 != 2;
                Place(Zone(layer, FMid, lx), fromager ? $"Fromager_0{k % 3 + 1}" : $"Hevea_0{k % 2 + 1}", lx, 0f, fromager ? 10f + (k % 2) * 2f : 9f, 0, k % 2 == 1, null, sort);
            }
        }

        static void BuildStreet(Transform layer)
        {
            var trees = new (int art, float wx, float h, bool flip)[]
                { (1, 24f, 13f, false), (3, 112f, 12f, true), (2, 196f, 13f, false), (1, 288f, 14f, true), (3, 372f, 13f, false) };
            foreach (var t in trees) Place(Zone(layer, FStreet, FStreet * t.wx), $"Fromager_0{t.art}", FStreet * t.wx, 0f, t.h, 1, t.flip);
        }

        // ---------------------------------------------------------------- monde jouable

        static void BuildGround(Transform world)
        {
            var ground = new GameObject("Sol").transform;
            ground.SetParent(world, false);
            var land = new List<(float a, float b)>();
            float from = -2f;
            foreach (var l in Lakes) { land.Add((from, l.x0)); from = l.x1; }
            land.Add((from, LevelLength + 2f));
            foreach (var (a, b) in land)
            {
                SolidBox(ground, $"Collision_x{a:0}-{b:0}", Mathf.Max(a, -1f), Mathf.Min(b, LevelLength + 1f), 0f, -KillY);
                GroundSpan(ground, "SolPierres", a, b, StonesTop);
                var top = Band(ground, "SolPierresDessus", a, b, StonesTop - 12f / 160f, 59f / 160f, 1);
                top.name = $"Mousse_x{a:0}-{b:0}";
                Soil(ground, "SolPierres", a, b, StonesTop - 2.2f, KillY + 2f, new Color(0.8f, 0.8f, 0.8f));
            }
            foreach (var l in Lakes)
            {
                Lake(ground, l.x0, l.x1, l.depth);
                if (l.current != 0f) ground.Find($"Lac_x{l.x0:0}-{l.x1:0}/Zone_De_Nage").GetComponent<SwimWater>().current = l.current;
            }
            // Berges de la rivière : racines qui pendent dans l'eau (rive gauche), pierres moussues (rive droite).
            var left = Place(ground, "BergeRacines", 0f, 0f, 4f, 2);
            float lw = left.sprite.bounds.size.x * left.transform.localScale.x;
            left.transform.localPosition = new Vector3(Lakes[1].x0 + 1.4f - lw / 2f, StonesTop - 0.81f * 4f, 0f);
            var right = Place(ground, "BergePierres", 0f, 0f, 2.6f, 2, true);
            float rw = right.sprite.bounds.size.x * right.transform.localScale.x;
            right.transform.localPosition = new Vector3(Lakes[1].x1 - 1.2f + rw / 2f, StonesTop - 0.9f * 2.6f, 0f);
            Wall(ground, "Mur_Debut", -0.5f);
            Wall(ground, "Mur_Fin", LevelLength + 0.5f);
        }

        /// <summary>
        /// Le parcours (420 cases). Entrée de la forêt : racines de fromager, chimpanzés (au sol et sur une branche),
        /// hippopotame nain, ruisseau. Rivière : rochers et pirogue ; tombé à l'eau, le courant ramène Yao en arrière (poissons,
        /// caïman). Le drapeau est sur un grand rocher au milieu de la rivière. Canopée : corde à nœuds, branches, caisses
        /// suspendues, lianes, un chimpanzé perché et le diamant ; en dessous, le sous-bois (hippopotames, chimpanzé). Danger
        /// des ennemis 42 pour 420 cases (agame et poisson-chat 1, vautour et poisson-tigre 2, caïman 2,25, chimpanzé et
        /// hippopotame 3). Pas de boss, comme sur le téléphone.
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

            // --- Entrée de la forêt
            Line(C, 18f, 22f, 1f, 1.2f);
            Roots(In(44f), 44f, false);
            Line(C, 44f, 47f, 1f, 4.6f);
            Rock(In(76f), 76f, 1, 0.5f);
            Line(B, 72f, 80f, 2f, 3f);
            var t1 = In(120f);
            Place(t1, "Hevea_01", 115.6f, BackOfStreet - 0.05f, 11f, -2);
            Branch(t1, 115.9f, 123.5f, 4f);
            Line(C, 117f, 122f, 1f, 5.2f);

            // --- La rivière (courant vers l'arrière) : rochers, pirogue, rochers.
            var river = new (float x, int art, float top)[]
            {
                (152f, 2, 0.4f), (157.5f, 1, 0.6f), (162.5f, 5, 0.5f),
                (209f, 3, 0.5f), (215f, 4, 1.4f), (220.5f, 6, 0.6f), (226f, 1, 0.8f), (231.5f, 2, 0.5f), (235.5f, 5, 0.4f),
            };
            foreach (var r in river) Rock(In(r.x), r.x, r.art, r.top);
            Pirogue(In(165f), 165f, 205f, 2.4f);
            Line(C, 152f, 162f, 2.5f, 2f);
            Line(C, 170f, 200f, 3f, 3.4f);
            Line(C, 168f, 196f, 4f, -3.4f);           // sous l'eau, contre le courant
            Line(B, 215f, 231f, 4f, 3.2f);

            // --- La canopée : en haut, de branche en branche ; en bas, le sous-bois.
            var canopy = In(300f);
            Place(canopy, "Hevea_02", 281f, BackOfStreet - 0.05f, 12f, -2);
            Branch(canopy, 281.3f, 291f, 8f);
            Climb(canopy, 283.4f, 0.3f, 8f);
            Crate(In(294.5f), 294.5f, 6f, 8.4f, 4f, 0f);
            Place(canopy, "Hevea_01", 297.6f, BackOfStreet - 0.05f, 12.5f, -2);
            Branch(canopy, 297.9f, 306f, 8.4f);
            Liana(canopy, 310f, 11f, 3.6f);
            Liana(canopy, 315.6f, 11.2f, 3.7f);
            Place(canopy, "Hevea_03", 327.2f, BackOfStreet - 0.05f, 12.5f, -2, true);
            Branch(canopy, 319.4f, 327f, 8.6f, true);
            Crate(In(331f), 331f, 6.4f, 8.6f, 3.6f, 0.4f);
            Crate(In(336.2f), 336.2f, 7f, 9f, 4.2f, 0.8f);
            Place(canopy, "Hevea_02", 341f, BackOfStreet - 0.05f, 12.5f, -2);
            Branch(canopy, 341.3f, 350f, 8.4f);
            Line(C, 284f, 290f, 1f, 9.4f);
            Line(B, 309f, 317f, 2f, 9.5f);
            Line(C, 342f, 345f, 1f, 9.6f);
            items.Add((D, 348f, 10.6f));
            Line(C, 300f, 304f, 1f, 1.2f);            // le sous-bois
            Roots(In(381f), 381f, true);
            Line(C, 378f, 381f, 1f, 4.6f);
            Line(C, 400f, 404f, 1f, 1.2f);

            var blocks = new (BumpBlock.Kind kind, float x)[]
            {
                (BumpBlock.Kind.CoinBox, 10f), (BumpBlock.Kind.Brick, 11f), (BumpBlock.Kind.PowerBox, 12f), (BumpBlock.Kind.Brick, 13f),
                (BumpBlock.Kind.CoinBox, 92f), (BumpBlock.Kind.Brick, 93f), (BumpBlock.Kind.CoinBox, 94f), (BumpBlock.Kind.CoinSack, 138f),
                (BumpBlock.Kind.Brick, 252f), (BumpBlock.Kind.PowerBox, 253f), (BumpBlock.Kind.Brick, 254f),
                (BumpBlock.Kind.CoinBox, 365f), (BumpBlock.Kind.Brick, 366f), (BumpBlock.Kind.CoinBox, 367f),
            };
            foreach (var b in blocks) CourseKit.Block(In(b.x), b.kind, b.x);
            foreach (var it in items) CourseKit.Item(In(it.x), it.kind, it.x, it.y);

            var E = new (Enemy.Kind kind, float x, float y)[]
            {
                // Entrée de la forêt (12)
                (Enemy.Kind.Chimp, 30f, 0f), (Enemy.Kind.Vulture, 60f, 6f), (Enemy.Kind.Catfish, 76f, -2.6f), (Enemy.Kind.Hippo, 104f, 0f),
                (Enemy.Kind.Chimp, 120f, 4f),
                // Rivière (15,25)
                (Enemy.Kind.Vulture, 160f, 7f), (Enemy.Kind.Tigerfish, 175f, -3.6f), (Enemy.Kind.Catfish, 195f, -2.6f),
                (Enemy.Kind.Caiman, 222f, Surface), (Enemy.Kind.Tigerfish, 230f, -4.2f), (Enemy.Kind.Hippo, 252f, 0f), (Enemy.Kind.Chimp, 266f, 0f),
                // Canopée (15)
                (Enemy.Kind.Hippo, 300f, 0f), (Enemy.Kind.Chimp, 323f, 8.6f), (Enemy.Kind.Chimp, 326f, 0f), (Enemy.Kind.Agama, 355f, 0f),
                (Enemy.Kind.Vulture, 390f, 6f), (Enemy.Kind.Hippo, 396f, 0f),
            };
            foreach (var e in E) CourseKit.EnemyAt(In(e.x), e.kind, e.x, e.y);

            CourseKit.Flag(In(209f), 209f, 3.5f, false, 0.5f); // un seul point de contrôle, au milieu, sur le grand rocher
            CourseKit.Flag(In(GoalX), GoalX, 5.5f, true);

            var catalog = MenuBuilder.Catalog();
            var stage = catalog.stages[8];
            stage.scene = System.IO.Path.GetFileNameWithoutExtension(ScenePath);
            stage.tagline = Tagline;
            stage.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SuperYao/UI/Backgrounds/thumb_tai.jpg");
            stage.coins = items.Count(i => i.kind == C) + blocks.Count(k => k.kind == BumpBlock.Kind.CoinBox)
                          + blocks.Count(k => k.kind == BumpBlock.Kind.CoinSack) * 8;
            stage.bananas = items.Count(i => i.kind == B);
            stage.diamonds = items.Count(i => i.kind == D);
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Rocher moussu dans l'eau (images de l'utilisateur) : on se pose dessus, dessus en <paramref name="top"/>.</summary>
        static void Rock(Transform parent, float x, int art, float top)
        {
            float[] heights = { 1.4f, 1.6f, 1.6f, 2.6f, 1.8f, 1.8f };
            float h = heights[art - 1];
            var sr = Place(parent, $"Rocher_0{art}", x, top - 0.96f * h, h, 3);
            sr.name = $"Rocher_x{x:0.#}";
            float w = sr.sprite.bounds.size.x * sr.transform.localScale.x;
            var go = new GameObject($"Rocher_Collision_x{x:0.#}");
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = new Vector2(x, top - (h * 0.8f) / 2f - 0.04f);
            box.size = new Vector2(w * 0.82f, h * 0.8f);
            box.sharedMaterial = frictionless;
        }

        /// <summary>Pirogue (stage 1) qui fait la navette sur la rivière (bord à y = 0).</summary>
        static void Pirogue(Transform parent, float from, float to, float speed)
        {
            const float length = 4f, hull = 62f / 87f, deck = 0.28f;
            var go = new GameObject($"Pirogue_x{from:0.#}-{to:0.#}");
            go.transform.SetParent(parent, false);
            float half = length / 2f + 0.05f;
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
            float s = length / (frames[0].bounds.size.x * hull);
            visual.transform.localScale = new Vector3(s, s, 1f);
            visual.transform.localPosition = new Vector3(0f, -deck * frames[0].bounds.size.y * s, 0f);
            moving.visual = visual;
            moving.frames = frames;
            CourseKit.OneWay(go, 0f, 0f, length * 0.9f);
        }

        /// <summary>Grandes racines de fromager (pied d'arbre géant) en x : on monte sur leurs deux replats (sens unique).</summary>
        static void Roots(Transform parent, float x, bool flip)
        {
            var sr = Place(parent, "Racines", x, BackOfStreet - 0.1f, RootsH, -1, flip);
            sr.name = $"Racines_Fromager_x{x:0}";
            // Replats mesurés (part de la largeur depuis la gauche de l'image → hauteur) : le haut descend du tronc vers
            // la pointe, le bas est plat.
            var upper = new[] { (0.455f, 0.72f), (0.51f, 0.67f), (0.565f, 0.61f), (0.62f, 0.57f), (0.70f, 0.54f), (0.80f, 0.53f), (0.89f, 0.52f) };
            var lower = new[] { (0.63f, 0.25f), (0.89f, 0.24f) };
            foreach (var (name, pts) in new[] { ("Replat_Haut", upper), ("Replat_Bas", lower) })
            {
                var go = new GameObject(name);
                go.transform.SetParent(sr.transform.parent, false);
                var edge = go.AddComponent<EdgeCollider2D>();
                edge.points = pts.Select(p => new Vector2(x + (flip ? 0.5f - p.Item1 : p.Item1 - 0.5f) * RootsW, BackOfStreet - 0.1f + p.Item2 * RootsH - 0.05f))
                    .OrderBy(v => v.x).ToArray();
                edge.sharedMaterial = frictionless;
                edge.usedByEffector = true;
                var eff = go.AddComponent<PlatformEffector2D>();
                eff.useOneWay = true;
                eff.surfaceArc = 160f;
            }
        }

        /// <summary>Caisse suspendue (deux caisses de bois du stage 1 côte à côte, deux cordes) qui monte et descend.</summary>
        static void Crate(Transform parent, float x, float low, float high, float period, float phase)
        {
            var go = new GameObject($"Caisse_Suspendue_x{x:0}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, low, 0f);
            go.AddComponent<Rigidbody2D>();
            var crate = CourseKit.LoadSprite(GameplayDir + "Blocks/crate_0.png");
            for (int i = 0; i < 2; i++)
            {
                var sr = CourseKit.Renderer(go.transform, $"Caisse_{i}", crate, 3, 1f);
                sr.transform.localScale = new Vector3(1f / crate.bounds.size.x, 1f / crate.bounds.size.y, 1f);
                sr.transform.localPosition = new Vector3(i - 0.5f, -1f, 0f);
            }
            CourseKit.OneWay(go, 0f, 0f, 1.9f);
            var ropes = new List<Transform>();
            float ropeH = prefabs["Corde"].GetComponent<SpriteRenderer>().sprite.bounds.size.y;
            foreach (var ox in new[] { -0.75f, 0.75f })
            {
                var r = Place(go.transform, "Corde", ox, 0f, 1f, 2, false, new Color(0.72f, 0.85f, 0.5f));
                r.transform.localScale = new Vector3(0.6f / ropeH, 1f, 1f);
                ropes.Add(r.transform);
            }
            var hc = go.AddComponent<HangingCrate>();
            hc.lowY = low;
            hc.highY = high;
            hc.period = period;
            hc.phase = phase;
            hc.anchorY = 12f;
            hc.ropes = ropes.ToArray();
            hc.ropeSpriteHeight = ropeH;
        }

        /// <summary>Corde à nœuds (stage 6) de bottom à top : on y grimpe avec le bouton de saut.</summary>
        static void Climb(Transform parent, float x, float bottom, float top)
        {
            Place(parent, "CordeNoeuds", x, bottom, top - bottom + 0.15f, 2);
            var go = new GameObject($"Corde_A_Noeuds_x{x:0.#}");
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.8f, top + 0.2f);
            box.offset = new Vector2(x, (top + 0.2f) / 2f);
            go.AddComponent<Climbable>().top = top;
        }

        static void BuildForeground(Transform layer)
        {
            var tint = new Color(0.62f, 0.7f, 0.62f);
            var plants = new (string prefab, float wx, float h, bool flip)[]
                { ("Plante_Herbes", 12f, 1.4f, false), ("Plante_Touffes", 96f, 1.3f, false), ("Plante_Herbes", 168f, 1.4f, true), ("Plante_Touffes", 255f, 1.3f, true), ("Plante_Herbes", 330f, 1.4f, false), ("Plante_Touffes", 398f, 1.3f, true) };
            foreach (var p in plants) Place(Zone(layer, FFront, FFront * p.wx), p.prefab, FFront * p.wx, -2.1f, p.h, 0, p.flip, tint, "AvantPlan");
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
            settings.AddComponent<StageState>().LevelNumber = 9;
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
            SpritePrefab("Mer", Art2 + "Water/sea.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("Plante_Herbes", Art2 + "Plants/plant_01.png", "Monde");
            SpritePrefab("Plante_Buisson", Art2 + "Plants/plant_03.png", "Monde");
            SpritePrefab("Plante_Touffes", Art2 + "Plants/plant_06.png", "Monde");
            for (int i = 1; i <= 3; i++) SpritePrefab($"Hevea_0{i}", Art6 + $"Trees/hevea_0{i}.png", "Monde");
            SpritePrefab("Branche", Art6 + "Trees/branch_01.png", "Monde");
            SpritePrefab("Corde", Art6 + "Wood/rope.png", "Monde");
            SpritePrefab("CordeNoeuds", Art6 + "Wood/rope_knots.png", "Monde");
            SpritePrefab("SolJardin", Art7 + "Ground/ground_garden.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SolJardinHerbe", Art7 + "Ground/ground_garden_grass.png", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SousLEau", Art7 + "Water/underwater.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SolPierres", Art8 + "Ground/ground_stones.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SolPierresDessus", Art8 + "Ground/ground_stones_top.png", "Monde", SpriteDrawMode.Tiled);
            for (int i = 1; i <= 3; i++) SpritePrefab($"Fromager_0{i}", Art8 + $"Trees/fromager_0{i}.png", "Rue");
            SpritePrefab("Racines", Art8 + "Trees/roots.png", "Monde");
            // Stage 9
            SpritePrefab("HorizonPrimaire", Art + "Horizon/horizon_primary.png", "Horizon", SpriteDrawMode.Tiled);
            SpritePrefab("HorizonCanopee", Art + "Horizon/horizon_canopy.png", "BatimentsLointains", SpriteDrawMode.Tiled);
            for (int i = 1; i <= 6; i++) SpritePrefab($"Rocher_0{i}", Art + $"Rocks/rock_0{i}.png", "Monde");
            SpritePrefab("BergeRacines", Art + "Banks/bank_roots.png", "Monde");
            SpritePrefab("BergePierres", Art + "Banks/bank_stones.png", "Monde");
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
