using System.Collections.Generic;
using UnityEngine;
using LastCheckout.Combat;
using LastCheckout.Interaction;
using LastCheckout.UI;

namespace LastCheckout.Game
{
    /// <summary>
    /// THE SUPPLY TRUCK in the loading bay.
    ///
    /// Every LOOT phase the jammed shutter of the loading bay rolls up and a supply truck is parked behind it.
    /// Walk into the trailer, look at an item and press E to buy it. When the wave starts the shutter closes and
    /// the truck drives off (it comes back next loot phase with fresh stock).
    ///
    /// LEFT WALL, bottom shelf  HEALS (cheap)                 LEFT WALL, top shelf  TIMED BOOSTS (cheap, 20-60 s)
    /// RIGHT WALL, bottom shelf PERMANENT UPGRADES (expensive, last the whole run, price goes up each time you buy one)
    ///
    ///   HEALS                          price   what it does
    ///   Canned Peaches                 $20     +35 HP
    ///   Bananas                        $25     +15 HP, then +1.5 HP/s for 20 s
    ///   Mineral Water                  $20     +2 HP/s for 20 s
    ///   Ribeye Steak                   $45     +4 HP/s for 12 s
    ///   Frozen Pizza                   $110    FULL heal + 15% damage for 30 s
    ///
    ///   TIMED BOOSTS
    ///   Chocolate Bar                  $25     +25 HP, +20% speed for 25 s
    ///   Energy Drink                   $35     +30% speed for 45 s
    ///   Protein Bar                    $35     +30% damage for 45 s
    ///   Black Coffee                   $30     reload 45% faster for 50 s
    ///   Orange Juice                   $35     take 25% less damage for 45 s
    ///   Hot Sauce                      $30     +35% fire / swing rate for 40 s
    ///
    ///   PERMANENT (buy up to 3 of each; each one costs 50% more than the last)
    ///   Fortified Cereal               $300    +20 max health
    ///   Protein Powder                 $450    +10% damage
    ///   Espresso Beans                 $350    reload 12% faster
    ///   Trail Mix                      $350    +6% move speed
    ///   Vitamin Gummies                $500    take 8% less damage
    ///   Beef Jerky                     $500    +10% fire / swing rate
    ///
    /// Heals and boosts: 2 of each in stock per visit. Permanent items: 1 of each per visit.
    /// Change prices / effects in <see cref="BuildCatalog"/>.
    /// </summary>
    public class LoadingBayTruck : MonoBehaviour
    {
        // ───────────────────────── Layout (world metres) ─────────────────────────
        // The store's back wall is z = 15; the loading bay opening is x 5..9. The truck backs up to it.
        const float WallLeftX = 5.35f, WallRightX = 8.65f, CentreX = 7f;
        const float RearZ = 15.7f, FrontZ = 23.2f;
        const float FloorTop = 0.12f, WallHeight = 3.0f;
        const float ShelfDepth = 0.5f;
        const float LowShelfTop = 0.62f, HighShelfTop = 1.4f;
        static readonly float[] SlotZ = { 16.85f, 18.0f, 19.15f, 20.3f, 21.45f, 22.6f };

        static readonly Color PriceColor = new Color(1f, 0.8f, 0.3f);
        static readonly Color SoldOutColor = new Color(1f, 0.4f, 0.32f);
        static readonly Color GoldColor = new Color(1f, 0.78f, 0.24f);

        // ───────────────────────── Items for sale ─────────────────────────

        public class TruckItem
        {
            public FoodDef def;
            public int basePrice;
            public bool permanent;
            public int stockPerVisit;
            public int maxBuys;          // permanent items: how many you can ever buy
            public int stock;
            public int bought;
            public int Price => permanent ? Mathf.RoundToInt(basePrice * (1f + 0.5f * bought) / 5f) * 5 : basePrice;
            public bool Maxed => permanent && bought >= maxBuys;
            public bool SoldOut => stock <= 0;
        }

        readonly List<TruckItem> items = new List<TruckItem>();
        readonly List<TruckSlot> slots = new List<TruckSlot>();

        GameDirector director;
        PlayerWallet wallet;
        Transform player;
        GameObject truckRoot;
        Transform shutter;
        bool isOpen, warned;
        float shutterOpen;   // 0 = closed, 1 = fully rolled up
        float nextRefresh;

        public bool IsOpen => isOpen;

        // ───────────────────────── Setup ─────────────────────────

        public static LoadingBayTruck Create(GameDirector d, GameObject playerObject, PlayerWallet w)
        {
            var truck = new GameObject("Supply Truck Controller").AddComponent<LoadingBayTruck>();
            truck.Init(d, playerObject, w);
            return truck;
        }

        void Init(GameDirector d, GameObject playerObject, PlayerWallet w)
        {
            director = d;
            wallet = w;
            player = playerObject.transform;
            shutter = LotBarriers.BayShutter;

            BuildCatalog();
            BuildTruck();
            truckRoot.SetActive(false);   // invisible and not part of the zombies' floor map until the first loot phase
        }

        static FoodDef Find(string name) => FoodCatalog.All.Find(f => f.name == name);

        void Offer(string name, int price, int stock = 2)
        {
            var def = Find(name);
            if (def == null) { Debug.LogWarning("[Truck] No food called " + name); return; }
            items.Add(new TruckItem { def = def, basePrice = price, stockPerVisit = stock });
        }

        void OfferNew(FoodDef def, int price, int stock = 2)
        {
            items.Add(new TruckItem { def = def, basePrice = price, stockPerVisit = stock });
        }

        void OfferPermanent(FoodDef def, int price)
        {
            items.Add(new TruckItem { def = def, basePrice = price, permanent = true, stockPerVisit = 1, maxBuys = 3 });
        }

        /// The shopping list. Prices, stock and effects are all here.
        void BuildCatalog()
        {
            // HEALS
            Offer("Canned Peaches", 20);
            Offer("Bananas", 25);
            Offer("Mineral Water", 20);
            Offer("Ribeye Steak", 45);
            Offer("Frozen Pizza", 110, 1);

            // TIMED BOOSTS (a few of the supply-drop foods, plus a truck-only chocolate so no two items do the same thing)
            var choc = Find("Chocolate Bar");
            if (choc != null)
                OfferNew(new FoodDef
                {
                    name = "Chocolate Bar", effect = "+25 HEALTH, +20% SPEED 25S", color = choc.color, heals = true, build = choc.build,
                    apply = (h, b) => { h.Heal(25f); b.Add("SUGAR RUSH", PlayerBuffs.Kind.Speed, 0.2f, 25f, new Color(0.9f, 0.5f, 0.25f)); },
                }, 25);
            Offer("Energy Drink", 35);
            Offer("Protein Bar", 35);
            Offer("Black Coffee", 30);
            Offer("Orange Juice", 35);
            Offer("Hot Sauce", 30);

            // PERMANENT UPGRADES
            var cereal = Find("Fortified Cereal");
            if (cereal != null) OfferPermanent(cereal, 300);
            OfferPermanent(Perm("Protein Powder", "+10% DAMAGE (PERMANENT)", new Color(0.95f, 0.45f, 0.15f),
                (h, b) => b.AddPermanent(PlayerBuffs.Kind.Damage, 0.10f), Tub), 450);
            OfferPermanent(Perm("Espresso Beans", "RELOAD 12% FASTER (PERMANENT)", new Color(0.5f, 0.3f, 0.15f),
                (h, b) => b.AddPermanent(PlayerBuffs.Kind.Reload, 0.12f), Bag), 350);
            OfferPermanent(Perm("Trail Mix", "+6% MOVE SPEED (PERMANENT)", new Color(0.4f, 0.8f, 0.3f),
                (h, b) => b.AddPermanent(PlayerBuffs.Kind.Speed, 0.06f), Pouch), 350);
            OfferPermanent(Perm("Vitamin Gummies", "TAKE 8% LESS DAMAGE (PERMANENT)", new Color(1f, 0.55f, 0.1f),
                (h, b) => b.AddPermanent(PlayerBuffs.Kind.Armor, 0.08f), Jar), 500);
            OfferPermanent(Perm("Beef Jerky", "+10% FIRE / SWING RATE (PERMANENT)", new Color(0.85f, 0.2f, 0.12f),
                (h, b) => b.AddPermanent(PlayerBuffs.Kind.FireRate, 0.10f), JerkyPack), 500);
        }

        static FoodDef Perm(string name, string effect, Color color, System.Action<PlayerHealth, PlayerBuffs> apply, System.Action<Transform, Color> model)
            => new FoodDef { name = name, effect = effect, color = color, apply = apply, build = t => model(t, color) };

        // ───────────────────────── Phases ─────────────────────────

        void Update()
        {
            if (director == null) return;

            bool want = director.HasStarted && director.Current == GameDirector.Phase.Loot;
            if (want && !isOpen) Arrive();
            else if (!want && isOpen) Leave();

            // Warn before the truck leaves (the shutter closes when the wave starts).
            if (isOpen && !warned && director.TimeLeft <= 8f)
            {
                warned = true;
                GameHUD.PushToast("<color=#FFB84D>SUPPLY TRUCK LEAVING IN 8 SECONDS</color>");
            }

            // Shutter rolls up / down (scales around its top edge).
            float target = isOpen ? 1f : 0f;
            if (!Mathf.Approximately(shutterOpen, target))
            {
                shutterOpen = Mathf.MoveTowards(shutterOpen, target, Time.deltaTime / 0.9f);
                ApplyShutter();
            }

            // Price tags: yellow = you can afford it, red = too expensive / sold out.
            if (isOpen && Time.time >= nextRefresh)
            {
                nextRefresh = Time.time + 0.25f;
                foreach (var s in slots) s.RefreshLabel();
            }
        }

        void ApplyShutter()
        {
            if (shutter == null) return;
            if (shutterOpen >= 0.995f) { shutter.gameObject.SetActive(false); return; }
            shutter.gameObject.SetActive(true);
            shutter.localScale = new Vector3(1f, Mathf.Max(0.03f, 1f - shutterOpen), 1f);
        }

        void Arrive()
        {
            isOpen = true;
            warned = false;
            foreach (var it in items) it.stock = it.stockPerVisit;
            truckRoot.SetActive(true);
            IgnorePlayerCollisions();
            foreach (var s in slots) s.RefreshLabel();

            GameAudio.Play(GameAudio.Sfx.CrateOpen, 0.8f);
            GameHUD.PushToast("<color=#FFC857>SUPPLY TRUCK ARRIVED</color>   loading bay, back of the store");
            UpgradeMarker.Show("SUPPLY TRUCK", "Loading bay  ·  food, drinks, buffs", new Vector3(CentreX, 2.4f, 14.2f), GoldColor, 40f);
        }

        void Leave()
        {
            isOpen = false;
            UpgradeMarker.Remove("SUPPLY TRUCK");

            // Don't leave the player locked outside the store behind the shutter.
            if (player != null && InBay(player.position))
            {
                var cc = player.GetComponent<CharacterController>();
                bool on = cc != null && cc.enabled;
                if (cc != null) cc.enabled = false;
                player.position = new Vector3(CentreX, 0.05f, 12.4f);
                if (cc != null) cc.enabled = on;
                GameHUD.PushToast("<color=#FFB84D>The truck pulled out. You were moved back inside.</color>");
            }
            truckRoot.SetActive(false);
        }

        static bool InBay(Vector3 p) => p.z > 14.95f && p.x > 4.6f && p.x < 9.2f;

        void IgnorePlayerCollisions()
        {
            var cc = player != null ? player.GetComponent<CharacterController>() : null;
            if (cc == null) return;
            foreach (var s in slots) s.IgnorePlayer(cc);
        }

        // ───────────────────────── Buying ─────────────────────────

        public string PromptFor(TruckItem it)
        {
            if (it.Maxed) return $"{it.def.name}  ·  YOU HAVE THE MAXIMUM ({it.maxBuys}/{it.maxBuys})";
            if (it.SoldOut) return $"{it.def.name}  ·  SOLD OUT  ·  new stock next loot phase";
            int cash = wallet != null ? wallet.Cash : 0;
            string text = $"Buy {it.def.name}  ·  ${it.Price}  ·  {it.def.effect}";
            if (it.permanent) text += $"  ·  {it.bought}/{it.maxBuys} bought";
            if (cash < it.Price) text += $"  ·  need ${it.Price - cash} more";
            return text;
        }

        public bool CanBuy(TruckItem it)
        {
            return isOpen && !it.Maxed && !it.SoldOut && wallet != null && wallet.Cash >= it.Price;
        }

        public void Buy(TruckItem it, PlayerInteractor who)
        {
            if (!CanBuy(it)) return;
            var health = who.GetComponent<PlayerHealth>();
            var buffs = who.GetComponent<PlayerBuffs>();
            if (health == null || buffs == null || health.IsDead) return;
            if (!wallet.TrySpend(it.Price, it.def.name.ToUpperInvariant())) return;

            it.stock--;
            it.bought++;
            it.def.apply(health, buffs);
            GameAudio.Play(GameAudio.Sfx.Eat, 0.9f);
            GameHUD.PushToast($"<color=#7CFF8A>{it.def.effect}</color>   {it.def.name.ToUpperInvariant()}");
            foreach (var s in slots) s.RefreshLabel();
        }

        // ───────────────────────── Building the truck ─────────────────────────

        static Material Mat(Color c, float smooth = 0.3f, float glow = 0f) => LevelKit.ColorMaterial(c, smooth, glow);

        GameObject Box(string name, Vector3 center, Vector3 size, Color c, bool collider = false, float smooth = 0.3f, float glow = 0f)
            => LevelKit.Box(name, truckRoot.transform, center, size, Mat(c, smooth, glow), null, collider);

        void Cyl(string name, Vector3 center, float radius, float length, Vector3 euler, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            LevelKit.RemoveCollider(go);
            go.name = name;
            go.transform.SetParent(truckRoot.transform, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Mat(c, 0.2f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void AddLight(string name, Vector3 pos, Color c, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(truckRoot.transform, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        void BuildTruck()
        {
            truckRoot = new GameObject("Supply Truck");
            truckRoot.transform.SetParent(transform, false);

            Color white = new Color(0.88f, 0.9f, 0.9f), steel = new Color(0.25f, 0.27f, 0.3f), green = new Color(0.1f, 0.4f, 0.26f);
            float len = FrontZ - RearZ, midZ = (RearZ + FrontZ) * 0.5f;

            // Trailer shell: floor, two side walls, front wall, roof. The back (towards the store) is open.
            Box("Trailer Floor", new Vector3(CentreX, FloorTop * 0.5f, midZ), new Vector3(3.5f, FloorTop, len), new Color(0.33f, 0.33f, 0.34f), true);
            Box("Dock Plate", new Vector3(CentreX, FloorTop * 0.5f, 15.4f), new Vector3(3.5f, FloorTop, 0.7f), new Color(0.4f, 0.4f, 0.42f), true);   // closes the gap between the store wall and the trailer
            Box("Trailer Wall Left", new Vector3(WallLeftX - 0.05f, FloorTop + WallHeight * 0.5f, midZ), new Vector3(0.1f, WallHeight, len), white, true);
            Box("Trailer Wall Right", new Vector3(WallRightX + 0.05f, FloorTop + WallHeight * 0.5f, midZ), new Vector3(0.1f, WallHeight, len), white, true);
            Box("Trailer Front Wall", new Vector3(CentreX, FloorTop + WallHeight * 0.5f, FrontZ + 0.05f), new Vector3(3.5f, WallHeight, 0.1f), white, true);
            Box("Trailer Roof", new Vector3(CentreX, FloorTop + WallHeight + 0.06f, midZ), new Vector3(3.6f, 0.12f, len + 0.1f), new Color(0.8f, 0.82f, 0.82f), true);
            // green band outside, like the store
            Box("Band Left", new Vector3(WallLeftX - 0.11f, 1.1f, midZ), new Vector3(0.03f, 0.9f, len), green);
            Box("Band Right", new Vector3(WallRightX + 0.11f, 1.1f, midZ), new Vector3(0.03f, 0.9f, len), green);
            LevelKit.Label("PRICEWELL FRESH", truckRoot.transform, new Vector3(WallLeftX - 0.14f, 1.1f, midZ), Quaternion.Euler(0f, -90f, 0f), 0.4f, Color.white);
            LevelKit.Label("PRICEWELL FRESH", truckRoot.transform, new Vector3(WallRightX + 0.14f, 1.1f, midZ), Quaternion.Euler(0f, 90f, 0f), 0.4f, Color.white);

            // Rear doors swung open against the sides, a bumper, and a header sign over the opening.
            Box("Door Left", new Vector3(WallLeftX - 0.2f, FloorTop + 1.45f, RearZ + 0.9f), new Vector3(0.05f, 2.8f, 1.7f), steel);
            Box("Door Right", new Vector3(WallRightX + 0.2f, FloorTop + 1.45f, RearZ + 0.9f), new Vector3(0.05f, 2.8f, 1.7f), steel);
            Box("Rear Bumper", new Vector3(CentreX, 0.28f, RearZ + 0.08f), new Vector3(3.5f, 0.16f, 0.16f), steel);
            LevelKit.Label("SUPPLY TRUCK", truckRoot.transform, new Vector3(CentreX, FloorTop + WallHeight - 0.05f, RearZ - 0.04f), Quaternion.identity, 0.34f, GoldColor);

            // Cab + chassis + wheels.
            Box("Cab", new Vector3(CentreX, 1.45f, FrontZ + 1.25f), new Vector3(3.3f, 2.5f, 2.3f), new Color(0.12f, 0.45f, 0.3f), true);
            Box("Windshield", new Vector3(CentreX, 1.95f, FrontZ + 2.41f), new Vector3(2.7f, 1.0f, 0.05f), new Color(0.15f, 0.22f, 0.3f), false, 0.9f);
            Box("Headlight L", new Vector3(CentreX - 1.1f, 0.75f, FrontZ + 2.42f), new Vector3(0.4f, 0.22f, 0.06f), new Color(1f, 0.95f, 0.8f), false, 0.5f, 3f);
            Box("Headlight R", new Vector3(CentreX + 1.1f, 0.75f, FrontZ + 2.42f), new Vector3(0.4f, 0.22f, 0.06f), new Color(1f, 0.95f, 0.8f), false, 0.5f, 3f);
            Box("Front Bumper", new Vector3(CentreX, 0.35f, FrontZ + 2.45f), new Vector3(3.4f, 0.3f, 0.2f), steel);
            foreach (float z in new[] { 20.6f, 21.7f, FrontZ + 1.2f })
            {
                Cyl("Wheel L", new Vector3(WallLeftX - 0.2f, 0.4f, z), 0.4f, 0.3f, new Vector3(0f, 0f, 90f), new Color(0.05f, 0.05f, 0.06f));
                Cyl("Wheel R", new Vector3(WallRightX + 0.2f, 0.4f, z), 0.4f, 0.3f, new Vector3(0f, 0f, 90f), new Color(0.05f, 0.05f, 0.06f));
            }

            // Lights inside + a warm one at the opening.
            foreach (float z in new[] { 17.3f, 19.5f, 21.7f })
            {
                Box("Light Panel", new Vector3(CentreX, FloorTop + WallHeight - 0.03f, z), new Vector3(1.2f, 0.05f, 0.3f), new Color(1f, 0.97f, 0.88f), false, 0.3f, 2.2f);
                AddLight("Truck Light", new Vector3(CentreX, FloorTop + WallHeight - 0.4f, z), new Color(1f, 0.95f, 0.85f), 2.6f, 5.5f);
            }

            BuildShelves();

            // Signs on the walls inside.
            LevelKit.Label("HEALS & FOOD", truckRoot.transform, new Vector3(WallLeftX + 0.06f, 2.7f, 19.8f), Quaternion.Euler(0f, -90f, 0f), 0.22f, new Color(0.6f, 1f, 0.65f));
            LevelKit.Label("TIMED BOOSTS", truckRoot.transform, new Vector3(WallLeftX + 0.06f, 2.35f, 19.8f), Quaternion.Euler(0f, -90f, 0f), 0.16f, new Color(0.6f, 0.85f, 1f));
            LevelKit.Label("PERMANENT UPGRADES", truckRoot.transform, new Vector3(WallRightX - 0.06f, 2.7f, 19.8f), Quaternion.Euler(0f, 90f, 0f), 0.22f, GoldColor);
            LevelKit.Label("keep for the whole run  ·  price rises each time", truckRoot.transform, new Vector3(WallRightX - 0.06f, 2.38f, 19.8f), Quaternion.Euler(0f, 90f, 0f), 0.1f, Color.white);
        }

        void BuildShelves()
        {
            Color rack = new Color(0.15f, 0.3f, 0.55f), board = new Color(0.55f, 0.57f, 0.6f);
            float zA = SlotZ[0] - 0.7f, zB = SlotZ[SlotZ.Length - 1] + 0.7f, zMid = (zA + zB) * 0.5f, zLen = zB - zA;

            // Two shelf boards per side + uprights.
            foreach (bool left in new[] { true, false })
            {
                float wallX = left ? WallLeftX : WallRightX;
                float dir = left ? 1f : -1f;                      // towards the aisle
                float boardX = wallX + dir * ShelfDepth * 0.5f;
                foreach (float top in new[] { LowShelfTop, HighShelfTop })
                {
                    Box("Shelf Board", new Vector3(boardX, top - 0.03f, zMid), new Vector3(ShelfDepth, 0.05f, zLen), board, true, 0.4f);
                    Box("Shelf Lip", new Vector3(wallX + dir * (ShelfDepth - 0.01f), top + 0.02f, zMid), new Vector3(0.03f, 0.05f, zLen), rack);
                }
                for (int i = 0; i <= SlotZ.Length; i++)
                {
                    float z = i == 0 ? zA : i == SlotZ.Length ? zB : (SlotZ[i - 1] + SlotZ[i]) * 0.5f;
                    Box("Upright", new Vector3(wallX + dir * (ShelfDepth - 0.03f), (FloorTop + HighShelfTop + 0.8f) * 0.5f, z), new Vector3(0.05f, HighShelfTop + 0.8f - FloorTop, 0.05f), rack);
                }
            }

            // Fill the slots.
            var heals = new List<TruckItem>();
            var boosts = new List<TruckItem>();
            var perms = new List<TruckItem>();
            foreach (var it in items)
            {
                if (it.permanent) perms.Add(it);
                else if (it.def.name == "Chocolate Bar" || it.def.name == "Energy Drink" || it.def.name == "Protein Bar" ||
                         it.def.name == "Black Coffee" || it.def.name == "Orange Juice" || it.def.name == "Hot Sauce") boosts.Add(it);
                else heals.Add(it);
            }
            PlaceRow(heals, true, LowShelfTop);
            PlaceRow(boosts, true, HighShelfTop);
            PlaceRow(perms, false, LowShelfTop);

            // Cardboard boxes on the empty shelves: decoration.
            Color card = new Color(0.55f, 0.42f, 0.26f);
            foreach (float z in SlotZ)
            {
                Box("Spare Box", new Vector3(WallRightX - ShelfDepth * 0.5f, HighShelfTop + 0.2f, z), new Vector3(0.4f, 0.36f, 0.7f), card, false, 0.1f);
                if (z > SlotZ[4]) Box("Spare Box", new Vector3(WallLeftX + ShelfDepth * 0.5f, LowShelfTop + 0.2f, z), new Vector3(0.4f, 0.36f, 0.7f), card, false, 0.1f);
            }
        }

        void PlaceRow(List<TruckItem> row, bool left, float shelfTop)
        {
            float wallX = left ? WallLeftX : WallRightX;
            float dir = left ? 1f : -1f;
            for (int i = 0; i < row.Count && i < SlotZ.Length; i++)
            {
                var it = row[i];
                var go = new GameObject("Slot: " + it.def.name);
                go.transform.SetParent(truckRoot.transform, false);
                float itemX = wallX + dir * ShelfDepth * 0.5f;
                go.transform.position = new Vector3(itemX, shelfTop, SlotZ[i]);

                // Coloured backing plate on the wall so each item reads from the aisle.
                Box("Backing", new Vector3(wallX + dir * 0.02f, shelfTop + 0.33f, SlotZ[i]), new Vector3(0.03f, 0.62f, 0.9f),
                    new Color(it.def.color.r * 0.55f, it.def.color.g * 0.55f, it.def.color.b * 0.55f), false, 0.2f, 0.6f);

                var slot = go.AddComponent<TruckSlot>();
                slot.Setup(this, it, left, it.permanent ? GoldColor : PriceColor);
                slots.Add(slot);
            }
        }

        // ───────────────────────── Models for the new permanent foods ─────────────────────────

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Color color, float smooth = 0.3f, float glow = 0f)
        {
            var go = GameObject.CreatePrimitive(type);
            LevelKit.RemoveCollider(go);
            go.name = "Part";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = LevelKit.ColorMaterial(color, smooth, glow);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static void Tub(Transform t, Color c)       // protein powder tub
        {
            Part(t, PrimitiveType.Cylinder, Vector3.zero, Vector3.zero, new Vector3(0.17f, 0.095f, 0.17f), new Color(0.12f, 0.12f, 0.14f), 0.5f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, -0.005f, 0f), Vector3.zero, new Vector3(0.175f, 0.06f, 0.175f), c, 0.35f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(0.18f, 0.012f, 0.18f), new Color(0.05f, 0.05f, 0.06f), 0.5f);
        }

        static void Bag(Transform t, Color c)       // coffee beans bag
        {
            Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0f, 12f, 0f), new Vector3(0.14f, 0.22f, 0.07f), c, 0.3f);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.015f, 0f), new Vector3(0f, 12f, 0f), new Vector3(0.1f, 0.08f, 0.075f), new Color(0.95f, 0.8f, 0.3f), 0.4f);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.115f, 0f), new Vector3(0f, 12f, 0f), new Vector3(0.14f, 0.02f, 0.06f), new Color(0.15f, 0.1f, 0.06f), 0.3f);
        }

        static void Pouch(Transform t, Color c)     // trail mix pouch
        {
            Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0f, -10f, 0f), new Vector3(0.16f, 0.2f, 0.05f), c, 0.3f);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(0f, -10f, 0f), new Vector3(0.11f, 0.09f, 0.055f), new Color(0.95f, 0.55f, 0.15f), 0.4f);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.105f, 0f), new Vector3(0f, -10f, 0f), new Vector3(0.16f, 0.015f, 0.04f), Color.white, 0.3f);
        }

        static void Jar(Transform t, Color c)       // gummy vitamins jar
        {
            Part(t, PrimitiveType.Cylinder, Vector3.zero, Vector3.zero, new Vector3(0.13f, 0.085f, 0.13f), c, 0.6f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.09f, 0f), Vector3.zero, new Vector3(0.135f, 0.02f, 0.135f), Color.white, 0.4f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, -0.005f, 0f), Vector3.zero, new Vector3(0.134f, 0.04f, 0.134f), new Color(0.3f, 0.8f, 0.3f), 0.35f);
        }

        static void JerkyPack(Transform t, Color c) // beef jerky pack
        {
            Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0f, 8f, 0f), new Vector3(0.15f, 0.21f, 0.04f), c, 0.3f);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0f, 8f, 0f), new Vector3(0.1f, 0.08f, 0.045f), new Color(1f, 0.82f, 0.2f), 0.4f);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.11f, 0f), new Vector3(0f, 8f, 0f), new Vector3(0.15f, 0.015f, 0.035f), new Color(0.12f, 0.08f, 0.06f), 0.3f);
        }

        // ───────────────────────── One thing on a shelf ─────────────────────────

        /// <summary>The item on the shelf: bobs a little, has name + price labels, and is what you look at and press E on.</summary>
        public class TruckSlot : MonoBehaviour, IInteractable
        {
            LoadingBayTruck truck;
            TruckItem item;
            Transform visual;
            Vector3 visualBase;
            float phase;
            TextMesh priceLabel;
            BoxCollider col;
            Color normalPrice;

            public void Setup(LoadingBayTruck owner, TruckItem it, bool leftWall, Color priceColor)
            {
                truck = owner;
                item = it;
                normalPrice = priceColor;
                phase = Random.value * 6.28f;

                float dir = leftWall ? 1f : -1f;
                Quaternion face = Quaternion.Euler(0f, leftWall ? -90f : 90f, 0f);   // text readable from the aisle

                var holder = new GameObject("Model").transform;
                holder.SetParent(transform, false);
                holder.localPosition = new Vector3(0f, 0.24f, 0f);
                it.def.build(holder);
                holder.localScale = Vector3.one * 1.7f;
                visual = holder;
                visualBase = holder.localPosition;

                // Name above, price on the shelf lip.
                var name = LevelKit.Label(it.def.name.ToUpperInvariant(), transform, transform.position + new Vector3(dir * 0.2f, 0.62f, 0f), face, 0.075f, Color.white);
                priceLabel = LevelKit.Label("", transform, transform.position + new Vector3(dir * 0.24f, -0.06f, 0f), face, 0.1f, normalPrice);

                col = gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.28f, 0f);
                col.size = new Vector3(0.4f, 0.56f, 0.85f);
            }

            public void IgnorePlayer(CharacterController cc)
            {
                if (col != null && cc != null) Physics.IgnoreCollision(cc, col);
            }

            void Update()
            {
                if (visual == null) return;
                float t = Time.time + phase;
                visual.localPosition = visualBase + new Vector3(0f, Mathf.Sin(t * 2f) * 0.012f, 0f);
                visual.localRotation = Quaternion.Euler(0f, t * 35f, 0f);
            }

            public void RefreshLabel()
            {
                if (priceLabel == null || item == null) return;
                if (item.Maxed) { priceLabel.text = "MAX"; priceLabel.color = SoldOutColor; return; }
                if (item.SoldOut) { priceLabel.text = "SOLD OUT"; priceLabel.color = SoldOutColor; return; }
                bool afford = truck.wallet != null && truck.wallet.Cash >= item.Price;
                priceLabel.text = item.permanent ? $"${item.Price}" : $"${item.Price}  x{item.stock}";
                priceLabel.color = afford ? normalPrice : SoldOutColor;
            }

            public string GetPrompt(PlayerInteractor who) => truck.PromptFor(item);
            public bool CanInteract(PlayerInteractor who) => truck.CanBuy(item);
            public void Interact(PlayerInteractor who) => truck.Buy(item, who);
        }
    }
}
