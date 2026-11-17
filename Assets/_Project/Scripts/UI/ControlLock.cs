using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using LastCheckout.Combat;
using LastCheckout.Player;

namespace LastCheckout.UI
{
    /// <summary>
    /// "A menu is open: freeze the player's hands and free the mouse."
    ///
    ///   Acquire(menu)  → movement, shooting, looking OFF · mouse cursor visible
    ///   Release(menu)  → when NO menu holds it any more: controls ON · mouse captured again
    ///
    /// Several menus can hold it at once (e.g. Settings opened from Pause); control only comes back
    /// when the last one lets go. Keys that open/close menus (Esc, U, N) keep working the whole time.
    /// </summary>
    public static class ControlLock
    {
        static readonly HashSet<object> holders = new HashSet<object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => holders.Clear();

        public static bool IsHeld => holders.Count > 0;

        public static void Acquire(object who) { if (holders.Add(who)) Apply(); }
        public static void Release(object who) { if (holders.Remove(who)) Apply(); }

        /// <summary>Forget every holder (new scene loaded).</summary>
        public static void Clear() => holders.Clear();

        static void Apply()
        {
            // Nothing stays "selected" in the UI (otherwise Space/Enter could press the last clicked button).
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            var player = GameObject.FindWithTag("Player");
            if (player == null) return;
            var input = player.GetComponent<PlayerInputReader>();
            var look = player.GetComponent<PlayerLook>();
            var health = player.GetComponent<PlayerHealth>();
            bool held = holders.Count > 0;
            bool dead = health != null && health.IsDead;

            if (input != null) input.GameplayEnabled = !held;
            if (look == null) return;
            if (held)
            {
                look.IsUIOpen = true;
                look.SetCursorLocked(false);
            }
            else
            {
                look.IsUIOpen = dead;          // the death screen keeps the mouse free
                look.SetCursorLocked(!dead);
            }
        }
    }
}
