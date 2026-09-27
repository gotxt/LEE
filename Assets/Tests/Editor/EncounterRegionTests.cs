#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NHN.TraceStrike.Editor;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NHN.TraceStrike.Tests
{
    public sealed class EncounterRegionTests
    {
        const string PatternId = "p1-petal-collapse";
        const string Key = "petal_collapse_01";
        static BossEncounterDefinition Saved => Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
        static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        static EncounterPattern CopyPattern() => JsonUtility.FromJson<EncounterPattern>(JsonUtility.ToJson(Saved.FindPattern(PatternId)));
        static PatternContext Context(PatternPreviewHost host) => new PatternContext(host, host.CenterCell, resolveEncounterPattern: Saved.FindPattern);

        [Test]
        public void SavedAssetHasSixIndependentEditableRegionsAndPreservedOriginalPattern()
        {
            var boss = Saved;
            Assert.IsNotNull(boss); Assert.IsEmpty(boss.ValidateDefinition());
            Assert.AreEqual(32, boss.arena.size); Assert.AreEqual(762, boss.arena.GetCells().Count);
            Assert.AreEqual(new Vector2(15, 14), boss.bossVisual.position);
            Assert.AreEqual(new Vector2Int(15, 1), boss.arena.playerStart);
            CollectionAssert.AreEqual(new[] { new Vector2Int(13, 10) }, boss.arena.startCells);
            CollectionAssert.AreEqual(new[] { new Vector2Int(18, 10) }, boss.arena.endCells);
            Assert.AreEqual("2e4a09d96e2a457498b3f9423272e343", boss.phases[0].patterns[0].id);
            Assert.IsEmpty(boss.phases[0].patterns[0].clips);
            CollectionAssert.AreEqual(new[] { 112, 114, 118, 106, 118, 114 }, boss.tileRegions.Select(r => r.cells.Count));
            Assert.AreEqual(682, boss.tileRegions.SelectMany(r => r.cells).Distinct().Count());
            Assert.IsFalse(boss.tileRegions.SelectMany(r => r.cells).Any(boss.bossVisual.OccupiedCells().Contains));
            var step = AttackStepEditing.Find(boss.FindPattern(PatternId)).Single();
            Assert.AreEqual(3, step.Members.Count); Assert.AreEqual(5, step.Warning.duration);
            Assert.AreEqual(6, boss.FindPattern(PatternId).Duration);
            foreach (var member in step.Members)
            { Assert.AreEqual(Key, AttackStepEditing.Tiles(member).snapshotKey); Assert.AreEqual(JsonUtility.ToJson(step.Tiles), JsonUtility.ToJson(AttackStepEditing.Tiles(member))); }
        }

        [Test]
        public void CaptureOccursAtWarningNotConstructionAndDamageVfxRemainFrozen()
        {
            var pattern = CopyPattern(); foreach (var clip in pattern.clips) clip.start += 1; pattern.minimumDuration = 7;
            using (var host = new PatternPreviewHost(Saved) { player = new Vector2Int(15, 31) })
            using (var context = Context(host))
            using (var runner = new PatternRunner(pattern, context))
            {
                runner.Advance(.5f); Assert.IsEmpty(context.Selections);
                host.player = new Vector2Int(15, 0); runner.Advance(.5f);
                var expected = Saved.tileRegions.Single(r => r.id == "petal-bottom").cells;
                CollectionAssert.AreEquivalent(expected, context.Selections[Key]);
                host.player = new Vector2Int(15, 31); runner.Advance(5.01f);
                CollectionAssert.AreEquivalent(expected, host.GetOrderedMarks().Single(m => m.Layer == PatternPreviewHost.PreviewLayer.Damage).Cells);
                CollectionAssert.AreEquivalent(expected, host.GetOrderedMarks().Where(m => m.Layer == PatternPreviewHost.PreviewLayer.Effect).SelectMany(m => m.Cells));
                runner.Advance(.2f); Assert.IsFalse(host.log.Any(l => l.StartsWith("Hit:")));
                runner.Advance(1); Assert.IsEmpty(host.marks); Assert.IsEmpty(context.Selections); Assert.IsEmpty(context.SelectedRegionIds);
            }
        }

        [Test]
        public void NextRunAndChildHaveFreshSnapshotsButSameRegionDefinitions()
        {
            using (var host = new PatternPreviewHost(Saved) { player = new Vector2Int(15, 0) })
            using (var parent = Context(host))
            {
                var tiles = ((WarningEvent)Saved.FindPattern(PatternId).clips[0].action).tiles;
                tiles.Capture(parent); host.player = new Vector2Int(15, 31);
                using (var child = parent.CreateChild(Vector2Int.zero))
                { tiles.Capture(child); Assert.AreEqual("petal-top", child.SelectedRegionIds[Key]); }
                Assert.AreEqual("petal-bottom", parent.SelectedRegionIds[Key]);
                using (var next = Context(host))
                { tiles.Capture(next); Assert.AreEqual("petal-top", next.SelectedRegionIds[Key]); }
                var copy = tiles.Resolve(parent); copy.Clear(); Assert.AreEqual(106, tiles.Resolve(parent).Count);
            }
        }

        [Test]
        public void CentralOrUnassignedPositionCachesEmptyEvenAfterEnteringPetal()
        {
            using (var host = new PatternPreviewHost(Saved) { player = new Vector2Int(15, 11) })
            using (var context = Context(host))
            using (var runner = new PatternRunner(CopyPattern(), context))
            {
                runner.Advance(0); Assert.IsEmpty(context.Selections[Key]); Assert.AreEqual("", context.SelectedRegionIds[Key]);
                host.player = new Vector2Int(15, 0); runner.Advance(5.2f);
                Assert.IsFalse(host.log.Any(l => l.StartsWith("Hit:") || l.StartsWith("Spawn ")));
                Assert.IsTrue(host.GetOrderedMarks().All(m => m.Cells.Count == 0));
            }
        }

        [Test]
        public void OverlapUsesEncounterPriorityAmongCandidatesAndRenameDoesNotBreakId()
        {
            var boss = Object.Instantiate(Saved);
            try
            {
                boss.tileRegions[0].cells.Add(new Vector2Int(15, 0)); boss.tileRegions[0].name = "이름 변경";
                using (var host = new PatternPreviewHost(boss) { player = new Vector2Int(15, 0) })
                {
                    var tiles = ((WarningEvent)CopyPattern().clips[0].action).tiles;
                    tiles.regionIds.Reverse();
                    using (var first = Context(host)) { tiles.Capture(first); Assert.AreEqual("petal-top", first.SelectedRegionIds[Key]); }
                    tiles.regionIds.Remove("petal-top");
                    using (var second = Context(host)) { tiles.Capture(second); Assert.AreEqual("petal-bottom", second.SelectedRegionIds[Key]); }
                }
            }
            finally { Object.DestroyImmediate(boss); }
        }

        [TestCase("early-vfx")]
        [TestCase("same-time-before-warning")]
        [TestCase("missing-warning")]
        [TestCase("duplicate-warning")]
        [TestCase("different-candidates")]
        [TestCase("empty-key")]
        [TestCase("ensure-escape")]
        public void InvalidRegionAttackCannotSilentlyResolveAnotherArea(string fault)
        {
            var pattern = CopyPattern();
            switch (fault)
            {
                case "early-vfx": pattern.clips[0].start = 1; pattern.clips[2].start = 0; break;
                case "same-time-before-warning": var vfx = pattern.clips[2]; vfx.start = 0; pattern.clips.RemoveAt(2); pattern.clips.Insert(0, vfx); break;
                case "missing-warning": pattern.clips[0].enabled = false; break;
                case "duplicate-warning": pattern.clips.Add(AttackStepEditing.CopyClip(pattern.clips[0])); break;
                case "different-candidates": ((DamageEvent)pattern.clips[1].action).tiles.regionIds.RemoveAt(0); break;
                case "empty-key": ((WarningEvent)pattern.clips[0].action).tiles.snapshotKey = ""; break;
                case "ensure-escape": ((WarningEvent)pattern.clips[0].action).tiles.ensureEscape = true; break;
            }
            Assert.IsNotEmpty(PatternValidation.Errors(pattern));
        }

        [Test]
        public void UncapturedConsumerThrowsAndMissingDefinitionIsReported()
        {
            var pattern = CopyPattern();
            using (var host = new PatternPreviewHost(Saved))
            using (var context = Context(host))
                Assert.Throws<InvalidOperationException>(() => ((DamageEvent)pattern.clips[1].action).tiles.Resolve(context));
            var boss = Object.Instantiate(Saved);
            try { boss.tileRegions.RemoveAt(0); Assert.IsTrue(boss.ValidateDefinition().Any(e => e.Contains("참조"))); }
            finally { Object.DestroyImmediate(boss); }
        }

        [Test]
        public void SimpleEditorTimingCopyAndUndoPreserveNamedAreaLinks()
        {
            var boss = Object.Instantiate(Saved);
            try
            {
                var pattern = boss.FindPattern(PatternId); var step = AttackStepEditing.Find(pattern).Single();
                step.Tiles.regionIds.RemoveAt(0); AttackStepEditing.SynchronizeArea(step);
                AttackStepEditing.SetTiming(step, .5f, 4, .4f);
                foreach (var member in step.Members) CollectionAssert.AreEqual(step.Tiles.regionIds, AttackStepEditing.Tiles(member).regionIds);
                var copy = AttackStepEditing.Duplicate(pattern, step, 6);
                Assert.AreNotEqual(step.Key, copy.Key); Assert.AreEqual(10, copy.Damage.start);
                copy.Tiles.regionIds.Clear(); Assert.AreEqual(5, step.Tiles.regionIds.Count);
                string original = JsonUtility.ToJson(boss.tileRegions[0]);
                Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(boss, "Region test stroke");
                TilePaintStroke.RasterLine(new Vector2Int(13, 28), new Vector2Int(18, 28), c => boss.tileRegions[0].cells.Remove(c));
                Undo.FlushUndoRecordObjects(); Assert.AreNotEqual(original, JsonUtility.ToJson(boss.tileRegions[0]));
                Undo.PerformUndo(); Assert.AreEqual(original, JsonUtility.ToJson(boss.tileRegions[0]));
                var roundtrip = JsonUtility.FromJson<EncounterTileRegion>(JsonUtility.ToJson(boss.tileRegions[0]));
                CollectionAssert.AreEqual(boss.tileRegions[0].cells, roundtrip.cells);
            }
            finally { Undo.ClearUndo(boss); Object.DestroyImmediate(boss); }
        }

        [Test]
        public void EveryAttackedCellCanEscapeThroughRealCorridorsBeforeImpact()
        {
            var boss = Saved; var pattern = boss.FindPattern(PatternId); int tested = 0;
            int[] expected = { 11, 10, 10, 11, 10, 10 };
            using (var layout = new PatternPreviewHost(boss))
            {
                Assert.AreEqual(746, layout.Traversable.Count);
                for (int i = 0; i < boss.tileRegions.Count; i++)
                {
                    var region = boss.tileRegions[i];
                    var distances = EncounterRegionRules.EscapeDistances(layout.Traversable, region.cells);
                    Assert.IsTrue(region.cells.All(distances.ContainsKey));
                    Assert.AreEqual(expected[i], region.cells.Max(c => distances[c]));
                    foreach (var start in region.cells)
                    {
                        var model = new TrailFieldModel(); boss.arena.ApplyTo(model); model.SetBlockedCells(boss.bossVisual.OccupiedCells());
                        model.BeginRound(0, true, start);
                        using (var host = new PatternPreviewHost(boss) { player = model.Player })
                        using (var runner = new PatternRunner(pattern, Context(host)))
                        {
                            runner.Advance(.5f); // Deliberate reaction delay. One input per .35s, no grace reliance.
                            while (distances[model.Player] > 0)
                            {
                                var direction = Directions.First(d => distances.TryGetValue(model.Player + d, out int n) && n == distances[model.Player] - 1);
                                Assert.AreNotEqual(MoveResult.Blocked, model.TryMove(direction));
                                host.player = model.Player; runner.Advance(.35f);
                            }
                            Assert.Less(runner.Time, 5); runner.Advance(5.25f - runner.Time);
                            Assert.IsFalse(host.log.Any(l => l.StartsWith("Hit:")), region.name + " from " + start);
                        }
                        tested++;
                    }
                }
            }
            Assert.AreEqual(682, tested);
        }

        [Test]
        public void RemainingInEachPetalHitsAndCancellationClearsEveryLease()
        {
            foreach (var region in Saved.tileRegions)
                using (var host = new PatternPreviewHost(Saved) { player = region.cells[0] })
                {
                    var context = Context(host); var runner = new PatternRunner(CopyPattern(), context);
                    runner.Advance(5.2f); Assert.AreEqual(1, host.log.Count(l => l.StartsWith("Hit:")));
                    runner.Dispose(); Assert.IsEmpty(host.marks); Assert.IsEmpty(context.Selections);
                }
        }

        [UnityTest]
        public IEnumerator EditorPanelsAndFrozenOverlayRenderWithoutChangingAsset()
        {
            var window = ScriptableObject.CreateInstance<BossEncounterEditorWindow>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic; var type = window.GetType();
            string before = EditorJsonUtility.ToJson(Saved);
            try
            {
                type.GetMethod("SelectEncounter", flags).Invoke(window, new object[] { Saved });
                window.Show(); window.position = new Rect(20, 20, 1350, 960);
                var nodeField = type.GetField("node", flags); nodeField.SetValue(window, Enum.Parse(nodeField.FieldType, "Regions"));
                window.Repaint(); yield return null; yield return null;
                nodeField.SetValue(window, Enum.Parse(nodeField.FieldType, "Pattern"));
                type.GetField("phaseIndex", flags).SetValue(window, 0); type.GetField("patternIndex", flags).SetValue(window, 1);
                type.GetField("selectedClip", flags).SetValue(window, 0);
                type.GetField("previewPlayer", flags).SetValue(window, new Vector2Int(15, 0));
                type.GetMethod("RebuildPreview", flags).Invoke(window, null);
                var host = (PatternPreviewHost)type.GetField("previewHost", flags).GetValue(window);
                host.player = new Vector2Int(15, 31);
                type.GetField("previewPlayer", flags).SetValue(window, host.player);
                var overlay = (HashSet<Vector2Int>)type.GetMethod("SelectionOverlay", flags).Invoke(window,
                    new object[] { ((WarningEvent)Saved.FindPattern(PatternId).clips[0].action).tiles });
                CollectionAssert.AreEquivalent(Saved.tileRegions[3].cells, overlay);
                window.Repaint(); yield return null; yield return null;
                Assert.AreEqual(before, EditorJsonUtility.ToJson(Saved)); LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }
    }
}
#endif
