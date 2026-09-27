using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    public readonly struct PlayerTileStep
    {
        public readonly Vector2Int From, To;
        // Includes the last charged move even when the remaining count becomes zero.
        public readonly bool HasFireOnArrival;
        public PlayerTileStep(Vector2Int from, Vector2Int to, bool fire)
        { From = from; To = to; HasFireOnArrival = fire; }
    }

    public sealed class SpecialTilePlayerState
    {
        public int FireMovesRemaining { get; private set; }
        public float StunRemaining { get; private set; }
        public bool IsStunned => StunRemaining > 0;

        // Call only for successful four-way movement, before resolving END attacks.
        public PlayerTileStep Enter(Vector2Int from, Vector2Int to, SpecialTileDefinition tile)
        {
            if (Math.Abs(to.x - from.x) + Math.Abs(to.y - from.y) != 1)
                throw new ArgumentException("Tile contact requires one successful adjacent move.");
            if (IsStunned) throw new InvalidOperationException("Cannot move while stunned.");
            bool fire = FireMovesRemaining > 0;
            FireMovesRemaining = Math.Max(0, FireMovesRemaining - 1);
            if (tile != null)
            {
                if (tile.fireMoveCount > 0) { FireMovesRemaining = tile.fireMoveCount; fire = true; }
                StunRemaining = Mathf.Max(StunRemaining, tile.stunSeconds);
            }
            return new PlayerTileStep(from, to, fire);
        }
        public void Advance(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            StunRemaining = Mathf.Max(0, StunRemaining - delta);
        }
        public void Reset() { FireMovesRemaining = 0; StunRemaining = 0; }
    }

    // Optional host extension for future growth mechanics, not tied to a boss ID.
    public interface ISpecialTileHost
    {
        int FireMovesRemaining { get; }
        IPatternLease PlaceSpecialTiles(SpecialTileDefinition tile, IReadOnlyCollection<Vector2Int> cells);
    }

    public sealed class SpecialTileField : IDisposable
    {
        private readonly Dictionary<Vector2Int, SpecialTileDefinition> permanent = new Dictionary<Vector2Int, SpecialTileDefinition>();
        private readonly SortedDictionary<int, Dictionary<Vector2Int, SpecialTileDefinition>> layers = new SortedDictionary<int, Dictionary<Vector2Int, SpecialTileDefinition>>();
        private int nextId;
        private bool disposed;
        public SpecialTileField(IEnumerable<SpecialTilePlacement> placements)
        {
            if (placements != null) foreach (var entry in placements)
                if (entry != null && entry.tile != null) permanent[entry.cell] = entry.tile;
        }
        public SpecialTileDefinition At(Vector2Int cell)
        {
            permanent.TryGetValue(cell, out var tile);
            foreach (var layer in layers.Values) if (layer.TryGetValue(cell, out var newer)) tile = newer;
            return tile;
        }
        // Last live placement wins. Removing it reveals the previous/base tile.
        public IPatternLease Add(SpecialTileDefinition tile, IEnumerable<Vector2Int> cells, Action changed = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SpecialTileField));
            if (tile == null || cells == null) throw new ArgumentNullException();
            var errors = new List<string>(); tile.Validate(errors);
            if (errors.Count > 0) throw new ArgumentException(string.Join("\n", errors));
            var layer = new Dictionary<Vector2Int, SpecialTileDefinition>();
            foreach (var cell in cells) layer[cell] = tile;
            int id = ++nextId; layers.Add(id, layer); changed?.Invoke();
            return new TileLease(() => { if (!disposed && layers.Remove(id)) changed?.Invoke(); });
        }
        public void Dispose() { disposed = true; permanent.Clear(); layers.Clear(); }
        private sealed class TileLease : IPatternLease
        {
            private Action cleanup;
            public TileLease(Action cleanup) { this.cleanup = cleanup; }
            public void SetProgress(float progress) { }
            public void Dispose() { var action = cleanup; cleanup = null; action?.Invoke(); }
        }
    }
}
