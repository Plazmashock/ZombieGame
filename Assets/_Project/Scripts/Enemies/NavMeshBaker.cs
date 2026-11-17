using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using LastCheckout.Game;
using LastCheckout.Items;
using LastCheckout.World;

namespace LastCheckout.Enemies
{
    /// <summary>
    /// Builds the "walkable floor map" (NavMesh) the moment the game starts.
    /// Zombies need this map to find their way around shelves and walls.
    /// Runs before everything else so the map exists before any zombie wakes up.
    /// Because it rebuilds on every Play, you never have to bake by hand after changing the store.
    ///
    /// WHY COLLIDERS (not visible meshes):
    ///   The store boxes are "static batched": on Play, Unity glues them into one big mesh
    ///   that scripts are not allowed to read. Reading meshes gave an EMPTY floor map,
    ///   so no zombie could stand anywhere (= no spawns). Colliders are never glued together,
    ///   so building from colliders always works, in the editor AND in a built game.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(NavMeshSurface))]
    public class NavMeshBaker : MonoBehaviour
    {
        void Awake() => Bake(GetComponent<NavMeshSurface>());

        /// <summary>Use this instead of surface.BuildNavMesh() so the settings are always right.</summary>
        public static void Bake(NavMeshSurface surface)
        {
            if (surface == null) return;

            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; // the fix (see above)
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;

            IgnoreThingsThatMove();
            surface.BuildNavMesh();
        }

        /// Things that move or get picked up must not punch holes in the floor map.
        static void IgnoreThingsThatMove()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) Ignore(player);

            foreach (var item in LevelKit.FindAll<WorldItem>()) Ignore(item.gameObject);

            // Unlocked doors: zombies push them open (see Zombie.OpenNearbyDoors), so the doorway counts as floor.
            // Locked doors stay solid walls.
            foreach (var door in LevelKit.FindAll<Door>())
                if (!door.IsLocked) Ignore(door.gameObject);
        }

        static void Ignore(GameObject go)
        {
            var modifier = go.GetComponent<NavMeshModifier>();
            if (modifier == null) modifier = go.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
        }
    }
}
