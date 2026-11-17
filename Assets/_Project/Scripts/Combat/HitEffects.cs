using System.Collections.Generic;
using UnityEngine;

namespace LastCheckout.Combat
{
    /// <summary>
    /// Little visual effects for bullet hits, made from code so you don't need any art files:
    ///   Blood  = red particles spraying out of a zombie
    ///   Impact = dusty sparks + a dark bullet hole on walls, shelves and floors
    /// </summary>
    public static class HitEffects
    {
        const int MaxBulletHoles = 40; // oldest holes disappear first, so the scene never fills up

        static Material particleMaterial, holeMaterial;
        static readonly Queue<GameObject> holes = new Queue<GameObject>();

        public static void Blood(Vector3 point, Vector3 normal)
            => Burst(point, normal, new Color(0.6f, 0.02f, 0.02f), count: 18, size: 0.08f, speed: 3.5f);

        public static void Impact(Vector3 point, Vector3 normal)
        {
            Burst(point, normal, new Color(0.9f, 0.82f, 0.6f), count: 8, size: 0.04f, speed: 2.5f);
            BulletHole(point, normal);
        }

        // ---------- Particle burst ----------

        static void Burst(Vector3 point, Vector3 normal, Color color, int count, float size, float speed)
        {
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;

            var go = new GameObject("Hit Effect");
            go.transform.SetPositionAndRotation(point + normal * 0.02f, Quaternion.LookRotation(normal));

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // it auto-starts; stop it so we can set it up

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;

            go.GetComponent<ParticleSystemRenderer>().material = GetParticleMaterial();

            ps.Play();
            Object.Destroy(go, 1.5f);
        }

        // ---------- Bullet hole ----------

        static void BulletHole(Vector3 point, Vector3 normal)
        {
            if (normal.sqrMagnitude < 0.001f) return;

            var hole = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hole.name = "Bullet Hole";
            Object.Destroy(hole.GetComponent<Collider>());

            hole.transform.position = point + normal * 0.01f;       // just off the surface so it doesn't flicker
            hole.transform.rotation = Quaternion.LookRotation(-normal);
            hole.transform.localScale = Vector3.one * Random.Range(0.06f, 0.1f);

            var r = hole.GetComponent<Renderer>();
            r.sharedMaterial = GetHoleMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            holes.Enqueue(hole);
            while (holes.Count > MaxBulletHoles)
            {
                var oldest = holes.Dequeue();
                if (oldest != null) Object.Destroy(oldest);
            }
        }

        // ---------- Materials (created once, then reused) ----------

        static Material GetParticleMaterial()
        {
            if (particleMaterial != null) return particleMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            particleMaterial = new Material(shader);
            return particleMaterial;
        }

        static Material GetHoleMaterial()
        {
            if (holeMaterial != null) return holeMaterial;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            holeMaterial = new Material(shader);
            holeMaterial.color = new Color(0.03f, 0.03f, 0.03f, 0.9f);
            return holeMaterial;
        }
    }
}
