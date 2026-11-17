using UnityEngine;
using LastCheckout.Combat;

namespace LastCheckout.Player
{
    /// <summary>
    /// Makes the first-person view feel alive. Lives on the Main Camera (added automatically by the GameDirector).
    ///
    ///   CAMERA (this object moves a little)            WEAPON (read by the gun / melee / bow / fists models)
    ///   • walking bob + sprint bob + crouch bob        • lags behind when you turn (sway)
    ///   • leans into strafing and turning              • rises into view when you switch weapons
    ///   • landing thump, small jump lift               • lowers and tilts while sprinting
    ///   • FOV widens while sprinting                   • breathes when you stand still
    ///   • recoil kick on shots, push on melee swings   • dips on landing
    ///   • flinch when you get hurt, falls over on death
    ///
    /// It only moves THIS camera object (the parent "CameraRoot" is still driven by PlayerLook / FirstPersonController),
    /// so looking and aiming are not affected.
    ///
    /// Weapon models are children of the camera, so they read WeaponPos / WeaponRot below and add them to their own pose.
    /// </summary>
    [DefaultExecutionOrder(-20)] // compute before the weapon models' Update
    public class FirstPersonFeel : MonoBehaviour
    {
        /// <summary>Extra offset for any weapon in your hands, in camera space (x right, y up, z forward).</summary>
        public static Vector3 WeaponPos;
        /// <summary>Extra turn for any weapon in your hands.</summary>
        public static Quaternion WeaponRot = Quaternion.identity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { WeaponPos = Vector3.zero; WeaponRot = Quaternion.identity; }

        /// <summary>A springy number: push it with Kick, it wobbles back to zero.</summary>
        struct Spring
        {
            public float x, v;
            public void Step(float dt, float stiffness, float damping)
            {
                v += (-stiffness * x - damping * v) * dt;
                x += v * dt;
            }
        }

        Camera cam;
        PlayerInputReader input;
        FirstPersonController motor;
        CharacterController body;
        PlayerShooting shooting;
        PlayerMelee melee;
        PlayerHealth health;

        Vector3 baseLocalPos;
        Quaternion baseLocalRot;
        float baseFov;

        float bobPhase, bobAmount, sprintBlend, crouchBlend, idleTime;
        float strafeRoll, turnRoll;
        float lastHealth, deathT;
        float lastShotTime = -10f;
        bool wasGrounded = true;
        float lastVy;
        Spring landY, kickPitch, kickRoll, hurtX, jumpY;
        Vector3 swayRot, swayPos;
        float airWeaponY;

        void Awake()
        {
            cam = GetComponent<Camera>();
            baseLocalPos = transform.localPosition;
            baseLocalRot = transform.localRotation;
            baseFov = cam != null ? cam.fieldOfView : 70f;

            var player = GetComponentInParent<PlayerInputReader>();
            if (player == null) { enabled = false; return; }
            input = player;
            motor = player.GetComponent<FirstPersonController>();
            body = player.GetComponent<CharacterController>();
            shooting = player.GetComponent<PlayerShooting>();
            melee = player.GetComponent<PlayerMelee>();
            health = player.GetComponent<PlayerHealth>();
            lastHealth = health != null ? health.Current : 0f;
        }

        void OnEnable()
        {
            if (shooting != null) shooting.Fired += OnFired;
            if (melee != null) melee.Swung += OnSwung;
            if (health != null) health.Changed += OnHealthChanged;
        }

        void OnDisable()
        {
            if (shooting != null) shooting.Fired -= OnFired;
            if (melee != null) melee.Swung -= OnSwung;
            if (health != null) health.Changed -= OnHealthChanged;
            WeaponPos = Vector3.zero;
            WeaponRot = Quaternion.identity;
        }

        // ───────────────────────── Events ─────────────────────────

        void OnFired(bool hit)
        {
            lastShotTime = Time.time;
            // Degrees the view jumps up per shot. Kept small and quick to settle: it never changes where bullets land by much.
            float kick = 1.2f;
            switch (shooting != null ? shooting.CurrentWeaponId : null)
            {
                case "pistol": kick = 1.5f; break;
                case "revolver": kick = 2.6f; break;
                case "shotgun": kick = 3.6f; break;
                case "smg": kick = 0.6f; break;
                case "rifle": kick = 0.9f; break;
                case "bow": kick = 0.8f; break;
            }
            kickPitch.v += -kick * 22f;                    // negative pitch = look up
            kickRoll.v += Random.Range(-1f, 1f) * kick * 8f;
        }

        void OnSwung(bool hit)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            kickPitch.v += (hit ? 34f : 22f);              // chop down
            kickRoll.v += side * (hit ? 60f : 40f);
        }

        void OnHealthChanged()
        {
            if (health == null) return;
            if (health.Current < lastHealth - 0.01f)
            {
                float hurt = Mathf.Clamp01((lastHealth - health.Current) / 30f);
                hurtX.v += Random.Range(-1f, 1f) * (60f + 120f * hurt);
                kickPitch.v += 40f + 90f * hurt;
            }
            lastHealth = health.Current;
        }

        // ───────────────────────── Weapon pose (Update) ─────────────────────────

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            // --- what is the body doing? ---
            Vector3 vel = body != null ? body.velocity : Vector3.zero;
            float vy = vel.y;
            vel.y = 0f;
            float speed = vel.magnitude;
            bool grounded = motor != null ? motor.IsGrounded : true;
            bool sprinting = motor != null && motor.IsSprinting && speed > 2f;
            bool crouching = motor != null && motor.IsCrouching;
            // landing thump / jump lift
            if (grounded && !wasGrounded && lastVy < -3f)
            {
                float impact = Mathf.Clamp01((-lastVy - 3f) / 9f);
                landY.v -= (0.9f + 2.6f * impact);
                kickPitch.v += 25f * impact;
            }
            if (!grounded && wasGrounded && vy > 2f) jumpY.v += 0.5f;
            wasGrounded = grounded;
            lastVy = vy;

            sprintBlend = Mathf.MoveTowards(sprintBlend, sprinting ? 1f : 0f, dt * 5f);
            crouchBlend = Mathf.MoveTowards(crouchBlend, crouching ? 1f : 0f, dt * 6f);
            float target = grounded ? Mathf.Clamp01(speed / 3.5f) : 0f;
            bobAmount = Mathf.Lerp(bobAmount, target, 1f - Mathf.Exp(-9f * dt));
            bobPhase += speed * dt * (1.0f + 0.3f * sprintBlend);   // slower steps (the player is faster now)
            idleTime += dt;

            // --- mouse movement drives weapon sway and turn-lean ---
            Vector2 look = input != null ? input.Look : Vector2.zero;       // pixels this frame
            float degPerPx = 0.12f;
            float yawRate = Mathf.Clamp(look.x * degPerPx / dt, -400f, 400f);   // degrees per second
            float pitchRate = Mathf.Clamp(look.y * degPerPx / dt, -400f, 400f);
            Vector3 swayTarget = new Vector3(Mathf.Clamp(pitchRate * 0.010f, -3.5f, 3.5f),   // weapon pitches opposite to the look
                                             Mathf.Clamp(-yawRate * 0.012f, -4.5f, 4.5f),
                                             Mathf.Clamp(-yawRate * 0.010f, -4f, 4f));
            swayRot = Vector3.Lerp(swayRot, swayTarget, 1f - Mathf.Exp(-12f * dt));
            Vector3 swayPosTarget = new Vector3(Mathf.Clamp(-yawRate * 0.00007f, -0.03f, 0.03f), Mathf.Clamp(pitchRate * 0.00005f, -0.02f, 0.02f), 0f);
            swayPos = Vector3.Lerp(swayPos, swayPosTarget, 1f - Mathf.Exp(-10f * dt));

            // --- weapon pose ---
            float sinceShot = Time.time - lastShotTime;
            float sprintPose = sprintBlend * Mathf.Clamp01((sinceShot - 0.1f) / 0.35f);   // not while firing
            float equipAge = shooting != null ? Time.time - shooting.EquippedAt : 99f;
            float raise = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(equipAge / 0.34f)); // 1 = just switched, 0 = ready
            float breathe = Mathf.Sin(idleTime * 1.5f) * (1f - bobAmount);
            airWeaponY = Mathf.Lerp(airWeaponY, Mathf.Clamp(-vy * 0.006f, -0.05f, 0.06f), 1f - Mathf.Exp(-10f * dt));

            Vector3 pos = swayPos;
            pos += new Vector3(0.045f, -0.07f, -0.035f) * sprintPose;                         // sprint: lowered + pulled in
            pos += new Vector3(0.02f, -0.015f, 0f) * crouchBlend;
            pos += new Vector3(0f, -0.26f * raise, -0.10f * raise);                            // switch: comes up from below
            pos.y += breathe * 0.0022f + airWeaponY + landY.x * 0.5f;

            Quaternion rot = Quaternion.Euler(swayRot.x, swayRot.y, swayRot.z)
                           * Quaternion.Euler(16f * sprintPose + 42f * raise, -22f * sprintPose - 10f * raise, 8f * sprintPose - 14f * raise)
                           * Quaternion.Euler(breathe * 0.35f, 0f, 0f);

            WeaponPos = pos;
            WeaponRot = rot;
        }

        // ───────────────────────── Camera (LateUpdate: after everything else moved) ─────────────────────────

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            // springs
            landY.Step(dt, 170f, 14f);
            jumpY.Step(dt, 170f, 14f);
            kickPitch.Step(dt, 190f, 17f);
            kickRoll.Step(dt, 190f, 17f);
            hurtX.Step(dt, 150f, 11f);

            bool dead = health != null && health.IsDead;
            if (dead) deathT = Mathf.Min(1f, deathT + dt * 1.1f); else deathT = Mathf.Max(0f, deathT - dt * 3f);
            float death = Mathf.SmoothStep(0f, 1f, deathT);

            // walking bob: up-down twice per stride, side-to-side once
            float amp = bobAmount * (1f + 0.3f * sprintBlend) * (1f - 0.4f * crouchBlend);
            float bobY = Mathf.Sin(bobPhase * 2f) * 0.010f * amp;
            float bobX = Mathf.Cos(bobPhase) * 0.006f * amp;
            float bobRoll = Mathf.Cos(bobPhase) * 0.25f * amp;
            float bobPitch = Mathf.Sin(bobPhase * 2f + 0.6f) * 0.12f * amp;
            float idleY = Mathf.Sin(idleTime * 1.5f) * 0.0025f * (1f - bobAmount);

            // lean into strafing and turning
            Vector3 vel = body != null ? body.velocity : Vector3.zero;
            vel.y = 0f;
            float strafe = body != null ? Vector3.Dot(vel, body.transform.right) / 6f : 0f;
            strafeRoll = Mathf.Lerp(strafeRoll, -Mathf.Clamp(strafe, -1f, 1f) * 1.8f, 1f - Mathf.Exp(-8f * dt));
            Vector2 look = input != null ? input.Look : Vector2.zero;
            float turn = Mathf.Clamp(look.x * 0.12f / dt, -300f, 300f);
            turnRoll = Mathf.Lerp(turnRoll, -turn * 0.0045f, 1f - Mathf.Exp(-9f * dt));

            float lean = 1.1f * sprintBlend;   // sprinting: head dips forward a touch

            Vector3 offset = new Vector3(bobX + hurtX.x * 0.0006f, bobY + idleY + landY.x * 0.35f + jumpY.x * 0.5f - 0.85f * death, 0f);
            float roll = bobRoll + strafeRoll + turnRoll + kickRoll.x * 0.35f + hurtX.x * 0.12f + 62f * death;
            float pitch = bobPitch + kickPitch.x + lean - landY.x * 6f + 12f * death;

            transform.localPosition = baseLocalPos + offset;
            transform.localRotation = baseLocalRot * Quaternion.Euler(pitch, 0f, roll);

            // sprinting opens the view up a little
            if (cam != null)
            {
                float fov = baseFov + 7f * sprintBlend + (health != null && health.IsDead ? -6f * death : 0f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-7f * dt));
            }
        }
    }
}
