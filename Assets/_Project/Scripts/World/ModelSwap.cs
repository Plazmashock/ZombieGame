using System.Collections.Generic;
using UnityEngine;
using LastCheckout.Items;

namespace LastCheckout.World
{
    /// <summary>
    /// Replaces grey boxes in the level with real low-poly models (Quaternius, CC0), and adds street clutter.
    ///
    ///   LOOT ITEMS  water, beans, first aid kit, fuel can...   → the box becomes invisible, the model sits in it
    ///               (the box's collider stays, so picking up works exactly as before)
    ///   PROPS       abandoned cars, dumpster, pallet, crates, office desk & chair → same trick
    ///   DRESSING    sandbags, barrels, cones, barriers, tyres, trash bags, a wrecked car... (new, solid)
    ///
    /// Runs once when the level starts, BEFORE the zombie floor map is made, so zombies walk around the new props.
    /// A missing model just leaves that thing as it was.
    /// </summary>
    public static class ModelSwap
    {
        // ───────────────────────── Loot items ─────────────────────────

        static readonly Dictionary<string, string> ItemModels = new Dictionary<string, string>
        {
            { "water", "Items/Water" },
            { "canned_beans", "Items/CannedBeans" },
            { "energy_bar", "Items/EnergyBar" },
            { "first_aid", "Items/FirstAid" },
            { "painkillers", "Items/Painkillers" },
            { "batteries", "Items/Batteries" },
            { "flashlight", "Items/Flashlight" },
            { "fuel_can", "Items/FuelCan" },
            { "kitchen_knife", "Items/KitchenKnife" },
            { "plank", "Items/Plank" },
            { "baseball_bat", "Weapons/Bat" },
            { "pistol", "Weapons/Pistol" },
            { "revolver", "Weapons/Revolver" },
            { "smg", "Weapons/SMG" },
            { "shotgun", "Weapons/Shotgun" },
            { "rifle", "Weapons/Rifle" },
            { "knife", "Weapons/Knife" },
            { "machete", "Weapons/Machete" },
            { "axe", "Weapons/Axe" },
            { "bat", "Weapons/Bat" },
        };

        static readonly Dictionary<string, string> ByName = new Dictionary<string, string>
        {
            { "Water Bottle", "Items/Water" }, { "Canned Beans", "Items/CannedBeans" }, { "Energy Bar", "Items/EnergyBar" },
            { "First Aid Kit", "Items/FirstAid" }, { "Painkillers", "Items/Painkillers" }, { "Batteries", "Items/Batteries" },
            { "Flashlight", "Items/Flashlight" }, { "Fuel Can", "Items/FuelCan" }, { "Kitchen Knife", "Items/KitchenKnife" },
            { "Wooden Plank", "Items/Plank" }, { "Baseball Bat", "Weapons/Bat" }, { "Pistol", "Weapons/Pistol" },
        };

        /// "Canned Beans x2" / "Item: Canned Beans" / "Canned Beans(Clone)" → "Canned Beans"
        static string BaseName(string n)
        {
            n = n.Replace("(Clone)", "").Replace("Item: ", "").Trim();
            int x = n.LastIndexOf(" x");
            if (x > 0 && int.TryParse(n.Substring(x + 2), out _)) n = n.Substring(0, x);
            return n.Trim();
        }

        public static void ApplyToAllItems()
        {
            int n = 0;
            foreach (var wi in Game.LevelKit.FindAll<WorldItem>())
                if (ApplyToItem(wi)) n++;
            Debug.Log($"[Models] {n} loot items got real models.");
        }

        /// <summary>Gives one loot item its model. The invisible box keeps the collider (for pickup).</summary>
        public static bool ApplyToItem(WorldItem wi)
        {
            if (wi == null || wi.transform.Find("Skin") != null) return false;
            // Normally by the item's id; if the item link is missing, by the object's name ("Canned Beans x2").
            string path = null;
            if (wi.Item != null && !string.IsNullOrEmpty(wi.Item.id)) ItemModels.TryGetValue(wi.Item.id, out path);
            if (path == null) ByName.TryGetValue(BaseName(wi.name), out path);
            if (path == null) return false;
            var box = wi.GetComponent<MeshRenderer>();
            if (box == null) return false;

            var model = ModelLibrary.Spawn(path);
            if (model == null) return false;

            // "Skin" undoes the box's stretching, so inside it 1 unit = 1 meter again.
            var skin = new GameObject("Skin").transform;
            skin.SetParent(wi.transform, false);
            Vector3 s = wi.transform.localScale;
            skin.localScale = new Vector3(1f / Mathf.Max(0.0001f, s.x), 1f / Mathf.Max(0.0001f, s.y), 1f / Mathf.Max(0.0001f, s.z));

            Vector3 size = wi.Item != null ? wi.Item.worldSize : s; // the box is scaled to the item's size
            Quaternion turn = ModelLibrary.MatchAxes(model, skin, size);
            ModelLibrary.Fit(model, skin, new Bounds(Vector3.zero, size), ModelLibrary.FitBy.Inside, turn, autoYaw: false, bottom: true);
            ModelLibrary.SetShadows(skin.gameObject, UnityEngine.Rendering.ShadowCastingMode.Off);
            box.enabled = false;
            return true;
        }

        // ───────────────────────── Props in the level ─────────────────────────

        static Transform propsRoot;

        static Transform Root()
        {
            if (propsRoot == null) propsRoot = new GameObject("Model Props (Quaternius)").transform;
            return propsRoot;
        }

        public static void ApplyToProps()
        {
            propsRoot = null;
            int n = 0;
            int crate = 0;
            foreach (var t in Game.LevelKit.FindAll<Transform>())
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                string name = t.name, path = null;
                if (name == "Abandoned Car 1") path = "Props/NormalCar1";
                else if (name == "Abandoned Car 2") path = "Props/SUV";
                else if (name == "Dumpster") path = "Props/TrashContainer";
                else if (name == "Pallet") path = "Props/Pallet";
                else if (name == "Pallet Boxes") path = "Props/CardboardBoxes_1";
                else if (name.StartsWith("Crate ")) path = (crate++ % 3) switch { 0 => "Props/Crate", 1 => "Props/CardboardBoxes_2", _ => "Props/CardboardBoxes_3" };
                else if (name == "Office Desk") path = "Props/Desk";
                else if (name == "Office Chair") path = "Props/OfficeChair";
                else if (name == "Filing Cabinet") path = "Props/ShortCloset";
                if (path != null && ReplaceObject(t.gameObject, path)) n++;
            }
            n += AddDressing();
            Debug.Log($"[Models] {n} props placed.");
        }

        /// Hides an object's look (keeps its collider) and puts the model in its place, filling its box.
        static bool ReplaceObject(GameObject go, string path)
        {
            var col = go.GetComponent<Collider>();
            Bounds target;
            if (col != null) target = col.bounds;
            else
            {
                var r = go.GetComponent<Renderer>();
                if (r == null) return false;
                target = r.bounds;
            }
            var model = ModelLibrary.Spawn(path);
            if (model == null) return false;
            ModelLibrary.Fit(model, Root(), target, ModelLibrary.FitBy.Inside, Quaternion.identity, autoYaw: true, bottom: true);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.enabled = false;
            return true;
        }

        // Street clutter: (model, position on the ground, facing, height in meters, solid?)
        static readonly (string path, Vector3 pos, float yaw, float height, bool solid)[] Dressing =
        {
            ("Props/SackTrench", new Vector3(-8.2f, 0f, -19.8f), 0f, 1.0f, true),
            ("Props/SackTrench", new Vector3(8.2f, 0f, -19.8f), 180f, 1.0f, true),
            ("Props/ExplodingBarrel", new Vector3(-17.6f, 0f, -16.6f), 0f, 1.1f, true),
            ("Props/ExplodingBarrel", new Vector3(-18.6f, 0f, -17.1f), 40f, 1.1f, true),
            ("Props/Barrel", new Vector3(-3.6f, 0f, 16.5f), 15f, 1.0f, true),
            ("Props/Barrel", new Vector3(-4.5f, 0f, 17.1f), 70f, 1.0f, true),
            ("Props/TrafficCone", new Vector3(-1.5f, 0f, -21.6f), 0f, 0.7f, false),
            ("Props/TrafficCone", new Vector3(1.9f, 0f, -22.5f), 30f, 0.7f, false),
            ("Props/TrafficCone", new Vector3(-4.2f, 0f, -23.2f), 60f, 0.7f, false),
            ("Props/Barrier_Single", new Vector3(-12.5f, 0f, -30f), 20f, 1.0f, true),
            ("Props/Barrier_Single", new Vector3(14f, 0f, -33f), -15f, 1.0f, true),
            ("Props/TrafficBarrier_1", new Vector3(-27f, 0f, -30f), 75f, 1.0f, true),
            ("Props/TrashBag_1", new Vector3(0.5f, 0f, 16.9f), 0f, 0.55f, false),
            ("Props/TrashBag_2", new Vector3(4.3f, 0f, 17.0f), 50f, 0.5f, false),
            ("Props/TrashBag_1", new Vector3(4.6f, 0f, 18.3f), 120f, 0.5f, false),
            ("Props/Debris_Tires", new Vector3(-26f, 0f, -18f), 30f, 0.9f, true),
            ("Props/Debris_BrokenCar", new Vector3(24f, 0f, -30f), 30f, 1.4f, true),
            ("Props/Wheels_Stack", new Vector3(-23f, 0f, 7.6f), 0f, 1.0f, true),
            ("Props/FireHydrant", new Vector3(-21.2f, 0f, -16.6f), 0f, 0.8f, true),
            ("Props/Trashcan", new Vector3(-5.9f, 0.12f, -15.8f), 0f, 0.95f, true),
            ("Props/Trashcan", new Vector3(5.9f, 0.12f, -15.8f), 0f, 0.95f, true),
            ("Props/Debris_Pile", new Vector3(30f, 0f, 10f), 0f, 1.2f, true),
            ("Props/Bookcase_Books", new Vector3(19.5f, 0.02f, 11.0f), -90f, 2.0f, true),
        };

        static int AddDressing()
        {
            int n = 0;
            foreach (var d in Dressing)
            {
                var model = ModelLibrary.Spawn(d.path);
                if (model == null) continue;
                var target = new Bounds(d.pos + Vector3.up * (d.height * 0.5f), new Vector3(50f, d.height, 50f));
                var pivot = ModelLibrary.Fit(model, Root(), target, ModelLibrary.FitBy.Height, Quaternion.Euler(0f, d.yaw, 0f), autoYaw: false, bottom: true);
                if (d.solid) ModelLibrary.AddBoxCollider(pivot.gameObject);
                n++;
            }
            return n;
        }
    }
}
