using UnityEngine;
using UnityEngine.AI;
using LastCheckout.Enemies;

namespace LastCheckout.Game
{
    /// <summary>
    /// Spawns each wave's zombies outside the store, a few at a time, out of your sight.
    /// Wave zombies are "hunters": they always know where you are.
    ///
    /// Every wave is bigger, tougher and faster:
    ///   zombies  = 4 + 2 × wave        (wave 1 = 6, wave 5 = 14 ...)
    ///   health   = +15% per wave
    ///   speed    = +0.12 m/s per wave (capped)
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] int maxAliveAtOnce = 24;
        [SerializeField] float minDistanceFromPlayer = 20f;

        // Spawn spots (x, z): ONLY the parking lot in front of the store. The sides and the back are fenced off
        // (see LotBarriers), so zombies always arrive through the lot and the front door.
        static readonly Vector2[] SpawnSpots =
        {
            new Vector2(-34f, -26f), new Vector2(-18f, -40f), new Vector2(0f, -44f), new Vector2(18f, -40f), new Vector2(34f, -26f),
            new Vector2(-40f, -36f), new Vector2(40f, -36f), new Vector2(-26f, -22f), new Vector2(26f, -22f),
        };

        GameObject template;   // a switched-off copy of a zombie we clone for every spawn
        Transform player;
        int toSpawn, spawnedThisWave, wave;
        float nextSpawnTime, interval, healthMult, speedBonus;

        public int TotalThisWave { get; private set; }
        /// <summary>Zombies of this wave that haven't appeared yet.</summary>
        public int StillToSpawn => toSpawn;
        public bool CanSpawn => template != null;

        /// <summary>Copies one zombie from the scene to use as the mould for all wave zombies.</summary>
        public void Init(Transform playerTransform)
        {
            player = playerTransform;
            Zombie source = null;
            foreach (var z in LevelKit.FindAll<Zombie>()) { source = z; break; }
            if (source == null)
            {
                Debug.LogWarning("[Waves] No zombie found in the scene to copy, so waves can't spawn. Run 'Last Checkout > Add Zombies And Combat To Scene'.");
                return;
            }

            // Switch the source off for a moment so the copy is born switched-off (its scripts don't start).
            bool wasActive = source.gameObject.activeSelf;
            source.gameObject.SetActive(false);
            template = Instantiate(source.gameObject, transform);
            template.name = "Zombie Template (do not touch)";
            source.gameObject.SetActive(wasActive);
        }

        public void Begin(int waveNumber)
        {
            wave = waveNumber;
            toSpawn = TotalThisWave = Mathf.Min(4 + 2 * waveNumber, 60);
            spawnedThisWave = 0;
            healthMult = 1f + 0.15f * (waveNumber - 1);
            speedBonus = Mathf.Min(0.12f * (waveNumber - 1), 1.4f);
            interval = Mathf.Max(0.35f, 1.2f - 0.07f * waveNumber);
            nextSpawnTime = Time.time + 1.5f; // short breather after the banner

            if (template == null) toSpawn = 0;
        }

        void Update()
        {
            if (toSpawn <= 0 || Time.time < nextSpawnTime) return;
            if (Zombie.AliveCount >= maxAliveAtOnce) return; // wait until some die

            if (TrySpawnOne()) { toSpawn--; failedInARow = 0; }
            else if (++failedInARow == 5)
                Debug.LogWarning("[Waves] Can't find walkable floor at any outside spawn spot. Is there a 'Navigation' object with NavMeshSurface + NavMeshBaker in the scene?");
            nextSpawnTime = Time.time + interval;
        }

        int failedInARow;

        bool TrySpawnOne()
        {
            // Try a few random spots; prefer ones far from the player so zombies don't pop in next to you.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 s = SpawnSpots[Random.Range(0, SpawnSpots.Length)] + Random.insideUnitCircle * 3f;
                Vector3 pos = new Vector3(s.x, 0f, s.y);
                if (player != null && attempt < 8 && (pos - player.position).sqrMagnitude < minDistanceFromPlayer * minDistanceFromPlayer)
                    continue;
                // Last tries search a wider area, in case something was built on top of the spot.
                float searchRadius = attempt < 10 ? 4f : 12f;
                if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, searchRadius, NavMesh.AllAreas)) continue;
                if (hit.position.y > 1f) continue; // landed on a roof or car top: not a real street spot

                var go = Instantiate(template, hit.position, Quaternion.LookRotation(Flat(player != null ? player.position - hit.position : Vector3.forward)));
                go.name = $"Wave {wave} Zombie {++spawnedThisWave}";
                var z = go.GetComponent<Zombie>();
                z.ApplyKind(PickKind());
                z.ConfigureForWave(healthMult, speedBonus);
                z.Hunt();
                go.SetActive(true);
                return true;
            }
            return false;
        }

        /// Which type the next zombie is. New types appear from a certain wave on, and become more common:
        ///   runners from wave 2, bloaters from wave 3, brutes from wave 4.
        /// The first zombie of a type's debut wave is always that type, so you meet it for sure.
        Zombie.Kind PickKind()
        {
            if (spawnedThisWave == 1)
            {
                if (wave == 2) return Zombie.Kind.Runner;
                if (wave == 3) return Zombie.Kind.Bloater;
                if (wave == 4) return Zombie.Kind.Brute;
            }
            float bloater = wave >= 3 ? Mathf.Min(0.08f + 0.02f * (wave - 3), 0.18f) : 0f;
            float brute = wave >= 4 ? Mathf.Min(0.06f + 0.02f * (wave - 4), 0.16f) : 0f;
            float runner = wave >= 2 ? Mathf.Min(0.15f + 0.04f * (wave - 2), 0.35f) : 0f;

            float r = Random.value;
            if (r < bloater) return Zombie.Kind.Bloater;
            r -= bloater;
            if (r < brute) return Zombie.Kind.Brute;
            r -= brute;
            if (r < runner) return Zombie.Kind.Runner;
            return Zombie.Kind.Walker;
        }

        /// <summary>A heads-up line for the wave banner when a new zombie type shows up, else null.</summary>
        public static string NewTypeWarning(int wave)
        {
            switch (wave)
            {
                case 2: return "NEW: RUNNERS. Fast and fragile.";
                case 3: return "NEW: BLOATERS. They explode next to you. Kill them from range.";
                case 4: return "NEW: BRUTES. Slow tanks that hit hard.";
                default: return null;
            }
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.001f ? v : Vector3.forward;
        }
    }
}
