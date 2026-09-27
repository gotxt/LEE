#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

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
        public void BossFootprintBlocksExactlySixteenTilesInRuntimeAndPreviewModels()
        {
            var boss = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            Assert.IsNotNull(boss);
            var occupied = boss.bossVisual.OccupiedCells();
            Assert.AreEqual(16, occupied.Count);
            for (int y = 15; y <= 18; y++)
            for (int x = 14; x <= 17; x++)
                Assert.Contains(new Vector2Int(x, y), new List<Vector2Int>(occupied));
            var model = new TrailFieldModel(); boss.arena.ApplyTo(model);
            model.SetBlockedCells(occupied);
            model.BeginRound(0, true, boss.arena.playerStart);
            Assert.AreEqual(boss.arena.playerStart, model.Player);
            Assert.AreEqual(boss.arena.startCells[0], model.Start);
            Assert.AreEqual(boss.arena.endCells[0], model.End);
            Assert.IsFalse(model.TryPlacePlayer(new Vector2Int(15, 15)));
            using (var preview = new Editor.PatternPreviewHost(boss))
            {
                CollectionAssert.IsSubsetOf(occupied, preview.Walkable);
                foreach (var cell in occupied) CollectionAssert.DoesNotContain(preview.Traversable, cell);
            }
        }

        [Test]
        public void BossFootprintValidationRejectsSpawnOverlap()
        {
            var root = new GameObject("Footprint validation boss");
            try
            {
                var visual = new BossVisualDefinition
                {
                    prefab = root.AddComponent<BossActor>(),
                    position = new Vector2(8, 8),
                    footprintSize = new Vector2Int(4, 4)
                };
                var arena = Square(17);
                arena.overridePlayerStart = true; arena.playerStart = new Vector2Int(8, 8);
                var errors = new List<string>(); visual.Validate(errors, arena);
                Assert.IsTrue(errors.Exists(error => error.Contains("spawn")));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void RottenBloomVisibleSpriteFillsAndCentersOnFourByFourFootprint()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Graphics device required for boss sprite alignment test.");
            var boss = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            Assert.IsNotNull(boss);
            var readback = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            var occupied = boss.bossVisual.OccupiedCells();
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var cell in occupied)
            {
                minX = Math.Min(minX, cell.x); maxX = Math.Max(maxX, cell.x);
                minY = Math.Min(minY, cell.y); maxY = Math.Max(maxY, cell.y);
            }
            try
            {
                using (var stage = new BossRenderStage(boss.bossVisual, boss.arena.GridSize, true))
                {
                    stage.Render();
                    float tilePixels = stage.Texture.width / (float)boss.arena.GridSize;
                    float cameraCenter = (boss.arena.GridSize - 1) * 0.5f;
                    int captureX = Mathf.RoundToInt(stage.Texture.width * 0.5f +
                        ((minX + maxX) * 0.5f - cameraCenter) * tilePixels - 256);
                    int captureY = Mathf.RoundToInt(stage.Texture.height * 0.5f +
                        ((minY + maxY) * 0.5f - cameraCenter) * tilePixels - 256);
                    var previous = RenderTexture.active;
                    try
                    {
                        RenderTexture.active = stage.Texture;
                        readback.ReadPixels(new Rect(captureX, captureY, 512, 512), 0, 0);
                        readback.Apply();
                    }
                    finally { RenderTexture.active = previous; }
                }
                var pixels = readback.GetPixels32();
                int left = 512, right = -1, bottom = 512, top = -1;
                for (int y = 0; y < 512; y++)
                for (int x = 0; x < 512; x++)
                    if (pixels[y * 512 + x].a > 25)
                    {
                        left = Math.Min(left, x); right = Math.Max(right, x);
                        bottom = Math.Min(bottom, y); top = Math.Max(top, y);
                    }
                Assert.Greater(right, left, "Boss sprite must render.");
                Assert.That(right - left + 1, Is.InRange(244, 268), "Visible width should fill four tiles.");
                Assert.That(top - bottom + 1, Is.InRange(244, 268), "Visible height should fill four tiles.");
                Assert.That((left + right) * 0.5f, Is.EqualTo(256).Within(8), "Horizontal centre should match the occupied tiles.");
                Assert.That((bottom + top) * 0.5f, Is.EqualTo(256).Within(8), "Vertical centre should match the occupied tiles.");
            }
            finally { UnityEngine.Object.DestroyImmediate(readback); }
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
