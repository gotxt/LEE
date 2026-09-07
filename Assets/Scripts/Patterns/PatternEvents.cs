using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    internal sealed class CallbackAction : PatternAction
    {
        public Action begin;
        public Action<float, float> tick;
        public Action<bool> end;
        public override void Begin() => begin?.Invoke();
        public override void Tick(float elapsed, float delta) => tick?.Invoke(elapsed, delta);
        public override void End(bool cancelled) => end?.Invoke(cancelled);
    }

    [Serializable]
    public sealed class WaitEvent : PatternEvent
    {
        public override PatternAction Create(PatternContext c, float duration) => new CallbackAction();
    }

    [Serializable]
    public sealed class WarningEvent : PatternEvent
    {
        public TileSelection tiles = new TileSelection();
        public Color color = new Color(1, 0.25f, 0.35f, 0.65f);
        public override PatternAction Create(PatternContext c, float duration)
        {
            IPatternLease lease = null;
            return new CallbackAction {
                begin = () => lease = c.Own(c.Host.Mark(tiles.Resolve(c), color, true)),
                tick = (t, dt) => lease?.SetProgress(duration <= 0 ? 1 : t / duration),
                end = cancelled => lease?.Dispose()
            };
        }
    }

    [Serializable]
    public sealed class DamageEvent : PatternEvent
    {
        public TileSelection tiles = new TileSelection();
        [Min(0)] public float escapeGrace = 0.18f;
        public string reason = "Pattern";
        public override void Validate(List<string> errors, float duration)
        {
            if (escapeGrace < 0 || float.IsNaN(escapeGrace) || float.IsInfinity(escapeGrace) || duration < escapeGrace)
                errors.Add("Damage duration must cover its nonnegative escape grace.");
        }
        public override PatternAction Create(PatternContext c, float duration)
        {
            HashSet<Vector2Int> cells = null;
            Vector2Int atImpact = default;
            bool applied = false;
            IPatternLease lease = null;
            return new CallbackAction {
                begin = () => { cells = tiles.Resolve(c); atImpact = c.Host.PlayerCell;
                    lease = c.Own(c.Host.Mark(cells, new Color(1, 0.2f, 0.1f, 0.8f), false)); },
                tick = (t, dt) => {
                    if (applied || t + 0.00001f < escapeGrace) return;
                    applied = true;
                    if (CombatBalanceRules.ShouldApplyExplosionDamage(cells, atImpact, c.Host.PlayerCell)) c.Host.Damage(reason);
                },
                end = cancelled => lease?.Dispose()
            };
        }
    }

    [Serializable]
    public sealed class HazardEvent : PatternEvent
    {
        public TileSelection tiles = new TileSelection();
        public string key = "hazard";
        public string reason = "Hazard";
        [Tooltip("Keep until RemoveResourceEvent or sequence cancellation/completion.")]
        public bool persist;
        public override PatternAction Create(PatternContext c, float duration)
        {
            IPatternLease lease = null;
            return new CallbackAction { begin = () => lease = c.Own(c.Host.Hazard(tiles.Resolve(c), reason), key),
                end = cancelled => { if (!persist || cancelled) lease?.Dispose(); } };
        }
    }

    [Serializable]
    public sealed class ObstacleEvent : PatternEvent
    {
        public TileSelection tiles = new TileSelection();
        public string key = "wall";
        public bool persist;
        public override PatternAction Create(PatternContext c, float duration)
        {
            IPatternLease lease = null;
            return new CallbackAction { begin = () => lease = c.Own(c.Host.Block(tiles.Resolve(c)), key),
                end = cancelled => { if (!persist || cancelled) lease?.Dispose(); } };
        }
    }

    [Serializable]
    public sealed class SpawnEvent : PatternEvent
    {
        public string key = "device";
        public GameObject prefab;
        public Sprite sprite;
        public Color color = Color.white;
        public Vector2Int cellOffset;
        public bool relativeToOrigin = true;
        public bool persist;
        public override void Validate(List<string> errors, float duration)
        { if (string.IsNullOrWhiteSpace(key) || key == "$boss") errors.Add("Spawn requires a nonempty key other than $boss."); }
        public override PatternAction Create(PatternContext c, float duration)
        {
            IPatternLease lease = null;
            return new CallbackAction { begin = () => lease = c.Own(c.Host.Spawn(c.ObjectKey(key), prefab,
                    (relativeToOrigin ? c.Origin : c.Host.CenterCell) + cellOffset, sprite, color), key),
                end = cancelled => { if (!persist || cancelled) lease?.Dispose(); } };
        }
    }

    [Serializable]
    public sealed class VfxEvent : PatternEvent
    {
        public GameObject prefab;
        public Sprite sprite;
        public Color color = new Color(1, 0.6f, 0.2f, 0.8f);
        public TileSelection tiles = new TileSelection();
        public override PatternAction Create(PatternContext c, float duration)
        {
            var leases = new List<IPatternLease>();
            return new CallbackAction { begin = () => {
                foreach (var cell in tiles.Resolve(c)) leases.Add(c.Own(c.Host.Spawn(c.ObjectKey(Guid.NewGuid().ToString()), prefab, cell, sprite, color)));
            }, end = cancelled => { foreach (var lease in leases) lease?.Dispose(); } };
        }
    }

    [Serializable]
    public sealed class SfxEvent : PatternEvent
    {
        public AudioClip clip;
        [Range(0, 1)] public float volume = 0.7f;
        public override void Validate(List<string> errors, float duration)
        { if (clip == null) errors.Add("SFX requires an AudioClip."); if (duration <= 0) errors.Add("SFX requires a positive duration."); }
        public override PatternAction Create(PatternContext c, float duration)
        {
            IPatternLease lease = null;
            return new CallbackAction { begin = () => lease = c.Own(c.Host.Sound(clip, volume)),
                end = cancelled => lease?.Dispose() };
        }
    }

    [Serializable]
    public sealed class MoveObjectEvent : PatternEvent
    {
        [Tooltip("$boss or the key of a SpawnEvent in this sequence.")]
        public string key = "$boss";
        public Vector2Int destination;
        public AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public override PatternAction Create(PatternContext c, float duration)
        {
            IPatternLease lease = null;
            return new CallbackAction { begin = () => lease = c.Own(c.Host.Motion(c.ObjectKey(key), c.Host.CenterCell + destination)),
                tick = (t, dt) => lease?.SetProgress(easing.Evaluate(duration <= 0 ? 1 : t / duration)) };
        }
    }

    [Serializable]
    public sealed class CameraEvent : PatternEvent
    {
        public Vector2 offset;
        [Min(0)] public float shake = 12;
        public override PatternAction Create(PatternContext c, float duration)
        {
            IPatternLease lease = null;
            return new CallbackAction { begin = () => lease = c.Own(c.Host.Camera(offset, shake)),
                tick = (t, dt) => lease?.SetProgress(duration <= 0 ? 1 : t / duration),
                end = cancelled => lease?.Dispose() };
        }
    }

    [Serializable]
    public sealed class RemoveResourceEvent : PatternEvent
    {
        public string key = "device";
        public override PatternAction Create(PatternContext c, float duration) =>
            new CallbackAction { begin = () => c.Remove(key) };
    }

    [Serializable]
    public sealed class SignalEvent : PatternEvent
    {
        public string signal;
        public string argument;
        public override PatternAction Create(PatternContext c, float duration) =>
            new CallbackAction { begin = () => c.Host.Signal(signal, argument) };
    }

    [Serializable]
    public sealed class CallPatternEvent : PatternEvent
    {
        public PatternSequence pattern;
        public Vector2Int originOffset;
        public bool anchorToPlayer;
        public override void Validate(List<string> errors, float duration)
        {
            if (pattern == null) errors.Add("CallPattern requires a shared pattern asset.");
            else if (duration < pattern.Duration)
                errors.Add("CallPattern duration must cover the child sequence (" + pattern.Duration + "s).");
        }
        public override PatternAction Create(PatternContext c, float duration)
        {
            PatternRunner child = null;
            return new CallbackAction { begin = () => {
                child = c.Own(new PatternRunner(pattern, c.CreateChild(
                    (anchorToPlayer ? c.Host.PlayerCell : c.Origin) + originOffset)));
                child.Advance(0);
            }, tick = (t, dt) => child?.Advance(dt), end = cancelled => child?.Dispose() };
        }
    }

    [Serializable]
    public sealed class CallEncounterPatternEvent : PatternEvent
    {
        public string patternId;
        public Vector2Int originOffset;
        public bool anchorToPlayer;

        public override void Validate(List<string> errors, float duration)
        {
            if (string.IsNullOrWhiteSpace(patternId))
                errors.Add("CallEncounterPattern requires a pattern selection.");
        }

        public override PatternAction Create(PatternContext c, float duration)
        {
            PatternRunner child = null;
            return new CallbackAction {
                begin = () => {
                    EncounterPattern pattern = c.ResolveEncounterPattern?.Invoke(patternId);
                    if (pattern == null)
                        throw new InvalidOperationException("Missing encounter pattern: " + patternId);
                    child = c.Own(new PatternRunner(pattern, c.CreateChild(
                        (anchorToPlayer ? c.Host.PlayerCell : c.Origin) + originOffset)));
                    child.Advance(0);
                },
                tick = (t, dt) => child?.Advance(dt),
                end = cancelled => child?.Dispose()
            };
        }
    }
}
