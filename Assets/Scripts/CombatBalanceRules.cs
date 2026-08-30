using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike
{
    public static class CombatBalanceRules
    {
        public const int DamagePerTrailCell = 1;
        public const int CenterDamagePerTrailCell = 2;
        public const int CenterDamageZoneSize = 3;
        public const float ExplosionCoyoteSeconds = 0.18f;

        public static int CalculateTrailDamage(int paintedCellCount)
        {
            return Mathf.Max(1, paintedCellCount) * DamagePerTrailCell;
        }

        public static int CalculateTrailDamage(
            IReadOnlyCollection<Vector2Int> paintedCells,
            int fieldSize)
        {
            if (paintedCells == null || paintedCells.Count == 0)
            {
                return DamagePerTrailCell;
            }

            int damage = 0;
            foreach (Vector2Int cell in paintedCells)
            {
                damage += DamageForCell(cell, fieldSize);
            }
            return Mathf.Max(DamagePerTrailCell, damage);
        }

        public static int DamageForCell(Vector2Int cell, int fieldSize)
        {
            return IsCenterDamageCell(cell, fieldSize)
                ? CenterDamagePerTrailCell
                : DamagePerTrailCell;
        }

        public static bool IsCenterDamageCell(Vector2Int cell, int fieldSize)
        {
            int start = Mathf.Max(0, (fieldSize - CenterDamageZoneSize) / 2);
            int endExclusive = Mathf.Min(fieldSize, start + CenterDamageZoneSize);
            return cell.x >= start && cell.x < endExclusive &&
                   cell.y >= start && cell.y < endExclusive;
        }

        public static bool ShouldApplyExplosionDamage(
            IReadOnlyCollection<Vector2Int> impactCells,
            Vector2Int playerAtImpact,
            Vector2Int playerAfterCoyote)
        {
            var danger = impactCells as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(impactCells);
            return danger.Contains(playerAtImpact) && danger.Contains(playerAfterCoyote);
        }
    }
}
