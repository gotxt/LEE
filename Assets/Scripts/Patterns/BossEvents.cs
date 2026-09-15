using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    [Serializable]
    public abstract class BossEvent : PatternEvent
    {
        public static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        protected static IBossPatternHost Host(PatternContext context) => context.Host as IBossPatternHost ??
            throw new InvalidOperationException("This pattern host does not support boss presentation.");
        public virtual void ValidateBoss(BossVisualDefinition visual, List<string> errors)
        {
            if (visual == null || visual.prefab == null) errors.Add("Boss event requires a BossActor prefab in the encounter.");
        }
    }

    [Serializable]
    public sealed class BossAnimationEvent : BossEvent
    {
        public string state = "";
        [Min(0)] public float transition = 0.1f;
        [Min(0.01f)] public float speed = 1f;
        public override void Validate(List<string> errors, float duration)
        {
            if (string.IsNullOrWhiteSpace(state)) errors.Add("Boss animation: choose an Animator state.");
            if (!Finite(transition) || transition < 0 || !Finite(speed) || speed <= 0)
                errors.Add("Boss animation: invalid transition or playback speed.");
        }
        public override void ValidateBoss(BossVisualDefinition visual, List<string> errors)
        {
            base.ValidateBoss(visual, errors);
            if (visual?.prefab != null && visual.prefab.Animator == null) errors.Add("Boss animation requires an Animator.");
        }
        public override PatternAction Create(PatternContext c, float duration)
        {
            IPatternLease lease = null;
            return new CallbackAction {
                begin = () => lease = c.Own(Host(c).BossAnimation(this)),
                // Animator keeps playing until another state starts or the owning pattern ends.
                end = cancelled => { if (cancelled) lease?.Dispose(); }
            };
        }
    }

    [Serializable]
    public sealed class BossVfxEvent : BossEvent
    {
        public GameObject prefab;
        public string socket = "";
        public bool follow = true;
        public Vector3 offset;
        [Min(0.01f)] public float scale = 1f;
        public override void Validate(List<string> errors, float duration)
        {
            if (prefab == null) errors.Add("Boss VFX requires an effect prefab.");
            if (!Finite(scale) || scale <= 0 || !Finite(offset.x) || !Finite(offset.y) || !Finite(offset.z)) errors.Add("Boss VFX: invalid transform.");
            if (duration <= 0) errors.Add("Boss VFX requires a positive duration.");
        }
        public override void ValidateBoss(BossVisualDefinition visual, List<string> errors)
        {
            base.ValidateBoss(visual, errors);
            if (visual?.prefab != null)
                try { visual.prefab.Socket(socket); } catch (InvalidOperationException e) { errors.Add(e.Message); }
        }
        public override PatternAction Create(PatternContext c, float duration)
        {
            IPatternLease lease = null;
            return new CallbackAction { begin = () => lease = c.Own(Host(c).BossVfx(this)), end = cancelled => lease?.Dispose() };
        }
    }

    [Serializable]
    public sealed class BossMotionEvent : BossEvent
    {
        public bool move = true, rotate, resize, shake;
        public Vector2 translation;
        public float rotation;
        public Vector2 scale = Vector2.one;
        [Min(0)] public float shakeStrength = 0.1f;
        [Min(0)] public float shakeFrequency = 12f;
        [Min(0)] public float returnTime = 0.2f;
        public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float Weight(float elapsed, float duration)
        {
            float forward = Mathf.Max(0, duration - returnTime);
            float p = elapsed <= forward && forward > 0 ? elapsed / forward :
                returnTime > 0 ? 1 - (elapsed - forward) / returnTime : 1;
            return curve.Evaluate(Mathf.Clamp01(p));
        }
        public override void Validate(List<string> errors, float duration)
        {
            if (duration <= 0 || !Finite(returnTime) || returnTime < 0 || returnTime > duration || curve == null || curve.length == 0)
                errors.Add("Boss motion: positive duration, a curve and return time within duration are required.");
            foreach (float value in new[] { translation.x, translation.y, rotation, scale.x, scale.y, shakeStrength, shakeFrequency })
                if (!Finite(value)) { errors.Add("Boss motion: all values must be finite."); break; }
            if (scale.x <= 0 || scale.y <= 0 || shakeStrength < 0 || shakeFrequency < 0) errors.Add("Boss motion: invalid scale or shake.");
        }
        public override PatternAction Create(PatternContext c, float duration)
        {
            IPatternLease lease = null;
            return new CallbackAction { begin = () => lease = c.Own(Host(c).BossMotion(this, duration)),
                tick = (t, dt) => lease?.SetProgress(duration <= 0 ? 1 : t / duration), end = cancelled => lease?.Dispose() };
        }
    }
}
