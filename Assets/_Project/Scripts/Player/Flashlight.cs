using UnityEngine;

namespace LastCheckout.Player
{
    /// <summary>
    /// A flashlight that is always on. A spot light on the camera that follows your view with a tiny delay,
    /// so it feels like it is held in your hand. Added to the Main Camera by GameDirector.
    ///
    /// WHAT WAS WRONG BEFORE (and what this fixes):
    ///   1) The light sat 20 cm in front of the camera, right next to the weapon. The weapon was lit 100x brighter
    ///      than anything else ("it lights the gun") and its shadow fell across the middle of the beam.
    ///   2) Light gets weaker with distance squared, so a weak light looked like it did nothing out in front of you.
    ///
    /// NOW:
    ///   • The beam starts AT the camera (a hand-held offset, but behind the weapon).
    ///   • Weapon models live on their own "rendering layer" (bit 7). The beam ignores that layer, and a soft
    ///     fill light lights ONLY that layer, so the gun is always readable but never blown out.
    ///   • Weapons no longer cast shadows, so they never block the beam.
    ///   • The beam is much stronger and reaches further, plus a dim wide light so the edges of your view aren't pitch black.
    /// </summary>
    public class Flashlight : MonoBehaviour
    {
        // Rendering layer used by everything held in your hands ("Light Layer 7" in the URP settings).
        const int ViewModelBit = 7;
        const uint ViewModelMask = 1u << ViewModelBit;

        Transform beam;
        Light mainBeam, widePool, viewFill;
        float nextSweep;

        void Awake()
        {
            // Everything that is NOT held in your hands (store, zombies, floor...) is on the Default layer (bit 0).
            const int worldMask = 1;

            // 1) Main beam: strong, narrow-ish, long reach.
            var go = new GameObject("Flashlight");
            beam = go.transform;
            beam.SetParent(transform, false);
            beam.localPosition = new Vector3(0.1f, -0.08f, 0.0f);   // at the camera, a little to the right and down (hand height)

            mainBeam = go.AddComponent<Light>();
            mainBeam.type = LightType.Spot;
            mainBeam.color = new Color(1f, 0.96f, 0.85f);
            mainBeam.intensity = 55f;
            mainBeam.range = 40f;
            mainBeam.spotAngle = 52f;
            mainBeam.innerSpotAngle = 22f;
            mainBeam.shadows = LightShadows.Soft;
            mainBeam.shadowStrength = 0.9f;
            mainBeam.renderingLayerMask = worldMask;            // does NOT touch the weapon

            // 2) Wide, dim pool of light so the edges of the screen aren't pitch black. No shadows (cheap).
            var wide = new GameObject("Flashlight Wide");
            wide.transform.SetParent(beam, false);
            widePool = wide.AddComponent<Light>();
            widePool.type = LightType.Spot;
            widePool.color = new Color(1f, 0.95f, 0.82f);
            widePool.intensity = 9f;
            widePool.range = 14f;
            widePool.spotAngle = 105f;
            widePool.innerSpotAngle = 40f;
            widePool.shadows = LightShadows.None;
            widePool.renderingLayerMask = worldMask;

            // 3) Soft fill that lights ONLY what you hold, so the weapon stays visible in the dark.
            var fill = new GameObject("Weapon Fill Light");
            fill.transform.SetParent(transform, false);
            fill.transform.localPosition = new Vector3(-0.1f, 0.15f, -0.1f);
            viewFill = fill.AddComponent<Light>();
            viewFill.type = LightType.Point;
            viewFill.color = new Color(1f, 0.97f, 0.9f);
            viewFill.intensity = 0.8f;
            viewFill.range = 1.6f;
            viewFill.shadows = LightShadows.None;
            viewFill.renderingLayerMask = (int)ViewModelMask;

            ClaimViewModels();
        }

        void LateUpdate()
        {
            // Smoothly trails the camera rotation (the "held in hand" lag).
            beam.rotation = Quaternion.Slerp(beam.rotation, transform.rotation, 1f - Mathf.Exp(-18f * Time.deltaTime));

            // Weapon models are created a moment after the game starts (and when you buy new ones): keep claiming them.
            if (Time.unscaledTime >= nextSweep)
            {
                nextSweep = Time.unscaledTime + 0.75f;
                ClaimViewModels();
            }
        }

        /// Puts every renderer under the camera (guns, bow, knife, fists) on the weapon-only light layer and stops it casting shadows.
        void ClaimViewModels()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;          // muzzle smoke etc. keep their own look
                if (r.renderingLayerMask == ViewModelMask) continue; // already done
                r.renderingLayerMask = ViewModelMask;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
