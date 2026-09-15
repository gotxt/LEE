#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NHN.TraceStrike.Patterns;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    public static class BossAnimatorOptions
    {
        public static string[] States(BossActor actor, int layer)
        {
            RuntimeAnimatorController controller = actor != null ? actor.Animator?.runtimeAnimatorController : null;
            while (controller is AnimatorOverrideController over) controller = over.runtimeAnimatorController;
            if (!(controller is AnimatorController graph) || layer < 0 || layer >= graph.layers.Length) return new string[0];
            var names = new List<string>();
            Walk(graph.layers[layer].stateMachine, graph.layers[layer].name + ".", names);
            return names.ToArray();
        }
        private static void Walk(AnimatorStateMachine machine, string prefix, List<string> names)
        {
            foreach (var state in machine.states) names.Add(prefix + state.state.name);
            foreach (var child in machine.stateMachines) Walk(child.stateMachine, prefix + child.stateMachine.name + ".", names);
        }
        public static List<string> Validate(BossEncounterDefinition boss)
        {
            var errors = new List<string>();
            var visual = boss.bossVisual;
            if (visual?.prefab == null) return errors;
            if (!PrefabUtility.IsPartOfPrefabAsset(visual.prefab)) errors.Add("보스 외형은 프로젝트의 프리팹 에셋으로 등록하세요.");
            var states = new HashSet<string>(States(visual.prefab, visual.animationLayer));
            if (visual.prefab.Animator != null && !states.Contains(visual.idleState)) errors.Add("보스 기본 대기 상태가 Animator에 없습니다.");
            var seen = new HashSet<IPatternTimeline>();
            foreach (var pattern in boss.AllPatterns()) ValidateTimeline(pattern, states, errors, seen);
            return errors;
        }
        private static void ValidateTimeline(IPatternTimeline timeline, HashSet<string> states, List<string> errors, HashSet<IPatternTimeline> seen)
        {
            if (timeline == null || !seen.Add(timeline)) return;
            foreach (var clip in timeline.Clips)
            {
                if (clip == null || !clip.enabled) continue;
                if (clip.action is BossAnimationEvent animation && !states.Contains(animation.state))
                    errors.Add(timeline.TimelineName + ": Animator 상태가 없습니다: " + animation.state);
                if (clip.action is CallPatternEvent call) ValidateTimeline(call.pattern, states, errors, seen);
            }
        }
        public static void Popup(Rect rect, SerializedProperty property, string label, string[] options, string empty)
        {
            var values = new List<string> { "" }; values.AddRange(options.Where(s => !string.IsNullOrEmpty(s)));
            var labels = new List<string> { empty }; labels.AddRange(values.Skip(1));
            if (!values.Contains(property.stringValue)) { values.Add(property.stringValue); labels.Add("없는 항목: " + property.stringValue); }
            int current = values.IndexOf(property.stringValue);
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(rect, label, current, labels.ToArray());
            if (EditorGUI.EndChangeCheck()) property.stringValue = values[selected];
        }
    }

    public abstract class BossFieldsDrawer : PropertyDrawer
    {
        protected abstract string[] Fields { get; }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight + 4;
            foreach (var name in Fields) height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(name), true) + 3;
            return height;
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(rect, label, EditorStyles.boldLabel);
            rect.y += rect.height + 4;
            var encounter = property.serializedObject.targetObject as BossEncounterDefinition;
            foreach (var name in Fields)
            {
                var child = property.FindPropertyRelative(name);
                rect.height = EditorGUI.GetPropertyHeight(child, true);
                if (name == "idleState")
                {
                    var actor = property.FindPropertyRelative("prefab").objectReferenceValue as BossActor;
                    int layer = property.FindPropertyRelative("animationLayer").intValue;
                    BossAnimatorOptions.Popup(rect, child, "기본 대기 상태", BossAnimatorOptions.States(actor, layer), "선택 안 함");
                }
                else if (name == "state" && encounter != null)
                    BossAnimatorOptions.Popup(rect, child, "애니메이션 상태", BossAnimatorOptions.States(encounter.bossVisual?.prefab,
                        encounter.bossVisual?.animationLayer ?? 0), "상태 선택");
                else if (name == "socket" && encounter != null)
                    BossAnimatorOptions.Popup(rect, child, "부착 지점", encounter.bossVisual?.prefab != null ?
                        encounter.bossVisual.prefab.sockets.Where(s => s != null).Select(s => s.name).ToArray() : new string[0], "보스 중심");
                else EditorGUI.PropertyField(rect, child, true);
                rect.y += rect.height + 3;
            }
            EditorGUI.EndProperty();
        }
    }
    [CustomPropertyDrawer(typeof(BossVisualDefinition))]
    public sealed class BossVisualDrawer : BossFieldsDrawer
    { protected override string[] Fields => new[] { "prefab", "position", "size", "animationLayer", "idleState" }; }
    [CustomPropertyDrawer(typeof(BossAnimationEvent))]
    public sealed class BossAnimationDrawer : BossFieldsDrawer
    { protected override string[] Fields => new[] { "state", "transition", "speed" }; }
    [CustomPropertyDrawer(typeof(BossVfxEvent))]
    public sealed class BossVfxDrawer : BossFieldsDrawer
    { protected override string[] Fields => new[] { "prefab", "socket", "follow", "offset", "scale" }; }

    public static class BossPrefabMenu
    {
        [MenuItem("Trace Strike/Patterns/Create Boss Actor Prefab")]
        public static void Create()
        {
            string path = EditorUtility.SaveFilePanelInProject("보스 프리팹 만들기", "BossActor", "prefab", "보스 외형 프리팹 저장 위치");
            if (string.IsNullOrEmpty(path)) return;
            var root = new GameObject("BossActor");
            try
            {
                var actor = root.AddComponent<BossActor>();
                var body = new GameObject("Body"); body.transform.SetParent(root.transform, false);
                body.AddComponent<SpriteRenderer>().sprite = Selection.activeObject as Sprite;
                var socket = new GameObject("BodyCenter"); socket.transform.SetParent(body.transform, false);
                actor.sockets.Add(new BossSocket { name = "Body", target = socket.transform });
                Selection.activeObject = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
