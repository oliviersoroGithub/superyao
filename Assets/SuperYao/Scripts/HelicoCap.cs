using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Chapeau à hélice à ramasser (stage 5, avant le grand trou) : il flotte en tournant doucement ; attrapé, Yao le
    /// garde jusqu'à ce qu'il se pose de l'autre côté du grand trou (dropAfterX) : les courants d'air le portent et il
    /// plane en gardant le bouton de saut (HeroController.canFly), avec les poses de l'hélice (HeroAnimator.flySet).
    /// S'il repart d'avant le trou après une vie perdue, le chapeau l'attend de nouveau.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class HelicoCap : MonoBehaviour
    {
        [Tooltip("Au-delà de cette abscisse (cases), une fois posé au sol, Yao rend le chapeau (0 = il le garde).")]
        public float dropAfterX;

        SpriteRenderer sr;
        Vector3 start;
        HeroController hero;
        float clock;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            start = transform.localPosition;
        }

        void Update()
        {
            clock += Time.deltaTime;
            transform.localPosition = start + Vector3.up * (Mathf.Sin(clock * 3f) * 0.15f);
            if (hero == null) hero = FindAnyObjectByType<HeroController>();
            if (hero == null || hero.IsDying) return;
            if (hero.canFly)
            {
                sr.enabled = false; // déjà coiffé
                // Posé de l'autre côté du trou : fini l'hélice, Yao repart à pied (ou en skate).
                if (dropAfterX > 0f && hero.Grounded && hero.transform.position.x > dropAfterX) hero.canFly = false;
                return;
            }
            if (dropAfterX > 0f && hero.transform.position.x > dropAfterX) { sr.enabled = false; return; }
            sr.enabled = true;
            if (Vector2.Distance(hero.transform.position + Vector3.up * 0.8f, transform.position + Vector3.up * 0.3f) < 1.1f)
            {
                hero.canFly = true;
                StageState.Instance?.AddPower();
                sr.enabled = false;
            }
        }
    }
}
