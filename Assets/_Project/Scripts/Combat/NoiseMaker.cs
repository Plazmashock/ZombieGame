using UnityEngine;

namespace LastCheckout.Combat
{
    /// <summary>
    /// A "loudspeaker" for the whole game. Anything noisy (a gunshot, later a breaking window)
    /// calls NoiseMaker.Emit(position, radius). Zombies listen and walk over to investigate.
    /// The shooter doesn't need to know which zombies exist, and zombies don't need to know who made the noise.
    /// </summary>
    public static class NoiseMaker
    {
        /// <summary>Fires on every noise: (where it happened, how far it carries in meters).</summary>
        public static event System.Action<Vector3, float> NoiseMade;

        public static void Emit(Vector3 position, float radius) => NoiseMade?.Invoke(position, radius);
    }
}
