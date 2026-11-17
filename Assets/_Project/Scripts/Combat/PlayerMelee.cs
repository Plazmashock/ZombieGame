using System.Collections.Generic;
using UnityEngine;
using LastCheckout.Inventory;
using LastCheckout.Items;
using LastCheckout.Player;

namespace LastCheckout.Combat
{
    /// <summary>One row of the "weapon table": carry this item = punch this hard.</summary>
    [System.Serializable]
    public class WeaponDamage
    {
        public ItemDefinition item;
        public float damage = 30f;
    }

    /// <summary>
    /// Left click = swing at whatever is in front of you.
    ///   - Melee weapon in hand (knife, bat, machete, axe): uses that weapon's stats from WeaponCatalog
    ///     (damage, speed, reach, how wide, how many zombies one swing can hit, knockback).
    ///   - Nothing in hand (fists): weak punch, a bit better if your backpack holds a hammer / tool from the table below.
    /// Melee is quiet: it barely makes noise, so it won't pull the whole horde to you.
    /// </summary>
    public class PlayerMelee : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerLook look;
        [SerializeField] PlayerInventory inventory;
        PlayerShooting shooting;
        [Tooltip("Where swings start and point from. Use the player camera.")]
        [SerializeField] Transform viewPoint;

        [Header("Fists")]
        [SerializeField] float fistDamage = 15f;
        [SerializeField] float reach = 2.0f;
        [SerializeField, Range(10f, 90f)] float swingAngle = 45f;
        [SerializeField] float cooldown = 0.5f;

        [Header("Loot that makes your fists hit harder (best one in your backpack is used)")]
        [SerializeField] WeaponDamage[] weapons = new WeaponDamage[0];

        /// <summary>Fires on every swing. True = it hit something. (The weapon models animate on this.)</summary>
        public event System.Action<bool> Swung;
        /// <summary>Seconds a swing takes to animate (the view model reads this).</summary>
        public float SwingDuration => Mathf.Min(0.4f, CurrentCooldown * 0.85f);

        /// <summary>The items in the table above (the game uses them to link store loot to weapons).</summary>
        public IEnumerable<ItemDefinition> LootWeaponItems
        {
            get { foreach (var w in weapons) if (w != null && w.item != null) yield return w.item; }
        }

        float nextSwingTime;
        bool cursorWasLocked;

        void Reset() => AutoFindReferences();
        void Awake() => AutoFindReferences();

        void AutoFindReferences()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (look == null) look = GetComponent<PlayerLook>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            shooting = GetComponent<PlayerShooting>();
            if (viewPoint == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) viewPoint = cam.transform;
            }
        }

        WeaponSpec MeleeSpec => shooting != null && shooting.HoldingMelee ? shooting.Current : null;
        PlayerBuffs buffs;
        PlayerBuffs Buffs => buffs != null ? buffs : (buffs = GetComponent<PlayerBuffs>());
        float CurrentCooldown => (MeleeSpec != null ? MeleeSpec.fireInterval : cooldown) / (Buffs != null ? Buffs.FireRateMult : 1f);

        void Update()
        {
            // Only swing while the mouse is captured and no menu is open.
            bool locked = look.IsCursorLocked && !look.IsUIOpen;
            bool canSwing = locked && cursorWasLocked; // the click that re-captures the mouse isn't a swing
            cursorWasLocked = locked;

            // A gun or bow in hand? Left click shoots instead (see PlayerShooting).
            if (shooting != null && shooting.IsEquipped && !shooting.HoldingMelee) return;

            if (!canSwing || !input.ClickPressed || Time.time < nextSwingTime) return;

            nextSwingTime = Time.time + CurrentCooldown;
            Swing();
        }

        /// <summary>The damage you'd deal right now (weapon in hand, or fists + best loot tool).</summary>
        public float CurrentDamage() => BaseDamage() * (Buffs != null ? Buffs.DamageMult : 1f);

        float BaseDamage()
        {
            if (MeleeSpec != null) return MeleeSpec.damage;
            float best = fistDamage;
            if (inventory == null) return best;
            foreach (var w in weapons)
                if (w.item != null && inventory.CountOf(w.item) > 0)
                    best = Mathf.Max(best, w.damage);
            return best;
        }

        void Swing()
        {
            var spec = MeleeSpec;
            float r = spec != null ? spec.reach : reach;
            float angle = spec != null ? spec.swingAngle : swingAngle;
            int maxTargets = spec != null ? Mathf.Max(1, spec.maxTargets) : 1;
            float damage = CurrentDamage();

            Vector3 origin = viewPoint.position;
            Vector3 forward = viewPoint.forward;

            // Everything in a sphere in front of us, that is within the swing's arc and not behind a wall.
            Collider[] nearby = Physics.OverlapSphere(origin + forward * (r * 0.5f), r * 0.65f, ~0, QueryTriggerInteraction.Ignore);
            var targets = new List<(IDamageable target, float dist)>();
            var seen = new HashSet<IDamageable>();

            foreach (var col in nearby)
            {
                if (col.transform.IsChildOf(transform)) continue; // don't hit yourself

                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead || seen.Contains(target)) continue;

                Vector3 point = col.bounds.center;
                Vector3 toTarget = point - origin;
                if (toTarget.magnitude > r + 0.4f) continue;
                if (Vector3.Angle(forward, toTarget) > angle) continue;

                if (Physics.Linecast(origin, point, out RaycastHit block, ~0, QueryTriggerInteraction.Ignore) &&
                    block.collider.GetComponentInParent<IDamageable>() != target && !block.collider.transform.IsChildOf(transform))
                    continue;

                seen.Add(target);
                targets.Add((target, toTarget.sqrMagnitude));
            }

            targets.Sort((a, b) => a.dist.CompareTo(b.dist));
            bool hitAny = false, killed = false;
            for (int i = 0; i < targets.Count && i < maxTargets; i++)
            {
                var t = targets[i].target;
                t.TakeDamage(damage);
                hitAny = true;
                if (t.IsDead) killed = true;
                else if (spec != null) PlayerShooting.HitReaction(t, forward, spec.knockback, spec.stun);
                else PlayerShooting.HitReaction(t, forward, 1f, 0.15f); // a punch still pushes a little
            }

            if (hitAny && shooting != null) shooting.RegisterHit(killed);
            NoiseMaker.Emit(transform.position, spec != null ? spec.noiseRadius : 2f);
            Swung?.Invoke(hitAny);
        }
    }
}
