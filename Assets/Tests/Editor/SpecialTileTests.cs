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
    public sealed class SpecialTileTests
    {
        private BossEncounterDefinition boss;
        private SpecialTileDefinition fire, vine;
        private readonly Vector2Int center = new Vector2Int(8, 8);
        [SetUp] public void Setup()
        {
            boss = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            fire = ScriptableObject.CreateInstance<SpecialTileDefinition>(); fire.fireMoveCount = 20;
            vine = ScriptableObject.CreateInstance<SpecialTileDefinition>(); vine.stunSeconds = 1;
        }
        [TearDown] public void Cleanup()
        { Undo.ClearUndo(boss); Object.DestroyImmediate(boss); Object.DestroyImmediate(fire); Object.DestroyImmediate(vine); }

        [Test] public void SharedDefaultAssetsHaveConfirmedRulesAndNoBossDependency()
        {
            var f = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Fire");
            var v = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Vines");
            Assert.NotNull(f); Assert.NotNull(v); Assert.AreEqual(20, f.fireMoveCount); Assert.AreEqual(0, f.stunSeconds);
            Assert.AreEqual(1, v.stunSeconds); Assert.AreEqual(0, v.fireMoveCount);
            var errors = new List<string>(); f.Validate(errors); v.Validate(errors); Assert.IsEmpty(errors);
        }
        [Test] public void PaintingReplacesOnlySpecialLayerAndUndoRestores()
        {
            string arena = JsonUtility.ToJson(boss.arena); var floor = boss.arena.GetCells();
            Undo.RegisterCompleteObjectUndo(boss, "Special tiles");
            boss.arena.SetSpecialTile(center, fire); boss.arena.SetSpecialTile(center, vine);
            Assert.AreEqual(1, boss.arena.specialTiles.Count); Assert.AreSame(vine, boss.arena.specialTiles[0].tile);
            Undo.PerformUndo(); Assert.AreEqual(arena, JsonUtility.ToJson(boss.arena));
            boss.arena.SetSpecialTile(center, fire); boss.arena.SetSpecialTile(center, null);
            Assert.IsEmpty(boss.arena.specialTiles); CollectionAssert.AreEquivalent(floor, boss.arena.GetCells());
        }
        [Test] public void AssetRoundTripPreservesSharedReferenceAndCoordinates()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Tests/TilePlacement_RoundTrip.asset");
            var copy = Object.Instantiate(boss);
            try
            {
                var shared = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Fire");
                copy.arena.SetSpecialTile(center, shared); AssetDatabase.CreateAsset(copy, path);
                AssetDatabase.SaveAssetIfDirty(copy); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(path);
                Assert.AreEqual(center, loaded.arena.specialTiles.Single().cell); Assert.AreSame(shared, loaded.arena.specialTiles.Single().tile);
                Assert.IsEmpty(loaded.ValidateDefinition());
            }
            finally { AssetDatabase.DeleteAsset(path); if (copy != null) Object.DestroyImmediate(copy); }
        }
        [Test] public void ValidationRejectsMissingFloorAndBossFootprintAndDuplicateTiles()
        {
            boss.bossVisual.position = center; boss.bossVisual.footprintSize = Vector2Int.one;
            boss.arena.SetSpecialTile(center, fire); boss.arena.SetSpecialTile(new Vector2Int(-1, 0), vine);
            boss.arena.specialTiles.Add(new SpecialTilePlacement { cell = center, tile = vine });
            var errors = new List<string>(); SpecialTileValidation.Validate(boss, errors);
            Assert.AreEqual(4, errors.Count);
        }
        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidEffectValuesAreReported(float duration)
        {
            vine.stunSeconds = duration; var errors = new List<string>(); vine.Validate(errors); Assert.IsNotEmpty(errors);
        }
        [Test] public void FireLastChargedArrivalIsValidThenExpires()
        {
            var state = new SpecialTilePlayerState(); var at = center;
            Assert.IsTrue(state.Enter(at, at + Vector2Int.right, fire).HasFireOnArrival); at += Vector2Int.right;
            Assert.AreEqual(20, state.FireMovesRemaining);
            for (int i = 0; i < 20; i++)
            {
                var next = at + (i % 2 == 0 ? Vector2Int.left : Vector2Int.right);
                Assert.IsTrue(state.Enter(at, next, null).HasFireOnArrival); at = next;
                Assert.AreEqual(19 - i, state.FireMovesRemaining);
            }
            Assert.IsFalse(state.Enter(at, at + Vector2Int.up, null).HasFireOnArrival);
        }
        [Test] public void RechargeUsesEditableValueAndWaitingNeverConsumesFire()
        {
            var state = new SpecialTilePlayerState(); state.Enter(center, center + Vector2Int.up, fire);
            state.Advance(100); Assert.AreEqual(20, state.FireMovesRemaining);
            state.Enter(center + Vector2Int.up, center, null); Assert.AreEqual(19, state.FireMovesRemaining);
            fire.fireMoveCount = 7; state.Enter(center, center + Vector2Int.up, fire);
            Assert.AreEqual(7, state.FireMovesRemaining);
        }
        [Test] public void EveryVineEntryStunsButStandingDoesNotRetrigger()
        {
            var state = new SpecialTilePlayerState(); state.Enter(center, center + Vector2Int.up, vine);
            Assert.IsTrue(state.IsStunned); state.Advance(.5f); Assert.AreEqual(.5f, state.StunRemaining);
            Assert.Throws<InvalidOperationException>(() => state.Enter(center, center + Vector2Int.right, null));
            state.Advance(.5f); Assert.IsFalse(state.IsStunned);
            state.Advance(10); Assert.IsFalse(state.IsStunned);
            state.Enter(center + Vector2Int.up, center, vine); Assert.AreEqual(1, state.StunRemaining);
            state.Reset(); Assert.AreEqual(0, state.FireMovesRemaining); Assert.IsFalse(state.IsStunned);
        }
        [Test] public void FailedMovesDoNotConsumeChargeAndVinesRemainTraversable()
        {
            boss.arena.SetSpecialTile(center + Vector2Int.up, fire);
            boss.arena.SetSpecialTile(center + Vector2Int.up * 2, vine);
            using (var host = new PatternPreviewHost(boss) { player = center })
            {
                Assert.IsTrue(host.TryStep(Vector2Int.up, out _)); Assert.AreEqual(20, host.FireMovesRemaining);
                Assert.IsFalse(host.TryStep(Vector2Int.zero, out _)); Assert.AreEqual(20, host.FireMovesRemaining);
                using (host.Block(new[] { center + Vector2Int.up + Vector2Int.right }))
                    Assert.IsFalse(host.TryStep(Vector2Int.right, out _));
                Assert.AreEqual(20, host.FireMovesRemaining);
                Assert.IsTrue(host.TryStep(Vector2Int.up, out _)); Assert.AreEqual(19, host.FireMovesRemaining);
                Assert.IsFalse(host.TryStep(Vector2Int.down, out _)); Assert.AreEqual(19, host.FireMovesRemaining);
                host.TileState.Advance(1); Assert.IsTrue(host.TryStep(Vector2Int.down, out _)); Assert.AreEqual(20, host.FireMovesRemaining);
            }
        }
        [Test] public void DynamicLayersRestorePreviousTileAndCannotLeakAcrossRestart()
        {
            boss.arena.SetSpecialTile(center, fire);
            using (var field = new SpecialTileField(boss.arena.specialTiles))
            {
                var first = field.Add(vine, new[] { center }); var last = field.Add(fire, new[] { center });
                first.Dispose(); Assert.AreSame(fire, field.At(center)); last.Dispose(); Assert.AreSame(fire, field.At(center));
                using (field.Add(vine, new[] { center })) Assert.AreSame(vine, field.At(center));
                Assert.AreSame(fire, field.At(center)); field.Dispose(); first.Dispose(); last.Dispose(); Assert.IsNull(field.At(center));
            }
        }
        [Test] public void TilePreviewStaysBehindAttackWarningsAndDoesNotBlockCells()
        {
            boss.arena.SetSpecialTile(center, fire);
            using (var host = new PatternPreviewHost(boss))
            using (host.Mark(new[] { center }, Color.red, true))
            {
                var marks = host.GetOrderedMarks(); Assert.AreEqual(PatternPreviewHost.PreviewLayer.SpecialTile, marks[0].Layer);
                Assert.AreEqual(PatternPreviewHost.PreviewLayer.Warning, marks.Last().Layer);
                CollectionAssert.Contains(host.Traversable, center);
                using (host.PlaceSpecialTiles(vine, new[] { center })) Assert.AreEqual(vine.color, host.GetOrderedMarks()[0].Color);
                Assert.AreEqual(fire.color, host.GetOrderedMarks()[0].Color);
            }
        }
        [UnityTest] public IEnumerator EditorSpecialTilePanelRendersAndMovementPreviewUsesSameRules()
        {
            var window = ScriptableObject.CreateInstance<BossEncounterEditorWindow>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic; var type = window.GetType();
            boss.arena.overridePlayerStart = true; boss.arena.playerStart = center;
            boss.arena.SetSpecialTile(center + Vector2Int.up, fire); boss.arena.SetSpecialTile(center + Vector2Int.up * 2, vine);
            string before = EditorJsonUtility.ToJson(boss);
            try
            {
                type.GetMethod("SelectEncounter", flags).Invoke(window, new object[] { boss });
                var node = type.GetField("node", flags); node.SetValue(window, Enum.Parse(node.FieldType, "SpecialTiles"));
                type.GetField("specialTileBrush", flags).SetValue(window, fire);
                type.GetMethod("RebuildPreview", flags).Invoke(window, null);
                window.Show(); window.position = new Rect(20, 20, 1400, 1100); window.Repaint(); yield return null; yield return null;
                type.GetMethod("MoveSpecialTilePreview", flags).Invoke(window, new object[] { Vector2Int.up });
                var state = (SpecialTilePlayerState)type.GetField("tileTestState", flags).GetValue(window);
                Assert.AreEqual(20, state.FireMovesRemaining);
                type.GetMethod("MoveSpecialTilePreview", flags).Invoke(window, new object[] { Vector2Int.up });
                Assert.IsTrue(state.IsStunned); Assert.AreEqual(19, state.FireMovesRemaining);
                Assert.AreEqual(before, EditorJsonUtility.ToJson(boss));
                // Drive the same pointer stroke used by team members, including drag + erase + undo.
                var board = (Rect)type.GetField("specialTileBoardRect", flags).GetValue(window);
                Assert.Greater(board.width, 0);
                Vector2 CellPointer(Vector2Int c) => new Vector2(board.x + (c.x + .5f) * board.width / 17,
                    board.y + (16 - c.y + .5f) * board.height / 17);
                var paintFrom = new Vector2Int(6, 8); var paintTo = new Vector2Int(7, 8);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = CellPointer(paintFrom) });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = CellPointer(paintTo) });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = CellPointer(paintTo) });
                Assert.IsTrue(boss.arena.specialTiles.Any(p => p.cell == paintFrom && p.tile == fire),
                    "Board=" + board + " window=" + window.position + " painted=" + string.Join(",", boss.arena.specialTiles.Select(p => p.cell)));
                Assert.IsTrue(boss.arena.specialTiles.Any(p => p.cell == paintTo && p.tile == fire));
                Undo.PerformUndo(); Assert.AreEqual(before, EditorJsonUtility.ToJson(boss));
                window.SendEvent(new Event { type = EventType.MouseDown, button = 1, mousePosition = CellPointer(center + Vector2Int.up) });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 1, mousePosition = CellPointer(center + Vector2Int.up) });
                Assert.IsFalse(boss.arena.specialTiles.Any(p => p.cell == center + Vector2Int.up));
                Undo.PerformUndo(); Assert.AreEqual(before, EditorJsonUtility.ToJson(boss));
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }
    }
}
#endif
