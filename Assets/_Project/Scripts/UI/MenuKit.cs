using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastCheckout.UI
{
    /// <summary>
    /// Builds menu widgets in code (same look as the HUD: dark panels, warm yellow accent, condensed type).
    ///
    ///   Button   [ PLAY            ]   Primary = filled yellow · Ghost = text with a yellow bar on hover · Panel = dark pill
    ///   Slider   MOUSE SENSITIVITY   ━━━━━●──────   0.12
    ///   Toggle   INVERT Y                          [ OFF ]
    ///   Choice   QUALITY                     ‹  HIGH  ›
    ///
    /// Rows are 720 px wide and placed by their TOP-LEFT corner (y goes down = negative).
    /// </summary>
    public static class MenuKit
    {
        public enum Style { Primary, Ghost, Panel }

        public const float RowHeight = 54f;
        public const float RowWidth = 720f;
        static readonly Color Dark = new Color(0.06f, 0.06f, 0.06f);

        // ───────────────────────── Buttons ─────────────────────────

        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                                    UnityAction onClick, Style style = Style.Panel, int fontSize = 28, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var bg = UIKit.Box("Button " + label, parent, Color.white, style != Style.Ghost, 2.5f);
            bg.raycastTarget = true;
            UIKit.Place(bg.rectTransform, anchor, pivot, pos, size);

            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.navigation = new Navigation { mode = Navigation.Mode.None }; // no keyboard "selection" (Space would press it)
            var colors = b.colors;
            switch (style)
            {
                case Style.Primary:
                    colors.normalColor = UIKit.Accent;
                    colors.highlightedColor = new Color(1f, 0.88f, 0.5f);
                    colors.pressedColor = new Color(0.9f, 0.62f, 0.12f);
                    colors.disabledColor = new Color(1f, 0.78f, 0.24f, 0.25f);
                    break;
                case Style.Ghost:
                    colors.normalColor = new Color(1f, 1f, 1f, 0f);
                    colors.highlightedColor = new Color(1f, 1f, 1f, 0.06f);
                    colors.pressedColor = new Color(1f, 1f, 1f, 0.12f);
                    colors.disabledColor = new Color(1f, 1f, 1f, 0f);
                    break;
                default:
                    colors.normalColor = new Color(1f, 1f, 1f, 0.08f);
                    colors.highlightedColor = new Color(1f, 0.78f, 0.24f, 0.9f);
                    colors.pressedColor = new Color(1f, 0.65f, 0.15f, 1f);
                    colors.disabledColor = new Color(1f, 1f, 1f, 0.03f);
                    break;
            }
            colors.selectedColor = colors.normalColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            b.colors = colors;
            b.onClick.AddListener(() =>
            {
                onClick?.Invoke();
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });

            var t = UIKit.Label("Text", bg.transform, fontSize, true, style == Style.Primary ? Dark : UIKit.Ink, align);
            var trt = UIKit.Stretch(t.rectTransform);
            if (align == TextAnchor.MiddleLeft) trt.offsetMin = new Vector2(style == Style.Ghost ? 28f : 22f, 0f);
            t.text = label;

            var fx = bg.gameObject.AddComponent<MenuButtonFx>();
            fx.Setup(b, t, style);
            return b;
        }

        public static Text LabelOf(Button b) => b.GetComponentInChildren<Text>();

        // ───────────────────────── Rows (settings) ─────────────────────────

        static RectTransform Row(Transform parent, string label, float y, string hint = null)
        {
            var row = UIKit.Place(UIKit.Node("Row " + label, parent), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(RowWidth, RowHeight));
            var line = UIKit.Box("Line", row, new Color(1f, 1f, 1f, 0.06f));
            UIKit.Place(line.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(RowWidth, 1f));
            var t = UIKit.Label("Label", row, 24, true, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, hint != null ? 8f : 0f), new Vector2(330f, 30f));
            t.text = label;
            if (hint != null)
            {
                var h = UIKit.Label("Hint", row, 16, false, UIKit.InkDim, TextAnchor.MiddleLeft);
                UIKit.Place(h.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -14f), new Vector2(400f, 20f));
                h.text = hint;
            }
            return row;
        }

        public static Slider SliderRow(Transform parent, string label, float y, float min, float max, float value,
                                       Func<float, string> format, Action<float> onChange, string hint = null)
        {
            var row = Row(parent, label, y, hint);

            var valueText = UIKit.Label("Value", row, 22, true, UIKit.Accent, TextAnchor.MiddleRight);
            UIKit.Place(valueText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(100f, 30f));

            var holder = UIKit.Place(UIKit.Node("Slider", row), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-116f, 0f), new Vector2(260f, 28f));
            var slider = holder.gameObject.AddComponent<Slider>();

            var track = UIKit.Box("Track", holder, UIKit.InkFaint, true, 8f);
            track.raycastTarget = true; // click anywhere on the track
            var tr = track.rectTransform;
            tr.anchorMin = new Vector2(0f, 0.5f); tr.anchorMax = new Vector2(1f, 0.5f); tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(0f, 6f); tr.anchoredPosition = Vector2.zero;

            var fillArea = UIKit.Node("Fill Area", holder);
            fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(0f, 6f); fillArea.anchoredPosition = Vector2.zero;
            var fill = UIKit.Box("Fill", fillArea, UIKit.Accent, true, 8f);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = UIKit.Node("Handle Area", holder);
            handleArea.anchorMin = new Vector2(0f, 0.5f); handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.sizeDelta = new Vector2(-22f, 22f); handleArea.anchoredPosition = Vector2.zero;
            var handle = UIKit.Box("Handle", handleArea, UIKit.Ink, true, 0.7f);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(22f, 0f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            var colors = slider.colors;
            colors.highlightedColor = UIKit.Accent;
            colors.pressedColor = UIKit.Accent;
            colors.selectedColor = Color.white;
            slider.colors = colors;

            valueText.text = format(value);
            slider.onValueChanged.AddListener(v => { valueText.text = format(v); onChange(v); });
            return slider;
        }

        public static void ToggleRow(Transform parent, string label, float y, bool value, Action<bool> onChange, string hint = null)
        {
            var row = Row(parent, label, y, hint);
            bool state = value;
            Button b = null;
            b = Button(row, "", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(110f, 38f), () =>
            {
                state = !state;
                Paint(b, state);
                onChange(state);
            }, Style.Panel, 20);
            Paint(b, state);

            void Paint(Button btn, bool on)
            {
                LabelOf(btn).text = on ? "ON" : "OFF";
                LabelOf(btn).color = on ? Dark : UIKit.InkDim;
                var c = btn.colors;
                c.normalColor = on ? UIKit.Accent : new Color(1f, 1f, 1f, 0.08f);
                c.selectedColor = c.normalColor;
                btn.colors = c;
            }
        }

        public static void ChoiceRow(Transform parent, string label, float y, string[] options, int index, Action<int> onChange, string hint = null)
        {
            var row = Row(parent, label, y, hint);
            int current = Mathf.Clamp(index, 0, Mathf.Max(0, options.Length - 1));

            var value = UIKit.Label("Value", row, 22, true, UIKit.Accent, TextAnchor.MiddleCenter);
            UIKit.Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-46f, 0f), new Vector2(200f, 30f));
            value.text = options.Length > 0 ? options[current] : "-";

            void Step(int dir)
            {
                if (options.Length == 0) return;
                current = (current + dir + options.Length) % options.Length;
                value.text = options[current];
                onChange(current);
            }
            Button(row, "<", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-254f, 0f), new Vector2(40f, 38f), () => Step(-1), Style.Panel, 22);
            Button(row, ">", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(40f, 38f), () => Step(1), Style.Panel, 22);
        }

        /// A section title inside a settings column ("VIDEO").
        public static void Header(Transform parent, string text, float y)
        {
            var t = UIKit.Label("Header " + text, parent, 20, true, UIKit.Accent, TextAnchor.LowerLeft);
            UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(RowWidth, 34f));
            t.text = text;
        }

        // ───────────────────────── Sprites ─────────────────────────

        static Sprite leftFade;

        /// Dark on the left, fading to clear on the right (behind the main menu buttons).
        public static Sprite LeftFade
        {
            get
            {
                if (leftFade != null) return leftFade;
                const int w = 256;
                var tex = new Texture2D(w, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[w * 4];
                for (int x = 0; x < w; x++)
                {
                    float t = x / (w - 1f);
                    byte a = (byte)(255f * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.25f, 1f, t)) * 0.92f);
                    for (int y = 0; y < 4; y++) px[y * w + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply();
                leftFade = Sprite.Create(tex, new Rect(0, 0, w, 4), new Vector2(0.5f, 0.5f));
                return leftFade;
            }
        }
    }
}
