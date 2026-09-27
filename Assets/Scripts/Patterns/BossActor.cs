using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    [Serializable]
    public sealed class BossSocket
    {
        public string name;
        public Transform target;
    }

    // Place this component on the one prefab representing a boss.
    public sealed class BossActor : MonoBehaviour
    {
        public Animator animator;
        public List<BossSocket> sockets = new List<BossSocket>();
        public Animator Animator => animator != null ? animator : GetComponentInChildren<Animator>(true);
        public Transform Socket(string socket)
        {
            if (string.IsNullOrEmpty(socket)) return transform;
            foreach (var item in sockets)
                if (item != null && item.name == socket && item.target != null) return item.target;
            throw new InvalidOperationException("Unknown boss socket: " + socket);
        }
    }

    [Serializable]
    public sealed class BossVisualDefinition
    {
        public BossActor prefab;
        [Tooltip("Grid coordinates; empty floor cells are valid.")]
        public Vector2 position = new Vector2(8, 8);
        [Min(0.01f), Tooltip("Prefab units measured in tiles.")]
        public float size = 1f;
        [Tooltip("Blocked floor tiles. The boss position is the bottom-row foot anchor, horizontally centred (even widths extend one extra tile right). (0, 0) disables blocking.")]
        public Vector2Int footprintSize = Vector2Int.zero;
        [Min(0)] public int animationLayer;
        public string idleState = "";

        public HashSet<Vector2Int> OccupiedCells()
        {
            var cells = new HashSet<Vector2Int>();
            if (footprintSize.x <= 0 || footprintSize.y <= 0 ||
                footprintSize.x > TrailFieldModel.MaxSize || footprintSize.y > TrailFieldModel.MaxSize)
                return cells;
            var anchor = Vector2Int.RoundToInt(position);
            int left = anchor.x - (footprintSize.x - 1) / 2;
            int bottom = anchor.y;
            for (int y = 0; y < footprintSize.y; y++)
            for (int x = 0; x < footprintSize.x; x++)
                cells.Add(new Vector2Int(left + x, bottom + y));
            return cells;
        }

        public void Validate(List<string> errors, BossArenaDefinition arena)
        {
            if (!BossEvent.Finite(size) || size <= 0 || !BossEvent.Finite(position.x) || !BossEvent.Finite(position.y))
                errors.Add("Boss visual: size and position must be finite; size must be positive.");
            if (arena != null && (position.x < 0 || position.y < 0 || position.x > arena.GridSize - 1 || position.y > arena.GridSize - 1))
                errors.Add("Boss visual: position must be inside the coordinate grid (empty cells are allowed).");
            if (footprintSize != Vector2Int.zero)
            {
                if (prefab == null || footprintSize.x <= 0 || footprintSize.y <= 0 ||
                    footprintSize.x > TrailFieldModel.MaxSize || footprintSize.y > TrailFieldModel.MaxSize)
                    errors.Add("Boss footprint: assign a prefab and use positive dimensions within the grid limit, or (0, 0) to disable blocking.");
                else if (!BossEvent.Finite(position.x) || !BossEvent.Finite(position.y) ||
                    Vector2.Distance(position, Vector2Int.RoundToInt(position)) > 0.001f)
                    errors.Add("Boss footprint: boss position must be on an integer grid cell.");
                else if (arena != null && arena.size >= 5 && arena.size <= TrailFieldModel.MaxSize)
                {
                    var occupied = OccupiedCells();
                    var floor = arena.GetCells();
                    if (!occupied.IsSubsetOf(floor))
                        errors.Add("Boss footprint: every occupied cell must be a floor tile.");
                    if (arena.overridePlayerStart && occupied.Contains(arena.playerStart))
                        errors.Add("Boss footprint: player spawn cannot overlap the boss.");
                    var model = new TrailFieldModel();
                    arena.ApplyTo(model);
                    model.SetBlockedCells(occupied);
                    try { model.BeginRound(0, true, arena.overridePlayerStart ? (Vector2Int?)arena.playerStart : null); }
                    catch (InvalidOperationException)
                    { errors.Add("Boss footprint: blocking must leave a reachable START/END pair and player spawn."); }
                    var open = new HashSet<Vector2Int>(model.Traversable);
                    if (open.Count > 0)
                    {
                        var queue = new Queue<Vector2Int>();
                        foreach (var cell in open) { queue.Enqueue(cell); break; }
                        var visited = new HashSet<Vector2Int>();
                        while (queue.Count > 0)
                        {
                            var cell = queue.Dequeue();
                            if (!open.Contains(cell) || !visited.Add(cell)) continue;
                            queue.Enqueue(cell + Vector2Int.up); queue.Enqueue(cell + Vector2Int.down);
                            queue.Enqueue(cell + Vector2Int.left); queue.Enqueue(cell + Vector2Int.right);
                        }
                        if (visited.Count != open.Count)
                            errors.Add("Boss footprint: remaining floor tiles must stay connected.");
                    }
                }
            }
            if (prefab == null) return; // Existing bosses retain their original presentation.
            var names = new HashSet<string>();
            foreach (var socket in prefab.sockets)
                if (socket == null || string.IsNullOrWhiteSpace(socket.name) || !names.Add(socket.name) ||
                    socket.target == null || !socket.target.IsChildOf(prefab.transform))
                    errors.Add("Boss visual: sockets need unique names and a transform inside the prefab.");
            if (prefab.Animator != null && (prefab.Animator.runtimeAnimatorController == null ||
                string.IsNullOrWhiteSpace(idleState) || animationLayer < 0))
                errors.Add("Boss visual: assign an Animator controller, layer and full idle state path.");
        }
    }

    public interface IBossPatternHost
    {
        IPatternLease BossAnimation(BossAnimationEvent action);
        IPatternLease BossVfx(BossVfxEvent action);
        IPatternLease BossMotion(BossMotionEvent action, float duration);
    }
}
