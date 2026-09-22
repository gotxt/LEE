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
        private void ActivateAll(BossMechanicSession session) => session.ResolvePlayerAttack(seal.PlacementCells.ToArray(), 150, 0);
        private int Warnings => host.GetOrderedMarks().Count(m => m.Layer == PatternPreviewHost.PreviewLayer.Warning);

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
        public void FinalActivationAttackRetainsHealthOneAndNextAttackCanKill()
        {
            using (var session = Session())
            {
                var runtime = (CrystalSealRuntime)session.Runtimes[0];
                Assert.AreEqual(1, session.ResolvePlayerAttack(new Vector2Int[0], 150, 999));
                Assert.AreEqual(0, runtime.ActiveCount);
                StringAssert.EndsWith("0/2", session.Status);
                Assert.AreEqual(1, session.ResolvePlayerAttack(new[] { seal.crystals[0].cell }, 1, 999));
                Assert.AreEqual(1, runtime.ActiveCount);
                Assert.AreEqual(1, session.ResolvePlayerAttack(new[] { seal.crystals[1].cell }, 1, 999));
                Assert.AreEqual(2, runtime.ActiveCount);
                StringAssert.EndsWith("2/2", session.Status);
                Assert.AreEqual(0, session.MinimumBossHealth);
                Assert.AreEqual(0, session.ResolvePlayerAttack(new[] { host.CenterCell }, 1, 1));
            }
        }

        [Test]
        public void OneCompletedAttackCanActivateAllWithoutChangingNormalDamageOrKillingHealthyBoss()
        {
            using (var session = Session())
            {
                int health = session.ResolvePlayerAttack(seal.crystals.Select(d => d.cell).ToArray(), 150, 10);
                Assert.AreEqual(140, health);
                Assert.AreEqual(2, ((CrystalSealRuntime)session.Runtimes[0]).ActiveCount);
                Assert.AreEqual(139, session.ResolvePlayerAttack(new Vector2Int[0], health, 1));
            }
        }

        [Test]
        public void InactiveCrystalsStaySilentAndWalkableUntilACompletedAttack()
        {
            using (var session = Session())
            {
                foreach (var crystal in seal.crystals)
                { host.player = crystal.cell; session.Advance(30); CollectionAssert.Contains(host.Traversable, crystal.cell); }
                Assert.AreEqual(0, ((CrystalSealRuntime)session.Runtimes[0]).ActiveCount);
                Assert.AreEqual(0, Warnings);
                Assert.IsEmpty(host.log);
                Assert.IsTrue(host.marks.Values.All(m => m.Color == seal.inactiveTint));
                CollectionAssert.AreEquivalent(seal.PlacementCells, session.RequiredCells);
            }
        }

        [Test]
        public void ZeroInitialDelayStillRequiresActivationBeforeTheFirstAttack()
        {
            seal.initialDelay = 0;
            using (var session = Session())
            {
                session.Advance(30);
                Assert.AreEqual(0, Warnings);
                session.ResolvePlayerAttack(new[] { seal.crystals[0].cell }, 150, 0);
                session.Advance(0);
                Assert.AreEqual(1, Warnings);
                session.ResolvePlayerAttack(new[] { seal.crystals[0].cell }, 150, 0);
                session.Advance(0);
                Assert.AreEqual(1, Warnings);
            }
            Assert.IsEmpty(host.marks);
        }

        [Test]
        public void ActivationStartsEachDevicesOwnDelayAndAllActivatedDevicesKeepAttacking()
        {
            using (var session = Session())
            {
                session.Advance(30);
                session.ResolvePlayerAttack(new[] { seal.crystals[0].cell }, 150, 0);
                session.Advance(.5f);
                session.ResolvePlayerAttack(new[] { seal.crystals[1].cell }, 150, 0);
                session.Advance(.6f);
                Assert.AreEqual(1, Warnings, "Only the first device has waited its full initial delay.");
                session.Advance(.5f);
                var warnings = host.GetOrderedMarks().Where(m => m.Layer == PatternPreviewHost.PreviewLayer.Warning).ToArray();
                Assert.AreEqual(2, warnings.Length);
                foreach (var device in seal.crystals)
                    using (var context = new PatternContext(host, device.cell))
                        Assert.IsTrue(warnings.Any(m => m.Cells.SetEquals(((WarningEvent)attack.clips[0].action).tiles.Resolve(context))));
                session.Advance(4);
                Assert.AreEqual(2, Warnings, "Completing the objective must not stop either device's repeating attack.");
                Assert.AreEqual(2, ((CrystalSealRuntime)session.Runtimes[0]).ActiveCount);
                Assert.AreEqual(0, session.MinimumBossHealth);
            }
            Assert.IsEmpty(host.marks);
        }

        [Test]
        public void HittingAnActiveDeviceAgainDoesNotRestartItsDelayOrReplaceItsVisualOrAttack()
        {
            using (var session = Session())
            {
                var cell = new[] { seal.crystals[0].cell };
                session.ResolvePlayerAttack(cell, 150, 0);
                session.Advance(.5f);
                var visuals = host.marks.Keys.ToArray();
                session.ResolvePlayerAttack(cell, 150, 0);
                CollectionAssert.AreEquivalent(visuals, host.marks.Keys);
                session.Advance(.6f);
                Assert.AreEqual(1, Warnings, "Repeated activation must not postpone the first attack.");
                var resources = host.marks.Keys.ToArray();
                session.ResolvePlayerAttack(cell, 150, 0);
                CollectionAssert.AreEquivalent(resources, host.marks.Keys, "Repeated hits must keep the in-flight attack and visual.");
                Assert.AreEqual(1, ((CrystalSealRuntime)session.Runtimes[0]).ActiveCount);
                CollectionAssert.AreEqual(new[] { seal.crystals[1].cell }, session.RequiredCells);
            }
            Assert.IsEmpty(host.marks);
        }

        [Test]
        public void PreviewWallsProtectOnlyCrystalsStillNeededForActivation()
        {
            using (var session = Session())
            {
                host.RequiredCellsProvider = () => session.RequiredCells;
                using (host.Block(seal.PlacementCells.ToArray()))
                    foreach (var cell in seal.PlacementCells) CollectionAssert.Contains(host.Traversable, cell);
                session.ResolvePlayerAttack(new[] { seal.crystals[0].cell }, 150, 0);
                using (host.Block(seal.PlacementCells.ToArray()))
                {
                    CollectionAssert.DoesNotContain(host.Traversable, seal.crystals[0].cell);
                    CollectionAssert.Contains(host.Traversable, seal.crystals[1].cell);
                }
            }
            host.RequiredCellsProvider = null;
            Assert.IsEmpty(host.marks);
        }

        [Test]
        public void DeviceTimingOverridesDoNotChangeSharedAttackData()
        {
            seal.crystals[1].overrideTiming = true; seal.crystals[1].initialDelay = 3; seal.crystals[1].interval = 4;
            string before = EditorJsonUtility.ToJson(boss);
            using (var session = Session())
            {
                session.Advance(20);
                ActivateAll(session);
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
            using (var session = Session()) { ActivateAll(session); session.Advance(14.25f); }
            int coarse = host.log.Count(s => s.StartsWith("Signal:")); host.log.Clear();
            using (var session = Session()) { ActivateAll(session); for (int i = 0; i < 114; i++) session.Advance(.125f); }
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
                Assert.AreEqual(7, ((CrystalSealRuntime)session.Runtimes[0]).Devices.Count);
                Assert.AreEqual(0, ((CrystalSealRuntime)session.Runtimes[0]).ActiveCount);
                ActivateAll(session);
                session.Advance(1.1f);
                Assert.AreEqual(7, Warnings);
            }
            Assert.IsEmpty(host.marks);
            using (var restarted = Session())
            {
                Assert.AreEqual(0, ((CrystalSealRuntime)restarted.Runtimes[0]).ActiveCount);
                Assert.AreEqual(7, restarted.RequiredCells.Count());
                restarted.Advance(30);
                Assert.AreEqual(0, Warnings);
            }
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

        [TestCase("construction")]
        [TestCase("activation")]
        [TestCase("attack")]
        public void HostFailuresReleaseEveryDeviceAndInFlightAttack(string stage)
        {
            var failing = new FaultHost(host);
            if (stage == "construction")
            {
                failing.FailDeviceOnCall = 2;
                Assert.Throws<System.InvalidOperationException>(() =>
                    new BossMechanicSession(boss.phases[0].mechanics, new MechanicContext(failing, boss.FindPattern)));
            }
            else
            {
                using (var session = new BossMechanicSession(boss.phases[0].mechanics, new MechanicContext(failing, boss.FindPattern)))
                {
                    if (stage == "activation")
                    {
                        failing.FailDeviceOnCall = 4; // One activation succeeds before the second fails.
                        Assert.Throws<System.InvalidOperationException>(() => ActivateAll(session));
                    }
                    else
                    {
                        ActivateAll(session);
                        failing.FailWarningOnCall = 2; // The first device already owns its warning.
                        Assert.Throws<System.InvalidOperationException>(() => session.Advance(1.1f));
                    }
                    Assert.IsEmpty(session.Runtimes);
                    Assert.IsEmpty(host.marks, "Failure cleanup must happen before the caller disposes the session.");
                    session.Dispose();
                }
            }
            Assert.IsEmpty(host.marks);
        }

        private sealed class FaultHost : IPatternHost, IMechanicPresentationHost
        {
            private readonly PatternPreviewHost inner;
            private int deviceCalls, warningCalls;
            public int FailDeviceOnCall, FailWarningOnCall;
            public FaultHost(PatternPreviewHost inner) { this.inner = inner; }
            public Vector2Int PlayerCell => inner.PlayerCell;
            public Vector2Int CenterCell => inner.CenterCell;
            public IReadOnlyCollection<Vector2Int> Walkable => inner.Walkable;
            public IReadOnlyCollection<Vector2Int> Traversable => inner.Traversable;
            public bool IsAlive => inner.IsAlive;
            public IPatternLease ShowDevice(Vector2Int cell, GameObject prefab, Sprite sprite, Color tint)
            {
                if (++deviceCalls == FailDeviceOnCall) throw new System.InvalidOperationException("Injected device presentation failure.");
                return inner.ShowDevice(cell, prefab, sprite, tint);
            }
            public IPatternLease Mark(IReadOnlyCollection<Vector2Int> cells, Color color, bool warning)
            {
                if (warning && ++warningCalls == FailWarningOnCall) throw new System.InvalidOperationException("Injected warning failure.");
                return inner.Mark(cells, color, warning);
            }
            public IPatternLease Block(IReadOnlyCollection<Vector2Int> cells) => inner.Block(cells);
            public IPatternLease Hazard(IReadOnlyCollection<Vector2Int> cells, string reason) => inner.Hazard(cells, reason);
            public IPatternLease Spawn(string key, GameObject prefab, Vector2Int cell, Sprite sprite, Color color) => inner.Spawn(key, prefab, cell, sprite, color);
            public IPatternLease Sound(AudioClip clip, float volume) => inner.Sound(clip, volume);
            public IPatternLease Motion(string key, Vector2Int target) => inner.Motion(key, target);
            public IPatternLease Camera(Vector2 offset, float shake) => inner.Camera(offset, shake);
            public void Damage(string reason) => inner.Damage(reason);
            public void Signal(string name, string argument) => inner.Signal(name, argument);
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
                var initialPreview = (BossMechanicSession)type.GetField("mechanicPreview", flags).GetValue(window);
                var initialHost = (PatternPreviewHost)type.GetField("previewHost", flags).GetValue(window);
                Assert.AreEqual(0, ((CrystalSealRuntime)initialPreview.Runtimes.Single()).ActiveCount);
                trail.UnionWith(seal.PlacementCells);
                type.GetMethod("ApplyMechanicPreviewAttack", flags).Invoke(window, null);
                Assert.AreEqual(1, type.GetField("mechanicPreviewHealth", flags).GetValue(window));
                Assert.AreEqual(2, ((CrystalSealRuntime)initialPreview.Runtimes.Single()).ActiveCount);
                Assert.IsNull(type.GetField("previewError", flags).GetValue(window));
                Assert.IsNotEmpty((string)type.GetField("mechanicPreviewResult", flags).GetValue(window));
                type.GetMethod("ApplyMechanicPreviewAttack", flags).Invoke(window, null);
                Assert.AreEqual(0, type.GetField("mechanicPreviewHealth", flags).GetValue(window));
                Assert.IsNull(type.GetField("mechanicPreview", flags).GetValue(window));
                Assert.IsEmpty(initialHost.marks);
                Assert.IsNull(initialHost.RequiredCellsProvider);
                type.GetMethod("RebuildPreview", flags).Invoke(window, null);
                var restarted = (BossMechanicSession)type.GetField("mechanicPreview", flags).GetValue(window);
                Assert.AreEqual(0, ((CrystalSealRuntime)restarted.Runtimes.Single()).ActiveCount);
                Assert.AreEqual(2, restarted.RequiredCells.Count());
                Assert.AreEqual(arenaBefore, JsonUtility.ToJson(boss.arena)); Assert.AreEqual(attackBefore, JsonUtility.ToJson(attack));
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }
    }
}
#endif
