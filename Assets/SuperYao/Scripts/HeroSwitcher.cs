using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SuperYao
{
    /// <summary>
    /// Pour les essais : les touches 1, 2 et 3 du clavier changent de héros (Yao, Lou, Moussa) en plein jeu.
    /// Les déplacements restent les mêmes, comme dans la version téléphone ; seules les poses changent.
    /// </summary>
    public class HeroSwitcher : MonoBehaviour
    {
        public HeroAnimationSet[] heroes;
        HeroAnimator animator;

        void Awake() => animator = GetComponentInChildren<HeroAnimator>();

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null || animator == null || heroes == null) return;
            for (int i = 0; i < heroes.Length && i < 9; i++)
                if (k[Key.Digit1 + i].wasPressedThisFrame && heroes[i] != null) animator.SetHero(heroes[i]);
#endif
        }
    }
}
