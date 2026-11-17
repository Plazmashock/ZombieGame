using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastCheckout.World
{
    /// <summary>
    /// Gives the grey-box store real surfaces when you press Play: tiled floor, block walls, ceiling tiles,
    /// cracked asphalt, wooden crates, scratched metal... plus textured skin and clothes on the zombies.
    ///
    ///   for every box in "Environment" whose material we know (Wall, Floor_Tiles, Ceiling...):
    ///       1. hide the old box's picture (its collider stays, so nothing changes for walking or shooting)
    ///       2. add a child "Surface" with the same shape, but a textured material and meter-sized UVs
    ///
    /// The material ASSETS in your project are never changed: we work on copies.
    /// </summary>
    public static class StoreRetexture
    {
        /// Which texture goes on which of the grey-box materials (by material name).
        static readonly Dictionary<string, Surface> ByMaterial = new Dictionary<string, Surface>
        {
            { "Floor_Tiles", Surface.FloorTiles },
            { "Wall", Surface.PaintedBlock },
            { "Ceiling", Surface.CeilingTiles },
            { "Ground_Asphalt", Surface.Asphalt },
            { "Shelf", Surface.PaintedMetal },
            { "Shelf_Board", Surface.BrushedMetal },
            { "Checkout", Surface.PaintedMetal },
            { "Dark_Metal", Surface.BrushedMetal },
            { "Crate", Surface.Wood },
            { "Desk", Surface.Wood },
            { "Car", Surface.PaintedMetal },
        };

        public static void Apply()
        {
            var env = GameObject.Find("Environment");
            if (env == null)
            {
                Debug.LogWarning("[Textures] No 'Environment' object in the scene, so the store wasn't retextured.");
                return;
            }

            int count = 0;
            foreach (var r in env.GetComponentsInChildren<MeshRenderer>(false))
                if (RetextureOne(r)) count++;

            HideFlatCheckerTiles();
            Debug.Log($"[Textures] Retextured {count} store surfaces.");
        }

        /// Retextures one grey box. Returns false if it isn't one we handle.
        /// Also used for wall pieces created later (e.g. when an upgrade cuts a new doorway).
        public static bool RetextureOne(MeshRenderer r)
        {
            if (r == null || !r.enabled || r.sharedMaterial == null) return false;
            string matName = r.sharedMaterial.name.Replace(" (Instance)", "");
            if (!ByMaterial.TryGetValue(matName, out var kind)) return false;
            // Skip things that move or get picked up (doors swing, items get carried).
            if (r.GetComponentInParent<Door>() != null || r.GetComponentInParent<Items.WorldItem>() != null) return false;
            if (r.GetComponentInParent<Rigidbody>() != null || r.GetComponentInParent<LightSwitch>() != null) return false;

            var mat = SurfaceMaterials.Textured(r.sharedMaterial, kind);
            float mpt = ProceduralTextures.Get(kind).metersPerTile;

            // Every grey-box object is a Unity cube (1 × 1 × 1, scaled), so we rebuild a 1 × 1 × 1 cube
            // as a child: it gets the same size and position automatically.
            var go = new GameObject("Surface");
            go.transform.SetParent(r.transform, false);
            var mb = new ModelKit.MeshBuilder();
            mb.AddBox(Vector3.zero, Vector3.one, Quaternion.identity, r.transform.localToWorldMatrix, mpt);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(r.name + " surface");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = r.shadowCastingMode;
            mr.receiveShadows = r.receiveShadows;

            r.enabled = false;
            return true;
        }

        /// The makeover painted half the floor tiles a flat darker grey. The new floor texture has its own
        /// checkerboard, so those flat tiles are switched off (they'd hide the texture).
        static void HideFlatCheckerTiles()
        {
            var makeover = GameObject.Find("Store Makeover");
            if (makeover == null) return;
            foreach (var r in makeover.GetComponentsInChildren<MeshRenderer>())
                if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Makeover 0.470_0.470_0.440"))
                    r.enabled = false;
        }

        /// Textured skin and clothes for every zombie in the scene, INCLUDING the wave spawner's
        /// switched-off template (so every zombie spawned later is textured too).
        public static void ApplyToZombies()
        {
            foreach (var z in Game.LevelKit.FindAll<Enemies.Zombie>())
                foreach (var r in z.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.sharedMaterial == null) continue;
                    string n = r.sharedMaterial.name;
                    if (n.Contains("Skin")) r.sharedMaterial = SurfaceMaterials.Textured(r.sharedMaterial, Surface.Skin);
                    else if (n.Contains("Shirt") || n.Contains("Pants")) r.sharedMaterial = SurfaceMaterials.Textured(r.sharedMaterial, Surface.Fabric);
                }
        }
    }
}
