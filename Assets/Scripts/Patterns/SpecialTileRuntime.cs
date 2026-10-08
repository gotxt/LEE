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
        public PlayerTileStep Enter(Vector2Int from, Vector2Int to, SpecialTileDefinition tile, bool activated = false)
        {
            if (Math.Abs(to.x - from.x) + Math.Abs(to.y - from.y) != 1)
                throw new ArgumentException("Tile contact requires one successful adjacent move.");
            if (IsStunned) throw new InvalidOperationException("Cannot move while stunned.");
            bool fire = FireMovesRemaining > 0;
            FireMovesRemaining = Math.Max(0, FireMovesRemaining - 1);
            if (tile != null && (!tile.requiresCompletedAttack || activated))
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
        // State belongs to each placement, not to the shared ScriptableObject or cell below a newer layer.
        private sealed class Placement
        {
            public readonly SpecialTileDefinition Tile;
            public double ExpiresAt;
            public Placement(SpecialTileDefinition tile) { Tile = tile; }
        }
        private readonly Dictionary<Vector2Int, Placement> permanent = new Dictionary<Vector2Int, Placement>();
        private readonly SortedDictionary<int, Dictionary<Vector2Int, Placement>> layers = new SortedDictionary<int, Dictionary<Vector2Int, Placement>>();
        private double elapsed;
        private int nextId;
        private bool disposed;
        public SpecialTileField(IEnumerable<SpecialTilePlacement> placements)
        {
            if (placements != null) foreach (var entry in placements)
                if (entry != null && entry.tile != null) permanent[entry.cell] = new Placement(entry.tile);
        }
        private Placement PlacementAt(Vector2Int cell)
        {
            permanent.TryGetValue(cell, out var tile);
            foreach (var layer in layers.Values) if (layer.TryGetValue(cell, out var newer)) tile = newer;
            return tile;
        }
        public SpecialTileDefinition At(Vector2Int cell) => PlacementAt(cell)?.Tile;
        public bool IsActive(Vector2Int cell)
        {
            var entry = PlacementAt(cell);
            return entry != null && (!entry.Tile.requiresCompletedAttack || entry.ExpiresAt > elapsed);
        }
        public float ActiveSecondsRemaining(Vector2Int cell)
        {
            var entry = PlacementAt(cell);
            return entry == null ? 0 : (float)Math.Max(0, entry.ExpiresAt - elapsed);
        }
        public Sprite SpriteAt(Vector2Int cell)
        {
            var entry = PlacementAt(cell);
            return entry == null ? null : !entry.Tile.requiresCompletedAttack || entry.ExpiresAt > elapsed
                ? entry.Tile.sprite : entry.Tile.inactiveSprite;
        }
        // Call only after a completed player attack succeeds, never for partial trails or boss attacks.
        // Covered placements cannot be activated through vines; repeated hits refresh, not stack.
        public bool OnCompletedAttack(IEnumerable<Vector2Int> completedTrail)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SpecialTileField));
            if (completedTrail == null) throw new ArgumentNullException(nameof(completedTrail));
            bool changed = false;
            foreach (var cell in completedTrail)
            {
                var entry = PlacementAt(cell);
                if (entry == null || !entry.Tile.requiresCompletedAttack) continue;
                entry.ExpiresAt = elapsed + entry.Tile.activationSeconds;
                changed = true;
            }
            return changed;
        }
        // Advance hidden layers too: revealing an old fire must not restore expired activation.
        // Return true only when a sprite/contact state expires, avoiding a full board redraw every frame.
        public bool Advance(float delta)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SpecialTileField));
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            elapsed += delta;
            bool changed = Expire(permanent);
            foreach (var layer in layers.Values) changed |= Expire(layer);
            return changed;
        }
        private bool Expire(Dictionary<Vector2Int, Placement> entries)
        {
            bool changed = false;
            foreach (var entry in entries.Values)
                if (entry.ExpiresAt > 0 && entry.ExpiresAt <= elapsed) { entry.ExpiresAt = 0; changed = true; }
            return changed;
        }
        // Last live placement wins. Removing it reveals the previous/base tile.
        public IPatternLease Add(SpecialTileDefinition tile, IEnumerable<Vector2Int> cells, Action changed = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SpecialTileField));
            if (tile == null || cells == null) throw new ArgumentNullException();
            var errors = new List<string>(); tile.Validate(errors);
            if (errors.Count > 0) throw new ArgumentException(string.Join("\n", errors));
            var layer = new Dictionary<Vector2Int, Placement>();
            foreach (var cell in cells) layer[cell] = new Placement(tile);
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
