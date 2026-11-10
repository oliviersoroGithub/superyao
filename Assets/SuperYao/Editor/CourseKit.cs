using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Éléments de parcours communs aux stages (à partir du stage 2), mêmes règles que le stage 1 : blocs à frapper,
    /// pièces, bananes et diamants, piles de latérite, planches traversables par en dessous, drapeaux (point de contrôle
    /// et arrivée), ennemis. Appeler <see cref="Init"/> avant usage. Unités : une case ; sol (collision) en y = 0.
    /// </summary>
    public static class CourseKit
    {
        public const string GameplayDir = "Assets/SuperYao/Gameplay/";
        /// <summary>Bas des blocs en l'air (Yao petit doit sauter un peu pour les frapper).</summary>
        public const float BlockY = 2.6f;
        /// <summary>Dessus de collision des blocs, 0,12 case sous le haut du dessin (vus un peu de dessus).</summary>
        public const float BlockTopInset = 0.12f;
        const float PlankDeck = 0.91f, PlankHeight = 0.45f;
        /// <summary>Échelles des planches du téléphone (cases par pixel de planche) : taxi (34 px pour 175), racketteur (46 px pour 200).</summary>
        public const float TaxiUnit = 34f / 16f / 175f, TollUnit = 46f / 16f / 200f;

        static Material material;
        static PhysicsMaterial2D frictionless;

        public static void Init(Material spriteMaterial, PhysicsMaterial2D noFriction)
        {
            material = spriteMaterial;
            frictionless = noFriction;
        }

        public static Sprite LoadSprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogError("Super Yao : image manquante " + path);
            return s;
        }

        public static Sprite[] LoadSprites(string pathPrefix, int count) =>
            Enumerable.Range(0, count).Select(i => LoadSprite($"{pathPrefix}{i}.png")).ToArray();

        public static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, int order, float size, bool byHeight = true)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = sprite;
            sr.sharedMaterial = material;
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
        public static void Block(Transform parent, BumpBlock.Kind kind, float x, float y = BlockY)
        {
            string art = kind == BumpBlock.Kind.Brick ? "crate" : kind == BumpBlock.Kind.CoinSack ? "sack" : "chest";
            var go = new GameObject($"Bloc_{kind}_x{x:0.#}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 1f - BlockTopInset);
            box.offset = new Vector2(0f, (1f - BlockTopInset) / 2f);
            box.sharedMaterial = frictionless;

            var block = go.AddComponent<BumpBlock>();
            block.kind = kind;
            var frames = LoadSprites($"{GameplayDir}Blocks/{art}_", 3);
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
        public static void Item(Transform parent, Collectible.Kind kind, float x, float y)
        {
            Sprite[] frames = kind == Collectible.Kind.Coin ? LoadSprites($"{GameplayDir}Items/coin_", 4)
                : kind == Collectible.Kind.Diamond ? LoadSprites($"{GameplayDir}Items/diamond_", 3)
                : new[] { LoadSprite($"{GameplayDir}Items/banana.png") };
            float size = kind == Collectible.Kind.Diamond ? 1.1f : 0.75f;
            var sr = Renderer(parent, $"{kind}_x{x:0.#}", frames[0], 4, size, kind != Collectible.Kind.Banana);
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

        /// <summary>Pile de blocs de latérite d'une case de large : une seule boîte de collision.</summary>
        public static void Stack(Transform parent, float x, int height, int variant)
        {
            var go = new GameObject($"Laterite_x{x:0.#}_h{height}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, 0f, 0f);
            for (int i = 0; i < height; i++)
            {
                var sprite = LoadSprite($"{GameplayDir}Blocks/laterite_{(variant + i) % 3}.png");
                var sr = Renderer(go.transform, $"Bloc_{i}", sprite, 3, 1f);
                sr.transform.localScale = new Vector3(1f / sprite.bounds.size.x, 1f / sprite.bounds.size.y, 1f);
                sr.transform.localPosition = new Vector3(0f, i, 0f);
            }
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, height - BlockTopInset);
            box.offset = new Vector2(0f, (height - BlockTopInset) / 2f);
            box.sharedMaterial = frictionless;
        }

        /// <summary>Planches bout à bout (bord gauche en x) dont le dessus est à <paramref name="top"/>.</summary>
        public static void Plank(Transform parent, float x, float top, int count)
        {
            var go = new GameObject($"Planche_x{x:0.#}_y{top:0.#}");
            go.transform.SetParent(parent, false);
            var sprite = LoadSprite($"{GameplayDir}Platforms/plank.png");
            float w = 0f;
            for (int i = 0; i < count; i++)
            {
                var sr = Renderer(go.transform, $"Planche_{i}", sprite, 3, PlankHeight);
                float segment = sprite.bounds.size.x * sr.transform.localScale.x;
                sr.transform.localPosition = new Vector3(x + segment * (i + 0.5f), top - PlankDeck * PlankHeight, 0f);
                w = segment * count;
            }
            OneWay(go, x + w / 2f, top, w - 0.1f);
        }

        public static void OneWay(GameObject go, float x, float top, float width)
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

        /// <summary>Cocotier au drapeau ivoirien : point de contrôle, ou arrivée.</summary>
        public static void Flag(Transform parent, float x, float height, bool goal, float y = 0f)
        {
            var sr = Renderer(parent, goal ? "Arrivee" : $"Point_De_Controle_x{x:0.#}", LoadSprite($"{GameplayDir}Flags/flagPalm.png"), 1, height);
            sr.transform.localPosition = new Vector3(x, y, 0f);
            var cp = sr.gameObject.AddComponent<Checkpoint>();
            cp.isGoal = goal;
            cp.respawnX = x;
        }

        /// <summary>Ennemi posé en (x, y) : crabe, tortue, mouette ou vautour (en vol), taxi, agame, gbaka ou charrette.</summary>
        public static void EnemyAt(Transform parent, SuperYao.Enemy.Kind kind, float x, float y)
        {
            SpriteRenderer sr;
            BoxCollider2D box;
            var enemyName = $"Ennemi_{kind}_x{x:0.#}";
            if (kind == SuperYao.Enemy.Kind.Taxi)
            {
                var drive = LoadSprites($"{GameplayDir}Enemies/taxi_drive_", 8);
                sr = Renderer(parent, enemyName, drive[0], 4, drive[0].rect.height * TaxiUnit);
                float s = sr.transform.localScale.x;
                box = sr.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1.9f / s, 1.0f / s);
                box.offset = new Vector2(0f, 0.5f / s);
                sr.gameObject.AddComponent<Rigidbody2D>();
                var taxi = sr.gameObject.AddComponent<SuperYao.Enemy>();
                taxi.kind = kind;
                taxi.walkFrames = drive;
                taxi.turnFrames = LoadSprites($"{GameplayDir}Enemies/taxi_turn_", 6);
                taxi.boomFrames = LoadSprites($"{GameplayDir}Enemies/taxi_boom_", 5);
                taxi.facesRight = true;
                taxi.speed = 1.35f * 3.75f;
                sr.transform.localPosition = new Vector3(x, y, 0f);
                return;
            }
            if (kind == SuperYao.Enemy.Kind.Gbaka)
            {
                // Gbaka (images du téléphone : il roule, fonce dans la poussière, reste sonné avec des étoiles) : 2,2 cases
                // de haut, 3 coups pour l'arrêter ; vaincu, il est éjecté en tournoyant (comme sur le téléphone).
                var drive = LoadSprite($"{GameplayDir}Enemies/gbaka_drive_0.png");
                sr = Renderer(parent, enemyName, drive, 4, 2.2f);
                sr.transform.localPosition = new Vector3(x, y, 0f);
                float sg = sr.transform.localScale.x;
                box = sr.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(3.3f / sg, 1.7f / sg);
                box.offset = new Vector2(0f, 0.85f / sg);
                sr.gameObject.AddComponent<Rigidbody2D>();
                var gbaka = sr.gameObject.AddComponent<SuperYao.Enemy>();
                gbaka.kind = kind;
                gbaka.walkFrames = new[] { drive };
                gbaka.chargeFrames = new[] { LoadSprite($"{GameplayDir}Enemies/gbaka_charge_0.png") };
                gbaka.hitFrames = new[] { LoadSprite($"{GameplayDir}Enemies/gbaka_dizzy_0.png") };
                gbaka.facesRight = true;
                gbaka.hp = 3;
                gbaka.speed = 0.55f * 3.75f;
                gbaka.chargeSpeed = 8.5f;
                gbaka.sight = 10f;
                return;
            }
            if (kind == SuperYao.Enemy.Kind.Cart)
            {
                // Charrette (images du téléphone) : le pousseur et sa charrette de bois, 1,6 case de haut, 2 coups.
                var walk = LoadSprites($"{GameplayDir}Enemies/cart_walk_", 4);
                sr = Renderer(parent, enemyName, walk[0], 4, 1.6f);
                sr.transform.localPosition = new Vector3(x, y, 0f);
                float sk = sr.transform.localScale.x;
                box = sr.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(2f / sk, 1.3f / sk);
                box.offset = new Vector2(0f, 0.65f / sk);
                sr.gameObject.AddComponent<Rigidbody2D>();
                var cart = sr.gameObject.AddComponent<SuperYao.Enemy>();
                cart.kind = kind;
                cart.walkFrames = walk;
                cart.chargeFrames = LoadSprites($"{GameplayDir}Enemies/cart_attack_", 3);
                cart.hitFrames = LoadSprites($"{GameplayDir}Enemies/cart_hit_", 3);
                cart.facesRight = true;
                cart.hp = 2;
                cart.speed = 0.42f * 3.75f;
                cart.chargeSpeed = 6.5f;
                cart.sight = 8f;
                return;
            }
            if (kind == SuperYao.Enemy.Kind.Vulture)
            {
                // Vautour (planche de l'utilisateur, 6 poses de 362 × 491 px, ailes comprises) : 1,8 case de haut.
                var fly = LoadSprites($"{GameplayDir}Enemies/vulture_", 6);
                sr = Renderer(parent, enemyName, fly[0], 4, 1.8f);
                sr.transform.localPosition = new Vector3(x, y, 0f);
                float sv = sr.transform.localScale.x;
                box = sr.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1.3f / sv, 0.8f / sv);
                box.offset = new Vector2(0.15f / sv, 0.75f / sv); // le corps, sans les ailes
                sr.gameObject.AddComponent<Rigidbody2D>();
                var vulture = sr.gameObject.AddComponent<SuperYao.Enemy>();
                vulture.kind = kind;
                vulture.walkFrames = fly;
                vulture.facesRight = true;
                vulture.speed = 0.7f * 3.75f;
                vulture.flyRange = 4f;
                vulture.flyBob = 0.8f;
                return;
            }
            if (kind == SuperYao.Enemy.Kind.Monkey)
            {
                // Singe (images du téléphone : 6 poses de marche, 6 de lancer, 4 de coup reçu) : 1,6 case de haut, il
                // lance des noix de coco (4 images : entière, en vol, fendue, éclatée).
                var walk = LoadSprites($"{GameplayDir}Enemies/monkey_walk_", 6);
                sr = Renderer(parent, enemyName, walk[0], 4, 1.6f);
                sr.transform.localPosition = new Vector3(x, y, 0f);
                float sm = sr.transform.localScale.x;
                box = sr.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(0.9f / sm, 1.1f / sm);
                box.offset = new Vector2(0f, 0.55f / sm);
                sr.gameObject.AddComponent<Rigidbody2D>();
                var monkey = sr.gameObject.AddComponent<SuperYao.Enemy>();
                monkey.kind = kind;
                monkey.walkFrames = walk;
                monkey.throwFrames = LoadSprites($"{GameplayDir}Enemies/monkey_throw_", 6);
                monkey.coconutFrames = LoadSprites($"{GameplayDir}Enemies/coconut_", 4);
                monkey.squashedSprite = LoadSprite($"{GameplayDir}Enemies/monkey_hit_3.png");
                monkey.facesRight = true;
                monkey.speed = 0.55f * 3.75f;
                return;
            }
            if (kind == SuperYao.Enemy.Kind.Agama)
            {
                // Agame (planche de l'utilisateur, 6 poses de 362 × 102 px) : un long lézard, 0,75 case de haut.
                var walk = LoadSprites($"{GameplayDir}Enemies/agama_", 6);
                sr = Renderer(parent, enemyName, walk[0], 4, 0.75f);
                sr.transform.localPosition = new Vector3(x, y, 0f);
                float sa = sr.transform.localScale.x;
                box = sr.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1.6f / sa, 0.6f / sa);
                box.offset = new Vector2(0.2f / sa, 0.3f / sa); // le corps, sans la longue queue
                sr.gameObject.AddComponent<Rigidbody2D>();
                var agama = sr.gameObject.AddComponent<SuperYao.Enemy>();
                agama.kind = kind;
                agama.walkFrames = walk;
                agama.facesRight = true;
                agama.speed = 0.32f * 3.75f;
                return;
            }
            string art = kind == SuperYao.Enemy.Kind.Crab ? "crab" : kind == SuperYao.Enemy.Kind.Gull ? "gull" : "turtle";
            int count = kind == SuperYao.Enemy.Kind.Turtle ? 4 : 3;
            var frames = LoadSprites($"{GameplayDir}Enemies/{art}_", count);
            float height = kind == SuperYao.Enemy.Kind.Gull ? 1.1f : 0.95f;
            sr = Renderer(parent, enemyName, frames[0], 4, height);
            sr.transform.localPosition = new Vector3(x, y, 0f);
            float sc = sr.transform.localScale.x;
            box = sr.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f / sc, 0.75f / sc);
            box.offset = new Vector2(0f, 0.375f / sc);
            sr.gameObject.AddComponent<Rigidbody2D>();
            var enemy = sr.gameObject.AddComponent<SuperYao.Enemy>();
            enemy.kind = kind;
            enemy.walkFrames = kind == SuperYao.Enemy.Kind.Turtle ? new[] { frames[0], frames[1] } : frames;
            enemy.squashedSprite = kind == SuperYao.Enemy.Kind.Turtle ? frames[3] : null;
            enemy.facesRight = kind != SuperYao.Enemy.Kind.Turtle;
            enemy.speed = kind == SuperYao.Enemy.Kind.Crab ? 0.38f * 3.75f : kind == SuperYao.Enemy.Kind.Turtle ? 0.3f * 3.75f : 0.75f * 3.75f;
        }
    }
}
