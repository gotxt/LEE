using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike
{
    public static class CrystalRules
    {
        public const int CrystalCount = 4;
        public const int BlastRange = 2;
        public const float WarningSeconds = 0.7f;
        public const float StandardIntervalSeconds = 5f;
        public const float EnragedIntervalSeconds = 4f;

        public static float AttackIntervalSeconds(bool relocated, int crystalIndex)
        {
            return relocated && crystalIndex >= CrystalCount / 2
                ? EnragedIntervalSeconds
                : StandardIntervalSeconds;
        }

        public static List<Vector2Int> CreateCardinalLayout(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int center)
        {
            var walkableSet = walkable as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(walkable);
            var result = new List<Vector2Int>();
            Vector2Int[] directions =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            foreach (Vector2Int direction in directions)
            {
                Vector2Int edge = center;
                while (walkableSet.Contains(edge + direction))
                {
                    edge += direction;
                }
                Vector2Int oneCellInside = edge - direction;
                if (oneCellInside != center && walkableSet.Contains(oneCellInside))
                {
                    result.Add(oneCellInside);
                }
            }
            return result;
        }

        public static List<Vector2Int> CreateRandomLayout(
            IReadOnlyCollection<Vector2Int> traversable,
            IReadOnlyCollection<Vector2Int> excluded,
            int seed)
        {
            var excludedSet = excluded as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(excluded);
            var candidates = new List<Vector2Int>();
            foreach (Vector2Int cell in traversable)
            {
                if (!excludedSet.Contains(cell))
                {
                    candidates.Add(cell);
                }
            }
            candidates.Sort((a, b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            var random = new System.Random(seed);
            var result = new List<Vector2Int>();
            while (result.Count < CrystalCount && candidates.Count > 0)
            {
                int index = random.Next(candidates.Count);
                Vector2Int candidate = candidates[index];
                candidates.RemoveAt(index);
                bool separated = true;
                foreach (Vector2Int existing in result)
                {
                    if (Mathf.Abs(candidate.x - existing.x) + Mathf.Abs(candidate.y - existing.y) < 3)
                    {
                        separated = false;
                        break;
                    }
                }
                if (separated)
                {
                    result.Add(candidate);
                }
            }

            if (result.Count < CrystalCount)
            {
                foreach (Vector2Int cell in traversable)
                {
                    if (result.Count >= CrystalCount)
                    {
                        break;
                    }
                    if (!excludedSet.Contains(cell) && !result.Contains(cell))
                    {
                        result.Add(cell);
                    }
                }
            }
            return result;
        }

        public static HashSet<Vector2Int> CreateCheckerBlast(
            IReadOnlyCollection<Vector2Int> traversable,
            Vector2Int center,
            int range = BlastRange)
        {
            var result = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in traversable)
            {
                int dx = Mathf.Abs(cell.x - center.x);
                int dy = Mathf.Abs(cell.y - center.y);
                if (dx <= range && dy <= range && cell != center && ((dx + dy) & 1) == 0)
                {
                    result.Add(cell);
                }
            }
            return result;
        }
    }
}
