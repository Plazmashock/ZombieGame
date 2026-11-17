using System.Collections.Generic;
using UnityEngine;
using LastCheckout.Items;

namespace LastCheckout.Combat
{
    /// <summary>
    /// A flying arrow from the bow. Unlike bullets it's a real object: it travels, drops a little,
    /// passes through one zombie (pierce) and sticks in whatever stops it.
    /// Each frame it checks the path it's about to travel with a raycast, so it can't skip through thin things.
    /// </summary>
    public class Arrow : MonoBehaviour
    {
        const float Gravity = 4f;      // gentler than real gravity: arrows fly fairly flat
        const float MaxLife = 6f;

        Vector3 velocity;
        float damage, knockback, stun, born;
        int pierceLeft;
        PlayerShooting owner;
        bool stuck;
        readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();

        static Material shaftMat, featherMat, tipMat;

        public static void Spawn(Vector3 position, Vector3 velocity, float damage, int pierce, float knockback, float stun, PlayerShooting owner)
        {
            var go = new GameObject("Arrow");
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(velocity));
            BuildLook(go.transform);
            var a = go.AddComponent<Arrow>();
            a.velocity = velocity;
            a.damage = damage;
            a.pierceLeft = pierce;
            a.knockback = knockback;
            a.stun = stun;
            a.owner = owner;
            a.born = Time.time;
        }

        /// The arrow model: shaft along +Z with the tip at the front.
        public static void BuildLook(Transform parent)
        {
            if (shaftMat == null)
            {
                shaftMat = Game.LevelKit.ColorMaterial(new Color(0.55f, 0.4f, 0.22f), 0.3f);
                featherMat = Game.LevelKit.ColorMaterial(new Color(0.85f, 0.15f, 0.1f), 0.2f);
                tipMat = Game.LevelKit.ColorMaterial(new Color(0.7f, 0.72f, 0.75f), 0.8f);
            }
            Part(parent, new Vector3(0f, 0f, -0.33f), new Vector3(0.012f, 0.012f, 0.68f), shaftMat);
            Part(parent, new Vector3(0f, 0f, 0.03f), new Vector3(0.022f, 0.022f, 0.06f), tipMat);
            Part(parent, new Vector3(0f, 0.012f, -0.6f), new Vector3(0.003f, 0.025f, 0.1f), featherMat);
            Part(parent, new Vector3(0f, -0.012f, -0.6f), new Vector3(0.003f, 0.025f, 0.1f), featherMat);
            Part(parent, new Vector3(0.012f, 0f, -0.6f), new Vector3(0.025f, 0.003f, 0.1f), featherMat);
        }

        static void Part(Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Game.LevelKit.RemoveCollider(p);
            p.transform.SetParent(parent, false);
            p.transform.localPosition = localPos;
            p.transform.localScale = size;
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            if (stuck) return;
            if (Time.time - born > MaxLife) { Destroy(gameObject); return; }

            float dt = Time.deltaTime;
            velocity += Vector3.down * Gravity * dt;
            Vector3 step = velocity * dt;
            Vector3 from = transform.position;

            var hits = Physics.RaycastAll(from, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (owner != null && hit.collider.transform.IsChildOf(owner.transform)) continue; // not the shooter

                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target == null) { StickInto(hit.point, hit.normal, hit.collider.transform, wall: true); return; }
                if (target.IsDead || !alreadyHit.Add(target)) continue;

                target.TakeDamage(damage);
                HitEffects.Blood(hit.point, -velocity.normalized);
                if (!target.IsDead) PlayerShooting.HitReaction(target, velocity.normalized, knockback, stun);
                if (owner != null) owner.RegisterHit(target.IsDead);

                if (pierceLeft-- <= 0) { StickInto(hit.point, hit.normal, hit.collider.transform, wall: false); return; }
            }

            transform.position = from + step;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
        }

        void StickInto(Vector3 point, Vector3 normal, Transform what, bool wall)
        {
            stuck = true;
            transform.position = point - velocity.normalized * 0.05f; // tip slightly buried
            transform.SetParent(what, true);                           // moves with a zombie, falls with its body
            if (wall) HitEffects.Impact(point, normal);
            Destroy(gameObject, wall ? 20f : 8f);
        }
    }
}
