using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LastCheckout.Combat;

/// <summary>
/// One-click upgrade. Run it from the top menu:  Last Checkout > Upgrade Gun And Graphics
///
/// It will:
///   1. Build a 3D pistol model in front of the camera (shown when you press 2)
///   2. Give the zombies a proper body: head, torso, arms out, legs, glowing eyes, blood
///   3. Turn on post-processing: tone mapping, glow (bloom), vignette, color grading, film grain, anti-aliasing
///   4. Save the scene
///
/// Safe to run again: it rebuilds the gun and zombie looks from scratch each time.
/// (Run "Add Shooting To Scene" first if you haven't.)
/// </summary>
public static class GunAndGraphicsSetup
{
    const string MaterialFolder = "Assets/_Project/Materials/Upgrade";
    const string SettingsFolder = "Assets/_Project/Settings";
    const string ProfilePath = SettingsFolder + "/PostProcessing.asset";
    const string ZombiePrefabPath = "Assets/_Project/Prefabs/Zombie.prefab";

    [MenuItem("Last Checkout/Upgrade Gun And Graphics")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Stop Play mode first", "Press the Play button again to stop the game, then run this.", "OK");
            return;
        }

        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("No Player found",
                "Open the Supermarket_Greybox scene first (the Player object must have the 'Player' tag).", "OK");
            return;
        }

        var cam = player.GetComponentInChildren<Camera>();
        if (cam == null)
        {
            EditorUtility.DisplayDialog("No camera found", "The Player needs a camera as a child object.", "OK");
            return;
        }

        if (player.GetComponent<PlayerShooting>() == null)
        {
            EditorUtility.DisplayDialog("Add shooting first",
                "Run  Last Checkout > Add Shooting To Scene  first, then run this again.", "OK");
            return;
        }

        EnsureFolder(MaterialFolder);

        BuildGunModel(cam);
        UpgradeZombiePrefab();
        SetupPostProcessing(cam);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("[Last Checkout] Gun model, zombie look and post-processing are ready. Press Play!");
    }

    // ───────────────────────── 1. Gun model ─────────────────────────

    static void BuildGunModel(Camera cam)
    {
        var slideMat = GetMaterial("Gun_Slide", new Color(0.28f, 0.30f, 0.33f), 0.9f, 0.65f);
        var frameMat = GetMaterial("Gun_Frame", new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.5f);
        var gripMat = GetMaterial("Gun_Grip", new Color(0.06f, 0.06f, 0.07f), 0f, 0.25f);
        var flashMat = GetMaterial("Gun_Flash", new Color(1f, 0.85f, 0.4f), 0f, 0f, unlit: true);

        // Start fresh each time so you can tweak and re-run.
        var old = cam.transform.Find("Pistol View");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // "Pistol View" sits in the lower-right of your screen, attached to the camera.
        var root = new GameObject("Pistol View").transform;
        root.SetParent(cam.transform, false);
        root.localPosition = new Vector3(0.17f, -0.17f, 0.38f);
        root.localRotation = Quaternion.Euler(0f, -1.5f, 0f);

        var model = new GameObject("Model").transform;
        model.SetParent(root, false);

        // Barrel points along +Z (forward). Sizes are in meters.
        Part(model, "Slide", PrimitiveType.Cube, new Vector3(0f, 0.035f, 0.02f), Vector3.zero, new Vector3(0.042f, 0.048f, 0.21f), slideMat);
        Part(model, "Frame", PrimitiveType.Cube, new Vector3(0f, 0.0f, 0.0f), Vector3.zero, new Vector3(0.036f, 0.03f, 0.17f), frameMat);
        Part(model, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.036f, 0.15f), new Vector3(90f, 0f, 0f), new Vector3(0.02f, 0.04f, 0.02f), frameMat);
        Part(model, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.07f, -0.065f), new Vector3(14f, 0f, 0f), new Vector3(0.038f, 0.11f, 0.055f), gripMat);
        Part(model, "Magazine Base", PrimitiveType.Cube, new Vector3(0f, -0.127f, -0.078f), new Vector3(14f, 0f, 0f), new Vector3(0.04f, 0.012f, 0.058f), frameMat);
        Part(model, "Trigger Guard Bottom", PrimitiveType.Cube, new Vector3(0f, -0.048f, 0.02f), Vector3.zero, new Vector3(0.012f, 0.008f, 0.07f), frameMat);
        Part(model, "Trigger Guard Front", PrimitiveType.Cube, new Vector3(0f, -0.033f, 0.058f), Vector3.zero, new Vector3(0.012f, 0.03f, 0.008f), frameMat);
        Part(model, "Trigger", PrimitiveType.Cube, new Vector3(0f, -0.025f, 0.02f), Vector3.zero, new Vector3(0.008f, 0.02f, 0.008f), frameMat);
        Part(model, "Hammer", PrimitiveType.Cube, new Vector3(0f, 0.045f, -0.105f), Vector3.zero, new Vector3(0.026f, 0.02f, 0.02f), frameMat);
        Part(model, "Front Sight", PrimitiveType.Cube, new Vector3(0f, 0.064f, 0.115f), Vector3.zero, new Vector3(0.008f, 0.012f, 0.012f), frameMat);
        Part(model, "Rear Sight", PrimitiveType.Cube, new Vector3(0f, 0.064f, -0.075f), Vector3.zero, new Vector3(0.026f, 0.012f, 0.01f), frameMat);

        // Muzzle = the end of the barrel. Bullet streaks start here, and the flash appears here.
        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(model, false);
        muzzle.localPosition = new Vector3(0f, 0.036f, 0.195f);

        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "Muzzle Flash";
        Object.DestroyImmediate(flash.GetComponent<Collider>());
        flash.transform.SetParent(muzzle, false);
        flash.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        flash.transform.localScale = new Vector3(0.07f, 0.07f, 0.12f);
        var flashRenderer = flash.GetComponent<Renderer>();
        flashRenderer.sharedMaterial = flashMat;
        flashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flash.SetActive(false);

        var lightObject = new GameObject("Muzzle Light");
        lightObject.transform.SetParent(muzzle, false);
        lightObject.transform.localPosition = new Vector3(0f, 0f, 0.06f);
        var muzzleLight = lightObject.AddComponent<Light>();
        muzzleLight.type = LightType.Point;
        muzzleLight.color = new Color(1f, 0.7f, 0.35f);
        muzzleLight.intensity = 3f;
        muzzleLight.range = 8f;
        muzzleLight.shadows = LightShadows.None;
        muzzleLight.enabled = false;

        // Hook everything up to the script that animates it.
        var viewModel = root.gameObject.AddComponent<GunViewModel>();
        var so = new SerializedObject(viewModel);
        so.FindProperty("model").objectReferenceValue = model;
        so.FindProperty("muzzle").objectReferenceValue = muzzle;
        so.FindProperty("muzzleFlash").objectReferenceValue = flash;
        so.FindProperty("muzzleLight").objectReferenceValue = muzzleLight;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───────────────────────── 2. Zombie look ─────────────────────────

    static void UpgradeZombiePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath) == null)
        {
            Debug.LogWarning("[Last Checkout] Zombie prefab not found, so I skipped the zombie look. Run 'Add Zombies And Combat To Scene' first.");
            return;
        }

        var skin = GetMaterial("Zombie_Skin", new Color(0.50f, 0.58f, 0.42f), 0f, 0.25f);
        var shirt = GetMaterial("Zombie_Shirt", new Color(0.22f, 0.27f, 0.32f), 0f, 0.15f);
        var pants = GetMaterial("Zombie_Pants", new Color(0.13f, 0.14f, 0.19f), 0f, 0.15f);
        var blood = GetMaterial("Zombie_Blood", new Color(0.38f, 0.02f, 0.02f), 0f, 0.7f);
        var eyes = GetMaterial("Zombie_Eyes", new Color(0.9f, 0.15f, 0.05f), 0f, 0.2f, emission: new Color(1f, 0.15f, 0.05f) * 3f);

        var contents = PrefabUtility.LoadPrefabContents(ZombiePrefabPath);
        try
        {
            // Throw away the old box-and-capsule look.
            for (int i = contents.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(contents.transform.GetChild(i).gameObject);

            // "Model" holds every visible part. The Zombie script sways it while walking.
            var model = new GameObject("Model").transform;
            model.SetParent(contents.transform, false);

            // The prefab's root sits at the feet; the zombie is 1.8m tall.
            Part(model, "Leg L", PrimitiveType.Capsule, new Vector3(-0.12f, 0.45f, 0f), new Vector3(-8f, 0f, 0f), new Vector3(0.17f, 0.45f, 0.17f), pants);
            Part(model, "Leg R", PrimitiveType.Capsule, new Vector3(0.12f, 0.45f, 0f), new Vector3(10f, 0f, 0f), new Vector3(0.17f, 0.45f, 0.17f), pants);
            Part(model, "Torso", PrimitiveType.Capsule, new Vector3(0f, 1.1f, 0f), new Vector3(10f, 0f, 0f), new Vector3(0.42f, 0.36f, 0.27f), shirt);
            Part(model, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.62f, 0.06f), Vector3.zero, new Vector3(0.24f, 0.27f, 0.25f), skin);
            Part(model, "Arm L", PrimitiveType.Capsule, new Vector3(-0.27f, 1.33f, 0.3f), new Vector3(80f, 0f, 6f), new Vector3(0.1f, 0.32f, 0.1f), skin);
            Part(model, "Arm R", PrimitiveType.Capsule, new Vector3(0.27f, 1.33f, 0.3f), new Vector3(80f, 0f, -6f), new Vector3(0.1f, 0.32f, 0.1f), skin);
            Part(model, "Eye L", PrimitiveType.Cube, new Vector3(-0.065f, 1.65f, 0.18f), Vector3.zero, new Vector3(0.045f, 0.03f, 0.02f), eyes);
            Part(model, "Eye R", PrimitiveType.Cube, new Vector3(0.065f, 1.65f, 0.18f), Vector3.zero, new Vector3(0.045f, 0.03f, 0.02f), eyes);
            Part(model, "Wound", PrimitiveType.Cube, new Vector3(0.06f, 1.16f, 0.145f), new Vector3(10f, 0f, 12f), new Vector3(0.2f, 0.14f, 0.02f), blood);

            PrefabUtility.SaveAsPrefabAsset(contents, ZombiePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ───────────────────────── 3. Post-processing ─────────────────────────

    static void SetupPostProcessing(Camera cam)
    {
        EnsureFolder(SettingsFolder);

        // Rebuild the effect list from scratch so re-running always gives the latest values.
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath) != null)
            AssetDatabase.DeleteAsset(ProfilePath);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        // Tone mapping: film-like contrast, stops bright lights from looking flat white.
        var tone = AddEffect<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);

        // Bloom: bright things (ceiling lights, muzzle flash, bullet streaks, zombie eyes) glow.
        var bloom = AddEffect<Bloom>(profile);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.7f);

        // Vignette: darker screen edges, more tense.
        var vignette = AddEffect<Vignette>(profile);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.45f);

        // Color grading: a bit more contrast, slightly washed-out colors = grim mood.
        var color = AddEffect<ColorAdjustments>(profile);
        color.postExposure.Override(0.15f);
        color.contrast.Override(15f);
        color.saturation.Override(-12f);

        // Film grain: subtle noise so dark areas feel gritty.
        var grain = AddEffect<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.18f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // A "Global Volume" applies the effects everywhere in the scene.
        var volumeObject = GameObject.Find("Post Processing");
        if (volumeObject == null) volumeObject = new GameObject("Post Processing");
        var volume = volumeObject.GetComponent<Volume>();
        if (volume == null) volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;

        // Camera: switch post-processing on, smoother edges (SMAA), HDR for glow.
        var cameraData = cam.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cameraData.antialiasingQuality = AntialiasingQuality.High;
        cam.allowHDR = true;
    }

    static T AddEffect<T>(VolumeProfile profile) where T : VolumeComponent
    {
        var effect = profile.Add<T>(false);
        AssetDatabase.AddObjectToAsset(effect, profile); // save the effect inside the profile file
        return effect;
    }

    // ───────────────────────── Helpers ─────────────────────────

    static void Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 euler, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>()); // parts are just looks; no collisions
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = scale;

        var r = part.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Material GetMaterial(string name, Color color, float metallic, float smoothness, Color? emission = null, bool unlit = false)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

        if (emission.HasValue && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
