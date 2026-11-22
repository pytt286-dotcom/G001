using UnityEngine;
using UnityEngine.InputSystem;

namespace Sealbound.Player
{
    /// <summary>
    /// Reads keyboard, mouse and gamepad input and exposes it as simple values.
    /// Actions are defined in code so the setup does not depend on an asset.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        public Vector2 LookMouse { get; private set; }
        public Vector2 LookStick { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool JumpHeld { get; private set; }

        /// <summary>True for one frame when jump was pressed.</summary>
        public bool JumpPressed { get; private set; }
        /// <summary>True for one frame when dodge was pressed.</summary>
        public bool DodgePressed { get; private set; }

        InputActionMap _map;
        InputAction _move, _lookMouse, _lookStick, _sprint, _jump, _dodge;

        void Awake()
        {
            _map = new InputActionMap("Player");

            _move = _map.AddAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");

            _lookMouse = _map.AddAction("LookMouse", InputActionType.Value, "<Mouse>/delta");
            _lookStick = _map.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            _sprint = _map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _sprint.AddBinding("<Gamepad>/leftStickPress");

            _jump = _map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _jump.AddBinding("<Gamepad>/buttonSouth");

            _dodge = _map.AddAction("Dodge", InputActionType.Button, "<Keyboard>/leftCtrl");
            _dodge.AddBinding("<Keyboard>/c");
            _dodge.AddBinding("<Mouse>/rightButton");
            _dodge.AddBinding("<Gamepad>/buttonEast");
        }

        void OnEnable() => _map.Enable();
        void OnDisable() => _map.Disable();
        void OnDestroy() => _map.Dispose();

        void Update()
        {
            Move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            bool cursorFree = Cursor.lockState != CursorLockMode.Locked;
            LookMouse = cursorFree ? Vector2.zero : _lookMouse.ReadValue<Vector2>();
            LookStick = _lookStick.ReadValue<Vector2>();
            SprintHeld = _sprint.IsPressed();
            JumpHeld = _jump.IsPressed();
            JumpPressed = _jump.WasPressedThisFrame();
            DodgePressed = _dodge.WasPressedThisFrame() && !(cursorFree && _dodge.activeControl?.device is Mouse);
        }
    }
}
