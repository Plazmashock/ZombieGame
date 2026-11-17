using System.Collections.Generic;
using UnityEngine;

namespace LastCheckout.World
{
    /// <summary>The kinds of surface we can paint. Each one gets a colour texture + a bumpiness (normal) map.</summary>
    public enum Surface
    {
        FloorTiles,     // store floor: 1 m tiles, light/dark checker, grout lines, grime
        PaintedBlock,   // walls: painted cinder blocks with mortar lines and stains
        CeilingTiles,   // ceiling: 60 cm acoustic tiles in a metal grid, water stains
        Asphalt,        // parking lot: speckled stones, cracks, oil stains
        Concrete,       // sidewalk, roof: blotchy grey with small pores
        BrushedMetal,   // dark metal, shelf boards: fine streaks, a little rust
        DiamondPlate,   // stairs, turret bases: raised diamond pattern
        Wood,           // crates, desks: planks with grain
        PaintedMetal,   // shelves, checkouts, cars: smooth paint with scratches
        Skin,           // zombies: mottled skin, dark veins, bruises
        Fabric,         // zombie clothes: weave, blood stains, holes
    }

    /// <summary>
    /// Makes textures from MATH when the game starts, so there are no image files to import.
    ///
    ///   noise (random smooth blobs) ──┐
    ///   patterns (tiles, bricks...) ──┼──► colour texture  (what you see)
    ///                                 └──► height ──► normal map (fake bumps that catch the light)
    ///
    /// All textures TILE seamlessly: the right edge matches the left edge, the top matches the bottom.
    /// That's why the noise below "wraps around" (period).
    /// Each texture is made once, the first time something asks for it, then reused.
    /// </summary>
    public static class ProceduralTextures
    {
        public struct Set
        {
            public Texture2D albedo;     // colour
            public Texture2D normal;     // bumps
            public float metersPerTile;  // how many meters of wall/floor one copy of the texture covers
            public float smoothness;     // 0 = matte, 1 = mirror
        }

        static readonly Dictionary<Surface, Set> cache = new Dictionary<Surface, Set>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        public static Set Get(Surface s)
        {
            if (cache.TryGetValue(s, out var set) && set.albedo != null) return set;
            set = Generate(s);
            cache[s] = set;
            return set;
        }

        // ───────────────────────── Generation ─────────────────────────

        static Set Generate(Surface s)
        {
            int size; float mpt, bump, smooth;
            switch (s)
            {
                case Surface.FloorTiles:   size = 512; mpt = 2f;   bump = 3f;   smooth = 0.45f; break;
                case Surface.PaintedBlock: size = 512; mpt = 1.6f; bump = 4f;   smooth = 0.15f; break;
                case Surface.CeilingTiles: size = 256; mpt = 1.2f; bump = 3f;   smooth = 0.05f; break;
                case Surface.Asphalt:      size = 512; mpt = 4f;   bump = 5f;   smooth = 0.12f; break;
                case Surface.Concrete:     size = 256; mpt = 3f;   bump = 3f;   smooth = 0.1f;  break;
                case Surface.BrushedMetal: size = 256; mpt = 1f;   bump = 1.5f; smooth = 0.55f; break;
                case Surface.DiamondPlate: size = 256; mpt = 0.5f; bump = 6f;   smooth = 0.5f;  break;
                case Surface.Wood:         size = 256; mpt = 1f;   bump = 3f;   smooth = 0.2f;  break;
                case Surface.PaintedMetal: size = 256; mpt = 1f;   bump = 1.5f; smooth = 0.4f;  break;
                case Surface.Skin:         size = 256; mpt = 1f;   bump = 2.5f; smooth = 0.3f;  break;
                default:                   size = 256; mpt = 1f;   bump = 2f;   smooth = 0.1f;  break; // Fabric
            }

            var colors = new Color32[size * size];
            var height = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // u, v = where we are in the texture, 0..1 (pixel centres)
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    Pixel(s, u, v, out Color c, out float h);
                    colors[y * size + x] = c;
                    height[y * size + x] = h;
                }

            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = s + " (colour)" };
            albedo.wrapMode = TextureWrapMode.Repeat;
            albedo.filterMode = FilterMode.Trilinear;
            albedo.anisoLevel = 4;
            albedo.SetPixels32(colors);
            albedo.Apply(true, true); // build mipmaps, then free the CPU copy

            var normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = s + " (normal)" };
            normal.wrapMode = TextureWrapMode.Repeat;
            normal.filterMode = FilterMode.Trilinear;
            normal.anisoLevel = 4;
            normal.SetPixels32(HeightToNormal(height, size, bump));
            normal.Apply(true, true);

            return new Set { albedo = albedo, normal = normal, metersPerTile = mpt, smoothness = smooth };
        }

        /// The recipe for every surface: given a spot (u, v), return its colour and its height (0 = low, 1 = high).
        static void Pixel(Surface s, float u, float v, out Color c, out float h)
        {
            switch (s)
            {
                case Surface.FloorTiles:
                {
                    // 2 × 2 tiles of 1 m. Every other tile is darker (checkerboard).
                    int ix = (int)(u * 2f), iy = (int)(v * 2f);
                    float fu = u * 2f - ix, fv = v * 2f - iy;
                    float edge = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); // 0 at the tile's edge
                    float tile = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.006f, 0.016f, edge)); // 0 = grout, 1 = tile
                    float checker = ((ix + iy) & 1) == 1 ? 0.76f : 1f;
                    float tileVar = (Hash(ix, iy, 7) - 0.5f) * 0.06f;
                    float speck = Fbm(u, v, 96, 2, 11);                     // terrazzo chips
                    float chips = speck > 0.66f ? -0.08f : speck < 0.3f ? 0.05f : 0f;
                    float grime = Fbm(u, v, 4, 4, 23);                      // big dirty patches
                    float scuff = Mathf.Clamp01((Fbm(u * 1f, v * 1f, 16, 3, 31) - 0.62f) * 4f);
                    float shade = checker * (0.95f + tileVar + chips) * (1f - 0.22f * grime) * (1f - 0.18f * scuff);
                    Color tileCol = new Color(1f, 0.99f, 0.95f) * shade;
                    Color grout = new Color(0.32f, 0.31f, 0.29f) * (0.8f + 0.2f * grime);
                    c = Color.Lerp(grout, tileCol, tile);
                    h = tile * 0.9f + chips * 0.3f;
                    break;
                }
                case Surface.PaintedBlock:
                {
                    // 1.6 m covers 2 blocks across and 4 rows (blocks are 80 × 40 cm), staggered like real walls.
                    int row = (int)(v * 4f);
                    float bx = u * 2f + ((row & 1) == 1 ? 0.5f : 0f);
                    int col = Mathf.FloorToInt(bx);
                    float fu = bx - col, fv = v * 4f - row;
                    float edgeM = Mathf.Min(Mathf.Min(fu, 1f - fu) * 0.8f, Mathf.Min(fv, 1f - fv) * 0.4f); // meters to the mortar
                    float block = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.004f, 0.012f, edgeM));
                    float pits = Fbm(u, v, 128, 2, 41);
                    float pit = pits > 0.7f ? (pits - 0.7f) * 2.5f : 0f;
                    float paint = 0.93f + 0.07f * Fbm(u, v, 8, 3, 43) + (Hash(col & 1, row & 3, 3) - 0.5f) * 0.03f;
                    float stain = Mathf.Clamp01((Fbm(u, v, 3, 4, 47) - 0.55f) * 2.5f);
                    Color wall = new Color(1f, 0.985f, 0.95f) * paint * (1f - pit * 0.35f);
                    wall = Color.Lerp(wall, new Color(0.62f, 0.55f, 0.43f), stain * 0.45f);
                    Color mortar = new Color(0.78f, 0.76f, 0.72f);
                    c = Color.Lerp(mortar, wall, block);
                    h = block * 0.8f - pit * 0.3f;
                    break;
                }
                case Surface.CeilingTiles:
                {
                    // 2 × 2 acoustic tiles (60 cm) held by a thin metal T-bar grid.
                    float fu = Frac(u * 2f), fv = Frac(v * 2f);
                    float edgeM = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)) * 0.6f;
                    float tile = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.008f, 0.014f, edgeM));
                    float holes = Fbm(u, v, 128, 1, 51);
                    float hole = holes > 0.72f ? 1f : 0f;
                    float fiss = Fbm(u, v, 48, 2, 53);
                    float stain = Mathf.Clamp01((Fbm(u, v, 2, 4, 57) - 0.58f) * 3f);
                    Color t = new Color(0.97f, 0.96f, 0.93f) * (0.92f + 0.08f * fiss) * (1f - hole * 0.25f);
                    t = Color.Lerp(t, new Color(0.72f, 0.6f, 0.38f), stain * 0.6f); // old water leak
                    Color bar = new Color(0.86f, 0.86f, 0.86f);
                    c = Color.Lerp(bar, t, tile);
                    h = tile * 0.6f - hole * 0.15f;
                    break;
                }
                case Surface.Asphalt:
                {
                    float stones = Fbm(u, v, 160, 2, 61);
                    float big = Fbm(u, v, 40, 2, 63);
                    float lightStone = stones > 0.68f ? 0.35f : 0f;
                    float darkStone = stones < 0.3f ? -0.18f : 0f;
                    float ridge = 1f - Mathf.Abs(Fbm(u, v, 6, 5, 67) * 2f - 1f);     // thin bright lines → cracks
                    float crack = Mathf.Clamp01((ridge - 0.94f) * 25f);
                    float oil = Mathf.Clamp01((Fbm(u, v, 3, 3, 71) - 0.6f) * 3f);
                    float shade = (0.85f + 0.15f * big + lightStone + darkStone) * (1f - 0.75f * crack) * (1f - 0.35f * oil);
                    c = new Color(1f, 1f, 1.02f) * shade;
                    h = stones * 0.5f + big * 0.3f - crack * 0.8f;
                    break;
                }
                case Surface.Concrete:
                {
                    float blot = Fbm(u, v, 6, 4, 81);
                    float fine = Fbm(u, v, 96, 2, 83);
                    float pore = fine > 0.74f ? 1f : 0f;
                    float seam = Mathf.Min(Frac(u), 1f - Frac(u)) < 0.004f ? 1f : 0f; // one expansion joint per tile
                    float shade = (0.86f + 0.14f * blot) * (1f - 0.3f * pore) * (1f - 0.45f * seam);
                    c = new Color(1f, 0.99f, 0.97f) * shade;
                    h = blot * 0.3f - pore * 0.3f - seam * 0.6f;
                    break;
                }
                case Surface.BrushedMetal:
                {
                    float streak = Fbm(u, v, 2, 1, 91) * 0.5f + ValueNoise(u * 4f, v * 256f, 4, 256, 93) * 0.5f;
                    float rust = Mathf.Clamp01((Fbm(u, v, 4, 4, 97) - 0.66f) * 4f);
                    Color metal = new Color(0.95f, 0.96f, 0.98f) * (0.82f + 0.18f * streak);
                    c = Color.Lerp(metal, new Color(0.55f, 0.32f, 0.16f), rust * 0.7f);
                    h = streak * 0.3f + rust * 0.4f;
                    break;
                }
                case Surface.DiamondPlate:
                {
                    // 8 × 8 cells, each with one raised "lozenge", tilted +45° / −45° in turn.
                    int cx = (int)(u * 8f), cy = (int)(v * 8f);
                    float lx = u * 8f - cx - 0.5f, ly = v * 8f - cy - 0.5f;
                    float sgn = ((cx + cy) & 1) == 0 ? 1f : -1f;
                    float rx = (lx + sgn * ly) * 0.7071f, ry = (-sgn * lx + ly) * 0.7071f;
                    float d = (rx / 0.36f) * (rx / 0.36f) + (ry / 0.09f) * (ry / 0.09f);
                    float bump = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.45f, d));
                    float dirt = Fbm(u, v, 4, 3, 101);
                    c = new Color(0.9f, 0.92f, 0.95f) * (0.72f + 0.2f * bump) * (1f - 0.25f * dirt);
                    h = bump;
                    break;
                }
                case Surface.Wood:
                {
                    // 6 planks per meter, running left-right, each with its own grain and shade.
                    int plank = (int)(v * 6f);
                    float fv = v * 6f - plank;
                    float gap = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.06f, Mathf.Min(fv, 1f - fv)));
                    float wobble = Fbm(u, v, 3, 3, 111 + plank) * 6f;
                    float rings = 0.5f + 0.5f * Mathf.Sin((fv * 9f + wobble + Hash(plank, 0, 113) * 10f) * Mathf.PI);
                    float fibers = ValueNoise(u * 64f, v * 6f * 2f, 64, 12, 117 + plank);
                    float knot = Mathf.Clamp01((Fbm(u, v, 12, 2, 119) - 0.75f) * 6f);
                    float shade = (0.82f + 0.12f * rings + 0.06f * fibers + (Hash(plank, 1, 121) - 0.5f) * 0.14f) * (1f - 0.35f * knot);
                    Color wood = new Color(1f, 0.95f, 0.88f) * shade;
                    c = Color.Lerp(new Color(0.18f, 0.14f, 0.1f), wood, gap);
                    h = gap * (0.7f + 0.2f * rings);
                    break;
                }
                case Surface.PaintedMetal:
                {
                    float peel = Fbm(u, v, 32, 2, 131);                     // orange-peel paint texture
                    float scratchN = 1f - Mathf.Abs(Fbm(u * 1f, v * 1f, 10, 3, 133) * 2f - 1f);
                    float scratch = Mathf.Clamp01((scratchN - 0.95f) * 30f);
                    float grime = Fbm(u, v, 3, 3, 137);
                    Color paint = Color.white * (0.92f + 0.08f * peel) * (1f - 0.2f * grime);
                    c = Color.Lerp(paint, new Color(0.62f, 0.6f, 0.58f), scratch); // scratches show bare grey metal
                    h = peel * 0.15f - scratch * 0.5f;
                    break;
                }
                case Surface.Skin:
                {
                    float mottle = Fbm(u, v, 6, 4, 141);
                    float vein = 1f - Mathf.Abs(Fbm(u, v, 5, 4, 143) * 2f - 1f);
                    float veins = Mathf.Clamp01((vein - 0.9f) * 10f);
                    float bruise = Mathf.Clamp01((Fbm(u, v, 3, 3, 147) - 0.6f) * 3f);
                    float sore = Mathf.Clamp01((Fbm(u, v, 24, 2, 149) - 0.78f) * 6f);
                    Color skin = Color.white * (0.82f + 0.2f * mottle);
                    skin = Color.Lerp(skin, new Color(0.55f, 0.45f, 0.62f), bruise * 0.5f);
                    skin = Color.Lerp(skin, new Color(0.35f, 0.25f, 0.4f), veins * 0.55f);
                    skin = Color.Lerp(skin, new Color(0.45f, 0.06f, 0.05f), sore * 0.8f);
                    c = skin;
                    h = mottle * 0.3f + veins * 0.3f - sore * 0.4f;
                    break;
                }
                default: // Fabric
                {
                    float weaveU = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 96f);
                    float weaveV = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 96f);
                    float weave = Mathf.Max(weaveU, weaveV);
                    float dirt = Fbm(u, v, 4, 4, 151);
                    float blood = Mathf.Clamp01((Fbm(u, v, 5, 4, 153) - 0.6f) * 4f);
                    float hole = Fbm(u, v, 7, 3, 157) > 0.78f ? 1f : 0f;
                    Color cloth = Color.white * (0.85f + 0.12f * weave) * (1f - 0.3f * dirt);
                    cloth = Color.Lerp(cloth, new Color(0.35f, 0.03f, 0.03f), blood * 0.85f);
                    c = Color.Lerp(cloth, new Color(0.1f, 0.08f, 0.08f), hole);
                    h = weave * 0.3f - hole * 0.6f;
                    break;
                }
            }
            c.a = 1f;
        }

        // ───────────────────────── Height → normal map ─────────────────────────

        /// Looks at how steep the height changes left/right and up/down at every pixel.
        /// Steep = the surface tilts there = the light catches it differently.
        static Color32[] HeightToNormal(float[] h, int size, float strength)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int xl = (x - 1 + size) % size, xr = (x + 1) % size; // wrap around = seamless
                    int yd = (y - 1 + size) % size, yu = (y + 1) % size;
                    float dx = (h[y * size + xr] - h[y * size + xl]) * strength;
                    float dy = (h[yu * size + x] - h[yd * size + x]) * strength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    px[y * size + x] = new Color32(
                        (byte)((n.x * 0.5f + 0.5f) * 255f),
                        (byte)((n.y * 0.5f + 0.5f) * 255f),
                        (byte)((n.z * 0.5f + 0.5f) * 255f),
                        255);
                }
            return px;
        }

        // ───────────────────────── Noise ─────────────────────────

        static float Frac(float x) => x - Mathf.Floor(x);

        /// A random number 0..1 for a grid point. Same input = same output (so the texture is the same every run).
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static int Wrap(int i, int period) { int m = i % period; return m < 0 ? m + period : m; }

        /// Smooth random blobs on a grid that repeats every 'period' cells (so it tiles).
        static float ValueNoise(float x, float y, int periodX, int periodY, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int ax = Wrap(x0, periodX), bx = Wrap(x0 + 1, periodX);
            int ay = Wrap(y0, periodY), by = Wrap(y0 + 1, periodY);
            float a = Hash(ax, ay, seed), b = Hash(bx, ay, seed), c = Hash(ax, by, seed), d = Hash(bx, by, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// "Fractal" noise: big blobs + smaller blobs + even smaller... (like clouds or dirt). Returns roughly 0..1.
        /// u, v are 0..1 across the texture; basePeriod = how many blobs fit across at the biggest size.
        static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int p = Mathf.Max(1, basePeriod);
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(u * p, v * p, p, p, seed + o * 1013);
                norm += amp;
                amp *= 0.5f;
                p *= 2;
            }
            return sum / norm;
        }
    }
}
