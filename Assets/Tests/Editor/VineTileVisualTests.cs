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
    public sealed class VineTileVisualTests
    {
        const string SpritePath = "Assets/Resources/Art/SpecialTiles/TileSprite_StunThornVines.png";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Get<T>(TraceStrikeGame game, string name) => (T)typeof(TraceStrikeGame).GetField(name, Flags).GetValue(game);
        static void Set(TraceStrikeGame game, string name, object value) => typeof(TraceStrikeGame).GetField(name, Flags).SetValue(game, value);
        static void Call(TraceStrikeGame game, string name, params object[] args) => typeof(TraceStrikeGame).GetMethod(name, Flags).Invoke(game, args);

        [Test]
        public void SharedVinesUseOriginalPixelArtWithoutOuterPaddingAndKeepContactRules()
        {
            var tile = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Vines");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            Assert.IsNotNull(sprite);
            Assert.AreEqual(sprite, tile.sprite);
            Assert.AreEqual(64, sprite.texture.width); Assert.AreEqual(64, sprite.texture.height);
            Assert.AreEqual(new Rect(5, 9, 55, 46), sprite.rect);
            Assert.AreEqual(new Vector2(27.5f, 23), sprite.pivot);
            Assert.AreEqual(64, sprite.pixelsPerUnit);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.IsTrue(importer.alphaIsTransparency);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.AreEqual(1, tile.stunSeconds);
            Assert.AreEqual(0, tile.fireMoveCount);
            var boss = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            foreach (var growth in boss.phases.SelectMany(p => p.mechanics).OfType<RegionGrowthMechanic>())
                Assert.AreEqual(tile, growth.overgrownTile, "Growth and manually painted vines use the same appearance.");
            using var hash = System.Security.Cryptography.SHA256.Create();
            Assert.AreEqual("9B1FD93DA5F178E6172BD79F039BDEAF47F49E877D1E0C435B4F84C1E37A7580",
                System.BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(SpritePath))).Replace("-", ""));
        }

        [UnityTest]
        public IEnumerator PaintedAndDynamicVinesCoverOneTileAndRestoreFallbackWithoutEditingBoss()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required.");
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            if (Object.FindAnyObjectByType<AudioListener>() == null) game.gameObject.AddComponent<AudioListener>();
            var saved = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            string before = EditorJsonUtility.ToJson(saved);
            var copy = Object.Instantiate(saved);
            var vine = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Vines");
            // Exercise the sprite-less fallback independently of the shared fire's new campfire artwork.
            var fire = Object.Instantiate(Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Fire"));
            fire.sprite = fire.inactiveSprite = null; fire.requiresCompletedAttack = false;
            float oldScale = Time.timeScale;
            IPatternLease dynamicLayer = null;
            try
            {
                Time.timeScale = 0;
                game.PreviewPattern(saved, new EncounterPattern { minimumDuration = 999 });
                Set(game, "activeBoss", copy);
                var model = Get<TrailFieldModel>(game, "model");
                var floor = new HashSet<Vector2Int>(model.Traversable);
                Vector2Int[] offsets = (from y in Enumerable.Range(0, 4) from x in Enumerable.Range(0, 4)
                    select new Vector2Int(x, y)).ToArray();
                var corner = floor.OrderBy(c => (c - model.Player - Vector2Int.up * 3).sqrMagnitude)
                    .First(c => offsets.All(d => floor.Contains(c + d)));
                var cells = offsets.Select(d => corner + d).ToArray();
                copy.arena.specialTiles.Clear();
                foreach (var cell in cells.Take(8)) copy.arena.SetSpecialTile(cell, vine);
                Call(game, "StartSpecialTiles");
                dynamicLayer = game.PlaceSpecialTiles(vine, cells.Skip(8).ToArray());
                Assert.IsTrue(model.TryPlacePlayer(corner + Vector2Int.down));
                foreach (float zoom in new[] { .75f, 2f, 3f })
                {
                    copy.arena.cameraZoom = zoom;
                    Call(game, "ApplyEncounterArenaLayout");
                    Set(game, "battleCameraInitialized", false);
                    Call(game, "RefreshBoard");
                    Call(game, "AnimateSpecialItems");
                    Canvas.ForceUpdateCanvases();
                    foreach (var cell in cells) AssertFullTile(game, cell, vine.sprite);
                }
                var covered = cells[0];
                using (game.PlaceSpecialTiles(fire, new[] { covered }))
                {
                    var root = Get<RectTransform[,]>(game, "specialItemVisuals")[covered.x, covered.y];
                    float size = Get<float>(game, "mainCellSize");
                    Assert.AreEqual(Vector2.one * size * .55f, root.sizeDelta);
                    Assert.AreEqual(new Vector2(-.2f, -.2f) * size, root.anchoredPosition);
                    Assert.AreEqual(fire.color, Get<Image[,]>(game, "specialItemImages")[covered.x, covered.y].color);
                    Assert.IsFalse(Get<Image[,]>(game, "specialItemIconImages")[covered.x, covered.y].gameObject.activeSelf);
                    Assert.IsTrue(Get<Text[,]>(game, "specialItemLabels")[covered.x, covered.y].gameObject.activeSelf);
                }
                AssertFullTile(game, covered, vine.sprite);
                copy.arena.cameraZoom = 2f;
                Call(game, "ApplyEncounterArenaLayout");
                Set(game, "battleCameraInitialized", false); Call(game, "RefreshBoard");
                yield return WaitForPlayerFrames();
                Capture(game, "AdjacentVines");
                using (((IPatternHost)game).Mark(cells.Take(4).ToArray(), Color.red, true))
                {
                    yield return WaitForPlayerFrames();
                    Capture(game, "WarningOverVines");
                }
                dynamicLayer.Dispose(); dynamicLayer = null;
                Assert.IsFalse(Get<RectTransform[,]>(game, "specialItemVisuals")[cells[8].x, cells[8].y].gameObject.activeSelf);
                AssertFullTile(game, cells[0], vine.sprite);
                game.StopPatternPreview();
                Assert.AreEqual(before, EditorJsonUtility.ToJson(saved));
                Assert.IsNull(Get<SpecialTileField>(game, "placedSpecialTiles").At(cells[0]));
            }
            finally
            {
                dynamicLayer?.Dispose();
                Time.timeScale = oldScale;
                Object.Destroy(copy);
                Object.Destroy(fire);
            }
            yield return new ExitPlayMode();
        }

        static void AssertFullTile(TraceStrikeGame game, Vector2Int cell, Sprite sprite)
        {
            var icon = Get<Image[,]>(game, "specialItemIconImages")[cell.x, cell.y];
            var root = Get<RectTransform[,]>(game, "specialItemVisuals")[cell.x, cell.y];
            var face = Get<Image[,]>(game, "mainTiles")[cell.x, cell.y].rectTransform;
            Assert.IsTrue(root.gameObject.activeSelf); Assert.IsTrue(icon.gameObject.activeSelf);
            Assert.AreEqual(sprite, icon.sprite); Assert.AreEqual(Color.white, icon.color);
            Assert.IsFalse(icon.preserveAspect, "Tile artwork fills the face like the editor's tile painter.");
            Assert.AreEqual(Color.clear, Get<Image[,]>(game, "specialItemImages")[cell.x, cell.y].color);
            Assert.IsFalse(Get<Text[,]>(game, "specialItemLabels")[cell.x, cell.y].gameObject.activeSelf);
            Assert.AreEqual(Vector2.zero, root.anchoredPosition);
            Assert.AreEqual(Vector3.one, root.localScale, "Floor artwork must not inherit the pickup pulse.");
            var expected = new Vector3[4]; var actual = new Vector3[4];
            face.GetWorldCorners(expected); icon.rectTransform.GetWorldCorners(actual);
            for (int i = 0; i < 4; i++) Assert.Less(Vector3.Distance(expected[i], actual[i]), .02f, cell + " corner " + i);
        }

        static IEnumerator WaitForPlayerFrames()
        {
            int frame = Time.frameCount;
            float deadline = Time.realtimeSinceStartup + 10;
            while (Time.frameCount < frame + 2)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Player loop did not advance.");
                yield return null;
            }
        }

        static void Capture(TraceStrikeGame game, string name)
        {
            var camera = Get<Camera>(game, "uiCamera");
            var rt = new RenderTexture(1600, 900, 24);
            var read = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); rt.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); read.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/VineTileVisual");
                File.WriteAllBytes("Logs/PatternValidation/VineTileVisual/" + name + ".png", read.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; rt.Release(); Object.Destroy(rt); Object.Destroy(read); }
        }

        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
