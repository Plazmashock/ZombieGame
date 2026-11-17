using UnityEngine;
using LastCheckout.Game;
using LastCheckout.Player;

namespace LastCheckout.Combat
{
    /// <summary>
    /// The two fists you SEE when no weapon is in your hands: leather-gloved fists coming out of olive jacket sleeves.
    ///   • shown only while no weapon is equipped (PlayerShooting.CurrentWeaponId == null)
    ///   • punches alternate right / left every time PlayerMelee swings (speed matches the swing time)
    ///   • sway, bob, sprint pose and "switch" animation come from FirstPersonFeel, like every other weapon
    ///   • pulls back near walls (ViewModelTuck)
    /// Built from simple boxes and cylinders at game start, so there is no model file to import.
    /// </summary>
    public class FistsViewModel : MonoBehaviour
    {
        class Hand
        {
            public Transform root;
            public Vector3 restPos;
            public Quaternion restRot;
            public float side;          // +1 right hand, -1 left hand
            public float punchT = -1f;  // 0 → 1 while punching, -1 = not punching
        }

        readonly Hand[] hands = new Hand[2];
        PlayerShooting shooting;
        PlayerMelee melee;
        CharacterController body;
        Transform cam;
        GameObject visual;
        bool visible;
        float bobTime, tuck, punchDuration = 0.3f;
        int nextHand; // 0 = right, 1 = left

        /// <summary>Builds the fists under the player's camera (once).</summary>
        public static FistsViewModel Create(PlayerShooting shooting, PlayerMelee melee)
        {
            if (shooting == null) return null;
            var camera = shooting.GetComponentInChildren<Camera>();
            if (camera == null) return null;
            var existing = camera.GetComponentInChildren<FistsViewModel>(true);
            if (existing != null) return existing;

            var go = new GameObject("Fists View");
            go.transform.SetParent(camera.transform, false);
            var vm = go.AddComponent<FistsViewModel>();
            vm.Build(shooting, melee, camera.transform);
            return vm;
        }

        void Build(PlayerShooting s, PlayerMelee m, Transform camera)
        {
            shooting = s;
            melee = m;
            cam = camera;
            body = s.GetComponent<CharacterController>();

            visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);

            var glove = LevelKit.ColorMaterial(new Color(0.11f, 0.09f, 0.08f), 0.28f);       // black-brown leather
            var knuckle = LevelKit.ColorMaterial(new Color(0.2f, 0.17f, 0.15f), 0.4f);        // worn knuckle pads
            var sleeve = LevelKit.ColorMaterial(new Color(0.2f, 0.23f, 0.16f), 0.08f);        // olive jacket
            var tape = LevelKit.ColorMaterial(new Color(0.62f, 0.6f, 0.52f), 0.1f);           // taped cuff

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                var root = BuildHand(visual.transform, side, glove, knuckle, sleeve, tape);
                var h = new Hand { root = root, side = side };
                h.restPos = new Vector3(side * 0.17f, -0.2f, 0.36f);
                h.restRot = Quaternion.Euler(-4f, -side * 9f, side * 6f);
                root.localPosition = h.restPos;
                root.localRotation = h.restRot;
                hands[i] = h;
            }

            if (melee != null) melee.Swung += OnSwung;
            SetVisible(false);
        }

        void OnDestroy()
        {
            if (melee != null) melee.Swung -= OnSwung;
        }

        static Transform BuildHand(Transform parent, float side, Material glove, Material knuckle, Material sleeve, Material tape)
        {
            var root = new GameObject(side > 0f ? "Right Fist" : "Left Fist").transform;
            root.SetParent(parent, false);

            // The fist: palm block, a row of knuckles on the front, curled fingers underneath, thumb across the front.
            Part(root, "Palm", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.092f, 0.064f, 0.115f), glove);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.033f + i * 0.022f;
                Part(root, "Knuckle", PrimitiveType.Cube, new Vector3(x, 0.012f, 0.066f), Vector3.zero, new Vector3(0.021f, 0.036f, 0.032f), knuckle);
                Part(root, "Finger", PrimitiveType.Cube, new Vector3(x, -0.02f, 0.056f), Vector3.zero, new Vector3(0.02f, 0.028f, 0.04f), glove);
            }
            Part(root, "Thumb", PrimitiveType.Cube, new Vector3(-side * 0.045f, 0.002f, 0.045f), new Vector3(0f, side * 22f, 0f), new Vector3(0.026f, 0.03f, 0.07f), glove);

            // Wrist and forearm go back towards the camera and down out of the screen.
            var arm = new GameObject("Arm").transform;
            arm.SetParent(root, false);
            arm.localRotation = Quaternion.Euler(-24f, -side * 12f, 0f);
            Part(arm, "Cuff", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.075f), new Vector3(90f, 0f, 0f), new Vector3(0.078f, 0.03f, 0.078f), tape);
            Part(arm, "Forearm", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.28f), new Vector3(90f, 0f, 0f), new Vector3(0.092f, 0.19f, 0.092f), sleeve);
            return root;
        }

        static void Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            LevelKit.RemoveCollider(go);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void OnSwung(bool hit)
        {
            if (!visible) return;
            var h = hands[nextHand];
            nextHand = 1 - nextHand;
            h.punchT = 0f;
            punchDuration = melee != null ? Mathf.Max(0.18f, melee.SwingDuration) : 0.3f;
        }

        void SetVisible(bool value)
        {
            visible = value;
            if (visual != null) visual.SetActive(value);
            foreach (var h in hands) if (h != null) h.punchT = -1f;
        }

        void Update()
        {
            bool show = shooting != null && shooting.enabled && shooting.CurrentWeaponId == null;
            if (show != visible) SetVisible(show);
            if (!visible) return;

            float dt = Time.deltaTime;
            float speed = 0f;
            if (body != null) { Vector3 v = body.velocity; v.y = 0f; speed = v.magnitude; }
            bobTime += speed * dt * 1.8f;
            float bob = Mathf.Clamp01(speed / 4f);

            if (cam != null) tuck = Mathf.Lerp(tuck, ViewModelTuck.Target(cam), 1f - Mathf.Exp(-12f * dt));

            // The whole pair follows the shared weapon motion (sway, sprint pose, raise on switch...).
            transform.SetLocalPositionAndRotation(
                FirstPersonFeel.WeaponPos + ViewModelTuck.Offset(tuck),
                ViewModelTuck.Turn(tuck) * FirstPersonFeel.WeaponRot);

            bool anyPunch = hands[0].punchT >= 0f || hands[1].punchT >= 0f;
            for (int i = 0; i < 2; i++)
            {
                var h = hands[i];
                float s = 0f;
                if (h.punchT >= 0f)
                {
                    h.punchT += dt / punchDuration;
                    if (h.punchT >= 1f) h.punchT = -1f;
                    else s = PunchCurve(h.punchT);
                }

                // Hands swing opposite to each other when walking.
                float phase = bobTime + (i == 0 ? 0f : Mathf.PI);
                Vector3 bobOffset = new Vector3(Mathf.Sin(phase) * 0.006f, -Mathf.Abs(Mathf.Sin(phase)) * 0.008f, Mathf.Cos(phase) * 0.01f) * bob;

                // Punch: shoots forward and in towards the crosshair; the other hand tucks back a little.
                Vector3 punch = new Vector3(-h.side * 0.13f * s, 0.075f * s, 0.34f * s);
                bool other = anyPunch && s <= 0f;
                if (other) punch += new Vector3(-h.side * 0.02f, 0.02f, -0.04f);

                Quaternion punchRot = Quaternion.Euler(-10f * s, -h.side * 14f * s, -h.side * 10f * s);
                h.root.localPosition = Vector3.Lerp(h.root.localPosition, h.restPos + bobOffset + punch, 1f - Mathf.Exp(-(s > 0f ? 40f : 18f) * dt));
                h.root.localRotation = Quaternion.Slerp(h.root.localRotation, h.restRot * punchRot, 1f - Mathf.Exp(-22f * dt));
            }
        }

        /// 0 → 1 → 0: fast snap out, slower return.
        static float PunchCurve(float t)
        {
            const float outPart = 0.32f;
            if (t < outPart) { float u = t / outPart; return 1f - (1f - u) * (1f - u); }
            float v = (t - outPart) / (1f - outPart);
            return 1f - v * v * (3f - 2f * v);
        }
    }
}
