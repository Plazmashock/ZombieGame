using System.Collections.Generic;
using UnityEngine;

namespace LastCheckout.World
{
    /// <summary>
    /// A climbable ladder.
    ///
    /// HOW TO USE IT AS A PLAYER
    ///   Walk into the ladder while looking at it and hold W  → you climb up.
    ///   At the top, keep holding W → you hop off onto the roof.
    ///   Press S to climb down (also works from the roof: face away from the roof, walk to the edge and hold S).
    ///   Space = jump off backwards.
    ///
    /// HOW IT WORKS
    ///   The ladder itself has no collider (nothing to get stuck on, and zombies' floor map ignores it, so zombies
    ///   can't climb it). It is just a "zone" the player's body is checked against every frame.
    ///   FirstPersonController asks <see cref="All"/> if you are inside a zone, and then climbs instead of walking.
    ///
    ///   Column  = where your feet hang while climbing (a bit out from the wall, so the roof edge doesn't bump your head)
    ///   Toward  = flat direction from you INTO the wall
    ///   TopY    = height of the roof you step onto
    /// </summary>
    public class Ladder : MonoBehaviour
    {
        public static readonly List<Ladder> All = new List<Ladder>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public Vector3 Column { get; private set; }
        public Vector3 Toward { get; private set; }
        public Vector3 Across { get; private set; }
        public float TopY { get; private set; }
        float halfWidth = 0.55f;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        Vector2 Local(Vector3 feet)
        {
            Vector3 d = feet - Column;
            return new Vector2(Vector3.Dot(d, Toward), Vector3.Dot(d, Across)); // x = along (towards wall), y = across
        }

        /// <summary>True when your feet are at the bottom / middle of the ladder, in front of the rungs.</summary>
        public bool Contains(Vector3 feet)
        {
            Vector2 l = Local(feet);
            return Mathf.Abs(l.y) <= halfWidth && l.x >= -1.0f && l.x <= 0.6f &&
                   feet.y >= Column.y - 0.3f && feet.y <= TopY + 0.6f;
        }

        /// <summary>True when you stand on the roof right at the top of the ladder (to climb DOWN).</summary>
        public bool ContainsTop(Vector3 feet)
        {
            Vector2 l = Local(feet);
            return Mathf.Abs(l.y) <= halfWidth && l.x >= 0.5f && l.x <= 1.8f &&
                   feet.y >= TopY - 0.3f && feet.y <= TopY + 0.6f;
        }

        public bool InZone(Vector3 feet) => Contains(feet) || ContainsTop(feet);

        // ───────────────────────── Building one ─────────────────────────

        /// <summary>
        /// Builds a ladder against a wall.
        ///   columnFloor  where you stand at the bottom (on the ground), centre of the ladder
        ///   toward       direction from you to the wall (horizontal)
        ///   topY         height of the roof to climb onto
        ///   standOff     how far from 'columnFloor' the rungs are (towards the wall)
        ///   wallGap      distance from the rungs to the wall (for the brackets)
        /// </summary>
        public static Ladder Build(string name, Transform parent, Vector3 columnFloor, Vector3 toward, float topY,
                                   Material material, float width = 0.6f, float standOff = 0.55f, float wallGap = 0.35f, float extraTop = 1.1f)
        {
            toward.y = 0f;
            toward.Normalize();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ladder = go.AddComponent<Ladder>();
            ladder.Toward = toward;
            ladder.Across = Vector3.Cross(Vector3.up, toward);
            ladder.Column = columnFloor;
            ladder.TopY = topY;
            ladder.halfWidth = Mathf.Max(0.5f, width * 0.5f + 0.2f);

            Quaternion rot = Quaternion.LookRotation(toward, Vector3.up);
            Vector3 plane = new Vector3(columnFloor.x, 0f, columnFloor.z) + toward * standOff;   // rungs are on this vertical plane
            float bottom = columnFloor.y, top = topY + extraTop;
            float h = top - bottom;

            var mb = new ModelKit.MeshBuilder();
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 railPos = plane + ladder.Across * (side * width * 0.5f);
                mb.AddBox(railPos + Vector3.up * (bottom + h * 0.5f), new Vector3(0.06f, h, 0.06f), rot, Matrix4x4.identity, 1f);
                // brackets holding the rails off the wall
                foreach (float y in new[] { bottom + 0.7f, bottom + (topY - bottom) * 0.5f, topY - 0.35f })
                    mb.AddBox(railPos + toward * (wallGap * 0.5f) + Vector3.up * y, new Vector3(0.05f, 0.05f, wallGap + 0.04f), rot, Matrix4x4.identity, 1f);
            }
            for (float y = bottom + 0.35f; y < top - 0.1f; y += 0.32f)
                mb.AddBox(plane + Vector3.up * y, new Vector3(width, 0.04f, 0.05f), rot, Matrix4x4.identity, 1f);

            ModelKit.Finish(go, mb, material, castShadows: false);
            return ladder;
        }
    }
}
