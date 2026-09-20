#if UNITY_EDITOR
using NHN.TraceStrike.Effects;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    [CustomEditor(typeof(UiEffectPlayer))]
    public sealed class UiEffectPlayerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var effect = (UiEffectPlayer)target;
            EditorGUILayout.HelpBox("Impact VFX의 Prefab에 넣어 사용하는 UI 이펙트입니다. 대상 타일마다 하나씩 생성되며 피해 판정은 Damage 이벤트가 담당합니다.", MessageType.Info);
            EditorGUILayout.LabelField("전체 재생 권장 시간", effect.Duration.ToString("0.###") + "초 이상");
            EditorGUILayout.HelpBox("VFX 이벤트의 Duration이 짧으면 효과가 도중에 끊깁니다. 크기는 Size Multiplier로 조절하세요. 현재 패턴 미리보기는 영역만 표시하므로 실제 효과는 게임에서 실행하여 확인합니다.", MessageType.None);
            DrawDefaultInspector();
        }
    }
}
#endif
