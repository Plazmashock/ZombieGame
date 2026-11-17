using UnityEngine;
using LastCheckout.Interaction;

namespace LastCheckout.World
{
    /// <summary>
    /// A hinged door. Put this on the HINGE object (the pivot); the door panel is a child.
    /// It always swings AWAY from the player who opens it.
    /// Later: zombies will be able to damage it, and the player can reinforce it.
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        [SerializeField] string displayName = "Door";
        [SerializeField] bool isLocked = false;
        [SerializeField] float openAngle = 95f;
        [Tooltip("How fast the door swings, in degrees per second.")]
        [SerializeField] float swingSpeed = 220f;

        public bool IsOpen { get; private set; }
        public bool IsLocked => isLocked;

        /// <summary>The middle of the doorway at floor level (the hinge is at one edge, the panel is 1.5 m wide).</summary>
        public Vector3 Center => transform.position + transform.right * 0.75f;

        // Every door in the level (zombies use this to push doors open).
        static readonly System.Collections.Generic.List<Door> all = new System.Collections.Generic.List<Door>();
        public static System.Collections.Generic.IReadOnlyList<Door> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();

        Quaternion closedRotation;
        Quaternion targetRotation;

        void Awake()
        {
            closedRotation = transform.localRotation;
            targetRotation = closedRotation;
        }

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() => all.Remove(this);

        public string GetPrompt(PlayerInteractor interactor)
        {
            if (isLocked) return $"{displayName} (Locked)";
            return IsOpen ? $"Close {displayName}" : $"Open {displayName}";
        }

        public bool CanInteract(PlayerInteractor interactor) => !isLocked;

        public void Interact(PlayerInteractor interactor)
        {
            if (IsOpen) Close();
            else Open(interactor.transform.position);
        }

        public void Open(Vector3 openerPosition)
        {
            // Which side of the door is the player on? Swing to the other side.
            float side = Vector3.Dot(openerPosition - transform.position, transform.forward);
            float angle = side > 0f ? openAngle : -openAngle;
            targetRotation = closedRotation * Quaternion.Euler(0f, angle, 0f);
            IsOpen = true;
        }

        public void Close()
        {
            targetRotation = closedRotation;
            IsOpen = false;
        }

        public void SetLocked(bool locked) => isLocked = locked;

        void Update()
        {
            if (transform.localRotation != targetRotation)
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, targetRotation, swingSpeed * Time.deltaTime);
        }
    }
}
