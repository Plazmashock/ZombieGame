using System.Collections.Generic;
using UnityEngine;
using LastCheckout.Items;

namespace LastCheckout.Inventory
{
    /// <summary>One line in the backpack: "Wooden Plank x3".</summary>
    [System.Serializable]
    public class ItemStack
    {
        public ItemDefinition item;
        public int count;
        public float Weight => item != null ? item.weight * count : 0f;
    }

    /// <summary>
    /// The player's backpack. Limited by WEIGHT, not slots:
    /// planks and fuel are heavy, medicine and batteries are light.
    /// The heavier it is, the slower you move (see FirstPersonController).
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] float maxWeight = 20f;
        [Tooltip("Where dropped items appear. Use the player camera.")]
        [SerializeField] Transform dropPoint;
        [SerializeField] List<ItemStack> stacks = new List<ItemStack>();

        public IReadOnlyList<ItemStack> Stacks => stacks;
        public float MaxWeight => maxWeight;
        public float CurrentWeight
        {
            get { float w = 0f; foreach (var s in stacks) w += s.Weight; return w; }
        }
        /// <summary>0 = empty, 1 = completely full.</summary>
        public float LoadFraction => maxWeight > 0f ? Mathf.Clamp01(CurrentWeight / maxWeight) : 0f;

        /// <summary>Fires whenever anything is added or removed (the UI listens to this).</summary>
        public event System.Action Changed;
        /// <summary>Fires when items are added: (item, how many). Used for "+2 Canned Beans" messages.</summary>
        public event System.Action<ItemDefinition, int> ItemAdded;

        void Reset() => AutoFindReferences();
        void Awake() => AutoFindReferences();

        void AutoFindReferences()
        {
            if (dropPoint == null)
            {
                var cam = GetComponentInChildren<Camera>();
                dropPoint = cam != null ? cam.transform : transform;
            }
        }

        /// <summary>How many of this item still fit in the backpack.</summary>
        public int HowManyFit(ItemDefinition item)
        {
            if (item == null) return 0;
            if (item.weight <= 0f) return int.MaxValue;
            float free = maxWeight - CurrentWeight;
            return Mathf.Max(0, Mathf.FloorToInt(free / item.weight + 0.0001f));
        }

        public int CountOf(ItemDefinition item)
        {
            var s = Find(item);
            return s != null ? s.count : 0;
        }

        /// <summary>Adds all or nothing. Returns false if it's too heavy.</summary>
        public bool TryAdd(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0 || HowManyFit(item) < count) return false;

            var s = Find(item);
            if (s == null) stacks.Add(new ItemStack { item = item, count = count });
            else s.count += count;

            ItemAdded?.Invoke(item, count);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes items (e.g. when used for building). Returns false if you don't have enough.</summary>
        public bool Remove(ItemDefinition item, int count = 1)
        {
            var s = Find(item);
            if (s == null || s.count < count || count <= 0) return false;

            s.count -= count;
            if (s.count == 0) stacks.Remove(s);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes items out of the backpack and puts them on the ground in front of the player.</summary>
        public void Drop(ItemDefinition item, int count = 1)
        {
            if (!Remove(item, count)) return;
            Vector3 pos = dropPoint.position + dropPoint.forward * 0.7f;
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            WorldItem.Spawn(item, count, pos, rot, withPhysics: true);
        }

        ItemStack Find(ItemDefinition item)
        {
            foreach (var s in stacks) if (s.item == item) return s;
            return null;
        }
    }
}
