using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    public interface IPatternTimeline
    {
        string TimelineName { get; }
        float Duration { get; }
        IReadOnlyList<PatternClip> Clips { get; }
    }

    [CreateAssetMenu(menuName = "Trace Strike/Shared Pattern Sequence")]
    public sealed class PatternSequence : ScriptableObject, IPatternTimeline
    {
        [Min(0)] public float minimumDuration;
        public List<PatternClip> clips = new List<PatternClip>();
        public float Duration
        {
            get
            {
                float end = Mathf.Max(0, minimumDuration);
                foreach (var clip in clips)
                    if (clip != null && clip.enabled) end = Mathf.Max(end, clip.start + clip.duration);
                return end;
            }
        }

        public string TimelineName => name;
        public IReadOnlyList<PatternClip> Clips => clips;
    }

    [Serializable]
    public sealed class EncounterPattern : IPatternTimeline
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "New Pattern";
        public bool enabled = true;
        [Min(0)] public float minimumDuration = 1f;
        public List<PatternClip> clips = new List<PatternClip>();

        public string TimelineName => name;
        public IReadOnlyList<PatternClip> Clips => clips;
        public float Duration
        {
            get
            {
                float end = Mathf.Max(0, minimumDuration);
                foreach (var clip in clips)
                    if (clip != null && clip.enabled) end = Mathf.Max(end, clip.start + clip.duration);
                return end;
            }
        }
    }

    [Serializable]
    public sealed class PatternClip
    {
        public bool enabled = true;
        public string label = "Event";
        [Min(0)] public float start;
        [Min(0)] public float duration = 1;
        [SerializeReference] public PatternEvent action;
        // Authoring-only ownership for sounds/camera cues in the simple attack UI.
        // Runtime execution still uses the existing event timeline unchanged.
        [HideInInspector] public string attackGroupKey;
        [HideInInspector] public bool attackAtImpact;
        [HideInInspector] public string attackName;
    }

    // Add a serializable subclass to extend both the runtime and editor menu.
    [Serializable]
    public abstract class PatternEvent
    {
        public abstract PatternAction Create(PatternContext context, float duration);
        public virtual void Validate(List<string> errors, float duration) { }
    }

    public abstract class PatternAction
    {
        public virtual void Begin() { }
        public virtual void Tick(float elapsed, float delta) { }
        public virtual void End(bool cancelled) { }
    }

    public interface IPatternLease : IDisposable
    {
        void SetProgress(float progress);
    }

    public interface IPatternHost
    {
        Vector2Int PlayerCell { get; }
        Vector2Int CenterCell { get; }
        IReadOnlyCollection<Vector2Int> Walkable { get; }
        IReadOnlyCollection<Vector2Int> Traversable { get; }
        bool IsAlive { get; }
        IPatternLease Mark(IReadOnlyCollection<Vector2Int> cells, Color color, bool warning);
        IPatternLease Block(IReadOnlyCollection<Vector2Int> cells);
        IPatternLease Hazard(IReadOnlyCollection<Vector2Int> cells, string reason);
        IPatternLease Spawn(string key, GameObject prefab, Vector2Int cell, Sprite sprite, Color color);
        IPatternLease Sound(AudioClip clip, float volume);
        IPatternLease Motion(string key, Vector2Int target);
        IPatternLease Camera(Vector2 offset, float shake);
        void Damage(string reason);
        void Signal(string name, string argument);
    }

    public sealed class PatternContext : IDisposable
    {
        public readonly IPatternHost Host;
        public readonly Vector2Int Origin;
        public readonly int Depth;
        public readonly Func<string, EncounterPattern> ResolveEncounterPattern;
        public readonly Dictionary<string, HashSet<Vector2Int>> Selections = new Dictionary<string, HashSet<Vector2Int>>();
        private readonly Dictionary<string, List<IDisposable>> resources = new Dictionary<string, List<IDisposable>>();
        private readonly string scope = Guid.NewGuid().ToString("N");
        public string ObjectKey(string key) => key == "$boss" ? key : scope + "/" + key;
        public PatternContext(IPatternHost host, Vector2Int origin, int depth = 0,
            Func<string, EncounterPattern> resolveEncounterPattern = null)
        {
            Host = host;
            Origin = origin;
            Depth = depth;
            ResolveEncounterPattern = resolveEncounterPattern;
        }

        public PatternContext CreateChild(Vector2Int origin)
        {
            return new PatternContext(Host, origin, Depth + 1, ResolveEncounterPattern);
        }
        public T Own<T>(T resource, string key = "") where T : IDisposable
        {
            if (resource == null) return resource;
            key = key ?? "";
            if (!resources.TryGetValue(key, out var list)) resources[key] = list = new List<IDisposable>();
            list.Add(resource);
            return resource;
        }
        public void Remove(string key)
        {
            if (!resources.TryGetValue(key ?? "", out var list)) return;
            resources.Remove(key ?? "");
            List<Exception> errors = null;
            for (int i = list.Count - 1; i >= 0; i--)
                try { list[i].Dispose(); }
                catch (Exception error) { if (errors == null) errors = new List<Exception>(); errors.Add(error); }
            if (errors != null) throw new AggregateException(errors);
        }
        public void Dispose()
        {
            List<Exception> errors = null;
            foreach (string key in new List<string>(resources.Keys))
                try { Remove(key); }
                catch (Exception error) { if (errors == null) errors = new List<Exception>(); errors.Add(error); }
            Selections.Clear();
            if (errors != null) throw new AggregateException(errors);
        }
    }

    public static class PatternValidation
    {
        public static List<string> Errors(PatternSequence sequence)
        {
            var errors = new List<string>();
            Visit(sequence, new HashSet<PatternSequence>(), errors, 0);
            return errors;
        }

        public static List<string> Errors(EncounterPattern pattern,
            Func<string, EncounterPattern> resolver = null)
        {
            var errors = new List<string>();
            Visit(pattern, resolver, new HashSet<string>(), errors, 0);
            return errors;
        }

        private static void Visit(EncounterPattern pattern, Func<string, EncounterPattern> resolver,
            HashSet<string> path, List<string> errors, int depth)
        {
            if (pattern == null) { errors.Add("Missing encounter pattern."); return; }
            string key = string.IsNullOrEmpty(pattern.id) ? pattern.name : pattern.id;
            if (depth > 16 || !path.Add(key))
            { errors.Add(pattern.name + ": recursive pattern call or depth > 16."); return; }
            if (!Finite(pattern.minimumDuration) || pattern.minimumDuration < 0)
                errors.Add(pattern.name + ": invalid duration.");
            foreach (var clip in pattern.clips)
            {
                if (clip == null || !clip.enabled) continue;
                if (!Finite(clip.start) || !Finite(clip.duration) || clip.start < 0 || clip.duration < 0)
                    errors.Add(pattern.name + "/" + clip.label + ": invalid timing.");
                if (clip.action == null)
                { errors.Add(pattern.name + "/" + clip.label + ": missing event type."); continue; }
                clip.action.Validate(errors, clip.duration);
                if (clip.action is CallEncounterPatternEvent local)
                {
                    EncounterPattern child = resolver?.Invoke(local.patternId);
                    if (child == null) errors.Add(pattern.name + ": missing local pattern " + local.patternId + ".");
                    else if (clip.duration < child.Duration)
                        errors.Add(pattern.name + ": local pattern clip is shorter than " + child.name + ".");
                    else Visit(child, resolver, path, errors, depth + 1);
                }
                else if (clip.action is CallPatternEvent shared)
                {
                    foreach (string error in Errors(shared.pattern))
                        errors.Add(pattern.name + "/" + clip.label + ": " + error);
                }
            }
            path.Remove(key);
        }
        private static void Visit(PatternSequence sequence, HashSet<PatternSequence> path, List<string> errors, int depth)
        {
            if (sequence == null) { errors.Add("Missing sequence."); return; }
            if (depth > 16 || !path.Add(sequence)) { errors.Add(sequence.name + ": recursive pattern call or depth > 16."); return; }
            if (!Finite(sequence.minimumDuration) || sequence.minimumDuration < 0) errors.Add(sequence.name + ": invalid duration.");
            foreach (var clip in sequence.clips)
            {
                if (clip == null || !clip.enabled) continue;
                if (!Finite(clip.start) || !Finite(clip.duration) || clip.start < 0 || clip.duration < 0)
                    errors.Add(sequence.name + "/" + clip.label + ": invalid timing.");
                if (clip.action == null) { errors.Add(sequence.name + "/" + clip.label + ": missing event type."); continue; }
                clip.action.Validate(errors, clip.duration);
                if (clip.action is CallPatternEvent call) Visit(call.pattern, path, errors, depth + 1);
                else if (clip.action is CallEncounterPatternEvent)
                    errors.Add(sequence.name + ": encounter-local calls are only valid inside a Boss Encounter Definition.");
            }
            path.Remove(sequence);
        }
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
}
