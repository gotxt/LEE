#if UNITY_EDITOR
using System.Collections;
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
    public sealed class RottenBloomFirePlacementTests
    {
        const string BossPath = "Assets/Resources/Patterns/BossData_.RottenBloom.asset";
        static readonly Vector2Int[] Corners = {
            new Vector2Int(12, 12), new Vector2Int(19, 12),
            new Vector2Int(12, 19), new Vector2Int(19, 19)
        };
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Get<T>(TraceStrikeGame game, string name) => (T)typeof(TraceStrikeGame).GetField(name, Flags).GetValue(game);
        static void Set(TraceStrikeGame game, string name, object value) => typeof(TraceStrikeGame).GetField(name, Flags).SetValue(game, value);
        static object Call(TraceStrikeGame game, string name, params object[] args) => typeof(TraceStrikeGame).GetMethod(name, Flags).Invoke(game, args);

        [Test]
        public void SavedFourCornersReloadAsValidCentralFloorWithSharedFireReferences()
        {
            AssetDatabase.ImportAsset(BossPath, ImportAssetOptions.ForceUpdate);
            var saved = AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(BossPath);
            var fire = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Fire");
            var copy = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(saved), copy);
                foreach (var boss in new[] { saved, copy })
                {
                    Assert.IsEmpty(boss.ValidateDefinition());
                    CollectionAssert.AreEquivalent(Corners, boss.arena.specialTiles.Select(p => p.cell));
                    var central = boss.tileRegions.Single(r => r.id == "97821fa0658f4977bf2903e110a25f20");
                    using var host = new NHN.TraceStrike.Editor.PatternPreviewHost(boss);
                    foreach (var placement in boss.arena.specialTiles)
                    {
                        Assert.AreEqual(fire, placement.tile);
                        Assert.Contains(placement.cell, central.cells);
                        Assert.IsTrue(host.Traversable.Contains(placement.cell));
                        Assert.IsFalse(boss.bossVisual.OccupiedCells().Contains(placement.cell));
                    }
                }
                Assert.IsTrue(fire.requiresCompletedAttack);
                Assert.AreEqual(30, fire.activationSeconds); Assert.AreEqual(20, fire.fireMoveCount);
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [UnityTest]
        public IEnumerator SavedPlacementsLoadRenderIgniteAndResetInGame()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required.");
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            if (Object.FindAnyObjectByType<AudioListener>() == null) game.gameObject.AddComponent<AudioListener>();
            var saved = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            var fire = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Fire");
            string before = EditorJsonUtility.ToJson(saved);
            var copy = Object.Instantiate(saved);
            float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                game.PreviewPattern(saved, new EncounterPattern { minimumDuration = 999 });
                var field = Get<SpecialTileField>(game, "placedSpecialTiles");
                // Only the temporary view is zoomed out; saved camera/spawn/layout are untouched.
                copy.arena.cameraZoom = 1.25f;
                Set(game, "activeBoss", copy);
                Call(game, "ApplyEncounterArenaLayout");
                Assert.IsTrue(Get<TrailFieldModel>(game, "model").TryPlacePlayer(new Vector2Int(15, 13)));
                Set(game, "battleCameraInitialized", false); Call(game, "RefreshBoard");
                foreach (var cell in Corners)
                {
                    Assert.AreEqual(fire, field.At(cell)); Assert.IsFalse(field.IsActive(cell));
                    Assert.AreEqual(fire.inactiveSprite, Get<Image[,]>(game, "specialItemIconImages")[cell.x, cell.y].sprite);
                }
                yield return Frames(); Capture(game, "CentralCorners-Inactive");
                // Supply completed-path cells directly; full attack flow is covered by FireTileRuntimeTests.
                Assert.IsTrue(field.OnCompletedAttack(Corners)); Call(game, "RefreshBoard");
                foreach (var cell in Corners)
                {
                    Assert.IsTrue(field.IsActive(cell)); Assert.AreEqual(30, field.ActiveSecondsRemaining(cell));
                    var icon = Get<Image[,]>(game, "specialItemIconImages")[cell.x, cell.y];
                    Assert.IsTrue(icon.gameObject.activeInHierarchy); Assert.AreEqual(fire.sprite, icon.sprite);
                }
                yield return Frames(); Capture(game, "CentralCorners-Active");
                Assert.IsTrue(Get<TrailFieldModel>(game, "model").TryPlacePlayer(new Vector2Int(12, 11)));
                Call(game, "Move", Vector2Int.up);
                Assert.AreEqual(new Vector2Int(12, 12), Get<TrailFieldModel>(game, "model").Player);
                Assert.AreEqual(20, game.FireMovesRemaining);
                game.StopPatternPreview();
                var restarted = Get<SpecialTileField>(game, "placedSpecialTiles");
                foreach (var cell in Corners) { Assert.AreEqual(fire, restarted.At(cell)); Assert.IsFalse(restarted.IsActive(cell)); }
                Assert.AreEqual(0, game.FireMovesRemaining);
                Assert.AreEqual(before, EditorJsonUtility.ToJson(saved));
            }
            finally { game.StopAllCoroutines(); Time.timeScale = oldScale; Object.Destroy(copy); }
            yield return new ExitPlayMode();
        }

        static IEnumerator Frames() { int frame = Time.frameCount + 2; while (Time.frameCount < frame) yield return null; }
        static void Capture(TraceStrikeGame game, string name)
        {
            var rt = new RenderTexture(1600, 900, 24);
            var read = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); rt.Create();
                RenderPipeline.SubmitRenderRequest(Get<Camera>(game, "uiCamera"),
                    new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); read.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/RottenBloomCornerFire-1007");
                File.WriteAllBytes("Logs/PatternValidation/RottenBloomCornerFire-1007/" + name + ".png", read.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; rt.Release(); Object.Destroy(rt); Object.Destroy(read); }
        }
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
