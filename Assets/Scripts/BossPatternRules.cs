using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike
{
    public static class BossPatternRules
    {
        public static float TelegraphSeconds(bool phaseTwo)
        {
            return phaseTwo ? 1f : 2f;
        }

        public static float TelegraphProgress(float elapsedSeconds, float durationSeconds)
        {
            if (durationSeconds <= 0f)
            {
                return 1f;
            }
            return Mathf.Clamp01(elapsedSeconds / durationSeconds);
        }

        public static float PatternIntervalSeconds(bool phaseTwo, int completedAttacks)
        {
            float phaseBase = phaseTwo ? 1.05f : 1.85f;
            return Mathf.Max(0.45f, phaseBase - completedAttacks * 0.1f);
        }

        public static int PhaseMaxHealth(bool phaseTwo)
        {
            return phaseTwo ? 1500 : 500;
        }

        public static HashSet<Vector2Int> CreateCrossGlyph(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int center)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in walkable)
            {
                if (cell.x == center.x || cell.y == center.y)
                {
                    cells.Add(cell);
                }
            }
            return cells;
        }

        public static HashSet<Vector2Int> CreateDiamondGlyph(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int center,
            int distance)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in walkable)
            {
                if (Mathf.Abs(cell.x - center.x) + Mathf.Abs(cell.y - center.y) == distance)
                {
                    cells.Add(cell);
                }
            }
            return cells;
        }

        public static HashSet<Vector2Int> CreateDiagonalGlyph(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int center)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in walkable)
            {
                if (Mathf.Abs(cell.x - center.x) == Mathf.Abs(cell.y - center.y))
                {
                    cells.Add(cell);
                }
            }
            return cells;
        }

        public static HashSet<Vector2Int> CreateCombinedGlyph(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int center,
            int diamondDistance)
        {
            var cells = CreateCrossGlyph(walkable, center);
            cells.UnionWith(CreateDiamondGlyph(walkable, center, diamondDistance));
            return cells;
        }

        public static HashSet<Vector2Int> CreateHorizontalGrid(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int center)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in walkable)
            {
                if (Mathf.Abs(cell.y - center.y) % 2 == 0)
                {
                    cells.Add(cell);
                }
            }
            return cells;
        }

        public static HashSet<Vector2Int> CreateVerticalGrid(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int center)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in walkable)
            {
                if (Mathf.Abs(cell.x - center.x) % 2 == 0)
                {
                    cells.Add(cell);
                }
            }
            return cells;
        }

        public static bool HasAdjacentSafeCell(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int player,
            IReadOnlyCollection<Vector2Int> primaryDanger,
            IReadOnlyCollection<Vector2Int> secondaryDanger = null)
        {
            var walkableSet = walkable as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(walkable);
            var primarySet = primaryDanger as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(primaryDanger);
            HashSet<Vector2Int> secondarySet = secondaryDanger == null
                ? null
                : secondaryDanger as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(secondaryDanger);

            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = player + direction;
                if (walkableSet.Contains(next) && !primarySet.Contains(next) &&
                    (secondarySet == null || !secondarySet.Contains(next)))
                {
                    return true;
                }
            }
            return false;
        }

        public static HashSet<Vector2Int> EnsureEscapeRoute(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int player,
            IReadOnlyCollection<Vector2Int> danger)
        {
            var result = new HashSet<Vector2Int>(danger);
            if (HasAdjacentSafeCell(walkable, player, result))
            {
                return result;
            }

            var walkableSet = walkable as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(walkable);
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = player + direction;
                if (walkableSet.Contains(next))
                {
                    result.Remove(next);
                    break;
                }
            }
            return result;
        }

        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public static HashSet<Vector2Int> CreateLaser(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int target,
            bool vertical)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in walkable)
            {
                if (vertical ? cell.x == target.x : cell.y == target.y)
                {
                    cells.Add(cell);
                }
            }
            return cells;
        }

        public static HashSet<Vector2Int> CreateMultiLaser(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int target,
            bool vertical,
            IReadOnlyList<int> offsets)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in walkable)
            {
                foreach (int offset in offsets)
                {
                    int targetLine = (vertical ? target.x : target.y) + offset;
                    if ((vertical ? cell.x : cell.y) == targetLine)
                    {
                        cells.Add(cell);
                        break;
                    }
                }
            }
            return cells;
        }

        public static HashSet<Vector2Int> CreateCombinedLaser(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int target,
            IReadOnlyList<int> verticalOffsets,
            IReadOnlyList<int> horizontalOffsets)
        {
            var cells = CreateMultiLaser(walkable, target, true, verticalOffsets);
            cells.UnionWith(CreateMultiLaser(walkable, target, false, horizontalOffsets));
            return cells;
        }

        public static HashSet<Vector2Int> CreateExplosion(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int target,
            int radius = 1)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in walkable)
            {
                if (Mathf.Abs(cell.x - target.x) <= radius && Mathf.Abs(cell.y - target.y) <= radius)
                {
                    cells.Add(cell);
                }
            }
            return cells;
        }
    }
}
