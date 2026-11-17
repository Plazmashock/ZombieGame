using UnityEngine;

namespace LastCheckout.World
{
    /// <summary>
    /// Hides a 3D text label when you're BEHIND it, so you never read mirrored text from the back of a sign.
    /// (A label is readable from the side its "forward" arrow points AWAY from.)
    /// Two-sided signs simply have two labels, one per side.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class OneSidedText : MonoBehaviour
    {
        Renderer textRenderer;

        void Awake() => textRenderer = GetComponent<Renderer>();

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 toCamera = cam.transform.position - transform.position;
            textRenderer.enabled = Vector3.Dot(toCamera, transform.forward) < 0f;
        }
    }
}
