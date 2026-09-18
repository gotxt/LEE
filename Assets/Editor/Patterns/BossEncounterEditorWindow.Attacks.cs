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
        [SerializeField] private int attackEditorMode;
        [SerializeField] private bool showSelectedAttackArea = true;
        private Vector2 attackTimelineScroll, attackSettingsScroll;

        private void DrawAttackDesigner(EncounterPattern pattern)
        {
            var steps = AttackStepEditing.Find(pattern);
            var selected = steps.FirstOrDefault(s => s.Members.Contains(
                selectedClip >= 0 && selectedClip < pattern.clips.Count ? pattern.clips[selectedClip] : null));
            if (selected == null && steps.Count > 0) selected = steps[0];
            selectedClip = selected != null ? pattern.clips.IndexOf(selected.Warning) : -1;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                string patternName = EditorGUILayout.TextField("패턴 이름", pattern.name);
                bool patternEnabled = GUILayout.Toggle(pattern.enabled, "패턴 사용", GUILayout.Width(90));
                if (EditorGUI.EndChangeCheck()) { BeginPaint("Edit pattern"); pattern.name = patternName; pattern.enabled = patternEnabled; Changed(); }
                using (new EditorGUI.DisabledScope(node == NodeKind.Background))
                    if (GUILayout.Button("패턴 복제", GUILayout.Width(85))) { DuplicateCurrentPattern(); GUIUtility.ExitGUI(); }
            }
            EditorGUILayout.HelpBox("① 공격 추가 → ② 목록에서 공격 선택 → ③ 맵에 영역 칠하기 → ④ 시간 조절 후 재생\n경고와 타격은 자동 연결됩니다. 같은 시간에 배치하면 동시에 공격합니다. 변경 후 상단의 ‘저장’을 누르세요.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 다음 공격"))
                {
                    BeginPaint("Add attack step");
                    var added = AttackStepEditing.Add(pattern, steps.Count == 0 ? 0 : steps.Max(s => s.End) + 0.25f);
                    added.Warning.attackName = (steps.Count + 1) + "차 공격";
                    selectedClip = pattern.clips.IndexOf(added.Warning); playhead = added.Warning.start;
                    eventBrush = 0; Changed(); GUIUtility.ExitGUI();
                }
                using (new EditorGUI.DisabledScope(selected == null))
                {
                    if (GUILayout.Button("선택 공격 복제")) DuplicateAttack(pattern, selected, false);
                    if (GUILayout.Button("동시 공격 복제")) DuplicateAttack(pattern, selected, true);
                    if (GUILayout.Button("선택 공격 삭제") && EditorUtility.DisplayDialog("공격 삭제",
                        "선택한 공격의 경고·타격과 연결된 연출을 함께 삭제합니다. Ctrl+Z로 되돌릴 수 있습니다.", "삭제", "취소"))
                    {
                        BeginPaint("Delete attack step"); AttackStepEditing.Delete(pattern, selected);
                        selectedClip = -1; Changed(); GUIUtility.ExitGUI();
                    }
                }
            }

            DrawAttackOverview(pattern, steps, selected);
            int extra = pattern.clips.Count(c => !steps.Any(s => s.Members.Contains(c)));
            if (extra > 0)
                EditorGUILayout.HelpBox($"개별 이벤트 {extra}개는 그대로 실행됩니다. 연결이 모호하거나 특수한 이벤트는 상단 ‘고급 이벤트 편집’에서 확인하세요.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(330)))
                {
                    attackSettingsScroll = EditorGUILayout.BeginScrollView(attackSettingsScroll);
                    if (selected == null)
                        EditorGUILayout.HelpBox("‘+ 다음 공격’을 누르면 경고와 타격이 함께 만들어집니다. 오른쪽 맵에 공격할 타일을 칠하세요.", MessageType.Info);
                    else DrawAttackSettings(pattern, selected);
                    EditorGUILayout.Space();
                    float minimum = EditorGUILayout.FloatField("패턴 최소 길이 (초)", pattern.minimumDuration);
                    if (minimum != pattern.minimumDuration && !float.IsNaN(minimum) && !float.IsInfinity(minimum))
                    { BeginPaint("Change pattern length"); pattern.minimumDuration = Mathf.Max(0, minimum); Changed(); }
                    EditorGUILayout.LabelField($"전체 재생 시간: {pattern.Duration:0.##}초", EditorStyles.miniLabel);
                    foreach (string error in encounter.ValidateDefinition().Distinct())
                        EditorGUILayout.HelpBox(error, MessageType.Error);
                    EditorGUILayout.EndScrollView();
                }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    tilePreviewScroll = EditorGUILayout.BeginScrollView(tilePreviewScroll);
                    EditorGUILayout.LabelField(selected == null ? "패턴 미리보기" : "편집 중: " + selected.Name, EditorStyles.boldLabel);
                    showSelectedAttackArea = EditorGUILayout.ToggleLeft("선택한 공격 영역을 초록색으로 표시", showSelectedAttackArea);
                    DrawPreview(pattern);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DuplicateAttack(EncounterPattern pattern, AttackStepEditing.Step selected, bool simultaneous)
        {
            BeginPaint("Duplicate attack step");
            var copy = AttackStepEditing.Duplicate(pattern, selected, simultaneous ? selected.Warning.start :
                AttackStepEditing.Find(pattern).Max(s => s.End) + 0.25f);
            selectedClip = pattern.clips.IndexOf(copy.Warning); playhead = copy.Warning.start;
            Changed(); GUIUtility.ExitGUI();
        }

        private void DrawAttackOverview(EncounterPattern pattern, List<AttackStepEditing.Step> steps, AttackStepEditing.Step selected)
        {
            EditorGUILayout.LabelField("공격 목록 / 시간표 — 노랑: 경고 · 빨강: 타격 · 행을 눌러 선택", EditorStyles.miniLabel);
            attackTimelineScroll = EditorGUILayout.BeginScrollView(attackTimelineScroll,
                GUILayout.Height(Mathf.Clamp(45 + steps.Count * 30, 85, 180)));
            float span = Mathf.Max(3, pattern.Duration + 0.5f);
            float width = Mathf.Max(position.width - 290, 200 + span * pixelsPerSecond);
            Rect area = GUILayoutUtility.GetRect(width, Mathf.Max(60, 26 + steps.Count * 30));
            EditorGUI.DrawRect(area, new Color(0.12f, 0.13f, 0.16f));
            for (int second = 0; second <= span; second++)
            {
                float x = area.x + 190 + second * pixelsPerSecond;
                EditorGUI.DrawRect(new Rect(x, area.y, 1, area.height), new Color(0.25f, 0.26f, 0.29f));
                GUI.Label(new Rect(x + 3, area.y, 58, 20), second + "초");
            }
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i]; float y = area.y + 25 + i * 30;
                Rect row = new Rect(area.x, y, area.width, 27);
                if (step == selected) EditorGUI.DrawRect(row, new Color(0.22f, 0.32f, 0.4f, 0.75f));
                GUI.Label(new Rect(area.x + 4, y, 184, 25), $"{i + 1}. {step.Name}" + (step.Warning.enabled ? "" : " (꺼짐)"));
                Rect warning = new Rect(area.x + 190 + step.Warning.start * pixelsPerSecond, y + 3,
                    Mathf.Max(2, step.Warning.duration * pixelsPerSecond), 20);
                Rect impact = new Rect(area.x + 190 + step.Damage.start * pixelsPerSecond, y + 3,
                    Mathf.Max(5, step.Damage.duration * pixelsPerSecond), 20);
                EditorGUI.DrawRect(warning, step.Warning.enabled ? new Color(0.85f, 0.65f, 0.2f) : Color.gray);
                EditorGUI.DrawRect(impact, step.Warning.enabled ? new Color(0.85f, 0.25f, 0.3f) : Color.gray);
                GUI.Label(warning, new GUIContent("", $"경고 {step.Warning.start:0.##}~{step.Damage.start:0.##}초"));
                GUI.Label(impact, new GUIContent("", $"타격 {step.Damage.start:0.##}초"));
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(Event.current.mousePosition))
                {
                    eventStroke.Cancel(); selectedClip = pattern.clips.IndexOf(step.Warning);
                    playing = false; playhead = step.Warning.start; RebuildPreview(); Event.current.Use(); Repaint();
                }
            }
            float cursor = area.x + 190 + playhead * pixelsPerSecond;
            EditorGUI.DrawRect(new Rect(cursor, area.y, 2, area.height), Color.white);
            EditorGUILayout.EndScrollView();
        }

        private void DrawAttackSettings(EncounterPattern pattern, AttackStepEditing.Step step)
        {
            EditorGUILayout.LabelField("선택한 공격", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("공격 이름", step.Name);
            bool enabled = EditorGUILayout.Toggle("이 공격 사용", step.Warning.enabled);
            float start = EditorGUILayout.FloatField("경고 시작 (초)", step.Warning.start);
            float lead = EditorGUILayout.FloatField("경고 길이 (초)", step.Warning.duration);
            float duration = EditorGUILayout.FloatField(new GUIContent("타격 구간 (초)", "단발 공격 이벤트의 실행 구간입니다. 지속 피해 장판은 고급 편집에서 추가하세요."), step.Damage.duration);
            if (EditorGUI.EndChangeCheck())
            {
                BeginPaint("Edit attack timing");
                step.Warning.attackName = name;
                if (enabled != step.Warning.enabled) AttackStepEditing.SetEnabled(step, enabled);
                AttackStepEditing.SetTiming(step, start, lead, duration);
                Changed();
            }
            EditorGUILayout.HelpBox($"경고 {step.Warning.start:0.##}초 → 타격 {step.Damage.start:0.##}초\n다른 공격과 시간이 겹쳐도 괜찮습니다.", MessageType.None);
            EditorGUILayout.LabelField("공격 영역", EditorStyles.boldLabel);
            var tiles = step.Tiles;
            EditorGUI.BeginChangeCheck();
            int shape = EditorGUILayout.Popup("영역 모양", (int)tiles.shape, new[] { "직접 칠하기", "전체 바닥", "십자", "마름모 테두리", "대각선", "십자 + 마름모", "가로 줄무늬", "세로 줄무늬", "정사각형", "체크무늬" });
            int anchor = EditorGUILayout.Popup("위치 기준", (int)tiles.anchor, new[] { "맵 중앙", "경고 시작 시 플레이어", "호출한 위치 (고급)", "맵 고정 좌표" });
            Vector2Int offset = EditorGUILayout.Vector2IntField("위치 보정 (칸)", tiles.offset);
            int radius = tiles.radius;
            if (shape == (int)TileShape.Diamond || shape == (int)TileShape.Combined || shape == (int)TileShape.Rectangle || shape == (int)TileShape.Checker)
                radius = EditorGUILayout.IntSlider("반경 (칸)", tiles.radius, 0, TrailFieldModel.MaxSize);
            bool escape = EditorGUILayout.Toggle(new GUIContent("탈출 경로 확보", "실행 시 플레이어가 피할 수 있도록 일부 공격 타일을 제외할 수 있습니다."), tiles.ensureEscape);
            if (EditorGUI.EndChangeCheck())
            {
                BeginPaint("Edit attack area");
                if (shape == (int)TileShape.Cells && tiles.shape != TileShape.Cells)
                {
                    var host = new PatternPreviewHost(encounter.arena) { player = previewPlayer };
                    using (var context = new PatternContext(host, host.CenterCell))
                        tiles.cells = tiles.Resolve(context).Select(c => c - PaintOrigin(tiles)).ToList();
                }
                tiles.shape = (TileShape)shape; tiles.anchor = (TileAnchor)anchor;
                tiles.offset = offset; tiles.radius = radius; tiles.ensureEscape = escape;
                eventBrush = 0; Changed();
            }
            if (tiles.shape == TileShape.Cells && tiles.cells.Count == 0)
                EditorGUILayout.HelpBox("아직 공격 영역이 없습니다. 오른쪽 맵을 클릭·드래그해서 칠하세요.", MessageType.Warning);
            EditorGUILayout.LabelField("경고·타격·연결된 이펙트가 같은 영역을 사용합니다. 다른 공격의 영역에는 영향을 주지 않습니다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("소리 (선택 사항)", EditorStyles.boldLabel);
            DrawAttackSound(pattern, step, false);
            DrawAttackSound(pattern, step, true);
            EditorGUILayout.LabelField("추가 연출이나 지속 장판은 ‘고급 이벤트 편집’에서 설정하세요.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawAttackSound(EncounterPattern pattern, AttackStepEditing.Step step, bool impact)
        {
            var sounds = step.Members.Where(c => c.action is SfxEvent &&
                (c.attackGroupKey == step.Key ? c.attackAtImpact : c.start >= step.Damage.start) == impact).ToList();
            if (sounds.Count > 1) { EditorGUILayout.LabelField("여러 소리가 연결되어 있습니다. 고급 편집에서 수정하세요.", EditorStyles.wordWrappedMiniLabel); return; }
            AudioClip current = sounds.Count == 0 ? null : ((SfxEvent)sounds[0].action).clip;
            AudioClip next = (AudioClip)EditorGUILayout.ObjectField(impact ? "타격 소리" : "경고 소리", current, typeof(AudioClip), false);
            if (next == current) return;
            BeginPaint("Edit attack sound"); AttackStepEditing.SetSound(pattern, step, impact, next); Changed();
        }

        private void SynchronizeSimpleAttack()
        {
            var pattern = CurrentPattern();
            if (attackEditorMode != 0 || pattern == null || selectedClip < 0 || selectedClip >= pattern.clips.Count) return;
            var step = AttackStepEditing.Find(pattern).FirstOrDefault(s => s.Warning == pattern.clips[selectedClip]);
            if (step != null) AttackStepEditing.SynchronizeArea(step);
        }

        private HashSet<Vector2Int> SelectionOverlay(TileSelection tiles)
        {
            if (tiles == null || !showSelectedAttackArea) return null;
            var host = new PatternPreviewHost(encounter.arena) { player = previewPlayer };
            using (var context = new PatternContext(host, host.CenterCell)) return tiles.Resolve(context);
        }
    }
}
#endif
