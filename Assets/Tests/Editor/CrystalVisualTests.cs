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
        public IEnumerator PhaseEntryCreatesFiveWalkableSealsAndFinalReleaseRequiresAnotherAttack()
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
                Assert.IsTrue(Field<bool>(game, "phaseTwoActive"));
                Assert.AreEqual(1, Field<int>(game, "activePhaseIndex"));
                var session = Field<BossMechanicSession>(game, "mechanicSession");
                var runtime = (CrystalSealRuntime)session.Runtimes.Single();
                Assert.AreEqual(5, runtime.ActiveCount);
                Assert.IsTrue(legacyViews.All(v => !v.gameObject.activeSelf));
                var grid = Field<RectTransform>(game, "mainGrid");
                var views = grid.GetComponentsInChildren<CrystalVisual>();
                var cells = runtime.Devices.Select(d => d.Cell).ToArray();
                var configuredSeal = Field<BossEncounterDefinition>(game, "activeBoss").phases[1]
                    .mechanics.OfType<CrystalSealMechanic>().Single();
                CollectionAssert.AreEqual(configuredSeal.PlacementCells, cells,
                    "Phase entry must spawn crystals at the editor-authored positions without random relocation.");
                CheckLayout(game, views, cells);
                FrameCrystal(game);
                yield return null;
                foreach (var view in views) view.SetPulse(1);
                Capture(game, "PhaseTwoCrystal_Entry");

                using (((IPatternHost)game).Block(cells))
                    foreach (var cell in cells) Assert.IsFalse(Field<TrailFieldModel>(game, "model").IsBlocked(cell));
                session.Advance(5.1f);
                Assert.IsNotEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));

                var model = Field<TrailFieldModel>(game, "model");
                // Supply a completed attack's hit cells to the real ExecuteAttack pipeline.
                // Route construction itself remains covered by TrailFieldModel tests.
                var trail = (List<Vector2Int>)model.Trail;
                trail.Clear(); trail.AddRange(cells);
                typeof(TraceStrikeGame).GetField("bossHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, 1);
                Time.timeScale = 1;
                game.StartCoroutine((IEnumerator)Call(game, "ExecuteAttack"));
                deadline = Time.realtimeSinceStartup + 10;
                while (Field<bool>(game, "inputLocked") && Time.realtimeSinceStartup < deadline) yield return null;
                Time.timeScale = 0;
                Assert.IsFalse(Field<bool>(game, "inputLocked"));
                Assert.AreEqual(1, Field<int>(game, "bossHealth"));
                Assert.AreEqual(0, runtime.ActiveCount);
                Assert.IsEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                Assert.IsFalse(Field<bool>(game, "gameCleared"));
                CollectionAssert.AreEqual(cells, runtime.Devices.Select(d => d.Cell));
                yield return null;
                views = grid.GetComponentsInChildren<CrystalVisual>();
                Assert.AreEqual(5, views.Length);
                Assert.IsTrue(views.All(v => v.Body.GetComponent<Image>().color.a < 1));
                Capture(game, "PhaseTwoCrystal_Disabled");

                trail.Clear(); trail.Add(model.Start); trail.Add(model.End);
                Time.timeScale = 1;
                game.StartCoroutine((IEnumerator)Call(game, "ExecuteAttack"));
                deadline = Time.realtimeSinceStartup + 10;
                while (!Field<bool>(game, "gameCleared") && Time.realtimeSinceStartup < deadline) yield return null;
                Time.timeScale = 0;
                Assert.IsTrue(Field<bool>(game, "gameCleared"));
                Assert.AreEqual(0, Field<int>(game, "bossHealth"));
                Assert.IsNull(Field<BossMechanicSession>(game, "mechanicSession"));
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
            var cells = ((CrystalSealRuntime)Field<BossMechanicSession>(game, "mechanicSession").Runtimes.Single()).Devices.Select(d => d.Cell);
            var model = Field<TrailFieldModel>(game, "model");
            var target = cells.OrderBy(c => (c - new Vector2Int(19, 10)).sqrMagnitude).First() + Vector2Int.right * 2;
            Assert.IsTrue(model.TryPlacePlayer(model.Traversable.OrderBy(c => (c - target).sqrMagnitude).First()));
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
