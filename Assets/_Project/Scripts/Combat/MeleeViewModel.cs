using UnityEngine;

namespace LastCheckout.Combat
{
    /// <summary>
    /// The melee weapon you SEE in your hand (knife, bat, machete, axe). Built by GunModelFactory.
    ///   - shows only while that weapon is equipped
    ///   - swings across the screen every time PlayerMelee swings (speed matches the weapon)
    ///   - sways while you walk, pulls back near walls
    /// </summary>
    public class MeleeViewModel : MonoBehaviour
    {
        string weaponId;
        Transform model;
        PlayerShooting shooting;
        PlayerMelee melee;
        CharacterController body;
        Transform cam;
        Vector3 restPosition;
        Quaternion restRotation;
        bool visible;
        float swingT = -1f, swingDuration = 0.3f, bobTime, tuck;

        public string WeaponId => weaponId;

        public void Configure(string id, Transform modelRoot)
        {
            weaponId = id;
            model = modelRoot;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            shooting = GetComponentInParent<PlayerShooting>();
            melee = GetComponentInParent<PlayerMelee>();
            body = GetComponentInParent<CharacterController>();
            var c = GetComponentInParent<Camera>();
            cam = c != null ? c.transform : null;
            if (melee != null) { melee.Swung -= OnSwung; melee.Swung += OnSwung; }
            SetVisible(false);
        }

        void OnDestroy() { if (melee != null) melee.Swung -= OnSwung; }

        void OnSwung(bool hit)
        {
            if (!visible) return;
            swingT = 0f;
            swingDuration = melee != null ? Mathf.Max(0.12f, melee.SwingDuration) : 0.3f;
        }

        void Update()
        {
            bool show = shooting != null && shooting.CurrentWeaponId == weaponId;
            if (show != visible) SetVisible(show);
            if (!visible) return;

            float dt = Time.deltaTime;

            // Swing: a quick chop down and across, then back to rest.
            float s = 0f;
            if (swingT >= 0f)
            {
                swingT += dt / swingDuration;
                if (swingT >= 1f) swingT = -1f;
                else s = Mathf.Sin(swingT * Mathf.PI);
            }

            float speed = 0f;
            if (body != null) { Vector3 v = body.velocity; v.y = 0f; speed = v.magnitude; }
            bobTime += speed * dt * 1.6f;
            float bob = Mathf.Clamp01(speed / 4f);
            Vector3 bobOffset = new Vector3(Mathf.Sin(bobTime) * 0.008f, -Mathf.Abs(Mathf.Sin(bobTime)) * 0.01f, 0f) * bob;

            tuck = Mathf.Lerp(tuck, ViewModelTuck.Target(cam), 1f - Mathf.Exp(-12f * dt));

            Vector3 position = restPosition + bobOffset + new Vector3(-0.2f * s, -0.04f * s, 0.12f * s) + ViewModelTuck.Offset(tuck);
            Quaternion rotation = ViewModelTuck.Turn(tuck) * restRotation * Quaternion.Euler(80f * s, 0f, 45f * s);
            position += LastCheckout.Player.FirstPersonFeel.WeaponPos;
            rotation = LastCheckout.Player.FirstPersonFeel.WeaponRot * rotation;
            transform.SetLocalPositionAndRotation(position, rotation);
        }

        void SetVisible(bool value)
        {
            visible = value;
            swingT = -1f;
            if (model != null) model.gameObject.SetActive(value);
        }
    }
}
