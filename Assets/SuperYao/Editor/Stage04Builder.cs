using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Construit le stage 4, « Le Plateau la nuit » (menu Super Yao > Construire le stage 4) — PROTOTYPE du vol : Yao
    /// porte l'hélice et traverse le Plateau par les airs, de toit en toit. Les courants ascendants (Updraft) le font
    /// monter ; en l'air, il plane tant qu'on garde le bouton de saut ; tomber entre deux tours coûte une vie.
    /// Décor : la nuit du stage 3 (ciel, lune, horizon, monuments, immeubles), les immeubles de nuit, lampadaires
    /// allumés et le stade Félix-Houphouët-Boigny du stage 4 ; le parcours finit sur le grand toit-terrasse (assemblé
    /// de trois morceaux de façade), où viendra la reine des roussettes.
    /// </summary>
    public static class Stage04Builder
    {
        const string Art4 = "Assets/SuperYao/Backgrounds/Stage4/";
        const string Art3 = "Assets/SuperYao/Backgrounds/Stage3/";
        const string Art1 = "Assets/SuperYao/Backgrounds/Stage1/";
        const string PrefabDir = "Assets/SuperYao/Prefabs/Stage4";
        const string ScenePath = "Assets/SuperYao/Scenes/Stage04_PlateauNuit.unity";
        const string HeroPrefab = "Assets/SuperYao/Prefabs/Heros/Heros_Yao.prefab";
        const string GameplayDir = "Assets/SuperYao/Gameplay/";
        const string FrictionlessPath = "Assets/SuperYao/Physics/SansFrottement.physicsMaterial2D";
        const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string Tagline = "Le Plateau s'illumine : coiffé de ton hélice, profite des courants d'air pour voler de tour en tour !";

        const float FClouds = 0.05f, FHorizon = 0.15f, FFar = 0.35f, FMid = 0.65f;
        const float VisibleRows = 20f;
        const float HalfViewMin = VisibleRows / 2f * 4f / 3f, HalfViewMax = VisibleRows / 2f * 2.4f;

        // Toits : (largeur, hauteur, ligne du toit, bords du toit, en px ; échelle en cases par px). 1-6 : immeubles de nuit du
        // stage 3 (un étage ≈ 2,4 cases pour 70 px) ; 7-10 : ceux du stage 4, dessinés plus petits (un étage ≈ 40 px).
        const float RoofScale = 2.4f / 70f, NightScale = 2.4f / 41f;
        static readonly (int w, int h, int roof, int x0, int x1, float scale)[] RoofArt =
        {
            (418, 607, 131, 18, 416, RoofScale), (746, 475, 100, 3, 743, RoofScale), (343, 832, 126, 14, 327, RoofScale),
            (456, 525, 72, 1, 454, RoofScale), (533, 784, 105, 1, 531, RoofScale), (451, 478, 103, 1, 449, RoofScale),
            (238, 815, 39, 3, 234, NightScale), (344, 628, 45, 2, 341, NightScale), (356, 484, 35, 0, 355, NightScale), (432, 398, 44, 1, 430, NightScale),
        };
        // Grand toit-terrasse (arène) : bord gauche, morceaux du milieu répétés, bord droit (887 px de large, toit à 52 px
        // du haut de l'image recadrée, 1334 px de haut) ; un étage fait 385 px, soit 3 cases.
        const float TerraceScale = 3f / 385f, TerracePieceW = 887f, TerracePieceH = 1334f, TerraceRoof = 52f;
        const int TerraceMiddles = 3;
        static float TerraceWidth => TerracePieceW * TerraceScale * (TerraceMiddles + 2);
        // Parcours : (image, hauteur du toit, écart avant, courant au bout du toit, courant au milieu de l'écart avant ce
        // toit). Image 0 = le toit-terrasse. Les écarts sont trop grands pour un saut : il faut un courant, puis planer.
        static readonly (int art, float top, float gap, bool updraft, bool gapUpdraft)[] RoofPath =
        {
            (2, 3f, 0f, true, false), (7, 7f, 12f, true, false), (3, 9f, 13f, false, false), (8, 5f, 10f, true, false),
            (6, 7f, 18f, true, true), (5, 11f, 12f, true, false), (9, 8f, 14f, true, false), (3, 12f, 12f, false, false),
            (1, 6f, 10f, true, false), (10, 9f, 20f, true, true), (6, 13f, 12f, false, false), (4, 7f, 10f, true, false),
            (0, 6f, 14f, false, false),
        };
        static float ArtWidth(int art) => art == 0 ? TerraceWidth : RoofArt[art - 1].w * RoofArt[art - 1].scale;
        const float FirstRoofX = -2f;
        /// <summary>Distance horizontale parcourue en planant par case de chute (vitesse de course 8,5 / chute 2,5 = 3,4 ;
        /// on garde de la marge).</summary>
        const float GlideRatio = 3f;

        static Material spriteMaterial;
        static PhysicsMaterial2D frictionless;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly List<string> missing = new List<string>();
        static readonly List<(float x0, float x1, float top)> roofs = new List<(float x0, float x1, float top)>();
        static readonly List<(float x, float bottom, float top, float width)> updrafts = new List<(float, float, float, float)>();
        static float levelLength;

        [MenuItem("Super Yao/Construire le stage 4 (Plateau la nuit)")]
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
                EditorUtility.DisplayDialog("Super Yao", "Lance d'abord « Construire le stage 1 » : il fabrique Yao, que le stage 4 réutilise.", "OK");
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
                Debug.LogError("Super Yao : images manquantes, stage 4 non construit :\n" + string.Join("\n", missing));
                return;
            }
            ComputeRoofs();

            // Les couches lointaines suivent un peu la caméra quand Yao monte (facteur vertical < 1) : on les voit encore
            // depuis le haut du ciel.
            var decor = new GameObject("Decor").transform;
            var sky = Layer(decor, "01_Ciel", 0f, 0f);
            Place(sky, "CielNuit", 0f, -7f, 14f, 0).gameObject.AddComponent<FitToCamera>();
            Place(sky, "Lune", 9f, 3.2f, 2.6f, 3);
            BuildClouds(Layer(decor, "02_Nuages", FClouds, 0f));
            BuildHorizon(Layer(decor, "03_Horizon_Plateau_Nuit", FHorizon, 0.35f));
            BuildFar(Layer(decor, "04_Monuments", FFar, 0.55f));
            BuildMid(Layer(decor, "05_Immeubles", FMid, 0.75f));

            var world = new GameObject("06_Monde_Jouable").transform;
            BuildRoofs(world);
            BuildUpdrafts(world);
            BuildLamps(world);
            BuildCourse(world);
            var yao = (GameObject)PrefabUtility.InstantiatePrefab(hero, world);
            yao.transform.localPosition = new Vector3(3f, roofs[0].top + 0.02f, 0f);
            yao.AddComponent<HeroRespawn>();
            yao.GetComponent<HeroController>().canFly = true;
            var animator = yao.GetComponentInChildren<HeroAnimator>();
            var helico = HelicoSet();
            if (helico != null)
            {
                animator.hero = helico;
                animator.GetComponent<SpriteRenderer>().sprite = helico.FirstSprite;
            }
            animator.skateSet = null; // pas de skate dans ce stage : l'hélice suffit
            var switcher = yao.GetComponent<HeroSwitcher>();
            if (switcher != null) switcher.heroes = new HeroAnimationSet[0];

            BuildCamera(yao.transform);
            BuildSettings();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log($"Super Yao : stage 4 (prototype du vol) construit ({levelLength:0} cases) dans {ScenePath}.");
        }

        // ---------------------------------------------------------------- décor (la nuit du stage 3)

        static void BuildClouds(Transform layer)
        {
            layer.gameObject.AddComponent<CloudDrift>();
            var night = new Color(0.45f, 0.5f, 0.75f);
            var clouds = new (int sprite, float x, float y, float h)[] { (1, -8f, 5f, 1.1f), (3, 2f, 3.4f, 2.0f), (2, 12f, 4.8f, 1.6f), (1, 20f, 2.8f, 1.0f) };
            foreach (var c in clouds) Place(layer, $"Nuage_0{c.sprite}", c.x, c.y, c.h, 0, false, night);
        }

        static void BuildHorizon(Transform layer)
        {
            var (min, max) = LocalRange(FHorizon);
            Band(layer, "HorizonNuit", min, max, 0.1f, 4.2f, 0);
        }

        static void BuildFar(Transform layer)
        {
            const string sort = "BatimentsLointains";
            var monuments = new (string prefab, float wx, float h)[]
                { ("Monument_01", 40f, 6.5f), ("Monument_03", 120f, 13f), ("Monument_02", 200f, 7f), ("Monument_03", 290f, 11f) };
            foreach (var m in monuments) Place(layer, m.prefab, FFar * m.wx, 0f, m.h, 0, false, null, sort);
            // Le stade Félix-Houphouët-Boigny, illuminé, avant le grand toit-terrasse.
            float stadeX = FFar * (roofs[roofs.Count - 1].x0 - 20f);
            var stade = Place(layer, "Stade", stadeX, -0.5f, 7f, 1, false, null, sort);
            float sw = stade.sprite.bounds.size.x * stade.transform.localScale.x;
            foreach (float fx in new[] { 0.072f, 0.24f, 0.76f, 0.93f }) // projecteurs
                Glow(layer, stadeX + (fx - 0.5f) * sw, -0.5f + 7f * (fx < 0.1f || fx > 0.9f ? 0.83f : 0.97f), 2.2f, sort, 2);
            float x = LocalRange(FFar).min, max = LocalRange(FFar).max;
            int[] order = { 5, 2, 7, 1, 4, 9, 6, 3, 10, 8 };
            for (int k = 0; x < max; k++)
            {
                var art = RoofArt[order[k % order.Length] - 1];
                float sc = art.scale * 0.5f, w = art.w * sc;
                Place(layer, $"Toit_{order[k % order.Length]:00}", x + w / 2f, 0f, art.h * sc, -1, false, new Color(0.7f, 0.72f, 0.85f), sort);
                x += w + 0.4f;
            }
        }

        static void BuildMid(Transform layer)
        {
            const string sort = "PlanIntermediaire";
            float x = LocalRange(FMid).min, max = LocalRange(FMid).max;
            int[] order = { 4, 8, 1, 6, 10, 3, 7, 5, 9, 2 };
            for (int k = 0; x < max; k++)
            {
                var art = RoofArt[order[k % order.Length] - 1];
                float sc = art.scale * 0.75f, w = art.w * sc;
                Place(layer, $"Toit_{order[k % order.Length]:00}", x + w / 2f, -4f, art.h * sc, 0, false, new Color(0.62f, 0.65f, 0.8f), sort);
                x += w + 1.5f;
            }
        }

        // ---------------------------------------------------------------- monde jouable

        static void ComputeRoofs()
        {
            roofs.Clear();
            updrafts.Clear();
            float cursor = FirstRoofX;
            foreach (var r in RoofPath)
            {
                cursor += r.gap;
                if (r.art == 0) roofs.Add((cursor + 0.2f, cursor + TerraceWidth - 0.2f, r.top));
                else
                {
                    var a = RoofArt[r.art - 1];
                    roofs.Add((cursor + a.x0 * a.scale, cursor + (a.x1 + 1) * a.scale, r.top));
                }
                cursor += ArtWidth(r.art);
            }
            levelLength = roofs[roofs.Count - 1].x1 + 1f;
            // Courants : au bout d'un toit (du toit jusqu'à une hauteur qui permet de planer jusqu'au toit suivant), ou au
            // milieu d'un grand écart (depuis le vide).
            for (int i = 0; i < RoofPath.Length; i++)
            {
                if (RoofPath[i].gapUpdraft && i > 0)
                {
                    float gx = (roofs[i - 1].x1 + roofs[i].x0) / 2f, half = (roofs[i].x0 - gx);
                    updrafts.Add((gx, -1.5f, roofs[i].top + half / GlideRatio + 2.5f, 3f));
                }
                if (RoofPath[i].updraft && i + 1 < roofs.Count)
                {
                    float ux = roofs[i].x1 - 2.5f;
                    float distance = (RoofPath[i + 1].gapUpdraft ? (roofs[i + 1].x0 - roofs[i].x1) / 2f : roofs[i + 1].x0 - roofs[i].x1) + 2.5f;
                    float target = RoofPath[i + 1].gapUpdraft ? roofs[i].top : roofs[i + 1].top;
                    updrafts.Add((ux, roofs[i].top, Mathf.Max(roofs[i].top + 6f, target + distance / GlideRatio + 2f), 2.4f));
                }
            }
        }

        static void BuildRoofs(Transform world)
        {
            var group = new GameObject("Toits_Plateau").transform;
            group.SetParent(world, false);
            float cursor = FirstRoofX;
            for (int i = 0; i < RoofPath.Length; i++)
            {
                var r = RoofPath[i];
                cursor += r.gap;
                var roof = roofs[i];
                if (r.art == 0)
                {
                    // Le grand toit-terrasse : bord gauche, morceaux du milieu, bord droit, une seule boîte de collision.
                    float pw = TerracePieceW * TerraceScale, ph = TerracePieceH * TerraceScale;
                    float baseY = r.top - (TerracePieceH - TerraceRoof) * TerraceScale;
                    for (int k = 0; k < TerraceMiddles + 2; k++)
                    {
                        string piece = k == 0 ? "TerrasseGauche" : k == TerraceMiddles + 1 ? "TerrasseDroite" : "TerrasseMilieu";
                        Place(group, piece, cursor + pw * (k + 0.5f), baseY, ph, 0).name = $"Terrasse_{k + 1}";
                    }
                    SolidBox(group, "Collision_Terrasse", roof.x0, roof.x1, r.top, 20f);
                }
                else
                {
                    var a = RoofArt[r.art - 1];
                    float w = a.w * a.scale, h = a.h * a.scale;
                    var sr = Place(group, $"Toit_{r.art:00}", cursor + w / 2f, r.top - (a.h - a.roof) * a.scale, h, 0);
                    sr.name = $"Toit_{i + 1}";
                    SolidBox(group, $"Collision_Toit_{i + 1}", roof.x0, roof.x1, r.top, 20f);
                }
                cursor += ArtWidth(r.art);
            }
            var walls = new GameObject("Murs").transform;
            walls.SetParent(world, false);
            Wall(walls, "Mur_Debut", -0.5f);
            Wall(walls, "Mur_Fin", levelLength + 0.5f);
        }

        /// <summary>Lampadaires allumés (planche du stage 4) sur quelques toits et sur le toit-terrasse, avec leur halo.</summary>
        static void BuildLamps(Transform world)
        {
            var group = new GameObject("Lampadaires").transform;
            group.SetParent(world, false);
            var R = roofs;
            var last = R[R.Count - 1];
            var lamps = new (int lamp, float x, float y, bool flip)[]
            {
                (2, R[1].x0 + 1.5f, R[1].top, false), (4, R[3].x0 + 2f, R[3].top, false), (4, R[6].x0 + 2f, R[6].top, false),
                (1, R[9].x0 + 2.5f, R[9].top, false), (4, R[11].x0 + 2f, R[11].top, false),
                (1, last.x0 + 3f, last.top, false), (3, (last.x0 + last.x1) / 2f, last.top, false), (1, last.x1 - 3f, last.top, true),
            };
            // Hauteur de chaque lampadaire (cases) et position de ses lampes (fractions de l'image, depuis le pied).
            float[] heights = { 4.2f, 4f, 4.6f, 3f };
            var heads = new[] { new[] { new Vector2(0.802f, 0.919f) }, new[] { new Vector2(0.773f, 0.89f) },
                new[] { new Vector2(0.097f, 0.89f), new Vector2(0.895f, 0.889f) }, new[] { new Vector2(0.5f, 0.887f) } };
            foreach (var l in lamps)
            {
                float h = heights[l.lamp - 1];
                var sr = Place(group, $"Lampadaire_0{l.lamp}", l.x, l.y, h, 1, l.flip);
                float w = sr.sprite.bounds.size.x * sr.transform.localScale.x;
                foreach (var head in heads[l.lamp - 1])
                {
                    float fx = l.flip ? 1f - head.x : head.x;
                    Glow(group, l.x + (fx - 0.5f) * w, l.y + head.y * h, 2.4f, "Monde", 2);
                }
            }
        }

        /// <summary>Halo de lumière (disque doux) centré en (x, y).</summary>
        static void Glow(Transform parent, float x, float y, float size, string sorting, int order)
        {
            var sr = Place(parent, "Halo", x, y - size / 2f, size, order, false, null, sorting);
            sr.name = "Nuit_Halo";
        }

        static void BuildUpdrafts(Transform world)
        {
            var group = new GameObject("Courants_Ascendants").transform;
            group.SetParent(world, false);
            foreach (var u in updrafts)
            {
                var go = new GameObject($"Courant_x{u.x:0.#}");
                go.transform.SetParent(group, false);
                go.transform.localPosition = new Vector3(u.x, 0f, 0f);
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(u.width, u.top - u.bottom);
                box.offset = new Vector2(0f, (u.top + u.bottom) / 2f);
                var up = go.AddComponent<Updraft>();
                up.material = spriteMaterial;
                up.wisps = Mathf.Clamp(Mathf.RoundToInt((u.top - u.bottom) * 1.2f), 8, 24);
            }
        }

        /// <summary>
        /// Pièces le long des courants et des vols planés, quelques coffres, le diamant au sommet d'un courant au-dessus du
        /// vide ; agames et crabes sur les toits, mouettes et vautours entre les tours. Danger des ennemis 28 (agame et crabe 1,
        /// mouette 1,5, vautour 2 ; cible du téléphone pour le stage 4 : 28 ± 20 %), le boss viendra en plus. Un seul point de
        /// contrôle, au milieu. En attendant le boss, l'arrivée est au bout du toit-terrasse.
        /// </summary>
        static void BuildCourse(Transform world)
        {
            var course = new GameObject("Parcours").transform;
            course.SetParent(world, false);
            var R = roofs;
            var items = new List<(Collectible.Kind kind, float x, float y)>();
            var C = Collectible.Kind.Coin;
            foreach (var u in updrafts)
                for (float y = u.bottom + 2.5f; y < u.top - 0.5f; y += 1.6f) items.Add((C, u.x, Mathf.Max(y, 1f)));
            for (int i = 0; i + 1 < R.Count; i++)
            {
                var u = updrafts.FirstOrDefault(d => Mathf.Abs(d.x - (R[i].x1 - 2.5f)) < 0.01f);
                if (u.width <= 0f) continue;
                var from = new Vector2(u.x + 2f, u.top - 0.5f);
                var to = new Vector2(R[i + 1].x0 + 1.5f, R[i + 1].top + 1.5f);
                for (int k = 1; k <= 4; k++) { var p = Vector2.Lerp(from, to, k / 5f); items.Add((C, p.x, p.y)); }
            }
            var gapUp = updrafts.Where(d => d.bottom < 0f).ToList();
            if (gapUp.Count > 0) items.Add((Collectible.Kind.Diamond, gapUp[gapUp.Count - 1].x, gapUp[gapUp.Count - 1].top - 0.6f));
            foreach (var it in items) CourseKit.Item(Zone(course, it.x), it.kind, it.x, it.y);

            var blocks = new (BumpBlock.Kind kind, int roof, float dx)[]
            {
                (BumpBlock.Kind.CoinBox, 0, 8f), (BumpBlock.Kind.CoinBox, 5, 6f), (BumpBlock.Kind.CoinBox, 8, 5f), (BumpBlock.Kind.CoinSack, 12, 8f),
            };
            foreach (var b in blocks) CourseKit.Block(Zone(course, R[b.roof].x0 + b.dx), b.kind, R[b.roof].x0 + b.dx, R[b.roof].top + CourseKit.BlockY);

            var E = new List<(Enemy.Kind kind, float x, float y)>();
            foreach (int i in new[] { 1, 3, 5, 7, 8, 10, 11 }) E.Add((Enemy.Kind.Agama, (R[i].x0 + R[i].x1) / 2f - 2f, R[i].top));
            foreach (int i in new[] { 2, 6, 9 }) E.Add((Enemy.Kind.Crab, (R[i].x0 + R[i].x1) / 2f - 2f, R[i].top));
            foreach (int i in new[] { 2, 3, 5, 7, 8, 10, 11, 12 })
            {
                float gx = (R[i - 1].x1 + R[i].x0) / 2f + (RoofPath[i].gapUpdraft ? 5f : 0f);
                var kind = i == 5 || i == 8 || i == 10 || i == 12 ? Enemy.Kind.Vulture : Enemy.Kind.Gull;
                E.Add((kind, gx, Mathf.Max(R[i - 1].top, R[i].top) + 4f));
            }
            // Des vautours qui guettent en haut des courants au-dessus du vide.
            foreach (var u in gapUp) E.Add((Enemy.Kind.Vulture, u.x - 4f, u.top + 1f));
            foreach (var e in E) CourseKit.EnemyAt(Zone(course, e.x), e.kind, e.x, e.y);

            int mid = 6;
            CourseKit.Flag(Zone(course, R[mid].x0 + 3f), R[mid].x0 + 3f, 3.5f, false, R[mid].top);
            var last = R[R.Count - 1];
            CourseKit.Flag(Zone(course, last.x1 - 4f), last.x1 - 4f, 5.5f, true, last.top);

            var catalog = MenuBuilder.Catalog();
            var stage = catalog.stages[3];
            stage.scene = System.IO.Path.GetFileNameWithoutExtension(ScenePath);
            stage.tagline = Tagline;
            stage.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SuperYao/UI/Backgrounds/thumb_fhb.jpg");
            stage.coins = items.Count(i => i.kind == C) + blocks.Count(k => k.kind == BumpBlock.Kind.CoinBox)
                          + blocks.Count(k => k.kind == BumpBlock.Kind.CoinSack) * 8;
            stage.bananas = 0;
            stage.diamonds = items.Count(i => i.kind == Collectible.Kind.Diamond);
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Poses de Yao à l'hélice (planche 8 × 4 de l'utilisateur, Heroes/YaoHelico) : décollage, vol sur place,
        /// vol vers l'avant, atterrissage. Au sol, il marche et court avec les planches de Yao à pied (demandé).
        /// Yao mesure 178 px debout sur la planche de l'hélice, sans l'hélice.</summary>
        internal static HeroAnimationSet HelicoSet()
        {
            const string dir = "Assets/SuperYao/Heroes/YaoHelico";
            if (!AssetDatabase.IsValidFolder(dir)) { Debug.LogWarning("Super Yao : poses de l'hélice introuvables (Heroes/YaoHelico)."); return null; }
            string path = dir + "/YaoHelico_Animations.asset";
            var set = AssetDatabase.LoadAssetAtPath<HeroAnimationSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<HeroAnimationSet>();
                AssetDatabase.CreateAsset(set, path);
            }
            var walker = AssetDatabase.LoadAssetAtPath<HeroAnimationSet>("Assets/SuperYao/Heroes/Yao/Yao_Animations.asset");
            set.heroName = "Yao (hélice)";
            set.sourceHeight = 178f;
            set.idleFps = 2f;
            set.idle = Frames(dir + "/Idle");
            set.jump = Frames(dir + "/Jump");
            set.fall = Frames(dir + "/Fall");
            set.land = Frames(dir + "/Land");
            set.crouch = Frames(dir + "/Crouch");
            set.hover = Frames(dir + "/Hover");
            set.glide = Frames(dir + "/Glide");
            if (walker != null)
            {
                // Au sol : la marche et la course de Yao à pied, à leur propre échelle.
                set.walk = walker.walk;
                set.run = walker.run;
                set.walkSourceHeight = walker.walkSourceHeight;
                set.runSourceHeight = walker.runSourceHeight;
                set.walkCycle = walker.walkCycle;
                set.runCycle = walker.runCycle;
                set.attack = walker.attack;
                set.hurt = walker.hurt;
                set.death = walker.death;
            }
            set.jumpTakeoff = 0; set.jumpRise = 1; set.jumpApex = 2; set.jumpFall = 3;
            set.crouchFrame = 0;
            set.crouchSlideFrame = -1;
            EditorUtility.SetDirty(set);
            return set;
        }

        static void BuildCamera(Transform yao)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = VisibleRows / 2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.22f);
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(12f, 8f, -10f);
            var follow = go.AddComponent<CameraFollow2D>();
            follow.target = yao;
            follow.visibleRows = VisibleRows;
            follow.levelMinX = 0f;
            follow.levelMaxX = levelLength;
            follow.followAltitude = true;
        }

        static void BuildSettings()
        {
            var settings = new GameObject("Reglages_Stage");
            settings.AddComponent<StageSettings>();
            settings.AddComponent<TouchControls>().fireIcon = CourseKit.LoadSprite($"{GameplayDir}Items/fireball.png");
            settings.AddComponent<StageState>().LevelNumber = 4;
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

        /// <summary>Groupes du parcours par tranche de 100 cases (pour s'y retrouver dans la hiérarchie).</summary>
        static Transform Zone(Transform parent, float x)
        {
            string name = $"Cases_{Mathf.FloorToInt(Mathf.Max(0f, x) / 100f) * 100:000}";
            var t = parent.Find(name);
            if (t != null) return t;
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
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
            box.offset = new Vector2(x, 15f);
            box.size = new Vector2(1f, 50f);
            box.sharedMaterial = frictionless;
        }

        static (float min, float max) LocalRange(float factor)
        {
            float camMin = HalfViewMin, camMax = levelLength - HalfViewMin;
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
            for (int i = 1; i <= 3; i++) SpritePrefab($"Nuage_0{i}", Art1 + $"Clouds/cloud_0{i}.png", "Nuages");
            SpritePrefab("CielNuit", Art3 + "Sky/sky_night.jpg", "Ciel");
            SpritePrefab("Lune", Art3 + "Sky/moon.png", "Ciel");
            SpritePrefab("HorizonNuit", Art3 + "Horizon/horizon_plateau_nuit.png", "Horizon", SpriteDrawMode.Tiled);
            for (int i = 1; i <= 3; i++) SpritePrefab($"Monument_0{i}", Art3 + $"Buildings/landmark_0{i}.png", "BatimentsLointains");
            for (int i = 1; i <= 6; i++) SpritePrefab($"Toit_{i:00}", Art3 + $"Roofs/roof_0{i}.png", "Monde");
            for (int i = 1; i <= 4; i++) SpritePrefab($"Toit_{i + 6:00}", Art4 + $"Buildings/night_0{i}.png", "Monde");
            SpritePrefab("TerrasseGauche", Art4 + "Terrace/terrace_left.png", "Monde");
            SpritePrefab("TerrasseMilieu", Art4 + "Terrace/terrace_middle.png", "Monde");
            SpritePrefab("TerrasseDroite", Art4 + "Terrace/terrace_right.png", "Monde");
            for (int i = 1; i <= 4; i++) SpritePrefab($"Lampadaire_0{i}", Art4 + $"Props/lamp_0{i}.png", "Monde");
            SpritePrefab("Halo", Art4 + "Props/glow.png", "Monde");
            SpritePrefab("Stade", Art4 + "Stadium/stade_fhb.png", "BatimentsLointains");
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
