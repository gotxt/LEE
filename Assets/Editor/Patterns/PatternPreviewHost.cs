#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NHN.TraceStrike.Patterns;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    // A sandbox model: editor scrubbing never changes the scene or plays prefabs/audio.
    public sealed class PatternPreviewHost : IPatternHost, IBossPatternHost
    {
        public BossPresentation boss;
        private BossPresentation Boss => boss ?? throw new InvalidOperationException("Select an encounter with a BossActor prefab to preview boss events.");
        public IPatternLease BossAnimation(BossAnimationEvent action) => Boss.BossAnimation(action);
        public IPatternLease BossVfx(BossVfxEvent action) => Boss.BossVfx(action);
        public IPatternLease BossMotion(BossMotionEvent action, float duration) => Boss.BossMotion(action, duration);
        public Vector2Int player = new Vector2Int(8, 8);
        public readonly Dictionary<int, Tuple<HashSet<Vector2Int>, Color>> marks = new Dictionary<int, Tuple<HashSet<Vector2Int>, Color>>();
        public readonly List<string> log = new List<string>();
        private readonly TrailFieldModel model = new TrailFieldModel();
        private readonly Dictionary<int, HashSet<Vector2Int>> walls = new Dictionary<int, HashSet<Vector2Int>>();
        private int id;
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
        public bool IsAlive => true;
        private sealed class PreviewLease : IPatternLease
        {
            private Action cleanup;
            private readonly Action<float> tick;
            public PreviewLease(Action cleanup, Action<float> tick = null) { this.cleanup = cleanup; this.tick = tick; }
            public void Dispose() { var fn = cleanup; cleanup = null; fn?.Invoke(); }
            public void SetProgress(float t) { if (cleanup != null) tick?.Invoke(t); }
        }
        public IPatternLease Mark(IReadOnlyCollection<Vector2Int> cells, Color color, bool warning)
        {
            int token = ++id;
            marks[token] = Tuple.Create(new HashSet<Vector2Int>(cells), color);
            return new PreviewLease(() => marks.Remove(token), t => {
                var tint = color; tint.a = warning ? Mathf.Lerp(0.2f, 1, t) : color.a;
                marks[token] = Tuple.Create(new HashSet<Vector2Int>(cells), tint);
            });
        }
        public IPatternLease Hazard(IReadOnlyCollection<Vector2Int> cells, string reason) => Mark(cells, Color.magenta, false);
        public IPatternLease Block(IReadOnlyCollection<Vector2Int> cells)
        {
            int token = ++id; walls[token] = new HashSet<Vector2Int>(cells); RebuildWalls();
            var visual = Mark(cells, Color.gray, false);
            return new PreviewLease(() => { walls.Remove(token); RebuildWalls(); visual.Dispose(); });
        }
        private void RebuildWalls()
        { var cells = new HashSet<Vector2Int>(); foreach (var wall in walls.Values) cells.UnionWith(wall); model.SetBlockedCells(cells); }
        public IPatternLease Spawn(string key, GameObject prefab, Vector2Int cell, Sprite sprite, Color color)
        { log.Add("Spawn " + (prefab != null ? prefab.name : "sprite") + " " + cell); return Mark(new[] { cell }, color, false); }
        public IPatternLease Sound(AudioClip clip, float volume) { log.Add("SFX " + (clip != null ? clip.name : "missing")); return new PreviewLease(() => { }); }
        public IPatternLease Motion(string key, Vector2Int target) { log.Add("Move → " + target); return new PreviewLease(() => { }); }
        public IPatternLease Camera(Vector2 offset, float shake) { log.Add("Camera " + offset + " shake " + shake); return new PreviewLease(() => { }); }
        public void Damage(string reason) { log.Add("Hit: " + reason); }
        public void Signal(string name, string argument) { log.Add("Signal: " + name + " " + argument); }
    }
}
#endif
