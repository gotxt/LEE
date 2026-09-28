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
    public sealed class MechanicOutputTests
    {
        [Serializable] public sealed class TestMechanic : BossMechanicDefinition
        {
            public List<Vector2Int> cells = new List<Vector2Int>();
            [NonSerialized] public TestRuntime live;
            public override IReadOnlyList<MechanicPositionOutput> PositionOutputs => new[] { new MechanicPositionOutput("positions", "시험 장치 위치") };
            public override BossMechanicRuntime Create(MechanicContext context) => live = new TestRuntime(cells);
            public override void Validate(BossEncounterDefinition boss, List<string> errors) { }
        }
        public sealed class TestRuntime : BossMechanicRuntime
        {
            public readonly List<Vector2Int> cells;
            public bool disposed;
            public TestRuntime(IEnumerable<Vector2Int> cells) { this.cells = cells.ToList(); }
            public override IEnumerable<Vector2Int> ReadPositions(string key) => key == "positions" ? cells : base.ReadPositions(key);
            public override void Advance(float delta) { }
            public override void Dispose() { disposed = true; cells.Clear(); }
        }
        [Serializable] public sealed class ThrowOnSecondOriginEvent : PatternEvent
        {
            private sealed class ThrowAction : PatternAction
            {
                public Vector2Int origin;
                public override void Begin() { if (origin.x == 10) throw new InvalidOperationException("test child failure"); }
            }
            public override PatternAction Create(PatternContext context, float duration) => new ThrowAction { origin = context.Origin };
        }
        private BossEncounterDefinition boss;
        private PatternPreviewHost host;
        private BossMechanicSession session;
        private TestMechanic first, second;
        private EncounterPattern child, parent;
        private CallAtMechanicPositionsEvent call;
        private PatternRunner runner;

        public static EncounterPattern Attack(string id = "child")
        {
            TileSelection Tiles() => new TileSelection { anchor = TileAnchor.Origin, shape = TileShape.Cells,
                cells = new List<Vector2Int> { Vector2Int.zero, Vector2Int.up }, snapshotKey = "shared-attack-key", ensureEscape = false };
            return new EncounterPattern { id = id, name = "호출 위치 주변", minimumDuration = 1.3f, clips = new List<PatternClip>
            {
                new PatternClip { start = 0, duration = 1, action = new WarningEvent { tiles = Tiles() } },
                new PatternClip { start = 1, duration = .3f, action = new DamageEvent { tiles = Tiles() } },
                new PatternClip { start = 1, duration = .3f, action = new VfxEvent { tiles = Tiles() } }
            } };
        }
        [SetUp] public void Setup()
        {
            boss = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            first = new TestMechanic { name = "같은 이름", connectionId = "first", cells = new List<Vector2Int> { new Vector2Int(6, 8), new Vector2Int(10, 8) } };
            second = new TestMechanic { name = "같은 이름", connectionId = "second", cells = new List<Vector2Int> { new Vector2Int(8, 10) } };
            boss.phases[0].mechanics.Add(first); boss.phases[0].mechanics.Add(second);
            child = Attack(); boss.libraryPatterns.Add(child);
            call = new CallAtMechanicPositionsEvent { mechanicId = "first", outputKey = "positions", patternId = child.id };
            parent = new EncounterPattern { id = "parent", clips = new List<PatternClip> { new PatternClip { action = call, duration = 1.3f } } };
            boss.phases[0].patterns.Clear(); boss.phases[0].patterns.Add(parent);
            host = new PatternPreviewHost(boss) { player = new Vector2Int(8, 5) };
            session = new BossMechanicSession(boss.phases[0].mechanics, new MechanicContext(host, boss.FindPattern));
            host.MechanicOutputs = session;
        }
        private PatternRunner Run() => runner = new PatternRunner(parent, new PatternContext(host, Vector2Int.zero, 0, boss.FindPattern));
        [TearDown] public void TearDown() { runner?.Dispose(); session?.Dispose(); host?.Dispose(); Object.DestroyImmediate(boss); }

        [Test] public void SameTypeAndSameNameMechanicsStayIndependentAcrossRenameAndReorder()
        {
            first.name = "변경"; boss.phases[0].mechanics.Reverse();
            Assert.IsEmpty(boss.ValidateDefinition());
            CollectionAssert.AreEquivalent(first.cells, session.CaptureMechanicPositions("first", "positions"));
            CollectionAssert.AreEquivalent(second.cells, session.CaptureMechanicPositions("second", "positions"));
            call.mechanicId = "second"; Run().Advance(0);
            Assert.AreEqual(1, host.marks.Count); Assert.Contains(second.cells[0], host.marks.Values.Single().Cells.ToArray());
        }
        [Test] public void SnapshotsAtEventStartAndIndependentChildrenKeepWarningDamageAndVfxAreas()
        {
            parent.clips[0].start = .5f;
            Run().Advance(.25f); Assert.IsEmpty(host.marks);
            first.live.cells[0] = new Vector2Int(5, 8);
            runner.Advance(.25f);
            var warnings = host.marks.Values.Select(m => m.Cells.ToArray()).ToArray(); Assert.AreEqual(2, warnings.Length);
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(5, 8), new Vector2Int(5, 9), new Vector2Int(10, 8), new Vector2Int(10, 9) }, warnings.SelectMany(x => x).ToArray());
            first.live.cells.Clear(); first.live.cells.Add(new Vector2Int(8, 12));
            runner.Advance(1);
            var damage = host.GetOrderedMarks().Where(m => m.Layer == PatternPreviewHost.PreviewLayer.Damage).ToArray();
            Assert.AreEqual(2, damage.Length);
            CollectionAssert.AreEquivalent(warnings.SelectMany(x => x).ToArray(), damage.SelectMany(x => x.Cells).ToArray());
            CollectionAssert.AreEquivalent(warnings.SelectMany(x => x).ToArray(), host.GetOrderedMarks().Where(m => m.Layer == PatternPreviewHost.PreviewLayer.Effect).SelectMany(m => m.Cells).ToArray());
            runner.Advance(.31f); Assert.IsEmpty(host.marks);
            parent.clips[0].start = 0; Run().Advance(0);
            Assert.AreEqual(1, host.marks.Count); Assert.Contains(new Vector2Int(8, 12), host.marks.Values.Single().Cells.ToArray());
        }
        [Test] public void EmptyIsNoOpButMissingAndUnknownOutputsFailClearly()
        {
            first.live.cells.Clear(); Run().Advance(2); Assert.IsEmpty(host.marks);
            call.mechanicId = "missing"; Run(); Assert.Throws<InvalidOperationException>(() => runner.Advance(0));
            call.mechanicId = "first"; call.outputKey = "wrong"; Run(); Assert.Throws<InvalidOperationException>(() => runner.Advance(0));
            call.outputKey = "positions"; session.Dispose(); Run(); Assert.Throws<InvalidOperationException>(() => runner.Advance(0));
        }
        [Test] public void DuplicatePositionsAreDeduplicatedOffsetAppliedAndCancellationReleasesAllChildren()
        {
            first.live.cells.Add(first.cells[0]); call.originOffset = Vector2Int.left;
            Run().Advance(.1f); Assert.AreEqual(2, host.marks.Count);
            Assert.IsTrue(host.marks.Values.Any(m => m.Cells.Contains(first.cells[0] + Vector2Int.left)));
            runner.Dispose(); Assert.IsEmpty(host.marks);
            Run().Advance(1.1f); Assert.AreEqual(2, host.marks.Values.Count(m => m.Layer == PatternPreviewHost.PreviewLayer.Damage));
            runner.Dispose(); Assert.IsEmpty(host.marks);
        }
        [Test] public void TwoIndependentMechanicEventsCanShareOneChildPatternSimultaneously()
        {
            parent.clips.Add(new PatternClip { duration = child.Duration, action = new CallAtMechanicPositionsEvent
                { mechanicId = "second", outputKey = "positions", patternId = child.id } });
            Assert.IsEmpty(boss.ValidateDefinition()); Run().Advance(0);
            Assert.AreEqual(3, host.marks.Count);
            var expected = first.cells.Concat(second.cells).SelectMany(c => new[] { c, c + Vector2Int.up }).ToArray();
            CollectionAssert.AreEquivalent(expected, host.marks.Values.SelectMany(m => m.Cells).ToArray());
            runner.Advance(1);
            CollectionAssert.AreEquivalent(expected, host.marks.Values.Where(m => m.Layer == PatternPreviewHost.PreviewLayer.Damage).SelectMany(m => m.Cells).ToArray());
            runner.Dispose(); Assert.IsEmpty(host.marks);
        }
        [Test] public void PartialChildFailureCleansEarlierChildren()
        {
            child.clips.Add(new PatternClip { action = new ThrowOnSecondOriginEvent(), duration = 1 });
            Run(); Assert.Throws<InvalidOperationException>(() => runner.Advance(0)); Assert.IsEmpty(host.marks);
        }
        [TestCase("missing")] [TestCase("disabled")] [TestCase("port")] [TestCase("phase")] [TestCase("duplicate")]
        public void BrokenBindingsAreValidatedBeforeBattle(string issue)
        {
            switch (issue)
            {
                case "missing": boss.phases[0].mechanics.Remove(first); break;
                case "disabled": first.enabled = false; break;
                case "port": call.outputKey = "unknown"; break;
                case "phase": boss.phases.Add(new BossPhaseDefinition()); boss.phases[0].mechanics.Remove(first); boss.phases[1].mechanics.Add(first); break;
                case "duplicate": second.connectionId = first.connectionId; break;
            }
            Assert.IsNotEmpty(boss.ValidateDefinition());
        }
        [Test] public void DurationMissingChildRecursionAndSharedAssetMisuseAreValidated()
        {
            parent.clips[0].duration = .5f; Assert.That(PatternValidation.Errors(parent, boss.FindPattern).Any(e => e.Contains("shorter")));
            parent.clips[0].duration = 1.3f; call.patternId = "absent"; Assert.IsNotEmpty(PatternValidation.Errors(parent, boss.FindPattern));
            call.patternId = parent.id; Assert.That(PatternValidation.Errors(parent, boss.FindPattern).Any(e => e.Contains("recursive")));
            var shared = ScriptableObject.CreateInstance<PatternSequence>();
            try { shared.clips.Add(new PatternClip { action = call, duration = 2 }); Assert.IsNotEmpty(PatternValidation.Errors(shared)); }
            finally { Object.DestroyImmediate(shared); }
        }
        [Test] public void DuplicateSessionIdsCleanAllCreatedRuntimesAndLegacyUnlinkedMechanicsStillRun()
        {
            session.Dispose(); second.connectionId = first.connectionId;
            Assert.Throws<InvalidOperationException>(() => new BossMechanicSession(boss.phases[0].mechanics, new MechanicContext(host, boss.FindPattern)));
            Assert.IsTrue(first.live.disposed); Assert.IsTrue(second.live.disposed);
            first.connectionId = second.connectionId = "";
            using var legacy = new BossMechanicSession(boss.phases[0].mechanics, new MechanicContext(host, boss.FindPattern));
            Assert.AreEqual(2, legacy.Runtimes.Count);
        }
        [Test] public void MatureOutputExcludesImmatureAndCrystalUsesSameContract()
        {
            var growth = new RegionGrowthMechanic { connectionId = "growth", seedsPerSpawn = 1, overgrownTile = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Vines"), regionIds = new List<string> { "test-region" } };
            boss.tileRegions.Add(new EncounterTileRegion { id = "test-region", cells = new List<Vector2Int> { new Vector2Int(7, 7), new Vector2Int(9, 9) } });
            // Host captures the shared region list; new runtime state remains independent.
            var crystals = new CrystalSealMechanic { connectionId = "crystals", attackPatternId = child.id, crystals = new List<CrystalPlacement> { new CrystalPlacement { cell = new Vector2Int(4, 8) } } };
            using var both = new BossMechanicSession(new BossMechanicDefinition[] { growth, crystals }, new MechanicContext(host, boss.FindPattern));
            both.Advance(24.9f); Assert.IsEmpty(both.CaptureMechanicPositions("growth", RegionGrowthMechanic.MaturePositions));
            both.Advance(.2f); Assert.AreEqual(1, both.CaptureMechanicPositions("growth", RegionGrowthMechanic.MaturePositions).Count);
            Assert.IsEmpty(both.CaptureMechanicPositions("crystals", CrystalSealMechanic.ActivePositions));
            both.ResolvePlayerAttack(crystals.PlacementCells.ToArray(), 150, 0);
            CollectionAssert.AreEqual(crystals.PlacementCells.ToArray(), both.CaptureMechanicPositions("crystals", CrystalSealMechanic.ActivePositions));
        }
        [Test] public void ConnectionIdAssignmentUndoAndSerializationDoNotDependOnNameOrIndex()
        {
            const string path = "Assets/MechanicOutputs_RoundTrip_Test.asset";
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(path));
            var copy = Object.Instantiate(boss);
            try
            {
                var source = copy.phases[0].mechanics[0]; source.connectionId = "";
                string id = BossEncounterEditorWindow.EnsureMechanicConnection(copy, source);
                Assert.AreEqual(id, BossEncounterEditorWindow.EnsureMechanicConnection(copy, source));
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.IsEmpty(copy.phases[0].mechanics[0].connectionId);
                source = copy.phases[0].mechanics[0]; id = BossEncounterEditorWindow.EnsureMechanicConnection(copy, source);
                var output = (CallAtMechanicPositionsEvent)copy.phases[0].patterns[0].clips[0].action; output.mechanicId = id;
                AssetDatabase.CreateAsset(copy, path); AssetDatabase.SaveAssetIfDirty(copy); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(path);
                Assert.AreEqual(id, loaded.phases[0].mechanics[0].connectionId);
                Assert.AreEqual(id, ((CallAtMechanicPositionsEvent)loaded.phases[0].patterns[0].clips[0].action).mechanicId);
                Assert.IsEmpty(loaded.ValidateDefinition());
            }
            finally { AssetDatabase.DeleteAsset(path); if (copy != null && !AssetDatabase.Contains(copy)) Object.DestroyImmediate(copy); }
        }
        [UnityTest] public IEnumerator EditorShowsSelectorsAndWarmupFeedsRealSnapshotWithoutSavingState()
        {
            var window = ScriptableObject.CreateInstance<BossEncounterEditorWindow>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object Call(string method, params object[] args) => typeof(BossEncounterEditorWindow).GetMethod(method, flags).Invoke(window, args);
            T Get<T>(string field) => (T)typeof(BossEncounterEditorWindow).GetField(field, flags).GetValue(window);
            string before = JsonUtility.ToJson(boss);
            try
            {
                window.position = new Rect(30, 30, 1500, 1000); window.Show(); Call("SelectEncounter", boss);
                var node = typeof(BossEncounterEditorWindow).GetNestedType("NodeKind", BindingFlags.NonPublic);
                Call("SelectNode", Enum.Parse(node, "Pattern"), 0, 0);
                typeof(BossEncounterEditorWindow).GetField("attackEditorMode", flags).SetValue(window, 1);
                typeof(BossEncounterEditorWindow).GetField("selectedClip", flags).SetValue(window, 0);
                window.Repaint(); yield return null; yield return null;
                Assert.IsNull(Get<string>("previewError"));
                Assert.AreEqual(2, Get<PatternPreviewHost>("previewHost").marks.Count);
                Assert.IsNotNull(Get<BossMechanicSession>("patternMechanicPreview"));
                Call("RebuildPreview"); Assert.AreEqual(2, Get<PatternPreviewHost>("previewHost").marks.Count);
                Assert.AreEqual(before, JsonUtility.ToJson(boss)); LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }
    }
}
#endif
