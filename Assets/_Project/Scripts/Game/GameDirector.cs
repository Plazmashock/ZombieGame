using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using LastCheckout.Combat;
using LastCheckout.Enemies;

namespace LastCheckout.Game
{
    /// <summary>
    /// Runs the game loop:
    ///
    ///   ┌──────────── LOOT (60 s) ────────────┐      ┌──────── WAVE n ────────┐
    ///   │ armory open, store restocked         │ ───► │ zombies pour in         │ ──► wave cleared ──► LOOT again
    ///   │ last 10 s: armory warning lights     │      │ kill them all           │     (next wave is bigger)
    ///   └── 0:00: armory shutter slams shut ───┘      └─────────────────────────┘
    ///        (still inside? you're sealed in = dead)
    ///
    /// It sets itself up automatically when you press Play in any scene that has a Player:
    /// no menu clicks, nothing to drag in. Everything it needs (armory, HUD, spawner, menus, upgrades) is built in code.
    ///
    /// The run doesn't start until you press PLAY on the main menu (UI/MenuSystem). Restart skips the menu.
    /// During the loot phase you can press N (or click the START WAVE NOW button) to skip the rest of the timer:
    /// the shutter closes and the wave starts right away, and you get $1 for every 2 seconds you skipped.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        public enum Phase { Loot, Wave, Cleared, GameOver }

        [SerializeField] float lootDuration = 60f;
        [SerializeField] float warningAt = 10f;
        [SerializeField] float clearedPause = 4f;

        public static GameDirector Instance { get; private set; }

        public Phase Current { get; private set; }
        /// <summary>Seconds left in the loot phase.</summary>
        public float TimeLeft { get; private set; }
        public float LootDuration => lootDuration;
        public float WarningAt => warningAt;
        /// <summary>The wave that is running, or (during loot) the wave that's coming next.</summary>
        public int Wave { get; private set; }
        public int WavesSurvived { get; private set; }
        public int Kills { get; private set; }
        public string DeathCause { get; private set; } = "";
        public int ZombiesRemaining => spawner != null ? spawner.StillToSpawn + Zombie.AliveCount : Zombie.AliveCount;
        public int WaveTotal => spawner != null ? spawner.TotalThisWave : 0;
        public bool PlayerInArmory => armory != null && player != null && armory.IsOpen && armory.IsInside(player.position);
        public Armory Armory => armory;
        /// <summary>False while the main menu is still up (the run hasn't begun).</summary>
        public bool HasStarted => started;

        /// <summary>Bonus for skipping the rest of the loot timer: $1 per 2 seconds left.</summary>
        public int SkipBonus => Current == Phase.Loot ? Mathf.FloorToInt(TimeLeft * 0.5f) : 0;

        /// <summary>Why "start wave now" can't be used right now, or null if it can.</summary>
        public string SkipBlockedReason
        {
            get
            {
                if (!started) return "NOT STARTED";
                if (Current == Phase.Wave) return "WAVE IN PROGRESS";
                if (Current == Phase.GameOver) return "GAME OVER";
                if (Current == Phase.Loot && PlayerInArmory) return "LEAVE THE ARMORY FIRST";
                return null;
            }
        }

        /// <summary>Big centre-screen announcements: (title, subtitle, isDanger).</summary>
        public event System.Action<string, string, bool> Announce;

        Transform player;
        PlayerHealth playerHealth;
        Armory armory;
        WaveSpawner spawner;
        SupplyRestocker restocker;
        PlayerWallet wallet;
        StoreUpgrades upgrades;
        bool warned, started;
        float clearedUntil;

        // ───────────────────────── Automatic setup ─────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; hooked = false; }
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!hooked) { SceneManager.sceneLoaded += (s, m) => Install(); hooked = true; }
            Install();
        }

        /// Runs right after a scene loads: after every object's Awake, but before any Start.
        static void Install()
        {
            if (Instance != null) return;
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null || playerObject.GetComponent<PlayerHealth>() == null) return; // not a game scene

            var go = new GameObject("Game Director");
            var director = go.AddComponent<GameDirector>();
            director.Setup(playerObject);
        }

        void Setup(GameObject playerObject)
        {
            Instance = this;
            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<PlayerHealth>();

            // Extra player abilities.
            if (playerObject.GetComponent<PlayerHealing>() == null) playerObject.AddComponent<PlayerHealing>();
            var shooting = playerObject.GetComponent<PlayerShooting>();
            // Money. Start: $125 = a pistol + one box of 9mm.
            wallet = playerObject.GetComponent<PlayerWallet>();
            if (wallet == null) wallet = playerObject.AddComponent<PlayerWallet>();
            if (playerObject.GetComponent<PlayerBuffs>() == null) playerObject.AddComponent<PlayerBuffs>();
            if (shooting != null)
            {
                WeaponCatalog.Init(shooting.PistolItem, shooting.PistolAmmo);
                var melee = playerObject.GetComponent<PlayerMelee>();
                if (melee != null) WeaponCatalog.LinkLootItems(melee.LootWeaponItems); // store bat / knife = that weapon
                GunModelFactory.EnsureModels(shooting);
                RemoveFreeGunsAndAmmo(shooting);

                // Camera feel (bob, sway, recoil, landing...) and the visible fists.
                var cam = shooting.GetComponentInChildren<Camera>();
                if (cam != null && cam.GetComponent<Player.FirstPersonFeel>() == null) cam.gameObject.AddComponent<Player.FirstPersonFeel>();
                if (cam != null && cam.GetComponent<Player.Flashlight>() == null) cam.gameObject.AddComponent<Player.Flashlight>();
                FistsViewModel.Create(shooting, playerObject.GetComponent<PlayerMelee>());
            }

            // 0) Dress up the store: stocked shelves, signs, lights, carts, parking lot (see StoreMakeover).
            StoreMakeover.Apply();
            LotBarriers.Apply();          // fences: only the parking lot + front door are reachable
            StoreInteriorDetail.Apply();  // tiles, ducts, beams, posters...

            // Real low-poly 3D models (Quaternius, CC0): loot items, cars, dumpster, crates + street clutter.
            // Before the restocker's snapshot (so restocked loot looks the same) and before the floor map.
            World.ModelSwap.ApplyToAllItems();
            World.ModelSwap.ApplyToProps();

            // 1) Remember the store's loot BEFORE the armory adds its own items.
            restocker = gameObject.AddComponent<SupplyRestocker>();
            restocker.Snapshot();

            // 2) Build the armory, put the player in front of it, then re-bake the zombie floor map
            //    so it includes the new room and the makeover's carts, cars and displays.
            armory = new GameObject("Armory").AddComponent<Armory>();
            armory.Build();
            PlacePlayerAtArmory(playerObject);

            // Real textures on the grey boxes (tiles, block walls, asphalt...). See World/StoreRetexture.
            World.StoreRetexture.Apply();

            // Store upgrades (turrets, wire, roof floors, annex room...) + the kiosk you buy them at.
            upgrades = gameObject.AddComponent<StoreUpgrades>();
            upgrades.Init(this, wallet);
            // The supply truck that parks in the loading bay every loot phase (built switched off, so it isn't in the floor map).
            LoadingBayTruck.Create(this, playerObject, wallet);
            var surface = LevelKit.FindFirst<NavMeshSurface>();
            if (surface != null) NavMeshBaker.Bake(surface);
            else Debug.LogWarning("[Game] No NavMeshSurface in the scene, so zombies can't walk. Run 'Last Checkout > Add Zombies And Combat To Scene'.");

            // 3) Wave spawner (copies a scene zombie as its mould).
            spawner = gameObject.AddComponent<WaveSpawner>();
            spawner.Init(player);
            World.StoreRetexture.ApplyToZombies(); // textured skin + clothes (the spawner's mould too)
            // Real animated zombie bodies. Added to the mould, so every spawned zombie gets one.
            foreach (var z in LevelKit.FindAll<Zombie>())
                if (z.transform.IsChildOf(spawner.transform) && z.GetComponent<ZombieModel>() == null)
                    z.gameObject.AddComponent<ZombieModel>();

            // No zombies before the first wave. The spawner already copied one as its mould,
            // so the zombies placed in the scene can go.
            foreach (var z in LevelKit.FindAll<Zombie>())
            {
                if (z.transform.IsChildOf(spawner.transform)) continue; // that's the mould
                z.gameObject.SetActive(false);
                Destroy(z.gameObject);
            }

            // 4) The new HUD.
            new GameObject("Game HUD").AddComponent<UI.GameHUD>().Init(this, playerObject);
            GameAudio.Create(this, playerObject);   // sounds + music (synthesised in code)
            new GameObject("Buff HUD").AddComponent<UI.BuffHUD>().Init(playerObject);

            // 5) Upgrade screen + "start wave now" bar, then the main/pause menus (they need the HUD to exist).
            new GameObject("Upgrade Menu").AddComponent<UI.UpgradeMenu>().Init(this, upgrades, playerObject);
            new GameObject("Menus").AddComponent<UI.MenuSystem>().Init(this, playerObject);

            Zombie.AnyDied += OnZombieDied;
            playerHealth.Died += OnPlayerDied;
        }

        /// Guns and ammo lying around the store for free would break the economy: they're bought in the armory now.
        static void RemoveFreeGunsAndAmmo(PlayerShooting shooting)
        {
            foreach (var wi in LevelKit.FindAll<Items.WorldItem>())
            {
                if (wi.Item == null) continue;
                if (wi.Item == shooting.PistolAmmo || wi.Item == shooting.PistolItem)
                {
                    wi.gameObject.SetActive(false);
                    Destroy(wi.gameObject);
                }
            }
        }

        /// Starts the player in the back corridor, facing the armory shutter.
        static void PlacePlayerAtArmory(GameObject playerObject)
        {
            Vector3 start = Armory.PlayerStartPoint;

            // A CharacterController ignores teleports while it's switched on, so switch it off for a moment.
            var cc = playerObject.GetComponent<CharacterController>();
            bool ccWasOn = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            playerObject.transform.SetPositionAndRotation(start, Quaternion.identity); // facing +Z = the shutter
            if (cc != null) cc.enabled = ccWasOn;
        }

        void OnDestroy()
        {
            Zombie.AnyDied -= OnZombieDied;
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            if (Instance == this) Instance = null;
        }

        /// The main menu calls BeginRun when you press PLAY. With no main menu (after Restart) it starts right away.
        void Start()
        {
            if (!UI.MenuSystem.ShowMainMenuOnLoad) BeginRun();
        }

        /// <summary>Starts the run: difficulty settings, then the first loot phase. Safe to call twice.</summary>
        public void BeginRun()
        {
            if (started) return;
            started = true;
            lootDuration = UI.GameSettings.LootSeconds;
            warningAt = Mathf.Min(warningAt, lootDuration * 0.25f);
            int bonus = UI.GameSettings.BonusStartCash;
            if (bonus > 0) wallet.Add(bonus, "EASY MODE BONUS");
            StartLoot();
        }

        /// <summary>Shows a big centre-screen message (used by upgrades and other systems).</summary>
        public void Say(string title, string subtitle, bool danger) => Announce?.Invoke(title, subtitle, danger);

        /// <summary>
        /// "START WAVE NOW": skips the rest of the waiting time.
        ///   loot phase    → shutter closes, bonus paid, wave starts immediately
        ///   cleared pause → the store restocks and the next wave starts immediately
        /// Refused while you stand in the armory (the shutter would seal you in).
        /// </summary>
        public void SkipToWave()
        {
            if (SkipBlockedReason != null) return;
            if (Current == Phase.Cleared) StartLoot();
            if (Current != Phase.Loot) return;

            int bonus = SkipBonus;
            if (bonus > 0) wallet.Add(bonus, "EARLY START");
            TimeLeft = 0f;
            armory.Close(player); // can't seal you in: we checked you're outside
            StartWave();
        }

        // ───────────────────────── Phases ─────────────────────────

        void StartLoot()
        {
            Current = Phase.Loot;
            TimeLeft = lootDuration;
            warned = false;
            Wave++;

            int restocked = Wave > 1 ? restocker.Restock() : 0;
            armory.Restock();
            armory.Open();

            string sub = Wave == 1
                ? $"You have ${wallet.Cash}: enough for a pistol and a box of 9mm. The armory is right in front of you."
                : $"Store restocked ({restocked} items). ${wallet.Cash} to spend in the armory.";
            Announce?.Invoke("LOOT PHASE", sub, false);
        }

        void StartWave()
        {
            Current = Phase.Wave;
            spawner.Begin(Wave);
            // Zombies already roaming the level join the attack too.
            foreach (var z in Zombie.All) if (z != null && !z.IsDead) z.Hunt();
            string warning = WaveSpawner.NewTypeWarning(Wave);
            Announce?.Invoke($"WAVE {Wave}", warning ?? $"{spawner.TotalThisWave} incoming. Hold the store.", true);
        }

        void Update()
        {
            if (Current == Phase.GameOver || !started) return;

            switch (Current)
            {
                case Phase.Loot:
                    TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
                    if (!warned && TimeLeft <= warningAt)
                    {
                        warned = true;
                        armory.SetWarning(true);
                        Announce?.Invoke("ARMORY SEALING", $"{warningAt:0} seconds. Get out of the gun room.", true);
                    }
                    if (TimeLeft <= 0f)
                    {
                        bool sealedIn = armory.Close(player);
                        if (sealedIn)
                        {
                            DeathCause = "Sealed inside the armory.";
                            playerHealth.TakeDamage(playerHealth.Max * 100f);
                            return;
                        }
                        StartWave();
                    }
                    break;

                case Phase.Wave:
                    if (spawner.StillToSpawn == 0 && Zombie.AliveCount == 0)
                    {
                        WavesSurvived = Wave;
                        UI.GameSettings.RecordRun(WavesSurvived, Kills);
                        Current = Phase.Cleared;
                        clearedUntil = Time.time + clearedPause;
                        int bonus = 50 + 25 * Wave;
                        wallet.Add(bonus, $"WAVE {Wave} CLEARED");
                        Announce?.Invoke("WAVE CLEARED", $"+${bonus} bonus. Next loot phase in {clearedPause:0} seconds.", false);
                        SupplyDrone.Dispatch();   // drops a crate of supermarket food in the parking lot
                    }
                    break;

                case Phase.Cleared:
                    if (Time.time >= clearedUntil) StartLoot();
                    break;
            }
        }

        void OnZombieDied(Zombie z)
        {
            if (Current == Phase.GameOver || !z.CountsAsKill) return;
            Kills++;
            if (wallet != null) wallet.Add(z.Reward, z.TypeName);
        }

        void OnPlayerDied()
        {
            if (string.IsNullOrEmpty(DeathCause)) DeathCause = "Overrun by the horde.";
            Current = Phase.GameOver;
            UI.GameSettings.RecordRun(WavesSurvived, Kills);
        }

        /// <summary>Reloads the level and starts a new run straight away (Restart buttons).</summary>
        public static void Restart()
        {
            UI.MenuSystem.ShowMainMenuOnLoad = false;
            ReloadScene();
        }

        /// <summary>Reloads the level and shows the main menu.</summary>
        public static void ReturnToMainMenu()
        {
            UI.MenuSystem.ShowMainMenuOnLoad = true;
            ReloadScene();
        }

        static void ReloadScene()
        {
            Time.timeScale = 1f;
            UI.GameSettings.Save();
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) { SceneManager.LoadScene(scene.buildIndex); return; }
#if UNITY_EDITOR
            // Scene not in Build Settings: in the editor we can still reload it by its file path.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Debug.LogWarning("Scene isn't in Build Settings, so it can't be reloaded.");
#endif
        }
    }
}
