using UnityEngine;
using LastCheckout.Combat;
using LastCheckout.Interaction;
using LastCheckout.Inventory;
using LastCheckout.Items;

namespace LastCheckout.Game
{
    /// <summary>
    /// The kiosk in the back corridor: look at it + E = open the STORE UPGRADES screen.
    /// (You can also press U anywhere during the loot phase.)
    /// </summary>
    public class UpgradeTerminal : MonoBehaviour, IInteractable
    {
        public string GetPrompt(PlayerInteractor who)
        {
            var d = GameDirector.Instance;
            if (d != null && d.Current == GameDirector.Phase.Wave) return "Upgrade terminal  ·  locked during waves";
            return "Open store upgrades";
        }

        public bool CanInteract(PlayerInteractor who)
        {
            var d = GameDirector.Instance;
            return d != null && d.Current != GameDirector.Phase.Wave && d.Current != GameDirector.Phase.GameOver;
        }

        public void Interact(PlayerInteractor who)
        {
            if (UI.UpgradeMenu.Instance != null) UI.UpgradeMenu.Instance.Open();
        }
    }
}
