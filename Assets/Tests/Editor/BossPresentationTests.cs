#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace NHN.TraceStrike.Tests
{
    public sealed class BossPresentationTests
    {
        private GameObject prefab, container, effectPrefab;
        private string folder;
        private BossVisualDefinition definition;
        private BossPresentation presentation;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/__BossPresentationTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            prefab = new GameObject("Boss test prefab");
            var actor = prefab.AddComponent<BossActor>();
            var body = new GameObject("Body"); body.transform.SetParent(prefab.transform, false);
            var socket = new GameObject("Hand"); socket.transform.SetParent(body.transform, false);
            socket.transform.localPosition = Vector3.right;
            actor.sockets.Add(new BossSocket { name = "Hand", target = socket.transform });
            actor.animator = body.AddComponent<Animator>();
            var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Animator.controller");
            foreach (var name in new[] { "Idle", "Charge", "Attack" })
            {
                var clip = new AnimationClip { name = name };
                clip.SetCurve("", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0, 0, 1, name == "Idle" ? 0 : 1));
                AssetDatabase.CreateAsset(clip, folder + "/" + name + ".anim");
                controller.layers[0].stateMachine.AddState(name).motion = clip;
            }
            actor.animator.runtimeAnimatorController = controller;
            definition = new BossVisualDefinition { prefab = actor, position = new Vector2(8, 8), idleState = "Base Layer.Idle" };
            container = new GameObject("Boss presentation test stage");
            effectPrefab = new GameObject("Effect test");
            presentation = new BossPresentation(definition, container.transform, true);
        }
        [TearDown]
        public void TearDown()
        {
            presentation?.Dispose();
            UnityEngine.Object.DestroyImmediate(container);
            UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(effectPrefab);
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void MotionChannelsComposeAndSupersededEventsCannotResetNewerMotion()
        {
            using (var move = presentation.BossMotion(new BossMotionEvent { translation = Vector2.right, returnTime = 0 }, 1))
            using (var rotate = presentation.BossMotion(new BossMotionEvent { move = false, rotate = true, rotation = 90, returnTime = 0 }, 1))
            {
                move.SetProgress(1); rotate.SetProgress(1);
                Assert.AreEqual(new Vector3(9, 8), presentation.MotionRoot.localPosition);
                Assert.That(presentation.MotionRoot.localEulerAngles.z, Is.EqualTo(90).Within(0.01));
                using (var newer = presentation.BossMotion(new BossMotionEvent { translation = Vector2.up * 2, returnTime = 0 }, 1))
                {
                    newer.SetProgress(1); move.Dispose();
                    Assert.AreEqual(new Vector3(8, 10), presentation.MotionRoot.localPosition);
                }
                Assert.AreEqual(new Vector3(8, 8), presentation.MotionRoot.localPosition);
                move.SetProgress(1);
                Assert.AreEqual(new Vector3(8, 8), presentation.MotionRoot.localPosition);
            }
            Assert.AreEqual(Quaternion.identity, presentation.MotionRoot.localRotation);
        }

        [Test]
        public void MotionReturnsBeforeCompletionAndCancellationRestoresBase()
        {
            using (var lease = presentation.BossMotion(new BossMotionEvent { translation = Vector2.up * 3, returnTime = 0.5f }, 1))
            {
                lease.SetProgress(0.5f); Assert.AreEqual(11, presentation.MotionRoot.localPosition.y);
                lease.SetProgress(1); Assert.AreEqual(8, presentation.MotionRoot.localPosition.y);
                lease.SetProgress(0.5f);
            }
            Assert.AreEqual(new Vector3(8, 8), presentation.MotionRoot.localPosition);
        }

        [Test]
        public void AnimationUsesLatestStateAndOldCleanupCannotInterruptIt()
        {
            var first = presentation.BossAnimation(new BossAnimationEvent { state = "Base Layer.Charge", transition = 0 });
            var second = presentation.BossAnimation(new BossAnimationEvent { state = "Base Layer.Attack", transition = 0 });
            first.Dispose(); presentation.Advance(0.25f);
            Assert.IsTrue(presentation.Actor.Animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Attack"));
            second.Dispose();
            Assert.IsTrue(presentation.Actor.Animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"));
        }

        [Test]
        public void FollowingAndDetachedEffectsUseSameSocketButDifferentMotion()
        {
            using (var attached = presentation.BossVfx(new BossVfxEvent { prefab = effectPrefab, socket = "Hand", follow = true }))
            using (var detached = presentation.BossVfx(new BossVfxEvent { prefab = effectPrefab, socket = "Hand", follow = false }))
            using (var move = presentation.BossMotion(new BossMotionEvent { translation = Vector2.up * 2, returnTime = 0 }, 1))
            {
                Transform hand = presentation.Actor.Socket("Hand");
                Transform attachedEffect = hand.GetChild(0);
                Transform detachedEffect = container.transform.GetChild(1);
                Assert.AreEqual(attachedEffect.position, detachedEffect.position);
                Vector3 fixedPosition = detachedEffect.position;
                move.SetProgress(1);
                Assert.AreEqual(fixedPosition, detachedEffect.position);
                Assert.AreEqual(fixedPosition + Vector3.up * 2, attachedEffect.position);
            }
            Assert.AreEqual(1, container.transform.childCount);
            Assert.AreEqual(0, presentation.Actor.Socket("Hand").childCount);
        }

        [Test]
        public void PatternCompletionAndCancellationReleaseBossEvents()
        {
            var host = new Editor.PatternPreviewHost(17) { boss = presentation };
            var pattern = new EncounterPattern { minimumDuration = 2 };
            pattern.clips.Add(new PatternClip { duration = 0.1f, action = new BossAnimationEvent { state = "Base Layer.Charge", transition = 0 } });
            pattern.clips.Add(new PatternClip { duration = 2, action = new BossMotionEvent { translation = Vector2.up, returnTime = 0 } });
            pattern.clips.Add(new PatternClip { duration = 2, action = new BossVfxEvent { prefab = effectPrefab } });
            using (var runner = new PatternRunner(pattern, new PatternContext(host, host.CenterCell)))
            {
                runner.Advance(1);
                Assert.IsTrue(presentation.Actor.Animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Charge"));
            }
            Assert.AreEqual(new Vector3(8, 8), presentation.MotionRoot.localPosition);
            Assert.IsTrue(presentation.Actor.Animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"));
            using (var runner = new PatternRunner(pattern, new PatternContext(host, host.CenterCell))) runner.Advance(3);
            Assert.AreEqual(new Vector3(8, 8), presentation.MotionRoot.localPosition);
        }

        [Test]
        public void EmptyArenaCentreDoesNotPreventBossPlacementAndMissingSocketsFailClearly()
        {
            var arena = new BossArenaDefinition(); arena.MakeCustom(); arena.floorCells.Remove(new Vector2Int(8, 8));
            Assert.IsFalse(arena.GetCells().Contains(new Vector2Int(8, 8)));
            Assert.AreEqual(new Vector3(8, 8), presentation.MotionRoot.localPosition);
            Assert.Throws<InvalidOperationException>(() => presentation.BossVfx(new BossVfxEvent { prefab = effectPrefab, socket = "missing" }));
            Assert.Throws<InvalidOperationException>(() => presentation.BossAnimation(new BossAnimationEvent { state = "missing" }));
        }

        [Test]
        public void ParticleScrubbingReplaysSameTimeAndCleansUp()
        {
            var ps = effectPrefab.AddComponent<ParticleSystem>();
            var main = ps.main; main.startLifetime = 5; main.startSpeed = 1; main.maxParticles = 100;
            var emission = ps.emission; emission.rateOverTime = 20;
            int count;
            Vector3 position;
            using (var lease = presentation.BossVfx(new BossVfxEvent { prefab = effectPrefab }))
            {
                presentation.Advance(0.5f);
                var live = presentation.Actor.GetComponentInChildren<ParticleSystem>();
                var particles = new ParticleSystem.Particle[100];
                count = live.GetParticles(particles); Assert.Greater(count, 0);
                position = particles[0].position;
            }
            using (var lease = presentation.BossVfx(new BossVfxEvent { prefab = effectPrefab }))
            {
                presentation.Advance(0.5f);
                var live = presentation.Actor.GetComponentInChildren<ParticleSystem>();
                var particles = new ParticleSystem.Particle[100];
                Assert.AreEqual(count, live.GetParticles(particles));
                Assert.That(Vector3.Distance(position, particles[0].position), Is.LessThan(0.001f));
            }
            Assert.IsNull(presentation.Actor.GetComponentInChildren<ParticleSystem>());
        }

        [Test]
        public void EditorStatePickerAndSerializationPreserveBossConfiguration()
        {
            CollectionAssert.AreEquivalent(new[] { "Base Layer.Idle", "Base Layer.Charge", "Base Layer.Attack" },
                Editor.BossAnimatorOptions.States(definition.prefab, 0));
            var boss = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            var copy = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            try
            {
                definition.prefab = PrefabUtility.SaveAsPrefabAsset(prefab, folder + "/Boss.prefab").GetComponent<BossActor>();
                var savedEffect = PrefabUtility.SaveAsPrefabAsset(effectPrefab, folder + "/Effect.prefab");
                boss.bossVisual = definition;
                boss.phases[0].patterns[0].clips.Add(new PatternClip { action = new BossAnimationEvent { state = "Base Layer.Charge" } });
                boss.phases[0].patterns[0].clips.Add(new PatternClip { action = new BossVfxEvent { prefab = savedEffect, socket = "Hand" } });
                boss.phases[0].patterns[0].clips.Add(new PatternClip { action = new BossMotionEvent { translation = Vector2.up } });
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(boss), copy);
                Assert.AreEqual(definition.prefab, copy.bossVisual.prefab);
                Assert.IsInstanceOf<BossMotionEvent>(copy.phases[0].patterns[0].clips[2].action);
                Assert.IsEmpty(copy.ValidateDefinition());
            }
            finally { UnityEngine.Object.DestroyImmediate(boss); UnityEngine.Object.DestroyImmediate(copy); }
        }

        [Test]
        public void RenderStageProducesTransparentTextureWithVisibleSprite()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required for visual render test.");
            var texture = new Texture2D(16, 16); var pixels = new Color[256];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 16);
            prefab.AddComponent<SpriteRenderer>().sprite = sprite;
            var readback = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            try
            {
                using (var stage = new BossRenderStage(definition, 17, true))
                {
                    stage.Render();
                    var previous = RenderTexture.active;
                    try { RenderTexture.active = stage.Texture; readback.ReadPixels(new Rect(stage.Texture.width / 2 - 256, stage.Texture.height / 2 - 256, 512, 512), 0, 0); readback.Apply(); }
                    finally { RenderTexture.active = previous; }
                    Assert.Greater(readback.GetPixel(256, 256).a, 0.5f);
                    Assert.Less(readback.GetPixel(0, 0).a, 0.1f);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(readback); UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
#endif
