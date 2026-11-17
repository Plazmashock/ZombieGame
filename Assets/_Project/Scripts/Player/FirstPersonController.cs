using UnityEngine;

namespace LastCheckout.Player
{
    /// <summary>
    /// Moves the player's body: walk, sprint, crouch, jump, gravity.
    /// Uses Unity's built-in CharacterController (simple and reliable, no physics surprises).
    /// Looking around is handled separately by PlayerLook.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PlayerInputReader input;
        [Tooltip("The object that holds the camera. It moves down when crouching.")]
        [SerializeField] Transform cameraRoot;

        [Header("Speed (meters per second)")]
        [SerializeField] float walkSpeed = 6f;
        [SerializeField] float sprintSpeed = 8.5f;
        // The scene may still hold the old saved values (3.5 / 6), so these are the minimums actually used.
        const float MinWalk = 6f, MinSprint = 8.5f;
        [SerializeField] float crouchSpeed = 1.8f;
        [Tooltip("How quickly you reach full speed. Higher = snappier.")]
        [SerializeField] float acceleration = 25f;
        [Tooltip("How much you can steer while in the air (0 = none, 1 = full).")]
        [SerializeField, Range(0f, 1f)] float airControl = 0.3f;

        [Header("Carrying weight")]
        [Tooltip("Optional. If set, a heavier backpack makes you slower.")]
        [SerializeField] Inventory.PlayerInventory inventory;
        [Tooltip("Speed multiplier when the backpack is completely full (0.7 = 30% slower).")]
        [SerializeField, Range(0.3f, 1f)] float fullLoadSpeedMultiplier = 0.7f;

        [Header("Jump & Gravity")]
        [SerializeField] float jumpHeight = 1.0f;
        [SerializeField] float gravity = -20f;

        [Header("Crouch")]
        [SerializeField] float standingHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.1f;
        [Tooltip("Distance from the top of the head down to the eyes.")]
        [SerializeField] float eyeOffsetFromTop = 0.2f;
        [SerializeField] float crouchTransitionSpeed = 6f;

        // Read-only state other systems can use later (noise, stamina, zombies hearing you...)
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;
        /// <summary>True while hanging on a ladder (see World/Ladder).</summary>
        public bool IsClimbing => ladder != null;

        const float ClimbSpeed = 3.2f;
        World.Ladder ladder;
        float ladderIgnoreUntil, hopUntil;
        Vector3 hopDirection;

        CharacterController controller;
        Combat.PlayerBuffs buffs;
        Vector3 horizontalVelocity;
        float verticalVelocity;

        void Reset() => AutoFindReferences();

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            AutoFindReferences();
        }

        void AutoFindReferences()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (inventory == null) inventory = GetComponent<Inventory.PlayerInventory>();
            if (cameraRoot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraRoot = cam.transform.parent != transform ? cam.transform.parent : cam.transform;
            }
        }

        void Update()
        {
            UpdateCrouch();
            UpdateMovement();
        }

        void UpdateCrouch()
        {
            // Hold to crouch. Only stand up if there is room above your head.
            bool wantsCrouch = input.CrouchHeld;
            if (!wantsCrouch && IsCrouching && !HasRoomToStand()) wantsCrouch = true;
            IsCrouching = wantsCrouch;

            float targetHeight = IsCrouching ? crouchHeight : standingHeight;
            float h = Mathf.MoveTowards(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            controller.height = h;
            controller.center = new Vector3(0f, h * 0.5f, 0f); // keep feet on the floor

            if (cameraRoot != null)
            {
                Vector3 p = cameraRoot.localPosition;
                p.y = h - eyeOffsetFromTop;
                cameraRoot.localPosition = p;
            }
        }

        bool HasRoomToStand()
        {
            float radius = controller.radius * 0.95f;
            Vector3 topSphere = transform.position + Vector3.up * (controller.height - radius);
            float distance = standingHeight - controller.height + 0.05f;
            return !Physics.SphereCast(topSphere, radius, Vector3.up, out _, distance, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Ladder climbing. Returns true when the ladder is handling movement this frame.
        ///   W = up, S = down, Space = jump off. At the top, W again hops you onto the roof.
        /// </summary>
        bool UpdateLadder(Vector2 move)
        {
            float dt = Time.deltaTime;

            // The little hop from the top rung onto the roof.
            if (hopUntil > 0f)
            {
                if (Time.time < hopUntil)
                {
                    horizontalVelocity = hopDirection * 3.4f;
                    verticalVelocity += gravity * dt;
                    controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
                    IsGrounded = false;
                    return true;
                }
                hopUntil = 0f;
                horizontalVelocity = Vector3.zero;
            }

            Vector3 feet = transform.position;

            if (ladder == null)
            {
                if (Time.time < ladderIgnoreUntil || Mathf.Abs(move.y) < 0.1f || World.Ladder.All.Count == 0) return false;
                float facing;
                foreach (var l in World.Ladder.All)
                {
                    facing = Vector3.Dot(transform.forward, l.Toward);
                    if (move.y > 0f && facing > 0.3f && l.Contains(feet)) { ladder = l; break; }          // climb up
                    if (move.y < 0f && facing < -0.3f && l.ContainsTop(feet)) { ladder = l; break; }      // climb down from the roof
                }
                if (ladder == null) return false;
                horizontalVelocity = Vector3.zero;
                verticalVelocity = 0f;
            }

            // Letting go: jump, walked out of the zone, or reached the ground going down.
            bool jumped = input.JumpPressed;
            if (jumped || !ladder.InZone(feet) || (move.y < -0.1f && IsGrounded))
            {
                Vector3 away = -ladder.Toward;
                ladder = null;
                ladderIgnoreUntil = Time.time + 0.4f;
                if (jumped) { horizontalVelocity = away * 3f; verticalVelocity = 4f; }
                return false;
            }

            // Top of the ladder: hop onto the roof.
            if (feet.y >= ladder.TopY - 0.05f && move.y > 0.1f)
            {
                hopDirection = ladder.Toward;
                hopUntil = Time.time + 0.5f;
                verticalVelocity = 3.4f;
                ladder = null;
                ladderIgnoreUntil = Time.time + 0.9f;
                return true;
            }

            // Climbing: slide towards the middle of the ladder and move up / down.
            if (buffs == null) buffs = GetComponent<Combat.PlayerBuffs>();
            float speed = ClimbSpeed * (buffs != null ? buffs.SpeedMult : 1f);
            Vector3 d = feet - ladder.Column;
            float along = Vector3.Dot(d, ladder.Toward), across = Vector3.Dot(d, ladder.Across);
            Vector3 hold = ladder.Toward * Mathf.Clamp(-along * 6f, -3f, 3f) + ladder.Across * Mathf.Clamp(-across * 4f, -1.5f, 1.5f);
            controller.Move((hold + Vector3.up * (move.y * speed)) * dt);
            IsGrounded = false;
            IsSprinting = false;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            return true;
        }

        void UpdateMovement()
        {
            IsGrounded = controller.isGrounded;

            // 1) Which direction does the player want to go? (relative to where they are facing)
            Vector2 move = input.Move;
            if (UpdateLadder(move)) return;   // on a ladder: climbing replaces walking
            Vector3 wishDir = Vector3.ClampMagnitude(transform.right * move.x + transform.forward * move.y, 1f);

            // 2) How fast?  (sprint only when moving forward and not crouching)
            IsSprinting = input.SprintHeld && !IsCrouching && move.y > 0.1f;
            float targetSpeed = IsCrouching ? crouchSpeed : IsSprinting ? Mathf.Max(sprintSpeed, MinSprint) : Mathf.Max(walkSpeed, MinWalk);
            if (inventory != null)
                targetSpeed *= Mathf.Lerp(1f, fullLoadSpeedMultiplier, inventory.LoadFraction); // heavy bag = slower

            if (buffs == null) buffs = GetComponent<Combat.PlayerBuffs>();
            if (buffs != null) targetSpeed *= buffs.SpeedMult; // energy drink etc.

            // 3) Smoothly speed up / slow down
            float accel = IsGrounded ? acceleration : acceleration * airControl;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wishDir * targetSpeed, accel * Time.deltaTime);

            // 4) Gravity and jumping
            if (IsGrounded && verticalVelocity < 0f) verticalVelocity = -2f; // small push keeps us glued to the floor
            if (IsGrounded && !IsCrouching && input.JumpPressed)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            verticalVelocity += gravity * Time.deltaTime;

            // 5) Move, and stop rising if we bump our head
            CollisionFlags flags = controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
        }
    }
}
