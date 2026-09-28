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
        private BossMechanicSession patternMechanicPreview;
        private int outputPreviewPhase;
        private float outputPreviewWarmup = 25;
        private int OutputPhase => phaseIndex >= 0 ? phaseIndex : Mathf.Clamp(outputPreviewPhase, 0, encounter.phases.Count - 1);

        internal static string EnsureMechanicConnection(BossEncounterDefinition boss, BossMechanicDefinition mechanic)
        {
            if (!string.IsNullOrEmpty(mechanic.connectionId)) return mechanic.connectionId;
            Undo.RecordObject(boss, "Connect mechanic output");
            mechanic.connectionId = Guid.NewGuid().ToString("N"); EditorUtility.SetDirty(boss);
            return mechanic.connectionId;
        }
        private void DrawMechanicOutputPicker(SerializedProperty clipProperty)
        {
            var action = clipProperty.FindPropertyRelative("action");
            var id = action.FindPropertyRelative("mechanicId");
            var key = action.FindPropertyRelative("outputKey");
            var sources = encounter.phases.SelectMany((phase, p) =>
                (phase.mechanics ?? new System.Collections.Generic.List<BossMechanicDefinition>())
                .Select((mechanic, i) => (mechanic, phase: p, index: i)))
                .Where(x => x.mechanic != null && x.mechanic.PositionOutputs.Count > 0 && (phaseIndex < 0 || x.phase == phaseIndex)).ToArray();
            var labels = new[] { "기믹 선택" }.Concat(sources.Select(x =>
                $"{encounter.phases[x.phase].name} / {x.index + 1}. {x.mechanic.name}" + (x.mechanic.enabled ? "" : " (꺼짐)"))).ToArray();
            int current = Array.FindIndex(sources, x => !string.IsNullOrEmpty(id.stringValue) && x.mechanic.connectionId == id.stringValue) + 1;
            int next = EditorGUILayout.Popup("값을 제공할 기믹", current, labels);
            if (next != current)
            {
                if (next == 0) { id.stringValue = ""; key.stringValue = ""; }
                else
                {
                    // Flush pending fields before issuing an Undo for a different serialized path.
                    serialized.ApplyModifiedProperties();
                    string stableId = EnsureMechanicConnection(encounter, sources[next - 1].mechanic);
                    serialized.Update();
                    id.stringValue = stableId; key.stringValue = sources[next - 1].mechanic.PositionOutputs[0].Key;
                }
                current = next;
            }
            if (current > 0)
            {
                var ports = sources[current - 1].mechanic.PositionOutputs;
                int selected = ports.ToList().FindIndex(p => p.Key == key.stringValue) + 1;
                int chosen = EditorGUILayout.Popup("위치 출력 값", selected, new[] { "출력 선택" }.Concat(ports.Select(p => p.Label)).ToArray());
                if (chosen != selected) key.stringValue = chosen == 0 ? "" : ports[chosen - 1].Key;
                if (selected == 0 && !string.IsNullOrEmpty(key.stringValue)) EditorGUILayout.HelpBox("없는 출력 키: " + key.stringValue, MessageType.Error);
            }
            else if (!string.IsNullOrEmpty(id.stringValue)) EditorGUILayout.HelpBox("이 페이즈에서 연결한 기믹을 찾을 수 없습니다. 다시 선택하세요.", MessageType.Error);
            EditorGUILayout.HelpBox("이벤트 시작 순간의 위치들을 고정하여 모든 위치에서 같은 패턴을 동시에 실행합니다. 자식 공격의 위치 기준은 ‘기믹 / 호출 위치’로 설정하세요. 새로 성장/활성화된 대상은 다음 호출부터 참여합니다.", MessageType.Info);
        }
        private void DrawMechanicOutputInfo(BossMechanicDefinition mechanic)
        {
            if (mechanic.PositionOutputs.Count == 0) return;
            EditorGUILayout.LabelField("패턴에 제공하는 위치 값", EditorStyles.boldLabel);
            foreach (var output in mechanic.PositionOutputs) EditorGUILayout.LabelField("• " + output.Label);
            EditorGUILayout.HelpBox("패턴의 고급 이벤트 편집 → 기믹 → 기믹 위치에서 패턴 호출에서 이 기믹을 선택하세요. ID는 최초 연결 때 저장하며 이름이나 목록 순서를 바꿔도 연결이 유지됩니다.", MessageType.None);
            if (!string.IsNullOrEmpty(mechanic.connectionId) && encounter.phases.Where(p => p != null)
                .SelectMany(p => p.mechanics ?? new System.Collections.Generic.List<BossMechanicDefinition>())
                .Count(m => m != null && m.connectionId == mechanic.connectionId) > 1)
            {
                EditorGUILayout.HelpBox("복제된 기믹의 연결 ID가 중복입니다. 복제본에서 새 ID를 발급하고 해당 공격의 기믹을 다시 선택하세요.", MessageType.Error);
                if (GUILayout.Button("선택한 기믹의 연결 ID 새로 발급"))
                { serialized.ApplyModifiedProperties(); BeginPaint("Reidentify duplicated mechanic"); mechanic.connectionId = Guid.NewGuid().ToString("N"); Changed(); GUIUtility.ExitGUI(); }
            }
        }
        private void DrawMechanicOutputPreview(EncounterPattern pattern)
        {
            if (!MechanicOutputValidation.UsesOutputs(pattern, encounter.FindPattern)) return;
            EditorGUILayout.LabelField("기믹 합동 미리보기", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            if (phaseIndex < 0)
                outputPreviewPhase = EditorGUILayout.Popup("시험할 페이즈", OutputPhase, encounter.phases.Select(p => p.name).ToArray());
            float warmup = EditorGUILayout.FloatField("기믹 사전 진행 (초)", outputPreviewWarmup);
            if (!float.IsNaN(warmup) && !float.IsInfinity(warmup)) outputPreviewWarmup = Mathf.Clamp(warmup, 0, 600);
            if (EditorGUI.EndChangeCheck()) { playhead = 0; playing = false; RebuildPreview(); }
            EditorGUILayout.HelpBox("선택 페이즈의 실제 기믹을 먼저 진행한 뒤 패턴을 시작합니다. 씨앗 기본 설정은 25초부터 완전 성장체가 생깁니다. 대상이 0개면 호출도 0회입니다. 기본 씨앗 위치는 초기화 시 동일하게 재현되며 에셋에 저장하지 않습니다.", MessageType.None);
            if (patternMechanicPreview != null)
            {
                EditorGUILayout.LabelField(patternMechanicPreview.Status, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("현재 출력 개수 (진행 중 공격은 호출 당시 위치 유지)", EditorStyles.miniBoldLabel);
                var mechanics = encounter.phases[OutputPhase].mechanics;
                for (int i = 0; i < mechanics.Count; i++)
                {
                    var mechanic = mechanics[i];
                    if (mechanic == null || !mechanic.enabled || string.IsNullOrEmpty(mechanic.connectionId)) continue;
                    foreach (var port in mechanic.PositionOutputs)
                        EditorGUILayout.LabelField($"{i + 1}. {mechanic.name} / {port.Label}",
                            patternMechanicPreview.CaptureMechanicPositions(mechanic.connectionId, port.Key).Count + "곳");
                }
            }
        }
        private void BuildPatternMechanicPreview(EncounterPattern pattern)
        {
            if (!MechanicOutputValidation.UsesOutputs(pattern, encounter.FindPattern)) return;
            var state = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(previewLocationSeed);
                patternMechanicPreview = new BossMechanicSession(encounter.phases[OutputPhase].mechanics, new MechanicContext(previewHost, encounter.FindPattern));
            }
            finally { UnityEngine.Random.state = state; }
            previewHost.MechanicOutputs = patternMechanicPreview;
            previewHost.RequiredCellsProvider = () => patternMechanicPreview?.RequiredCells;
            patternMechanicPreview.Advance(outputPreviewWarmup);
        }
    }
}
#endif
