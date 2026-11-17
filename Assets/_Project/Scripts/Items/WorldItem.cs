using UnityEngine;
using LastCheckout.Interaction;
using LastCheckout.Inventory;

namespace LastCheckout.Items
{
    /// <summary>
    /// An item lying in the world (on a shelf, on the floor) that you can pick up with E.
    /// If only some of a pile fits in your backpack, you take what fits and the rest stays.
    /// </summary>
    public class WorldItem : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemDefinition item;
        [SerializeField, Min(1)] int count = 1;

        public ItemDefinition Item => item;
        public int Count => count;

        const float MinColliderSize = 0.15f; // tiny items get a bigger "hitbox" so they're easy to aim at

        public void Setup(ItemDefinition definition, int amount)
        {
            item = definition;
            count = Mathf.Max(1, amount);
        }

        // ---------- IInteractable ----------

        public string GetPrompt(PlayerInteractor interactor)
        {
            if (item == null) return "???";
            string label = count > 1 ? $"{item.displayName} x{count}" : item.displayName;
            var inv = interactor.GetComponent<PlayerInventory>();
            if (inv != null && inv.HowManyFit(item) == 0)
                return $"{label}  —  too heavy ({item.weight:0.#} kg each)";
            return $"Take {label}  ({item.weight * count:0.#} kg)";
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            var inv = interactor.GetComponent<PlayerInventory>();
            return item != null && inv != null && inv.HowManyFit(item) >= 1;
        }

        public void Interact(PlayerInteractor interactor)
        {
            var inv = interactor.GetComponent<PlayerInventory>();
            if (inv == null) return;

            int amount = Mathf.Min(count, inv.HowManyFit(item));
            if (amount <= 0 || !inv.TryAdd(item, amount)) return;

            count -= amount;
            if (count <= 0) Destroy(gameObject);
        }

        // ---------- Helpers shared by the game and the editor scene builder ----------

        /// <summary>Creates a placeholder box for an item (used when dropping items).</summary>
        public static WorldItem Spawn(ItemDefinition definition, int amount, Vector3 position, Quaternion rotation, bool withPhysics)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Item: {definition.displayName}";
            go.transform.SetPositionAndRotation(position, rotation);
            ApplyPlaceholderShape(go, definition);
            ApplyColor(go.GetComponent<Renderer>(), definition.placeholderColor);

            if (withPhysics)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = Mathf.Max(0.1f, definition.weight);
            }

            var wi = go.AddComponent<WorldItem>();
            wi.Setup(definition, amount);
            World.ModelSwap.ApplyToItem(wi); // real 3D model instead of the box, if there is one
            return wi;
        }

        /// <summary>Scales the box to the item's size and makes small items easier to aim at.</summary>
        public static void ApplyPlaceholderShape(GameObject go, ItemDefinition definition)
        {
            Vector3 s = definition.worldSize;
            go.transform.localScale = s;
            var box = go.GetComponent<BoxCollider>();
            if (box != null)
                box.size = new Vector3(Mathf.Max(s.x, MinColliderSize) / s.x,
                                       Mathf.Max(s.y, MinColliderSize) / s.y,
                                       Mathf.Max(s.z, MinColliderSize) / s.z);
        }

        /// <summary>Tints a renderer without creating a new material each time.</summary>
        public static void ApplyColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color); // URP
            block.SetColor("_Color", color);     // built-in fallback
            renderer.SetPropertyBlock(block);
        }
    }
}
