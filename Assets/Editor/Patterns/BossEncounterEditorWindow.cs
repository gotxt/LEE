#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NHN.TraceStrike.Patterns;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    public sealed partial class BossEncounterEditorWindow : EditorWindow
    {
        private enum NodeKind { Encounter, Arena, Phase, Pattern, Background, LibraryPattern }

        [SerializeField] private BossEncounterDefinition encounter;
        [SerializeField] private NodeKind node = NodeKind.Encounter;
        [SerializeField] private int phaseIndex = -1;
        [SerializeField] private int patternIndex = -1;
        [SerializeField] private int selectedClip = -1;
        private SerializedObject serialized;
        private Vector2 treeScroll, timelineScroll, inspectorScroll;
        private float pixelsPerSecond = 100f;
        private float snap = 0.05f;
        private float playhead;
        private bool playing;
        private double lastPreviewTime;
        private int draggingClip = -1;
        private bool resizingClip;
        private float dragStartX, originalStart, originalDuration;
        private PatternPreviewHost previewHost;
        private BossRenderStage bossPreview;
        private PatternRunner previewRunner;
        private string previewError;
        private Vector2Int previewPlayer = new Vector2Int(8, 8);

        [MenuItem("Trace Strike/Patterns/Boss Encounter Editor")]
        public static void Open()
        {
            GetWindow<BossEncounterEditorWindow>("Boss Encounter");
        }

        public static void Open(BossEncounterDefinition target)
        {
            BossEncounterEditorWindow window = GetWindow<BossEncounterEditorWindow>("Boss Encounter");
            window.SelectEncounter(target);
        }

        [OnOpenAsset]
        public static bool OnOpen(EntityId id, int line)
        {
            var asset = EditorUtility.EntityIdToObject(id) as BossEncounterDefinition;
            if (asset == null) return false;
            Open(asset);
            return true;
        }

        private void OnEnable()
        {
            minSize = new Vector2(1050f, 650f);
            EditorApplication.update += UpdatePreview;
            Undo.undoRedoPerformed += UndoChanged;
            if (encounter != null) serialized = new SerializedObject(encounter);
        }

        private void OnDisable()
        {
            mapStroke.Cancel();
            eventStroke.Cancel();
            EditorApplication.update -= UpdatePreview;
            Undo.undoRedoPerformed -= UndoChanged;
            DisposePreview();
        }

        private void UndoChanged()
        {
            serialized = encounter != null ? new SerializedObject(encounter) : null;
            ClampSelection();
            RebuildPreview();
            Repaint();
        }

        private void SelectEncounter(BossEncounterDefinition value)
        {
            mapStroke.Cancel();
            eventStroke.Cancel();
            DisposePreview();
            encounter = value;
            if (value != null && value.arena != null)
                previewPlayer = value.arena.overridePlayerStart ? value.arena.playerStart : value.arena.CenterCell;
            serialized = value != null ? new SerializedObject(value) : null;
            node = NodeKind.Encounter;
            phaseIndex = patternIndex = selectedClip = -1;
            playhead = 0f;
            playing = false;
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (encounter == null)
            {
                EditorGUILayout.HelpBox("상단에서 보스 데이터를 선택하거나 ‘새 보스’를 누르세요. 보스 하나의 전장과 모든 공격 패턴을 한곳에서 편집합니다.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTree();
                using (new EditorGUILayout.VerticalScope())
                {
                    EncounterPattern pattern = CurrentPattern();
                    if (node == NodeKind.Arena)
                    {
                        DrawArenaPainter();
                    }
                    else
                    {
                        if (pattern != null)
                            attackEditorMode = GUILayout.Toolbar(attackEditorMode,
                                new[] { "간편 공격 설계", "고급 이벤트 편집" });
                        if (pattern != null && attackEditorMode == 0) DrawAttackDesigner(pattern);
                        else
                        {
                            if (pattern != null) DrawTimeline(pattern);
                            else DrawOverview();
                            DrawLowerPanel(pattern);
                        }
                    }
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                BossEncounterDefinition next = (BossEncounterDefinition)EditorGUILayout.ObjectField(
                    encounter, typeof(BossEncounterDefinition), false, GUILayout.Width(310f));
                if (next != encounter) SelectEncounter(next);
                if (GUILayout.Button("새 보스", EditorStyles.toolbarButton)) CreateEncounter();
                EditorGUI.BeginDisabledGroup(encounter == null);
                if (GUILayout.Button("저장", EditorStyles.toolbarButton)) AssetDatabase.SaveAssets();
                if (GUILayout.Button("에셋 찾기", EditorStyles.toolbarButton)) EditorGUIUtility.PingObject(encounter);
                EditorGUI.EndDisabledGroup();
                GUILayout.FlexibleSpace();
                GUILayout.Label("시간축 확대");
                pixelsPerSecond = GUILayout.HorizontalSlider(pixelsPerSecond, 35f, 250f, GUILayout.Width(100f));
                GUILayout.Label("시간 간격");
                snap = Mathf.Max(0f, EditorGUILayout.FloatField(snap, GUILayout.Width(50f)));
            }
        }

        private void CreateEncounter()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Boss Encounter",
                "BossEncounter", "asset", "Choose where to save the encounter.");
            if (string.IsNullOrEmpty(path)) return;
            var asset = CreateInstance<BossEncounterDefinition>();
            asset.id = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Replace(' ', '-');
            asset.displayName = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            SelectEncounter(asset);
        }

        private void DrawTree()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(245f)))
            {
                EditorGUILayout.LabelField("BOSS ENCOUNTER", EditorStyles.boldLabel);
                treeScroll = EditorGUILayout.BeginScrollView(treeScroll);
                TreeButton("● " + encounter.displayName, NodeKind.Encounter, -1, -1);
                TreeButton("  ▣ 전장 (Arena)", NodeKind.Arena, -1, -1);

                EditorGUILayout.Space(5f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("재사용 패턴 (고급)", EditorStyles.miniBoldLabel);
                    if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(24f)))
                        AddPattern(true, -1);
                }
                for (int i = 0; i < encounter.libraryPatterns.Count; i++)
                    TreeButton("  ◇ " + encounter.libraryPatterns[i].name,
                        NodeKind.LibraryPattern, -1, i);

                for (int p = 0; p < encounter.phases.Count; p++)
                {
                    BossPhaseDefinition phase = encounter.phases[p];
                    EditorGUILayout.Space(5f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Toggle(node == NodeKind.Phase && phaseIndex == p,
                                "▼ " + phase.name, EditorStyles.miniButtonLeft))
                            SelectNode(NodeKind.Phase, p, -1);
                        if (GUILayout.Button(new GUIContent("+", "이 페이즈에 새 패턴 추가"), EditorStyles.miniButtonRight, GUILayout.Width(24f)))
                            AddPattern(false, p);
                    }
                    if (phase.backgroundEnabled && phase.background != null)
                        TreeButton("  ↻ " + phase.background.name, NodeKind.Background, p, -1);
                    else if (GUILayout.Button("  + 반복 배경 패턴 (고급)", EditorStyles.miniButton))
                    {
                        Record("Add background timeline");
                        phase.backgroundEnabled = true;
                        phase.background = NewPattern("Background");
                        Changed();
                        SelectNode(NodeKind.Background, p, -1);
                    }
                    for (int i = 0; i < phase.patterns.Count; i++)
                        TreeButton("  ▶ " + phase.patterns[i].name, NodeKind.Pattern, p, i);
                }
                EditorGUILayout.Space(8f);
                if (GUILayout.Button("+ 보스 페이즈 추가")) AddPhase();
                EditorGUILayout.EndScrollView();
            }
        }

        private void TreeButton(string label, NodeKind kind, int phase, int pattern)
        {
            bool active = node == kind && phaseIndex == phase && patternIndex == pattern;
            if (GUILayout.Toggle(active, label, active ? EditorStyles.miniButton : EditorStyles.label))
                SelectNode(kind, phase, pattern);
        }

        private void SelectNode(NodeKind kind, int phase, int pattern)
        {
            if (node == kind && phaseIndex == phase && patternIndex == pattern) return;
            mapStroke.Cancel();
            eventStroke.Cancel();
            node = kind;
            phaseIndex = phase;
            patternIndex = pattern;
            selectedClip = -1;
            playhead = 0f;
            playing = false;
            RebuildPreview();
        }

        private EncounterPattern CurrentPattern()
        {
            if (encounter == null) return null;
            if (node == NodeKind.LibraryPattern && patternIndex >= 0 && patternIndex < encounter.libraryPatterns.Count)
                return encounter.libraryPatterns[patternIndex];
            if (phaseIndex < 0 || phaseIndex >= encounter.phases.Count) return null;
            BossPhaseDefinition phase = encounter.phases[phaseIndex];
            if (node == NodeKind.Background) return phase.background;
            return node == NodeKind.Pattern && patternIndex >= 0 && patternIndex < phase.patterns.Count
                ? phase.patterns[patternIndex] : null;
        }

        private SerializedProperty CurrentPatternProperty()
        {
            if (node == NodeKind.LibraryPattern)
                return serialized.FindProperty("libraryPatterns").GetArrayElementAtIndex(patternIndex);
            SerializedProperty phase = serialized.FindProperty("phases").GetArrayElementAtIndex(phaseIndex);
            if (node == NodeKind.Background) return phase.FindPropertyRelative("background");
            return phase.FindPropertyRelative("patterns").GetArrayElementAtIndex(patternIndex);
        }

        private void DrawOverview()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(235f)))
            {
                EditorGUILayout.LabelField(node == NodeKind.Arena ? "Arena" :
                    node == NodeKind.Phase ? encounter.phases[phaseIndex].name : encounter.displayName,
                    EditorStyles.largeLabel);
                EditorGUILayout.HelpBox(node == NodeKind.Arena
                    ? "Arena settings are stored with this boss and drive every pattern preview."
                    : node == NodeKind.Phase
                        ? "This phase owns its attack pattern list and optional repeating background timeline."
                        : "Select a phase or pattern from the tree. Everything in this window is saved in one encounter asset.",
                    MessageType.None);
            }
        }

        private void DrawTimeline(EncounterPattern pattern)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(pattern.name, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add Event", EditorStyles.toolbarButton)) EventMenu(pattern);
                if (node != NodeKind.Background &&
                    GUILayout.Button("Duplicate Pattern", EditorStyles.toolbarButton)) DuplicateCurrentPattern();
                if (GUILayout.Button("Delete Pattern", EditorStyles.toolbarButton)) DeleteCurrentPattern();
            }
            timelineScroll = EditorGUILayout.BeginScrollView(timelineScroll, GUILayout.Height(235f));
            float width = Mathf.Max(position.width - 270f, 170f + (pattern.Duration + 2f) * pixelsPerSecond);
            Rect area = GUILayoutUtility.GetRect(width, Mathf.Max(180f, 30f + pattern.clips.Count * 28f));
            EditorGUI.DrawRect(area, new Color(0.12f, 0.13f, 0.16f));
            for (int second = 0; second <= pattern.Duration + 2f; second++)
            {
                float x = area.x + 160f + second * pixelsPerSecond;
                EditorGUI.DrawRect(new Rect(x, area.y, 1f, area.height), new Color(0.25f, 0.26f, 0.29f));
                GUI.Label(new Rect(x + 3f, area.y, 50f, 20f), second + "s");
            }

            Event current = Event.current;
            for (int i = 0; i < pattern.clips.Count; i++)
            {
                PatternClip clip = pattern.clips[i];
                if (clip == null) continue;
                float y = area.y + 27f + i * 28f;
                if (GUI.Button(new Rect(area.x + 3f, y, 151f, 23f), clip.label,
                        i == selectedClip ? EditorStyles.miniButton : EditorStyles.label))
                    selectedClip = i;
                Rect bar = new Rect(area.x + 160f + clip.start * pixelsPerSecond, y,
                    Mathf.Max(8f, clip.duration * pixelsPerSecond), 22f);
                Color color = ClipColor(clip);
                EditorGUI.DrawRect(bar, i == selectedClip ? color * 1.25f : color);
                GUI.Label(bar, " " + clip.label + "  " + clip.duration.ToString("0.00") + "s");
                Rect resize = new Rect(bar.xMax - 7f, y, 7f, 22f);
                EditorGUIUtility.AddCursorRect(resize, MouseCursor.ResizeHorizontal);
                if (current.type == EventType.MouseDown && current.button == 0 && bar.Contains(current.mousePosition))
                {
                    selectedClip = draggingClip = i;
                    resizingClip = resize.Contains(current.mousePosition);
                    dragStartX = current.mousePosition.x;
                    originalStart = clip.start;
                    originalDuration = clip.duration;
                    Record("Move pattern event");
                    current.Use();
                }
            }

            if (draggingClip >= 0 && draggingClip < pattern.clips.Count)
            {
                if (current.type == EventType.MouseDrag)
                {
                    float delta = (current.mousePosition.x - dragStartX) / pixelsPerSecond;
                    PatternClip clip = pattern.clips[draggingClip];
                    if (resizingClip) clip.duration = Quantize(originalDuration + delta);
                    else clip.start = Quantize(originalStart + delta);
                    Changed(false);
                    current.Use();
                }
                if (current.rawType == EventType.MouseUp)
                {
                    draggingClip = -1;
                    RebuildPreview();
                }
            }

            EditorGUI.DrawRect(new Rect(area.x + 160f + playhead * pixelsPerSecond,
                area.y, 2f, area.height), Color.cyan);
            Rect ruler = new Rect(area.x + 160f, area.y, width - 160f, 24f);
            if (current.type == EventType.MouseDown && current.button == 0 && ruler.Contains(current.mousePosition))
            {
                playhead = Mathf.Clamp((current.mousePosition.x - area.x - 160f) /
                    pixelsPerSecond, 0f, pattern.Duration);
                playing = false;
                RebuildPreview();
                current.Use();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawLowerPanel(EncounterPattern pattern)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox,
                           GUILayout.Width(Mathf.Max(350f, (position.width - 245f) * 0.52f))))
                    DrawInspector(pattern);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    tilePreviewScroll = EditorGUILayout.BeginScrollView(tilePreviewScroll);
                    DrawPreview(pattern);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawInspector(EncounterPattern pattern)
        {
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
            serialized.Update();
            EditorGUI.BeginChangeCheck();
            if (node == NodeKind.Encounter)
            {
                EditorGUILayout.PropertyField(serialized.FindProperty("id"));
                EditorGUILayout.PropertyField(serialized.FindProperty("displayName"));
                EditorGUILayout.PropertyField(serialized.FindProperty("portrait"));
                EditorGUILayout.PropertyField(serialized.FindProperty("bossVisual"), true);
            }
            else if (node == NodeKind.Arena)
                EditorGUILayout.PropertyField(serialized.FindProperty("arena"), true);
            else if (node == NodeKind.Phase)
            {
                SerializedProperty phase = serialized.FindProperty("phases").GetArrayElementAtIndex(phaseIndex);
                EditorGUILayout.PropertyField(phase.FindPropertyRelative("name"));
                EditorGUILayout.PropertyField(phase.FindPropertyRelative("health"));
                EditorGUILayout.PropertyField(phase.FindPropertyRelative("initialDelay"));
                EditorGUILayout.PropertyField(phase.FindPropertyRelative("interval"));
                EditorGUILayout.PropertyField(phase.FindPropertyRelative("acceleration"));
                EditorGUILayout.PropertyField(phase.FindPropertyRelative("minimumInterval"));
                EditorGUILayout.PropertyField(phase.FindPropertyRelative("shuffle"));
                EditorGUILayout.PropertyField(phase.FindPropertyRelative("legacyCrystals"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Move Phase Up") && phaseIndex > 0) MovePhase(-1);
                    if (GUILayout.Button("Move Phase Down") && phaseIndex + 1 < encounter.phases.Count) MovePhase(1);
                    if (GUILayout.Button("Delete Phase")) DeletePhase();
                }
            }
            else if (pattern != null)
            {
                SerializedProperty property = CurrentPatternProperty();
                EditorGUILayout.PropertyField(property.FindPropertyRelative("name"));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("id"));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("enabled"));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("minimumDuration"));
                if (node == NodeKind.Pattern || node == NodeKind.LibraryPattern)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Move Pattern Up")) MoveCurrentPattern(-1);
                        if (GUILayout.Button("Move Pattern Down")) MoveCurrentPattern(1);
                    }
                }
                if (selectedClip >= 0 && selectedClip < pattern.clips.Count)
                {
                    EditorGUILayout.Space(5f);
                    EditorGUILayout.LabelField("Selected Event", EditorStyles.boldLabel);
                    SerializedProperty clips = property.FindPropertyRelative("clips");
                    SerializedProperty selectedProperty = clips.GetArrayElementAtIndex(selectedClip);
                    EditorGUILayout.PropertyField(selectedProperty, true);
                    DrawEncounterCallPicker(pattern, selectedProperty);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Duplicate Event")) DuplicateEvent(pattern);
                        if (GUILayout.Button("Delete Event")) DeleteEvent(pattern);
                    }
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(encounter);
                RebuildPreview();
            }
            EditorGUILayout.Space(4f);
            foreach (string error in encounter.ValidateDefinition().Distinct())
                EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (string error in BossAnimatorOptions.Validate(encounter))
                EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.EndScrollView();
        }

        private void DrawEncounterCallPicker(EncounterPattern owner, SerializedProperty clipProperty)
        {
            if (!(owner.clips[selectedClip].action is CallEncounterPatternEvent call)) return;
            List<EncounterPattern> candidates = encounter.AllPatterns()
                .Where(candidate => candidate != null && candidate != owner).ToList();
            string[] labels = new string[candidates.Count + 1];
            labels[0] = "<Select encounter pattern>";
            for (int i = 0; i < candidates.Count; i++)
                labels[i + 1] = candidates[i].name + "  [" + candidates[i].id + "]";
            int current = candidates.FindIndex(candidate => candidate.id == call.patternId) + 1;
            int next = EditorGUILayout.Popup("Encounter Pattern", current, labels);
            if (next == current) return;

            SerializedProperty action = clipProperty.FindPropertyRelative("action");
            SerializedProperty id = action != null ? action.FindPropertyRelative("patternId") : null;
            if (id != null) id.stringValue = next == 0 ? "" : candidates[next - 1].id;
            if (next > 0)
            {
                SerializedProperty duration = clipProperty.FindPropertyRelative("duration");
                duration.floatValue = Mathf.Max(duration.floatValue, candidates[next - 1].Duration);
            }
        }

        private void DrawPreview(EncounterPattern pattern)
        {
            if (pattern == null)
            {
                EditorGUILayout.LabelField("Select a pattern to preview its arena timeline.",
                    EditorStyles.wordWrappedLabel);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "일시정지" : "미리보기 재생"))
                {
                    playing = !playing;
                    if (playhead >= pattern.Duration) { playhead = 0f; RebuildPreview(); }
                    lastPreviewTime = EditorApplication.timeSinceStartup;
                }
                if (GUILayout.Button("처음으로")) { playing = false; playhead = 0f; RebuildPreview(); }
                EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
                if (GUILayout.Button("게임에서 실행"))
                {
                    TraceStrikeGame game = FindAnyObjectByType<TraceStrikeGame>();
                    if (game != null) game.PreviewPattern(encounter, pattern);
                }
                if (GUILayout.Button("게임 실행 중지"))
                {
                    TraceStrikeGame game = FindAnyObjectByType<TraceStrikeGame>();
                    if (game != null && game.IsPatternPreview) game.StopPatternPreview();
                }
                EditorGUI.EndDisabledGroup();
            }
            float next = EditorGUILayout.Slider(playhead, 0f, Mathf.Max(0.01f, pattern.Duration));
            if (!Mathf.Approximately(next, playhead))
            {
                playhead = next;
                playing = false;
                RebuildPreview();
            }
            EditorGUILayout.LabelField("Arena " + encounter.arena.size + "×" + encounter.arena.size +
                " · " + encounter.arena.shape, EditorStyles.miniBoldLabel);
            TileSelection selectedTiles = SelectedTiles(pattern);
            DrawEventPaintTools(selectedTiles);
            var paintedCells = SelectionOverlay(selectedTiles);
            float edge = Mathf.Max(Mathf.Min(300f, position.width * 0.32f), encounter.arena.GridSize * 14f);
            Rect board = GUILayoutUtility.GetRect(edge, edge, GUILayout.ExpandWidth(false));
            var tileSprites = encounter.arena.BuildTileSpriteLookup();
            for (int y = 0; y < encounter.arena.GridSize; y++)
            for (int x = 0; x < encounter.arena.GridSize; x++)
            {
                var cell = new Vector2Int(x, y);
                Rect rect = PatternPreviewGridGUI.CellRect(
                    board, x, y, encounter.arena.GridSize);
                Color color = previewHost != null && previewHost.Walkable.Contains(cell)
                    ? new Color(0.27f, 0.3f, 0.35f) : new Color(0.12f, 0.13f, 0.15f);
                EditorGUI.DrawRect(rect, color);
                if (previewHost != null && previewHost.Walkable.Contains(cell) &&
                    PatternPreviewGridGUI.DrawTileSprite(rect, encounter.arena.ResolveTileSprite(cell, tileSprites))) Repaint();
                if (previewHost != null)
                    foreach (var mark in previewHost.marks.Values)
                        if (mark.Item1.Contains(cell)) EditorGUI.DrawRect(rect, mark.Item2);
                if (showSelectedAttackArea && paintedCells != null && paintedCells.Contains(cell))
                    EditorGUI.DrawRect(rect, new Color(0f, 1f, 0f, 0.55f));
                if (cell == previewPlayer) GUI.Label(rect, "P", EditorStyles.whiteMiniLabel);
            }
            PatternPreviewGridGUI.DrawLines(board, encounter.arena.GridSize);
            if (bossPreview != null && Event.current.type == EventType.Repaint)
            {
                bossPreview.Render();
                GUI.DrawTexture(board, bossPreview.Texture, ScaleMode.StretchToFill, true);
            }
            HandleEventPainting(board, selectedTiles);
            EditorGUILayout.LabelField("클릭·드래그: 칠하기 · 우클릭/Shift: 지우기 · 플레이어 이동: 위치 도구",
                EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrEmpty(previewError))
                EditorGUILayout.HelpBox(previewError, MessageType.Warning);
            if (previewHost != null)
                EditorGUILayout.LabelField(string.Join("\n",
                    previewHost.log.Skip(Mathf.Max(0, previewHost.log.Count - 3))),
                    EditorStyles.wordWrappedMiniLabel);
        }

        private void EventMenu(EncounterPattern pattern)
        {
            var menu = new GenericMenu();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<PatternEvent>()
                         .Where(t => !t.IsAbstract && t.IsSerializable).OrderBy(t => t.Name))
            {
                Type captured = type;
                string category = EventCategory(type) + "/" +
                    ObjectNames.NicifyVariableName(type.Name.Replace("Event", ""));
                menu.AddItem(new GUIContent(category), false, () =>
                {
                    Record("Add pattern event");
                    PatternEvent action = (PatternEvent)Activator.CreateInstance(captured);
                    float duration = 1f;
                    if (action is CallEncounterPatternEvent call)
                    {
                        EncounterPattern first = encounter.AllPatterns().FirstOrDefault(p => p != pattern);
                        if (first != null)
                        {
                            call.patternId = first.id;
                            duration = first.Duration;
                        }
                    }
                    pattern.clips.Add(new PatternClip
                    {
                        label = ObjectNames.NicifyVariableName(captured.Name.Replace("Event", "")),
                        start = playhead,
                        duration = duration,
                        action = action
                    });
                    selectedClip = pattern.clips.Count - 1;
                    Changed();
                });
            }
            menu.ShowAsContext();
        }

        private static string EventCategory(Type type)
        {
            if (typeof(BossEvent).IsAssignableFrom(type)) return "Boss";
            if (type == typeof(WarningEvent) || type == typeof(DamageEvent) ||
                type == typeof(HazardEvent) || type == typeof(ObstacleEvent)) return "Tiles";
            if (type == typeof(VfxEvent) || type == typeof(SfxEvent) ||
                type == typeof(CameraEvent)) return "Presentation";
            if (type == typeof(SpawnEvent) || type == typeof(MoveObjectEvent) ||
                type == typeof(RemoveResourceEvent)) return "Objects";
            return "Flow";
        }

        private void AddPhase()
        {
            Record("Add boss phase");
            var phase = new BossPhaseDefinition { name = "PHASE " + (encounter.phases.Count + 1) };
            phase.patterns.Add(NewPattern("New Pattern"));
            encounter.phases.Add(phase);
            Changed();
            SelectNode(NodeKind.Phase, encounter.phases.Count - 1, -1);
        }

        private void AddPattern(bool library, int phase)
        {
            Record("Add encounter pattern");
            EncounterPattern created = NewPattern(library ? "Helper Pattern" : "New Pattern");
            if (library)
            {
                encounter.libraryPatterns.Add(created);
                SelectNode(NodeKind.LibraryPattern, -1, encounter.libraryPatterns.Count - 1);
            }
            else
            {
                encounter.phases[phase].patterns.Add(created);
                SelectNode(NodeKind.Pattern, phase, encounter.phases[phase].patterns.Count - 1);
            }
            Changed();
        }

        private static EncounterPattern NewPattern(string name)
        {
            return new EncounterPattern
            {
                id = Guid.NewGuid().ToString("N"),
                name = name,
                minimumDuration = 1f
            };
        }

        private void DuplicateCurrentPattern()
        {
            EncounterPattern source = CurrentPattern();
            if (source == null) return;
            Record("Duplicate encounter pattern");
            EncounterPattern copy = ClonePattern(source);
            copy.id = Guid.NewGuid().ToString("N");
            copy.name += " Copy";
            if (node == NodeKind.LibraryPattern)
            {
                encounter.libraryPatterns.Insert(patternIndex + 1, copy);
                patternIndex++;
            }
            else if (node == NodeKind.Pattern)
            {
                encounter.phases[phaseIndex].patterns.Insert(patternIndex + 1, copy);
                patternIndex++;
            }
            else return;
            selectedClip = -1;
            Changed();
        }

        private void DeleteCurrentPattern()
        {
            if (node != NodeKind.Pattern && node != NodeKind.LibraryPattern && node != NodeKind.Background) return;
            Record("Delete encounter pattern");
            if (node == NodeKind.LibraryPattern) encounter.libraryPatterns.RemoveAt(patternIndex);
            else if (node == NodeKind.Pattern) encounter.phases[phaseIndex].patterns.RemoveAt(patternIndex);
            else encounter.phases[phaseIndex].backgroundEnabled = false;
            SelectNode(node == NodeKind.LibraryPattern ? NodeKind.Encounter : NodeKind.Phase,
                node == NodeKind.LibraryPattern ? -1 : phaseIndex, -1);
            Changed();
        }

        private void DeletePhase()
        {
            if (encounter.phases.Count <= 1)
            {
                EditorUtility.DisplayDialog("Cannot delete phase", "An encounter needs at least one phase.", "OK");
                return;
            }
            Record("Delete boss phase");
            encounter.phases.RemoveAt(phaseIndex);
            SelectNode(NodeKind.Encounter, -1, -1);
            Changed();
        }

        private void MovePhase(int delta)
        {
            int target = Mathf.Clamp(phaseIndex + delta, 0, encounter.phases.Count - 1);
            if (target == phaseIndex) return;
            Record("Move boss phase");
            BossPhaseDefinition phase = encounter.phases[phaseIndex];
            encounter.phases.RemoveAt(phaseIndex);
            encounter.phases.Insert(target, phase);
            phaseIndex = target;
            Changed();
        }

        private void MoveCurrentPattern(int delta)
        {
            List<EncounterPattern> list = node == NodeKind.LibraryPattern
                ? encounter.libraryPatterns : encounter.phases[phaseIndex].patterns;
            int target = Mathf.Clamp(patternIndex + delta, 0, list.Count - 1);
            if (target == patternIndex) return;
            Record("Move encounter pattern");
            EncounterPattern pattern = list[patternIndex];
            list.RemoveAt(patternIndex);
            list.Insert(target, pattern);
            patternIndex = target;
            Changed();
        }

        private void DuplicateEvent(EncounterPattern pattern)
        {
            Record("Duplicate pattern event");
            PatternClip source = pattern.clips[selectedClip];
            pattern.clips.Insert(selectedClip + 1, CloneClip(source));
            selectedClip++;
            Changed();
        }

        private void DeleteEvent(EncounterPattern pattern)
        {
            Record("Delete pattern event");
            pattern.clips.RemoveAt(selectedClip);
            selectedClip = Mathf.Min(selectedClip, pattern.clips.Count - 1);
            Changed();
        }

        private static EncounterPattern ClonePattern(EncounterPattern source)
        {
            var copy = new EncounterPattern
            {
                id = source.id,
                name = source.name,
                enabled = source.enabled,
                minimumDuration = source.minimumDuration
            };
            foreach (PatternClip clip in source.clips) copy.clips.Add(CloneClip(clip, true));
            return copy;
        }

        private static PatternClip CloneClip(PatternClip source, bool preserveAttackGroup = false)
        {
            return new PatternClip
            {
                enabled = source.enabled,
                label = source.label + (preserveAttackGroup ? "" : " Copy"),
                start = source.start,
                duration = source.duration,
                attackGroupKey = preserveAttackGroup ? source.attackGroupKey : null,
                attackAtImpact = source.attackAtImpact,
                attackName = source.attackName,
                action = source.action == null ? null : (PatternEvent)JsonUtility.FromJson(
                    JsonUtility.ToJson(source.action), source.action.GetType())
            };
        }

        private void Record(string name)
        {
            Undo.RecordObject(encounter, name);
        }

        private void Changed(bool rebuildPreview = true)
        {
            SynchronizeSimpleAttack();
            EditorUtility.SetDirty(encounter);
            serialized = new SerializedObject(encounter);
            if (rebuildPreview) RebuildPreview();
            Repaint();
        }

        private float Quantize(float value)
        {
            return Mathf.Max(0f, snap > 0f ? Mathf.Round(value / snap) * snap : value);
        }

        private static Color ClipColor(PatternClip clip)
        {
            if (!clip.enabled) return Color.gray;
            if (clip.action is DamageEvent || clip.action is HazardEvent)
                return new Color(0.8f, 0.28f, 0.36f);
            if (clip.action is WarningEvent) return new Color(0.8f, 0.62f, 0.18f);
            if (clip.action is SpawnEvent || clip.action is MoveObjectEvent)
                return new Color(0.3f, 0.67f, 0.42f);
            return new Color(0.2f, 0.55f, 0.7f);
        }

        private TileSelection SelectedTiles(EncounterPattern pattern)
        {
            if (selectedClip < 0 || selectedClip >= pattern.clips.Count) return null;
            PatternEvent action = pattern.clips[selectedClip].action;
            return action?.GetType().GetField("tiles")?.GetValue(action) as TileSelection;
        }

        private Vector2Int PaintOrigin(TileSelection tiles)
        {
            Vector2Int center = new Vector2Int(encounter.arena.GridSize / 2,
                encounter.arena.GridSize / 2);
            return (tiles.anchor == TileAnchor.Absolute ? Vector2Int.zero :
                tiles.anchor == TileAnchor.Player ? previewPlayer : center) + tiles.offset;
        }

        private void RebuildPreview()
        {
            DisposePreview();
            EncounterPattern pattern = CurrentPattern();
            if (encounter == null || pattern == null) return;
            previewError = null;
            try
            {
                previewHost = new PatternPreviewHost(encounter.arena) { player = previewPlayer };
                if (encounter.bossVisual?.prefab != null)
                {
                    bossPreview = new BossRenderStage(encounter.bossVisual, encounter.arena.GridSize, true);
                    previewHost.boss = bossPreview.Presentation;
                }
                previewRunner = new PatternRunner(pattern, new PatternContext(previewHost,
                    previewHost.CenterCell, 0, encounter.FindPattern));
                previewRunner.Advance(0f);
                float remaining = playhead;
                while (remaining > 0f && !previewRunner.IsComplete)
                {
                    float step = Mathf.Min(1f / 60f, remaining);
                    bossPreview?.Presentation.Advance(step);
                    previewRunner.Advance(step);
                    remaining -= step;
                }
            }
            catch (Exception error)
            {
                previewError = error.Message;
                playing = false;
            }
        }

        private void DisposePreview()
        {
            try { previewRunner?.Dispose(); }
            catch { }
            previewRunner = null;
            bossPreview?.Dispose();
            bossPreview = null;
            previewHost = null;
        }

        private void UpdatePreview()
        {
            EncounterPattern pattern = CurrentPattern();
            if (!playing || pattern == null) return;
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - lastPreviewTime);
            lastPreviewTime = now;
            try
            {
                if (previewRunner == null) RebuildPreview();
                while (delta > 0 && previewRunner != null && !previewRunner.IsComplete)
                {
                    float step = Mathf.Min(delta, 1f / 60f);
                    bossPreview?.Presentation.Advance(step);
                    previewRunner.Advance(step);
                    delta -= step;
                }
                playhead = previewRunner != null ? previewRunner.Time : 0f;
            }
            catch (Exception error)
            {
                previewError = error.Message;
                playing = false;
            }
            if (previewRunner == null || previewRunner.IsComplete) playing = false;
            Repaint();
        }

        private void ClampSelection()
        {
            if (encounter == null) return;
            if (phaseIndex >= encounter.phases.Count) phaseIndex = encounter.phases.Count - 1;
            EncounterPattern pattern = CurrentPattern();
            if (pattern != null && selectedClip >= pattern.clips.Count)
                selectedClip = pattern.clips.Count - 1;
        }
    }
}
#endif
