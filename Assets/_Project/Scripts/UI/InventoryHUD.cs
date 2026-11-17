using System.Collections.Generic;
using UnityEngine;
using LastCheckout.Inventory;
using LastCheckout.Items;
using LastCheckout.Player;

namespace LastCheckout.UI
{
    /// <summary>
    /// TEMPORARY placeholder backpack UI (simple OnGUI, zero setup):
    ///  - weight bar in the bottom-left corner, always visible
    ///  - "+2 Canned Beans" pickup messages
    ///  - Tab (or I) opens the backpack list with Drop buttons. You can still walk while it's open!
    /// A proper styled UI comes in the polish milestone.
    /// </summary>
    public class InventoryHUD : MonoBehaviour
    {
        [SerializeField] PlayerInventory inventory;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerLook look;
        [SerializeField] float messageDuration = 2.5f;

        public bool IsOpen { get; private set; }

        readonly List<(string text, float time)> messages = new List<(string, float)>();
        Vector2 scroll;
        GUIStyle titleStyle, rowStyle, smallStyle, rightStyle, msgStyle;

        void Reset() => AutoFindReferences();

        void Awake() => AutoFindReferences();

        void AutoFindReferences()
        {
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (look == null) look = GetComponent<PlayerLook>();
        }

        void OnEnable() { if (inventory != null) inventory.ItemAdded += OnItemAdded; }
        void OnDisable() { if (inventory != null) inventory.ItemAdded -= OnItemAdded; }

        void OnItemAdded(ItemDefinition item, int count) =>
            messages.Add(($"+{count}  {item.displayName}", Time.time));

        void Update()
        {
            if (input.InventoryPressed) SetOpen(!IsOpen, relockCursor: true);
            else if (IsOpen && input.PausePressed) SetOpen(false, relockCursor: false);

            messages.RemoveAll(m => Time.time - m.time > messageDuration);
        }

        void SetOpen(bool open, bool relockCursor)
        {
            IsOpen = open;
            look.IsUIOpen = open;              // stops mouse-look and click-to-capture while the list is open
            if (open) look.SetCursorLocked(false);
            else if (relockCursor) look.SetCursorLocked(true);
        }

        void OnGUI()
        {
            if (inventory == null) return;
            if (GameHUD.IsActive) return; // the new HUD draws the weight bar, pickup feed and backpack screen
            EnsureStyles();

            DrawWeightBar();
            DrawMessages();
            if (IsOpen) DrawPanel();
        }

        void DrawWeightBar()
        {
            float x = 20f, y = Screen.height - 64f, w = 280f;
            GUI.Label(new Rect(x, y, w, 22f),
                $"Backpack  {inventory.CurrentWeight:0.0} / {inventory.MaxWeight:0} kg      [Tab] open", rowStyle);

            float load = inventory.LoadFraction;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(x, y + 24f, w, 10f), Texture2D.whiteTexture);
            GUI.color = load < 0.6f ? new Color(0.4f, 0.85f, 0.4f) : load < 0.9f ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.95f, 0.35f, 0.3f);
            GUI.DrawTexture(new Rect(x, y + 24f, w * load, 10f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawMessages()
        {
            float y = Screen.height - 100f;
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                float age = Time.time - messages[i].time;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((messageDuration - age) / 0.5f));
                GUI.Label(new Rect(20f, y, 400f, 26f), messages[i].text, msgStyle);
                y -= 26f;
            }
            GUI.color = Color.white;
        }

        void DrawPanel()
        {
            var area = new Rect(40f, 50f, 560f, Screen.height - 150f);
            GUI.color = new Color(1f, 1f, 1f, 0.95f);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none); // drawn twice = darker background
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(area.x + 16f, area.y + 12f, area.width - 32f, area.height - 24f));
            GUILayout.Label("BACKPACK", titleStyle);
            GUILayout.Label($"{inventory.CurrentWeight:0.0} / {inventory.MaxWeight:0} kg   •   heavier = slower   •   Tab to close", smallStyle);
            GUILayout.Space(8f);

            if (inventory.Stacks.Count == 0)
                GUILayout.Label("Empty. Look at an item and press E to take it.", rowStyle);

            scroll = GUILayout.BeginScrollView(scroll);
            // copy the list: pressing Drop changes the inventory while we draw it
            var stacks = new List<ItemStack>(inventory.Stacks);
            foreach (var s in stacks)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{s.item.displayName}  x{s.count}", rowStyle, GUILayout.Width(250f));
                GUILayout.Label($"{s.Weight:0.0} kg", rightStyle, GUILayout.Width(70f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Drop 1", GUILayout.Width(70f))) inventory.Drop(s.item, 1);
                if (s.count > 1 && GUILayout.Button("Drop all", GUILayout.Width(70f))) inventory.Drop(s.item, s.count);
                GUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(s.item.usage)) GUILayout.Label(s.item.usage, smallStyle);
                GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            rightStyle = new GUIStyle(rowStyle) { alignment = TextAnchor.MiddleRight };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            smallStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            msgStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        }
    }
}
