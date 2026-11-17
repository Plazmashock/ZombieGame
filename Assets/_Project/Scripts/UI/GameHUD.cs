using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using LastCheckout.Combat;
using LastCheckout.Game;
using LastCheckout.Interaction;
using LastCheckout.Inventory;
using LastCheckout.Items;
using LastCheckout.Player;

namespace LastCheckout.UI
{
    /// <summary>
    /// The in-game HUD, built in code with Unity's UI system (Canvas). Replaces the old placeholder OnGUI bits.
    ///
    ///   ┌ WAVE / KILLS                 LOOT PHASE  0:42  ━━━━                                     ┐
    ///   │                                  BIG BANNERS (wave start, cleared...)                    │
    ///   │                                         + crosshair                       pickup feed →  │
    ///   │                                     [E] Take Shotgun                                     │
    ///   └ 100 HP ━━━━━━  [H] HEAL x2  PACK ━                  SHOTGUN  6 / 18   [1]..[5] / [6]..[0] ┘
    ///   Money ($) sits under WAVE / KILLS; earnings and purchases pop up in the feed on the right.
    ///
    /// Everything fades and slides smoothly instead of popping on and off.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        /// <summary>True while this HUD is running. The old OnGUI HUDs check this and stay quiet.</summary>
        public static bool IsActive { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsActive = false;

        // References
        GameDirector director;
        PlayerHealth health;
        PlayerShooting shooting;
        PlayerMelee melee;
        PlayerInventory inventory;
        PlayerInteractor interactor;
        PlayerLook look;
        PlayerHealing healing;
        PlayerInputReader input;
        InventoryHUD backpackToggle;
        PlayerWallet wallet;

        RectTransform root;
        CanvasGroup hudGroup;

        static GameHUD instance;

        /// <summary>Shows a line in the pickup feed (food, buffs...). Safe to call when no HUD exists.</summary>
        public static void PushToast(string text)
        {
            if (instance != null && instance.feed != null) instance.AddToast(text);
        }

        void OnEnable() { IsActive = true; instance = this; }
        void OnDisable() { IsActive = false; if (instance == this) instance = null; }

        public void Init(GameDirector d, GameObject player)
        {
            director = d;
            health = player.GetComponent<PlayerHealth>();
            shooting = player.GetComponent<PlayerShooting>();
            melee = player.GetComponent<PlayerMelee>();
            inventory = player.GetComponent<PlayerInventory>();
            interactor = player.GetComponent<PlayerInteractor>();
            look = player.GetComponent<PlayerLook>();
            healing = player.GetComponent<PlayerHealing>();
            input = player.GetComponent<PlayerInputReader>();
            backpackToggle = player.GetComponent<InventoryHUD>();
            wallet = player.GetComponent<PlayerWallet>();

            EnsureEventSystem();
            BuildCanvas();
            BuildVignette();
            BuildPhase();
            BuildStats();
            BuildHealth();
            BuildWeapon();
            BuildCrosshair();
            BuildPrompt();
            BuildFeed();
            BuildBanner();
            BuildArmoryWarning();
            BuildResumeHint();
            BuildBackpack();
            BuildGameOver();

            if (health != null) { lastHealth = health.Current; health.Changed += OnHealthChanged; }
            if (inventory != null) { inventory.ItemAdded += OnItemAdded; inventory.Changed += OnInventoryChanged; }
            if (director != null) director.Announce += ShowBanner;
            if (wallet != null) { wallet.Earned += OnEarned; wallet.Spent += OnSpent; }
        }

        void OnDestroy()
        {
            if (health != null) health.Changed -= OnHealthChanged;
            if (inventory != null) { inventory.ItemAdded -= OnItemAdded; inventory.Changed -= OnInventoryChanged; }
            if (director != null) director.Announce -= ShowBanner;
            if (wallet != null) { wallet.Earned -= OnEarned; wallet.Spent -= OnSpent; }
        }

        static void EnsureEventSystem()
        {
            if (LevelKit.FindFirst<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions(); // mouse clicks work on buttons without any setup
        }

        void BuildCanvas()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);  // designed at 1080p, scales to any screen
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = (RectTransform)transform;
            var hud = UIKit.Stretch(UIKit.Node("HUD", root));
            hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            hudGroup.blocksRaycasts = false;
            hudGroup.interactable = false;
        }

        Transform Hud => hudGroup.transform;

        // ───────────────────────── Damage vignette ─────────────────────────

        Image vignette;
        float damageFlash, lastHealth;

        void BuildVignette()
        {
            vignette = UIKit.Box("Damage Vignette", root, UIKit.WithAlpha(UIKit.Danger, 0f));
            vignette.sprite = UIKit.Vignette;
            UIKit.Stretch(vignette.rectTransform);
        }

        void OnHealthChanged()
        {
            if (health.Current < lastHealth) damageFlash = 1f;
            lastHealth = health.Current;
        }

        void UpdateVignette()
        {
            damageFlash = Mathf.MoveTowards(damageFlash, 0f, Time.unscaledDeltaTime * 2.2f);
            float hpFrac = health != null ? health.Current / Mathf.Max(1f, health.Max) : 1f;
            float lowPulse = hpFrac < 0.3f && !health.IsDead ? (0.25f + 0.15f * Mathf.Sin(Time.time * 5f)) * (1f - hpFrac / 0.3f) : 0f;
            float a = Mathf.Max(damageFlash * 0.55f, lowPulse);
            vignette.color = UIKit.WithAlpha(UIKit.Danger, a);
        }

        // ───────────────────────── Phase / timer (top centre) ─────────────────────────

        Text phaseLabel, phaseBig, phaseSub;
        RectTransform phaseBarFill, phaseBigRt;
        Image phaseBarFillImg;
        float phaseBarShown = 1f;

        void BuildPhase()
        {
            var box = UIKit.Place(UIKit.Node("Phase", Hud), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(600f, 170f));

            phaseLabel = UIKit.Shadowed(UIKit.Label("Phase Label", box, 24, true, UIKit.Accent, TextAnchor.UpperCenter));
            UIKit.Place(phaseLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(600f, 30f));

            phaseBig = UIKit.Shadowed(UIKit.Label("Phase Big", box, 80, true, UIKit.Ink, TextAnchor.MiddleCenter));
            phaseBigRt = UIKit.Place(phaseBig.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -72f), new Vector2(600f, 84f));

            var barBg = UIKit.Box("Phase Bar", box, UIKit.InkFaint, true, 6f);
            UIKit.Place(barBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(300f, 5f));
            phaseBarFillImg = UIKit.Box("Fill", barBg.transform, UIKit.Accent, true, 6f);
            phaseBarFill = UIKit.Stretch(phaseBarFillImg.rectTransform);

            phaseSub = UIKit.Shadowed(UIKit.Label("Phase Sub", box, 22, false, UIKit.InkDim, TextAnchor.UpperCenter));
            UIKit.Place(phaseSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(600f, 28f));
        }

        void UpdatePhase()
        {
            if (director == null) return;
            float bar = 1f;
            Color barColor = UIKit.Accent;
            float bigScale = 1f;

            switch (director.Current)
            {
                case GameDirector.Phase.Loot:
                {
                    bool warn = director.TimeLeft <= director.WarningAt;
                    phaseLabel.text = "LOOT PHASE";
                    phaseLabel.color = warn ? UIKit.Danger : UIKit.Accent;
                    phaseBig.text = UIKit.Clock(director.TimeLeft);
                    phaseBig.color = warn ? UIKit.Danger : UIKit.Ink;
                    phaseSub.text = warn ? "ARMORY SEALING  ·  GET OUT OF THE GUN ROOM" : $"ARMORY OPEN  ·  WAVE {director.Wave} INCOMING";
                    bar = director.TimeLeft / Mathf.Max(1f, director.LootDuration);
                    if (warn) { barColor = UIKit.Danger; bigScale = 1f + 0.06f * Mathf.Max(0f, Mathf.Sin(Time.time * Mathf.PI * 2f)); }
                    break;
                }
                case GameDirector.Phase.Wave:
                {
                    int left = director.ZombiesRemaining;
                    phaseLabel.text = $"WAVE {director.Wave}";
                    phaseLabel.color = UIKit.Danger;
                    phaseBig.text = left.ToString();
                    phaseBig.color = UIKit.Ink;
                    phaseSub.text = left == 1 ? "HOSTILE REMAINING" : "HOSTILES REMAINING";
                    int total = Mathf.Max(1, director.WaveTotal);
                    bar = Mathf.Clamp01((float)left / total);
                    barColor = UIKit.Danger;
                    break;
                }
                case GameDirector.Phase.Cleared:
                    phaseLabel.text = $"WAVE {director.Wave}";
                    phaseLabel.color = UIKit.Good;
                    phaseBig.text = "CLEAR";
                    phaseBig.color = UIKit.Good;
                    phaseSub.text = "RESTOCKING...";
                    bar = 0f;
                    barColor = UIKit.Good;
                    break;
                case GameDirector.Phase.GameOver:
                    break;
            }

            phaseBarShown = UIKit.Smooth(phaseBarShown, bar, 10f);
            phaseBarFill.anchorMax = new Vector2(Mathf.Clamp01(phaseBarShown), 1f);
            phaseBarFillImg.color = Color.Lerp(phaseBarFillImg.color, barColor, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            phaseBigRt.localScale = Vector3.one * bigScale;
        }

        // ───────────────────────── Stats (top left) ─────────────────────────

        Text stats;
        float cashShown = -1f;

        void BuildStats()
        {
            stats = UIKit.Shadowed(UIKit.Label("Stats", Hud, 22, true, UIKit.Ink, TextAnchor.UpperLeft));
            UIKit.Place(stats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -34f), new Vector2(400f, 120f));
            stats.lineSpacing = 1.1f;
        }

        void UpdateStats()
        {
            if (director == null) return;
            int cash = wallet != null ? wallet.Cash : 0;
            if (cashShown < 0f) cashShown = cash;
            cashShown = Mathf.MoveTowards(cashShown, cash, Mathf.Max(60f, Mathf.Abs(cash - cashShown) * 6f) * Time.unscaledDeltaTime); // counts up/down
            stats.text = $"<color=#F5F5F08C>WAVE</color>  {director.Wave}\n<color=#F5F5F08C>KILLS</color>  {director.Kills}\n<size=40><color=#FFC83D>${Mathf.RoundToInt(cashShown)}</color></size>";
        }

        // ───────────────────────── Health, meds, backpack (bottom left) ─────────────────────────

        Text hpText, medsText, packText;
        RectTransform hpFill, hpGhost, packFill;
        Image hpFillImg, packFillImg;
        float hpShown = 1f, hpGhostShown = 1f, packShown;

        void BuildHealth()
        {
            var box = UIKit.Place(UIKit.Node("Health", Hud), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 40f), new Vector2(380f, 170f));

            packText = UIKit.Shadowed(UIKit.Label("Pack Text", box, 18, false, UIKit.InkDim, TextAnchor.LowerLeft));
            UIKit.Place(packText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, 10f), new Vector2(360f, 22f));
            var packBg = UIKit.Box("Pack Bar", box, UIKit.InkFaint, true, 8f);
            UIKit.Place(packBg.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, 0f), new Vector2(360f, 4f));
            packFillImg = UIKit.Box("Fill", packBg.transform, UIKit.InkDim, true, 8f);
            packFill = UIKit.Stretch(packFillImg.rectTransform);

            medsText = UIKit.Shadowed(UIKit.Label("Meds", box, 20, true, UIKit.Ink, TextAnchor.LowerLeft));
            UIKit.Place(medsText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, 42f), new Vector2(360f, 24f));

            var hpBg = UIKit.Box("HP Bar", box, new Color(1f, 1f, 1f, 0.16f), true, 4f);
            UIKit.Place(hpBg.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, 76f), new Vector2(360f, 10f));
            var ghost = UIKit.Box("Ghost", hpBg.transform, new Color(1f, 0.85f, 0.75f, 0.45f), true, 4f);
            hpGhost = UIKit.Stretch(ghost.rectTransform);
            hpFillImg = UIKit.Box("Fill", hpBg.transform, UIKit.Ink, true, 4f);
            hpFill = UIKit.Stretch(hpFillImg.rectTransform);

            hpText = UIKit.Shadowed(UIKit.Label("HP", box, 64, true, UIKit.Ink, TextAnchor.LowerLeft));
            UIKit.Place(hpText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(-3f, 86f), new Vector2(360f, 70f));
        }

        void UpdateHealth()
        {
            if (health == null) return;
            float f = Mathf.Clamp01(health.Current / Mathf.Max(1f, health.Max));
            hpShown = UIKit.Smooth(hpShown, f, 14f);
            // The pale "ghost" bar trails behind, so you can see how much a hit took.
            hpGhostShown = hpGhostShown > hpShown ? Mathf.MoveTowards(hpGhostShown, hpShown, Time.unscaledDeltaTime * 0.35f) : hpShown;
            hpFill.anchorMax = new Vector2(hpShown, 1f);
            hpGhost.anchorMax = new Vector2(hpGhostShown, 1f);
            bool low = f < 0.3f;
            hpFillImg.color = low ? UIKit.Danger : UIKit.Ink;
            hpText.text = $"{Mathf.CeilToInt(health.Current)}<size=26><color=#F5F5F08C>  HP</color></size>";
            hpText.color = low ? UIKit.Danger : UIKit.Ink;

            if (healing != null)
            {
                int meds = healing.MedCount;
                if (healing.IsHealing) { medsText.text = $"HEALING  {Mathf.RoundToInt(healing.Progress * 100f)}%"; medsText.color = UIKit.Good; }
                else if (meds > 0) { medsText.text = $"<color=#FFC83D>[H]</color>  HEAL   x{meds}"; medsText.color = UIKit.Ink; }
                else { medsText.text = "NO MEDS"; medsText.color = UIKit.InkFaint; }
            }
            else medsText.text = "";

            if (inventory != null)
            {
                packShown = UIKit.Smooth(packShown, inventory.LoadFraction, 10f);
                packFill.anchorMax = new Vector2(packShown, 1f);
                bool heavy = inventory.LoadFraction > 0.85f;
                packFillImg.color = heavy ? UIKit.Accent : UIKit.InkDim;
                packText.text = $"PACK  {inventory.CurrentWeight:0.0} / {inventory.MaxWeight:0} KG{(heavy ? "  ·  HEAVY" : "")}      <color=#F5F5F059>[TAB]</color>";
            }
        }

        // ───────────────────────── Weapon (bottom right) ─────────────────────────

        Text weaponName, ammoText, reloadLabel;
        RectTransform reloadFill;
        CanvasGroup reloadGroup;
        // CS / Valorant style loadout: one weapon per slot. 1 = primary, 2 = sidearm, 3 = melee.
        class LoadoutRow { public Image bg; public Text key, name, ammo; public float scale = 1f; }
        readonly LoadoutRow[] loadout = new LoadoutRow[3];
        static readonly WeaponGroup[] LoadoutGroups = { WeaponGroup.Primary, WeaponGroup.Sidearm, WeaponGroup.Melee };
        CanvasGroup loadoutGroup;
        static readonly Color DarkInk = new Color(0.07f, 0.07f, 0.06f);

        void BuildWeapon()
        {
            var box = UIKit.Place(UIKit.Node("Weapon", Hud), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 40f), new Vector2(560f, 240f));

            // The loadout list on the right edge (like CS): three rows, the weapon in your hands is highlighted.
            //     [1] ASSAULT RIFLE   30 / 90
            //     [2] PISTOL          12 / 24
            //     [3] BASEBALL BAT
            var list = UIKit.Place(UIKit.Node("Loadout", Hud), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, -30f), new Vector2(310f, 150f));
            loadoutGroup = list.gameObject.AddComponent<CanvasGroup>();
            for (int i = 0; i < 3; i++)
            {
                var row = new LoadoutRow();
                row.bg = UIKit.Box($"Slot {i + 1}", list, UIKit.PanelSoft, true, 3f);
                UIKit.Place(row.bg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -i * 52f), new Vector2(310f, 44f));
                row.key = UIKit.Label("Key", row.bg.transform, 24, true, UIKit.Accent, TextAnchor.MiddleCenter);
                UIKit.Place(row.key.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(38f, 44f));
                row.key.text = WeaponCatalog.GroupKey(LoadoutGroups[i]);
                row.name = UIKit.Label("Name", row.bg.transform, 22, true, UIKit.Ink, TextAnchor.MiddleLeft);
                UIKit.Place(row.name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(180f, 44f));
                row.ammo = UIKit.Label("Ammo", row.bg.transform, 19, true, UIKit.InkDim, TextAnchor.MiddleRight);
                UIKit.Place(row.ammo.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(96f, 44f));
                loadout[i] = row;
            }

            reloadGroup = UIKit.Node("Reload", box).gameObject.AddComponent<CanvasGroup>();
            var rg = (RectTransform)reloadGroup.transform;
            UIKit.Place(rg, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(240f, 24f));
            var rbg = UIKit.Box("Reload Bar", rg, UIKit.InkFaint, true, 8f);
            UIKit.Place(rbg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(240f, 4f));
            reloadFill = UIKit.Stretch(UIKit.Box("Fill", rbg.transform, UIKit.Accent, true, 8f).rectTransform);
            reloadLabel = UIKit.Label("Reload Label", rg, 16, true, UIKit.Accent, TextAnchor.LowerRight);
            UIKit.Place(reloadLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 8f), new Vector2(240f, 20f));
            reloadLabel.text = "RELOADING";

            ammoText = UIKit.Shadowed(UIKit.Label("Ammo", box, 76, true, UIKit.Ink, TextAnchor.LowerRight));
            UIKit.Place(ammoText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 38f), new Vector2(560f, 80f));

            weaponName = UIKit.Shadowed(UIKit.Label("Weapon Name", box, 26, true, UIKit.Accent, TextAnchor.LowerRight));
            UIKit.Place(weaponName.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 122f), new Vector2(560f, 30f));
        }

        void UpdateWeapon()
        {
            if (shooting == null) return;
            var w = shooting.Current;

            if (w == null || w.IsMelee)
            {
                weaponName.text = w != null ? w.displayName : "FISTS";
                float dmg = melee != null ? melee.CurrentDamage() : 15f;
                ammoText.text = $"{dmg:0}<size=34><color=#F5F5F08C>  DMG</color></size>";
            }
            else
            {
                weaponName.text = w.displayName;
                int loaded = shooting.Loaded, reserve = shooting.Reserve;
                string loadedColor = loaded == 0 ? "#FF4D38" : (loaded <= Mathf.CeilToInt(w.magazineSize * 0.25f) ? "#FFC83D" : "#F5F5F0");
                ammoText.text = $"<color={loadedColor}>{loaded}</color><size=34><color=#F5F5F08C>  / {reserve}</color></size>";
            }

            // The bar above the ammo shows reloading, or how far the bow is drawn.
            bool drawing = shooting.IsDrawing;
            reloadLabel.text = drawing ? (shooting.DrawAmount >= 1f ? "FULL DRAW" : "DRAWING") : (w != null && w.kind == WeaponKind.Bow ? "NOCKING" : "RELOADING");
            float targetReload = shooting.IsReloading || drawing ? 1f : 0f;
            reloadGroup.alpha = UIKit.Smooth(reloadGroup.alpha, targetReload, 14f);
            reloadFill.anchorMax = new Vector2(drawing ? shooting.DrawAmount : shooting.ReloadProgress, 1f);

            // Loadout list: bright for a couple of seconds after you switch, then fades back a little.
            bool justSwitched = Time.time - shooting.EquippedAt < 2.2f;
            loadoutGroup.alpha = UIKit.Smooth(loadoutGroup.alpha, justSwitched ? 1f : 0.6f, 8f);
            for (int i = 0; i < 3; i++)
            {
                var row = loadout[i];
                WeaponSpec spec = shooting.WeaponIn(LoadoutGroups[i]);
                bool has = spec != null;
                bool selected = has && w == spec;

                row.name.text = has ? spec.displayName : "EMPTY";
                row.ammo.text = has && !spec.IsMelee ? $"{shooting.LoadedOf(spec)} / {shooting.ReserveOf(spec)}" : "";

                Color bg = selected ? UIKit.Accent : has ? UIKit.PanelSoft : new Color(0f, 0f, 0f, 0.18f);
                Color fg = selected ? DarkInk : has ? UIKit.Ink : UIKit.InkFaint;
                float k = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
                row.bg.color = Color.Lerp(row.bg.color, bg, k);
                row.name.color = fg;
                row.key.color = selected ? DarkInk : has ? UIKit.Accent : UIKit.InkFaint;
                row.ammo.color = selected ? new Color(0.07f, 0.07f, 0.06f, 0.75f) : UIKit.InkDim;

                // The selected row pops out a little (grows from its top-right corner).
                row.scale = UIKit.Smooth(row.scale, selected ? 1.07f : 1f, 16f);
                row.bg.rectTransform.localScale = new Vector3(row.scale, row.scale, 1f);
            }
        }

        // ───────────────────────── Crosshair + hitmarker (centre) ─────────────────────────

        RectTransform[] crossLines;
        RectTransform hitRoot;
        Image[] hitLines;
        Image crossDot;
        float crossGap = 8f;
        CanvasGroup crossGroup;
        Text ammoHint;

        void BuildCrosshair()
        {
            var c = UIKit.Place(UIKit.Node("Crosshair", Hud), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            crossGroup = c.gameObject.AddComponent<CanvasGroup>();

            crossDot = UIKit.Shadowed(UIKit.Box("Dot", c, UIKit.Ink), 0.6f);
            UIKit.Place(crossDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 3f));

            crossLines = new RectTransform[4];
            Vector2[] sizes = { new Vector2(2f, 10f), new Vector2(2f, 10f), new Vector2(10f, 2f), new Vector2(10f, 2f) };
            for (int i = 0; i < 4; i++)
            {
                var line = UIKit.Shadowed(UIKit.Box($"Line {i}", c, UIKit.Ink), 0.6f);
                crossLines[i] = UIKit.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, sizes[i]);
            }

            hitRoot = UIKit.Place(UIKit.Node("Hitmarker", c), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            hitLines = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var arm = UIKit.Node($"Arm {i}", hitRoot);
                UIKit.Place(arm, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                arm.localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);
                hitLines[i] = UIKit.Shadowed(UIKit.Box("Line", arm, UIKit.Ink), 0.6f);
                UIKit.Place(hitLines[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(2.5f, 9f));
            }

            ammoHint = UIKit.Shadowed(UIKit.Label("Ammo Hint", Hud, 22, true, UIKit.Accent, TextAnchor.MiddleCenter));
            UIKit.Place(ammoHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(500f, 30f));
        }

        void UpdateCrosshair()
        {
            bool gun = shooting != null && shooting.IsEquipped && !shooting.HoldingMelee;
            float spread = gun ? shooting.CurrentSpread : 0f;
            float targetGap = gun ? 6f + spread * 7f : 4f;
            crossGap = UIKit.Smooth(crossGap, targetGap, 20f);

            crossLines[0].anchoredPosition = new Vector2(0f, crossGap + 5f);
            crossLines[1].anchoredPosition = new Vector2(0f, -crossGap - 5f);
            crossLines[2].anchoredPosition = new Vector2(-crossGap - 5f, 0f);
            crossLines[3].anchoredPosition = new Vector2(crossGap + 5f, 0f);
            foreach (var l in crossLines) l.gameObject.SetActive(gun);

            bool show = look != null && look.IsCursorLocked && !look.IsUIOpen;
            crossGroup.alpha = UIKit.Smooth(crossGroup.alpha, show ? 1f : 0f, 18f);

            // Hitmarker: white X on a hit, red and bigger on a kill. Pops in, fades out.
            float sinceHit = shooting != null ? Time.time - shooting.LastHitTime : 99f;
            bool kill = shooting != null && Time.time - shooting.LastKillTime < 0.35f;
            float a = Mathf.Clamp01(1f - sinceHit / (kill ? 0.45f : 0.25f));
            float s = 1f + 0.35f * a * (kill ? 1.5f : 1f);
            hitRoot.localScale = Vector3.one * s;
            foreach (var l in hitLines) l.color = UIKit.WithAlpha(kill ? UIKit.Danger : UIKit.Ink, a);

            // "RELOAD" / "NO AMMO" hint under the crosshair
            string hint = "";
            Color hc = UIKit.Accent;
            if (gun && !shooting.IsReloading)
            {
                var w = shooting.Current;
                if (shooting.Loaded == 0 && shooting.Reserve == 0) { hint = $"NO {w.ammoName}  ·  BUY IT IN THE ARMORY"; hc = UIKit.Danger; }
                else if (shooting.Loaded == 0 && w.kind != WeaponKind.Bow) hint = "RELOAD  [R]";
                else if (w.kind != WeaponKind.Bow && shooting.Loaded <= Mathf.CeilToInt(w.magazineSize * 0.25f) && shooting.Reserve > 0) hint = "LOW AMMO  [R]";
            }
            if (hint.Length > 0) { ammoHint.text = hint; ammoHint.color = UIKit.WithAlpha(hc, UIKit.Smooth(ammoHint.color.a, 1f, 14f)); }
            else ammoHint.color = UIKit.WithAlpha(ammoHint.color, UIKit.Smooth(ammoHint.color.a, 0f, 14f));
        }

        // ───────────────────────── Interaction prompt ─────────────────────────

        CanvasGroup promptGroup;
        RectTransform promptKey;
        Text promptText, promptKeyText;
        string lastPrompt = "";

        void BuildPrompt()
        {
            var box = UIKit.Place(UIKit.Node("Prompt", Hud), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -96f), new Vector2(800f, 40f));
            promptGroup = box.gameObject.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0f;

            var key = UIKit.Box("Key", box, UIKit.Ink, true, 3f);
            promptKey = UIKit.Place(key.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            promptKeyText = UIKit.Label("E", key.transform, 22, true, new Color(0.06f, 0.06f, 0.06f), TextAnchor.MiddleCenter);
            UIKit.Stretch(promptKeyText.rectTransform);
            promptKeyText.text = "E";

            promptText = UIKit.Shadowed(UIKit.Label("Text", box, 26, true, UIKit.Ink, TextAnchor.MiddleLeft));
            UIKit.Place(promptText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(700f, 40f));
        }

        void UpdatePrompt()
        {
            bool free = look == null || (look.IsCursorLocked && !look.IsUIOpen);
            bool has = interactor != null && interactor.enabled && interactor.HasTarget && free;
            if (has)
            {
                bool can = interactor.CanInteractWithTarget;
                string text = interactor.CurrentPrompt.ToUpperInvariant();
                if (text != lastPrompt)
                {
                    lastPrompt = text;
                    promptText.text = text;
                }
                promptKey.gameObject.SetActive(can);
                promptText.color = can ? UIKit.Ink : UIKit.InkDim;

                // Centre "[E] text" as one group.
                float tw = promptText.preferredWidth;
                float keyW = can ? 34f + 12f : 0f;
                float left = -(tw + keyW) * 0.5f;
                promptKey.anchoredPosition = new Vector2(left + 17f, 0f);
                promptText.rectTransform.anchoredPosition = new Vector2(left + keyW, 0f);
            }
            promptGroup.alpha = UIKit.Smooth(promptGroup.alpha, has ? 1f : 0f, 18f);
        }

        // ───────────────────────── Pickup feed (right) ─────────────────────────

        class Toast { public RectTransform rt; public CanvasGroup group; public float born; public float y; }
        readonly List<Toast> toasts = new List<Toast>();
        RectTransform feed;

        void BuildFeed()
        {
            feed = UIKit.Place(UIKit.Node("Pickup Feed", Hud), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-48f, -20f), new Vector2(340f, 10f));
        }

        void OnItemAdded(ItemDefinition item, int count) =>
            AddToast($"<color=#FFC83D>+{count}</color>   {item.displayName.ToUpperInvariant()}");

        void OnEarned(int amount, string reason) => AddToast($"<color=#7CFF8A>+${amount}</color>   {reason.ToUpperInvariant()}");
        void OnSpent(int amount, string what) => AddToast($"<color=#FF6B5A>-${amount}</color>   {what.ToUpperInvariant()}");

        void AddToast(string text)
        {
            var bg = UIKit.Box("Toast", feed, UIKit.Panel, true, 3f);
            var rt = UIKit.Place(bg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(80f, 0f), new Vector2(320f, 40f));
            var strip = UIKit.Box("Accent", rt, UIKit.Accent, true, 8f);
            UIKit.Place(strip.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(4f, 24f));
            var t = UIKit.Label("Text", rt, 22, true, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(290f, 40f));
            t.text = text;
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;

            toasts.Insert(0, new Toast { rt = rt, group = g, born = Time.unscaledTime, y = 0f });
            while (toasts.Count > 5) { Destroy(toasts[toasts.Count - 1].rt.gameObject); toasts.RemoveAt(toasts.Count - 1); }
        }

        void UpdateFeed()
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                float age = Time.unscaledTime - t.born;
                const float life = 2.8f;
                if (age > life) { Destroy(t.rt.gameObject); toasts.RemoveAt(i); continue; }

                float targetAlpha = age < life - 0.4f ? 1f : 0f;
                t.group.alpha = UIKit.Smooth(t.group.alpha, targetAlpha, 14f);
                t.y = UIKit.Smooth(t.y, -i * 46f, 14f);
                float x = UIKit.Smooth(t.rt.anchoredPosition.x, 0f, 16f);
                t.rt.anchoredPosition = new Vector2(x, t.y);
            }
        }

        // ───────────────────────── Banner (big announcements) ─────────────────────────

        CanvasGroup bannerGroup;
        RectTransform bannerRt, bannerLine;
        Text bannerTitle, bannerSub;
        Image bannerLineImg;
        float bannerShownAt = -100f;
        const float BannerHold = 2.8f;

        void BuildBanner()
        {
            bannerRt = UIKit.Place(UIKit.Node("Banner", Hud), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1200f, 200f));
            bannerGroup = bannerRt.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;

            bannerTitle = UIKit.Shadowed(UIKit.Label("Title", bannerRt, 92, true, UIKit.Ink, TextAnchor.MiddleCenter), 0.6f);
            UIKit.Place(bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1200f, 100f));

            bannerLineImg = UIKit.Box("Line", bannerRt, UIKit.Accent, true, 8f);
            bannerLine = UIKit.Place(bannerLineImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -26f), new Vector2(140f, 4f));

            bannerSub = UIKit.Shadowed(UIKit.Label("Subtitle", bannerRt, 26, false, UIKit.InkDim, TextAnchor.MiddleCenter));
            UIKit.Place(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -58f), new Vector2(1200f, 34f));
        }

        void ShowBanner(string title, string subtitle, bool danger)
        {
            bannerTitle.text = title;
            bannerSub.text = subtitle;
            bannerTitle.color = danger ? UIKit.Danger : UIKit.Ink;
            bannerLineImg.color = danger ? UIKit.Danger : UIKit.Accent;
            bannerShownAt = Time.unscaledTime;
        }

        void UpdateBanner()
        {
            float age = Time.unscaledTime - bannerShownAt;
            float alpha;
            if (age < 0.25f) alpha = age / 0.25f;                                    // fade in
            else if (age < BannerHold) alpha = 1f;                                   // hold
            else alpha = Mathf.Clamp01(1f - (age - BannerHold) / 0.45f);             // fade out
            bannerGroup.alpha = alpha;

            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.35f), 3f);       // ease-out
            bannerRt.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, ease);
            bannerLine.sizeDelta = new Vector2(Mathf.Lerp(0f, 160f, Mathf.Clamp01(age / 0.5f)), 4f);
        }

        // ───────────────────────── "Get out of the armory!" ─────────────────────────

        Text armoryWarning;

        void BuildArmoryWarning()
        {
            armoryWarning = UIKit.Shadowed(UIKit.Label("Armory Warning", Hud, 44, true, UIKit.Danger, TextAnchor.MiddleCenter), 0.7f);
            UIKit.Place(armoryWarning.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1200f, 60f));
        }

        void UpdateArmoryWarning()
        {
            bool show = director != null && director.Current == GameDirector.Phase.Loot
                        && director.TimeLeft <= director.WarningAt && director.PlayerInArmory;
            float flash = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
            if (show) armoryWarning.text = $"GET OUT OF THE ARMORY   {UIKit.Clock(director.TimeLeft)}";
            armoryWarning.color = UIKit.WithAlpha(UIKit.Danger, show ? flash : UIKit.Smooth(armoryWarning.color.a, 0f, 12f));
        }

        // ───────────────────────── "Click to resume" ─────────────────────────

        CanvasGroup resumeGroup;

        void BuildResumeHint()
        {
            var box = UIKit.Place(UIKit.Node("Resume", Hud), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(600f, 120f));
            resumeGroup = box.gameObject.AddComponent<CanvasGroup>();
            var bg = UIKit.Box("Back", box, UIKit.Panel, true, 2f);
            UIKit.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 104f));
            var t = UIKit.Label("Title", box, 36, true, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(600f, 44f));
            t.text = "CLICK TO RESUME";
            var s = UIKit.Label("Sub", box, 20, false, UIKit.InkDim, TextAnchor.MiddleCenter);
            UIKit.Place(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(600f, 28f));
            s.text = "ESC RELEASES THE MOUSE";
        }

        void UpdateResumeHint()
        {
            bool show = look != null && !look.IsCursorLocked && !look.IsUIOpen && (health == null || !health.IsDead);
            resumeGroup.alpha = UIKit.Smooth(resumeGroup.alpha, show ? 1f : 0f, 14f);
        }

        // ───────────────────────── Backpack screen (Tab) ─────────────────────────

        CanvasGroup packGroup;
        RectTransform packList;
        Text packTitleWeight;
        RectTransform packBarFill;
        bool packDirty = true;
        const int MaxRows = 9;

        void BuildBackpack()
        {
            var panel = UIKit.Box("Backpack", root, new Color(0.04f, 0.045f, 0.05f, 0.88f), true, 1.5f);
            var rt = UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 680f));
            packGroup = rt.gameObject.AddComponent<CanvasGroup>();
            packGroup.alpha = 0f;
            packGroup.blocksRaycasts = false;
            packGroup.interactable = false;

            var title = UIKit.Label("Title", rt, 44, true, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -26f), new Vector2(400f, 50f));
            title.text = "BACKPACK";

            packTitleWeight = UIKit.Label("Weight", rt, 26, true, UIKit.Accent, TextAnchor.UpperRight);
            UIKit.Place(packTitleWeight.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -38f), new Vector2(400f, 34f));

            var barBg = UIKit.Box("Bar", rt, UIKit.InkFaint, true, 8f);
            UIKit.Place(barBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(708f, 5f));
            packBarFill = UIKit.Stretch(UIKit.Box("Fill", barBg.transform, UIKit.Accent, true, 8f).rectTransform);

            packList = UIKit.Place(UIKit.Node("List", rt), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(708f, 520f));

            var hint = UIKit.Label("Hint", rt, 18, true, UIKit.InkDim, TextAnchor.LowerRight);
            UIKit.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 20f), new Vector2(500f, 24f));
            hint.text = "[TAB]  CLOSE";
        }

        void OnInventoryChanged() => packDirty = true;

        void UpdateBackpack()
        {
            bool open = backpackToggle != null && backpackToggle.IsOpen && (health == null || !health.IsDead);
            packGroup.alpha = UIKit.Smooth(packGroup.alpha, open ? 1f : 0f, 18f);
            packGroup.blocksRaycasts = packGroup.interactable = open;
            if (!open || inventory == null) return;

            packTitleWeight.text = $"{inventory.CurrentWeight:0.0} / {inventory.MaxWeight:0} KG";
            packBarFill.anchorMax = new Vector2(inventory.LoadFraction, 1f);
            if (packDirty) RebuildBackpackRows();
        }

        void RebuildBackpackRows()
        {
            packDirty = false;
            for (int i = packList.childCount - 1; i >= 0; i--) Destroy(packList.GetChild(i).gameObject);

            var stacks = inventory.Stacks;
            if (stacks.Count == 0)
            {
                var empty = UIKit.Label("Empty", packList, 24, false, UIKit.InkDim, TextAnchor.MiddleCenter);
                UIKit.Place(empty.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(700f, 40f));
                empty.text = "EMPTY. PRESS E ON ITEMS TO PICK THEM UP.";
                return;
            }

            const float rowH = 56f;
            int shown = Mathf.Min(stacks.Count, MaxRows);
            for (int i = 0; i < shown; i++)
            {
                var s = stacks[i];
                if (s.item == null) continue;
                var row = UIKit.Box($"Row {i}", packList, i % 2 == 0 ? new Color(1f, 1f, 1f, 0.04f) : new Color(1f, 1f, 1f, 0.015f), true, 3f);
                var rrt = UIKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -i * (rowH + 2f)), new Vector2(708f, rowH));

                var name = UIKit.Label("Name", rrt, 24, true, UIKit.Ink, TextAnchor.UpperLeft);
                UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -5f), new Vector2(420f, 28f));
                name.text = $"{s.item.displayName.ToUpperInvariant()}  <color=#FFC83D>x{s.count}</color>   <size=18><color=#F5F5F059>{s.Weight:0.0} KG</color></size>";

                var use = UIKit.Label("Usage", rrt, 17, false, UIKit.InkDim, TextAnchor.UpperLeft);
                UIKit.Place(use.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -32f), new Vector2(470f, 20f));
                use.horizontalOverflow = HorizontalWrapMode.Wrap;
                use.verticalOverflow = VerticalWrapMode.Truncate;
                use.text = string.IsNullOrEmpty(s.item.usage) ? s.item.description : s.item.usage;

                var item = s.item;
                int count = s.count;
                if (count > 1) RowButton(rrt, "DROP ALL", new Vector2(-16f, 0f), 104f, () => inventory.Drop(item, count));
                RowButton(rrt, "DROP", new Vector2(count > 1 ? -128f : -16f, 0f), 80f, () => inventory.Drop(item, 1));
            }

            if (stacks.Count > MaxRows)
            {
                var more = UIKit.Label("More", packList, 20, false, UIKit.InkDim, TextAnchor.MiddleCenter);
                UIKit.Place(more.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -shown * (rowH + 2f) - 16f), new Vector2(700f, 28f));
                more.text = $"+{stacks.Count - MaxRows} MORE";
            }
        }

        void RowButton(RectTransform row, string label, Vector2 pos, float width, UnityEngine.Events.UnityAction onClick)
        {
            var img = UIKit.Box(label, row, Color.white, true, 3f);
            img.raycastTarget = true;
            UIKit.Place(img.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), pos, new Vector2(width, 34f));
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.1f);
            colors.highlightedColor = new Color(1f, 0.78f, 0.24f, 0.9f);
            colors.pressedColor = new Color(1f, 0.65f, 0.15f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = 0.08f;
            b.colors = colors;
            b.onClick.AddListener(onClick);
            var t = UIKit.Label("Text", img.transform, 17, true, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Stretch(t.rectTransform);
            t.text = label;
        }

        // ───────────────────────── Game over ─────────────────────────

        CanvasGroup overGroup;
        Text overCause, overStats;
        RectTransform overTitle;
        float diedAt = -1f;

        void BuildGameOver()
        {
            var bg = UIKit.Box("Game Over", root, new Color(0.02f, 0.02f, 0.025f, 0.82f));
            var rt = UIKit.Stretch(bg.rectTransform);
            overGroup = rt.gameObject.AddComponent<CanvasGroup>();
            overGroup.alpha = 0f;
            overGroup.blocksRaycasts = false;
            overGroup.interactable = false;

            var title = UIKit.Shadowed(UIKit.Label("Title", rt, 150, true, UIKit.Danger, TextAnchor.MiddleCenter), 0.6f);
            overTitle = UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1400f, 170f));
            title.text = "YOU DIED";

            overCause = UIKit.Label("Cause", rt, 30, false, UIKit.InkDim, TextAnchor.MiddleCenter);
            UIKit.Place(overCause.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 52f), new Vector2(1400f, 40f));

            overStats = UIKit.Label("Stats", rt, 34, true, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Place(overStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -16f), new Vector2(1400f, 44f));

            var btn = UIKit.Box("Restart", rt, Color.white, true, 2.5f);
            btn.raycastTarget = true;
            UIKit.Place(btn.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(280f, 64f));
            var b = btn.gameObject.AddComponent<Button>();
            b.targetGraphic = btn;
            var colors = b.colors;
            colors.normalColor = UIKit.Accent;
            colors.highlightedColor = new Color(1f, 0.88f, 0.5f);
            colors.pressedColor = new Color(0.9f, 0.62f, 0.12f);
            colors.selectedColor = UIKit.Accent;
            colors.fadeDuration = 0.08f;
            b.colors = colors;
            b.onClick.AddListener(GameDirector.Restart);
            var bt = UIKit.Label("Text", btn.transform, 30, true, new Color(0.06f, 0.06f, 0.06f), TextAnchor.MiddleCenter);
            UIKit.Stretch(bt.rectTransform);
            bt.text = "RESTART";

            var hint = UIKit.Label("Hint", rt, 20, false, UIKit.InkDim, TextAnchor.MiddleCenter);
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -178f), new Vector2(600f, 28f));
            hint.text = "OR PRESS ENTER";
        }

        void UpdateGameOver()
        {
            bool dead = health != null && health.IsDead;
            if (dead && diedAt < 0f)
            {
                diedAt = Time.unscaledTime;
                overCause.text = director != null ? director.DeathCause.ToUpperInvariant() : "";
                int waves = director != null ? director.WavesSurvived : 0;
                int kills = director != null ? director.Kills : 0;
                overStats.text = $"<color=#F5F5F08C>WAVES SURVIVED</color>  {waves}        <color=#F5F5F08C>KILLS</color>  {kills}";
            }

            overGroup.alpha = UIKit.Smooth(overGroup.alpha, dead ? 1f : 0f, 4f);
            overGroup.blocksRaycasts = overGroup.interactable = dead;
            hudGroup.alpha = UIKit.Smooth(hudGroup.alpha, dead ? 0f : 1f, 6f);
            if (dead)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - diedAt) / 1.2f);
                overTitle.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, 1f - Mathf.Pow(1f - t, 3f));
                if (input != null && input.ConfirmPressed && Time.unscaledTime - diedAt > 0.8f) GameDirector.Restart();
            }
        }

        // ───────────────────────── Every frame ─────────────────────────

        void Update()
        {
            if (director == null) return;
            UpdateVignette();
            UpdatePhase();
            UpdateStats();
            UpdateHealth();
            UpdateWeapon();
            UpdateCrosshair();
            UpdatePrompt();
            UpdateFeed();
            UpdateBanner();
            UpdateArmoryWarning();
            UpdateResumeHint();
            UpdateBackpack();
            UpdateGameOver();
        }
    }
}
