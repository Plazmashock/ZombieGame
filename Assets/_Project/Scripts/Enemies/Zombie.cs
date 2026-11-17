using UnityEngine;
using UnityEngine.AI;
using LastCheckout.Combat;
using LastCheckout.Player;
using LastCheckout.World;

namespace LastCheckout.Enemies
{
    /// <summary>
    /// A basic zombie. It moves around using Unity's NavMesh (the invisible "walkable floor map").
    ///
    ///   IDLE ──(sees or hears you)──> CHASING ──(loses you for a few seconds)──> SEARCHING ──(gives up)──> IDLE
    ///     └ wanders slowly                └ runs at you, bites when close            └ walks to where it last saw you
    ///
    /// Noise matters: sprinting is loud, walking is quiet, crouching is nearly silent.
    /// The zombie is slower than your sprint, but a heavy backpack slows YOU down...
    ///
    /// TYPES (set by the wave spawner with ApplyKind):
    ///   WALKER   the normal one                                         $10
    ///   RUNNER   small, reddish, sprints almost as fast as you, fragile  $15   (from wave 2)
    ///   BLOATER  swollen, glowing boils: stops next to you and EXPLODES  $25   (from wave 3)
    ///            Shoot it first: it also blows up when killed, hurting zombies around it.
    ///   BRUTE    huge, dark, 4.5x health, hits hard, barely flinches     $40   (from wave 4)
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Zombie : MonoBehaviour, IDamageable
    {
        enum State { Idle, Chasing, Searching, Dead }

        [Header("Health")]
        [SerializeField] float maxHealth = 60f;

        [Header("Senses")]
        [Tooltip("How far it can SEE you (meters), if you're in front of it and nothing blocks the view.")]
        [SerializeField] float sightRange = 14f;
        [Tooltip("Total width of its field of view, in degrees.")]
        [SerializeField, Range(30f, 360f)] float sightAngle = 120f;
        [Tooltip("How far it can HEAR you (meters), even through walls.")]
        [SerializeField] float hearSprintRange = 12f;
        [SerializeField] float hearWalkRange = 6f;
        [SerializeField] float hearCrouchRange = 2.5f;
        [SerializeField] float hearStillRange = 1.5f;
        [SerializeField] float eyeHeight = 1.6f;
        [Tooltip("Seconds chasing without sensing you before it starts searching.")]
        [SerializeField] float giveUpTime = 5f;

        [Header("Movement")]
        [Tooltip("Slower than your walk (3.5) so you can escape, if your bag isn't too heavy.")]
        [SerializeField] float chaseSpeed = 3f;
        [SerializeField] float wanderSpeed = 0.8f;
        [SerializeField] float wanderRadius = 5f;
        [SerializeField] Vector2 wanderPauseRange = new Vector2(3f, 7f);

        [Header("Attack")]
        [SerializeField] float attackRange = 1.5f;
        [SerializeField] float attackDamage = 10f;
        [SerializeField] float attackCooldown = 1.2f;

        // Read-only info other systems can use later (waves, score...)
        public bool IsDead => state == State.Dead;
        public float Health => health;

        // ---------- Every zombie in the level (the wave system counts these) ----------
        static readonly System.Collections.Generic.List<Zombie> all = new System.Collections.Generic.List<Zombie>();
        public static System.Collections.Generic.IReadOnlyList<Zombie> All => all;
        /// <summary>How many zombies are currently alive.</summary>
        public static int AliveCount { get { int n = 0; foreach (var z in all) if (z != null && !z.IsDead) n++; return n; } }
        /// <summary>Fires once whenever any zombie dies (used for the kill counter).</summary>
        public static event System.Action<Zombie> AnyDied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); AnyDied = null; }

        bool hunter; // wave zombies always know where you are and come straight for you

        // ---------- Zombie types ----------
        public enum Kind { Walker, Runner, Brute, Bloater }
        public Kind Type { get; private set; } = Kind.Walker;

        // Used by ZombieModel (the animated body): when it's there, it plays real Walk / Death animations instead.
        /// <summary>False = don't bob the old box body (the animated body walks by itself).</summary>
        [System.NonSerialized] public bool UseProceduralWalk = true;
        /// <summary>True = a Death animation plays, so don't tip the whole zombie over.</summary>
        [System.NonSerialized] public bool AnimatedDeath;
        /// <summary>When it last hit the player (for the punch animation).</summary>
        public float LastAttackTime { get; private set; } = -99f;
        /// <summary>When it was last hurt (for the flinch animation).</summary>
        public float LastHitTime { get; private set; } = -99f;
        /// <summary>Money you get for killing it.</summary>
        public int Reward { get; private set; } = 10;
        /// <summary>False if it blew itself up next to you (you don't get paid for that).</summary>
        public bool CountsAsKill { get; private set; } = true;
        public string TypeName => Type == Kind.Walker ? "ZOMBIE" : Type.ToString().ToUpperInvariant();

        Color typeTint = Color.white;
        float knockbackResist;                 // 0 = flies back, 1 = doesn't budge
        float stunUntil;
        Vector3 knockVelocity;
        bool stunned;
        bool fuseLit, exploded, pendingExplode;
        float fuseEnd, explodeAt;

        bool placed;          // standing on the walkable-floor map yet?
        bool warnedNoFloor;   // only complain once in the Console
        float nextPlaceTry, nextDoorCheck;

        NavMeshAgent agent;
        PlayerHealth playerHealth;
        FirstPersonController playerMotor;
        Transform playerTransform;
        Renderer[] renderers;      // every visible part of the zombie (head, arms, legs...)
        Color[] baseColors;        // each part's normal color, so we can restore it after a red hit-flash
        Transform visual;          // the "Model" child: we sway it while walking
        float walkCycle;
        float showHealthBarUntil;
        // NOTE: Unity does not allow "new MaterialPropertyBlock()" up here (it stays null and crashes later).
        // It is created on first use inside SetTint instead.
        MaterialPropertyBlock block;

        State state = State.Idle;
        float health;
        float loseTimer, searchTimer;
        float nextAttackTime, nextWanderTime, nextRepathTime;
        float flashUntil;
        bool flashing;
        Vector3 lastKnownPlayerPos;

        // death animation
        float deathTime;
        Quaternion deathStartRotation;

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            health = maxHealth;

            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
            {
                playerTransform = playerObject.transform;
                playerHealth = playerObject.GetComponent<PlayerHealth>();
                playerMotor = playerObject.GetComponent<FirstPersonController>();
            }

            visual = transform.Find("Model");
            renderers = GetComponentsInChildren<Renderer>();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var m = renderers[i].sharedMaterial;
                baseColors[i] = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                // Zombie type colour (runner reddish, brute dark...). Glowing eyes and boils keep their own colour.
                string n = renderers[i].name;
                if (typeTint != Color.white && !n.StartsWith("Eye") && !n.StartsWith("Pustule"))
                {
                    Color b = baseColors[i];
                    baseColors[i] = new Color(b.r * typeTint.r, b.g * typeTint.g, b.b * typeTint.b, b.a);
                }
            }
            if (typeTint != Color.white) SetTint(null);

            TryPlaceOnFloor();
        }

        /// The walkable-floor map is built when the game starts (see NavMeshBaker),
        /// so the agent starts switched off and we turn it on here, once the floor exists.
        /// If there's no floor nearby yet, we DON'T give up: Update tries again twice a second.
        void TryPlaceOnFloor()
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                agent.enabled = true;
                agent.speed = wanderSpeed;
                agent.stoppingDistance = attackRange * 0.7f;
                agent.angularSpeed = 360f;
                agent.acceleration = 12f;
                placed = true;
                ScheduleWander();
                if (hunter) StartChase();
                return;
            }

            nextPlaceTry = Time.time + 0.5f;
            if (!warnedNoFloor)
            {
                warnedNoFloor = true;
                Debug.LogWarning($"[Zombie] {name} isn't close enough to walkable floor at {transform.position}. Retrying...", this);
            }
        }

        /// True when the zombie is standing on the walkable-floor map and can be given orders.
        bool AgentReady => agent != null && agent.enabled && agent.isOnNavMesh;

        /// Zombies shove unlocked doors open when they walk into them (checked 4 times a second).
        void OpenNearbyDoors()
        {
            if (Time.time < nextDoorCheck) return;
            nextDoorCheck = Time.time + 0.25f;
            foreach (var door in Door.All)
            {
                if (door == null || door.IsOpen || door.IsLocked) continue;
                Vector3 d = door.Center - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < 1.8f * 1.8f) door.Open(transform.position);
            }
        }

        // Listen for loud noises (gunshots). Subscribing = "tell me when something noisy happens".
        void OnEnable() { NoiseMaker.NoiseMade += OnNoise; if (!all.Contains(this)) all.Add(this); }
        void OnDisable() { NoiseMaker.NoiseMade -= OnNoise; all.Remove(this); }

        /// <summary>Makes this zombie hunt the player for the rest of its life (wave mode).</summary>
        public void Hunt() => hunter = true;

        /// <summary>Later waves: tougher and faster. Call BEFORE the zombie is switched on.</summary>
        /// <summary>Turns this zombie into a runner / brute / bloater. Call BEFORE it's switched on.</summary>
        public void ApplyKind(Kind kind)
        {
            Type = kind;
            switch (kind)
            {
                case Kind.Runner:
                    maxHealth *= 0.55f; chaseSpeed = 5.2f; wanderSpeed = 1.4f;
                    attackDamage = 7f; attackCooldown = 0.8f; hearSprintRange *= 1.3f;
                    Reward = 15;
                    typeTint = new Color(1.3f, 0.62f, 0.55f);
                    transform.localScale = new Vector3(0.88f, 0.92f, 0.88f);
                    break;
                case Kind.Brute:
                    maxHealth *= 4.5f; chaseSpeed = 2.3f;
                    attackDamage = 25f; attackCooldown = 1.6f; attackRange = 1.9f;
                    knockbackResist = 0.8f;
                    Reward = 40;
                    typeTint = new Color(0.5f, 0.46f, 0.62f);
                    transform.localScale = new Vector3(1.4f, 1.35f, 1.4f);
                    break;
                case Kind.Bloater:
                    maxHealth *= 0.8f; chaseSpeed = 3.3f;
                    Reward = 25;
                    typeTint = new Color(1.25f, 1.3f, 0.45f);
                    transform.localScale = new Vector3(1.3f, 1.05f, 1.3f);
                    AddBoils();
                    break;
            }
        }

        /// Glowing boils on the bloater, so you can spot it in a crowd.
        void AddBoils()
        {
            Transform parent = transform.Find("Model");
            if (parent == null) parent = transform;
            var mat = Game.LevelKit.ColorMaterial(new Color(0.75f, 0.95f, 0.2f), 0.6f, 1.8f);
            Vector3[] spots = { new Vector3(0.22f, 1.1f, 0.2f), new Vector3(-0.2f, 1.3f, 0.15f), new Vector3(0.1f, 0.9f, -0.22f),
                                new Vector3(-0.16f, 1.0f, 0.26f), new Vector3(0.02f, 1.42f, -0.12f), new Vector3(0.26f, 1.25f, -0.05f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var boil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                boil.name = "Pustule " + i;
                Game.LevelKit.RemoveCollider(boil);
                boil.transform.SetParent(parent, false);
                boil.transform.localPosition = spots[i];
                boil.transform.localScale = Vector3.one * (0.16f + 0.03f * (i % 3));
                var r = boil.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>Shoves the zombie back and makes it stagger (weapons call this on a hit).</summary>
        public void Knockback(Vector3 direction, float force, float stunSeconds)
        {
            if (IsDead) return;
            float k = 1f - knockbackResist;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f && force > 0f) knockVelocity = direction.normalized * force * k;
            stunUntil = Mathf.Max(stunUntil, Time.time + stunSeconds * k);
        }

        public void ConfigureForWave(float healthMultiplier, float speedBonus)
        {
            maxHealth *= Mathf.Max(0.1f, healthMultiplier);
            chaseSpeed += speedBonus;
            if (health > 0f) health = maxHealth; // already started: top it up to the new maximum
        }

        void OnNoise(Vector3 position, float radius)
        {
            // Ignore if dead, already chasing you, or not ready yet.
            if (state == State.Dead || state == State.Chasing) return;
            if (agent == null || !agent.enabled) return;
            if ((position - transform.position).sqrMagnitude > radius * radius) return;

            // Go and check out where the noise came from.
            lastKnownPlayerPos = position;
            StartSearch();
        }

        void Update()
        {
            if (state == State.Dead) { UpdateDeath(); return; }

            UpdateFlash();

            // Not on the walkable floor yet: keep trying, do nothing else.
            if (!placed)
            {
                if (Time.time >= nextPlaceTry) TryPlaceOnFloor();
                return;
            }
            if (!AgentReady) return;

            AnimateWalk();
            OpenNearbyDoors();

            // Staggered / shoved by a hit: slide back, do nothing else for a moment.
            if (Time.time < stunUntil || knockVelocity.sqrMagnitude > 0.04f)
            {
                stunned = true;
                agent.isStopped = true;
                agent.Move(knockVelocity * Time.deltaTime);
                knockVelocity = Vector3.MoveTowards(knockVelocity, Vector3.zero, 14f * Time.deltaTime);
                if (fuseLit) UpdateFuse();
                return;
            }
            if (stunned)
            {
                stunned = false;
                agent.isStopped = false;
                if (state == State.Searching) agent.SetDestination(lastKnownPlayerPos);
            }

            // Bloater counting down next to you.
            if (fuseLit) { UpdateFuse(); return; }

            // No player, or player is dead: just wander.
            if (playerHealth == null || playerHealth.IsDead)
            {
                if (state != State.Idle) GoIdle();
                Wander();
                return;
            }

            bool aware = hunter || SensesPlayer(alreadyAlert: state == State.Chasing);

            switch (state)
            {
                case State.Idle:
                    if (aware) StartChase();
                    else Wander();
                    break;

                case State.Chasing:
                    if (aware)
                    {
                        loseTimer = 0f;
                        lastKnownPlayerPos = playerTransform.position;
                    }
                    else
                    {
                        loseTimer += Time.deltaTime;
                        if (loseTimer >= giveUpTime) { StartSearch(); break; }
                    }
                    Chase();
                    break;

                case State.Searching:
                    if (aware) { StartChase(); break; }
                    // Arrived at the spot where we last saw you? Look around a few seconds, then give up.
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f)
                    {
                        searchTimer += Time.deltaTime;
                        if (searchTimer >= 3f) GoIdle();
                    }
                    break;
            }
        }

        // ---------- Senses ----------

        bool SensesPlayer(bool alreadyAlert)
        {
            Vector3 toPlayer = playerTransform.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            // HEARING: depends on how noisy you are. Goes through walls.
            if (distance <= HearingRange()) return true;

            // SIGHT: must be close enough, in front of us (unless already chasing), and not behind a wall.
            float range = alreadyAlert ? sightRange * 1.5f : sightRange;
            if (distance > range) return false;
            if (!alreadyAlert && Vector3.Angle(transform.forward, toPlayer) > sightAngle * 0.5f) return false;
            return HasLineOfSight();
        }

        float HearingRange()
        {
            if (playerMotor == null) return hearWalkRange;

            Vector3 v = playerMotor.Velocity;
            v.y = 0f;
            bool moving = v.magnitude > 0.3f;

            if (!moving) return hearStillRange;
            if (playerMotor.IsSprinting) return hearSprintRange;
            if (playerMotor.IsCrouching) return hearCrouchRange;
            return hearWalkRange;
        }

        bool HasLineOfSight()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 target = playerTransform.position + Vector3.up * 1.2f;
            if (Physics.Linecast(eye, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<PlayerHealth>() != null; // the first thing hit must be the player
            return true;
        }

        // ---------- Behaviours ----------

        void StartChase()
        {
            state = State.Chasing;
            loseTimer = 0f;
            if (playerTransform != null) lastKnownPlayerPos = playerTransform.position;
            if (!AgentReady) return;
            agent.isStopped = false;
            agent.speed = chaseSpeed;
        }

        void StartSearch()
        {
            state = State.Searching;
            searchTimer = 0f;
            if (!AgentReady) return;
            agent.isStopped = false;
            agent.speed = wanderSpeed * 1.5f;
            agent.SetDestination(lastKnownPlayerPos);
        }

        void GoIdle()
        {
            state = State.Idle;
            if (!AgentReady) return;
            agent.isStopped = false;
            agent.speed = wanderSpeed;
            ScheduleWander();
        }

        void Chase()
        {
            Vector3 flat = playerTransform.position - transform.position;
            flat.y = 0f;

            // A bloater doesn't bite: it stops and blows up.
            if (Type == Kind.Bloater && flat.magnitude <= 2.3f) { LightFuse(); return; }

            if (flat.magnitude <= attackRange)
            {
                // Close enough: stop, face the player, bite.
                agent.isStopped = true;
                if (flat.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 540f * Time.deltaTime);

                if (Time.time >= nextAttackTime)
                {
                    nextAttackTime = Time.time + attackCooldown;
                    LastAttackTime = Time.time;
                    playerHealth.TakeDamage(attackDamage);
                }
            }
            else
            {
                agent.isStopped = false;
                // Update the path 5 times a second (every frame would be wasteful).
                if (Time.time >= nextRepathTime)
                {
                    nextRepathTime = Time.time + 0.2f;
                    agent.SetDestination(playerTransform.position);
                }
            }
        }

        void Wander()
        {
            if (Time.time < nextWanderTime) return;

            Vector3 random = transform.position + Random.insideUnitSphere * wanderRadius;
            if (NavMesh.SamplePosition(random, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.speed = wanderSpeed;
                agent.SetDestination(hit.position);
            }
            ScheduleWander();
        }

        void ScheduleWander() => nextWanderTime = Time.time + Random.Range(wanderPauseRange.x, wanderPauseRange.y);

        // ---------- Damage & death ----------

        public void TakeDamage(float amount)
        {
            if (state == State.Dead || amount <= 0f) return;

            health -= amount;
            LastHitTime = Time.time;
            showHealthBarUntil = Time.time + 3f; // show the health bar for 3 seconds after a hit

            // Check for death FIRST, so a problem in the visual flash can never stop the zombie from dying.
            if (health <= 0f) { Die(); return; }

            Flash();

            // Getting hit makes it notice you, wherever you are.
            if (state != State.Chasing && playerTransform != null) StartChase();
        }

        // ---------- Bloater explosion ----------

        void LightFuse()
        {
            if (fuseLit) return;
            fuseLit = true;
            fuseEnd = Time.time + 0.9f;
            agent.isStopped = true;
        }

        void UpdateFuse()
        {
            // Flashes faster and faster, then pops.
            float left = Mathf.Max(0f, fuseEnd - Time.time);
            bool on = Mathf.Repeat(Time.time * Mathf.Lerp(20f, 6f, left / 0.9f), 1f) < 0.5f;
            SetTint(on ? new Color(1f, 1f, 0.55f) : (Color?)null);
            if (Time.time >= fuseEnd) Explode(selfDestruct: true);
        }

        void Explode(bool selfDestruct)
        {
            if (exploded) return;
            exploded = true;
            if (selfDestruct) CountsAsKill = false;     // blew up on its own: no reward
            Vector3 centre = transform.position + Vector3.up;
            if (state != State.Dead) Die();
            Explosion.Detonate(centre, 4.5f, 120f, this);
            Destroy(gameObject);                         // blown apart: nothing left
        }

        void Die()
        {
            state = State.Dead;
            // A shot bloater still pops a moment later, hurting the zombies around it.
            if (Type == Kind.Bloater && !exploded) { pendingExplode = true; explodeAt = Time.time + 0.15f; }
            if (agent != null && agent.enabled) agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

            deathTime = 0f;
            deathStartRotation = transform.rotation;
            AnyDied?.Invoke(this);
            SetTint(null, 0.5f); // darken the body
            Destroy(gameObject, 15f); // clean up the body after 15 seconds
        }

        void UpdateDeath()
        {
            if (pendingExplode && !exploded && Time.time >= explodeAt) { Explode(selfDestruct: false); return; }

            if (AnimatedDeath) return; // the body plays its own Death animation

            // Tip over forward onto the floor in about half a second.
            deathTime += Time.deltaTime;
            float t = Mathf.Clamp01(deathTime / 0.5f);
            transform.rotation = Quaternion.Slerp(deathStartRotation, deathStartRotation * Quaternion.Euler(-90f, 0f, 0f), t);
        }

        // ---------- Hit flash (body turns red for a moment) ----------

        void Flash()
        {
            flashing = true;
            flashUntil = Time.time + 0.12f;
            SetTint(Color.red);
        }

        void UpdateFlash()
        {
            if (flashing && Time.time >= flashUntil)
            {
                flashing = false;
                SetTint(null);
            }
        }

        /// <summary>Paints every body part one flat color (flash), or restores each part's own color (null).</summary>
        void SetTint(Color? flat, float brightness = 1f)
        {
            if (renderers == null) return;
            if (block == null) block = new MaterialPropertyBlock(); // safe to create here (not in a field initializer)
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = flat ?? baseColors[i] * brightness;
                renderers[i].GetPropertyBlock(block);
                block.SetColor("_BaseColor", c); // URP
                block.SetColor("_Color", c);     // built-in fallback
                renderers[i].SetPropertyBlock(block);
            }
        }

        // Walking animation: the whole body bobs, rolls side to side and leans forward when it moves.
        void AnimateWalk()
        {
            if (!UseProceduralWalk || visual == null || agent == null || !agent.enabled) return;

            float speed = agent.velocity.magnitude;
            float amount = Mathf.Clamp01(speed / chaseSpeed);
            walkCycle += speed * 2.4f * Time.deltaTime;

            visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(walkCycle)) * 0.05f * amount, 0f);
            visual.localRotation = Quaternion.Euler(10f * amount, 0f, Mathf.Sin(walkCycle) * 5f * amount);
        }

        // Small health bar floating over the head, shown for a few seconds after you hit it.
        void OnGUI()
        {
            if (state == State.Dead || Time.time > showHealthBarUntil) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.1f);
            if (screen.z <= 0f) return; // behind the camera

            // Slim, clean bar: fades out over its last half second.
            float scale = Screen.height / 1080f;
            float w = 64f * scale, h = Mathf.Max(3f, 5f * scale);
            float fraction = Mathf.Clamp01(health / maxHealth);
            float alpha = Mathf.Clamp01((showHealthBarUntil - Time.time) / 0.5f);
            var back = new Rect(screen.x - w * 0.5f, Screen.height - screen.y - h * 0.5f, w, h);

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.32f, 0.24f, alpha);
            GUI.DrawTexture(new Rect(back.x, back.y, w * fraction, h), Texture2D.whiteTexture);
            GUI.color = old;
        }

        // Select a zombie in the scene to see its hearing (yellow) and sight (red) ranges.
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, sightRange);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, hearSprintRange);
            Gizmos.DrawWireSphere(transform.position, hearWalkRange);
        }
    }
}
