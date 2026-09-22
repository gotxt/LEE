#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NHN.TraceStrike.Effects;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace NHN.TraceStrike.Tests
{
    public sealed class RockfallPatternTests
    {
        const string Id = "p1-alternating-rockfall";
        static BossEncounterDefinition Boss => Resources.Load<BossEncounterDefinition>("Patterns/BossData_CrimsonGolem");
        static T Field<T>(TraceStrikeGame game, string name) =>
            (T)typeof(TraceStrikeGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);

        [Test]
        public void TwoPaintableGroupsPartitionActualFloorAndEveryCellHasAnEscape()
        {
            var boss = Boss; var pattern = boss.FindPattern(Id);
            Assert.IsEmpty(boss.ValidateDefinition());
            var steps = Editor.AttackStepEditing.Find(pattern);
            Assert.AreEqual(2, steps.Count);
            Assert.AreNotEqual(steps[0].Key, steps[1].Key);
            var host = new Editor.PatternPreviewHost(boss.arena);
            using (var context = new PatternContext(host, host.CenterCell))
            {
                var a = steps[0].Tiles.Resolve(context); var b = steps[1].Tiles.Resolve(context);
                Assert.IsFalse(a.Overlaps(b)); var all = new HashSet<Vector2Int>(a); all.UnionWith(b);
                CollectionAssert.AreEquivalent(boss.arena.GetCells(), all);
                Assert.IsTrue(a.All(c => ((c.x / 2 + c.y / 2) & 1) == 0));
                var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                foreach (var group in new[] { a, b }) foreach (var cell in group)
                {
                    var one = directions.Select(d => cell + d).Where(all.Contains).ToArray();
                    Assert.IsTrue(one.Any(c => !group.Contains(c)) || one.Any(c => directions.Any(d => all.Contains(c + d) && !group.Contains(c + d))), "No two-step escape: " + cell);
                }
                Debug.Log($"ROCKFALL AREA CHECK: {a.Count}+{b.Count}={all.Count}; at most two steps to safety from every hit tile.");
            }
            foreach (var step in steps)
            {
                Assert.IsNotEmpty(step.Warning.attackName); Assert.AreEqual(7, step.Members.Count);
                var selections = step.Members.Select(Editor.AttackStepEditing.Tiles).Where(t => t != null).ToArray();
                Assert.AreEqual(4, selections.Length);
                foreach (var selection in selections.Skip(1))
                { Assert.AreNotSame(selections[0], selection); Assert.AreEqual(JsonUtility.ToJson(selections[0]), JsonUtility.ToJson(selection)); }
                Assert.IsTrue(step.Members.All(c => c.attackGroupKey == step.Key));
                var falling = step.Members.Single(c => c.action is VfxEvent v && v.prefab.name == "VFX_CrimsonGolem_RockfallFalling");
                Assert.IsTrue(falling.attackAtImpact);
                Assert.That(falling.start + falling.duration, Is.EqualTo(step.Damage.start).Within(.00001));
            }
            Assert.Less(steps[0].End, steps[1].Warning.start);
            Assert.IsFalse(pattern.clips.Any(c => c.action is HazardEvent || c.action is ObstacleEvent));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SwappingSafeGroupsSurvivesAndStayingOnTheSecondGroupIsHit(bool stay)
        {
            var boss = Boss; var source = boss.FindPattern(Id);
            var steps = Editor.AttackStepEditing.Find(source);
            var host = new Editor.PatternPreviewHost(boss.arena);
            var a = steps[0].Tiles.cells[0];
            var b = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }
                .Select(d => a + d).First(c => steps[1].Tiles.cells.Contains(c));
            // Exercise the actual tile events without requiring the renderer in this logic check.
            var pattern = new EncounterPattern { minimumDuration = source.Duration,
                clips = source.clips.Where(c => !(c.action is BossEvent)).ToList() };
            host.player = b;
            using (var runner = new PatternRunner(pattern, new PatternContext(host, host.CenterCell)))
            {
                runner.Advance(steps[0].Damage.start + steps[0].Damage.duration + .01f);
                Assert.IsFalse(host.log.Any(s => s.StartsWith("Hit:")));
                if (!stay) host.player = a;
                runner.Advance(source.Duration);
                Assert.AreEqual(stay ? 1 : 0, host.log.Count(s => s.StartsWith("Hit:")));
            }
            Assert.IsEmpty(host.marks);
        }

        [Test]
        public void FallingStoneReachesGroundAtImpactWithBoundedParticleCount()
        {
            var vfx = Boss.FindPattern(Id).clips.Select(c => c.action).OfType<VfxEvent>();
            var falling = vfx.First(v => v.prefab.name == "VFX_CrimsonGolem_RockfallFalling").prefab;
            var impact = vfx.First(v => v.prefab.name == "VFX_CrimsonGolem_RockfallImpact").prefab;
            var instance = Object.Instantiate(falling);
            try
            {
                var effect = instance.GetComponent<UiEffectPlayer>(); effect.Play(effect.referenceCellSize);
                Assert.AreEqual(1, effect.ParticleCount);
                effect.Sample(.349f);
                Assert.That(Mathf.Abs(instance.transform.GetChild(0).localPosition.y), Is.LessThanOrEqualTo(4));
                Assert.AreEqual(2, impact.GetComponent<UiEffectPlayer>().emitters.Sum(e => e.count));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [UnityTest]
        public IEnumerator RuntimeBothSlamsRenderAndReleaseTheirOwnEffects()
        {
            yield return new EnterPlayMode();
            var listener = new GameObject("Rockfall test audio", typeof(AudioListener));
            var game = Object.FindAnyObjectByType<TraceStrikeGame>(); var boss = Boss;
            game.PreviewPattern(boss, boss.FindPattern(Id));
            var runner = Field<PatternRunner>(game, "timeline"); var stage = Field<BossRenderStage>(game, "bossRenderStage");
            var grid = Field<RectTransform>(game, "mainGrid"); var host = (IPatternHost)game;
            var steps = Editor.AttackStepEditing.Find(boss.FindPattern(Id));
            var model = Field<TrailFieldModel>(game, "model");
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                // Frame the boss from a nearby valid tile without changing the saved spawn/camera.
                var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                var viewpoint = boss.bossVisual.position + Vector2.right * 3;
                var initial = steps[1].Tiles.cells.Where(c => directions.Any(d => steps[0].Tiles.cells.Contains(c + d)))
                    .OrderBy(c => ((Vector2)c - viewpoint).sqrMagnitude).First();
                Assert.IsTrue(model.TryPlacePlayer(initial));
                RefreshPlayerView(game);
                runner.Advance(0);
                float[] times = { .98f, 1.20001f, 1.7f, 2.9f, 3.18001f, 3.40001f, boss.FindPattern(Id).Duration };
                foreach (var t in times)
                {
                    if (t > 1.7f && steps[1].Tiles.cells.Contains(model.Player))
                    {
                        model.TryMove(directions.First(d => steps[0].Tiles.cells.Contains(model.Player + d)));
                        RefreshPlayerView(game);
                    }
                    // Small clock steps preserve animation time on both sides of event boundaries.
                    while (runner.Time < t && !runner.IsComplete)
                    {
                        float delta = Mathf.Min(.01f, t - runner.Time);
                        stage.Presentation.Advance(delta); runner.Advance(delta);
                    }
                    stage.Render();
                    foreach (var effect in grid.GetComponentsInChildren<UiEffectPlayer>())
                    {
                        bool fall = effect.name.StartsWith("VFX_CrimsonGolem_RockfallFalling");
                        float start = t < 2 ? (fall ? .85f : 1.2f) : (fall ? 3.05f : 3.4f);
                        effect.Sample(Mathf.Max(0, t - start));
                    }
                    yield return null;
                    Assert.IsTrue(host.IsAlive);
                    if (Mathf.Abs(t - 1.2f) < .01f || Mathf.Abs(t - 3.4f) < .01f)
                    {
                        int group = t < 2 ? 0 : 1;
                        Assert.AreEqual(steps[group].Tiles.cells.Count, grid.GetComponentsInChildren<UiEffectPlayer>().Length);
                        Assert.AreEqual("Slam_07", stage.Presentation.Actor.GetComponentInChildren<SpriteRenderer>().sprite.name);
                    }
                    if (t < 4) Capture(game, t);
                }
                Assert.IsEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());
                Assert.AreEqual("Slam_00", stage.Presentation.Actor.GetComponentInChildren<SpriteRenderer>().sprite.name);
                game.PreviewPattern(boss, boss.FindPattern(Id));
                Field<PatternRunner>(game, "timeline").Advance(.95f);
                game.StopPatternPreview(); yield return null;
                Assert.IsEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());
            }
            finally { Time.timeScale = previousTimeScale; Object.Destroy(listener); }
            yield return new ExitPlayMode();
        }

        static void RefreshPlayerView(TraceStrikeGame game)
        {
            // Snap only the test camera; the actual runtime smoothing and saved camera setting stay unchanged.
            typeof(TraceStrikeGame).GetField("battleCameraInitialized", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, false);
            typeof(TraceStrikeGame).GetMethod("RefreshBoard", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
        }

        static void Capture(TraceStrikeGame game, float time)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var camera = Field<Camera>(game, "uiCamera");
            var rt = new RenderTexture(1600, 900, 24); var read = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); rt.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); read.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/Rockfall");
                File.WriteAllBytes("Logs/PatternValidation/Rockfall/Rockfall_" + time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".png", read.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; rt.Release(); Object.Destroy(rt); Object.Destroy(read); }
        }
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
