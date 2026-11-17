using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using LastCheckout.Combat;
using LastCheckout.Game;
using LastCheckout.Player;

namespace LastCheckout.UI
{
    /// <summary>
    /// MAIN MENU, PAUSE MENU, SETTINGS and CONTROLS screens. Built in code, like the HUD.
    ///
    ///   Press Play in Unity
    ///        │
    ///        ▼
    ///   MAIN MENU ── camera slowly circles the store at night, game frozen
    ///    ├ PLAY ────────► the run starts (loot phase 1)
    ///    ├ difficulty ‹ NORMAL ›
    ///    ├ SETTINGS ────► sliders / toggles, saved automatically ── BACK
    ///    ├ CONTROLS ────► key list ── BACK
    ///    └ QUIT
    ///
    ///   In game:  Esc ──► PAUSE (game frozen)
    ///                      RESUME · SETTINGS · CONTROLS · RESTART RUN · MAIN MENU · QUIT GAME
    ///             Esc again = back / resume.
    ///
    ///   RESTART skips the main menu; MAIN MENU reloads the level and shows it again.
    /// </summary>
    [DefaultExecutionOrder(100)] // after the player scripts, so our Esc handling has the last word
    public class MenuSystem : MonoBehaviour
    {
        /// <summary>Show the main menu when the level loads? (Restart turns it off for the next load.)</summary>
        public static bool ShowMainMenuOnLoad = true;
        public static MenuSystem Instance { get; private set; }
        /// <summary>True while the main menu or pause menu (or its settings/controls) is on screen.</summary>
        public static bool IsBlocking => Instance != null && Instance.page != Page.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ShowMainMenuOnLoad = true; Instance = null; }

        enum Page { None, Main, Pause, Settings, Controls }

        GameDirector director;
        PlayerHealth health;
        PlayerInputReader input;
        InventoryHUD backpack;
        Camera playerCam;
        Canvas hudCanvas;
        Camera menuCam;

        Page page = Page.None, returnPage = Page.Main;
        bool inMainMenu;
        bool backpackWasOpen;
        float diedAt = -1f;

        CanvasGroup mainGroup, pauseGroup, settingsGroup, controlsGroup, deathGroup;
        RectTransform settingsContent;
        Text bestText, pauseStats, fpsText;
        float fpsTimer; int fpsFrames;

        // ───────────────────────── Setup ─────────────────────────

        public void Init(GameDirector d, GameObject player)
        {
            Instance = this;
            director = d;
            health = player.GetComponent<PlayerHealth>();
            input = player.GetComponent<PlayerInputReader>();
            backpack = player.GetComponent<InventoryHUD>();
            playerCam = player.GetComponentInChildren<Camera>();
            var hudGo = GameObject.Find("Game HUD");
            if (hudGo != null) hudCanvas = hudGo.GetComponent<Canvas>();

            ControlLock.Clear();
            Time.timeScale = 1f;
            GameSettings.ApplyAll();

            EnsureEventSystem();
            BuildCanvas();
            BuildMain();
            BuildPause();
            BuildSettings();
            BuildControls();
            BuildDeathButton();
            BuildFps();
            ShowOnly(Page.None, instant: true);

            if (ShowMainMenuOnLoad) OpenMainMenu();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (menuCam != null) Destroy(menuCam.gameObject);
        }

        static void EnsureEventSystem()
        {
            if (LevelKit.FindFirst<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        RectTransform root;

        void BuildCanvas()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // above the HUD (10) and the upgrade screen (30)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            root = (RectTransform)transform;
        }

        CanvasGroup Group(string name)
        {
            var rt = UIKit.Stretch(UIKit.Node(name, root));
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            g.blocksRaycasts = g.interactable = false;
            return g;
        }

        // ───────────────────────── Main menu ─────────────────────────

        void BuildMain()
        {
            mainGroup = Group("Main Menu");
            var t = mainGroup.transform;

            var vig = UIKit.Box("Vignette", t, new Color(0f, 0f, 0f, 0.75f));
            vig.sprite = UIKit.Vignette;
            UIKit.Stretch(vig.rectTransform);
            var fade = UIKit.Box("Left Fade", t, new Color(0.02f, 0.02f, 0.03f, 1f));
            fade.sprite = MenuKit.LeftFade;
            var frt = fade.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0.62f, 1f); frt.offsetMin = frt.offsetMax = Vector2.zero;

            Vector2 tl = new Vector2(0f, 1f);
            var last = UIKit.Shadowed(UIKit.Label("Title 1", t, 168, true, UIKit.Ink, TextAnchor.UpperLeft), 0.6f);
            UIKit.Place(last.rectTransform, tl, tl, new Vector2(132f, -222f), new Vector2(1000f, 170f));
            last.text = "LAST";
            var checkout = UIKit.Shadowed(UIKit.Label("Title 2", t, 168, true, UIKit.Accent, TextAnchor.UpperLeft), 0.6f);
            UIKit.Place(checkout.rectTransform, tl, tl, new Vector2(132f, -372f), new Vector2(1000f, 170f));
            checkout.text = "CHECKOUT";

            var bar = UIKit.Box("Red Bar", t, UIKit.Danger, true, 8f);
            UIKit.Place(bar.rectTransform, tl, tl, new Vector2(144f, -560f), new Vector2(120f, 5f));

            float x = 120f, w = 440f;
            MenuKit.Button(t, "PLAY", tl, tl, new Vector2(x, -650f), new Vector2(w, 70f), Play, MenuKit.Style.Primary, 36, TextAnchor.MiddleLeft);

            MenuKit.Button(t, "SETTINGS", tl, tl, new Vector2(x, -740f), new Vector2(w, 60f), () => OpenSub(Page.Settings), MenuKit.Style.Ghost, 30, TextAnchor.MiddleLeft);
            MenuKit.Button(t, "CONTROLS", tl, tl, new Vector2(x, -800f), new Vector2(w, 60f), () => OpenSub(Page.Controls), MenuKit.Style.Ghost, 30, TextAnchor.MiddleLeft);
            MenuKit.Button(t, "QUIT", tl, tl, new Vector2(x, -860f), new Vector2(w, 60f), Quit, MenuKit.Style.Ghost, 30, TextAnchor.MiddleLeft);

            bestText = UIKit.Label("Best", t, 20, true, UIKit.InkDim, TextAnchor.LowerRight);
            UIKit.Place(bestText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 40f), new Vector2(700f, 28f));
        }

        void OpenMainMenu()
        {
            inMainMenu = true;
            Time.timeScale = 0f;
            ControlLock.Acquire(this);
            if (hudCanvas != null) hudCanvas.enabled = false;
            StartMenuCamera();
            int bw = GameSettings.BestWave;
            bestText.text = bw > 0 ? $"<color=#F5F5F059>BEST RUN</color>   WAVE {bw}   ·   {GameSettings.BestKills} KILLS" : "<color=#F5F5F059>NO RUNS YET.  THE STORE IS WAITING.</color>";
            ShowOnly(Page.Main, instant: true);
        }

        void Play()
        {
            inMainMenu = false;
            ShowMainMenuOnLoad = false;
            StopMenuCamera();
            if (hudCanvas != null) hudCanvas.enabled = true;
            ShowOnly(Page.None);
            Time.timeScale = 1f;
            ControlLock.Release(this);
            director.BeginRun();
        }

        // The main menu's slowly circling camera, looking at the store front.
        void StartMenuCamera()
        {
            if (playerCam == null) return;
            var go = new GameObject("Menu Camera");
            menuCam = go.AddComponent<Camera>();
            menuCam.CopyFrom(playerCam);
            menuCam.fieldOfView = 48f;
            menuCam.nearClipPlane = 0.1f;
            menuCam.farClipPlane = Mathf.Max(400f, playerCam.farClipPlane);
            menuCam.depth = playerCam.depth + 1f;
            var data = menuCam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
            playerCam.enabled = false;
            UpdateMenuCamera();
        }

        void StopMenuCamera()
        {
            if (menuCam != null) Destroy(menuCam.gameObject);
            menuCam = null;
            if (playerCam != null) playerCam.enabled = true;
        }

        void UpdateMenuCamera()
        {
            if (menuCam == null) return;
            float t = Time.unscaledTime * 0.06f;
            float ang = (-90f + Mathf.Sin(t) * 50f) * Mathf.Deg2Rad;  // swings across the front of the store
            float dist = 34f + Mathf.Sin(t * 0.7f) * 4f;
            Vector3 pos = new Vector3(Mathf.Cos(ang) * dist, 7.5f + Mathf.Sin(t * 1.3f) * 1.5f, -2f + Mathf.Sin(ang) * dist);
            menuCam.transform.position = pos;
            menuCam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 3.2f, -6f) - pos);
        }

        // ───────────────────────── Pause ─────────────────────────

        void BuildPause()
        {
            pauseGroup = Group("Pause Menu");
            var t = pauseGroup.transform;
            UIKit.Stretch(UIKit.Box("Dim", t, new Color(0.02f, 0.02f, 0.025f, 0.78f)).rectTransform);

            Vector2 c = new Vector2(0.5f, 0.5f);
            var title = UIKit.Shadowed(UIKit.Label("Title", t, 120, true, UIKit.Ink, TextAnchor.MiddleCenter), 0.6f);
            UIKit.Place(title.rectTransform, c, c, new Vector2(0f, 330f), new Vector2(900f, 130f));
            title.text = "PAUSED";
            pauseStats = UIKit.Label("Stats", t, 24, true, UIKit.InkDim, TextAnchor.MiddleCenter);
            UIKit.Place(pauseStats.rectTransform, c, c, new Vector2(0f, 250f), new Vector2(1200f, 32f));

            float w = 460f, h = 60f, y = 170f, gap = 70f;
            MenuKit.Button(t, "RESUME", c, c, new Vector2(0f, y), new Vector2(w, h + 6f), Resume, MenuKit.Style.Primary, 32);
            MenuKit.Button(t, "SETTINGS", c, c, new Vector2(0f, y - gap - 10f), new Vector2(w, h), () => OpenSub(Page.Settings), MenuKit.Style.Panel, 28);
            MenuKit.Button(t, "CONTROLS", c, c, new Vector2(0f, y - gap * 2f - 10f), new Vector2(w, h), () => OpenSub(Page.Controls), MenuKit.Style.Panel, 28);
            MenuKit.Button(t, "RESTART RUN", c, c, new Vector2(0f, y - gap * 3f - 10f), new Vector2(w, h), GameDirector.Restart, MenuKit.Style.Panel, 28);
            MenuKit.Button(t, "MAIN MENU", c, c, new Vector2(0f, y - gap * 4f - 10f), new Vector2(w, h), GameDirector.ReturnToMainMenu, MenuKit.Style.Panel, 28);
            MenuKit.Button(t, "QUIT GAME", c, c, new Vector2(0f, y - gap * 5f - 10f), new Vector2(w, h), Quit, MenuKit.Style.Panel, 28);

            var hint = UIKit.Label("Hint", t, 18, true, UIKit.InkFaint, TextAnchor.MiddleCenter);
            UIKit.Place(hint.rectTransform, c, c, new Vector2(0f, -300f), new Vector2(800f, 24f));
            hint.text = "[ESC]  RESUME";
        }

        void OpenPause()
        {
            Time.timeScale = 0f;
            ControlLock.Acquire(this);
            int cash = 0;
            var p = GameObject.FindWithTag("Player");
            if (p != null) { var w = p.GetComponent<PlayerWallet>(); if (w != null) cash = w.Cash; }
            pauseStats.text = director != null
                ? $"WAVE {director.Wave}   ·   {director.Kills} KILLS   ·   <color=#FFC83D>${cash}</color>"
                : "";
            ShowOnly(Page.Pause);
        }

        void Resume()
        {
            ShowOnly(Page.None);
            Time.timeScale = 1f;
            ControlLock.Release(this);
        }

        // ───────────────────────── Settings ─────────────────────────

        void BuildSettings()
        {
            settingsGroup = Group("Settings");
            var t = settingsGroup.transform;
            UIKit.Stretch(UIKit.Box("Dim", t, new Color(0.02f, 0.02f, 0.025f, 0.7f)).rectTransform);
            var panel = UIKit.Box("Panel", t, new Color(0.04f, 0.045f, 0.05f, 0.95f), true, 1.5f);
            var prt = UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 900f));

            var title = UIKit.Label("Title", prt, 56, true, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -36f), new Vector2(800f, 64f));
            title.text = "SETTINGS";
            var saved = UIKit.Label("Saved", prt, 18, true, UIKit.InkDim, TextAnchor.UpperRight);
            UIKit.Place(saved.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -58f), new Vector2(800f, 24f));
            saved.text = "CHANGES APPLY AND SAVE INSTANTLY";

            settingsContent = UIKit.Place(UIKit.Node("Content", prt), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -120f), new Vector2(1480f, 660f));

            MenuKit.Button(prt, "BACK", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(240f, 60f), Back, MenuKit.Style.Primary, 28);
            MenuKit.Button(prt, "RESET TO DEFAULTS", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(320f, 60f), () =>
            {
                GameSettings.ResetToDefaults();
                RebuildSettings();
            }, MenuKit.Style.Panel, 24);
        }

        /// Settings rows are rebuilt every time the screen opens, so they always show the current values.
        void RebuildSettings()
        {
            for (int i = settingsContent.childCount - 1; i >= 0; i--) Destroy(settingsContent.GetChild(i).gameObject);
            float step = MenuKit.RowHeight + 6f;

            // Left column: gameplay + audio
            var left = UIKit.Place(UIKit.Node("Left", settingsContent), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(MenuKit.RowWidth, 660f));
            float y = 0f;
            MenuKit.Header(left, "GAMEPLAY", y); y -= 44f;
            MenuKit.SliderRow(left, "MOUSE SENSITIVITY", y, 0.02f, 0.5f, GameSettings.Sensitivity, v => v.ToString("0.00"), v => GameSettings.Sensitivity = v); y -= step;
            MenuKit.ToggleRow(left, "INVERT MOUSE Y", y, GameSettings.InvertY, v => GameSettings.InvertY = v); y -= step;
            MenuKit.SliderRow(left, "FIELD OF VIEW", y, 55f, 100f, GameSettings.Fov, v => Mathf.RoundToInt(v) + "°", v => GameSettings.Fov = Mathf.Round(v)); y -= step;
            MenuKit.ToggleRow(left, "SHOW FPS", y, GameSettings.ShowFps, v => GameSettings.ShowFps = v); y -= step + 18f;
            MenuKit.Header(left, "AUDIO", y); y -= 44f;
            MenuKit.SliderRow(left, "MASTER VOLUME", y, 0f, 1f, GameSettings.Volume, v => Mathf.RoundToInt(v * 100f) + "%", v => GameSettings.Volume = v); y -= step;
            MenuKit.SliderRow(left, "MUSIC VOLUME", y, 0f, 1f, GameSettings.MusicVolume, v => Mathf.RoundToInt(v * 100f) + "%", v => GameSettings.MusicVolume = v); y -= step;
            MenuKit.SliderRow(left, "SOUND EFFECTS", y, 0f, 1f, GameSettings.SfxVolume, v => Mathf.RoundToInt(v * 100f) + "%", v => GameSettings.SfxVolume = v);

            // Right column: video
            var right = UIKit.Place(UIKit.Node("Right", settingsContent), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(760f, 0f), new Vector2(MenuKit.RowWidth, 660f));
            y = 0f;
            MenuKit.Header(right, "VIDEO", y); y -= 44f;
            MenuKit.ChoiceRow(right, "DISPLAY MODE", y, GameSettings.DisplayModeNames, GameSettings.DisplayMode, i => GameSettings.DisplayMode = i); y -= step;
            var resList = GameSettings.Resolutions();
            var resNames = new string[resList.Count];
            for (int i = 0; i < resList.Count; i++) resNames[i] = $"{resList[i].x} × {resList[i].y}";
            MenuKit.ChoiceRow(right, "RESOLUTION", y, resNames, GameSettings.ResolutionIndex(resList), i => GameSettings.SetResolution(resList[i])); y -= step;
            var qNames = (string[])QualitySettings.names.Clone();
            for (int i = 0; i < qNames.Length; i++) qNames[i] = qNames[i].ToUpperInvariant();
            MenuKit.ChoiceRow(right, "QUALITY", y, qNames, GameSettings.Quality, i => GameSettings.Quality = i, "Shadows, anti-aliasing, render scale"); y -= step;
            MenuKit.ToggleRow(right, "V-SYNC", y, GameSettings.VSync, v => GameSettings.VSync = v, "Stops screen tearing"); y -= step;
            MenuKit.ChoiceRow(right, "FPS LIMIT", y, GameSettings.FpsLimitNames, GameSettings.FpsLimitIndex, i => GameSettings.FpsLimitIndex = i, "Only used when V-sync is off"); y -= step;
            MenuKit.SliderRow(right, "BRIGHTNESS", y, -1f, 1.5f, GameSettings.Brightness, v => (v >= 0f ? "+" : "") + v.ToString("0.0"), v => GameSettings.Brightness = v); y -= step;
            MenuKit.ToggleRow(right, "FILM GRAIN", y, GameSettings.FilmGrain, v => GameSettings.FilmGrain = v);
        }

        // ───────────────────────── Controls ─────────────────────────

        void BuildControls()
        {
            controlsGroup = Group("Controls");
            var t = controlsGroup.transform;
            UIKit.Stretch(UIKit.Box("Dim", t, new Color(0.02f, 0.02f, 0.025f, 0.7f)).rectTransform);
            var panel = UIKit.Box("Panel", t, new Color(0.04f, 0.045f, 0.05f, 0.95f), true, 1.5f);
            var prt = UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 860f));

            var title = UIKit.Label("Title", prt, 56, true, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -36f), new Vector2(800f, 64f));
            title.text = "CONTROLS";

            string[,] keys =
            {
                { "W A S D", "MOVE" }, { "MOUSE", "LOOK" }, { "SPACE", "JUMP" }, { "SHIFT", "SPRINT (LOUD)" },
                { "CTRL / C", "CROUCH (QUIET)" }, { "E", "PICK UP · USE · BUY" }, { "LEFT CLICK", "SHOOT · SWING · DRAW BOW" },
                { "R", "RELOAD" }, { "H", "HEAL" }, { "1 – 0 / WHEEL", "SWITCH WEAPON" }, { "TAB / I", "BACKPACK" },
                { "U", "STORE UPGRADES (LOOT PHASE)" }, { "N", "START THE WAVE NOW (+ BONUS $)" }, { "ESC", "PAUSE" },
            };
            int rows = keys.GetLength(0), perCol = 7;
            for (int i = 0; i < rows; i++)
            {
                int col = i / perCol, row = i % perCol;
                Vector2 pos = new Vector2(60f + col * 600f, -140f - row * 82f);
                var keyBg = UIKit.Box("Key", prt, UIKit.Ink, true, 3f);
                UIKit.Place(keyBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(200f, 52f));
                var k = UIKit.Label("Key Text", keyBg.transform, 22, true, new Color(0.06f, 0.06f, 0.06f), TextAnchor.MiddleCenter);
                UIKit.Stretch(k.rectTransform);
                k.text = keys[i, 0];
                var a = UIKit.Label("Action", prt, 24, true, UIKit.Ink, TextAnchor.MiddleLeft);
                UIKit.Place(a.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos + new Vector2(224f, 0f), new Vector2(360f, 52f));
                a.text = keys[i, 1];
            }

            var tip = UIKit.Label("Tip", prt, 20, false, UIKit.InkDim, TextAnchor.LowerLeft);
            UIKit.Place(tip.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 50f), new Vector2(900f, 60f));
            tip.horizontalOverflow = HorizontalWrapMode.Wrap;
            tip.text = "Tip: the armory shutter slams shut when the loot timer hits 0:00. Don't be inside.\nPress N once you're ready: the earlier you start the wave, the bigger the bonus.";

            MenuKit.Button(prt, "BACK", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(240f, 60f), Back, MenuKit.Style.Primary, 28);
        }

        // ───────────────────────── Death screen extra ─────────────────────────

        void BuildDeathButton()
        {
            deathGroup = Group("Death Extra");
            MenuKit.Button(deathGroup.transform, "MAIN MENU", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(280f, 54f),
                           GameDirector.ReturnToMainMenu, MenuKit.Style.Panel, 24);
        }

        void BuildFps()
        {
            fpsText = UIKit.Shadowed(UIKit.Label("FPS", root, 18, true, UIKit.InkDim, TextAnchor.UpperRight));
            UIKit.Place(fpsText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -12f), new Vector2(200f, 24f));
        }

        // ───────────────────────── Navigation ─────────────────────────

        void OpenSub(Page p)
        {
            returnPage = page == Page.Pause ? Page.Pause : Page.Main;
            if (p == Page.Settings) RebuildSettings();
            ShowOnly(p);
        }

        void Back()
        {
            GameSettings.Save();
            ShowOnly(returnPage);
        }

        void ShowOnly(Page p, bool instant = false)
        {
            page = p;
            foreach (var (g, which) in new[] { (mainGroup, Page.Main), (pauseGroup, Page.Pause), (settingsGroup, Page.Settings), (controlsGroup, Page.Controls) })
            {
                bool on = which == p;
                g.blocksRaycasts = g.interactable = on;
                if (instant) g.alpha = on ? 1f : 0f;
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        static void Quit()
        {
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ───────────────────────── Every frame ─────────────────────────

        void Update()
        {
            bool dead = health != null && health.IsDead;

            // Esc
            if (input != null && input.PausePressed)
            {
                if (page == Page.Settings || page == Page.Controls) Back();
                else if (page == Page.Pause) Resume();
                else if (page == Page.None && !dead && !backpackWasOpen && director != null && director.HasStarted)
                {
                    if (UpgradeMenu.Instance != null && UpgradeMenu.Instance.IsOpen) UpgradeMenu.Instance.Close();
                    else OpenPause();
                }
            }
            backpackWasOpen = backpack != null && backpack.IsOpen;

            // While a menu is up, keep the mouse free (the player's scripts try to capture it when they start).
            if (page != Page.None && Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Fade screens in/out (real time: works while the game is frozen).
            Fade(mainGroup, page == Page.Main);
            Fade(pauseGroup, page == Page.Pause);
            Fade(settingsGroup, page == Page.Settings);
            Fade(controlsGroup, page == Page.Controls);

            // "MAIN MENU" under the death screen's RESTART button.
            if (dead && diedAt < 0f) diedAt = Time.unscaledTime;
            bool showDeath = dead && Time.unscaledTime - diedAt > 1.2f;
            Fade(deathGroup, showDeath);
            deathGroup.blocksRaycasts = deathGroup.interactable = showDeath;

            if (inMainMenu) UpdateMenuCamera();
            UpdateFps();
        }

        void Fade(CanvasGroup g, bool on) => g.alpha = UIKit.Smooth(g.alpha, on ? 1f : 0f, 14f);

        void UpdateFps()
        {
            bool show = GameSettings.ShowFps;
            fpsText.enabled = show;
            if (!show) return;
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                fpsText.text = $"{Mathf.RoundToInt(fpsFrames / fpsTimer)} FPS";
                fpsFrames = 0;
                fpsTimer = 0f;
            }
        }
    }
}
