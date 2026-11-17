using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LastCheckout.Items;
using LastCheckout.World;

namespace LastCheckout.Game
{
    /// <summary>
    /// Turns the grey box store into a real (abandoned) supermarket when you press Play.
    /// Like the Armory, it's built in code, so there's nothing to set up and nothing in the scene to break.
    ///
    ///   INSIDE                                         OUTSIDE
    ///   • ~2000 products on the shelves (some looted)  • "PRICEWELL MARKET" sign + green fascia
    ///   • colour-coded aisles with hanging signs       • sidewalk, bollards, zebra crossing
    ///   • end-cap displays + a SALE island (cover!)    • painted parking bays, sodium street lamps
    ///   • glowing fridges, fluorescent tube rows       • detailed + extra wrecked cars, cart corral
    ///   • checkout belts, candy racks, lane signs      • dumpster moved off the loading bay
    ///   • yellow floor line: entrance → ARMORY
    ///   • blood, litter, tipped carts, flickering lights
    ///
    /// Speed trick: hundreds of small decorations are glued together into a few big meshes
    /// ("batches", one per colour), so the game draws them in a handful of steps instead of thousands.
    /// Store layout (x −20..20, z −15..15) is described in GreyboxSupermarketBuilder.
    /// </summary>
    public static class StoreMakeover
    {
        const float FloorTop = 0.02f;
        static readonly float[] BoardHeights = { 0.45f, 1.05f, 1.65f };

        // The 8 aisle shelves (same x as the greybox builder) and what each one sells.
        static readonly float[] AisleX = { -16f, -12f, -8f, -4f, 4f, 8f, 12f, 16f };
        static readonly string[] AisleNames = { "CANNED FOOD", "DRINKS", "PHARMACY", "HOUSEHOLD", "KITCHEN", "SPORTS", "HARDWARE", "SNACKS" };
        static readonly Color[] AisleColors =
        {
            new Color(0.80f, 0.20f, 0.15f), new Color(0.15f, 0.42f, 0.82f), new Color(0.08f, 0.62f, 0.52f), new Color(0.52f, 0.30f, 0.72f),
            new Color(0.93f, 0.50f, 0.12f), new Color(0.42f, 0.70f, 0.15f), new Color(0.95f, 0.75f, 0.10f), new Color(0.90f, 0.30f, 0.55f),
        };
        static readonly Color[][] AislePalettes =
        {
            new[] { new Color(0.78f, 0.18f, 0.12f), new Color(0.92f, 0.78f, 0.25f), new Color(0.85f, 0.85f, 0.80f), new Color(0.55f, 0.30f, 0.15f), new Color(0.30f, 0.55f, 0.20f) },
            new[] { new Color(0.20f, 0.45f, 0.85f), new Color(0.70f, 0.85f, 0.95f), new Color(0.85f, 0.15f, 0.12f), new Color(0.95f, 0.55f, 0.10f), new Color(0.10f, 0.12f, 0.15f) },
            new[] { new Color(0.92f, 0.94f, 0.95f), new Color(0.10f, 0.60f, 0.55f), new Color(0.35f, 0.55f, 0.85f), new Color(0.85f, 0.25f, 0.30f) },
            new[] { new Color(0.55f, 0.32f, 0.75f), new Color(0.95f, 0.85f, 0.20f), new Color(0.30f, 0.70f, 0.90f), new Color(0.92f, 0.92f, 0.90f) },
            new[] { new Color(0.75f, 0.76f, 0.78f), new Color(0.95f, 0.55f, 0.15f), new Color(0.20f, 0.20f, 0.22f), new Color(0.90f, 0.90f, 0.86f) },
            new[] { new Color(0.45f, 0.72f, 0.15f), new Color(0.95f, 0.45f, 0.10f), new Color(0.15f, 0.30f, 0.65f), new Color(0.90f, 0.90f, 0.90f) },
            new[] { new Color(0.95f, 0.72f, 0.10f), new Color(0.40f, 0.42f, 0.45f), new Color(0.85f, 0.30f, 0.10f), new Color(0.15f, 0.15f, 0.16f) },
            new[] { new Color(0.92f, 0.30f, 0.55f), new Color(0.98f, 0.80f, 0.15f), new Color(0.30f, 0.75f, 0.85f), new Color(0.60f, 0.25f, 0.70f), new Color(0.95f, 0.45f, 0.15f) },
        };
        static readonly Color[] MixedPalette =
        {
            new Color(0.80f, 0.20f, 0.15f), new Color(0.20f, 0.45f, 0.85f), new Color(0.95f, 0.80f, 0.20f),
            new Color(0.90f, 0.90f, 0.88f), new Color(0.40f, 0.70f, 0.20f), new Color(0.95f, 0.50f, 0.15f),
        };
        static readonly Color[] CardboardPalette =
        {
            new Color(0.62f, 0.47f, 0.28f), new Color(0.55f, 0.41f, 0.24f), new Color(0.68f, 0.54f, 0.34f), new Color(0.48f, 0.36f, 0.22f),
        };

        // Brand colours
        static readonly Color BrandGreen = new Color(0.08f, 0.32f, 0.22f);
        static readonly Color Cream = new Color(1f, 0.93f, 0.76f);
        static readonly Color Accent = new Color(1f, 0.78f, 0.24f);
        static readonly Color DarkMetal = new Color(0.12f, 0.12f, 0.13f);
        static readonly Color Steel = new Color(0.62f, 0.64f, 0.66f);
        static readonly Color Sodium = new Color(1f, 0.70f, 0.40f);

        static Transform root;
        static System.Random rng;
        static Material baseMaterial;
        static Mesh cube;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<Material, List<CombineInstance>> batches = new Dictionary<Material, List<CombineInstance>>();
        static readonly List<Vector3> lootSpots = new List<Vector3>();

        // ───────────────────────── Entry point ─────────────────────────

        public static void Apply()
        {
            if (GameObject.Find("Store Makeover") != null) return; // already done
            if (GameObject.Find("Store Floor") == null)
            {
                Debug.LogWarning("[Makeover] This isn't the supermarket scene (no 'Store Floor'), so I didn't decorate it.");
                return;
            }

            root = new GameObject("Store Makeover").transform;
            rng = new System.Random(1337); // same seed = the store looks the same every time
            materials.Clear();
            batches.Clear();
            lootSpots.Clear();

            baseMaterial = LevelKit.SceneMaterial("Store Floor");
            cube = GrabCubeMesh();
            foreach (var wi in LevelKit.FindAll<WorldItem>()) lootSpots.Add(wi.transform.position);

            FixShelves();
            FloorAndWalls();
            StockShelves();
            EndCapsAndSaleIsland();
            Fridges();
            Checkouts();
            CeilingLights();
            Signs();
            GuideLineToArmory();
            Mess();
            Carts();
            Outside();
            NightSky();
            WorldEdge();

            FlushBatches();
        }

        // ───────────────────────── Inside ─────────────────────────

        /// Two fixes for the store's own shelves:
        ///  1. FLICKER: the thin shelf boards were exactly as long as the shelf body, so their ends sat in the same
        ///     spot as the body's ends and flickered as you moved ("z-fighting"). We hide them and redraw them 2 cm shorter.
        ///  2. CLIPPING: the boards stuck out past the shelf's invisible wall (collider), so the camera could slide
        ///     into them. The collider is widened to cover most of the board, but not its front edge,
        ///     so loot sitting on the boards is still easy to grab.
        static void FixShelves()
        {
            foreach (var t in LevelKit.FindAll<Transform>())
            {
                string n = t.name;
                if (!(n.StartsWith("Aisle Shelf") || n.StartsWith("Wall Shelf") || n.StartsWith("Pallet Rack"))) continue;

                Vector3 s = t.lossyScale;
                bool alongZ = s.z >= s.x;
                var body = t.GetComponent<BoxCollider>();
                if (body != null)
                    body.size = alongZ ? new Vector3((s.x + 0.24f) / s.x, 1f, 1f) : new Vector3(1f, 1f, (s.z + 0.24f) / s.z);

                foreach (Transform child in t)
                {
                    if (child.name != "Board") continue;
                    var r = child.GetComponent<Renderer>();
                    if (r == null || !r.enabled) continue;
                    Vector3 size = child.lossyScale; // a cube primitive: its scale IS its size in meters
                    if (alongZ) size.z -= 0.02f; else size.x -= 0.02f;
                    r.enabled = false;
                    Piece(r.sharedMaterial, child.position, size, child.rotation);
                }
            }
        }

        static void FloorAndWalls()
        {
            // Checkerboard floor tiles across the sales hall (every other 1 m tile slightly darker).
            var darkTile = Mat(new Color(0.47f, 0.47f, 0.44f), 0.45f);
            for (int ix = 0; ix < 40; ix++)
                for (int iz = 0; iz < 24; iz++)
                    if (((ix + iz) & 1) == 1)
                        Piece(darkTile, new Vector3(-19.5f + ix, 0.0255f, -14.5f + iz), new Vector3(1f, 0.007f, 1f));

            // Dark green wall panelling (1 m high) with a cream trim, all around the sales hall.
            var band = Mat(BrandGreen, 0.35f);
            var trim = Mat(Cream, 0.3f);
            void BandX(float z, float x0, float x1, float side)
            {
                float zc = z + side * 0.17f;
                Piece(band, new Vector3((x0 + x1) * 0.5f, 0.5f, zc), new Vector3(x1 - x0, 1f, 0.04f));
                Piece(trim, new Vector3((x0 + x1) * 0.5f, 1.03f, zc + side * 0.005f), new Vector3(x1 - x0, 0.06f, 0.05f));
            }
            void BandZ(float x, float z0, float z1, float side)
            {
                float xc = x + side * 0.17f;
                Piece(band, new Vector3(xc, 0.5f, (z0 + z1) * 0.5f), new Vector3(0.04f, 1f, z1 - z0));
                Piece(trim, new Vector3(xc + side * 0.005f, 1.03f, (z0 + z1) * 0.5f), new Vector3(0.05f, 0.06f, z1 - z0));
            }
            BandX(-15f, -19.85f, -3f, +1f); BandX(-15f, 3f, 19.85f, +1f);       // front wall (gap = entrance)
            BandX(9f, -19.85f, -10f, -1f); BandX(9f, -8.5f, 1.85f, -1f);        // storage wall (gap = door)
            BandX(9f, 11.85f, 15f, -1f); BandX(9f, 16.5f, 19.85f, -1f);         // office wall (gap = door)
            BandZ(-20f, -14.85f, -5f, +1f); BandZ(-20f, -3.5f, 8.85f, +1f);     // left wall (gap = emergency exit)
            BandZ(20f, -14.85f, 8.85f, -1f);                                    // right wall
        }

        static void StockShelves()
        {
            var priceStrip = Mat(new Color(0.95f, 0.95f, 0.92f), 0.2f);

            // Aisle shelves: both sides, three boards each.
            for (int i = 0; i < AisleX.Length; i++)
            {
                float x = AisleX[i];
                var header = Mat(AisleColors[i], 0.3f, 0.35f);
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int level = 0; level < BoardHeights.Length; level++)
                    {
                        float boardTop = FloorTop + BoardHeights[level] + 0.025f;
                        float maxH = level == BoardHeights.Length - 1 ? 0.45f : 0.42f;
                        // (aisle products removed: the shelves are empty)
                        Piece(priceStrip, new Vector3(x + side * 0.805f, FloorTop + BoardHeights[level], 0.5f), new Vector3(0.012f, 0.062f, 11f));
                    }
                    // Coloured header strip along the top of the shelf = which department you're in.
                    Piece(header, new Vector3(x + side * 0.515f, FloorTop + 2.05f, 0.5f), new Vector3(0.03f, 0.22f, 11f));

                }
            }

            // Wall shelves (only the side facing the store).
            for (int level = 0; level < BoardHeights.Length; level++)
            {
                float boardTop = FloorTop + BoardHeights[level] + 0.025f;
                _ = boardTop; // wall shelves are empty too
            }

            // Storage room pallet rack: big cardboard boxes.
            float[] rackBoards = { 0.45f, 1.05f, 1.65f, 2.25f };
            foreach (float y in rackBoards)
                FillRun(false, 13.7f, -18f, -4f, FloorTop + y + 0.025f, 0.5f, 0.28f, 0.35f, 0.7f, CardboardPalette, 0.12f);
        }

        /// Fills one shelf board with rows of products ("facings"), leaving looted gaps,
        /// and never covering a real pickup item.
        static void FillRun(bool alongZ, float fixedCoord, float from, float to, float baseY, float maxH, float depth,
                            float minW, float maxW, Color[] palette, float gapChance)
        {
            float t = from + 0.04f;
            while (t < to - 0.1f)
            {
                if (Chance(gapChance)) { t += R(0.25f, 0.9f); continue; } // someone already looted this bit

                var mat = Mat(Pick(palette), 0.35f);
                float w = R(minW, maxW), h = R(0.14f, maxH);
                int facings = rng.Next(1, 5);
                for (int k = 0; k < facings && t + w < to - 0.02f; k++)
                {
                    float c = t + w * 0.5f;
                    Vector3 pos = alongZ ? new Vector3(fixedCoord, baseY + h * 0.5f, c) : new Vector3(c, baseY + h * 0.5f, fixedCoord);
                    if (!NearLoot(pos, 0.38f))
                    {
                        Vector3 size = alongZ ? new Vector3(depth, h, w) : new Vector3(w, h, depth);
                        if (Chance(0.05f)) // tipped over, lying on its side
                        {
                            size = alongZ ? new Vector3(depth, w, h) : new Vector3(h, w, depth);
                            pos.y = baseY + w * 0.5f;
                        }
                        Piece(mat, pos, size, Quaternion.Euler(0f, R(-4f, 4f), 0f));
                    }
                    t += w + 0.012f;
                }
                t += 0.02f;
            }
        }

        static void EndCapsAndSaleIsland()
        {
            var priceCard = Mat(Accent, 0.2f, 0.3f);
            for (int i = 0; i < AisleX.Length; i++)
            {
                float x = AisleX[i];
                var body = Mat(Dim(AisleColors[i], 0.75f), 0.3f);

                // Front (towards the checkouts) and back end caps: solid, so they're real cover.
                Solid("End Cap Front", new Vector3(x, FloorTop + 0.6f, -5.33f), new Vector3(1.1f, 1.2f, 0.6f), body);
                Solid("End Cap Back", new Vector3(x, FloorTop + 0.6f, 6.26f), new Vector3(1.1f, 1.2f, 0.5f), body);
                Piece(priceCard, new Vector3(x, FloorTop + 0.85f, -5.64f), new Vector3(0.5f, 0.24f, 0.01f));

                // Stacks of products on top.
                foreach (float z in new[] { -5.33f, 6.26f })
                    for (int gx = -1; gx <= 1; gx++)
                    {
                        if (Chance(0.25f)) continue;
                        var m = Mat(Pick(AislePalettes[i]), 0.35f);
                        int stack = rng.Next(1, 4);
                        for (int s = 0; s < stack; s++)
                            Piece(m, new Vector3(x + gx * 0.34f, FloorTop + 1.2f + 0.11f + s * 0.22f, z), new Vector3(0.3f, 0.2f, 0.36f),
                                  Quaternion.Euler(0f, R(-6f, 6f), 0f));
                    }
            }

            // SALE island in the middle of the centre aisle: a pyramid of cans. Good cover in a fight.
            Solid("Sale Island", new Vector3(0f, FloorTop + 0.28f, 0.5f), new Vector3(2.2f, 0.56f, 3.0f), Mat(new Color(0.72f, 0.12f, 0.10f), 0.3f));
            Piece(Mat(Cream, 0.3f), new Vector3(0f, FloorTop + 0.575f, 0.5f), new Vector3(2.3f, 0.03f, 3.1f));
            Color[] canColors = { new Color(0.85f, 0.15f, 0.12f), new Color(0.92f, 0.92f, 0.88f), new Color(0.95f, 0.78f, 0.15f), new Color(0.85f, 0.15f, 0.12f) };
            for (int layer = 0; layer < 4; layer++)
            {
                var m = Mat(canColors[layer], 0.5f);
                int cols = 9 - layer * 2, rows = 12 - layer * 2;
                for (int cx = 0; cx < cols; cx++)
                    for (int cz = 0; cz < rows; cz++)
                    {
                        if (layer == 0 && Chance(0.06f)) continue;
                        Piece(m, new Vector3((cx - (cols - 1) * 0.5f) * 0.2f, FloorTop + 0.59f + 0.1f + layer * 0.2f, 0.5f + (cz - (rows - 1) * 0.5f) * 0.2f),
                              new Vector3(0.16f, 0.2f, 0.16f));
                    }
            }
            Piece(Mat(DarkMetal, 0.5f), new Vector3(0f, FloorTop + 1.4f, 1.92f), new Vector3(0.05f, 1.65f, 0.05f));
            Sign("SALE", "EVERYTHING MUST GO", new Vector3(0f, 2.45f, 1.92f), 0f, new Vector2(1.5f, 0.6f), new Color(0.95f, 0.78f, 0.15f), new Color(0.6f, 0.05f, 0.05f), 0.3f, twoSided: true);
        }

        static void Fridges()
        {
            // The fridge block: x −19..−13, front face at z 7.95. Glowing interior, door frames, bottles behind the glass.
            var glow = Mat(new Color(0.55f, 0.8f, 0.95f), 0.9f, 0.9f);
            var frame = Mat(new Color(0.85f, 0.87f, 0.9f), 0.6f);
            var rail = Mat(new Color(0.3f, 0.32f, 0.35f), 0.5f);
            Piece(glow, new Vector3(-16f, FloorTop + 1.12f, 7.94f), new Vector3(5.8f, 1.9f, 0.01f));
            for (int i = 0; i <= 5; i++) Piece(frame, new Vector3(-19f + i * 1.2f, FloorTop + 1.12f, 7.92f), new Vector3(0.07f, 1.98f, 0.04f));
            Piece(frame, new Vector3(-16f, FloorTop + 2.12f, 7.92f), new Vector3(6f, 0.08f, 0.04f));
            Piece(frame, new Vector3(-16f, FloorTop + 0.15f, 7.92f), new Vector3(6f, 0.1f, 0.04f));
            foreach (float y in new[] { 0.6f, 1.1f, 1.6f })
            {
                Piece(rail, new Vector3(-16f, FloorTop + y, 7.912f), new Vector3(5.8f, 0.025f, 0.01f));
                for (float x = -18.85f; x < -13.15f; x += 0.11f)
                {
                    if (Chance(0.3f)) { x += 0.15f; continue; }
                    float h = R(0.18f, 0.3f);
                    Piece(Mat(Pick(AislePalettes[1]), 0.6f), new Vector3(x, FloorTop + y + 0.015f + h * 0.5f, 7.925f), new Vector3(0.07f, h, 0.01f));
                }
            }
            AddLight("Fridge Glow", new Vector3(-16f, 1.3f, 7.2f), new Color(0.6f, 0.85f, 1f), 1.6f, 5f);
        }

        static void Checkouts()
        {
            float[] laneX = { -15f, -11f, -7f, 7f, 11f, 15f };
            var belt = Mat(new Color(0.05f, 0.05f, 0.06f), 0.6f);
            var steel = Mat(Steel, 0.7f);
            var dark = Mat(DarkMetal, 0.5f);
            var rackMat = Mat(new Color(0.3f, 0.36f, 0.42f), 0.3f);
            for (int i = 0; i < laneX.Length; i++)
            {
                float x = laneX[i];
                // Conveyor belt and divider bar on the counter (counter top is at y ≈ 1.02).
                Piece(belt, new Vector3(x - 0.05f, FloorTop + 1.005f, -10.0f), new Vector3(0.5f, 0.012f, 1.6f));
                Piece(steel, new Vector3(x - 0.05f, FloorTop + 1.03f, -9.15f), new Vector3(0.52f, 0.04f, 0.04f));
                Piece(dark, new Vector3(x + 0.27f, FloorTop + 1.08f, -8.95f), new Vector3(0.12f, 0.14f, 0.18f)); // card reader

                // Candy rack next to the counter (solid).
                Solid("Candy Rack", new Vector3(x - 0.62f, FloorTop + 0.6f, -10.5f), new Vector3(0.32f, 1.2f, 0.7f), rackMat);
                foreach (float y in new[] { 0.35f, 0.75f, 1.15f })
                {
                    Piece(steel, new Vector3(x - 0.82f, FloorTop + y, -10.5f), new Vector3(0.1f, 0.02f, 0.7f));
                    FillRun(true, x - 0.83f, -10.85f, -10.15f, FloorTop + y + 0.01f, 0.18f, 0.08f, 0.06f, 0.12f, AislePalettes[7], 0.1f);
                }

                // Lane number on a pole.
                Piece(dark, new Vector3(x + 0.32f, FloorTop + 1.3f, -11.05f), new Vector3(0.05f, 2.6f, 0.05f));
                Sign((i + 1).ToString(), null, new Vector3(x + 0.32f, 2.85f, -11.05f), 0f, new Vector2(0.55f, 0.45f), BrandGreen, Cream, 0.34f, twoSided: true, glow: 0.4f);
            }
        }

        static void CeilingLights()
        {
            // Rows of fluorescent tubes above the aisles. A few are dead.
            var housing = Mat(new Color(0.78f, 0.78f, 0.75f), 0.3f);
            var tube = Mat(new Color(1f, 0.97f, 0.9f), 0.2f, 2.2f);
            var dead = Mat(new Color(0.32f, 0.32f, 0.3f), 0.4f);
            float[] xs = { -14f, -10f, -6f, -2f, 2f, 6f, 10f, 14f };
            foreach (float x in xs)
                for (float z = -12f; z <= 6.1f; z += 2f)
                {
                    Piece(housing, new Vector3(x, 4.965f, z), new Vector3(1.3f, 0.05f, 0.24f));
                    Piece(Chance(0.09f) ? dead : tube, new Vector3(x, 4.928f, z), new Vector3(1.2f, 0.03f, 0.13f));
                }

            // Two of the store's own lights are on their last legs.
            foreach (var l in LevelKit.FindAll<Light>())
                if ((l.name == "Hall Light (12,3)" || l.name == "Storage Light 1") && l.GetComponent<LightFlicker>() == null)
                    l.gameObject.AddComponent<LightFlicker>();
        }

        static void Signs()
        {
            // Hanging aisle signs (number + department), readable from both ends.
            var wire = Mat(DarkMetal, 0.4f);
            for (int i = 0; i < AisleX.Length; i++)
            {
                float x = AisleX[i];
                Sign((i + 1).ToString(), AisleNames[i], new Vector3(x, 3.75f, -4.4f), 0f, new Vector2(1.8f, 0.85f), AisleColors[i], Color.white, 0.36f, twoSided: true);
                Piece(wire, new Vector3(x - 0.7f, 4.59f, -4.4f), new Vector3(0.015f, 0.83f, 0.015f));
                Piece(wire, new Vector3(x + 0.7f, 4.59f, -4.4f), new Vector3(0.015f, 0.83f, 0.015f));
            }

            // Department and wall signs. yaw 0 = read from the store side of a back wall (looking +z),
            // 180 = front wall seen from inside, 90 = right wall, −90 = left wall.
            Sign("FRESH & FROZEN", null, new Vector3(-16f, 3.1f, 8.82f), 0f, new Vector2(5.6f, 0.8f), new Color(0.12f, 0.38f, 0.7f), Color.white, 0.45f);
            Sign("STAFF ONLY", "STORAGE", new Vector3(-9.25f, 2.95f, 8.82f), 0f, new Vector2(1.6f, 0.55f), DarkMetal, Accent, 0.22f);
            Sign("OFFICE", null, new Vector3(15.75f, 2.85f, 8.82f), 0f, new Vector2(1.4f, 0.4f), DarkMetal, Color.white, 0.22f);
            Sign("THANK YOU FOR SHOPPING", null, new Vector3(0f, 4.2f, -14.82f), 180f, new Vector2(7f, 0.7f), BrandGreen, Cream, 0.42f);
            Sign("EXIT", null, new Vector3(0f, 3.35f, -14.82f), 180f, new Vector2(0.9f, 0.32f), new Color(0.05f, 0.55f, 0.25f), Color.white, 0.2f, glow: 0.8f);
            Sign("FRESH DEALS DAILY", null, new Vector3(19.82f, 3.6f, 3f), 90f, new Vector2(5f, 1.1f), new Color(0.72f, 0.12f, 0.1f), Cream, 0.5f);
            Sign("STOCK UP. STAY SAFE.", null, new Vector3(-19.82f, 3.6f, 2f), -90f, new Vector2(5f, 1.1f), Accent, new Color(0.1f, 0.1f, 0.1f), 0.48f);
            Sign("EMERGENCY EXIT", "LOCKED", new Vector3(-19.82f, 2.95f, -4.25f), -90f, new Vector2(1.6f, 0.5f), new Color(0.7f, 0.1f, 0.08f), Color.white, 0.18f);

            // ARMORY sign hanging at the mouth of the back corridor.
            Sign("ARMORY", "BACK CORRIDOR", new Vector3(7f, 3.9f, 8.7f), 0f, new Vector2(2.6f, 0.8f), new Color(0.06f, 0.06f, 0.07f), Accent, 0.4f, glow: 0.2f);
            Piece(wire, new Vector3(5.9f, 4.65f, 8.7f), new Vector3(0.015f, 0.7f, 0.015f));
            Piece(wire, new Vector3(8.1f, 4.65f, 8.7f), new Vector3(0.015f, 0.7f, 0.015f));
        }

        /// Yellow painted line with arrows: entrance → up the right side of the centre aisle → back corridor → armory.
        static void GuideLineToArmory()
        {
            var paint = Mat(Accent, 0.3f, 0.35f);
            Vector3[] path =
            {
                new Vector3(2.5f, 0f, -13.5f), new Vector3(2.5f, 0f, 7.6f),
                new Vector3(10.8f, 0f, 7.6f), new Vector3(10.8f, 0f, 14.2f),
            };
            const float y = 0.045f; // floor layers, bottom to top: floor .020, tiles .029, blood .041, paint .047, paper .051
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                Vector3 dir = (b - a).normalized;
                float len = Vector3.Distance(a, b);
                Quaternion rot = Quaternion.LookRotation(dir);
                Piece(paint, (a + b) * 0.5f + Vector3.up * y, new Vector3(0.1f, 0.004f, len + 0.1f), rot);

                for (float d = 1.25f; d < len - 0.5f; d += 2.5f)
                {
                    Vector3 tip = a + dir * (d + 0.25f) + Vector3.up * y;
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 arm = (-dir + side * s).normalized;
                        Piece(paint, tip + arm * 0.2f + Vector3.up * 0.0005f, new Vector3(0.09f, 0.004f, 0.42f), Quaternion.LookRotation(arm));
                    }
                }
            }

            // Hazard stripes across the loading bay (x 5..9 at the back wall).
            var yellow = Mat(Accent, 0.3f);
            var black = Mat(new Color(0.06f, 0.06f, 0.06f), 0.3f);
            for (int i = 0; i < 8; i++)
                Piece(i % 2 == 0 ? yellow : black, new Vector3(5.25f + i * 0.5f, y, 14.5f), new Vector3(0.5f, 0.004f, 0.6f));
        }

        static void Mess()
        {
            // Blood: a drag trail out of the storage room, and a few splatters near the doors.
            var fresh = Mat(new Color(0.30f, 0.02f, 0.02f), 0.8f);
            var dried = Mat(new Color(0.17f, 0.04f, 0.03f), 0.3f);
            void Splat(float x, float z, float size)
            {
                int n = rng.Next(2, 5);
                for (int k = 0; k < n; k++)
                    Piece(Chance(0.5f) ? fresh : dried,
                          new Vector3(x + R(-0.3f, 0.3f) * size, 0.0335f + k * 0.002f, z + R(-0.3f, 0.3f) * size),
                          new Vector3(R(0.3f, 1f) * size, 0.002f, R(0.3f, 1f) * size), Quaternion.Euler(0f, R(0f, 180f), 0f));
            }
            Splat(-9.3f, 8.2f, 0.8f); Splat(-8.6f, 7.4f, 0.6f); Splat(-7.9f, 6.9f, 0.5f); Splat(-7.2f, 6.2f, 0.7f);
            Splat(-12f, 11.5f, 1.1f); Splat(1.0f, -13.6f, 1.2f); Splat(-0.6f, -12.8f, 0.6f);
            Splat(5.5f, -11.5f, 0.9f); Splat(15.6f, 7.8f, 0.7f); Splat(5.6f, 11.0f, 0.6f);
            Piece(fresh, new Vector3(-11f, 1.25f, 8.825f), new Vector3(0.5f, 0.8f, 0.01f)); // smear on the storage wall

            // Litter: receipts, flyers, paper.
            var paper = Mat(new Color(0.9f, 0.88f, 0.82f), 0.1f);
            for (int i = 0; i < 60; i++)
                Piece(paper, new Vector3(R(-19f, 19f), 0.05f, R(-14f, 8.5f)), new Vector3(0.21f, 0.002f, 0.29f), Quaternion.Euler(0f, R(0f, 180f), 0f));

            // Cardboard boxes dumped in the back corridor (solid: zombies and you walk around them).
            var card = Mat(CardboardPalette[0], 0.2f);
            Solid("Box Pile", new Vector3(3.0f, FloorTop + 0.35f, 13.6f), new Vector3(0.8f, 0.7f, 0.7f), card);
            Solid("Box Pile", new Vector3(3.1f, FloorTop + 0.95f, 13.55f), new Vector3(0.6f, 0.5f, 0.55f), Mat(CardboardPalette[2], 0.2f), Quaternion.Euler(0f, 15f, 0f));
            Solid("Box Pile", new Vector3(3.2f, FloorTop + 0.3f, 10.4f), new Vector3(0.7f, 0.6f, 0.9f), Mat(CardboardPalette[1], 0.2f), Quaternion.Euler(0f, -10f, 0f));
        }

        static void Carts()
        {
            Cart(new Vector3(-2.0f, 0f, -7.0f), 20f, false);
            Cart(new Vector3(-6.0f, 0f, -13.0f), 100f, true);
            Cart(new Vector3(13.2f, 0f, -12.8f), -30f, false);
            Cart(new Vector3(-2.6f, 0f, 7.0f), 80f, false);
            Cart(new Vector3(-10.5f, 0f, 3.5f), 10f, true);
            // Outside
            Cart(new Vector3(4.5f, 0.12f, -17.2f), 160f, false);
            Cart(new Vector3(-11f, 0f, -20f), 40f, true);
            Cart(new Vector3(5.7f, 0f, -30.4f), 0f, false);
            Cart(new Vector3(5.7f, 0f, -31.5f), 0f, false);
        }

        /// A shopping cart made of boxes. 'toppled' = lying on its side.
        static void Cart(Vector3 position, float yaw, bool toppled)
        {
            var cart = new GameObject("Shopping Cart").transform;
            cart.SetParent(root, false);
            cart.SetPositionAndRotation(position + (toppled ? Vector3.up * 0.3f : Vector3.zero), Quaternion.Euler(0f, yaw, toppled ? 90f : 0f));

            var col = cart.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.55f, 0f);
            col.size = new Vector3(0.6f, 1.05f, 1.0f);

            var wire = Mat(new Color(0.55f, 0.57f, 0.6f), 0.7f);
            var red = Mat(new Color(0.75f, 0.1f, 0.08f), 0.4f);
            var black = Mat(new Color(0.05f, 0.05f, 0.05f), 0.3f);
            void Part(Vector3 p, Vector3 s, Material m) => CartPart(cart, p, s, m);

            Part(new Vector3(0f, 0.42f, 0f), new Vector3(0.55f, 0.03f, 0.85f), wire);
            Part(new Vector3(-0.275f, 0.65f, 0f), new Vector3(0.02f, 0.45f, 0.85f), wire);
            Part(new Vector3(0.275f, 0.65f, 0f), new Vector3(0.02f, 0.45f, 0.85f), wire);
            Part(new Vector3(0f, 0.65f, 0.425f), new Vector3(0.55f, 0.45f, 0.02f), wire);
            Part(new Vector3(0f, 0.65f, -0.425f), new Vector3(0.55f, 0.45f, 0.02f), wire);
            Part(new Vector3(0f, 0.88f, 0f), new Vector3(0.6f, 0.025f, 0.9f), wire);     // top rim
            Part(new Vector3(0f, 1.0f, -0.55f), new Vector3(0.62f, 0.04f, 0.04f), red);  // handle
            Part(new Vector3(-0.27f, 0.93f, -0.5f), new Vector3(0.025f, 0.16f, 0.1f), wire);
            Part(new Vector3(0.27f, 0.93f, -0.5f), new Vector3(0.025f, 0.16f, 0.1f), wire);
            foreach (float x in new[] { -0.22f, 0.22f })
            {
                Part(new Vector3(x, 0.12f, 0f), new Vector3(0.03f, 0.03f, 0.9f), wire);
                foreach (float z in new[] { -0.38f, 0.38f })
                {
                    Part(new Vector3(x, 0.27f, z), new Vector3(0.025f, 0.3f, 0.025f), wire);
                    Part(new Vector3(x, 0.05f, z), new Vector3(0.04f, 0.1f, 0.1f), black);
                }
            }
        }

        // ───────────────────────── Outside ─────────────────────────

        static void Outside()
        {
            var concrete = Mat(new Color(0.42f, 0.42f, 0.4f), 0.15f);
            var white = Mat(new Color(0.82f, 0.82f, 0.78f), 0.2f);
            var dark = Mat(DarkMetal, 0.5f);

            // Sidewalk along the front (12 cm step: easy to walk up for you and for zombies).
            Solid("Sidewalk", new Vector3(0f, 0.06f, -16.83f), new Vector3(41f, 0.12f, 3.35f), concrete);

            // Green fascia band + the store's name above the entrance.
            Piece(Mat(BrandGreen, 0.3f), new Vector3(0f, 4.3f, -15.17f), new Vector3(40.3f, 1.4f, 0.04f));
            Sign("PRICEWELL MARKET", null, new Vector3(0f, 4.3f, -15.3f), 0f, new Vector2(13f, 1.2f), new Color(0.05f, 0.18f, 0.12f), Cream, 0.8f, glow: 0.2f);
            Piece(Mat(new Color(0.75f, 0.12f, 0.1f), 0.3f, 0.5f), new Vector3(0f, 3.72f, -15.37f), new Vector3(12.5f, 0.06f, 0.02f));
            Sign("OPEN 24 HRS", null, new Vector3(4.1f, 2.4f, -15.25f), 0f, new Vector2(1.7f, 0.5f), new Color(0.75f, 0.1f, 0.08f), Color.white, 0.26f, glow: 0.5f);
            AddLight("Store Sign Light", new Vector3(0f, 3.4f, -17.2f), new Color(1f, 0.9f, 0.75f), 2.2f, 9f);

            // Bollards in front of the entrance (gaps wide enough to walk through).
            foreach (float x in new[] { -8f, -6f, -4.2f, 4.2f, 6f, 8f })
            {
                var b = Cylinder("Bollard", new Vector3(x, 0.62f, -17.9f), new Vector3(0.24f, 0.5f, 0.24f), Mat(Accent, 0.4f));
                b.transform.SetParent(root, true);
            }

            // Zebra crossing from the sidewalk into the lot.
            for (int i = 0; i < 7; i++)
                Piece(white, new Vector3(-2.4f + i * 0.8f, 0.006f, -19.7f), new Vector3(0.4f, 0.01f, 2.2f));

            // Parking bays: two rows of white lines, with a driving lane left clear in the middle.
            foreach (float rowZ in new[] { -24.5f, -32.5f })
            {
                for (float x = -27f; x <= 27.1f; x += 3f)
                {
                    if (Mathf.Abs(x) < 3.5f) continue;
                    Piece(white, new Vector3(x, 0.006f, rowZ), new Vector3(0.12f, 0.01f, 5f));
                }
                Piece(white, new Vector3(-15.25f, 0.006f, rowZ - 2.5f), new Vector3(23.5f, 0.01f, 0.12f));
                Piece(white, new Vector3(15.25f, 0.006f, rowZ - 2.5f), new Vector3(23.5f, 0.01f, 0.12f));
            }

            // Street lamps (warm sodium light). One of them flickers.
            Lamp(new Vector3(-15f, 0f, -21f), false);
            Lamp(new Vector3(15f, 0f, -21f), true);
            Lamp(new Vector3(-15f, 0f, -33f), false);
            Lamp(new Vector3(15f, 0f, -33f), false);

            // Make the two old box cars look like cars, and add two more wrecks (cover outside).
            DetailOldCar(GameObject.Find("Abandoned Car 1"));
            DetailOldCar(GameObject.Find("Abandoned Car 2"));
            Car(new Vector3(11f, 0f, -20.5f), 35f, new Color(0.45f, 0.12f, 0.1f));
            Car(new Vector3(-17f, 0f, -31f), 85f, new Color(0.75f, 0.76f, 0.74f));

            // Cart corral.
            foreach (float x in new[] { 5.0f, 6.4f })
            {
                Piece(Mat(Steel, 0.6f), new Vector3(x, 0.95f, -31f), new Vector3(0.05f, 0.05f, 3.2f));
                foreach (float z in new[] { -32.5f, -29.5f })
                    Piece(Mat(Steel, 0.6f), new Vector3(x, 0.475f, z), new Vector3(0.05f, 0.95f, 0.05f));
            }

            // The dumpster used to block most of the loading bay: replace it with one off to the side.
            var oldDumpster = GameObject.Find("Dumpster");
            if (oldDumpster != null) oldDumpster.SetActive(false);
            Solid("Dumpster", new Vector3(2.4f, 0.75f, 17.6f), new Vector3(3f, 1.5f, 1.5f), Mat(new Color(0.1f, 0.28f, 0.18f), 0.35f));
            Piece(dark, new Vector3(2.4f, 1.53f, 17.55f), new Vector3(3.05f, 0.06f, 1.6f), Quaternion.Euler(-4f, 0f, 0f));

            // Pallets outside the loading bay.
            var pallet = Mat(new Color(0.5f, 0.38f, 0.22f), 0.15f);
            Solid("Pallet", new Vector3(-0.8f, 0.07f, 16.8f), new Vector3(1.2f, 0.14f, 1.0f), pallet);
            Solid("Pallet Boxes", new Vector3(-0.8f, 0.5f, 16.8f), new Vector3(1.1f, 0.72f, 0.9f), Mat(CardboardPalette[1], 0.2f));
        }

        // ───────────────────────── Sky & edge of the map ─────────────────────────

        /// The camera used Unity's default daytime skybox, which the post-processing blew out to white.
        /// Now the "sky" is a dark night colour that matches the fog, so far-away things fade into darkness.
        static void NightSky()
        {
            Color night = new Color(0.035f, 0.04f, 0.055f);
            RenderSettings.skybox = null;
            RenderSettings.fogColor = night;
            foreach (var cam in Camera.allCameras)
            {
                if (!cam.CompareTag("MainCamera")) continue;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = night;
            }
        }

        /// The playable area ends at a 3.2 m concrete wall with barbed wire (you can't jump over it).
        /// Behind it: an empty road and dark city blocks with a few lit windows, disappearing into the fog.
        ///
        ///   z=38  ┌──────────────── wall ────────────────┐
        ///         │            store (x −20..20)          │
        ///         │                                       │
        ///   z=−52 └────────[ROAD CLOSED gate]─────────────┘   ← road and buildings beyond
        ///       x=−46                                   x=46
        static void WorldEdge()
        {
            const float minX = -46f, maxX = 46f, minZ = -52f, maxZ = 38f, height = 3.2f, thick = 0.4f;
            var concrete = Mat(new Color(0.30f, 0.29f, 0.27f), 0.1f);
            var pillar = Mat(new Color(0.24f, 0.23f, 0.22f), 0.1f);
            var wire = Mat(new Color(0.1f, 0.1f, 0.1f), 0.5f);

            // The four walls (solid: they stop you AND the zombies).
            float w = maxX - minX, d = maxZ - minZ;
            Solid("Boundary Wall Front", new Vector3(0f, height * 0.5f, minZ), new Vector3(w + thick, height, thick), concrete);
            Solid("Boundary Wall Back", new Vector3(0f, height * 0.5f, maxZ), new Vector3(w + thick, height, thick), concrete);
            Solid("Boundary Wall Left", new Vector3(minX, height * 0.5f, (minZ + maxZ) * 0.5f), new Vector3(thick, height, d), concrete);
            Solid("Boundary Wall Right", new Vector3(maxX, height * 0.5f, (minZ + maxZ) * 0.5f), new Vector3(thick, height, d), concrete);

            // Pillars every 6 m and barbed wire on top.
            void Run(Vector3 a, Vector3 b)
            {
                Vector3 dir = (b - a).normalized;
                float len = Vector3.Distance(a, b);
                Quaternion rot = Quaternion.LookRotation(dir);
                for (float t = 0f; t <= len + 0.01f; t += 6f)
                {
                    Vector3 p = a + dir * t;
                    Piece(pillar, p + Vector3.up * (height + 0.1f) * 0.5f, new Vector3(0.6f, height + 0.1f, 0.6f));
                    Piece(wire, p + Vector3.up * (height + 0.35f), new Vector3(0.05f, 0.6f, 0.05f));
                }
                foreach (float y in new[] { height + 0.2f, height + 0.4f, height + 0.6f })
                    Piece(wire, (a + b) * 0.5f + Vector3.up * y, new Vector3(0.02f, 0.02f, len), rot);
            }
            Run(new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, minZ));
            Run(new Vector3(minX, 0f, maxZ), new Vector3(maxX, 0f, maxZ));
            Run(new Vector3(minX, 0f, minZ), new Vector3(minX, 0f, maxZ));
            Run(new Vector3(maxX, 0f, minZ), new Vector3(maxX, 0f, maxZ));

            // A chained-shut gate in the front wall, with a bus jammed against it on the other side.
            var gateMetal = Mat(new Color(0.16f, 0.17f, 0.18f), 0.5f);
            Piece(gateMetal, new Vector3(0f, 1.5f, minZ + 0.25f), new Vector3(8f, 3f, 0.08f));
            for (float x = -3.6f; x <= 3.65f; x += 0.4f) Piece(gateMetal, new Vector3(x, 1.5f, minZ + 0.3f), new Vector3(0.05f, 3f, 0.05f));
            Piece(Mat(Accent, 0.3f), new Vector3(0f, 1.2f, minZ + 0.33f), new Vector3(0.9f, 0.08f, 0.04f));
            Sign("ROAD CLOSED", "QUARANTINE ZONE", new Vector3(0f, 2.1f, minZ + 0.36f), 180f, new Vector2(2.6f, 0.8f), new Color(0.75f, 0.1f, 0.08f), Color.white, 0.36f);
            Piece(Mat(new Color(0.55f, 0.45f, 0.12f), 0.3f), new Vector3(0.5f, 1.6f, minZ - 3.2f), new Vector3(11f, 3f, 2.6f), Quaternion.Euler(0f, 4f, 0f));

            // Graffiti on the inside of the wall.
            Color paint = new Color(0.55f, 0.06f, 0.05f);
            LevelKit.Label("NO WAY OUT", root, new Vector3(-24f, 1.7f, minZ + 0.22f), Quaternion.Euler(0f, 180f, 0f), 1.1f, paint);
            LevelKit.Label("THEY HEAR YOU", root, new Vector3(minX + 0.22f, 1.7f, -14f), Quaternion.Euler(0f, -90f, 0f), 1.1f, paint);
            LevelKit.Label("STAY QUIET", root, new Vector3(maxX - 0.22f, 1.7f, -20f), Quaternion.Euler(0f, 90f, 0f), 1.1f, paint);
            LevelKit.Label("DON'T OPEN THE BACK", root, new Vector3(-20f, 1.7f, maxZ - 0.22f), Quaternion.identity, 1.0f, paint);

            // Outside the wall: dark ground, an empty road, and city blocks.
            Piece(Mat(new Color(0.045f, 0.045f, 0.05f), 0.1f), new Vector3(0f, -0.03f, 0f), new Vector3(700f, 0.02f, 700f));
            Piece(Mat(new Color(0.07f, 0.07f, 0.075f), 0.3f), new Vector3(0f, 0.004f, minZ - 7f), new Vector3(700f, 0.006f, 9f));
            var lane = Mat(new Color(0.6f, 0.55f, 0.3f), 0.2f);
            for (float x = -200f; x < 200f; x += 6f) Piece(lane, new Vector3(x, 0.009f, minZ - 7f), new Vector3(3f, 0.004f, 0.15f));

            var lit = new[] { Mat(new Color(1f, 0.78f, 0.45f), 0.2f, 1.6f), Mat(new Color(0.6f, 0.75f, 1f), 0.2f, 1.2f) };
            int placed = 0;
            for (int tries = 0; tries < 400 && placed < 70; tries++)
            {
                float angle = R(0f, Mathf.PI * 2f), dist = R(62f, 150f);
                float cx = Mathf.Cos(angle) * dist, cz = -7f + Mathf.Sin(angle) * dist;
                float bw = R(8f, 22f), bd = R(8f, 22f), bh = R(6f, 38f);
                // Keep them outside the wall and off the road.
                if (cx + bw * 0.5f > minX - 8f && cx - bw * 0.5f < maxX + 8f && cz + bd * 0.5f > minZ - 13f && cz - bd * 0.5f < maxZ + 8f) continue;
                placed++;
                float shade = R(0.06f, 0.12f);
                Piece(Mat(new Color(shade, shade, shade * 1.08f), 0.1f), new Vector3(cx, bh * 0.5f, cz), new Vector3(bw, bh, bd));

                // A few lit windows on the side that faces the store.
                bool faceX = Mathf.Abs(cx) > Mathf.Abs(cz + 7f);
                float faceCoord = faceX ? cx - Mathf.Sign(cx) * (bw * 0.5f + 0.03f) : cz - Mathf.Sign(cz + 7f) * (bd * 0.5f + 0.03f);
                float span = faceX ? bd : bw;
                for (float y = 2f; y < bh - 1.5f; y += 3.5f)
                    for (float s2 = -span * 0.5f + 1.5f; s2 < span * 0.5f - 1f; s2 += 3f)
                    {
                        if (!Chance(0.07f)) continue;
                        Vector3 pos = faceX ? new Vector3(faceCoord, y, cz + s2) : new Vector3(cx + s2, y, faceCoord);
                        Vector3 size = faceX ? new Vector3(0.05f, 1.3f, 1f) : new Vector3(1f, 1.3f, 0.05f);
                        Piece(lit[rng.Next(lit.Length)], pos, size);
                    }
            }
        }

        static void Lamp(Vector3 basePos, bool flicker)
        {
            var dark = Mat(new Color(0.18f, 0.19f, 0.2f), 0.5f);
            var pole = Cylinder("Street Lamp", basePos + Vector3.up * 3f, new Vector3(0.18f, 3f, 0.18f), dark); // keeps its collider
            pole.transform.SetParent(root, true);
            Piece(dark, basePos + new Vector3(0f, 5.95f, 0.6f), new Vector3(0.08f, 0.08f, 1.25f));
            Piece(Mat(Sodium, 0.3f, 2f), basePos + new Vector3(0f, 5.85f, 1.2f), new Vector3(0.55f, 0.12f, 0.3f));
            var light = AddLight("Street Lamp Light", basePos + new Vector3(0f, 5.6f, 1.2f), Sodium, 4f, 14f);
            if (flicker) light.gameObject.AddComponent<LightFlicker>();
        }

        /// Adds windows, wheels, bumpers and lights to one of the plain box cars (2 × 1.5 × 4.5 m).
        static void DetailOldCar(GameObject car)
        {
            if (car == null) return;
            Transform t = car.transform;
            var glass = Mat(new Color(0.08f, 0.1f, 0.12f), 0.9f);
            var tyre = Mat(new Color(0.04f, 0.04f, 0.04f), 0.2f);
            var bumper = Mat(new Color(0.15f, 0.15f, 0.16f), 0.4f);
            Vector3 P(float x, float y, float z) => t.position + t.rotation * new Vector3(x, y, z);

            Piece(glass, P(0f, 0.45f, -0.2f), new Vector3(2.02f, 0.45f, 2.4f), t.rotation);
            Piece(glass, P(0f, 0.45f, 0f), new Vector3(1.8f, 0.42f, 4.52f), t.rotation);
            foreach (float z in new[] { -2.27f, 2.27f })
                Piece(bumper, P(0f, -0.4f, z), new Vector3(2.05f, 0.18f, 0.08f), t.rotation);
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1.45f, 1.45f })
                    Piece(tyre, P(x, -0.47f, z), new Vector3(0.12f, 0.56f, 0.65f), t.rotation);
            foreach (float x in new[] { -0.65f, 0.65f })
            {
                Piece(Mat(new Color(0.9f, 0.9f, 0.8f), 0.8f), P(x, -0.1f, 2.26f), new Vector3(0.35f, 0.16f, 0.02f), t.rotation);
                Piece(Mat(new Color(0.5f, 0.05f, 0.05f), 0.6f), P(x, -0.1f, -2.26f), new Vector3(0.35f, 0.14f, 0.02f), t.rotation);
            }
        }

        /// A new wrecked car: solid body + cabin (real cover), plus wheels and lights.
        static void Car(Vector3 pos, float yaw, Color paint)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 P(float x, float y, float z) => pos + rot * new Vector3(x, y, z);
            Solid("Wrecked Car", P(0f, 0.65f, 0f), new Vector3(1.9f, 0.75f, 4.4f), Mat(paint, 0.5f), rot);
            Solid("Wrecked Car Cabin", P(0f, 1.32f, -0.25f), new Vector3(1.7f, 0.6f, 2.3f), Mat(new Color(0.08f, 0.1f, 0.12f), 0.9f), rot);
            var tyre = Mat(new Color(0.04f, 0.04f, 0.04f), 0.2f);
            foreach (float x in new[] { -0.92f, 0.92f })
                foreach (float z in new[] { -1.4f, 1.4f })
                    Piece(tyre, P(x, 0.32f, z), new Vector3(0.14f, 0.6f, 0.6f), rot);
            foreach (float x in new[] { -0.6f, 0.6f })
            {
                Piece(Mat(new Color(0.9f, 0.9f, 0.8f), 0.8f), P(x, 0.75f, 2.21f), new Vector3(0.35f, 0.15f, 0.02f), rot);
                Piece(Mat(new Color(0.5f, 0.05f, 0.05f), 0.6f), P(x, 0.75f, -2.21f), new Vector3(0.35f, 0.13f, 0.02f), rot);
            }
        }

        // ───────────────────────── Building blocks ─────────────────────────

        /// A sign: a board with text. yaw decides which way it faces (see Signs()).
        static void Sign(string text, string sub, Vector3 center, float yaw, Vector2 size, Color board, Color textColor,
                         float textHeight, bool twoSided = false, float glow = 0f)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Piece(Mat(board, 0.3f, glow), center, new Vector3(size.x, size.y, 0.05f), rot);

            Vector3 viewerSide = rot * Vector3.back; // the side people read it from
            float mainY = sub != null ? size.y * 0.14f : 0f;
            void Write(Vector3 offset, Quaternion facing)
            {
                LevelKit.Label(text, root, center + offset + Vector3.up * mainY, facing, textHeight, textColor);
                if (sub != null)
                    LevelKit.Label(sub, root, center + offset - Vector3.up * size.y * 0.27f, facing, textHeight * 0.45f, textColor);
            }
            Write(viewerSide * 0.035f, rot);
            if (twoSided) Write(-viewerSide * 0.035f, rot * Quaternion.Euler(0f, 180f, 0f));
        }

        static Light AddLight(string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        /// A box WITH a collider (you and zombies bump into it). Size in meters.
        static GameObject Solid(string name, Vector3 center, Vector3 size, Material mat, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(center, rotation ?? Quaternion.identity);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Cylinder(string name, Vector3 center, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.position = center;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void CartPart(Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            LevelKit.RemoveCollider(go);
            go.name = "Part";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// A decoration with NO collider. It's not created right away: it's added to its colour's batch.
        static void Piece(Material mat, Vector3 center, Vector3 size) => Piece(mat, center, size, Quaternion.identity);

        static void Piece(Material mat, Vector3 center, Vector3 size, Quaternion rotation)
        {
            if (!batches.TryGetValue(mat, out var list)) batches[mat] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(center, rotation, size) });
        }

        /// Glues every batch into a few big meshes (max 2000 boxes each).
        static void FlushBatches()
        {
            int n = 0;
            foreach (var kv in batches)
            {
                var list = kv.Value;
                for (int start = 0; start < list.Count; start += 2000)
                {
                    int count = Mathf.Min(2000, list.Count - start);
                    var mesh = new Mesh { name = "Makeover Batch " + n, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(list.GetRange(start, count).ToArray(), true, true);
                    mesh.RecalculateBounds();

                    var go = new GameObject("Detail Batch " + (++n));
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = kv.Key;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            batches.Clear();
        }

        /// One shared material per colour (copied from the store floor material, so it's the same URP shader).
        static Material Mat(Color color, float smoothness = 0.25f, float emission = 0f)
        {
            string key = $"{color.r:F3}_{color.g:F3}_{color.b:F3}_{smoothness:F2}_{emission:F2}";
            if (materials.TryGetValue(key, out var m)) return m;

            m = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "Makeover " + key;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_EmissionColor"))
            {
                if (emission > 0f)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", color * emission);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                else
                {
                    m.DisableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                }
            }
            materials[key] = m;
            return m;
        }

        static Mesh GrabCubeMesh()
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temp.SetActive(false); // switched off so it can't end up in the zombie floor map this frame
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(temp);
            return mesh;
        }

        static bool NearLoot(Vector3 p, float radius)
        {
            foreach (var l in lootSpots)
            {
                Vector3 d = l - p;
                if (Mathf.Abs(d.y) < 0.5f && d.x * d.x + d.z * d.z < radius * radius) return true;
            }
            return false;
        }

        static float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        static bool Chance(float p) => rng.NextDouble() < p;
        static Color Pick(Color[] palette) => palette[rng.Next(palette.Length)];
        static Color Dim(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
    }
}
