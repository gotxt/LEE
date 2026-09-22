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
    public sealed class TileWarningVisualTests
    {
        const string SpritePath = "Assets/Resources/Art/Warnings/danger_indicator_128x128.png";
        static TileWarningVisual Prefab => Resources.Load<TileWarningVisual>("Art/Warnings/TileVisual_AttackWarning");
        static T Field<T>(TraceStrikeGame game, string name) =>
            (T)typeof(TraceStrikeGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        static void Set(TraceStrikeGame game, string name, object value) =>
            typeof(TraceStrikeGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, value);
        static object Call(TraceStrikeGame game, string name, params object[] args)
        {
            var method = typeof(TraceStrikeGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            var parameters = method.GetParameters();
            return method.Invoke(game, parameters.Select((p, i) => i < args.Length ? args[i] : p.DefaultValue).ToArray());
        }

        [Test]
        public void PrefabUsesOriginalPointFilteredSpriteAboveNonInteractiveProgressFill()
        {
            var prefab = Prefab;
            Assert.IsNotNull(prefab);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            AssertOriginalSprite(prefab.Indicator.sprite);
            Assert.AreEqual(new Vector2(128, 128), sprite.rect.size);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.IsTrue(importer.alphaIsTransparency);
            Assert.AreEqual(Color.white, prefab.Indicator.color);
            Assert.IsNull(prefab.ProgressFill.sprite);
            Assert.IsFalse(prefab.Indicator.raycastTarget || prefab.ProgressFill.raycastTarget);
            Assert.Less(prefab.ProgressFill.transform.GetSiblingIndex(), prefab.Indicator.transform.GetSiblingIndex());
        }

        [TestCase(96f)]
        [TestCase(160f)]
        public void ProgressPreservesFullSizeSymbolAndOriginalColorAtEveryTileScale(float size)
        {
            var view = Object.Instantiate(Prefab);
            try
            {
                ((RectTransform)view.transform).sizeDelta = Vector2.one * size;
                var purple = new Color(.7f, .3f, 1, .7f);
                view.SetColor(purple);
                Assert.That(view.ProgressFill.color.a, Is.EqualTo(.7f * .65f).Within(.0001f));
                Assert.AreEqual(purple.r, view.ProgressFill.color.r);
                Assert.AreEqual(purple.b, view.ProgressFill.color.b);
                foreach (float progress in new[] { -.1f, 0, .5f, 1, 1.2f })
                {
                    view.SetProgress(progress);
                    Assert.AreEqual(Vector3.one * Mathf.Clamp01(progress), view.ProgressFill.transform.localScale);
                    Assert.AreEqual(Vector3.one, view.Indicator.transform.localScale);
                    Assert.AreEqual(Vector3.one, view.transform.localScale);
                    Assert.AreEqual(Color.white, view.Indicator.color);
                    Assert.That(view.Indicator.rectTransform.rect.width, Is.EqualTo(size).Within(.001f));
                }
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        [UnityTest]
        public IEnumerator TimelineWarningsKeepAreaColorOverlapOwnershipAndDamageDisplay()
        {
            yield return new EnterPlayMode();
            var audio = new GameObject("Warning test audio", typeof(AudioListener));
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            var boss = Resources.Load<BossEncounterDefinition>("Patterns/BossData_CrimsonGolem");
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                var pattern = boss.FindPattern("p1-alternating-rockfall");
                game.PreviewPattern(boss, pattern);
                var runner = Field<PatternRunner>(game, "timeline");
                var grid = Field<RectTransform>(game, "mainGrid");
                var model = Field<TrailFieldModel>(game, "model");
                Frame(game, model.Traversable.OrderBy(c => ((Vector2)c - boss.bossVisual.position - Vector2.right * 3).sqrMagnitude).First());
                runner.Advance(.6f);
                yield return null;
                var warning = (WarningEvent)pattern.clips.First(c => c.action is WarningEvent).action;
                var cells = warning.tiles.Resolve(new PatternContext((IPatternHost)game, model.CenterCell));
                var views = grid.GetComponentsInChildren<TileWarningVisual>().Where(v => v.name == "Timeline Warning").ToArray();
                Assert.AreEqual(cells.Count, views.Length);
                Assert.AreEqual(cells.Count, Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger").Values.Single().Count);
                foreach (var view in views)
                {
                    AssertOriginalSprite(view.Indicator.sprite);
                    Assert.AreEqual(Color.white, view.Indicator.color);
                    Assert.AreEqual(warning.color.r, view.ProgressFill.color.r);
                    Assert.That(view.ProgressFill.transform.localScale.x, Is.EqualTo(.575f).Within(.001f));
                }
                Capture(game, "RockfallWarning");
                var cell = cells.First();
                var purple = new Color(.7f, .3f, 1, .7f);
                var host = (IPatternHost)game;
                using (var extra = host.Mark(new[] { cell }, purple, true))
                {
                    extra.SetProgress(.25f);
                    var overlay = grid.GetComponentsInChildren<TileWarningVisual>().Last(v => v.name == "Timeline Warning");
                    Assert.That(overlay.ProgressFill.color.b, Is.EqualTo(1));
                    Assert.AreEqual(Color.white, overlay.Indicator.color);
                    Assert.AreEqual(2, Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger").Count);
                }
                yield return null;
                Assert.AreEqual(views.Length, grid.GetComponentsInChildren<TileWarningVisual>().Count(v => v.name == "Timeline Warning"));
                Assert.AreEqual(1, Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger").Count);
                var damageColor = new Color(1, .2f, .1f, .8f);
                using (host.Mark(new[] { cell }, damageColor, false))
                {
                    var damage = grid.GetComponentsInChildren<Image>().Single(i => i.name == "Timeline Damage");
                    Assert.IsNull(damage.sprite);
                    Assert.AreEqual(damageColor, damage.color);
                    Assert.IsNull(damage.GetComponent<TileWarningVisual>());
                }
                // Cross the actual warning-to-impact boundary without advancing beyond the grace period.
                runner.Advance(.60001f);
                yield return null;
                Assert.IsFalse(grid.GetComponentsInChildren<TileWarningVisual>().Any(v => v.name == "Timeline Warning"));
                Assert.IsTrue(grid.GetComponentsInChildren<Image>().Any(i => i.name == "Timeline Damage"));
                game.StopPatternPreview();
                yield return null;
                Assert.IsEmpty(Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                Assert.IsFalse(grid.GetComponentsInChildren<TileWarningVisual>().Any(v => v.name == "Timeline Warning"));
                game.PreviewPattern(boss, pattern);
                Field<PatternRunner>(game, "timeline").Advance(.2f);
                game.StopPatternPreview();
                yield return null;
                Assert.IsFalse(grid.GetComponentsInChildren<TileWarningVisual>().Any(v => v.name == "Timeline Warning"));
            }
            finally { Time.timeScale = previousTimeScale; Object.Destroy(audio); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CrystalBlastUsesSameImageAndReleasesItsWarningOnFinishAndRestart()
        {
            yield return new EnterPlayMode();
            var audio = new GameObject("Crystal warning test audio", typeof(AudioListener));
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            float previousTimeScale = Time.timeScale;
            try
            {
                Call(game, "StartStage", 0);
                game.StartCoroutine((IEnumerator)Call(game, "SkipBossPhase"));
                float deadline = Time.realtimeSinceStartup + 10;
                while (Field<bool>(game, "inputLocked") && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(Field<bool>(game, "phaseTwoActive"));
                Call(game, "CancelTimeline");
                Set(game, "inputLocked", true); // Hold unrelated main patterns, not the crystal coroutine.
                var model = Field<TrailFieldModel>(game, "model");
                var crystal = Field<List<Vector2Int>>(game, "crystalCells")[0];
                Frame(game, model.Traversable.Where(c => Mathf.Abs(c.x - crystal.x) + Mathf.Abs(c.y - crystal.y) >= 3)
                    .OrderBy(c => (c - crystal).sqrMagnitude).First());
                game.StartCoroutine((IEnumerator)Call(game, "FireCrystalBlast", 0, Field<int>(game, "crystalLayoutVersion")));
                float captureTime = Time.time + .25f;
                while (Time.time < captureTime) yield return null;
                Time.timeScale = 0;
                Call(game, "AnimateAttackWarnings");
                var counts = Field<Dictionary<Vector2Int, int>>(game, "crystalWarningCounts");
                var views = Field<TileWarningVisual[,]>(game, "attackWarningPresentations");
                Assert.IsNotEmpty(counts);
                foreach (var c in counts.Keys)
                {
                    var view = views[c.x, c.y];
                    Assert.IsTrue(view.gameObject.activeInHierarchy);
                    AssertOriginalSprite(view.Indicator.sprite);
                    Assert.AreEqual(Color.white, view.Indicator.color);
                    Assert.That(view.ProgressFill.transform.localScale.x, Is.InRange(.1f, .9f));
                }
                Capture(game, "CrystalWarning");
                var testedCells = counts.Keys.ToArray();
                Time.timeScale = 1;
                deadline = Time.realtimeSinceStartup + 3;
                while (counts.Count > 0 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsEmpty(counts);
                Assert.IsFalse(Field<bool>(game, "playerDead"));
                Call(game, "AnimateAttackWarnings");
                Assert.IsTrue(testedCells.All(c => !views[c.x, c.y].gameObject.activeSelf));
                game.StartCoroutine((IEnumerator)Call(game, "FireCrystalBlast", 0, Field<int>(game, "crystalLayoutVersion")));
                Assert.IsNotEmpty(counts);
                Call(game, "StartStage", 0);
                yield return null;
                Assert.IsEmpty(counts);
                Assert.IsTrue(testedCells.All(c => !views[c.x, c.y].gameObject.activeSelf));
            }
            finally { Time.timeScale = previousTimeScale; Object.Destroy(audio); }
            yield return new ExitPlayMode();
        }

        static void AssertOriginalSprite(Sprite sprite)
        {
            // Native Unity assets can have distinct managed wrappers after Play Mode reloads.
            // Compare the serialized asset identity, not CLR reference identity.
            var expected = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            Assert.IsNotNull(sprite);
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(expected, out string expectedGuid, out long expectedId));
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string actualGuid, out long actualId));
            Assert.AreEqual(expectedGuid, actualGuid);
            Assert.AreEqual(expectedId, actualId);
            Assert.AreEqual(SpritePath, AssetDatabase.GetAssetPath(sprite));
        }

        static void Frame(TraceStrikeGame game, Vector2Int cell)
        {
            Assert.IsTrue(Field<TrailFieldModel>(game, "model").TryPlacePlayer(cell));
            Set(game, "battleCameraInitialized", false);
            Call(game, "RefreshBoard");
        }

        static void Capture(TraceStrikeGame game, string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var rt = new RenderTexture(1600, 900, 24);
            var read = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); rt.Create();
                RenderPipeline.SubmitRenderRequest(Field<Camera>(game, "uiCamera"),
                    new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); read.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/DangerIndicator");
                File.WriteAllBytes("Logs/PatternValidation/DangerIndicator/" + name + ".png", read.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; rt.Release(); Object.Destroy(rt); Object.Destroy(read); }
        }

        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
