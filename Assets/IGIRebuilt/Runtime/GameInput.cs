using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IGIRebuilt
{
    public static class GameInput
    {
        public static Vector2 Move
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;
                return Vector2.ClampMagnitude(new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0)), 1);
#else
                return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            }
        }
        public static Vector2 Look
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.075f : Vector2.zero;
#else
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2;
#endif
            }
        }
        public static bool Fire(bool automatic)
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && (automatic ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame);
#else
            return automatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
#endif
        }
        public static bool Aim
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
                return Input.GetMouseButton(1);
#endif
            }
        }
        public static bool ArsenalHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.tabKey.isPressed;
#else
                return Input.GetKey(KeyCode.Tab);
#endif
            }
        }
        public static bool Sprint
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
                return Input.GetKey(KeyCode.LeftShift);
#endif
            }
        }
        public static bool UseHeld { get {
#if ENABLE_INPUT_SYSTEM
 return Keyboard.current != null && Keyboard.current.eKey.isPressed;
#else
 return Input.GetKey(KeyCode.E);
#endif
 } }
        public static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            switch (key)
            {
                case KeyCode.Return: return k.enterKey.wasPressedThisFrame;
                case KeyCode.Space: return k.spaceKey.wasPressedThisFrame;
                case KeyCode.LeftControl: return k.leftCtrlKey.wasPressedThisFrame;
                case KeyCode.C: return k.cKey.wasPressedThisFrame;
                case KeyCode.E: return k.eKey.wasPressedThisFrame;
                case KeyCode.R: return k.rKey.wasPressedThisFrame;
                case KeyCode.Escape: return k.escapeKey.wasPressedThisFrame;
                case KeyCode.Alpha1: return k.digit1Key.wasPressedThisFrame;
                case KeyCode.Alpha2: return k.digit2Key.wasPressedThisFrame;
                case KeyCode.Alpha3: return k.digit3Key.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetKeyDown(key);
#endif
        }
        public static float Scroll
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                float delta = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0;
#else
                float delta = Input.mouseScrollDelta.y;
#endif
                return delta > 0 ? 1 : delta < 0 ? -1 : 0;
            }
        }
    }
}
