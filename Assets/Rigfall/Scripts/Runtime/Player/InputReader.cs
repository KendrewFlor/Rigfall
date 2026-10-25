// One place that reads keyboard, mouse, gamepad and on-screen touch controls, using
// Unity's built-in Input Manager (no extra packages). Gameplay code asks for actions
// ("attack held", "interact held") instead of keys.
//
// Keyboard/mouse:  WASD move · LMB/J attack · RMB/K ability · Space dash · E hold to build
//                  Q/Tab next option · 1-4 pick option · F hold to command · N hold to start night · Esc pause
// Gamepad:         stick move · A attack · B ability · X hold build · Y hold night · LB dash
//                  RB hold command · Back next option · Start pause
using UnityEngine;

namespace Rigfall.Runtime
{
    public static class InputReader
    {
        // Touch buttons write here (see TouchControls).
        public static Vector2 TouchMove;
        public static bool TouchAttack, TouchAbility, TouchInteract, TouchCommand, TouchNight, TouchDash, TouchCycle;
        static bool lastTouchAbility, lastTouchDash, lastTouchCycle;
        static bool touchAbilityDown, touchDashDown, touchCycleDown;

        static Vector3 lastMouse;
        static float lastMouseMove = -99f, lastPadUse = -99f;

        /// <summary>True while the mouse was the most recently used aiming device.</summary>
        public static bool AimWithMouse => lastMouseMove > lastPadUse && Time.unscaledTime - lastMouseMove < 6f && !UsingTouch;

        public static bool UsingTouch => Input.touchSupported && Input.touchCount > 0 || TouchMove.sqrMagnitude > 0.01f;

        /// <summary>Call once per frame (Game does this) to update edge-triggered touch buttons and device tracking.</summary>
        public static void Tick()
        {
            touchAbilityDown = TouchAbility && !lastTouchAbility; lastTouchAbility = TouchAbility;
            touchDashDown = TouchDash && !lastTouchDash; lastTouchDash = TouchDash;
            touchCycleDown = TouchCycle && !lastTouchCycle; lastTouchCycle = TouchCycle;
            if ((Input.mousePosition - lastMouse).sqrMagnitude > 4f) { lastMouseMove = Time.unscaledTime; lastMouse = Input.mousePosition; }
            if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.3f && !AnyMoveKey() || PadButton()) lastPadUse = Time.unscaledTime;
        }

        static bool AnyMoveKey() =>
            Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);

        static bool PadButton()
        {
            for (int i = 0; i <= 7; i++) if (Input.GetKey(KeyCode.JoystickButton0 + i)) return true;
            return false;
        }

        public static Vector2 Move
        {
            get
            {
                var v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                if (TouchMove.sqrMagnitude > v.sqrMagnitude) v = TouchMove;
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        // Touches also arrive as mouse button 0, so the mouse is ignored while a finger is down
        // (otherwise steering with the on-screen stick would swing the weapon).
        public static bool AttackHeld =>
            (Input.GetMouseButton(0) && Input.touchCount == 0 && !Ui.PointerOverUi) || Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.JoystickButton0) || TouchAttack;

        public static bool AbilityPressed =>
            (Input.GetMouseButtonDown(1) && !Ui.PointerOverUi) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.JoystickButton1) || touchAbilityDown;

        public static bool DashPressed =>
            Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.JoystickButton4) || touchDashDown;

        public static bool InteractHeld =>
            Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.JoystickButton2) || TouchInteract;

        public static bool CyclePressed =>
            Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton6) || touchCycleDown;

        /// <summary>Number keys 1-4 pick an upgrade option directly. Returns -1 if none pressed.</summary>
        public static int OptionKeyPressed
        {
            get
            {
                for (int i = 0; i < 4; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) return i;
                return -1;
            }
        }

        public static bool CommandHeld =>
            Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.JoystickButton5) || TouchCommand;

        public static bool StartNightHeld =>
            Input.GetKey(KeyCode.N) || Input.GetKey(KeyCode.JoystickButton3) || TouchNight;

        public static bool PausePressed =>
            Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7);

        public static bool BackPressed =>
            Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);

        public static bool ToggleRangesPressed => Input.GetKeyDown(KeyCode.R);

        public static Vector3 MouseScreen => Input.mousePosition;
    }
}
