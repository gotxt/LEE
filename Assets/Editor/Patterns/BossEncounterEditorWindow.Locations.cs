#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NHN.TraceStrike.Patterns;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    public sealed partial class BossEncounterEditorWindow
    {
        [SerializeField] private bool showLocationGroups = true;
        [SerializeField] private int selectedLocationGroup = -1;
        [SerializeField] private int previewLocationSeed;
        [SerializeField] private bool editRandomLocationCells;
        [SerializeField] private int randomLocationBrush;
        private readonly Dictionary<string, Vector2Int> previewLocations = new Dictionary<string, Vector2Int>();

        private static readonly string[] LocationSourceLabels =
        { "맵 중앙", "패턴 시작 시 플레이어", "기믹 / 호출 위치", "고정 좌표", "랜덤 이동 가능 타일" };

        private void DrawLocationGroups(EncounterPattern pattern)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    showLocationGroups = EditorGUILayout.Foldout(showLocationGroups, "공유 위치 그룹", true, EditorStyles.foldoutHeader);
                    if (GUILayout.Button("+ 위치 그룹", GUILayout.Width(105)))
                    {
                        BeginPaint("Add pattern location group");
                        if (pattern.locationGroups == null) pattern.locationGroups = new List<PatternLocationGroup>();
                        pattern.locationGroups.Add(new PatternLocationGroup
                        { name = "위치 그룹 " + (pattern.locationGroups.Count + 1) });
                        selectedLocationGroup = pattern.locationGroups.Count - 1;
                        Changed(); GUIUtility.ExitGUI();
                    }
                }
                if (!showLocationGroups) return;
                EditorGUILayout.LabelField("패턴이 시작할 때 위치를 한 번 정합니다. 여러 공격을 같은 그룹에 연결하면 시간을 달리해도 같은 기준 위치를 사용합니다.",
                    EditorStyles.wordWrappedMiniLabel);
                var groups = pattern.locationGroups;
                if (groups == null || groups.Count == 0) return;
                selectedLocationGroup = Mathf.Clamp(selectedLocationGroup, 0, groups.Count - 1);
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    if (group == null) continue;
                    string label = group.name + " · " + LocationSourceLabels[Mathf.Clamp((int)group.source, 0, LocationSourceLabels.Length - 1)];
                    if (GUILayout.Toggle(selectedLocationGroup == i, label, EditorStyles.miniButton))
                        selectedLocationGroup = i;
                }
                var selected = groups[selectedLocationGroup];
                if (selected == null) return;
                EditorGUI.BeginChangeCheck();
                string name = EditorGUILayout.TextField("그룹 이름", selected.name);
                int source = EditorGUILayout.Popup("위치 선택", (int)selected.source, LocationSourceLabels);
                Vector2Int fixedCell = selected.fixedCell;
                bool usePlayerCell = false;
                if (source == (int)PatternLocationSource.FixedCell)
                {
                    if (selected.source != PatternLocationSource.FixedCell && fixedCell == Vector2Int.zero)
                        fixedCell = PreviewLocation(selected.id);
                    fixedCell = EditorGUILayout.Vector2IntField("기준 타일 좌표", fixedCell);
                    usePlayerCell = GUILayout.Button("현재 P 위치를 기준 타일로 지정");
                    if (usePlayerCell) fixedCell = previewPlayer;
                }
                if (EditorGUI.EndChangeCheck() || usePlayerCell)
                {
                    BeginPaint("Edit pattern location group");
                    selected.name = name;
                    selected.source = (PatternLocationSource)source;
                    selected.fixedCell = fixedCell;
                    if (selected.source != PatternLocationSource.RandomWalkable) editRandomLocationCells = false;
                    Changed();
                }
                if (selected.source == PatternLocationSource.RandomWalkable)
                {
                    bool restrict = EditorGUILayout.Toggle("랜덤 후보 타일 직접 지정", selected.restrictRandomCells);
                    if (restrict != selected.restrictRandomCells)
                    {
                        BeginPaint("Set random location candidates");
                        selected.restrictRandomCells = restrict;
                        if (restrict && (selected.randomCells == null || selected.randomCells.Count == 0))
                        {
                            if (selected.randomCells == null) selected.randomCells = new List<Vector2Int>();
                            selected.randomCells.Add(PreviewLocation(selected.id));
                        }
                        if (!restrict) editRandomLocationCells = false;
                        Changed(); GUIUtility.ExitGUI();
                    }
                    if (selected.restrictRandomCells)
                    {
                        EditorGUILayout.LabelField("허용 타일 " + (selected.randomCells?.Count ?? 0) + "개 · 맵에서 칠하거나 지우세요.", EditorStyles.miniLabel);
                        if (GUILayout.Button(editRandomLocationCells ? "후보 타일 칠하기 끝내기" : "맵에서 후보 타일 칠하기"))
                        {
                            eventStroke.Cancel();
                            editRandomLocationCells = !editRandomLocationCells;
                            Repaint();
                        }
                    }
                    else EditorGUILayout.LabelField("후보를 제한하지 않으면 실행 시 이동 가능한 바닥 전체에서 뽑습니다.", EditorStyles.wordWrappedMiniLabel);
                }
                int used = pattern.clips.Count(c => c?.action != null &&
                    AttackStepEditing.Tiles(c)?.locationGroupId == selected.id);
                if (previewLocations.TryGetValue(selected.id, out var cell))
                    EditorGUILayout.LabelField("이번 미리보기 기준 타일: " + cell, EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(used > 0))
                    if (GUILayout.Button("선택 그룹 삭제", GUILayout.Width(120)))
                    {
                        BeginPaint("Delete pattern location group");
                        groups.RemoveAt(selectedLocationGroup);
                        selectedLocationGroup = Mathf.Min(selectedLocationGroup, groups.Count - 1);
                        editRandomLocationCells = false;
                        Changed(); GUIUtility.ExitGUI();
                    }
                if (used > 0) EditorGUILayout.LabelField("연결된 이벤트 " + used + "개 — 삭제하려면 먼저 다른 위치 그룹으로 옮기세요.", EditorStyles.miniLabel);
            }
        }

        // Both the simple attack editor and the advanced event inspector use the same assignment rule.
        private void DrawLocationGroupPicker(EncounterPattern pattern, TileSelection tiles)
        {
            var groups = pattern.locationGroups?.Where(g => g != null).ToList() ?? new List<PatternLocationGroup>();
            string[] labels = new string[groups.Count + 1];
            labels[0] = "사용 안 함 (개별 위치 기준)";
            for (int i = 0; i < groups.Count; i++) labels[i + 1] = groups[i].name;
            int current = string.IsNullOrEmpty(tiles.locationGroupId) ? 0 :
                groups.FindIndex(g => g.id == tiles.locationGroupId) + 1;
            if (current == 0 && !string.IsNullOrEmpty(tiles.locationGroupId)) current = -1;
            int next = EditorGUILayout.Popup("공유 위치 그룹", current, labels);
            if (current < 0) EditorGUILayout.HelpBox("삭제되거나 찾을 수 없는 위치 그룹입니다. 다시 선택하세요.", MessageType.Error);
            if (next == current || next < 0) return;
            BeginPaint("Assign pattern location group");
            var selectedEvent = selectedClip >= 0 && selectedClip < pattern.clips.Count
                ? pattern.clips[selectedClip] : null;
            var step = AttackStepEditing.Find(pattern).FirstOrDefault(s => s.Members.Contains(selectedEvent));
            AssignLocationGroup(step?.Tiles ?? tiles, next == 0 ? "" : groups[next - 1].id);
            if (step != null) AttackStepEditing.SynchronizeArea(step);
            Changed(); GUIUtility.ExitGUI();
        }

        private void AssignLocationGroup(TileSelection tiles, string groupId)
        {
            Vector2Int oldOrigin = PaintOrigin(tiles);
            Vector2Int newBase = string.IsNullOrEmpty(groupId) ? LegacyPaintBase(tiles) : PreviewLocation(groupId);
            if (tiles.shape == TileShape.Cells)
            {
                if (tiles.cells != null)
                    for (int i = 0; i < tiles.cells.Count; i++)
                        tiles.cells[i] += oldOrigin - newBase;
                tiles.offset = Vector2Int.zero;
            }
            else tiles.offset = oldOrigin - newBase;
            tiles.locationGroupId = groupId;
        }

        private Vector2Int LegacyPaintBase(TileSelection tiles) =>
            tiles.anchor == TileAnchor.Absolute ? Vector2Int.zero :
            tiles.anchor == TileAnchor.Player ? previewPlayer :
            tiles.anchor == TileAnchor.Origin ? PreviewOrigin : encounter.arena.CenterCell;

        private Vector2Int PreviewLocation(string id)
        {
            if (previewLocations.TryGetValue(id, out var cell)) return cell;
            var pattern = CurrentPattern();
            if (pattern == null) return PreviewOrigin;
            try
            {
                using (var host = new PatternPreviewHost(encounter.arena) { player = previewPlayer })
                using (var context = new PatternContext(host, PreviewOrigin, locationSeed: previewLocationSeed))
                {
                    context.InitializeLocations(pattern.locationGroups);
                    return context.Location(id);
                }
            }
            catch (InvalidOperationException) { return PreviewOrigin; } // Keep invalid groups editable in the board.
        }

        private void DrawPreviewLocations(EncounterPattern pattern, Rect board, TileSelection selectedTiles)
        {
            string id = !string.IsNullOrEmpty(selectedTiles?.locationGroupId) ? selectedTiles.locationGroupId :
                selectedLocationGroup >= 0 && selectedLocationGroup < (pattern.locationGroups?.Count ?? 0)
                    ? pattern.locationGroups[selectedLocationGroup]?.id : null;
            if (string.IsNullOrEmpty(id) || !previewLocations.TryGetValue(id, out var cell)) return;
            if (cell.x < 0 || cell.y < 0 || cell.x >= encounter.arena.GridSize || cell.y >= encounter.arena.GridSize) return;
            PatternPreviewGridGUI.DrawCenterHighlight(PatternPreviewGridGUI.CellRect(board,
                cell.x, cell.y, encounter.arena.GridSize));
        }

        private PatternLocationGroup EditingRandomLocationGroup(EncounterPattern pattern)
        {
            if (!editRandomLocationCells || selectedLocationGroup < 0 ||
                selectedLocationGroup >= (pattern.locationGroups?.Count ?? 0)) return null;
            var group = pattern.locationGroups[selectedLocationGroup];
            return group != null && group.source == PatternLocationSource.RandomWalkable &&
                group.restrictRandomCells ? group : null;
        }

        private void HandleRandomLocationPainting(Rect board, PatternLocationGroup group)
        {
            HashSet<Vector2Int> walkable = encounter.arena.GetCells();
            eventStroke.Handle(board, encounter.arena.GridSize, randomLocationBrush == 1,
                () => BeginPaint("Paint random location candidates"),
                (cell, erase) =>
                {
                    if (group.randomCells == null) group.randomCells = new List<Vector2Int>();
                    if (erase || walkable.Contains(cell)) SetCell(group.randomCells, cell, erase);
                }, () => Changed(false), () => Changed());
        }
    }
}
#endif
