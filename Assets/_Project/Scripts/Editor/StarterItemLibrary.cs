using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LastCheckout.Items;

namespace LastCheckout.EditorTools
{
    /// <summary>
    /// EDITOR TOOL. Creates the starter item data files in Assets/_Project/Data/Items.
    /// Menu: Last Checkout > Create Starter Items  (the scene builder also runs this automatically).
    ///
    /// Safe to re-run: items that already exist are NOT overwritten,
    /// so any weights or text you change in the Inspector are kept.
    /// </summary>
    public static class StarterItemLibrary
    {
        public const string ItemsFolder = "Assets/_Project/Data/Items";

        struct Spec
        {
            public string id, name, description, usage;
            public ItemCategory category;
            public float weight;
            public Vector3 size;
        }

        static readonly Spec[] Specs =
        {
            // FOOD & DRINK — keep you going between waves
            new Spec { id = "canned_beans", name = "Canned Beans", category = ItemCategory.Food, weight = 0.5f, size = new Vector3(0.1f, 0.12f, 0.1f),
                description = "Doesn't go bad. Tastes like it already did.", usage = "Eat between waves to restore health." },
            new Spec { id = "water", name = "Water Bottle", category = ItemCategory.Drink, weight = 1.0f, size = new Vector3(0.08f, 0.25f, 0.08f),
                description = "1 liter of clean water.", usage = "Drink to restore stamina." },
            new Spec { id = "energy_bar", name = "Energy Bar", category = ItemCategory.Food, weight = 0.1f, size = new Vector3(0.12f, 0.03f, 0.05f),
                description = "Light and quick to eat.", usage = "Small stamina boost. Can be eaten mid-fight." },

            // MEDICAL — treat injuries
            new Spec { id = "painkillers", name = "Painkillers", category = ItemCategory.Medical, weight = 0.1f, size = new Vector3(0.06f, 0.1f, 0.06f),
                description = "A small bottle of pills.", usage = "Heals a little, instantly." },
            new Spec { id = "first_aid", name = "First Aid Kit", category = ItemCategory.Medical, weight = 1.0f, size = new Vector3(0.3f, 0.12f, 0.2f),
                description = "Bandages, antiseptic, the works.", usage = "Heals a lot, but takes a few seconds to use." },

            // TOOLS & MATERIALS — barricading
            new Spec { id = "hammer", name = "Hammer", category = ItemCategory.Tool, weight = 1.0f, size = new Vector3(0.3f, 0.05f, 0.1f),
                description = "A sturdy claw hammer.", usage = "Required to nail planks over windows and doors. Weak weapon in a pinch." },
            new Spec { id = "nails", name = "Box of Nails", category = ItemCategory.Material, weight = 1.0f, size = new Vector3(0.12f, 0.08f, 0.08f),
                description = "Enough nails for a few barricades.", usage = "Used with a hammer to build and reinforce barricades." },
            new Spec { id = "plank", name = "Wooden Plank", category = ItemCategory.Material, weight = 3.0f, size = new Vector3(1.0f, 0.03f, 0.15f),
                description = "Heavy, but zombies hate it.", usage = "Barricade material for windows and doors." },
            new Spec { id = "duct_tape", name = "Duct Tape", category = ItemCategory.Material, weight = 0.3f, size = new Vector3(0.1f, 0.05f, 0.1f),
                description = "Fixes everything. Almost.", usage = "Quick repairs on damaged barricades; crafting ingredient." },

            // ELECTRONICS & FUEL — power
            new Spec { id = "batteries", name = "Batteries", category = ItemCategory.Electronics, weight = 0.2f, size = new Vector3(0.08f, 0.04f, 0.06f),
                description = "A pack of AA batteries.", usage = "Powers flashlights and electronic traps." },
            new Spec { id = "flashlight", name = "Flashlight", category = ItemCategory.Electronics, weight = 0.4f, size = new Vector3(0.05f, 0.05f, 0.2f),
                description = "Bright, if it has batteries.", usage = "See in the dark when the power fails. Needs batteries." },
            new Spec { id = "fuel_can", name = "Fuel Can", category = ItemCategory.Fuel, weight = 5.0f, size = new Vector3(0.3f, 0.35f, 0.15f),
                description = "Five liters of gasoline. Very heavy.", usage = "Powers the generator that keeps the lights on." },

            // WEAPONS — fighting back
            new Spec { id = "kitchen_knife", name = "Kitchen Knife", category = ItemCategory.Weapon, weight = 0.3f, size = new Vector3(0.03f, 0.02f, 0.25f),
                description = "Sharp, short, fast.", usage = "Fast melee weapon with short reach." },
            new Spec { id = "baseball_bat", name = "Baseball Bat", category = ItemCategory.Weapon, weight = 1.2f, size = new Vector3(0.07f, 0.07f, 0.85f),
                description = "Aluminum. Still has the price tag.", usage = "Strong melee weapon with good reach." },
        };

        public static Color CategoryColor(ItemCategory c) => c switch
        {
            ItemCategory.Food => new Color(0.95f, 0.6f, 0.2f),
            ItemCategory.Drink => new Color(0.45f, 0.75f, 0.95f),
            ItemCategory.Medical => new Color(0.95f, 0.25f, 0.3f),
            ItemCategory.Tool => new Color(0.95f, 0.85f, 0.2f),
            ItemCategory.Weapon => new Color(0.55f, 0.55f, 0.6f),
            ItemCategory.Material => new Color(0.75f, 0.55f, 0.3f),
            ItemCategory.Electronics => new Color(0.2f, 0.85f, 0.75f),
            ItemCategory.Fuel => new Color(0.85f, 0.1f, 0.1f),
            _ => new Color(0.8f, 0.8f, 0.8f),
        };

        [MenuItem("Last Checkout/Create Starter Items")]
        public static void CreateFromMenu()
        {
            var items = CreateOrLoadAll();
            Debug.Log($"[Last Checkout] {items.Count} starter items ready in {ItemsFolder}");
        }

        /// <summary>Returns every starter item, keyed by id. Creates the ones that are missing.</summary>
        public static Dictionary<string, ItemDefinition> CreateOrLoadAll()
        {
            GreyboxSupermarketBuilder.EnsureFolder(ItemsFolder);
            var result = new Dictionary<string, ItemDefinition>();

            foreach (var spec in Specs)
            {
                string path = $"{ItemsFolder}/{spec.id}.asset";
                var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<ItemDefinition>();
                    def.id = spec.id;
                    def.displayName = spec.name;
                    def.category = spec.category;
                    def.description = spec.description;
                    def.usage = spec.usage;
                    def.weight = spec.weight;
                    def.worldSize = spec.size;
                    def.placeholderColor = CategoryColor(spec.category);
                    AssetDatabase.CreateAsset(def, path);
                }
                result[spec.id] = def;
            }

            AssetDatabase.SaveAssets();
            return result;
        }
    }
}
