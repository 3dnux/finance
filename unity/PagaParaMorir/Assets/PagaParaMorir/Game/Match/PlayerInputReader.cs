using UnityEngine;
#if ENABLE_INPUT_SYSTEM && PPM_HAS_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PagaParaMorir.Game.Match
{
    public struct InputSnapshot
    {
        public Vector2 Move;
        /// <summary>Movimiento del ratón en grados (ya escalado por sensibilidad).</summary>
        public Vector2 Look;
        public bool Jump;
        public bool FireHeld;
        public bool Reload;
        /// <summary>0-3 si se apretó una tecla de arma; -1 si no.</summary>
        public int WeaponSlot;
        public bool ToggleCursor;
    }

    /// <summary>Teclado y ratón, con el Input System nuevo o el antiguo según el proyecto.</summary>
    public static class PlayerInputReader
    {
        public static float Sensitivity = 1f;

        public static InputSnapshot Read()
        {
            var s = new InputSnapshot { WeaponSlot = -1 };
#if ENABLE_INPUT_SYSTEM && PPM_HAS_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                s.Move = new Vector2(
                    (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                    (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
                s.Jump = kb.spaceKey.wasPressedThisFrame;
                s.Reload = kb.rKey.wasPressedThisFrame;
                s.ToggleCursor = kb.escapeKey.wasPressedThisFrame;
                if (kb.digit1Key.wasPressedThisFrame) s.WeaponSlot = 0;
                if (kb.digit2Key.wasPressedThisFrame) s.WeaponSlot = 1;
                if (kb.digit3Key.wasPressedThisFrame) s.WeaponSlot = 2;
                if (kb.digit4Key.wasPressedThisFrame) s.WeaponSlot = 3;
            }
            if (mouse != null)
            {
                s.Look = mouse.delta.ReadValue() * 0.08f * Sensitivity;
                s.FireHeld = mouse.leftButton.isPressed;
            }
#else
            s.Move = new Vector2(
                (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            s.Look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 2f * Sensitivity;
            s.Jump = Input.GetKeyDown(KeyCode.Space);
            s.FireHeld = Input.GetMouseButton(0);
            s.Reload = Input.GetKeyDown(KeyCode.R);
            s.ToggleCursor = Input.GetKeyDown(KeyCode.Escape);
            if (Input.GetKeyDown(KeyCode.Alpha1)) s.WeaponSlot = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) s.WeaponSlot = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) s.WeaponSlot = 2;
            if (Input.GetKeyDown(KeyCode.Alpha4)) s.WeaponSlot = 3;
#endif
            return s;
        }

        public static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
