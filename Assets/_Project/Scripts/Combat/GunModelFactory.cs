using UnityEngine;
using LastCheckout.Items;

namespace LastCheckout.Combat
{
    /// <summary>
    /// Builds every weapon model out of simple boxes and cylinders when the game starts:
    ///   - the first-person ones in your hands (attached to the camera), and
    ///   - display copies for the armory walls (BuildDisplay).
    /// The pistol in your hands already exists in the scene; everything else is made here,
    /// re-using the pistol's materials so they all match.
    ///
    /// Model directions: guns point along +Z (forward). Melee weapons and the bow stand along +Y (up),
    /// with the handle at the bottom.
    /// </summary>
    public static class GunModelFactory
    {
        static Material slideMat, frameMat, gripMat, flashMat;

        static readonly Color Wood = new Color(0.36f, 0.22f, 0.12f);
        static readonly Color LightWood = new Color(0.72f, 0.55f, 0.35f);
        static readonly Color Steel = new Color(0.72f, 0.74f, 0.78f);
        static readonly Color Rubber = new Color(0.06f, 0.06f, 0.07f);
        static readonly Color FireRed = new Color(0.75f, 0.1f, 0.08f);

        // Where each weapon sits in front of the camera, and how it's tilted (the "rest" pose).
        static readonly (string id, Vector3 pos, Vector3 euler)[] Poses =
        {
            ("knife",    new Vector3(0.2f, -0.2f, 0.38f),    new Vector3(60f, -5f, 15f)),
            ("bat",      new Vector3(0.24f, -0.3f, 0.42f),   new Vector3(35f, -10f, 22f)),
            ("machete",  new Vector3(0.22f, -0.27f, 0.4f),   new Vector3(45f, -8f, 20f)),
            ("axe",      new Vector3(0.24f, -0.32f, 0.42f),  new Vector3(32f, -10f, 22f)),
            ("revolver", new Vector3(0.15f, -0.17f, 0.34f),  new Vector3(0f, -1.5f, 0f)),
            ("bow",      new Vector3(0.2f, -0.1f, 0.5f),     new Vector3(0f, 0f, -15f)),   // held on the RIGHT side of the screen
            ("smg",      new Vector3(0.15f, -0.18f, 0.33f),  new Vector3(0f, -1.5f, 0f)),
            ("shotgun",  new Vector3(0.16f, -0.2f, 0.34f),   new Vector3(0f, -1.5f, 0f)),
            ("rifle",    new Vector3(0.15f, -0.19f, 0.33f),  new Vector3(0f, -1.5f, 0f)),
        };

        /// <summary>Creates any first-person weapon model that doesn't exist yet.</summary>
        public static void EnsureModels(PlayerShooting shooting)
        {
            if (shooting == null) return;
            var cam = shooting.GetComponentInChildren<Camera>();
            if (cam == null) return;

            FindMaterials(shooting);

            foreach (var pose in Poses)
            {
                if (Exists(shooting, pose.id)) continue;
                var root = new GameObject(pose.id + " View").transform;
                root.SetParent(cam.transform, false);
                root.localPosition = pose.pos;
                root.localRotation = Quaternion.Euler(pose.euler);
                var model = new GameObject("Model").transform;
                model.SetParent(root, false);

                var spec = WeaponCatalog.Get(pose.id);
                if (pose.id == "bow") BuildBowView(root, model);
                else if (spec != null && spec.IsMelee)
                {
                    BuildParts(pose.id, model);
                    root.gameObject.AddComponent<MeleeViewModel>().Configure(pose.id, model);
                }
                else
                {
                    Vector3 muzzle = BuildParts(pose.id, model);
                    AddMuzzle(root, model, pose.id, muzzle);
                }
            }

            // Swap the box shapes for real 3D models (keeps everything above working). See WeaponSkins.
            WeaponSkins.ReskinAll(shooting);
        }

        static bool Exists(PlayerShooting shooting, string id)
        {
            foreach (var vm in shooting.GetComponentsInChildren<GunViewModel>(true)) if (vm.WeaponId == id) return true;
            foreach (var vm in shooting.GetComponentsInChildren<MeleeViewModel>(true)) if (vm.WeaponId == id) return true;
            if (id == "bow" && shooting.GetComponentInChildren<BowViewModel>(true) != null) return true;
            return false;
        }

        /// <summary>A copy of a weapon's model for the shop wall (no scripts, no colliders).</summary>
        public static Transform BuildDisplay(string id, Transform parent)
        {
            if (frameMat == null) FindMaterials(null);
            var root = new GameObject("Display " + id).transform;
            root.SetParent(parent, false);
            if (id == "bow")
            {
                BuildBowFrame(root);
                Part(root, "String", PrimitiveType.Cube, new Vector3(0f, 0f, -0.06f), Vector3.zero, new Vector3(0.004f, 0.6f, 0.004f), gripMat, Color.white);
            }
            else BuildParts(id, root);
            WeaponSkins.Reskin(root, id); // real 3D model on the armory wall too
            return root;
        }

        // ───────────────────────── Shapes ─────────────────────────

        /// Builds the parts of one weapon under 'model'. Returns the muzzle position for guns.
        static Vector3 BuildParts(string id, Transform m)
        {
            switch (id)
            {
                // ── Melee (along +Y, handle at the bottom) ──
                case "knife":
                    Part(m, "Handle", PrimitiveType.Cube, new Vector3(0f, 0.055f, 0f), Vector3.zero, new Vector3(0.028f, 0.11f, 0.035f), gripMat, Rubber);
                    Part(m, "Guard", PrimitiveType.Cube, new Vector3(0f, 0.115f, 0f), Vector3.zero, new Vector3(0.05f, 0.012f, 0.04f), slideMat, Steel);
                    Part(m, "Blade", PrimitiveType.Cube, new Vector3(0f, 0.2f, 0.004f), Vector3.zero, new Vector3(0.006f, 0.16f, 0.032f), slideMat, Steel);
                    Part(m, "Tip", PrimitiveType.Cube, new Vector3(0f, 0.282f, 0.006f), new Vector3(40f, 0f, 0f), new Vector3(0.006f, 0.028f, 0.022f), slideMat, Steel);
                    return Vector3.zero;
                case "bat":
                    Part(m, "Knob", PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.055f, 0.03f, 0.055f), gripMat, Rubber);
                    Part(m, "Grip Tape", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(0.034f, 0.12f, 0.034f), gripMat, Rubber);
                    Part(m, "Taper", PrimitiveType.Cylinder, new Vector3(0f, 0.36f, 0f), Vector3.zero, new Vector3(0.045f, 0.13f, 0.045f), gripMat, LightWood);
                    Part(m, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.66f, 0f), Vector3.zero, new Vector3(0.066f, 0.2f, 0.066f), gripMat, LightWood);
                    Part(m, "Cap", PrimitiveType.Sphere, new Vector3(0f, 0.86f, 0f), Vector3.zero, new Vector3(0.066f, 0.03f, 0.066f), gripMat, LightWood);
                    return Vector3.zero;
                case "machete":
                    Part(m, "Handle", PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), Vector3.zero, new Vector3(0.03f, 0.12f, 0.04f), gripMat, Rubber);
                    Part(m, "Blade", PrimitiveType.Cube, new Vector3(0f, 0.35f, 0.012f), Vector3.zero, new Vector3(0.006f, 0.46f, 0.07f), slideMat, Steel);
                    Part(m, "Tip", PrimitiveType.Cube, new Vector3(0f, 0.585f, 0.022f), new Vector3(30f, 0f, 0f), new Vector3(0.006f, 0.06f, 0.05f), slideMat, Steel);
                    return Vector3.zero;
                case "axe":
                    Part(m, "Handle", PrimitiveType.Cube, new Vector3(0f, 0.36f, 0f), Vector3.zero, new Vector3(0.035f, 0.72f, 0.045f), gripMat, LightWood);
                    Part(m, "Head", PrimitiveType.Cube, new Vector3(0f, 0.68f, 0.04f), Vector3.zero, new Vector3(0.05f, 0.12f, 0.17f), frameMat, FireRed);
                    Part(m, "Edge", PrimitiveType.Cube, new Vector3(0f, 0.68f, 0.14f), Vector3.zero, new Vector3(0.022f, 0.15f, 0.04f), slideMat, Steel);
                    Part(m, "Spike", PrimitiveType.Cube, new Vector3(0f, 0.68f, -0.08f), Vector3.zero, new Vector3(0.03f, 0.04f, 0.1f), frameMat, FireRed);
                    return Vector3.zero;

                // ── Guns (along +Z) ──
                case "pistol":
                    Part(m, "Slide", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.03f), Vector3.zero, new Vector3(0.034f, 0.035f, 0.19f), slideMat);
                    Part(m, "Frame", PrimitiveType.Cube, new Vector3(0f, 0.0f, 0.02f), Vector3.zero, new Vector3(0.032f, 0.03f, 0.17f), frameMat);
                    Part(m, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.06f, -0.04f), new Vector3(15f, 0f, 0f), new Vector3(0.032f, 0.1f, 0.045f), gripMat);
                    return new Vector3(0f, 0.03f, 0.13f);
                case "revolver":
                    Part(m, "Frame", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.02f), Vector3.zero, new Vector3(0.035f, 0.065f, 0.12f), frameMat);
                    Part(m, "Cylinder", PrimitiveType.Cylinder, new Vector3(0f, 0.022f, 0.03f), new Vector3(90f, 0f, 0f), new Vector3(0.056f, 0.03f, 0.056f), slideMat, Steel);
                    Part(m, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0.17f), new Vector3(90f, 0f, 0f), new Vector3(0.024f, 0.08f, 0.024f), slideMat, Steel);
                    Part(m, "Rib", PrimitiveType.Cube, new Vector3(0f, 0.057f, 0.17f), Vector3.zero, new Vector3(0.012f, 0.012f, 0.16f), frameMat);
                    Part(m, "Hammer", PrimitiveType.Cube, new Vector3(0f, 0.058f, -0.045f), new Vector3(-30f, 0f, 0f), new Vector3(0.01f, 0.022f, 0.02f), frameMat);
                    Part(m, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.05f, -0.05f), new Vector3(20f, 0f, 0f), new Vector3(0.032f, 0.1f, 0.045f), gripMat, Wood);
                    return new Vector3(0f, 0.04f, 0.26f);
                case "smg":
                    Part(m, "Receiver", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.04f), Vector3.zero, new Vector3(0.05f, 0.07f, 0.26f), frameMat);
                    Part(m, "Shroud", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.21f), Vector3.zero, new Vector3(0.04f, 0.04f, 0.1f), gripMat);
                    Part(m, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0.28f), new Vector3(90f, 0f, 0f), new Vector3(0.018f, 0.03f, 0.018f), slideMat);
                    Part(m, "Magazine", PrimitiveType.Cube, new Vector3(0f, -0.09f, 0.08f), new Vector3(-5f, 0f, 0f), new Vector3(0.03f, 0.16f, 0.04f), gripMat);
                    Part(m, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.05f, -0.05f), new Vector3(18f, 0f, 0f), new Vector3(0.035f, 0.09f, 0.045f), gripMat);
                    Part(m, "Foregrip", PrimitiveType.Cube, new Vector3(0f, -0.04f, 0.18f), Vector3.zero, new Vector3(0.03f, 0.07f, 0.035f), gripMat);
                    Part(m, "Stock", PrimitiveType.Cube, new Vector3(0f, 0.0f, -0.16f), Vector3.zero, new Vector3(0.02f, 0.02f, 0.16f), slideMat);
                    Part(m, "Sight", PrimitiveType.Cube, new Vector3(0f, 0.065f, 0.0f), Vector3.zero, new Vector3(0.012f, 0.02f, 0.02f), slideMat);
                    return new Vector3(0f, 0.03f, 0.32f);
                case "shotgun":
                    Part(m, "Receiver", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.055f, 0.075f, 0.24f), frameMat);
                    Part(m, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.045f, 0.33f), new Vector3(90f, 0f, 0f), new Vector3(0.03f, 0.22f, 0.03f), slideMat);
                    Part(m, "Mag Tube", PrimitiveType.Cylinder, new Vector3(0f, 0.008f, 0.3f), new Vector3(90f, 0f, 0f), new Vector3(0.026f, 0.18f, 0.026f), frameMat);
                    Part(m, "Pump", PrimitiveType.Cube, new Vector3(0f, 0.004f, 0.27f), Vector3.zero, new Vector3(0.062f, 0.055f, 0.17f), gripMat, Wood);
                    Part(m, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.055f, -0.07f), new Vector3(20f, 0f, 0f), new Vector3(0.042f, 0.1f, 0.05f), gripMat, Wood);
                    Part(m, "Stock", PrimitiveType.Cube, new Vector3(0f, -0.02f, -0.17f), new Vector3(6f, 0f, 0f), new Vector3(0.05f, 0.08f, 0.14f), gripMat, Wood);
                    Part(m, "Bead Sight", PrimitiveType.Cube, new Vector3(0f, 0.066f, 0.53f), Vector3.zero, new Vector3(0.008f, 0.01f, 0.01f), slideMat);
                    return new Vector3(0f, 0.045f, 0.56f);
                case "rifle":
                    Part(m, "Receiver", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.02f), Vector3.zero, new Vector3(0.05f, 0.08f, 0.3f), frameMat);
                    Part(m, "Handguard", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.26f), Vector3.zero, new Vector3(0.056f, 0.062f, 0.2f), gripMat);
                    Part(m, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0.42f), new Vector3(90f, 0f, 0f), new Vector3(0.022f, 0.08f, 0.022f), slideMat);
                    Part(m, "Magazine", PrimitiveType.Cube, new Vector3(0f, -0.07f, 0.08f), new Vector3(-12f, 0f, 0f), new Vector3(0.035f, 0.13f, 0.06f), gripMat);
                    Part(m, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.06f, -0.07f), new Vector3(18f, 0f, 0f), new Vector3(0.036f, 0.1f, 0.046f), gripMat);
                    Part(m, "Stock", PrimitiveType.Cube, new Vector3(0f, 0f, -0.17f), Vector3.zero, new Vector3(0.046f, 0.08f, 0.14f), frameMat);
                    Part(m, "Top Rail", PrimitiveType.Cube, new Vector3(0f, 0.07f, 0.04f), Vector3.zero, new Vector3(0.03f, 0.02f, 0.16f), slideMat);
                    Part(m, "Front Sight", PrimitiveType.Cube, new Vector3(0f, 0.075f, 0.34f), Vector3.zero, new Vector3(0.008f, 0.03f, 0.01f), slideMat);
                    return new Vector3(0f, 0.035f, 0.51f);
            }
            return Vector3.zero;
        }

        // Bow frame: riser in the middle, limbs bending back towards you (−Z), tips at ±0.3 m.
        static readonly Vector3 BowTopTip = new Vector3(0f, 0.3f, -0.06f);
        static readonly Vector3 BowBottomTip = new Vector3(0f, -0.3f, -0.06f);

        static void BuildBowFrame(Transform m)
        {
            Part(m, "Riser", PrimitiveType.Cube, Vector3.zero, Vector3.zero, new Vector3(0.03f, 0.2f, 0.045f), gripMat, Wood);
            Part(m, "Grip Wrap", PrimitiveType.Cube, Vector3.zero, Vector3.zero, new Vector3(0.034f, 0.07f, 0.048f), gripMat, Rubber);
            Part(m, "Limb Top", PrimitiveType.Cube, new Vector3(0f, 0.2f, -0.03f), new Vector3(-17f, 0f, 0f), new Vector3(0.022f, 0.21f, 0.03f), frameMat, new Color(0.18f, 0.2f, 0.17f));
            Part(m, "Limb Bottom", PrimitiveType.Cube, new Vector3(0f, -0.2f, -0.03f), new Vector3(17f, 0f, 0f), new Vector3(0.022f, 0.21f, 0.03f), frameMat, new Color(0.18f, 0.2f, 0.17f));
        }

        static void BuildBowView(Transform root, Transform model)
        {
            BuildBowFrame(model);
            var top = Part(model, "String Top", PrimitiveType.Cube, Vector3.zero, Vector3.zero, Vector3.one * 0.004f, gripMat, Color.white).transform;
            var bottom = Part(model, "String Bottom", PrimitiveType.Cube, Vector3.zero, Vector3.zero, Vector3.one * 0.004f, gripMat, Color.white).transform;
            var arrow = new GameObject("Nocked Arrow").transform;
            arrow.SetParent(model, false);
            Arrow.BuildLook(arrow);
            root.gameObject.AddComponent<BowViewModel>().Configure(model, top, bottom, arrow, BowTopTip, BowBottomTip, new Vector3(0f, 0f, -0.06f));
        }

        // ───────────────────────── Helpers ─────────────────────────

        // Re-use the pistol's materials (found by part name), so nothing new needs to exist in the project.
        static void FindMaterials(PlayerShooting shooting)
        {
            if (shooting != null)
            {
                foreach (var vm in shooting.GetComponentsInChildren<GunViewModel>(true))
                {
                    foreach (var r in vm.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r.name == "Slide") slideMat = r.sharedMaterial;
                        else if (r.name == "Frame") frameMat = r.sharedMaterial;
                        else if (r.name == "Grip") gripMat = r.sharedMaterial;
                        else if (r.name == "Muzzle Flash") flashMat = r.sharedMaterial;
                    }
                }
            }
            // Fallbacks if the pistol model is missing: plain coloured materials.
            if (frameMat == null) frameMat = Game.LevelKit.ColorMaterial(new Color(0.12f, 0.12f, 0.13f), 0.5f);
            if (slideMat == null) slideMat = Game.LevelKit.ColorMaterial(new Color(0.28f, 0.3f, 0.33f), 0.7f);
            if (gripMat == null) gripMat = Game.LevelKit.ColorMaterial(new Color(0.06f, 0.06f, 0.07f), 0.25f);
            if (flashMat == null) flashMat = Game.LevelKit.ColorMaterial(new Color(1f, 0.85f, 0.4f), 0f, 3f);
        }

        static void AddMuzzle(Transform root, Transform model, string id, Vector3 muzzlePos)
        {
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = muzzlePos;

            Vector3 flashScale = id == "shotgun" ? new Vector3(0.1f, 0.1f, 0.17f)
                               : id == "revolver" ? new Vector3(0.09f, 0.09f, 0.15f)
                               : new Vector3(0.07f, 0.07f, 0.12f);
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Muzzle Flash";
            Game.LevelKit.RemoveCollider(flash);
            flash.transform.SetParent(muzzle, false);
            flash.transform.localPosition = new Vector3(0f, 0f, flashScale.z * 0.4f);
            flash.transform.localScale = flashScale;
            var fr = flash.GetComponent<Renderer>();
            fr.sharedMaterial = flashMat;
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flash.SetActive(false);

            var lightObject = new GameObject("Muzzle Light");
            lightObject.transform.SetParent(muzzle, false);
            lightObject.transform.localPosition = new Vector3(0f, 0f, 0.06f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.7f, 0.35f);
            light.intensity = 3.5f;
            light.range = 9f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            var vm = root.gameObject.AddComponent<GunViewModel>();
            vm.Configure(id, model, muzzle, flash, light);
        }

        static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material mat, Color? tint = null)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Game.LevelKit.RemoveCollider(part); // looks only: bullets must never hit your own weapon
            part.transform.SetParent(parent, false);
            part.transform.localPosition = pos;
            part.transform.localRotation = Quaternion.Euler(euler);
            part.transform.localScale = scale;
            var r = part.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (tint.HasValue) WorldItem.ApplyColor(r, tint.Value);
            return part;
        }
    }
}
