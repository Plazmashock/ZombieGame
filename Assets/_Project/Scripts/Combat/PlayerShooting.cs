using System.Collections.Generic;
using UnityEngine;
using LastCheckout.Inventory;
using LastCheckout.Items;
using LastCheckout.Player;

namespace LastCheckout.Combat
{
    /// <summary>
    /// Holds and fires your weapons. Controls:
    ///   1 = primary, 2 = sidearm, 3 = melee (one weapon per slot, like CS / Valorant)
    ///   Q = swap back to the previous weapon      Mouse wheel = next / previous weapon
    ///   Left click = shoot (hold for SMG / rifle)    BOW: hold to draw, release to fire    R = reload
    ///
    /// Weapon stats live in WeaponCatalog. You own a weapon once you buy it (PlayerWallet).
    /// Ammo is a real item in your backpack. Each gun remembers its own magazine; guns are bought EMPTY
    /// and load themselves from your backpack when you take them out.
    /// Melee weapons are equipped here too, but PlayerMelee does the swinging.
    /// </summary>
    public class PlayerShooting : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerLook look;
        [SerializeField] PlayerInventory inventory;
        [Tooltip("Where shots start and point from. Use the player camera.")]
        [SerializeField] Transform viewPoint;

        [Header("Pistol items (the data cards for the pistol and its bullets)")]
        [SerializeField] ItemDefinition gunItem;
        [SerializeField] ItemDefinition ammoItem;

        [Header("Debug")]
        [Tooltip("Prints what every shot hit into the Console tab. Handy for finding problems.")]
        [SerializeField] bool logShots = false;

        // ---------- What the HUD and other scripts read ----------

        /// <summary>The weapon in your hands, or null for bare fists.</summary>
        public WeaponSpec Current { get; private set; }
        /// <summary>True when a GUN or BOW is out (left click shoots instead of punching).</summary>
        public bool IsEquipped => Current != null;
        public bool HoldingMelee => Current != null && Current.IsMelee;
        public string CurrentWeaponId => Current != null ? Current.id : null;
        public int Loaded => Current != null ? LoadedOf(Current) : 0;
        public int Reserve => Current != null ? ReserveOf(Current) : 0;
        public bool IsReloading => reloading;
        /// <summary>0 → 1 while reloading.</summary>
        public float ReloadProgress => reloading && Current != null
            ? Mathf.Clamp01(1f - (reloadEndTime - Time.time) / Mathf.Max(0.01f, reloadDuration)) : 0f;
        /// <summary>Bow: how far the string is pulled back, 0 → 1.</summary>
        public float DrawAmount => draw;
        public bool IsDrawing => drawing;
        /// <summary>Current inaccuracy in degrees (the crosshair opens up with it).</summary>
        public float CurrentSpread => Current != null && !Current.IsMelee ? Current.spreadDegrees + bloom : 0f;
        public float LastHitTime { get; private set; } = -10f;
        public float LastKillTime { get; private set; } = -10f;

        /// <summary>Fires when you switch weapons (null = fists).</summary>
        public event System.Action<WeaponSpec> Switched;
        /// <summary>Time.time of the last weapon switch (view models and the HUD animate from it).</summary>
        public float EquippedAt { get; private set; } = -10f;

        public ItemDefinition PistolItem => gunItem;
        public ItemDefinition PistolAmmo => ammoItem;

        /// <summary>Fires on every shot or arrow. True = it hit something that can be hurt.</summary>
        public event System.Action<bool> Fired;

        // ---------- Internal state ----------

        readonly Dictionary<string, int> loaded = new Dictionary<string, int>();   // rounds in each gun's magazine
        readonly HashSet<IDamageable> hitThisPellet = new HashSet<IDamageable>();
        PlayerWallet wallet;
        bool reloading, drawing;
        float reloadEndTime, reloadDuration = 1f, nextShotTime, bloom, draw;
        WeaponSpec previous;
        PlayerBuffs buffs;
        bool cursorWasLocked;
        readonly List<GunViewModel> viewModels = new List<GunViewModel>();
        GUIStyle ammoStyle;

        void Reset() => AutoFindReferences();

        void Awake()
        {
            AutoFindReferences();
            WeaponCatalog.Init(gunItem, ammoItem);
        }

        void AutoFindReferences()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (look == null) look = GetComponent<PlayerLook>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (viewPoint == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) viewPoint = cam.transform;
            }
        }

        PlayerWallet Wallet => wallet != null ? wallet : (wallet = GetComponent<PlayerWallet>());
        PlayerBuffs Buffs => buffs != null ? buffs : (buffs = GetComponent<PlayerBuffs>());
        float DamageMult => Buffs != null ? Buffs.DamageMult : 1f;      // food buffs
        float FireRateMult => Buffs != null ? Buffs.FireRateMult : 1f;
        float ReloadMult => Buffs != null ? Buffs.ReloadMult : 1f;

        /// <summary>
        /// The weapon in a loadout slot: the one in your hands if it belongs there, else the one you carry for that slot
        /// (bought weapons first, then store loot like the bat). Null = the slot is empty.
        /// </summary>
        public WeaponSpec WeaponIn(WeaponGroup group)
        {
            if (Current != null && Current.group == group) return Current;
            WeaponSpec loot = null;
            foreach (var s in WeaponCatalog.All)
            {
                if (s.group != group || !Owns(s)) continue;
                if (Wallet != null && Wallet.Owns(s.id)) return s;
                if (loot == null) loot = s;
            }
            return loot;
        }

        public bool Owns(WeaponSpec spec)
        {
            if (spec == null) return false;
            if (Wallet != null && Wallet.Owns(spec.id)) return true;
            return spec.gunItem != null && inventory != null && inventory.CountOf(spec.gunItem) > 0;
        }

        public int LoadedOf(WeaponSpec spec) => spec != null && loaded.TryGetValue(spec.id, out int n) ? n : 0;
        public int ReserveOf(WeaponSpec spec) => spec != null && spec.ammoItem != null && inventory != null ? inventory.CountOf(spec.ammoItem) : 0;

        /// <summary>Takes out a weapon by id (used right after buying one).</summary>
        public void EquipById(string id)
        {
            var spec = WeaponCatalog.Get(id);
            if (Owns(spec)) Equip(spec);
        }

        /// <summary>Melee hits and arrows report here so the crosshair hitmarker shows for them too.</summary>
        public void RegisterHit(bool kill)
        {
            LastHitTime = Time.time;
            if (kill) LastKillTime = Time.time;
        }

        void Update()
        {
            // Only act while the mouse is captured and no menu is open.
            bool free = look.IsCursorLocked && !look.IsUIOpen;
            bool canAct = free && cursorWasLocked; // ignore the click that re-captures the mouse
            cursorWasLocked = free;

            // Accuracy recovers when you stop shooting.
            bloom = Mathf.MoveTowards(bloom, 0f, 6f * Time.deltaTime);

            // Lost the weapon you were holding (dropped the loot bat)? Back to fists.
            if (IsEquipped && !Owns(Current)) Unequip();

            if (canAct)
            {
                int key = input.SlotPressed;                       // keys 1, 2, 3 = primary, sidearm, melee
                if (key >= 1 && key <= 3) PressGroupKey((WeaponGroup)(key - 1));
                if (input.LastWeaponPressed) SwapToPrevious();
                float wheel = input.Scroll;
                if (Mathf.Abs(wheel) > 0.01f) Cycle(wheel > 0f ? -1 : 1);
            }

            if (reloading && Time.time >= reloadEndTime) FinishReload();
            if (!IsEquipped || !canAct || Current.IsMelee) { drawing = false; draw = 0f; return; }

            if (input.ReloadPressed) StartReload();

            if (Current.kind == WeaponKind.Bow) { UpdateBow(); return; }

            bool trigger = Current.automatic ? input.ClickHeld : input.ClickPressed;
            if (trigger && !reloading && Time.time >= nextShotTime)
            {
                if (LoadedOf(Current) > 0) Shoot();
                else if (input.ClickPressed) StartReload(); // empty: a fresh click starts a reload
            }
        }

        /// Key 1/2/3: take out the weapon in that slot. Pressing the same key again swaps to the next weapon in that slot
        /// (only the melee slot can hold two: a bought weapon + a bat you found in the store).
        void PressGroupKey(WeaponGroup group)
        {
            var list = new List<WeaponSpec>();
            foreach (var s in WeaponCatalog.All)
                if (s.group == group && Owns(s) && Wallet != null && Wallet.Owns(s.id)) list.Add(s); // bought first
            foreach (var s in WeaponCatalog.All)
                if (s.group == group && Owns(s) && !list.Contains(s)) list.Add(s);                    // then store loot
            // Fists are the last choice of the melee slot (null = bare hands).
            if (group == WeaponGroup.Melee) list.Add(null);
            if (list.Count == 0) return;

            bool inThisSlot = Current != null ? Current.group == group : group == WeaponGroup.Melee;
            if (inThisSlot)
            {
                if (list.Count > 1)
                {
                    int at = Current == null ? list.Count - 1 : list.IndexOf(Current);
                    Take(list[(at + 1) % list.Count]);
                }
                return;
            }
            Take(list[0]);
        }

        /// Takes out a weapon, or puts everything away (null) = fists.
        void Take(WeaponSpec spec)
        {
            if (spec == null) Unequip(); else Equip(spec);
        }

        /// Q: back to the weapon you had before (like CS).
        void SwapToPrevious()
        {
            if (previous != Current && (previous == null || Owns(previous))) Take(previous);
        }

        /// Mouse wheel: step through every weapon you carry, in slot order (primary, sidearm, melee).
        void Cycle(int direction)
        {
            var list = new List<WeaponSpec>();
            foreach (WeaponGroup g in System.Enum.GetValues(typeof(WeaponGroup)))
                foreach (var s in WeaponCatalog.All)
                    if (s.group == g && Owns(s)) list.Add(s);
            list.Add(null); // fists, after the melee weapons
            int index = Current == null ? list.Count - 1 : list.IndexOf(Current);
            index = (index + direction + list.Count) % list.Count;
            if (list[index] != Current) Take(list[index]);
        }

        void Equip(WeaponSpec spec)
        {
            if (Current != spec) { previous = Current; EquippedAt = Time.time; }
            Current = spec;
            reloading = false;
            drawing = false;
            draw = 0f;
            bloom = 0f;
            nextShotTime = Time.time + 0.25f; // short "taking it out" delay

            // Bought guns come empty: load straight away if there's ammo in the backpack.
            if (!spec.IsMelee && LoadedOf(spec) == 0 && ReserveOf(spec) > 0) StartReload();
            Switched?.Invoke(spec);
        }

        void Unequip()
        {
            if (Current != null) { previous = Current; EquippedAt = Time.time; }
            Current = null;
            reloading = false; // putting it away cancels a reload
            drawing = false;
            draw = 0f;
        }

        // ---------- Reloading ----------

        void StartReload()
        {
            if (reloading || Current == null || Current.IsMelee || LoadedOf(Current) >= Current.magazineSize || Reserve <= 0) return;
            reloading = true;
            drawing = false;
            draw = 0f;
            reloadDuration = Current.reloadTime * ReloadMult;
            reloadEndTime = Time.time + reloadDuration;
        }

        void FinishReload()
        {
            reloading = false;
            if (Current == null) return;
            int have = LoadedOf(Current);
            int take = Mathf.Min(Current.magazineSize - have, Reserve);
            // Rounds leave the backpack (it gets lighter) and go into the gun.
            if (take > 0 && inventory.Remove(Current.ammoItem, take)) loaded[Current.id] = have + take;
        }

        // ---------- Bow ----------

        void UpdateBow()
        {
            var w = Current;
            // Nock the next arrow automatically.
            if (!reloading && LoadedOf(w) == 0 && ReserveOf(w) > 0) StartReload();

            if (input.ClickHeld && !reloading && LoadedOf(w) > 0 && Time.time >= nextShotTime)
            {
                drawing = true;
                draw = Mathf.MoveTowards(draw, 1f, Time.deltaTime / Mathf.Max(0.05f, w.drawTime));
            }
            else if (drawing && !input.ClickHeld)
            {
                if (draw >= 0.2f) FireArrow(draw);  // a tiny tap doesn't fire
                drawing = false;
                draw = 0f;
            }
        }

        void FireArrow(float power)
        {
            var w = Current;
            nextShotTime = Time.time + w.fireInterval / FireRateMult;
            loaded[w.id] = LoadedOf(w) - 1;

            Vector2 wobble = Random.insideUnitCircle * Mathf.Tan(w.spreadDegrees * (2f - power) * Mathf.Deg2Rad);
            Vector3 direction = (viewPoint.forward + viewPoint.right * wobble.x + viewPoint.up * wobble.y).normalized;
            float speed = w.projectileSpeed * Mathf.Lerp(0.45f, 1f, power);
            float damage = w.damage * DamageMult * Mathf.Lerp(0.3f, 1f, power);

            Arrow.Spawn(viewPoint.position + viewPoint.forward * 0.3f - viewPoint.up * 0.03f, direction * speed,
                        damage, w.pierce, w.knockback, w.stun, this);
            NoiseMaker.Emit(transform.position, w.noiseRadius); // a bow is almost silent
            Fired?.Invoke(false);
        }

        // ---------- Guns ----------

        void Shoot()
        {
            var w = Current;
            nextShotTime = Time.time + w.fireInterval / FireRateMult;
            loaded[w.id] = LoadedOf(w) - 1;

            Vector3 origin = viewPoint.position;
            Vector3 muzzle = MuzzlePosition(w, origin);
            float spread = w.spreadDegrees + bloom;

            int pelletsHit = 0, kills = 0;
            string firstHitName = null;

            for (int i = 0; i < Mathf.Max(1, w.pellets); i++)
            {
                // Aim straight ahead, plus a random wobble inside a cone.
                Vector2 wobble = Random.insideUnitCircle * Mathf.Tan(spread * Mathf.Deg2Rad);
                Vector3 direction = (viewPoint.forward + viewPoint.right * wobble.x + viewPoint.up * wobble.y).normalized;
                Vector3 endPoint = origin + direction * w.range;

                // Invisible laser through everything in its path, nearest first.
                // It stops at the first wall, or after hitting (1 + pierce) zombies.
                var hits = Physics.RaycastAll(origin, direction, w.range, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                int pierceLeft = w.pierce;
                hitThisPellet.Clear();

                foreach (var hit in hits)
                {
                    if (hit.collider.transform.IsChildOf(transform)) continue; // never your own body
                    if (firstHitName == null) firstHitName = hit.collider.name;

                    var target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target == null)
                    {
                        HitEffects.Impact(hit.point, hit.normal); // sparks + a bullet hole
                        endPoint = hit.point;
                        break;
                    }
                    if (target.IsDead || !hitThisPellet.Add(target)) continue;

                    // Safety net: if the target's script ever throws an error, log it but keep going.
                    try { target.TakeDamage(w.damage * DamageMult); }
                    catch (System.Exception e) { Debug.LogException(e); }
                    pelletsHit++;
                    if (target.IsDead) kills++;
                    else HitReaction(target, direction, w.knockback, w.stun);
                    HitEffects.Blood(hit.point, hit.normal);

                    if (pierceLeft-- <= 0) { endPoint = hit.point; break; }
                }

                BulletTrail.Spawn(muzzle, endPoint);
            }

            if (pelletsHit > 0) RegisterHit(kills > 0);
            if (w.automatic) bloom = Mathf.Min(bloom + 0.35f, 2.5f); // full-auto gets less accurate the longer you hold

            if (logShots)
                Debug.Log($"[Shoot] {w.displayName}: {(firstHitName != null ? $"first hit '{firstHitName}'" : "missed")}, {pelletsHit} hit(s), {kills} kill(s).");

            // BANG: tell every zombie in earshot. (Use our feet position, so zombies can path to it.)
            NoiseMaker.Emit(transform.position, w.noiseRadius);

            Fired?.Invoke(pelletsHit > 0);
        }

        /// <summary>Shoves / staggers a zombie that survived a hit.</summary>
        public static void HitReaction(IDamageable target, Vector3 direction, float knockback, float stun)
        {
            if (target is Enemies.Zombie z && (knockback > 0f || stun > 0f)) z.Knockback(direction, knockback, stun);
        }

        /// <summary>Where the bullet streak starts: the barrel of the gun model you're holding, else near the eyes.</summary>
        Vector3 MuzzlePosition(WeaponSpec w, Vector3 origin)
        {
            var vm = FindViewModel(w.id);
            if (vm != null && vm.Muzzle != null) return vm.Muzzle.position;
            return origin + viewPoint.right * 0.15f - viewPoint.up * 0.12f + viewPoint.forward * 0.4f;
        }

        GunViewModel FindViewModel(string id)
        {
            foreach (var vm in viewModels) if (vm != null && vm.WeaponId == id) return vm;

            // Not found: gun models may have been added after we started (built at runtime). Look again.
            viewModels.Clear();
            viewModels.AddRange(GetComponentsInChildren<GunViewModel>(true));
            foreach (var vm in viewModels) if (vm.WeaponId == id) return vm;
            return null;
        }

        // ---------- Fallback HUD (only used if the new GameHUD isn't running) ----------

        void OnGUI()
        {
            if (UI.GameHUD.IsActive) return;
            if (!IsEquipped || Current.IsMelee) return;

            if (ammoStyle == null)
            {
                ammoStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
                ammoStyle.normal.textColor = Color.white;
            }

            string text = reloading ? "RELOADING..." : $"{Current.displayName}   {Loaded} / {Reserve}";
            GUI.Label(new Rect(Screen.width - 324f, Screen.height - 50f, 300f, 30f), text, ammoStyle);
        }
    }
}
