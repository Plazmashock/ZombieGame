using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using LastCheckout.Combat;
using LastCheckout.Enemies;
using LastCheckout.World;   // ModelKit, Ladder, SurfaceMaterials

namespace LastCheckout.Game
{
    /// <summary>
    /// STORE UPGRADES: spend your cash to grow and fortify the supermarket. Bought between waves
    /// (loot phase), from the kiosk in the back corridor or by pressing U. They last for the whole run.
    ///
    ///   AUTO TURRETS      4 levels   +1 sentry gun each (entrance L, entrance R, loading bay, centre aisle)
    ///   TURRET OVERCLOCK  3 levels   more damage → faster fire → longer range (all turrets)
    ///   BARBED WIRE       2 levels   wire across both doorways, then a second outer line + razor wire
    ///   FLOODLIGHTS       1 level    big lights on the roof edge: see them coming
    ///   ROOFTOP           2 levels   2nd FLOOR: ladder up the front wall to a walled roof deck  →  3rd FLOOR: sniper tower + turret
    ///   STOCKROOM ANNEX   2 levels   NEW ROOM off the storage room with a supply crate  →  medical bay
    ///
    /// Building something solid (turret, stairs, room) also re-makes the zombies' walkable-floor map,
    /// so they path around (or up!) the new structures. That's why you can only buy between waves.
    ///
    /// Top view of where things go:
    ///
    ///               ┌annex┐      loading bay ≋wire≋   ┌armory┐
    ///               │crate├door─ storage │T3 corridor  │      │
    ///               └─────┘     ─────────┘ kiosk ──────┴──────┤
    ///                                │       T4               │
    ///                            T1  │                    T2  │
    ///               └────────────── entrance ≋wire≋ ───────[ladder]┘
    /// </summary>
    public class StoreUpgrades : MonoBehaviour
    {
        public class Upgrade
        {
            public string id, title, tag, requiresId, requiresText;
            public int[] prices;
            public string[] steps;     // what each level adds
            public Color color;
            public int Level;
            public int MaxLevel => prices.Length;
            public bool Maxed => Level >= prices.Length;
            public int NextPrice => Maxed ? 0 : prices[Level];
            public string NextStep => Maxed ? "Fully upgraded." : steps[Level];
        }

        public static StoreUpgrades Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public IReadOnlyList<Upgrade> All => upgrades;
        /// <summary>Fires after every purchase.</summary>
        public event System.Action Changed;

        readonly List<Upgrade> upgrades = new List<Upgrade>();
        GameDirector director;
        PlayerWallet wallet;
        Transform root;

        static readonly Color Accent = new Color(1f, 0.78f, 0.24f);

        // ───────────────────────── Setup ─────────────────────────

        public void Init(GameDirector d, PlayerWallet w)
        {
            Instance = this;
            director = d;
            wallet = w;
            // Upgrade effects are per run: a restart starts from scratch.
            Turret.DamageMultiplier = 1f; Turret.FireRateMultiplier = 1f; Turret.RangeBonus = 0f;
            BarbedWireZone.DamageMultiplier = 1f;
            root = new GameObject("Store Upgrades").transform;

            upgrades.Add(new Upgrade
            {
                id = "turrets", title = "AUTO TURRETS", tag = "DEFENSE", color = new Color(1f, 0.45f, 0.3f),
                prices = new[] { 150, 225, 300, 400 },
                steps = new[]
                {
                    "Sentry gun guarding the main entrance (left side). Shoots any zombie it can see.",
                    "Second sentry gun on the right side of the main entrance.",
                    "Sentry gun in the back corridor, covering the loading bay.",
                    "Sentry gun in the centre aisle, last line of defense.",
                },
            });
            upgrades.Add(new Upgrade
            {
                id = "power", title = "TURRET OVERCLOCK", tag = "DEFENSE", color = new Color(1f, 0.6f, 0.2f),
                requiresId = "turrets", requiresText = "NEEDS AN AUTO TURRET",
                prices = new[] { 125, 200, 300 },
                steps = new[]
                {
                    "+35% damage for every turret.",
                    "+70% damage total, and turrets fire 25% faster.",
                    "+110% damage, 40% faster fire, +8 m range.",
                },
            });
            upgrades.Add(new Upgrade
            {
                id = "wire", title = "BARBED WIRE", tag = "TRAPS", color = new Color(0.7f, 0.75f, 0.8f),
                prices = new[] { 100, 175 },
                steps = new[]
                {
                    "Wire coils across the front door and the loading bay. Zombies get cut and slowed.",
                    "A second, outer line at both doors, and razor wire: double damage.",
                },
            });
            upgrades.Add(new Upgrade
            {
                id = "lights", title = "FLOODLIGHTS", tag = "UTILITY", color = new Color(1f, 0.92f, 0.6f),
                prices = new[] { 75 },
                steps = new[] { "Eight floodlights on the roof edge light up the parking lot and the back. See them coming." },
            });
            upgrades.Add(new Upgrade
            {
                id = "roof", title = "ROOFTOP", tag = "NEW FLOORS", color = new Color(0.45f, 0.8f, 1f),
                prices = new[] { 200, 325 },
                steps = new[]
                {
                    "2ND FLOOR: a steel LADDER on the front wall (parking-lot side, right of the shop window) up to a walled roof deck. Walk into it and hold W. Zombies can't climb ladders: safe high ground.",
                    "3RD FLOOR: a sniper tower on the roof, with its own long-range turret and a searchlight.",
                },
            });
            upgrades.Add(new Upgrade
            {
                id = "annex", title = "STOCKROOM ANNEX", tag = "NEW ROOM", color = new Color(0.5f, 0.95f, 0.6f),
                prices = new[] { 225, 175 },
                steps = new[]
                {
                    "New room through the storage room's left wall. Its supply crate gives ammo + meds every loot phase.",
                    "Medical bay in the annex: heal to full health once per loot phase.",
                },
            });

            BuildKiosk();
        }

        public Upgrade Get(string id)
        {
            foreach (var u in upgrades) if (u.id == id) return u;
            return null;
        }

        // ───────────────────────── Buying ─────────────────────────

        /// <summary>Upgrades can only be bought between waves (they change the zombies' floor map).</summary>
        public bool ShopOpen => director != null && director.HasStarted &&
                                (director.Current == GameDirector.Phase.Loot || director.Current == GameDirector.Phase.Cleared);

        /// <summary>Why you can't buy the next level right now, or null if you can.</summary>
        public string BlockReason(Upgrade u)
        {
            if (u.Maxed) return "MAXED";
            if (!ShopOpen) return "LOCKED DURING WAVES";
            if (!string.IsNullOrEmpty(u.requiresId))
            {
                var req = Get(u.requiresId);
                if (req != null && req.Level == 0) return u.requiresText;
            }
            int cash = wallet != null ? wallet.Cash : 0;
            if (cash < u.NextPrice) return $"NEED ${u.NextPrice - cash} MORE";
            return null;
        }

        public bool TryBuy(Upgrade u)
        {
            if (u == null || BlockReason(u) != null) return false;
            if (!wallet.TrySpend(u.NextPrice, $"{u.title} {u.Level + 1}")) return false;
            u.Level++;
            bool solid = Build(u.id, u.Level);
            if (solid) RebakeNavMesh();
            ShowMarker(u.id, u.Level);   // floating "NEW" tag + light beam where the new thing is
            director.Say(u.title + (u.MaxLevel > 1 ? $"  LV {u.Level}" : ""), u.steps[u.Level - 1], false);
            Changed?.Invoke();
            return true;
        }

        /// Builds what one level adds. Returns true if it added solid things (so the floor map must be re-made).
        bool Build(string id, int level)
        {
            switch (id)
            {
                case "turrets": BuildTurret(level - 1); return true;
                case "power": ApplyOverclock(level); return false;
                case "wire": BuildWire(level); return false;
                case "lights": BuildFloodlights(); return false;
                case "roof": if (level == 1) BuildRoofDeck(); else BuildTower(); return true;
                case "annex": if (level == 1) BuildAnnex(); else BuildMedBay(); return true;
            }
            return false;
        }

        /// Floating notification that shows where an upgrade appeared (see UpgradeMarker).
        void ShowMarker(string id, int level)
        {
            Vector3 pos;
            string title, hint;
            Color color = Accent;
            switch (id)
            {
                case "turrets":
                    pos = TurretSpots[Mathf.Clamp(level - 1, 0, TurretSpots.Length - 1)].pos + Vector3.up * 1.3f;
                    title = "AUTO TURRET"; hint = new[] { "main entrance, left", "main entrance, right", "back corridor", "centre aisle" }[Mathf.Clamp(level - 1, 0, 3)];
                    color = new Color(1f, 0.45f, 0.3f);
                    break;
                case "wire":
                    pos = level == 1 ? new Vector3(0f, 1.0f, -16.3f) : new Vector3(0f, 1.0f, -19.0f);
                    title = level == 1 ? "BARBED WIRE" : "RAZOR WIRE"; hint = "across the front door";
                    color = new Color(0.7f, 0.75f, 0.8f);
                    break;
                case "lights":
                    pos = new Vector3(0f, 7.0f, -15f);
                    title = "FLOODLIGHTS"; hint = "on the roof edge, all around";
                    color = new Color(1f, 0.92f, 0.6f);
                    break;
                case "roof":
                    color = new Color(0.45f, 0.8f, 1f);
                    if (level == 1) { pos = new Vector3(18.6f, 2.2f, -16.05f); title = "ROOF LADDER"; hint = "front wall, right of the shop window"; }
                    else { pos = new Vector3(13f, 9.4f, 10.8f); title = "SNIPER TOWER"; hint = "on the roof, back right"; }
                    break;
                case "annex":
                    color = new Color(0.5f, 0.95f, 0.6f);
                    if (level == 1) { pos = new Vector3(-19.8f, 1.8f, 12.7f); title = "STOCKROOM ANNEX"; hint = "new door in the storage room's left wall"; }
                    else { pos = new Vector3(-23.6f, 1.9f, 10.2f); title = "MEDICAL BAY"; hint = "inside the stockroom annex"; }
                    break;
                default:
                    return;   // e.g. turret overclock: nothing new to look at
            }
            UpgradeMarker.Show(title, hint, pos, color);
        }

        static void RebakeNavMesh()
        {
            var surface = LevelKit.FindFirst<NavMeshSurface>();
            if (surface != null) NavMeshBaker.Bake(surface);
        }

        // ───────────────────────── Materials ─────────────────────────

        static Material Plate => SurfaceMaterials.Get(Surface.DiamondPlate, new Color(0.62f, 0.64f, 0.67f));
        static Material DarkPaint => SurfaceMaterials.Get(Surface.PaintedMetal, new Color(0.2f, 0.22f, 0.24f));
        static Material Hazard => SurfaceMaterials.Get(Surface.PaintedMetal, Accent);
        static Material Concrete => SurfaceMaterials.Get(Surface.Concrete, new Color(0.56f, 0.56f, 0.54f));
        static Material Block => SurfaceMaterials.Get(Surface.PaintedBlock, new Color(0.72f, 0.7f, 0.64f));
        static Material Tiles => SurfaceMaterials.Get(Surface.FloorTiles, new Color(0.6f, 0.6f, 0.57f));
        static Material CeilingMat => SurfaceMaterials.Get(Surface.CeilingTiles, new Color(0.62f, 0.62f, 0.6f));
        static Material Crate => SurfaceMaterials.Get(Surface.Wood, new Color(0.36f, 0.4f, 0.24f));
        static Material Cardboard => SurfaceMaterials.Get(Surface.Wood, new Color(0.62f, 0.47f, 0.28f));
        static Material WhitePaint => SurfaceMaterials.Get(Surface.PaintedMetal, new Color(0.9f, 0.91f, 0.9f));
        static Material Glow(Color c, float e = 2.5f) => SurfaceMaterials.Get(Surface.PaintedMetal, c, e);

        Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        static Light AddLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range,
                              LightType type = LightType.Point, Quaternion? rotation = null, float spotAngle = 70f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rotation ?? Quaternion.identity);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            if (type == LightType.Spot) { l.spotAngle = spotAngle; l.innerSpotAngle = spotAngle * 0.6f; }
            l.shadows = LightShadows.None;
            return l;
        }

        // ───────────────────────── Kiosk (always there) ─────────────────────────

        /// The upgrade terminal against the right wall of the back corridor, to your right when the game starts.
        void BuildKiosk()
        {
            var k = Group("Upgrade Terminal").gameObject;
            Vector3 c = new Vector3(11.575f, 0.02f, 11.0f);
            ModelKit.Box("Body", k.transform, c + Vector3.up * 0.65f, new Vector3(0.55f, 1.3f, 0.8f), DarkPaint);
            ModelKit.Box("Top", k.transform, c + new Vector3(-0.05f, 1.42f, 0f), new Vector3(0.6f, 0.25f, 0.84f), Hazard, collider: false,
                         rotation: Quaternion.Euler(0f, 0f, 20f));
            ModelKit.Box("Screen", k.transform, c + new Vector3(-0.29f, 1.05f, 0f), new Vector3(0.02f, 0.38f, 0.6f), Glow(new Color(0.3f, 0.85f, 1f), 2f), collider: false);
            ModelKit.Box("Keypad", k.transform, c + new Vector3(-0.33f, 0.72f, 0f), new Vector3(0.12f, 0.04f, 0.5f), Plate, collider: false,
                         rotation: Quaternion.Euler(0f, 0f, -25f));
            ModelKit.Box("Stripe", k.transform, c + new Vector3(-0.28f, 0.3f, 0f), new Vector3(0.02f, 0.08f, 0.8f), Hazard, collider: false);
            LevelKit.Label("UPGRADES", k.transform, c + new Vector3(-0.31f, 1.62f, 0f), Quaternion.Euler(0f, 90f, 0f), 0.16f, Accent);
            LevelKit.Label("[E] OR [U]", k.transform, c + new Vector3(-0.31f, 0.92f, 0f), Quaternion.Euler(0f, 90f, 0f), 0.06f, new Color(0.75f, 0.95f, 1f));
            AddLight("Terminal Glow", k.transform, c + new Vector3(-0.6f, 1.1f, 0f), new Color(0.35f, 0.8f, 1f), 1.2f, 3f);
            k.AddComponent<UpgradeTerminal>();
        }

        // ───────────────────────── Turrets ─────────────────────────

        static readonly (Vector3 pos, float yaw)[] TurretSpots =
        {
            (new Vector3(-4.4f, 0.02f, -13.0f), 150f),  // main entrance, left
            (new Vector3(4.4f, 0.02f, -13.0f), 210f),   // main entrance, right
            (new Vector3(3.0f, 0.02f, 13.6f), 60f),     // back corridor → loading bay
            (new Vector3(0f, 0.02f, -2.0f), 180f),      // centre aisle
        };

        void BuildTurret(int index)
        {
            if (index < 0 || index >= TurretSpots.Length) return;
            var s = TurretSpots[index];
            Turret.Build($"Turret {index + 1}", root, s.pos, s.yaw, 22f);
        }

        static void ApplyOverclock(int level)
        {
            Turret.DamageMultiplier = level >= 3 ? 2.1f : level == 2 ? 1.7f : 1.35f;
            Turret.FireRateMultiplier = level >= 3 ? 1.4f : level == 2 ? 1.25f : 1f;
            Turret.RangeBonus = level >= 3 ? 8f : 0f;
        }

        // ───────────────────────── Barbed wire ─────────────────────────

        void BuildWire(int level)
        {
            var g = Group("Barbed Wire " + level);
            if (level == 1)
            {
                BarbedWireZone.Build("Wire (front door)", g, new Vector3(-3.4f, 0.12f, -16.3f), new Vector3(3.4f, 0.12f, -16.3f), 12f);
                BarbedWireZone.Build("Wire (loading bay)", g, new Vector3(4.7f, 0f, 27.2f), new Vector3(8.9f, 0f, 27.2f), 12f);   // behind the supply truck, which parks right at the bay
            }
            else
            {
                BarbedWireZone.Build("Wire (front, outer)", g, new Vector3(-3.6f, 0f, -19.0f), new Vector3(3.6f, 0f, -19.0f), 12f);
                BarbedWireZone.Build("Wire (bay, outer)", g, new Vector3(4.7f, 0f, 28.7f), new Vector3(8.9f, 0f, 28.7f), 12f);
                BarbedWireZone.DamageMultiplier = 2f;
            }
        }

        // ───────────────────────── Floodlights ─────────────────────────

        void BuildFloodlights()
        {
            var g = Group("Floodlights");
            const float roofTop = 5.3f;
            // (base position on the roof edge, direction it shines: yaw)
            var spots = new (Vector3 p, float yaw)[]
            {
                (new Vector3(-15f, roofTop, -14.9f), 180f), (new Vector3(-5f, roofTop, -14.9f), 180f),
                (new Vector3(5f, roofTop, -14.9f), 180f),   (new Vector3(15f, roofTop, -14.9f), 180f),
                (new Vector3(-10f, roofTop, 14.9f), 0f),    (new Vector3(4f, roofTop, 14.9f), 0f),
                (new Vector3(-19.9f, roofTop, -2f), -90f),  (new Vector3(19.9f, roofTop, -6f), 90f),
            };
            var lens = Glow(new Color(1f, 0.97f, 0.85f), 4f);
            foreach (var s in spots)
            {
                Quaternion face = Quaternion.Euler(0f, s.yaw, 0f);
                ModelKit.Cylinder("Pole", g, s.p + Vector3.up * 0.8f, 0.05f, 1.6f, DarkPaint, 10);
                Quaternion tilt = Quaternion.Euler(35f, s.yaw, 0f); // pointing down-and-out at 35°
                Vector3 head = s.p + Vector3.up * 1.65f + face * Vector3.forward * 0.12f;
                ModelKit.Box("Lamp Head", g, head, new Vector3(0.5f, 0.32f, 0.22f), DarkPaint, collider: false, rotation: tilt);
                ModelKit.Box("Lens", g, head + tilt * Vector3.forward * 0.115f, new Vector3(0.44f, 0.26f, 0.02f), lens, collider: false, rotation: tilt);
                AddLight("Floodlight", g, head + tilt * Vector3.forward * 0.2f, new Color(1f, 0.96f, 0.86f), 7f, 34f, LightType.Spot, tilt, 85f);
            }
        }

        // ───────────────────────── Rooftop: 2nd floor ─────────────────────────

        /// Stairs go up the OUTSIDE of the right wall, from the front corner (z −0.5) to the back (z 12.5),
        /// onto a landing that steps onto the roof (y 5.3). The roof gets a waist-high wall all around.
        void BuildRoofDeck()
        {
            var g = Group("Rooftop Deck");
            const float roof = 5.3f;

            // ACCESS: a ladder on the FRONT wall. (The old stairs were on the right side of the store, behind the side fence,
            // where the player can't go, so nobody could ever climb them.)
            BuildRoofLadder(g, roof);

            // Roof surface (gravel-ish concrete) + parapet walls. Gap in the right wall where the landing is.
            ModelKit.Box("Roof Surface", g, new Vector3(0f, roof + 0.01f, 0f), new Vector3(40.5f, 0.02f, 30.5f), Concrete, collider: false);
            float h = 1.05f, t = 0.25f, y = roof + h * 0.5f;
            // The front parapet has a 1.8 m gap (x 17.7..19.5) where the ladder arrives.
            ModelKit.Box("Parapet Front (left part)", g, new Vector3(-1.3f, y, -15.175f), new Vector3(38f, h, t), Concrete);
            ModelKit.Box("Parapet Front (right part)", g, new Vector3(19.9f, y, -15.175f), new Vector3(0.8f, h, t), Concrete);
            ModelKit.Box("Parapet Back", g, new Vector3(0f, y, 15.175f), new Vector3(40.6f, h, t), Concrete);
            ModelKit.Box("Parapet Left", g, new Vector3(-20.175f, y, 0f), new Vector3(t, h, 30.6f), Concrete);
            ModelKit.Box("Parapet Right", g, new Vector3(20.175f, y, 0f), new Vector3(t, h, 30.6f), Concrete);
            ModelKit.Box("Parapet Cap (left part)", g, new Vector3(-1.325f, roof + h + 0.02f, -15.175f), new Vector3(38.05f, 0.04f, 0.35f), Hazard, collider: false);
            ModelKit.Box("Parapet Cap (right part)", g, new Vector3(19.925f, roof + h + 0.02f, -15.175f), new Vector3(0.85f, 0.04f, 0.35f), Hazard, collider: false);

            // Air-conditioning units: cover from the tower and something to look at.
            foreach (var p in new[] { new Vector3(-9f, roof, 2f), new Vector3(7f, roof, -6f), new Vector3(-14f, roof, -9f) })
            {
                ModelKit.Box("AC Unit", g, p + Vector3.up * 0.65f, new Vector3(2.2f, 1.3f, 1.6f), SurfaceMaterials.Get(Surface.PaintedMetal, new Color(0.7f, 0.72f, 0.72f)));
                ModelKit.Cylinder("Fan Grille", g, p + Vector3.up * 1.32f, 0.55f, 0.04f, SurfaceMaterials.Get(Surface.BrushedMetal, new Color(0.25f, 0.25f, 0.26f)), 20);
                ModelKit.Box("Duct", g, p + new Vector3(1.4f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), SurfaceMaterials.Get(Surface.BrushedMetal, new Color(0.65f, 0.66f, 0.68f)));
            }

            // Lights: roof work lights + one at the bottom of the stairs.
            AddLight("Roof Light 1", g, new Vector3(-8f, roof + 2.4f, -6f), new Color(1f, 0.85f, 0.65f), 3f, 16f);
            AddLight("Roof Light 2", g, new Vector3(10f, roof + 2.4f, 6f), new Color(1f, 0.85f, 0.65f), 3f, 16f);
        }

        /// <summary>
        /// The roof ladder: on the front wall of the store, in the parking lot, right of the right shop window (x 17..20).
        ///   Walk into it facing the wall and hold W. At the top keep holding W to hop onto the roof. S = down, Space = jump off.
        /// The ladder has no collider (see World/Ladder), so zombies can't use it.
        /// </summary>
        void BuildRoofLadder(Transform g, float roof)
        {
            const float x = 18.6f, floorY = 0.12f;   // 0.12 = top of the sidewalk
            var column = new Vector3(x, floorY, -16.05f);
            Ladder.Build("Roof Ladder", g, column, Vector3.forward, roof, Glow(Accent, 0.4f), width: 0.6f, standOff: 0.55f, wallGap: 0.35f);

            // Hazard stripes on the ground and a sign + lamp so you can find it.
            ModelKit.Box("Ladder Floor Mark", g, new Vector3(x, floorY + 0.005f, -16.05f), new Vector3(1.1f, 0.01f, 1.0f), Hazard, collider: false);
            LevelKit.Label("ROOF ACCESS", g, new Vector3(x, 3.55f, -15.2f), Quaternion.identity, 0.28f, Accent);
            LevelKit.Label("2ND FLOOR  ·  walk in and hold W", g, new Vector3(x, 3.2f, -15.2f), Quaternion.identity, 0.11f, Color.white);
            AddLight("Ladder Light", g, new Vector3(x, 3.6f, -16.6f), new Color(1f, 0.85f, 0.65f), 3f, 8f);
        }

        // ───────────────────────── Rooftop: 3rd floor (tower) ─────────────────────────

        void BuildTower()
        {
            var g = Group("Sniper Tower");
            const float roof = 5.3f, deck = 8.3f;
            // Ramp up from the roof (x 3 → 11), then a 4 × 4 m platform with railings and a turret.
            BuildStairs("Tower Stairs", g, new Vector3(3f, roof, 10.8f), new Vector3(11f, deck, 10.8f), 1.4f, railLeft: true, railRight: true);
            ModelKit.Box("Tower Deck", g, new Vector3(13f, deck - 0.1f, 10.8f), new Vector3(4f, 0.2f, 4f), Plate);
            foreach (var p in new[] { new Vector3(11.2f, 0f, 9.0f), new Vector3(14.8f, 0f, 9.0f), new Vector3(11.2f, 0f, 12.6f), new Vector3(14.8f, 0f, 12.6f) })
            {
                float legH = deck - 0.2f - roof;
                ModelKit.Cylinder("Tower Leg", g, new Vector3(p.x, roof + legH * 0.5f, p.z), 0.12f, legH, DarkPaint, 12, collider: true);
            }
            // Diagonal braces between the legs (looks sturdy).
            var mb = new ModelKit.MeshBuilder();
            mb.AddStick(new Vector3(11.2f, roof, 9.0f), new Vector3(14.8f, deck - 0.2f, 9.0f), 0.08f, Matrix4x4.identity, 1f);
            mb.AddStick(new Vector3(14.8f, roof, 12.6f), new Vector3(11.2f, deck - 0.2f, 12.6f), 0.08f, Matrix4x4.identity, 1f);
            mb.AddStick(new Vector3(14.8f, roof, 9.0f), new Vector3(14.8f, deck - 0.2f, 12.6f), 0.08f, Matrix4x4.identity, 1f);
            var braces = new GameObject("Braces"); braces.transform.SetParent(g, false);
            ModelKit.Finish(braces, mb, DarkPaint);

            // Railings: all around, except where the ramp arrives (west side, z 10.1–11.5).
            Railing(g, new Vector3(11f, deck, 8.8f), new Vector3(15f, deck, 8.8f));
            Railing(g, new Vector3(15f, deck, 8.8f), new Vector3(15f, deck, 12.8f));
            Railing(g, new Vector3(15f, deck, 12.8f), new Vector3(11f, deck, 12.8f));
            Railing(g, new Vector3(11f, deck, 12.8f), new Vector3(11f, deck, 11.5f));
            Railing(g, new Vector3(11f, deck, 10.1f), new Vector3(11f, deck, 8.8f));

            Turret.Build("Tower Turret", g, new Vector3(14.2f, deck, 10.8f), 180f, 42f, pedestalHeight: 0.35f);

            // Searchlight on a pole, sweeping the parking lot.
            ModelKit.Cylinder("Searchlight Pole", g, new Vector3(11.4f, deck + 0.9f, 9.1f), 0.05f, 1.8f, DarkPaint, 10);
            var head = new GameObject("Searchlight");
            head.transform.SetParent(g, false);
            head.transform.position = new Vector3(11.4f, deck + 1.9f, 9.1f);
            ModelKit.Cylinder("Searchlight Drum", head.transform, head.transform.position, 0.22f, 0.4f, DarkPaint, 16, rotation: Quaternion.Euler(90f, 0f, 0f));
            ModelKit.Cylinder("Searchlight Lens", head.transform, head.transform.position + Vector3.forward * 0.21f, 0.19f, 0.02f, Glow(new Color(1f, 0.97f, 0.85f), 5f), 16, rotation: Quaternion.Euler(90f, 0f, 0f));
            var l = AddLight("Searchlight Beam", head.transform, head.transform.position + Vector3.forward * 0.3f, new Color(1f, 0.97f, 0.88f), 14f, 55f, LightType.Spot, null, 28f);
            l.transform.localRotation = Quaternion.identity;
            head.AddComponent<SearchlightSweep>().Setup(180f, 55f, 22f, 0.25f);

            LevelKit.Label("3RD FLOOR", g, new Vector3(3.2f, roof + 1.4f, 11.55f), Quaternion.identity, 0.25f, Accent);
        }

        // ───────────────────────── Stockroom annex: new room ─────────────────────────

        //   x −27 ─────────── −20 (store's left wall)
        //   z 15.5 ┌ shelves ─────┐
        //          │              │
        //          │  crate    ═══╪═ new doorway (z 11.9–13.5) into the storage room
        //   z 9.5  └ med bay ─────┘
        const float AnnexMinX = -27f, AnnexMaxX = -20.15f, AnnexMinZ = 9.5f, AnnexMaxZ = 15.5f, AnnexH = 3.4f;
        const float AnnexDoorMinZ = 11.9f, AnnexDoorMaxZ = 13.5f, AnnexDoorTop = 2.6f;

        void BuildAnnex()
        {
            var g = Group("Stockroom Annex");
            float cx = (AnnexMinX + AnnexMaxX) * 0.5f, cz = (AnnexMinZ + AnnexMaxZ) * 0.5f, w = 0.3f;

            CutDoorwayInLeftWall(AnnexDoorMinZ, AnnexDoorMaxZ, AnnexDoorTop);

            // Shell: floor, three walls, ceiling, roof cap.
            ModelKit.Box("Annex Floor", g, new Vector3(cx, -0.04f, cz), new Vector3(AnnexMaxX - AnnexMinX + 0.6f, 0.12f, AnnexMaxZ - AnnexMinZ + 0.6f), Tiles);
            ModelKit.Box("Annex Wall West", g, new Vector3(AnnexMinX - w * 0.5f, AnnexH * 0.5f, cz), new Vector3(w, AnnexH, AnnexMaxZ - AnnexMinZ + 2f * w), Block);
            ModelKit.Box("Annex Wall South", g, new Vector3(cx - 0.15f, AnnexH * 0.5f, AnnexMinZ - w * 0.5f), new Vector3(AnnexMaxX - AnnexMinX + 0.6f, AnnexH, w), Block);
            ModelKit.Box("Annex Wall North", g, new Vector3(cx - 0.15f, AnnexH * 0.5f, AnnexMaxZ + w * 0.5f), new Vector3(AnnexMaxX - AnnexMinX + 0.6f, AnnexH, w), Block);
            ModelKit.Box("Annex Ceiling", g, new Vector3(cx, AnnexH + 0.15f, cz), new Vector3(AnnexMaxX - AnnexMinX + 0.9f, 0.3f, AnnexMaxZ - AnnexMinZ + 0.9f), CeilingMat);
            ModelKit.Box("Annex Roof", g, new Vector3(cx, AnnexH + 0.31f, cz), new Vector3(AnnexMaxX - AnnexMinX + 0.95f, 0.02f, AnnexMaxZ - AnnexMinZ + 0.95f), Concrete, collider: false);
            // Green band like the store (1 m high)
            var band = SurfaceMaterials.Get(Surface.PaintedMetal, new Color(0.08f, 0.32f, 0.22f));
            ModelKit.Box("Band West", g, new Vector3(AnnexMinX + 0.02f, 0.5f, cz), new Vector3(0.04f, 1f, AnnexMaxZ - AnnexMinZ), band, collider: false);
            ModelKit.Box("Band North", g, new Vector3(cx, 0.5f, AnnexMaxZ - 0.02f), new Vector3(AnnexMaxX - AnnexMinX, 1f, 0.04f), band, collider: false);
            ModelKit.Box("Band South", g, new Vector3(cx, 0.5f, AnnexMinZ + 0.02f), new Vector3(AnnexMaxX - AnnexMinX, 1f, 0.04f), band, collider: false);

            // Door frame (yellow) on both sides + sign over the door (storage-room side).
            foreach (float x in new[] { -20.2f, -19.8f })
            {
                ModelKit.Box("Frame L", g, new Vector3(x, AnnexDoorTop * 0.5f, AnnexDoorMinZ - 0.04f), new Vector3(0.06f, AnnexDoorTop, 0.08f), Hazard, collider: false);
                ModelKit.Box("Frame R", g, new Vector3(x, AnnexDoorTop * 0.5f, AnnexDoorMaxZ + 0.04f), new Vector3(0.06f, AnnexDoorTop, 0.08f), Hazard, collider: false);
                ModelKit.Box("Frame Top", g, new Vector3(x, AnnexDoorTop + 0.04f, (AnnexDoorMinZ + AnnexDoorMaxZ) * 0.5f), new Vector3(0.06f, 0.08f, AnnexDoorMaxZ - AnnexDoorMinZ + 0.16f), Hazard, collider: false);
            }
            LevelKit.Label("STOCKROOM ANNEX", g, new Vector3(-19.82f, 3.0f, 12.7f), Quaternion.Euler(0f, -90f, 0f), 0.26f, Accent);

            // Light
            ModelKit.Box("Annex Light Fixture", g, new Vector3(cx, AnnexH - 0.04f, cz), new Vector3(1.6f, 0.06f, 0.3f), Glow(new Color(1f, 0.96f, 0.88f), 1.5f), collider: false);
            AddLight("Annex Light", g, new Vector3(cx, AnnexH - 0.35f, cz), new Color(1f, 0.93f, 0.8f), 5f, 10f);

            // Shelving rack along the back (north) wall, full of boxes.
            BuildRack(g, new Vector3(-24.3f, 0.02f, 15.1f), 4.6f);

            // The supply crate (military green) with a lid that opens.
            BuildSupplyCrate(g, new Vector3(-25.6f, 0.02f, 11.0f));
        }

        /// A metal storage rack with cardboard boxes. 'front' = centre of the rack's base, against a wall.
        void BuildRack(Transform g, Vector3 baseCenter, float length)
        {
            var rack = new GameObject("Rack").transform;
            rack.SetParent(g, false);
            var mb = new ModelKit.MeshBuilder();
            var boxes = new ModelKit.MeshBuilder();
            const float depth = 0.55f, height = 2.2f;
            float x0 = baseCenter.x - length * 0.5f;
            for (int i = 0; i <= 3; i++) // uprights
            {
                float x = x0 + length * i / 3f;
                foreach (float dz in new[] { -depth * 0.5f, depth * 0.5f })
                    mb.AddBox(new Vector3(x, baseCenter.y + height * 0.5f, baseCenter.z + dz), new Vector3(0.05f, height, 0.05f), Quaternion.identity, Matrix4x4.identity, 1f);
            }
            var rng = new System.Random(77);
            foreach (float y in new[] { 0.12f, 0.85f, 1.55f }) // shelves
            {
                mb.AddBox(new Vector3(baseCenter.x, baseCenter.y + y, baseCenter.z), new Vector3(length, 0.04f, depth), Quaternion.identity, Matrix4x4.identity, 1f);
                for (float x = x0 + 0.3f; x < x0 + length - 0.25f; x += 0.5f + (float)rng.NextDouble() * 0.2f)
                {
                    if (rng.NextDouble() < 0.2) continue; // a few gaps: looted
                    float bw = 0.35f + (float)rng.NextDouble() * 0.15f, bh = 0.25f + (float)rng.NextDouble() * 0.3f;
                    boxes.AddBox(new Vector3(x, baseCenter.y + y + 0.02f + bh * 0.5f, baseCenter.z), new Vector3(bw, bh, 0.42f),
                                 Quaternion.Euler(0f, (float)rng.NextDouble() * 10f - 5f, 0f), Matrix4x4.identity, 1f);
                }
            }
            var frame = new GameObject("Rack Frame"); frame.transform.SetParent(rack, false);
            ModelKit.Finish(frame, mb, SurfaceMaterials.Get(Surface.PaintedMetal, new Color(0.15f, 0.3f, 0.55f)));
            var stock = new GameObject("Rack Boxes"); stock.transform.SetParent(rack, false);
            ModelKit.Finish(stock, boxes, Cardboard);
            ModelKit.ColliderBox("Rack Solid", rack, new Vector3(baseCenter.x, baseCenter.y + height * 0.5f, baseCenter.z), new Vector3(length, height, depth), Quaternion.identity);
        }

        void BuildSupplyCrate(Transform g, Vector3 floor)
        {
            var crateRoot = new GameObject("Supply Crate");
            crateRoot.transform.SetParent(g, false);
            crateRoot.transform.position = floor;
            ModelKit.Box("Crate Body", crateRoot.transform, floor + Vector3.up * 0.36f, new Vector3(1.3f, 0.72f, 0.8f), Crate);
            foreach (float x in new[] { -0.55f, 0.55f })
                ModelKit.Box("Strap", crateRoot.transform, floor + new Vector3(x, 0.36f, 0f), new Vector3(0.08f, 0.74f, 0.82f), DarkPaint, collider: false);
            LevelKit.Label("SUPPLY", crateRoot.transform, floor + new Vector3(0f, 0.38f, 0.41f), Quaternion.Euler(0f, 180f, 0f), 0.16f, new Color(0.95f, 0.92f, 0.8f));

            // Lid hinge along the back edge: rotating it lifts the lid.
            var hinge = new GameObject("Lid Hinge").transform;
            hinge.SetParent(crateRoot.transform, false);
            hinge.position = floor + new Vector3(0f, 0.72f, -0.4f);
            ModelKit.Box("Lid", hinge, floor + new Vector3(0f, 0.75f, 0f), new Vector3(1.34f, 0.06f, 0.84f), Crate, collider: false);

            // A few things visible inside (ammo boxes + medkit).
            ModelKit.Box("Ammo", crateRoot.transform, floor + new Vector3(-0.3f, 0.62f, 0f), new Vector3(0.3f, 0.16f, 0.4f), SurfaceMaterials.Get(Surface.PaintedMetal, new Color(0.3f, 0.36f, 0.2f)), collider: false);
            ModelKit.Box("Medkit", crateRoot.transform, floor + new Vector3(0.25f, 0.62f, 0f), new Vector3(0.36f, 0.16f, 0.28f), SurfaceMaterials.Get(Surface.PaintedMetal, new Color(0.85f, 0.15f, 0.12f)), collider: false);

            var lamp = AddLight("Crate Lamp", crateRoot.transform, floor + new Vector3(0f, 1.6f, 0f), new Color(0.4f, 1f, 0.5f), 1.2f, 3.5f);
            crateRoot.AddComponent<SupplyCrate>().Setup(hinge, lamp);
        }

        void BuildMedBay()
        {
            var g = Group("Medical Bay");
            Vector3 c = new Vector3(-23.6f, 0.02f, 9.78f);
            var cabinet = new GameObject("Med Station");
            cabinet.transform.SetParent(g, false);
            cabinet.transform.position = c;
            ModelKit.Box("Cabinet", cabinet.transform, c + Vector3.up * 1.0f, new Vector3(1.4f, 2.0f, 0.5f), WhitePaint);
            ModelKit.Box("Cross V", cabinet.transform, c + new Vector3(0f, 1.35f, 0.26f), new Vector3(0.14f, 0.46f, 0.02f), Glow(new Color(1f, 0.15f, 0.12f), 2f), collider: false);
            ModelKit.Box("Cross H", cabinet.transform, c + new Vector3(0f, 1.35f, 0.26f), new Vector3(0.46f, 0.14f, 0.02f), Glow(new Color(1f, 0.15f, 0.12f), 2f), collider: false);
            ModelKit.Box("Glass", cabinet.transform, c + new Vector3(0f, 0.65f, 0.255f), new Vector3(1.2f, 0.7f, 0.01f), Glow(new Color(0.55f, 0.85f, 0.8f), 0.6f), collider: false);
            LevelKit.Label("MEDICAL BAY", g, c + new Vector3(0f, 2.3f, 0.26f), Quaternion.Euler(0f, 180f, 0f), 0.2f, new Color(1f, 0.4f, 0.35f));
            var lamp = AddLight("Med Lamp", cabinet.transform, c + new Vector3(0f, 1.4f, 0.8f), new Color(1f, 0.35f, 0.3f), 1.6f, 4f);
            cabinet.AddComponent<MedStation>().Setup(lamp);

            // A stretcher bed (decoration with a collider so you don't walk through it).
            Vector3 b = new Vector3(-25.8f, 0.02f, 12.9f);
            ModelKit.Box("Stretcher", g, b + Vector3.up * 0.55f, new Vector3(0.75f, 0.1f, 1.9f), WhitePaint);
            ModelKit.Box("Mattress", g, b + Vector3.up * 0.65f, new Vector3(0.7f, 0.1f, 1.8f), SurfaceMaterials.Get(Surface.Fabric, new Color(0.55f, 0.65f, 0.75f)), collider: false);
            foreach (var o in new[] { new Vector3(-0.3f, 0f, -0.85f), new Vector3(0.3f, 0f, -0.85f), new Vector3(-0.3f, 0f, 0.85f), new Vector3(0.3f, 0f, 0.85f) })
                ModelKit.Cylinder("Leg", g, b + o + Vector3.up * 0.25f, 0.025f, 0.5f, DarkPaint, 8);
        }

        /// Replaces the piece of the store's LEFT wall (x = −20) that covers the doorway with pieces around it
        /// (same trick the armory uses on the back wall).
        static void CutDoorwayInLeftWall(float zMin, float zMax, float top)
        {
            float zMid = (zMin + zMax) * 0.5f;
            foreach (var r in LevelKit.FindAll<MeshRenderer>())
            {
                if (!r.name.StartsWith("Left Wall") || !r.gameObject.activeInHierarchy) continue;
                // Grey-box walls are scaled cubes: position = centre, scale = size.
                Vector3 c = r.transform.position, s = r.transform.lossyScale;
                if (Mathf.Abs(c.x - (-20f)) > 0.5f) continue;
                if (zMid < c.z - s.z * 0.5f || zMid > c.z + s.z * 0.5f) continue;
                if (1.3f < c.y - s.y * 0.5f || 1.3f > c.y + s.y * 0.5f) continue;

                float z0 = c.z - s.z * 0.5f, z1 = c.z + s.z * 0.5f, y0 = c.y - s.y * 0.5f, y1 = c.y + s.y * 0.5f;
                Material mat = r.sharedMaterial;
                Transform parent = r.transform.parent;
                WallPiece(parent, "Left Wall (before annex door)", c.x, s.x, z0, zMin, y0, y1, mat);
                WallPiece(parent, "Left Wall (after annex door)", c.x, s.x, zMax, z1, y0, y1, mat);
                WallPiece(parent, "Left Wall (above annex door)", c.x, s.x, zMin, zMax, top, y1, mat);
                r.gameObject.SetActive(false); // off right away (the floor map is re-made this frame)
                Destroy(r.gameObject);
                return;
            }
            Debug.LogWarning("[Upgrades] Couldn't find the store's left wall behind the storage room. Did the layout change?");
        }

        static void WallPiece(Transform parent, string name, float x, float thick, float z0, float z1, float y0, float y1, Material mat)
        {
            if (z1 - z0 < 0.01f || y1 - y0 < 0.01f) return;
            var go = LevelKit.Box(name, null, new Vector3(x, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(thick, y1 - y0, z1 - z0), mat);
            go.transform.SetParent(parent, true);
            StoreRetexture.RetextureOne(go.GetComponent<MeshRenderer>());
        }

        // ───────────────────────── Shared builders: stairs & railings ─────────────────────────

        /// <summary>
        /// Stairs from 'bottom' to 'top' (both = middle of the stair's width, on the walking surface).
        ///   what you SEE:  steel treads + risers + side beams (+ handrails)
        ///   what you WALK on: one smooth invisible ramp underneath (no stumbling on steps, zombies can use it too)
        /// railLeft / railRight = handrail (and an invisible wall) on that side, looking up the stairs.
        /// </summary>
        static void BuildStairs(string name, Transform parent, Vector3 bottom, Vector3 top, float width, bool railLeft, bool railRight)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);

            Vector3 rise = top - bottom;
            Vector3 flat = new Vector3(rise.x, 0f, rise.z);
            float run = flat.magnitude, height = rise.y;
            Vector3 h = flat / run;                                   // horizontal direction up the stairs
            Vector3 across = Vector3.Cross(Vector3.up, h);           // to the right, looking up the stairs
            Quaternion facing = Quaternion.LookRotation(h);
            Quaternion slope = Quaternion.LookRotation(rise.normalized, Vector3.up);
            float length = rise.magnitude;

            // The walkable ramp (invisible).
            Vector3 mid = (bottom + top) * 0.5f;
            ModelKit.ColliderBox("Ramp (walk surface)", g, mid - (slope * Vector3.up) * 0.1f, new Vector3(width, 0.2f, length + 0.5f), slope);

            // The visible steps.
            int n = Mathf.Max(2, Mathf.RoundToInt(height / 0.2f));
            float d = run / n, rs = height / n;
            var treads = new ModelKit.MeshBuilder();
            for (int i = 0; i < n; i++)
            {
                Vector3 front = bottom + h * (d * i);
                treads.AddBox(front + h * (d * 0.5f) + Vector3.up * ((i + 1) * rs - 0.025f), new Vector3(width, 0.05f, d + 0.02f), facing, Matrix4x4.identity, 0.5f);
                treads.AddBox(front + Vector3.up * ((i + 0.5f) * rs), new Vector3(width, rs, 0.03f), facing, Matrix4x4.identity, 0.5f);
            }
            var stepsGo = new GameObject("Steps"); stepsGo.transform.SetParent(g, false);
            ModelKit.Finish(stepsGo, treads, Plate);

            // Side beams (stringers) and handrails.
            var frame = new ModelKit.MeshBuilder();
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 off = across * (side * (width * 0.5f + 0.04f));
                frame.AddBox(mid + off - Vector3.up * 0.05f, new Vector3(0.08f, 0.32f, length), slope, Matrix4x4.identity, 1f);
                bool rail = side > 0f ? railRight : railLeft;
                if (!rail) continue;
                int posts = Mathf.Max(2, Mathf.CeilToInt(run / 2f) + 1);
                for (int i = 0; i < posts; i++)
                {
                    float t = (float)i / (posts - 1);
                    Vector3 p = bottom + rise * t + off;
                    frame.AddStick(p, p + Vector3.up * 1.0f, 0.05f, Matrix4x4.identity, 1f);
                }
                frame.AddStick(bottom + off + Vector3.up * 1.0f, top + off + Vector3.up * 1.0f, 0.05f, Matrix4x4.identity, 1f);
                frame.AddStick(bottom + off + Vector3.up * 0.5f, top + off + Vector3.up * 0.5f, 0.035f, Matrix4x4.identity, 1f);
                // Invisible wall so you can't fall off that side.
                ModelKit.ColliderBox("Rail (solid)", g, mid + off + Vector3.up * 0.6f, new Vector3(0.08f, 1.2f, length), slope);
            }
            // Support posts under the stairs every ~4 m.
            for (float t = 0.3f; t < 0.95f; t += 0.35f)
            {
                Vector3 p = bottom + rise * t;
                float postH = p.y - bottom.y; // from the floor under the stairs up to the steps
                if (postH < 0.4f) continue;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 basePt = new Vector3(p.x, bottom.y, p.z) + across * (side * width * 0.42f);
                    frame.AddBox(basePt + Vector3.up * (postH * 0.5f - 0.1f), new Vector3(0.1f, postH - 0.2f, 0.1f), facing, Matrix4x4.identity, 1f);
                }
            }
            var frameGo = new GameObject("Frame"); frameGo.transform.SetParent(g, false);
            ModelKit.Finish(frameGo, frame, DarkPaint);
        }

        /// A handrail from a to b (both on the floor), with an invisible wall so you can't fall off.
        static void Railing(Transform parent, Vector3 a, Vector3 b)
        {
            var mb = new ModelKit.MeshBuilder();
            Vector3 d = b - a;
            float len = d.magnitude;
            int posts = Mathf.Max(2, Mathf.CeilToInt(len / 1.5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / (posts - 1));
                mb.AddStick(p, p + Vector3.up * 1.05f, 0.05f, Matrix4x4.identity, 1f);
            }
            mb.AddStick(a + Vector3.up * 1.05f, b + Vector3.up * 1.05f, 0.05f, Matrix4x4.identity, 1f);
            mb.AddStick(a + Vector3.up * 0.52f, b + Vector3.up * 0.52f, 0.035f, Matrix4x4.identity, 1f);
            mb.AddBox((a + b) * 0.5f + Vector3.up * 0.06f, new Vector3(0.02f, 0.12f, len), Quaternion.LookRotation(d), Matrix4x4.identity, 1f); // kick plate
            var go = new GameObject("Railing");
            go.transform.SetParent(parent, false);
            ModelKit.Finish(go, mb, Hazard);
            ModelKit.ColliderBox("Railing (solid)", go.transform, (a + b) * 0.5f + Vector3.up * 0.6f, new Vector3(0.08f, 1.2f, len), Quaternion.LookRotation(d));
        }
    }
}
