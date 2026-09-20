#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NHN.TraceStrike.Editor;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NHN.TraceStrike.Tests
{
    public sealed class ArenaCenterHighlightTests
    {
        [Test]
        public void EverySupportedSizeHighlightsExactlyTheGeometricMiddle()
        {
            for (int size = 5; size <= TrailFieldModel.MaxSize; size++)
            {
                var arena = new BossArenaDefinition { size = size };
                RectInt center = PatternPreviewGridGUI.ArenaCenterCells(arena);
                int count = 0, minX = int.MaxValue, maxX = int.MinValue;
                for (int y = 0; y < arena.GridSize; y++)
                for (int x = 0; x < arena.GridSize; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (arena.ContainsBounds(cell))
                    { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
                    if (!center.Contains(cell)) continue;
                    Assert.IsTrue(arena.ContainsBounds(cell), "size=" + size);
                    count++;
                }
                Assert.AreEqual(size % 2 == 0 ? 4 : 1, count, "size=" + size);
                // Compare centres in tile-centre coordinates (RectInt's max is exclusive).
                float geometricMiddle = (minX + maxX) * 0.5f;
                Assert.AreEqual(geometricMiddle, (center.xMin + center.xMax - 1) * 0.5f);
                Assert.AreEqual(geometricMiddle, (center.yMin + center.yMax - 1) * 0.5f);
            }
        }

        [TestCase(5, 8, 1)]
        [TestCase(6, 7, 2)]
        [TestCase(16, 7, 2)]
        [TestCase(17, 8, 1)]
        [TestCase(18, 8, 2)]
        [TestCase(39, 19, 1)]
        [TestCase(50, 24, 2)]
        public void OddEvenAndLegacyOffsetExamplesMatch(int size, int first, int width)
        {
            var arena = new BossArenaDefinition { size = size };
            Vector2Int runtimeAnchor = arena.CenterCell;
            Assert.AreEqual(new RectInt(first, first, width, width), PatternPreviewGridGUI.ArenaCenterCells(arena));
            Assert.AreEqual(runtimeAnchor, arena.CenterCell);
        }

        [Test]
        public void EmptyAndAsymmetricFloorsDoNotMoveTheHighlightOrModifyArenaData()
        {
            var arena = new BossArenaDefinition { size = 50, shape = ArenaShape.Custom,
                overridePlayerStart = true, playerStart = Vector2Int.zero, restrictStartCells = true };
            arena.startCells.Add(Vector2Int.zero);
            arena.floorCells.Add(Vector2Int.zero);
            arena.floorCells.Add(Vector2Int.right);
            string before = JsonUtility.ToJson(arena);
            Assert.AreEqual(new RectInt(24, 24, 2, 2), PatternPreviewGridGUI.ArenaCenterCells(arena));
            Assert.AreEqual(before, JsonUtility.ToJson(arena));
            arena.floorCells.Clear();
            Assert.AreEqual(new RectInt(24, 24, 2, 2), PatternPreviewGridGUI.ArenaCenterCells(arena));
            arena.size = 39;
            Assert.AreEqual(new RectInt(19, 19, 1, 1), PatternPreviewGridGUI.ArenaCenterCells(arena));
        }

        [UnityTest]
        public IEnumerator ArenaViewRendersWithHighlightOnAndOffWithoutChangingBossData()
        {
            var boss = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            boss.arena.shape = ArenaShape.Custom;
            boss.arena.floorCells.Add(Vector2Int.zero);
            boss.arena.floorCells.Add(Vector2Int.right);
            var window = ScriptableObject.CreateInstance<BossEncounterEditorWindow>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(BossEncounterEditorWindow);
            try
            {
                type.GetMethod("SelectEncounter", flags).Invoke(window, new object[] { boss });
                type.GetField("node", flags).SetValue(window,
                    System.Enum.Parse(type.GetNestedType("NodeKind", BindingFlags.NonPublic), "Arena"));
                window.Show(); window.position = new Rect(20, 20, 1200, 900);
                foreach (int size in new[] { 17, 16, 50, 39 })
                {
                    boss.arena.size = size;
                    type.GetField("serialized", flags).SetValue(window, new SerializedObject(boss));
                    string before = EditorJsonUtility.ToJson(boss);
                    int dirtyCount = EditorUtility.GetDirtyCount(boss);
                    foreach (bool visible in new[] { true, false })
                    {
                        type.GetField("showArenaCenter", flags).SetValue(window, visible);
                        window.Repaint(); yield return null; yield return null;
                        Assert.AreEqual(before, EditorJsonUtility.ToJson(boss));
                        Assert.AreEqual(dirtyCount, EditorUtility.GetDirtyCount(boss));
                    }
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); Object.DestroyImmediate(boss); }
        }
    }
}
#endif
