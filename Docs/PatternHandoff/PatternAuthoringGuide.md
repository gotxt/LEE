# 패턴 제작 가이드 — 사람과 제작 에이전트 공용

기준일: 2026-09-19. 먼저 [게임 설명](GameOverview.md)과 [현재 패턴 목록](CurrentPatternCatalog.md)을 읽는다. 이 문서는 작업 절차와 실수 방지 기준이며, 에디터의 세부 기능은 기존 문서를 재사용한다.

2026-09-20 현황 갱신: 크림슨 골렘에는 사용자 제공 시트로 만든 BossActor와 Idle/FistWindup/FistContact Animator 상태가 등록되었다. 아래의 외형 미지정 설명은 최초 인수인계 시점의 기록이다. 프레임 애니메이션이며 고정 GroundImpact 소켓만 있으므로 움직이는 손 소켓을 가정하지 않는다. 새 주먹 패턴의 시간표/간편 UI 편집 주의는 현재 패턴 목록을 참조한다.

- [간편 공격 설계](../AttackDesigner.md)
- [전장·타임라인·이벤트 전체 설명](../PatternEditor.md)
- [보스 본체 연출](../BossPresentation.md)
- [타일 이펙트 프리팹](../EffectPrefabs.md)

## 1. 시작 전에 정할 것

최소한 대상 보스/페이즈, 추가인지 교체인지, 패턴의 의도와 개수를 확인한다. 사용자가 이미 정한 내용은 다시 묻지 않는다. 맵·스폰·기존 패턴·페이즈 순서 변경은 별도 범위로 다룬다.

설계안에는 다음을 기록한다.

| 항목 | 적을 내용 |
|---|---|
| 이름/ID | 팀원이 읽을 이름, 보스 내부에서 중복되지 않는 안정적인 ID |
| 목적 | 어떤 이동/경로 판단을 요구하는지 |
| 공격 순서 | 각 공격의 영역·경고 시작·경고 길이·타격 시점 |
| 회피 방법 | 실제 맵에서 어느 방향으로 몇 칸 이동해야 하는지 |
| 겹침 | 다른 공격·장판·벽·2페이즈 수정과 동시에 작동하는지 |
| 연출 | 타일 VFX/SFX, 보스 모션, 카메라. 없는 리소스는 미보유로 표시 |
| 종료 | 장판·장치·벽을 언제 정리하는지, 다음 공격까지 쉴 시간 |

한 패턴 한 함수, 보스 이름별 switch, 패턴 ID를 판별하는 코루틴을 추가하는 방식은 사용하지 않는다. 먼저 기존 이벤트 조합으로 만들고, 새 기능이 꼭 필요하면 재사용 가능한 이벤트 타입으로 설계를 설명한다.

## 2. 에디터로 제작하기

1. Unity에서 `Trace Strike > Patterns > Boss Encounter Editor`를 연다. 보스 에셋 더블클릭으로도 열린다.
2. 대상 보스와 페이즈를 확인한다. 기존 보스를 편집할 때 `New Encounter`나 기본 생성기로 대체하지 않는다.
3. 페이즈의 `+`로 패턴을 추가하고 구분 가능한 이름을 입력한다.
4. **간편 공격 설계 → + 다음 공격**을 누른다. 새 공격 영역은 비어 있으므로 반드시 칠한다.
5. 공격 이름·위치 기준·경고 시작·경고 길이·타격 구간을 정한다.
6. 맵을 클릭/드래그하여 영역을 칠한다. 우클릭 또는 Shift+드래그로 지운다.
7. 다음 공격은 `+ 다음 공격` 또는 `선택 공격 복제`, 동시 공격은 `동시 공격 복제`를 사용한다. 생성 후 원하는 시간을 직접 확인한다.
8. 지속 장판·벽·보스 연출·패턴 호출은 **고급 이벤트 편집**에서 추가한다.
9. 경고/타격 영역과 타이밍을 미리보기로 확인하고 상단 저장을 누른다.

간편 모드에서 한 공격의 영역을 바꾸면 그 공격에 연결된 Warning/Damage/VFX를 함께 수정하는 것이 정상이다. 서로 다른 공격끼리는 별도의 영역 키를 가져야 한다.

### 예시: 서로 다른 위치를 세 차례 공격

아래는 **제작 예시이며 현재 에셋에 추가된 패턴이 아니다**. A/B/C 영역은 실제 맵의 유효 바닥에 따로 칠한다.

| 공격 | 경고 시작 | 경고 길이 | 타격 구간 | 영역 연결 키 예시 |
|---|---:|---:|---|---|
| A: 왼쪽 | 0.0 | 1.0 | 1.0~1.3 | `attack_left` |
| B: 오른쪽 | 1.5 | 1.0 | 2.5~2.8 | `attack_right` |
| C: 안쪽 | 3.0 | 1.0 | 4.0~4.3 | `attack_inner` |

`minimumDuration=4.8`이면 마지막 타격 뒤 0.5초의 패턴 내부 여유가 있다. 페이즈의 패턴 간 대기는 여기에 추가된다. 동시 공격은 행의 시작 시간을 같게 맞춘다. `WaitEvent`를 놓아도 다른 이벤트가 자동으로 뒤로 밀리지는 않는다.

## 3. 위치와 모양을 정확하게 지정하기

| Anchor | 의미 |
|---|---|
| Center | 논리 격자 중심 타일 + offset |
| Player | 해당 이벤트가 시작할 때의 플레이어 타일 + offset |
| Origin | 이 패턴을 호출한 쪽이 전달한 원점 + offset |
| Absolute | (0,0) + offset. Cells 목록을 절대 좌표로 쓸 때 사용 |

최종 선택은 현재 전장 바닥과 교집합을 취한다. 이때 기준은 `Walkable`이며 임시 벽을 뺀 `Traversable`과 동일하지 않다. 벽/장치의 실제 배치와 이동 가능성은 게임 연결부에서도 확인한다.

중심 기준은 보스 외형 위치나 칠한 바닥의 중심이 아니다. `GridSize=max(17, arena.size)`, `Center=(GridSize/2, GridSize/2)`의 정수 나눗셈 결과다. 17 이하 맵은 기존 17칸 좌표 공간을 유지하며, 범위 시작은 `(GridSize-size)/2`다. 50×50이면 선택 기준은 (25,25), 시각적 정중앙은 (24.5,24.5)다.

현재 보스는 size=39라 Center=(19,19)지만, 바닥은 y=0~19에만 있다. 보스 배치 설정 (19,8)과도 다르다. 중앙 상대 공격을 만들 때 특히 주의한다.

| Shape | 정확한 의미 |
|---|---|
| Cells | 직접 칠한 상대/절대 좌표 목록 |
| All | 모든 바닥 |
| Cross | 원점을 지나는 가로줄 + 세로줄. radius로 길이를 제한하지 않음 |
| Diamond | `abs(dx)+abs(dy)==radius`인 테두리. 채워진 마름모가 아님 |
| Diagonal | `abs(dx)==abs(dy)`인 대각선. radius 제한 없음 |
| Combined | Cross + Diamond 테두리 |
| Horizontal / Vertical | 원점 기준 짝수 간격 행 / 열 전체. 한 줄 공격이 아님 |
| Rectangle | `abs(dx), abs(dy)<=radius`인 채워진 정사각형 |
| Checker | 반경 안의 체커. 중앙 칸은 제외 |

서로 다른 가로/세로 크기의 직사각형은 Cells로 칠한다. 계산 모양을 Cells로 변환하면 현재 좌표 결과가 저장되며, 이후 전장 크기에 맞춰 자동 확장되지 않는다.

### 영역 연결의 핵심: snapshotKey

- 한 번의 공격에 속한 Warning/Damage/VFX는 같은 키를 사용한다.
- 같은 패턴 실행에서 그 키를 **처음 계산한 영역**을 재사용한다. 보통 Warning이 먼저 계산한다.
- 두 번째 타격에서 다른 위치를 사용할 때는 다른 키를 만든다. 같은 키를 재사용하면 예전 경고 위치가 그대로 사용될 수 있다.
- 플레이어 조준도 Warning 시작 시 위치를 고정한다. 경고 중 계속 추적하는 공격이 아니다.
- 부모/호출된 자식 패턴은 스냅샷 저장 공간이 다르다. 자식은 `Origin`으로 호출 위치를 받는다.
- `ensureEscape`는 한 선택 집합이 플레이어 주변을 전부 막을 때 가능한 인접 칸을 제외하는 보조 장치다. 모든 공격을 합친 안전 경로나 충분한 반응 시간을 보장하지 않는다.

간편 모드가 공격으로 인식하려면 일반적으로 같은 키에 Warning 1개, Damage 1개가 있고 `Warning 끝=Damage 시작`이어야 한다. 같은 키의 여러 타격, 서로 떨어진 경고/타격, 섞인 특수 이벤트는 고급 편집에 남을 수 있다.

## 4. 이벤트 선택과 수명

| 하고 싶은 일 | 이벤트 / 주의 사항 |
|---|---|
| 위험 경고 | WarningEvent. 경고 자체에는 피해 없음 |
| 한 번 타격 | DamageEvent. duration은 escapeGrace 이상. 기본 grace=0.18 |
| 남아 있는 불바닥 | HazardEvent. 단발 Damage를 길게 늘리지 말 것 |
| 벽/장애물 | ObstacleEvent. 플레이어·START·END·기록된 경로를 피하고 연결성/생성 후보를 보호함 |
| 장치 표시 | SpawnEvent. 소환 자체가 장치 AI/피해/벽을 자동 제공하지 않음 |
| 장치/장판/벽 제거 | RemoveResourceEvent. 같은 실행에서 등록한 key의 리소스 제거 |
| 타일별 시각 효과 | VfxEvent. 타일마다 프리팹 한 개 생성, 피해는 별도 |
| 소리 | SfxEvent. AudioClip 필수, duration>0. 종료하면 소리도 중단 |
| 소환물 이동 | MoveObjectEvent. 같은 실행의 소환 key, 목적지는 전장 중심 상대 좌표 |
| 카메라 | CameraEvent. 겹친 효과는 합산되므로 강도를 확인 |
| 시간만 확보 | minimumDuration 또는 WaitEvent. 다른 병렬 이벤트를 멈추지 않음 |
| 보조 패턴 호출 | CallEncounterPatternEvent. 보스 내부 ID 사용, duration≥자식의 실제 Duration |
| 보스 간 공유 호출 | CallPatternEvent. 별도 PatternSequence 에셋 사용 |
| 사용자 장치 명령 | SignalEvent. 수신 코드가 있어야 동작, 문자열만으로 새 기능이 생기지 않음 |
| 보스 모션/애니메이션/부착 효과 | BossMotionEvent / BossAnimationEvent / BossVfxEvent |

Hazard/Obstacle/Spawn의 `persist=true`는 이벤트 구간 뒤에도 남기지만 **소유 패턴이 종료/취소되면 정리**된다. 다음 패턴까지 영구 유지되는 설정은 아니다. 긴 유지 기믹은 background 또는 충분히 긴 소유 패턴에서 다룬다. 동일 실행에서 살아 있는 Spawn key를 중복 사용하지 않는다.

같은 시각에는 종료 처리가 시작 처리보다 먼저이며, 동시 시작은 목록 순서를 따른다. 소환 후 이동처럼 의존 관계가 있으면 이 순서를 확인한다. 호출은 순환하면 안 되며 실행/검증의 호출 깊이 제한은 16이다.

## 5. 이펙트와 보스 연출

타일 이펙트 폴더: `Assets/Resources/Effects/Prefabs/Impact`.

| 프리팹 | 권장 VFX duration | 내용 |
|---|---:|---|
| VFX_TileImpact | 0.3초 이상 | 주황색 타일 섬광 |
| VFX_CrystalSparks | 0.12초 이상 | 불꽃 파편 4개 |
| VFX_DirtLaneEruption | 0.4초 이상 | 먼지와 흙 파편 16개 |
| VFX_DirtAreaExplosion | 0.5초 이상 | 충격파·먼지·파편 합계 47개 |

고급 편집에서 VFX 이벤트 선택 → `Selected Event → Element → Action → Prefab`에 지정한다. 이벤트 표시 이름이 Impact VFX인지보다 실제 타입이 VfxEvent인지가 중요하다. 프리팹이 있으면 이벤트 Sprite/Color 대신 프리팹 내부 설정을 쓴다.

이 네 프리팹은 **Canvas UI용**이다. BossVfxEvent의 보스 소켓용 SpriteRenderer/ParticleSystem 효과와 섞지 않는다. 크기는 타일에 자동 맞춤되며 `UiEffectPlayer.sizeMultiplier`로 추가 조정한다. 이벤트를 길게 해도 효과가 반복되지는 않고, 짧으면 재생 도중 제거된다. Damage와 VFX의 duration은 달라도 된다.

효과음은 `Assets/Resources/Effects/Audio/Warning.wav`, `Impact.wav`다. 넓은 영역의 모든 칸에 VFX_DirtAreaExplosion을 쓰면 타일 수×47개의 UI 입자를 만들 수 있으므로 성능/가독성을 확인한다.

보스 전용 이벤트에는 BossActor 프리팹이 필요하다. 현재 크림슨 골렘에는 미지정이므로 존재하지 않는 Charge/Slam 상태나 소켓을 가정하지 않는다. Animator와 실제 상태가 있는지 확인한 뒤 같은 보스 프리팹에서 상태를 전환한다. 자세한 수명/겹침/미리보기 규칙은 [보스 본체 연출](../BossPresentation.md)을 따른다.

## 6. 코드/에셋으로 작업하는 에이전트의 규칙

1. 먼저 `git status --short`와 대상 diff를 확인한다. 변경된 파일은 사용자의 작업일 수 있다. 같은 보스 에셋을 다른 채팅/Unity 에디터와 동시에 덮어쓰지 않는다.
2. 문서보다 최신 에셋/코드를 우선하고, 이 문서의 기준일/해시와 비교한다. 별도 worktree를 쓰면 미커밋 맵·패턴·이펙트가 포함되어 있는지 먼저 확인한다.
3. 가능하면 Unity 에디터 API로 기존 BossEncounterDefinition을 로드하고 요청한 패턴만 추가/수정한다. Undo.RecordObject, EditorUtility.SetDirty, AssetDatabase.SaveAssets를 적절히 사용한다. 관련 없는 에셋이 저장되지 않도록 범위를 확인한다.
4. 경고/타격/VFX는 TileSelection 값과 snapshotKey를 일치시킨다. 별개 공격은 독립 키와 데이터 복사본을 사용한다.
5. 간편 UI의 소리/카메라 연결에는 PatternClip의 `attackGroupKey`, `attackAtImpact`, Warning의 `attackName`이 사용된다. `Assets/Editor/Patterns/AttackStepEditing.cs`의 기존 Add/Bind/SynchronizeArea/Duplicate/SetTiming 로직을 참고한다. 이 유틸은 Editor 어셈블리 내부용이지 런타임 API가 아니다.
6. `[SerializeReference] action`의 구체 타입을 보존한다. Unity YAML을 직접 편집해야 한다면 rid 연결·타입·유일성을 확인하고 Unity 재로드 검증을 수행한다. rid는 **64비트 정수**일 수 있으므로 float/double/JavaScript Number로 변환하지 말고 문자열 또는 정확한 정수로 다룬다.
7. 기존 ID/GUID를 유지한다. 새 에셋은 고유 GUID를 사용한다. 파일을 옮길 때 `.meta`를 함께 이동한다. 보스 복제 시 별도 보스 ID, 보스 내부 패턴 ID 유일성 및 로컬 호출을 검증한다.
8. PatternLibraryBuilder는 기본 콘텐츠 생성기다. 현재 커스텀 보스를 지우거나 전체 재생성하는 도구로 사용하지 않는다.
9. 새 이벤트가 필요한 경우에만 `[Serializable] PatternEvent`/`BossEvent`를 확장한다. 설정과 실행 상태를 분리하고, 리소스를 PatternContext.Own에 등록하여 정상 종료·취소·페이즈 전환 때 정리한다. 필요시 런타임 호스트와 미리보기, 테스트를 함께 확장한다.

단순 패턴 추가를 위해 실행기나 에디터 전체를 개편하지 않는다. 현재 보스의 맵/스폰/기존 공격 순서/수정 호환 옵션을 승인 없이 바꾸지 않는다.

## 7. 검증과 완료 기준

### 에셋/로직 확인

- BossEncounterDefinition.ValidateDefinition() 및 PatternValidation.Errors에 오류가 없어야 한다.
- ID/호출/프리팹/오디오/Animator 상태가 실제로 존재하고 재로드 후에도 보존되어야 한다.
- 실제 패턴 길이는 `max(minimumDuration, 활성 이벤트의 start+duration)`이다. 호출 길이 검사에서도 이 값을 사용한다.
- 경고와 타격이 같은 곳인지, 다음 공격의 키가 독립인지, 소리/카메라 연결이 모호하지 않은지 확인한다.
- 새 보스는 BossCatalog에 등록해야 게임 실행/게임 프리뷰가 가능하다. startingBoss 변경은 별도 의도 확인 후 수행한다.

프로젝트 루트에서 실행할 기존 도구:

```powershell
python Tools/ValidatePatternAssets.py
powershell -ExecutionPolicy Bypass -File Tools/VerifyPatternCompilation.ps1
powershell -ExecutionPolicy Bypass -File Tools/RunPatternHeadlessTests.ps1
```

`python`은 설치된 Python 실행 경로로 대체할 수 있다. 컴파일 도구는 Unity 설치와 임포트된 Library 응답 파일이 필요하다. 헤드리스 검사는 독립 컴파일 뒤 실행하며 Unity 전체 테스트의 대체가 아니다. 특히 **현재 Python 정적 검사는 BossData_CrimsonGolem.asset에 한정**되고 Unity 직렬화/장면 렌더링/새 보스 전체를 검증하지 않는다.

Unity Test Runner의 EditMode 전체 검사를 사용한다. 관련 테스트는 PatternAssetTests, PatternRunnerTests, AttackStepEditingTests, ArenaConfigurationTests, ArenaRuntimeTests, BossPresentationTests, UiEffectPrefabTests다. 일부 검사는 Play Mode로 진입하거나 그래픽 장치가 필요하므로 `-nographics`만으로 충분하다고 가정하지 않는다. 사용자가 작업 중인 원본 에디터를 닫지 말고 필요하면 최신 Assets/Packages/ProjectSettings를 포함한 격리 사본을 사용한다.

### 플레이 확인

- 에디터 프리뷰: 타일 선택·경고·타격 순서 확인. 보스 외형 프리뷰는 별도 연출 시스템으로 지원한다.
- 일반 타일 VFX/오디오/카메라/장치 스크립트는 에디터의 타일 색칠만으로 확인 완료라 하지 않는다.
- Play Mode의 `Run in game`/`게임에서 실행`: 선택한 패턴을 한 번 실행한다. 전투를 재시작하고 기록 저장을 제외한다.
- 단일 패턴 프리뷰는 정상 페이즈 진행 전체와 같지 않다. 특히 2페이즈 수정과의 겹침은 일반 전투에서도 확인한다.
- 시작 위치, 모서리, 좁은 통로, START/END 주변, 여러 경고 겹침에서 회피할 수 있는지 확인한다. 플레이어는 키 입력 한 번당 한 칸이라는 점을 고려한다.
- 사망/재시작/페이즈 전환/프리뷰 중단 후 장판·벽·소환물·이펙트가 남지 않는지 확인한다.

완료 보고에는 추가/변경한 패턴 ID, 의도/피하는 법, 파일, 실제 수행한 검증과 미검증 사항을 적는다. [현재 패턴 목록](CurrentPatternCatalog.md)의 날짜·목록·타이밍·주의 사항·변경 기록도 갱신한다. 과거 127개 테스트 통과 기록을 새 변경의 테스트 결과로 재사용하지 않는다.
