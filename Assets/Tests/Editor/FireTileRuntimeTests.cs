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
using Object = UnityEngine.Object;

namespace NHN.TraceStrike.Tests
{
    public sealed class FireTileRuntimeTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Get<T>(TraceStrikeGame game, string name) => (T)typeof(TraceStrikeGame).GetField(name, Flags).GetValue(game);
        static void Set(TraceStrikeGame game, string name, object value) => typeof(TraceStrikeGame).GetField(name, Flags).SetValue(game, value);
        static object Call(TraceStrikeGame game, string name, params object[] args) => typeof(TraceStrikeGame).GetMethod(name, Flags).Invoke(game, args);
        static void ClearPickups(TraceStrikeGame game) => Get<IDictionary>(game, "specialTiles").Clear();

        [UnityTest]
        public IEnumerator CompletedPathIgnitesStartMiddleEndThenContactBurnsSeedsAndExpiryChangesOnlyOverlay()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required.");
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            if (Object.FindAnyObjectByType<AudioListener>() == null) game.gameObject.AddComponent<AudioListener>();
            var saved = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            string before = EditorJsonUtility.ToJson(saved);
            var copy = Object.Instantiate(saved);
            var fire = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Fire");
            var vine = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Vines");
            var start = new Vector2Int(15, 2); var middle = new Vector2Int(15, 3);
            var end = new Vector2Int(15, 4); var outside = new Vector2Int(16, 2);
            var hit = new[] { start, middle, end };
            float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                game.PreviewPattern(saved, new EncounterPattern { minimumDuration = 999 });
                Set(game, "activeBoss", copy);
                copy.arena.specialTiles.Clear();
                foreach (var cell in hit.Concat(new[] { outside })) copy.arena.SetSpecialTile(cell, fire);
                Call(game, "StartSpecialTiles");
                var field = Get<SpecialTileField>(game, "placedSpecialTiles");
                var model = Get<TrailFieldModel>(game, "model");
                model.SetEndpointRegions(new[] { start }, new[] { end });
                model.BeginRound(0, true, start + Vector2Int.down); ClearPickups(game);
                Call(game, "RefreshBoard");
                var floorImage = Get<Image[,]>(game, "mainTiles")[outside.x, outside.y];
                var floorSprite = floorImage.sprite; var floorColor = floorImage.color;
                foreach (float zoom in new[] { .75f, 2f, 3f })
                {
                    copy.arena.cameraZoom = zoom; Call(game, "ApplyEncounterArenaLayout");
                    Call(game, "RefreshBoard"); Call(game, "AnimateSpecialItems"); Canvas.ForceUpdateCanvases();
                    foreach (var cell in hit) AssertOverlay(game, cell, fire.inactiveSprite);
                }
                copy.arena.cameraZoom = 2f; Call(game, "ApplyEncounterArenaLayout");
                Set(game, "battleCameraInitialized", false); Call(game, "RefreshBoard");
                yield return Frames(); Capture(game, "Inactive");
                Call(game, "Move", Vector2Int.up); Call(game, "Move", Vector2Int.up);
                Assert.AreEqual(0, game.FireMovesRemaining); Assert.IsFalse(field.IsActive(start));
                // Walking part of a trail and cancelling it must not ignite anything.
                Assert.IsTrue(model.TryPlacePlayer(start + Vector2Int.down));
                Assert.IsFalse(field.IsActive(start)); game.StopAllCoroutines();
                foreach (var unused in hit) Call(game, "Move", Vector2Int.up);
                Assert.IsTrue(Get<bool>(game, "inputLocked")); Assert.AreEqual(end, model.Player);
                Assert.IsFalse(field.IsActive(start), "Wait until the attack reaches its success/damage point.");
                Assert.AreEqual(0, game.FireMovesRemaining, "END contact happens before ignition; no standing refill.");
                int health = Get<int>(game, "bossHealth");
                DriveAttack(game); // Real coroutine logic; waits are advanced by the test, not by wall clock.
                Assert.Less(Get<int>(game, "bossHealth"), health);
                Assert.IsEmpty(model.Trail); Assert.IsFalse(Get<bool>(game, "inputLocked"));
                foreach (var cell in hit) { Assert.IsTrue(field.IsActive(cell)); Assert.AreEqual(30, field.ActiveSecondsRemaining(cell)); }
                Assert.IsFalse(field.IsActive(outside)); Assert.AreEqual(0, game.FireMovesRemaining);
                ClearPickups(game); Call(game, "RefreshBoard");
                yield return Frames();
                foreach (var cell in hit) AssertOverlay(game, cell, fire.sprite);
                AssertOverlay(game, outside, fire.inactiveSprite);
                Assert.AreEqual(floorSprite, floorImage.sprite); Assert.AreEqual(floorColor, floorImage.color);
                Capture(game, "Active");
                using (((IPatternHost)game).Mark(new[] { middle }, Color.red, true))
                { yield return Frames(); Capture(game, "WarningOverFire"); }

                Set(game, "inputLocked", true); Call(game, "TickSpecialTiles", 15f);
                Assert.AreEqual(30, field.ActiveSecondsRemaining(start));
                Set(game, "inputLocked", false); Call(game, "TickSpecialTiles", 29.5f);
                Assert.AreEqual(.5f, field.ActiveSecondsRemaining(start));
                Call(game, "Move", Vector2Int.down); Assert.AreEqual(20, game.FireMovesRemaining);
                Call(game, "TickSpecialTiles", .5f);
                Assert.IsFalse(field.IsActive(middle)); Assert.AreEqual(20, game.FireMovesRemaining);
                AssertOverlay(game, middle, fire.inactiveSprite);
                Call(game, "Move", Vector2Int.up); Assert.AreEqual(19, game.FireMovesRemaining);

                // Two actual growth seeds, removed through actual Move -> OnPlayerStep after charging.
                var seeds = new[] { new Vector2Int(16, 4), new Vector2Int(17, 4) };
                Assert.IsTrue(seeds.All(c => model.IsTraversable(c)));
                copy.tileRegions.Add(new EncounterTileRegion { id = "fire-test-seeds", name = "Test", cells = seeds.ToList() });
                var growth = new RegionGrowthMechanic { regionIds = new List<string> { "fire-test-seeds" },
                    initialDelay = 0, interval = 100, seedsPerSpawn = 2, overgrownTile = vine };
                using (var session = new BossMechanicSession(new[] { growth }, new MechanicContext(game, copy.FindPattern)))
                {
                    Set(game, "mechanicSession", session); session.Advance(0);
                    var runtime = session.Runtimes.OfType<RegionGrowthRuntime>().Single(); Assert.AreEqual(2, runtime.Seeds.Count);
                    Call(game, "Move", Vector2Int.right); Assert.AreEqual(1, runtime.Seeds.Count);
                    Call(game, "Move", Vector2Int.right); Assert.AreEqual(0, runtime.Seeds.Count);
                    Assert.AreEqual(17, game.FireMovesRemaining);
                }
                Set(game, "mechanicSession", null);

                // A valid path in a damage-immune phase still ignites; no boss-name/type branch in the hook.
                var immune = new BossMechanicSession(new[] { new EnrageSurvivalMechanic() }, new MechanicContext(game, copy.FindPattern));
                Set(game, "mechanicSession", immune); Set(game, "bossHealth", immune.EnterEnrage(100));
                model.BeginRound(1, true, start + Vector2Int.down); ClearPickups(game);
                foreach (var unused in hit) Call(game, "Move", Vector2Int.up);
                DriveAttack(game); Assert.AreEqual(160, Get<int>(game, "bossHealth"));
                Assert.AreEqual(30, field.ActiveSecondsRemaining(start));
                using (game.PlaceSpecialTiles(vine, new[] { start }))
                {
                    AssertOverlay(game, start, vine.sprite);
                    Assert.IsFalse(field.OnCompletedAttack(new[] { start }));
                    Call(game, "TickSpecialTiles", 30f);
                }
                AssertOverlay(game, start, fire.inactiveSprite); Assert.IsFalse(field.IsActive(start));

                // Death disposes both active timers and foot charge; a restarted placement is inactive again.
                field.OnCompletedAttack(hit);
                var death = (IEnumerator)Call(game, "KillPlayer", "Fire test"); Assert.IsTrue(death.MoveNext());
                Assert.IsNull(Get<SpecialTileField>(game, "placedSpecialTiles")); Assert.AreEqual(0, game.FireMovesRemaining);
                Assert.IsNull(field.At(start));
                game.PreviewPattern(saved, new EncounterPattern { minimumDuration = 999 });
                Set(game, "activeBoss", copy); Call(game, "StartSpecialTiles");
                Assert.IsFalse(Get<SpecialTileField>(game, "placedSpecialTiles").IsActive(start));
                game.enabled = false;
                Assert.IsNull(Get<SpecialTileField>(game, "placedSpecialTiles"));
                Assert.AreEqual(before, EditorJsonUtility.ToJson(saved));
            }
            finally { game.StopAllCoroutines(); Time.timeScale = oldScale; Object.Destroy(copy); }
            yield return new ExitPlayMode();
        }

        static void DriveAttack(TraceStrikeGame game)
        {
            game.StopAllCoroutines();
            var attack = (IEnumerator)Call(game, "ExecuteAttack"); int yields = 0;
            while (attack.MoveNext()) Assert.Less(++yields, 100, "Unexpected attack branch or non-terminating attack.");
            game.StopAllCoroutines();
        }
        static void AssertOverlay(TraceStrikeGame game, Vector2Int cell, Sprite expected)
        {
            var root = Get<RectTransform[,]>(game, "specialItemVisuals")[cell.x, cell.y];
            var icon = Get<Image[,]>(game, "specialItemIconImages")[cell.x, cell.y];
            var face = Get<Image[,]>(game, "mainTiles")[cell.x, cell.y].rectTransform;
            Assert.IsTrue(root.gameObject.activeSelf); Assert.IsTrue(icon.gameObject.activeSelf);
            Assert.AreEqual(expected, icon.sprite); Assert.AreEqual(Color.white, icon.color);
            Assert.AreEqual(Color.clear, Get<Image[,]>(game, "specialItemImages")[cell.x, cell.y].color);
            Assert.AreEqual(Vector3.one, root.localScale); Assert.AreEqual(Vector2.zero, root.anchoredPosition);
            Assert.IsFalse(Get<Text[,]>(game, "specialItemLabels")[cell.x, cell.y].gameObject.activeSelf);
            var a = new Vector3[4]; var b = new Vector3[4]; face.GetWorldCorners(a); icon.rectTransform.GetWorldCorners(b);
            for (int i = 0; i < 4; i++) Assert.Less(Vector3.Distance(a[i], b[i]), .02f);
        }
        static IEnumerator Frames()
        {
            int frame = Time.frameCount; float deadline = Time.realtimeSinceStartup + 10;
            while (Time.frameCount < frame + 2) { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; }
        }
        static void Capture(TraceStrikeGame game, string name)
        {
            var camera = Get<Camera>(game, "uiCamera"); var rt = new RenderTexture(1600, 900, 24);
            var read = new Texture2D(1600, 900, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); rt.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); read.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/FireTiles");
                File.WriteAllBytes("Logs/PatternValidation/FireTiles/" + name + ".png", read.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; rt.Release(); Object.Destroy(rt); Object.Destroy(read); }
        }
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
