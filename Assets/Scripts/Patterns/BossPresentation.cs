using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    // Owns a single instance and its transient effects. Both editor and game use this controller.
    public sealed class BossPresentation : IBossPatternHost, IDisposable
    {
        private sealed class Lease : IPatternLease
        {
            private Action release;
            private readonly Action<float> progress;
            public Lease(Action release, Action<float> progress = null) { this.release = release; this.progress = progress; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
            public void SetProgress(float value) { if (release != null) progress?.Invoke(value); }
        }
        private sealed class Effect
        {
            public GameObject root;
            public ParticleSystem[] particles;
            public Animator[] animators;
        }
        public readonly BossActor Actor;
        public readonly Transform MotionRoot;
        private readonly Transform container;
        private readonly BossVisualDefinition definition;
        private readonly bool preview;
        private readonly Animator animator;
        private readonly ParticleSystem[] ambientParticles;
        private readonly List<Effect> effects = new List<Effect>();
        private long serial, animationOwner, moveOwner, rotateOwner, scaleOwner, shakeOwner;
        private Vector2 translation, shakeOffset, scale = Vector2.one;
        private float rotation;
        private bool disposed;

        public BossPresentation(BossVisualDefinition definition, Transform container, bool preview)
        {
            this.definition = definition; this.container = container; this.preview = preview;
            MotionRoot = new GameObject("Boss motion (tiles)").transform;
            MotionRoot.SetParent(container, false);
            MotionRoot.localPosition = definition.position;
            try
            {
                Actor = UnityEngine.Object.Instantiate(definition.prefab, MotionRoot, false);
                Actor.transform.localScale *= definition.size;
                PrepareObjects(Actor.gameObject, preview);
                ambientParticles = Actor.GetComponentsInChildren<ParticleSystem>(true);
                PrepareParticles(ambientParticles);
                animator = Actor.Animator;
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.fireEvents = !preview;
                    animator.enabled = false;
                    animator.Rebind();
                    if (definition.animationLayer < 0 || definition.animationLayer >= animator.layerCount)
                        throw new InvalidOperationException("Boss animation layer does not exist.");
                    RequireState(definition.idleState);
                    Idle();
                }
            }
            catch { Destroy(MotionRoot.gameObject); throw; }
        }

        private void RequireState(string state)
        {
            if (animator == null || string.IsNullOrEmpty(state) || !animator.HasState(definition.animationLayer, Animator.StringToHash(state)))
                throw new InvalidOperationException("Boss Animator state does not exist: " + state);
        }
        private void Idle()
        {
            if (animator == null) return;
            animator.speed = 1;
            animator.Play(definition.idleState, definition.animationLayer, 0);
            animator.Update(0);
        }
        public IPatternLease BossAnimation(BossAnimationEvent action)
        {
            RequireState(action.state);
            long owner = animationOwner = ++serial;
            animator.speed = action.speed;
            if (action.transition > 0) animator.CrossFadeInFixedTime(action.state, action.transition, definition.animationLayer, 0);
            else animator.Play(action.state, definition.animationLayer, 0);
            animator.Update(0);
            return new Lease(() => { if (!disposed && animationOwner == owner) { animationOwner = 0; Idle(); } });
        }
        public IPatternLease BossMotion(BossMotionEvent action, float duration)
        {
            long owner = ++serial;
            if (action.move) moveOwner = owner;
            if (action.rotate) rotateOwner = owner;
            if (action.resize) scaleOwner = owner;
            if (action.shake) shakeOwner = owner;
            return new Lease(() => {
                if (disposed) return;
                if (moveOwner == owner) { moveOwner = 0; translation = Vector2.zero; }
                if (rotateOwner == owner) { rotateOwner = 0; rotation = 0; }
                if (scaleOwner == owner) { scaleOwner = 0; scale = Vector2.one; }
                if (shakeOwner == owner) { shakeOwner = 0; shakeOffset = Vector2.zero; }
                ApplyMotion();
            }, progress => {
                if (disposed) return;
                float elapsed = Mathf.Clamp01(progress) * duration;
                float weight = action.Weight(elapsed, duration);
                if (moveOwner == owner) translation = action.translation * weight;
                if (rotateOwner == owner) rotation = action.rotation * weight;
                if (scaleOwner == owner) scale = Vector2.LerpUnclamped(Vector2.one, action.scale, weight);
                if (shakeOwner == owner) shakeOffset = new Vector2(Mathf.Sin(elapsed * action.shakeFrequency * 6.283185f),
                    Mathf.Sin(elapsed * action.shakeFrequency * 8.317f)) * action.shakeStrength * weight;
                ApplyMotion();
            });
        }
        private void ApplyMotion()
        {
            MotionRoot.localPosition = definition.position + translation + shakeOffset;
            MotionRoot.localRotation = Quaternion.Euler(0, 0, rotation);
            MotionRoot.localScale = new Vector3(scale.x, scale.y, 1);
        }
        public IPatternLease BossVfx(BossVfxEvent action)
        {
            var target = Actor.Socket(action.socket);
            var effect = new Effect();
            effect.root = UnityEngine.Object.Instantiate(action.prefab, target, false);
            effect.root.transform.localPosition = action.offset;
            effect.root.transform.localScale *= action.scale;
            if (!action.follow) effect.root.transform.SetParent(container, true);
            PrepareObjects(effect.root, preview);
            effect.particles = effect.root.GetComponentsInChildren<ParticleSystem>(true);
            effect.animators = effect.root.GetComponentsInChildren<Animator>(true);
            PrepareParticles(effect.particles);
            foreach (var anim in effect.animators)
            { anim.enabled = false; anim.fireEvents = !preview; anim.Rebind(); anim.Update(0); }
            effects.Add(effect);
            return new Lease(() => { effects.Remove(effect); if (effect.root != null) Destroy(effect.root); });
        }
        public void Advance(float delta)
        {
            if (disposed || delta <= 0) return;
            if (animator != null) animator.Update(delta);
            AdvanceParticles(ambientParticles, delta);
            foreach (var effect in effects)
            {
                AdvanceParticles(effect.particles, delta);
                foreach (var anim in effect.animators) if (anim != null) anim.Update(delta);
            }
        }
        internal static void PrepareObjects(GameObject root, bool preview)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            if (!preview) return;
            // Only Animator/ParticleSystem visuals are supported in edit-time simulation.
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (var audio in root.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var collider in root.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        }
        private static void PrepareParticles(ParticleSystem[] particles)
        {
            foreach (var ps in particles)
            {
                ps.useAutoRandomSeed = false; ps.randomSeed = 12345;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Simulate(0, false, true, true); ps.Pause(false);
            }
        }
        private static void AdvanceParticles(ParticleSystem[] particles, float delta)
        {
            if (particles == null) return;
            foreach (var ps in particles)
                if (ps != null && ps.gameObject.activeInHierarchy) { ps.Simulate(delta, false, false, true); ps.Pause(false); }
        }
        public static void Destroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (obj is GameObject go) go.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var effect in effects) if (effect.root != null) Destroy(effect.root);
            effects.Clear();
            if (MotionRoot != null) Destroy(MotionRoot.gameObject);
        }
    }
}
