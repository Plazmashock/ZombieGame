using UnityEngine;

namespace LastCheckout.Combat
{
    /// <summary>
    /// The visible bullet: a short glowing streak that races from the gun to where the shot landed,
    /// then disappears. Purely decoration (the real hit was already decided by the laser in PlayerShooting).
    /// </summary>
    public class BulletTrail : MonoBehaviour
    {
        const float Speed = 220f;        // meters per second: fast, but you can still see it fly
        const float TailLength = 2.0f;   // how long the streak is

        static Material sharedMaterial;

        LineRenderer line;
        Vector3 start, direction;
        float length, travelled;

        public static void Spawn(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Bullet Trail");
            go.AddComponent<BulletTrail>().Init(from, to);
        }

        void Init(Vector3 from, Vector3 to)
        {
            start = from;
            length = Vector3.Distance(from, to);
            direction = length > 0.001f ? (to - from) / length : transform.forward;

            line = gameObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = 0.012f;   // thin at the back...
            line.endWidth = 0.05f;      // ...thick at the front, like a streak
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = GetMaterial(); // shared, so we don't create a new material for every bullet
            line.startColor = new Color(1f, 0.6f, 0.15f, 0f);   // fades in from the tail
            line.endColor = new Color(1f, 0.95f, 0.6f, 1f);

            Step();
        }

        void Update() => Step();

        void Step()
        {
            if (length < 0.05f) { Destroy(gameObject); return; }

            travelled += Speed * Time.deltaTime;
            float head = Mathf.Min(travelled, length);
            float tail = Mathf.Max(0f, travelled - TailLength);

            line.SetPosition(0, start + direction * tail);
            line.SetPosition(1, start + direction * head);

            if (tail >= length) Destroy(gameObject);
        }

        static Material GetMaterial()
        {
            if (sharedMaterial != null) return sharedMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            sharedMaterial = new Material(shader);
            return sharedMaterial;
        }
    }
}
