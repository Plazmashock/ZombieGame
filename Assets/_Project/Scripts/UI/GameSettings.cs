using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LastCheckout.Player;

namespace LastCheckout.UI
{
    /// <summary>
    /// Every player setting, saved on this computer (PlayerPrefs) so it's remembered next time.
    ///
    ///   change a value  →  saved straight away  →  applied straight away
    ///   game starts     →  ApplyAll() puts everything back the way you left it
    ///
    /// GAMEPLAY: mouse sensitivity, invert Y, field of view, difficulty, FPS counter
    /// AUDIO:    master volume
    /// VIDEO:    display mode, resolution, quality preset, V-sync, FPS limit, brightness, film grain
    /// </summary>
    public static class GameSettings
    {
        // ───────────────────────── Defaults ─────────────────────────
        public const float DefaultVolume = 0.8f;
        public const float DefaultSensitivity = 0.12f;
        public const float DefaultFov = 70f;

        public static readonly string[] DifficultyNames = { "EASY", "NORMAL", "HARD" };
        public static readonly string[] DifficultyHints =
        {
            "90 s loot phases, +$75 starting cash",
            "60 s loot phases, the way it's meant to be played",
            "40 s loot phases. Good luck.",
        };
        public static readonly int[] FpsLimits = { -1, 30, 60, 120, 144, 240 };
        public static readonly string[] FpsLimitNames = { "UNLIMITED", "30", "60", "120", "144", "240" };
        public static readonly string[] DisplayModeNames = { "FULLSCREEN", "EXCLUSIVE", "WINDOWED" };
        static readonly FullScreenMode[] DisplayModes = { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };

        // ───────────────────────── Values ─────────────────────────

        public static float MusicVolume { get => PlayerPrefs.GetFloat("lc.music", 0.6f); set { PlayerPrefs.SetFloat("lc.music", value); } }
        public static float SfxVolume { get => PlayerPrefs.GetFloat("lc.sfx", 0.8f); set { PlayerPrefs.SetFloat("lc.sfx", value); } }
        public static float Volume { get => PlayerPrefs.GetFloat("lc.volume", DefaultVolume); set { PlayerPrefs.SetFloat("lc.volume", value); ApplyAudio(); } }
        public static float Sensitivity { get => PlayerPrefs.GetFloat("lc.sens", DefaultSensitivity); set { PlayerPrefs.SetFloat("lc.sens", value); ApplyPlayer(); } }
        public static bool InvertY { get => PlayerPrefs.GetInt("lc.invert", 0) == 1; set { PlayerPrefs.SetInt("lc.invert", value ? 1 : 0); ApplyPlayer(); } }
        public static float Fov { get => PlayerPrefs.GetFloat("lc.fov", DefaultFov); set { PlayerPrefs.SetFloat("lc.fov", value); ApplyPlayer(); } }
        /// <summary>No difficulty choice any more: always NORMAL (60 s loot phases).</summary>
        public static int Difficulty => 1;
        public static bool ShowFps { get => PlayerPrefs.GetInt("lc.fps", 0) == 1; set => PlayerPrefs.SetInt("lc.fps", value ? 1 : 0); }
        public static float Brightness { get => PlayerPrefs.GetFloat("lc.bright", 0f); set { PlayerPrefs.SetFloat("lc.bright", value); ApplyPost(); } }
        public static bool FilmGrain { get => PlayerPrefs.GetInt("lc.grain", 1) == 1; set { PlayerPrefs.SetInt("lc.grain", value ? 1 : 0); ApplyPost(); } }
        public static bool VSync { get => PlayerPrefs.GetInt("lc.vsync", 1) == 1; set { PlayerPrefs.SetInt("lc.vsync", value ? 1 : 0); ApplyFrameRate(); } }
        public static int FpsLimitIndex { get => Mathf.Clamp(PlayerPrefs.GetInt("lc.fpslimit", 0), 0, FpsLimits.Length - 1); set { PlayerPrefs.SetInt("lc.fpslimit", value); ApplyFrameRate(); } }

        public static int Quality
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("lc.quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            set { PlayerPrefs.SetInt("lc.quality", value); ApplyQuality(); }
        }

        public static int DisplayMode
        {
            get
            {
                for (int i = 0; i < DisplayModes.Length; i++) if (Screen.fullScreenMode == DisplayModes[i]) return i;
                return Screen.fullScreenMode == FullScreenMode.MaximizedWindow ? 2 : 0;
            }
            set => Screen.SetResolution(Screen.width, Screen.height, DisplayModes[Mathf.Clamp(value, 0, DisplayModes.Length - 1)]);
        }

        // Unity remembers the resolution and display mode by itself, so those two are not saved here.
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (!list.Contains(v)) list.Add(v);
            }
            if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
            return list;
        }

        public static int ResolutionIndex(List<Vector2Int> list)
        {
            int best = list.Count - 1;
            for (int i = 0; i < list.Count; i++) if (list[i].x == Screen.width && list[i].y == Screen.height) return i;
            return best;
        }

        public static void SetResolution(Vector2Int r) => Screen.SetResolution(r.x, r.y, Screen.fullScreenMode);

        public static float LootSeconds => Difficulty == 0 ? 90f : Difficulty == 2 ? 40f : 60f;
        public static int BonusStartCash => Difficulty == 0 ? 75 : 0;

        // ───────────────────────── Best run ─────────────────────────

        public static int BestWave => PlayerPrefs.GetInt("lc.bestwave", 0);
        public static int BestKills => PlayerPrefs.GetInt("lc.bestkills", 0);

        public static void RecordRun(int waves, int kills)
        {
            if (waves > BestWave || (waves == BestWave && kills > BestKills))
            {
                PlayerPrefs.SetInt("lc.bestwave", waves);
                PlayerPrefs.SetInt("lc.bestkills", kills);
                PlayerPrefs.Save();
            }
        }

        public static void ResetToDefaults()
        {
            foreach (var key in new[] { "lc.volume", "lc.music", "lc.sfx", "lc.sens", "lc.invert", "lc.fov", "lc.diff", "lc.fps", "lc.bright", "lc.grain", "lc.vsync", "lc.fpslimit", "lc.quality" })
                PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            ApplyAll();
        }

        public static void Save() => PlayerPrefs.Save();

        // ───────────────────────── Applying ─────────────────────────

        public static void ApplyAll()
        {
            ApplyQuality();   // first: switching quality resets V-sync, so frame rate comes after
            ApplyFrameRate();
            ApplyAudio();
            ApplyPlayer();
            ApplyPost();
        }

        static void ApplyAudio() => AudioListener.volume = Mathf.Clamp01(Volume);

        static void ApplyPlayer()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;
            var look = player.GetComponent<PlayerLook>();
            if (look != null) { look.Sensitivity = Sensitivity; look.InvertY = InvertY; }
            var cam = player.GetComponentInChildren<Camera>();
            if (cam != null) cam.fieldOfView = Fov;
        }

        static void ApplyQuality()
        {
            int q = Quality;
            if (QualitySettings.GetQualityLevel() != q) QualitySettings.SetQualityLevel(q, true);
        }

        static void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = FpsLimits[FpsLimitIndex];
        }

        // Brightness and film grain live in the scene's post-processing (the "Global Volume").
        static UnityEngine.Rendering.Volume volume;
        static float baseExposure;

        static void ApplyPost()
        {
            var v = Game.LevelKit.FindFirst<UnityEngine.Rendering.Volume>();
            if (v == null) return;
            var profile = v.profile; // a private copy for this play session: your settings asset is never changed
            if (profile.TryGet(out ColorAdjustments color))
            {
                if (volume != v) { volume = v; baseExposure = color.postExposure.value; }
                color.postExposure.overrideState = true;
                color.postExposure.value = baseExposure + Brightness;
            }
            if (profile.TryGet(out UnityEngine.Rendering.Universal.FilmGrain grain)) grain.active = FilmGrain;
        }
    }
}
