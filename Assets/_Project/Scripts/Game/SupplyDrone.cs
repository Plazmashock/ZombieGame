using UnityEngine;

namespace LastCheckout.Game
{
    /// <summary>
    /// The supply drone. After every cleared wave it flies in from the edge of the map, hovers over the parking lot,
    /// lowers a SupplyDropCrate on a rope, lets go and flies away.
    ///
    ///   Dispatch() → Arrive → Lower → Release → Leave → (drone destroyed, crate stays until the next drop)
    /// </summary>
    public class SupplyDrone : MonoBehaviour
    {
        enum Stage { Arrive, Lower, Release, Leave }

        const float CruiseHeight = 16f;
        const float HoverHeight = 7.5f;

        static SupplyDropCrate lastCrate;
        static AudioClip humClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { lastCrate = null; humClip = null; }

        Stage stage = Stage.Arrive;
        Vector3 dropPoint, startPoint, exitPoint;
        SupplyDropCrate crate;
        Transform rope;
        Transform[] rotors = new Transform[4];
        Light lamp;
        float timer;

        /// <summary>Sends a drone to a random spot in the parking lot. The previous crate (and leftover food) is removed.</summary>
        public static SupplyDrone Dispatch()
        {
            if (lastCrate != null) lastCrate.Clear();

            Vector2 p = new Vector2(Random.Range(-14f, 14f), Random.Range(-36f, -24f));
            var go = new GameObject("Supply Drone");
            var drone = go.AddComponent<SupplyDrone>();
            drone.dropPoint = new Vector3(p.x, 0f, p.y);

            // Comes in from the left or right edge, leaves the other way.
            float side = Random.value < 0.5f ? -1f : 1f;
            drone.startPoint = new Vector3(side * 60f, CruiseHeight, p.y + Random.Range(-8f, 8f));
            drone.exitPoint = new Vector3(-side * 60f, CruiseHeight + 4f, p.y + Random.Range(-10f, 10f));
            go.transform.position = drone.startPoint;
            drone.BuildBody();

            drone.crate = SupplyDropCrate.Build(drone.startPoint);
            lastCrate = drone.crate;
            return drone;
        }

        void BuildBody()
        {
            Color dark = new Color(0.12f, 0.13f, 0.14f), grey = new Color(0.35f, 0.37f, 0.38f), orange = new Color(0.95f, 0.5f, 0.1f);

            Part("Body", PrimitiveType.Cube, Vector3.zero, new Vector3(1.6f, 0.45f, 1.1f), dark);
            Part("Top", PrimitiveType.Cube, new Vector3(0f, 0.27f, 0f), new Vector3(1.2f, 0.12f, 0.8f), orange);
            Part("Nose", PrimitiveType.Sphere, new Vector3(0f, -0.05f, 0.6f), new Vector3(0.45f, 0.3f, 0.3f), grey);
            Part("Winch", PrimitiveType.Cylinder, new Vector3(0f, -0.3f, 0f), new Vector3(0.3f, 0.08f, 0.3f), grey);

            int i = 0;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    Vector3 arm = new Vector3(sx * 1.25f, 0.1f, sz * 0.9f);
                    var a = Part("Arm", PrimitiveType.Cube, arm * 0.5f, new Vector3(0.1f, 0.08f, 1.9f), grey);
                    a.transform.localRotation = Quaternion.LookRotation(arm.normalized);
                    a.transform.localScale = new Vector3(0.1f, 0.08f, arm.magnitude);
                    Part("Motor", PrimitiveType.Cylinder, arm, new Vector3(0.22f, 0.12f, 0.22f), dark);
                    var rotor = new GameObject("Rotor").transform;
                    rotor.SetParent(transform, false);
                    rotor.localPosition = arm + Vector3.up * 0.16f;
                    var blade = Part("Blade", PrimitiveType.Cube, Vector3.zero, new Vector3(1.1f, 0.015f, 0.09f), new Color(0.06f, 0.06f, 0.06f), rotor);
                    var blade2 = Part("Blade2", PrimitiveType.Cube, Vector3.zero, new Vector3(0.09f, 0.015f, 1.1f), new Color(0.06f, 0.06f, 0.06f), rotor);
                    rotors[i++] = rotor;
                }

            // Red / green nav lights and a spotlight on the ground.
            Part("Nav Red", PrimitiveType.Sphere, new Vector3(-0.8f, 0.05f, -0.55f), Vector3.one * 0.12f, new Color(1f, 0.1f, 0.1f), null, 3f);
            Part("Nav Green", PrimitiveType.Sphere, new Vector3(0.8f, 0.05f, -0.55f), Vector3.one * 0.12f, new Color(0.2f, 1f, 0.3f), null, 3f);

            var lg = new GameObject("Spot");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, -0.3f, 0.3f);
            lg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lamp = lg.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.spotAngle = 55f;
            lamp.range = 22f;
            lamp.intensity = 6f;
            lamp.color = new Color(1f, 0.95f, 0.8f);
            lamp.shadows = LightShadows.None;

            // Rope (a thin box, stretched between the winch and the crate every frame).
            rope = Part("Rope", PrimitiveType.Cube, Vector3.zero, new Vector3(0.03f, 1f, 0.03f), new Color(0.1f, 0.1f, 0.1f)).transform;
            rope.SetParent(null, true);
            rope.gameObject.SetActive(false);

            // Engine hum, 3D so it gets louder as it flies over you.
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = HumClip();
            src.loop = true;
            src.spatialBlend = 1f;
            src.minDistance = 6f;
            src.maxDistance = 70f;
            src.volume = 0.55f;
            src.Play();
        }

        GameObject Part(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Color c, Transform parent = null, float glow = 0f)
        {
            var go = GameObject.CreatePrimitive(type);
            LevelKit.RemoveCollider(go);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = LevelKit.ColorMaterial(c, 0.35f, glow);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static AudioClip HumClip()
        {
            if (humClip != null) return humClip;
            const int rate = 22050;
            const float length = 1f;                 // one second loops cleanly: all frequencies are whole numbers
            int n = (int)(rate * length);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float s = Mathf.Sin(2f * Mathf.PI * 92f * t) * 0.5f
                        + Mathf.Sin(2f * Mathf.PI * 184f * t) * 0.25f
                        + Mathf.Sin(2f * Mathf.PI * 46f * t) * 0.3f
                        + Mathf.Sin(2f * Mathf.PI * 7f * t) * 0.15f * Mathf.Sin(2f * Mathf.PI * 276f * t);
                data[i] = s * 0.35f;
            }
            humClip = AudioClip.Create("Drone Hum", n, 1, rate, false);
            humClip.SetData(data, 0);
            return humClip;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var r in rotors) if (r != null) r.Rotate(0f, 1800f * dt, 0f);

            Vector3 hover = new Vector3(dropPoint.x, HoverHeight, dropPoint.z);
            const float crateHang = 4.5f;   // rope length when fully lowered

            switch (stage)
            {
                case Stage.Arrive:
                    transform.position = Vector3.MoveTowards(transform.position, new Vector3(hover.x, Mathf.Lerp(CruiseHeight, HoverHeight, 0.0f), hover.z), 26f * dt);
                    // descend only once nearly above the drop point
                    float flat = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(hover.x, hover.z));
                    if (flat < 20f)
                    {
                        var p = transform.position;
                        p.y = Mathf.MoveTowards(p.y, HoverHeight, 6f * dt);
                        transform.position = p;
                    }
                    Tilt(hover - transform.position);
                    if (flat < 0.1f && Mathf.Abs(transform.position.y - HoverHeight) < 0.1f)
                    { stage = Stage.Lower; timer = 0f; }
                    HangCrate(0.6f);
                    break;

                case Stage.Lower:
                    timer += dt;
                    float len = Mathf.Lerp(0.6f, HoverHeight - 0.05f, Mathf.SmoothStep(0f, 1f, timer / 5f));
                    HangCrate(len);
                    transform.position = hover + new Vector3(Mathf.Sin(Time.time * 1.3f) * 0.15f, Mathf.Sin(Time.time * 2f) * 0.08f, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.identity, 4f * dt);
                    if (timer >= 5f) { stage = Stage.Release; timer = 0f; }
                    break;

                case Stage.Release:
                    timer += dt;
                    HangCrate(HoverHeight - 0.05f);
                    if (timer > 0.8f)
                    {
                        crate.transform.position = dropPoint;
                        crate.Landed();
                        rope.gameObject.SetActive(false);
                        if (GameDirector.Instance != null)
                            GameDirector.Instance.Say("SUPPLY DROP DELIVERED", "Follow the green beacon in the parking lot. Press E on the crate.", false);
                        stage = Stage.Leave;
                    }
                    break;

                case Stage.Leave:
                    transform.position = Vector3.MoveTowards(transform.position, exitPoint, 22f * dt);
                    var pos = transform.position;
                    pos.y = Mathf.MoveTowards(pos.y, exitPoint.y, 5f * dt);
                    transform.position = pos;
                    Tilt(exitPoint - transform.position);
                    if ((transform.position - exitPoint).sqrMagnitude < 4f)
                    {
                        if (rope != null) Destroy(rope.gameObject);
                        Destroy(gameObject);
                    }
                    break;
            }
        }

        /// Hangs the crate 'len' metres under the drone and stretches the rope between them.
        void HangCrate(float len)
        {
            if (crate == null) return;
            Vector3 top = transform.position + Vector3.down * 0.35f;
            // The crate's origin is its base; its top is 0.7 m higher.
            Vector3 cratePos = new Vector3(top.x, top.y - len - 0.75f, top.z);
            if (cratePos.y < dropPoint.y) cratePos.y = dropPoint.y;
            crate.transform.position = cratePos;
            crate.transform.rotation = Quaternion.Euler(0f, Time.time * 8f, 0f);

            Vector3 a = top, b = cratePos + Vector3.up * 0.75f;
            rope.gameObject.SetActive(true);
            rope.position = (a + b) * 0.5f;
            rope.localScale = new Vector3(0.03f, Mathf.Max(0.01f, (a - b).magnitude), 0.03f);
            rope.rotation = Quaternion.identity;
        }

        void Tilt(Vector3 toward)
        {
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.01f) { transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.identity, 3f * Time.deltaTime); return; }
            Quaternion want = Quaternion.LookRotation(toward.normalized) * Quaternion.Euler(10f, 0f, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 3f * Time.deltaTime);
        }

        void OnDestroy()
        {
            if (rope != null) Destroy(rope.gameObject);
        }
    }
}
