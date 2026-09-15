#if UNITY_EDITOR
using NHN.TraceStrike.Patterns;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    [CustomEditor(typeof(BossEncounterDefinition))]
    public sealed class BossEncounterDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var boss = (BossEncounterDefinition)target;
            foreach (var error in boss.ValidateDefinition()) EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (var error in BossAnimatorOptions.Validate(boss)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.HelpBox("Double-click this asset to edit the complete encounter in Boss Encounter Editor.", MessageType.Info);
        }
    }
}
#endif
