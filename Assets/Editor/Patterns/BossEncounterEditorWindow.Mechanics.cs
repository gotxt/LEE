#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NHN.TraceStrike.Patterns;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    public sealed partial class BossEncounterEditorWindow
    {
        private BossMechanicSession mechanicPreview;
        private string mechanicPreviewResult;
        private Vector2 mechanicScroll, mechanicMapScroll;
        private int selectedDevice = -1, mechanicTool, mechanicPreviewHealth = 150;
        private readonly HashSet<Vector2Int> mechanicTestTrail = new HashSet<Vector2Int>();
        private readonly TilePaintStroke mechanicTrailStroke = new TilePaintStroke();
        private bool previewOriginOverride;
        private Vector2Int mechanicAttackOrigin;
        private int originPhase = -1, originMechanic = -1;
        private Vector2Int PreviewOrigin => previewOriginOverride ? mechanicAttackOrigin : encounter.arena.CenterCell;
        private BossMechanicDefinition CurrentMechanic => encounter != null && phaseIndex >= 0 && phaseIndex < encounter.phases.Count &&
            patternIndex >= 0 && encounter.phases[phaseIndex].mechanics != null && patternIndex < encounter.phases[phaseIndex].mechanics.Count
                ? encounter.phases[phaseIndex].mechanics[patternIndex] : null;

        private void ShowMechanicMenu(int phase)
        {
            var menu = new GenericMenu();
            foreach (var type in TypeCache.GetTypesDerivedFrom<BossMechanicDefinition>().Where(t => !t.IsAbstract && t.IsSerializable))
            {
                Type captured = type;
                var sample = (BossMechanicDefinition)Activator.CreateInstance(type);
                menu.AddItem(new GUIContent(sample.name), false, () =>
                {
                    BeginPaint("Add phase mechanic");
                    var mechanics = encounter.phases[phase].mechanics ??= new List<BossMechanicDefinition>();
                    mechanics.Add((BossMechanicDefinition)Activator.CreateInstance(captured));
                    Changed(false); SelectNode(NodeKind.Mechanic, phase, mechanics.Count - 1);
                });
            }
            menu.ShowAsContext();
        }

        private void DrawMechanicEditor()
        {
            var mechanic = CurrentMechanic;
            if (mechanic == null) { EditorGUILayout.HelpBox("기믹을 선택하세요.", MessageType.Info); return; }
            EditorGUILayout.LabelField("페이즈 기믹 · " + mechanic.name, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("페이즈 시작 시 생성되며 일반 공격이 바뀌어도 유지됩니다. 기믹의 공격은 기존 패턴 에디터에서 설계합니다.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(340)))
                {
                    mechanicScroll = EditorGUILayout.BeginScrollView(mechanicScroll);
                    serialized.Update();
                    var property = serialized.FindProperty("phases").GetArrayElementAtIndex(phaseIndex)
                        .FindPropertyRelative("mechanics").GetArrayElementAtIndex(patternIndex);
                    EditorGUI.BeginChangeCheck();
                    if (mechanic is CrystalSealMechanic seal) DrawCrystalSettings(property, seal);
                    else EditorGUILayout.PropertyField(property, true); // New types can supply a PropertyDrawer.
                    if (EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedProperties(); Changed(); }
                    var errors = new List<string>(); mechanic.Validate(encounter, errors);
                    foreach (string error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
                    if (GUILayout.Button("이 기믹 삭제") && EditorUtility.DisplayDialog("기믹 삭제", "배치와 기믹 설정을 삭제할까요? 연결된 공격 패턴은 유지됩니다.", "삭제", "취소"))
                    {
                        BeginPaint("Delete phase mechanic"); encounter.phases[phaseIndex].mechanics.RemoveAt(patternIndex);
                        Changed(false); SelectNode(NodeKind.Phase, phaseIndex, -1); GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndScrollView();
                }
                using (new EditorGUILayout.VerticalScope()) DrawMechanicBoard(mechanic as CrystalSealMechanic);
            }
        }

        private void DrawCrystalSettings(SerializedProperty property, CrystalSealMechanic seal)
        {
            EditorGUILayout.PropertyField(property.FindPropertyRelative("name"), new GUIContent("기믹 이름"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("enabled"), new GUIContent("기믹 사용"));
            EditorGUILayout.LabelField("공통 공격", EditorStyles.boldLabel);
            PatternChoice(property.FindPropertyRelative("attackPatternId"), "공격 패턴", false);
            EditorGUILayout.PropertyField(property.FindPropertyRelative("initialDelay"), new GUIContent("활성화 후 첫 공격 대기 (초)"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("interval"), new GUIContent("반복 주기 (시작→시작)"));
            if (GUILayout.Button("+ 주변 공격 패턴 만들기"))
            {
                serialized.ApplyModifiedProperties(); BeginPaint("Create crystal attack");
                var pattern = CreateCrystalAttackPattern(); encounter.libraryPatterns.Add(pattern); seal.attackPatternId = pattern.id;
                Changed(); GUIUtility.ExitGUI();
            }
                if (GUILayout.Button("공통 공격 편집"))
            {
                serialized.ApplyModifiedProperties();
                OpenMechanicAttack(seal.attackPatternId, selectedDevice >= 0 && selectedDevice < seal.crystals.Count ? seal.crystals[selectedDevice].cell : encounter.arena.CenterCell);
            }
            EditorGUILayout.HelpBox("공격의 위치 기준은 ‘기믹 / 호출 위치’를 사용하세요. 첫 대기는 각 수정이 활성화된 순간부터 셉니다. 이미 활성인 수정을 다시 맞혀도 공격 시간을 초기화하지 않습니다. 반복 주기는 공격 전체 길이 이상으로 설정합니다.", MessageType.Info);
            EditorGUILayout.LabelField("외형 (UI 프리팹 또는 Sprite)", EditorStyles.boldLabel);
            foreach (var entry in new[] { ("activePrefab", "활성 프리팹"), ("activeSprite", "활성 이미지"),
                ("activeTint", "활성 색상"), ("inactivePrefab", "비활성 프리팹"), ("inactiveSprite", "비활성 이미지"), ("inactiveTint", "비활성 색상") })
                EditorGUILayout.PropertyField(property.FindPropertyRelative(entry.Item1), new GUIContent(entry.Item2));
            EditorGUILayout.HelpBox("처음에는 비활성 외형으로 표시하고, 경로 공격으로 활성화되면 활성 외형으로 바뀝니다. 비활성 외형을 비우면 활성 외형에 비활성 색상을 적용합니다. 프리팹이 이미지보다 우선합니다.", MessageType.None);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("수정 배치 · " + seal.crystals.Count + "개", EditorStyles.boldLabel);
            for (int i = 0; i < seal.crystals.Count; i++)
            {
                if (GUILayout.Toggle(selectedDevice == i, $"수정 {i + 1} · {seal.crystals[i].cell}", EditorStyles.miniButton)) selectedDevice = i;
            }
            if (selectedDevice >= 0 && selectedDevice < seal.crystals.Count)
            {
                var item = property.FindPropertyRelative("crystals").GetArrayElementAtIndex(selectedDevice);
                EditorGUILayout.PropertyField(item.FindPropertyRelative("cell"), new GUIContent("선택 수정 좌표"));
                PatternChoice(item.FindPropertyRelative("patternId"), "개별 공격", true);
                EditorGUILayout.PropertyField(item.FindPropertyRelative("overrideTiming"), new GUIContent("개별 시간 사용"));
                if (item.FindPropertyRelative("overrideTiming").boolValue)
                {
                    EditorGUILayout.PropertyField(item.FindPropertyRelative("initialDelay"), new GUIContent("활성화 후 첫 공격 대기 (초)"));
                    EditorGUILayout.PropertyField(item.FindPropertyRelative("interval"), new GUIContent("반복 주기 (초)"));
                }
                if (GUILayout.Button("이 수정의 공격 편집"))
                { serialized.ApplyModifiedProperties(); OpenMechanicAttack(seal.PatternId(seal.crystals[selectedDevice]), seal.crystals[selectedDevice].cell); }
                if (GUILayout.Button("이 수정만 별도 공격으로 복제"))
                {
                    serialized.ApplyModifiedProperties();
                    var source = encounter.FindPattern(seal.PatternId(seal.crystals[selectedDevice]));
                    if (source != null)
                    {
                        BeginPaint("Duplicate device attack"); var copy = ClonePattern(source);
                        copy.id = Guid.NewGuid().ToString("N"); copy.name += " · 수정 " + (selectedDevice + 1);
                        encounter.libraryPatterns.Add(copy); seal.crystals[selectedDevice].patternId = copy.id;
                        Changed(); GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.HelpBox("수정은 처음에 비활성이며 통과할 수 있습니다. 완성된 경로 공격에 포함된 수정이 활성화되어 반복 공격합니다. 모두 활성화된 뒤에도 수정 공격은 계속됩니다. 마지막 수정 활성화 공격에는 체력 1 보호가 남고 다음 공격부터 처치 가능합니다.", MessageType.None);
        }

        private void PatternChoice(SerializedProperty property, string label, bool inherit)
        {
            var patterns = encounter.AllPatterns().ToArray();
            var labels = new List<string> { inherit ? "공통 공격 사용" : "선택하세요" };
            labels.AddRange(patterns.Select(p => p.name + (p.enabled ? "" : " (꺼짐)")));
            int current = Array.FindIndex(patterns, p => p.id == property.stringValue) + 1;
            int choice = EditorGUILayout.Popup(label, current, labels.ToArray());
            if (choice != current) property.stringValue = choice == 0 ? "" : patterns[choice - 1].id;
            if (current == 0 && !string.IsNullOrEmpty(property.stringValue)) EditorGUILayout.HelpBox("연결한 패턴을 찾을 수 없습니다: " + property.stringValue, MessageType.Error);
        }

        internal static EncounterPattern CreateCrystalAttackPattern()
        {
            var pattern = NewPattern("수정 주변 공격");
            var step = AttackStepEditing.Add(pattern, 0);
            step.Warning.attackName = "주변 체크무늬";
            step.Tiles.anchor = TileAnchor.Origin; step.Tiles.shape = TileShape.Checker;
            step.Tiles.radius = 2; step.Tiles.ensureEscape = true;
            AttackStepEditing.SetTiming(step, 0, 0.7f, 0.3f); AttackStepEditing.SynchronizeArea(step);
            pattern.minimumDuration = 1;
            return pattern;
        }

        private void OpenMechanicAttack(string id, Vector2Int origin)
        {
            var pattern = encounter.FindPattern(id);
            if (pattern == null) return;
            int fromPhase = phaseIndex, fromMechanic = patternIndex;
            int library = encounter.libraryPatterns.IndexOf(pattern);
            if (library >= 0) SelectNode(NodeKind.LibraryPattern, -1, library);
            else
            {
                for (int p = 0; p < encounter.phases.Count; p++)
                {
                    int index = encounter.phases[p].patterns.IndexOf(pattern);
                    if (index >= 0) { SelectNode(NodeKind.Pattern, p, index); break; }
                    if (encounter.phases[p].background == pattern) { SelectNode(NodeKind.Background, p, -1); break; }
                }
            }
            originPhase = fromPhase; originMechanic = fromMechanic;
            mechanicAttackOrigin = origin; previewOriginOverride = true; selectedClip = 0; RebuildPreview(); GUIUtility.ExitGUI();
        }

        private void DrawMechanicBoard(CrystalSealMechanic seal)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(mechanicPreview == null))
                    if (GUILayout.Button(playing ? "일시정지" : "기믹 재생")) { playing = !playing; lastPreviewTime = EditorApplication.timeSinceStartup; }
                if (GUILayout.Button("미리보기 초기화")) { playing = false; RebuildPreview(); }
                GUILayout.Label(playhead.ToString("0.00") + "초");
            }
            mechanicTool = GUILayout.Toolbar(mechanicTool, new[] { "선택/이동", "수정 추가", "삭제", "플레이어", "시험 경로" });
            EditorGUILayout.LabelField("선택/이동: 수정 클릭 후 빈 바닥 클릭 · 추가/삭제: 타일 클릭 · 시험 경로: 드래그 (우클릭 지우기)", EditorStyles.wordWrappedMiniLabel);
            mechanicMapScroll = EditorGUILayout.BeginScrollView(mechanicMapScroll);
            float edge = Mathf.Max(330, encounter.arena.GridSize * 16);
            Rect board = GUILayoutUtility.GetRect(edge, edge, GUILayout.ExpandWidth(false));
            var floor = encounter.arena.GetCells(); var sprites = encounter.arena.BuildTileSpriteLookup();
            var marks = previewHost?.GetOrderedMarks();
            for (int y = 0; y < encounter.arena.GridSize; y++) for (int x = 0; x < encounter.arena.GridSize; x++)
            {
                var cell = new Vector2Int(x, y); Rect rect = PatternPreviewGridGUI.CellRect(board, x, y, encounter.arena.GridSize);
                EditorGUI.DrawRect(rect, floor.Contains(cell) ? new Color(.27f, .3f, .35f) : new Color(.12f, .13f, .15f));
                if (floor.Contains(cell) && PatternPreviewGridGUI.DrawTileSprite(rect, encounter.arena.ResolveTileSprite(cell, sprites))) Repaint();
                if (marks != null) foreach (var mark in marks) if (mark.Cells.Contains(cell)) EditorGUI.DrawRect(rect, mark.Color);
                if (seal != null)
                {
                    int index = seal.crystals.FindIndex(d => d.cell == cell);
                    if (index >= 0)
                    {
                        GUI.Label(rect, (index + 1).ToString(), EditorStyles.whiteMiniLabel);
                        if (index == selectedDevice) PatternPreviewGridGUI.DrawSelectionOutline(rect);
                    }
                }
                if (mechanicTestTrail.Contains(cell)) PatternPreviewGridGUI.DrawCenterHighlight(rect);
                if (cell == previewPlayer) GUI.Label(rect, "P", EditorStyles.whiteMiniLabel);
                Event e = Event.current;
                if (e.type != EventType.MouseDown || !rect.Contains(e.mousePosition) || mechanicTool == 4) continue;
                if (mechanicTool == 3 && floor.Contains(cell)) { previewPlayer = cell; if (previewHost != null) previewHost.player = cell; e.Use(); }
                else if (seal != null && (e.button == 0 || e.button == 1))
                { EditCrystalCell(seal, cell, e.button == 1 ? 2 : mechanicTool); e.Use(); }
            }
            PatternPreviewGridGUI.DrawLines(board, encounter.arena.GridSize);
            if (bossPreview != null && Event.current.type == EventType.Repaint)
            { bossPreview.Render(); GUI.DrawTexture(board, bossPreview.Texture, ScaleMode.StretchToFill, true); }
            if (mechanicTool == 4)
                mechanicTrailStroke.Handle(board, encounter.arena.GridSize, false, () => { playing = false; },
                    (cell, erase) => { if (erase) mechanicTestTrail.Remove(cell); else if (floor.Contains(cell)) mechanicTestTrail.Add(cell); }, Repaint, Repaint);
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(previewError)) EditorGUILayout.HelpBox(previewError, MessageType.Error);
            if (!string.IsNullOrEmpty(mechanicPreviewResult)) EditorGUILayout.HelpBox(mechanicPreviewResult, MessageType.Info);
            EditorGUILayout.LabelField(mechanicPreview?.Status ?? (mechanicPreviewHealth == 0 ? "미리보기 종료" : "설정을 확인하세요."));
            mechanicPreviewHealth = Mathf.Max(0, EditorGUILayout.IntField("시험 보스 체력", mechanicPreviewHealth));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("시험 경로로 공격 (큰 피해)")) ApplyMechanicPreviewAttack();
                if (GUILayout.Button("시험 경로 지우기")) mechanicTestTrail.Clear();
            }
            EditorGUILayout.HelpBox("선택 기믹만 시뮬레이션합니다. 처음에는 수정 공격이 없습니다. 시험 경로로 수정을 활성화한 뒤 재생하여 공격을 확인하세요. 시험 경로는 활성화·체력 보호 검증용이며 START/END 연결은 검사하지 않습니다. 초기화하면 모든 수정이 비활성으로 돌아갑니다.", MessageType.None);
        }

        private void EditCrystalCell(CrystalSealMechanic seal, Vector2Int cell, int tool)
        {
            int index = seal.crystals.FindIndex(d => d.cell == cell);
            if (tool == 0 && index >= 0) { selectedDevice = index; return; }
            if (tool != 2 && !encounter.arena.GetCells().Contains(cell)) return;
            if (tool == 2 && index >= 0)
            { BeginPaint("Delete crystal"); seal.crystals.RemoveAt(index); selectedDevice = -1; }
            else if (tool == 1 && index < 0)
            { BeginPaint("Place crystal"); seal.crystals.Add(new CrystalPlacement { cell = cell }); selectedDevice = seal.crystals.Count - 1; }
            else if (tool == 0 && index < 0 && selectedDevice >= 0 && selectedDevice < seal.crystals.Count)
            { BeginPaint("Move crystal"); seal.crystals[selectedDevice].cell = cell; }
            else return;
            Changed();
        }

        private void RebuildMechanicPreview()
        {
            playing = false; playhead = 0; previewError = null; mechanicPreviewResult = null; mechanicTestTrail.Clear();
            if (CurrentMechanic == null) return;
            try
            {
                mechanicPreviewHealth = encounter.phases[phaseIndex].health;
                var errors = new List<string>(); CurrentMechanic.Validate(encounter, errors);
                if (errors.Count > 0) { previewError = string.Join("\n", errors); return; }
                previewHost = new PatternPreviewHost(encounter.arena)
                { player = previewPlayer, RequiredCellsProvider = () => mechanicPreview?.RequiredCells };
                if (encounter.bossVisual?.prefab != null)
                { bossPreview = new BossRenderStage(encounter.bossVisual, encounter.arena.GridSize, true); previewHost.boss = bossPreview.Presentation; }
                mechanicPreview = new BossMechanicSession(new[] { CurrentMechanic }, new MechanicContext(previewHost, encounter.FindPattern));
                mechanicPreview.Advance(0);
            }
            catch (Exception error) { FailPreview(error); }
        }
        private void UpdateMechanicPreview()
        {
            if (!playing || mechanicPreview == null) return;
            double now = EditorApplication.timeSinceStartup; float delta = (float)(now - lastPreviewTime); lastPreviewTime = now;
            try { bossPreview?.Presentation.Advance(delta); mechanicPreview.Advance(delta); playhead += delta; }
            catch (Exception error) { FailPreview(error); }
            Repaint();
        }
        private void ApplyMechanicPreviewAttack()
        {
            if (mechanicPreview == null || mechanicTestTrail.Count == 0) return;
            try
            {
                mechanicPreviewHealth = mechanicPreview.ResolvePlayerAttack(mechanicTestTrail, mechanicPreviewHealth, 99999);
                if (mechanicPreviewHealth == 0)
                {
                    playing = false; DisposePreview();
                    mechanicPreviewResult = "처치 가능: 보호 조건을 완료한 다음 공격입니다. 미리보기를 종료했습니다.";
                }
                else
                {
                    mechanicPreviewResult = "체력 " + mechanicPreviewHealth + " · " + (mechanicPreview.MinimumBossHealth > 0
                        ? "기믹의 체력 보호가 남아 있습니다." : "보호 조건을 완료했습니다. 다음 공격부터 처치 가능합니다.");
                }
            }
            catch (Exception error) { FailPreview(error); }
            Repaint();
        }
    }
}
#endif
