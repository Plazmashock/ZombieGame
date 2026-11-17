using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using LastCheckout.Combat;
using LastCheckout.Enemies;
using LastCheckout.Items;

/// <summary>
/// One-click setup. Run it from the top menu:  Last Checkout > Add Zombies And Combat To Scene
///
/// It will:
///   1. Give the Player health + a melee attack (and tell it which items are weapons)
///   2. Add a "Navigation" object that builds the walkable-floor map when you press Play
///   3. Create a Zombie prefab (Assets/_Project/Prefabs/Zombie.prefab)
///   4. Place 4 zombies in the store
///   5. Save the scene
///
/// Safe to run twice: it skips anything that already exists.
/// </summary>
public static class ZombieSystemSetup
{
    const string PrefabFolder = "Assets/_Project/Prefabs";
    const string PrefabPath = PrefabFolder + "/Zombie.prefab";
    const string MaterialPath = "Assets/_Project/Materials/Greybox/Zombie.mat";
    const string ItemFolder = "Assets/_Project/Data/Items/";

    // Where zombies start (x, z on the store floor). They snap to the nearest walkable spot.
    static readonly Vector2[] SpawnPoints =
    {
        new Vector2(0f, 7f),     // center aisle, middle of the store
        new Vector2(-2f, 2f),    // center aisle, closer to you
        new Vector2(7f, 12f),    // back corridor
        new Vector2(-12f, 12f),  // storage room
    };

    [MenuItem("Last Checkout/Add Zombies And Combat To Scene")]
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

        SetupPlayer(player);
        SetupNavigation();
        var prefab = GetOrCreateZombiePrefab();
        SpawnZombies(prefab);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("[Last Checkout] Zombies and combat added. Press Play! Left click = swing. Zombies hear you when you sprint.");
    }

    // ───────────── 1. Player ─────────────

    static void SetupPlayer(GameObject player)
    {
        if (player.GetComponent<PlayerHealth>() == null) player.AddComponent<PlayerHealth>();

        var melee = player.GetComponent<PlayerMelee>();
        if (melee == null) melee = player.AddComponent<PlayerMelee>();

        // Weapons: carrying one of these in your backpack makes your hits stronger.
        (string file, float damage)[] table =
        {
            ("baseball_bat", 50f),
            ("hammer", 40f),
            ("kitchen_knife", 35f),
        };

        var so = new SerializedObject(melee);
        var array = so.FindProperty("weapons");
        array.arraySize = table.Length;
        for (int i = 0; i < table.Length; i++)
        {
            var element = array.GetArrayElementAtIndex(i);
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemFolder + table[i].file + ".asset");
            if (item == null) Debug.LogWarning("[Last Checkout] Couldn't find item asset: " + table[i].file);
            element.FindPropertyRelative("item").objectReferenceValue = item;
            element.FindPropertyRelative("damage").floatValue = table[i].damage;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───────────── 2. Navigation ─────────────

    static void SetupNavigation()
    {
        var nav = GameObject.Find("Navigation");
        if (nav == null) nav = new GameObject("Navigation");

        var surface = nav.GetComponent<NavMeshSurface>();
        if (surface == null) surface = nav.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

        if (nav.GetComponent<NavMeshBaker>() == null) nav.AddComponent<NavMeshBaker>();

        // Loot boxes lying around shouldn't punch holes in the walkable floor.
        var loot = GameObject.Find("Loot");
        if (loot != null && loot.GetComponent<NavMeshModifier>() == null)
            loot.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
    }

    // ───────────── 3. Zombie prefab ─────────────

    static GameObject GetOrCreateZombiePrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null) return existing;

        EnsureFolder(PrefabFolder);
        var material = GetOrCreateMaterial();

        // The root sits at the zombie's FEET (that's what the navigation system expects).
        var root = new GameObject("Zombie");

        var collider = root.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.9f, 0f);
        collider.height = 1.8f;
        collider.radius = 0.35f;

        MakePart(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), material);
        MakePart(root.transform, "Face (shows which way it looks)", PrimitiveType.Cube, new Vector3(0f, 1.45f, 0.3f), new Vector3(0.22f, 0.2f, 0.25f), material);

        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.4f;
        agent.height = 1.8f;
        agent.enabled = false; // the Zombie script switches it on once the walkable floor exists

        root.AddComponent<Zombie>();

        // Zombies must not become obstacles in the walkable-floor map.
        root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void MakePart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>()); // the root has the collider
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    static Material GetOrCreateMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        material = new Material(shader);
        material.SetColor("_BaseColor", new Color(0.35f, 0.55f, 0.3f)); // URP
        material.SetColor("_Color", new Color(0.35f, 0.55f, 0.3f));     // built-in fallback
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    // ───────────── 4. Place zombies ─────────────

    static void SpawnZombies(GameObject prefab)
    {
        if (GameObject.Find("Zombies") != null)
        {
            Debug.Log("[Last Checkout] A 'Zombies' group already exists, so I didn't add more.");
            return;
        }

        var group = new GameObject("Zombies").transform;
        for (int i = 0; i < SpawnPoints.Length; i++)
        {
            var zombie = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            zombie.name = "Zombie " + (i + 1);
            zombie.transform.SetPositionAndRotation(
                new Vector3(SpawnPoints[i].x, 0.02f, SpawnPoints[i].y),
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        }
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
