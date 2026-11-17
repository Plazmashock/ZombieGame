using UnityEngine;
using LastCheckout.Interaction;
using LastCheckout.Player;

namespace LastCheckout.UI
{
    /// <summary>
    /// TEMPORARY placeholder HUD: a crosshair dot and the "[E] Open door" prompt.
    /// Drawn with Unity's simple OnGUI system so it needs zero setup.
    /// It will be replaced by a proper UI Canvas in Milestone 2 (when we add inventory UI).
    /// </summary>
    public class InteractionHUD : MonoBehaviour
    {
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] PlayerLook look;

        GUIStyle promptStyle, hintStyle;

        void Reset() => AutoFindReferences();
        void Awake() => AutoFindReferences();

        void AutoFindReferences()
        {
            if (interactor == null) interactor = GetComponent<PlayerInteractor>();
            if (look == null) look = GetComponent<PlayerLook>();
        }

        void OnGUI()
        {
            if (GameHUD.IsActive) return; // the new HUD draws the crosshair and prompts
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold };
                hintStyle = new GUIStyle(promptStyle) { fontSize = 16, fontStyle = FontStyle.Normal };
            }

            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (look != null && look.IsUIOpen) return; // a menu is open, don't show "click to play"
                GUI.Label(new Rect(cx - 300, cy - 20, 600, 40), "Click to play   •   Esc releases the mouse", hintStyle);
                return;
            }

            // Crosshair dot
            const float dot = 4f;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(cx - dot / 2, cy - dot / 2, dot, dot), Texture2D.whiteTexture);

            // Prompt
            if (interactor != null && interactor.HasTarget)
            {
                bool can = interactor.CanInteractWithTarget;
                GUI.color = can ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                string text = can ? $"[E]  {interactor.CurrentPrompt}" : interactor.CurrentPrompt;
                GUI.Label(new Rect(cx - 300, cy + 30, 600, 40), text, promptStyle);
            }
            GUI.color = Color.white;
        }
    }
}
