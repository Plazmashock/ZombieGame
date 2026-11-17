using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;
using LastCheckout.World;

namespace LastCheckout.Enemies
{
    /// <summary>
    /// Gives a zombie a real animated body (Quaternius Zombie Apocalypse Kit, CC0) instead of the box body.
    ///
    ///   type      model
    ///   walker    Zombie_Basic or Zombie_Arm (random)
    ///   runner    Zombie_Ribcage
    ///   brute     Zombie_Chubby (the Zombie script makes it big)
    ///   bloater   Zombie_Chubby (+ the glowing boils)
    ///
    /// Animation (no Animator Controller asset needed: we mix the clips in code):
    ///
    ///   standing ── Idle          moving slowly ── Walk         moving fast ── Run
    ///   just hit you ── Punch (on top)       just got shot ── HitReact (on top)
    ///   dead ── Death (plays once, stays on the floor)
    ///
    /// Added to the wave spawner's mould by the GameDirector, so every spawned zombie gets one.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZombieModel : MonoBehaviour
    {
        enum Slot { Idle, Walk, Run, Attack, Hit, Death }
        const int SlotCount = 6;

        Zombie zombie;
        NavMeshAgent agent;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[SlotCount];
        readonly float[] lengths = new float[SlotCount];
        readonly bool[] has = new bool[SlotCount];
        readonly float[] w = new float[SlotCount]; // this frame's mix weights (reused, no garbage)
        float walkPhase, runPhase, idlePhase;
        float attackT = -1f, hitT = -1f, deathT = -1f;
        float seenAttack, seenHit;
        float weightSmoothRun, weightSmoothMove;

        void Awake()
        {
            zombie = GetComponent<Zombie>();
            agent = GetComponent<NavMeshAgent>();
            if (zombie == null) { enabled = false; return; }

            string path = PathFor(zombie.Type);
            var model = ModelLibrary.Spawn(path);
            if (model == null) { enabled = false; return; } // keep the old box body

            // Remove the old box body parts (keep the bloater's boils).
            Transform holder = transform.Find("Model");
            if (holder == null) { holder = new GameObject("Model").transform; holder.SetParent(transform, false); }
            for (int i = holder.childCount - 1; i >= 0; i--)
            {
                var c = holder.GetChild(i);
                if (c.name.StartsWith("Pustule")) continue;
                c.gameObject.SetActive(false); // gone right now (before the Zombie script lists its body parts)
                Destroy(c.gameObject);
            }

            // 1.8 m tall, feet on the ground, facing forward (+Z, the way the zombie walks).
            ModelLibrary.Fit(model, holder, new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(1f, 1.8f, 1f)),
                             ModelLibrary.FitBy.Height, Quaternion.identity, autoYaw: false, bottom: true);
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = false;

            // Make sure the colour atlas is on the model, and no two zombies are exactly the same size.
            ModelLibrary.ForceAtlas(model);
            holder.localScale *= Random.Range(0.93f, 1.08f);

            BuildAnimation(model, path);
            zombie.UseProceduralWalk = false;
            zombie.AnimatedDeath = has[(int)Slot.Death];
        }

        static string PathFor(Zombie.Kind kind)
        {
            switch (kind)
            {
                case Zombie.Kind.Runner: return "Zombies/Zombie_Ribcage";
                case Zombie.Kind.Brute: return "Zombies/Zombie_Chubby";
                case Zombie.Kind.Bloater: return "Zombies/Zombie_Chubby";
                default: return Random.value < 0.5f ? "Zombies/Zombie_Basic" : "Zombies/Zombie_Arm";
            }
        }

        // ───────────────────────── Animation setup ─────────────────────────

        void BuildAnimation(GameObject model, string path)
        {
            var clips = ModelLibrary.Clips(path);
            if (clips.Length == 0) return;

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            graph = PlayableGraph.Create(name + " Animation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, SlotCount);
            var output = AnimationPlayableOutput.Create(graph, "Zombie", animator);
            output.SetSourcePlayable(mixer);

            Assign(Slot.Idle, Find(clips, "Idle"));
            Assign(Slot.Walk, Find(clips, "Walk"));
            Assign(Slot.Run, Find(clips, "Run") ?? Find(clips, "Walk"));
            Assign(Slot.Attack, Find(clips, "Punch") ?? Find(clips, "Run_Attack") ?? Find(clips, "Idle_Attack") ?? Find(clips, "Run_Arms_Attack"));
            Assign(Slot.Hit, Find(clips, "HitReact"));
            Assign(Slot.Death, Find(clips, "Death"));

            idlePhase = Random.value * 3f; // so a crowd doesn't move in sync
            walkPhase = Random.value * 3f;
            runPhase = Random.value * 3f;
            graph.Play();
        }

        /// Clip names look like "CharacterArmature|Walk": match the part after the "|".
        static AnimationClip Find(AnimationClip[] clips, string wanted)
        {
            foreach (var c in clips)
            {
                string n = c.name;
                int bar = n.LastIndexOf('|');
                if (bar >= 0) n = n.Substring(bar + 1);
                if (n == wanted) return c;
            }
            return null;
        }

        void Assign(Slot slot, AnimationClip clip)
        {
            int i = (int)slot;
            if (clip == null)
            {
                // An empty input keeps the mixer happy; its weight always stays 0.
                playables[i] = AnimationClipPlayable.Create(graph, new AnimationClip());
                has[i] = false;
            }
            else
            {
                playables[i] = AnimationClipPlayable.Create(graph, clip);
                lengths[i] = Mathf.Max(0.05f, clip.length);
                has[i] = true;
            }
            playables[i].SetApplyFootIK(false);
            playables[i].Pause(); // we set the time ourselves every frame
            graph.Connect(playables[i], 0, mixer, i);
            mixer.SetInputWeight(i, 0f);
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        // ───────────────────────── Every frame ─────────────────────────

        void Update()
        {
            if (!graph.IsValid()) return;
            float dt = Time.deltaTime;
            System.Array.Clear(w, 0, SlotCount);

            if (zombie.IsDead && has[(int)Slot.Death])
            {
                if (deathT < 0f) deathT = 0f;
                deathT += dt;
                w[(int)Slot.Death] = 1f;
                SetTime(Slot.Death, Mathf.Min(deathT, lengths[(int)Slot.Death] - 0.01f));
                Apply();
                return;
            }

            // Walking / running, blended by how fast the zombie moves.
            float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
            weightSmoothMove = Mathf.MoveTowards(weightSmoothMove, Mathf.InverseLerp(0.15f, 0.9f, speed), dt * 4f);
            weightSmoothRun = Mathf.MoveTowards(weightSmoothRun, Mathf.InverseLerp(2.2f, 3.8f, speed), dt * 3f);
            float idle = 1f - weightSmoothMove;
            float walk = weightSmoothMove * (1f - weightSmoothRun);
            float run = weightSmoothMove * weightSmoothRun;

            idlePhase += dt;
            walkPhase += dt * Mathf.Clamp(speed / 1.3f, 0.6f, 1.8f);
            runPhase += dt * Mathf.Clamp(speed / 4f, 0.7f, 1.5f);

            // One-shot moves on top: punch when it hits you, flinch when it gets hit.
            if (zombie.LastAttackTime > seenAttack) { seenAttack = zombie.LastAttackTime; if (has[(int)Slot.Attack]) attackT = 0f; }
            if (zombie.LastHitTime > seenHit) { seenHit = zombie.LastHitTime; if (has[(int)Slot.Hit] && attackT < 0f) hitT = 0f; }

            float attack = OneShot(ref attackT, Slot.Attack, dt, 0.9f);
            float hit = OneShot(ref hitT, Slot.Hit, dt, 0.6f);
            float over = Mathf.Clamp01(attack + hit);
            float baseW = 1f - over;

            w[(int)Slot.Idle] = idle * baseW;
            w[(int)Slot.Walk] = walk * baseW;
            w[(int)Slot.Run] = run * baseW;
            w[(int)Slot.Attack] = attack;
            w[(int)Slot.Hit] = hit;

            SetTime(Slot.Idle, Mathf.Repeat(idlePhase, lengths[(int)Slot.Idle]));
            SetTime(Slot.Walk, Mathf.Repeat(walkPhase, lengths[(int)Slot.Walk]));
            SetTime(Slot.Run, Mathf.Repeat(runPhase, lengths[(int)Slot.Run]));
            Apply();
        }

        /// Advances a one-shot clip. Returns its weight (fades in and out), or 0 when not playing.
        float OneShot(ref float t, Slot slot, float dt, float maxWeight)
        {
            if (t < 0f) return 0f;
            int i = (int)slot;
            t += dt;
            float len = lengths[i];
            if (t >= len) { t = -1f; return 0f; }
            SetTime(slot, t);
            float fade = Mathf.Min(Mathf.Clamp01(t / 0.12f), Mathf.Clamp01((len - t) / 0.15f));
            return fade * maxWeight;
        }

        void SetTime(Slot slot, float t)
        {
            int i = (int)slot;
            if (has[i]) playables[i].SetTime(t);
        }

        void Apply()
        {
            // If a slot has no clip (e.g. the runner has no punch), give its weight to the walk/idle pose.
            float lost = 0f;
            for (int i = 0; i < SlotCount; i++) if (!has[i]) { lost += w[i]; w[i] = 0f; }
            if (lost > 0f) w[has[(int)Slot.Idle] ? (int)Slot.Idle : (int)Slot.Walk] += lost;
            for (int i = 0; i < SlotCount; i++) mixer.SetInputWeight(i, w[i]);
        }
    }
}
