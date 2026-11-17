using UnityEngine;
using LastCheckout.Combat;
using LastCheckout.Enemies;
using LastCheckout.World;

namespace LastCheckout.Game
{
    /// <summary>
    /// A coil of barbed wire across a doorway (bought as a store upgrade).
    /// It has NO collider: zombies (and you) can still walk through, so the zombie path map doesn't change.
    /// But every zombie standing in it gets cut (damage) and snagged (keeps stumbling = slowed down).
    ///
    ///     post ─ ~~~~~~~~~ coil ~~~~~~~~~ ─ post        zone = a box around the coil
    ///                 ↑ zombies inside: −HP every 0.25 s, short stagger each time
    /// </summary>
    public class BarbedWireZone : MonoBehaviour
    {
        public static float DamageMultiplier = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => DamageMultiplier = 1f;

        Bounds zone;
        float damagePerSecond = 12f;
        float nextTick;

        /// Builds a coil from 'from' to 'to' (on the ground) and returns it.
        public static BarbedWireZone Build(string name, Transform parent, Vector3 from, Vector3 to, float dps)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            Vector3 dir = to - from; dir.y = 0f;
            float len = dir.magnitude;
            dir /= Mathf.Max(0.001f, len);
            Vector3 side = Vector3.Cross(Vector3.up, dir); // sideways (across the coil)

            var wireMat = SurfaceMaterials.Get(Surface.BrushedMetal, new Color(0.5f, 0.5f, 0.52f));
            var postMat = SurfaceMaterials.Get(Surface.PaintedMetal, new Color(0.22f, 0.24f, 0.2f));
            var mb = new ModelKit.MeshBuilder();
            Matrix4x4 world = Matrix4x4.identity;

            // The coil: a spiral (helix) of thin wire sticks, 0.4 m tall, 4 loops per meter.
            const float radius = 0.38f, loopsPerMeter = 4f;
            int steps = Mathf.CeilToInt(len * loopsPerMeter * 14f);
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float ang = t * len * loopsPerMeter * Mathf.PI * 2f;
                Vector3 p = from + dir * (t * len) + Vector3.up * (radius + 0.02f + Mathf.Sin(ang) * radius) + side * (Mathf.Cos(ang) * radius);
                if (i > 0)
                {
                    mb.AddStick(prev, p, 0.012f, world, 1f);
                    if (i % 5 == 0) // a barb: a tiny cross sticking out
                    {
                        mb.AddStick(p - side * 0.04f - Vector3.up * 0.03f, p + side * 0.04f + Vector3.up * 0.03f, 0.008f, world, 1f);
                        mb.AddStick(p - dir * 0.04f + Vector3.up * 0.03f, p + dir * 0.04f - Vector3.up * 0.03f, 0.008f, world, 1f);
                    }
                }
                prev = p;
            }
            // Two straight wires along the top and bottom.
            mb.AddStick(from + Vector3.up * 0.1f, to + Vector3.up * 0.1f, 0.01f, world, 1f);
            mb.AddStick(from + Vector3.up * 0.75f, to + Vector3.up * 0.75f, 0.01f, world, 1f);

            var coilGo = new GameObject("Coil");
            coilGo.transform.SetParent(root.transform, false);
            ModelKit.Finish(coilGo, mb, wireMat, castShadows: false);

            // Posts at both ends and every ~2 m.
            int posts = Mathf.Max(2, Mathf.CeilToInt(len / 2f) + 1);
            for (int i = 0; i < posts; i++)
            {
                Vector3 p = from + dir * (len * i / (posts - 1));
                ModelKit.Box("Post", root.transform, p + Vector3.up * 0.5f, new Vector3(0.06f, 1f, 0.06f), postMat, collider: false);
                ModelKit.Box("Foot", root.transform, p + Vector3.up * 0.02f, new Vector3(0.3f, 0.04f, 0.06f), postMat, collider: false,
                             rotation: Quaternion.LookRotation(side));
            }

            var zoneComp = root.AddComponent<BarbedWireZone>();
            Vector3 center = (from + to) * 0.5f + Vector3.up * 0.8f;
            Vector3 size = new Vector3(Mathf.Abs(dir.x) * len + Mathf.Abs(side.x) * 1.3f, 3f, Mathf.Abs(dir.z) * len + Mathf.Abs(side.z) * 1.3f);
            zoneComp.zone = new Bounds(center, size);
            zoneComp.damagePerSecond = dps;
            return zoneComp;
        }

        void Update()
        {
            if (Time.time < nextTick) return;
            const float tick = 0.25f;
            nextTick = Time.time + tick;

            var all = Zombie.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var z = all[i];
                if (z == null || z.IsDead || !zone.Contains(z.transform.position + Vector3.up * 0.3f)) continue;
                z.Knockback(Vector3.zero, 0f, 0.14f);                       // snagged: stumbles for a moment
                z.TakeDamage(damagePerSecond * DamageMultiplier * tick);   // may kill it (and remove it from the list)
                if (Random.value < 0.25f) HitEffects.Blood(z.transform.position + Vector3.up * 0.5f, Vector3.up);
            }
        }
    }
}
