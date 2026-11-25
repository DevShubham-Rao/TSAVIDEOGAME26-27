using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SignalLost
{
    /// <summary>
    /// Reads pointer + keys once per frame. Uses the Input System "PlayerControls" action map when the
    /// package backend is active, and the legacy Input Manager otherwise, so the game runs either way.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class InputReader : MonoBehaviour
    {
        public static InputReader I { get; private set; }

        public Vector2 PointerPosition { get; private set; }
        public bool PressedThisFrame { get; private set; }
        public bool ReleasedThisFrame { get; private set; }
        public bool IsPressed { get; private set; }
        public bool UpgradesPressed { get; private set; }
        public bool SkillsPressed { get; private set; }
        public bool MailPressed { get; private set; }
        public bool CancelPressed { get; private set; }
        public bool AnyKeyPressed { get; private set; }
        public bool SpacePressed { get; private set; }
        public bool PausePressed { get; private set; }
        /// <summary>Right mouse button: sucks (vacuums) bits. Left button pops Static.</summary>
        public bool SecondaryPressedThisFrame { get; private set; }
        public bool SecondaryReleasedThisFrame { get; private set; }
        public bool SecondaryIsPressed { get; private set; }
        /// <summary>Mouse wheel ticks this frame (positive = up / zoom in).</summary>
        public float ScrollDelta { get; private set; }
        /// <summary>~ (backquote) or F1: toggles the debug menu.</summary>
        public bool DebugTogglePressed { get; private set; }
        /// <summary>True when the press that is currently held started over a UI element.</summary>
        public bool PressStartedOverUI { get; private set; }

#if ENABLE_INPUT_SYSTEM
        private InputActionMap map;
        private InputAction point;
        private InputAction press;
        private InputAction upgrades;
        private InputAction skills;
        private InputAction mail;
        private InputAction cancel;
        private InputAction anyKey;
        private InputAction space;
        private InputAction pause;
        private InputAction secondary;
        private InputAction scroll;
        private InputAction debug;
#endif

        private void Awake()
        {
            I = this;
#if ENABLE_INPUT_SYSTEM
            map = new InputActionMap("PlayerControls");
            point = map.AddAction("Point", InputActionType.PassThrough, "<Pointer>/position");
            press = map.AddAction("Press", InputActionType.Button, "<Pointer>/press");
            upgrades = map.AddAction("Upgrades", InputActionType.Button, "<Keyboard>/u");
            skills = map.AddAction("Skills", InputActionType.Button, "<Keyboard>/k");
            mail = map.AddAction("Mail", InputActionType.Button, "<Keyboard>/m");
            cancel = map.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");
            pause = map.AddAction("Pause", InputActionType.Button, "<Keyboard>/p");
            pause.AddBinding("<Gamepad>/start");
            secondary = map.AddAction("Secondary", InputActionType.Button, "<Mouse>/rightButton");
            scroll = map.AddAction("Scroll", InputActionType.PassThrough, "<Mouse>/scroll");
            debug = map.AddAction("Debug", InputActionType.Button, "<Keyboard>/backquote");
            debug.AddBinding("<Keyboard>/f1");
            anyKey = map.AddAction("AnyKey", InputActionType.Button, "<Keyboard>/anyKey");
            space = map.AddAction("Space", InputActionType.Button, "<Keyboard>/space");
            map.Enable();
#endif
        }

        private void OnDestroy()
        {
#if ENABLE_INPUT_SYSTEM
            if (map != null)
            {
                map.Disable();
                map.Dispose();
            }
#endif
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            PointerPosition = point.ReadValue<Vector2>();
            PressedThisFrame = press.WasPressedThisFrame();
            ReleasedThisFrame = press.WasReleasedThisFrame();
            IsPressed = press.IsPressed();
            UpgradesPressed = upgrades.WasPressedThisFrame();
            SkillsPressed = skills.WasPressedThisFrame();
            MailPressed = mail.WasPressedThisFrame();
            CancelPressed = cancel.WasPressedThisFrame();
            AnyKeyPressed = anyKey.WasPressedThisFrame();
            SpacePressed = space.WasPressedThisFrame();
            PausePressed = pause.WasPressedThisFrame();
            SecondaryPressedThisFrame = secondary.WasPressedThisFrame();
            SecondaryReleasedThisFrame = secondary.WasReleasedThisFrame();
            SecondaryIsPressed = secondary.IsPressed();
            float sv = scroll.ReadValue<Vector2>().y;
            ScrollDelta = Mathf.Abs(sv) > 3f ? sv / 120f : sv;
            DebugTogglePressed = debug.WasPressedThisFrame();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                PointerPosition = t.position;
                PressedThisFrame = t.phase == TouchPhase.Began;
                ReleasedThisFrame = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                IsPressed = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
            }
            else
            {
                PointerPosition = Input.mousePosition;
                PressedThisFrame = Input.GetMouseButtonDown(0);
                ReleasedThisFrame = Input.GetMouseButtonUp(0);
                IsPressed = Input.GetMouseButton(0);
            }
            UpgradesPressed = Input.GetKeyDown(KeyCode.U);
            SkillsPressed = Input.GetKeyDown(KeyCode.K);
            MailPressed = Input.GetKeyDown(KeyCode.M);
            CancelPressed = Input.GetKeyDown(KeyCode.Escape);
            AnyKeyPressed = Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1);
            SpacePressed = Input.GetKeyDown(KeyCode.Space);
            PausePressed = Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7);
            SecondaryPressedThisFrame = Input.GetMouseButtonDown(1);
            SecondaryReleasedThisFrame = Input.GetMouseButtonUp(1);
            SecondaryIsPressed = Input.GetMouseButton(1);
            ScrollDelta = Input.mouseScrollDelta.y;
            DebugTogglePressed = Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.F1);
#endif
            if (PressedThisFrame) PressStartedOverUI = IsPointerOverUI();
        }

        public bool IsPointerOverUI()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            if (es.IsPointerOverGameObject()) return true;
#if !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0 && es.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return true;
#endif
            return false;
        }
    }
}
