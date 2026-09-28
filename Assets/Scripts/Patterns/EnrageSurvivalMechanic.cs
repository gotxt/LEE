using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    // Optional phase health policy. The game owns HP; mechanics supply changes and immunity.
    public interface IBossHealthMechanic
    {
        bool BlocksPlayerDamage { get; }
        int EnterEnrage(int health);
        int ApplyTimedHealthChange(int health);
    }

    [Serializable]
    public sealed class EnrageSurvivalMechanic : BossMechanicDefinition
    {
        [Min(0), InspectorName("광폭화 진입 추가 체력")] public int bonusHealth = 60;
        [Min(.01f), InspectorName("체력 감소 간격 (초)")] public float tickInterval = 1;
        [Min(1), InspectorName("간격당 감소 체력")] public int healthPerTick = 1;
        public EnrageSurvivalMechanic() { name = "광폭화 생존전"; }
        public override BossMechanicRuntime Create(MechanicContext context) => new EnrageSurvivalRuntime(this, context);
        internal void ValidateSettings(List<string> errors)
        {
            if (bonusHealth < 0 || float.IsNaN(tickInterval) || float.IsInfinity(tickInterval) || tickInterval < .01f || healthPerTick < 1)
                errors.Add(name + ": 추가 체력은 0 이상, 감소 간격은 0.01초 이상, 감소 체력은 1 이상이어야 합니다.");
        }
        public override void Validate(BossEncounterDefinition boss, List<string> errors)
        {
            ValidateSettings(errors);
            foreach (var phase in boss.phases.Where(p => p != null && p.mechanics != null && p.mechanics.Contains(this)))
                if (phase.mechanics.Count(m => m is EnrageSurvivalMechanic && m.enabled) > 1)
                    errors.Add(phase.name + ": 광폭화 생존전 기믹은 페이즈당 하나만 사용하세요.");
        }
    }

    public sealed class EnrageSurvivalRuntime : BossMechanicRuntime, IBossHealthMechanic
    {
        private readonly IPatternHost host;
        private readonly int bonusHealth, healthPerTick;
        private readonly double tickInterval;
        private double elapsed;
        private int pendingLoss;
        private bool active, disposed;
        public bool BlocksPlayerDamage => active && !disposed;
        public override string Status => disposed ? "" : active ? "생존전 · 공격 피해 면역" : "생존전 · 광폭화 대기";

        public EnrageSurvivalRuntime(EnrageSurvivalMechanic definition, MechanicContext context)
        {
            var errors = new List<string>(); definition.ValidateSettings(errors);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            host = context.Host;
            bonusHealth = definition.bonusHealth; healthPerTick = definition.healthPerTick; tickInterval = definition.tickInterval;
        }
        public int EnterEnrage(int health)
        {
            if (disposed || active || health <= 0) return health;
            active = true;
            return (int)Math.Min(int.MaxValue, (long)health + bonusHealth);
        }
        public override void Advance(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            if (!BlocksPlayerDamage || !host.IsAlive) return;
            elapsed += delta;
            double ticks = Math.Floor((elapsed + 1e-8) / tickInterval);
            if (ticks < 1) return;
            elapsed = Math.Max(0, elapsed - ticks * tickInterval);
            pendingLoss = (int)Math.Min(int.MaxValue, pendingLoss + ticks * healthPerTick);
        }
        public int ApplyTimedHealthChange(int health)
        {
            if (!BlocksPlayerDamage) return health;
            int result = Math.Max(0, health - pendingLoss);
            pendingLoss = 0;
            return result;
        }
        public override void Dispose() { disposed = true; active = false; elapsed = 0; pendingLoss = 0; }
    }
}
