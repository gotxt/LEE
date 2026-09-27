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
        [SerializeField] private SpecialTileDefinition specialTileBrush;
        private SpecialTileDefinition[] specialTilePalette;
        private readonly TilePaintStroke specialTileStroke = new TilePaintStroke();
        private Vector2 specialTileScroll;
        private int specialTileTool;
        private TrailFieldModel tileTestModel;
        private SpecialTileField tileTestField;
        private readonly SpecialTilePlayerState tileTestState = new SpecialTilePlayerState();
        private double tileTestTime;
        private string tileTestError;
        private Rect specialTileBoardRect;

        private void RefreshSpecialTilePalette()
        {
            specialTilePalette = AssetDatabase.FindAssets("t:SpecialTileDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<SpecialTileDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(tile => tile != null).OrderBy(tile => tile.name).ToArray();
            if (specialTileBrush == null) specialTileBrush = specialTilePalette.FirstOrDefault();
        }
        private void DrawSpecialTilePainter()
        {
            if (specialTilePalette == null) RefreshSpecialTilePalette();
            EditorGUILayout.LabelField("공용 특수 타일 배치", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("종류 선택 → 맵 클릭·드래그. 우클릭/Shift: 지우기 · Ctrl+Z: 한 획 되돌리기.\n바닥 이미지·영역·START/END는 바뀌지 않습니다. 한 칸에는 한 종류만 배치하며 다시 칠하면 교체합니다. 배치는 모든 페이즈에서 유지됩니다.", MessageType.Info);
            specialTileScroll = EditorGUILayout.BeginScrollView(specialTileScroll);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var tile in specialTilePalette)
                    if (GUILayout.Toggle(specialTileBrush == tile, tile.displayName, EditorStyles.miniButton)) specialTileBrush = tile;
                if (GUILayout.Button("목록 새로고침", GUILayout.Width(100))) RefreshSpecialTilePalette();
            }
            specialTileBrush = (SpecialTileDefinition)EditorGUILayout.ObjectField("선택한 특수 타일", specialTileBrush, typeof(SpecialTileDefinition), false);
            if (specialTileBrush != null)
            {
                EditorGUILayout.HelpBox("아래는 공용 타일 데이터입니다. 수정하면 이 데이터를 쓰는 모든 보스에 적용됩니다. 보스별로 다른 값이 필요하면 복제하세요.", MessageType.None);
                var tileData = new SerializedObject(specialTileBrush);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(tileData.FindProperty("displayName"), new GUIContent("타일 이름"));
                EditorGUILayout.PropertyField(tileData.FindProperty("sprite"), new GUIContent("표시 이미지"));
                EditorGUILayout.PropertyField(tileData.FindProperty("color"), new GUIContent("표시 색"));
                EditorGUILayout.PropertyField(tileData.FindProperty("marker"), new GUIContent("이미지 없을 때 기호"));
                EditorGUILayout.PropertyField(tileData.FindProperty("stunSeconds"), new GUIContent("진입 시 기절 (초)", "0이면 기절 없음. 매 타일 진입 시 발동합니다."));
                EditorGUILayout.PropertyField(tileData.FindProperty("fireMoveCount"), new GUIContent("불 지속 이동 칸 수", "0이면 불 부여 없음. 재진입 시 이 값으로 갱신합니다."));
                if (EditorGUI.EndChangeCheck()) { tileData.ApplyModifiedProperties(); ResetSpecialTilePreview(); }
                if (GUILayout.Button("선택한 특수 타일 데이터 복제"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("타일 복제", specialTileBrush.name + "_Copy", "asset", "별도 수치로 사용할 타일 데이터");
                    if (!string.IsNullOrEmpty(path))
                    {
                        path = AssetDatabase.GenerateUniqueAssetPath(path);
                        var copy = Instantiate(specialTileBrush); AssetDatabase.CreateAsset(copy, path);
                        specialTileBrush = copy; AssetDatabase.SaveAssetIfDirty(copy); RefreshSpecialTilePalette();
                    }
                }
            }
            specialTileTool = GUILayout.Toolbar(specialTileTool, new[] { "타일 칠하기", "지우개", "시험 시작 위치", "한 칸 이동 시험" });
            if (tileTestModel == null && tileTestError == null) ResetSpecialTilePreview();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("시험 초기화")) ResetSpecialTilePreview();
                using (new EditorGUI.DisabledScope(tileTestModel == null || tileTestState.IsStunned))
                {
                    if (GUILayout.Button("←")) MoveSpecialTilePreview(Vector2Int.left);
                    if (GUILayout.Button("↑")) MoveSpecialTilePreview(Vector2Int.up);
                    if (GUILayout.Button("↓")) MoveSpecialTilePreview(Vector2Int.down);
                    if (GUILayout.Button("→")) MoveSpecialTilePreview(Vector2Int.right);
                }
            }
            EditorGUILayout.LabelField($"시험 상태: 불 {tileTestState.FireMovesRemaining}칸 · 기절 {tileTestState.StunRemaining:0.0}초");
            EditorGUILayout.LabelField("시험은 실제 스폰을 바꾸지 않습니다. 기절 중 이동 불가 · 성공한 이동만 불 차감 · 마지막 남은 1칸의 도착까지 불 유효", EditorStyles.wordWrappedMiniLabel);
            var floor = encounter.arena.GetCells();
            var blocked = encounter.bossVisual.OccupiedCells();
            var images = encounter.arena.BuildTileSpriteLookup();
            using (var field = new SpecialTileField(encounter.arena.specialTiles))
            {
                float edge = Mathf.Max(448, encounter.arena.GridSize * 18);
                Rect board = GUILayoutUtility.GetRect(edge, edge, GUILayout.ExpandWidth(false));
                // Window-local coordinates, including the scroll-view offset (also used by UI tests).
                specialTileBoardRect = new Rect(GUIUtility.GUIToScreenPoint(board.position) - position.position, board.size);
                for (int y = 0; y < encounter.arena.GridSize; y++)
                for (int x = 0; x < encounter.arena.GridSize; x++)
                {
                    var cell = new Vector2Int(x, y); var rect = PatternPreviewGridGUI.CellRect(board, x, y, encounter.arena.GridSize);
                    EditorGUI.DrawRect(rect, floor.Contains(cell) ? new Color(.27f, .3f, .35f) : new Color(.1f, .1f, .12f));
                    if (floor.Contains(cell)) PatternPreviewGridGUI.DrawTileSprite(rect, encounter.arena.ResolveTileSprite(cell, images));
                    var tile = field.At(cell);
                    if (tile != null)
                    {
                        Color tint = tile.color; tint.a = .65f; EditorGUI.DrawRect(rect, tint);
                        if (tile.sprite != null) PatternPreviewGridGUI.DrawTileSprite(rect, tile.sprite);
                        else GUI.Label(rect, tile.marker, EditorStyles.whiteMiniLabel);
                    }
                    if (blocked.Contains(cell)) { EditorGUI.DrawRect(rect, new Color(.4f, .12f, .35f, .85f)); GUI.Label(rect, "B"); }
                    if (tileTestModel != null && cell == tileTestModel.Player) { PatternPreviewGridGUI.DrawSelectionOutline(rect); GUI.Label(rect, "P"); }
                    var e = Event.current;
                    if (specialTileTool < 2 || e.type != EventType.MouseDown || e.button != 0 || !rect.Contains(e.mousePosition)) continue;
                    if (specialTileTool == 2 && tileTestModel != null && tileTestModel.TryPlacePlayer(cell)) tileTestState.Reset();
                    if (specialTileTool == 3 && tileTestModel != null) MoveSpecialTilePreview(cell - tileTestModel.Player);
                    e.Use();
                }
                PatternPreviewGridGUI.DrawLines(board, encounter.arena.GridSize);
                if (specialTileTool < 2)
                    specialTileStroke.Handle(board, encounter.arena.GridSize, specialTileTool == 1,
                        () => BeginPaint("Paint special tiles"),
                        (cell, erase) =>
                        {
                            if (erase) encounter.arena.SetSpecialTile(cell, null);
                            else if (specialTileBrush != null && floor.Contains(cell) && !blocked.Contains(cell))
                                encounter.arena.SetSpecialTile(cell, specialTileBrush);
                        }, () => Changed(false), () => Changed());
            }
            var errors = new System.Collections.Generic.List<string>(); SpecialTileValidation.Validate(encounter, errors);
            foreach (var error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (tileTestError != null) EditorGUILayout.HelpBox(tileTestError, MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
        private void ResetSpecialTilePreview()
        {
            tileTestField?.Dispose(); tileTestField = null; tileTestModel = null;
            tileTestState.Reset(); tileTestError = null; tileTestTime = EditorApplication.timeSinceStartup;
            try
            {
                var model = new TrailFieldModel(); encounter.arena.ApplyTo(model);
                model.SetBlockedCells(encounter.bossVisual?.OccupiedCells());
                model.BeginRound(0, true, encounter.arena.overridePlayerStart ? (Vector2Int?)encounter.arena.playerStart : null);
                tileTestField = new SpecialTileField(encounter.arena.specialTiles); tileTestModel = model;
            }
            catch (Exception error) { tileTestError = error.Message; }
        }
        private void MoveSpecialTilePreview(Vector2Int direction)
        {
            if (tileTestModel == null || tileTestState.IsStunned) return;
            var from = tileTestModel.Player;
            if (tileTestModel.TryMove(direction) == MoveResult.Blocked) return;
            tileTestState.Enter(from, tileTestModel.Player, tileTestField.At(tileTestModel.Player));
            Repaint();
        }
        private void UpdateSpecialTilePreview()
        {
            double now = EditorApplication.timeSinceStartup;
            bool stunned = tileTestState.IsStunned;
            tileTestState.Advance((float)Math.Max(0, now - tileTestTime)); tileTestTime = now;
            if (stunned) Repaint();
        }
    }
}
#endif
