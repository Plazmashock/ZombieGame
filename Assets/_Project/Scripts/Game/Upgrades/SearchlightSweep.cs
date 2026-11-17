using UnityEngine;

namespace LastCheckout.Game
{
    /// <summary>Slowly swings an object left and right (the sniper tower's searchlight).</summary>
    public class SearchlightSweep : MonoBehaviour
    {
        float centerYaw, halfAngle, pitch, speed;

        public void Setup(float yaw, float half, float pitchDown, float spd)
        {
            centerYaw = yaw; halfAngle = half; pitch = pitchDown; speed = spd;
        }

        void Update() => transform.rotation = Quaternion.Euler(pitch, centerYaw + Mathf.Sin(Time.time * speed) * halfAngle, 0f);
    }
}
