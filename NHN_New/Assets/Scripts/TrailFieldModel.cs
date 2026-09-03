using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike
{
    public enum MoveResult
    {
        Blocked,
        Moved,
        TrailStarted,
        TrailExtended,
        TrailReset,
        AttackReady
    }

    /// <summary>
    /// Grid rules are kept separate from presentation so the movement and
    /// trail-reset behaviour stays deterministic on mobile and in the editor.
    /// </summary>
    public sealed class TrailFieldModel
    {
        public const int Size = 17;

        public static int ScaleLegacyDistance(int distance)
        {
            return Mathf.RoundToInt(distance * (Size - 1) / 10f);
        }

        private readonly HashSet<Vector2Int> walkable = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> traversable = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> trailSet = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> trail = new List<Vector2Int>();
        private readonly List<Vector2Int> orderedCells = new List<Vector2Int>();

        public IReadOnlyCollection<Vector2Int> Walkable => walkable;
        public IReadOnlyCollection<Vector2Int> Blocked => blocked;
        public IReadOnlyCollection<Vector2Int> Traversable => traversable;
        public IReadOnlyList<Vector2Int> Trail => trail;
        public Vector2Int Player { get; private set; }
        public Vector2Int Start { get; private set; }
        public Vector2Int End { get; private set; }
        public bool IsTracing { get; private set; }
        public Vector2Int NavigationTarget => IsTracing ? End : Start;
        public int ShapeIndex { get; private set; }

        public void CreateField(int stage)
        {
            CreateField(stage, null);
        }

        public void CreateField(int stage, Vector2Int? requiredPlayerCell)
        {
            ShapeIndex = Mathf.Abs(stage) % 3;
            walkable.Clear();
            blocked.Clear();
            traversable.Clear();
            orderedCells.Clear();

            int center = Size / 2;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int dx = x - center;
                    int dy = y - center;
                    bool inside;

                    switch (ShapeIndex)
                    {
                        case 1: // triangle
                            int halfWidth = (Size - 1 - y) / 2;
                            inside = Mathf.Abs(dx) <= halfWidth;
                            break;
                        case 2: // eight-point grid star
                            int ax = Mathf.Abs(dx);
                            int ay = Mathf.Abs(dy);
                            int armWidth = ScaleLegacyDistance(1);
                            inside = (ax <= armWidth || ay <= armWidth || ax == ay) &&
                                ax <= center && ay <= center;
                            break;
                        default: // Scale the original rounded outline uniformly on both axes.
                            inside = (dx * dx + dy * dy) * 25 <= 27 * center * center;
                            break;
                    }

                    if (!inside)
                    {
                        continue;
                    }

                    var cell = new Vector2Int(x, y);
                    walkable.Add(cell);
                    orderedCells.Add(cell);
                }
            }

            if (requiredPlayerCell.HasValue)
            {
                Vector2Int corridor = requiredPlayerCell.Value;
                var fieldCenter = new Vector2Int(center, center);
                while (true)
                {
                    if (corridor.x >= 0 && corridor.x < Size && corridor.y >= 0 && corridor.y < Size && walkable.Add(corridor))
                    {
                        orderedCells.Add(corridor);
                    }

                    if (corridor == fieldCenter)
                    {
                        break;
                    }

                    if (corridor.x != fieldCenter.x)
                        corridor.x += corridor.x < fieldCenter.x ? 1 : -1;
                    else
                        corridor.y += corridor.y < fieldCenter.y ? 1 : -1;
                }
            }

            orderedCells.Sort((a, b) =>
            {
                int compareY = a.y.CompareTo(b.y);
                return compareY != 0 ? compareY : a.x.CompareTo(b.x);
            });
            RebuildTraversable();
        }

        public void SetBlockedCells(IEnumerable<Vector2Int> cells)
        {
            blocked.Clear();
            if (cells != null)
            {
                foreach (Vector2Int cell in cells)
                {
                    if (walkable.Contains(cell))
                    {
                        blocked.Add(cell);
                    }
                }
            }
            RebuildTraversable();
        }

        public void BeginRound(int round)
        {
            BeginRound(round, true);
        }

        public void BeginRound(int round, bool movePlayerToStart)
        {
            Vector2Int previousPlayer = Player;
            ClearTrail();
            var availableCells = new List<Vector2Int>();
            foreach (Vector2Int cell in orderedCells)
            {
                if (!blocked.Contains(cell))
                {
                    availableCells.Add(cell);
                }
            }
            if (availableCells.Count == 0)
            {
                throw new InvalidOperationException("The field has no traversable cells.");
            }

            int seedIndex = PositiveModulo(round * 17 + ShapeIndex * 11, availableCells.Count);
            if (!movePlayerToStart && availableCells.Count > 1 && availableCells[seedIndex] == previousPlayer)
            {
                seedIndex = (seedIndex + 1) % availableCells.Count;
            }
            Start = availableCells[seedIndex];
            End = FindFarthestCell(Start);
            Player = movePlayerToStart
                ? Start
                : traversable.Contains(previousPlayer) ? previousPlayer : FindClosestCell(previousPlayer);
            IsTracing = movePlayerToStart;
            if (movePlayerToStart)
            {
                AddTrail(Start);
            }
        }

        public MoveResult TryMove(Vector2Int direction)
        {
            if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
            {
                return MoveResult.Blocked;
            }

            Vector2Int target = Player + direction;
            if (!traversable.Contains(target))
            {
                return MoveResult.Blocked;
            }

            Player = target;

            if (!IsTracing)
            {
                if (target == Start)
                {
                    IsTracing = true;
                    AddTrail(target);
                    return MoveResult.TrailStarted;
                }

                return MoveResult.Moved;
            }

            if (trailSet.Contains(target))
            {
                ClearTrail();
                IsTracing = false;
                return MoveResult.TrailReset;
            }

            AddTrail(target);
            return target == End ? MoveResult.AttackReady : MoveResult.TrailExtended;
        }

        public bool IsWalkable(Vector2Int cell) => walkable.Contains(cell);
        public bool IsBlocked(Vector2Int cell) => blocked.Contains(cell);
        public bool IsTraversable(Vector2Int cell) => traversable.Contains(cell);
        public bool IsTrail(Vector2Int cell) => trailSet.Contains(cell);

        public bool TryPlacePlayer(Vector2Int cell)
        {
            if (!traversable.Contains(cell))
            {
                return false;
            }

            Player = cell;
            ClearTrail();
            return true;
        }

        private void AddTrail(Vector2Int cell)
        {
            trail.Add(cell);
            trailSet.Add(cell);
        }

        private void ClearTrail()
        {
            trail.Clear();
            trailSet.Clear();
            IsTracing = false;
        }

        private Vector2Int FindFarthestCell(Vector2Int origin)
        {
            var queue = new Queue<Vector2Int>();
            var distance = new Dictionary<Vector2Int, int>();
            queue.Enqueue(origin);
            distance[origin] = 0;
            Vector2Int farthest = origin;

            Vector2Int[] directions =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                if (distance[current] > distance[farthest])
                {
                    farthest = current;
                }

                foreach (Vector2Int direction in directions)
                {
                    Vector2Int next = current + direction;
                    if (!traversable.Contains(next) || distance.ContainsKey(next))
                    {
                        continue;
                    }

                    distance[next] = distance[current] + 1;
                    queue.Enqueue(next);
                }
            }

            return farthest;
        }

        private Vector2Int FindClosestCell(Vector2Int position)
        {
            Vector2Int closest = default;
            int closestDistance = int.MaxValue;
            foreach (Vector2Int cell in orderedCells)
            {
                if (blocked.Contains(cell))
                {
                    continue;
                }
                int distance = Mathf.Abs(cell.x - position.x) + Mathf.Abs(cell.y - position.y);
                if (distance < closestDistance)
                {
                    closest = cell;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        private void RebuildTraversable()
        {
            traversable.Clear();
            foreach (Vector2Int cell in walkable)
            {
                if (!blocked.Contains(cell))
                {
                    traversable.Add(cell);
                }
            }
        }

        private static int PositiveModulo(int value, int modulo)
        {
            int result = value % modulo;
            return result < 0 ? result + modulo : result;
        }
    }
}
