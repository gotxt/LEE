#if UNITY_EDITOR
using System;
using System.Linq;
using NHN.TraceStrike.Patterns;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    public sealed partial class BossEncounterEditorWindow
    {
        private bool growthTeleport;
        private Rect growthBoardRect;
        private void DrawCombatCondition(EncounterPattern pattern)
        {
            int condition = EditorGUILayout.Popup("자동 실행 조건", (int)pattern.combatCondition,
                new[] { "항상", "광폭화 전", "광폭화 후" });
            if (condition != (int)pattern.combatCondition)
            { BeginPaint("Change combat condition"); pattern.combatCondition = (PatternCombatCondition)condition; Changed(); }
            if (node == NodeKind.LibraryPattern)
                EditorGUILayout.LabelField("직접 호출/단일 패턴 미리보기에는 실행 조건을 적용하지 않습니다.", EditorStyles.wordWrappedMiniLabel);
        }
        private void DrawGrowthSettings(SerializedProperty property, RegionGrowthMechanic growth)
        {
            foreach (var field in new[] { ("name", "기믹 이름"), ("enabled", "기믹 사용"),
                ("initialDelay", "첫 씨앗 생성 (초)"), ("interval", "생성 간격 (초)"),
                ("seedsPerSpawn", "한 번에 총 생성 개수"), ("growthSeconds", "완전 성장까지 (초)"),
                ("coverage", "덩굴화 비율 (0~1)"), ("overgrownTile", "덩굴화 특수 타일") })
                EditorGUILayout.PropertyField(property.FindPropertyRelative(field.Item1), new GUIContent(field.Item2));
            EditorGUILayout.HelpBox("구역의 유효 바닥 수 × 비율을 올림합니다. 완전 성장체는 통과 가능하지만 불로 제거할 수 없습니다. 덩굴은 기존 불 타일까지 덮습니다.", MessageType.Info);
            GrowthRegionChoices(property.FindPropertyRelative("regionIds"), "씨앗 생성 구역", growth, false);
            if (GUILayout.Button("구역 모양 칠하기 → 보스 공통 영역"))
            { serialized.ApplyModifiedProperties(); SelectNode(NodeKind.Regions, -1, -1); GUIUtility.ExitGUI(); }
            EditorGUILayout.LabelField("광폭화 조건 (두 묶음 중 하나 충족)", EditorStyles.boldLabel);
            GrowthRegionChoices(property.FindPropertyRelative("enrageWhenAll"), "선택 구역 모두 덩굴화", growth, true);
            GrowthRegionChoices(property.FindPropertyRelative("enrageWhenAny"), "선택 구역 중 하나 덩굴화", growth, true);
            EditorGUILayout.HelpBox("빈 묶음은 조건 없음입니다. 광폭화는 체력/씨앗/덩굴을 유지합니다. 패턴의 ‘자동 실행 조건’으로 전용 공격을 나중에 만드세요. 진행 중 공격은 끝까지 재생하며 다음 선택부터 적용합니다. 속도나 체력은 자동 변경하지 않습니다.", MessageType.None);
            EditorGUILayout.LabelField("씨앗 외형 · UI 프리팹 또는 Sprite", EditorStyles.boldLabel);
            foreach (var field in new[] { ("seedPrefab", "성장 중 프리팹"), ("seedSprite", "성장 중 이미지"),
                ("seedTint", "성장 중 색"), ("maturePrefab", "완전 성장 프리팹"), ("matureSprite", "완전 성장 이미지"), ("matureTint", "완전 성장 색") })
                EditorGUILayout.PropertyField(property.FindPropertyRelative(field.Item1), new GUIContent(field.Item2));
            EditorGUILayout.HelpBox("외형 미지정 시 색상 사각형으로 표시합니다. 불은 왼쪽 ‘특수 타일 배치’에서 직접 칠하세요. 생성은 빈자리 있는 구역을 균등 추첨 후 그 안의 칸을 추첨합니다.", MessageType.None);
        }
        private void GrowthRegionChoices(SerializedProperty list, string label, RegionGrowthMechanic growth, bool onlySelected)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            foreach (var region in encounter.tileRegions.Where(r => r != null))
            {
                int index = -1;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).stringValue == region.id) { index = i; break; }
                // Keep stale selections visible so the author can remove them after changing candidates.
                if (onlySelected && !growth.regionIds.Contains(region.id) && index < 0) continue;
                bool selected = EditorGUILayout.ToggleLeft(region.name + (onlySelected ? "" :
                    $" · {region.cells.Distinct().Count()}칸 / 성장 {RegionGrowthMechanic.Threshold(region.cells.Distinct().Count(), growth.coverage)}개"), index >= 0);
                if (selected && index < 0) { list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = region.id; }
                if (!selected && index >= 0) list.DeleteArrayElementAtIndex(index);
            }
            for (int i = list.arraySize - 1; i >= 0; i--)
                if (!encounter.tileRegions.Any(r => r != null && r.id == list.GetArrayElementAtIndex(i).stringValue))
                    if (GUILayout.Button("삭제된 구역 연결 지우기: " + list.GetArrayElementAtIndex(i).stringValue)) list.DeleteArrayElementAtIndex(i);
        }
        private void DrawGrowthBoard()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(mechanicPreview == null))
                {
                    if (GUILayout.Button(playing ? "일시정지" : "기믹 재생")) { playing = !playing; lastPreviewTime = EditorApplication.timeSinceStartup; }
                    if (GUILayout.Button("+1초")) { playing = false; AdvanceMechanicPreview(1); }
                    if (GUILayout.Button("+5초")) { playing = false; AdvanceMechanicPreview(5); }
                }
                if (GUILayout.Button("초기화")) { playing = false; RebuildPreview(); }
            }
            growthTeleport = GUILayout.Toggle(growthTeleport, "시험 위치 직접 놓기 (접촉 효과 없음)", EditorStyles.miniButton);
            EditorGUILayout.LabelField($"{playhead:0.00}초 · 불 {previewHost?.FireMovesRemaining ?? 0}칸 · 기절 {previewHost?.TileState.StunRemaining ?? 0:0.00}초 · " +
                (mechanicPreview?.IsEnraged == true ? "광폭화" : "일반"));
            EditorGUILayout.HelpBox("인접 칸 클릭 또는 아래 화살표로 한 칸 이동합니다. 기절은 재생/+1초로 풀립니다. 불을 밟은 뒤 미성장 씨앗을 밟아 제거하세요. 선택 기믹만 시험하며 공격 난이도는 실제 전투에서 별도 확인하세요.", MessageType.None);
            mechanicMapScroll = EditorGUILayout.BeginScrollView(mechanicMapScroll);
            float edge = Mathf.Max(330, encounter.arena.GridSize * 16);
            Rect board = GUILayoutUtility.GetRect(edge, edge, GUILayout.ExpandWidth(false));
            growthBoardRect = new Rect(GUIUtility.GUIToScreenPoint(board.position) - position.position, board.size);
            var floor = encounter.arena.GetCells(); var sprites = encounter.arena.BuildTileSpriteLookup();
            var marks = previewHost?.GetOrderedMarks();
            var runtime = mechanicPreview?.Runtimes.OfType<RegionGrowthRuntime>().FirstOrDefault();
            var seeds = runtime?.Seeds.ToDictionary(s => s.Cell);
            for (int y = 0; y < encounter.arena.GridSize; y++) for (int x = 0; x < encounter.arena.GridSize; x++)
            {
                var cell = new Vector2Int(x, y); var rect = PatternPreviewGridGUI.CellRect(board, x, y, encounter.arena.GridSize);
                EditorGUI.DrawRect(rect, floor.Contains(cell) ? new Color(.27f, .3f, .35f) : new Color(.12f, .13f, .15f));
                if (floor.Contains(cell)) PatternPreviewGridGUI.DrawTileSprite(rect, encounter.arena.ResolveTileSprite(cell, sprites));
                if (marks != null) foreach (var mark in marks) if (mark.Cells.Contains(cell)) EditorGUI.DrawRect(rect, mark.Color);
                if (seeds != null && seeds.TryGetValue(cell, out var seed)) GUI.Label(rect, seed.Mature ? "成" : "·", EditorStyles.whiteMiniLabel);
                if (cell == previewPlayer) GUI.Label(rect, "P", EditorStyles.whiteMiniLabel);
                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition) && previewHost != null)
                {
                    if (growthTeleport && previewHost.Traversable.Contains(cell))
                    { previewPlayer = previewHost.player = cell; previewHost.TileState.Reset(); }
                    else if (!growthTeleport) StepGrowthPreview(cell - previewPlayer);
                    e.Use();
                }
            }
            PatternPreviewGridGUI.DrawLines(board, encounter.arena.GridSize);
            if (bossPreview != null && Event.current.type == EventType.Repaint)
            { bossPreview.Render(); GUI.DrawTexture(board, bossPreview.Texture, ScaleMode.StretchToFill, true); }
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("←")) StepGrowthPreview(Vector2Int.left);
                if (GUILayout.Button("↑")) StepGrowthPreview(Vector2Int.up);
                if (GUILayout.Button("↓")) StepGrowthPreview(Vector2Int.down);
                if (GUILayout.Button("→")) StepGrowthPreview(Vector2Int.right);
            }
            if (runtime != null) foreach (var region in runtime.Regions)
                EditorGUILayout.LabelField($"{region.Name}: {region.MatureCount}/{region.Threshold}개 · " + (region.Overgrown ? "덩굴화 완료" : "성장 중"));
            if (!string.IsNullOrEmpty(previewError)) EditorGUILayout.HelpBox(previewError, MessageType.Error);
        }
        private void StepGrowthPreview(Vector2Int direction)
        {
            try
            {
                if (mechanicPreview != null && previewHost != null && previewHost.TryStep(direction, out var step))
                { previewPlayer = previewHost.player; mechanicPreview.OnPlayerStep(step); Repaint(); }
            }
            catch (Exception error) { FailPreview(error); }
        }
        private void AdvanceMechanicPreview(float delta)
        {
            if (mechanicPreview == null) return;
            try
            {
                previewHost?.TileState.Advance(delta); bossPreview?.Presentation.Advance(delta);
                mechanicPreview.Advance(delta); playhead += delta;
            }
            catch (Exception error) { FailPreview(error); }
        }
    }
}
#endif
