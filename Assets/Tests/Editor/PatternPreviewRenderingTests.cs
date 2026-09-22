#if UNITY_EDITOR
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

namespace NHN.TraceStrike.Tests
{
    public sealed class PatternPreviewRenderingTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void LayersHaveExplicitPriorityRegardlessOfCreationOrder(bool reverse)
        {
            var host = new PatternPreviewHost(17);
            var cells = new[] { host.CenterCell };
            var leases = new List<IPatternLease>();
            try
            {
                var layers = Enumerable.Range(0, 5);
                foreach (int layer in reverse ? layers.Reverse() : layers)
                {
                    switch (layer)
                    {
                        case 0: leases.Add(host.Block(cells)); break;
                        case 1: leases.Add(host.Hazard(cells, "test")); break;
                        case 2: leases.Add(host.Mark(cells, Color.yellow, true)); break;
                        case 3: leases.Add(host.Mark(cells, Color.red, false)); break;
                        case 4: leases.Add(host.Spawn("effect", null, cells[0], null, Color.blue)); break;
                    }
                }
                CollectionAssert.AreEqual(new[] {
                    PatternPreviewHost.PreviewLayer.Obstacle, PatternPreviewHost.PreviewLayer.Hazard,
                    PatternPreviewHost.PreviewLayer.Warning, PatternPreviewHost.PreviewLayer.Damage,
                    PatternPreviewHost.PreviewLayer.Effect
                }, host.GetOrderedMarks().Select(m => m.Layer));
            }
            finally { foreach (var lease in leases) lease.Dispose(); }
            Assert.IsEmpty(host.GetOrderedMarks());
            CollectionAssert.Contains(host.Traversable, host.CenterCell);
        }

        [Test]
        public void ReusedSlotsAndProgressUpdatesKeepNewerMarksOnTopWithinALayer()
        {
            var host = new PatternPreviewHost(17);
            var cells = new[] { host.CenterCell };
            var removed = host.Mark(cells, Color.red, true);
            using (var older = host.Mark(cells, Color.yellow, true))
            {
                removed.Dispose();
                using (var newer = host.Mark(cells, Color.blue, true))
                {
                    older.SetProgress(0.5f);
                    var ordered = host.GetOrderedMarks();
                    var expected = Color.yellow; expected.a = 0.6f;
                    Assert.AreEqual(expected, ordered[0].Color);
                    Assert.AreEqual(Color.blue, ordered[1].Color);
                    removed.SetProgress(1); // Disposed leases cannot resurrect a mark.
                    Assert.AreEqual(2, host.GetOrderedMarks().Count);
                    CollectionAssert.AreEqual(ordered, host.GetOrderedMarks());
                }
            }
            Assert.IsEmpty(host.GetOrderedMarks());
        }

        [Test]
        public void AllThreeFistWavesHaveUniformTileStackingAfterEarlierWavesExpire()
        {
            var boss = Resources.Load<BossEncounterDefinition>("Patterns/BossData_CrimsonGolem");
            var pattern = boss.FindPattern("p1-fist-ripple");
            string before = EditorJsonUtility.ToJson(boss);
            var root = new GameObject("Preview layering test");
            try
            {
                using (var presentation = new BossPresentation(boss.bossVisual, root.transform, true))
                {
                    var host = new PatternPreviewHost(boss.arena) { boss = presentation };
                    using (var runner = new PatternRunner(pattern, new PatternContext(host, host.CenterCell)))
                    using (var area = new PatternContext(host, host.CenterCell))
                    {
                        var steps = AttackStepEditing.Find(pattern).OrderBy(s => s.Damage.start).ToArray();
                        Assert.AreEqual(3, steps.Length);
                        foreach (var step in steps)
                        {
                            runner.Advance(step.Damage.start + 0.01f - runner.Time);
                            var ordered = host.GetOrderedMarks();
                            Color? firstColor = null;
                            var cells = step.Tiles.Resolve(area);
                            Assert.IsNotEmpty(cells);
                            foreach (var cell in cells)
                            {
                                var stack = ordered.Where(m => m.Cells.Contains(cell)).ToArray();
                                CollectionAssert.AreEqual(new[] { PatternPreviewHost.PreviewLayer.Damage,
                                    PatternPreviewHost.PreviewLayer.Effect }, stack.Select(m => m.Layer), step.Name + " " + cell);
                                Color composite = stack.Aggregate(new Color(0.27f, 0.3f, 0.35f),
                                    (color, mark) => Color.Lerp(color, mark.Color, mark.Color.a));
                                if (firstColor.HasValue) Assert.AreEqual(firstColor.Value, composite, step.Name + " " + cell);
                                else firstColor = composite;
                            }
                        }
                        runner.Advance(pattern.Duration - runner.Time);
                        Assert.IsEmpty(host.GetOrderedMarks());
                    }
                    Assert.IsEmpty(host.marks);
                }
                Assert.AreEqual(before, EditorJsonUtility.ToJson(boss));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator SharedTimelineRendersLayeredMarksAndSelectionWithoutChangingPattern()
        {
            var pattern = ScriptableObject.CreateInstance<PatternSequence>();
            var tiles = new TileSelection { shape = TileShape.Cells, anchor = TileAnchor.Absolute,
                ensureEscape = false, cells = new List<Vector2Int> { new Vector2Int(8, 8) } };
            pattern.clips.Add(new PatternClip { start = 0, duration = 1, action = new WarningEvent { tiles = tiles } });
            pattern.clips.Add(new PatternClip { start = 1, duration = 0.3f, action = new DamageEvent { tiles = tiles } });
            pattern.clips.Add(new PatternClip { start = 1, duration = 0.35f, action = new VfxEvent { tiles = tiles } });
            var window = ScriptableObject.CreateInstance<PatternTimelineWindow>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(PatternTimelineWindow);
            string before = EditorJsonUtility.ToJson(pattern);
            try
            {
                type.GetMethod("Select", flags).Invoke(window, new object[] { pattern });
                type.GetField("selected", flags).SetValue(window, 0);
                window.Show(); window.position = new Rect(20, 20, 1200, 760);
                foreach (float time in new[] { 0.5f, 1.1f, 1.4f })
                {
                    type.GetField("playhead", flags).SetValue(window, time);
                    type.GetMethod("RebuildPreview", flags).Invoke(window, null);
                    window.Repaint(); yield return null; yield return null;
                }
                Assert.AreEqual(before, EditorJsonUtility.ToJson(pattern));
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); Object.DestroyImmediate(pattern); }
        }
    }
}
#endif
