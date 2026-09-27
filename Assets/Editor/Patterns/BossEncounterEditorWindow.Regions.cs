#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NHN.TraceStrike.Patterns;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    public sealed partial class BossEncounterEditorWindow
    {
        private int selectedRegion, regionBrush;
        private Vector2 regionScroll;
        private readonly TilePaintStroke regionStroke = new TilePaintStroke();
        private PatternContext previewContext;
        private string regionEscapeReport;

        private void DrawRegionPainter()
        {
            EditorGUILayout.LabelField("보스 공통 영역", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("영역을 추가하고 이름을 붙인 뒤 맵에 클릭·드래그해서 칠하세요. 바닥·타일 이미지·스폰은 바뀌지 않습니다.\n겹친 타일은 목록의 위쪽 영역이 우선합니다. 어느 영역에도 속하지 않으면 공격하지 않습니다.", MessageType.Info);
            regionScroll = EditorGUILayout.BeginScrollView(regionScroll);
            var regions = encounter.tileRegions;
            if (GUILayout.Button("+ 이름 붙인 영역 추가"))
            {
                BeginPaint("Add named region");
                regions.Add(new EncounterTileRegion { name = "영역 " + (regions.Count + 1) });
                selectedRegion = regions.Count - 1; Changed();
            }
            selectedRegion = Mathf.Clamp(selectedRegion, 0, Mathf.Max(0, regions.Count - 1));
            for (int i = 0; i < regions.Count; i++)
                if (GUILayout.Toggle(i == selectedRegion, (i + 1) + ". " + regions[i].name + "  (" + regions[i].cells.Count + "칸)", EditorStyles.miniButton))
                    selectedRegion = i;
            if (regions.Count == 0) { EditorGUILayout.EndScrollView(); return; }
            var region = regions[selectedRegion];
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("영역 이름", region.name);
            Color tint = EditorGUILayout.ColorField("편집 표시 색", region.color);
            if (EditorGUI.EndChangeCheck())
            { BeginPaint("Rename region"); region.name = name; region.color = tint; Changed(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selectedRegion == 0))
                    if (GUILayout.Button("우선순위 ↑")) MoveRegion(-1);
                using (new EditorGUI.DisabledScope(selectedRegion == regions.Count - 1))
                    if (GUILayout.Button("우선순위 ↓")) MoveRegion(1);
                if (GUILayout.Button("영역 삭제") && EditorUtility.DisplayDialog("영역 삭제",
                    "이 영역을 쓰는 공격의 후보 목록도 수정해야 합니다. Ctrl+Z로 되돌릴 수 있습니다.", "삭제", "취소"))
                { BeginPaint("Delete region"); regions.RemoveAt(selectedRegion); Changed(); GUIUtility.ExitGUI(); }
            }
            regionBrush = GUILayout.Toolbar(regionBrush, new[] { "타일 칠하기", "지우개" });
            EditorGUILayout.LabelField("우클릭 또는 Shift+드래그: 지우기 · Ctrl+Z: 한 획 되돌리기 · 빨간 표시: 중복", EditorStyles.miniLabel);
            var floor = encounter.arena.GetCells();
            var blocked = encounter.bossVisual?.OccupiedCells() ?? new HashSet<Vector2Int>();
            var lookup = encounter.arena.BuildTileSpriteLookup();
            var memberships = regions.SelectMany(r => r.cells.Distinct()).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            float edge = Mathf.Max(448, encounter.arena.GridSize * 18);
            Rect board = GUILayoutUtility.GetRect(edge, edge, GUILayout.ExpandWidth(false));
            for (int y = 0; y < encounter.arena.GridSize; y++)
            for (int x = 0; x < encounter.arena.GridSize; x++)
            {
                var cell = new Vector2Int(x, y);
                var rect = PatternPreviewGridGUI.CellRect(board, x, y, encounter.arena.GridSize);
                EditorGUI.DrawRect(rect, floor.Contains(cell) ? new Color(0.27f, 0.3f, 0.35f) : new Color(0.1f, 0.1f, 0.12f));
                if (floor.Contains(cell)) PatternPreviewGridGUI.DrawTileSprite(rect, encounter.arena.ResolveTileSprite(cell, lookup));
                var owner = regions.FirstOrDefault(r => r.cells.Contains(cell));
                if (owner != null) { var color = owner.color; color.a = 0.35f; EditorGUI.DrawRect(rect, color); }
                if (region.cells.Contains(cell)) PatternPreviewGridGUI.DrawSelectionOutline(rect);
                if (memberships.TryGetValue(cell, out int count) && count > 1)
                { EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), Color.red); GUI.Label(rect, "!"); }
                if (blocked.Contains(cell)) { EditorGUI.DrawRect(rect, new Color(0.4f, 0.12f, 0.35f, 0.85f)); GUI.Label(rect, "B"); }
            }
            PatternPreviewGridGUI.DrawLines(board, encounter.arena.GridSize);
            regionStroke.Handle(board, encounter.arena.GridSize, regionBrush == 1,
                () => BeginPaint("Paint named region"),
                (cell, erase) => { if (erase || (floor.Contains(cell) && !blocked.Contains(cell))) SetCell(region.cells, cell, erase); },
                () => Changed(false), () => Changed());
            int overlaps = memberships.Count(pair => pair.Value > 1);
            if (overlaps > 0) EditorGUILayout.HelpBox("영역이 겹친 타일 " + overlaps + "칸: 공격 후보 중 목록 위쪽 영역 하나만 선택합니다.", MessageType.Warning);
            var errors = new List<string>(); EncounterRegionRules.ValidateDefinitions(encounter, errors);
            foreach (var error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.EndScrollView();
        }

        private void MoveRegion(int delta)
        {
            BeginPaint("Reorder region priority");
            var region = encounter.tileRegions[selectedRegion];
            encounter.tileRegions.RemoveAt(selectedRegion); selectedRegion += delta;
            encounter.tileRegions.Insert(selectedRegion, region); Changed(); GUIUtility.ExitGUI();
        }

        private void DrawRegionCandidates(TileSelection tiles)
        {
            EditorGUILayout.HelpBox("Warning 시작 시 플레이어가 속한 후보 영역 하나를 고정합니다. 경고 중 이동해도 타격/VFX는 같은 영역입니다.\n목록 위쪽 우선 · 영역 밖에서는 공격 없음 · 위치 그룹/보정/탈출 보조는 사용하지 않습니다.", MessageType.None);
            if (tiles.regionIds == null) tiles.regionIds = new List<string>();
            foreach (var region in encounter.tileRegions)
            {
                bool before = tiles.regionIds.Contains(region.id);
                bool after = EditorGUILayout.ToggleLeft(region.name + " (" + region.cells.Count + "칸)", before);
                if (before == after) continue;
                BeginPaint("Select attack regions");
                if (after) tiles.regionIds.Add(region.id); else tiles.regionIds.Remove(region.id);
                Changed();
            }
            if (tiles.regionIds.Any(id => !encounter.tileRegions.Any(r => r.id == id)))
            {
                EditorGUILayout.HelpBox("삭제된 영역을 참조합니다. 잘못된 참조를 지우고 후보를 다시 선택하세요.", MessageType.Error);
                if (GUILayout.Button("없는 영역 참조 지우기"))
                { BeginPaint("Remove missing region references"); tiles.regionIds.RemoveAll(id => !encounter.tileRegions.Any(r => r.id == id)); Changed(); }
            }
            if (tiles.regionIds.Count == 0) EditorGUILayout.HelpBox("공격 후보 영역을 하나 이상 선택하세요.", MessageType.Warning);
            if (GUILayout.Button("현재 맵의 회피 거리 검사"))
            {
                using (var host = new PatternPreviewHost(encounter))
                {
                    var reports = new List<string>();
                    foreach (var region in encounter.tileRegions.Where(r => tiles.regionIds.Contains(r.id)))
                    {
                        var distances = EncounterRegionRules.EscapeDistances(host.Traversable, region.cells);
                        var hits = region.cells.Where(c => host.Traversable.Contains(c)).Distinct().ToArray();
                        int unreachable = hits.Count(c => !distances.ContainsKey(c));
                        int longest = hits.Where(distances.ContainsKey).Select(c => distances[c]).DefaultIfEmpty(0).Max();
                        reports.Add(region.name + ": 최대 " + longest + "칸 / 탈출 불가 " + unreachable + "칸");
                    }
                    regionEscapeReport = string.Join("\n", reports) + "\n보스 점유 반영. 다른 공격·임시 벽·입력 속도는 별도 플레이 검증이 필요합니다.";
                }
            }
            if (!string.IsNullOrEmpty(regionEscapeReport)) EditorGUILayout.HelpBox(regionEscapeReport, MessageType.Info);
            if (GUILayout.Button("보스 공통 영역 편집 열기")) { SelectNode(NodeKind.Regions, -1, -1); GUIUtility.ExitGUI(); }
        }

        private void DrawRegionSnapshot(TileSelection tiles)
        {
            if (tiles?.shape != TileShape.PlayerRegion) return;
            string status = "아직 경고 전";
            if (previewContext != null && previewContext.SelectedRegionIds.TryGetValue(tiles.snapshotKey, out string id))
                status = string.IsNullOrEmpty(id) ? "대상 없음 (이 실행에서는 공격 안 함)" :
                    encounter.tileRegions.FirstOrDefault(r => r.id == id)?.name ?? id;
            EditorGUILayout.LabelField("고정된 공격 영역: " + status, EditorStyles.wordWrappedMiniLabel);
        }
    }
}
#endif
