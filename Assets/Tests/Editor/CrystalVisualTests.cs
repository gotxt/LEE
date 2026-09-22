#if UNITY_EDITOR
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

namespace NHN.TraceStrike.Tests
{
    public sealed class CrystalVisualTests
    {
        static CrystalVisual Prefab => Resources.Load<CrystalVisual>("Art/Crystals/PhaseTwoCrystal");
        static T Field<T>(TraceStrikeGame game, string name) =>
            (T)typeof(TraceStrikeGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        static void Set(TraceStrikeGame game, string name, object value) =>
            typeof(TraceStrikeGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, value);
        static object Call(TraceStrikeGame game, string name, params object[] args)
        {
            var method = typeof(TraceStrikeGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            var parameters = method.GetParameters();
            var supplied = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                supplied[i] = i < args.Length ? args[i] : parameters[i].DefaultValue;
            return method.Invoke(game, supplied);
        }

        [Test]
        public void PrefabUsesSeparateOriginalSpritesAndTranslucentShadowBehindBody()
        {
            var prefab = Prefab;
            Assert.IsNotNull(prefab);
            var body = prefab.Body.GetComponent<Image>(); var shadow = prefab.Shadow;
            AssertSameSpriteAsset(Resources.Load<Sprite>("Art/Crystals/red"), body.sprite);
            AssertSameSpriteAsset(Resources.Load<Sprite>("Art/Crystals/hole"), shadow.sprite);
            Assert.AreEqual(new Vector2(64, 64), body.sprite.rect.size);
            Assert.AreEqual(new Vector2(64, 64), shadow.sprite.rect.size);
            Assert.AreEqual(FilterMode.Point, body.sprite.texture.filterMode);
            Assert.AreEqual(FilterMode.Point, shadow.sprite.texture.filterMode);
            Assert.Less(shadow.transform.GetSiblingIndex(), body.transform.GetSiblingIndex());
            Assert.That(shadow.color.a, Is.EqualTo(.42f).Within(.0001f));
            Assert.AreEqual(Color.white, body.color);
            Assert.IsFalse(body.raycastTarget || shadow.raycastTarget);
            Assert.IsEmpty(prefab.GetComponentsInChildren<Outline>(true));
        }

        static void AssertSameSpriteAsset(Sprite expected, Sprite actual)
        {
            // Play Mode transitions can recreate managed wrappers for the same asset.
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(expected, out string expectedGuid, out long expectedId));
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actual, out string actualGuid, out long actualId));
            Assert.AreEqual(expectedGuid, actualGuid); Assert.AreEqual(expectedId, actualId);
        }

        [TestCase(96f)]
        [TestCase(160f)]
        public void PulseKeepsGroundShadowAndBodyTipFixedAtAnyTileScale(float size)
        {
            var view = Object.Instantiate(Prefab);
            try
            {
                view.SetCellSize(size);
                Assert.AreEqual(Vector2.one * size, ((RectTransform)view.transform).sizeDelta);
                Assert.That(view.Body.rect.height / size, Is.EqualTo(1.22f).Within(.0001f));
                Vector3 tip = view.Body.position, shadowPosition = view.Shadow.transform.position;
                foreach (float pulse in new[] { .94f, 1.08f })
                {
                    view.SetPulse(pulse);
                    Assert.AreEqual(Vector3.one * pulse, view.Body.localScale);
                    Assert.AreEqual(tip, view.Body.position);
                    Assert.AreEqual(shadowPosition, view.Shadow.transform.position);
                    Assert.AreEqual(Vector3.one, view.Shadow.transform.localScale);
                    Assert.AreEqual(Vector3.one, view.transform.localScale);
                    Assert.That(view.Shadow.color.a, Is.EqualTo(.42f).Within(.0001f));
                }
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        [UnityTest]
        public IEnumerator PhaseEntryCreatesInactiveSealsAndFinalActivationRequiresAnotherAttack()
        {
            yield return new EnterPlayMode();
            var listener = new GameObject("Crystal visual test audio", typeof(AudioListener));
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            float previousTimeScale = Time.timeScale;
            try
            {
                Call(game, "StartStage", 0);
                var legacyViews = Field<CrystalVisual[]>(game, "crystalPresentations");
                Assert.IsTrue(legacyViews.All(v => !v.gameObject.activeSelf));
                Assert.IsEmpty(Field<RectTransform>(game, "mainGrid").GetComponentsInChildren<CrystalVisual>(),
                    "Crystals must not appear before entering phase two.");
                // Use the actual phase-entry coroutine, not a separately constructed session.
                game.StartCoroutine((IEnumerator)Call(game, "SkipBossPhase"));
                float deadline = Time.realtimeSinceStartup + 10;
                while (Field<bool>(game, "inputLocked") && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.IsFalse(Field<bool>(game, "inputLocked"), "Phase-entry transition did not finish.");
                Time.timeScale = 0;
                Call(game, "CancelTimeline");
                // Keep unrelated phase patterns outside this mechanic integration test.
                Set(game, "timelineVersion", Field<int>(game, "patternVersion"));
                Set(game, "timelineWait", 10000f);
                Assert.IsTrue(Field<bool>(game, "phaseTwoActive"));
                Assert.AreEqual(1, Field<int>(game, "activePhaseIndex"));
                var session = Field<BossMechanicSession>(game, "mechanicSession");
                var runtime = (CrystalSealRuntime)session.Runtimes.Single();
                Assert.AreEqual(0, runtime.ActiveCount);
                Assert.IsTrue(legacyViews.All(v => !v.gameObject.activeSelf));
                var grid = Field<RectTransform>(game, "mainGrid");
                var views = grid.GetComponentsInChildren<CrystalVisual>();
                var cells = runtime.Devices.Select(d => d.Cell).ToArray();
                var configuredSeal = Field<BossEncounterDefinition>(game, "activeBoss").phases[1]
                    .mechanics.OfType<CrystalSealMechanic>().Single();
                CollectionAssert.AreEqual(configuredSeal.PlacementCells, cells,
                    "Phase entry must spawn crystals at the editor-authored positions without random relocation.");
                CheckLayout(game, views, cells);
                Assert.IsTrue(views.All(v => v.Body.GetComponent<Image>().color.a < 1));
                FrameCrystal(game);
                yield return null;
                foreach (var view in views) view.SetPulse(1);
                Capture(game, "Activation_InitialInactive");

                using (((IPatternHost)game).Block(cells))
                    foreach (var cell in cells) Assert.IsFalse(Field<TrailFieldModel>(game, "model").IsBlocked(cell));
                session.Advance(30);
                Assert.IsEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                Assert.AreEqual(0, runtime.ActiveCount, "Waiting alone cannot activate a device or start its attack.");

                yield return CompleteAttack(game, new[] { cells[0] });
                Assert.AreEqual(1, runtime.ActiveCount);
                Assert.AreEqual(4, session.RequiredCells.Count());
                yield return null;
                views = grid.GetComponentsInChildren<CrystalVisual>();
                Assert.AreEqual(1, views.Count(v => v.Body.GetComponent<Image>().color.a == 1));
                Assert.AreEqual(4, views.Count(v => v.Body.GetComponent<Image>().color.a < 1));
                Capture(game, "Activation_Partial");
                session.Advance(5.1f);
                Assert.AreEqual(1, Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger").Count);
                var viewInstances = views.Select(v => v.GetEntityId()).ToArray();
                var warningIds = Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger").Keys.ToArray();
                yield return CompleteAttack(game, new[] { cells[0] });
                CollectionAssert.AreEquivalent(viewInstances, grid.GetComponentsInChildren<CrystalVisual>().Select(v => v.GetEntityId()));
                CollectionAssert.AreEquivalent(warningIds, Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger").Keys);

                Set(game, "bossHealth", 1);
                yield return CompleteAttack(game, cells);
                Assert.IsFalse(Field<bool>(game, "inputLocked"));
                Assert.AreEqual(1, Field<int>(game, "bossHealth"));
                Assert.AreEqual(5, runtime.ActiveCount);
                Assert.AreEqual(0, session.MinimumBossHealth);
                Assert.IsEmpty(session.RequiredCells);
                Assert.IsFalse(Field<bool>(game, "gameCleared"));
                CollectionAssert.AreEqual(cells, runtime.Devices.Select(d => d.Cell));
                yield return null;
                views = grid.GetComponentsInChildren<CrystalVisual>();
                Assert.AreEqual(5, views.Length);
                Assert.IsTrue(views.All(v => v.Body.GetComponent<Image>().color.a == 1));
                Capture(game, "Activation_AllActive");
                session.Advance(5.1f);
                Assert.IsNotEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"),
                    "All activated devices keep attacking until the encounter actually ends.");

                var model = Field<TrailFieldModel>(game, "model");
                yield return CompleteAttack(game, new[] { model.Start, model.End });
                Assert.IsTrue(Field<bool>(game, "gameCleared"));
                Assert.AreEqual(0, Field<int>(game, "bossHealth"));
                Assert.IsNull(Field<BossMechanicSession>(game, "mechanicSession"));
                Assert.IsEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                yield return null;
                Assert.IsEmpty(grid.GetComponentsInChildren<CrystalVisual>());

                Call(game, "StartStage", 0);
                yield return null;
                Assert.IsTrue(legacyViews.All(v => !v.gameObject.activeSelf));
                Assert.IsEmpty(Field<List<Vector2Int>>(game, "crystalCells"));
                Assert.AreEqual(CrystalRules.CrystalCount,
                    Field<RectTransform>(game, "mainGrid").GetComponentsInChildren<CrystalVisual>(true).Length);
                Call(game, "StartHub");
                Assert.IsNull(Field<BossMechanicSession>(game, "mechanicSession"));
            }
            finally { Time.timeScale = previousTimeScale; Object.Destroy(listener); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PhaseOneProtectionTransitionsToFreshPhaseTwoAndDeathRestartsWithoutLeasesOrAssetChanges()
        {
            yield return new EnterPlayMode();
            var listener = new GameObject("Crystal phase integration audio", typeof(AudioListener));
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            var source = Resources.Load<BossEncounterDefinition>("Patterns/CrimsonGolem");
            string sourceBefore = EditorJsonUtility.ToJson(source);
            bool dirtyBefore = EditorUtility.IsDirty(source);
            var fixture = Object.Instantiate(source);
            float previousTimeScale = Time.timeScale;
            try
            {
                // Clone settings only into a test instance. Resources and BossCatalog are never edited.
                var phaseOneSeal = new CrystalSealMechanic
                {
                    attackPatternId = fixture.phases[1].mechanics.OfType<CrystalSealMechanic>().Single().attackPatternId,
                    activePrefab = Prefab.gameObject,
                    initialDelay = 5,
                    interval = 5
                };
                phaseOneSeal.crystals.Add(new CrystalPlacement
                { cell = fixture.phases[1].mechanics.OfType<CrystalSealMechanic>().Single().crystals[0].cell });
                fixture.phases[0].mechanics.Add(phaseOneSeal);
                foreach (var phase in fixture.phases) phase.initialDelay = 10000;
                Assert.AreEqual(sourceBefore, EditorJsonUtility.ToJson(source), "Editing the fixture must not mutate the source's managed references.");
                Assert.AreEqual(dirtyBefore, EditorUtility.IsDirty(source));
                Call(game, "StartStage", 0);
                Time.timeScale = 0;
                Call(game, "CancelTimeline");
                Set(game, "activeBoss", fixture);
                Set(game, "timelineWait", 10000f);
                Call(game, "StartMechanics");
                var phaseOneSession = Field<BossMechanicSession>(game, "mechanicSession");
                var phaseOneRuntime = (CrystalSealRuntime)phaseOneSession.Runtimes.Single();
                Assert.AreEqual(0, Field<int>(game, "activePhaseIndex"));
                Assert.AreEqual(0, phaseOneRuntime.ActiveCount);
                Set(game, "bossHealth", 1);
                yield return CompleteAttack(game, phaseOneSeal.PlacementCells);
                Assert.AreEqual(1, Field<int>(game, "bossHealth"));
                Assert.AreEqual(0, Field<int>(game, "activePhaseIndex"));
                Assert.AreEqual(1, phaseOneRuntime.ActiveCount);
                FrameCrystal(game);
                phaseOneSession.Advance(5.1f);
                Assert.IsNotEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                var phaseOneViews = Field<RectTransform>(game, "mainGrid").GetComponentsInChildren<CrystalVisual>();
                var model = Field<TrailFieldModel>(game, "model");
                yield return CompleteAttack(game, new[] { model.Start, model.End });
                Assert.AreEqual(1, Field<int>(game, "activePhaseIndex"));
                Assert.AreEqual(fixture.phases[1].health, Field<int>(game, "bossHealth"));
                Assert.IsEmpty(phaseOneSession.Runtimes);
                Assert.IsEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                yield return null;
                Assert.IsTrue(phaseOneViews.All(v => v == null), "P1 device objects must be destroyed at the transition.");

                var phaseTwoSession = Field<BossMechanicSession>(game, "mechanicSession");
                var phaseTwoRuntime = (CrystalSealRuntime)phaseTwoSession.Runtimes.Single();
                Assert.AreEqual(0, phaseTwoRuntime.ActiveCount);
                Assert.AreEqual(5, phaseTwoSession.RequiredCells.Count());
                Assert.AreEqual(1, phaseTwoSession.MinimumBossHealth);
                yield return CompleteAttack(game, new[] { phaseTwoRuntime.Devices[0].Cell });
                phaseTwoSession.Advance(5.1f);
                Assert.IsNotEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                var phaseTwoViews = Field<RectTransform>(game, "mainGrid").GetComponentsInChildren<CrystalVisual>();
                Time.timeScale = 1;
                game.StartCoroutine((IEnumerator)Call(game, "KillPlayer", "Crystal integration test"));
                Assert.IsNull(Field<BossMechanicSession>(game, "mechanicSession"));
                Assert.IsEmpty(phaseTwoSession.Runtimes);
                Assert.IsEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                float deadline = Time.realtimeSinceStartup + 10;
                while (Field<bool>(game, "playerDead") && Time.realtimeSinceStartup < deadline) yield return null;
                Time.timeScale = 0;
                Assert.IsFalse(Field<bool>(game, "playerDead"), "Death should complete the ordinary StartStage restart.");
                Assert.AreEqual(0, Field<int>(game, "activePhaseIndex"));
                Assert.AreEqual(source.phases[0].health, Field<int>(game, "bossHealth"));
                Assert.IsTrue(phaseTwoViews.All(v => v == null));
                Assert.IsEmpty(Field<BossMechanicSession>(game, "mechanicSession").Runtimes);
                Call(game, "StartHub");
                Assert.IsNull(Field<BossMechanicSession>(game, "mechanicSession"));
                Assert.AreEqual(sourceBefore, EditorJsonUtility.ToJson(source));
                Assert.AreEqual(dirtyBefore, EditorUtility.IsDirty(source));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Call(game, "StopMechanics");
                Call(game, "CancelTimeline");
                Set(game, "activeBoss", source);
                Time.timeScale = previousTimeScale;
                Object.Destroy(fixture);
                Object.Destroy(listener);
            }
            yield return new ExitPlayMode();
        }

        static IEnumerator CompleteAttack(TraceStrikeGame game, IEnumerable<Vector2Int> cells)
        {
            // Exercise the real attack and phase-transition pipeline with completed hit cells.
            // Path construction/input remains the responsibility of TrailFieldModel tests.
            var trail = (List<Vector2Int>)Field<TrailFieldModel>(game, "model").Trail;
            trail.Clear(); trail.AddRange(cells);
            Time.timeScale = 1;
            game.StartCoroutine((IEnumerator)Call(game, "ExecuteAttack"));
            float deadline = Time.realtimeSinceStartup + 12;
            while (Field<bool>(game, "inputLocked") && !Field<bool>(game, "gameCleared") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 0;
            Assert.IsTrue(!Field<bool>(game, "inputLocked") || Field<bool>(game, "gameCleared"), "Completed attack did not finish.");
        }

        static void CheckLayout(TraceStrikeGame game, CrystalVisual[] views, Vector2Int[] cells)
        {
            var model = Field<TrailFieldModel>(game, "model");
            float size = Field<float>(game, "mainCellSize");
            var tiles = Field<Image[,]>(game, "mainTiles");
            Assert.AreEqual(5, cells.Length);
            Assert.AreEqual(cells.Length, views.Count(v => v.gameObject.activeSelf));
            for (int i = 0; i < cells.Length; i++)
            {
                Assert.IsFalse(model.IsBlocked(cells[i]));
                CollectionAssert.Contains(model.Traversable, cells[i]);
                var root = (RectTransform)views[i].transform;
                float middle = (model.GridSize - 1) * .5f;
                Assert.That(Vector2.Distance(root.anchoredPosition,
                    new Vector2(cells[i].x - middle, cells[i].y - middle) * size), Is.LessThan(1));
                Assert.AreEqual(Vector2.one * size, root.sizeDelta);
                Assert.IsTrue(views[i].Shadow.gameObject.activeInHierarchy);
                var floorColor = (Color)Call(game, "GetFloorColor", cells[i].x, cells[i].y);
                Assert.That(tiles[cells[i].x, cells[i].y].color.r, Is.EqualTo(floorColor.r).Within(.0001f));
                Assert.That(tiles[cells[i].x, cells[i].y].color.g, Is.EqualTo(floorColor.g).Within(.0001f));
                Assert.That(tiles[cells[i].x, cells[i].y].color.b, Is.EqualTo(floorColor.b).Within(.0001f));
            }
        }

        static void FrameCrystal(TraceStrikeGame game)
        {
            var cells = ((CrystalSealRuntime)Field<BossMechanicSession>(game, "mechanicSession").Runtimes.Single()).Devices.Select(d => d.Cell).ToArray();
            var model = Field<TrailFieldModel>(game, "model");
            var target = cells.OrderBy(c => (c - new Vector2Int(19, 10)).sqrMagnitude).First() + Vector2Int.right * 3;
            Assert.IsTrue(model.TryPlacePlayer(model.Traversable
                .Where(c => cells.All(device => Mathf.Max(Mathf.Abs(c.x - device.x), Mathf.Abs(c.y - device.y)) > 2))
                .OrderBy(c => (c - target).sqrMagnitude).First()));
            typeof(TraceStrikeGame).GetField("battleCameraInitialized", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, false);
            Call(game, "RefreshBoard");
        }

        static void Capture(TraceStrikeGame game, string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var rt = new RenderTexture(1600, 900, 24); var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); rt.Create();
                RenderPipeline.SubmitRenderRequest(Field<Camera>(game, "uiCamera"),
                    new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/CrystalVisuals");
                File.WriteAllBytes("Logs/PatternValidation/CrystalVisuals/" + name + ".png", pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; rt.Release(); Object.Destroy(rt); Object.Destroy(pixels); }
        }

        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
