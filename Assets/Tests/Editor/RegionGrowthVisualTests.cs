#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NHN.TraceStrike.Tests
{
    public sealed class RegionGrowthVisualTests
    {
        private const string PrefabPath = "Assets/Art/Bosses/RottenBloom/MechanicVisual_SeedGrowth.prefab";
        private const string SheetPath = "Assets/Art/Bosses/RottenBloom/SpriteSheet_SeedGrowth.png";
        private static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        [Test]
        public void SavedPrefabKeepsAllSixOriginalCellsInLeftToRightOrder()
        {
            Assert.IsNotNull(Prefab);
            var visual = Prefab.GetComponent<MechanicProgressVisual>();
            Assert.IsNotNull(visual);
            Assert.AreEqual(6, visual.frames.Length);
            for (int i = 0; i < visual.frames.Length; i++)
            {
                var frame = visual.frames[i];
                Assert.IsNotNull(frame);
                Assert.AreEqual(SheetPath, AssetDatabase.GetAssetPath(frame));
                Assert.AreEqual(new Rect(i * 32, 0, 32, 32), frame.rect);
                Assert.AreEqual(192, frame.texture.width);
                Assert.AreEqual(32, frame.texture.height);
                Assert.AreEqual(FilterMode.Point, frame.texture.filterMode);
            }
            var image = Prefab.GetComponent<Image>();
            Assert.AreEqual(Color.white, image.color);
            Assert.IsFalse(image.raycastTarget);
            Assert.AreEqual(visual.frames[0], image.sprite);
            Assert.IsNull(Prefab.GetComponent<Animator>(), "Growth comes from mechanic age, not an independent animation clock.");
            var saved = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            var growth = saved.phases[0].mechanics.OfType<RegionGrowthMechanic>().Single();
            Assert.AreEqual(Prefab, growth.seedPrefab);
            Assert.AreEqual(Prefab, growth.maturePrefab);
            Assert.AreEqual(Color.white, growth.seedTint);
            Assert.AreEqual(Color.white, growth.matureTint);
        }

        [TestCase(30f)]
        [TestCase(60f)]
        public void SpriteSelectionUsesFiveEqualIntervalsAndBloomsOnlyAtCompletion(float seconds)
        {
            var instance = Object.Instantiate(Prefab);
            try
            {
                var visual = instance.GetComponent<MechanicProgressVisual>();
                var image = instance.GetComponent<Image>();
                for (int stage = 0; stage < 5; stage++)
                {
                    float start = seconds * stage / 5;
                    float next = seconds * (stage + 1) / 5;
                    visual.SetProgress(start / seconds);
                    Assert.AreEqual(visual.frames[stage], image.sprite, "At " + start + " seconds");
                    visual.SetProgress((next - .001f) / seconds);
                    Assert.AreEqual(visual.frames[stage], image.sprite, "Immediately before " + next + " seconds");
                }
                visual.SetProgress(1);
                Assert.AreEqual(visual.frames[5], image.sprite);
                visual.SetProgress(5);
                Assert.AreEqual(visual.frames[5], image.sprite);
                visual.SetProgress(-1);
                Assert.AreEqual(visual.frames[0], image.sprite);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [TestCase(30f)]
        [TestCase(60f)]
        public void RuntimeAgeDrivesFrameBoundariesAndMatureReplacement(float seconds)
        {
            using var fixture = new Fixture(seconds, 1000);
            string before = JsonUtility.ToJson(fixture.Definition);
            fixture.Runtime.Advance(0);
            var seed = fixture.Runtime.Seeds.Single();
            var firstVisual = fixture.Host.Live(seed.Cell);
            Assert.AreEqual(0, firstVisual.Frame);
            for (int stage = 1; stage <= 5; stage++)
            {
                float boundary = seconds * stage / 5;
                fixture.Runtime.Advance((float)(boundary - .001 - fixture.Runtime.Elapsed));
                Assert.AreEqual(stage - 1, fixture.Host.Live(seed.Cell).Frame);
                Assert.IsFalse(seed.Mature);
                fixture.Runtime.Advance(.0011f);
                Assert.AreEqual(stage, fixture.Host.Live(seed.Cell).Frame);
                Assert.AreEqual(stage == 5, seed.Mature);
            }
            Assert.IsTrue(firstVisual.Disposed);
            Assert.AreEqual(1, fixture.Host.Live(seed.Cell).Progress);
            Assert.AreEqual(before, JsonUtility.ToJson(fixture.Definition));
        }

        [Test]
        public void SeedsSpawnedLaterKeepTheirOwnGrowthClock()
        {
            using var fixture = new Fixture(30, 10);
            fixture.Runtime.Advance(16);
            var seeds = fixture.Runtime.Seeds.OrderBy(s => s.MaturesAt).ToArray();
            Assert.AreEqual(2, seeds.Length);
            Assert.AreEqual(2, fixture.Host.Live(seeds[0].Cell).Frame);
            Assert.AreEqual(1, fixture.Host.Live(seeds[1].Cell).Frame);
            Assert.That(fixture.Host.Live(seeds[0].Cell).Progress, Is.EqualTo(16f / 30).Within(.00001));
            Assert.That(fixture.Host.Live(seeds[1].Cell).Progress, Is.EqualTo(6f / 30).Within(.00001));
            fixture.Runtime.Advance(14);
            Assert.IsTrue(seeds[0].Mature);
            Assert.IsFalse(seeds[1].Mature);
            Assert.AreEqual(5, fixture.Host.Live(seeds[0].Cell).Frame);
            Assert.AreEqual(3, fixture.Host.Live(seeds[1].Cell).Frame);
        }

        [TestCase(30f)]
        [TestCase(60f)]
        public void RuntimeChangesOnExactSixOrTwelveSecondBoundaries(float seconds)
        {
            using var fixture = new Fixture(seconds, 1000);
            fixture.Runtime.Advance(0);
            var seed = fixture.Runtime.Seeds.Single();
            for (int stage = 1; stage <= 5; stage++)
            {
                fixture.Runtime.Advance(seconds / 5);
                Assert.AreEqual(seconds * stage / 5, fixture.Runtime.Elapsed);
                Assert.AreEqual(stage, fixture.Host.Live(seed.Cell).Frame);
                Assert.AreEqual(stage == 5, seed.Mature);
            }
        }

        [Test]
        public void LargeAndSmallAdvancesProduceSamePerSeedSprites()
        {
            using var large = new Fixture(30, 10);
            using var small = new Fixture(30, 10);
            large.Runtime.Advance(47);
            for (int i = 0; i < 188; i++) small.Runtime.Advance(.25f);
            CollectionAssert.AreEquivalent(
                large.Runtime.Seeds.Select(s => (s.Cell, s.Mature, large.Host.Live(s.Cell).Frame)),
                small.Runtime.Seeds.Select(s => (s.Cell, s.Mature, small.Host.Live(s.Cell).Frame)));
        }

        [Test]
        public void BurningAndDisposalReleaseViewsWithoutLaterProgressUpdates()
        {
            using var fixture = new Fixture(30, 10);
            fixture.Runtime.Advance(12);
            var seed = fixture.Runtime.Seeds.OrderBy(s => s.MaturesAt).First();
            var removed = fixture.Host.Live(seed.Cell);
            fixture.Runtime.OnPlayerStep(new PlayerTileStep(seed.Cell - Vector2Int.up, seed.Cell, true));
            Assert.IsTrue(removed.Disposed);
            int removedUpdates = removed.Updates;
            fixture.Runtime.Advance(28);
            Assert.AreEqual(removedUpdates, removed.Updates);
            var mature = fixture.Runtime.Seeds.First(s => s.Mature);
            var matureView = fixture.Host.Live(mature.Cell);
            fixture.Runtime.OnPlayerStep(new PlayerTileStep(mature.Cell - Vector2Int.up, mature.Cell, true));
            Assert.IsFalse(matureView.Disposed);
            Assert.AreEqual(5, matureView.Frame);
            fixture.Runtime.Dispose();
            int updates = fixture.Host.Views.Sum(v => v.Updates);
            fixture.Runtime.Advance(100);
            fixture.Runtime.Dispose();
            Assert.IsTrue(fixture.Host.Views.All(v => v.Disposed));
            Assert.AreEqual(updates, fixture.Host.Views.Sum(v => v.Updates));
            Assert.IsEmpty(fixture.Runtime.Seeds);
        }

        [UnityTest]
        public IEnumerator RealGameHostDisplaysGrowthStagesPausesAndCleansUpWithoutEditingBossAsset()
        {
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            Assert.IsNotNull(game);
            var source = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            string before = EditorJsonUtility.ToJson(source);
            bool dirtyBefore = EditorUtility.IsDirty(source);
            var copy = Object.Instantiate(source);
            float oldScale = Time.timeScale;
            try
            {
                if (Object.FindAnyObjectByType<AudioListener>() == null) game.gameObject.AddComponent<AudioListener>();
                Time.timeScale = 0;
                game.PreviewPattern(source, new EncounterPattern { minimumDuration = 999 });
                Set(game, "activeBoss", copy);
                var growth = copy.phases[0].mechanics.OfType<RegionGrowthMechanic>().Single();
                growth.initialDelay = 0; growth.interval = 1000; growth.seedsPerSpawn = 1; growth.growthSeconds = 30;
                var model = Get<TrailFieldModel>(game, "model");
                var seedCell = model.Traversable.Where(c => c != model.Player)
                    .OrderBy(c => (c - model.Player - Vector2Int.up * 2).sqrMagnitude).First();
                const string testRegionId = "seed-visual-test-region";
                copy.tileRegions.Add(new EncounterTileRegion { id = testRegionId, cells = new List<Vector2Int> { seedCell } });
                growth.regionIds = new List<string> { testRegionId };
                growth.enrageWhenAll.Clear(); growth.enrageWhenAny.Clear();
                Call(game, "StartSpecialTiles"); Call(game, "StartMechanics");
                Call(game, "AdvanceMechanics", 0f);
                var runtime = Get<BossMechanicSession>(game, "mechanicSession").Runtimes.OfType<RegionGrowthRuntime>().Single();
                var root = Get<RectTransform>(game, "mechanicVisualRoot");
                Assert.AreEqual(seedCell, runtime.Seeds.Single().Cell);
                Set(game, "battleCameraInitialized", false); Call(game, "RefreshBoard");
                yield return WaitForPlayerFrames("initial seed");
                for (int stage = 0; stage < 6; stage++)
                {
                    if (stage > 0)
                    {
                        Call(game, "AdvanceMechanics", 6f);
                        yield return WaitForPlayerFrames("growth stage " + stage);
                    }
                    var view = root.GetComponentsInChildren<MechanicProgressVisual>().Single();
                    AssertSpriteIdentity(view.frames[stage], view.GetComponent<Image>().sprite);
                    Assert.AreEqual(Color.white, view.GetComponent<Image>().color);
                    Assert.AreEqual(Vector2.one * Get<float>(game, "mainCellSize"), ((RectTransform)view.transform).sizeDelta);
                    if (stage == 0 || stage == 2 || stage == 5)
                        Capture(game, stage == 0 ? "Initial" : stage == 2 ? "MidGrowth" : "FullBloom");
                    if (stage == 2)
                    {
                        Set(game, "inputLocked", true); Call(game, "AdvanceMechanics", 60f);
                        Assert.AreEqual(12, runtime.Elapsed);
                        AssertSpriteIdentity(view.frames[2], view.GetComponent<Image>().sprite);
                        Set(game, "inputLocked", false);
                    }
                }
                Assert.IsTrue(runtime.Seeds.Single().Mature);
                Call(game, "StopMechanics");
                Assert.IsNull(Get<BossMechanicSession>(game, "mechanicSession"));
                Assert.IsEmpty(runtime.Seeds);
                Assert.IsEmpty(Get<Dictionary<string, Transform>>(game, "timelineObjects"));
                yield return WaitForViewsDestroyed(root);
                Call(game, "StartMechanics"); Call(game, "AdvanceMechanics", 0f);
                yield return WaitForPlayerFrames("restarted seed");
                var restartedRuntime = Get<BossMechanicSession>(game, "mechanicSession").Runtimes.OfType<RegionGrowthRuntime>().Single();
                var restarted = root.GetComponentsInChildren<MechanicProgressVisual>().Single();
                AssertSpriteIdentity(restarted.frames[0], restarted.GetComponent<Image>().sprite);
                Call(game, "StopMechanics");
                Assert.IsNull(Get<BossMechanicSession>(game, "mechanicSession"));
                Assert.IsEmpty(restartedRuntime.Seeds);
                Assert.IsEmpty(Get<Dictionary<string, Transform>>(game, "timelineObjects"));
                yield return WaitForViewsDestroyed(root);
                Assert.AreEqual(before, EditorJsonUtility.ToJson(source));
                Assert.AreEqual(dirtyBefore, EditorUtility.IsDirty(source));
            }
            finally
            {
                Call(game, "StopMechanics"); game.StopPatternPreview();
                Time.timeScale = oldScale; Object.Destroy(copy);
            }
            yield return new ExitPlayMode();
        }

        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Get<T>(TraceStrikeGame game, string field) => (T)typeof(TraceStrikeGame).GetField(field, Flags).GetValue(game);
        private static void Set(TraceStrikeGame game, string field, object value) => typeof(TraceStrikeGame).GetField(field, Flags).SetValue(game, value);
        private static object Call(TraceStrikeGame game, string method, params object[] args) => typeof(TraceStrikeGame).GetMethod(method, Flags).Invoke(game, args);
        private static IEnumerator WaitForPlayerFrames(string reason)
        {
            int firstFrame = Time.frameCount;
            double started = EditorApplication.timeSinceStartup;
            // The EditMode runner's null yields are editor updates. Wait for actual player frames
            // before inspecting replacements or rendering so deferred Destroy has been flushed.
            while (Time.frameCount - firstFrame < 2 && EditorApplication.timeSinceStartup - started < 3)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
            string state = $"Seed visual frame wait ({reason}): start={firstFrame}, end={Time.frameCount}, " +
                $"playerFrames={Time.frameCount - firstFrame}, elapsed={EditorApplication.timeSinceStartup - started:F4}, " +
                $"time={Time.timeAsDouble:F4}, unscaledTime={Time.unscaledTimeAsDouble:F4}, " +
                $"timeScale={Time.timeScale}, paused={EditorApplication.isPaused}, playing={Application.isPlaying}";
            Debug.Log(state);
            Assert.GreaterOrEqual(Time.frameCount - firstFrame, 2, "Player loop did not advance within three real seconds. " + state);
        }
        private static IEnumerator WaitForViewsDestroyed(RectTransform root)
        {
            // Runtime ownership is checked synchronously above. Destroy is deferred; an EditMode
            // test enumerator advances on editor updates, which are not player-loop frames.
            int firstFrame = Time.frameCount;
            double started = EditorApplication.timeSinceStartup;
            Debug.Log(DestructionState("start", root, firstFrame, started));
            while (root.GetComponentsInChildren<MechanicProgressVisual>().Length > 0 &&
                Time.frameCount - firstFrame < 8 && EditorApplication.timeSinceStartup - started < 3)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
            string state = DestructionState("end", root, firstFrame, started);
            Debug.Log(state);
            Assert.IsEmpty(root.GetComponentsInChildren<MechanicProgressVisual>(),
                "Disposed views must disappear within eight player-loop frames or three real seconds. " + state);
        }
        private static string DestructionState(string phase, RectTransform root, int firstFrame, double started) =>
            $"Seed view destruction {phase}: frame={Time.frameCount}, playerFrames={Time.frameCount - firstFrame}, " +
            $"elapsed={EditorApplication.timeSinceStartup - started:F4}, time={Time.timeAsDouble:F4}, " +
            $"unscaledTime={Time.unscaledTimeAsDouble:F4}, timeScale={Time.timeScale}, paused={EditorApplication.isPaused}, " +
            $"playing={Application.isPlaying}, focused={Application.isFocused}, background={Application.runInBackground}, " +
            $"remaining={root.GetComponentsInChildren<MechanicProgressVisual>().Length}";
        private static void AssertSpriteIdentity(Sprite expected, Sprite actual)
        {
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(expected, out string expectedGuid, out long expectedId));
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actual, out string actualGuid, out long actualId));
            Assert.AreEqual(expectedGuid, actualGuid); Assert.AreEqual(expectedId, actualId);
        }
        private static void Capture(TraceStrikeGame game, string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var texture = new RenderTexture(1600, 900, 24);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); texture.Create();
                RenderPipeline.SubmitRenderRequest(Get<Camera>(game, "uiCamera"),
                    new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/SeedGrowthVisual");
                File.WriteAllBytes("Logs/PatternValidation/SeedGrowthVisual/" + name + ".png", pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; texture.Release(); Object.Destroy(texture); Object.Destroy(pixels); }
        }
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        private sealed class Fixture : IDisposable
        {
            public readonly RecordingHost Host = new RecordingHost();
            public readonly RegionGrowthMechanic Definition;
            public readonly RegionGrowthRuntime Runtime;
            public Fixture(float seconds, float interval)
            {
                Definition = new RegionGrowthMechanic
                {
                    initialDelay = 0, interval = interval, growthSeconds = seconds, seedsPerSpawn = 1,
                    coverage = 1, regionIds = new List<string> { "test-region" },
                    overgrownTile = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Vines"),
                    seedPrefab = Prefab, maturePrefab = Prefab
                };
                Runtime = new RegionGrowthRuntime(Definition, new MechanicContext(Host, _ => null), 42);
            }
            public void Dispose() { Runtime.Dispose(); Host.Dispose(); }
        }
        private sealed class DeviceLease : IPatternLease
        {
            public readonly Vector2Int Cell;
            public readonly MechanicProgressVisual Visual;
            public bool Disposed { get; private set; }
            public float Progress { get; private set; } = -1;
            public int Updates { get; private set; }
            public int Frame => Array.IndexOf(Visual.frames, Visual.GetComponent<Image>().sprite);
            public DeviceLease(Vector2Int cell, GameObject prefab)
            { Cell = cell; Visual = Object.Instantiate(prefab).GetComponent<MechanicProgressVisual>(); }
            public void SetProgress(float progress)
            {
                Assert.IsFalse(Disposed, "A removed seed must never receive another visual update.");
                Progress = progress; Updates++; Visual.SetProgress(progress);
            }
            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true; Object.DestroyImmediate(Visual.gameObject);
            }
        }
        private sealed class EmptyLease : IPatternLease
        { public void SetProgress(float progress) { } public void Dispose() { } }
        private sealed class RecordingHost : IPatternHost, IMechanicPresentationHost, IEncounterRegionHost, ISpecialTileHost, IDisposable
        {
            private readonly Vector2Int[] cells = Enumerable.Range(0, 10).Select(x => new Vector2Int(x, 0)).ToArray();
            public readonly List<DeviceLease> Views = new List<DeviceLease>();
            public Vector2Int PlayerCell => new Vector2Int(100, 100);
            public Vector2Int CenterCell => Vector2Int.zero;
            public IReadOnlyCollection<Vector2Int> Walkable => cells;
            public IReadOnlyCollection<Vector2Int> Traversable => cells;
            public bool IsAlive => true;
            public int FireMovesRemaining => 0;
            public IReadOnlyList<EncounterTileRegion> TileRegions => new[] { new EncounterTileRegion { id = "test-region", cells = cells.ToList() } };
            public DeviceLease Live(Vector2Int cell) => Views.Single(v => v.Cell == cell && !v.Disposed);
            public IPatternLease ShowDevice(Vector2Int cell, GameObject prefab, Sprite sprite, Color tint)
            { var view = new DeviceLease(cell, prefab); Views.Add(view); return view; }
            public IPatternLease PlaceSpecialTiles(SpecialTileDefinition tile, IReadOnlyCollection<Vector2Int> cells) => new EmptyLease();
            public IPatternLease Spawn(string key, GameObject prefab, Vector2Int cell, Sprite sprite, Color color) => throw new NotSupportedException();
            public IPatternLease Mark(IReadOnlyCollection<Vector2Int> cells, Color color, bool warning) => throw new NotSupportedException();
            public IPatternLease Block(IReadOnlyCollection<Vector2Int> cells) => throw new NotSupportedException();
            public IPatternLease Hazard(IReadOnlyCollection<Vector2Int> cells, string reason) => throw new NotSupportedException();
            public IPatternLease Sound(AudioClip clip, float volume) => throw new NotSupportedException();
            public IPatternLease Motion(string key, Vector2Int target) => throw new NotSupportedException();
            public IPatternLease Camera(Vector2 offset, float shake) => throw new NotSupportedException();
            public void Damage(string reason) => throw new NotSupportedException();
            public void Signal(string name, string argument) => throw new NotSupportedException();
            public void Dispose() { foreach (var view in Views) view.Dispose(); }
        }
    }
}
#endif
