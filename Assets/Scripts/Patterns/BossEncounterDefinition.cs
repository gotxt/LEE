using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    public enum ArenaShape { Rounded, Triangle, Star }

    [Serializable]
    public sealed class BossArenaDefinition
    {
        [Range(5, TrailFieldModel.MaxSize)] public int size = 17;
        public ArenaShape shape = ArenaShape.Rounded;
        [Range(0.5f, 3f)] public float cameraZoom = 2.05f;
        [Range(0.25f, 1.5f)] public float playerSizeRatio = 0.78f;
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
                if (arena.size < 5 || arena.size > TrailFieldModel.MaxSize || arena.size % 2 == 0)
                    errors.Add("Arena size must be odd, from 5 to 17.");
                if (!Enum.IsDefined(typeof(ArenaShape), arena.shape))
                    errors.Add("Arena shape is invalid.");
                if (!FinitePositive(arena.cameraZoom) || !FinitePositive(arena.playerSizeRatio))
                    errors.Add("Arena camera zoom and player size must be finite and positive.");
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
