using System.Collections.Generic;
using UnityEngine;

namespace LastCheckout.World
{
    /// <summary>
    /// Loads the downloaded 3D models (Quaternius, CC0) from Assets/_Project/Resources/Models and makes them
    /// fit where the old grey boxes were.
    ///
    ///   Spawn("Props/Crate")       → a copy of the model, with materials fixed for URP
    ///   Fit(model, space, box...)  → turned, scaled and moved so it fills that box
    ///   GunOrientation / MeleeOrientation → works out which end is the muzzle / the handle by measuring the mesh
    ///
    /// Missing model? Everything returns null and the game keeps its old box look (nothing breaks).
    /// </summary>
    public static class ModelLibrary
    {
        public enum FitBy { Inside, Height, LengthZ, LengthY }

        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, AnimationClip[]> clips = new Dictionary<string, AnimationClip[]>();
        static readonly Dictionary<Material, Material> fixedMats = new Dictionary<Material, Material>();
        static readonly HashSet<string> warned = new HashSet<string>();
        static Texture2D atlas;
        static bool atlasTried;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            prefabs.Clear(); clips.Clear(); fixedMats.Clear(); warned.Clear();
            atlas = null; atlasTried = false;
        }

        // ───────────────────────── Loading ─────────────────────────

        public static GameObject Load(string path)
        {
            if (prefabs.TryGetValue(path, out var p)) return p;
            p = Resources.Load<GameObject>("Models/" + path);
            if (p == null && warned.Add(path)) Debug.LogWarning($"[Models] Missing model 'Resources/Models/{path}' — keeping the old look for it.");
            prefabs[path] = p;
            return p;
        }

        /// <summary>All animation clips inside a model file (e.g. a zombie's Walk, Run, Death...).</summary>
        public static AnimationClip[] Clips(string path)
        {
            if (clips.TryGetValue(path, out var c)) return c;
            var list = new List<AnimationClip>();
            foreach (var clip in Resources.LoadAll<AnimationClip>("Models/" + path))
                if (clip != null && !clip.name.StartsWith("__preview")) list.Add(clip);
            c = list.ToArray();
            clips[path] = c;
            return c;
        }

        public static GameObject Spawn(string path, Transform parent = null)
        {
            var prefab = Load(path);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            FixMaterials(go);
            return go;
        }

        // ───────────────────────── Materials ─────────────────────────

        static Texture2D Atlas
        {
            get
            {
                if (!atlasTried) { atlasTried = true; atlas = Resources.Load<Texture2D>("Models/Zombie_Atlas"); }
                return atlas;
            }
        }

        /// Makes sure every material uses URP (no pink), and that the zombie-kit models get their colour atlas.
        public static void FixMaterials(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var f = Fix(mats[i]);
                    if (f != mats[i]) { mats[i] = f; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        /// <summary>
        /// For the zombie kit: any material that still has no colour texture gets the shared colour atlas
        /// (the zombie FBX materials aren't always named "...Atlas", which left zombies plain white/grey).
        /// </summary>
        public static void ForceAtlas(GameObject go)
        {
            if (Atlas == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    Texture tex = null;
                    if (m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
                    if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                    if (tex != null) continue;
                    if (!m.HasProperty("_BaseMap")) continue;
                    var n = new Material(m) { name = m.name + " (Atlas)" };
                    n.SetTexture("_BaseMap", Atlas);
                    n.SetColor("_BaseColor", Color.white);
                    if (n.HasProperty("_Smoothness")) n.SetFloat("_Smoothness", 0.2f);
                    mats[i] = n;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static Material Fix(Material m)
        {
            if (m == null) return null;
            if (fixedMats.TryGetValue(m, out var done)) return done;

            bool urp = m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline");
            Texture tex = null;
            if (m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
            if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
            bool wantsAtlas = tex == null && m.name.Contains("Atlas") && Atlas != null;

            Material n = m;
            if (!urp || wantsAtlas)
            {
                if (urp) n = new Material(m);
                else
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    var template = Game.LevelKit.SceneMaterial("Store Floor");
                    n = shader != null ? new Material(shader) : new Material(template);
                    Color c = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    n.SetColor("_BaseColor", c);
                    if (tex != null) n.SetTexture("_BaseMap", tex);
                }
                if (wantsAtlas)
                {
                    n.SetTexture("_BaseMap", Atlas);
                    n.SetColor("_BaseColor", Color.white);
                }
                if (n.HasProperty("_Smoothness")) n.SetFloat("_Smoothness", Mathf.Min(n.GetFloat("_Smoothness"), 0.35f));
                n.name = m.name + " (URP)";
            }
            fixedMats[m] = n;
            return n;
        }

        // ───────────────────────── Measuring ─────────────────────────

        /// The box around everything visible in 'go', measured in 'space' coordinates.
        public static Bounds BoundsIn(Transform space, GameObject go)
        {
            bool any = false;
            Bounds result = new Bounds();
            Matrix4x4 toSpace = space != null ? space.worldToLocalMatrix : Matrix4x4.identity;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) mesh = mf.sharedMesh; }
                if (mesh == null) continue;
                Matrix4x4 m = toSpace * r.transform.localToWorldMatrix;
                Bounds mb = mesh.bounds;
                Vector3 c = mb.center, e = mb.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }

        // ───────────────────────── Fitting ─────────────────────────

        /// <summary>
        /// Puts 'model' inside a new pivot under 'space' and fits it to 'target' (in space coordinates).
        ///   orient   : extra turn first (e.g. "muzzle forward")
        ///   autoYaw  : turn 90° if the model is long the other way (cars, crates)
        ///   by       : Inside = whole model fits in the box · Height / LengthZ / LengthY = match that one size
        ///   bottom   : stand it on the box's floor (true) or centre it (false)
        /// Returns the pivot.
        /// </summary>
        public static Transform Fit(GameObject model, Transform space, Bounds target, FitBy by, Quaternion orient,
                                    bool autoYaw = false, bool bottom = true)
        {
            var pivot = new GameObject(model.name).transform;
            pivot.SetParent(space, false);
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = orient;
            pivot.localScale = Vector3.one;
            model.transform.SetParent(pivot, false); // keeps the file's own rotation/scale inside the pivot

            Bounds b = BoundsIn(space, model);
            if (autoYaw)
            {
                bool targetWide = target.size.x > target.size.z * 1.15f, targetDeep = target.size.z > target.size.x * 1.15f;
                bool modelWide = b.size.x > b.size.z, modelDeep = b.size.z > b.size.x;
                if ((targetWide && modelDeep) || (targetDeep && modelWide))
                {
                    pivot.localRotation = Quaternion.Euler(0f, 90f, 0f) * pivot.localRotation;
                    b = BoundsIn(space, model);
                }
            }

            float s = 1f;
            Vector3 bs = Vector3.Max(b.size, Vector3.one * 0.0001f), ts = target.size;
            switch (by)
            {
                case FitBy.Inside: s = Mathf.Min(ts.x / bs.x, Mathf.Min(ts.y / bs.y, ts.z / bs.z)); break;
                case FitBy.Height: s = ts.y / bs.y; break;
                case FitBy.LengthZ: s = ts.z / bs.z; break;
                case FitBy.LengthY: s = ts.y / bs.y; break;
            }
            pivot.localScale = Vector3.one * s;

            b = BoundsIn(space, model);
            Vector3 offset = target.center - b.center;
            if (bottom) offset.y = target.min.y - b.min.y;
            pivot.localPosition += offset;
            return pivot;
        }

        /// A turn that lines the model's longest side up with the box's longest side (and middle with middle).
        /// Good for loot items: a knife in a long flat box lies flat, a can in a tall box stands up.
        public static Quaternion MatchAxes(GameObject model, Transform space, Vector3 targetSize)
        {
            var pivot = new GameObject("measure").transform;
            pivot.SetParent(space, false);
            model.transform.SetParent(pivot, false);
            Vector3 ms = BoundsIn(space, model).size;
            model.transform.SetParent(null, false);
            Object.Destroy(pivot.gameObject);

            int[] mo = Order(ms), to = Order(targetSize);
            Vector3 mL = Axis(mo[0]), mM = Axis(mo[1]), tL = Axis(to[0]), tM = Axis(to[1]);
            return Quaternion.LookRotation(tL, tM) * Quaternion.Inverse(Quaternion.LookRotation(mL, mM));
        }

        static int[] Order(Vector3 v)
        {
            var idx = new List<int> { 0, 1, 2 };
            idx.Sort((a, b) => v[b].CompareTo(v[a])); // biggest first
            return idx.ToArray();
        }

        static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        // ───────────────────────── Weapon orientation (measured from the mesh) ─────────────────────────

        static List<Vector3> Vertices(GameObject model)
        {
            var list = new List<Vector3>();
            // Measured in the model's PARENT space: the space a pivot (holding the model as-is) turns.
            Matrix4x4 toRoot = model.transform.parent != null ? model.transform.parent.worldToLocalMatrix : Matrix4x4.identity;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
                var v = mesh.vertices;
                int step = Mathf.Max(1, v.Length / 4000);
                for (int i = 0; i < v.Length; i += step) list.Add(m.MultiplyPoint3x4(v[i]));
            }
            return list;
        }

        /// <summary>
        /// Turn for a gun so the MUZZLE points +Z and the top points +Y.
        ///   longest side = the barrel line
        ///   thinner end  = the muzzle (the other end has the grip / stock)
        ///   the muzzle sits near the TOP of the gun (grips and magazines hang down)
        /// The turn is in the model root's own coordinates (apply it to a pivot holding the model).
        /// </summary>
        public static Quaternion GunOrientation(GameObject model)
        {
            var v = Vertices(model);
            if (v.Count < 8) return Quaternion.identity;
            GetAxes(v, out int L, out int U, out Vector3 min, out Vector3 max);
            float slice = (max[L] - min[L]) * 0.15f;
            Ends(v, L, min[L] + slice, max[L] - slice, U, out float crossLow, out float crossHigh, out float upLow, out float upHigh);
            int muzzleSign = crossHigh < crossLow ? 1 : -1;
            float muzzleUp = muzzleSign > 0 ? upHigh : upLow;
            float centerUp = (min[U] + max[U]) * 0.5f;
            int upSign = muzzleUp >= centerUp ? 1 : -1;
            Vector3 forward = Axis(L) * muzzleSign, up = Axis(U) * upSign;
            return Quaternion.Inverse(Quaternion.LookRotation(forward, up));
        }

        /// <summary>
        /// Turn for a melee weapon so it stands along +Y with the HANDLE at the bottom and the flat of the blade facing ±X.
        /// headIsBigger: true for bats and axes (the heavy end is up), false for knives (the pointy, thin end is up).
        /// </summary>
        public static Quaternion MeleeOrientation(GameObject model, bool headIsBigger)
        {
            var v = Vertices(model);
            if (v.Count < 8) return Quaternion.identity;
            GetAxes(v, out int L, out int W, out Vector3 min, out Vector3 max);
            float slice = (max[L] - min[L]) * 0.12f;
            Ends(v, L, min[L] + slice, max[L] - slice, W, out float crossLow, out float crossHigh, out _, out _);
            bool highIsBigger = crossHigh > crossLow;
            int headSign = (highIsBigger == headIsBigger) ? 1 : -1;
            Vector3 up = Axis(L) * headSign, width = Axis(W);
            return Quaternion.Inverse(Quaternion.LookRotation(width, up));
        }

        /// L = longest axis, U = the bigger of the other two.
        static void GetAxes(List<Vector3> v, out int L, out int U, out Vector3 min, out Vector3 max)
        {
            min = v[0]; max = v[0];
            foreach (var p in v) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            Vector3 size = max - min;
            int[] o = Order(size);
            L = o[0]; U = o[1];
        }

        /// Measures both ends of the long axis: how fat they are (cross) and where they sit on the 'up' axis.
        static void Ends(List<Vector3> v, int L, float lowCut, float highCut, int U, out float crossLow, out float crossHigh, out float upLow, out float upHigh)
        {
            int a = (L + 1) % 3, b = (L + 2) % 3;
            Vector3 lmin = Vector3.one * float.MaxValue, lmax = Vector3.one * float.MinValue;
            Vector3 hmin = lmin, hmax = lmax;
            float upSumL = 0f, upSumH = 0f; int nL = 0, nH = 0;
            foreach (var p in v)
            {
                if (p[L] <= lowCut) { lmin = Vector3.Min(lmin, p); lmax = Vector3.Max(lmax, p); upSumL += p[U]; nL++; }
                else if (p[L] >= highCut) { hmin = Vector3.Min(hmin, p); hmax = Vector3.Max(hmax, p); upSumH += p[U]; nH++; }
            }
            crossLow = nL > 0 ? (lmax[a] - lmin[a]) + (lmax[b] - lmin[b]) : 0f;
            crossHigh = nH > 0 ? (hmax[a] - hmin[a]) + (hmax[b] - hmin[b]) : 0f;
            upLow = nL > 0 ? upSumL / nL : 0f;
            upHigh = nH > 0 ? upSumH / nH : 0f;
        }

        // ───────────────────────── Small helpers ─────────────────────────

        public static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        public static void SetShadows(GameObject go, UnityEngine.Rendering.ShadowCastingMode mode)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = mode;
        }

        /// Adds one box collider around the whole model (props you bump into).
        public static BoxCollider AddBoxCollider(GameObject root)
        {
            var b = BoundsIn(root.transform, root);
            var col = root.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;
            return col;
        }
    }
}
