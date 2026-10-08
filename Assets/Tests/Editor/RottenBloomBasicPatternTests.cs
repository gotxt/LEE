#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NHN.TraceStrike.Editor;
using NHN.TraceStrike.Effects;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NHN.TraceStrike.Tests
{
    public sealed class RottenBloomBasicPatternTests
    {
        static readonly string[] Ids = { "p1-spore-volley", "p1-vein-rupture", "p1-thorn-calyx" };
        static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        static BossEncounterDefinition Boss => Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
        static HashSet<Vector2Int> Area(PatternPreviewHost host, PatternPreviewHost.PreviewLayer layer) =>
            new HashSet<Vector2Int>(host.marks.Values.Where(m => m.Layer == layer).SelectMany(m => m.Cells));

        [Test]
        public void ImportedPatternsRemainEditableAndRoundTripWithoutEnrageSelection()
        {
            var boss = Boss;
            Assert.IsEmpty(boss.ValidateDefinition());
            var copy = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(boss), copy);
                foreach (string id in Ids)
                {
                    var pattern = copy.FindPattern(id);
                    Assert.IsNotNull(pattern);
                    Assert.IsTrue(pattern.CanSchedule(false)); Assert.IsFalse(pattern.CanSchedule(true));
                    var steps = AttackStepEditing.Find(pattern);
                    Assert.AreEqual(pattern.clips.Count, steps.Sum(s => s.Members.Count));
                    Assert.AreEqual(steps.Count, steps.Select(s => s.Key).Distinct().Count());
                    foreach (var step in steps)
                    {
                        Assert.IsNotEmpty(step.Name);
                        var vfx = step.Members.Select(c => c.action).OfType<VfxEvent>().Single();
                        Assert.IsNotNull(vfx.prefab);
                        Assert.AreEqual("VFX_RottenBloom_PollenBurst", vfx.prefab.name);
                    }
                }
                Assert.IsEmpty(copy.ValidateDefinition());
            }
            finally { Object.DestroyImmediate(copy); }
        }

        // Every saved start cell includes petal tips, boundaries and the blocked boss footprint's neighbors.
        // Real TileState/TryStep apply stun; 0.35s per tap is a test input assumption, not a human play result.
        [TestCaseSource(nameof(Ids))]
        public void EveryStartEscapesConsecutiveHitsWithStunAndChangingVines(string id)
        {
            var boss = Boss;
            var vine = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Vines");
            using var map = new PatternPreviewHost(boss);
            var floor = new HashSet<Vector2Int>(map.Traversable);
            var central = new HashSet<Vector2Int>(boss.tileRegions
                .Single(r => r.id == "97821fa0658f4977bf2903e110a25f20").cells);
            var petals = new HashSet<Vector2Int>(boss.tileRegions.Where(r => r.id.StartsWith("petal-"))
                .SelectMany(r => r.cells));
            var states = new[] { new HashSet<Vector2Int>(), central, petals, floor, new HashSet<Vector2Int>() };
            foreach (var start in floor)
            for (int state = 0; state < states.Length; state++)
            {
                using var host = new PatternPreviewHost(boss);
                host.player = start;
                using var vines = host.PlaceSpecialTiles(vine, states[state]);
                var neighbor = Directions.Select(d => start + d).First(floor.Contains);
                host.TileState.Enter(neighbor, start, vine); // Worst allowed remaining stun at warning start.
                var pattern = boss.FindPattern(id);
                using var runner = new PatternRunner(pattern, new PatternContext(host, host.CenterCell));
                foreach (var warning in pattern.clips.Where(c => c.action is WarningEvent))
                {
                    Advance(host, runner, warning.start - runner.Time);
                    var danger = Area(host, PatternPreviewHost.PreviewLayer.Warning);
                    // Fifth scenario grows vines after warning capture. Area and impact time stay fixed.
                    if (state == 4)
                    {
                        Advance(host, runner, .2f);
                        host.PlaceSpecialTiles(vine, floor);
                    }
                    foreach (var target in EscapePath(host.player, danger, floor))
                    {
                        Advance(host, runner, host.TileState.StunRemaining + .35f);
                        Assert.Less(runner.Time, warning.start + warning.duration, id + " " + start);
                        Assert.IsTrue(host.TryStep(target - host.player, out _));
                    }
                    Assert.IsFalse(danger.Contains(host.player));
                    Advance(host, runner, warning.start + warning.duration + .301f - runner.Time);
                    Assert.IsFalse(host.log.Any(s => s.StartsWith("Hit:")), id + " " + start + " state=" + state);
                }
                Advance(host, runner, pattern.Duration - runner.Time);
                Assert.IsTrue(runner.IsComplete); Assert.IsEmpty(host.marks);
            }
        }

        static void Advance(PatternPreviewHost host, PatternRunner runner, float time)
        {
            Assert.GreaterOrEqual(time, -.0001f);
            time = Mathf.Max(0, time);
            host.TileState.Advance(time); runner.Advance(time);
        }

        [TestCaseSource(nameof(Ids))]
        public void NoStationaryRefugeSurvivesAnEntireImprovedPattern(string id)
        {
            using var map = new PatternPreviewHost(Boss);
            foreach (var start in map.Traversable)
            {
                using var host = new PatternPreviewHost(Boss);
                host.player = start;
                using var runner = new PatternRunner(Boss.FindPattern(id), new PatternContext(host, host.CenterCell));
                runner.Advance(Boss.FindPattern(id).Duration);
                Assert.IsTrue(host.log.Any(s => s.StartsWith("Hit:")), id + " has a stationary refuge at " + start);
                Assert.IsTrue(runner.IsComplete); Assert.IsEmpty(host.marks);
            }
        }

        [TestCaseSource(nameof(Ids))]
        public void EveryAimedWarningRecapturesPositionAndLocksDamageAndVfxThere(string id)
        {
            using var host = new PatternPreviewHost(Boss);
            var pattern = Boss.FindPattern(id);
            host.player = new Vector2Int(15, 4);
            using var runner = new PatternRunner(pattern, new PatternContext(host, host.CenterCell));
            int index = 0;
            foreach (var clip in pattern.clips.Where(c => c.action is WarningEvent))
            {
                var warning = (WarningEvent)clip.action;
                var capturedPlayer = index++ % 2 == 0 ? new Vector2Int(15, 4) : new Vector2Int(15, 27);
                var laterPlayer = capturedPlayer.y == 4 ? new Vector2Int(15, 27) : new Vector2Int(15, 4);
                host.player = capturedPlayer;
                runner.Advance(clip.start - runner.Time);
                var area = Area(host, PatternPreviewHost.PreviewLayer.Warning);
                if (string.IsNullOrEmpty(warning.tiles.locationGroupId))
                    Assert.IsTrue(area.Contains(capturedPlayer), id + " " + clip.label);
                host.player = laterPlayer;
                // Sample just inside the serialized damage interval, avoiding float addition at its exact boundary.
                float impact = pattern.clips.Single(c => c.attackGroupKey == clip.attackGroupKey && c.action is DamageEvent).start;
                runner.Advance(impact + .001f - runner.Time);
                CollectionAssert.AreEquivalent(area, Area(host, PatternPreviewHost.PreviewLayer.Damage));
                CollectionAssert.AreEquivalent(area, Area(host, PatternPreviewHost.PreviewLayer.Effect));
                runner.Advance(.301f);
            }
        }

        static List<Vector2Int> EscapePath(Vector2Int start, HashSet<Vector2Int> danger, HashSet<Vector2Int> floor)
        {
            var queue = new Queue<Vector2Int>(); queue.Enqueue(start);
            var previous = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (!danger.Contains(cell))
                {
                    var path = new List<Vector2Int>();
                    while (cell != start) { path.Add(cell); cell = previous[cell]; }
                    path.Reverse(); return path;
                }
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (!floor.Contains(next) || previous.ContainsKey(next)) continue;
                    previous[next] = cell; queue.Enqueue(next);
                }
            }
            Assert.Fail("No safe route from " + start); return null;
        }

        [Test]
        public void VolleyRetargetsEachWarningWhileCalyxKeepsItsOriginalCenter()
        {
            foreach (string id in new[] { Ids[0], Ids[2] })
            {
                using var host = new PatternPreviewHost(Boss);
                var first = new Vector2Int(15, 4); var second = new Vector2Int(15, 27);
                host.player = first;
                var pattern = Boss.FindPattern(id);
                using var runner = new PatternRunner(pattern, new PatternContext(host, host.CenterCell));
                var warnings = pattern.clips.Where(c => c.action is WarningEvent).ToArray();
                runner.Advance(0);
                var captured = Area(host, PatternPreviewHost.PreviewLayer.Warning);
                host.player = second;
                runner.Advance(warnings[0].duration);
                CollectionAssert.AreEquivalent(captured, Area(host, PatternPreviewHost.PreviewLayer.Damage));
                CollectionAssert.AreEquivalent(captured, Area(host, PatternPreviewHost.PreviewLayer.Effect));
                runner.Advance(warnings[1].start - runner.Time);
                var next = Area(host, PatternPreviewHost.PreviewLayer.Warning);
                Assert.IsTrue(next.Contains(id == Ids[0] ? second : first));
                Assert.IsFalse(next.Contains(id == Ids[0] ? first : second));
            }
        }

        [TestCaseSource(nameof(Ids))]
        public void ImpactReallyHitsAndCancellationReleasesWarningAndEffectLeases(string id)
        {
            var pattern = Boss.FindPattern(id);
            var first = pattern.clips.First(c => c.action is WarningEvent);
            foreach (float time in new[] { .1f, first.duration + .2f })
            {
                using var host = new PatternPreviewHost(Boss);
                host.player = new Vector2Int(15, 4);
                using var runner = new PatternRunner(pattern, new PatternContext(host, host.CenterCell));
                runner.Advance(0);
                host.player = Area(host, PatternPreviewHost.PreviewLayer.Warning).First(c => host.Traversable.Contains(c));
                runner.Advance(time);
                Assert.IsNotEmpty(host.marks);
                if (time > first.duration) Assert.AreEqual(1, host.log.Count(s => s.StartsWith("Hit:")));
                runner.Dispose(); runner.Dispose(); Assert.IsEmpty(host.marks);
            }
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(TraceStrikeGame game, string name) => (T)typeof(TraceStrikeGame).GetField(name, Flags).GetValue(game);
        static IEnumerator Frames() { int frame = Time.frameCount + 2; while (Time.frameCount < frame) yield return null; }

        [UnityTest]
        public IEnumerator GameRendersEachSavedPatternAndCancelsItsLiveEffects()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required.");
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            if (Object.FindAnyObjectByType<AudioListener>() == null) game.gameObject.AddComponent<AudioListener>();
            string before = EditorJsonUtility.ToJson(Boss);
            float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                foreach (string id in Ids)
                {
                    var pattern = Boss.FindPattern(id);
                    game.PreviewPattern(Boss, pattern);
                    var runner = Field<PatternRunner>(game, "timeline");
                    var grid = Field<RectTransform>(game, "mainGrid");
                    var model = Field<TrailFieldModel>(game, "model");
                    int shot = 0;
                    foreach (var clip in pattern.clips.Where(c => c.action is WarningEvent))
                    {
                        runner.Advance(clip.start + .4f - runner.Time);
                        yield return Frames(); Capture(game, id + "-" + ++shot + "-warning");
                        var danger = new HashSet<Vector2Int>(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger")
                            .Values.SelectMany(c => c));
                        var escape = EscapePath(model.Player, danger, new HashSet<Vector2Int>(model.Traversable));
                        // Rendering test moves directly; real step/stun timing is checked above for every start.
                        if (escape.Count > 0) Assert.IsTrue(model.TryPlacePlayer(escape.Last()));
                        typeof(TraceStrikeGame).GetMethod("RefreshBoard", Flags).Invoke(game, null);
                        runner.Advance(clip.start + clip.duration + .01f - runner.Time);
                        var effects = grid.GetComponentsInChildren<UiEffectPlayer>();
                        Assert.IsNotEmpty(effects);
                        foreach (var effect in effects) effect.Sample(.01f);
                        yield return Frames(); Capture(game, id + "-" + shot + "-impact");
                    }
                    runner.Advance(pattern.Duration - runner.Time);
                    Assert.IsTrue(runner.IsComplete);
                    yield return Frames();
                    Assert.IsEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                    Assert.IsEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());
                    game.StopPatternPreview(); yield return Frames();
                    Assert.IsEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                    Assert.IsEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());

                    game.PreviewPattern(Boss, pattern);
                    Field<PatternRunner>(game, "timeline").Advance(.1f);
                    Assert.IsNotEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                    var death = (IEnumerator)typeof(TraceStrikeGame).GetMethod("KillPlayer", Flags)
                        .Invoke(game, new object[] { "Pattern cleanup test" });
                    Assert.IsTrue(death.MoveNext());
                    Assert.IsEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                    game.PreviewPattern(Boss, pattern); yield return Frames();
                    Assert.AreEqual(0, Field<PatternRunner>(game, "timeline").Time);
                    // Advance(0) in Update starts the new pattern's first warning even while timeScale is zero.
                    Assert.AreEqual(1, Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger").Count);
                    Assert.IsEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());
                }
                Assert.AreEqual(before, EditorJsonUtility.ToJson(Boss));
            }
            finally { game.StopPatternPreview(); Time.timeScale = oldScale; }
            yield return new ExitPlayMode();
        }

        static void Capture(TraceStrikeGame game, string name)
        {
            var camera = Field<Camera>(game, "uiCamera");
            var rt = new RenderTexture(1600, 900, 24);
            var read = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); rt.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); read.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/RottenBloomBasicImprove-1007");
                File.WriteAllBytes("Logs/PatternValidation/RottenBloomBasicImprove-1007/" + name + ".png", read.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; rt.Release(); Object.Destroy(rt); Object.Destroy(read); }
        }

        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
