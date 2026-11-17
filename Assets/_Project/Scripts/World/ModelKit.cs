using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastCheckout.World
{
    /// <summary>
    /// Textured materials, shared by everyone asking for the same look.
    ///   SurfaceMaterials.Get(Surface.DiamondPlate, someColour)  → a steel-plate material tinted that colour
    ///   SurfaceMaterials.Textured(existingMaterial, Surface.Wood) → a copy of an existing material, now with wood texture
    /// The textures come from ProceduralTextures (made in code, no image files).
    /// </summary>
    public static class SurfaceMaterials
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static readonly Dictionary<(Material, Surface), Material> texturedCache = new Dictionary<(Material, Surface), Material>();
        static Material template;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); texturedCache.Clear(); template = null; }

        /// A fresh material of 'kind', tinted 'tint'. emission > 0 makes it glow.
        public static Material Get(Surface kind, Color tint, float emission = 0f)
        {
            string key = $"{kind}_{tint.r:F2}_{tint.g:F2}_{tint.b:F2}_{emission:F1}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;

            m = new Material(Template());
            m.name = "Surface " + key;
            m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            Dress(m, kind);
            if (emission > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", tint * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            cache[key] = m;
            return m;
        }

        /// A copy of 'original' (keeping its colour) with the texture of 'kind'. The original asset is NOT changed.
        public static Material Textured(Material original, Surface kind)
        {
            if (original == null) return Get(kind, Color.white);
            var key = (original, kind); // the material itself is the key (no IDs needed)
            if (texturedCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(original) { name = original.name + " (textured)" };
            Dress(m, kind);
            texturedCache[key] = m;
            return m;
        }

        static void Dress(Material m, Surface kind)
        {
            var set = ProceduralTextures.Get(kind);
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", set.albedo); m.SetTextureScale("_BaseMap", Vector2.one); }
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", set.albedo);
            if (m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", set.normal);
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", set.smoothness);
        }

        /// Any URP Lit material from the scene works as a starting point (we copy it, then change everything).
        static Material Template()
        {
            if (template != null) return template;
            template = Game.LevelKit.SceneMaterial("Store Floor");
            if (template == null) template = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            return template;
        }
    }

    /// <summary>
    /// Builds simple 3D models (boxes, cylinders) whose textures are sized in METERS.
    ///
    /// Why not Unity's own Cube? A Unity cube stretches its texture over each face, so a 40 m wall would show
    /// one giant brick. Here every face measures itself, so a 1 m brick stays 1 m, on any size of box,
    /// and neighbouring boxes line up seamlessly (the pattern is pinned to the world, not to the box).
    /// </summary>
    public static class ModelKit
    {
        // ───────────────────────── Ready-made objects ─────────────────────────

        /// A textured box. size and center are in meters (world space). collider = solid (you and zombies bump into it).
        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat,
                                     bool collider = true, Quaternion? rotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rotation ?? Quaternion.identity);
            var mb = new MeshBuilder();
            mb.AddBox(Vector3.zero, size, Quaternion.identity, go.transform.localToWorldMatrix, MetersPerTile(mat));
            Finish(go, mb, mat);
            if (collider) go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// A textured cylinder standing up (or rotated). radius / height in meters.
        public static GameObject Cylinder(string name, Transform parent, Vector3 center, float radius, float height, Material mat,
                                          int segments = 16, bool collider = false, Quaternion? rotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rotation ?? Quaternion.identity);
            var mb = new MeshBuilder();
            mb.AddCylinder(Vector3.zero, radius, height, Quaternion.identity, segments, MetersPerTile(mat));
            Finish(go, mb, mat);
            if (collider)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.radius = radius;
                cap.height = Mathf.Max(height, radius * 2f);
            }
            return go;
        }

        /// An invisible solid box (for ramps under stairs, etc.). Zombies can walk on it too.
        public static GameObject ColliderBox(string name, Transform parent, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rotation);
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        public static void Finish(GameObject go, MeshBuilder mb, Material mat, bool castShadows = true)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(go.name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        /// How many meters one copy of this material's texture covers (1 if it has no procedural texture).
        public static float MetersPerTile(Material mat)
        {
            if (mat == null) return 1f;
            foreach (Surface s in System.Enum.GetValues(typeof(Surface)))
                if (mat.name.Contains(s.ToString())) return ProceduralTextures.Get(s).metersPerTile;
            return 1f;
        }

        // ───────────────────────── Mesh builder ─────────────────────────

        /// <summary>
        /// Collects boxes and cylinders into ONE mesh (one draw call), e.g. a whole staircase or a coil of wire.
        /// Positions are given in the object's own space; 'world' tells it where that space sits in the world,
        /// so the texture can be pinned to world meters.
        /// </summary>
        public class MeshBuilder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();

            public int VertexCount => verts.Count;

            // The 6 faces of a box: (outward direction, "up" direction used for the texture)
            static readonly Vector3[] FaceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            static readonly Vector3[] FaceUp = { Vector3.up, Vector3.up, Vector3.forward, Vector3.forward, Vector3.up, Vector3.up };

            /// Adds a box. center/size/rotation are in the object's own space.
            /// world = the object's local→world matrix (for meter-sized, world-pinned textures).
            public void AddBox(Vector3 center, Vector3 size, Quaternion rotation, Matrix4x4 world, float metersPerTile)
            {
                Vector3 half = size * 0.5f;
                for (int f = 0; f < 6; f++)
                {
                    Vector3 n = FaceN[f], b = FaceUp[f];
                    Vector3 a = Vector3.Cross(n, b);   // "right" when looking at the face from outside
                    float hn = Mathf.Abs(Vector3.Dot(n, half)), ha = Mathf.Abs(Vector3.Dot(a, half)), hb = Mathf.Abs(Vector3.Dot(b, half));

                    // Corners: bottom-left, top-left, top-right, bottom-right (as seen from outside).
                    Vector3 c = n * hn;
                    Vector3[] corners = { c - a * ha - b * hb, c - a * ha + b * hb, c + a * ha + b * hb, c + a * ha - b * hb };

                    Vector3 nObj = rotation * n, aObj = rotation * a, bObj = rotation * b;
                    Vector3 aW = world.MultiplyVector(aObj).normalized, bW = world.MultiplyVector(bObj).normalized;

                    int start = verts.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 p = center + rotation * corners[k];
                        verts.Add(p);
                        normals.Add(nObj);
                        Vector3 pw = world.MultiplyPoint3x4(p);
                        uvs.Add(new Vector2(Vector3.Dot(pw, aW), Vector3.Dot(pw, bW)) / metersPerTile);
                    }
                    tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                    tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
                }
            }

            /// Adds a thin "stick" box running from point a to point b (wire, rails, struts).
            public void AddStick(Vector3 from, Vector3 to, float thickness, Matrix4x4 world, float metersPerTile)
            {
                Vector3 d = to - from;
                float len = d.magnitude;
                if (len < 0.0001f) return;
                Vector3 up = Mathf.Abs(Vector3.Dot(d / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                AddBox((from + to) * 0.5f, new Vector3(thickness, thickness, len), Quaternion.LookRotation(d, up), world, metersPerTile);
            }

            /// Adds a cylinder standing along its own Y axis.
            public void AddCylinder(Vector3 center, float radius, float height, Quaternion rotation, int segments, float metersPerTile)
            {
                float hh = height * 0.5f;
                float circumference = Mathf.PI * 2f * radius;

                // Sides (smooth): one column of vertices per step around.
                int sideStart = verts.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments, ang = t * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    foreach (float y in new[] { -hh, hh })
                    {
                        verts.Add(center + rotation * (n * radius + Vector3.up * y));
                        normals.Add(rotation * n);
                        uvs.Add(new Vector2(t * circumference, y + hh) / metersPerTile);
                    }
                }
                for (int i = 0; i < segments; i++)
                {
                    int bl = sideStart + i * 2, tl = bl + 1, br = bl + 2, tr = bl + 3;
                    tris.Add(bl); tris.Add(tl); tris.Add(tr);
                    tris.Add(bl); tris.Add(tr); tris.Add(br);
                }

                // Caps (flat): a fan of triangles around the middle.
                foreach (float sign in new[] { 1f, -1f })
                {
                    Vector3 capN = rotation * (Vector3.up * sign);
                    int mid = verts.Count;
                    verts.Add(center + rotation * (Vector3.up * hh * sign));
                    normals.Add(capN);
                    uvs.Add(Vector2.zero);
                    for (int i = 0; i <= segments; i++)
                    {
                        float ang = (float)i / segments * Mathf.PI * 2f;
                        Vector3 p = new Vector3(Mathf.Cos(ang) * radius, hh * sign, Mathf.Sin(ang) * radius);
                        verts.Add(center + rotation * p);
                        normals.Add(capN);
                        uvs.Add(new Vector2(p.x, p.z) / metersPerTile);
                    }
                    for (int i = 0; i < segments; i++)
                    {
                        int p0 = mid + 1 + i, p1 = mid + 2 + i;
                        if (sign > 0f) { tris.Add(mid); tris.Add(p1); tris.Add(p0); }
                        else { tris.Add(mid); tris.Add(p0); tris.Add(p1); }
                    }
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateTangents(); // needed for the bump (normal) maps
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
