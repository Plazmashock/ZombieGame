using UnityEngine;

namespace LastCheckout.Combat
{
    /// <summary>
    /// The pistol you SEE in your hand (first-person "view model").
    /// The gun parts are built by the menu item Last Checkout > Upgrade Gun And Graphics.
    /// This script only brings it to life:
    ///   - shows it when the pistol is equipped, hides it otherwise
    ///   - recoil kick + muzzle flash on every shot
    ///   - dips down while reloading
    ///   - gentle sway while you walk
    ///   - pulls back and turns aside when you're close to a wall, so it never pokes through it
    /// It never changes gameplay: damage and ammo live in PlayerShooting.
    /// </summary>
    public class GunViewModel : MonoBehaviour
    {
        [Tooltip("Which gun this model belongs to: pistol, shotgun or rifle (see WeaponCatalog).")]
        [SerializeField] string weaponId = "pistol";
        [Tooltip("The holder object with all the gun parts inside. Hidden when this gun isn't equipped.")]
        [SerializeField] Transform model;
        [Tooltip("The end of the barrel. Bullet streaks start here.")]
        [SerializeField] Transform muzzle;
        [SerializeField] GameObject muzzleFlash;
        [SerializeField] Light muzzleLight;

        public Transform Muzzle => muzzle;
        public string WeaponId => weaponId;

        PlayerShooting shooting;
        CharacterController body;
        Vector3 restPosition;
        Quaternion restRotation;
        float kick, reloadBlend, bobTime, flashUntil, tuck;
        Transform cam;
        bool visible;
        Vector3 flashBaseScale = Vector3.one; // the flash's normal (tiny) size, remembered so we only scale RELATIVE to it

        void Awake()
        {
            if (muzzleFlash != null) flashBaseScale = muzzleFlash.transform.localScale;
            shooting = GetComponentInParent<PlayerShooting>();
            body = GetComponentInParent<CharacterController>();
            var c = GetComponentInParent<Camera>();
            cam = c != null ? c.transform : null;
            restPosition = transform.localPosition;   // where you placed it in the scene = its "resting" pose
            restRotation = transform.localRotation;
            SetVisible(false);
        }

        /// <summary>Used by code that builds gun models at runtime (shotgun, rifle): fills in what the Inspector normally would.</summary>
        public void Configure(string id, Transform modelRoot, Transform muzzlePoint, GameObject flash, Light flashLight)
        {
            weaponId = id;
            model = modelRoot;
            muzzle = muzzlePoint;
            muzzleFlash = flash;
            muzzleLight = flashLight;
            if (muzzleFlash != null) flashBaseScale = muzzleFlash.transform.localScale;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            SetVisible(false);
        }

        void OnEnable()
        {
            if (shooting == null) shooting = GetComponentInParent<PlayerShooting>();
            if (shooting != null) shooting.Fired += OnFired;
        }

        void OnDisable()
        {
            if (shooting != null) shooting.Fired -= OnFired;
        }

        void Update()
        {
            bool shouldShow = shooting != null && shooting.enabled && shooting.CurrentWeaponId == weaponId;
            if (shouldShow != visible) SetVisible(shouldShow);
            if (!visible) return;

            float dt = Time.deltaTime;

            kick = Mathf.Lerp(kick, 0f, 16f * dt);                                              // recoil settles back
            reloadBlend = Mathf.MoveTowards(reloadBlend, shooting.IsReloading ? 1f : 0f, dt / 0.25f);

            // Walking sway: stronger the faster you move.
            float speed = 0f;
            if (body != null)
            {
                Vector3 v = body.velocity;
                v.y = 0f;
                speed = v.magnitude;
            }
            bobTime += speed * dt * 1.6f;
            float bob = Mathf.Clamp01(speed / 4f);
            Vector3 bobOffset = new Vector3(Mathf.Sin(bobTime) * 0.005f, -Mathf.Abs(Mathf.Sin(bobTime)) * 0.006f, 0f) * bob;

            if (cam == null) { var c = GetComponentInParent<Camera>(); cam = c != null ? c.transform : null; }
            tuck = Mathf.Lerp(tuck, ViewModelTuck.Target(cam), 1f - Mathf.Exp(-12f * dt));

            Vector3 position = restPosition + bobOffset
                               + new Vector3(0f, -0.14f * reloadBlend, -0.05f * kick)    // dip down / kick back
                               + ViewModelTuck.Offset(tuck);                             // pulled back near walls
            Quaternion rotation = ViewModelTuck.Turn(tuck) * restRotation
                                  * Quaternion.Euler(-9f * kick + 40f * reloadBlend, 0f, 3f * kick); // muzzle flips up

            // Shared first-person motion: sway when turning, raise on switch, sprint pose, breathing (see FirstPersonFeel).
            position += LastCheckout.Player.FirstPersonFeel.WeaponPos;
            rotation = LastCheckout.Player.FirstPersonFeel.WeaponRot * rotation;

            transform.SetLocalPositionAndRotation(position, rotation);

            if (muzzleFlash != null && muzzleFlash.activeSelf && Time.time >= flashUntil) SetFlash(false);
        }

        void OnFired(bool hit)
        {
            if (!visible) return;
            kick = 1f;
            flashUntil = Time.time + 0.045f;

            if (muzzleFlash != null)
            {
                muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)); // each flash looks different
                // 80%-130% of its normal size. (Before, this set it to 0.8-1.3 METERS: a giant ball in your face.)
                muzzleFlash.transform.localScale = flashBaseScale * Random.Range(0.8f, 1.3f);
            }
            SetFlash(true);
        }

        void SetVisible(bool value)
        {
            visible = value;
            if (model != null) model.gameObject.SetActive(value);
            if (!value) SetFlash(false);
        }

        void SetFlash(bool on)
        {
            if (muzzleFlash != null) muzzleFlash.SetActive(on);
            if (muzzleLight != null) muzzleLight.enabled = on;
        }
    }
}
