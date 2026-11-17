using UnityEngine;
using UnityEngine.UI;

namespace LastCheckout.UI
{
    /// <summary>
    /// Tiny toolkit for building the HUD in code: fonts, colours, rounded panels, text, bars.
    /// Look & feel: dark translucent panels, one warm accent colour, condensed bold type, soft motion.
    /// </summary>
    public static class UIKit
    {
        // ---- Palette ----
        public static readonly Color Ink = new Color(0.96f, 0.96f, 0.94f);
        public static readonly Color InkDim = new Color(0.96f, 0.96f, 0.94f, 0.55f);
        public static readonly Color InkFaint = new Color(0.96f, 0.96f, 0.94f, 0.28f);
        public static readonly Color Accent = new Color(1f, 0.78f, 0.24f);       // warm yellow
        public static readonly Color Danger = new Color(1f, 0.3f, 0.22f);        // red-orange
        public static readonly Color Good = new Color(0.45f, 0.95f, 0.6f);
        public static readonly Color Panel = new Color(0.04f, 0.045f, 0.05f, 0.62f);
        public static readonly Color PanelSoft = new Color(0.04f, 0.045f, 0.05f, 0.4f);

        static Font regular, bold;
        static Sprite rounded, vignette;

        public static Font Regular
        {
            get
            {
                if (regular == null) regular = Resources.Load<Font>("Fonts/BarlowCondensed_600SemiBold");
                if (regular == null) regular = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return regular;
            }
        }

        public static Font Bold
        {
            get
            {
                if (bold == null) bold = Resources.Load<Font>("Fonts/BarlowCondensed_800ExtraBold");
                if (bold == null) bold = Regular;
                return bold;
            }
        }

        /// <summary>A white rounded rectangle that stretches without blurring its corners (9-slice).</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64, radius = 16;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        // distance outside the rounded corner → soft anti-aliased edge
                        float dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                        float dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                        float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                        byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                        px[y * size + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
                return rounded;
            }
        }

        /// <summary>Transparent in the middle, solid at the edges (for the red "you're hurt" glow).</summary>
        public static Sprite Vignette
        {
            get
            {
                if (vignette != null) return vignette;
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                        float d = Mathf.Sqrt(u * u * 0.8f + v * v);
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, d));
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                vignette = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                return vignette;
            }
        }

        // ---- Building blocks ----

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Place a rect: anchor = where on the parent it's pinned (0..1), pos = offset in pixels, size in pixels.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Box(string name, Transform parent, Color color, bool roundedCorners = false, float cornerScale = 2f)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (roundedCorners)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = cornerScale; // bigger = smaller corners
            }
            return img;
        }

        public static Text Label(string name, Transform parent, int size, bool isBold, Color color, TextAnchor align)
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = isBold ? Bold : Regular;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Soft drop shadow so text stays readable over bright scenery.</summary>
        public static T Shadowed<T>(T graphic, float alpha = 0.45f) where T : Graphic
        {
            var s = graphic.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, alpha);
            s.effectDistance = new Vector2(0f, -2f);
            return graphic;
        }

        /// <summary>Frame-rate independent smoothing: moves 'current' towards 'target' (higher speed = snappier).</summary>
        public static float Smooth(float current, float target, float speed) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        /// <summary>"0:07"</summary>
        public static string Clock(float seconds)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
