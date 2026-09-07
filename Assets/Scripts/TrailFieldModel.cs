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
        public const int MaxSize = 50;
        // Legacy coordinate canvas remains stable for existing assets and the hub.
        public const int Size = 17;
        public int FieldSize { get; private set; } = Size;
        public int GridSize => Math.Max(Size, FieldSize);
        public Vector2Int CenterCell => new Vector2Int(GridSize / 2, GridSize / 2);
        public static bool ContainsFieldBounds(Vector2Int cell, int size)
        {
            int offset = (Math.Max(Size, size) - size) / 2;
            return cell.x >= offset && cell.y >= offset && cell.x < offset + size && cell.y < offset + size;
        }
        private HashSet<Vector2Int> allowedStarts, allowedEnds;
        public void SetEndpointRegions(IEnumerable<Vector2Int> starts, IEnumerable<Vector2Int> ends)
        {
            allowedStarts = starts == null ? null : new HashSet<Vector2Int>(starts);
            allowedEnds = ends == null ? null : new HashSet<Vector2Int>(ends);
        }

        public bool HasEndpointPair(IEnumerable<Vector2Int> excluded)
        {
            var unavailable = new HashSet<Vector2Int>(excluded);
            Vector2Int? firstStart = null, firstEnd = null;
            int startCount = 0, endCount = 0;
            foreach (var cell in orderedCells)
            {
                if (unavailable.Contains(cell)) continue;
                if (allowedStarts == null || allowedStarts.Contains(cell)) { firstStart = cell; startCount++; }
                if (allowedEnds == null || allowedEnds.Contains(cell)) { firstEnd = cell; endCount++; }
            }
            return startCount > 0 && endCount > 0 && (startCount > 1 || endCount > 1 || firstStart != firstEnd);
        }

        public static int ScaleLegacyDistance(int distance, int fieldSize = Size)
        {
            return Mathf.RoundToInt(distance * (fieldSize - 1) / 10f);
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
            CreateField(stage, Size, requiredPlayerCell);
        }

        public void CreateField(int stage, int fieldSize, Vector2Int? requiredPlayerCell = null)
        {
            if (fieldSize < 5 || fieldSize > MaxSize)
                throw new ArgumentOutOfRangeException(nameof(fieldSize), "Arena size must be from 5 to 50.");
            FieldSize = fieldSize;
            ShapeIndex = Mathf.Abs(stage) % 3;
            walkable.Clear();
            blocked.Clear();
            traversable.Clear();
            orderedCells.Clear();

            ClearTrail();
            SetEndpointRegions(null, null);
            int center = GridSize / 2;
            float shapeCenter = (FieldSize - 1) * 0.5f;
            int fieldOffset = (GridSize - FieldSize) / 2;
            float geometricCenter = fieldOffset + shapeCenter;
            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    float dx = x - geometricCenter;
                    float dy = y - geometricCenter;
                    bool inside;

                    switch (ShapeIndex)
                    {
                        case 1: // triangle
                            int localY = y - fieldOffset;
                            float halfWidth = Mathf.Max(FieldSize % 2 == 0 ? 0.5f : 0f, (FieldSize - 1 - localY) / 2);
                            inside = localY >= 0 && localY < FieldSize && Mathf.Abs(dx) <= halfWidth;
                            break;
                        case 2: // eight-point grid star
                            float ax = Mathf.Abs(dx);
                            float ay = Mathf.Abs(dy);
                            int armWidth = ScaleLegacyDistance(1, FieldSize);
                            inside = (ax <= armWidth || ay <= armWidth || ax == ay) &&
                                ax <= shapeCenter && ay <= shapeCenter;
                            break;
                        default: // Scale the original rounded outline uniformly on both axes.
                            inside = Mathf.Abs(dx) <= shapeCenter && Mathf.Abs(dy) <= shapeCenter &&
                                (dx * dx + dy * dy) * 25 <= 27 * shapeCenter * shapeCenter;
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
                    if (corridor.x >= 0 && corridor.x < GridSize && corridor.y >= 0 && corridor.y < GridSize && walkable.Add(corridor))
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

        public void CreateCustomField(int fieldSize, IEnumerable<Vector2Int> cells)
        {
            CreateField(0, fieldSize);
            ShapeIndex = 3;
            walkable.Clear();
            orderedCells.Clear();
            ClearTrail();
            if (cells != null)
                foreach (var cell in cells)
                    if (ContainsFieldBounds(cell, fieldSize) && walkable.Add(cell))
                        orderedCells.Add(cell);
            orderedCells.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
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

        public void BeginRound(int round, bool movePlayerToStart, Vector2Int? initialPlayerCell = null)
        {
            Vector2Int previousPlayer = initialPlayerCell ?? Player;
            if (initialPlayerCell.HasValue && !traversable.Contains(previousPlayer))
                throw new InvalidOperationException("Player spawn must be on a traversable floor tile.");
            ClearTrail();
            var availableCells = new List<Vector2Int>();
            foreach (Vector2Int cell in orderedCells)
            {
                if (!blocked.Contains(cell) && (allowedStarts == null || allowedStarts.Contains(cell)))
                {
                    availableCells.Add(cell);
                }
            }
            if (availableCells.Count == 0)
            {
                throw new InvalidOperationException("No traversable START tiles in the allowed region.");
            }

            if (!HasEndpointPair(blocked))
                throw new InvalidOperationException("Allowed START/END regions need two distinct traversable tiles.");
            int seedIndex = PositiveModulo(round * 17 + ShapeIndex * 11, availableCells.Count);
            if (!movePlayerToStart && availableCells.Count > 1 && availableCells[seedIndex] == previousPlayer)
            {
                seedIndex = (seedIndex + 1) % availableCells.Count;
            }
            Vector2Int retainedPlayer = traversable.Contains(previousPlayer) ? previousPlayer : FindClosestCell(previousPlayer);
            var reachable = !movePlayerToStart || initialPlayerCell.HasValue ? ReachableFrom(retainedPlayer) : null;
            var failedComponent = new HashSet<Vector2Int>();
            for (int attempt = 0; attempt < availableCells.Count; attempt++)
            {
                Vector2Int start = availableCells[(seedIndex + attempt) % availableCells.Count];
                if (failedComponent.Contains(start) || (reachable != null && !reachable.Contains(start))) continue;
                Vector2Int end = FindFarthestCell(start);
                if (end == start)
                {
                    // If this start itself is a permitted END, another start in this
                    // component can still form a valid pair with it.
                    if (allowedEnds == null || !allowedEnds.Contains(start))
                        failedComponent.UnionWith(ReachableFrom(start));
                    continue;
                }
                Start = start;
                End = end;
                Player = initialPlayerCell.HasValue || !movePlayerToStart ? retainedPlayer : Start;
                IsTracing = Player == Start;
                if (IsTracing) AddTrail(Start);
                return;
            }
            throw new InvalidOperationException("Allowed START/END regions need two distinct, reachable floor tiles.");
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
                if (current != origin && (allowedEnds == null || allowedEnds.Contains(current)) &&
                    (farthest == origin || distance[current] > distance[farthest]))
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

        private HashSet<Vector2Int> ReachableFrom(Vector2Int origin)
        {
            var visited = new HashSet<Vector2Int>();
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(origin);
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                if (!traversable.Contains(cell) || !visited.Add(cell)) continue;
                pending.Enqueue(cell + Vector2Int.up); pending.Enqueue(cell + Vector2Int.down);
                pending.Enqueue(cell + Vector2Int.left); pending.Enqueue(cell + Vector2Int.right);
            }
            return visited;
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
