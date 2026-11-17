using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastCheckout.Combat;
using LastCheckout.Game;
using LastCheckout.Player;

namespace LastCheckout.UI
{
    /// <summary>
    /// Two things, both only between waves:
    ///
    ///  1) The ACTION BAR at the bottom centre of the screen:
    ///         [N]  START WAVE NOW   +$21        [U]  UPGRADES
    ///     N skips the rest of the waiting time and sends the zombies in right away.
    ///     You get a bonus for every second you skip ($1 per 2 s left).
    ///
    ///  2) The STORE UPGRADES screen (U, or E on the kiosk in the back corridor):
    ///     ┌──────────────────────────── STORE UPGRADES ─────────── $435 ┐
    ///     │ [AUTO TURRETS ●●○○]  [TURRET OVERCLOCK]  [BARBED WIRE]       │
    ///     │ [FLOODLIGHTS]        [ROOFTOP ●○]        [STOCKROOM ANNEX]   │
    ///     └─ the loot timer keeps running while you shop! ───── [U] CLOSE ┘
    /// </summary>
    public class UpgradeMenu : MonoBehaviour
    {
        public static UpgradeMenu Instance { get; private set; }
        public bool IsOpen { get; private set; }

        GameDirector director;
        StoreUpgrades store;
        PlayerWallet wallet;
        PlayerHealth health;
        PlayerInputReader input;
        InventoryHUD backpack;

        RectTransform root;
        CanvasGroup barGroup, panelGroup;
        Button skipButton, upgradesButton;
        Text skipText, upgradesText, barHint, panelCash, panelTimer;
        float hintUntil;

        class Card { public StoreUpgrades.Upgrade u; public Text tag, title, level, info, status; public Button buy; public Text buyText; public Image[] pips; }
        readonly List<Card> cards = new List<Card>();

        // ───────────────────────── Setup ─────────────────────────

        public void Init(GameDirector d, StoreUpgrades s, GameObject player)
        {
            Instance = this;
            director = d;
            store = s;
            wallet = player.GetComponent<PlayerWallet>();
            health = player.GetComponent<PlayerHealth>();
            input = player.GetComponent<PlayerInputReader>();
            backpack = player.GetComponent<InventoryHUD>();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            root = (RectTransform)transform;

            BuildBar();
            BuildPanel();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ───────────────────────── Action bar ─────────────────────────

        void BuildBar()
        {
            var bar = UIKit.Place(UIKit.Node("Action Bar", root), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(700f, 44f));
            barGroup = bar.gameObject.AddComponent<CanvasGroup>();
            barGroup.alpha = 0f;

            skipButton = MenuKit.Button(bar, "", new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-6f, 0f), new Vector2(380f, 44f),
                                        () => { if (director != null) director.SkipToWave(); }, MenuKit.Style.Panel, 20);
            skipText = MenuKit.LabelOf(skipButton);
            upgradesButton = MenuKit.Button(bar, "", new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(6f, 0f), new Vector2(230f, 44f),
                                            Open, MenuKit.Style.Panel, 20);
            upgradesText = MenuKit.LabelOf(upgradesButton);

            // The hint lives outside the bar, so it can show even when the bar is hidden (e.g. pressing U during a wave).
            barHint = UIKit.Shadowed(UIKit.Label("Hint", root, 20, true, UIKit.Danger, TextAnchor.UpperCenter));
            UIKit.Place(barHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(900f, 26f));
            barHint.color = UIKit.WithAlpha(UIKit.Danger, 0f);
        }

        void Hint(string text)
        {
            barHint.text = text;
            hintUntil = Time.unscaledTime + 2f;
        }

        // ───────────────────────── Upgrade panel ─────────────────────────

        void BuildPanel()
        {
            var dim = UIKit.Box("Upgrades", root, new Color(0.02f, 0.02f, 0.025f, 0.6f));
            var drt = UIKit.Stretch(dim.rectTransform);
            panelGroup = drt.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = panelGroup.interactable = false;

            var panel = UIKit.Box("Panel", drt, new Color(0.04f, 0.045f, 0.05f, 0.95f), true, 1.5f);
            var prt = UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1280f, 820f));

            var title = UIKit.Label("Title", prt, 54, true, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -30f), new Vector2(700f, 60f));
            title.text = "STORE UPGRADES";
            var sub = UIKit.Label("Sub", prt, 20, false, UIKit.InkDim, TextAnchor.UpperLeft);
            UIKit.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -92f), new Vector2(900f, 26f));
            sub.text = "Built instantly and kept for the whole run. Careful: the loot timer keeps running while you shop.";

            panelCash = UIKit.Label("Cash", prt, 54, true, UIKit.Accent, TextAnchor.UpperRight);
            UIKit.Place(panelCash.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -30f), new Vector2(400f, 60f));
            panelTimer = UIKit.Label("Timer", prt, 20, true, UIKit.InkDim, TextAnchor.UpperRight);
            UIKit.Place(panelTimer.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -92f), new Vector2(500f, 26f));

            // 3 × 2 grid of cards
            const float cw = 380f, ch = 290f, gap = 22f;
            for (int i = 0; i < store.All.Count && i < 6; i++)
            {
                int col = i % 3, row = i / 3;
                Vector2 pos = new Vector2(48f + col * (cw + gap), -140f - row * (ch + gap));
                cards.Add(BuildCard(prt, store.All[i], pos, new Vector2(cw, ch)));
            }

            var close = MenuKit.Button(prt, "CLOSE   [U]", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 30f), new Vector2(220f, 52f), Close, MenuKit.Style.Panel, 22);
            var tip = UIKit.Label("Tip", prt, 18, false, UIKit.InkDim, TextAnchor.MiddleLeft);
            UIKit.Place(tip.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(50f, 30f), new Vector2(900f, 52f));
            tip.text = "Turrets and traps earn you the kill money too.   Zombies will use your new stairs: hold the top!";
        }

        Card BuildCard(RectTransform parent, StoreUpgrades.Upgrade u, Vector2 pos, Vector2 size)
        {
            var c = new Card { u = u };
            var bg = UIKit.Box("Card " + u.title, parent, new Color(1f, 1f, 1f, 0.045f), true, 2.5f);
            var rt = UIKit.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            var strip = UIKit.Box("Strip", rt, u.color, true, 8f);
            UIKit.Place(strip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(56f, 4f));

            Vector2 tl = new Vector2(0f, 1f);
            c.tag = UIKit.Label("Tag", rt, 16, true, u.color, TextAnchor.UpperLeft);
            UIKit.Place(c.tag.rectTransform, tl, tl, new Vector2(18f, -26f), new Vector2(340f, 20f));
            c.tag.text = u.tag;
            c.title = UIKit.Label("Title", rt, 32, true, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Place(c.title.rectTransform, tl, tl, new Vector2(18f, -46f), new Vector2(340f, 38f));
            c.title.text = u.title;

            // Level pips: ● filled = owned levels
            c.pips = new Image[u.MaxLevel];
            for (int i = 0; i < u.MaxLevel; i++)
            {
                c.pips[i] = UIKit.Box("Pip", rt, UIKit.InkFaint, true, 0.6f);
                UIKit.Place(c.pips[i].rectTransform, tl, tl, new Vector2(20f + i * 22f, -94f), new Vector2(14f, 14f));
            }
            c.level = UIKit.Label("Level", rt, 16, true, UIKit.InkDim, TextAnchor.MiddleLeft);
            UIKit.Place(c.level.rectTransform, tl, tl, new Vector2(24f + u.MaxLevel * 22f, -87f), new Vector2(200f, 28f));

            c.info = UIKit.Label("Info", rt, 19, false, UIKit.InkDim, TextAnchor.UpperLeft);
            UIKit.Place(c.info.rectTransform, tl, tl, new Vector2(18f, -122f), new Vector2(size.x - 36f, 100f));
            c.info.horizontalOverflow = HorizontalWrapMode.Wrap;
            c.info.verticalOverflow = VerticalWrapMode.Truncate;
            c.info.lineSpacing = 1.05f;

            var upgrade = u;
            c.buy = MenuKit.Button(rt, "", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(size.x - 36f, 50f),
                                   () => { if (store.TryBuy(upgrade)) Refresh(); }, MenuKit.Style.Primary, 24);
            c.buyText = MenuKit.LabelOf(c.buy);
            c.status = UIKit.Label("Status", rt, 20, true, UIKit.Danger, TextAnchor.MiddleCenter);
            UIKit.Place(c.status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(size.x - 36f, 50f));
            return c;
        }

        void Refresh()
        {
            int cash = wallet != null ? wallet.Cash : 0;
            panelCash.text = "$" + cash;
            if (director != null && director.Current == GameDirector.Phase.Loot)
            {
                bool warn = director.TimeLeft <= director.WarningAt;
                panelTimer.text = $"LOOT PHASE  {UIKit.Clock(director.TimeLeft)}  ·  WAVE {director.Wave} INCOMING";
                panelTimer.color = warn ? UIKit.Danger : UIKit.InkDim;
            }
            else panelTimer.text = "";

            foreach (var c in cards)
            {
                var u = c.u;
                for (int i = 0; i < c.pips.Length; i++) c.pips[i].color = i < u.Level ? u.color : UIKit.InkFaint;
                c.level.text = u.Maxed ? "MAX" : $"LV {u.Level} / {u.MaxLevel}";
                c.info.text = u.Maxed ? "Fully upgraded." : (u.Level == 0 ? "" : "NEXT: ") + u.NextStep;

                string reason = store.BlockReason(u);
                bool buyable = reason == null;
                bool showButton = !u.Maxed && (buyable || reason.StartsWith("NEED $"));
                c.buy.gameObject.SetActive(showButton);
                c.buy.interactable = buyable;
                c.buyText.text = buyable ? $"BUY   ${u.NextPrice}" : $"${u.NextPrice}   ·   {reason}";
                c.buyText.color = buyable ? new Color(0.06f, 0.06f, 0.06f) : new Color(1f, 1f, 1f, 0.6f);
                c.status.gameObject.SetActive(!showButton);
                c.status.text = u.Maxed ? "MAXED" : reason;
                c.status.color = u.Maxed ? UIKit.Good : UIKit.Danger;
            }
        }

        // ───────────────────────── Open / close ─────────────────────────

        public void Open()
        {
            if (IsOpen || store == null) return;
            if (!store.ShopOpen) { Hint("UPGRADES ARE LOCKED DURING WAVES"); return; }
            if (health != null && health.IsDead) return;
            IsOpen = true;
            ControlLock.Acquire(this);
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ControlLock.Release(this);
        }

        // ───────────────────────── Every frame ─────────────────────────

        void Update()
        {
            if (director == null) return;
            bool dead = health != null && health.IsDead;
            bool between = director.HasStarted && (director.Current == GameDirector.Phase.Loot || director.Current == GameDirector.Phase.Cleared);

            // Auto-close when the wave starts (or you die).
            if (IsOpen && (!between || dead)) Close();

            // Keys (they work even while the upgrade screen is open; not while the pause/main menu is up).
            if (!MenuSystem.IsBlocking && !dead && input != null)
            {
                if (input.UpgradesPressed)
                {
                    if (IsOpen) Close();
                    else if (backpack == null || !backpack.IsOpen) Open();
                }
                if (input.SkipWavePressed)
                {
                    if (!between) { if (director.Current == GameDirector.Phase.Wave) Hint("THE WAVE IS ALREADY HERE"); }
                    else
                    {
                        string why = director.SkipBlockedReason;
                        if (why != null) Hint(why);
                        else { if (IsOpen) Close(); director.SkipToWave(); }
                    }
                }
            }

            if (IsOpen)
            {
                Refresh();
                if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            }

            // Action bar
            bool showBar = between && !dead && !MenuSystem.IsBlocking && !IsOpen;
            barGroup.alpha = UIKit.Smooth(barGroup.alpha, showBar ? 1f : 0f, 12f);
            barGroup.blocksRaycasts = barGroup.interactable = showBar;
            if (showBar)
            {
                string why = director.SkipBlockedReason;
                int bonus = director.SkipBonus;
                skipText.text = why == null
                    ? $"<color=#FFC83D>[N]</color>   START WAVE NOW{(bonus > 0 ? $"   <color=#7CFF8A>+${bonus}</color>" : "")}"
                    : $"<color=#F5F5F059>[N]   {why}</color>";
                skipButton.interactable = why == null;
                upgradesText.text = "<color=#FFC83D>[U]</color>   UPGRADES";
            }
            float hintA = Time.unscaledTime < hintUntil ? 1f : 0f;
            barHint.color = UIKit.WithAlpha(barHint.color, UIKit.Smooth(barHint.color.a, hintA, 12f));

            panelGroup.alpha = UIKit.Smooth(panelGroup.alpha, IsOpen ? 1f : 0f, 16f);
            panelGroup.blocksRaycasts = panelGroup.interactable = IsOpen;
        }
    }
}
