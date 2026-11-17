using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastCheckout.UI
{
    /// <summary>
    /// Small hover animation for menu buttons (works while the game is paused: uses real time).
    ///   Ghost buttons: a yellow bar grows on the left and the text slides right.
    ///   Other buttons: the text turns dark on the yellow hover colour so it stays readable.
    /// </summary>
    public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Button button;
        Text text;
        MenuKit.Style style;
        RectTransform bar;
        Color baseTextColor;
        float hover, textBaseX;
        bool over;

        public void Setup(Button b, Text t, MenuKit.Style s)
        {
            button = b;
            text = t;
            style = s;
            baseTextColor = t.color;
            textBaseX = t.rectTransform.offsetMin.x;
            if (s == MenuKit.Style.Ghost)
            {
                var img = UIKit.Box("Hover Bar", transform, UIKit.Accent, true, 8f);
                bar = UIKit.Place(img.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(4f, 0f));
            }
        }

        public void OnPointerEnter(PointerEventData e) => over = true;
        public void OnPointerExit(PointerEventData e) => over = false;
        void OnDisable() { over = false; hover = 0f; }

        void Update()
        {
            if (button == null || text == null) return;
            bool active = over && button.interactable;
            hover = UIKit.Smooth(hover, active ? 1f : 0f, 16f);

            if (style == MenuKit.Style.Ghost)
            {
                float h = ((RectTransform)transform).rect.height;
                bar.sizeDelta = new Vector2(4f, h * 0.6f * hover);
                var o = text.rectTransform.offsetMin;
                text.rectTransform.offsetMin = new Vector2(textBaseX + 10f * hover, o.y);
                text.color = Color.Lerp(button.interactable ? baseTextColor : UIKit.InkFaint, UIKit.Accent, hover);
            }
            else if (style == MenuKit.Style.Panel)
            {
                // Toggle buttons repaint their own text colour; only adjust ordinary panel buttons.
                if (text.text != "ON" && text.text != "OFF")
                    text.color = Color.Lerp(button.interactable ? UIKit.Ink : UIKit.InkFaint, new Color(0.06f, 0.06f, 0.06f), hover);
            }
        }
    }
}
