using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    public enum ArenaShape { Rounded, Triangle, Star, Custom }

    [Serializable]
    public sealed class BossArenaDefinition
    {
        [Range(5, TrailFieldModel.MaxSize)] public int size = 17;
        public ArenaShape shape = ArenaShape.Rounded;
        [HideInInspector] public List<Vector2Int> floorCells = new List<Vector2Int>();
        [Range(0.5f, 3f)] public float cameraZoom = 2.05f;
        [Range(0.25f, 1.5f)] public float playerSizeRatio = 0.78f;

        public bool overridePlayerStart;
        public Vector2Int playerStart = new Vector2Int(8, 8);
        public bool restrictStartCells;
        [HideInInspector] public List<Vector2Int> startCells = new List<Vector2Int>();
        public bool restrictEndCells;
        [HideInInspector] public List<Vector2Int> endCells = new List<Vector2Int>();

        public int GridSize => Math.Max(TrailFieldModel.Size, size);
        public Vector2Int CenterCell => new Vector2Int(GridSize / 2, GridSize / 2);
        public bool ContainsBounds(Vector2Int cell) => TrailFieldModel.ContainsFieldBounds(cell, size);

        public void ApplyTo(TrailFieldModel model)
        {
            if (shape == ArenaShape.Custom) model.CreateCustomField(size, floorCells);
            else model.CreateField((int)shape, size);
            model.SetEndpointRegions(restrictStartCells ? startCells : null, restrictEndCells ? endCells : null);
        }

        public HashSet<Vector2Int> GetCells()
        {
            var model = new TrailFieldModel();
            ApplyTo(model);
            return new HashSet<Vector2Int>(model.Walkable);
        }

        public void MakeCustom()
        {
            if (shape == ArenaShape.Custom) return;
            floorCells = new List<Vector2Int>(GetCells());
            shape = ArenaShape.Custom;
        }

        public void ValidateMap(List<string> errors)
        {
            if (size < 5 || size > TrailFieldModel.MaxSize) return;
            var cells = GetCells();
            if (cells.Count < 2)
            { errors.Add("전장: START/END를 위한 바닥 타일이 최소 2개 필요합니다."); return; }
            if (overridePlayerStart && !cells.Contains(playerStart))
                errors.Add("전장: 플레이어 시작 위치를 현재 바닥 타일 위에 지정하세요.");
            var testModel = new TrailFieldModel();
            ApplyTo(testModel);
            try { testModel.BeginRound(0, true, overridePlayerStart ? (Vector2Int?)playerStart : null); }
            catch (InvalidOperationException exception)
            { errors.Add("전장: START/END 허용 영역에 서로 연결된 다른 타일이 필요합니다. " + exception.Message); }
            if (shape != ArenaShape.Custom) return;
            var pending = new Queue<Vector2Int>();
            foreach (var cell in cells) { pending.Enqueue(cell); break; }
            var visited = new HashSet<Vector2Int>();
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                if (!cells.Contains(cell) || !visited.Add(cell)) continue;
                pending.Enqueue(cell + Vector2Int.up);
                pending.Enqueue(cell + Vector2Int.down);
                pending.Enqueue(cell + Vector2Int.left);
                pending.Enqueue(cell + Vector2Int.right);
            }
            if (visited.Count != cells.Count)
                errors.Add("전장: 모든 바닥 타일을 상하좌우로 연결하세요. 분리된 영역에서는 START/END 경로가 끊깁니다.");
        }
    }

    [Serializable]
    public sealed class BossPhaseDefinition
    {
        public string name = "Phase";
        [Min(1)] public int health = 150;
        [Min(0)] public float initialDelay = 2.4f;
        [Min(0)] public float interval = 1.85f;
        [Min(0)] public float acceleration = 0.1f;
        [Min(0)] public float minimumInterval = 0.45f;
        public bool shuffle;
        public List<EncounterPattern> patterns = new List<EncounterPattern>();
        public bool backgroundEnabled;
        [Tooltip("Independent timeline that repeats during the phase.")]
        public EncounterPattern background = new EncounterPattern
        {
            name = "Background",
            minimumDuration = 1f
        };
        [Tooltip("Compatibility with the Crimson Golem crystal system.")]
        public bool legacyCrystals;
    }

    [CreateAssetMenu(menuName = "Trace Strike/Boss Encounter Definition")]
    public sealed class BossEncounterDefinition : ScriptableObject
    {
        public string id = "boss";
        public string displayName = "Boss";
        public Sprite portrait;
        public BossArenaDefinition arena = new BossArenaDefinition();
        [Tooltip("Reusable helper timelines owned by this encounter and callable from phase patterns.")]
        public List<EncounterPattern> libraryPatterns = new List<EncounterPattern>();
        public List<BossPhaseDefinition> phases = new List<BossPhaseDefinition>
        {
            new BossPhaseDefinition { patterns = new List<EncounterPattern> { new EncounterPattern() } }
        };

        public EncounterPattern FindPattern(string patternId)
        {
            if (string.IsNullOrEmpty(patternId)) return null;
            if (libraryPatterns != null)
                foreach (EncounterPattern pattern in libraryPatterns)
                    if (pattern != null && pattern.id == patternId) return pattern;
            if (phases == null) return null;
            foreach (BossPhaseDefinition phase in phases)
            {
                if (phase == null) continue;
                if (phase.backgroundEnabled && phase.background != null &&
                    phase.background.id == patternId) return phase.background;
                if (phase.patterns == null) continue;
                foreach (EncounterPattern pattern in phase.patterns)
                    if (pattern != null && pattern.id == patternId) return pattern;
            }
            return null;
        }

        public IEnumerable<EncounterPattern> AllPatterns()
        {
            if (libraryPatterns != null)
                foreach (EncounterPattern pattern in libraryPatterns)
                    if (pattern != null) yield return pattern;
            if (phases == null) yield break;
            foreach (BossPhaseDefinition phase in phases)
            {
                if (phase == null) continue;
                if (phase.backgroundEnabled && phase.background != null) yield return phase.background;
                if (phase.patterns == null) continue;
                foreach (EncounterPattern pattern in phase.patterns)
                    if (pattern != null) yield return pattern;
            }
        }

        public List<string> ValidateDefinition()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(id)) errors.Add("Boss requires a unique, stable ID.");
            if (arena == null) errors.Add("Boss requires arena settings.");
            else
            {
                if (arena.size < 5 || arena.size > TrailFieldModel.MaxSize)
                    errors.Add("Arena size must be from 5 to 50 (odd or even).");
                if (!Enum.IsDefined(typeof(ArenaShape), arena.shape))
                    errors.Add("Arena shape is invalid.");
                if (!FinitePositive(arena.cameraZoom) || !FinitePositive(arena.playerSizeRatio))
                    errors.Add("Arena camera zoom and player size must be finite and positive.");
                arena.ValidateMap(errors);
            }
            if (phases == null || phases.Count == 0) errors.Add("Boss requires at least one phase.");

            var ids = new HashSet<string>();
            foreach (EncounterPattern pattern in AllPatterns())
            {
                if (string.IsNullOrWhiteSpace(pattern.id)) errors.Add("Every pattern requires an ID.");
                else if (!ids.Add(pattern.id)) errors.Add("Duplicate pattern ID: " + pattern.id);
                errors.AddRange(PatternValidation.Errors(pattern, FindPattern));
            }

            if (phases != null)
            foreach (BossPhaseDefinition phase in phases)
            {
                if (phase == null) { errors.Add("Missing phase."); continue; }
                if (phase.health < 1) errors.Add(phase.name + ": health must be positive.");
                foreach (float value in new[] { phase.initialDelay, phase.interval, phase.minimumInterval, phase.acceleration })
                    if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                        errors.Add(phase.name + ": timing must be finite and nonnegative.");
                bool hasEnabledAttack = false;
                if (phase.patterns != null)
                    foreach (EncounterPattern pattern in phase.patterns)
                        if (pattern != null && pattern.enabled) { hasEnabledAttack = true; break; }
                bool hasEnabledBackground = phase.backgroundEnabled &&
                    phase.background != null && phase.background.enabled;
                if (!hasEnabledAttack && !hasEnabledBackground)
                    errors.Add(phase.name + ": add at least one pattern or background timeline.");
                if (phase.backgroundEnabled)
                {
                    if (phase.background == null)
                    { errors.Add(phase.name + ": missing background timeline."); continue; }
                    if (phase.background.Duration <= 0) errors.Add(phase.name + ": background duration must be positive.");
                }
            }
            return errors;
        }

        private static bool FinitePositive(float value) => value > 0f &&
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
