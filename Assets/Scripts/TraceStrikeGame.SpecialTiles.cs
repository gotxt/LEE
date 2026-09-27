using System;
using System.Collections.Generic;
using NHN.TraceStrike.Patterns;
using UnityEngine;

namespace NHN.TraceStrike
{
    public sealed partial class TraceStrikeGame : ISpecialTileHost
    {
        private SpecialTileField placedSpecialTiles;
        private readonly SpecialTilePlayerState tilePlayerState = new SpecialTilePlayerState();
        private bool IsMovementFrozen => movementFrozen || tilePlayerState.IsStunned;
        public int FireMovesRemaining => tilePlayerState.FireMovesRemaining;

        private void StartSpecialTiles()
        {
            StopSpecialTiles();
            placedSpecialTiles = new SpecialTileField(activeBoss.arena.specialTiles);
        }
        private void StopSpecialTiles()
        {
            placedSpecialTiles?.Dispose(); placedSpecialTiles = null;
            tilePlayerState.Reset();
            if (playerHealthText != null) playerHealthText.text = "♥  HP 1";
        }
        private void TickSpecialTiles(float delta)
        {
            if (titleActive || tutorialActive || hubActive || playerDead || gameCleared) return;
            // Same gameplay clock as attacks/growth: inputLocked pauses it; stun does not.
            if (!inputLocked) tilePlayerState.Advance(delta);
            if (tilePlayerState.IsStunned) mainPlayerImage.color = new Color(.45f, .9f, .3f);
            else if (FireMovesRemaining > 0) mainPlayerImage.color = new Color(1, .55f, .18f);
            playerHealthText.text = "♥  HP 1" + (FireMovesRemaining > 0 ? "  · 불 " + FireMovesRemaining + "칸" : "") +
                (tilePlayerState.IsStunned ? "  · 기절 " + tilePlayerState.StunRemaining.ToString("0.0") + "초" : "");
        }
        private bool EnterSpecialTile(Vector2Int from, Vector2Int to)
        {
            try
            {
                var step = tilePlayerState.Enter(from, to, placedSpecialTiles?.At(to));
                mechanicSession?.OnPlayerStep(step);
                return true;
            }
            catch (Exception error)
            {
                CancelTimeline(); StopMechanics(); inputLocked = true;
                Debug.LogException(error, this);
                statusText.text = "특수 타일 실행 오류 — Console을 확인하세요";
                return false;
            }
        }
        public IPatternLease PlaceSpecialTiles(SpecialTileDefinition tile, IReadOnlyCollection<Vector2Int> cells)
        {
            if (placedSpecialTiles == null) throw new InvalidOperationException("No active special-tile field.");
            var valid = new HashSet<Vector2Int>(cells);
            valid.IntersectWith(model.Walkable); valid.ExceptWith(bossOccupiedCells);
            return placedSpecialTiles.Add(tile, valid, RefreshBoard);
        }
        private void DrawPlacedSpecialTile(int x, int y)
        {
            var tile = placedSpecialTiles?.At(new Vector2Int(x, y));
            if (tile == null) return;
            var root = specialItemVisuals[x, y];
            root.gameObject.SetActive(true);
            float size = mainCellSize * .55f;
            root.sizeDelta = Vector2.one * size;
            root.anchoredPosition = new Vector2(-.2f, -.2f) * mainCellSize;
            specialItemImages[x, y].color = tile.color;
            var icon = specialItemIconImages[x, y];
            icon.gameObject.SetActive(tile.sprite != null);
            if (tile.sprite != null)
            {
                icon.sprite = tile.sprite; icon.color = Color.white;
                icon.rectTransform.sizeDelta = Vector2.one * size;
                icon.rectTransform.anchoredPosition = Vector2.zero;
            }
            var label = specialItemLabels[x, y];
            label.gameObject.SetActive(tile.sprite == null);
            label.text = tile.marker; label.color = Color.white;
            label.fontSize = Mathf.Max(12, Mathf.RoundToInt(size * .65f));
            label.rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
