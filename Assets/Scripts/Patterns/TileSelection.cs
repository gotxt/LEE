using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    public enum TileShape { Cells, All, Cross, Diamond, Diagonal, Combined, Horizontal, Vertical, Rectangle, Checker }
    public enum TileAnchor { Center, Player, Origin, Absolute }

    [Serializable]
    public sealed class TileSelection
    {
        public TileShape shape;
        public TileAnchor anchor = TileAnchor.Center;
        public Vector2Int offset;
        [Min(0)] public int radius = 2;
        public List<Vector2Int> cells = new List<Vector2Int> { Vector2Int.zero };
        [Tooltip("Reuse the first resolved cells under this key within this sequence. Warning and damage should share a key.")]
        public string snapshotKey = "";
        public bool ensureEscape;

        public HashSet<Vector2Int> Resolve(PatternContext context)
        {
            if (!string.IsNullOrEmpty(snapshotKey) && context.Selections.TryGetValue(snapshotKey, out var saved))
                return new HashSet<Vector2Int>(saved);
            Vector2Int origin = (anchor == TileAnchor.Center ? context.Host.CenterCell :
                anchor == TileAnchor.Player ? context.Host.PlayerCell :
                anchor == TileAnchor.Origin ? context.Origin : Vector2Int.zero) + offset;
            var result = new HashSet<Vector2Int>();
            foreach (var cell in context.Host.Walkable)
            {
                Vector2Int p = cell - origin;
                int ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y);
                bool inside = false;
                switch (shape)
                {
                    case TileShape.Cells: inside = cells.Contains(p); break;
                    case TileShape.All: inside = true; break;
                    case TileShape.Cross: inside = p.x == 0 || p.y == 0; break;
                    case TileShape.Diamond: inside = ax + ay == radius; break;
                    case TileShape.Diagonal: inside = ax == ay; break;
                    case TileShape.Combined: inside = p.x == 0 || p.y == 0 || ax + ay == radius; break;
                    case TileShape.Horizontal: inside = ay % 2 == 0; break;
                    case TileShape.Vertical: inside = ax % 2 == 0; break;
                    case TileShape.Rectangle: inside = ax <= radius && ay <= radius; break;
                    case TileShape.Checker: inside = ax <= radius && ay <= radius && (ax + ay) % 2 == 0 && ax + ay > 0; break;
                }
                if (inside) result.Add(cell);
            }
            if (ensureEscape) result = BossPatternRules.EnsureEscapeRoute(context.Host.Traversable, context.Host.PlayerCell, result);
            if (!string.IsNullOrEmpty(snapshotKey)) context.Selections[snapshotKey] = new HashSet<Vector2Int>(result);
            return result;
        }
    }
}
