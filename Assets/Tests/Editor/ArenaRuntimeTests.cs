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
        private static T Field<T>(TraceStrikeGame game, string name) =>
            (T)typeof(TraceStrikeGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        private static void Call(TraceStrikeGame game, string name, params object[] args) =>
            typeof(TraceStrikeGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);

        [UnityTest]
        public IEnumerator LargeArenaSpawnsRendersAndReturnsToLegacyArena()
        {
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            Assert.IsNotNull(game);
            var catalog = Resources.Load<BossCatalog>("Patterns/BossCatalog");
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

                Call(game, "StartStage", 0);
                Assert.AreEqual(17, model.GridSize);
                Assert.IsFalse(tiles[17, 17].gameObject.activeSelf);
                Call(game, "StartHub");
                Assert.IsFalse(tiles[49, 49].gameObject.activeSelf);
                Assert.IsTrue(tiles[16, 16].gameObject.activeSelf);
                yield return null;
                LogAssert.NoUnexpectedReceived();
            }
            finally { catalog.bosses.Remove(boss); Object.Destroy(boss); }
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
#endif
