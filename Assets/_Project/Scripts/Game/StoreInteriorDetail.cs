using System.Collections.Generic;
using UnityEngine;

namespace LastCheckout.Game
{
    /// <summary>
    /// Extra detail inside the supermarket, added on top of StoreMakeover (shelves, lights, signs):
    ///   • floor: tile grid lines, centre-aisle guide line with arrows
    ///   • walls: skirting boards, green brand band, sale posters, a stopped clock
    ///   • ceiling: air ducts, pipes, steel beams, vent grilles
    /// Everything here is decoration (no colliders), so it never blocks you or changes the zombie floor map.
    /// Store layout: x -20..20, z -15..15, ceiling at y = 5 (see GreyboxSupermarketBuilder).
    /// </summary>
    public static class StoreInteriorDetail
    {
        static Transform root;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { root = null; mats.Clear(); }

        public static void Apply()
        {
            if (GameObject.Find("Store Interior Detail") != null) return;
            if (GameObject.Find("Store Floor") == null) return;

            root = new GameObject("Store Interior Detail").transform;
            mats.Clear();

            Floor();
            Walls();
            Ceiling();
            Posters();
        }

        // ───────────────────────── Floor ─────────────────────────

        static void Floor()
        {
            var grout = Mat(new Color(0.18f, 0.19f, 0.2f), 0.1f);
            for (float x = -18f; x <= 18f; x += 2f)
                Box("Grout", new Vector3(x, 0.03f, 0f), new Vector3(0.025f, 0.004f, 29.6f), grout);
            for (float z = -14f; z <= 14f; z += 2f)
                Box("Grout", new Vector3(0f, 0.031f, z), new Vector3(39.6f, 0.004f, 0.025f), grout);

            // Yellow guide line down the middle aisle with arrows pointing to the checkouts (front, -Z).
            var yellow = Mat(new Color(0.95f, 0.75f, 0.12f), 0.15f);
            for (float z = -9f; z <= 8f; z += 1.6f)
                Box("Guide Dash", new Vector3(0f, 0.036f, z), new Vector3(0.14f, 0.004f, 0.8f), yellow);
            foreach (float z in new[] { -9.8f, 0f, 9f })
            {
                Box("Arrow L", new Vector3(-0.22f, 0.037f, z), new Vector3(0.12f, 0.004f, 0.7f), yellow, 0f, 35f);
                Box("Arrow R", new Vector3(0.22f, 0.037f, z), new Vector3(0.12f, 0.004f, 0.7f), yellow, 0f, -35f);
            }

            // Wet-floor style hazard stripe along the front wall.
            var hazard = Mat(new Color(0.9f, 0.7f, 0.1f), 0.15f);
            var black = Mat(new Color(0.07f, 0.07f, 0.07f), 0.15f);
            for (int i = 0; i < 38; i++)
                Box("Hazard", new Vector3(-18.9f + i * 1f, 0.034f, -14.55f), new Vector3(0.5f, 0.004f, 0.25f), i % 2 == 0 ? hazard : black, 0f, 0f);
        }

        // ───────────────────────── Walls ─────────────────────────

        static void Walls()
        {
            var skirting = Mat(new Color(0.1f, 0.11f, 0.12f), 0.3f);
            var band = Mat(new Color(0.08f, 0.32f, 0.22f), 0.2f);
            var trim = Mat(new Color(1f, 0.78f, 0.24f), 0.2f);

            // Left (-X) and right (+X) walls.
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * 19.93f;
                Box("Skirting", new Vector3(x, 0.14f, 0f), new Vector3(0.06f, 0.28f, 29.6f), skirting);
                Box("Wall Band", new Vector3(x, 0.55f, 0f), new Vector3(0.05f, 0.24f, 29.6f), band);
                Box("Wall Trim", new Vector3(x, 0.71f, 0f), new Vector3(0.055f, 0.04f, 29.6f), trim);
            }
            // Front wall (z = -15): the front door and windows are cut into it, so only the skirting runs along it.
            Box("Skirting", new Vector3(-12.5f, 0.14f, -14.93f), new Vector3(15f, 0.28f, 0.06f), skirting);
            Box("Skirting", new Vector3(12.5f, 0.14f, -14.93f), new Vector3(15f, 0.28f, 0.06f), skirting);

            Clock(new Vector3(-19.88f, 3.6f, -4f));
        }

        static void Clock(Vector3 c)
        {
            var rim = Mat(new Color(0.1f, 0.1f, 0.11f), 0.4f);
            var face = Mat(new Color(0.93f, 0.92f, 0.86f), 0.3f);
            var hand = Mat(new Color(0.05f, 0.05f, 0.05f), 0.3f);

            var r = Cyl("Clock Rim", c, new Vector3(0.7f, 0.03f, 0.7f), rim);
            r.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            var f = Cyl("Clock Face", c + Vector3.right * 0.03f, new Vector3(0.62f, 0.03f, 0.62f), face);
            f.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            // Stuck at a minute to midnight.
            var h1 = Box("Hour Hand", c + new Vector3(0.07f, 0.1f, 0.01f), new Vector3(0.02f, 0.2f, 0.03f), hand);
            var h2 = Box("Minute Hand", c + new Vector3(0.07f, 0.14f, 0.0f), new Vector3(0.02f, 0.28f, 0.02f), hand);
            h1.transform.SetPositionAndRotation(h1.transform.position, Quaternion.Euler(-10f, 0f, 0f));
            _ = h2;
        }

        // ───────────────────────── Ceiling ─────────────────────────

        static void Ceiling()
        {
            var duct = Mat(new Color(0.58f, 0.6f, 0.62f), 0.55f);
            var pipeRed = Mat(new Color(0.65f, 0.12f, 0.1f), 0.4f);
            var pipeBlue = Mat(new Color(0.15f, 0.35f, 0.7f), 0.4f);
            var steel = Mat(new Color(0.16f, 0.17f, 0.18f), 0.4f);
            var grille = Mat(new Color(0.8f, 0.8f, 0.78f), 0.3f);
            var slot = Mat(new Color(0.1f, 0.1f, 0.1f), 0.2f);

            // Steel beams across the store (clear of the light rows).
            for (float x = -16f; x <= 16f; x += 4f)
                Box("Beam", new Vector3(x, 4.85f, 0f), new Vector3(0.28f, 0.28f, 29.6f), steel);

            // Two long air ducts along X, plus pipes beside them, hung from the beams.
            foreach (float z in new[] { -13.3f, 7.6f })
            {
                var d = Cyl("Duct", new Vector3(0f, 4.45f, z), new Vector3(0.7f, 19.7f, 0.7f), duct);
                d.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                for (float x = -19f; x <= 19f; x += 4f)
                    Box("Duct Ring", new Vector3(x, 4.45f, z), new Vector3(0.06f, 0.78f, 0.78f), steel);
                var p1 = Cyl("Pipe", new Vector3(0f, 4.6f, z + 0.65f), new Vector3(0.12f, 19.7f, 0.12f), pipeRed);
                p1.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                var p2 = Cyl("Pipe", new Vector3(0f, 4.6f, z - 0.65f), new Vector3(0.1f, 19.7f, 0.1f), pipeBlue);
                p2.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            }

            // Vent grilles on the ceiling in the corners and the middle.
            foreach (var p in new[] { new Vector2(-17.5f, -13f), new Vector2(17.5f, -13f), new Vector2(-17.5f, 12f), new Vector2(17.5f, 12f), new Vector2(0f, 12f), new Vector2(0f, -13f) })
            {
                Box("Vent", new Vector3(p.x, 4.97f, p.y), new Vector3(1.1f, 0.04f, 1.1f), grille);
                for (int i = -2; i <= 2; i++)
                    Box("Vent Slot", new Vector3(p.x, 4.945f, p.y + i * 0.2f), new Vector3(0.95f, 0.01f, 0.05f), slot);
            }
        }

        // ───────────────────────── Posters ─────────────────────────

        static void Posters()
        {
            string[] titles = { "SALE", "2 FOR 1", "FRESH BREAD", "NEW!", "FAMILY PACK", "WEEKEND DEAL", "COUPONS", "HALF PRICE" };
            string[] subs = { "EVERYTHING MUST GO", "TODAY ONLY", "BAKED DAILY", "TRY IT", "SAVE $5", "ALL WEEKEND", "CLIP & SAVE", "SELECTED LINES" };
            Color[] colors =
            {
                new Color(0.8f, 0.15f, 0.12f), new Color(0.12f, 0.4f, 0.75f), new Color(0.9f, 0.62f, 0.12f), new Color(0.1f, 0.55f, 0.35f),
                new Color(0.55f, 0.25f, 0.7f), new Color(0.85f, 0.3f, 0.45f), new Color(0.2f, 0.55f, 0.6f), new Color(0.75f, 0.2f, 0.12f),
            };

            int n = 0;
            // Left wall, readable looking -X (yaw -90).
            foreach (float z in new[] { -12f, -7.5f, 0f, 4f, 8f, 12f })
                Poster(new Vector3(-19.9f, 2.2f, z), -90f, titles[n % 8], subs[n % 8], colors[n++ % 8]);
            // Right wall, readable looking +X (yaw 90). Skip the boarded window (z -10..-2).
            foreach (float z in new[] { -13f, 1.5f, 5f, 9f, 12.5f })
                Poster(new Vector3(19.9f, 2.2f, z), 90f, titles[n % 8], subs[n % 8], colors[n++ % 8]);
        }

        static void Poster(Vector3 c, float yaw, string title, string sub, Color board)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 normal = rot * Vector3.back; // points out of the wall into the store
            var paper = Box("Poster", c, new Vector3(1.3f, 1.7f, 0.04f), Mat(board, 0.25f));
            paper.transform.rotation = rot;
            var border = Box("Poster Border", c - normal * 0.005f, new Vector3(1.4f, 1.8f, 0.03f), Mat(new Color(0.95f, 0.93f, 0.85f), 0.2f));
            border.transform.rotation = rot;
            Vector3 front = c + normal * 0.03f;
            LevelKit.Label(title, root, front + Vector3.up * 0.3f, rot, 0.2f, Color.white);
            LevelKit.Label(sub, root, front - Vector3.up * 0.25f, rot, 0.09f, new Color(1f, 0.95f, 0.8f));
        }

        // ───────────────────────── Building blocks ─────────────────────────

        static Material Mat(Color c, float smooth, float glow = 0f)
        {
            string key = $"{c.r:0.00}{c.g:0.00}{c.b:0.00}{smooth:0.0}{glow:0.0}";
            if (!mats.TryGetValue(key, out var m)) { m = LevelKit.ColorMaterial(c, smooth, glow); mats[key] = m; }
            return m;
        }

        static GameObject Box(string name, Vector3 center, Vector3 size, Material mat, float tiltX = 0f, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Finish(go, name, center, size, mat);
            go.transform.rotation = Quaternion.Euler(tiltX, yaw, 0f);
            return go;
        }

        static GameObject Cyl(string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            // Unity's cylinder is 2 m tall: the Y scale is half the length.
            Finish(go, name, center, new Vector3(size.x, size.y * 0.5f, size.z), mat);
            return go;
        }

        static void Finish(GameObject go, string name, Vector3 center, Vector3 size, Material mat)
        {
            LevelKit.RemoveCollider(go);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.position = center;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
