using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace LastCheckout.Game
{
    /// <summary>
    /// Small building helpers for things made in code at game start (the armory, signs...).
    /// Same idea as the editor's greybox builder: everything is a simple box.
    /// </summary>
    public static class LevelKit
    {
        static Font labelFont;
        static Material labelMaterial;

        /// <summary>A solid box (with collider) whose size is in meters.</summary>
        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, Color? tint = null, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            if (!collider) RemoveCollider(go);
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            if (tint.HasValue) Items.WorldItem.ApplyColor(r, tint.Value);
            return go;
        }

        /// <summary>
        /// Removes a primitive's collider. It is switched off FIRST, because Destroy only happens at the end
        /// of the frame, and the zombie floor map is rebuilt in this same frame (it must not see it).
        /// </summary>
        public static void RemoveCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            c.enabled = false;
            Object.Destroy(c);
        }

        static readonly Dictionary<string, Material> colorMaterials = new Dictionary<string, Material>();

        /// <summary>
        /// A plain coloured material (same URP shader as the store floor), shared by everyone asking for the same colour.
        /// emission > 0 makes it glow.
        /// </summary>
        public static Material ColorMaterial(Color color, float smoothness = 0.25f, float emission = 0f)
        {
            string key = $"{color.r:F3}_{color.g:F3}_{color.b:F3}_{smoothness:F2}_{emission:F2}";
            if (colorMaterials.TryGetValue(key, out var m) && m != null) return m;

            var baseMat = SceneMaterial("Store Floor");
            m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "Color " + key;
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
            colorMaterials[key] = m;
            return m;
        }

        /// <summary>Finds a material already used in the scene by an object with this exact name (e.g. "Store Floor").</summary>
        public static Material SceneMaterial(string objectName)
        {
            foreach (var r in FindAll<Renderer>())
                if (r.name == objectName && r.sharedMaterial != null) return r.sharedMaterial;
            return null;
        }

        /// <summary>Every component of type T in the open scene (including switched-off ones).</summary>
        public static List<T> FindAll<T>() where T : Component
        {
            var list = new List<T>();
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return list;
            foreach (var root in scene.GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<T>(true));
            return list;
        }

        public static T FindFirst<T>() where T : Component
        {
            var all = FindAll<T>();
            return all.Count > 0 ? all[0] : null;
        }

        /// <summary>A floating 3D text label (signs on walls).</summary>
        public static TextMesh Label(string text, Transform parent, Vector3 position, Quaternion rotation, float height, Color color)
        {
            if (labelFont == null) labelFont = Resources.Load<Font>("Fonts/BarlowCondensed_800ExtraBold");
            if (labelFont == null) labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (labelMaterial == null)
            {
                // Our own text shader (Resources/Shaders/WorldText) is hidden by walls.
                // Unity's default text shader draws on top of everything, so signs showed through walls.
                var shader = Shader.Find("LastCheckout/WorldText");
                labelMaterial = shader != null ? new Material(shader) : new Material(labelFont.material);
                labelMaterial.mainTexture = labelFont.material.mainTexture;
                labelMaterial.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                Font.textureRebuilt += f => { if (labelMaterial != null && f == labelFont) labelMaterial.mainTexture = f.material.mainTexture; };
            }

            var go = new GameObject($"Sign: {text}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = labelFont;
            tm.fontSize = 96;
            tm.characterSize = height / 9.6f; // fontSize 96 → roughly 'height' meters tall
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = labelMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<World.OneSidedText>(); // invisible from behind (no mirrored text)
            return tm;
        }

        /// <summary>Unity's "smooth step" style ease used by moving parts.</summary>
        public static float EaseInOut(float t) => t * t * (3f - 2f * t);
    }
}
