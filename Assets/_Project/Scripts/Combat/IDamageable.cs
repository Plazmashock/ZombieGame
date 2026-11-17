namespace LastCheckout.Combat
{
    /// <summary>
    /// Anything that can be hurt: zombies, the player, later maybe barricades.
    /// The attacker doesn't care WHAT it hit, only that it can call TakeDamage.
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(float amount);
    }
}
