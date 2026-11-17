using UnityEngine;

namespace LastCheckout.Combat
{
    /// <summary>
    /// Stops the weapon in your hands from poking through walls and shelves.
    /// A short invisible probe goes forward from the camera; the closer a wall is, the more the weapon
    /// is pulled back and turned aside (like a real person lifting their gun near a wall).
    /// Zombies don't count, so you can still shoot point-blank.
    /// </summary>
    public static class ViewModelTuck
    {
        const float Reach = 0.85f;

        /// <summary>0 = nothing in front, 1 = face against a wall.</summary>
        public static float Target(Transform cam)
        {
            if (cam == null) return 0f;
            if (!Physics.SphereCast(cam.position, 0.08f, cam.forward, out RaycastHit hit, Reach, ~0, QueryTriggerInteraction.Ignore))
                return 0f;
            if (hit.collider.GetComponentInParent<IDamageable>() != null) return 0f; // a zombie or yourself
            return Mathf.Clamp01(1f - hit.distance / Reach);
        }

        /// <summary>Position offset for a given tuck amount (in camera space).</summary>
        public static Vector3 Offset(float tuck) => new Vector3(-0.04f, -0.07f, -0.2f) * tuck;

        /// <summary>Rotation for a given tuck amount: weapon turns inward and tips up.</summary>
        public static Quaternion Turn(float tuck) => Quaternion.Euler(-20f * tuck, -40f * tuck, 0f);
    }
}
