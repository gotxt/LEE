using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    // A named, typed output port. Keys are stable; labels are free to change.
    // This consumer needs position lists, not strings parsed as commands or boss IDs.
    public readonly struct MechanicPositionOutput
    {
        public readonly string Key, Label;
        public MechanicPositionOutput(string key, string label) { Key = key; Label = label; }
    }
    public interface IMechanicOutputHost
    {
        IReadOnlyCollection<Vector2Int> CaptureMechanicPositions(string mechanicId, string outputKey);
    }
    public interface IEncounterPatternCall
    {
        string PatternId { get; }
    }

    [Serializable]
    public sealed class CallAtMechanicPositionsEvent : PatternEvent, IEncounterPatternCall
    {
        [HideInInspector] public string mechanicId;
        [HideInInspector] public string outputKey;
        [HideInInspector] public string patternId;
        public Vector2Int originOffset;
        public string PatternId => patternId;
        public override void Validate(List<string> errors, float duration)
        {
            if (string.IsNullOrWhiteSpace(mechanicId) || string.IsNullOrWhiteSpace(outputKey))
                errors.Add("기믹 위치 호출: 기믹과 위치 출력 값을 선택하세요.");
            if (string.IsNullOrWhiteSpace(patternId)) errors.Add("기믹 위치 호출: 호출할 패턴을 선택하세요.");
        }
        public override PatternAction Create(PatternContext context, float duration)
        {
            var children = new List<PatternRunner>();
            return new CallbackAction
            {
                begin = () =>
                {
                    var pattern = context.ResolveEncounterPattern?.Invoke(patternId);
                    if (pattern == null || duration < pattern.Duration)
                        throw new InvalidOperationException("기믹 위치 호출: 패턴이 없거나 호출 구간이 자식 패턴보다 짧습니다.");
                    var host = context.Host as IMechanicOutputHost ??
                        throw new InvalidOperationException("기믹 출력 호스트가 없습니다. 기믹 합동 미리보기를 사용하세요.");
                    // Freeze ONCE at this event's start, before starting any child callbacks.
                    var origins = host.CaptureMechanicPositions(mechanicId, outputKey)
                        .Distinct().OrderBy(c => c.y).ThenBy(c => c.x).ToArray();
                    foreach (var origin in origins)
                    {
                        var child = context.Own(new PatternRunner(pattern, context.CreateChild(origin + originOffset)));
                        children.Add(child);
                    }
                    foreach (var child in children) child.Advance(0);
                },
                tick = (t, dt) => { foreach (var child in children) child.Advance(dt); },
                end = cancelled =>
                {
                    var errors = new List<Exception>();
                    foreach (var child in children) try { child.Dispose(); } catch (Exception error) { errors.Add(error); }
                    children.Clear();
                    if (errors.Count > 0) throw new AggregateException(errors);
                }
            };
        }
    }

    public static class MechanicOutputValidation
    {
        public static bool UsesOutputs(EncounterPattern pattern, Func<string, EncounterPattern> resolve)
        {
            var visited = new HashSet<EncounterPattern>();
            bool Visit(EncounterPattern current)
            {
                if (current == null || !visited.Add(current)) return false;
                foreach (var clip in current.clips.Where(c => c != null && c.enabled))
                    if (clip.action is CallAtMechanicPositionsEvent ||
                        (clip.action is IEncounterPatternCall call && Visit(resolve?.Invoke(call.PatternId)))) return true;
                return false;
            }
            return Visit(pattern);
        }
        public static void Validate(BossEncounterDefinition boss, List<string> errors)
        {
            var definitions = (boss.phases ?? new List<BossPhaseDefinition>()).Where(p => p != null)
                .SelectMany(p => p.mechanics ?? new List<BossMechanicDefinition>()).Where(m => m != null).ToArray();
            foreach (var group in definitions.Where(m => !string.IsNullOrEmpty(m.connectionId)).GroupBy(m => m.connectionId))
                if (group.Count() > 1) errors.Add("기믹 연결 ID 중복: " + group.Key + ". 복제한 기믹의 연결 ID를 새로 발급하세요.");
            foreach (var definition in definitions)
            {
                var keys = new HashSet<string>();
                foreach (var output in definition.PositionOutputs)
                    if (string.IsNullOrWhiteSpace(output.Key) || !keys.Add(output.Key))
                        errors.Add(definition.name + ": 위치 출력 키가 비어 있거나 중복입니다.");
            }
            foreach (var pattern in boss.AllPatterns())
                foreach (var clip in pattern.clips.Where(c => c != null && c.enabled))
                    if (clip.action is CallAtMechanicPositionsEvent call)
                    {
                        var source = definitions.FirstOrDefault(m => !string.IsNullOrEmpty(call.mechanicId) && m.connectionId == call.mechanicId);
                        if (source == null || !source.enabled) errors.Add(pattern.name + ": 연결한 기믹이 없거나 꺼져 있습니다.");
                        else if (!source.PositionOutputs.Any(o => o.Key == call.outputKey))
                            errors.Add(pattern.name + ": 기믹의 위치 출력 값을 찾을 수 없습니다: " + call.outputKey);
                    }
            // Validate the active phase, including reusable children and mechanic-owned attacks.
            foreach (var phase in boss.phases ?? new List<BossPhaseDefinition>())
            {
                if (phase == null) continue;
                var active = (phase.mechanics ?? new List<BossMechanicDefinition>()).Where(m => m != null && m.enabled).ToArray();
                var available = new HashSet<string>(active.Select(m => m.connectionId));
                var visited = new HashSet<EncounterPattern>();
                void Visit(EncounterPattern pattern)
                {
                    if (pattern == null || !visited.Add(pattern)) return;
                    foreach (var clip in pattern.clips.Where(c => c != null && c.enabled))
                    {
                        if (clip.action is CallAtMechanicPositionsEvent positions && !available.Contains(positions.mechanicId))
                            errors.Add(phase.name + "/" + pattern.name + ": 다른 페이즈의 기믹을 호출합니다. 현재 페이즈의 기믹을 연결하세요.");
                        if (clip.action is IEncounterPatternCall call) Visit(boss.FindPattern(call.PatternId));
                    }
                }
                foreach (var pattern in phase.patterns ?? new List<EncounterPattern>()) if (pattern != null && pattern.enabled) Visit(pattern);
                if (phase.backgroundEnabled && phase.background != null && phase.background.enabled) Visit(phase.background);
                foreach (var mechanic in active) foreach (var id in mechanic.ReferencedPatternIds) Visit(boss.FindPattern(id));
            }
        }
    }
}
