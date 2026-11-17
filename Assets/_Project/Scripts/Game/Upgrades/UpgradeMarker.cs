using System.Collections.Generic;
using UnityEngine;

namespace LastCheckout.Game
{
    /// <summary>
    /// A floating "NEW HERE" notification that points at something new in the store.
    ///
    ///   • A glowing beam of light stands where the new thing is (visible through the dark, from far away).
    ///   • On screen, a floating tag follows it: its name, a short hint and how many metres away it is.
    ///     It bobs up and down so it catches your eye.
    ///   • If it is behind you or off to the side, the tag sticks to the edge of the screen with an arrow
    ///     showing which way to turn.
    ///   • It goes away when you walk up to it (about 3.5 m), or after a while.
    ///
    /// From any script:   UpgradeMarker.Show("ROOF LADDER", "front wall, right of the window", position, color);
    /// </summary>
    public class UpgradeMarker : MonoBehaviour
    {
        class Mark
        {
            public string title, hint;
            public Vector3 position;
            public Color color;
            public float until, bornAt;
            public GameObject beam;
        }

        const float ArriveDistance = 3.5f;
        const float MarginX = 145f, MarginY = 60f;   // keeps the whole tag on screen

        static UpgradeMarker instance;
        readonly List<Mark> marks = new List<Mark>();
        Transform player;
        GUIStyle titleStyle, hintStyle, arrowStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        /// <summary>Shows (or replaces) a marker. 'position' is the world point the tag floats at.</summary>
        public static void Show(string title, string hint, Vector3 position, Color color, float seconds = 60f)
        {
            if (instance == null) instance = new GameObject("Upgrade Markers").AddComponent<UpgradeMarker>();
            instance.Add(title, hint, position, color, seconds);
        }

        /// <summary>Removes one marker by its title (e.g. when the truck leaves).</summary>
        public static void Remove(string title)
        {
            if (instance == null) return;
            for (int i = instance.marks.Count - 1; i >= 0; i--)
                if (instance.marks[i].title == title)
                {
                    if (instance.marks[i].beam != null) Destroy(instance.marks[i].beam);
                    instance.marks.RemoveAt(i);
                }
        }

        /// <summary>Removes every marker (new run / game over).</summary>
        public static void ClearAll()
        {
            if (instance != null) instance.RemoveAll();
        }

        void Add(string title, string hint, Vector3 position, Color color, float seconds)
        {
            for (int i = marks.Count - 1; i >= 0; i--)
                if (marks[i].title == title) { Destroy(marks[i].beam); marks.RemoveAt(i); }

            var m = new Mark { title = title, hint = hint, position = position, color = color, bornAt = Time.time, until = Time.time + seconds };
            m.beam = BuildBeam(position, color);
            marks.Add(m);
        }

        void RemoveAll()
        {
            foreach (var m in marks) if (m.beam != null) Destroy(m.beam);
            marks.Clear();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ───────────────────────── The light beam in the world ─────────────────────────

        static GameObject BuildBeam(Vector3 position, Color color)
        {
            var root = new GameObject("Upgrade Beam");
            root.transform.position = position;

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            LevelKit.RemoveCollider(pillar);
            pillar.name = "Pillar";
            pillar.transform.SetParent(root.transform, false);
            pillar.transform.localScale = new Vector3(0.14f, 3.5f, 0.14f);   // 7 m tall, centred on 'position'
            var r = pillar.GetComponent<Renderer>();
            r.sharedMaterial = LevelKit.ColorMaterial(color, 0.1f, 2.2f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = 2.5f;
            l.range = 6f;
            l.shadows = LightShadows.None;
            return root;
        }

        // ───────────────────────── Tags on the screen ─────────────────────────

        void Update()
        {
            if (marks.Count == 0) return;
            if (player == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) player = p.transform;
            }

            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var m = marks[i];
                bool arrived = player != null && Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(m.position.x, m.position.z)) < ArriveDistance
                               && Mathf.Abs(player.position.y - (m.position.y - 1f)) < 3.5f;
                if (arrived || Time.time > m.until)
                {
                    if (m.beam != null) Destroy(m.beam);
                    marks.RemoveAt(i);
                    continue;
                }
                if (m.beam != null) // beam pulses a little
                    m.beam.transform.localScale = new Vector3(1f + 0.25f * Mathf.Sin(Time.time * 4f), 1f, 1f + 0.25f * Mathf.Sin(Time.time * 4f));
            }
        }

        void OnGUI()
        {
            if (marks.Count == 0 || Event.current.type != EventType.Repaint || Time.timeScale == 0f) return;
            var cam = Camera.main;
            if (cam == null) return;
            EnsureStyles();

            foreach (var m in marks)
            {
                float age = Time.time - m.bornAt;
                float fadeIn = Mathf.Clamp01(age / 0.4f);
                float fadeOut = Mathf.Clamp01((m.until - Time.time) / 2f);
                float alpha = Mathf.Min(fadeIn, fadeOut);

                Vector3 world = m.position + Vector3.up * (0.25f * Mathf.Sin(Time.time * 2.5f) + 1.2f);   // bobbing
                Vector3 sp = cam.WorldToScreenPoint(world);
                bool behind = sp.z < 0f;
                if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }

                float x = sp.x, y = Screen.height - sp.y; // GUI space: 0 = top
                bool onScreen = !behind && x > MarginX && x < Screen.width - MarginX && y > MarginY && y < Screen.height - MarginY;
                float arrowAngle = 0f;
                if (!onScreen)
                {
                    // Stick to the edge of the screen, on the line from the screen centre towards the marker.
                    Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                    Vector2 dir = new Vector2(x, y) - c;
                    if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                    float sx = (Screen.width * 0.5f - MarginX) / Mathf.Max(0.01f, Mathf.Abs(dir.x));
                    float sy = (Screen.height * 0.5f - MarginY) / Mathf.Max(0.01f, Mathf.Abs(dir.y));
                    Vector2 edge = c + dir * Mathf.Min(sx, sy);
                    x = edge.x; y = edge.y;
                    arrowAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                }

                float metres = player != null ? Vector3.Distance(player.position, m.position) : 0f;
                Color c1 = new Color(m.color.r, m.color.g, m.color.b, alpha);
                Color white = new Color(1f, 1f, 1f, alpha);

                // Dark plate behind the text so it reads on any background.
                const float w = 250f, h = 62f;
                Rect plate = new Rect(x - w * 0.5f, y - h * 0.5f, w, h);
                GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.72f * alpha);
                GUI.DrawTexture(plate, Texture2D.whiteTexture);
                GUI.color = c1;
                GUI.DrawTexture(new Rect(plate.x, plate.y, 5f, h), Texture2D.whiteTexture);   // coloured edge
                GUI.DrawTexture(new Rect(plate.x, plate.y + h - 2f, w, 2f), Texture2D.whiteTexture);

                titleStyle.normal.textColor = c1;
                hintStyle.normal.textColor = white;
                GUI.Label(new Rect(plate.x + 12f, plate.y + 2f, w - 16f, 26f), "NEW  ·  " + m.title, titleStyle);
                GUI.Label(new Rect(plate.x + 12f, plate.y + 26f, w - 16f, 20f), m.hint, hintStyle);
                GUI.Label(new Rect(plate.x + 12f, plate.y + 42f, w - 16f, 18f), $"{metres:0} m", hintStyle);

                if (!onScreen)
                {
                    // An arrow at the right end of the tag that points the way to turn.
                    Vector2 arrowCentre = new Vector2(plate.xMax - 24f, y);
                    var old = GUI.matrix;
                    GUIUtility.RotateAroundPivot(arrowAngle, arrowCentre);
                    arrowStyle.normal.textColor = c1;
                    GUI.Label(new Rect(arrowCentre.x - 18f, arrowCentre.y - 16f, 36f, 32f), ">", arrowStyle);
                    GUI.matrix = old;
                }
                GUI.color = Color.white;
            }
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            arrowStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
    }
}
