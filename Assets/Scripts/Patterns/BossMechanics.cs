using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    // Assets describe rules; each phase execution owns separate state and resources.
    [Serializable]
    public abstract class BossMechanicDefinition
    {
        public string name = "기믹";
        public bool enabled = true;
        // Legacy mechanics remain valid without an ID. The editor assigns/persists one on first connection.
        [HideInInspector] public string connectionId = "";
        public virtual IReadOnlyList<MechanicPositionOutput> PositionOutputs => Array.Empty<MechanicPositionOutput>();
        public virtual IEnumerable<string> ReferencedPatternIds => Array.Empty<string>();
        public virtual IEnumerable<Vector2Int> PlacementCells { get { yield break; } }
        public abstract BossMechanicRuntime Create(MechanicContext context);
        public abstract void Validate(BossEncounterDefinition boss, List<string> errors);
    }

    public abstract class BossMechanicRuntime : IDisposable
    {
        public virtual bool IsEnraged => false;
        public virtual IEnumerable<Vector2Int> ReadPositions(string outputKey) =>
            throw new InvalidOperationException("지원하지 않는 기믹 위치 출력: " + outputKey);
        public virtual int MinimumBossHealth => 0;
        public virtual string Status => "";
        public virtual IEnumerable<Vector2Int> RequiredCells { get { yield break; } }
        public abstract void Advance(float delta);
        public virtual void OnPlayerAttack(IReadOnlyCollection<Vector2Int> completedTrail) { }
        public virtual void OnPlayerStep(PlayerTileStep step) { }
        public abstract void Dispose();
    }

    // Optional presentation adapter. Preview and game share the same mechanic rules.
    public interface IMechanicPresentationHost
    {
        IPatternLease ShowDevice(Vector2Int cell, GameObject prefab, Sprite sprite, Color tint);
    }

    public sealed class MechanicContext
    {
        public readonly IPatternHost Host;
        public readonly Func<string, EncounterPattern> ResolvePattern;
        public MechanicContext(IPatternHost host, Func<string, EncounterPattern> resolvePattern)
        { Host = host; ResolvePattern = resolvePattern; }
        public IPatternLease ShowDevice(Vector2Int cell, GameObject prefab, Sprite sprite, Color tint) =>
            Host is IMechanicPresentationHost presentation ? presentation.ShowDevice(cell, prefab, sprite, tint) :
                Host.Spawn("mechanic/" + Guid.NewGuid().ToString("N"), prefab, cell, sprite, tint);
    }

    public sealed class BossMechanicSession : IDisposable, IMechanicOutputHost
    {
        private readonly List<BossMechanicRuntime> runtimes = new List<BossMechanicRuntime>();
        private readonly Dictionary<string, (BossMechanicRuntime runtime, HashSet<string> keys)> outputs =
            new Dictionary<string, (BossMechanicRuntime, HashSet<string>)>();
        public IReadOnlyCollection<Vector2Int> CaptureMechanicPositions(string mechanicId, string outputKey)
        {
            if (string.IsNullOrEmpty(mechanicId) || !outputs.TryGetValue(mechanicId, out var source))
                throw new InvalidOperationException("현재 페이즈에서 기믹 연결을 찾을 수 없습니다: " + mechanicId);
            if (string.IsNullOrEmpty(outputKey) || !source.keys.Contains(outputKey))
                throw new InvalidOperationException("기믹의 위치 출력 값을 찾을 수 없습니다: " + outputKey);
            return source.runtime.ReadPositions(outputKey).Distinct().OrderBy(c => c.y).ThenBy(c => c.x).ToArray();
        }
        public IReadOnlyList<BossMechanicRuntime> Runtimes => runtimes;
        public bool IsEnraged => runtimes.Exists(runtime => runtime.IsEnraged);
        public int MinimumBossHealth
        {
            get { int floor = 0; foreach (var runtime in runtimes) floor = Math.Max(floor, runtime.MinimumBossHealth); return floor; }
        }
        public IEnumerable<Vector2Int> RequiredCells
        { get { foreach (var runtime in runtimes) foreach (var cell in runtime.RequiredCells) yield return cell; } }
        public string Status
        { get { var texts = new List<string>(); foreach (var runtime in runtimes) if (!string.IsNullOrEmpty(runtime.Status)) texts.Add(runtime.Status); return string.Join(" · ", texts); } }

        public BossMechanicSession(IEnumerable<BossMechanicDefinition> definitions, MechanicContext context)
        {
            try
            {
                if (definitions != null) foreach (var definition in definitions)
                    if (definition != null && definition.enabled)
                    {
                        var runtime = definition.Create(context); runtimes.Add(runtime);
                        if (!string.IsNullOrEmpty(definition.connectionId))
                        {
                            if (outputs.ContainsKey(definition.connectionId)) throw new InvalidOperationException("Duplicate mechanic connection ID: " + definition.connectionId);
                            outputs.Add(definition.connectionId, (runtime, new HashSet<string>(definition.PositionOutputs.Select(o => o.Key))));
                        }
                    }
            }
            catch { Dispose(); throw; }
        }
        public void Advance(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            try { foreach (var runtime in runtimes) runtime.Advance(delta); }
            catch (Exception error) { DisposeAfterFailure(error); throw; }
        }
        public int ResolvePlayerAttack(IReadOnlyCollection<Vector2Int> completedTrail, int health, int damage)
        {
            if (health <= 0) return 0;
            // Snapshot before state changes: the attack completing the mechanic cannot kill.
            int floor = MinimumBossHealth;
            try { foreach (var runtime in runtimes) runtime.OnPlayerAttack(completedTrail); }
            catch (Exception error) { DisposeAfterFailure(error); throw; }
            return Math.Max(floor, Math.Max(0, health - Math.Max(0, damage)));
        }
        public void OnPlayerStep(PlayerTileStep step)
        {
            try { foreach (var runtime in runtimes) runtime.OnPlayerStep(step); }
            catch (Exception error) { DisposeAfterFailure(error); throw; }
        }
        private void DisposeAfterFailure(Exception failure)
        {
            try { Dispose(); }
            catch (Exception cleanupError) { throw new AggregateException(failure, cleanupError); }
        }
        public void Dispose()
        {
            List<Exception> errors = null;
            foreach (var runtime in runtimes)
                try { runtime.Dispose(); }
                catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            runtimes.Clear();
            outputs.Clear();
            if (errors != null) throw new AggregateException(errors);
        }
    }
}
