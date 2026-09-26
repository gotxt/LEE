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

    [CreateAssetMenu(fileName = "PatternData_NewPattern", menuName = "Trace Strike/Shared Pattern Sequence")]
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

    public enum PatternLocationSource { Center, PlayerAtStart, CallerOrigin, FixedCell, RandomWalkable }

    [Serializable]
    public sealed class PatternLocationGroup
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "위치 그룹";
        public PatternLocationSource source = PatternLocationSource.RandomWalkable;
        public Vector2Int fixedCell;
        public bool restrictRandomCells;
        [HideInInspector] public List<Vector2Int> randomCells = new List<Vector2Int>();
    }

    [Serializable]
    public sealed class EncounterPattern : IPatternTimeline
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "New Pattern";
        public bool enabled = true;
        [Min(0)] public float minimumDuration = 1f;
        public List<PatternLocationGroup> locationGroups = new List<PatternLocationGroup>();
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
        private readonly Dictionary<string, Vector2Int> locations = new Dictionary<string, Vector2Int>();
        private readonly System.Random locationRandom;
        private readonly Dictionary<string, List<IDisposable>> resources = new Dictionary<string, List<IDisposable>>();
        private readonly string scope = Guid.NewGuid().ToString("N");
        public string ObjectKey(string key) => key == "$boss" ? key : scope + "/" + key;
        public PatternContext(IPatternHost host, Vector2Int origin, int depth = 0,
            Func<string, EncounterPattern> resolveEncounterPattern = null, int? locationSeed = null)
        {
            Host = host;
            Origin = origin;
            Depth = depth;
            ResolveEncounterPattern = resolveEncounterPattern;
            locationRandom = new System.Random(locationSeed ?? Guid.NewGuid().GetHashCode());
        }

        public PatternContext CreateChild(Vector2Int origin)
        {
            return new PatternContext(Host, origin, Depth + 1, ResolveEncounterPattern, locationRandom.Next());
        }
        public Vector2Int Location(string id)
        {
            if (locations.TryGetValue(id, out var cell)) return cell;
            throw new InvalidOperationException("Missing pattern location group: " + id);
        }
        public bool TryGetLocation(string id, out Vector2Int cell) => locations.TryGetValue(id, out cell);
        public void InitializeLocations(IReadOnlyList<PatternLocationGroup> groups)
        {
            if (groups == null) return;
            foreach (var group in groups)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.id) || locations.ContainsKey(group.id))
                    throw new InvalidOperationException("Invalid or duplicate pattern location group.");
                Vector2Int cell;
                switch (group.source)
                {
                    case PatternLocationSource.Center: cell = Host.CenterCell; break;
                    case PatternLocationSource.PlayerAtStart: cell = Host.PlayerCell; break;
                    case PatternLocationSource.CallerOrigin: cell = Origin; break;
                    case PatternLocationSource.FixedCell: cell = group.fixedCell; break;
                    case PatternLocationSource.RandomWalkable:
                        var traversable = new HashSet<Vector2Int>(Host.Traversable);
                        var choices = group.restrictRandomCells
                            ? new List<Vector2Int>(group.randomCells ?? new List<Vector2Int>())
                            : new List<Vector2Int>(traversable);
                        choices.RemoveAll(candidate => !traversable.Contains(candidate));
                        choices = new List<Vector2Int>(new HashSet<Vector2Int>(choices));
                        choices.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
                        if (choices.Count == 0)
                            throw new InvalidOperationException("No traversable candidate tile for random pattern location.");
                        cell = choices[locationRandom.Next(choices.Count)];
                        break;
                    default: throw new InvalidOperationException("Unknown pattern location source.");
                }
                locations.Add(group.id, cell);
            }
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
            var groupIds = new HashSet<string>();
            if (pattern.locationGroups != null)
                foreach (var group in pattern.locationGroups)
                {
                    if (group == null || string.IsNullOrWhiteSpace(group.id))
                        errors.Add(pattern.name + ": location group needs an ID.");
                    else if (!groupIds.Add(group.id))
                        errors.Add(pattern.name + ": duplicate location group ID " + group.id + ".");
                    if (group != null && !Enum.IsDefined(typeof(PatternLocationSource), group.source))
                        errors.Add(pattern.name + ": invalid location source.");
                    if (group != null && group.source == PatternLocationSource.RandomWalkable &&
                        group.restrictRandomCells && (group.randomCells == null || group.randomCells.Count == 0))
                        errors.Add(pattern.name + ": random location group needs at least one candidate tile.");
                }
            foreach (var clip in pattern.clips)
            {
                if (clip == null || !clip.enabled) continue;
                if (!Finite(clip.start) || !Finite(clip.duration) || clip.start < 0 || clip.duration < 0)
                    errors.Add(pattern.name + "/" + clip.label + ": invalid timing.");
                if (clip.action == null)
                { errors.Add(pattern.name + "/" + clip.label + ": missing event type."); continue; }
                clip.action.Validate(errors, clip.duration);
                var tiles = EventTiles(clip.action);
                if (tiles != null && !string.IsNullOrEmpty(tiles.locationGroupId) &&
                    !groupIds.Contains(tiles.locationGroupId))
                    errors.Add(pattern.name + "/" + clip.label + ": missing location group " + tiles.locationGroupId + ".");
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
                var tiles = EventTiles(clip.action);
                if (tiles != null && !string.IsNullOrEmpty(tiles.locationGroupId))
                    errors.Add(sequence.name + "/" + clip.label + ": location groups require a boss encounter pattern.");
                if (clip.action is CallPatternEvent call) Visit(call.pattern, path, errors, depth + 1);
                else if (clip.action is CallEncounterPatternEvent)
                    errors.Add(sequence.name + ": encounter-local calls are only valid inside a Boss Encounter Definition.");
            }
            path.Remove(sequence);
        }
        private static TileSelection EventTiles(PatternEvent action) =>
            action.GetType().GetField("tiles")?.GetValue(action) as TileSelection;
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
}
