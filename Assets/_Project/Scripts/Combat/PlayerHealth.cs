using UnityEngine;
using UnityEngine.SceneManagement;
using LastCheckout.Interaction;
using LastCheckout.Player;

namespace LastCheckout.Combat
{
    /// <summary>
    /// The player's health.
    /// Zombies call TakeDamage when they hit you. At 0 you die, controls switch off,
    /// and a "YOU DIED" screen with a Restart button appears.
    /// Also draws a simple health bar bottom-left (placeholder until a real HUD exists).
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 100f;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public bool IsDead { get; private set; }

        /// <summary>Fires whenever health changes (a real HUD can listen to this later).</summary>
        public event System.Action Changed;
        /// <summary>Fires once when you die.</summary>
        public event System.Action Died;

        PlayerLook look;
        float hurtFlashUntil;
        GUIStyle labelStyle, titleStyle;

        void Awake()
        {
            Current = maxHealth;
            look = GetComponent<PlayerLook>();
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            var buffs = GetComponent<PlayerBuffs>();
            if (buffs != null) amount *= buffs.DamageTakenMult; // armor food

            Current = Mathf.Max(0f, Current - amount);
            hurtFlashUntil = Time.time + 0.35f; // red screen flash
            Changed?.Invoke();

            if (Current <= 0f) Die();
        }

        /// <summary>Permanent max-health boost (fortified cereal). Also heals that much.</summary>
        public void IncreaseMax(float amount)
        {
            if (IsDead || amount <= 0f) return;
            maxHealth += amount;
            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke();
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke();
        }

        void Die()
        {
            IsDead = true;
            Died?.Invoke();

            // Switch off everything the player can DO.
            SetEnabled<FirstPersonController>(false);
            SetEnabled<PlayerInteractor>(false);
            SetEnabled<PlayerMelee>(false);
            SetEnabled<PlayerShooting>(false);

            // Free the mouse so the Restart button can be clicked, and stop mouse-look.
            if (look != null)
            {
                look.IsUIOpen = true;
                look.SetCursorLocked(false);
            }
        }

        void SetEnabled<T>(bool value) where T : Behaviour
        {
            var c = GetComponent<T>();
            if (c != null) c.enabled = value;
        }

        // ---------- Placeholder HUD (drawn with Unity's simple OnGUI system) ----------

        void OnGUI()
        {
            if (UI.GameHUD.IsActive) return; // the new HUD draws health and the death screen
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                labelStyle.normal.textColor = Color.white;
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                titleStyle.normal.textColor = new Color(0.8f, 0.1f, 0.1f);
            }

            // Scale so the HUD looks the same at any screen size (we design for 720 pixels tall).
            Matrix4x4 oldMatrix = GUI.matrix; // put it back at the end so other HUDs aren't affected
            float s = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;
            const float h = 720f;

            // Red flash when hit
            if (Time.time < hurtFlashUntil)
                Fill(new Rect(0f, 0f, w, h), new Color(0.8f, 0f, 0f, 0.3f));

            // Health bar
            var bar = new Rect(24f, h - 44f, 260f, 22f);
            float f = maxHealth > 0f ? Current / maxHealth : 0f;
            Fill(bar, new Color(0f, 0f, 0f, 0.6f));
            Fill(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * f, bar.height - 4f),
                 Color.Lerp(new Color(0.8f, 0.1f, 0.1f), new Color(0.2f, 0.75f, 0.25f), f));
            GUI.Label(new Rect(bar.x + 8f, bar.y, bar.width, bar.height), $"HP  {Mathf.CeilToInt(Current)} / {Mathf.CeilToInt(maxHealth)}", labelStyle);

            // Death screen
            if (IsDead)
            {
                Fill(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.7f));
                GUI.Label(new Rect(0f, h * 0.3f, w, 100f), "YOU DIED", titleStyle);
                if (GUI.Button(new Rect(w * 0.5f - 90f, h * 0.55f, 180f, 44f), "Restart"))
                {
                    int index = SceneManager.GetActiveScene().buildIndex;
                    if (index >= 0) SceneManager.LoadScene(index);
                    else Debug.LogWarning("Scene isn't in Build Settings, so it can't be reloaded. File > Build Profiles > Add Open Scene.");
                }
            }

            GUI.matrix = oldMatrix;
        }

        static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
