#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NHN.TraceStrike.Tests
{
    public class TrailFieldModelTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EveryFieldHasReachableStartAndEnd(int stage)
        {
            var model = new TrailFieldModel();
            model.CreateField(stage);
            model.BeginRound(0);

            Assert.That(model.IsWalkable(model.Start), Is.True);
            Assert.That(model.IsWalkable(model.End), Is.True);
            Assert.That(model.Start, Is.Not.EqualTo(model.End));
        }

        [Test]
        public void LeavingTheFieldIsBlocked()
        {
            var model = new TrailFieldModel();
            model.CreateField(1);
            model.BeginRound(0);

            Vector2Int direction = model.Start.x < TrailFieldModel.Size / 2 ? Vector2Int.left : Vector2Int.down;
            Vector2Int before = model.Player;
            MoveResult result = model.TryMove(direction);

            Assert.That(result, Is.EqualTo(MoveResult.Blocked));
            Assert.That(model.Player, Is.EqualTo(before));
        }

        [Test]
        public void RevisitingTrailResetsAndRequiresStart()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);

            Vector2Int validDirection = Vector2Int.zero;
            foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
            {
                if (model.IsWalkable(model.Player + direction))
                {
                    validDirection = direction;
                    break;
                }
            }

            MoveResult firstMove = model.TryMove(validDirection);
            Assert.That(firstMove == MoveResult.TrailExtended || firstMove == MoveResult.AttackReady, Is.True);
            Assert.That(model.TryMove(-validDirection), Is.EqualTo(MoveResult.TrailReset));
            Assert.That(model.IsTracing, Is.False);
            Assert.That(model.Trail.Count, Is.Zero);
        }

        [Test]
        public void NewAttackPointsDoNotTeleportPlayer()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);

            Vector2Int validDirection = Vector2Int.zero;
            foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
            {
                if (model.IsWalkable(model.Player + direction))
                {
                    validDirection = direction;
                    break;
                }
            }

            model.TryMove(validDirection);
            Vector2Int positionBeforeReset = model.Player;
            model.BeginRound(1, false);

            Assert.That(model.Player, Is.EqualTo(positionBeforeReset));
            Assert.That(model.IsTracing, Is.False);
            Assert.That(model.Trail.Count, Is.Zero);
        }

        [Test]
        public void ShapeChangeCanPreservePlayerCell()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);
            Vector2Int previousPosition = model.Player;

            model.CreateField(1, previousPosition);
            model.BeginRound(0, false);

            Assert.That(model.Player, Is.EqualTo(previousPosition));
            Assert.That(model.IsWalkable(previousPosition), Is.True);
        }

        [Test]
        public void BossTelegraphAcceleratesInPhaseTwo()
        {
            Assert.That(BossPatternRules.TelegraphSeconds(false), Is.EqualTo(2f));
            Assert.That(BossPatternRules.TelegraphSeconds(true), Is.EqualTo(1f));
        }

        [Test]
        public void LaserMarksOnlyTargetedRowOrColumn()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);

            var laser = BossPatternRules.CreateLaser(model.Walkable, model.Player, true);
            Assert.That(laser.Count, Is.GreaterThan(0));
            foreach (Vector2Int cell in laser)
            {
                Assert.That(cell.x, Is.EqualTo(model.Player.x));
            }
        }

        [Test]
        public void ExplosionMarksThreeByThreeAreaInsideField()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);

            var explosion = BossPatternRules.CreateExplosion(model.Walkable, model.Player);
            Assert.That(explosion.Contains(model.Player), Is.True);
            foreach (Vector2Int cell in explosion)
            {
                Assert.That(Mathf.Abs(cell.x - model.Player.x), Is.LessThanOrEqualTo(1));
                Assert.That(Mathf.Abs(cell.y - model.Player.y), Is.LessThanOrEqualTo(1));
            }
        }

        [Test]
        public void PatternIntervalAcceleratesWithFightProgress()
        {
            float opening = BossPatternRules.PatternIntervalSeconds(false, 0);
            float later = BossPatternRules.PatternIntervalSeconds(false, 6);
            float enraged = BossPatternRules.PatternIntervalSeconds(true, 6);

            Assert.That(later, Is.LessThan(opening));
            Assert.That(enraged, Is.LessThan(later));
            Assert.That(BossPatternRules.PatternIntervalSeconds(true, 99), Is.EqualTo(0.45f));
        }

        [Test]
        public void MultiLaserMarksEveryRequestedLine()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);
            var laser = BossPatternRules.CreateMultiLaser(model.Walkable, model.Player, true, new[] { 0, 1 });

            foreach (Vector2Int cell in laser)
            {
                Assert.That(cell.x == model.Player.x || cell.x == model.Player.x + 1, Is.True);
            }
        }

        [Test]
        public void CombinedLaserContainsHorizontalAndVerticalPairs()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);
            var laser = BossPatternRules.CreateCombinedLaser(model.Walkable, model.Player,
                new[] { 0, 1 }, new[] { 0, 1 });

            Assert.That(laser.Contains(model.Player), Is.True);
            Assert.That(laser.Count, Is.GreaterThan(
                BossPatternRules.CreateMultiLaser(model.Walkable, model.Player, true, new[] { 0, 1 }).Count));
        }

        [Test]
        public void BossUsesSeparateHealthPoolForEachPhase()
        {
            Assert.That(BossPatternRules.PhaseMaxHealth(false), Is.EqualTo(500));
            Assert.That(BossPatternRules.PhaseMaxHealth(true), Is.EqualTo(1500));
        }

        [Test]
        public void FixedGlyphsMatchTheirGridEquations()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            var center = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);

            var cross = BossPatternRules.CreateCrossGlyph(model.Walkable, center);
            var diamond = BossPatternRules.CreateDiamondGlyph(model.Walkable, center, 3);
            var diagonal = BossPatternRules.CreateDiagonalGlyph(model.Walkable, center);

            foreach (Vector2Int cell in cross)
                Assert.That(cell.x == center.x || cell.y == center.y, Is.True);
            foreach (Vector2Int cell in diamond)
                Assert.That(Mathf.Abs(cell.x - center.x) + Mathf.Abs(cell.y - center.y), Is.EqualTo(3));
            foreach (Vector2Int cell in diagonal)
                Assert.That(Mathf.Abs(cell.x - center.x), Is.EqualTo(Mathf.Abs(cell.y - center.y)));
        }

        [Test]
        public void CombinedGlyphContainsCrossAndDiamond()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            var center = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);
            var combined = BossPatternRules.CreateCombinedGlyph(model.Walkable, center, 3);

            Assert.That(combined.IsSupersetOf(BossPatternRules.CreateCrossGlyph(model.Walkable, center)), Is.True);
            Assert.That(combined.IsSupersetOf(BossPatternRules.CreateDiamondGlyph(model.Walkable, center, 3)), Is.True);
        }

        [Test]
        public void EscapeRouteAlwaysLeavesAnAdjacentSafeTile()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);
            var allDanger = new HashSet<Vector2Int>(model.Walkable);

            var adjusted = BossPatternRules.EnsureEscapeRoute(model.Walkable, model.Player, allDanger);
            Assert.That(BossPatternRules.HasAdjacentSafeCell(model.Walkable, model.Player, adjusted), Is.True);
        }

        [Test]
        public void SpecialTilesContainEveryEffectAndAvoidReservedCells()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);
            var excluded = new HashSet<Vector2Int> { model.Player, model.Start, model.End };

            Dictionary<Vector2Int, SpecialTileType> tiles =
                SpecialTileRules.Generate(model.Walkable, excluded, 42);

            Assert.That(tiles.Count, Is.EqualTo(4));
            Assert.That(new HashSet<SpecialTileType>(tiles.Values).Count, Is.EqualTo(4));
            Assert.That(tiles.ContainsValue(SpecialTileType.Spike), Is.False);
            foreach (Vector2Int cell in excluded)
                Assert.That(tiles.ContainsKey(cell), Is.False);
        }

        [Test]
        public void SpecialTileGenerationIsDeterministic()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);
            var excluded = new HashSet<Vector2Int> { model.Player, model.Start, model.End };

            Dictionary<Vector2Int, SpecialTileType> first =
                SpecialTileRules.Generate(model.Walkable, excluded, 73);
            Dictionary<Vector2Int, SpecialTileType> second =
                SpecialTileRules.Generate(model.Walkable, excluded, 73);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            foreach (KeyValuePair<Vector2Int, SpecialTileType> tile in first)
                Assert.That(second[tile.Key], Is.EqualTo(tile.Value));
        }

        [Test]
        public void SpecialDamageModifiersApplyFlatBonusBeforeMultiplier()
        {
            Assert.That(SpecialTileRules.ApplyDamageModifiers(20, 25, 1.35f), Is.EqualTo(61));
            Assert.That(SpecialTileRules.ApplyDamageModifiers(20, 0, 0.65f), Is.EqualTo(13));
            Assert.That(SpecialTileRules.IsBeneficial(SpecialTileType.Power), Is.True);
            Assert.That(SpecialTileRules.IsBeneficial(SpecialTileType.Spike), Is.False);
        }

        [Test]
        public void BlockedCrystalCellRejectsMovementAndRoundEndpoints()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);
            Vector2Int direction = Vector2Int.zero;
            foreach (Vector2Int candidate in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
            {
                if (model.IsWalkable(model.Player + candidate))
                {
                    direction = candidate;
                    break;
                }
            }
            Vector2Int crystal = model.Player + direction;
            model.SetBlockedCells(new[] { crystal });

            Assert.That(model.TryMove(direction), Is.EqualTo(MoveResult.Blocked));
            model.BeginRound(3);
            Assert.That(model.Start, Is.Not.EqualTo(crystal));
            Assert.That(model.End, Is.Not.EqualTo(crystal));
        }

        [Test]
        public void CardinalCrystalLayoutPlacesFourCellsOneStepInsideEdges()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            var center = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);
            List<Vector2Int> crystals = CrystalRules.CreateCardinalLayout(model.Walkable, center);

            Assert.That(crystals.Count, Is.EqualTo(4));
            foreach (Vector2Int crystal in crystals)
            {
                Assert.That(crystal.x == center.x || crystal.y == center.y, Is.True);
                Vector2Int outward = new Vector2Int(
                    crystal.x == center.x ? 0 : crystal.x > center.x ? 1 : -1,
                    crystal.y == center.y ? 0 : crystal.y > center.y ? 1 : -1);
                Assert.That(model.IsWalkable(crystal + outward), Is.True);
                Assert.That(model.IsWalkable(crystal + outward * 2), Is.False);
            }
        }

        [Test]
        public void RandomCrystalLayoutAvoidsExcludedAndUsesUniqueCells()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);
            var excluded = new HashSet<Vector2Int> { model.Player, model.Start, model.End };
            List<Vector2Int> crystals = CrystalRules.CreateRandomLayout(model.Walkable, excluded, 125);

            Assert.That(crystals.Count, Is.EqualTo(4));
            Assert.That(new HashSet<Vector2Int>(crystals).Count, Is.EqualTo(4));
            foreach (Vector2Int crystal in crystals)
                Assert.That(excluded.Contains(crystal), Is.False);
        }

        [Test]
        public void CrystalBlastUsesTwoCellCheckerPattern()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            var center = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);
            HashSet<Vector2Int> blast = CrystalRules.CreateCheckerBlast(model.Walkable, center);

            Assert.That(blast.Contains(center), Is.False);
            Assert.That(blast.Count, Is.GreaterThan(0));
            foreach (Vector2Int cell in blast)
            {
                int dx = Mathf.Abs(cell.x - center.x);
                int dy = Mathf.Abs(cell.y - center.y);
                Assert.That(dx, Is.LessThanOrEqualTo(CrystalRules.BlastRange));
                Assert.That(dy, Is.LessThanOrEqualTo(CrystalRules.BlastRange));
                Assert.That((dx + dy) % 2, Is.EqualTo(0));
            }
            Assert.That(CrystalRules.BlastRange, Is.EqualTo(2));
        }

        [Test]
        public void PhaseTwoGridPatternsCoverAlternatingRowsAndColumnsSeparately()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            var center = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);
            HashSet<Vector2Int> horizontal = BossPatternRules.CreateHorizontalGrid(model.Walkable, center);
            HashSet<Vector2Int> vertical = BossPatternRules.CreateVerticalGrid(model.Walkable, center);
            foreach (Vector2Int cell in horizontal)
                Assert.That(Mathf.Abs(cell.y - center.y) % 2, Is.EqualTo(0));
            foreach (Vector2Int cell in vertical)
                Assert.That(Mathf.Abs(cell.x - center.x) % 2, Is.EqualTo(0));
        }

        [Test]
        public void CrystalAttackIntervalsMatchBothGimmickStages()
        {
            for (int i = 0; i < CrystalRules.CrystalCount; i++)
            {
                Assert.That(CrystalRules.AttackIntervalSeconds(false, i), Is.EqualTo(5f));
            }

            Assert.That(CrystalRules.AttackIntervalSeconds(true, 0), Is.EqualTo(5f));
            Assert.That(CrystalRules.AttackIntervalSeconds(true, 1), Is.EqualTo(5f));
            Assert.That(CrystalRules.AttackIntervalSeconds(true, 2), Is.EqualTo(4f));
            Assert.That(CrystalRules.AttackIntervalSeconds(true, 3), Is.EqualTo(4f));
        }

        [Test]
        public void TutorialUsesFourByFourBounds()
        {
            Assert.That(TutorialRules.IsInside(new Vector2Int(0, 0)), Is.True);
            Assert.That(TutorialRules.IsInside(new Vector2Int(3, 3)), Is.True);
            Assert.That(TutorialRules.IsInside(new Vector2Int(-1, 0)), Is.False);
            Assert.That(TutorialRules.IsInside(new Vector2Int(4, 3)), Is.False);
        }

        [Test]
        public void TutorialIntroducesSpecialTilesInRequiredOrder()
        {
            SpecialTileType[] expected =
            {
                SpecialTileType.Power,
                SpecialTileType.Amplify,
                SpecialTileType.Mud,
                SpecialTileType.Curse
            };

            Assert.That(TutorialRules.SpecialStepCount, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(TutorialRules.GetSpecialType(i), Is.EqualTo(expected[i]));
            }
            Assert.That(TutorialRules.GetSpecialType(TutorialRules.SpecialStepCount - 1),
                Is.EqualTo(SpecialTileType.Curse));
        }

        [Test]
        public void TutorialSpecialTargetsAreUniqueAndConnected()
        {
            var targets = new HashSet<Vector2Int>();
            Vector2Int previous = TutorialRules.End;
            for (int i = 0; i < TutorialRules.SpecialStepCount; i++)
            {
                Vector2Int target = TutorialRules.GetSpecialTarget(i);
                Assert.That(TutorialRules.IsInside(target), Is.True);
                Assert.That(targets.Add(target), Is.True);
                Assert.That(Mathf.Abs(target.x - previous.x) + Mathf.Abs(target.y - previous.y), Is.EqualTo(1));
                previous = target;
            }
        }

        [Test]
        public void TelegraphProgressReachesBorderExactlyAtAttackTime()
        {
            Assert.That(BossPatternRules.TelegraphProgress(0f, 2f), Is.EqualTo(0f));
            Assert.That(BossPatternRules.TelegraphProgress(1f, 2f), Is.EqualTo(0.5f));
            Assert.That(BossPatternRules.TelegraphProgress(2f, 2f), Is.EqualTo(1f));
            Assert.That(BossPatternRules.TelegraphProgress(3f, 2f), Is.EqualTo(1f));
            Assert.That(BossPatternRules.TelegraphProgress(0f, 0f), Is.EqualTo(1f));
        }

        [Test]
        public void MobileViewportAlwaysFillsTheWholeScreen()
        {
            Rect mobileViewport = TraceStrikeGame.CalculateViewport(9f / 20f, true);
            Assert.That(mobileViewport, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));

            Rect desktopViewport = TraceStrikeGame.CalculateViewport(9f / 20f, false);
            Assert.That(desktopViewport.height, Is.LessThan(1f));
            Assert.That(desktopViewport.y, Is.GreaterThan(0f));
        }
    }
}
#endif
