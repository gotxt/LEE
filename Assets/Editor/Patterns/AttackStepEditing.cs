#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NHN.TraceStrike.Patterns;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    // A view over existing clips, not a second runtime or a second copy of the timeline.
    internal static class AttackStepEditing
    {
        internal sealed class Step
        {
            public string Key;
            public PatternClip Warning, Damage;
            public readonly List<PatternClip> Members = new List<PatternClip>();
            public TileSelection Tiles => ((WarningEvent)Warning.action).tiles;
            public float End => Members.Max(c => c.start + c.duration);
            public string Name => string.IsNullOrEmpty(Warning.attackName) ? "공격" : Warning.attackName;
        }

        internal static TileSelection Tiles(PatternClip clip) =>
            clip?.action?.GetType().GetField("tiles")?.GetValue(clip.action) as TileSelection;

        internal static List<Step> Find(EncounterPattern pattern)
        {
            var result = new List<Step>();
            foreach (var keyed in pattern.clips.Where(c => !string.IsNullOrEmpty(Tiles(c)?.snapshotKey))
                         .GroupBy(c => Tiles(c).snapshotKey))
            {
                var warnings = keyed.Where(c => c.action is WarningEvent).ToList();
                var damage = keyed.Where(c => c.action is DamageEvent).ToList();
                // Reused keys / custom events may have intentional advanced semantics.
                // Never guess which warning owns which impact in those cases.
                if (warnings.Count != 1 || damage.Count != 1 ||
                    keyed.Any(c => !(c.action is WarningEvent || c.action is DamageEvent || c.action is VfxEvent))) continue;
                var w = warnings[0]; var d = damage[0];
                if (w.enabled != d.enabled || !Near(w.start + w.duration, d.start)) continue;
                var step = new Step { Key = keyed.Key, Warning = w, Damage = d };
                step.Members.AddRange(keyed);
                result.Add(step);
            }
            // Existing generated encounters have no ownership metadata. Only adopt
            // known legacy cue labels with an unambiguous exact timing match.
            foreach (var clip in pattern.clips)
            {
                if (clip == null || !(clip.action is SfxEvent || clip.action is CameraEvent)) continue;
                var owners = result.Where(s => !string.IsNullOrEmpty(clip.attackGroupKey)
                    ? s.Key == clip.attackGroupKey
                    : LegacyCueMatches(s, clip)).ToList();
                if (owners.Count == 1) owners[0].Members.Add(clip);
            }
            return result.OrderBy(s => s.Warning.start).ThenBy(s => pattern.clips.IndexOf(s.Warning)).ToList();
        }

        private static bool LegacyCueMatches(Step step, PatternClip clip)
        {
            PatternClip reference = clip.action is SfxEvent && clip.label == "Warning SFX" ? step.Warning :
                (clip.action is SfxEvent && clip.label == "Impact SFX") ||
                (clip.action is CameraEvent && clip.label == "Camera") ? step.Damage : null;
            return reference != null && Near(reference.start, clip.start) && Near(reference.duration, clip.duration);
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.0001f;
        private static float Safe(float value, float minimum) =>
            float.IsNaN(value) || float.IsInfinity(value) ? minimum : Mathf.Max(minimum, value);

        internal static Step Add(EncounterPattern pattern, float start)
        {
            string key = "attack_" + Guid.NewGuid().ToString("N");
            var tiles = new TileSelection { shape = TileShape.Cells, anchor = TileAnchor.Absolute,
                snapshotKey = key, cells = new List<Vector2Int>() };
            var warning = new PatternClip { label = "경고", start = Safe(start, 0), duration = 1,
                attackName = "새 공격", action = new WarningEvent { tiles = tiles } };
            var damage = new PatternClip { label = "타격", start = warning.start + 1, duration = 0.3f,
                action = new DamageEvent { tiles = CopyTiles(tiles), reason = "공격" } };
            pattern.clips.Add(warning); pattern.clips.Add(damage);
            var step = new Step { Key = key, Warning = warning, Damage = damage };
            step.Members.Add(warning); step.Members.Add(damage);
            Bind(step);
            return step;
        }

        internal static void Bind(Step step)
        {
            foreach (var clip in step.Members)
            {
                if (clip.attackGroupKey != step.Key)
                    clip.attackAtImpact = clip == step.Damage ||
                        (clip != step.Warning && clip.label != "Warning SFX" && clip.start >= step.Damage.start);
                clip.attackGroupKey = step.Key;
            }
        }

        internal static void SynchronizeArea(Step step)
        {
            Bind(step);
            step.Tiles.snapshotKey = step.Key;
            foreach (var clip in step.Members)
            {
                if (clip == step.Warning) continue;
                if (clip.action is DamageEvent damage) damage.tiles = CopyTiles(step.Tiles);
                else if (clip.action is VfxEvent vfx) vfx.tiles = CopyTiles(step.Tiles);
            }
        }

        internal static void SetTiming(Step step, float start, float warningDuration, float damageDuration)
        {
            Bind(step);
            start = Safe(start, 0); warningDuration = Safe(warningDuration, 0);
            damageDuration = Safe(damageDuration, Mathf.Max(0.01f, ((DamageEvent)step.Damage.action).escapeGrace));
            float oldStart = step.Warning.start, oldWarning = step.Warning.duration;
            float oldImpact = step.Damage.start, oldDamage = step.Damage.duration;
            foreach (var clip in step.Members)
            {
                if (clip == step.Warning || clip == step.Damage) continue;
                clip.start = Mathf.Max(0, clip.start + (clip.attackAtImpact ? start + warningDuration - oldImpact : start - oldStart));
                if (Near(clip.duration, clip.attackAtImpact ? oldDamage : oldWarning))
                    clip.duration = clip.attackAtImpact ? damageDuration : warningDuration;
                if (clip.action is SfxEvent) clip.duration = Mathf.Max(0.01f, clip.duration);
            }
            step.Warning.start = start; step.Warning.duration = warningDuration;
            step.Damage.start = start + warningDuration; step.Damage.duration = damageDuration;
        }

        internal static void SetEnabled(Step step, bool enabled)
        {
            Bind(step);
            foreach (var clip in step.Members) clip.enabled = enabled;
        }

        internal static Step Duplicate(EncounterPattern pattern, Step source, float start)
        {
            string key = "attack_" + Guid.NewGuid().ToString("N");
            var step = new Step { Key = key };
            foreach (var member in source.Members)
            {
                var copy = CopyClip(member);
                copy.attackGroupKey = key;
                copy.attackAtImpact = member == source.Damage ||
                    (member != source.Warning && (member.attackGroupKey == source.Key ? member.attackAtImpact : member.start >= source.Damage.start));
                if (Tiles(copy) != null) Tiles(copy).snapshotKey = key;
                pattern.clips.Add(copy); step.Members.Add(copy);
                if (member == source.Warning) step.Warning = copy;
                if (member == source.Damage) step.Damage = copy;
            }
            step.Warning.attackName = source.Name + " 복사";
            SynchronizeArea(step);
            SetTiming(step, start, source.Warning.duration, source.Damage.duration);
            return step;
        }

        internal static void Delete(EncounterPattern pattern, Step step) =>
            pattern.clips.RemoveAll(c => step.Members.Contains(c));

        internal static void SetSound(EncounterPattern pattern, Step step, bool impact, AudioClip sound)
        {
            Bind(step);
            var existing = step.Members.FirstOrDefault(c => c.action is SfxEvent && c.attackAtImpact == impact);
            if (sound == null)
            {
                if (existing != null) { pattern.clips.Remove(existing); step.Members.Remove(existing); }
                return;
            }
            if (existing == null)
            {
                var phase = impact ? step.Damage : step.Warning;
                existing = new PatternClip { label = impact ? "타격 소리" : "경고 소리", start = phase.start,
                    duration = Mathf.Max(0.01f, phase.duration), enabled = phase.enabled,
                    attackGroupKey = step.Key, attackAtImpact = impact, action = new SfxEvent() };
                pattern.clips.Add(existing); step.Members.Add(existing);
            }
            ((SfxEvent)existing.action).clip = sound;
        }

        internal static TileSelection CopyTiles(TileSelection source) =>
            JsonUtility.FromJson<TileSelection>(JsonUtility.ToJson(source));

        internal static PatternClip CopyClip(PatternClip source) => new PatternClip
        {
            label = source.label, enabled = source.enabled, start = source.start, duration = source.duration,
            attackGroupKey = source.attackGroupKey, attackAtImpact = source.attackAtImpact, attackName = source.attackName,
            action = source.action == null ? null : (PatternEvent)JsonUtility.FromJson(JsonUtility.ToJson(source.action), source.action.GetType())
        };
    }
}
#endif
