using System.Collections.Generic;
using UnityEngine;

namespace LastCheckout.Combat
{
    /// <summary>
    /// Timed power-ups the player gets from food (see Game/SupplyDropCrate).
    ///
    ///   Speed       walk / sprint faster
    ///   Damage      every hit hurts more (guns, bow, melee)
    ///   FireRate    shoot / swing faster
    ///   Reload      reload takes less time
    ///   Armor       take less damage
    ///   Regen       heal a little every second
    ///
    /// Other scripts only READ the multipliers below (SpeedMult, DamageMult...). They never need to know which food gave them.
    /// Taking the same food again just refreshes its timer.
    /// </summary>
    public class PlayerBuffs : MonoBehaviour
    {
        public enum Kind { Speed, Damage, FireRate, Reload, Armor, Regen }

        public class Active
        {
            public string name;
            public Kind kind;
            public float amount;     // 0.25 = +25% (Regen: HP per second)
            public float duration;
            public float endTime;
            public Color color;
            public float Left => Mathf.Max(0f, endTime - Time.time);
            public float Fraction => duration > 0f ? Mathf.Clamp01(Left / duration) : 0f;
        }

        readonly List<Active> actives = new List<Active>();
        // Permanent bonuses (bought in the supply truck). They never run out and stack with the timed buffs.
        readonly Dictionary<Kind, float> permanent = new Dictionary<Kind, float>();
        PlayerHealth health;

        public IReadOnlyList<Active> Actives => actives;
        /// <summary>Fires when a buff starts or ends (the HUD redraws).</summary>
        public event System.Action Changed;

        public float SpeedMult => 1f + Sum(Kind.Speed);
        public float DamageMult => 1f + Sum(Kind.Damage);
        /// <summary>2 = twice as fast. Divide cooldowns by this.</summary>
        public float FireRateMult => 1f + Sum(Kind.FireRate);
        /// <summary>0.6 = reload takes 60% of the time. Multiply reload time by this.</summary>
        public float ReloadMult => Mathf.Clamp(1f - Sum(Kind.Reload), 0.25f, 1f);
        /// <summary>0.75 = you take 75% of the damage.</summary>
        public float DamageTakenMult => Mathf.Clamp(1f - Sum(Kind.Armor), 0.3f, 1f);

        void Awake() => health = GetComponent<PlayerHealth>();

        /// <summary>Adds a bonus that lasts for the rest of the run (e.g. +10% damage).</summary>
        public void AddPermanent(Kind kind, float amount)
        {
            permanent.TryGetValue(kind, out float have);
            permanent[kind] = have + amount;
            Changed?.Invoke();
        }

        /// <summary>The permanent bonus for one kind (0.1 = +10%).</summary>
        public float PermanentBonus(Kind kind) => permanent.TryGetValue(kind, out float v) ? v : 0f;

        float Sum(Kind kind)
        {
            float s = PermanentBonus(kind);
            for (int i = 0; i < actives.Count; i++) if (actives[i].kind == kind) s += actives[i].amount;
            return s;
        }

        /// <summary>Starts (or refreshes) a buff.</summary>
        public void Add(string name, Kind kind, float amount, float seconds, Color color)
        {
            foreach (var a in actives)
            {
                if (a.name != name) continue;
                a.amount = amount; a.duration = seconds; a.endTime = Time.time + seconds;
                Changed?.Invoke();
                return;
            }
            actives.Add(new Active { name = name, kind = kind, amount = amount, duration = seconds, endTime = Time.time + seconds, color = color });
            Changed?.Invoke();
        }

        public void ClearAll()
        {
            if (actives.Count == 0) return;
            actives.Clear();
            Changed?.Invoke();
        }

        void Update()
        {
            if (actives.Count == 0) return;
            bool removed = false;
            float regen = 0f;
            for (int i = actives.Count - 1; i >= 0; i--)
            {
                var a = actives[i];
                if (a.kind == Kind.Regen) regen += a.amount;
                if (Time.time >= a.endTime) { actives.RemoveAt(i); removed = true; }
            }
            if (regen > 0f && health != null && !health.IsDead) health.Heal(regen * Time.deltaTime);
            if (removed) Changed?.Invoke();
        }
    }
}
