#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NHN.TraceStrike.Patterns;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    // A sandbox model: editor scrubbing never changes the scene or plays prefabs/audio.
    public sealed class PatternPreviewHost : IPatternHost, IBossPatternHost, IMechanicPresentationHost, IDisposable
    {
        // Back to front. Dictionary slot reuse must never determine visual stacking.
        public enum PreviewLayer { Obstacle, Hazard, Warning, Damage, Effect }

        public sealed class PreviewMark
        {
            public readonly HashSet<Vector2Int> Cells;
            public readonly PreviewLayer Layer;
            public Color Color { get; internal set; }

            internal PreviewMark(IReadOnlyCollection<Vector2Int> cells, Color color, PreviewLayer layer)
            { Cells = new HashSet<Vector2Int>(cells); Color = color; Layer = layer; }
        }

        public BossPresentation boss;
        private BossPresentation Boss => boss ?? throw new InvalidOperationException("Select an encounter with a BossActor prefab to preview boss events.");
        public IPatternLease BossAnimation(BossAnimationEvent action) => Boss.BossAnimation(action);
        public IPatternLease BossVfx(BossVfxEvent action) => Boss.BossVfx(action);
        public IPatternLease BossMotion(BossMotionEvent action, float duration) => Boss.BossMotion(action, duration);
        public Vector2Int player = new Vector2Int(8, 8);
        public readonly Dictionary<int, PreviewMark> marks = new Dictionary<int, PreviewMark>();
        public readonly List<string> log = new List<string>();
        public Func<IEnumerable<Vector2Int>> RequiredCellsProvider { get; set; }
        private readonly TrailFieldModel model = new TrailFieldModel();
        private readonly Dictionary<int, HashSet<Vector2Int>> walls = new Dictionary<int, HashSet<Vector2Int>>();
        private int id;
        private bool disposed;
        public PatternPreviewHost(int size, ArenaShape shape = ArenaShape.Rounded)
        {
            model.CreateField((int)shape, size);
        }
        public PatternPreviewHost(BossArenaDefinition arena)
        { arena.ApplyTo(model); player = arena.overridePlayerStart ? arena.playerStart : model.CenterCell; }
        public Vector2Int PlayerCell => player;
        public Vector2Int CenterCell => model.CenterCell;
        public IReadOnlyCollection<Vector2Int> Walkable => model.Walkable;
        public IReadOnlyCollection<Vector2Int> Traversable => model.Traversable;
        public bool IsAlive => !disposed;

        // Take once per board draw, not per tile. Newer marks win within a layer.
        public IReadOnlyList<PreviewMark> GetOrderedMarks() => marks
            .OrderBy(pair => pair.Value.Layer).ThenBy(pair => pair.Key)
            .Select(pair => pair.Value).ToArray();

        private sealed class PreviewLease : IPatternLease
        {
            private Action cleanup;
            private readonly Action<float> tick;
            public PreviewLease(Action cleanup, Action<float> tick = null) { this.cleanup = cleanup; this.tick = tick; }
            public void Dispose() { var fn = cleanup; cleanup = null; fn?.Invoke(); }
            public void SetProgress(float t) { if (cleanup != null) tick?.Invoke(t); }
        }
        public IPatternLease Mark(IReadOnlyCollection<Vector2Int> cells, Color color, bool warning)
            => AddMark(cells, color, warning ? PreviewLayer.Warning : PreviewLayer.Damage);

        private IPatternLease AddMark(IReadOnlyCollection<Vector2Int> cells, Color color, PreviewLayer layer)
        {
            int token = ++id;
            var mark = new PreviewMark(cells, color, layer);
            marks[token] = mark;
            return new PreviewLease(() => marks.Remove(token), t => {
                var tint = color; tint.a = layer == PreviewLayer.Warning ? Mathf.Lerp(0.2f, 1, t) : color.a;
                mark.Color = tint;
            });
        }
        public IPatternLease Hazard(IReadOnlyCollection<Vector2Int> cells, string reason) => AddMark(cells, Color.magenta, PreviewLayer.Hazard);
        public IPatternLease Block(IReadOnlyCollection<Vector2Int> cells)
        {
            var blocked = new HashSet<Vector2Int>(cells);
            var required = RequiredCellsProvider?.Invoke();
            if (required != null) blocked.ExceptWith(required);
            int token = ++id; walls[token] = blocked; RebuildWalls();
            var visual = AddMark(blocked, Color.gray, PreviewLayer.Obstacle);
            return new PreviewLease(() => { walls.Remove(token); RebuildWalls(); visual.Dispose(); });
        }
        private void RebuildWalls()
        { var cells = new HashSet<Vector2Int>(); foreach (var wall in walls.Values) cells.UnionWith(wall); model.SetBlockedCells(cells); }
        public IPatternLease Spawn(string key, GameObject prefab, Vector2Int cell, Sprite sprite, Color color)
        { log.Add("Spawn " + (prefab != null ? prefab.name : "sprite") + " " + cell); return AddMark(new[] { cell }, color, PreviewLayer.Effect); }
        public IPatternLease ShowDevice(Vector2Int cell, GameObject prefab, Sprite sprite, Color tint) =>
            AddMark(new[] { cell }, tint, PreviewLayer.Obstacle);
        public IPatternLease Sound(AudioClip clip, float volume) { log.Add("SFX " + (clip != null ? clip.name : "missing")); return new PreviewLease(() => { }); }
        public IPatternLease Motion(string key, Vector2Int target) { log.Add("Move → " + target); return new PreviewLease(() => { }); }
        public IPatternLease Camera(Vector2 offset, float shake) { log.Add("Camera " + offset + " shake " + shake); return new PreviewLease(() => { }); }
        public void Damage(string reason) { log.Add("Hit: " + reason); }
        public void Signal(string name, string argument) { log.Add("Signal: " + name + " " + argument); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            RequiredCellsProvider = null;
            marks.Clear(); walls.Clear(); RebuildWalls();
            boss = null;
        }
    }
}
#endif
