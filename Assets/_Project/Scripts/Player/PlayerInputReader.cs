using UnityEngine;
using UnityEngine.InputSystem;

namespace LastCheckout.Player
{
    /// <summary>
    /// The ONLY script that knows which keys do what.
    /// Other scripts ask this one ("is jump pressed?") instead of reading the keyboard themselves.
    /// If you want to change controls or add a gamepad later, you only edit this file.
    ///
    /// Controls:  WASD/Arrows = move, Mouse = look, Space = jump, Shift = sprint,
    ///            Ctrl or C = crouch, E = interact, Tab or I = backpack, 1 = primary, 2 = sidearm, 3 = melee (mouse wheel cycles, Q = previous weapon), R = reload, H = heal,
    ///            Esc = pause menu, Left click = capture mouse, U = store upgrades, N = start the wave now.
    ///
    /// Two groups of keys:
    ///   GAMEPLAY keys (move, shoot...) are switched OFF while a menu is open (see GameplayEnabled).
    ///   MENU keys (Esc, U, N) always work, so they can open AND close menus.
    /// </summary>
    [DefaultExecutionOrder(-100)] // runs before other player scripts, so input is ready for them
    public class PlayerInputReader : MonoBehaviour
    {
        InputAction move, look, jump, sprint, crouch, interact, pause, click, inventory, reload, slot1, slot2, slot3, slot4, heal, confirm, scroll;
        InputAction skipWave, upgrades, lastWeapon;
        InputAction[] slots; // keys 1..9 and 0 (0 = slot 10)

        // --- What other scripts read ---
        public Vector2 Move => move.ReadValue<Vector2>();
        public Vector2 Look => look.ReadValue<Vector2>();          // mouse movement this frame, in pixels
        public bool JumpPressed => jump.WasPressedThisFrame();
        public bool SprintHeld => sprint.IsPressed();
        public bool CrouchHeld => crouch.IsPressed();
        public bool InteractPressed => interact.WasPressedThisFrame();
        public bool PausePressed => pause.WasPressedThisFrame();
        public bool ClickPressed => click.WasPressedThisFrame();
        public bool ClickHeld => click.IsPressed();                 // held down (full-auto rifle, drawing the bow)
        public bool ClickReleased => click.WasReleasedThisFrame();  // let go (fires the bow)
        /// <summary>Mouse wheel this frame: positive = up, negative = down, 0 = not scrolled.</summary>
        public float Scroll => scroll.ReadValue<Vector2>().y;
        /// <summary>Which weapon key was pressed this frame: 1-9, 10 for the 0 key, or 0 if none.</summary>
        public int SlotPressed
        {
            get
            {
                for (int i = 0; i < slots.Length; i++) if (slots[i].WasPressedThisFrame()) return i + 1;
                return 0;
            }
        }
        public bool InventoryPressed => inventory.WasPressedThisFrame();
        public bool ReloadPressed => reload.WasPressedThisFrame();
        public bool Slot1Pressed => slot1.WasPressedThisFrame();   // melee
        public bool Slot2Pressed => slot2.WasPressedThisFrame();   // pistol
        public bool Slot3Pressed => slot3.WasPressedThisFrame();   // shotgun
        public bool Slot4Pressed => slot4.WasPressedThisFrame();   // rifle
        public bool LastWeaponPressed => lastWeapon.WasPressedThisFrame(); // Q: swap back to the previous weapon
        public bool HealPressed => heal.WasPressedThisFrame();     // use a medkit / painkillers
        public bool ConfirmPressed => confirm.WasPressedThisFrame(); // Enter (restart on the death screen)
        public bool SkipWavePressed => skipWave.WasPressedThisFrame(); // N: skip the loot timer, zombies now
        public bool UpgradesPressed => upgrades.WasPressedThisFrame(); // U: open/close store upgrades

        bool gameplayEnabled = true;

        /// <summary>
        /// False while a menu is open: movement, looking, shooting etc. read as "nothing pressed".
        /// The menu keys (Esc, U, N) keep working.
        /// </summary>
        public bool GameplayEnabled
        {
            get => gameplayEnabled;
            set
            {
                if (gameplayEnabled == value) return;
                gameplayEnabled = value;
                if (!isActiveAndEnabled) return;
                foreach (var a in Gameplay) { if (value) a.Enable(); else a.Disable(); }
            }
        }

        void Awake()
        {
            // We define the controls in code so nothing has to be wired up in the Inspector.
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            sprint = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");

            crouch = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
            crouch.AddBinding("<Keyboard>/c");

            interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            click = new InputAction("Click", InputActionType.Button, "<Mouse>/leftButton");

            inventory = new InputAction("Inventory", InputActionType.Button, "<Keyboard>/tab");
            inventory.AddBinding("<Keyboard>/i");

            reload = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");
            slots = new InputAction[10];
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
            for (int i = 0; i < keys.Length; i++)
                slots[i] = new InputAction("Slot" + (i + 1), InputActionType.Button, "<Keyboard>/" + keys[i]);
            slot1 = slots[0]; slot2 = slots[1]; slot3 = slots[2]; slot4 = slots[3];
            scroll = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll");
            lastWeapon = new InputAction("Last Weapon", InputActionType.Button, "<Keyboard>/q");
            heal = new InputAction("Heal", InputActionType.Button, "<Keyboard>/h");
            confirm = new InputAction("Confirm", InputActionType.Button, "<Keyboard>/enter");
            skipWave = new InputAction("Skip Wave", InputActionType.Button, "<Keyboard>/n");
            upgrades = new InputAction("Upgrades", InputActionType.Button, "<Keyboard>/u");
        }

        /// Keys that get switched off while a menu is open.
        InputAction[] Gameplay
        {
            get
            {
                var list = new System.Collections.Generic.List<InputAction> { move, look, jump, sprint, crouch, interact, click, inventory, reload, heal, confirm, scroll, lastWeapon };
                list.AddRange(slots);
                return list.ToArray();
            }
        }

        /// Keys that always work (they open and close menus).
        InputAction[] Menu => new[] { pause, skipWave, upgrades };

        void OnEnable()
        {
            foreach (var a in Menu) a.Enable();
            if (gameplayEnabled) foreach (var a in Gameplay) a.Enable();
        }
        void OnDisable() { foreach (var a in Menu) a.Disable(); foreach (var a in Gameplay) a.Disable(); }
        void OnDestroy() { foreach (var a in Menu) a.Dispose(); foreach (var a in Gameplay) a.Dispose(); }
    }
}
