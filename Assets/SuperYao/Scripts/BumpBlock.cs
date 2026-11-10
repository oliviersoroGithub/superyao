using System.Collections;
using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Bloc qu'on frappe de la tête, par en dessous (règles de la version téléphone, items.ts) :
    /// - coffre « ? » à pièce : une pièce jaillit, le coffre devient vide ;
    /// - coffre « ? » à pouvoir : le skate en sort (Yao monte dessus ; s'il l'a déjà, l'orbe de feu) ;
    /// - sac de cacao : une pièce à chaque coup, jusqu'à épuisement ;
    /// - caisse (brique) : Yao petit la fait sauter ; Yao grand la casse.
    /// Le bloc saute un peu à chaque coup. Tout est préparé d'avance (pièce, noix, éclats) : rien n'est créé en jeu.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class BumpBlock : MonoBehaviour
    {
        public enum Kind { CoinBox, PowerBox, CoinSack, Brick }

        public Kind kind;
        [Tooltip("Pièces dans un sac de cacao.")]
        public int coins = 8;
        public SpriteRenderer visual;
        public Sprite usedSprite;
        [Tooltip("Pièce qui jaillit (enfant désactivé).")]
        public SpriteRenderer coinPop;
        public Sprite[] coinFrames;
        [Tooltip("Le skate, pour un coffre à pouvoir (enfant désactivé).")]
        public PowerItem power;
        [Tooltip("Éclats de la caisse cassée (enfants désactivés).")]
        public SpriteRenderer[] debris;

        bool used;
        Coroutine bumping, popping;

        void OnCollisionEnter2D(Collision2D collision)
        {
            var hero = collision.collider.GetComponentInParent<HeroController>();
            if (hero == null || used) return;
            // Coup de tête : le héros est sous le bloc et son centre est sous ce bloc-ci (pas le voisin).
            var mine = GetComponent<BoxCollider2D>().bounds;
            var his = collision.collider.bounds;
            if (his.max.y > mine.min.y + 0.2f || Mathf.Abs(his.center.x - mine.center.x) > mine.extents.x + 0.05f) return;
            Hit(hero);
        }

        void Hit(HeroController hero)
        {
            var state = StageState.Instance;
            switch (kind)
            {
                case Kind.CoinBox:
                    state?.AddCoin();
                    PopCoin();
                    Use();
                    break;
                case Kind.PowerBox:
                    if (power != null) power.Emerge(hero.IsBig); // déjà grand : l'orbe de feu
                    Use();
                    break;
                case Kind.CoinSack:
                    state?.AddCoin();
                    PopCoin();
                    if (--coins <= 0) Use();
                    break;
                case Kind.Brick:
                    if (hero.IsBig) { Break(); state?.AddBrick(); return; }
                    break;
            }
            SoundFx.Play(kind == Kind.PowerBox ? "power" : "bump");
            if (bumping != null) StopCoroutine(bumping);
            bumping = StartCoroutine(Bump());
        }

        void Use()
        {
            used = true;
            if (usedSprite != null) visual.sprite = usedSprite;
        }

        IEnumerator Bump()
        {
            for (float t = 0f; t < 0.16f; t += Time.deltaTime)
            {
                visual.transform.localPosition = new Vector3(0f, Mathf.Sin(t / 0.16f * Mathf.PI) * 0.3f, 0f);
                yield return null;
            }
            visual.transform.localPosition = Vector3.zero;
        }

        void PopCoin()
        {
            if (coinPop == null) return;
            if (popping != null) StopCoroutine(popping);
            popping = StartCoroutine(Pop());
        }

        IEnumerator Pop()
        {
            coinPop.enabled = true;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float k = t / 0.5f;
                coinPop.transform.localPosition = new Vector3(0f, 1f + Mathf.Sin(k * Mathf.PI) * 2f, 0f);
                if (coinFrames != null && coinFrames.Length > 0) coinPop.sprite = coinFrames[Mathf.FloorToInt(t * 16f) % coinFrames.Length];
                coinPop.color = new Color(1f, 1f, 1f, k < 0.7f ? 1f : (1f - k) / 0.3f);
                yield return null;
            }
            coinPop.enabled = false;
        }

        void Break()
        {
            used = true;
            visual.enabled = false;
            GetComponent<Collider2D>().enabled = false;
            StartCoroutine(Debris());
        }

        IEnumerator Debris()
        {
            var speeds = new Vector2[debris.Length];
            for (int i = 0; i < debris.Length; i++)
            {
                debris[i].enabled = true;
                debris[i].transform.localPosition = new Vector3(i % 2 == 0 ? -0.25f : 0.25f, i < 2 ? 0.75f : 0.25f, 0f);
                speeds[i] = new Vector2((i % 2 == 0 ? -1f : 1f) * Random.Range(3f, 5f), i < 2 ? 16f : 11f);
            }
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                for (int i = 0; i < debris.Length; i++)
                {
                    speeds[i].y -= 60f * Time.deltaTime;
                    debris[i].transform.localPosition += (Vector3)(speeds[i] * Time.deltaTime);
                    debris[i].transform.Rotate(0f, 0f, speeds[i].x * 90f * Time.deltaTime);
                }
                yield return null;
            }
            foreach (var d in debris) d.enabled = false;
        }
    }
}
