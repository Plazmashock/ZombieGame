using UnityEngine;

namespace LastCheckout.World
{
    /// <summary>
    /// Makes a light buzz and stutter like a dying fluorescent tube.
    /// Mostly on, with short random drop-outs. Put it on any object that has a Light.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [Tooltip("Chance (0-1) that the next change is a drop-out instead of 'on'.")]
        [SerializeField, Range(0f, 1f)] float dropOutChance = 0.3f;

        Light lamp;
        float baseIntensity;
        float nextChange;

        void Awake()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp.intensity;
        }

        void Update()
        {
            if (Time.time < nextChange) return;

            bool dropOut = Random.value < dropOutChance;
            lamp.intensity = dropOut
                ? baseIntensity * Random.Range(0f, 0.15f)      // nearly off, very briefly
                : baseIntensity * Random.Range(0.85f, 1.05f);  // on, with a little hum
            nextChange = Time.time + (dropOut ? Random.Range(0.03f, 0.12f) : Random.Range(0.05f, 1.8f));
        }
    }
}
