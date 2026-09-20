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
        [SerializeField] private Sprite tileImageBrush;
        [SerializeField] private bool showMapRegions = true;
        [SerializeField] private bool showArenaCenter;
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
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("맵 기본 이미지", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("defaultTileSprite"),
                    new GUIContent("기본 베이스 타일 이미지", "따로 칠하지 않은 모든 바닥 타일에 적용합니다. 개별 이미지가 있으면 개별 이미지가 우선합니다."));
                EditorGUILayout.LabelField("칠하지 않은 바닥 → 베이스 이미지 · 따로 칠한 바닥 → 칠한 이미지\n베이스를 바꿔도 따로 칠한 이미지는 유지됩니다. 이미지 지우개로 지우면 현재 베이스로 돌아갑니다.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            int size = EditorGUILayout.IntSlider("전장 크기", encounter.arena.size, 5, TrailFieldModel.MaxSize);
            EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("cameraZoom"));
            EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("playerSizeRatio"));
            EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("tilePalette"), new GUIContent("타일 이미지 팔레트"), true);
            EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("overridePlayerStart"), new GUIContent("플레이어 시작 위치 고정"));
            if (arenaProperty.FindPropertyRelative("overridePlayerStart").boolValue)
                EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("playerStart"), new GUIContent("시작 좌표"));
            EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("restrictStartCells"), new GUIContent("START 생성 영역 제한"));
            EditorGUILayout.PropertyField(arenaProperty.FindPropertyRelative("restrictEndCells"), new GUIContent("END 생성 영역 제한"));
            EditorGUILayout.PropertyField(serialized.FindProperty("bossVisual"), new GUIContent("보스 외형 / 배치"), true);
            if (EditorGUI.EndChangeCheck())
            {
                arenaProperty.FindPropertyRelative("size").intValue = size;
                serialized.ApplyModifiedProperties();
                // Keep the Arena view and the initial pattern preview aligned
                // when the persisted encounter spawn is edited in the inspector.
                previewPlayer = encounter.arena.overridePlayerStart
                    ? encounter.arena.playerStart : encounter.arena.CenterCell;
                Changed();
            }
            RectInt centerCells = PatternPreviewGridGUI.ArenaCenterCells(encounter.arena);
            // Display-only control: keep it outside the encounter change check.
            using (new EditorGUILayout.HorizontalScope())
            {
                showArenaCenter = GUILayout.Toggle(showArenaCenter,
                    new GUIContent("전장 중앙 표시", "전장 크기의 정중앙을 강조합니다. 홀수: 1칸 · 짝수: 2×2의 4칸. 다시 누르면 표시를 끕니다."),
                    GUI.skin.button, GUILayout.Width(130));
                if (showArenaCenter)
                    EditorGUILayout.LabelField(centerCells.width == 1
                        ? $"중앙 1칸: ({centerCells.xMin}, {centerCells.yMin})"
                        : $"중앙 2×2 · 4칸: X {centerCells.xMin}~{centerCells.xMax - 1}, Y {centerCells.yMin}~{centerCells.yMax - 1}",
                        EditorStyles.miniLabel);
                else
                    EditorGUILayout.LabelField("홀수: 가운데 1칸 · 짝수: 가운데 4칸 · 빈칸에도 표시", EditorStyles.miniLabel);
            }
            int geometryBrush = GUILayout.Toolbar(mapBrush < 6 ? mapBrush : -1,
                new[] { "바닥 칠하기", "바닥 지우개", "플레이어 시작", "START 영역", "END 영역", "보스 배치" });
            if (geometryBrush >= 0) mapBrush = geometryBrush;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(mapBrush == 6, "이미지 칠하기", EditorStyles.miniButton)) mapBrush = 6;
                if (GUILayout.Toggle(mapBrush == 7, "이미지 지우개", EditorStyles.miniButton)) mapBrush = 7;
                showMapRegions = GUILayout.Toggle(showMapRegions, "START/END 영역 표시");
            }
            if (mapBrush >= 6) DrawTileImagePalette();
            EditorGUILayout.HelpBox("P: 플레이어 시작 · 초록: START · 주황: END · 노랑: 겹친 영역\nSTART/END 도구로 칠하면 해당 제한이 켜집니다. 우클릭/Shift로 선택 영역만 지웁니다. 제한을 끄면 전체 바닥에서 생성합니다.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("형태 프리셋", GUILayout.Width(110))) ShowArenaPresets();
                if (GUILayout.Button("전체 채우기", GUILayout.Width(100))) SetArenaCells(true);
                if (GUILayout.Button("비우기", GUILayout.Width(70))) SetArenaCells(false);
                mapZoom = EditorGUILayout.Slider("확대", mapZoom, 0.6f, 4f);
            }
            EditorGUILayout.LabelField("현재 형태: " + encounter.arena.shape + " · P 위치는 전투 시작과 에디터 미리보기에 적용됩니다.", EditorStyles.miniLabel);
            mapScroll = EditorGUILayout.BeginScrollView(mapScroll);
            float edge = Mathf.Max(408f, encounter.arena.GridSize * 16f) * mapZoom;
            Rect board = GUILayoutUtility.GetRect(edge, edge, GUILayout.ExpandWidth(false));
            HashSet<Vector2Int> cells = encounter.arena.GetCells();
            var tileSprites = encounter.arena.BuildTileSpriteLookup();
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
                EditorGUI.DrawRect(rect, color);
                if (cells.Contains(cell))
                {
                    if (PatternPreviewGridGUI.DrawTileSprite(rect, encounter.arena.ResolveTileSprite(cell, tileSprites))) Repaint();
                    if (showMapRegions && (start || end))
                        EditorGUI.DrawRect(rect, start && end ? new Color(0.8f, 0.72f, 0.22f, 0.5f) :
                            start ? new Color(0.2f, 0.65f, 0.35f, 0.5f) : new Color(0.85f, 0.45f, 0.16f, 0.5f));
                }
                if (showArenaCenter && centerCells.Contains(cell))
                    PatternPreviewGridGUI.DrawCenterHighlight(rect);
                if (encounter.arena.overridePlayerStart && encounter.arena.playerStart == cell)
                    GUI.Label(rect, "P", EditorStyles.whiteBoldLabel);
                if (encounter.bossVisual?.prefab != null && Vector2Int.RoundToInt(encounter.bossVisual.position) == cell)
                    GUI.Label(rect, "B", EditorStyles.whiteBoldLabel);
            }
            PatternPreviewGridGUI.DrawLines(board, encounter.arena.GridSize);
            mapStroke.Handle(board, encounter.arena.GridSize, mapBrush == 1 || mapBrush == 7,
                () => {
                    BeginPaint("Paint arena configuration");
                    if (mapBrush <= 1) encounter.arena.MakeCustom();
                },
                (cell, erase) =>
                {
                    if (mapBrush == 5)
                    {
                        if (!erase) encounter.bossVisual.position = cell;
                        return; // Boss placement is independent of walkable floor cells.
                    }
                    if (!encounter.arena.ContainsBounds(cell)) return;
                    if (mapBrush >= 6)
                    {
                        if (erase) encounter.arena.SetTileSprite(cell, null);
                        else if (cells.Contains(cell) && tileImageBrush != null)
                            encounter.arena.SetTileSprite(cell, tileImageBrush);
                        return;
                    }
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

        private void DrawTileImagePalette()
        {
            tileImageBrush = (Sprite)EditorGUILayout.ObjectField("칠할 이미지", tileImageBrush, typeof(Sprite), false);
            var palette = encounter.arena.tilePalette;
            if (palette != null)
                for (int first = 0; first < palette.Count; first += 8)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (int i = first; i < Mathf.Min(first + 8, palette.Count); i++)
                        {
                            Sprite sprite = palette[i];
                            if (sprite == null) continue;
                            Rect rect = GUILayoutUtility.GetRect(48, 48, GUILayout.ExpandWidth(false));
                            if (GUI.Toggle(rect, tileImageBrush == sprite && mapBrush == 6,
                                new GUIContent("", sprite.name), GUI.skin.button))
                            { tileImageBrush = sprite; mapBrush = 6; }
                            Rect imageRect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8);
                            if (PatternPreviewGridGUI.DrawTileSprite(imageRect, sprite)) Repaint();
                        }
                    }
            EditorGUILayout.HelpBox("Sprite 이미지를 선택한 뒤 바닥을 클릭·드래그하세요. 이미지 지우개/우클릭/Shift는 기본 이미지로 복원합니다.\n이미지는 바닥 생성·삭제와 별개입니다. 팔레트 순서 변경이나 맵 크기 변경으로 칠한 이미지가 사라지지 않습니다.", MessageType.None);
            if (mapBrush == 6 && tileImageBrush == null)
                EditorGUILayout.HelpBox("칠할 이미지를 지정하거나 팔레트에서 선택하세요. 텍스처의 Texture Type은 Sprite (2D and UI)여야 합니다.", MessageType.Info);
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
                EditorGUILayout.HelpBox("현재는 모양으로 영역을 지정하고 있습니다. 직접 칠하기로 전환하면 현재 모양을 유지한 채 타일을 추가하거나 지울 수 있습니다.", MessageType.None);
                if (GUILayout.Button("현재 모양을 유지하고 직접 칠하기"))
                {
                    BeginPaint("Convert event area to cells");
                    var host = new PatternPreviewHost(encounter.arena) { player = previewPlayer };
                    using (var context = new PatternContext(host, host.CenterCell))
                        tiles.cells = tiles.Resolve(context).Select(c => c - PaintOrigin(tiles)).ToList();
                    tiles.shape = TileShape.Cells;
                    Changed();
                }
            }
            else if (GUILayout.Button(attackEditorMode == 0 ? "선택 공격 영역 비우기" : "선택 이벤트 영역 비우기"))
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
