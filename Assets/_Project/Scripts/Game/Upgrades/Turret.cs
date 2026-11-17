using System.Collections.Generic;
using UnityEngine;
using LastCheckout.Combat;
using LastCheckout.Enemies;
using LastCheckout.World;

namespace LastCheckout.Game
{
    /// <summary>
    /// An automatic sentry gun (bought as a store upgrade).
    ///
    ///   every 0.25 s: look for the closest living zombie in range that it can SEE (no wall in between)
    ///        │
    ///        ├─ found ──► turn the head towards it ──► lined up? ──► FIRE (tracer, blood, damage)
    ///        └─ none  ──► slowly sweep left and right
    ///
    /// Kills made by turrets pay you like your own kills (the GameDirector pays for every dead zombie).
    /// Turret Overclock upgrades raise DamageMultiplier / FireRateMultiplier / RangeBonus for ALL turrets.
    /// </summary>
    public class Turret : MonoBehaviour
    {
        public static float DamageMultiplier = 1f;
        public static float FireRateMultiplier = 1f;
        public static float RangeBonus = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { DamageMultiplier = 1f; FireRateMultiplier = 1f; RangeBonus = 0f; }

        const float BaseDamage = 7f;
        const float BaseInterval = 0.3f;   // seconds between shots
        const float TurnSpeed = 300f;      // degrees per second

        float range = 22f;
        Transform yaw, pitch, muzzle, barrels;
        Light flash;
        Renderer eye;
        Zombie target;
        float nextScan, nextShot, flashOff, idlePhase, restYaw;

        // ───────────────────────── Building the model ─────────────────────────

        /// Builds a turret standing on 'floorPoint', resting facing 'facingYaw' degrees.
        /// pedestalHeight 0 = sits right on the floor (e.g. on top of the sniper tower).
        public static Turret Build(string name, Transform parent, Vector3 floorPoint, float facingYaw, float range, float pedestalHeight = 0.9f)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = floorPoint;

            Color gunmetal = new Color(0.32f, 0.34f, 0.36f);
            var plate = SurfaceMaterials.Get(Surface.DiamondPlate, new Color(0.55f, 0.57f, 0.6f));
            var body = SurfaceMaterials.Get(Surface.PaintedMetal, gunmetal);
            var olive = SurfaceMaterials.Get(Surface.PaintedMetal, new Color(0.33f, 0.36f, 0.22f));
            var black = SurfaceMaterials.Get(Surface.BrushedMetal, new Color(0.12f, 0.12f, 0.13f));
            var hazard = SurfaceMaterials.Get(Surface.PaintedMetal, new Color(1f, 0.75f, 0.15f));

            //   ┌─ head (turns left/right) ─┐
            //   │  ┌─ cradle (tilts up/down) ─ twin barrels ─ muzzle
            //   │  └ ammo box, red sensor eye
            //   └─ pedestal (solid, steel plate) on the floor
            float top = pedestalHeight;
            if (pedestalHeight > 0.05f)
            {
                ModelKit.Box("Pedestal", root, floorPoint + Vector3.up * (top * 0.5f), new Vector3(0.62f, top, 0.62f), plate);
                ModelKit.Box("Hazard Band", root, floorPoint + Vector3.up * (top - 0.06f), new Vector3(0.64f, 0.06f, 0.64f), hazard, collider: false);
            }
            ModelKit.Cylinder("Base Ring", root, floorPoint + Vector3.up * (top + 0.05f), 0.28f, 0.1f, black, 20);

            var yawGo = new GameObject("Head (yaw)");
            yawGo.transform.SetParent(root, false);
            yawGo.transform.position = floorPoint + Vector3.up * (top + 0.1f);
            Transform head = yawGo.transform;
            ModelKit.Cylinder("Swivel", head, head.position + Vector3.up * 0.08f, 0.16f, 0.16f, body, 16);
            ModelKit.Box("Fork L", head, head.position + new Vector3(-0.2f, 0.25f, 0f), new Vector3(0.05f, 0.34f, 0.2f), body, collider: false);
            ModelKit.Box("Fork R", head, head.position + new Vector3(0.2f, 0.25f, 0f), new Vector3(0.05f, 0.34f, 0.2f), body, collider: false);

            var pitchGo = new GameObject("Cradle (pitch)");
            pitchGo.transform.SetParent(head, false);
            pitchGo.transform.position = head.position + Vector3.up * 0.32f;
            Transform cradle = pitchGo.transform;
            Vector3 c = cradle.position;
            ModelKit.Box("Housing", cradle, c + new Vector3(0f, 0f, -0.02f), new Vector3(0.32f, 0.22f, 0.46f), body, collider: false);
            ModelKit.Box("Top Plate", cradle, c + new Vector3(0f, 0.12f, 0.02f), new Vector3(0.26f, 0.03f, 0.36f), plate, collider: false);
            ModelKit.Box("Ammo Box", cradle, c + new Vector3(0.24f, -0.04f, -0.06f), new Vector3(0.14f, 0.16f, 0.24f), olive, collider: false);
            ModelKit.Box("Ammo Belt", cradle, c + new Vector3(0.17f, 0.02f, 0.06f), new Vector3(0.04f, 0.04f, 0.12f), hazard, collider: false);

            var barrelGo = new GameObject("Barrels");
            barrelGo.transform.SetParent(cradle, false);
            barrelGo.transform.position = c;
            Quaternion along = Quaternion.Euler(90f, 0f, 0f); // cylinder's long axis (Y) turned to point forward (Z)
            foreach (float x in new[] { -0.06f, 0.06f })
            {
                ModelKit.Cylinder("Barrel", barrelGo.transform, c + new Vector3(x, 0.01f, 0.45f), 0.028f, 0.5f, black, 10, rotation: along);
                ModelKit.Cylinder("Shroud", barrelGo.transform, c + new Vector3(x, 0.01f, 0.27f), 0.045f, 0.14f, body, 12, rotation: along);
            }

            var eyeGo = ModelKit.Box("Sensor", cradle, c + new Vector3(-0.1f, 0.06f, 0.22f), new Vector3(0.06f, 0.05f, 0.03f),
                                     SurfaceMaterials.Get(Surface.PaintedMetal, new Color(1f, 0.15f, 0.1f), 3f), collider: false);

            var muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(cradle, false);
            muzzleGo.transform.position = c + new Vector3(0f, 0.01f, 0.72f);

            var lightGo = new GameObject("Muzzle Flash");
            lightGo.transform.SetParent(muzzleGo.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.75f, 0.35f);
            l.range = 6f;
            l.intensity = 0f;
            l.shadows = LightShadows.None;

            var t = root.gameObject.AddComponent<Turret>();
            t.yaw = head;
            t.pitch = cradle;
            t.barrels = barrelGo.transform;
            t.muzzle = muzzleGo.transform;
            t.flash = l;
            t.eye = eyeGo.GetComponent<Renderer>();
            t.range = range;
            t.restYaw = facingYaw;
            t.idlePhase = Random.value * 10f;
            head.rotation = Quaternion.Euler(0f, facingYaw, 0f);
            return t;
        }

        // ───────────────────────── Every frame ─────────────────────────

        void Update()
        {
            float r = range + RangeBonus;
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.25f;
                if (!IsValid(target, r)) target = FindTarget(r);
            }

            Quaternion wantYaw, wantPitch = Quaternion.identity;
            if (target != null)
            {
                Vector3 aim = AimPoint(target);
                Vector3 flat = aim - yaw.position; flat.y = 0f;
                wantYaw = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : yaw.rotation;
                Vector3 local = Quaternion.Inverse(wantYaw) * (aim - pitch.position);
                float angle = -Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
                wantPitch = Quaternion.Euler(Mathf.Clamp(angle, -50f, 35f), 0f, 0f);
            }
            else
            {
                // Idle: sweep ±40° around its resting direction.
                wantYaw = Quaternion.Euler(0f, restYaw + Mathf.Sin(Time.time * 0.5f + idlePhase) * 40f, 0f);
            }

            float turn = (target != null ? TurnSpeed : 60f) * Time.deltaTime;
            yaw.rotation = Quaternion.RotateTowards(yaw.rotation, wantYaw, turn);
            pitch.localRotation = Quaternion.RotateTowards(pitch.localRotation, wantPitch, turn);

            // Barrel recoil springs back, muzzle flash fades.
            barrels.localPosition = Vector3.Lerp(barrels.localPosition, Vector3.zero, 1f - Mathf.Exp(-18f * Time.deltaTime));
            if (Time.time >= flashOff) flash.intensity = 0f;

            if (target != null && Time.time >= nextShot)
            {
                Vector3 toTarget = AimPoint(target) - muzzle.position;
                if (Vector3.Angle(muzzle.forward, toTarget) < 7f)
                {
                    nextShot = Time.time + BaseInterval / Mathf.Max(0.1f, FireRateMultiplier);
                    Fire();
                }
            }

            // Sensor eye: brighter while it has a target.
            if (eye != null) eye.enabled = target != null || Mathf.Repeat(Time.time + idlePhase, 1.2f) < 0.9f;
        }

        void Fire()
        {
            Vector3 origin = muzzle.position;
            Vector3 dir = (AimPoint(target) - origin).normalized;
            dir = Quaternion.Euler(Random.Range(-1.2f, 1.2f), Random.Range(-1.2f, 1.2f), 0f) * dir; // a little spread
            float r = range + RangeBonus;

            Vector3 end = origin + dir * r;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, r, ~0, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var z = hit.collider.GetComponentInParent<Zombie>();
                if (z != null && !z.IsDead)
                {
                    z.TakeDamage(BaseDamage * DamageMultiplier);
                    z.Knockback(dir, 0.5f, 0.05f);
                    HitEffects.Blood(hit.point, hit.normal);
                }
                else HitEffects.Impact(hit.point, hit.normal);
            }
            BulletTrail.Spawn(origin, end);

            flash.intensity = 5f;
            flashOff = Time.time + 0.04f;
            barrels.localPosition = new Vector3(0f, 0f, -0.06f); // kick back
        }

        // ───────────────────────── Targeting ─────────────────────────

        static Vector3 AimPoint(Zombie z) => z.transform.position + Vector3.up * (1.15f * z.transform.localScale.y);

        bool IsValid(Zombie z, float r)
        {
            if (z == null || z.IsDead || !z.isActiveAndEnabled) return false;
            if ((AimPoint(z) - muzzle.position).sqrMagnitude > r * r) return false;
            return CanSee(z, r);
        }

        Zombie FindTarget(float r)
        {
            Zombie best = null;
            float bestSqr = r * r;
            IReadOnlyList<Zombie> all = Zombie.All;
            for (int i = 0; i < all.Count; i++)
            {
                var z = all[i];
                if (z == null || z.IsDead || !z.isActiveAndEnabled) continue;
                float d = (AimPoint(z) - muzzle.position).sqrMagnitude;
                if (d >= bestSqr) continue;
                if (!CanSee(z, r)) continue;
                best = z;
                bestSqr = d;
            }
            return best;
        }

        /// True if nothing solid (wall, shelf...) is between the muzzle and the zombie.
        bool CanSee(Zombie z, float r)
        {
            Vector3 from = muzzle.position, to = AimPoint(z);
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.01f) return true;
            if (!Physics.Raycast(from, d / dist, out RaycastHit hit, Mathf.Min(dist, r), ~0, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.GetComponentInParent<Zombie>() != null; // hitting any zombie is fine: it's in the way anyway
        }
    }
}
