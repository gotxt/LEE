using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NHN.TraceStrike.Tests
{
    public sealed class CombatBalanceRulesTests
    {
        [TestCase(1, 1)]
        [TestCase(7, 7)]
        [TestCase(24, 24)]
        public void TrailDamageIsExactlyOnePerPaintedCell(int cells, int expected)
        {
            Assert.AreEqual(expected, CombatBalanceRules.CalculateTrailDamage(cells));
        }

        [Test]
        public void CenterThreeByThreeUsesDoubleDamage()
        {
            const int size = 11;
            var centerCells = new HashSet<Vector2Int>();
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (CombatBalanceRules.IsCenterDamageCell(cell, size))
                    {
                        centerCells.Add(cell);
                    }
                }
            }

            Assert.AreEqual(9, centerCells.Count);
            Assert.AreEqual(2, CombatBalanceRules.DamageForCell(new Vector2Int(6, 6), size));
            Assert.AreEqual(1, CombatBalanceRules.DamageForCell(new Vector2Int(0, 0), size));
            Assert.AreEqual(18, CombatBalanceRules.CalculateTrailDamage(centerCells, size));
        }

        [Test]
        public void MixedTrailDamageDependsOnTilePosition()
        {
            var trail = new HashSet<Vector2Int>
            {
                new Vector2Int(0, 0),
                new Vector2Int(5, 5),
                new Vector2Int(8, 8)
            };

            Assert.AreEqual(4, CombatBalanceRules.CalculateTrailDamage(trail, 11));
        }

        [Test]
        public void LeavingDangerDuringCoyoteTimeSurvives()
        {
            var danger = new HashSet<Vector2Int> { Vector2Int.zero };
            Assert.IsFalse(CombatBalanceRules.ShouldApplyExplosionDamage(
                danger, Vector2Int.zero, Vector2Int.right));
        }

        [Test]
        public void EnteringAfterExplosionDoesNotReceiveOldDamage()
        {
            var danger = new HashSet<Vector2Int> { Vector2Int.zero };
            Assert.IsFalse(CombatBalanceRules.ShouldApplyExplosionDamage(
                danger, Vector2Int.left, Vector2Int.zero));
        }

        [Test]
        public void RemainingInsideDangerAfterCoyoteTimeIsHit()
        {
            var danger = new HashSet<Vector2Int> { Vector2Int.zero };
            Assert.IsTrue(CombatBalanceRules.ShouldApplyExplosionDamage(
                danger, Vector2Int.zero, Vector2Int.zero));
        }
    }
}
