using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Construit le stage 5, « D'Adjamé à Yopougon » (menu Super Yao > Construire le stage 5), à la tombée du jour : le
    /// grand marché d'Adjamé (gare routière, halle, boutiques, étals, passants), les maquis de la rue Princesse, puis le
    /// corridor de Gesco et ses conteneurs. Nouveautés : les bâches des étals qui font rebondir (Bouncer), les gbakas et
    /// les charrettes qui foncent (Enemy), les conteneurs à escalader, et le gnambro (GnambroBoss) dans l'arène du corridor.
    /// Reprend les stages précédents : ciels, nuages, oiseaux, sols, cocotiers, immeubles du stage 2 au loin, lampadaires
    /// allumés et halos du stage 4, Yao à pied et son skate, CourseKit.
    /// </summary>
    public static class Stage05Builder
    {
        const string Art = "Assets/SuperYao/Backgrounds/Stage5/";
        const string Art4 = "Assets/SuperYao/Backgrounds/Stage4/";
        const string Art3 = "Assets/SuperYao/Backgrounds/Stage3/";
        const string Art2 = "Assets/SuperYao/Backgrounds/Stage2/";
        const string Art1 = "Assets/SuperYao/Backgrounds/Stage1/";
        const string PrefabDir = "Assets/SuperYao/Prefabs/Stage5";
        const string ScenePath = "Assets/SuperYao/Scenes/Stage05_AdjameYopougon.unity";
        const string HeroPrefab = "Assets/SuperYao/Prefabs/Heros/Heros_Yao.prefab";
        const string GameplayDir = "Assets/SuperYao/Gameplay/";
        const string FrictionlessPath = "Assets/SuperYao/Physics/SansFrottement.physicsMaterial2D";
        const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string Tagline = "Traverse le marché d'Adjamé entre les gbakas, file vers les maquis de Yopougon et force le passage du corridor !";

        const float FSky = 0f, FClouds = 0.05f, FHorizon = 0.15f, FFar = 0.35f, FMid = 0.65f, FStreet = 0.9f, FFront = 1.1f;
        const float VisibleRows = 20f;
        const float HalfViewMin = VisibleRows / 2f * 4f / 3f, HalfViewMax = VisibleRows / 2f * 2.4f;

        // Parcours (350 cases) : marché d'Adjamé | rue Princesse (Yopougon) | corridor de Gesco.
        const float LevelLength = 350f, PrincesseStart = 130f, CorridorStart = 250f, GoalX = 347.5f;
        // L'arène du gnambro, au bout du corridor (barrières en x), et sa place au départ.
        const float ArenaX0 = 310f, ArenaX1 = 344f, BossX = 336f;
        /// <summary>Échelle des planches du gnambro (téléphone : 46 px pour 217 px de planche).</summary>
        const float GnUnit = 46f / 16f / 217f;
        static readonly float[] ZoneEnds = { PrincesseStart, CorridorStart, float.MaxValue };
        static readonly string[] ZoneNames = { "Z1_Marche_Adjame", "Z2_Rue_Princesse", "Z3_Corridor_Gesco" };
        const float LateriteTop = 0.15f, AsphaltTop = 0.2f, BackOfStreet = 0.18f;
        // Le jour tombe : coucher de soleil au marché, presque la nuit au corridor.
        const float DuskFrom = 40f, DuskTo = 180f, NightFrom = 230f, NightTo = 400f;
        // Conteneurs (555 × 268 px) : 2,3 cases de haut, soit 4,76 de long ; tentes : 3,6 cases de haut, bâche à 75 %.
        const float ContainerH = 2.3f, ContainerW = ContainerH * 555f / 268f, TentH = 3.6f;
        // Le grand trou de la rue Princesse (eau au fond) : on le franchit avec le chapeau à hélice, ramassé juste avant,
        // porté par un courant d'air au bord du trou.
        const float PitX0 = 205f, PitX1 = 224f, CapX = 196f;
        // Le pont routier du corridor : rampe qui monte, passage à 3 cases de haut, descente rapide (blocs de béton).
        const float RampUp0 = 232f, RampUp1 = 244f, RampDown0 = 250f, RampDown1 = 262f, DeckRise = 3f;
        const float DeckHeight = 297f / 160f, DeckFeet = 118f / 160f;
        static float DeckY(float x) =>
            x <= RampUp0 || x >= RampDown1 ? 0f
            : x < RampUp1 ? DeckRise * (x - RampUp0) / (RampUp1 - RampUp0)
            : x <= RampDown0 ? DeckRise
            : DeckRise * (RampDown1 - x) / (RampDown1 - RampDown0);

        static Material spriteMaterial;
        static PhysicsMaterial2D frictionless;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly List<string> missing = new List<string>();

        [MenuItem("Super Yao/Construire le stage 5 (Adjamé - Yopougon)")]
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
                EditorUtility.DisplayDialog("Super Yao", "Lance d'abord « Construire le stage 1 » : il fabrique Yao, que le stage 5 réutilise.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            prefabs.Clear();
            missing.Clear();
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            frictionless = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(FrictionlessPath);
            EnsureFolder(PrefabDir);
            CourseKit.Init(spriteMaterial, frictionless);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreatePrefabs();
            if (missing.Count > 0)
            {
                Debug.LogError("Super Yao : images manquantes, stage 5 non construit :\n" + string.Join("\n", missing));
                return;
            }

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
            BuildHorizon(Layer(decor, "03_Horizon_Adjame_Yopougon", FHorizon, 1f));
            BuildFar(Layer(decor, "04_Immeubles_Lointains", FFar, 1f));
            BuildMid(Layer(decor, "05a_Marche_Maquis_Conteneurs", FMid, 1f));
            BuildStreet(Layer(decor, "05b_Cocotiers", FStreet, 1f));

            var world = new GameObject("06_Monde_Jouable").transform;
            var ground = BuildGround(world);
            var props = BuildProps(world);
            BuildCourse(world);
            BuildBoss(world);
            var yao = (GameObject)PrefabUtility.InstantiatePrefab(hero, world);
            yao.transform.localPosition = new Vector3(4f, 0.02f, 0f);
            yao.AddComponent<HeroRespawn>();
            Stage02Builder.PutOnSkate(yao);
            var helico = Stage04Builder.HelicoSet();
            if (helico != null) yao.GetComponentInChildren<HeroAnimator>().flySet = helico;
            BuildForeground(Layer(new GameObject("07_Avant_Plan").transform, "Vegetation_Avant_Plan", FFront, 1f));

            var dn = new GameObject("Jour_Nuit").AddComponent<DayNight>();
            dn.sunsetSky = sunset;
            dn.nightSky = night;
            dn.duskFrom = DuskFrom; dn.duskTo = DuskTo; dn.nightFrom = NightFrom; dn.nightTo = NightTo;
            dn.tintRoots = new[] { decor, ground, props };

            BuildCamera(yao.transform);
            BuildSettings();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log($"Super Yao : stage 5 construit ({LevelLength:0} cases) dans {ScenePath}.");
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

        /// <summary>Horizon : Adjamé (tours, mosquée, halle) puis Yopougon (quartiers bas, antennes), dont la bande commence
        /// par une copie fondue sur la gauche.</summary>
        static void BuildHorizon(Transform layer)
        {
            var (min, max) = LocalRange(FHorizon);
            float yopStart = FHorizon * (PrincesseStart - 10f);
            const float adjameH = 4f, yopH = 2.6f;
            var start = Place(layer, "HorizonYopougonDebut", 0f, 0.1f, yopH, 1);
            float sw = start.sprite.bounds.size.x * start.transform.localScale.x;
            start.transform.localPosition = new Vector3(yopStart + sw / 2f, 0.1f, 0f);
            Band(layer, "HorizonAdjame", min, yopStart + sw * 0.4f, 0.1f, adjameH, 0);
            Band(layer, "HorizonYopougon", yopStart + sw, max, 0.1f, yopH, 1);
        }

        static void BuildFar(Transform layer)
        {
            const string sort = "BatimentsLointains";
            var tint = new Color(0.82f, 0.82f, 0.9f);
            float x = LocalRange(FFar).min, max = LocalRange(FFar).max;
            int[] order = { 8, 11, 9, 13, 10, 12, 5, 7 };
            float[] heights = { 10f, 11.5f, 11f, 9.5f, 10f, 12f, 6.5f, 9f };
            for (int k = 0; x < max; k++)
            {
                int b = order[k % order.Length];
                var sr = Place(layer, $"Lointain_{b:00}", 0f, 0f, heights[k % heights.Length] * 0.55f, 0, k % 3 == 1, tint, sort);
                float w = sr.sprite.bounds.size.x * sr.transform.localScale.x;
                sr.transform.localPosition = new Vector3(x + w / 2f, 0f, 0f);
                x += w + 0.6f;
            }
        }

        /// <summary>Plan intermédiaire (x local) : la gare routière et ses gbakas garés, la halle et les boutiques du marché,
        /// les maquis de la rue Princesse (dessinés éclairés : ils gardent leurs couleurs la nuit), puis des piles de
        /// conteneurs au corridor.</summary>
        static void BuildMid(Transform layer)
        {
            const string sort = "PlanIntermediaire";
            var items = new (string prefab, float x, float h, bool flip, int order)[]
            {
                ("Gare", 12f, 5f, false, 0), ("Van_01", 5f, 1.5f, false, 1), ("Van_03", 10.5f, 1.5f, true, 1), ("Van_06", 18f, 1.5f, false, 1),
                ("Marche_01", 33f, 5.5f, false, 0), ("Marche_02", 45f, 4.5f, false, 0), ("Marche_05", 55f, 5f, false, 0),
                ("Marche_06", 63f, 4.6f, false, 0), ("Marche_02", 72f, 4.5f, true, 0), ("Marche_01", 81f, 5.5f, true, 0),
                ("Maquis_01", 93f, 5.5f, false, 0), ("Maquis_02", 104f, 5f, false, 0), ("Maquis_03", 116f, 6f, false, 0),
                ("Maquis_04", 127f, 5f, false, 0), ("Maquis_01", 139f, 5.5f, true, 0), ("Maquis_03", 151f, 6f, true, 0), ("Maquis_02", 161f, 5f, true, 0),
                ("Cocotier_02", 24f, 9f, false, -1), ("Cocotier_01", 86f, 9.5f, true, -1), ("Arbre", 166f, 7f, false, -1),
            };
            foreach (var i in items)
            {
                var sr = Place(Zone(layer, FMid, i.x), i.prefab, i.x, 0f, i.h, i.order, i.flip, null, sort);
                if (i.prefab.StartsWith("Maquis")) sr.name = $"Nuit_{i.prefab}";
            }
            // Piles de conteneurs au corridor : (x local, hauteur en conteneurs).
            var piles = new (float x, int n)[] { (171f, 2), (176f, 1), (186f, 3), (198f, 2), (203f, 1), (214f, 3), (227f, 2), (240f, 1), (245f, 3) };
            int color = 0;
            foreach (var p in piles)
                for (int k = 0; k < p.n; k++)
                    Place(Zone(layer, FMid, p.x), $"Conteneur_0{color++ % 6 + 1}", p.x + (k % 2) * 0.6f, k * ContainerH * 0.8f, ContainerH * 0.8f, 0, false, new Color(0.85f, 0.85f, 0.9f), sort);
        }

        static void BuildStreet(Transform layer)
        {
            var palms = new (string prefab, float wx, float h, bool flip)[]
                { ("Cocotier_04", 20f, 6f, false), ("Cocotier_01", 62f, 10.5f, true), ("Cocotier_04", 120f, 6f, true), ("Cocotier_03", 245f, 10f, false) };
            foreach (var p in palms) Place(Zone(layer, FStreet, FStreet * p.wx), p.prefab, FStreet * p.wx, 0f, p.h, 1, p.flip);
        }

        // ---------------------------------------------------------------- monde jouable

        static Transform BuildGround(Transform world)
        {
            var ground = new GameObject("Sol").transform;
            ground.SetParent(world, false);
            GroundSpan(ground, "SolLaterite", -2f, PrincesseStart + 0.6f, LateriteTop);
            GroundSpan(ground, "SolAsphalte", PrincesseStart - 0.6f, PitX0, AsphaltTop);
            GroundSpan(ground, "SolAsphalte", PitX1, LevelLength + 2f, AsphaltTop);
            SolidBox(ground, "Collision_Marche_Princesse", -1f, PitX0, 0f, 3f);
            SolidBox(ground, "Collision_Corridor", PitX1, LevelLength + 1f, 0f, 3f);
            // Le grand trou : de l'eau au fond, un courant d'air au bord (il ne porte que Yao coiffé de l'hélice).
            var water = Band(ground, "Mer", PitX0 - 0.4f, PitX1 + 0.4f, -2.8f, 2.45f, -1);
            water.sortingLayerName = "Monde";
            water.gameObject.name = "Trou_Eau";
            var fx = water.gameObject.AddComponent<WaterFx>();
            fx.drift = 0.7f; fx.glintSize = 0.3f; fx.glintDepth = 0.4f; fx.casesPerGlint = 3.5f;
            var up = new GameObject("Courant_Trou");
            up.transform.SetParent(ground, false);
            up.transform.localPosition = new Vector3(PitX0 + 1.8f, 0f, 0f);
            var ubox = up.AddComponent<BoxCollider2D>();
            ubox.isTrigger = true;
            ubox.size = new Vector2(3f, 11.5f);
            ubox.offset = new Vector2(0f, (-1.5f + 10f) / 2f);
            var draft = up.AddComponent<Updraft>();
            draft.material = spriteMaterial;
            draft.wisps = 16;
            // Le pont routier : collision du profil (rampe, passage, descente), tablier du stage 3, une pile.
            var deck = new GameObject("Pont_Routier").transform;
            deck.SetParent(ground, false);
            var pc = deck.gameObject.AddComponent<PolygonCollider2D>();
            pc.points = new[] { new Vector2(RampUp0, 0f), new Vector2(RampUp1, DeckRise), new Vector2(RampDown0, DeckRise), new Vector2(RampDown1, 0f) };
            pc.sharedMaterial = frictionless;
            foreach (var (a, b) in new[] { (RampUp0, RampUp1), (RampUp1, RampDown0), (RampDown0, RampDown1) }) DeckPiece(deck, a, b);
            float deckBottom = DeckRise + DeckFeet - DeckHeight;
            Place(deck, "Pile", (RampUp1 + RampDown0) / 2f, 0f, deckBottom + 0.2f, 0);
            Wall(ground, "Mur_Debut", -0.5f);
            Wall(ground, "Mur_Fin", LevelLength + 0.5f);
            return ground;
        }

        /// <summary>Tronçon de tablier (image du stage 3) entre x0 et x1, incliné sur les rampes.</summary>
        static void DeckPiece(Transform parent, float x0, float x1)
        {
            var a = new Vector2(x0, DeckY(x0));
            var b = new Vector2(x1, DeckY(x1));
            var d = b - a;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float len = d.magnitude + (Mathf.Abs(angle) > 0.1f ? 0.4f : 0f);
            var sr = Band(parent, "Tablier", -len / 2f, len / 2f, 0f, DeckHeight, 1);
            sr.gameObject.name = $"Tablier_x{x0:0}-{x1:0}";
            var rot = Quaternion.Euler(0f, 0f, angle);
            sr.transform.localRotation = rot;
            sr.transform.localPosition = (Vector3)((a + b) / 2f) - rot * new Vector3(0f, DeckHeight - DeckFeet, 0f);
        }

        /// <summary>Décor au fond de la rue, derrière Yao : étals du marché, la vendeuse qui marche, le mobilier et
        /// les lampadaires des maquis, la signalisation, la guérite et la barrière du corridor.</summary>
        static Transform BuildProps(Transform world)
        {
            var props = new GameObject("Rue_Et_Vendeuse").transform;
            props.SetParent(world, false);
            var items = new (string prefab, float x, float y, float h, bool flip, int order)[]
            {
                // Marché d'Adjamé
                ("Marche_03", 8f, 0f, 3.2f, false, 1), ("Marche_04", 24f, 0f, 2.6f, false, 2), ("Marche_04", 58f, 0f, 2.6f, true, 2),
                ("Marche_03", 94f, 0f, 3.2f, true, 1), ("Plante_Buisson", PrincesseStart, -0.1f, 1.2f, false, 5),
                // Rue Princesse : tables, chaises, glacière, enceinte, comptoir, guirlandes
                ("Table_Bleue", 140f, 0f, 0.95f, false, 2), ("Chaise_Rouge", 138.6f, 0f, 1.05f, false, 3), ("Chaise_Blanche", 141.5f, 0f, 1.05f, true, 3),
                ("Glaciere", 152f, 0f, 0.75f, false, 2), ("Enceinte", 158f, 0f, 1.4f, false, 2), ("Comptoir", 182f, 0f, 1.1f, false, 2),
                ("Table_Blanche", 195f, 0f, 0.95f, false, 2), ("Chaise_Blanche", 193.6f, 0f, 1.05f, false, 3), ("Chaise_Rouge", 196.5f, 0f, 1.05f, true, 3),
("Table_Bleue", 226f, 0f, 0.95f, false, 2), ("Chaise_Rouge", 227.5f, 0f, 1.05f, true, 3), ("Enceinte", 229.5f, 0f, 1.4f, true, 2),

                ("Guirlande", 146f, 3.2f, 0.9f, false, 1), ("Guirlande", 190f, 3.3f, 0.9f, false, 1), ("Guirlande", 227f, 3.2f, 0.9f, false, 1),
                // Corridor de Gesco
                ("Panneau_Bleu", 296f, 0f, 2.8f, false, 2), ("Portique", 302f, 0f, 4.6f, false, 1), ("Barriere_Foule", 326f, 0f, 1.1f, false, 2),
                ("Barriere_Foule", 328.2f, 0f, 1.1f, false, 2), ("Guerite", 334f, 0f, 2.6f, false, 2), ("Barriere_Levee", 338.5f, 0f, 2.8f, false, 3),
                ("Barriere_Fermee", 349.3f, 0f, 1.6f, false, 3),
            };
            foreach (var i in items)
            {
                var sr = Place(Zone(props, 1f, i.x), i.prefab, i.x, BackOfStreet + i.y, i.h, i.order, i.flip);
                if (i.prefab == "Guirlande") sr.name = "Nuit_Guirlande";
            }
            // La vendeuse au plateau de fruits fait ses allers-retours dans le marché.
            var seller = Frames(Art + "People").Where(s => s.name.StartsWith("seller")).ToArray();
            foreach (var (from, to) in new[] { (10f, 44f), (70f, 114f) })
            {
                var sr = CourseKit.Renderer(Zone(props, 1f, from), "Vendeuse", seller[0], 3, 1.95f);
                sr.transform.localPosition = new Vector3(from, BackOfStreet, 0f);
                Background(sr);
                var w = sr.gameObject.AddComponent<DecorWalker>();
                w.frames = seller;
                w.fromX = from;
                w.toX = to;
            }
            // Lampadaires allumés (stage 4) le long des maquis, petits lampadaires du corridor, avec leur halo.
            var lamps = new (string prefab, float x, float h, Vector2[] heads)[]
            {
                ("Lampadaire_01", 135f, 4.2f, new[] { new Vector2(0.802f, 0.919f) }), ("Lampadaire_03", 165f, 4.6f, new[] { new Vector2(0.097f, 0.89f), new Vector2(0.895f, 0.889f) }),
                ("Lampadaire_02", 200f, 4f, new[] { new Vector2(0.773f, 0.89f) }), ("Lampadaire_04", 230f, 3f, new[] { new Vector2(0.5f, 0.887f) }),
                ("Lampadaire_01", 265f, 4.2f, new[] { new Vector2(0.802f, 0.919f) }),
                ("Lampe_Corridor", 290f, 2.6f, new[] { new Vector2(0.5f, 0.93f) }), ("Lampe_Corridor", 320f, 2.6f, new[] { new Vector2(0.5f, 0.93f) }),
            };
            foreach (var l in lamps)
            {
                var sr = Place(Zone(props, 1f, l.x), l.prefab, l.x, BackOfStreet, l.h, 1);
                sr.name = $"Nuit_{l.prefab}";
                float w = sr.sprite.bounds.size.x * sr.transform.localScale.x;
                foreach (var head in l.heads)
                {
                    var g = Place(Zone(props, 1f, l.x), "Halo", l.x + (head.x - 0.5f) * w, BackOfStreet + head.y * l.h - 1.2f, 2.4f, 2);
                    g.name = "Nuit_Halo";
                }
            }
            return props;
        }

        /// <summary>Personnage du décor (passants, vendeuse) : dessiné sur le calque du plan intermédiaire, juste devant les
        /// bâtiments, pour qu'il prenne la même brume (Haze) et ne se confonde pas avec Yao et les ennemis.</summary>
        static void Background(SpriteRenderer sr)
        {
            sr.sortingLayerName = "PlanIntermediaire";
            sr.sortingOrder = 9000;
        }

        /// <summary>
        /// Le parcours (350 cases) : coffres et pièces, bâches d'étal qui font rebondir (plus haut en gardant le bouton de
        /// saut), conteneurs en escalier et une tour de 3 conteneurs avec le diamant (il faut rebondir sur la bâche d'à
        /// côté en gardant le bouton), le grand trou de la rue Princesse (chapeau à hélice et courant d'air), le pont
        /// routier du corridor (descente rapide, blocs de béton). Danger des ennemis 30,5 (agame 1, mouette 1,5, vautour
        /// et taxi 2, charrette 3, gbaka 4 ; cible du téléphone pour le stage 5 : 29 ± 20 %), le gnambro viendra en plus.
        /// Un seul point de contrôle, au milieu. Le gnambro attend dans l'arène, au bout du corridor (BuildBoss).
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

            // Bâches qui font rebondir : (x, image).
            var tents = new (float x, int art)[] { (34f, 2), (78f, 1), (112f, 3), (168f, 4), (292f, 1) };
            foreach (var t in tents)
            {
                Tent(In(t.x), t.x, t.art);
                float top = TentH * 0.75f;
                items.Add((C, t.x - 1f, top + 3.5f)); items.Add((C, t.x, top + 4.5f)); items.Add((C, t.x + 1f, top + 3.5f));
            }

            // Conteneurs : escalier 1-2-3-2-1, puis une tour de 3 (diamant au sommet).
            const float stepsX = 268f;
            var steps = new (float x, int n)[] { (stepsX, 1), (stepsX + ContainerW, 2), (stepsX + 2 * ContainerW, 3), (stepsX + 3 * ContainerW, 2), (stepsX + 4 * ContainerW, 1) };
            int color = 0;
            foreach (var st in steps)
            {
                for (int k = 0; k < st.n; k++) Container(In(st.x), st.x, k, color++ % 6 + 1);
                Line(C, st.x - 1.5f, st.x + 1.5f, 1.5f, st.n * ContainerH + 1.2f);
            }
            float towerX = 299f;
            for (int k = 0; k < 3; k++) Container(In(towerX), towerX, k, (k + 3) % 6 + 1);
            items.Add((Collectible.Kind.Diamond, towerX, 3 * ContainerH + 1.3f));

            // Blocs de béton du corridor (obstacles à sauter, Yao en skate les percute).
            // Dans la descente du pont routier (posés sur la pente), puis au corridor.
            foreach (float bx in new[] { 254.5f, 258.5f })
            {
                float slopeDeg = bx > RampDown0 && bx < RampDown1 ? -Mathf.Atan(DeckRise / (RampDown1 - RampDown0)) * Mathf.Rad2Deg : 0f;
                var block = Place(In(bx), "Separateur", bx, DeckY(bx) - 0.05f, 1.0f, 4);
                block.transform.localRotation = Quaternion.Euler(0f, 0f, slopeDeg);
                block.name = $"Obstacle_Beton_x{bx:0}";
                var box = block.gameObject.AddComponent<BoxCollider2D>();
                var size = block.sprite.bounds.size;
                box.size = new Vector2(size.x * 0.9f, size.y * 0.85f);
                box.offset = new Vector2(0f, size.y * 0.85f / 2f);
                box.sharedMaterial = frictionless;
                block.gameObject.AddComponent<Obstacle>();
            }

            var blocks = new (BumpBlock.Kind kind, float x)[]
            {
                (BumpBlock.Kind.CoinBox, 10f), (BumpBlock.Kind.Brick, 14f), (BumpBlock.Kind.PowerBox, 15f), (BumpBlock.Kind.Brick, 16f),
                (BumpBlock.Kind.CoinSack, 100f),
                (BumpBlock.Kind.CoinBox, 172f), (BumpBlock.Kind.Brick, 185f), (BumpBlock.Kind.PowerBox, 186f), (BumpBlock.Kind.Brick, 187f),
                (BumpBlock.Kind.CoinBox, 306f),
            };
            foreach (var b in blocks) CourseKit.Block(In(b.x), b.kind, b.x);
            Line(C, 20f, 23f, 1f, 1.2f);
            Line(C, 60f, 63f, 1f, 1.2f);
            Line(B, 142f, 146f, 1f, 1.2f);
            Line(C, 190f, 194f, 1f, 3.4f);
            Line(B, 211f, 219.5f, 1.5f, 7f);                  // au-dessus du trou, sur le vol plané
            Line(C, 236f, 242f, 1.5f, 0f);                    // la rampe du pont routier (hauteurs plus bas)
            Line(C, 245f, 249f, 1f, DeckRise + 1.2f);
            for (int i = 0; i < items.Count; i++)
                if (items[i].y == 0f) items[i] = (items[i].kind, items[i].x, DeckY(items[i].x) + 1.2f);
            // Le chapeau à hélice, juste avant le trou.
            var cap = CourseKit.Renderer(In(CapX), "Chapeau_Helice", CourseKit.LoadSprite($"{GameplayDir}Items/propeller_cap.png"), 5, 1.1f, false);
            cap.transform.localPosition = new Vector3(CapX, 1.3f, 0f);
            cap.gameObject.AddComponent<HelicoCap>().dropAfterX = PitX1 + 0.5f; // rendu une fois le trou franchi
            CourseKit.Plank(In(317f), 317f, 3.4f, 1);   // l'arène : au-dessus du gbaka qui fonce
            CourseKit.Plank(In(330f), 330f, 3.4f, 1);
            Line(C, 318f, 320f, 1f, 4.6f);
            foreach (var it in items) CourseKit.Item(In(it.x), it.kind, it.x, it.y);

            float top3 = 3 * ContainerH;
            var E = new (Enemy.Kind kind, float x, float y)[]
            {
                // Marché (14)
                (Enemy.Kind.Agama, 18f, 0f), (Enemy.Kind.Cart, 40f, 0f), (Enemy.Kind.Vulture, 52f, 4.5f), (Enemy.Kind.Vulture, 63f, 4.5f),
                (Enemy.Kind.Agama, 84f, 0f), (Enemy.Kind.Gbaka, 96f, 0f),
                // Rue Princesse (10,5)
                (Enemy.Kind.Gull, 126f, 3.5f), (Enemy.Kind.Taxi, 134f, 0f), (Enemy.Kind.Cart, 148f, 0f), (Enemy.Kind.Agama, 160f, 0f),
                (Enemy.Kind.Agama, 188f, 0f), (Enemy.Kind.Vulture, 215f, 6f), (Enemy.Kind.Taxi, 228f, 0f),
                // Corridor (6)
                (Enemy.Kind.Agama, stepsX + 2 * ContainerW, top3), (Enemy.Kind.Vulture, 290f, 8.5f),
            };
            foreach (var e in E) CourseKit.EnemyAt(In(e.x), e.kind, e.x, e.y);

            CourseKit.Flag(In(175f), 175f, 3.5f, false); // un seul point de contrôle, au milieu
            CourseKit.Flag(In(GoalX), GoalX, 5.5f, true);

            var catalog = MenuBuilder.Catalog();
            var stage = catalog.stages[4];
            stage.scene = System.IO.Path.GetFileNameWithoutExtension(ScenePath);
            stage.tagline = Tagline;
            stage.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SuperYao/UI/Backgrounds/thumb_adjame.jpg");
            stage.coins = items.Count(i => i.kind == C) + blocks.Count(k => k.kind == BumpBlock.Kind.CoinBox)
                          + blocks.Count(k => k.kind == BumpBlock.Kind.CoinSack) * 8;
            stage.bananas = items.Count(i => i.kind == B);
            stage.diamonds = items.Count(i => i.kind == Collectible.Kind.Diamond);
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Le gnambro (images du téléphone), son gbaka (caché entre deux passages) et les barrières de l'arène.</summary>
        static void BuildBoss(Transform world)
        {
            var group = new GameObject("Combat_Gnambro").transform;
            group.SetParent(Zone(world, 1f, BossX), false);
            string dir = CourseKit.GameplayDir + "Boss/gn_";
            Sprite[] S(string n, int k) => CourseKit.LoadSprites($"{dir}{n}_", k);
            var idle = S("idle", 2);
            var sr = CourseKit.Renderer(group, "Gnambro", idle[0], 5, idle[0].rect.height * GnUnit);
            sr.transform.localPosition = new Vector3(BossX, 0f, 0f);
            float s = sr.transform.localScale.x;
            var box = sr.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.5f / s, 2.4f / s);
            box.offset = new Vector2(0f, 1.2f / s);
            sr.gameObject.AddComponent<Rigidbody2D>();
            var boss = sr.gameObject.AddComponent<GnambroBoss>();
            boss.idle = idle;
            boss.shout = S("shout", 2);
            boss.argue = S("argue", 2);
            boss.run = S("run", 3);
            boss.hit = S("hit", 2);
            boss.defeat = S("defeat", 2);
            boss.bag = S("bag", 1);
            boss.bus = S("bus", 4);
            boss.arenaX0 = ArenaX0;
            boss.arenaX1 = ArenaX1;
            boss.maxHp = 12; // troisième boss du téléphone : 12 points de vie, soit 6 sauts
            // Le gbaka qu'il appelle : même échelle que lui, caché au départ.
            var bus = CourseKit.Renderer(group, "Gbaka_Du_Gnambro", boss.bus[0], 4, boss.bus[0].rect.height * GnUnit);
            bus.transform.localPosition = new Vector3(ArenaX0 + 2f, 0f, 0f);
            float sb = bus.transform.localScale.x;
            var busBox = bus.gameObject.AddComponent<BoxCollider2D>();
            busBox.isTrigger = true;
            busBox.size = new Vector2(3.4f / sb, 2.1f / sb);
            busBox.offset = new Vector2(0f, 1.05f / sb);
            bus.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            bus.gameObject.AddComponent<GnambroBus>().boss = boss;
            boss.busRenderer = bus;
            boss.busBox = busBox;
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

        /// <summary>Tente d'étal : l'image (Yao passe dessous) et, sur la bâche, une surface où l'on se pose par au-dessus et
        /// qui fait rebondir.</summary>
        static void Tent(Transform parent, float x, int art)
        {
            var sr = Place(parent, $"Tente_0{art}", x, BackOfStreet, TentH, 1);
            float w = sr.sprite.bounds.size.x * sr.transform.localScale.x;
            var go = new GameObject($"Bache_Rebond_x{x:0}");
            go.transform.SetParent(parent, false);
            float top = BackOfStreet + TentH * 0.75f;
            CourseKit.OneWay(go, x, top, w * 0.6f);
            var b = go.AddComponent<Bouncer>();
            b.visual = sr.transform;
        }

        /// <summary>Conteneur plein (on monte dessus), au niveau <paramref name="level"/> d'une pile en x.</summary>
        static void Container(Transform parent, float x, int level, int art)
        {
            var sr = Place(parent, $"Conteneur_0{art}", x, level * ContainerH, ContainerH, 3);
            sr.name = $"Conteneur_x{x:0.#}_n{level + 1}";
            var box = sr.gameObject.AddComponent<BoxCollider2D>();
            var size = sr.sprite.bounds.size;
            box.size = new Vector2(size.x * 0.98f, size.y);
            box.offset = new Vector2(0f, size.y / 2f);
            box.sharedMaterial = frictionless;
        }

        static void BuildForeground(Transform layer)
        {
            var tint = new Color(0.72f, 0.78f, 0.72f);
            var plants = new (string prefab, float wx, float h, bool flip)[]
                { ("Plante_Herbes", 12f, 1.3f, false), ("Plante_Touffes", 70f, 1.2f, false), ("Plante_Herbes", 205f, 1.3f, true), ("Plante_Touffes", 300f, 1.2f, true) };
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
            settings.AddComponent<StageState>().LevelNumber = 5;
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
            SpritePrefab("CielCoucher", Art3 + "Sky/sky_sunset.jpg", "Ciel");
            SpritePrefab("CielNuit", Art3 + "Sky/sky_night.jpg", "Ciel");
            for (int i = 1; i <= 3; i++) SpritePrefab($"Nuage_0{i}", Art1 + $"Clouds/cloud_0{i}.png", "Nuages");
            SpritePrefab("SolLaterite", Art2 + "Ground/ground_laterite.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("SolAsphalte", Art2 + "Ground/ground_asphalt.jpg", "Monde", SpriteDrawMode.Tiled);
            foreach (int b in new[] { 5, 7, 8, 9, 10, 11, 12, 13 }) SpritePrefab($"Lointain_{b:00}", Art2 + $"Buildings/building_{b:00}.png", "BatimentsLointains");
            for (int i = 1; i <= 4; i++) SpritePrefab($"Cocotier_0{i}", Art2 + $"Vegetation/palm_0{i}.png", "Rue");
            SpritePrefab("Arbre", Art2 + "Vegetation/tree_01.png", "Rue");
            SpritePrefab("Plante_Herbes", Art2 + "Plants/plant_01.png", "Monde");
            SpritePrefab("Plante_Buisson", Art2 + "Plants/plant_03.png", "Monde");
            SpritePrefab("Plante_Touffes", Art2 + "Plants/plant_06.png", "Monde");
            for (int i = 1; i <= 4; i++) SpritePrefab($"Lampadaire_0{i}", Art4 + $"Props/lamp_0{i}.png", "Monde");
            SpritePrefab("Halo", Art4 + "Props/glow.png", "Monde");
            SpritePrefab("Mer", Art2 + "Water/sea.jpg", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("Tablier", Art3 + "Bridge/deck.png", "Monde", SpriteDrawMode.Tiled);
            SpritePrefab("Pile", Art3 + "Bridge/pillar_big.png", "Monde");
            // Stage 5
            SpritePrefab("HorizonAdjame", Art + "Horizon/horizon_adjame.png", "Horizon", SpriteDrawMode.Tiled);
            SpritePrefab("HorizonYopougon", Art + "Horizon/horizon_yopougon.png", "Horizon", SpriteDrawMode.Tiled);
            SpritePrefab("HorizonYopougonDebut", Art + "Horizon/horizon_yopougon_start.png", "Horizon");
            SpritePrefab("Gare", Art + "Buildings/gare.png", "PlanIntermediaire");
            for (int i = 1; i <= 6; i++)
            {
                SpritePrefab($"Marche_0{i}", Art + $"Buildings/market_0{i}.png", "PlanIntermediaire");
                SpritePrefab($"Conteneur_0{i}", Art + $"Containers/container_0{i}.png", "Monde");
                SpritePrefab($"Van_0{i}", Art + $"Vans/van_0{i}.png", "PlanIntermediaire");
            }
            for (int i = 1; i <= 4; i++)
            {
                SpritePrefab($"Maquis_0{i}", Art + $"Buildings/maquis_0{i}.png", "PlanIntermediaire");
                SpritePrefab($"Tente_0{i}", Art + $"Tents/tent_0{i}.png", "Monde");
            }
            string[] furniture = { "Table_Blanche", "Table_Bleue", "Chaise_Blanche", "Chaise_Rouge", "Glaciere", "Enceinte", "Guirlande", "Comptoir" };
            for (int i = 1; i <= furniture.Length; i++) SpritePrefab(furniture[i - 1], Art + $"Furniture/furniture_{i:00}.png", "Monde");
            SpritePrefab("Barriere_Fermee", Art + "Corridor/gear_01.png", "Monde");
            SpritePrefab("Barriere_Levee", Art + "Corridor/gear_02.png", "Monde");
            SpritePrefab("Guerite", Art + "Corridor/gear_03.png", "Monde");
            SpritePrefab("Separateur", Art + "Corridor/gear_04.png", "Monde");
            SpritePrefab("Portique", Art + "Corridor/sign_01.png", "Monde");
            SpritePrefab("Panneau_Bleu", Art + "Corridor/sign_02.png", "Monde");
            SpritePrefab("Lampe_Corridor", Art + "Corridor/sign_03.png", "Monde");
            SpritePrefab("Barriere_Foule", Art + "Corridor/sign_04.png", "Monde");
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
