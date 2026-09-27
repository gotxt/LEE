using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    // Shared asset: appearance and contact effects, never per-battle mutable state.
    [CreateAssetMenu(fileName = "TileData_NewTile", menuName = "Trace Strike/Special Tile")]
    public sealed class SpecialTileDefinition : ScriptableObject
    {
        public string displayName = "특수 타일";
        public Sprite sprite;
        public Color color = Color.white;
        public string marker = "*";
        [Min(0)] public float stunSeconds;
        [Min(0)] public int fireMoveCount;

        public void Validate(List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(displayName)) errors.Add(name + ": 특수 타일 이름을 지정하세요.");
            if (float.IsNaN(stunSeconds) || float.IsInfinity(stunSeconds) || stunSeconds < 0 || fireMoveCount < 0)
                errors.Add(displayName + ": 효과 수치는 유한한 0 이상의 값이어야 합니다.");
            if (stunSeconds == 0 && fireMoveCount == 0) errors.Add(displayName + ": 접촉 효과를 하나 이상 지정하세요.");
        }
    }

    [Serializable]
    public sealed class SpecialTilePlacement
    {
        public Vector2Int cell;
        public SpecialTileDefinition tile;
    }

    public static class SpecialTileValidation
    {
        public static void Validate(BossEncounterDefinition boss, List<string> errors)
        {
            var floor = boss.arena.GetCells();
            var blocked = boss.bossVisual?.OccupiedCells();
            var cells = new HashSet<Vector2Int>();
            var definitions = new HashSet<SpecialTileDefinition>();
            foreach (var entry in boss.arena.specialTiles ?? new List<SpecialTilePlacement>())
            {
                if (entry == null || entry.tile == null) { errors.Add("특수 타일: 종류가 지정되지 않은 배치가 있습니다."); continue; }
                if (!cells.Add(entry.cell)) errors.Add("특수 타일: 한 칸에는 한 종류만 배치하세요. " + entry.cell);
                if (!floor.Contains(entry.cell) || (blocked?.Contains(entry.cell) ?? false))
                    errors.Add("특수 타일: 바닥이 없거나 보스가 점유한 칸입니다. " + entry.cell);
                if (definitions.Add(entry.tile)) entry.tile.Validate(errors);
            }
        }
    }
}
