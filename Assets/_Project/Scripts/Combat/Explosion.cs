using UnityEngine;

namespace LastCheckout.Combat
{
    /// <summary>
    /// The bloater's blast. Hurts EVERYTHING nearby (you and other zombies), less the further away it is,
    /// and is loud enough to bring the rest of the horde over.
    /// Tip: shoot a bloater while it's walking through a crowd.
    /// </summary>
    public class Explosion : MonoBehaviour
    {
        const float Duration = 0.55f;

        Light flash;
        Transform ball;
        float born, radius;

        /// <param name="source">The thing exploding (it doesn't damage itself).</param>
        public static void Detonate(Vector3 position, float radius, float maxDamage, Component source)
        {
            // Damage with fall-off: full damage at the centre, a quarter at the edge.
            var done = new System.Collections.Generic.HashSet<IDamageable>();
            foreach (var col in Physics.OverlapSphere(position, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead || !done.Add(target)) continue;
                if (source != null && col.transform.IsChildOf(source.transform)) continue;

                float d = Vector3.Distance(position, col.bounds.ClosestPoint(position)); // bounds work for every collider type
                float falloff = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(d / radius));
                bool isPlayer = target is PlayerHealth;
                target.TakeDamage(maxDamage * falloff * (isPlayer ? 0.35f : 1f)); // the player takes much less
                if (!target.IsDead)
                {
                    Vector3 away = (col.transform.position - position).normalized;
                    PlayerShooting.HitReaction(target, away, 6f * falloff, 0.8f);
                }
            }

            NoiseMaker.Emit(position, 35f);
            for (int i = 0; i < 6; i++) HitEffects.Blood(position + Random.insideUnitSphere * 0.6f, Random.onUnitSphere + Vector3.up);

            var go = new GameObject("Explosion");
            go.transform.position = position;
            go.AddComponent<Explosion>().Init(radius);
        }

        void Init(float r)
        {
            radius = r;
            born = Time.time;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Game.LevelKit.RemoveCollider(sphere);
            sphere.name = "Fireball";
            ball = sphere.transform;
            ball.SetParent(transform, false);
            ball.localScale = Vector3.zero;
            var rend = sphere.GetComponent<Renderer>();
            rend.sharedMaterial = Game.LevelKit.ColorMaterial(new Color(1f, 0.55f, 0.15f), 0f, 4f);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            flash = gameObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.6f, 0.25f);
            flash.range = radius * 3f;
            flash.intensity = 10f;
            flash.shadows = LightShadows.None;
        }

        void Update()
        {
            float t = (Time.time - born) / Duration;
            if (t >= 1f) { Destroy(gameObject); return; }
            // Fireball pops out fast, then shrinks away.
            float size = t < 0.25f ? Mathf.Lerp(0.2f, 1.6f, t / 0.25f) : Mathf.Lerp(1.6f, 0f, (t - 0.25f) / 0.75f);
            ball.localScale = Vector3.one * size * radius * 0.6f;
            flash.intensity = 10f * (1f - t);
        }
    }
}
