using UnityEngine;
using LastCheckout.Player;

namespace LastCheckout.Interaction
{
    /// <summary>
    /// Every frame, shoots an invisible ray from the camera forward.
    /// If it hits something interactable within reach, that becomes the "target".
    /// Pressing E interacts with the target.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [Tooltip("Where the ray starts and points from. Use the player camera.")]
        [SerializeField] Transform viewPoint;
        [Tooltip("How far away (meters) you can interact with things.")]
        [SerializeField] float reach = 2.5f;
        [SerializeField] LayerMask interactionMask = ~0; // ~0 = everything

        public IInteractable CurrentTarget { get; private set; }
        public bool HasTarget => IsAlive(CurrentTarget);
        public string CurrentPrompt => HasTarget ? CurrentTarget.GetPrompt(this) : string.Empty;
        public bool CanInteractWithTarget => HasTarget && CurrentTarget.CanInteract(this);

        /// <summary>Fires when the player starts or stops looking at something interactable.</summary>
        public event System.Action<IInteractable> TargetChanged;

        void Reset() => AutoFindReferences();

        void Awake() => AutoFindReferences();

        void AutoFindReferences()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (viewPoint == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) viewPoint = cam.transform;
            }
        }

        void Update()
        {
            UpdateTarget();

            if (input.InteractPressed && CanInteractWithTarget)
                CurrentTarget.Interact(this);
        }

        void UpdateTarget()
        {
            IInteractable found = null;
            if (Physics.Raycast(viewPoint.position, viewPoint.forward, out RaycastHit hit, reach,
                                interactionMask, QueryTriggerInteraction.Ignore))
            {
                // GetComponentInParent: we may hit a child (e.g. a door's panel) of the interactable object.
                found = hit.collider.GetComponentInParent<IInteractable>();
            }

            if (!ReferenceEquals(found, CurrentTarget))
            {
                CurrentTarget = found;
                TargetChanged?.Invoke(found);
            }
        }

        // Unity objects can be destroyed while we still hold a reference; this catches that.
        static bool IsAlive(IInteractable i) => i is Object o ? o != null : i != null;
    }
}
