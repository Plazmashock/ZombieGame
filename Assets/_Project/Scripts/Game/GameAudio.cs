using System.Collections.Generic;
using UnityEngine;
using LastCheckout.Combat;
using LastCheckout.Enemies;
using LastCheckout.Player;
using LastCheckout.UI;

namespace LastCheckout.Game
{
    /// <summary>
    /// ALL the game's sound and music. There are no audio files in the project: every sound is
    /// synthesised in code at game start (noise, sine waves, envelopes), then played from a small pool of AudioSources.
    ///
    ///   MUSIC   two looping tracks play together and crossfade:
    ///             AMBIENT  slow dark pads + sparse bell notes (menu, loot phase)
    ///             TENSION  heartbeat pulse + low ostinato (while a wave is running)
    ///   SOUNDS  gunshots, swings, hits, reloads, footsteps, zombie groans, hurt, money, pickups, food, UI stingers
    ///
    /// Volumes (Settings menu):  MASTER = AudioListener.volume   MUSIC = GameSettings.MusicVolume   SOUND EFFECTS = GameSettings.SfxVolume
    /// To use a sound from another script:  GameAudio.Play(GameAudio.Sfx.Eat);
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public enum Sfx { Gunshot, Twang, Swoosh, Hit, ReloadOut, ReloadIn, Hurt, Groan, ZombieDie, Coin, Cash, Pickup, Eat, CrateOpen, Step, StingerDanger, StingerCalm, Death, Switch }

        public static GameAudio Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        const int Rate = 22050;
        const int MusicRate = 16000;

        readonly Dictionary<Sfx, AudioClip[]> clips = new Dictionary<Sfx, AudioClip[]>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource ambient, tension;
        float tensionLevel, ambientLevel = 1f, masterFade = 1f;
        float nextGroan = 8f, stepTimer, lastHealth, nextDeathSound;
        const float GroanRange = 22f;   // zombies further away than this stay silent
        bool wasReloading;
        System.Random rng = new System.Random(77);

        GameDirector director;
        PlayerHealth health;
        PlayerShooting shooting;
        PlayerMelee melee;
        PlayerWallet wallet;
        CharacterController body;
        FirstPersonController motor;

        // ───────────────────────── Setup ─────────────────────────

        public static void Create(GameDirector d, GameObject player)
        {
            if (Instance != null) return;
            new GameObject("Game Audio").AddComponent<GameAudio>().Init(d, player);
        }

        void Init(GameDirector d, GameObject player)
        {
            Instance = this;
            director = d;
            health = player.GetComponent<PlayerHealth>();
            shooting = player.GetComponent<PlayerShooting>();
            melee = player.GetComponent<PlayerMelee>();
            wallet = player.GetComponent<PlayerWallet>();
            body = player.GetComponent<CharacterController>();
            motor = player.GetComponent<FirstPersonController>();
            if (health != null) lastHealth = health.Current;

            for (int i = 0; i < 14; i++)
            {
                var go = new GameObject("Sfx " + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                pool.Add(s);
            }

            BuildSounds();
            BuildMusic();

            if (shooting != null) { shooting.Fired += OnFired; shooting.Switched += OnSwitched; }
            if (melee != null) melee.Swung += OnSwung;
            if (wallet != null) { wallet.Earned += OnEarned; wallet.Spent += OnSpent; }
            if (health != null) health.Died += OnDied;
            var inv = player.GetComponent<Inventory.PlayerInventory>();
            if (inv != null) inv.ItemAdded += (item, n) => Play(Sfx.Pickup, 0.7f);
            if (director != null) director.Announce += OnAnnounce;
            Zombie.AnyDied += OnZombieDied;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (shooting != null) { shooting.Fired -= OnFired; shooting.Switched -= OnSwitched; }
            if (melee != null) melee.Swung -= OnSwung;
            if (wallet != null) { wallet.Earned -= OnEarned; wallet.Spent -= OnSpent; }
            if (health != null) health.Died -= OnDied;
            if (director != null) director.Announce -= OnAnnounce;
            Zombie.AnyDied -= OnZombieDied;
        }

        // ───────────────────────── Playing ─────────────────────────

        /// <summary>Plays a sound in the player's ears (2D).</summary>
        public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
        {
            if (Instance != null) Instance.PlayInternal(sfx, volume, pitch, null);
        }

        /// <summary>Plays a sound at a place in the world (3D: gets quieter with distance).</summary>
        public static void PlayAt(Sfx sfx, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (Instance != null) Instance.PlayInternal(sfx, volume, pitch, position);
        }

        void PlayInternal(Sfx sfx, float volume, float pitch, Vector3? at)
        {
            if (!clips.TryGetValue(sfx, out var list) || list.Length == 0) return;
            float vol = volume * GameSettings.SfxVolume;
            if (vol <= 0.001f) return;

            AudioSource src = null;
            foreach (var s in pool) if (!s.isPlaying) { src = s; break; }
            if (src == null) src = pool[0]; // all busy: steal the oldest

            var clip = list[rng.Next(list.Length)];
            src.clip = clip;
            src.volume = Mathf.Clamp01(vol);
            src.pitch = pitch * (0.96f + (float)rng.NextDouble() * 0.08f);
            if (at.HasValue)
            {
                src.transform.position = at.Value;
                src.spatialBlend = 1f;
                src.minDistance = 3f;
                src.maxDistance = 40f;
                src.rolloffMode = AudioRolloffMode.Linear;
            }
            else
            {
                src.spatialBlend = 0f;
            }
            src.Play();
        }

        // ───────────────────────── Event reactions ─────────────────────────

        void OnFired(bool _)
        {
            var spec = shooting != null ? shooting.Current : null;
            if (spec == null) return;
            if (spec.kind == WeaponKind.Bow) { Play(Sfx.Twang, 0.9f); return; }
            string id = spec.id ?? "";
            float pitch = id.Contains("shotgun") ? 0.7f : id.Contains("rifle") || id.Contains("sniper") ? 0.85f : id.Contains("smg") ? 1.15f : 1f;
            Play(Sfx.Gunshot, 0.9f, pitch);
        }

        void OnSwitched(WeaponSpec _) => Play(Sfx.Switch, 0.6f);

        void OnSwung(bool hit)
        {
            Play(Sfx.Swoosh, 0.7f);
            if (hit) Play(Sfx.Hit, 0.9f);
        }

        void OnEarned(int amount, string reason) { if (!reason.StartsWith("SOLD")) Play(Sfx.Coin, 0.55f); }
        void OnSpent(int amount, string what) => Play(Sfx.Cash, 0.8f);

        void OnDied() { Play(Sfx.Death, 1f); }

        void OnZombieDied(Zombie z)
        {
            if (z == null || Time.time < nextDeathSound) return;
            nextDeathSound = Time.time + 0.35f;   // a big horde dying at once = one sound, not twenty
            PlayAt(Sfx.ZombieDie, z.transform.position, 0.35f, z.Type == Zombie.Kind.Brute ? 0.8f : 1f);
        }

        void OnAnnounce(string title, string sub, bool danger) => Play(danger ? Sfx.StingerDanger : Sfx.StingerCalm, 0.8f);

        // ───────────────────────── Every frame ─────────────────────────

        void Update()
        {
            // Hurt sound when health goes down.
            if (health != null)
            {
                if (health.Current < lastHealth - 0.5f && !health.IsDead) Play(Sfx.Hurt, 0.9f);
                lastHealth = health.Current;
            }

            // Reload clicks: magazine out when it starts, magazine in when it ends.
            if (shooting != null)
            {
                bool r = shooting.IsReloading;
                if (r && !wasReloading) Play(Sfx.ReloadOut, 0.7f);
                if (!r && wasReloading) Play(Sfx.ReloadIn, 0.7f);
                wasReloading = r;
            }

            UpdateFootsteps();
            UpdateGroans();
            UpdateMusic();
        }

        void UpdateFootsteps()
        {
            if (body == null || motor == null || Time.timeScale == 0f) return;
            Vector3 v = body.velocity; v.y = 0f;
            float speed = v.magnitude;
            if (!motor.IsGrounded || speed < 0.5f) { stepTimer = 0.2f; return; }
            stepTimer -= Time.deltaTime * (speed / 3.2f);
            if (stepTimer > 0f) return;
            stepTimer = 1f;
            float vol = motor.IsCrouching ? 0.12f : motor.IsSprinting ? 0.55f : 0.35f;
            Play(Sfx.Step, vol, motor.IsSprinting ? 1.05f : 1f);
        }

        void UpdateGroans()
        {
            if (Time.time < nextGroan || Time.timeScale == 0f) return;

            // Zombie voices were the most annoying sound in the game, so now:
            //   • rare: one groan every 5-11 seconds in total (not per zombie), whatever the horde size
            //   • only zombies you can actually be worried about (within GroanRange metres)
            //   • quiet, and softer-sounding (see the Groan clips below)
            nextGroan = Time.time + Random.Range(5f, 11f);
            Zombie pick = null; int seen = 0;
            Vector3 ears = body != null ? body.transform.position : Vector3.zero;
            foreach (var z in Zombie.All)
            {
                if (z == null || z.IsDead) continue;
                if ((z.transform.position - ears).sqrMagnitude > GroanRange * GroanRange) continue;
                if (Random.Range(0, ++seen) == 0) pick = z; // random living zombie in range
            }
            if (pick != null) PlayAt(Sfx.Groan, pick.transform.position + Vector3.up * 1.5f, 0.3f, pick.Type == Zombie.Kind.Brute ? 0.8f : Random.Range(0.95f, 1.1f));
        }

        void UpdateMusic()
        {
            bool wave = director != null && director.Current == GameDirector.Phase.Wave;
            bool dead = health != null && health.IsDead;
            tensionLevel = Mathf.MoveTowards(tensionLevel, wave ? 1f : 0f, Time.unscaledDeltaTime / 3f);
            masterFade = Mathf.MoveTowards(masterFade, dead ? 0.25f : 1f, Time.unscaledDeltaTime / 2f);

            float music = GameSettings.MusicVolume * 0.55f * masterFade;
            ambient.volume = music * (1f - tensionLevel * 0.85f);
            tension.volume = music * tensionLevel;
        }

        // ───────────────────────── Sound synthesis ─────────────────────────

        delegate float Gen(float t, float len);

        AudioClip Make(string name, float seconds, Gen g)
        {
            int n = Mathf.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = g(i / (float)Rate, seconds);
            // fade the last 5 ms so nothing clicks
            int fade = Mathf.Min(n, Rate / 200);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
        static float Env(float t, float decay) => Mathf.Exp(-t * decay);
        static float Tri(float x) => Mathf.Abs(((x % 1f) + 1f) % 1f * 4f - 2f) - 1f;
        static float Saw(float x) => (((x % 1f) + 1f) % 1f) * 2f - 1f;

        void BuildSounds()
        {
            // Gunshot: crack (bright noise) + body (noise) + thump (falling sine).
            float lp = 0f;
            clips[Sfx.Gunshot] = new[] { 0, 1, 2 }.Select(v => Make("Gunshot" + v, 0.5f, (t, L) =>
            {
                float n = Noise();
                lp += (n - lp) * (0.25f + 0.05f * v);
                float crack = n * Env(t, 90f);
                float body = lp * Env(t, 14f + 3f * v);
                float thump = Mathf.Sin(2f * Mathf.PI * (45f + 110f * Env(t, 25f)) * t) * Env(t, 18f);
                return Mathf.Clamp((crack * 0.8f + body * 1.4f + thump * 0.9f) * 0.8f, -1f, 1f);
            })).ToArray();

            float bp = 0f;
            clips[Sfx.Twang] = new[] { Make("Twang", 0.5f, (t, L) =>
            {
                float f = 190f + 8f * Mathf.Sin(t * 40f);
                bp += (Noise() - bp) * 0.3f;
                return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.25f + bp * 0.2f * Env(t, 60f)) * Env(t, 9f);
            }) };

            float sw = 0f;
            clips[Sfx.Swoosh] = new[] { 0, 1 }.Select(v => Make("Swoosh" + v, 0.32f, (t, L) =>
            {
                float u = t / L;
                float cutoff = 0.04f + 0.3f * Mathf.Sin(u * Mathf.PI);
                sw += (Noise() - sw) * cutoff;
                return sw * Mathf.Sin(u * Mathf.PI) * 1.6f * 0.6f;
            })).ToArray();

            float hn = 0f;
            clips[Sfx.Hit] = new[] { 0, 1, 2 }.Select(v => Make("Hit" + v, 0.3f, (t, L) =>
            {
                hn += (Noise() - hn) * 0.18f;
                return (Mathf.Sin(2f * Mathf.PI * (70f + 20f * v) * t) * Env(t, 22f) + hn * Env(t, 30f) * 0.9f) * 0.9f;
            })).ToArray();

            float rc = 0f;
            clips[Sfx.ReloadOut] = new[] { Make("ReloadOut", 0.25f, (t, L) => ClickPair(t, ref rc, 0.0f, 0.11f, 900f)) };
            clips[Sfx.ReloadIn] = new[] { Make("ReloadIn", 0.28f, (t, L) => ClickPair(t, ref rc, 0.0f, 0.14f, 600f)) };
            clips[Sfx.Switch] = new[] { Make("Switch", 0.12f, (t, L) => Noise() * Env(t, 70f) * 0.5f + Mathf.Sin(2f * Mathf.PI * 300f * t) * Env(t, 60f) * 0.4f) };

            clips[Sfx.Hurt] = new[] { 0, 1 }.Select(v => Make("Hurt" + v, 0.4f, (t, L) =>
            {
                float f = 190f - 70f * (t / L) + 25f * v;
                float s = Saw(f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.2f;
                return s * Env(t, 7f) * Mathf.Clamp01(t * 80f) * 0.8f;
            })).ToArray();

            // Soft, low "mmmmhh" instead of a harsh buzzy growl: few harmonics, smooth fade in/out, low-pass filtered.
            clips[Sfx.Groan] = new[] { 0, 1, 2 }.Select(v =>
            {
                float soft = 0f;
                return Make("Groan" + v, 1.1f + 0.15f * v, (t, L) =>
                {
                    float u = t / L;
                    float f0 = 70f + 10f * v + 10f * Mathf.Sin(u * Mathf.PI);
                    float s = 0f;
                    for (int h = 1; h <= 6; h++)    // only the first few harmonics, around one soft vowel (~450 Hz)
                    {
                        float fh = f0 * h;
                        float gain = Mathf.Exp(-Mathf.Pow((fh - 450f) / 260f, 2f));
                        s += Mathf.Sin(2f * Mathf.PI * fh * t) * gain / h * 2.2f;
                    }
                    float env = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 2f);   // slow fade in and out
                    soft += (s * env * 0.4f - soft) * 0.25f;                   // one-pole low-pass: removes the rasp
                    return Mathf.Clamp(soft, -1f, 1f);
                });
            }).ToArray();

            float zd = 0f;
            clips[Sfx.ZombieDie] = new[] { Make("ZombieDie", 1.0f, (t, L) =>
            {
                float f = 110f * Mathf.Exp(-t * 2.2f) + 30f;
                zd += (Noise() - zd) * 0.12f;
                float s = Tri(f * t) * 0.4f + zd * 0.25f * Env(t, 8f);   // triangle wave: much less buzzy than a saw
                return s * Env(t, 4.5f) * Mathf.Clamp01(t * 25f) * 0.7f;
            }) };

            clips[Sfx.Coin] = new[] { Make("Coin", 0.45f, (t, L) =>
            {
                float s = Mathf.Sin(2f * Mathf.PI * 1318f * t) * Env(t, 12f);
                if (t > 0.07f) s += Mathf.Sin(2f * Mathf.PI * 1760f * (t - 0.07f)) * Env(t - 0.07f, 9f);
                return s * 0.4f;
            }) };

            float cn = 0f;
            clips[Sfx.Cash] = new[] { Make("Cash", 0.6f, (t, L) =>
            {
                cn += (Noise() - cn) * 0.5f;
                float s = cn * Env(t, 60f) * 0.6f;                                   // drawer kick
                if (t > 0.12f) s += Mathf.Sin(2f * Mathf.PI * 2093f * (t - 0.12f)) * Env(t - 0.12f, 7f) * 0.35f;  // bell
                if (t > 0.2f) s += Mathf.Sin(2f * Mathf.PI * 2637f * (t - 0.2f)) * Env(t - 0.2f, 7f) * 0.3f;
                return s;
            }) };

            clips[Sfx.Pickup] = new[] { Make("Pickup", 0.14f, (t, L) =>
                Mathf.Sin(2f * Mathf.PI * (520f + 900f * t) * t) * Env(t, 22f) * 0.45f) };

            float ea = 0f;
            clips[Sfx.Eat] = new[] { Make("Eat", 0.8f, (t, L) =>
            {
                ea += (Noise() - ea) * 0.35f;
                float burst = 0f;
                foreach (float st in new[] { 0f, 0.2f, 0.4f })
                    if (t >= st) burst += ea * Env(t - st, 28f);
                float gulp = t > 0.6f ? Mathf.Sin(2f * Mathf.PI * (200f - 150f * (t - 0.6f)) * t) * Env(t - 0.6f, 16f) * 0.5f : 0f;
                return burst * 0.9f + gulp;
            }) };

            float co = 0f;
            clips[Sfx.CrateOpen] = new[] { Make("CrateOpen", 0.9f, (t, L) =>
            {
                co += (Noise() - co) * (0.05f + 0.25f * (t / L));
                float s = co * Mathf.Sin(t / L * Mathf.PI) * 0.7f;                   // air hiss
                s += Mathf.Sin(2f * Mathf.PI * 70f * t) * Env(t, 16f) * 0.6f;       // thunk
                return s;
            }) };

            float st2 = 0f;
            clips[Sfx.Step] = new[] { 0, 1, 2, 3 }.Select(v => Make("Step" + v, 0.16f, (t, L) =>
            {
                st2 += (Noise() - st2) * (0.1f + 0.03f * v);
                return (st2 * 1.8f * Env(t, 38f) + Mathf.Sin(2f * Mathf.PI * (60f + 6f * v) * t) * Env(t, 30f) * 0.5f) * 0.7f;
            })).ToArray();

            clips[Sfx.StingerDanger] = new[] { Make("StingerDanger", 1.8f, (t, L) =>
            {
                float u = t / L;
                float s = 0f;
                foreach (float f in new[] { 55f, 58.3f, 82.4f, 110f })
                    s += Saw(f * t) * 0.18f;
                float n = Noise() * 0.05f * u;
                return (s + n) * Mathf.Sin(Mathf.Pow(u, 0.6f) * Mathf.PI) * 0.9f;
            }) };

            clips[Sfx.StingerCalm] = new[] { Make("StingerCalm", 1.8f, (t, L) =>
            {
                float s = 0f;
                int k = 0;
                foreach (float f in new[] { 440f, 523.3f, 659.3f })
                {
                    float d = t - k++ * 0.12f;
                    if (d > 0f) s += Mathf.Sin(2f * Mathf.PI * f * d) * Env(d, 2.6f) * 0.2f;
                }
                return s;
            }) };

            clips[Sfx.Death] = new[] { Make("Death", 2.4f, (t, L) =>
            {
                float f = 90f * Mathf.Exp(-t * 0.9f);
                return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.7f + Saw(f * 1.01f * t) * 0.2f) * Env(t, 1.1f) * Mathf.Clamp01(t * 30f);
            }) };
        }

        static float ClickPair(float t, ref float state, float t0, float t1, float freq)
        {
            float s = 0f;
            foreach (float st in new[] { t0, t1 })
            {
                float d = t - st;
                if (d < 0f) continue;
                s += Mathf.Sin(2f * Mathf.PI * freq * d) * Env(d, 90f) * 0.5f + Mathf.Sin(2f * Mathf.PI * freq * 3.1f * d) * Env(d, 140f) * 0.3f;
            }
            return s;
        }

        // ───────────────────────── Music synthesis ─────────────────────────

        // A minor: A2 = 110 Hz
        static float Hz(int semitonesFromA2) => 110f * Mathf.Pow(2f, semitonesFromA2 / 12f);

        void BuildMusic()
        {
            ambient = NewMusicSource("Music Ambient", MakeAmbient());
            tension = NewMusicSource("Music Tension", MakeTension());
        }

        AudioSource NewMusicSource(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.spatialBlend = 0f;
            s.playOnAwake = false;
            s.ignoreListenerPause = true;
            s.volume = 0f;
            s.Play();
            return s;
        }

        AudioClip MakeAmbient()
        {
            const float length = 24f;
            int n = (int)(length * MusicRate);
            var d = new float[n];

            // Four chords (Am, F, Dm, E), 6 s each, each fading in and out over 12 s so they overlap smoothly.
            int[][] chords =
            {
                new[] { 0, 7, 12, 15 },   // A  E  A  C
                new[] { -4, 3, 8, 12 },   // F  C  F  A
                new[] { -7, 0, 5, 9 },    // D  A  D  F
                new[] { -5, 2, 7, 11 },   // E  B  E  G#
            };
            for (int c = 0; c < chords.Length; c++)
            {
                foreach (int semi in chords[c])
                {
                    float f = Hz(semi);
                    for (int detune = -1; detune <= 1; detune += 2)
                    {
                        float ff = f * (1f + detune * 0.0035f);
                        for (int i = 0; i < n; i++)
                        {
                            float t = i / (float)MusicRate;
                            float u = Mathf.Repeat(t - c * 6f, length) / 12f;      // 0..1 across this chord's 12 s window
                            if (u >= 1f) continue;
                            float w = Mathf.Sin(u * Mathf.PI); w *= w;
                            float tremolo = 0.85f + 0.15f * Mathf.Sin(t * 0.9f + c);
                            d[i] += Mathf.Sin(2f * Mathf.PI * ff * t) * w * tremolo * 0.035f;
                        }
                    }
                }
            }

            // Constant room hum (50 Hz + buzz), loops perfectly: 24 s is a whole number of cycles.
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)MusicRate;
                d[i] += Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.03f + Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.012f;
            }

            // Sparse bell notes from the A minor scale. They wrap around the end of the loop.
            var r = new System.Random(5);
            int[] scale = { 12, 15, 19, 22, 24, 27 };
            for (float at = 1.5f; at < length - 1f; at += 1.8f + (float)r.NextDouble() * 2.6f)
            {
                float f = Hz(scale[r.Next(scale.Length)]);
                int start = (int)(at * MusicRate), len = (int)(3.2f * MusicRate);
                for (int k = 0; k < len; k++)
                {
                    float t = k / (float)MusicRate;
                    float env = Mathf.Exp(-t * 1.6f) * Mathf.Clamp01(t * 200f);
                    float s = Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 3f);
                    d[(start + k) % n] += s * env * 0.05f;
                }
            }
            return Finish("Music Ambient", d, MusicRate);
        }

        AudioClip MakeTension()
        {
            const float bpm = 100f, beat = 60f / bpm;
            const int beats = 32;
            float length = beats * beat;                 // 19.2 s
            int n = (int)(length * MusicRate);
            var d = new float[n];
            var r = new System.Random(9);

            // Heartbeat: two soft thumps every beat pair.
            for (int b = 0; b < beats; b++)
            {
                AddThump(d, n, (b * beat), 55f, 0.55f);
                AddThump(d, n, (b * beat) + beat * 0.28f, 48f, 0.35f);
            }

            // Low ostinato on eighth notes: A A C A, A A D E ...
            int[] pattern = { 0, 0, 3, 0, 0, 0, 5, 7 };
            for (int e = 0; e < beats * 2; e++)
            {
                float f = Hz(pattern[e % pattern.Length] - 12);
                int start = (int)(e * beat * 0.5f * MusicRate), len = (int)(0.3f * MusicRate);
                for (int k = 0; k < len; k++)
                {
                    float t = k / (float)MusicRate;
                    float env = Mathf.Exp(-t * 9f) * Mathf.Clamp01(t * 300f);
                    float s = Saw(f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f;
                    d[(start + k) % n] += s * env * 0.09f;
                }
            }

            // Dissonant pad (A + Bb) swelling over 4 bars, and a hi-hat style tick on off beats.
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)MusicRate;
                float swell = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / (length / 2f));
                d[i] += (Mathf.Sin(2f * Mathf.PI * Hz(0) * t) + Mathf.Sin(2f * Mathf.PI * Hz(1) * t)) * 0.025f * swell;
            }
            for (int b = 0; b < beats * 2; b++)
            {
                if ((b & 1) == 0) continue;
                int start = (int)(b * beat * 0.5f * MusicRate), len = (int)(0.05f * MusicRate);
                for (int k = 0; k < len; k++)
                    d[(start + k) % n] += ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-k / (float)MusicRate * 80f) * 0.05f;
            }
            return Finish("Music Tension", d, MusicRate);
        }

        static void AddThump(float[] d, int n, float at, float freq, float gain)
        {
            int start = (int)(at * MusicRate), len = (int)(0.35f * MusicRate);
            for (int k = 0; k < len; k++)
            {
                float t = k / (float)MusicRate;
                float f = freq * (1f + 1.2f * Mathf.Exp(-t * 30f));
                d[(start + k) % n] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 11f) * gain * 0.45f;
            }
        }

        static AudioClip Finish(string name, float[] d, int rate)
        {
            float peak = 0.0001f;
            foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            float g = 0.9f / peak;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
            var clip = AudioClip.Create(name, d.Length, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }

    /// Tiny helpers so the sound code above can say .Select(...).ToArray() without LINQ.
    static class ArrayExt
    {
        public static List<T> Select<T>(this int[] source, System.Func<int, T> f)
        {
            var list = new List<T>(source.Length);
            foreach (var v in source) list.Add(f(v));
            return list;
        }
    }
}
