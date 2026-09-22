using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    // Assets describe rules; each phase execution owns separate state and resources.
    [Serializable]
    public abstract class BossMechanicDefinition
    {
        public string name = "기믹";
        public bool enabled = true;
        public virtual IEnumerable<Vector2Int> PlacementCells { get { yield break; } }
        public abstract BossMechanicRuntime Create(MechanicContext context);
        public abstract void Validate(BossEncounterDefinition boss, List<string> errors);
    }

    public abstract class BossMechanicRuntime : IDisposable
    {
        public virtual int MinimumBossHealth => 0;
        public virtual string Status => "";
        public virtual IEnumerable<Vector2Int> RequiredCells { get { yield break; } }
        public abstract void Advance(float delta);
        public virtual void OnPlayerAttack(IReadOnlyCollection<Vector2Int> completedTrail) { }
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

    public sealed class BossMechanicSession : IDisposable
    {
        private readonly List<BossMechanicRuntime> runtimes = new List<BossMechanicRuntime>();
        public IReadOnlyList<BossMechanicRuntime> Runtimes => runtimes;
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
                    if (definition != null && definition.enabled) runtimes.Add(definition.Create(context));
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
            if (errors != null) throw new AggregateException(errors);
        }
    }
}
