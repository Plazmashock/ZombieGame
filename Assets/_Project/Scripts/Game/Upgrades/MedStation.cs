using UnityEngine;
using LastCheckout.Combat;
using LastCheckout.Interaction;
using LastCheckout.Inventory;
using LastCheckout.Items;

namespace LastCheckout.Game
{
    /// <summary>
    /// Medical bay cabinet (Annex level 2): heals you to full, once per loot phase.
    /// </summary>
    public class MedStation : MonoBehaviour, IInteractable
    {
        int usedWave = -1;
        Light lamp;

        public void Setup(Light statusLight) => lamp = statusLight;

        bool Ready => GameDirector.Instance != null && GameDirector.Instance.Wave != usedWave;

        public string GetPrompt(PlayerInteractor who)
        {
            if (!Ready) return "Med station  ·  recharging until next loot phase";
            var h = who.GetComponent<PlayerHealth>();
            if (h != null && h.Current >= h.Max) return "Med station  ·  you're at full health";
            return "Use med station  ·  heal to full";
        }

        public bool CanInteract(PlayerInteractor who)
        {
            var h = who.GetComponent<PlayerHealth>();
            return Ready && h != null && !h.IsDead && h.Current < h.Max;
        }

        public void Interact(PlayerInteractor who)
        {
            if (!CanInteract(who)) return;
            usedWave = GameDirector.Instance.Wave;
            var h = who.GetComponent<PlayerHealth>();
            h.Heal(h.Max);
        }

        void Update()
        {
            if (lamp != null) lamp.intensity = Ready ? 1.6f + 0.4f * Mathf.Sin(Time.time * 3f) : 0.3f;
        }
    }
}
