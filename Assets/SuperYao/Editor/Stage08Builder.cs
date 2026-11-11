using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Construit le stage 8, « La forêt sacrée » (menu Super Yao > Construire le stage 8), du coucher du soleil à la nuit :
    /// la lisière (fromagers géants, grandes racines où l'on grimpe, statues de pierre, pagnes noués), les marigots et
    /// leurs caisses suspendues aux lianes qui montent et descendent (HangingCrate), puis la nuit, ses lucioles et ses
    /// champignons qui luisent (NightLights), jusqu'à la clairière du bassin sacré où attend le vieux caïman sacré
    /// (CaimanBoss : on peut aussi le poursuivre à la nage). 420 cases.
    /// Reprend les stages précédents : ciels de coucher et de nuit (stage 3) et DayNight, lisière de forêt, hévéas, branches,
    /// lianes, corde à nœuds (stage 6), nage, fond des lacs, poissons et caïmans (stage 7), singes, vautours, CourseKit.
    /// </summary>
    public static class Stage08Builder
    {
        const string Art = "Assets/SuperYao/Backgrounds/Stage8/";
        const string Art7 = "Assets/SuperYao/Backgrounds/Stage7/";
        const string Art6 = "Assets/SuperYao/Backgrounds/Stage6/";
        const string Art3 = "Assets/SuperYao/Backgrounds/Stage3/";
        const string Art2 = "Assets/SuperYao/Backgrounds/Stage2/";
        const string Art1 = "Assets/SuperYao/Backgrounds/Stage1/";
        const string PrefabDir = "Assets/SuperYao/Prefabs/Stage8";
        const string ScenePath = "Assets/SuperYao/Scenes/Stage08_ForetSacree.unity";
        const string HeroPrefab = "Assets/SuperYao/Prefabs/Heros/Heros_Yao.prefab";
        const string GameplayDir = "Assets/SuperYao/Gameplay/";
        const string FrictionlessPath = "Assets/SuperYao/Physics/SansFrottement.physicsMaterial2D";
        const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string Tagline = "Entre dans la forêt sacrée au coucher du soleil : saute de caisse en caisse au-dessus des marigots… le vieux caïman sacré garde le bassin !";

        const float FSky = 0f, FClouds = 0.05f, FHorizon = 0.15f, FMid = 0.65f, FStreet = 0.9f, FFront = 1.1f;
        const float VisibleRows = 20f;
        const float HalfViewMin = VisibleRows / 2f * 4f / 3f, HalfViewMax = VisibleRows / 2f * 2.4f;

        // Parcours (420 cases) : lisière au couchant | marigots | nuit et clairière du bassin sacré.
        const float LevelLength = 420f, MarigotsStart = 140f, NightStart = 280f, GoalX = 415f;
        static readonly float[] ZoneEnds = { MarigotsStart, NightStart, float.MaxValue };
        static readonly string[] ZoneNames = { "Z1_Lisiere", "Z2_Marigots", "Z3_Nuit_Bassin_Sacre" };
        const float StonesTop = 0.05f, BackOfStreet = 0.15f, Surface = -0.35f, KillY = -16f;
        // Coucher de soleil puis nuit, selon la position de la caméra.
        const float DuskFrom = 40f, DuskTo = 180f, NightFrom = 240f, NightTo = 320f;
        // Les marigots (on y nage) et le bassin sacré : (début, fin, profondeur).
        static readonly (float x0, float x1, float depth)[] Lakes = { (94f, 104f, 5f), (160f, 182f, 5f), (244f, 262f, 6f), (380f, 402f, 7f) };
        // L'arène du bassin sacré (barrières en x) et le vieux caïman.
        const float ArenaX0 = 372f, ArenaX1 = 410f, PoolX0 = 380f, PoolX1 = 402f, PoolDepth = 7f, BossX = 391f;
        // Grandes racines de fromager (image 1240 × 859 px) : 6 cases de haut ; replats mesurés sur l'image.
        const float RootsH = 6f, RootsW = RootsH * 1240f / 859f;

        static Material spriteMaterial;
        static PhysicsMaterial2D frictionless;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        [MenuItem("Super Yao/Construire le stage 8 (La forêt sacrée)")]
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
                EditorUtility.DisplayDialog("Super Yao", "Lance d'abord « Construire le stage 1 » : il fabrique Yao, que le stage 8 réutilise.", "OK");
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
            var sunset = Place(skyLayer, "CielCoucher", 0f, -7f, 14f, 1);
            sunset.gameObject.AddComponent<FitToCamera>();
            var night = Place(skyLayer, "CielNuit", 0f, -7f, 14f, 2);
            night.gameObject.AddComponent<FitToCamera>();
            foreach (var r in new[] { sunset, night }) { r.color = new Color(1f, 1f, 1f, 0f); r.enabled = false; }
            BuildClouds(Layer(decor, "02_Nuages", FClouds, 0f));
            BuildSkyLife(decor);
            BuildHorizon(Layer(decor, "03_Foret_Lointaine", FHorizon, 1f));
            BuildMid(Layer(decor, "05a_Fromagers", FMid, 1f));
            BuildStreet(Layer(decor, "05b_Fromagers_Proches", FStreet, 1f));

            var world = new GameObject("06_Monde_Jouable").transform;
            var ground = BuildGround(world);
            var props = BuildProps(world);
            BuildCourse(world);
            BuildBoss(world);
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

            var dn = new GameObject("Jour_Nuit").AddComponent<DayNight>();
            dn.sunsetSky = sunset;
            dn.nightSky = night;
            dn.duskFrom = DuskFrom; dn.duskTo = DuskTo; dn.nightFrom = NightFrom; dn.nightTo = NightTo;
            dn.tintRoots = new[] { decor, ground, props };
            // Lucioles et champignons qui luisent (la nuit).
            var lights = new GameObject("Nuit_Lucioles_Champignons").AddComponent<NightLights>();
            lights.material = spriteMaterial;
            lights.area = new Rect(240f, 0.6f, 180f, 7f);
            lights.fireflies = 46;
            lights.mushrooms = new[]
            {
                new Vector2(236f, 0f), new Vector2(238.5f, 0f), new Vector2(268f, 0f), new Vector2(286f, 0f), new Vector2(287.2f, 0f),
                new Vector2(305f, 0f), new Vector2(322f, 0f), new Vector2(323.4f, 0f), new Vector2(341f, 0f), new Vector2(358f, 0f),
                new Vector2(359.5f, 0f), new Vector2(374f, 0f), new Vector2(406f, 0f), new Vector2(407.6f, 0f), new Vector2(413f, 0f),
            };

            BuildCamera(yao.transform);
            BuildSettings();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log($"Super Yao : stage 8 construit ({LevelLength:0} cases) dans {ScenePath}.");
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

        /// <summary>Forêt lointaine (lisière du stage 6), un peu assombrie : la forêt sacrée est dense.</summary>
        static void BuildHorizon(Transform layer)
        {
            var (min, max) = LocalRange(FHorizon);
            var band = Band(layer, "HorizonForet", min, max, 0f, 7f, 0);
            band.color = new Color(0.72f, 0.82f, 0.74f);
        }

        /// <summary>Plan intermédiaire (x local) : les fromagers géants, en rang, et quelques hévéas.</summary>
        static void BuildMid(Transform layer)
        {
            const string sort = "PlanIntermediaire";
            for (int k = 0; k * 13f < LevelLength + 30f; k++)
            {
                float wx = 6f + k * 13f + (k % 3) * 2.5f;
                float lx = FMid * wx;
                Place(Zone(layer, FMid, lx), $"Fromager_0{k % 3 + 1}", lx, 0f, 9f + (k % 2) * 2f, 0, k % 2 == 1, null, sort);
            }
        }

        /// <summary>Quelques fromagers plus proches, derrière le parcours.</summary>
        static void BuildStreet(Transform layer)
        {
            var trees = new (int art, float wx, float h, bool flip)[]
                { (2, 30f, 12f, false), (1, 128f, 13f, true), (3, 212f, 12f, false), (2, 300f, 13f, true), (1, 366f, 14f, false) };
            foreach (var t in trees) Place(Zone(layer, FStreet, FStreet * t.wx), $"Fromager_0{t.art}", FStreet * t.wx, 0f, t.h, 1, t.flip);
        }

        // ---------------------------------------------------------------- monde jouable

        static Transform BuildGround(Transform world)
        {
            var ground = new GameObject("Sol").transform;
            ground.SetParent(world, false);
            // Sol de pierres moussues entre les marigots ; berges profondes jusqu'au fond.
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
            foreach (var l in Lakes) Lake(ground, l.x0, l.x1, l.depth);
            Wall(ground, "Mur_Debut", -0.5f);
            Wall(ground, "Mur_Fin", LevelLength + 0.5f);
            return ground;
        }

        /// <summary>Décor sacré au fond : statues de pierre, pagnes noués aux arbres, autels d'offrandes.</summary>
        static Transform BuildProps(Transform world)
        {
            var props = new GameObject("Statues_Et_Offrandes").transform;
            props.SetParent(world, false);
            var items = new (string prefab, float x, float y, float h, bool flip, int order)[]
            {
                ("Statue_01", 18f, 0f, 2.4f, false, 1), ("Statue_03", 70f, 0f, 2.6f, false, 1), ("Statue_02", 135f, 0f, 2.4f, false, 1),
                ("Offrande", 206f, 0f, 1.3f, false, 2), ("Statue_04", 214f, 0f, 2.2f, true, 1), ("Statue_01", 290f, 0f, 2.4f, true, 1),
                ("Statue_03", 340f, 0f, 2.6f, true, 1), ("Statue_02", 368f, 0f, 2.6f, false, 1), ("Offrande", 375.5f, 0f, 1.3f, false, 2),
                ("Statue_04", 406.5f, 0f, 2.4f, false, 1), ("Statue_03", 412f, 0f, 2.8f, false, 1),
                ("Plante_Buisson", 52f, -0.1f, 1.2f, false, 5), ("Plante_Touffes", 150f, -0.05f, 1f, true, 5), ("Plante_Herbes", 270f, -0.05f, 1.1f, false, 5),
            };
            foreach (var i in items) Place(Zone(props, 1f, i.x), i.prefab, i.x, BackOfStreet + i.y, i.h, i.order, i.flip);
            return props;
        }

        /// <summary>
        /// Le parcours (420 cases). Lisière : grandes racines de fromager où l'on grimpe, premier marigot (liane au-dessus,
        /// poisson-chat dedans), singe sur sa branche. Marigots : caisses suspendues qui montent et descendent (caïman dessous),
        /// corde à nœuds vers le diamant, second marigot à caisses (poisson-tigre). Nuit : racines, statues, lucioles,
        /// puis l'arène du bassin sacré et le vieux caïman. Danger des ennemis 37,5 pour 420 cases (agame, tortue, poisson-chat
        /// 1, vautour et poisson-tigre 2, caïman 2,25, singe 3), plus le boss. Un seul point de contrôle, au milieu.
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

            // --- Lisière
            Line(C, 20f, 24f, 1f, 1.2f);
            Roots(In(44f), 44f, false);
            Line(C, 44f, 47f, 1f, 4.6f);
            Line(B, 50f, 52f, 1f, 6f);
            var m1 = In(99f);
            Place(m1, "Hevea_02", 92.4f, BackOfStreet - 0.05f, 12f, -2);
            Pagne(m1, 92.4f, 3.2f, 1);
            Branch(m1, 92.7f, 103f, 9f);
            Liana(m1, 96.8f, 8.7f, 5.4f);
            Liana(m1, 101.2f, 8.7f, 5.6f);
            Line(C, 96f, 102f, 2f, -2.6f);
            var t1 = In(120f);
            Place(t1, "Hevea_01", 115.6f, BackOfStreet - 0.05f, 11f, -2);
            Pagne(t1, 115.6f, 2.8f, 4);
            Branch(t1, 115.9f, 123.5f, 4f);
            Line(C, 117f, 122f, 1f, 5.2f);

            // --- Marigots
            // Quatre caisses au-dessus du marigot (le caïman guette dessous).
            Crate(In(163f), 163f, 0.6f, 3.2f, 4f, 0f);
            Crate(In(168.5f), 168.5f, 0.8f, 4f, 4.6f, 0.5f);
            Crate(In(174f), 174f, 0.6f, 3.6f, 4.2f, 0.2f);
            Crate(In(179f), 179f, 0.8f, 3.4f, 4.8f, 0.7f);
            Line(C, 164f, 178f, 2f, 6.2f);
            var rope = In(216f);
            Place(rope, "Hevea_03", 212.5f, BackOfStreet - 0.05f, 11f, -2);
            Pagne(rope, 212.5f, 3f, 2);
            Branch(rope, 212.8f, 221.5f, 8f);
            Climb(rope, 215f, 0.3f, 8f);
            Line(C, 217f, 220f, 1f, 9.2f);
            items.Add((D, 220.5f, 10.4f));
            Crate(In(247f), 247f, 0.6f, 3.6f, 3.8f, 0.25f);
            Crate(In(252.5f), 252.5f, 1f, 4.4f, 4.2f, 0.75f);
            Crate(In(258f), 258f, 0.6f, 3.8f, 3.6f, 0.5f);
            Line(B, 248f, 258f, 2.5f, 6.8f);
            Line(C, 247f, 259f, 3f, -3.6f);
            var t2 = In(270f);
            Place(t2, "Hevea_02", 266.6f, BackOfStreet - 0.05f, 11f, -2);
            Pagne(t2, 266.6f, 3.1f, 3);
            Branch(t2, 266.9f, 274f, 4.2f);

            // --- Nuit et bassin sacré
            Roots(In(306f), 306f, true);
            Line(C, 302f, 305f, 1f, 4.6f);
            Line(C, 326f, 330f, 1f, 1.2f);
            CourseKit.Plank(In(388f), 388f, 4f, 2); // au-dessus du bassin : pour retomber sur la tête du caïman
            Line(C, 352f, 356f, 1f, 1.2f);

            var blocks = new (BumpBlock.Kind kind, float x)[]
            {
                (BumpBlock.Kind.CoinBox, 10f), (BumpBlock.Kind.Brick, 11f), (BumpBlock.Kind.PowerBox, 12f), (BumpBlock.Kind.Brick, 13f),
                (BumpBlock.Kind.CoinBox, 132f), (BumpBlock.Kind.CoinSack, 198f),
                (BumpBlock.Kind.Brick, 284f), (BumpBlock.Kind.PowerBox, 285f), (BumpBlock.Kind.Brick, 286f),
                (BumpBlock.Kind.CoinBox, 344f), (BumpBlock.Kind.Brick, 345f), (BumpBlock.Kind.CoinBox, 346f),
            };
            foreach (var b in blocks) CourseKit.Block(In(b.x), b.kind, b.x);
            foreach (var it in items) CourseKit.Item(In(it.x), it.kind, it.x, it.y);

            var E = new (Enemy.Kind kind, float x, float y)[]
            {
                // Lisière (10)
                (Enemy.Kind.Agama, 20f, 0f), (Enemy.Kind.Turtle, 32f, 0f), (Enemy.Kind.Vulture, 75f, 6f), (Enemy.Kind.Catfish, 99f, -3.2f),
                (Enemy.Kind.Vulture, 110f, 6.5f), (Enemy.Kind.Monkey, 120f, 4f),
                // Marigots (16,5)
                (Enemy.Kind.Agama, 150f, 0f), (Enemy.Kind.Caiman, 171f, Surface), (Enemy.Kind.Monkey, 190f, 0f), (Enemy.Kind.Turtle, 200f, 0f),
                (Enemy.Kind.Vulture, 228f, 7f), (Enemy.Kind.Agama, 236f, 0f), (Enemy.Kind.Tigerfish, 253f, -3.6f), (Enemy.Kind.Catfish, 258f, -5f),
                (Enemy.Kind.Monkey, 270f, 4.2f),
                // Nuit (11)
                (Enemy.Kind.Agama, 296f, 0f), (Enemy.Kind.Turtle, 314f, 0f), (Enemy.Kind.Monkey, 330f, 0f), (Enemy.Kind.Vulture, 350f, 6f),
                (Enemy.Kind.Agama, 356f, 0f), (Enemy.Kind.Turtle, 362f, 0f), (Enemy.Kind.Caiman, 253f, Surface),
            };
            foreach (var e in E) CourseKit.EnemyAt(In(e.x), e.kind, e.x, e.y);

            CourseKit.Flag(In(210f), 210f, 3.5f, false); // un seul point de contrôle, au milieu
            CourseKit.Flag(In(GoalX), GoalX, 5.5f, true);

            var catalog = MenuBuilder.Catalog();
            var stage = catalog.stages[7];
            stage.scene = System.IO.Path.GetFileNameWithoutExtension(ScenePath);
            stage.tagline = Tagline;
            stage.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SuperYao/UI/Backgrounds/thumb_foret_sacree.jpg");
            stage.coins = items.Count(i => i.kind == C) + blocks.Count(k => k.kind == BumpBlock.Kind.CoinBox)
                          + blocks.Count(k => k.kind == BumpBlock.Kind.CoinSack) * 8;
            stage.bananas = items.Count(i => i.kind == B);
            stage.diamonds = items.Count(i => i.kind == D);
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Le vieux caïman sacré (images du téléphone) dans son bassin, et les barrières de l'arène.</summary>
        static void BuildBoss(Transform world)
        {
            var group = new GameObject("Combat_Vieux_Caiman").transform;
            group.SetParent(world, false);
            var go = new GameObject("Vieux_Caiman_Sacre");
            go.transform.SetParent(group, false);
            go.transform.localPosition = new Vector3(BossX, Surface, 0f);
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<Rigidbody2D>();
            string dir = GameplayDir + "Boss/cm_";
            var image = CourseKit.Renderer(go.transform, "Image", CourseKit.LoadSprite(dir + "lurk_0.png"), 5, 1f);
            var boss = go.AddComponent<CaimanBoss>();
            boss.image = image;
            boss.swim = CourseKit.LoadSprites(dir + "swim_", 4);
            boss.lurk = CourseKit.LoadSprites(dir + "lurk_", 4);
            boss.bite = CourseKit.LoadSprites(dir + "bite_", 3);
            boss.tail = CourseKit.LoadSprites(dir + "tail_", 1);
            boss.under = CourseKit.LoadSprites(dir + "under_", 4);
            boss.splash = new[] { CourseKit.LoadSprite(GameplayDir + "Items/bubble.png") };
            boss.arenaX0 = ArenaX0;
            boss.arenaX1 = ArenaX1;
            boss.poolX0 = PoolX0;
            boss.poolX1 = PoolX1;
            boss.surfaceY = Surface;
            boss.poolBottom = -PoolDepth;
            boss.maxHp = 12;
            var walls = new List<Collider2D>();
            foreach (var wx in new[] { ArenaX0 - 0.5f, ArenaX1 + 0.5f })
            {
                var w = new GameObject($"Barriere_Arene_x{wx:0.#}");
                w.transform.SetParent(group, false);
                var c = w.AddComponent<BoxCollider2D>();
                c.offset = new Vector2(wx, 5f);
                c.size = new Vector2(1f, 40f);
                c.sharedMaterial = frictionless;
                walls.Add(c);
            }
            boss.walls = walls.ToArray();
        }

        /// <summary>Pagne noué autour du tronc d'un arbre sacré (à la hauteur y).</summary>
        static void Pagne(Transform parent, float x, float y, int art) =>
            Place(parent, $"Pagne_0{art}", x, BackOfStreet + y, 1.1f, -1).name = $"Pagne_Noue_x{x:0}";

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
            settings.AddComponent<StageState>().LevelNumber = 8;
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
            SpritePrefab("CielCoucher", Art3 + "Sky/sky_sunset.jpg", "Ciel");
            SpritePrefab("CielNuit", Art3 + "Sky/sky_night.jpg", "Ciel");
            for (int i = 1; i <= 3; i++) SpritePrefab($"Nuage_0{i}", Art1 + $"Clouds/cloud_0{i}.png", "Nuages");
            SpritePrefab("Mer", Art2 + "Water/sea.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("Plante_Herbes", Art2 + "Plants/plant_01.png", "Monde");
            SpritePrefab("Plante_Buisson", Art2 + "Plants/plant_03.png", "Monde");
            SpritePrefab("Plante_Touffes", Art2 + "Plants/plant_06.png", "Monde");
            SpritePrefab("HorizonForet", Art6 + "Horizon/horizon_forest.png", "Horizon", SpriteDrawMode.Tiled);
            for (int i = 1; i <= 3; i++) SpritePrefab($"Hevea_0{i}", Art6 + $"Trees/hevea_0{i}.png", "Monde");
            SpritePrefab("Branche", Art6 + "Trees/branch_01.png", "Monde");
            SpritePrefab("Corde", Art6 + "Wood/rope.png", "Monde");
            SpritePrefab("CordeNoeuds", Art6 + "Wood/rope_knots.png", "Monde");
            SpritePrefab("SolJardin", Art7 + "Ground/ground_garden.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SolJardinHerbe", Art7 + "Ground/ground_garden_grass.png", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SousLEau", Art7 + "Water/underwater.jpg", "Monde", SpriteDrawMode.Tiled);
            // Stage 8
            SpritePrefab("SolPierres", Art + "Ground/ground_stones.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SolPierresDessus", Art + "Ground/ground_stones_top.png", "Monde", SpriteDrawMode.Tiled);
            for (int i = 1; i <= 3; i++) SpritePrefab($"Fromager_0{i}", Art + $"Trees/fromager_0{i}.png", "Rue");
            SpritePrefab("Racines", Art + "Trees/roots.png", "Monde");
            for (int i = 1; i <= 4; i++) SpritePrefab($"Statue_0{i}", Art + $"Sacred/statue_0{i}.png", "Monde");
            for (int i = 1; i <= 4; i++) SpritePrefab($"Pagne_0{i}", Art + $"Sacred/pagne_0{i}.png", "Monde");
            SpritePrefab("Offrande", Art + "Sacred/offrande.png", "Monde");
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
