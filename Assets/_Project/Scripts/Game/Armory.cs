using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using LastCheckout.Combat;
using LastCheckout.Items;

namespace LastCheckout.Game
{
    /// <summary>
    /// The GUN ROOM ("Armory"). Built in code when the game starts, so there's nothing to set up.
    ///
    /// Where:  behind the store's back wall, through a new doorway at the right end of the back corridor.
    ///
    ///     z=21 ┌──────────── ARMORY ────────────┐
    ///          │ [5][6][7][8][9][0]  ranged     │  ← guns + bow for sale on the back wall
    ///          │ melee 1-4             ammo ▣   │  ← melee on the left wall, ammo crates on the right
    ///     z=15 └──[shutter]─────────────────────┘  ← store back wall
    ///             ↑ back corridor (x 10-11.6)
    ///
    /// Loot phase: the steel shutter is UP. At 0:00 it SLAMS DOWN.
    /// Anyone still inside is sealed in for good (that means you: game over).
    /// It's a SHOP: look at a weapon or crate and press E to buy (see ShopCounter). Stock never runs out.
    /// </summary>
    public class Armory : MonoBehaviour
    {
        // ---- Layout (world meters). The store is x -20..20, z -15..15; the back wall sits at z = 15. ----
        const float DoorMinX = 10.0f, DoorMaxX = 11.6f, DoorTop = 2.6f;
        const float RoomMinX = 9.3f, RoomMaxX = 17.7f, RoomMinZ = 15.15f, RoomMaxZ = 21.0f, RoomHeight = 3.4f;
        const float WallT = 0.3f, Floor = 0.02f, BackWallZ = 15f;
        const float ShutterZ = 14.78f; // just in front of the back wall, on the corridor side

        public bool IsOpen { get; private set; }

        /// <summary>Where the player starts: in the back corridor, 2.6 m in front of the shutter.</summary>
        public static Vector3 PlayerStartPoint => new Vector3((DoorMinX + DoorMaxX) * 0.5f, Floor + 0.03f, ShutterZ - 2.6f);

        Transform root;
        Transform shutter;
        NavMeshObstacle shutterObstacle;
        Light warnCorridor, warnInside;
        TextMesh statusSign;
        float shutterT;          // 0 = closed (down), 1 = open (up)
        float shutterTarget;
        float shutterSpeed;
        bool warning;


        static readonly Color Accent = new Color(1f, 0.78f, 0.24f);
        static readonly Color Danger = new Color(1f, 0.25f, 0.18f);

        // ───────────────────────── Building ─────────────────────────

        public void Build()
        {
            root = transform;

            Material wall = LevelKit.SceneMaterial("Back Wall 3") ?? LevelKit.SceneMaterial("Back Wall 1");
            Material floor = LevelKit.SceneMaterial("Store Floor") ?? wall;
            Material ceiling = LevelKit.SceneMaterial("Ceiling") ?? wall;
            Material metal = LevelKit.SceneMaterial("Register") ?? wall;
            Material crate = LevelKit.SceneMaterial("Crate 1") ?? metal;

            CutDoorway(wall);

            // Room shell
            float cx = (RoomMinX + RoomMaxX) * 0.5f, cz = (RoomMinZ + RoomMaxZ) * 0.5f;
            float w = RoomMaxX - RoomMinX + WallT * 2f, d = RoomMaxZ - BackWallZ + WallT;
            LevelKit.Box("Armory Floor", root, new Vector3(cx, Floor - 0.06f, cz), new Vector3(w, 0.12f, RoomMaxZ - BackWallZ + 0.1f), floor, new Color(0.42f, 0.43f, 0.45f));
            LevelKit.Box("Armory Wall Left", root, new Vector3(RoomMinX - WallT * 0.5f, RoomHeight * 0.5f, BackWallZ + d * 0.5f), new Vector3(WallT, RoomHeight, d), wall, new Color(0.3f, 0.32f, 0.34f));
            LevelKit.Box("Armory Wall Right", root, new Vector3(RoomMaxX + WallT * 0.5f, RoomHeight * 0.5f, BackWallZ + d * 0.5f), new Vector3(WallT, RoomHeight, d), wall, new Color(0.3f, 0.32f, 0.34f));
            LevelKit.Box("Armory Wall Back", root, new Vector3(cx, RoomHeight * 0.5f, RoomMaxZ + WallT * 0.5f), new Vector3(w, RoomHeight, WallT), wall, new Color(0.3f, 0.32f, 0.34f));
            LevelKit.Box("Armory Ceiling", root, new Vector3(cx, RoomHeight + 0.15f, BackWallZ + d * 0.5f), new Vector3(w, 0.3f, d), ceiling);
            // Yellow safety line on the floor just inside the door: "you are now in the danger zone".
            LevelKit.Box("Armory Floor Line", root, new Vector3((DoorMinX + DoorMaxX) * 0.5f, Floor + 0.004f, RoomMinZ + 0.25f), new Vector3(DoorMaxX - DoorMinX, 0.01f, 0.12f), floor, Accent, collider: false);

            // Lights: one bright work light, plus red warning lights on both sides of the door.
            AddLight("Armory Light", new Vector3(cx, RoomHeight - 0.25f, cz), new Color(0.9f, 0.95f, 1f), 9f, 11f);
            LevelKit.Box("Armory Light Fixture", root, new Vector3(cx, RoomHeight - 0.04f, cz), new Vector3(2.2f, 0.06f, 0.3f), metal, Color.white, collider: false);
            warnCorridor = AddLight("Armory Warning (corridor)", new Vector3((DoorMinX + DoorMaxX) * 0.5f, 3.1f, ShutterZ - 0.4f), Danger, 0f, 6f);
            warnInside = AddLight("Armory Warning (inside)", new Vector3((DoorMinX + DoorMaxX) * 0.5f, 2.9f, RoomMinZ + 0.6f), Danger, 0f, 7f);

            BuildShutter(metal);

            // Sign above the door (corridor side) + a live status line under it.
            LevelKit.Box("Armory Sign Plate", root, new Vector3((DoorMinX + DoorMaxX) * 0.5f, 3.3f, ShutterZ - 0.1f), new Vector3(2.4f, 0.75f, 0.04f), metal, new Color(0.06f, 0.06f, 0.07f), collider: false);
            LevelKit.Label("ARMORY", root, new Vector3((DoorMinX + DoorMaxX) * 0.5f, 3.42f, ShutterZ - 0.13f), Quaternion.identity, 0.34f, Accent);
            statusSign = LevelKit.Label("OPEN", root, new Vector3((DoorMinX + DoorMaxX) * 0.5f, 3.1f, ShutterZ - 0.13f), Quaternion.identity, 0.16f, new Color(0.5f, 1f, 0.6f));

            BuildRacks(metal, crate);
        }

        /// The store's back wall is one solid box at x 9..20. Replace it with three pieces that leave a doorway.
        void CutDoorway(Material fallback)
        {
            Vector3 probe = new Vector3((DoorMinX + DoorMaxX) * 0.5f, 1.3f, BackWallZ);
            foreach (var r in LevelKit.FindAll<Renderer>())
            {
                if (!r.name.StartsWith("Back Wall")) continue;
                Bounds b = r.bounds;
                if (!b.Contains(probe)) continue;

                Material mat = r.sharedMaterial != null ? r.sharedMaterial : fallback;
                Transform parent = r.transform.parent;
                float z = b.center.z, t = b.size.z;

                Piece(parent, "Back Wall (left of armory door)", b.min.x, DoorMinX, 0f, b.max.y, z, t, mat);
                Piece(parent, "Back Wall (right of armory door)", DoorMaxX, b.max.x, 0f, b.max.y, z, t, mat);
                Piece(parent, "Back Wall (above armory door)", DoorMinX, DoorMaxX, DoorTop, b.max.y, z, t, mat);
                // Switch it off right away (so the zombie floor map rebuilt this frame ignores it), then delete it.
                r.gameObject.SetActive(false);
                Destroy(r.gameObject);
                return;
            }
            Debug.LogWarning("[Armory] Couldn't find the back wall behind the corridor. Did the store layout change?");
        }

        void Piece(Transform parent, string name, float x0, float x1, float y0, float y1, float z, float t, Material mat)
        {
            if (x1 - x0 < 0.01f || y1 - y0 < 0.01f) return;
            var go = LevelKit.Box(name, null, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z), new Vector3(x1 - x0, y1 - y0, t), mat);
            go.transform.SetParent(parent, true);
        }

        void BuildShutter(Material metal)
        {
            float width = DoorMaxX - DoorMinX + 0.2f, height = DoorTop + 0.05f;
            shutter = new GameObject("Armory Shutter").transform;
            shutter.SetParent(root, false);

            var panel = LevelKit.Box("Shutter Panel", shutter, Vector3.zero, new Vector3(width, height, 0.08f), metal, new Color(0.2f, 0.21f, 0.23f));
            panel.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            // Horizontal slats so it reads as a roller shutter
            for (float y = 0.3f; y < height - 0.1f; y += 0.3f)
            {
                var slat = LevelKit.Box("Slat", shutter, Vector3.zero, new Vector3(width, 0.035f, 0.1f), metal, new Color(0.14f, 0.15f, 0.16f), collider: false);
                slat.transform.localPosition = new Vector3(0f, y, 0f);
            }
            var stripe = LevelKit.Box("Hazard Edge", shutter, Vector3.zero, new Vector3(width, 0.14f, 0.1f), metal, Accent, collider: false);
            stripe.transform.localPosition = new Vector3(0f, 0.07f, 0f);

            // Zombies treat the CLOSED shutter as a wall (it cuts a hole in their walkable-floor map).
            shutterObstacle = panel.AddComponent<NavMeshObstacle>();
            shutterObstacle.shape = NavMeshObstacleShape.Box;
            shutterObstacle.size = Vector3.one; // in the panel's own (scaled) space = the whole panel
            shutterObstacle.carving = true;
            shutterObstacle.enabled = false;

            shutterT = 1f; shutterTarget = 1f;
            PlaceShutter();
            IsOpen = true;
        }

        /// The SHOP. Everything here is bought with money (look at it + E):
        ///
        ///                 back wall:  [PISTOL][REVOLVER][BOW][SMG][SHOTGUN][RIFLE]
        ///   left wall:  KNIFE                                         right wall: ammo crates
        ///               BAT                                                       9MM / .357 / ARROWS
        ///               MACHETE                                                   12 GAUGE / 5.56
        ///               FIRE AXE                  [shutter]
        void BuildRacks(Material metal, Material crate)
        {
            var ranged = new List<WeaponSpec>();
            var melee = new List<WeaponSpec>();
            foreach (var w in WeaponCatalog.All) (w.IsMelee ? melee : ranged).Add(w);

            // ── Back wall: guns and the bow ──
            float rackZ = RoomMaxZ - 0.05f;
            for (int i = 0; i < ranged.Count && i < 6; i++)
            {
                var spec = ranged[i];
                float x = RoomMinX + 0.7f + i * 1.4f;
                var station = Station(spec);
                LevelKit.Box("Pegboard", station, new Vector3(x, 1.5f, rackZ - 0.03f), new Vector3(1.28f, 1.5f, 0.05f), metal, new Color(0.12f, 0.13f, 0.14f));
                LevelKit.Box("Shelf", station, new Vector3(x, 1.0f, rackZ - 0.22f), new Vector3(1.18f, 0.05f, 0.36f), metal, new Color(0.24f, 0.25f, 0.27f));
                LevelKit.Box("Light Strip", station, new Vector3(x, 2.27f, rackZ - 0.06f), new Vector3(1.2f, 0.025f, 0.03f), metal, Accent, collider: false);

                LevelKit.Label(spec.displayName, station, new Vector3(x, 2.5f, rackZ - 0.08f), Quaternion.identity, 0.2f, Color.white);
                var price = LevelKit.Label("$" + spec.price, station, new Vector3(x, 2.1f, rackZ - 0.08f), Quaternion.identity, 0.2f, Accent);
                LevelKit.Label(spec.blurb.ToUpperInvariant(), station, new Vector3(x, 0.86f, rackZ - 0.08f), Quaternion.identity, 0.05f, new Color(0.85f, 0.85f, 0.85f));

                var display = GunModelFactory.BuildDisplay(spec.id, station);
                bool bow = spec.kind == WeaponKind.Bow;
                display.position = new Vector3(x, bow ? 1.5f : 1.2f, rackZ - (bow ? 0.15f : 0.22f));
                display.rotation = Quaternion.Euler(0f, 90f, 0f);  // seen from the side
                display.localScale = Vector3.one * 1.5f;

                station.gameObject.AddComponent<ShopCounter>().Setup(spec, false, price);
            }

            // ── Left wall: melee weapons hanging on hooks ──
            Quaternion leftWallText = Quaternion.Euler(0f, -90f, 0f); // read while looking at the left wall
            for (int i = 0; i < melee.Count && i < 4; i++)
            {
                var spec = melee[i];
                float z = 16.2f + i * 1.15f;
                var station = Station(spec);
                LevelKit.Box("Pegboard", station, new Vector3(RoomMinX + 0.03f, 1.5f, z), new Vector3(0.05f, 1.5f, 1.05f), metal, new Color(0.12f, 0.13f, 0.14f));
                LevelKit.Box("Hook", station, new Vector3(RoomMinX + 0.1f, 1.0f, z), new Vector3(0.1f, 0.03f, 0.03f), metal, new Color(0.5f, 0.5f, 0.5f), collider: false);
                LevelKit.Box("Light Strip", station, new Vector3(RoomMinX + 0.06f, 2.27f, z), new Vector3(0.03f, 0.025f, 1.0f), metal, Accent, collider: false);

                LevelKit.Label(spec.displayName, station, new Vector3(RoomMinX + 0.08f, 2.5f, z), leftWallText, 0.18f, Color.white);
                var price = LevelKit.Label("$" + spec.price, station, new Vector3(RoomMinX + 0.08f, 2.12f, z), leftWallText, 0.18f, Accent);
                LevelKit.Label(spec.blurb.ToUpperInvariant(), station, new Vector3(RoomMinX + 0.08f, 0.86f, z), leftWallText, 0.045f, new Color(0.85f, 0.85f, 0.85f));

                var display = GunModelFactory.BuildDisplay(spec.id, station);
                display.position = new Vector3(RoomMinX + 0.14f, 0.95f, z);
                display.rotation = Quaternion.Euler(0f, 90f, 0f);  // edge pointing into the room
                display.localScale = Vector3.one * (spec.id == "knife" ? 1.6f : 1f);

                station.gameObject.AddComponent<ShopCounter>().Setup(spec, false, price);
            }

            // ── Right wall: one ammo crate per ammo type (the pistol and SMG share 9mm) ──
            var seen = new HashSet<ItemDefinition>();
            int crateIndex = 0;
            foreach (var spec in ranged)
            {
                if (spec.ammoItem == null || !seen.Add(spec.ammoItem)) continue;
                float z = 15.9f + crateIndex++ * 0.9f;
                float crateX = RoomMaxX - 0.45f;
                var box = LevelKit.Box($"Ammo Crate {spec.ammoName}", root, new Vector3(crateX, Floor + 0.3f, z), new Vector3(0.7f, 0.6f, 0.8f), crate, new Color(0.25f, 0.3f, 0.2f));
                // A few ammo boxes on top (decoration).
                for (int k = 0; k < 3; k++)
                    LevelKit.Box("Ammo Box", box.transform.parent, new Vector3(crateX - 0.15f + k * 0.15f, Floor + 0.65f, z + (k == 1 ? 0.12f : -0.08f)),
                                 new Vector3(0.12f, 0.1f, 0.16f), crate, spec.ammoItem.placeholderColor, collider: false);

                LevelKit.Label(spec.ammoName, root, new Vector3(RoomMaxX - 0.02f, 1.3f, z), Quaternion.Euler(0f, 90f, 0f), 0.2f, Accent);
                var price = LevelKit.Label($"x{spec.ammoPack}  ${spec.ammoPrice}", root, new Vector3(RoomMaxX - 0.02f, 1.05f, z), Quaternion.Euler(0f, 90f, 0f), 0.14f, Accent);
                box.AddComponent<ShopCounter>().Setup(spec, true, price);
            }
        }

        Transform Station(WeaponSpec spec)
        {
            var station = new GameObject("Shop: " + spec.displayName).transform;
            station.SetParent(root, false);
            return station;
        }

        Light AddLight(string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // ───────────────────────── Game control ─────────────────────────

        /// <summary>Kept so older code can still call it.</summary>
        public void Restock()
        {
            // Nothing to put back any more: the armory is a shop now (ShopCounter), and its stock never runs out.
        }

        public void Open()
        {
            IsOpen = true;
            warning = false;
            shutterTarget = 1f;
            shutterSpeed = 1f / 1.4f; // rolls up in 1.4 s
            shutterObstacle.enabled = false;
            SetStatus("OPEN", new Color(0.5f, 1f, 0.6f));
        }

        /// <summary>Last 10 seconds: flashing red lights.</summary>
        public void SetWarning(bool on)
        {
            warning = on;
            if (on) SetStatus("SEALING", Danger);
        }

        /// <summary>Slams the shutter. Returns true if the player was still inside (they're sealed in).</summary>
        public bool Close(Transform player)
        {
            IsOpen = false;
            warning = false;
            shutterTarget = 0f;
            shutterSpeed = 1f / 0.35f; // slams down fast
            shutterObstacle.enabled = true;
            SetStatus("SEALED", Danger);

            // Zombies that wandered in are sealed in too (otherwise the wave could never be finished).
            foreach (var z in Enemies.Zombie.All)
                if (z != null && !z.IsDead && IsInside(z.transform.position)) z.TakeDamage(100000f);

            return player != null && IsInside(player.position);
        }

        /// <summary>True if a point is inside the armory (including standing in its doorway).</summary>
        public bool IsInside(Vector3 p) =>
            p.x > RoomMinX - 0.1f && p.x < RoomMaxX + 0.1f && p.z > ShutterZ + 0.12f && p.z < RoomMaxZ + 0.2f && p.y < RoomHeight + 1f;

        void SetStatus(string text, Color color)
        {
            if (statusSign == null) return;
            statusSign.text = text;
            statusSign.color = color;
        }

        void Update()
        {
            // Move the shutter towards its target position.
            if (!Mathf.Approximately(shutterT, shutterTarget))
            {
                shutterT = Mathf.MoveTowards(shutterT, shutterTarget, shutterSpeed * Time.deltaTime);
                PlaceShutter();
            }

            // Warning lights: pulse during the countdown, steady dim red when sealed, off when open.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);
            float intensity = warning ? 7f * pulse : (!IsOpen ? 1.5f : 0f);
            warnCorridor.intensity = intensity;
            warnInside.intensity = intensity;
        }

        void PlaceShutter()
        {
            // Closing accelerates as it drops (= slam); opening rolls up smoothly.
            float t = shutterTarget < shutterT ? 1f - (1f - shutterT) * (1f - shutterT) : LevelKit.EaseInOut(shutterT);
            float lift = t * (DoorTop + 0.05f);
            shutter.position = new Vector3((DoorMinX + DoorMaxX) * 0.5f, Floor + lift, ShutterZ);
        }
    }
}
