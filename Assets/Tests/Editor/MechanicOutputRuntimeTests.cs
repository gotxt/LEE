#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NHN.TraceStrike.Tests
{
    public sealed class MechanicOutputRuntimeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Get<T>(TraceStrikeGame game, string field) => (T)typeof(TraceStrikeGame).GetField(field, Flags).GetValue(game);
        private static object Call(TraceStrikeGame game, string method, params object[] args) => typeof(TraceStrikeGame).GetMethod(method, Flags).Invoke(game, args);
        [UnityTest] public IEnumerator RealGameUsesOnlySelectedMatureSourceAndKeepsSnapshotAndCleanup()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required");
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            if (Object.FindAnyObjectByType<AudioListener>() == null) game.gameObject.AddComponent<AudioListener>();
            var saved = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            var catalog = Resources.Load<BossCatalog>("Patterns/BossCatalog_Main");
            var copy = Object.Instantiate(saved); string before = JsonUtility.ToJson(saved);
            var originalCatalog = catalog.bosses.ToArray(); float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                var bottom = copy.phases[0].mechanics.OfType<RegionGrowthMechanic>().Single();
                bottom.connectionId = "bottom"; bottom.regionIds = new List<string> { "petal-bottom" };
                bottom.enrageWhenAll.Clear(); bottom.enrageWhenAny.Clear();
                var top = new RegionGrowthMechanic { name = bottom.name, connectionId = "top", regionIds = new List<string> { "petal-top" }, overgrownTile = bottom.overgrownTile };
                copy.phases[0].mechanics.Add(top);
                var child = MechanicOutputTests.Attack("mechanic-child-test"); copy.libraryPatterns.Add(child);
                var parent = new EncounterPattern { id = "mechanic-parent-test", clips = new List<PatternClip>
                { new PatternClip { duration = child.Duration, action = new CallAtMechanicPositionsEvent { mechanicId = "bottom", outputKey = RegionGrowthMechanic.MaturePositions, patternId = child.id } } } };
                copy.phases[0].patterns.Clear(); copy.phases[0].patterns.Add(parent);
                Assert.IsEmpty(copy.ValidateDefinition()); catalog.bosses.Add(copy);
                game.PreviewPatternWithMechanics(copy, parent, 0, 25);
                var session = Get<BossMechanicSession>(game, "mechanicSession");
                var origins = game.CaptureMechanicPositions("bottom", RegionGrowthMechanic.MaturePositions).ToArray();
                Assert.AreEqual(5, origins.Length);
                Assert.AreEqual(5, game.CaptureMechanicPositions("top", RegionGrowthMechanic.MaturePositions).Count);
                Call(game, "TickTimeline");
                var danger = Get<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger");
                Assert.AreEqual(5, danger.Count);
                var expected = origins.SelectMany(c => new[] { c, c + Vector2Int.up }).Where(c => ((IPatternHost)game).Walkable.Contains(c)).Distinct().ToArray();
                CollectionAssert.AreEquivalent(expected, danger.Values.SelectMany(c => c).Distinct().ToArray());
                yield return null; Capture(game, "Warning");
                Call(game, "AdvanceMechanics", 5f);
                Assert.AreEqual(10, game.CaptureMechanicPositions("bottom", RegionGrowthMechanic.MaturePositions).Count);
                var timeline = Get<PatternRunner>(game, "timeline"); timeline.Advance(1);
                Assert.AreEqual(5, danger.Count);
                CollectionAssert.AreEquivalent(expected, danger.Values.SelectMany(c => c).Distinct().ToArray());
                var objects = Get<Dictionary<string, Transform>>(game, "timelineObjects");
                Assert.AreEqual(origins.Sum(c => new[] { c, c + Vector2Int.up }.Count(p => ((IPatternHost)game).Walkable.Contains(p))), objects.Count(pair => !pair.Key.StartsWith("mechanic/", StringComparison.Ordinal)));
                yield return null; Capture(game, "Impact");
                Call(game, "CancelTimeline"); Assert.IsEmpty(danger);
                Assert.IsTrue(objects.Keys.All(k => k.StartsWith("mechanic/", StringComparison.Ordinal)));
                Assert.AreSame(session, Get<BossMechanicSession>(game, "mechanicSession"));
                game.StopPatternPreview(); Assert.IsEmpty(game.CaptureMechanicPositions("bottom", RegionGrowthMechanic.MaturePositions));
                game.enabled = false; Assert.IsEmpty(objects);
                Assert.Throws<InvalidOperationException>(() => game.CaptureMechanicPositions("bottom", RegionGrowthMechanic.MaturePositions));
                Assert.AreEqual(before, JsonUtility.ToJson(saved));
            }
            finally { Time.timeScale = oldScale; catalog.bosses.Clear(); catalog.bosses.AddRange(originalCatalog); Object.Destroy(copy); }
            yield return new ExitPlayMode();
        }
        private static void Capture(TraceStrikeGame game, string name)
        {
            var camera = Get<Camera>(game, "uiCamera"); var rt = new RenderTexture(1600, 900, 24);
            var read = new Texture2D(1600, 900, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); rt.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); read.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/MechanicOutputs");
                File.WriteAllBytes("Logs/PatternValidation/MechanicOutputs/" + name + ".png", read.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; rt.Release(); Object.Destroy(rt); Object.Destroy(read); }
        }
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
