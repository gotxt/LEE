#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NHN.TraceStrike.Tests
{
    public sealed class ArenaRuntimeTests
    {
        private GameObject testAudioListener;
        [SetUp]
        public void AllowSceneBootstrapAudioWarning()
        {
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                testAudioListener = new GameObject("Arena test audio listener");
                testAudioListener.AddComponent<AudioListener>();
            }
        }

        [TearDown]
        public void RestoreLogAssertions()
        {
            if (testAudioListener != null) Object.DestroyImmediate(testAudioListener);
        }

        private static T Field<T>(TraceStrikeGame game, string name) =>
            (T)typeof(TraceStrikeGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        private static void Call(TraceStrikeGame game, string name, params object[] args)
        {
            foreach (var method in typeof(TraceStrikeGame).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                var parameters = method.GetParameters();
                if (method.Name != name || parameters.Length < args.Length) continue;
                bool remainingOptional = true;
                for (int i = args.Length; i < parameters.Length; i++)
                    remainingOptional &= parameters[i].IsOptional;
                if (!remainingOptional) continue;
                {
                    var supplied = new object[parameters.Length];
                    for (int i = 0; i < args.Length; i++) supplied[i] = args[i];
                    for (int i = args.Length; i < parameters.Length; i++) supplied[i] = parameters[i].DefaultValue;
                    method.Invoke(game, supplied);
                    return;
                }
            }
            Assert.Fail("Missing method " + name + " with " + args.Length + " arguments.");
        }

        [UnityTest]
        public IEnumerator LargeArenaSpawnsRendersAndReturnsToLegacyArena()
        {
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            Assert.IsNotNull(game);
            var catalog = Resources.Load<BossCatalog>("Patterns/BossCatalog_Main");
            var boss = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            boss.id = "runtime-arena-test";
            boss.arena.size = 50; boss.arena.shape = ArenaShape.Custom;
            boss.arena.overridePlayerStart = true; boss.arena.playerStart = new Vector2Int(24, 24);
            boss.arena.restrictStartCells = true; boss.arena.startCells.Add(new Vector2Int(0, 0));
            boss.arena.restrictEndCells = true; boss.arena.endCells.Add(new Vector2Int(49, 49));
            for (int y = 0; y < 50; y++) for (int x = 0; x < 50; x++) boss.arena.floorCells.Add(new Vector2Int(x, y));
            boss.phases[0].patterns[0].minimumDuration = 20;
            boss.phases[0].initialDelay = 100;
            int index = catalog.bosses.Count; catalog.bosses.Add(boss);
            try
            {
                Call(game, "StartStage", index);
                var model = Field<TrailFieldModel>(game, "model");
                var tiles = Field<Image[,]>(game, "mainTiles");
                var minimap = Field<Image[,]>(game, "minimapTiles");
                Assert.AreEqual(2500, model.Walkable.Count);
                Assert.AreEqual(boss.arena.playerStart, model.Player);
                Assert.AreEqual(Vector2Int.zero, model.Start);
                Assert.AreEqual(new Vector2Int(49, 49), model.End);
                Assert.IsTrue(tiles[49, 49].gameObject.activeSelf);
                Assert.IsTrue(minimap[49, 49].gameObject.activeSelf);
                Assert.Less(tiles[0, 0].rectTransform.anchoredPosition.x, 0);
                Assert.Greater(tiles[49, 49].rectTransform.anchoredPosition.x, 0);
                Assert.AreEqual(-tiles[0, 0].rectTransform.anchoredPosition, tiles[49, 49].rectTransform.anchoredPosition);
                Assert.AreEqual(new Vector2Int(25, 25), ((IPatternHost)game).CenterCell);
                yield return null;

                boss.arena.size = 18; boss.arena.playerStart = new Vector2Int(8, 8);
                boss.arena.endCells[0] = new Vector2Int(17, 17);
                Call(game, "StartStage", index);
                Assert.AreEqual(324, model.Walkable.Count);
                Assert.IsFalse(tiles[49, 49].gameObject.activeSelf);
                Assert.IsFalse(minimap[49, 49].gameObject.activeSelf);
                Assert.IsTrue(tiles[17, 17].gameObject.activeSelf);
                yield return null;

                // Use a test-owned legacy arena; the real boss asset is editable.
                boss.arena = new BossArenaDefinition();
                Call(game, "StartStage", index);
                Assert.AreEqual(17, model.GridSize);
                Assert.IsFalse(tiles[17, 17].gameObject.activeSelf);
                Call(game, "StartStage", 0);
                Assert.AreEqual(catalog.bosses[0].arena.GridSize, model.GridSize);
                Call(game, "StartHub");
                Assert.IsFalse(tiles[49, 49].gameObject.activeSelf);
                Assert.IsTrue(tiles[16, 16].gameObject.activeSelf);
                yield return null;
            }
            finally { catalog.bosses.Remove(boss); Object.Destroy(boss); }
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TileImagesApplyPerBossAndResetInHubAndTutorial()
        {
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            var catalog = Resources.Load<BossCatalog>("Patterns/BossCatalog_Main");
            var boss = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            var texture = new Texture2D(8, 4);
            var first = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            var second = Sprite.Create(texture, new Rect(4, 0, 4, 4), Vector2.one * 0.5f);
            boss.id = "tile-image-test";
            boss.arena.size = 50; boss.arena.shape = ArenaShape.Custom;
            for (int y = 0; y < 50; y++) for (int x = 0; x < 50; x++)
                boss.arena.floorCells.Add(new Vector2Int(x, y));
            boss.arena.floorCells.Remove(new Vector2Int(3, 1));
            boss.arena.defaultTileSprite = first;
            boss.arena.SetTileSprite(new Vector2Int(1, 1), second);
            boss.arena.SetTileSprite(new Vector2Int(3, 1), second);
            boss.phases[0].initialDelay = 100;
            int index = catalog.bosses.Count; catalog.bosses.Add(boss);
            try
            {
                Call(game, "StartStage", index);
                var tiles = Field<Image[,]>(game, "mainTiles");
                Assert.AreSame(second, tiles[1, 1].sprite);
                Assert.AreSame(first, tiles[2, 1].sprite);
                Assert.AreSame(first, tiles[49, 49].sprite);
                Assert.AreEqual(Color.white, tiles[2, 1].color);
                Assert.IsFalse(tiles[3, 1].gameObject.activeSelf, "Images must not create walkable tiles");
                yield return null;

                Call(game, "StartStage", 0);
                Assert.AreNotSame(first, tiles[2, 1].sprite);
                Assert.AreNotSame(second, tiles[1, 1].sprite);
                Call(game, "StartStage", index);
                Call(game, "StartHub");
                Assert.AreNotSame(first, tiles[2, 1].sprite);
                Assert.AreNotSame(second, tiles[1, 1].sprite);
                Call(game, "StartStage", index);
                Call(game, "StartTutorial");
                Assert.AreNotSame(first, tiles[2, 1].sprite);
                Assert.AreNotSame(second, tiles[1, 1].sprite);
                Call(game, "StartStage", index);
                Assert.AreSame(second, tiles[1, 1].sprite);
                boss.arena.SetTileSprite(new Vector2Int(1, 1), null);
                Call(game, "StartStage", index);
                Assert.AreSame(first, tiles[1, 1].sprite);
                yield return null;
            }
            finally
            {
                catalog.bosses.Remove(boss);
                Call(game, "StartStage", 0);
                Object.Destroy(boss); Object.Destroy(first); Object.Destroy(second); Object.Destroy(texture);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PrefabBossRunsPatternOnEmptyCentreAndCleansUpOnRestart()
        {
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            var catalog = Resources.Load<BossCatalog>("Patterns/BossCatalog_Main");
            var boss = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            var source = new GameObject("Runtime boss source");
            var actor = source.AddComponent<BossActor>();
            var texture = new Texture2D(4, 4);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) texture.SetPixel(x, y, Color.white);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 4);
            source.AddComponent<SpriteRenderer>().sprite = sprite;
            boss.bossVisual.prefab = actor;
            boss.arena.MakeCustom(); boss.arena.floorCells.Remove(new Vector2Int(8, 8));
            boss.arena.overridePlayerStart = true; boss.arena.playerStart = new Vector2Int(7, 7);
            var pattern = boss.phases[0].patterns[0]; pattern.minimumDuration = 2;
            pattern.clips.Add(new PatternClip { duration = 1, action = new BossMotionEvent { translation = Vector2.up, returnTime = 0.5f } });
            catalog.bosses.Add(boss);
            try
            {
                game.PreviewPattern(boss, pattern);
                var stage = Field<BossRenderStage>(game, "bossRenderStage");
                Assert.IsNotNull(stage);
                Assert.IsTrue(Field<RawImage>(game, "bossVisualImage").gameObject.activeSelf);
                Assert.IsFalse(Field<Image[,]>(game, "mainTiles")[8, 8].gameObject.activeSelf);
                Assert.AreEqual(new Vector2Int(7, 7), Field<TrailFieldModel>(game, "model").Player);
                Field<PatternRunner>(game, "timeline").Advance(0.5f);
                Assert.AreEqual(new Vector3(8, 9), stage.Presentation.MotionRoot.localPosition);
                Call(game, "CancelTimeline");
                Assert.AreEqual(new Vector3(8, 8), stage.Presentation.MotionRoot.localPosition);
                Call(game, "StartStage", 0);
                yield return null;
                Assert.IsTrue(stage.Presentation.Actor == null);
            }
            finally
            {
                catalog.bosses.Remove(boss); Object.Destroy(boss); Object.Destroy(source);
                Object.Destroy(sprite); Object.Destroy(texture);
            }
            yield return new ExitPlayMode();
        }
    }
}
#endif
