using System.Collections.Generic;
using UnityEngine;
using LastCheckout.World;

namespace LastCheckout.Combat
{
    /// <summary>
    /// Swaps the box-and-cylinder weapons for real low-poly models (Quaternius, CC0).
    ///
    ///   old weapon parts ──measure──► their size and position
    ///          │
    ///          └─ hidden (not deleted: the gun scripts still find their materials)
    ///   new model ──turned (muzzle forward / handle down)──► scaled to the same length ──► put in the same place
    ///
    /// The muzzle flash, recoil, swing animations etc. keep working: they move the "Model" holder, not the parts.
    /// Used for the guns in your hands AND the display copies on the armory walls.
    /// </summary>
    public static class WeaponSkins
    {
        struct Skin { public string path; public bool melee; public bool headIsBigger; }

        static readonly Dictionary<string, Skin> Skins = new Dictionary<string, Skin>
        {
            { "pistol",   new Skin { path = "Weapons/Pistol" } },
            { "revolver", new Skin { path = "Weapons/Revolver" } },
            { "smg",      new Skin { path = "Weapons/SMG" } },
            { "shotgun",  new Skin { path = "Weapons/Shotgun" } },
            { "rifle",    new Skin { path = "Weapons/Rifle" } },
            { "knife",    new Skin { path = "Weapons/Knife", melee = true, headIsBigger = false } },
            { "machete",  new Skin { path = "Weapons/Machete", melee = true, headIsBigger = false } },
            { "bat",      new Skin { path = "Weapons/Bat", melee = true, headIsBigger = true } },
            { "axe",      new Skin { path = "Weapons/Axe", melee = true, headIsBigger = true } },
        };

        /// <summary>Re-skins every first-person weapon the player has.</summary>
        public static void ReskinAll(PlayerShooting shooting)
        {
            if (shooting == null) return;
            foreach (var vm in shooting.GetComponentsInChildren<GunViewModel>(true))
                Reskin(vm.transform.Find("Model"), vm.WeaponId, viewModel: true);
            foreach (var vm in shooting.GetComponentsInChildren<MeleeViewModel>(true))
                Reskin(vm.transform.Find("Model"), vm.WeaponId, viewModel: true);
        }

        /// <summary>Replaces the parts under 'holder' with the model for weapon 'id'. Safe to call twice.</summary>
        public static void Reskin(Transform holder, string id, bool viewModel = false)
        {
            if (holder == null || string.IsNullOrEmpty(id) || !Skins.TryGetValue(id, out var skin)) return;
            if (holder.Find("Skin") != null) return; // done already

            // 1) The old parts (everything visible except the muzzle flash).
            var parts = new List<Renderer>();
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
                if (r.enabled && !UnderMuzzle(r.transform, holder)) parts.Add(r);
            if (parts.Count == 0) return;

            Bounds old = new Bounds();
            bool any = false;
            foreach (var r in parts)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Matrix4x4 m = holder.worldToLocalMatrix * r.transform.localToWorldMatrix;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(c);
                    if (!any) { old = new Bounds(p, Vector3.zero); any = true; } else old.Encapsulate(p);
                }
            }
            if (!any) return;

            // 2) The new model, turned the right way.
            var model = ModelLibrary.Spawn(skin.path);
            if (model == null) return;
            Quaternion orient = skin.melee ? ModelLibrary.MeleeOrientation(model, skin.headIsBigger) : ModelLibrary.GunOrientation(model);

            // 3) Same length as the old weapon, same place.
            var space = new GameObject("Skin").transform;
            space.SetParent(holder, false);
            ModelLibrary.Fit(model, space, old, skin.melee ? ModelLibrary.FitBy.LengthY : ModelLibrary.FitBy.LengthZ, orient, autoYaw: false, bottom: false);

            int layer = parts[0].gameObject.layer;
            ModelLibrary.SetLayer(space.gameObject, layer);
            ModelLibrary.SetShadows(space.gameObject, viewModel ? UnityEngine.Rendering.ShadowCastingMode.Off : parts[0].shadowCastingMode);

            foreach (var r in parts) r.enabled = false;
        }

        static bool UnderMuzzle(Transform t, Transform stopAt)
        {
            for (var p = t; p != null && p != stopAt; p = p.parent)
                if (p.name.StartsWith("Muzzle")) return true;
            return false;
        }
    }
}
