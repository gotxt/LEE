#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NHN.TraceStrike.Editor;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Tests
{
    // Exercise shipped content with the current editor, without migrating or saving the source asset.
    public sealed class CrimsonCompatibilityTests
    {
        const string AssetPath = "Assets/Resources/Patterns/BossData_CrimsonGolem.asset";
        static BossEncounterDefinition Boss => AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(AssetPath);
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string[] PatternIds = {
            "targeted", "example-devices", "crystal-seal-attack",
            "p1-cross-0", "p1-diagonal-0", "p1-diagonal-1", "p1-diamond-0", "p1-diamond-1",
            "p1-fist-ripple", "p1-alternating-rockfall", "p1-chasing-rockfall",
            "p2-combined-0", "p2-combined-1", "p2-cross-0", "p2-cross-1",
            "p2-diagonal-0", "p2-diagonal-1", "p2-diamond-0", "p2-diamond-1",
            "p2-horizontal-0", "p2-horizontal-1", "p2-vertical-0", "p2-vertical-1"
        };

        [Test]
        public void SavedContentImportsWithoutConflictsMissingTypesOrDuplicateIds()
        {
            string text = File.ReadAllText(AssetPath);
            foreach (string marker in new[] { "<<<<<<<", "=======", ">>>>>>>" })
                StringAssert.DoesNotContain(marker, text);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Assert.IsNotNull(Boss);
            Assert.IsEmpty(Boss.ValidateDefinition());
            CollectionAssert.AreEquivalent(PatternIds,
                Boss.libraryPatterns.Concat(Boss.phases.SelectMany(p => p.patterns)).Select(p => p.id));
            foreach (var pattern in Boss.AllPatterns())
            {
                Assert.IsTrue(pattern.clips.All(c => c?.action != null), pattern.id);
                Assert.IsEmpty(PatternValidation.Errors(pattern, Boss.FindPattern), pattern.id);
            }
            // A stale empty copy in the conflict must not replace either existing disabled timeline.
            foreach (string id in new[] { "p1-diagonal-1", "p1-diamond-0" })
            {
                Assert.IsFalse(Boss.FindPattern(id).enabled);
                Assert.AreEqual(6, Boss.FindPattern(id).clips.Count);
            }
            Assert.IsTrue(Boss.FindPattern("p1-chasing-rockfall").enabled);
            Assert.AreEqual(23, Boss.FindPattern("p1-chasing-rockfall").clips.Count);
            foreach (var clip in Boss.FindPattern("p2-cross-0").clips)
                if (AttackStepEditing.Tiles(clip) is TileSelection tiles)
                    Assert.IsFalse(tiles.ensureEscape, "Keep the incoming authored attack option with current metadata.");
        }

        [TestCaseSource(nameof(PatternIds))]
        public void CurrentSimpleEditorRecognizesAndEditsEveryAttackOnACopy(string id)
        {
            var source = Boss.FindPattern(id);
            Assert.IsNotNull(source);
            string unchanged = EditorJsonUtility.ToJson(Boss);
            var copy = JsonUtility.FromJson<EncounterPattern>(JsonUtility.ToJson(source));
            var steps = AttackStepEditing.Find(copy);
            Assert.AreEqual(copy.clips.Count(c => c.action is WarningEvent), steps.Count, id);
            Assert.AreEqual(copy.clips.Count(c => c.action is DamageEvent), steps.Count, id);
            foreach (var step in steps)
            {
                var independent = copy.clips.Except(step.Members).ToDictionary(c => c, JsonUtility.ToJson);
                AttackStepEditing.SetTiming(step, step.Warning.start + .125f, step.Warning.duration, step.Damage.duration);
                Assert.That(step.Damage.start, Is.EqualTo(step.Warning.start + step.Warning.duration).Within(.0001f));
                step.Tiles.shape = TileShape.Cells;
                step.Tiles.cells = new List<Vector2Int> { Vector2Int.zero, Vector2Int.right };
                AttackStepEditing.SynchronizeArea(step);
                foreach (var clip in step.Members)
                {
                    Assert.AreEqual(step.Key, clip.attackGroupKey);
                    var tiles = AttackStepEditing.Tiles(clip);
                    if (tiles != null) Assert.AreEqual(JsonUtility.ToJson(step.Tiles), JsonUtility.ToJson(tiles));
                }
                foreach (var pair in independent) Assert.AreEqual(pair.Value, JsonUtility.ToJson(pair.Key));
            }
            Assert.AreEqual(unchanged, EditorJsonUtility.ToJson(Boss), "Opening/editing a copy must not dirty shipped data.");
        }

        [TestCaseSource(nameof(PatternIds))]
        public void CurrentEditorCanSelectScrubAndFinishEachTimeline(string id)
        {
            var boss = Boss;
            var pattern = boss.FindPattern(id);
            var window = ScriptableObject.CreateInstance<BossEncounterEditorWindow>();
            string unchanged = EditorJsonUtility.ToJson(boss);
            try
            {
                var type = typeof(BossEncounterEditorWindow);
                type.GetMethod("SelectEncounter", PrivateInstance).Invoke(window, new object[] { boss });
                int phase = boss.phases.FindIndex(p => p.patterns.Contains(pattern));
                int index = phase >= 0 ? boss.phases[phase].patterns.IndexOf(pattern) : boss.libraryPatterns.IndexOf(pattern);
                var kind = System.Enum.Parse(type.GetNestedType("NodeKind", BindingFlags.NonPublic),
                    phase >= 0 ? "Pattern" : "LibraryPattern");
                type.GetMethod("SelectNode", PrivateInstance).Invoke(window, new[] { kind, (object)phase, index });
                Assert.IsNull(type.GetField("previewError", PrivateInstance).GetValue(window), id);
                var runner = (PatternRunner)type.GetField("previewRunner", PrivateInstance).GetValue(window);
                var host = (PatternPreviewHost)type.GetField("previewHost", PrivateInstance).GetValue(window);
                Assert.IsNotNull(runner, id);
                while (!runner.IsComplete) runner.Advance(.05f);
                Assert.IsEmpty(host.marks, id + " normal completion cleanup");
                type.GetField("playhead", PrivateInstance).SetValue(window, pattern.Duration * .5f);
                type.GetMethod("RebuildPreview", PrivateInstance).Invoke(window, null);
                Assert.IsNull(type.GetField("previewError", PrivateInstance).GetValue(window), id + " scrub");
                host = (PatternPreviewHost)type.GetField("previewHost", PrivateInstance).GetValue(window);
                type.GetMethod("DisposePreview", PrivateInstance).Invoke(window, null);
                Assert.IsEmpty(host.marks, id + " cancellation cleanup");
                Assert.AreEqual(unchanged, EditorJsonUtility.ToJson(boss));
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void WarningSnapshotsRemainSharedAfterPlayerMoves()
        {
            foreach (var pattern in Boss.AllPatterns())
            foreach (var step in AttackStepEditing.Find(pattern))
            using (var host = new PatternPreviewHost(Boss))
            using (var context = new PatternContext(host, host.CenterCell, 0, Boss.FindPattern))
            {
                context.InitializeLocations(pattern.locationGroups);
                var expected = step.Tiles.Capture(context);
                host.player = host.Traversable.First(c => c != host.player);
                foreach (var clip in step.Members)
                    if (AttackStepEditing.Tiles(clip) is TileSelection tiles)
                        CollectionAssert.AreEquivalent(expected, tiles.Resolve(context), pattern.id + "/" + step.Key);
            }
        }

        [Test]
        public void CopySurvivesCurrentUnitySaveAndReimport()
        {
            const string path = "Assets/__CrimsonCompatibilityTest.asset";
            Assert.IsFalse(File.Exists(path));
            var copy = Object.Instantiate(Boss);
            try
            {
                copy.name = "CrimsonCompatibilityTest";
                AssetDatabase.CreateAsset(copy, path);
                AssetDatabase.SaveAssetIfDirty(copy);
                string before = EditorJsonUtility.ToJson(copy);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var reloaded = AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(path);
                Assert.AreEqual(before, EditorJsonUtility.ToJson(reloaded));
                Assert.IsEmpty(reloaded.ValidateDefinition());
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
#endif
