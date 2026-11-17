using UnityEngine;
using LastCheckout.Inventory;
using LastCheckout.Items;
using LastCheckout.Player;

namespace LastCheckout.Combat
{
    /// <summary>
    /// Press H to patch yourself up with medical supplies from your backpack.
    /// Uses the best item you carry that isn't wasted:
    ///   First Aid Kit = +60 HP      Painkillers = +25 HP
    /// Healing takes a moment (you can keep moving, but can't heal twice at once).
    /// </summary>
    public class PlayerHealing : MonoBehaviour
    {
        [SerializeField] float useTime = 1.2f;

        PlayerInputReader input;
        PlayerHealth health;
        PlayerInventory inventory;
        PlayerLook look;

        ItemDefinition pendingItem;
        float pendingHeal, finishTime;

        static readonly (string id, float heal)[] Meds = { ("first_aid", 60f), ("painkillers", 25f) };

        public bool IsHealing => pendingItem != null;
        public float Progress => IsHealing ? Mathf.Clamp01(1f - (finishTime - Time.time) / useTime) : 0f;

        /// <summary>How many healing items you carry in total.</summary>
        public int MedCount
        {
            get
            {
                int n = 0;
                if (inventory == null) return 0;
                foreach (var s in inventory.Stacks)
                    foreach (var m in Meds)
                        if (s.item != null && s.item.id == m.id) n += s.count;
                return n;
            }
        }

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            health = GetComponent<PlayerHealth>();
            inventory = GetComponent<PlayerInventory>();
            look = GetComponent<PlayerLook>();
        }

        void Update()
        {
            if (health == null || inventory == null || health.IsDead) { pendingItem = null; return; }

            if (IsHealing && Time.time >= finishTime)
            {
                // Only use up the item at the END, so getting interrupted (death) doesn't waste it.
                if (inventory.Remove(pendingItem, 1)) health.Heal(pendingHeal);
                pendingItem = null;
            }

            bool free = look == null || (look.IsCursorLocked && !look.IsUIOpen);
            if (!free || IsHealing || !input.HealPressed) return;
            if (health.Current >= health.Max) return; // full health: don't waste it

            var choice = PickBest();
            if (choice.item == null) return;
            pendingItem = choice.item;
            pendingHeal = choice.heal;
            finishTime = Time.time + useTime;
        }

        (ItemDefinition item, float heal) PickBest()
        {
            float missing = health.Max - health.Current;
            ItemDefinition best = null;
            float bestHeal = 0f;

            // Prefer the smallest item that fully covers what's missing; otherwise the biggest one we have.
            foreach (var m in Meds)
            {
                var item = Find(m.id);
                if (item == null) continue;
                bool covers = m.heal >= missing;
                bool bestCovers = bestHeal >= missing;
                if (best == null || (covers && (!bestCovers || m.heal < bestHeal)) || (!covers && !bestCovers && m.heal > bestHeal))
                {
                    best = item;
                    bestHeal = m.heal;
                }
            }
            return (best, bestHeal);
        }

        ItemDefinition Find(string id)
        {
            foreach (var s in inventory.Stacks)
                if (s.item != null && s.item.id == id && s.count > 0) return s.item;
            return null;
        }
    }
}
