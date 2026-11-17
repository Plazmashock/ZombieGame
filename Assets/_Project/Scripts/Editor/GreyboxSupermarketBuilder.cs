using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using LastCheckout.Player;
using LastCheckout.Interaction;
using LastCheckout.Inventory;
using LastCheckout.Items;
using LastCheckout.UI;
using LastCheckout.World;

namespace LastCheckout.EditorTools
{
    /// <summary>
    /// EDITOR TOOL (not part of the game itself).
    /// Menu:  Last Checkout > Build Greybox Supermarket Scene
    /// Builds the whole placeholder supermarket + a ready-to-play Player in one click,
    /// and saves it as Assets/_Project/Scenes/Supermarket_Greybox.unity.
    /// Safe to run again: it rebuilds the scene from scratch (asks first).
    ///
    /// Floor plan (top view, +Z is "back of the store"):
    ///
    ///   z=15 ┌──────────────── back wall ──[loading bay]────────┐
    ///        │  STORAGE ROOM              │ back   │  OFFICE      │
    ///   z=9  ├────────[door]──────────────┘corridor└──[door]─────┤
    ///        │ fridges                                           │
    ///        │   ║  ║  ║  ║      (center aisle)    ║  ║  ║  ║     │  ← windows (right)
    ///  side  │   ║  ║  ║  ║                        ║  ║  ║  ║     │
    ///  door →│                                                   │
    ///        │   ▭  ▭  ▭          checkouts         ▭  ▭  ▭       │
    ///   z=-15└──[ windows ]────[ main entrance ]────[ windows ]──┘
    ///      x=-20                     x=0                       x=20
    /// </summary>
    public static class GreyboxSupermarketBuilder
    {
        const string ScenesFolder = "Assets/_Project/Scenes";
        const string ScenePath = ScenesFolder + "/Supermarket_Greybox.unity";
        const string MaterialFolder = "Assets/_Project/Materials/Greybox";

        const float WallHeight = 5f;
        const float WallThickness = 0.3f;
        const float FloorTop = 0.02f;
        const float ShelfLip = 0.3f;                              // how far shelf boards stick out (items sit here)
        static readonly float[] BoardHeights = { 0.45f, 1.05f, 1.65f }; // shelf board heights above the floor

        // Materials (filled in Build)
        static Material matGround, matFloor, matWall, matCeiling, matShelf, matShelfBoard, matCounter,
                        matFridge, matDoor, matDark, matCrate, matDesk, matFixture, matCar, matSwitch;

        [MenuItem("Last Checkout/Build Greybox Supermarket Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Rebuild supermarket?",
                    "Supermarket_Greybox already exists. Rebuild it from scratch?\n(Any manual changes in that scene will be lost.)",
                    "Rebuild", "Cancel"))
                return;

            EnsureFolder(ScenesFolder);
            EnsureFolder(MaterialFolder);
            CreateMaterials();
            var items = StarterItemLibrary.CreateOrLoadAll();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupAtmosphere();

            var env = new GameObject("Environment").transform;
            var structure = Group("Structure", env);
            var doors = Group("Doors", env);
            var shelving = Group("Shelving", env);
            var fixtures = Group("Fixtures", env);
            var props = Group("Props", env);
            var lighting = Group("Lighting", env);
            var loot = Group("Loot", env);

            BuildStructure(structure);
            BuildDoors(doors);
            BuildShelving(shelving);
            BuildFixtures(fixtures);
            BuildProps(props);
            BuildLighting(lighting, fixtures);
            BuildLoot(loot, items);

            var player = BuildPlayer(new Vector3(0f, FloorTop, -12f), 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = player;
            Debug.Log("[Last Checkout] Greybox supermarket built and saved to " + ScenePath + ". Press Play to test!");
        }

        // ───────────────────────────── Layout ─────────────────────────────

        static void BuildStructure(Transform p)
        {
            // Outside ground (parking lot) and store floor/ceiling
            Box("Ground (Parking Lot)", p, new Vector3(0f, -0.25f, -10f), new Vector3(120f, 0.5f, 110f), matGround);
            Box("Store Floor", p, new Vector3(0f, FloorTop - 0.06f, 0f), new Vector3(40f, 0.12f, 30f), matFloor);
            Box("Ceiling", p, new Vector3(0f, WallHeight + 0.15f, 0f), new Vector3(40.6f, 0.3f, 30.6f), matCeiling);

            // Outer walls  (Opening = start, end, bottom, top)
            WallX(p, "Front Wall", z: -15f, from: -20.15f, to: 20.15f,
                new Opening(-17f, -5f, 1f, 3.5f),   // left shop window
                new Opening(-3f, 3f, 0f, 3f),       // main entrance
                new Opening(5f, 17f, 1f, 3.5f));    // right shop window
            WallX(p, "Back Wall", z: 15f, from: -20.15f, to: 20.15f,
                new Opening(5f, 9f, 0f, 3.5f));     // loading bay
            WallZ(p, "Left Wall", x: -20f, from: -15f, to: 15f,
                new Opening(-5f, -3.5f, 0f, 2.5f)); // side emergency exit
            WallZ(p, "Right Wall", x: 20f, from: -15f, to: 15f,
                new Opening(-10f, -2f, 1f, 3f));    // side window

            // Interior walls: storage room (left back), office (right back), corridor between
            WallX(p, "Storage Wall", z: 9f, from: -20f, to: 2.15f, new Opening(-10f, -8.5f, 0f, 2.5f));
            WallX(p, "Office Wall", z: 9f, from: 11.85f, to: 20f, new Opening(15f, 16.5f, 0f, 2.5f));
            WallZ(p, "Corridor Wall L", x: 2f, from: 9f, to: 15f);
            WallZ(p, "Corridor Wall R", x: 12f, from: 9f, to: 15f);
        }

        static void BuildDoors(Transform p)
        {
            BuildDoor(p, "Storage Room Door", new Vector3(-10f, FloorTop, 9f), 0f, "Storage Door", false);
            BuildDoor(p, "Office Door", new Vector3(15f, FloorTop, 9f), 0f, "Office Door", false);
            BuildDoor(p, "Emergency Exit Door", new Vector3(-20f, FloorTop, -5f), -90f, "Emergency Exit", true);
        }

        static void BuildShelving(Transform p)
        {
            // 8 aisle shelves, leaving a wide center aisle
            float[] aisleX = { -16f, -12f, -8f, -4f, 4f, 8f, 12f, 16f };
            for (int i = 0; i < aisleX.Length; i++)
                ShelfUnit(p, $"Aisle Shelf {i + 1}", new Vector3(aisleX[i], 0f, 0.5f), new Vector3(1f, 2.2f, 11f), alongZ: true);

            // Wall shelves (gaps left for the side door and the right window)
            ShelfUnit(p, "Wall Shelf L1", new Vector3(-19.45f, 0f, -9f), new Vector3(0.8f, 2.4f, 6f), alongZ: true);
            ShelfUnit(p, "Wall Shelf L2", new Vector3(-19.45f, 0f, 2.25f), new Vector3(0.8f, 2.4f, 9.5f), alongZ: true);
            ShelfUnit(p, "Wall Shelf R1", new Vector3(19.45f, 0f, 3f), new Vector3(0.8f, 2.4f, 8f), alongZ: true);

            // Storage room pallet rack
            ShelfUnit(p, "Pallet Rack", new Vector3(-11f, 0f, 14.35f), new Vector3(14f, 3f, 1f), alongZ: false);
        }

        static void BuildFixtures(Transform p)
        {
            Box("Fridges", p, new Vector3(-16f, FloorTop + 1.1f, 8.4f), new Vector3(6f, 2.2f, 0.9f), matFridge);

            float[] checkoutX = { -15f, -11f, -7f, 7f, 11f, 15f };
            for (int i = 0; i < checkoutX.Length; i++)
            {
                var counter = Box($"Checkout {i + 1}", p, new Vector3(checkoutX[i], FloorTop + 0.5f, -9.5f), new Vector3(0.8f, 1f, 3f), matCounter);
                Box("Register", counter.transform, new Vector3(checkoutX[i], FloorTop + 1.15f, -8.5f), new Vector3(0.4f, 0.3f, 0.4f), matDark);
            }

            // Office furniture
            Box("Office Desk", p, new Vector3(17.5f, FloorTop + 0.4f, 13f), new Vector3(2.2f, 0.8f, 1f), matDesk);
            Box("Office Chair", p, new Vector3(17.5f, FloorTop + 0.25f, 12f), new Vector3(0.5f, 0.5f, 0.5f), matDark);
            Box("Filing Cabinet", p, new Vector3(19.4f, FloorTop + 0.7f, 10.5f), new Vector3(0.6f, 1.4f, 0.8f), matDark);
        }

        static void BuildProps(Transform p)
        {
            // Crate stacks in storage: (x, z, how many stacked)
            var stacks = new (float x, float z, int count)[] { (-17.5f, 11f, 2), (-16f, 11.4f, 1), (-6f, 11f, 3), (-4.5f, 11.6f, 1), (0f, 12.5f, 2) };
            int n = 0;
            foreach (var s in stacks)
                for (int k = 0; k < s.count; k++)
                    Box($"Crate {++n}", p, new Vector3(s.x, FloorTop + 0.55f + k * 1.1f, s.z), new Vector3(1.1f, 1.1f, 1.1f), matCrate);

            // Outside
            Box("Dumpster", p, new Vector3(7f, 0.75f, 17.5f), new Vector3(3f, 1.5f, 1.5f), matDark);
            Box("Abandoned Car 1", p, new Vector3(-8f, 0.75f, -24f), new Vector3(2f, 1.5f, 4.5f), matCar);
            Box("Abandoned Car 2", p, new Vector3(6f, 0.75f, -27f), new Vector3(2f, 1.5f, 4.5f), matCar);
        }

        static void BuildLighting(Transform p, Transform fixtures)
        {
            // Store lights: 6 in the main hall, 2 storage, 1 corridor, 1 office
            foreach (float x in new[] { -12f, 0f, 12f })
                foreach (float z in new[] { -8f, 3f })
                    CeilingLight(p, $"Hall Light ({x},{z})", new Vector3(x, 4.6f, z), 15f);
            CeilingLight(p, "Storage Light 1", new Vector3(-14f, 4.6f, 12f), 10f);
            CeilingLight(p, "Storage Light 2", new Vector3(-4f, 4.6f, 12f), 10f);
            CeilingLight(p, "Corridor Light", new Vector3(7f, 4.6f, 12f), 8f);
            var officeLight = CeilingLight(p, "Office Light", new Vector3(16f, 4.6f, 12f), 10f);

            // Light switch inside the office, next to the door
            var sw = Box("Office Light Switch", fixtures, new Vector3(17.2f, 1.3f, 9.18f), new Vector3(0.15f, 0.22f, 0.06f), matSwitch, isStatic: false);
            var lightSwitch = sw.AddComponent<LightSwitch>();
            SetArray(lightSwitch, "lights", new Object[] { officeLight });
        }

        static void SetupAtmosphere()
        {
            var sunGO = new GameObject("Directional Light (Dusk)");
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.55f, 0.62f, 0.8f);
            sun.intensity = 0.6f;
            sun.shadows = LightShadows.Soft;
            sunGO.transform.rotation = Quaternion.Euler(30f, -35f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.2f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.11f);
            RenderSettings.fogDensity = 0.015f;
        }

        // ───────────────────────────── Loot ─────────────────────────────

        /// Hand-placed loot. Each area has a theme so players learn WHERE to look:
        /// food aisles, pharmacy, hardware, heavy stuff in storage, electronics in the office.
        static void BuildLoot(Transform p, Dictionary<string, ItemDefinition> it)
        {
            // Aisle shelves: (shelf x, side -1/+1, board level 0-2, z position)
            void OnShelf(string id, float x, int side, int level, float z, int count = 1) =>
                PlaceItem(p, it[id], new Vector3(x + side * (0.5f + ShelfLip * 0.5f), FloorTop + BoardHeights[level] + 0.025f, z), count);
            void OnFloor(string id, float x, float z, int count = 1, float yaw = 0f) =>
                PlaceItem(p, it[id], new Vector3(x, FloorTop, z), count, yaw);
            void OnTop(string id, float x, float topY, float z, int count = 1) =>
                PlaceItem(p, it[id], new Vector3(x, topY, z), count);

            // Aisle 1-2: food & drinks
            OnShelf("canned_beans", -16f, -1, 0, -3f, 2);
            OnShelf("canned_beans", -16f, 1, 1, 0f);
            OnShelf("water", -16f, -1, 1, 2f);
            OnShelf("energy_bar", -16f, 1, 2, -2f);
            OnShelf("water", -12f, -1, 0, -4f);
            OnShelf("water", -12f, 1, 1, 1f);
            OnShelf("canned_beans", -12f, -1, 2, 3f);
            OnShelf("energy_bar", -12f, 1, 0, 5f);
            // Aisle 3: pharmacy
            OnShelf("painkillers", -8f, -1, 1, -2f);
            OnShelf("painkillers", -8f, 1, 1, 2f);
            OnShelf("first_aid", -8f, -1, 0, 4f);
            // Aisle 4: household
            OnShelf("duct_tape", -4f, 1, 1, -3f);
            OnShelf("batteries", -4f, -1, 1, 0f);
            OnShelf("batteries", -4f, 1, 0, 3f);
            OnShelf("flashlight", -4f, -1, 2, 4f);
            // Aisle 5: kitchen
            OnShelf("kitchen_knife", 4f, -1, 1, -1f);
            OnShelf("canned_beans", 4f, 1, 0, 2f);
            // Aisle 6: sports
            OnShelf("baseball_bat", 8f, 1, 0, 0f);
            OnShelf("water", 8f, -1, 1, 3f);
            // Aisle 7: hardware
            OnShelf("nails", 12f, -1, 1, -2f);
            OnShelf("nails", 12f, 1, 0, 1f);
            OnShelf("hammer", 12f, 1, 1, 4f);
            OnShelf("duct_tape", 12f, -1, 0, 4f);
            // Aisle 8: snacks
            OnShelf("energy_bar", 16f, -1, 1, -3f, 2);
            OnShelf("energy_bar", 16f, 1, 1, 2f);
            OnShelf("canned_beans", 16f, -1, 0, 1f);

            // Checkouts: impulse buys
            OnTop("energy_bar", -15f, FloorTop + 1f, -10.4f);
            OnTop("painkillers", 11f, FloorTop + 1f, -10.4f);

            // Storage room: the heavy, valuable building stuff
            OnFloor("plank", -14f, 12.6f, 3);
            OnFloor("plank", -8f, 12.4f, 2);
            OnFloor("plank", -2f, 13f, 2, 20f);
            OnFloor("fuel_can", -18.5f, 13f);
            OnFloor("nails", -12f, 11f);
            OnTop("hammer", -16f, FloorTop + 1.1f, 11.4f); // on top of a crate

            // Office: electronics & the good medkit
            OnTop("flashlight", 17f, FloorTop + 0.8f, 13f);
            OnTop("batteries", 18.2f, FloorTop + 0.8f, 13f);
            OnTop("first_aid", 19.4f, FloorTop + 1.4f, 10.5f);

            // Back corridor by the loading bay
            OnFloor("fuel_can", 10.5f, 14f);
        }

        /// basePosition = the point the item's bottom rests on.
        static void PlaceItem(Transform parent, ItemDefinition def, Vector3 basePosition, int count = 1, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = count > 1 ? $"{def.displayName} x{count}" : def.displayName;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(basePosition + Vector3.up * (def.worldSize.y * 0.5f), Quaternion.Euler(0f, yaw, 0f));
            WorldItem.ApplyPlaceholderShape(go, def);
            go.GetComponent<Renderer>().sharedMaterial = Mat($"Item_{def.category}", def.placeholderColor, 0.3f);

            var wi = go.AddComponent<WorldItem>();
            SetRef(wi, "item", def);
            SetInt(wi, "count", count);
        }

        // ───────────────────────────── Player ─────────────────────────────

        static GameObject BuildPlayer(Vector3 position, float yaw)
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;

            var cameraRoot = new GameObject("CameraRoot").transform;
            cameraRoot.SetParent(player.transform, false);
            cameraRoot.localPosition = new Vector3(0f, 1.6f, 0f);

            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(cameraRoot, false);
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            camGO.AddComponent<AudioListener>();

            var input = player.AddComponent<PlayerInputReader>();

            var inventory = player.AddComponent<PlayerInventory>();
            SetRef(inventory, "dropPoint", camGO.transform);

            var motor = player.AddComponent<FirstPersonController>();
            SetRef(motor, "input", input);
            SetRef(motor, "cameraRoot", cameraRoot);
            SetRef(motor, "inventory", inventory);

            var look = player.AddComponent<PlayerLook>();
            SetRef(look, "input", input);
            SetRef(look, "cameraRoot", cameraRoot);

            var interactor = player.AddComponent<PlayerInteractor>();
            SetRef(interactor, "input", input);
            SetRef(interactor, "viewPoint", camGO.transform);

            var hud = player.AddComponent<InteractionHUD>();
            SetRef(hud, "interactor", interactor);
            SetRef(hud, "look", look);

            var invHud = player.AddComponent<InventoryHUD>();
            SetRef(invHud, "inventory", inventory);
            SetRef(invHud, "input", input);
            SetRef(invHud, "look", look);

            return player;
        }

        // ───────────────────────────── Building blocks ─────────────────────────────

        struct Opening
        {
            public float start, end, bottom, top;
            public Opening(float start, float end, float bottom, float top)
            { this.start = start; this.end = end; this.bottom = bottom; this.top = top; }
        }

        static void WallX(Transform p, string name, float z, float from, float to, params Opening[] openings)
            => Wall(p, name, true, z, from, to, openings);

        static void WallZ(Transform p, string name, float x, float from, float to, params Opening[] openings)
            => Wall(p, name, false, x, from, to, openings);

        /// Builds a straight wall out of box pieces, leaving holes for doors/windows.
        static void Wall(Transform parent, string name, bool alongX, float fixedCoord, float from, float to, Opening[] openings)
        {
            var group = Group(name, parent);
            var list = new List<Opening>(openings);
            list.Sort((a, b) => a.start.CompareTo(b.start));

            float cursor = from;
            int i = 0;
            foreach (var o in list)
            {
                WallPiece(group, $"{name} {++i}", alongX, fixedCoord, cursor, o.start, 0f, WallHeight);
                if (o.bottom > 0f) WallPiece(group, $"{name} {++i} (below opening)", alongX, fixedCoord, o.start, o.end, 0f, o.bottom);
                if (o.top < WallHeight) WallPiece(group, $"{name} {++i} (above opening)", alongX, fixedCoord, o.start, o.end, o.top, WallHeight);
                cursor = o.end;
            }
            WallPiece(group, $"{name} {++i}", alongX, fixedCoord, cursor, to, 0f, WallHeight);
        }

        static void WallPiece(Transform parent, string name, bool alongX, float fixedCoord, float a, float b, float yBottom, float yTop)
        {
            float length = b - a, height = yTop - yBottom;
            if (length <= 0.01f || height <= 0.01f) return;
            float mid = (a + b) * 0.5f, yMid = (yBottom + yTop) * 0.5f;
            Vector3 center = alongX ? new Vector3(mid, yMid, fixedCoord) : new Vector3(fixedCoord, yMid, mid);
            Vector3 size = alongX ? new Vector3(length, height, WallThickness) : new Vector3(WallThickness, height, length);
            Box(name, parent, center, size, matWall);
        }

        /// A shelf: solid body + thin boards sticking out on both sides so it reads as "shelving".
        static void ShelfUnit(Transform parent, string name, Vector3 footCenter, Vector3 size, bool alongZ)
        {
            var body = Box(name, parent, footCenter + Vector3.up * (FloorTop + size.y * 0.5f), size, matShelf);
            Vector3 boardSize = alongZ ? new Vector3(size.x + ShelfLip * 2f, 0.05f, size.z) : new Vector3(size.x, 0.05f, size.z + ShelfLip * 2f);
            for (float y = 0.45f; y < size.y - 0.2f; y += 0.6f)
            {
                var board = Box("Board", body.transform, footCenter + Vector3.up * (FloorTop + y), boardSize, matShelfBoard);
                Object.DestroyImmediate(board.GetComponent<Collider>()); // visual only
            }
        }

        static Door BuildDoor(Transform parent, string objectName, Vector3 hingePos, float yaw, string displayName, bool locked)
        {
            const float width = 1.5f, height = 2.4f;

            var pivot = new GameObject(objectName);
            pivot.transform.SetParent(parent, false);
            pivot.transform.SetPositionAndRotation(hingePos, Quaternion.Euler(0f, yaw, 0f));

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(pivot.transform, false);
            panel.transform.localPosition = new Vector3(width * 0.5f, height * 0.5f, 0f);
            panel.transform.localScale = new Vector3(width - 0.04f, height, 0.08f);
            panel.GetComponent<Renderer>().sharedMaterial = matDoor;

            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            handle.transform.SetParent(pivot.transform, false);
            handle.transform.localPosition = new Vector3(width - 0.18f, 1.0f, 0f);
            handle.transform.localScale = new Vector3(0.06f, 0.06f, 0.2f);
            handle.GetComponent<Renderer>().sharedMaterial = matDark;
            Object.DestroyImmediate(handle.GetComponent<Collider>());

            var door = pivot.AddComponent<Door>();
            SetString(door, "displayName", displayName);
            SetBool(door, "isLocked", locked);
            return door;
        }

        static Light CeilingLight(Transform parent, string name, Vector3 pos, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.range = 14f;
            light.intensity = intensity;
            light.shadows = LightShadows.None; // cheap; we'll add shadows selectively later

            var fixture = Box("Fixture", go.transform, pos + Vector3.up * 0.3f, new Vector3(1.6f, 0.08f, 0.35f), matFixture);
            Object.DestroyImmediate(fixture.GetComponent<Collider>());
            return light;
        }

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, bool isStatic = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            // children of other boxes: undo parent scale so 'size' stays in meters
            Vector3 ps = parent != null ? parent.lossyScale : Vector3.one;
            go.transform.localScale = new Vector3(size.x / ps.x, size.y / ps.y, size.z / ps.z);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        // ───────────────────────────── Materials ─────────────────────────────

        static void CreateMaterials()
        {
            matGround     = Mat("Ground_Asphalt",  new Color(0.13f, 0.13f, 0.14f));
            matFloor      = Mat("Floor_Tiles",     new Color(0.62f, 0.62f, 0.58f), 0.45f);
            matWall       = Mat("Wall",            new Color(0.72f, 0.70f, 0.64f));
            matCeiling    = Mat("Ceiling",         new Color(0.55f, 0.55f, 0.55f));
            matShelf      = Mat("Shelf",           new Color(0.30f, 0.36f, 0.42f), 0.3f);
            matShelfBoard = Mat("Shelf_Board",     new Color(0.75f, 0.75f, 0.75f), 0.4f);
            matCounter    = Mat("Checkout",        new Color(0.55f, 0.15f, 0.12f));
            matFridge     = Mat("Fridge",          new Color(0.80f, 0.85f, 0.88f), 0.6f);
            matDoor       = Mat("Door",            new Color(0.45f, 0.32f, 0.20f));
            matDark       = Mat("Dark_Metal",      new Color(0.10f, 0.10f, 0.11f), 0.4f);
            matCrate      = Mat("Crate",           new Color(0.55f, 0.42f, 0.25f));
            matDesk       = Mat("Desk",            new Color(0.35f, 0.25f, 0.18f));
            matCar        = Mat("Car",             new Color(0.25f, 0.30f, 0.38f), 0.5f);
            matSwitch     = Mat("Switch",          new Color(0.95f, 0.85f, 0.2f));
            matFixture    = Mat("Light_Fixture",   Color.white, 0.2f, emission: new Color(1f, 0.96f, 0.88f) * 1.5f);
        }

        static Material Mat(string name, Color color, float smoothness = 0.15f, Color? emission = null)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard"); // fallback if URP isn't installed
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            if (emission.HasValue && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ───────────────────────────── Helpers ─────────────────────────────

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // These fill in private [SerializeField] fields, exactly like dragging things into the Inspector.
        static SerializedProperty Prop(SerializedObject so, string field)
        {
            var prop = so.FindProperty(field);
            if (prop == null) Debug.LogError($"[Last Checkout] Field '{field}' not found on {so.targetObject.GetType().Name}");
            return prop;
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = Prop(so, field);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            var prop = Prop(so, field);
            if (prop == null) return;
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var prop = Prop(so, field);
            if (prop == null) return;
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var prop = Prop(so, field);
            if (prop == null) return;
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = Prop(so, field);
            if (prop == null) return;
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
