using UnityEngine;
#if HAS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Corrida
{
    public struct DriverInput
    {
        public float throttle;  // 0..1
        public float brake;     // 0..1
        public float steer;     // -1 (esquerda) .. 1 (direita)
        public bool handbrake;
        public bool drs;        // pede para abrir a asa (só abre na zona, se habilitado)
    }

    public enum K { W, A, S, D, Space, R, C, Up, Down, Left, Right, RightShift, Enter, RightCtrl, Escape, E, Keypad0, Slash }

    /// <summary>
    /// Teclado (funciona com o Input Manager antigo ou com o Input System novo) + gamepads (Input System).
    /// Jogador 1: WASD / Espaço / R / C. Jogador 2: Setas / Shift direito / Enter / Ctrl direito. DRS: E (J1), 0 do teclado numérico ou ; ao lado do Shift (J2), X no gamepad.
    /// </summary>
    public static class GameInput
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        static KeyCode Code(K k)
        {
            switch (k)
            {
                case K.W: return KeyCode.W;
                case K.A: return KeyCode.A;
                case K.S: return KeyCode.S;
                case K.D: return KeyCode.D;
                case K.Space: return KeyCode.Space;
                case K.R: return KeyCode.R;
                case K.C: return KeyCode.C;
                case K.Up: return KeyCode.UpArrow;
                case K.Down: return KeyCode.DownArrow;
                case K.Left: return KeyCode.LeftArrow;
                case K.Right: return KeyCode.RightArrow;
                case K.RightShift: return KeyCode.RightShift;
                case K.Enter: return KeyCode.Return;
                case K.RightCtrl: return KeyCode.RightControl;
                case K.Escape: return KeyCode.Escape;
                case K.E: return KeyCode.E;
                case K.Keypad0: return KeyCode.Keypad0;
                case K.Slash: return KeyCode.Slash;
            }
            return KeyCode.None;
        }
#elif HAS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        static Key Code(K k)
        {
            switch (k)
            {
                case K.W: return Key.W;
                case K.A: return Key.A;
                case K.S: return Key.S;
                case K.D: return Key.D;
                case K.Space: return Key.Space;
                case K.R: return Key.R;
                case K.C: return Key.C;
                case K.Up: return Key.UpArrow;
                case K.Down: return Key.DownArrow;
                case K.Left: return Key.LeftArrow;
                case K.Right: return Key.RightArrow;
                case K.RightShift: return Key.RightShift;
                case K.Enter: return Key.Enter;
                case K.RightCtrl: return Key.RightCtrl;
                case K.Escape: return Key.Escape;
                case K.E: return Key.E;
                case K.Keypad0: return Key.Numpad0;
                case K.Slash: return Key.Slash;
            }
            return Key.None;
        }
#endif

        public static bool Held(K k)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(Code(k))) return true;
            return k == K.Enter && Input.GetKey(KeyCode.KeypadEnter);
#elif HAS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (kb[Code(k)].isPressed) return true;
            return k == K.Enter && kb.numpadEnterKey.isPressed;
#else
            return false;
#endif
        }

        public static bool Pressed(K k)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(Code(k))) return true;
            return k == K.Enter && Input.GetKeyDown(KeyCode.KeypadEnter);
#elif HAS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (kb[Code(k)].wasPressedThisFrame) return true;
            return k == K.Enter && kb.numpadEnterKey.wasPressedThisFrame;
#else
            return false;
#endif
        }

#if HAS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        static Gamepad Pad(int i)
        {
            var all = Gamepad.all;
            return i >= 0 && i < all.Count ? all[i] : null;
        }
#endif

        public static DriverInput ReadPlayer(int p)
        {
            var d = new DriverInput();
            if (p == 0)
            {
                d.throttle = Held(K.W) ? 1f : 0f;
                d.brake = Held(K.S) ? 1f : 0f;
                d.steer = (Held(K.D) ? 1f : 0f) - (Held(K.A) ? 1f : 0f);
                d.handbrake = Held(K.Space);
                d.drs = Held(K.E);
            }
            else
            {
                d.throttle = Held(K.Up) ? 1f : 0f;
                d.brake = Held(K.Down) ? 1f : 0f;
                d.steer = (Held(K.Right) ? 1f : 0f) - (Held(K.Left) ? 1f : 0f);
                d.handbrake = Held(K.RightShift);
                d.drs = Held(K.Keypad0) || Held(K.Slash);
            }
#if HAS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var pad = Pad(p);
            if (pad != null)
            {
                d.throttle = Mathf.Max(d.throttle, pad.rightTrigger.ReadValue());
                d.brake = Mathf.Max(d.brake, pad.leftTrigger.ReadValue());
                float s = pad.leftStick.ReadValue().x;
                if (Mathf.Abs(s) > 0.12f && Mathf.Abs(s) > Mathf.Abs(d.steer)) d.steer = s;
                d.handbrake |= pad.buttonSouth.isPressed;
                d.drs |= pad.buttonWest.isPressed;
            }
#endif
            return d;
        }

        public static bool ResetPressed(int p)
        {
            bool r = p == 0 ? Pressed(K.R) : Pressed(K.Enter);
#if HAS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var pad = Pad(p);
            if (pad != null && pad.buttonNorth.wasPressedThisFrame) r = true;
#endif
            return r;
        }

        public static bool CameraPressed(int p)
        {
            bool r = p == 0 ? Pressed(K.C) : Pressed(K.RightCtrl);
#if HAS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var pad = Pad(p);
            if (pad != null && pad.buttonEast.wasPressedThisFrame) r = true;
#endif
            return r;
        }

        /// <summary>Navegação nos menus: x = esquerda/direita, y = cima (-1) / baixo (+1). Teclado (setas/WASD) e direcional do gamepad.</summary>
        public static Vector2Int MenuMove()
        {
            int x = 0, y = 0;
            if (Pressed(K.Up) || Pressed(K.W)) y = -1;
            if (Pressed(K.Down) || Pressed(K.S)) y = 1;
            if (Pressed(K.Left) || Pressed(K.A)) x = -1;
            if (Pressed(K.Right) || Pressed(K.D)) x = 1;
#if HAS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            foreach (var pad in Gamepad.all)
            {
                if (pad.dpad.up.wasPressedThisFrame) y = -1;
                if (pad.dpad.down.wasPressedThisFrame) y = 1;
                if (pad.dpad.left.wasPressedThisFrame) x = -1;
                if (pad.dpad.right.wasPressedThisFrame) x = 1;
            }
#endif
            return new Vector2Int(x, y);
        }

        public static bool MenuConfirm()
        {
            bool r = Pressed(K.Enter) || Pressed(K.Space);
#if HAS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            foreach (var pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame) r = true;
#endif
            return r;
        }

        public static bool PausePressed()
        {
            bool r = Pressed(K.Escape);
#if HAS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            foreach (var pad in Gamepad.all)
                if (pad.startButton.wasPressedThisFrame) r = true;
#endif
            return r;
        }
    }
}
