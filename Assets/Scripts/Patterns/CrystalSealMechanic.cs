using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    [Serializable]
    public sealed class CrystalPlacement
    {
        public Vector2Int cell;
        public string patternId = ""; // Empty means the group's attack.
        public bool overrideTiming;
        [Min(0)] public float initialDelay = 5;
        [Min(0.01f)] public float interval = 5;
    }

    [Serializable]
    public sealed class CrystalSealMechanic : BossMechanicDefinition
    {
        public List<CrystalPlacement> crystals = new List<CrystalPlacement>();
        public string attackPatternId = "";
        [Min(0)] public float initialDelay = 5;
        [Min(0.01f)] public float interval = 5;
        public GameObject activePrefab, inactivePrefab;
        public Sprite activeSprite, inactiveSprite;
        public Color activeTint = Color.white;
        public Color inactiveTint = new Color(0.35f, 0.35f, 0.4f, 0.55f);

        public CrystalSealMechanic() { name = "수정 봉인"; }
        public override IEnumerable<Vector2Int> PlacementCells
        { get { if (crystals != null) foreach (var crystal in crystals) if (crystal != null) yield return crystal.cell; } }
        public override BossMechanicRuntime Create(MechanicContext context) => new CrystalSealRuntime(this, context);
        public string PatternId(CrystalPlacement crystal) => string.IsNullOrEmpty(crystal.patternId) ? attackPatternId : crystal.patternId;
        public float Delay(CrystalPlacement crystal) => crystal.overrideTiming ? crystal.initialDelay : initialDelay;
        public float Interval(CrystalPlacement crystal) => crystal.overrideTiming ? crystal.interval : interval;
        public override void Validate(BossEncounterDefinition boss, List<string> errors)
        {
            if (crystals == null || crystals.Count == 0) { errors.Add(name + ": 수정을 한 개 이상 배치하세요."); return; }
            var floor = boss.arena.GetCells(); var used = new HashSet<Vector2Int>();
            foreach (var crystal in crystals)
            {
                if (crystal == null) { errors.Add(name + ": 수정 배치 데이터가 없습니다."); continue; }
                if (!floor.Contains(crystal.cell) || !used.Add(crystal.cell)) errors.Add(name + ": 수정은 서로 다른 바닥 타일에 배치하세요. " + crystal.cell);
                var pattern = boss.FindPattern(PatternId(crystal));
                if (pattern == null || !pattern.enabled || pattern.Duration <= 0) errors.Add(name + ": 사용할 공격 패턴을 연결하세요.");
                float delay = Delay(crystal), interval = Interval(crystal);
                if (!Finite(delay) || delay < 0 || !Finite(interval) || interval < 0.01f || (pattern != null && interval < pattern.Duration))
                    errors.Add(name + ": 첫 대기는 0 이상, 반복 주기는 공격 길이 이상이어야 합니다.");
            }
            foreach (var prefab in new[] { activePrefab, inactivePrefab })
                if (prefab != null && prefab.GetComponent<RectTransform>() == null)
                    errors.Add(name + ": 장치 외형은 RectTransform을 가진 UI 프리팹을 사용하세요.");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class CrystalSealRuntime : BossMechanicRuntime
    {
        public sealed class Device
        {
            public Vector2Int Cell { get; internal set; }
            public bool Active { get; internal set; } = true;
            internal EncounterPattern pattern;
            internal float interval, untilAttack;
            internal PatternRunner attack;
            internal IPatternLease visual;
        }
        private readonly CrystalSealMechanic definition;
        private readonly MechanicContext context;
        private readonly List<Device> devices = new List<Device>();
        private bool disposed;
        public IReadOnlyList<Device> Devices => devices;
        public int ActiveCount { get { int count = 0; foreach (var d in devices) if (d.Active) count++; return count; } }
        public override int MinimumBossHealth => !disposed && ActiveCount > 0 ? 1 : 0;
        public override string Status => disposed ? "" : definition.name + " " + (devices.Count - ActiveCount) + "/" + devices.Count;
        public override IEnumerable<Vector2Int> RequiredCells
        { get { if (!disposed) foreach (var device in devices) if (device.Active) yield return device.Cell; } }

        public CrystalSealRuntime(CrystalSealMechanic definition, MechanicContext context)
        {
            this.definition = definition; this.context = context;
            try
            {
                foreach (var placement in definition.crystals)
                {
                    var pattern = context.ResolvePattern(definition.PatternId(placement));
                    if (pattern == null || !pattern.enabled || pattern.Duration <= 0 ||
                        !ValidTime(definition.Interval(placement)) || definition.Interval(placement) < Math.Max(0.01f, pattern.Duration) ||
                        !ValidTime(definition.Delay(placement))) throw new InvalidOperationException("Invalid crystal attack schedule.");
                    var device = new Device { Cell = placement.cell, pattern = pattern,
                        interval = definition.Interval(placement), untilAttack = definition.Delay(placement) };
                    devices.Add(device);
                    device.visual = context.ShowDevice(device.Cell, definition.activePrefab, definition.activeSprite, definition.activeTint);
                }
            }
            catch { Dispose(); throw; }
        }
        private static bool ValidTime(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        public override void Advance(float delta)
        {
            if (!ValidTime(delta)) throw new ArgumentOutOfRangeException(nameof(delta));
            if (disposed || !context.Host.IsAlive) return;
            foreach (var device in devices)
            {
                if (!device.Active || !context.Host.IsAlive) continue;
                float remaining = delta;
                do
                {
                    if (device.untilAttack <= 0)
                    {
                        device.attack?.Dispose();
                        device.attack = new PatternRunner(device.pattern, new PatternContext(context.Host, device.Cell, 0, context.ResolvePattern));
                        device.attack.Advance(0);
                        device.untilAttack = device.interval;
                    }
                    if (!context.Host.IsAlive) return;
                    float step = Mathf.Min(remaining, device.untilAttack);
                    device.attack?.Advance(step);
                    device.untilAttack -= step; remaining -= step;
                } while (remaining > 0 || device.untilAttack <= 0);
            }
        }
        public override void OnPlayerAttack(IReadOnlyCollection<Vector2Int> completedTrail)
        {
            if (disposed) return;
            var hit = new HashSet<Vector2Int>(completedTrail);
            foreach (var device in devices)
            {
                if (!device.Active || !hit.Contains(device.Cell)) continue;
                device.Active = false;
                device.attack?.Dispose(); device.attack = null;
                device.visual?.Dispose(); device.visual = null;
                device.visual = context.ShowDevice(device.Cell, definition.inactivePrefab != null ? definition.inactivePrefab :
                    definition.inactiveSprite != null ? null : definition.activePrefab,
                    definition.inactiveSprite != null ? definition.inactiveSprite : definition.activeSprite, definition.inactiveTint);
            }
        }
        public override void Dispose()
        {
            if (disposed) return;
            disposed = true;
            List<Exception> errors = null;
            foreach (var device in devices)
            {
                try { device.attack?.Dispose(); } catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
                try { device.visual?.Dispose(); } catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            }
            if (errors != null) throw new AggregateException(errors);
        }
    }
}
