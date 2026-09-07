#if UNITY_EDITOR
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Tests
{
    public sealed class PatternAssetTests
    {
        [Test]
        public void RecursivePatternCallsAreRejected()
        {
            var a = ScriptableObject.CreateInstance<PatternSequence>();
            var b = ScriptableObject.CreateInstance<PatternSequence>();
            try
            {
                a.clips.Add(new PatternClip { action = new CallPatternEvent { pattern = b }, duration = 2 });
                b.clips.Add(new PatternClip { action = new CallPatternEvent { pattern = a }, duration = 2 });
                Assert.IsNotEmpty(PatternValidation.Errors(a));
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void RecursiveEncounterPatternCallsAreRejected()
        {
            var encounter = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            try
            {
                EncounterPattern pattern = encounter.phases[0].patterns[0];
                pattern.id = "recursive";
                pattern.minimumDuration = 1f;
                pattern.clips.Add(new PatternClip
                {
                    duration = 1f,
                    action = new CallEncounterPatternEvent { patternId = pattern.id }
                });
                Assert.IsNotEmpty(encounter.ValidateDefinition());
            }
            finally { Object.DestroyImmediate(encounter); }
        }
        [Test]
        public void ShippedBossPatternsLoadWithAllManagedEventTypes()
        {
            var catalog = Resources.Load<BossCatalog>("Patterns/BossCatalog");
            Assert.IsNotNull(catalog);
            Assert.IsNotEmpty(catalog.bosses);
            foreach (var boss in catalog.bosses)
            {
                Assert.IsNotNull(boss);
                Assert.IsNotEmpty(boss.phases);
                Assert.IsEmpty(boss.ValidateDefinition(), boss.displayName);
                foreach (var pattern in boss.AllPatterns())
                {
                    Assert.IsNotEmpty(pattern.id);
                    Assert.IsEmpty(PatternValidation.Errors(pattern, boss.FindPattern), pattern.name);
                }
            }
        }

        [Test]
        public void InlineEncounterPatternsSurviveEditorJsonRoundTrip()
        {
            var source = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            var copy = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            try
            {
                var helper = new EncounterPattern { id = "helper", name = "Helper" };
                helper.clips.Add(new PatternClip { duration = 0.2f, action = new WarningEvent() });
                source.libraryPatterns.Add(helper);
                source.arena.shape = ArenaShape.Custom;
                source.arena.floorCells.Add(new Vector2Int(8, 8));
                source.arena.floorCells.Add(new Vector2Int(9, 8));
                source.arena.size = 50;
                source.arena.overridePlayerStart = true; source.arena.playerStart = new Vector2Int(8, 8);
                source.arena.restrictStartCells = true; source.arena.startCells.Add(new Vector2Int(8, 8));
                source.arena.restrictEndCells = true; source.arena.endCells.Add(new Vector2Int(9, 8));
                source.phases[0].patterns[0].clips.Add(new PatternClip
                {
                    duration = helper.Duration,
                    action = new CallEncounterPatternEvent { patternId = helper.id }
                });
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), copy);
                Assert.IsInstanceOf<CallEncounterPatternEvent>(copy.phases[0].patterns[0].clips[0].action);
                Assert.IsInstanceOf<WarningEvent>(copy.libraryPatterns[0].clips[0].action);
                Assert.AreEqual(ArenaShape.Custom, copy.arena.shape);
                CollectionAssert.AreEquivalent(source.arena.floorCells, copy.arena.floorCells);
                Assert.AreEqual(50, copy.arena.size);
                Assert.IsTrue(copy.arena.overridePlayerStart); Assert.AreEqual(source.arena.playerStart, copy.arena.playerStart);
                Assert.IsTrue(copy.arena.restrictStartCells); Assert.IsTrue(copy.arena.restrictEndCells);
                CollectionAssert.AreEqual(source.arena.startCells, copy.arena.startCells);
                CollectionAssert.AreEqual(source.arena.endCells, copy.arena.endCells);
                Assert.IsEmpty(copy.ValidateDefinition());
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(copy); }
        }
        [Test]
        public void ManagedReferencesSurviveEditorJsonRoundTrip()
        {
            var a = ScriptableObject.CreateInstance<PatternSequence>(); var b = ScriptableObject.CreateInstance<PatternSequence>();
            try
            {
                a.clips.Add(new PatternClip { start = 0.4f, duration = 2, action = new WarningEvent() });
                a.clips.Add(new PatternClip { start = 1, duration = 1, action = new DamageEvent() });
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(a), b);
                Assert.IsInstanceOf<WarningEvent>(b.clips[0].action); Assert.IsInstanceOf<DamageEvent>(b.clips[1].action);
                Assert.AreEqual(0.4f, b.clips[0].start);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void PreviewGridCellsSharePixelSnappedBordersWithoutGaps()
        {
            var board = new Rect(0.37f, 0.63f, 300f, 300f);
            for (int i = 0; i < TrailFieldModel.MaxSize - 1; i++)
            {
                Rect left = Editor.PatternPreviewGridGUI.CellRect(
                    board, i, 0, TrailFieldModel.MaxSize);
                Rect right = Editor.PatternPreviewGridGUI.CellRect(
                    board, i + 1, 0, TrailFieldModel.MaxSize);
                Rect upper = Editor.PatternPreviewGridGUI.CellRect(
                    board, 0, i + 1, TrailFieldModel.MaxSize);
                Rect lower = Editor.PatternPreviewGridGUI.CellRect(
                    board, 0, i, TrailFieldModel.MaxSize);
                Assert.AreEqual(left.xMax, right.xMin);
                Assert.AreEqual(upper.yMax, lower.yMin);
                Assert.Greater(left.width, 0f);
                Assert.Greater(lower.height, 0f);
            }
        }
    }
}
#endif
