#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEngine;

namespace NHN.TraceStrike.Tests
{
    public sealed class ArenaConfigurationTests
    {
        private static BossArenaDefinition Square(int size)
        {
            var arena = new BossArenaDefinition { size = size, shape = ArenaShape.Custom };
            for (int y = 0; y < arena.GridSize; y++)
            for (int x = 0; x < arena.GridSize; x++)
            {
                var cell = new Vector2Int(x, y);
                if (arena.ContainsBounds(cell)) arena.floorCells.Add(cell);
            }
            return arena;
        }

        [Test]
        public void EveryIntegerSizeThroughFiftyHasExactSquareBounds()
        {
            for (int size = 5; size <= 50; size++)
            {
                var arena = Square(size);
                var model = new TrailFieldModel(); arena.ApplyTo(model);
                Assert.AreEqual(size * size, model.Walkable.Count, "size=" + size);
                Assert.AreEqual(Math.Max(17, size), model.GridSize);
                foreach (var cell in model.Walkable) Assert.IsTrue(arena.ContainsBounds(cell));
                model.BeginRound(0);
                Assert.AreNotEqual(model.Start, model.End);
                var preview = new Editor.PatternPreviewHost(arena);
                CollectionAssert.AreEquivalent(model.Walkable, preview.Walkable);
                Assert.AreEqual(model.CenterCell, preview.CenterCell);
            }
        }

        [Test]
        public void AllPresetsSupportOddAndEvenSizesWithoutExceedingBounds()
        {
            for (int size = 5; size <= 50; size++)
            for (int shape = 0; shape < 3; shape++)
            {
                var model = new TrailFieldModel(); model.CreateField(shape, size);
                foreach (var cell in model.Walkable)
                    Assert.IsTrue(TrailFieldModel.ContainsFieldBounds(cell, size), "size=" + size);
                model.BeginRound(7);
                Assert.AreNotEqual(model.Start, model.End);
            }
        }

        [Test]
        public void LegacyDefaultAndSmallMapCoordinatesArePreserved()
        {
            var model = new TrailFieldModel(); model.CreateField(0);
            Assert.AreEqual(221, model.Walkable.Count);
            Assert.AreEqual(new Vector2Int(8, 8), model.CenterCell);
            Assert.AreEqual(17, model.FieldSize);
            Assert.AreEqual(5, TrailFieldModel.ScaleLegacyDistance(3));
            model.CreateField(0, 11);
            Assert.AreEqual(new Vector2Int(8, 8), model.CenterCell);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.CreateField(0, 51));
        }

        [Test]
        public void FixedSpawnIsIndependentOfStartAndStartsTraceOnlyOnContact()
        {
            var arena = Square(50);
            arena.overridePlayerStart = true; arena.playerStart = new Vector2Int(20, 20);
            arena.restrictStartCells = true; arena.startCells.Add(new Vector2Int(21, 20));
            arena.restrictEndCells = true; arena.endCells.Add(new Vector2Int(22, 20));
            var model = new TrailFieldModel(); arena.ApplyTo(model);
            model.BeginRound(0, true, arena.playerStart);
            Assert.AreEqual(arena.playerStart, model.Player);
            Assert.IsFalse(model.IsTracing); Assert.IsEmpty(model.Trail);
            Assert.AreEqual(MoveResult.TrailStarted, model.TryMove(Vector2Int.right));
            Assert.AreEqual(MoveResult.AttackReady, model.TryMove(Vector2Int.right));
            var previous = model.Player;
            model.BeginRound(1, false);
            Assert.AreEqual(previous, model.Player);
            Assert.AreEqual(arena.startCells[0], model.Start);
            Assert.AreEqual(arena.endCells[0], model.End);
        }

        [Test]
        public void RestrictedRegionsAreRespectedAcrossRoundsAndObstacles()
        {
            var arena = Square(50);
            arena.restrictStartCells = arena.restrictEndCells = true;
            arena.startCells.AddRange(new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) });
            arena.endCells.AddRange(new[] { new Vector2Int(49, 49), new Vector2Int(48, 49) });
            var model = new TrailFieldModel(); arena.ApplyTo(model);
            model.SetBlockedCells(new[] { arena.startCells[0], arena.endCells[0] });
            for (int round = 0; round < 30; round++)
            {
                model.BeginRound(round, round == 0);
                Assert.AreEqual(arena.startCells[1], model.Start);
                Assert.AreEqual(arena.endCells[1], model.End);
            }
            Assert.IsFalse(model.HasEndpointPair(arena.startCells));
        }

        [Test]
        public void EndIsFarthestReachableTileWithinItsRegion()
        {
            var arena = Square(18);
            arena.restrictStartCells = arena.restrictEndCells = true;
            arena.startCells.Add(new Vector2Int(0, 0));
            arena.endCells.AddRange(new[] { new Vector2Int(1, 0), new Vector2Int(3, 0) });
            var model = new TrailFieldModel(); arena.ApplyTo(model); model.BeginRound(0);
            Assert.AreEqual(new Vector2Int(3, 0), model.End);
        }

        [Test]
        public void EmptyOrSameSingleCellRegionsFailWithoutFallingBack()
        {
            var arena = Square(50); arena.restrictStartCells = true;
            var model = new TrailFieldModel(); arena.ApplyTo(model);
            Assert.Throws<InvalidOperationException>(() => model.BeginRound(0));
            arena.startCells.Add(Vector2Int.zero); arena.restrictEndCells = true;
            arena.endCells.Add(Vector2Int.zero); arena.ApplyTo(model);
            Assert.Throws<InvalidOperationException>(() => model.BeginRound(0));
            var errors = new List<string>(); arena.ValidateMap(errors); Assert.IsNotEmpty(errors);
            arena.restrictStartCells = arena.restrictEndCells = false; arena.ApplyTo(model);
            Assert.DoesNotThrow(() => model.BeginRound(0));
        }

        [Test]
        public void OverlappingRegionsWithOneEndTryAnotherStart()
        {
            var arena = Square(18);
            arena.restrictStartCells = arena.restrictEndCells = true;
            arena.startCells.AddRange(new[] { Vector2Int.zero, Vector2Int.right });
            arena.endCells.Add(Vector2Int.zero);
            var model = new TrailFieldModel(); arena.ApplyTo(model);
            for (int round = 0; round < 4; round++)
            {
                model.BeginRound(round);
                Assert.AreEqual(Vector2Int.right, model.Start);
                Assert.AreEqual(Vector2Int.zero, model.End);
            }
        }

        [Test]
        public void UnreachableEndpointsAndInvalidSpawnAreRejected()
        {
            var arena = Square(6);
            arena.overridePlayerStart = true; arena.playerStart = Vector2Int.zero;
            var errors = new List<string>(); arena.ValidateMap(errors); Assert.IsNotEmpty(errors);
            var model = new TrailFieldModel(); arena.ApplyTo(model);
            Assert.Throws<InvalidOperationException>(() => model.BeginRound(0, true, Vector2Int.zero));
            model.CreateCustomField(50, new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(49, 49) });
            model.SetEndpointRegions(new[] { Vector2Int.zero }, new[] { new Vector2Int(49, 49) });
            Assert.Throws<InvalidOperationException>(() => model.BeginRound(0));
        }

        [Test]
        public void SpawnOnStartBeginsTracingAndSettingsSurviveSizeChanges()
        {
            var arena = Square(50); arena.overridePlayerStart = true;
            arena.playerStart = new Vector2Int(49, 49); arena.restrictStartCells = true;
            arena.startCells.Add(arena.playerStart);
            var model = new TrailFieldModel(); arena.ApplyTo(model);
            model.BeginRound(0, true, arena.playerStart);
            Assert.IsTrue(model.IsTracing); Assert.AreEqual(1, model.Trail.Count);
            arena.size = 17;
            Assert.IsFalse(arena.GetCells().Contains(arena.playerStart));
            arena.size = 50;
            arena.ApplyTo(model); model.BeginRound(0, true, arena.playerStart);
            Assert.AreEqual(arena.playerStart, model.Start);
            Assert.AreEqual(2500, model.Walkable.Count);
        }
    }
}
#endif
