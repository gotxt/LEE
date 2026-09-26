#if UNITY_EDITOR
using System.Collections;
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
    public sealed class AttackStepEditingTests
    {
        private BossEncounterDefinition boss;
        private EncounterPattern Pattern => boss.phases[0].patterns[0];
        [SetUp] public void SetUp() { boss = ScriptableObject.CreateInstance<BossEncounterDefinition>(); }
        [TearDown] public void TearDown() { Undo.ClearUndo(boss); Object.DestroyImmediate(boss); }

        [Test]
        public void ExistingCrossIsRecognizedWithoutChangingData()
        {
            var existing = Resources.Load<BossEncounterDefinition>("Patterns/BossData_CrimsonGolem");
            Assert.IsNotNull(existing, "The shipped encounter must import as a BossEncounterDefinition");
            var pattern = existing.AllPatterns().First(p => p.name == "P1_Cross_0");
            string before = EditorJsonUtility.ToJson(existing);
            var steps = AttackStepEditing.Find(pattern);
            // The shipped encounter can be edited by designers; extra attacks
            // must not invalidate this test of the original legacy group.
            var legacy = steps.Single(s => s.Key == "glyph");
            Assert.AreEqual(6, legacy.Members.Count);
            Assert.AreEqual(before, EditorJsonUtility.ToJson(existing), "Opening simple mode must not migrate or dirty assets");
            var cloned = (EncounterPattern)typeof(BossEncounterEditorWindow)
                .GetMethod("ClonePattern", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { pattern });
            Assert.AreEqual(6, AttackStepEditing.Find(cloned).Single(s => s.Key == "glyph").Members.Count,
                "Duplicating the whole legacy pattern must also preserve cue connections");
        }

        [Test]
        public void AddedAndDuplicatedAttacksHaveIndependentKeysAndPaintedAreas()
        {
            var a = AttackStepEditing.Add(Pattern, 0);
            a.Tiles.cells.Add(new Vector2Int(6, 8));
            AttackStepEditing.SynchronizeArea(a);
            var b = AttackStepEditing.Duplicate(Pattern, a, 3);
            b.Tiles.cells.Clear(); b.Tiles.cells.Add(new Vector2Int(10, 8));
            AttackStepEditing.SynchronizeArea(b);
            Assert.AreNotEqual(a.Key, b.Key);
            CollectionAssert.AreEqual(new[] { new Vector2Int(6, 8) }, a.Tiles.cells);
            CollectionAssert.AreEqual(b.Tiles.cells, ((DamageEvent)b.Damage.action).tiles.cells);
            Assert.AreNotSame(a.Tiles.cells, b.Tiles.cells);
            Assert.AreEqual(3, b.Warning.start); Assert.AreEqual(4, b.Damage.start);
        }

        [Test]
        public void SequentialAndSimultaneousImpactsResolveTheirOwnAreasAtRuntime()
        {
            var a = AttackStepEditing.Add(Pattern, 0); a.Tiles.cells.Add(new Vector2Int(6, 8));
            AttackStepEditing.SynchronizeArea(a);
            var b = AttackStepEditing.Duplicate(Pattern, a, 2);
            b.Tiles.cells.Clear(); b.Tiles.cells.Add(new Vector2Int(10, 8)); AttackStepEditing.SynchronizeArea(b);
            var host = new PatternPreviewHost(boss.arena);
            using (var runner = new PatternRunner(Pattern, new PatternContext(host, host.CenterCell)))
            {
                runner.Advance(1.05f);
                CollectionAssert.AreEquivalent(new[] { new Vector2Int(6, 8) }, host.marks.Values.SelectMany(m => m.Cells));
                runner.Advance(2f);
                CollectionAssert.AreEquivalent(new[] { new Vector2Int(10, 8) }, host.marks.Values.SelectMany(m => m.Cells));
            }
            AttackStepEditing.SetTiming(b, 0, 1, 0.3f);
            using (var runner = new PatternRunner(Pattern, new PatternContext(host, host.CenterCell)))
            {
                runner.Advance(1.05f);
                CollectionAssert.AreEquivalent(new[] { new Vector2Int(6, 8), new Vector2Int(10, 8) }, host.marks.Values.SelectMany(m => m.Cells));
            }
        }

        [Test]
        public void SequentialAttacksShareOneRandomLocationButKeepTheirOwnOffsets()
        {
            var group = new PatternLocationGroup { id = "shared-random", source = PatternLocationSource.RandomWalkable };
            Pattern.locationGroups.Add(group);
            var first = AttackStepEditing.Add(Pattern, 0);
            first.Tiles.locationGroupId = group.id;
            first.Tiles.cells.Add(Vector2Int.zero);
            AttackStepEditing.SynchronizeArea(first);
            var second = AttackStepEditing.Duplicate(Pattern, first, 2);
            second.Tiles.cells.Clear(); second.Tiles.cells.Add(Vector2Int.right);
            AttackStepEditing.SynchronizeArea(second);

            using (var host = new PatternPreviewHost(boss.arena))
            using (var context = new PatternContext(host, host.CenterCell, locationSeed: 42))
            using (var runner = new PatternRunner(Pattern, context))
            {
                Vector2Int origin = context.Location(group.id);
                CollectionAssert.Contains(host.Traversable.ToArray(), origin);
                runner.Advance(0);
                CollectionAssert.AreEquivalent(new[] { origin }, host.marks.Values.SelectMany(m => m.Cells));
                host.player = origin + Vector2Int.up;
                runner.Advance(2f);
                CollectionAssert.AreEquivalent(host.Walkable.Contains(origin + Vector2Int.right)
                    ? new[] { origin + Vector2Int.right } : new Vector2Int[0],
                    host.marks.Values.SelectMany(m => m.Cells));
                Assert.AreEqual(origin, context.Location(group.id));
                Assert.AreNotEqual(first.Key, second.Key, "Each attack still needs its own warning/damage area snapshot.");
            }
        }

        [Test]
        public void CopyingPatternPreservesLocationGroupsAndReferences()
        {
            var group = new PatternLocationGroup { id = "spot", name = "첫 위치", source = PatternLocationSource.FixedCell,
                fixedCell = new Vector2Int(9, 8) };
            Pattern.locationGroups.Add(group);
            var step = AttackStepEditing.Add(Pattern, 0);
            step.Tiles.locationGroupId = group.id;
            AttackStepEditing.SynchronizeArea(step);
            var copy = (EncounterPattern)typeof(BossEncounterEditorWindow)
                .GetMethod("ClonePattern", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { Pattern });
            Assert.AreNotSame(group, copy.locationGroups[0]);
            Assert.AreEqual(group.id, copy.locationGroups[0].id);
            Assert.AreEqual(group.id, AttackStepEditing.Find(copy).Single().Tiles.locationGroupId);
            Assert.IsEmpty(PatternValidation.Errors(copy));
        }

        [Test]
        public void AssigningSharedLocationKeepsThePaintedAreaThenMovesWithItsGroup()
        {
            var group = new PatternLocationGroup { id = "fixed-group", source = PatternLocationSource.FixedCell,
                fixedCell = new Vector2Int(9, 8) };
            Pattern.locationGroups.Add(group);
            var step = AttackStepEditing.Add(Pattern, 0);
            step.Tiles.cells.Add(new Vector2Int(6, 8));
            AttackStepEditing.SynchronizeArea(step);
            var window = ScriptableObject.CreateInstance<BossEncounterEditorWindow>();
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(BossEncounterEditorWindow).GetMethod("SelectEncounter", flags).Invoke(window, new object[] { boss });
                typeof(BossEncounterEditorWindow).GetField("node", flags).SetValue(window,
                    System.Enum.Parse(typeof(BossEncounterEditorWindow).GetNestedType("NodeKind", BindingFlags.NonPublic), "Pattern"));
                typeof(BossEncounterEditorWindow).GetField("phaseIndex", flags).SetValue(window, 0);
                typeof(BossEncounterEditorWindow).GetField("patternIndex", flags).SetValue(window, 0);
                var positions = (System.Collections.Generic.Dictionary<string, Vector2Int>)
                    typeof(BossEncounterEditorWindow).GetField("previewLocations", flags).GetValue(window);
                positions[group.id] = group.fixedCell;
                typeof(BossEncounterEditorWindow).GetMethod("AssignLocationGroup", flags)
                    .Invoke(window, new object[] { step.Tiles, group.id });
                AttackStepEditing.SynchronizeArea(step);
                Assert.AreEqual(new Vector2Int(-3, 0), step.Tiles.cells.Single());
                var host = new PatternPreviewHost(boss.arena);
                using (var context = new PatternContext(host, host.CenterCell))
                {
                    context.InitializeLocations(Pattern.locationGroups);
                    CollectionAssert.AreEquivalent(new[] { new Vector2Int(6, 8) }, step.Tiles.Resolve(context));
                }
                group.fixedCell = new Vector2Int(10, 8);
                using (var context = new PatternContext(host, host.CenterCell))
                {
                    context.InitializeLocations(Pattern.locationGroups);
                    CollectionAssert.AreEquivalent(new[] { new Vector2Int(7, 8) }, step.Tiles.Resolve(context));
                    CollectionAssert.AreEquivalent(step.Tiles.Resolve(context),
                        ((DamageEvent)step.Damage.action).tiles.Resolve(context));
                }
                host.Dispose();
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void ChangingLegacyTimingMovesItsSoundCameraAndVfxButNotOtherEvents()
        {
            var a = AttackStepEditing.Add(Pattern, 0);
            var warningSound = new PatternClip { label = "Warning SFX", start = 0, duration = 1, action = new SfxEvent() };
            var impactSound = new PatternClip { label = "Impact SFX", start = 1, duration = 0.3f, action = new SfxEvent() };
            var camera = new PatternClip { label = "Camera", start = 1, duration = 0.3f, action = new CameraEvent() };
            var vfx = new PatternClip { start = 1, duration = 0.3f, action = new VfxEvent { tiles = AttackStepEditing.CopyTiles(a.Tiles) } };
            var unrelated = new PatternClip { start = 0, duration = 8, action = new WaitEvent() };
            Pattern.clips.AddRange(new[] { warningSound, impactSound, camera, vfx, unrelated });
            a = AttackStepEditing.Find(Pattern).Single();
            AttackStepEditing.SetTiming(a, 4, 2, 0.6f);
            Assert.AreEqual(4, warningSound.start); Assert.AreEqual(2, warningSound.duration);
            foreach (var clip in new[] { impactSound, camera, vfx })
            { Assert.AreEqual(6, clip.start); Assert.AreEqual(0.6f, clip.duration); }
            Assert.AreEqual(0, unrelated.start); Assert.AreEqual(8, unrelated.duration);
            Assert.AreEqual(6, AttackStepEditing.Find(Pattern).Single().Members.Count);
        }

        [Test]
        public void AmbiguousSharedKeysAndSharedLegacyCuesStayInAdvancedMode()
        {
            var a = AttackStepEditing.Add(Pattern, 0);
            var b = AttackStepEditing.Duplicate(Pattern, a, 0);
            var sound = new PatternClip { label = "Warning SFX", start = 0, duration = 1, action = new SfxEvent() };
            Pattern.clips.Add(sound);
            Assert.IsFalse(AttackStepEditing.Find(Pattern).Any(s => s.Members.Contains(sound)));
            ((WarningEvent)b.Warning.action).tiles.snapshotKey = a.Key;
            ((DamageEvent)b.Damage.action).tiles.snapshotKey = a.Key;
            Assert.IsEmpty(AttackStepEditing.Find(Pattern));
        }

        [Test]
        public void UndoRedoAndSerializationKeepGroupOwnershipAndAreas()
        {
            var a = AttackStepEditing.Add(Pattern, 0);
            Undo.RegisterCompleteObjectUndo(boss, "Edit grouped area");
            a.Tiles.cells.Add(new Vector2Int(8, 8)); AttackStepEditing.SynchronizeArea(a);
            EditorUtility.SetDirty(boss);
            Undo.PerformUndo(); Assert.IsEmpty(AttackStepEditing.Find(Pattern).Single().Tiles.cells);
            Undo.PerformRedo(); Assert.AreEqual(1, AttackStepEditing.Find(Pattern).Single().Tiles.cells.Count);
            var restored = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(boss), restored);
                var copy = AttackStepEditing.Find(restored.phases[0].patterns[0]).Single();
                Assert.AreEqual(a.Key, copy.Key); Assert.AreEqual(copy.Key, copy.Damage.attackGroupKey);
                CollectionAssert.AreEqual(copy.Tiles.cells, ((DamageEvent)copy.Damage.action).tiles.cells);
            }
            finally { Object.DestroyImmediate(restored); }
        }

        [Test]
        public void DeleteAndDisableAffectOnlySelectedAttackAndOwnedCues()
        {
            var a = AttackStepEditing.Add(Pattern, 0);
            var b = AttackStepEditing.Duplicate(Pattern, a, 2);
            var camera = new PatternClip { action = new CameraEvent(), attackGroupKey = a.Key, attackAtImpact = true };
            var custom = new PatternClip { action = new WaitEvent() };
            Pattern.clips.Add(camera); Pattern.clips.Add(custom);
            a = AttackStepEditing.Find(Pattern).First();
            AttackStepEditing.SetEnabled(a, false);
            Assert.IsTrue(a.Members.All(c => !c.enabled)); Assert.IsTrue(b.Warning.enabled);
            AttackStepEditing.Delete(Pattern, a);
            Assert.IsFalse(Pattern.clips.Contains(camera)); Assert.IsTrue(Pattern.clips.Contains(custom));
            Assert.AreEqual(b.Key, AttackStepEditing.Find(Pattern).Single().Key);
        }

        [Test]
        public void SoundCanBeAddedRemovedAndCopiedWithoutLosingAssetReference()
        {
            var audio = AudioClip.Create("Test sound", 64, 1, 8000, false);
            try
            {
                var a = AttackStepEditing.Add(Pattern, 0);
                AttackStepEditing.SetSound(Pattern, a, true, audio);
                var b = AttackStepEditing.Duplicate(Pattern, a, 2);
                Assert.AreSame(audio, ((SfxEvent)b.Members.Single(c => c.action is SfxEvent).action).clip);
                AttackStepEditing.SetSound(Pattern, a, true, null);
                Assert.IsFalse(a.Members.Any(c => c.action is SfxEvent));
                Assert.IsTrue(b.Members.Any(c => c.action is SfxEvent));
            }
            finally { Object.DestroyImmediate(audio); }
        }

        [UnityTest]
        public IEnumerator SimpleAndAdvancedWindowViewsRenderWithoutGuiErrors()
        {
            var group = new PatternLocationGroup { id = "gui-location", source = PatternLocationSource.RandomWalkable,
                restrictRandomCells = true,
                randomCells = new System.Collections.Generic.List<Vector2Int> { new Vector2Int(8, 8) } };
            Pattern.locationGroups.Add(group);
            var step = AttackStepEditing.Add(Pattern, 0);
            step.Tiles.locationGroupId = group.id;
            step.Tiles.cells.Add(Vector2Int.zero);
            AttackStepEditing.SynchronizeArea(step);
            var window = ScriptableObject.CreateInstance<BossEncounterEditorWindow>();
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(BossEncounterEditorWindow).GetMethod("SelectEncounter", flags).Invoke(window, new object[] { boss });
                typeof(BossEncounterEditorWindow).GetField("node", flags).SetValue(window,
                    System.Enum.Parse(typeof(BossEncounterEditorWindow).GetNestedType("NodeKind", BindingFlags.NonPublic), "Pattern"));
                typeof(BossEncounterEditorWindow).GetField("phaseIndex", flags).SetValue(window, 0);
                typeof(BossEncounterEditorWindow).GetField("patternIndex", flags).SetValue(window, 0);
                typeof(BossEncounterEditorWindow).GetMethod("RebuildPreview", flags).Invoke(window, null);
                window.Show(); window.position = new Rect(20, 20, 1200, 760);
                for (int mode = 0; mode <= 1; mode++)
                {
                    typeof(BossEncounterEditorWindow).GetField("attackEditorMode", flags).SetValue(window, mode);
                    window.Repaint(); yield return null; yield return null;
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }
    }
}
#endif
