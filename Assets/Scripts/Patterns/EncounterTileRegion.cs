using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    [Serializable]
    public sealed class EncounterTileRegion
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "새 영역";
        public Color color = new Color(0.4f, 0.8f, 0.5f, 0.4f);
        public List<Vector2Int> cells = new List<Vector2Int>();
    }

    // Optional, so existing hosts and shared patterns keep their previous behaviour.
    public interface IEncounterRegionHost
    {
        IReadOnlyList<EncounterTileRegion> TileRegions { get; }
    }

    public static class EncounterRegionRules
    {
        // Distance to safety through actual four-way corridors, not geometric distance.
        public static Dictionary<Vector2Int, int> EscapeDistances(IEnumerable<Vector2Int> traversable,
            IEnumerable<Vector2Int> attacked)
        {
            var open = new HashSet<Vector2Int>(traversable);
            var danger = new HashSet<Vector2Int>(attacked);
            var distances = new Dictionary<Vector2Int, int>();
            var queue = new Queue<Vector2Int>();
            foreach (var cell in open)
                if (!danger.Contains(cell)) { distances[cell] = 0; queue.Enqueue(cell); }
            var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (!open.Contains(next) || distances.ContainsKey(next)) continue;
                    distances[next] = distances[cell] + 1; queue.Enqueue(next);
                }
            }
            return distances;
        }

        public static HashSet<Vector2Int> Capture(TileSelection tiles, PatternContext context)
        {
            var regions = (context.Host as IEncounterRegionHost)?.TileRegions;
            if (regions == null || tiles.regionIds == null || tiles.regionIds.Count == 0)
                throw new InvalidOperationException("영역 공격: 보스 공통 영역과 후보 영역을 지정하세요.");
            var ids = new HashSet<string>(tiles.regionIds);
            foreach (var id in ids)
                if (!regions.Any(r => r != null && r.id == id))
                    throw new InvalidOperationException("영역 공격: 없는 영역 ID " + id);
            // Serialized encounter order is the explicit priority, never HashSet order.
            var chosen = regions.FirstOrDefault(r => r != null && ids.Contains(r.id) &&
                r.cells != null && r.cells.Contains(context.Host.PlayerCell));
            var result = chosen == null ? new HashSet<Vector2Int>() : new HashSet<Vector2Int>(chosen.cells);
            result.IntersectWith(context.Host.Walkable);
            context.SelectedRegionIds[tiles.snapshotKey] = chosen?.id ?? "";
            return result;
        }

        public static void ValidateDefinitions(BossEncounterDefinition boss, List<string> errors)
        {
            var ids = new HashSet<string>();
            var floor = boss.arena?.GetCells();
            var blocked = boss.bossVisual?.OccupiedCells();
            foreach (var region in boss.tileRegions ?? new List<EncounterTileRegion>())
            {
                if (region == null || string.IsNullOrWhiteSpace(region.id) || !ids.Add(region.id))
                { errors.Add("보스 공통 영역: 비어 있거나 중복된 ID가 있습니다."); continue; }
                if (string.IsNullOrWhiteSpace(region.name)) errors.Add("보스 공통 영역: 이름을 지정하세요.");
                if (region.cells == null || region.cells.Count == 0)
                    errors.Add(region.name + ": 영역에 타일을 칠하세요.");
                else if (region.cells.Any(c => floor == null || !floor.Contains(c) || (blocked?.Contains(c) ?? false)))
                    errors.Add(region.name + ": 빈 바닥 또는 보스 점유칸이 포함되어 있습니다. 영역을 수정하세요.");
            }
        }

        public static void ValidateReferences(IPatternTimeline timeline, BossEncounterDefinition boss, List<string> errors)
        {
            var ids = new HashSet<string>((boss.tileRegions ?? new List<EncounterTileRegion>())
                .Where(r => r != null).Select(r => r.id));
            foreach (var clip in timeline.Clips)
            {
                if (clip == null || !clip.enabled) continue;
                var tiles = Tiles(clip);
                if (tiles?.shape == TileShape.PlayerRegion && tiles.regionIds != null && tiles.regionIds.Any(id => !ids.Contains(id)))
                    errors.Add(timeline.TimelineName + "/" + clip.label + ": 삭제되었거나 없는 보스 공통 영역을 참조합니다.");
            }
        }

        public static TileSelection Tiles(PatternClip clip) =>
            clip?.action?.GetType().GetField("tiles")?.GetValue(clip.action) as TileSelection;

        public static void ValidateTimeline(IReadOnlyList<PatternClip> clips, List<string> errors)
        {
            var active = clips.Where(c => c != null && c.enabled).ToList();
            foreach (var clip in active)
            {
                var tiles = Tiles(clip);
                if (tiles?.shape != TileShape.PlayerRegion) continue;
                if (string.IsNullOrWhiteSpace(tiles.snapshotKey) || tiles.regionIds == null || tiles.regionIds.Count == 0)
                { errors.Add(clip.label + ": 영역 공격에는 영역 연결 키와 후보 영역이 필요합니다."); continue; }
                if (tiles.ensureEscape || !string.IsNullOrEmpty(tiles.locationGroupId) || tiles.offset != Vector2Int.zero)
                    errors.Add(clip.label + ": 공통 영역은 고정 좌표 전체를 사용합니다. 위치 그룹/보정/탈출 보조를 해제하세요.");
                var linked = active.Where(c => Tiles(c)?.snapshotKey == tiles.snapshotKey).ToList();
                var warnings = linked.Where(c => c.action is WarningEvent).ToList();
                if (warnings.Count != 1)
                { errors.Add(clip.label + ": 영역 연결 키마다 Warning이 정확히 하나 필요합니다."); continue; }
                var warning = warnings[0];
                if (linked.Any(c => Tiles(c).shape != TileShape.PlayerRegion ||
                    !new HashSet<string>(tiles.regionIds).SetEquals(Tiles(c).regionIds ?? new List<string>())))
                    errors.Add(clip.label + ": 같은 키의 경고/공격/효과는 동일한 후보 영역을 사용해야 합니다.");
                if (clip.start < warning.start || (clip != warning && clip.start == warning.start && active.IndexOf(clip) < active.IndexOf(warning)))
                    errors.Add(clip.label + ": 영역을 고정하는 Warning보다 먼저 실행할 수 없습니다.");
            }
        }
    }
}
