#if UNITY_EDITOR
using System;
using System.Linq;
using NHN.TraceStrike.Patterns;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    public sealed class PatternTimelineWindow : EditorWindow
    {
        [SerializeField] private PatternSequence pattern;
        private SerializedObject serialized;
        private Vector2 timelineScroll, inspectorScroll;
        private int selected = -1, dragging = -1;
        private bool resizing, playing;
        private float dragX, initialStart, initialDuration, playhead;
        private float pixelsPerSecond = 100, snap = 0.05f;
        private int fieldSize = 17;
        private Vector2Int previewPlayer = new Vector2Int(8, 8);
        private double lastTime;
        private PatternRunner preview;
        private PatternPreviewHost previewHost;
        private string previewError;

        [MenuItem("Trace Strike/Patterns/Shared Pattern Timeline Editor")]
        public static void Open() => GetWindow<PatternTimelineWindow>("Shared Pattern Timeline");
        [OnOpenAsset]
        public static bool OnOpen(EntityId id, int line)
        {
            var asset = EditorUtility.EntityIdToObject(id) as PatternSequence;
            if (asset == null) return false;
            var window = GetWindow<PatternTimelineWindow>("Shared Pattern Timeline"); window.Select(asset); return true;
        }
        private void OnEnable() { minSize = new Vector2(920, 620); EditorApplication.update += UpdatePreview; Undo.undoRedoPerformed += UndoChanged; }
        private void OnDisable() { EditorApplication.update -= UpdatePreview; Undo.undoRedoPerformed -= UndoChanged; preview?.Dispose(); }
        private void UndoChanged() { serialized = pattern != null ? new SerializedObject(pattern) : null; RebuildPreview(); Repaint(); }
        private void Select(PatternSequence asset)
        { preview?.Dispose(); pattern = asset; serialized = asset != null ? new SerializedObject(asset) : null; selected = -1; playhead = 0; playing = false; RebuildPreview(); }
        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var next = (PatternSequence)EditorGUILayout.ObjectField(pattern, typeof(PatternSequence), false, GUILayout.Width(270));
                if (next != pattern) Select(next);
                if (GUILayout.Button("New", EditorStyles.toolbarButton))
                {
                    string path = EditorUtility.SaveFilePanelInProject("New pattern", "PatternData_NewPattern", "asset", "Choose an asset path");
                    if (!string.IsNullOrEmpty(path)) { var asset = CreateInstance<PatternSequence>(); AssetDatabase.CreateAsset(asset, path); Select(asset); }
                }
                if (GUILayout.Button("Add event", EditorStyles.toolbarButton) && pattern != null) EventMenu();
                if (GUILayout.Button("Save", EditorStyles.toolbarButton)) AssetDatabase.SaveAssets();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Zoom"); pixelsPerSecond = GUILayout.HorizontalSlider(pixelsPerSecond, 35, 250, GUILayout.Width(90));
                GUILayout.Label("Snap"); snap = EditorGUILayout.FloatField(snap, GUILayout.Width(48));
            }
            if (pattern == null) { EditorGUILayout.HelpBox("Open a Pattern Sequence asset or create one. Double-clicking a sequence opens this editor.", MessageType.Info); return; }
            if (serialized == null) serialized = new SerializedObject(pattern);
            DrawTimeline();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.49f))) DrawInspector();
                using (new EditorGUILayout.VerticalScope()) DrawPreview();
            }
        }

        private void EventMenu()
        {
            var menu = new GenericMenu();
            foreach (var type in TypeCache.GetTypesDerivedFrom<PatternEvent>()
                         .Where(t => !t.IsAbstract && t.IsSerializable &&
                             t != typeof(CallEncounterPatternEvent)).OrderBy(t => t.Name))
            {
                var captured = type;
                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false, () => {
                    Undo.RecordObject(pattern, "Add pattern event");
                    pattern.clips.Add(new PatternClip { label = ObjectNames.NicifyVariableName(captured.Name),
                        start = playhead, action = (PatternEvent)Activator.CreateInstance(captured) });
                    selected = pattern.clips.Count - 1; Changed();
                });
            }
            menu.ShowAsContext();
        }

        private void DrawTimeline()
        {
            timelineScroll = EditorGUILayout.BeginScrollView(timelineScroll, GUILayout.Height(225));
            float width = Mathf.Max(position.width - 25, 170 + (pattern.Duration + 2) * pixelsPerSecond);
            Rect area = GUILayoutUtility.GetRect(width, Mathf.Max(180, 30 + pattern.clips.Count * 28));
            EditorGUI.DrawRect(area, new Color(0.12f, 0.13f, 0.16f));
            for (int second = 0; second <= pattern.Duration + 2; second++)
            {
                float x = area.x + 160 + second * pixelsPerSecond;
                EditorGUI.DrawRect(new Rect(x, area.y, 1, area.height), new Color(0.25f, 0.26f, 0.29f));
                GUI.Label(new Rect(x + 3, area.y, 60, 20), second + "s");
            }
            Event e = Event.current;
            for (int i = 0; i < pattern.clips.Count; i++)
            {
                var clip = pattern.clips[i]; if (clip == null) continue;
                float y = area.y + 27 + i * 28;
                if (GUI.Button(new Rect(area.x + 3, y, 151, 23), clip.label, i == selected ? EditorStyles.miniButton : EditorStyles.label)) selected = i;
                Rect bar = new Rect(area.x + 160 + clip.start * pixelsPerSecond, y, Mathf.Max(8, clip.duration * pixelsPerSecond), 22);
                Color color = clip.action is DamageEvent || clip.action is HazardEvent ? new Color(0.8f, 0.28f, 0.36f) :
                    clip.action is WarningEvent ? new Color(0.8f, 0.62f, 0.18f) : new Color(0.2f, 0.55f, 0.7f);
                if (!clip.enabled) color = Color.gray;
                EditorGUI.DrawRect(bar, i == selected ? color * 1.3f : color);
                GUI.Label(bar, " " + clip.label + "  " + clip.duration.ToString("0.00") + "s");
                EditorGUIUtility.AddCursorRect(new Rect(bar.xMax - 7, y, 7, 22), MouseCursor.ResizeHorizontal);
                if (e.type == EventType.MouseDown && e.button == 0 && bar.Contains(e.mousePosition))
                {
                    selected = dragging = i; resizing = e.mousePosition.x >= bar.xMax - 7;
                    dragX = e.mousePosition.x; initialStart = clip.start; initialDuration = clip.duration;
                    Undo.RecordObject(pattern, "Move/resize pattern event"); e.Use();
                }
            }
            if (dragging >= 0 && dragging < pattern.clips.Count)
            {
                if (e.type == EventType.MouseDrag)
                {
                    float delta = (e.mousePosition.x - dragX) / pixelsPerSecond;
                    var clip = pattern.clips[dragging];
                    if (resizing) clip.duration = Quantize(initialDuration + delta); else clip.start = Quantize(initialStart + delta);
                    Changed(); e.Use();
                }
                if (e.rawType == EventType.MouseUp) dragging = -1;
            }
            EditorGUI.DrawRect(new Rect(area.x + 160 + playhead * pixelsPerSecond, area.y, 2, area.height), Color.cyan);
            if (e.type == EventType.MouseDown && e.button == 0 && new Rect(area.x + 160, area.y, width - 160, 24).Contains(e.mousePosition))
            { playhead = Mathf.Clamp((e.mousePosition.x - area.x - 160) / pixelsPerSecond, 0, pattern.Duration); playing = false; RebuildPreview(); e.Use(); }
            EditorGUILayout.EndScrollView();
        }
        private float Quantize(float value) => Mathf.Max(0, snap > 0 ? Mathf.Round(value / snap) * snap : value);
        private void Changed() { EditorUtility.SetDirty(pattern); serialized = new SerializedObject(pattern); RebuildPreview(); Repaint(); }

        private void DrawInspector()
        {
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
            serialized.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serialized.FindProperty("minimumDuration"));
            var clips = serialized.FindProperty("clips");
            if (selected >= 0 && selected < clips.arraySize)
            {
                var clip = clips.GetArrayElementAtIndex(selected);
                EditorGUILayout.LabelField("Selected event", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(clip, true);
            }
            if (EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedProperties(); RebuildPreview(); }
            if (selected >= 0 && selected < pattern.clips.Count)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Duplicate"))
                    {
                        Undo.RecordObject(pattern, "Duplicate event"); var old = pattern.clips[selected];
                        pattern.clips.Insert(selected + 1, new PatternClip { label = old.label + " copy", start = old.start,
                            duration = old.duration, enabled = old.enabled, action = old.action == null ? null :
                            (PatternEvent)JsonUtility.FromJson(JsonUtility.ToJson(old.action), old.action.GetType()) }); selected++; Changed();
                    }
                    if (GUILayout.Button("Delete")) { Undo.RecordObject(pattern, "Delete event"); pattern.clips.RemoveAt(selected); selected = -1; Changed(); }
                    if (GUILayout.Button("↑") && selected > 0) { Undo.RecordObject(pattern, "Reorder event"); var c = pattern.clips[selected]; pattern.clips.RemoveAt(selected--); pattern.clips.Insert(selected, c); Changed(); }
                }
            }
            foreach (string error in PatternValidation.Errors(pattern)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.EndScrollView();
        }

        private void DrawPreview()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "Pause" : "Play")) { playing = !playing; if (playhead >= pattern.Duration) { playhead = 0; RebuildPreview(); } lastTime = EditorApplication.timeSinceStartup; }
                if (GUILayout.Button("Reset")) { playing = false; playhead = 0; RebuildPreview(); }
                EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
                if (GUILayout.Button("Run in game"))
                { var game = FindAnyObjectByType<TraceStrikeGame>(); if (game != null) game.PreviewPattern(pattern); }
                if (GUILayout.Button("Stop in game"))
                { var game = FindAnyObjectByType<TraceStrikeGame>(); if (game != null && game.IsPatternPreview) game.StopPatternPreview(); }
                EditorGUI.EndDisabledGroup();
            }
            float time = EditorGUILayout.Slider(playhead, 0, Mathf.Max(0.01f, pattern.Duration));
            if (!Mathf.Approximately(time, playhead)) { playhead = time; playing = false; RebuildPreview(); }
            int size = EditorGUILayout.IntSlider("Preview arena", fieldSize, 5, TrailFieldModel.MaxSize);
            if (size != fieldSize) { fieldSize = size; RebuildPreview(); }
            GUILayout.Label("Left: paint Cells offsets. Right: player position. Simulation only; VFX/SFX/movement are logged.", EditorStyles.wordWrappedMiniLabel);
            float edge = Mathf.Min(300, position.width * 0.45f);
            Rect board = GUILayoutUtility.GetRect(edge, edge, GUILayout.ExpandWidth(false));
            var selectedTiles = SelectedTiles();
            var orderedMarks = previewHost?.GetOrderedMarks();
            for (int y = 0; y < Mathf.Max(TrailFieldModel.Size, fieldSize); y++) for (int x = 0; x < Mathf.Max(TrailFieldModel.Size, fieldSize); x++)
            {
                var cell = new Vector2Int(x, y);
                Rect r = PatternPreviewGridGUI.CellRect(board, x, y, Mathf.Max(TrailFieldModel.Size, fieldSize));
                Color color = previewHost != null && previewHost.Walkable.Contains(cell) ? new Color(0.27f, 0.3f, 0.35f) : new Color(0.12f, 0.13f, 0.15f);
                if (orderedMarks != null) foreach (var mark in orderedMarks) if (mark.Cells.Contains(cell)) color = Color.Lerp(color, mark.Color, mark.Color.a);
                EditorGUI.DrawRect(r, color);
                if (selectedTiles != null && selectedTiles.shape == TileShape.Cells && selectedTiles.cells.Contains(cell - PaintOrigin(selectedTiles)))
                    PatternPreviewGridGUI.DrawSelectionOutline(r);
                if (cell == previewPlayer) GUI.Label(r, "P", EditorStyles.whiteMiniLabel);
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    if (Event.current.button == 1) { previewPlayer = cell; RebuildPreview(); }
                    else if (selectedTiles != null && selectedTiles.shape == TileShape.Cells)
                    {
                        Undo.RecordObject(pattern, "Paint pattern tiles"); var offset = cell - PaintOrigin(selectedTiles);
                        if (!selectedTiles.cells.Remove(offset)) selectedTiles.cells.Add(offset); Changed();
                    }
                    Event.current.Use();
                }
            }
            PatternPreviewGridGUI.DrawLines(board, Mathf.Max(TrailFieldModel.Size, fieldSize));
            if (!string.IsNullOrEmpty(previewError)) EditorGUILayout.HelpBox(previewError, MessageType.Warning);
            if (previewHost != null) GUILayout.Label(string.Join("\n", previewHost.log.Skip(Mathf.Max(0, previewHost.log.Count - 4))), EditorStyles.wordWrappedMiniLabel);
        }
        private TileSelection SelectedTiles()
        {
            if (selected < 0 || selected >= pattern.clips.Count) return null;
            var action = pattern.clips[selected].action;
            return action?.GetType().GetField("tiles")?.GetValue(action) as TileSelection;
        }
        private Vector2Int PaintOrigin(TileSelection tiles) => (tiles.anchor == TileAnchor.Absolute ? Vector2Int.zero :
            tiles.anchor == TileAnchor.Player ? previewPlayer : previewHost.CenterCell) + tiles.offset;
        private void RebuildPreview()
        {
            preview?.Dispose(); preview = null; previewError = null;
            previewHost = new PatternPreviewHost(fieldSize) { player = previewPlayer };
            if (pattern == null) return;
            try
            {
                preview = new PatternRunner(pattern, new PatternContext(previewHost, previewHost.CenterCell));
                preview.Advance(0);
                for (float t = 0; t < playhead && !preview.IsComplete; t += 1f / 60) preview.Advance(Mathf.Min(1f / 60, playhead - t));
            }
            catch (Exception ex) { previewError = ex.Message; playing = false; }
        }
        private void UpdatePreview()
        {
            if (!playing || pattern == null) return;
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - lastTime); lastTime = now;
            try { if (preview == null) RebuildPreview(); preview?.Advance(dt); playhead = preview != null ? preview.Time : 0; }
            catch (Exception ex) { previewError = ex.Message; playing = false; }
            if (preview == null || preview.IsComplete) playing = false;
            Repaint();
        }
    }
}
#endif
