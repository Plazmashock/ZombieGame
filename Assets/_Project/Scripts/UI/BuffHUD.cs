using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastCheckout.Combat;

namespace LastCheckout.UI
{
    /// <summary>
    /// Active food buffs on the left side of the screen: name, what it gives, and a bar that drains.
    /// Has its own canvas so it doesn't touch GameHUD's layout.
    /// </summary>
    public class BuffHUD : MonoBehaviour
    {
        class Row { public RectTransform rt; public Image fill; public Text label; public PlayerBuffs.Active data; }

        PlayerBuffs buffs;
        RectTransform list;
        readonly List<Row> rows = new List<Row>();
        bool dirty = true;

        public void Init(GameObject player)
        {
            buffs = player.GetComponent<PlayerBuffs>();
            if (buffs == null) return;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            list = UIKit.Place(UIKit.Node("Buffs", transform), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, -60f), new Vector2(300f, 10f));
            buffs.Changed += () => dirty = true;
        }

        static string Describe(PlayerBuffs.Active a)
        {
            switch (a.kind)
            {
                case PlayerBuffs.Kind.Speed: return $"+{a.amount * 100f:0}% SPEED";
                case PlayerBuffs.Kind.Damage: return $"+{a.amount * 100f:0}% DAMAGE";
                case PlayerBuffs.Kind.FireRate: return $"+{a.amount * 100f:0}% FIRE RATE";
                case PlayerBuffs.Kind.Reload: return $"{a.amount * 100f:0}% FASTER RELOAD";
                case PlayerBuffs.Kind.Armor: return $"-{a.amount * 100f:0}% DAMAGE TAKEN";
                default: return $"+{a.amount:0.#} HP/S";
            }
        }

        void Rebuild()
        {
            dirty = false;
            foreach (var r in rows) if (r.rt != null) Destroy(r.rt.gameObject);
            rows.Clear();

            for (int i = 0; i < buffs.Actives.Count; i++)
            {
                var a = buffs.Actives[i];
                var rt = UIKit.Place(UIKit.Node("Buff", list), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -i * 58f), new Vector2(300f, 52f));
                var bg = rt.gameObject.AddComponent<Image>();
                bg.sprite = null; bg.color = UIKit.Panel; bg.raycastTarget = false;

                var stripe = UIKit.Box("Stripe", rt, a.color);
                UIKit.Place(stripe.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, 52f));

                var label = UIKit.Label("Text", rt, 17, true, UIKit.Ink, TextAnchor.UpperLeft);
                UIKit.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -6f), new Vector2(280f, 24f));
                UIKit.Shadowed(label);

                var track = UIKit.Box("Track", rt, new Color(1f, 1f, 1f, 0.12f));
                UIKit.Place(track.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 9f), new Vector2(272f, 6f));
                var fill = UIKit.Box("Fill", track.rectTransform, a.color);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = Vector2.one;
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

                rows.Add(new Row { rt = rt, fill = fill, label = label, data = a });
            }
        }

        void Update()
        {
            if (buffs == null) return;
            if (dirty) Rebuild();
            foreach (var r in rows)
            {
                if (r.data == null) continue;
                r.fill.rectTransform.anchorMax = new Vector2(r.data.Fraction, 1f);
                r.label.text = $"{r.data.name}  <color=#FFFFFF99>{Describe(r.data)}  {Mathf.CeilToInt(r.data.Left)}s</color>";
            }
        }
    }
}
