#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NHN.TraceStrike.Effects;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NHN.TraceStrike.Tests
{
    public sealed class FistRippleTests
    {
        const string Id = "p1-fist-ripple";
        static BossEncounterDefinition Boss => Resources.Load<BossEncounterDefinition>("Patterns/CrimsonGolem");
        static T Field<T>(TraceStrikeGame game, string name) =>
            (T)typeof(TraceStrikeGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);

        [Test]
        public void ReloadedPatternHasIndependentEditableWavesWithMatchingAreas()
        {
            var boss = Boss; var pattern = boss.FindPattern(Id);
            Assert.IsEmpty(boss.ValidateDefinition());
            CollectionAssert.Contains(boss.phases[0].patterns, pattern);
            var steps = Editor.AttackStepEditing.Find(pattern);
            Assert.AreEqual(3, steps.Count);
            Assert.AreEqual(3, steps.Select(s => s.Key).Distinct().Count());
            foreach (var step in steps)
            {
                Assert.IsNotEmpty(step.Name);
                Assert.AreEqual(6, step.Members.Count);
                var selections = step.Members.Select(Editor.AttackStepEditing.Tiles).Where(t => t != null).ToArray();
                Assert.AreEqual(3, selections.Length);
                Assert.AreNotSame(selections[0], selections[1]);
                Assert.AreNotSame(selections[0], selections[2]);
                Assert.IsTrue(selections.All(t => JsonUtility.ToJson(t) == JsonUtility.ToJson(selections[0])));
                Assert.IsTrue(step.Members.All(c => c.attackGroupKey == step.Key));
                Assert.That(step.Damage.start - step.Warning.start, Is.EqualTo(1).Within(.00001));
            }
            Assert.IsFalse(pattern.clips.Any(c => c.action is HazardEvent || c.action is ObstacleEvent));
        }

        [Test]
        public void EveryHitCellHasOneStepInwardEscapeOnActualFloor()
        {
            var boss = Boss; var pattern = boss.FindPattern(Id); var origin = new Vector2Int(19, 8);
            var host = new Editor.PatternPreviewHost(boss.arena);
            var steps = Editor.AttackStepEditing.Find(pattern);
            var context = new PatternContext(host, host.CenterCell);
            var floor = new HashSet<Vector2Int>(host.Traversable);
            var rings = steps.Select(s => s.Tiles.Resolve(context)).ToArray();
            CollectionAssert.AreEqual(new[] { 8, 12, 16 }, rings.Select(r => r.Count));
            var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            for (int i = 0; i < rings.Length; i++)
            {
                Assert.IsFalse(rings[i].Contains(boss.arena.playerStart));
                foreach (var cell in rings[i])
                {
                    var inside = directions.Select(d => cell + d).Where(c => floor.Contains(c) &&
                        Mathf.Abs(c.x - origin.x) + Mathf.Abs(c.y - origin.y) == i + 1).ToArray();
                    Assert.IsNotEmpty(inside, "No inward step at " + cell);
                    Assert.IsTrue(inside.Any(c => rings.Skip(i).All(r => !r.Contains(c))), "Later wave catches escape at " + cell);
                }
            }
            // Earlier wave has ended before the next warning reaches its impact.
            for (int i = 1; i < steps.Count; i++) Assert.Less(steps[i - 1].End, steps[i].Damage.start);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ContactBoundarySelectsGroundedFrameAndEndOrCancelReturnsIdle(bool cancel)
        {
            var boss = Boss; var root = new GameObject("Fist test stage");
            try
            {
                using (var presentation = new BossPresentation(boss.bossVisual, root.transform, true))
                {
                    var host = new Editor.PatternPreviewHost(boss.arena) { boss = presentation };
                    using (var runner = new PatternRunner(boss.FindPattern(Id), new PatternContext(host, host.CenterCell)))
                    {
                        runner.Advance(0);
                        presentation.Advance(1.19f); runner.Advance(1.19f);
                        Assert.AreEqual("Slam_06", presentation.Actor.GetComponentInChildren<SpriteRenderer>().sprite.name);
                        runner.Advance(.01001f);
                        Assert.AreEqual("Slam_07", presentation.Actor.GetComponentInChildren<SpriteRenderer>().sprite.name);
                        if (!cancel) runner.Advance(3);
                    }
                    Assert.AreEqual("Slam_00", presentation.Actor.GetComponentInChildren<SpriteRenderer>().sprite.name);
                    Assert.IsEmpty(host.marks);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator RuntimeWavesRenderAndCleanUpWithVisiblePrefabHealthBar()
        {
            yield return new EnterPlayMode();
            var listener = new GameObject("Fist test listener", typeof(AudioListener));
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            var boss = Boss;
            game.PreviewPattern(boss, boss.FindPattern(Id));
            var runner = Field<PatternRunner>(game, "timeline");
            var stage = Field<BossRenderStage>(game, "bossRenderStage");
            var grid = Field<RectTransform>(game, "mainGrid");
            float oldTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0; // Deterministic snapshots through the real runtime host.
                runner.Advance(0);
                Assert.IsTrue(Field<RectTransform>(game, "arenaBossHealthRoot").gameObject.activeInHierarchy);
                Assert.IsFalse(Field<RectTransform>(game, "arenaBossCore").GetComponent<Image>().enabled);
                var hits = boss.FindPattern(Id).clips.Where(c => c.action is DamageEvent).OrderBy(c => c.start).ToArray();
                // Use serialized boundaries, not rounded decimal labels (third start is 2.3000002).
                float[] times = { 1.05f, hits[0].start + .00001f, hits[1].start + .00001f, hits[2].start + .00001f, boss.FindPattern(Id).Duration };
                for (int snapshot = 0; snapshot < times.Length; snapshot++)
                {
                    float target = times[snapshot];
                    float delta = target - runner.Time;
                    stage.Presentation.Advance(delta); runner.Advance(delta);
                    stage.Render();
                    yield return null;
                    if (snapshot == 1)
                    {
                        Assert.AreEqual("Slam_07", stage.Presentation.Actor.GetComponentInChildren<SpriteRenderer>().sprite.name);
                        Assert.AreEqual(9, grid.GetComponentsInChildren<UiEffectPlayer>().Length); // 8 ring + contact dust
                    }
                    if (snapshot == 2) Assert.AreEqual(12, grid.GetComponentsInChildren<UiEffectPlayer>().Length);
                    if (snapshot == 3) Assert.AreEqual(16, grid.GetComponentsInChildren<UiEffectPlayer>().Length);
                    if (target < 3) Capture(game, "FistRipple_" + target.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                }
                Assert.IsTrue(runner.IsComplete);
                Assert.IsEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());
                Assert.IsFalse(grid.GetComponentsInChildren<Image>().Any(i => i.name == "Timeline Warning" || i.name == "Timeline Damage"));
                Assert.AreEqual("Slam_00", stage.Presentation.Actor.GetComponentInChildren<SpriteRenderer>().sprite.name);
                game.PreviewPattern(boss, boss.FindPattern(Id));
                Field<PatternRunner>(game, "timeline").Advance(1.21f);
                game.StopPatternPreview();
                yield return null;
                Assert.IsEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());
                Assert.IsTrue(Field<RectTransform>(game, "arenaBossHealthRoot").gameObject.activeInHierarchy);
            }
            finally { Time.timeScale = oldTimeScale; Object.Destroy(listener); }
            yield return new ExitPlayMode();
        }

        static void Capture(TraceStrikeGame game, string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var camera = Field<Camera>(game, "uiCamera");
            var texture = new RenderTexture(1600, 900, 24);
            var readback = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); texture.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); readback.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/FistSlam");
                File.WriteAllBytes("Logs/PatternValidation/FistSlam/" + name + ".png", readback.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; texture.Release(); Object.Destroy(texture); Object.Destroy(readback); }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
