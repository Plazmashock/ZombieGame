using System.Collections.Generic;
using UnityEngine;
using LastCheckout.Combat;
using LastCheckout.Interaction;
using LastCheckout.UI;

namespace LastCheckout.Game
{
    /// <summary>One kind of food from the supermarket and what it does when you eat it.</summary>
    public sealed class FoodDef
    {
        public string name;
        public string effect;            // short line shown when you look at it ("+35 HEALTH")
        public Color color;
        public float weight = 1f;        // how often it shows up
        public bool heals;               // counts as a "heal" (every drop has at least one)
        public System.Action<PlayerHealth, PlayerBuffs> apply;
        public System.Action<Transform> build;   // builds the little 3D model under a parent, centred on (0,0,0), about 0.25 m tall
    }

    /// <summary>
    /// The food that comes in supply drops. Every food has its OWN effect:
    ///
    ///   HEALS                                         BUFFS (timed, shown on the left of the screen)
    ///   Canned Peaches   +35 HP                       Energy Drink    +30% speed, 45 s
    ///   Ribeye Steak     +4 HP/s for 12 s             Protein Bar     +30% damage, 45 s
    ///   Bananas          +15 HP, then +1.5 HP/s       Black Coffee    reload 45% faster, 50 s
    ///   Mineral Water    +2 HP/s for 20 s             Orange Juice    take 25% less damage, 45 s
    ///   Frozen Pizza     FULL heal + 15% damage       Hot Sauce       +35% fire / swing rate, 40 s
    ///   Chocolate Bar    +10 HP + 25% fire rate       Fortified Cereal  +20 max health for the whole run
    /// </summary>
    public static class FoodCatalog
    {
        public static readonly List<FoodDef> All = new List<FoodDef>();

        static FoodCatalog()
        {
            Add(new FoodDef
            {
                name = "Canned Peaches", effect = "+35 HEALTH", color = new Color(1f, 0.66f, 0.3f), weight = 10f, heals = true,
                apply = (h, b) => h.Heal(35f), build = t => Can(t, new Color(1f, 0.62f, 0.25f)),
            });
            Add(new FoodDef
            {
                name = "Ribeye Steak", effect = "+4 HP/S FOR 12S", color = new Color(0.9f, 0.25f, 0.2f), weight = 6f, heals = true,
                apply = (h, b) => b.Add("HEARTY MEAL", PlayerBuffs.Kind.Regen, 4f, 12f, new Color(0.9f, 0.3f, 0.25f)), build = Steak,
            });
            Add(new FoodDef
            {
                name = "Bananas", effect = "+15 HEALTH, THEN +1.5 HP/S", color = new Color(1f, 0.88f, 0.2f), weight = 7f, heals = true,
                apply = (h, b) => { h.Heal(15f); b.Add("POTASSIUM", PlayerBuffs.Kind.Regen, 1.5f, 20f, new Color(1f, 0.88f, 0.25f)); }, build = Bananas,
            });
            Add(new FoodDef
            {
                name = "Mineral Water", effect = "+2 HP/S FOR 20S", color = new Color(0.45f, 0.78f, 1f), weight = 8f, heals = true,
                apply = (h, b) => b.Add("HYDRATED", PlayerBuffs.Kind.Regen, 2f, 20f, new Color(0.5f, 0.8f, 1f)), build = Water,
            });
            Add(new FoodDef
            {
                name = "Frozen Pizza", effect = "FULL HEAL + 15% DAMAGE", color = new Color(0.85f, 0.3f, 0.2f), weight = 1.5f, heals = true,
                apply = (h, b) => { h.Heal(h.Max); b.Add("PIZZA NIGHT", PlayerBuffs.Kind.Damage, 0.15f, 30f, new Color(0.95f, 0.4f, 0.25f)); }, build = Pizza,
            });
            Add(new FoodDef
            {
                name = "Chocolate Bar", effect = "+10 HEALTH, +25% FIRE RATE 30S", color = new Color(0.62f, 0.34f, 0.18f), weight = 7f, heals = true,
                apply = (h, b) => { h.Heal(10f); b.Add("SUGAR RUSH", PlayerBuffs.Kind.FireRate, 0.25f, 30f, new Color(0.9f, 0.5f, 0.25f)); }, build = Chocolate,
            });
            Add(new FoodDef
            {
                name = "Energy Drink", effect = "+30% SPEED 45S", color = new Color(0.4f, 1f, 0.3f), weight = 8f,
                apply = (h, b) => b.Add("ENERGY", PlayerBuffs.Kind.Speed, 0.3f, 45f, new Color(0.45f, 1f, 0.35f)), build = EnergyDrink,
            });
            Add(new FoodDef
            {
                name = "Protein Bar", effect = "+30% DAMAGE 45S", color = new Color(0.3f, 0.5f, 1f), weight = 7f,
                apply = (h, b) => b.Add("PROTEIN", PlayerBuffs.Kind.Damage, 0.3f, 45f, new Color(0.4f, 0.6f, 1f)), build = ProteinBar,
            });
            Add(new FoodDef
            {
                name = "Black Coffee", effect = "RELOAD 45% FASTER 50S", color = new Color(0.55f, 0.35f, 0.2f), weight = 6f,
                apply = (h, b) => b.Add("CAFFEINATED", PlayerBuffs.Kind.Reload, 0.45f, 50f, new Color(0.85f, 0.6f, 0.35f)), build = Coffee,
            });
            Add(new FoodDef
            {
                name = "Orange Juice", effect = "TAKE 25% LESS DAMAGE 45S", color = new Color(1f, 0.6f, 0.1f), weight = 6f,
                apply = (h, b) => b.Add("VITAMIN C", PlayerBuffs.Kind.Armor, 0.25f, 45f, new Color(1f, 0.65f, 0.15f)), build = Juice,
            });
            Add(new FoodDef
            {
                name = "Hot Sauce", effect = "+35% FIRE / SWING RATE 40S", color = new Color(0.95f, 0.2f, 0.1f), weight = 5f,
                apply = (h, b) => b.Add("ON FIRE", PlayerBuffs.Kind.FireRate, 0.35f, 40f, new Color(1f, 0.35f, 0.15f)), build = HotSauce,
            });
            Add(new FoodDef
            {
                name = "Fortified Cereal", effect = "+20 MAX HEALTH (PERMANENT)", color = new Color(0.3f, 0.6f, 1f), weight = 3f,
                apply = (h, b) => h.IncreaseMax(20f), build = Cereal,
            });
        }

        static void Add(FoodDef f) => All.Add(f);

        /// <summary>Picks 'count' different foods. The first one is always a heal, so a drop is never all buffs.</summary>
        public static List<FoodDef> PickDrop(int count)
        {
            var pool = new List<FoodDef>(All);
            var result = new List<FoodDef>();

            var heals = pool.FindAll(f => f.heals);
            var first = WeightedPick(heals);
            result.Add(first);
            pool.Remove(first);

            while (result.Count < count && pool.Count > 0)
            {
                var f = WeightedPick(pool);
                result.Add(f);
                pool.Remove(f);
            }
            return result;
        }

        static FoodDef WeightedPick(List<FoodDef> list)
        {
            float total = 0f;
            foreach (var f in list) total += f.weight;
            float r = Random.value * total;
            foreach (var f in list) { r -= f.weight; if (r <= 0f) return f; }
            return list[list.Count - 1];
        }

        // ───────────────────────── Little 3D models (built from simple shapes) ─────────────────────────

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

        static readonly Color Steel = new Color(0.75f, 0.77f, 0.8f);
        static readonly Color Cream = new Color(0.95f, 0.92f, 0.82f);

        static void Can(Transform t, Color label)
        {
            Part(t, PrimitiveType.Cylinder, Vector3.zero, Vector3.zero, new Vector3(0.11f, 0.075f, 0.11f), Steel, 0.7f);
            Part(t, PrimitiveType.Cylinder, Vector3.zero, Vector3.zero, new Vector3(0.113f, 0.052f, 0.113f), label, 0.35f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.115f, 0.012f, 0.115f), Cream, 0.3f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.077f, 0f), Vector3.zero, new Vector3(0.1f, 0.004f, 0.1f), Steel, 0.8f);
        }

        static void EnergyDrink(Transform t)
        {
            Part(t, PrimitiveType.Cylinder, Vector3.zero, Vector3.zero, new Vector3(0.076f, 0.1f, 0.076f), new Color(0.05f, 0.05f, 0.06f), 0.6f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f), Vector3.zero, new Vector3(0.079f, 0.03f, 0.079f), new Color(0.35f, 1f, 0.2f), 0.4f, 0.6f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.101f, 0f), Vector3.zero, new Vector3(0.07f, 0.004f, 0.07f), Steel, 0.8f);
            Part(t, PrimitiveType.Cube, new Vector3(0f, -0.035f, 0.039f), new Vector3(0f, 0f, 45f), new Vector3(0.03f, 0.03f, 0.004f), new Color(0.35f, 1f, 0.2f), 0.4f, 0.6f);
        }

        static void ProteinBar(Transform t)
        {
            Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0f, 20f, 0f), new Vector3(0.19f, 0.028f, 0.065f), new Color(0.12f, 0.25f, 0.75f), 0.5f);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.015f, 0f), new Vector3(0f, 20f, 0f), new Vector3(0.1f, 0.004f, 0.045f), new Color(1f, 0.85f, 0.2f), 0.4f);
            Part(t, PrimitiveType.Cube, new Vector3(0.093f, 0f, 0.034f), new Vector3(0f, 20f, 0f), new Vector3(0.012f, 0.03f, 0.067f), new Color(0.08f, 0.17f, 0.55f), 0.5f);
            Part(t, PrimitiveType.Cube, new Vector3(-0.093f, 0f, -0.034f), new Vector3(0f, 20f, 0f), new Vector3(0.012f, 0.03f, 0.067f), new Color(0.08f, 0.17f, 0.55f), 0.5f);
        }

        static void Chocolate(Transform t)
        {
            Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0f, -15f, 0f), new Vector3(0.17f, 0.024f, 0.075f), new Color(0.7f, 0.1f, 0.1f), 0.5f);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.014f, 0f), new Vector3(0f, -15f, 0f), new Vector3(0.12f, 0.004f, 0.05f), new Color(0.95f, 0.82f, 0.3f), 0.4f);
            for (int i = 0; i < 4; i++)  // a corner torn open, showing the chocolate
                Part(t, PrimitiveType.Cube, new Vector3(0.09f + i * 0.0f, 0.016f, -0.02f + i * 0.012f), new Vector3(0f, -15f, 0f), new Vector3(0.025f, 0.012f, 0.009f), new Color(0.24f, 0.12f, 0.07f), 0.55f);
        }

        static void Steak(Transform t)
        {
            Part(t, PrimitiveType.Cube, new Vector3(0f, -0.012f, 0f), new Vector3(0f, 10f, 0f), new Vector3(0.22f, 0.014f, 0.16f), Cream, 0.4f);            // tray
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(0f, 18f, 0f), new Vector3(0.17f, 0.03f, 0.115f), new Color(0.55f, 0.1f, 0.1f), 0.6f);
            Part(t, PrimitiveType.Cube, new Vector3(0.02f, 0.012f, -0.055f), new Vector3(0f, 18f, 0f), new Vector3(0.15f, 0.032f, 0.014f), Cream, 0.4f);   // fat edge
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.0265f, 0f), new Vector3(0f, 18f, 0f), new Vector3(0.12f, 0.003f, 0.07f), new Color(0.72f, 0.18f, 0.16f), 0.7f);
        }

        static void Coffee(Transform t)
        {
            Part(t, PrimitiveType.Cylinder, Vector3.zero, Vector3.zero, new Vector3(0.078f, 0.06f, 0.078f), new Color(0.93f, 0.93f, 0.9f), 0.4f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, -0.005f, 0f), Vector3.zero, new Vector3(0.082f, 0.026f, 0.082f), new Color(0.45f, 0.28f, 0.16f), 0.3f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.064f, 0f), Vector3.zero, new Vector3(0.088f, 0.008f, 0.088f), new Color(0.1f, 0.1f, 0.11f), 0.5f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.076f, 0f), Vector3.zero, new Vector3(0.05f, 0.008f, 0.05f), new Color(0.1f, 0.1f, 0.11f), 0.5f);
        }

        static void Juice(Transform t)
        {
            Part(t, PrimitiveType.Cube, Vector3.zero, Vector3.zero, new Vector3(0.09f, 0.2f, 0.09f), new Color(1f, 0.58f, 0.08f), 0.4f);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.115f, 0f), Vector3.zero, new Vector3(0.09f, 0.03f, 0.04f), Cream, 0.4f);
            Part(t, PrimitiveType.Sphere, new Vector3(0f, 0.01f, 0.046f), Vector3.zero, new Vector3(0.06f, 0.06f, 0.006f), new Color(1f, 0.8f, 0.2f), 0.4f);
            Part(t, PrimitiveType.Sphere, new Vector3(0.012f, 0.032f, 0.047f), Vector3.zero, new Vector3(0.018f, 0.018f, 0.004f), new Color(0.3f, 0.65f, 0.2f), 0.4f);
        }

        static void Bananas(Transform t)
        {
            Color yellow = new Color(1f, 0.86f, 0.18f), tip = new Color(0.25f, 0.2f, 0.08f);
            for (int i = -1; i <= 1; i++)
            {
                Part(t, PrimitiveType.Capsule, new Vector3(i * 0.04f, 0f, 0f), new Vector3(0f, i * 12f, 90f + i * 14f), new Vector3(0.042f, 0.085f, 0.042f), yellow, 0.35f);
                Part(t, PrimitiveType.Sphere, new Vector3(i * 0.04f + 0.09f, 0.012f * i, 0f), Vector3.zero, new Vector3(0.016f, 0.016f, 0.016f), tip, 0.3f);
            }
            Part(t, PrimitiveType.Cylinder, new Vector3(-0.1f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.03f, 0.02f, 0.03f), new Color(0.4f, 0.45f, 0.15f), 0.3f);
        }

        static void Cereal(Transform t)
        {
            Part(t, PrimitiveType.Cube, Vector3.zero, Vector3.zero, new Vector3(0.16f, 0.24f, 0.055f), new Color(0.15f, 0.45f, 0.95f), 0.4f);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.0f, 0.029f), Vector3.zero, new Vector3(0.13f, 0.18f, 0.005f), new Color(1f, 0.82f, 0.2f), 0.4f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0.033f), new Vector3(90f, 0f, 0f), new Vector3(0.09f, 0.004f, 0.09f), new Color(0.95f, 0.95f, 0.9f), 0.5f);
            Part(t, PrimitiveType.Sphere, new Vector3(0.015f, -0.02f, 0.037f), Vector3.zero, new Vector3(0.02f, 0.02f, 0.008f), new Color(0.9f, 0.5f, 0.15f), 0.4f);
            Part(t, PrimitiveType.Sphere, new Vector3(-0.02f, -0.03f, 0.037f), Vector3.zero, new Vector3(0.02f, 0.02f, 0.008f), new Color(0.9f, 0.5f, 0.15f), 0.4f);
        }

        static void Pizza(Transform t)
        {
            Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0f, 20f, 0f), new Vector3(0.27f, 0.028f, 0.27f), new Color(0.78f, 0.62f, 0.38f), 0.2f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.016f, 0f), new Vector3(0f, 20f, 0f), new Vector3(0.23f, 0.003f, 0.23f), new Color(0.88f, 0.65f, 0.3f), 0.3f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.0185f, 0f), new Vector3(0f, 20f, 0f), new Vector3(0.19f, 0.003f, 0.19f), new Color(0.85f, 0.25f, 0.15f), 0.4f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Part(t, PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * 0.065f, 0.021f, Mathf.Sin(a) * 0.065f), Vector3.zero, new Vector3(0.035f, 0.003f, 0.035f), new Color(0.65f, 0.1f, 0.1f), 0.4f);
            }
        }

        static void HotSauce(Transform t)
        {
            Part(t, PrimitiveType.Cylinder, Vector3.zero, Vector3.zero, new Vector3(0.052f, 0.07f, 0.052f), new Color(0.85f, 0.12f, 0.06f), 0.7f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.09f, 0f), Vector3.zero, new Vector3(0.026f, 0.03f, 0.026f), new Color(0.85f, 0.12f, 0.06f), 0.7f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.125f, 0f), Vector3.zero, new Vector3(0.03f, 0.012f, 0.03f), new Color(0.07f, 0.07f, 0.07f), 0.4f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.0f, 0f), Vector3.zero, new Vector3(0.055f, 0.035f, 0.055f), Cream, 0.3f);
            Part(t, PrimitiveType.Sphere, new Vector3(0f, 0.0f, 0.027f), Vector3.zero, new Vector3(0.025f, 0.025f, 0.006f), new Color(0.9f, 0.1f, 0.05f), 0.3f);
        }

        static void Water(Transform t)
        {
            Part(t, PrimitiveType.Cylinder, Vector3.zero, Vector3.zero, new Vector3(0.066f, 0.1f, 0.066f), new Color(0.75f, 0.9f, 1f), 0.9f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.115f, 0f), Vector3.zero, new Vector3(0.03f, 0.018f, 0.03f), new Color(0.75f, 0.9f, 1f), 0.9f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.14f, 0f), Vector3.zero, new Vector3(0.034f, 0.01f, 0.034f), new Color(0.1f, 0.35f, 0.85f), 0.4f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.0f, 0f), Vector3.zero, new Vector3(0.069f, 0.03f, 0.069f), new Color(0.1f, 0.4f, 0.9f), 0.4f);
        }
    }

    /// <summary>
    /// One piece of food lying next to an opened supply crate. Look at it and press E to eat it.
    /// It bobs and spins, and has a little coloured glow so you can spot it in the dark.
    /// </summary>
    public class FoodPickup : MonoBehaviour, IInteractable
    {
        FoodDef def;
        Transform visual;
        float phase;
        Vector3 visualBase;

        public static FoodPickup Spawn(FoodDef def, Vector3 ground)
        {
            var go = new GameObject("Food: " + def.name);
            go.transform.position = ground;
            var pickup = go.AddComponent<FoodPickup>();
            pickup.def = def;
            pickup.phase = Random.value * 6.28f;

            // Model, floating at about hip height.
            var holder = new GameObject("Model").transform;
            holder.SetParent(go.transform, false);
            holder.localPosition = new Vector3(0f, 0.62f, 0f);
            def.build(holder);
            holder.localScale = Vector3.one * 1.5f;   // bigger than life so it reads from a few metres away
            pickup.visual = holder;
            pickup.visualBase = holder.localPosition;

            // Glow disc on the ground + a small coloured light.
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            LevelKit.RemoveCollider(disc);
            disc.name = "Glow";
            disc.transform.SetParent(go.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            disc.transform.localScale = new Vector3(0.55f, 0.004f, 0.55f);
            var dr = disc.GetComponent<Renderer>();
            dr.sharedMaterial = LevelKit.ColorMaterial(def.color, 0.2f, 1.4f);
            dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = def.color;
            l.intensity = 1.3f;
            l.range = 3.2f;
            l.shadows = LightShadows.None;

            // A solid collider so the interact ray can hit it (the player walks through it).
            var col = go.AddComponent<SphereCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.radius = 0.4f;
            var who = LevelKit.FindFirst<PlayerInteractor>();
            var cc = who != null ? who.GetComponent<CharacterController>() : null;
            if (cc != null) Physics.IgnoreCollision(cc, col);
            return pickup;
        }

        void Update()
        {
            if (visual == null) return;
            float t = Time.time + phase;
            visual.localPosition = visualBase + new Vector3(0f, Mathf.Sin(t * 2f) * 0.05f, 0f);
            visual.localRotation = Quaternion.Euler(0f, t * 55f, Mathf.Sin(t * 1.3f) * 8f);
        }

        public string GetPrompt(PlayerInteractor who) => $"Eat {def.name}  ·  {def.effect}";
        public bool CanInteract(PlayerInteractor who) => who.GetComponent<PlayerBuffs>() != null && who.GetComponent<PlayerHealth>() != null;

        public void Interact(PlayerInteractor who)
        {
            var health = who.GetComponent<PlayerHealth>();
            var buffs = who.GetComponent<PlayerBuffs>();
            if (health == null || buffs == null || health.IsDead) return;
            def.apply(health, buffs);
            GameAudio.Play(GameAudio.Sfx.Eat, 0.9f);
            GameHUD.PushToast($"<color=#7CFF8A>{def.effect}</color>   {def.name.ToUpperInvariant()}");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// The crate the supply drone leaves in the parking lot.
    ///   Hanging under the drone: can't be used.   Landed: look + E opens the lid and 4 foods pop out around it.
    /// A green light beam shows where it is until you open it.
    /// </summary>
    public class SupplyDropCrate : MonoBehaviour, IInteractable
    {
        enum State { Hanging, Closed, Open }
        const int FoodCount = 4;

        State state = State.Hanging;
        Transform lid;
        BoxCollider col;
        GameObject beam;
        Light beamLight;
        readonly List<GameObject> foods = new List<GameObject>();

        /// <summary>Builds the crate (hanging) with its feet at 'position'.</summary>
        public static SupplyDropCrate Build(Vector3 position)
        {
            var go = new GameObject("Supply Drop Crate");
            go.transform.position = position;
            var crate = go.AddComponent<SupplyDropCrate>();

            Color olive = new Color(0.22f, 0.27f, 0.17f), orange = new Color(0.95f, 0.5f, 0.1f), steel = new Color(0.3f, 0.32f, 0.33f);
            void Box(string name, Vector3 pos, Vector3 size, Color c, float glow = 0f, float smooth = 0.25f)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                LevelKit.RemoveCollider(b);
                b.name = name;
                b.transform.SetParent(go.transform, false);
                b.transform.localPosition = pos;
                b.transform.localScale = size;
                var r = b.GetComponent<Renderer>();
                r.sharedMaterial = LevelKit.ColorMaterial(c, smooth, glow);
            }

            // Body 1.1 × 0.7 × 0.8, feet on the ground.
            Box("Body", new Vector3(0f, 0.35f, 0f), new Vector3(1.1f, 0.7f, 0.8f), olive);
            Box("Corner A", new Vector3(0.53f, 0.35f, 0.38f), new Vector3(0.08f, 0.74f, 0.08f), steel, 0f, 0.5f);
            Box("Corner B", new Vector3(-0.53f, 0.35f, 0.38f), new Vector3(0.08f, 0.74f, 0.08f), steel, 0f, 0.5f);
            Box("Corner C", new Vector3(0.53f, 0.35f, -0.38f), new Vector3(0.08f, 0.74f, 0.08f), steel, 0f, 0.5f);
            Box("Corner D", new Vector3(-0.53f, 0.35f, -0.38f), new Vector3(0.08f, 0.74f, 0.08f), steel, 0f, 0.5f);
            Box("Stripe", new Vector3(0f, 0.5f, 0.405f), new Vector3(1.1f, 0.12f, 0.01f), orange);
            Box("Stripe Back", new Vector3(0f, 0.5f, -0.405f), new Vector3(1.1f, 0.12f, 0.01f), orange);
            Box("Handle L", new Vector3(-0.57f, 0.45f, 0f), new Vector3(0.05f, 0.05f, 0.3f), steel, 0f, 0.5f);
            Box("Handle R", new Vector3(0.57f, 0.45f, 0f), new Vector3(0.05f, 0.05f, 0.3f), steel, 0f, 0.5f);
            Box("Light Housing", new Vector3(0.38f, 0.76f, 0.3f), new Vector3(0.1f, 0.08f, 0.1f), steel, 0f, 0.5f);
            Box("Status Lamp", new Vector3(0.38f, 0.81f, 0.3f), new Vector3(0.07f, 0.04f, 0.07f), new Color(0.3f, 1f, 0.4f), 2f);

            // Lid, hinged along the back edge so it can flip open.
            var hinge = new GameObject("Lid Hinge").transform;
            hinge.SetParent(go.transform, false);
            hinge.localPosition = new Vector3(0f, 0.7f, -0.4f);
            var lidBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            LevelKit.RemoveCollider(lidBox);
            lidBox.name = "Lid";
            lidBox.transform.SetParent(hinge, false);
            lidBox.transform.localPosition = new Vector3(0f, 0.04f, 0.4f);
            lidBox.transform.localScale = new Vector3(1.14f, 0.08f, 0.84f);
            lidBox.GetComponent<Renderer>().sharedMaterial = LevelKit.ColorMaterial(new Color(0.26f, 0.32f, 0.2f), 0.25f);
            var cross = GameObject.CreatePrimitive(PrimitiveType.Cube);
            LevelKit.RemoveCollider(cross);
            cross.transform.SetParent(lidBox.transform, false);
            cross.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            cross.transform.localScale = new Vector3(0.5f, 0.05f, 0.12f);
            cross.GetComponent<Renderer>().sharedMaterial = LevelKit.ColorMaterial(new Color(0.95f, 0.95f, 0.9f), 0.3f);
            var cross2 = Object.Instantiate(cross, lidBox.transform);
            cross2.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            cross2.transform.localScale = new Vector3(0.12f, 0.05f, 0.5f);
            crate.lid = hinge;

            // Solid box so the interact ray (and bullets) hit it. The player can't walk through it.
            crate.col = go.AddComponent<BoxCollider>();
            crate.col.center = new Vector3(0f, 0.38f, 0f);
            crate.col.size = new Vector3(1.1f, 0.78f, 0.8f);
            crate.col.enabled = false; // switched on when it lands
            return crate;
        }

        /// <summary>Called by the drone when the crate touches down.</summary>
        public void Landed()
        {
            if (state != State.Hanging) return;
            state = State.Closed;
            col.enabled = true;

            beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            LevelKit.RemoveCollider(beam);
            beam.name = "Beacon";
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = new Vector3(0f, 5f, 0f);
            beam.transform.localScale = new Vector3(0.12f, 5f, 0.12f);
            var r = beam.GetComponent<Renderer>();
            r.sharedMaterial = LevelKit.ColorMaterial(new Color(0.35f, 1f, 0.5f), 0.1f, 2.5f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lg = new GameObject("Beacon Light");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            beamLight = lg.AddComponent<Light>();
            beamLight.type = LightType.Point;
            beamLight.color = new Color(0.4f, 1f, 0.55f);
            beamLight.intensity = 2.2f;
            beamLight.range = 9f;
            beamLight.shadows = LightShadows.None;
        }

        public string GetPrompt(PlayerInteractor who) =>
            state == State.Closed ? "Open supply drop  ·  free food" :
            state == State.Open ? "Supply drop  ·  grab the food around it" : "";
        public bool CanInteract(PlayerInteractor who) => state == State.Closed;

        public void Interact(PlayerInteractor who)
        {
            if (state != State.Closed) return;
            state = State.Open;
            GameAudio.Play(GameAudio.Sfx.CrateOpen, 0.9f);
            if (beam != null) Destroy(beam);
            if (beamLight != null) beamLight.color = new Color(0.9f, 0.9f, 0.7f);

            // Foods pop out in a ring around the crate (never on the player's side wall-to-wall: just a ring).
            var picks = FoodCatalog.PickDrop(FoodCount);
            float start = Random.value * 360f;
            for (int i = 0; i < picks.Count; i++)
            {
                float a = (start + i * (360f / picks.Count)) * Mathf.Deg2Rad;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * 1.55f, 0f, Mathf.Sin(a) * 1.35f);
                foods.Add(FoodPickup.Spawn(picks[i], p).gameObject);
            }
            if (GameDirector.Instance != null)
                GameDirector.Instance.Say("SUPPLY DROP OPENED", "Eat what you need. Each food does something different.", false);
        }

        void Update()
        {
            if (lid != null)
            {
                Quaternion want = Quaternion.Euler(state == State.Open ? -115f : 0f, 0f, 0f);
                lid.localRotation = Quaternion.RotateTowards(lid.localRotation, want, 260f * Time.deltaTime);
            }
            if (beamLight != null && state == State.Closed)
                beamLight.intensity = 2.2f + Mathf.Sin(Time.time * 4f) * 0.6f;
        }

        /// <summary>Removes the crate and any food left over (when the next drop arrives).</summary>
        public void Clear()
        {
            foreach (var f in foods) if (f != null) Destroy(f);
            Destroy(gameObject);
        }
    }
}
