using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LastCheckout.Combat;
using LastCheckout.Items;

/// <summary>
/// One-click setup. Run it from the top menu:  Last Checkout > Add Shooting To Scene
///
/// It will:
///   1. Create two item cards: Pistol and Pistol Ammo (Assets/_Project/Data/Items)
///   2. Give the Player the shooting script
///   3. Put a pistol and some ammo on the floor near the entrance, so you can test right away
///   4. Save the scene
///
/// Safe to run twice: it skips anything that already exists.
/// </summary>
public static class ShootingSetup
{
    const string ItemFolder = "Assets/_Project/Data/Items";

    [MenuItem("Last Checkout/Add Shooting To Scene")]
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

        var pistol = CreateOrLoadItem("pistol", "Pistol", ItemCategory.Weapon,
            "A 9mm pistol. Loud.",
            "Press 2 to take it out, left click to shoot, R to reload. Every shot attracts zombies. Needs Pistol Ammo.",
            weight: 1.2f, size: new Vector3(0.25f, 0.12f, 0.06f), color: new Color(0.15f, 0.15f, 0.18f));

        var ammo = CreateOrLoadItem("pistol_ammo", "Pistol Ammo", ItemCategory.Weapon,
            "Loose 9mm rounds.",
            "Reloads the pistol. Each round weighs a little, so a full pocket gets heavy.",
            weight: 0.04f, size: new Vector3(0.15f, 0.08f, 0.1f), color: new Color(0.8f, 0.65f, 0.2f));

        SetupPlayer(player, pistol, ammo);
        PlaceTestPickups(pistol, ammo);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("[Last Checkout] Shooting added. Pick up the Pistol (E), then Ammo, press 2, left click to shoot, R to reload.");
    }

    static void SetupPlayer(GameObject player, ItemDefinition pistol, ItemDefinition ammo)
    {
        var shooting = player.GetComponent<PlayerShooting>();
        if (shooting == null) shooting = player.AddComponent<PlayerShooting>();

        var so = new SerializedObject(shooting);
        so.FindProperty("gunItem").objectReferenceValue = pistol;
        so.FindProperty("ammoItem").objectReferenceValue = ammo;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Items on the floor just inside the main entrance (the Player starts at z = -12).
    static void PlaceTestPickups(ItemDefinition pistol, ItemDefinition ammo)
    {
        if (GameObject.Find("Item: " + pistol.displayName) != null)
        {
            Debug.Log("[Last Checkout] Pistol pickup already in the scene, so I didn't add another.");
            return;
        }

        var parent = GameObject.Find("Loot");
        Place(pistol, 1, new Vector3(1.5f, 0f, -10f), parent);
        Place(ammo, 24, new Vector3(-1.5f, 0f, -10f), parent);
        Place(ammo, 12, new Vector3(0f, 0f, -8f), parent);
    }

    static void Place(ItemDefinition def, int count, Vector3 floorPosition, GameObject parent)
    {
        // Sit on the floor (floor surface is at y = 0.02).
        Vector3 position = new Vector3(floorPosition.x, 0.02f + def.worldSize.y * 0.5f, floorPosition.z);
        var item = WorldItem.Spawn(def, count, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), withPhysics: false);
        if (parent != null) item.transform.SetParent(parent.transform, true);
        Undo.RegisterCreatedObjectUndo(item.gameObject, "Place item");
    }

    static ItemDefinition CreateOrLoadItem(string id, string displayName, ItemCategory category,
                                           string description, string usage,
                                           float weight, Vector3 size, Color color)
    {
        string path = $"{ItemFolder}/{id}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (existing != null) return existing;

        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        item.id = id;
        item.displayName = displayName;
        item.category = category;
        item.description = description;
        item.usage = usage;
        item.weight = weight;
        item.worldSize = size;
        item.placeholderColor = color;
        AssetDatabase.CreateAsset(item, path);
        return item;
    }
}
