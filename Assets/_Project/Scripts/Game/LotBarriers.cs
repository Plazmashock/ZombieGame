using UnityEngine;

namespace LastCheckout.Game
{
    /// <summary>
    /// Closes the map so the only outside area you can reach is the PARKING LOT in front of the store,
    /// and the only way in is the FRONT DOOR.
    ///
    ///              z=38 ┌─────────────── boundary wall ───────────────┐
    ///                   │   (sealed)   ┌─────────────┐   (sealed)    │
    ///                   │              │    STORE    │               │
    ///                   │  (sealed)    │ loading bay │   (sealed)    │
    ///                   │              │   SEALED    │               │
    ///      z=-15.2 ═════╪══ side fence ╧══[ door ]═══╧═ side fence ══╪═════
    ///                   │             PARKING LOT (playable)           │
    ///             z=-52 └─────────────[ROAD CLOSED gate]──────────────┘
    ///
    ///   • side fences run from the store's front corners to the boundary wall (left and right),
    ///     so the sides and the back of the store can't be reached from the lot
    ///   • the loading bay opening in the back wall gets a jammed roll-up shutter
    ///   • the side window on the right wall is boarded up
    ///   • the two shop windows in the front get glass (you can see through, nothing can pass)
    ///
    /// Runs once at game start, BEFORE the zombie floor map is baked, so zombies know about the new walls.
    /// Wave zombies spawn only in the lot (see WaveSpawner).
    /// </summary>
    public static class LotBarriers
    {
        static Transform root;
        static Material glassMat;

        /// <summary>
        /// The loading bay's roll-up shutter and its sign, grouped so it can be rolled up and down.
        /// Its pivot sits at the TOP edge (y 3.56), so scaling it in Y makes it roll up. See LoadingBayTruck.
        /// </summary>
        public static Transform BayShutter { get; private set; }

        static readonly Color Concrete = new Color(0.46f, 0.46f, 0.44f);
        static readonly Color RustSheet = new Color(0.31f, 0.2f, 0.15f);
        static readonly Color Steel = new Color(0.14f, 0.15f, 0.16f);
        static readonly Color Hazard = new Color(0.95f, 0.75f, 0.1f);
        static readonly Color Warning = new Color(0.75f, 0.1f, 0.08f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { root = null; glassMat = null; BayShutter = null; }

        public static void Apply()
        {
            if (GameObject.Find("Lot Barriers") != null) return; // already built
            if (GameObject.Find("Store Floor") == null) return;   // not the supermarket scene

            root = new GameObject("Lot Barriers").transform;

            SideFence(-1f);
            SideFence(+1f);
            int before = root.childCount;
            LoadingBayShutter();
            GroupBayShutter(before);
            BoardedSideWindow();
            ShopWindow(-11f);
            ShopWindow(11f);
        }

        // ───────────────────────── Side fences (left and right of the lot) ─────────────────────────

        static void SideFence(float side)
        {
            const float z = -15.2f, innerX = 20.3f, outerX = 45.8f;
            float length = outerX - innerX;
            float cx = side * (innerX + outerX) * 0.5f;

            var concrete = LevelKit.ColorMaterial(Concrete, 0.1f);
            var rust = LevelKit.ColorMaterial(RustSheet, 0.2f);
            var steel = LevelKit.ColorMaterial(Steel, 0.5f);
            var yellow = LevelKit.ColorMaterial(Hazard, 0.2f);

            // Concrete barrier at the bottom, tall rusty sheet metal above it (4 m in total: nobody climbs it).
            Solid("Side Fence Base", new Vector3(cx, 0.5f, z), new Vector3(length, 1f, 0.7f), concrete);
            Solid("Side Fence Sheet", new Vector3(cx, 2.5f, z), new Vector3(length, 3f, 0.18f), rust);

            // Hazard stripe along the lot side, posts every 3 m, barbed wire on top.
            Deco("Fence Stripe", new Vector3(cx, 1.07f, z - 0.36f), new Vector3(length, 0.14f, 0.02f), yellow);
            for (float x = innerX + 0.4f; x <= outerX; x += 3f)
            {
                Deco("Fence Post", new Vector3(side * x, 2.1f, z - 0.12f), new Vector3(0.2f, 4.2f, 0.12f), steel);
                Deco("Wire Arm", new Vector3(side * x, 4.15f, z - 0.05f), new Vector3(0.06f, 0.5f, 0.06f), steel, 0f);
            }
            foreach (float y in new[] { 4.05f, 4.22f, 4.4f })
                Deco("Barbed Wire", new Vector3(cx, y, z - 0.05f), new Vector3(length, 0.02f, 0.02f), steel);

            // Warning signs facing the parking lot (you read them from the -Z side).
            foreach (float x in new[] { 27f, 40f })
                SignBoard("NO ENTRY", "RESTRICTED AREA", new Vector3(side * x, 2.4f, z - 0.4f), new Vector2(2.4f, 0.8f), Warning, Color.white, 0.36f);
        }

        // ───────────────────────── Loading bay (back wall) ─────────────────────────

        static void LoadingBayShutter()
        {
            // The opening in the back wall is x 5..9, up to 3.5 m. The shutter sits just inside (corridor side).
            const float z = 14.7f;
            var dark = LevelKit.ColorMaterial(new Color(0.26f, 0.28f, 0.29f), 0.35f);
            var rib = LevelKit.ColorMaterial(new Color(0.17f, 0.18f, 0.19f), 0.3f);
            var yellow = LevelKit.ColorMaterial(Hazard, 0.2f);
            var chain = LevelKit.ColorMaterial(new Color(0.55f, 0.55f, 0.5f), 0.6f);

            Solid("Loading Bay Shutter", new Vector3(7f, 1.78f, z), new Vector3(4.3f, 3.56f, 0.18f), dark);
            for (float y = 0.3f; y < 3.5f; y += 0.22f)
                Deco("Shutter Rib", new Vector3(7f, y, z - 0.1f), new Vector3(4.2f, 0.05f, 0.03f), rib);
            Deco("Shutter Stripe", new Vector3(7f, 0.12f, z - 0.1f), new Vector3(4.2f, 0.22f, 0.03f), yellow);
            Deco("Shutter Padlock", new Vector3(7f, 0.4f, z - 0.12f), new Vector3(0.12f, 0.16f, 0.06f), chain);
            Deco("Shutter Chain", new Vector3(7f, 0.55f, z - 0.11f), new Vector3(0.04f, 0.3f, 0.03f), chain);
            SignBoard("LOADING BAY", "CLOSED DURING WAVES", new Vector3(7f, 2.9f, z - 0.12f), new Vector2(2.6f, 0.8f), Warning, Color.white, 0.34f);
        }

        /// Moves the pieces LoadingBayShutter() just made under one parent whose pivot is the shutter's top edge.
        static void GroupBayShutter(int firstChild)
        {
            var group = new GameObject("Loading Bay Shutter Group").transform;
            group.SetParent(root, false);
            group.position = new Vector3(0f, 3.56f, 0f);
            for (int i = root.childCount - 2; i >= firstChild; i--)    // -2: the group itself is the last child
                root.GetChild(i).SetParent(group, true);
            BayShutter = group;
        }

        // ───────────────────────── Right-wall window ─────────────────────────

        static void BoardedSideWindow()
        {
            // The wall has a hole at z -10..-2, y 1..3 (x = 20). Fill it, then nail planks over the inside.
            var ply = LevelKit.ColorMaterial(new Color(0.16f, 0.13f, 0.1f), 0.1f);
            var plank = LevelKit.ColorMaterial(new Color(0.42f, 0.31f, 0.19f), 0.1f);
            var nail = LevelKit.ColorMaterial(new Color(0.55f, 0.55f, 0.5f), 0.5f);
            Solid("Boarded Window Fill", new Vector3(20f, 2f, -6f), new Vector3(0.32f, 2.1f, 8.2f), ply);

            float[] ys = { 1.25f, 1.75f, 2.25f, 2.75f };
            for (int i = 0; i < ys.Length; i++)
            {
                float tilt = (i % 2 == 0 ? 2.5f : -2f);
                Deco("Window Plank", new Vector3(19.72f, ys[i], -6f + (i % 2) * 0.1f), new Vector3(0.06f, 0.3f, 8.0f), plank, 0f, tilt);
                foreach (float zz in new[] { -9.6f, -6f, -2.4f })
                    Deco("Nail", new Vector3(19.68f, ys[i], zz), new Vector3(0.03f, 0.04f, 0.04f), nail);
            }
        }

        // ───────────────────────── Front shop windows ─────────────────────────

        static void ShopWindow(float centerX)
        {
            // Opening: width 12 m (x ±6 around the centre), y 1..3.5 in the front wall (z = -15).
            var frame = LevelKit.ColorMaterial(new Color(0.12f, 0.13f, 0.14f), 0.5f);
            Solid("Shop Window Glass", new Vector3(centerX, 2.25f, -15f), new Vector3(12.04f, 2.54f, 0.06f), Glass());
            Deco("Window Rail Top", new Vector3(centerX, 3.46f, -15.05f), new Vector3(12.1f, 0.1f, 0.14f), frame);
            Deco("Window Rail Bottom", new Vector3(centerX, 1.04f, -15.05f), new Vector3(12.1f, 0.1f, 0.14f), frame);
            for (int k = 0; k <= 4; k++)
                Deco("Window Mullion", new Vector3(centerX - 6f + k * 3f, 2.25f, -15.05f), new Vector3(0.08f, 2.45f, 0.12f), frame);
        }

        /// A see-through dark-blue glass (falls back to a tinted solid if the shader can't do transparency).
        static Material Glass()
        {
            if (glassMat != null) return glassMat;
            var m = new Material(LevelKit.ColorMaterial(new Color(0.55f, 0.7f, 0.8f), 0.9f));
            m.name = "Shop Glass";
            var c = new Color(0.55f, 0.72f, 0.82f, 0.16f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);   // 1 = transparent
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            glassMat = m;
            return m;
        }

        // ───────────────────────── Building blocks ─────────────────────────

        /// A box you and zombies bump into.
        static GameObject Solid(string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// A decoration with no collider.
        static GameObject Deco(string name, Vector3 center, Vector3 size, Material mat, float yaw = 0f, float tiltX = 0f)
        {
            var go = Solid(name, center, size, mat);
            LevelKit.RemoveCollider(go);
            go.transform.rotation = Quaternion.Euler(tiltX, yaw, 0f);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// A board with a big title and a smaller line under it. Read from the -Z side.
        static void SignBoard(string title, string sub, Vector3 center, Vector2 size, Color board, Color text, float textHeight)
        {
            Deco("Sign Board", center, new Vector3(size.x, size.y, 0.05f), LevelKit.ColorMaterial(board, 0.3f));
            Vector3 front = center + Vector3.back * 0.04f;
            LevelKit.Label(title, root, front + Vector3.up * (size.y * 0.14f), Quaternion.identity, textHeight, text);
            if (!string.IsNullOrEmpty(sub))
                LevelKit.Label(sub, root, front - Vector3.up * (size.y * 0.27f), Quaternion.identity, textHeight * 0.45f, text);
        }
    }
}
