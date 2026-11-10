using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SuperYao
{
    /// <summary>
    /// Commandes du héros, toutes sources confondues : boutons tactiles (téléphone), clavier et manette (PC).
    /// Clavier (touches repérées par leur place, donc valables en AZERTY comme en QWERTY) :
    ///   flèches ou Q/D (A/D en QWERTY) = marcher ; ↓ ou S = s'accroupir ;
    ///   Espace, ↑ ou Z (W en QWERTY) = sauter ; Maj ou J = courir.
    /// Manette : stick ou croix = marcher et s'accroupir ; A (croix sur PlayStation) = sauter ; X (carré) ou gâchette
    /// droite = courir.
    /// Lu à chaque image ; l'appui sur « sauter » est gardé jusqu'à ce que le pas de physique le consomme.
    /// </summary>
    public class HeroInput : MonoBehaviour
    {
        public float Horizontal { get; private set; }
        public bool Down { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool RunHeld { get; private set; }

        bool jumpPressed, previousJump, firePressed, previousFire;

        void Update()
        {
            float x = 0f;
            bool down = false, jump = false, run = false, fire = false;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.leftArrowKey.isPressed || k.aKey.isPressed) x -= 1f;
                if (k.rightArrowKey.isPressed || k.dKey.isPressed) x += 1f;
                down |= k.downArrowKey.isPressed || k.sKey.isPressed;
                jump |= k.spaceKey.isPressed || k.upArrowKey.isPressed || k.wKey.isPressed;
                run |= k.leftShiftKey.isPressed || k.rightShiftKey.isPressed || k.jKey.isPressed;
                fire |= k.xKey.isPressed || k.kKey.isPressed || k.leftCtrlKey.isPressed;
            }
            var g = Gamepad.current;
            if (g != null)
            {
                var stick = g.leftStick.ReadValue();
                var pad = g.dpad.ReadValue();
                x += Mathf.Abs(stick.x) > 0.3f ? stick.x : pad.x;
                down |= stick.y < -0.6f || pad.y < -0.5f;
                jump |= g.buttonSouth.isPressed;
                run |= g.buttonWest.isPressed || g.rightTrigger.isPressed;
                fire |= g.buttonEast.isPressed || g.rightShoulder.isPressed;
            }
#else
            x = Input.GetAxisRaw("Horizontal");
            down = Input.GetAxisRaw("Vertical") < -0.5f;
            jump = Input.GetButton("Jump");
            run = Input.GetKey(KeyCode.LeftShift);
#endif
            // Les boutons tactiles s'ajoutent au clavier et à la manette.
            x += TouchControls.Horizontal;
            Horizontal = Mathf.Clamp(x, -1f, 1f);
            Down = down || TouchControls.Down;
            RunHeld = run || TouchControls.Run;
            jump |= TouchControls.Jump;
            fire |= TouchControls.Fire;
            if (fire && !previousFire) firePressed = true;
            previousFire = fire;
            if (jump && !previousJump) jumpPressed = true;
            previousJump = jump;
            JumpHeld = jump;
        }

        /// <summary>Vrai une seule fois par appui sur « sauter ».</summary>
        public bool ConsumeJumpPressed()
        {
            bool pressed = jumpPressed;
            jumpPressed = false;
            return pressed;
        }

        /// <summary>Vrai une seule fois par appui sur « feu ».</summary>
        public bool ConsumeFirePressed()
        {
            bool pressed = firePressed;
            firePressed = false;
            return pressed;
        }

        void OnDisable() => jumpPressed = previousJump = firePressed = previousFire = false;
    }
}
