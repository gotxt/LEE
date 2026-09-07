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
        private readonly TilePaintStroke mapStroke = new TilePaintStroke();
        private readonly TilePaintStroke eventStroke = new TilePaintStroke();
        [SerializeField] private int mapBrush, eventBrush;
        [SerializeField] private float mapZoom = 1f;
        private Vector2 mapScroll, tilePreviewScroll;
        private Vector2Int strokeOrigin;

        private void OnLostFocus() { mapStroke.Cancel(); eventStroke.Cancel(); }

        private void BeginPaint(string label)
        {
            playing = false;
            Undo.RegisterCompleteObjectUndo(encounter, label);
        }

        private void DrawArenaPainter()
        {
            EditorGUILayout.LabelField("전장 타일 편집", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("바닥을 칠하거나 지워 전장 모양을 만드세요. 우클릭 또는 Shift+드래그: 지우기 · Ctrl+Z: 한 획 되돌리기", MessageType.None);
            serialized.Update();
            var arenaProperty = serialized.FindProperty("arena");
            EditorGUI.BeginChangeCheck();
            int size = EditorGUILayout.IntSlider("전장 크기", encounter.arena.size, 5, TrailFieldModel.MaxSize);
            EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("cameraZoom"));
            EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("playerSizeRatio"));
            EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("overridePlayerStart"), new GUIContent("플레이어 시작 위치 고정"));
            if (arenaProperty.FindPropertyRelative("overridePlayerStart").boolValue)
                EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("playerStart"), new GUIContent("시작 좌표"));
            EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("restrictStartCells"), new GUIContent("START 생성 영역 제한"));
            EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("restrictEndCells"), new GUIContent("END 생성 영역 제한"));
            if (EditorGUI.EndChangeCheck())
            {
                arenaProperty.FindPropertyRelative("size").intValue = size;
                serialized.ApplyModifiedProperties();
                Changed();
            }
            mapBrush = GUILayout.Toolbar(mapBrush, new[] { "바닥 칠하기", "바닥 지우개", "플레이어 시작", "START 영역", "END 영역" });
            EditorGUILayout.HelpBox("P: 플레이어 시작 · 초록: START · 주황: END · 노랑: 겹친 영역\nSTART/END 도구로 칠하면 해당 제한이 켜집니다. 우클릭/Shift로 선택 영역만 지웁니다. 제한을 끄면 전체 바닥에서 생성합니다.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("형태 프리셋", GUILayout.Width(110))) ShowArenaPresets();
                if (GUILayout.Button("전체 채우기", GUILayout.Width(100))) SetArenaCells(true);
                if (GUILayout.Button("비우기", GUILayout.Width(70))) SetArenaCells(false);
                mapZoom = EditorGUILayout.Slider("확대", mapZoom, 0.6f, 4f);
            }
            EditorGUILayout.LabelField("현재 형태: " + encounter.arena.shape + " · 크기를 줄여 가려진 타일은 다시 확대하면 복원됩니다.", EditorStyles.miniLabel);
            mapScroll = EditorGUILayout.BeginScrollView(mapScroll);
            float edge = Mathf.Max(408f, encounter.arena.GridSize * 16f) * mapZoom;
            Rect board = GUILayoutUtility.GetRect(edge, edge, GUILayout.ExpandWidth(false));
            HashSet<Vector2Int> cells = encounter.arena.GetCells();
            var starts = new HashSet<Vector2Int>(encounter.arena.startCells);
            var ends = new HashSet<Vector2Int>(encounter.arena.endCells);
            for (int y = 0; y < encounter.arena.GridSize; y++)
            for (int x = 0; x < encounter.arena.GridSize; x++)
            {
                var cell = new Vector2Int(x, y);
                Rect rect = PatternPreviewGridGUI.CellRect(board, x, y, encounter.arena.GridSize);
                Color color = !encounter.arena.ContainsBounds(cell) ? new Color(0.08f, 0.08f, 0.09f) :
                    cells.Contains(cell) ? new Color(0.32f, 0.46f, 0.53f) : new Color(0.16f, 0.17f, 0.19f);
                bool start = encounter.arena.restrictStartCells && starts.Contains(cell);
                bool end = encounter.arena.restrictEndCells && ends.Contains(cell);
                if (cells.Contains(cell) && (start || end))
                    color = start && end ? new Color(0.8f, 0.72f, 0.22f) : start ? new Color(0.2f, 0.65f, 0.35f) : new Color(0.85f, 0.45f, 0.16f);
                EditorGUI.DrawRect(rect, color);
                if (encounter.arena.overridePlayerStart && encounter.arena.playerStart == cell)
                    GUI.Label(rect, "P", EditorStyles.whiteBoldLabel);
            }
            PatternPreviewGridGUI.DrawLines(board, encounter.arena.GridSize);
            mapStroke.Handle(board, encounter.arena.GridSize, mapBrush == 1,
                () => {
                    BeginPaint("Paint arena configuration");
                    if (mapBrush <= 1) encounter.arena.MakeCustom();
                },
                (cell, erase) =>
                {
                    if (!encounter.arena.ContainsBounds(cell)) return;
                    if (mapBrush <= 1) SetCell(encounter.arena.floorCells, cell, erase);
                    else if (mapBrush == 2)
                    {
                        if (erase) encounter.arena.overridePlayerStart = false;
                        else if (cells.Contains(cell))
                        {
                            encounter.arena.overridePlayerStart = true;
                            encounter.arena.playerStart = cell;
                            previewPlayer = cell;
                        }
                    }
                    else if (erase || cells.Contains(cell))
                    {
                        if (mapBrush == 3) { encounter.arena.restrictStartCells = true; SetCell(encounter.arena.startCells, cell, erase); }
                        else { encounter.arena.restrictEndCells = true; SetCell(encounter.arena.endCells, cell, erase); }
                    }
                }, () => Changed(false), () => Changed());
            EditorGUILayout.EndScrollView();
            var errors = new List<string>();
            encounter.arena.ValidateMap(errors);
            foreach (string error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        private void ShowArenaPresets()
        {
            var menu = new GenericMenu();
            foreach (ArenaShape shape in new[] { ArenaShape.Rounded, ArenaShape.Triangle, ArenaShape.Star })
            {
                ArenaShape chosen = shape;
                menu.AddItem(new GUIContent(shape.ToString()), encounter.arena.shape == chosen, () =>
                {
                    BeginPaint("Apply arena preset");
                    encounter.arena.shape = chosen;
                    Changed();
                });
            }
            menu.ShowAsContext();
        }

        private void SetArenaCells(bool fill)
        {
            BeginPaint(fill ? "Fill arena" : "Clear arena");
            encounter.arena.shape = ArenaShape.Custom;
            encounter.arena.floorCells.Clear();
            if (fill)
                for (int y = 0; y < encounter.arena.GridSize; y++)
                for (int x = 0; x < encounter.arena.GridSize; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (encounter.arena.ContainsBounds(cell)) encounter.arena.floorCells.Add(cell);
                }
            Changed();
        }

        private static void SetCell(List<Vector2Int> cells, Vector2Int cell, bool erase)
        {
            if (erase) cells.RemoveAll(c => c == cell);
            else if (!cells.Contains(cell)) cells.Add(cell);
        }

        private void DrawEventPaintTools(TileSelection tiles)
        {
            eventBrush = GUILayout.Toolbar(eventBrush, new[] { "영역 칠하기", "지우개", "플레이어 위치" });
            if (tiles == null)
            {
                EditorGUILayout.HelpBox("타일 영역을 편집하려면 경고·데미지·장판·벽·VFX 이벤트를 선택하세요.", MessageType.None);
                return;
            }
            if (tiles.shape != TileShape.Cells)
            {
                EditorGUILayout.HelpBox("현재 영역은 " + tiles.shape + "입니다. 타일 편집으로 변환하면 현재 기준 위치의 영역을 고정해 칠할 수 있습니다.", MessageType.None);
                if (GUILayout.Button("현재 영역을 타일 편집으로 변환"))
                {
                    BeginPaint("Convert event area to cells");
                    var host = new PatternPreviewHost(encounter.arena) { player = previewPlayer };
                    using (var context = new PatternContext(host, host.CenterCell))
                        tiles.cells = tiles.Resolve(context).Select(c => c - PaintOrigin(tiles)).ToList();
                    tiles.shape = TileShape.Cells;
                    Changed();
                }
            }
            else if (GUILayout.Button("선택 이벤트 영역 비우기"))
            {
                BeginPaint("Clear event tiles");
                tiles.cells.Clear();
                Changed();
            }
        }

        private void HandleEventPainting(Rect board, TileSelection tiles)
        {
            if (eventBrush == 2)
            {
                Event e = Event.current;
                if (e.type != EventType.MouseDown || e.button != 0 || !board.Contains(e.mousePosition)) return;
                HashSet<Vector2Int> floor = encounter.arena.GetCells();
                for (int y = 0; y < encounter.arena.GridSize; y++)
                for (int x = 0; x < encounter.arena.GridSize; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (PatternPreviewGridGUI.CellRect(board, x, y, encounter.arena.GridSize).Contains(e.mousePosition) &&
                        floor.Contains(cell))
                    { previewPlayer = cell; RebuildPreview(); e.Use(); return; }
                }
                return;
            }
            if (tiles == null || tiles.shape != TileShape.Cells) return;
            HashSet<Vector2Int> walkable = encounter.arena.GetCells();
            eventStroke.Handle(board, encounter.arena.GridSize, eventBrush == 1,
                () => { BeginPaint("Paint event tiles"); strokeOrigin = PaintOrigin(tiles); },
                (cell, erase) =>
                {
                    if (erase || walkable.Contains(cell)) SetCell(tiles.cells, cell - strokeOrigin, erase);
                }, () => Changed(false), () => Changed());
        }
    }
}
#endif
