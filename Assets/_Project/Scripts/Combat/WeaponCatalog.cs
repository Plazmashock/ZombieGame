using System.Collections.Generic;
using UnityEngine;
using LastCheckout.Items;

namespace LastCheckout.Combat
{
    public enum WeaponKind { Melee, Gun, Bow }

    /// <summary>
    /// The three loadout slots (like CS / Valorant). You carry ONE weapon in each:
    ///   Primary (key 1)  SMG, shotgun, rifle, bow
    ///   Sidearm (key 2)  pistol, revolver
    ///   Melee   (key 3)  knife, bat, machete, fire axe
    /// Buying a weapon for a slot you already filled swaps it (the old one is sold back for half price).
    /// </summary>
    public enum WeaponGroup { Primary = 0, Sidearm = 1, Melee = 2 }

    /// <summary>
    /// Everything that makes one weapon feel different. One of these per weapon.
    /// You own a weapon once you BUY it in the armory (see PlayerWallet / ShopCounter).
    /// Melee weapons you find in the store (bat, knife) also count while they're in your backpack.
    /// </summary>
    public class WeaponSpec
    {
        public string id;              // "pistol", "bat"...
        public string displayName;     // shown on the HUD and in the shop
        public int slot;               // number key 1-10 (10 = the 0 key)
        public WeaponKind kind;
        public WeaponGroup group;      // which loadout slot (key 1-3) this weapon lives in
        public int price;              // dollars
        public string blurb;           // one line for the shop

        // Guns & bow
        public ItemDefinition gunItem;     // optional: carrying this item also counts as owning it (store loot)
        public ItemDefinition ammoItem;
        public string ammoName;            // "9MM"
        public int ammoPack;               // rounds per purchase
        public int ammoPrice;              // dollars per pack
        public float damage;               // per pellet / bullet / swing / full-draw arrow
        public int pellets = 1;
        public float spreadDegrees;
        public float fireInterval;         // seconds between shots or swings
        public bool automatic;
        public int magazineSize;
        public float reloadTime;
        public float range;
        public float noiseRadius;          // how far zombies hear it
        public int pierce;                 // extra zombies one bullet/arrow passes through
        public float knockback;            // shove (m/s) on hit
        public float stun;                 // seconds the zombie staggers on hit

        // Melee
        public float reach;
        public float swingAngle;
        public int maxTargets = 1;
        public string lootItemId;          // store loot item that unlocks this melee weapon while carried

        // Bow
        public float drawTime;
        public float projectileSpeed;

        public bool IsMelee => kind == WeaponKind.Melee;
    }

    /// <summary>
    /// The 10 weapons. Keys 1-9 and 0, or scroll the mouse wheel.
    ///
    ///   MELEE (quiet, no ammo)                         RANGED
    ///   1 KNIFE    $40   fastest, short reach          5 PISTOL    $100  reliable, cheap 9mm
    ///   2 BAT      $75   wide swing, knocks back       6 REVOLVER  $220  huge damage, bullet goes through 2
    ///   3 MACHETE  $110  hits up to 3 in a wide arc    7 BOW       $160  SILENT, hold to draw, arrow pierces
    ///   4 FIRE AXE $180  slow, massive hit, staggers   8 SMG       $320  full-auto spray, shares 9mm
    ///                                                  9 SHOTGUN   $400  8 pellets, shove, close range
    ///                                                  0 RIFLE     $600  full-auto, long range, pierces
    ///
    /// You start with $125 = a pistol + one 12-round box of 9mm.
    /// </summary>
    public static class WeaponCatalog
    {
        static readonly Dictionary<string, WeaponSpec> specs = new Dictionary<string, WeaponSpec>();
        static readonly List<WeaponSpec> ordered = new List<WeaponSpec>();

        /// <summary>All weapons, in slot order (1..10).</summary>
        public static IReadOnlyList<WeaponSpec> All => ordered;

        // Statics survive between Play sessions when "domain reload" is off, so start clean every time.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { specs.Clear(); ordered.Clear(); }

        /// <summary>Builds the list once. The pistol's items come from PlayerShooting (your existing assets).</summary>
        public static void Init(ItemDefinition pistolItem, ItemDefinition pistolAmmo)
        {
            if (ordered.Count > 0) return;

            // ── Melee ──
            Add(new WeaponSpec
            {
                id = "knife", displayName = "KNIFE", slot = 1, kind = WeaponKind.Melee, price = 40,
                blurb = "Fastest weapon. Short reach.", lootItemId = "kitchen_knife",
                damage = 28f, fireInterval = 0.32f, reach = 1.8f, swingAngle = 35f, maxTargets = 1,
                knockback = 0.5f, stun = 0.1f, noiseRadius = 2f,
            });
            Add(new WeaponSpec
            {
                id = "bat", displayName = "BASEBALL BAT", slot = 2, kind = WeaponKind.Melee, price = 75,
                blurb = "Wide swing. Knocks zombies back.", lootItemId = "baseball_bat",
                damage = 45f, fireInterval = 0.62f, reach = 2.4f, swingAngle = 55f, maxTargets = 2,
                knockback = 4.5f, stun = 0.5f, noiseRadius = 3f,
            });
            Add(new WeaponSpec
            {
                id = "machete", displayName = "MACHETE", slot = 3, kind = WeaponKind.Melee, price = 110,
                blurb = "Cleaves through up to 3 at once.",
                damage = 52f, fireInterval = 0.45f, reach = 2.2f, swingAngle = 75f, maxTargets = 3,
                knockback = 1f, stun = 0.2f, noiseRadius = 3f,
            });
            Add(new WeaponSpec
            {
                id = "axe", displayName = "FIRE AXE", slot = 4, kind = WeaponKind.Melee, price = 180,
                blurb = "Slow. One hit kills most zombies.",
                damage = 115f, fireInterval = 1.0f, reach = 2.5f, swingAngle = 40f, maxTargets = 2,
                knockback = 2.5f, stun = 1.0f, noiseRadius = 4f,
            });

            // ── Ranged ──
            var arrows = MakeItem("arrows", "Arrows", 0.05f, new Vector3(0.7f, 0.04f, 0.04f), new Color(0.55f, 0.4f, 0.22f), "Hunting arrows.", "Ammo for the bow.");
            var magnum = MakeItem("magnum_ammo", ".357 Rounds", 0.03f, new Vector3(0.1f, 0.06f, 0.08f), new Color(0.75f, 0.6f, 0.25f), ".357 magnum rounds.", "Ammo for the revolver.");
            var shells = MakeItem("shotgun_shells", "Shotgun Shells", 0.06f, new Vector3(0.16f, 0.08f, 0.1f), new Color(0.75f, 0.15f, 0.12f), "12-gauge buckshot.", "Ammo for the shotgun.");
            var rifleAmmo = MakeItem("rifle_ammo", "Rifle Ammo", 0.03f, new Vector3(0.16f, 0.08f, 0.1f), new Color(0.35f, 0.55f, 0.25f), "5.56mm rounds.", "Ammo for the rifle.");

            Add(new WeaponSpec
            {
                id = "pistol", displayName = "PISTOL", slot = 5, kind = WeaponKind.Gun, price = 100,
                blurb = "Reliable. Cheap ammo.",
                gunItem = pistolItem, ammoItem = pistolAmmo, ammoName = "9MM", ammoPack = 12, ammoPrice = 25,
                damage = 34f, spreadDegrees = 0.6f, fireInterval = 0.28f, magazineSize = 12, reloadTime = 1.4f,
                range = 60f, noiseRadius = 30f, knockback = 0.4f, stun = 0.05f,
            });
            Add(new WeaponSpec
            {
                id = "revolver", displayName = "REVOLVER", slot = 6, kind = WeaponKind.Gun, price = 220,
                blurb = "Huge damage. Bullet goes through 2.",
                ammoItem = magnum, ammoName = ".357", ammoPack = 6, ammoPrice = 30,
                damage = 90f, spreadDegrees = 0.3f, fireInterval = 0.55f, magazineSize = 6, reloadTime = 2.4f,
                range = 70f, noiseRadius = 38f, pierce = 1, knockback = 1.5f, stun = 0.25f,
            });
            Add(new WeaponSpec
            {
                id = "bow", displayName = "BOW", slot = 7, kind = WeaponKind.Bow, price = 160,
                blurb = "SILENT. Hold to draw, release to fire.",
                ammoItem = arrows, ammoName = "ARROWS", ammoPack = 8, ammoPrice = 20,
                damage = 130f, spreadDegrees = 0.15f, fireInterval = 0.2f, magazineSize = 1, reloadTime = 0.45f,
                range = 80f, noiseRadius = 2f, pierce = 1, knockback = 1f, stun = 0.3f,
                drawTime = 0.7f, projectileSpeed = 55f,
            });
            Add(new WeaponSpec
            {
                id = "smg", displayName = "SMG", slot = 8, kind = WeaponKind.Gun, price = 320,
                blurb = "Full-auto spray. Uses 9mm.",
                ammoItem = pistolAmmo, ammoName = "9MM", ammoPack = 12, ammoPrice = 25,
                damage = 17f, spreadDegrees = 2.2f, fireInterval = 0.065f, automatic = true, magazineSize = 30, reloadTime = 1.8f,
                range = 40f, noiseRadius = 32f, knockback = 0.2f,
            });
            Add(new WeaponSpec
            {
                id = "shotgun", displayName = "SHOTGUN", slot = 9, kind = WeaponKind.Gun, price = 400,
                blurb = "8 pellets. Shoves zombies back.",
                ammoItem = shells, ammoName = "12 GAUGE", ammoPack = 6, ammoPrice = 35,
                damage = 15f, pellets = 8, spreadDegrees = 5f, fireInterval = 0.85f, magazineSize = 6, reloadTime = 2.2f,
                range = 28f, noiseRadius = 40f, knockback = 0.8f, stun = 0.15f,
            });
            Add(new WeaponSpec
            {
                id = "rifle", displayName = "ASSAULT RIFLE", slot = 10, kind = WeaponKind.Gun, price = 600,
                blurb = "Full-auto. Long range. Pierces.",
                ammoItem = rifleAmmo, ammoName = "5.56", ammoPack = 30, ammoPrice = 60,
                damage = 32f, spreadDegrees = 1.1f, fireInterval = 0.1f, automatic = true, magazineSize = 30, reloadTime = 2.0f,
                range = 90f, noiseRadius = 45f, pierce = 1, knockback = 0.5f, stun = 0.05f,
            });
        }

        /// <summary>Lets store loot (baseball bat, kitchen knife) unlock the matching melee weapon while carried.</summary>
        public static void LinkLootItems(IEnumerable<ItemDefinition> items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item == null) continue;
                foreach (var s in ordered)
                    if (s.IsMelee && s.gunItem == null && s.lootItemId == item.id) s.gunItem = item;
            }
        }

        public static WeaponSpec Get(string id) => id != null && specs.TryGetValue(id, out var s) ? s : null;

        public static WeaponSpec BySlot(int slot)
        {
            foreach (var s in ordered) if (s.slot == slot) return s;
            return null;
        }

        /// <summary>The key that picks a loadout slot: "1" primary, "2" sidearm, "3" melee.</summary>
        public static string GroupKey(WeaponGroup group) => ((int)group + 1).ToString();
        public static string GroupName(WeaponGroup group) => group == WeaponGroup.Primary ? "PRIMARY" : group == WeaponGroup.Sidearm ? "SIDEARM" : "MELEE";

        /// <summary>The key you press for a slot ("0" for slot 10).</summary>
        public static string KeyFor(int slot) => slot == 10 ? "0" : slot.ToString();

        static void Add(WeaponSpec s)
        {
            switch (s.id)
            {
                case "pistol": case "revolver": s.group = WeaponGroup.Sidearm; break;
                case "smg": case "shotgun": case "rifle": case "bow": s.group = WeaponGroup.Primary; break;
                default: s.group = WeaponGroup.Melee; break;
            }
            specs[s.id] = s;
            ordered.Add(s);
        }

        static ItemDefinition MakeItem(string id, string name, float weight, Vector3 size, Color color, string description, string usage)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.name = id;
            item.id = id;
            item.displayName = name;
            item.category = ItemCategory.Weapon;
            item.weight = weight;
            item.worldSize = size;
            item.placeholderColor = color;
            item.description = description;
            item.usage = usage;
            return item;
        }
    }
}
