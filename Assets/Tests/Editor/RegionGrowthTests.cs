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
    public sealed class RegionGrowthTests
    {
        private BossEncounterDefinition boss;
        private PatternPreviewHost host;
        private RegionGrowthMechanic definition;
        private RegionGrowthRuntime runtime;
        [SetUp] public void SetUp()
        {
            boss = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            boss.arena.size = 17;
            definition = new RegionGrowthMechanic { overgrownTile = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Vines") };
            for (int i = 0; i < 7; i++)
            {
                string id = "r" + i;
                boss.tileRegions.Add(new EncounterTileRegion { id = id, name = id,
                    cells = new List<Vector2Int> { new Vector2Int(5 + i, 8) } });
                definition.regionIds.Add(id);
                if (i < 6) definition.enrageWhenAll.Add(id); else definition.enrageWhenAny.Add(id);
            }
            host = new PatternPreviewHost(boss) { player = new Vector2Int(8, 7) };
        }
        private RegionGrowthRuntime Start(int seed = 42) => runtime = new RegionGrowthRuntime(definition, new MechanicContext(host, boss.FindPattern), seed);
        [TearDown] public void TearDown() { runtime?.Dispose(); host?.Dispose(); Object.DestroyImmediate(boss); }

        [TestCase(64, 20)] [TestCase(112, 34)] [TestCase(114, 35)] [TestCase(118, 36)]
        [TestCase(106, 32)] [TestCase(100, 30)] [TestCase(2500, 750)]
        public void ThirtyPercentRoundsUpWithoutFloatingPointOffByOne(int cells, int expected)
        { Assert.AreEqual(expected, RegionGrowthMechanic.Threshold(cells, .3f)); }

        [Test] public void InitialBatchGrowthBoundaryUniqueWalkableAndPassable()
        {
            Start(); runtime.Advance(4.99f); Assert.IsEmpty(runtime.Seeds);
            runtime.Advance(.02f); Assert.AreEqual(5, runtime.Seeds.Count);
            Assert.AreEqual(5, runtime.Seeds.Select(s => s.Cell).Distinct().Count());
            Assert.IsTrue(runtime.Seeds.All(s => host.Traversable.Contains(s.Cell) && s.Cell != host.player));
            runtime.Advance(19.98f); Assert.IsFalse(runtime.Seeds.Any(s => s.Mature));
            runtime.Advance(.02f); Assert.AreEqual(5, runtime.Seeds.Count(s => s.Mature));
            Assert.IsTrue(runtime.Seeds.All(s => host.Traversable.Contains(s.Cell)));
        }
        [Test] public void OneLargeAdvanceEqualsSmallFramesAndFullMapDoesNotHang()
        {
            Start(); runtime.Advance(200);
            var expected = runtime.Seeds.Select(s => (s.Cell, s.Mature, s.MaturesAt)).ToArray();
            runtime.Dispose(); host.Dispose(); host = new PatternPreviewHost(boss) { player = new Vector2Int(8, 7) };
            Start(); for (int i = 0; i < 800; i++) runtime.Advance(.25f);
            CollectionAssert.AreEquivalent(expected, runtime.Seeds.Select(s => (s.Cell, s.Mature, s.MaturesAt)).ToArray());
            Assert.AreEqual(7, runtime.Seeds.Count);
        }
        [Test] public void FireBurnsMultipleImmatureIncludingLastMoveButNotMatureOrTrailAttack()
        {
            Start(); runtime.Advance(5); var cells = runtime.Seeds.Select(s => s.Cell).ToArray();
            runtime.OnPlayerStep(new PlayerTileStep(cells[0] - Vector2Int.up, cells[0], false));
            runtime.OnPlayerAttack(cells); Assert.AreEqual(5, runtime.Seeds.Count);
            var flame = ScriptableObject.CreateInstance<SpecialTileDefinition>(); flame.fireMoveCount = 2;
            var state = new SpecialTilePlayerState();
            try
            {
                state.Enter(Vector2Int.zero, Vector2Int.up, flame);
                foreach (var cell in cells.Take(2)) runtime.OnPlayerStep(state.Enter(cell - Vector2Int.up, cell, null));
                Assert.AreEqual(0, state.FireMovesRemaining); Assert.AreEqual(3, runtime.Seeds.Count);
                runtime.Advance(20);
                var mature = runtime.Seeds.First(s => s.Mature);
                runtime.OnPlayerStep(new PlayerTileStep(mature.Cell - Vector2Int.up, mature.Cell, true));
                Assert.Contains(mature, runtime.Seeds.ToArray());
            }
            finally { Object.DestroyImmediate(flame); }
        }
        [Test] public void CentralAloneEnragesAndOverridesFireThenDisposeRestoresIt()
        {
            var central = boss.tileRegions[6].cells[0];
            var fire = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Fire");
            using var baseFire = host.PlaceSpecialTiles(fire, new[] { central });
            using var walls = host.Block(boss.tileRegions.Take(6).SelectMany(r => r.cells).ToArray());
            Start(); runtime.Advance(25);
            Assert.IsTrue(runtime.IsEnraged); Assert.AreEqual(1, runtime.Regions.Count(r => r.Overgrown));
            host.player = central - Vector2Int.up;
            Assert.IsTrue(host.TryStep(Vector2Int.up, out var step)); Assert.IsFalse(step.HasFireOnArrival);
            Assert.AreEqual(1, host.TileState.StunRemaining); Assert.AreEqual(0, host.FireMovesRemaining);
            runtime.Dispose(); host.TileState.Reset(); host.player = central - Vector2Int.up;
            Assert.IsTrue(host.TryStep(Vector2Int.up, out step));
            // The restored shared fire now starts inactive; the unchanged editor has no attack ignition simulation.
            Assert.AreEqual(0, host.FireMovesRemaining); Assert.IsFalse(step.HasFireOnArrival);
            Assert.AreEqual(fire.color, host.GetOrderedMarks().First().Color);
            Assert.IsEmpty(host.marks.Where(m => m.Value.Layer == PatternPreviewHost.PreviewLayer.Obstacle && m.Value.Color == definition.matureTint));
        }
        [Test] public void FivePetalsDoNotEnrageSixDoWithoutCentral()
        {
            using var wall = host.Block(boss.tileRegions[6].cells);
            Start(); runtime.Advance(25); Assert.AreEqual(5, runtime.Regions.Count(r => r.Overgrown)); Assert.IsFalse(runtime.IsEnraged);
            runtime.Advance(5); Assert.IsTrue(runtime.IsEnraged); Assert.IsFalse(runtime.Regions[6].Overgrown);
        }
        [Test] public void EmptyEnrageGroupsNeverTriggerAndDataIsNotMutated()
        {
            definition.enrageWhenAll.Clear(); definition.enrageWhenAny.Clear();
            string before = JsonUtility.ToJson(definition); Start(); runtime.Advance(100);
            Assert.IsFalse(runtime.IsEnraged); Assert.AreEqual(before, JsonUtility.ToJson(definition));
            Assert.AreEqual(0, runtime.MinimumBossHealth);
        }
        [Test] public void InvalidRegionsOverlapSettingsAndDeletedReferencesAreRejected()
        {
            var errors = new List<string>(); definition.Validate(boss, errors); Assert.IsEmpty(errors);
            boss.tileRegions[1].cells.Add(boss.tileRegions[0].cells[0]); definition.Validate(boss, errors);
            Assert.That(errors.Any(e => e.Contains("겹치지")));
            boss.tileRegions[1].cells.RemoveAt(1); definition.regionIds.Add("missing");
            errors.Clear(); definition.Validate(boss, errors); Assert.That(errors.Any(e => e.Contains("없는 구역")));
            definition.interval = float.NaN; Assert.Throws<InvalidOperationException>(() => Start());
        }
        [Test] public void DenominatorDoesNotShrinkWithTemporaryWallsAndThirtyPercentConvertsWholeRegion()
        {
            definition.regionIds = new List<string> { "r0" }; definition.enrageWhenAll.Clear(); definition.enrageWhenAny.Clear();
            boss.tileRegions[0].cells = Enumerable.Range(4, 10).Select(x => new Vector2Int(x, 8)).ToList();
            using var blocked = host.Block(boss.tileRegions[0].cells.Take(7).ToArray());
            Start(); Assert.AreEqual(3, runtime.Regions[0].Threshold); runtime.Advance(25);
            Assert.AreEqual(3, runtime.Seeds.Count); Assert.IsTrue(runtime.Regions[0].Overgrown);
            Assert.AreEqual(10, host.GetOrderedMarks().Single(m => m.Layer == PatternPreviewHost.PreviewLayer.SpecialTile).Cells.Count);
        }
        [Test] public void SavedBossUsesSevenRegionsAndOriginalPatternWithNoAutoFirePlacement()
        {
            var saved = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            Assert.IsEmpty(saved.ValidateDefinition());
            var growth = saved.phases[0].mechanics.OfType<RegionGrowthMechanic>().Single();
            Assert.AreEqual(7, growth.regionIds.Count); Assert.AreEqual(6, growth.enrageWhenAll.Count);
            Assert.AreEqual("97821fa0658f4977bf2903e110a25f20", growth.enrageWhenAny.Single());
            Assert.AreEqual(5, growth.initialDelay); Assert.AreEqual(5, growth.interval);
            Assert.AreEqual(5, growth.seedsPerSpawn); Assert.AreEqual(20, growth.growthSeconds);
            Assert.AreEqual(.3f, growth.coverage); Assert.IsNotNull(growth.overgrownTile);
            Assert.IsEmpty(saved.arena.specialTiles); Assert.AreEqual(1, saved.phases[0].patterns.Count);
            var pattern = saved.phases[0].patterns[0]; Assert.AreEqual("p1-petal-collapse", pattern.id);
            Assert.AreEqual(2.5f, pattern.clips[0].duration); Assert.AreEqual(PatternCombatCondition.Always, pattern.combatCondition);
        }
        [Test] public void ConditionCloneUndoAndAssetRoundTrip()
        {
            const string path = "Assets/RegionGrowthRoundTrip_Test.asset";
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(path));
            var copy = Object.Instantiate(boss);
            try
            {
                copy.phases[0].mechanics.Add(definition);
                var pattern = copy.phases[0].patterns[0]; pattern.combatCondition = PatternCombatCondition.Enraged;
                var cloned = (EncounterPattern)typeof(BossEncounterEditorWindow).GetMethod("ClonePattern", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { pattern });
                Assert.IsTrue(cloned.CanSchedule(true)); Assert.IsFalse(cloned.CanSchedule(false));
                Assert.AreEqual(pattern.combatCondition, cloned.combatCondition);
                AssetDatabase.CreateAsset(copy, path); AssetDatabase.SaveAssetIfDirty(copy);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(path);
                var growth = (RegionGrowthMechanic)loaded.phases[0].mechanics[0];
                Assert.AreEqual(7, growth.regionIds.Count); Assert.AreEqual(20, growth.growthSeconds);
                Undo.RecordObject(loaded, "Growth threshold"); growth.coverage = .5f; Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Assert.AreEqual(.3f, ((RegionGrowthMechanic)loaded.phases[0].mechanics[0]).coverage);
                Assert.AreEqual(PatternCombatCondition.Enraged, loaded.phases[0].patterns[0].combatCondition);
            }
            finally { AssetDatabase.DeleteAsset(path); if (copy != null && !AssetDatabase.Contains(copy)) Object.DestroyImmediate(copy); }
        }
        [Test] public void ActualCorridorsStayOpenButExistingWarningIsNotAnEscapeGuaranteeWithVines()
        {
            var saved = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            using var preview = new PatternPreviewHost(saved);
            using var growth = new RegionGrowthRuntime(saved.phases[0].mechanics.OfType<RegionGrowthMechanic>().Single(), new MechanicContext(preview, saved.FindPattern), 42);
            var before = preview.Traversable.ToArray(); growth.Advance(1200);
            CollectionAssert.AreEquivalent(before, preview.Traversable);
            Assert.AreEqual(7, growth.Regions.Count(r => r.Overgrown));
            int worst = 0;
            foreach (var region in saved.tileRegions.Where(r => r.id.StartsWith("petal-", StringComparison.Ordinal)))
            {
                var distances = EncounterRegionRules.EscapeDistances(preview.Traversable, region.cells);
                Assert.IsTrue(region.cells.All(c => distances.ContainsKey(c)));
                worst = Math.Max(worst, region.cells.Max(c => distances[c]));
            }
            Assert.AreEqual(11, worst);
            float stun = saved.phases[0].mechanics.OfType<RegionGrowthMechanic>().Single().overgrownTile.stunSeconds;
            float earliestExitIgnoringReaction = (worst - 1) * stun;
            Assert.Greater(earliestExitIgnoringReaction, saved.phases[0].patterns[0].clips[0].duration);
            TestContext.WriteLine($"Worst actual corridor: {worst} moves, at least {earliestExitIgnoringReaction}s on vines even before reaction/input time. New attack timing needs balancing.");
        }
        [UnityTest] public IEnumerator EditorPanelPlaybackAndAdjacentClickUseSameMechanic()
        {
            var window = ScriptableObject.CreateInstance<BossEncounterEditorWindow>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object Call(string name, params object[] args) => typeof(BossEncounterEditorWindow).GetMethod(name, flags).Invoke(window, args);
            T Get<T>(string name) => (T)typeof(BossEncounterEditorWindow).GetField(name, flags).GetValue(window);
            boss.phases[0].mechanics.Add(definition); boss.arena.overridePlayerStart = true; boss.arena.playerStart = new Vector2Int(8, 7);
            try
            {
                window.position = new Rect(80, 80, 1400, 1000); window.Show(); Call("SelectEncounter", boss);
                var nodeType = typeof(BossEncounterEditorWindow).GetNestedType("NodeKind", BindingFlags.NonPublic);
                Call("SelectNode", Enum.Parse(nodeType, "Mechanic"), 0, 0); window.Repaint(); yield return null; yield return null;
                Call("AdvanceMechanicPreview", 5f);
                Assert.AreEqual(5, Get<BossMechanicSession>("mechanicPreview").Runtimes.OfType<RegionGrowthRuntime>().Single().Seeds.Count);
                var preview = Get<PatternPreviewHost>("previewHost"); var from = preview.player;
                var rect = PatternPreviewGridGUI.CellRect(Get<Rect>("growthBoardRect"), from.x + 1, from.y, boss.arena.GridSize);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = rect.center });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = rect.center });
                Assert.AreEqual(from + Vector2Int.right, preview.player);
                Call("AdvanceMechanicPreview", 25f); Assert.IsTrue(Get<BossMechanicSession>("mechanicPreview").IsEnraged);
                Call("RebuildPreview"); Assert.IsFalse(Get<BossMechanicSession>("mechanicPreview").IsEnraged);
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }
    }
}
#endif
