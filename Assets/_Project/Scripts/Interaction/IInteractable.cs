namespace LastCheckout.Interaction
{
    /// <summary>
    /// A "contract": any object the player can press E on.
    /// Doors, light switches, and later loot, shelves and barricades all implement this.
    /// The PlayerInteractor doesn't care WHAT the object is — only that it follows this contract.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Text shown on screen, e.g. "Open door".</summary>
        string GetPrompt(PlayerInteractor interactor);

        /// <summary>False = show the prompt greyed-out and ignore E (e.g. a locked door).</summary>
        bool CanInteract(PlayerInteractor interactor);

        /// <summary>What happens when the player presses E.</summary>
        void Interact(PlayerInteractor interactor);
    }
}
