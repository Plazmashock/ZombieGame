using UnityEngine;
using LastCheckout.Interaction;

namespace LastCheckout.World
{
    /// <summary>
    /// Toggles a group of lights. Later this can require power (generator + fuel, batteries).
    /// </summary>
    public class LightSwitch : MonoBehaviour, IInteractable
    {
        [SerializeField] Light[] lights;
        [SerializeField] bool isOn = true;

        void Start() => Apply();

        public string GetPrompt(PlayerInteractor interactor) => isOn ? "Turn lights off" : "Turn lights on";
        public bool CanInteract(PlayerInteractor interactor) => true;

        public void Interact(PlayerInteractor interactor)
        {
            isOn = !isOn;
            Apply();
        }

        void Apply()
        {
            if (lights == null) return;
            foreach (var l in lights)
                if (l != null) l.enabled = isOn;
        }
    }
}
