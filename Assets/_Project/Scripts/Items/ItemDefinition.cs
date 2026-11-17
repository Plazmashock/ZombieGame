using UnityEngine;

namespace LastCheckout.Items
{
    public enum ItemCategory { Food, Drink, Medical, Tool, Weapon, Material, Electronics, Fuel, Misc }

    /// <summary>
    /// The "recipe card" for one kind of item (e.g. Wooden Plank).
    /// It's a ScriptableObject = a data file in your Project window, not something in the scene.
    /// Every plank in the world points to the SAME Wooden Plank card, so changing its weight here changes it everywhere.
    ///
    /// Create new ones with: right-click in Project window > Create > Last Checkout > Item
    /// </summary>
    [CreateAssetMenu(menuName = "Last Checkout/Item", fileName = "New Item")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique short code, e.g. 'plank'. Used by save files and recipes later.")]
        public string id;
        public string displayName = "New Item";
        public ItemCategory category = ItemCategory.Misc;

        [Header("Description")]
        [TextArea(2, 4)] public string description;
        [Tooltip("What the player can DO with it. Shown in the backpack so every item has a clear purpose.")]
        [TextArea(2, 4)] public string usage;

        [Header("Carrying")]
        [Tooltip("Kilograms. The backpack has a weight limit, so this is the main looting trade-off.")]
        [Min(0f)] public float weight = 0.5f;

        [Header("Placeholder look (until real models exist)")]
        [Tooltip("Size of the placeholder box in meters.")]
        public Vector3 worldSize = new Vector3(0.2f, 0.2f, 0.2f);
        public Color placeholderColor = Color.white;
    }
}
