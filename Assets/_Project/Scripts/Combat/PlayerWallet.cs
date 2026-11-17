using System.Collections.Generic;
using UnityEngine;

namespace LastCheckout.Combat
{
    /// <summary>
    /// Your money and the weapons you've bought.
    ///   Earn:  every zombie kill (bigger zombies pay more) + a bonus for clearing each wave.
    ///   Spend: weapons and ammo at the armory counters (see Game/ShopCounter).
    /// Bought weapons are yours for the rest of the run and weigh nothing; ammo goes in your backpack.
    /// </summary>
    public class PlayerWallet : MonoBehaviour
    {
        [Tooltip("Enough for a pistol ($100) + one box of 9mm ($25).")]
        [SerializeField] int startingCash = 125;

        public int Cash { get; private set; }

        /// <summary>Fires on any change (money or owned weapons).</summary>
        public event System.Action Changed;
        /// <summary>(amount, reason) for "+$15 RUNNER" messages.</summary>
        public event System.Action<int, string> Earned;
        /// <summary>(amount, what was bought) for "-$100 PISTOL" messages.</summary>
        public event System.Action<int, string> Spent;

        readonly HashSet<string> owned = new HashSet<string>();

        void Awake() => Cash = startingCash;

        public bool Owns(string weaponId) => weaponId != null && owned.Contains(weaponId);
        public bool CanAfford(int price) => Cash >= price;

        /// <summary>The weapon you bought for a loadout slot, or null.</summary>
        public WeaponSpec OwnedWeaponIn(WeaponGroup group)
        {
            foreach (var id in owned)
            {
                var s = WeaponCatalog.Get(id);
                if (s != null && s.group == group) return s;
            }
            return null;
        }

        /// <summary>
        /// You get the weapon. Only ONE weapon per loadout slot: the one you had there is sold back for half its price.
        /// </summary>
        public void Grant(string weaponId)
        {
            if (weaponId == null) return;
            var spec = WeaponCatalog.Get(weaponId);
            if (spec != null)
            {
                var old = OwnedWeaponIn(spec.group);
                if (old != null && old != spec)
                {
                    owned.Remove(old.id);
                    int refund = old.price / 2;
                    if (refund > 0) { Cash += refund; Earned?.Invoke(refund, "SOLD " + old.displayName); }
                }
            }
            if (owned.Add(weaponId) || spec != null) Changed?.Invoke();
        }

        public void Add(int amount, string reason)
        {
            if (amount <= 0) return;
            Cash += amount;
            Earned?.Invoke(amount, reason);
            Changed?.Invoke();
        }

        /// <summary>Pays if you have enough. Returns false (and charges nothing) if you don't.</summary>
        public bool TrySpend(int amount, string what)
        {
            if (amount < 0 || Cash < amount) return false;
            Cash -= amount;
            Spent?.Invoke(amount, what);
            Changed?.Invoke();
            return true;
        }
    }
}
