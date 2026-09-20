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
    public sealed class BossMechanicTests
    {
        private BossEncounterDefinition boss;
        private CrystalSealMechanic seal;
        private EncounterPattern attack;
        private PatternPreviewHost host;
        [SetUp] public void SetUp()
        {
            boss = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            attack = BossEncounterEditorWindow.CreateCrystalAttackPattern();
            boss.libraryPatterns.Add(attack);
            seal = new CrystalSealMechanic { attackPatternId = attack.id, initialDelay = 1, interval = 2 };
            seal.crystals.Add(new CrystalPlacement { cell = new Vector2Int(5, 8) });
            seal.crystals.Add(new CrystalPlacement { cell = new Vector2Int(11, 8) });
            boss.phases[0].mechanics.Add(seal);
            host = new PatternPreviewHost(boss.arena);
        }
        [TearDown] public void TearDown() { Undo.ClearUndo(boss); Object.DestroyImmediate(boss); }
        private BossMechanicSession Session() => new BossMechanicSession(boss.phases[0].mechanics, new MechanicContext(host, boss.FindPattern));

        [Test]
        public void ShippedEncounterHasFiveFixedCrystalsAndValidAttackAndVisualReferences()
        {
            var shipped = Resources.Load<BossEncounterDefinition>("Patterns/CrimsonGolem");
            Assert.IsEmpty(shipped.ValidateDefinition());
            Assert.IsTrue(shipped.phases.All(p => !p.legacyCrystals));
            var setting = shipped.phases[1].mechanics.OfType<CrystalSealMechanic>().Single();
            Assert.AreEqual(5, setting.crystals.Count);
            Assert.AreEqual(5, setting.crystals.Select(d => d.cell).Distinct().Count());
            Assert.IsNotNull(setting.activePrefab.GetComponent<CrystalVisual>());
            Assert.IsNotNull(((VfxEvent)shipped.FindPattern(setting.attackPatternId).clips.Single(c => c.action is VfxEvent).action).prefab);
            Assert.IsTrue(setting.crystals.All(d => shipped.arena.GetCells().Contains(d.cell)));
        }

        [Test]
        public void FinalSealAttackRetainsHealthOneAndNextAttackCanKill()
        {
            using (var session = Session())
            {
                var runtime = (CrystalSealRuntime)session.Runtimes[0];
                Assert.AreEqual(1, session.ResolvePlayerAttack(new Vector2Int[0], 150, 999));
                Assert.AreEqual(2, runtime.ActiveCount);
                Assert.AreEqual(1, session.ResolvePlayerAttack(new[] { seal.crystals[0].cell }, 1, 999));
                Assert.AreEqual(1, runtime.ActiveCount);
                Assert.AreEqual(1, session.ResolvePlayerAttack(new[] { seal.crystals[1].cell }, 1, 999));
                Assert.AreEqual(0, runtime.ActiveCount);
                Assert.AreEqual(0, session.MinimumBossHealth);
                Assert.AreEqual(0, session.ResolvePlayerAttack(new[] { host.CenterCell }, 1, 1));
            }
        }

        [Test]
        public void OneCompletedAttackCanDisableAllAndDoesNotAutomaticallyKillHealthyBoss()
        {
            using (var session = Session())
            {
                int health = session.ResolvePlayerAttack(seal.crystals.Select(d => d.cell).ToArray(), 150, 10);
                Assert.AreEqual(140, health);
                Assert.AreEqual(0, ((CrystalSealRuntime)session.Runtimes[0]).ActiveCount);
                Assert.AreEqual(139, session.ResolvePlayerAttack(new Vector2Int[0], health, 1));
            }
        }

        [Test]
        public void WalkingAndClockTicksDoNotDisableCrystalsOrBlockTheirTiles()
        {
            using (var session = Session())
            {
                foreach (var crystal in seal.crystals)
                { host.player = crystal.cell; session.Advance(.1f); CollectionAssert.Contains(host.Traversable, crystal.cell); }
                Assert.AreEqual(2, ((CrystalSealRuntime)session.Runtimes[0]).ActiveCount);
            }
        }

        [Test]
        public void EachAttackUsesItsDeviceOriginAndDeactivationCancelsOnlyItsOwnAttack()
        {
            using (var session = Session())
            {
                session.Advance(1.1f);
                var warnings = host.GetOrderedMarks().Where(m => m.Layer == PatternPreviewHost.PreviewLayer.Warning).ToArray();
                Assert.AreEqual(2, warnings.Length);
                foreach (var device in seal.crystals)
                    using (var context = new PatternContext(host, device.cell))
                        Assert.IsTrue(warnings.Any(m => m.Cells.SetEquals(((WarningEvent)attack.clips[0].action).tiles.Resolve(context))));
                session.ResolvePlayerAttack(new[] { seal.crystals[0].cell }, 150, 0);
                Assert.AreEqual(1, host.GetOrderedMarks().Count(m => m.Layer == PatternPreviewHost.PreviewLayer.Warning));
                session.Advance(4);
                Assert.AreEqual(1, host.GetOrderedMarks().Count(m => m.Layer == PatternPreviewHost.PreviewLayer.Warning));
                Assert.AreEqual(1, ((CrystalSealRuntime)session.Runtimes[0]).ActiveCount);
            }
            Assert.IsEmpty(host.marks);
        }

        [Test]
        public void DeviceTimingOverridesDoNotChangeSharedAttackData()
        {
            seal.crystals[1].overrideTiming = true; seal.crystals[1].initialDelay = 3; seal.crystals[1].interval = 4;
            string before = EditorJsonUtility.ToJson(boss);
            using (var session = Session())
            {
                session.Advance(1.1f);
                Assert.AreEqual(1, host.GetOrderedMarks().Count(m => m.Layer == PatternPreviewHost.PreviewLayer.Warning));
                session.Advance(2);
                Assert.AreEqual(2, host.GetOrderedMarks().Count(m => m.Layer == PatternPreviewHost.PreviewLayer.Warning));
            }
            Assert.AreEqual(before, EditorJsonUtility.ToJson(boss));
        }

        [Test]
        public void LargeAndSmallTimeStepsProduceTheSameRepeatingAttacks()
        {
            attack.clips.Add(new PatternClip { duration = 0, action = new SignalEvent { signal = "tick" } });
            using (var session = Session()) session.Advance(14.25f);
            int coarse = host.log.Count(s => s.StartsWith("Signal:")); host.log.Clear();
            using (var session = Session()) for (int i = 0; i < 114; i++) session.Advance(.125f);
            Assert.AreEqual(14, coarse);
            Assert.AreEqual(coarse, host.log.Count(s => s.StartsWith("Signal:")));
            Assert.IsEmpty(host.marks);
        }

        [Test]
        public void RestartCreatesFreshStateWithoutFixedDeviceLimit()
        {
            for (int x = 6; x <= 10; x++) seal.crystals.Add(new CrystalPlacement { cell = new Vector2Int(x, 10) });
            using (var session = Session())
            {
                Assert.AreEqual(7, ((CrystalSealRuntime)session.Runtimes[0]).ActiveCount);
                session.ResolvePlayerAttack(seal.crystals.Select(d => d.cell).ToArray(), 1, 1);
            }
            Assert.IsEmpty(host.marks);
            using (var restarted = Session()) Assert.AreEqual(7, ((CrystalSealRuntime)restarted.Runtimes[0]).ActiveCount);
        }

        [Test]
        public void MultipleMechanicsKeepIndependentOwnershipAndAggregateProtection()
        {
            var second = new CrystalSealMechanic { attackPatternId = attack.id };
            second.crystals.Add(new CrystalPlacement { cell = new Vector2Int(8, 11) });
            boss.phases[0].mechanics.Add(second);
            using (var session = Session())
            {
                session.ResolvePlayerAttack(seal.crystals.Select(d => d.cell).ToArray(), 1, 1);
                Assert.AreEqual(1, session.MinimumBossHealth);
                Assert.AreEqual(1, session.ResolvePlayerAttack(second.PlacementCells.ToArray(), 1, 1));
                Assert.AreEqual(0, session.ResolvePlayerAttack(new Vector2Int[0], 1, 1));
            }
        }

        [TestCase("empty")]
        [TestCase("duplicate")]
        [TestCase("void")]
        [TestCase("missing")]
        [TestCase("disabled")]
        [TestCase("interval")]
        [TestCase("nan")]
        public void InvalidMechanicSettingsAreRejected(string issue)
        {
            switch (issue)
            {
                case "empty": seal.crystals.Clear(); break;
                case "duplicate": seal.crystals[1].cell = seal.crystals[0].cell; break;
                case "void": seal.crystals[0].cell = new Vector2Int(-1, -1); break;
                case "missing": seal.attackPatternId = "missing"; break;
                case "disabled": attack.enabled = false; break;
                case "interval": seal.interval = .1f; break;
                case "nan": seal.initialDelay = float.NaN; break;
            }
            Assert.IsNotEmpty(boss.ValidateDefinition());
        }

        [Test]
        public void SerializationAndUndoPreserveMechanicTypesPlacementsAndPatternLinks()
        {
            var copy = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(boss), copy);
                var restored = (CrystalSealMechanic)copy.phases[0].mechanics.Single();
                Assert.AreEqual(2, restored.crystals.Count); Assert.AreEqual(attack.id, restored.attackPatternId);
                Undo.RegisterCompleteObjectUndo(boss, "Change crystal placement");
                seal.crystals[0].cell = new Vector2Int(6, 8); EditorUtility.SetDirty(boss);
                Undo.PerformUndo(); Assert.AreEqual(new Vector2Int(5, 8), ((CrystalSealMechanic)boss.phases[0].mechanics[0]).crystals[0].cell);
                Undo.PerformRedo(); Assert.AreEqual(new Vector2Int(6, 8), ((CrystalSealMechanic)boss.phases[0].mechanics[0]).crystals[0].cell);
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test]
        public void MechanicAttackEditingUsesSelectedDeviceOriginForPaintingAndPreview()
        {
            var window = ScriptableObject.CreateInstance<BossEncounterEditorWindow>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic; var type = typeof(BossEncounterEditorWindow);
            try
            {
                Assert.IsTrue(attack.clips.Select(AttackStepEditing.Tiles).Where(t => t != null).All(t => t.anchor == TileAnchor.Origin));
                type.GetMethod("SelectEncounter", flags).Invoke(window, new object[] { boss });
                type.GetMethod("SelectNode", flags).Invoke(window, new object[] {
                    System.Enum.Parse(type.GetNestedType("NodeKind", BindingFlags.NonPublic), "LibraryPattern"), -1, 0 });
                var origin = seal.crystals[0].cell;
                type.GetField("previewOriginOverride", flags).SetValue(window, true);
                type.GetField("mechanicAttackOrigin", flags).SetValue(window, origin);
                type.GetMethod("RebuildPreview", flags).Invoke(window, null);
                var tiles = ((WarningEvent)attack.clips[0].action).tiles;
                Assert.AreEqual(origin, type.GetMethod("PaintOrigin", flags).Invoke(window, new object[] { tiles }));
                var overlay = (HashSet<Vector2Int>)type.GetMethod("SelectionOverlay", flags).Invoke(window, new object[] { tiles });
                var previewHost = (PatternPreviewHost)type.GetField("previewHost", flags).GetValue(window);
                using (var context = new PatternContext(host, origin))
                {
                    var expected = tiles.Resolve(context);
                    CollectionAssert.AreEquivalent(expected, overlay);
                    CollectionAssert.AreEquivalent(expected, previewHost.marks.Values.Single().Cells);
                }
            }
            finally { Object.DestroyImmediate(window); }
        }

        [UnityTest]
        public IEnumerator MechanicEditorRendersAndPlacementAndSimulationDoNotChangeArenaOrAttack()
        {
            var window = ScriptableObject.CreateInstance<BossEncounterEditorWindow>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic; var type = typeof(BossEncounterEditorWindow);
            string arenaBefore = JsonUtility.ToJson(boss.arena), attackBefore = JsonUtility.ToJson(attack);
            try
            {
                type.GetMethod("SelectEncounter", flags).Invoke(window, new object[] { boss });
                type.GetMethod("SelectNode", flags).Invoke(window, new object[] {
                    System.Enum.Parse(type.GetNestedType("NodeKind", BindingFlags.NonPublic), "Mechanic"), 0, 0 });
                window.Show(); window.position = new Rect(20, 20, 1350, 1000);
                type.GetMethod("EditCrystalCell", flags).Invoke(window, new object[] { seal, new Vector2Int(8, 11), 1 });
                Assert.AreEqual(3, seal.crystals.Count);
                type.GetMethod("EditCrystalCell", flags).Invoke(window, new object[] { seal, new Vector2Int(8, 12), 0 });
                Assert.AreEqual(new Vector2Int(8, 12), seal.crystals[2].cell);
                type.GetMethod("EditCrystalCell", flags).Invoke(window, new object[] { seal, new Vector2Int(8, 12), 2 });
                Assert.AreEqual(2, seal.crystals.Count);
                for (int tool = 0; tool < 5; tool++)
                {
                    type.GetField("mechanicTool", flags).SetValue(window, tool);
                    window.Repaint(); yield return null; yield return null;
                }
                var trail = (HashSet<Vector2Int>)type.GetField("mechanicTestTrail", flags).GetValue(window);
                trail.UnionWith(seal.PlacementCells);
                type.GetMethod("ApplyMechanicPreviewAttack", flags).Invoke(window, null);
                Assert.AreEqual(1, type.GetField("mechanicPreviewHealth", flags).GetValue(window));
                type.GetMethod("ApplyMechanicPreviewAttack", flags).Invoke(window, null);
                Assert.AreEqual(0, type.GetField("mechanicPreviewHealth", flags).GetValue(window));
                Assert.AreEqual(arenaBefore, JsonUtility.ToJson(boss.arena)); Assert.AreEqual(attackBefore, JsonUtility.ToJson(attack));
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }
    }
}
#endif
