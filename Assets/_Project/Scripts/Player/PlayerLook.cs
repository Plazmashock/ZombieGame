using UnityEngine;

namespace LastCheckout.Player
{
    /// <summary>
    /// Mouse look.
    /// Left/right turns the whole player body. Up/down tilts only the camera.
    /// Also locks the mouse cursor to the game window (Esc releases it, left click captures it again).
    /// </summary>
    [DefaultExecutionOrder(-50)] // look before moving, so movement uses this frame's facing
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] Transform cameraRoot;

        [Tooltip("Degrees turned per pixel of mouse movement.")]
        [SerializeField, Range(0.01f, 1f)] float sensitivity = 0.12f;
        [SerializeField] bool invertY = false;
        [SerializeField] float minPitch = -85f;
        [SerializeField] float maxPitch = 85f;

        float pitch;

        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>Degrees per pixel of mouse movement (set from the Settings menu).</summary>
        public float Sensitivity { get => sensitivity; set => sensitivity = Mathf.Clamp(value, 0.01f, 1f); }
        /// <summary>Mouse up = look down (set from the Settings menu).</summary>
        public bool InvertY { get => invertY; set => invertY = value; }

        /// <summary>True while a menu (like the backpack) is open: no mouse-look, and clicking won't capture the mouse.</summary>
        public bool IsUIOpen { get; set; }

        void Reset() => AutoFindReferences();

        void Awake() => AutoFindReferences();

        void AutoFindReferences()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (cameraRoot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraRoot = cam.transform.parent != transform ? cam.transform.parent : cam.transform;
            }
        }

        void Start() => SetCursorLocked(true);

        void Update()
        {
            if (input.PausePressed) SetCursorLocked(false);
            else if (!IsCursorLocked && !IsUIOpen && input.ClickPressed) SetCursorLocked(true);

            if (!IsCursorLocked || IsUIOpen) return;

            Vector2 delta = input.Look * sensitivity;

            transform.Rotate(0f, delta.x, 0f, Space.Self);            // turn body left/right

            pitch += invertY ? delta.y : -delta.y;                     // tilt camera up/down
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
