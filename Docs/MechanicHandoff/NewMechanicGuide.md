# 새로운 보스 기믹 개발 인수인계

기준일: 2026-09-20 · 프로젝트: `C:/GameMake/Unity6/LEE` · Unity: `6000.5.3f1`

이 문서는 **기존 수정 봉인과 다른 규칙을 가진 기믹을 새 Codex 작업에서 설계·구현하기 위한 가이드**다. 현재 지원되는 기능과 앞으로 구현해야 할 기능을 구분한다. 문서만으로 이전 대화나 미커밋 파일까지 전달되지는 않는다.

시작 프롬프트는 [FirstPrompt.md](FirstPrompt.md)에 있다. 문서 작성 자체는 새 작업 생성이나 새 기믹 구현을 의미하지 않는다. 아래 기획 예시는 아직 구현되지 않은 예시다.

## 1. 다른 작업으로 넘기기 전에

- 같은 프로젝트의 최신 작업 폴더를 연결한다. `ProjectSettings/ProjectVersion.txt`, `git status --short`, 대상 파일의 diff부터 확인한다.
- 현재 기믹 기반 구조에는 **아직 커밋하지 않은 수정과 신규 파일**이 있다. HEAD에서 만든 새 worktree에는 이 구조가 없을 수 있다. 새 작업에서 핵심 파일·에셋·`.meta`가 모두 있는지 확인하고, 누락이면 먼저 사용자에게 알린다. 코드가 없다고 구형 시스템을 새로 구현하지 않는다.
- 같은 폴더를 사용하면 최신 변경을 읽을 수 있지만 Unity 에디터나 다른 작업과 같은 파일을 동시에 저장하지 않도록 조율한다. 특히 `BossData_CrimsonGolem.asset` 전체 덮어쓰기를 피한다.
- 문서보다 현재 코드와 에셋을 우선한다. 설명과 구현이 다르면 차이를 먼저 보고한다. 기존 문서의 과거 전장 크기·보스 외형 미지정 설명을 현재 상태로 오해하지 않는다.
- 이전 작업의 변경을 되돌리거나 자동 커밋하지 않는다. 보스/패턴을 기본 생성기로 재생성하지 않는다.

## 2. 게임과 설계 방향

게임 이름은 **Trace Strike**다. 플레이어가 보스 공격을 피하면서 START에서 END까지 타일 경로를 완성하면 보스에게 피해를 준다. 공격 버튼으로 보스 몸체를 때리는 게임이 아니다.

- 상하좌우 타일 이동이며 대각선 이동은 없다. 바닥이 없는 칸과 장애물은 통과할 수 없다.
- 경로 작성 중 이미 밟은 경로를 다시 밟으면 기록이 초기화된다. 수정 봉인은 완성된 공격 경로에만 반응한다.
- 경로 길이 등에 따라 피해를 계산한다. 현재 보스 공격 피격은 플레이어 사망이며 다단계 플레이어 HP가 아니다.
- 페이즈 HP가 0이면 다음 페이즈, 마지막 페이즈라면 클리어다. 기믹은 이 피해/진행 규칙에 필요한 제한을 추가할 수 있다.
- 전장은 **Unity Tilemap 컴포넌트가 아니라 자체 논리 격자 + Canvas UI**다. 에디터가 타일맵 같은 칠하기를 제공한다. 전장 크기는 5~50이며 짝수도 허용한다.
- 보스 외형은 보스당 BossActor 프리팹 하나와 Animator 상태 전환을 사용한다. 보스 그림 자체는 이동을 막지 않으며, 중앙의 빈 바닥과 충돌 규칙은 별개다.

**패턴은 한 번의 시간축 공격이고, 기믹은 페이즈 동안 유지되는 상태와 게임 규칙이다.**

경고→타격→이펙트처럼 시간만으로 표현할 수 있으면 기존 패턴 이벤트 조합을 우선한다. 완료 개수, 작동 순서, 제한 시간, 보호 상태 같은 지속 상태가 필요하면 기믹 타입으로 구현한다. 기믹이 발생시키는 개별 공격은 기존 패턴 에디터에서 계속 편집할 수 있게 연결한다.

목표는 개발자가 규칙을 한 번 구현하면 비개발 팀원이 배치·시간·외형·공격을 에디터에서 수정할 수 있도록 하는 것이다. 모든 규칙을 무코드로 만드는 범용 노드 에디터로 확장하는 것이 이번 구조의 목표는 아니다.

## 3. 현재 구현을 읽는 순서

아래 링크는 저장소 안의 실제 파일이다. 필요한 확장 범위에 맞춰 본문과 호출부를 함께 읽는다.

| 파일 | 확인할 내용 |
|---|---|
| [BossMechanics.cs](../../Assets/Scripts/Patterns/BossMechanics.cs) | Definition / Runtime / Context / Session, 피해 처리 순서 |
| [CrystalSealMechanic.cs](../../Assets/Scripts/Patterns/CrystalSealMechanic.cs) | 기존 기믹의 설정, 장치별 상태, 공격 반복, 해제, 정리 구현 예 |
| [BossEncounterDefinition.cs](../../Assets/Scripts/Patterns/BossEncounterDefinition.cs) | 보스·전장·페이즈 데이터, `mechanics`, 전체 검증 |
| [TraceStrikeGame.Mechanics.cs](../../Assets/Scripts/TraceStrikeGame.Mechanics.cs) | 세션 생성/정리, 완성 경로 전달, 장치 실물 표시 |
| [TraceStrikeGame.Patterns.cs](../../Assets/Scripts/TraceStrikeGame.Patterns.cs) / [TraceStrikeGame.cs](../../Assets/Scripts/TraceStrikeGame.cs) | 게임 시간, 입력, 페이즈 진입, 사망/클리어, 실제 피해 파이프라인 |
| [PatternSequence.cs](../../Assets/Scripts/Patterns/PatternSequence.cs) / [PatternRunner.cs](../../Assets/Scripts/Patterns/PatternRunner.cs) | 이벤트 계약, 호스트, 원점, 자원 소유와 시간축 실행 |
| [PatternEvents.cs](../../Assets/Scripts/Patterns/PatternEvents.cs) / [TileSelection.cs](../../Assets/Scripts/Patterns/TileSelection.cs) | 재사용할 공격·표시·소환·신호 및 타일 선택 |
| [BossEncounterEditorWindow.Mechanics.cs](../../Assets/Editor/Patterns/BossEncounterEditorWindow.Mechanics.cs) | 기믹 추가 메뉴, 수정 전용 UI, 시험 경로와 기믹 프리뷰 |
| [BossEncounterEditorWindow.cs](../../Assets/Editor/Patterns/BossEncounterEditorWindow.cs) / [PatternPreviewHost.cs](../../Assets/Editor/Patterns/PatternPreviewHost.cs) | 에디터 선택/프리뷰 수명과 시뮬레이션 호스트 |
| [BossMechanicTests.cs](../../Assets/Tests/Editor/BossMechanicTests.cs) / [CrystalVisualTests.cs](../../Assets/Tests/Editor/CrystalVisualTests.cs) | 규칙·직렬화·Undo·실제 페이즈 전환 회귀 검사 |
| [BossData_CrimsonGolem.asset](../../Assets/Resources/Patterns/BossData_CrimsonGolem.asset) / [BossCatalog_Main.asset](../../Assets/Resources/Patterns/BossCatalog_Main.asset) | 현재 콘텐츠, 기믹 연결, 플레이 가능한 보스 목록 |

게임 배경은 [게임 설명](../PatternHandoff/GameOverview.md), 공격 제작은 [패턴 제작 가이드](../PatternHandoff/PatternAuthoringGuide.md), 기존 기믹의 사용법은 [수정 봉인 가이드](../BossMechanics.md)를 참고한다. 새 기믹의 사실 확인은 위 코드가 기준이다.

## 4. 기존 수정 봉인에서 보존할 규칙

현재 크림슨 골렘 전장은 35×35 Custom, 보스 위치는 (17,17), 플레이어 시작은 (19,2)다. 이 값은 편집 가능하므로 착수할 때 에셋을 다시 확인한다.

- 2페이즈 전환 연출 중 활성 수정 5개를 생성한다. 전투 재개 시 이미 배치되어 있다.
- 초기 배치는 (17,5), (28,13), (24,27), (10,27), (6,13): 보스에서 약 12칸 거리의 좌우 대칭 오각형이다. 런타임이 임의로 균등 배치하는 기능이 아니라 **에디터에 저장된 고정 좌표**다.
- 수정은 통과 가능하다. 단순히 밟거나 경로를 취소하면 해제되지 않는다.
- 완성된 경로 공격에 포함된 활성 수정을 모두 비활성화한다. 그 수정의 진행 중 공격도 취소하고 같은 위치에 비활성 외형을 남긴다.
- 활성 수정이 남아 있으면 보스 HP 하한은 1이다. **마지막 수정을 해제하는 공격에도 HP 1 보호가 남고, 다음 공격부터 처치할 수 있다.**
- 모두 해제했다고 자동으로 보스를 처치하거나 남은 HP를 1로 바꾸지 않는다.
- 공격 패턴은 `crystal-seal-attack`을 참조하며 공통 첫 대기/주기는 현재 5초/5초다. 장치별 패턴·시간 override가 가능하다.
- 일반 보스 공격은 별도로 계속 실행된다. 일반 패턴 교체로 수정의 해제 상태를 초기화하지 않는다.
- 구형 비활성 `Attack Crystal` 오브젝트와 캡처용 함수는 남아 있지만 정상 전투의 생성·재배치·공격에는 사용하지 않는다. 새 기믹에서 `SetupFixedCrystals`, `RelocateCrystals`, `CrystalPatternLoop`, `legacyCrystals` 경로를 되살리지 않는다.

새 기믹을 개발하기 위해 수정 봉인의 위 규칙이나 현재 맵·스폰·START/END 영역·보스 외형·기존 패턴을 임의로 바꾸지 않는다.

## 5. 실제 확장 계약

### 설정 데이터: BossMechanicDefinition

`BossPhaseDefinition.mechanics`는 `[SerializeReference] List<BossMechanicDefinition>`이다. 기믹 하나마다 별도 ScriptableObject를 만들어야 하는 구조가 아니라 **보스 에셋 안의 페이즈에 구체 타입이 직렬화**된다.

- 새 타입은 런타임 폴더에 `[Serializable]`인 비추상 클래스로 추가한다. 공개 매개변수 없는 생성자를 제공하고 `name`에 팀원이 알아볼 이름을 넣는다.
- 생성자는 기본값만 설정한다. 에디터가 메뉴를 열 때도 인스턴스를 생성하므로 오브젝트 생성·게임 상태 변경을 하지 않는다.
- `Create(MechanicContext)`는 해당 실행을 위한 새 Runtime을 반환한다.
- `Validate(BossEncounterDefinition, List<string>)`는 편집 가능한 값과 참조를 검사한다. 불완전한 새 설정에서도 예외 대신 이해 가능한 오류를 반환한다. 유효 바닥, 중복 위치, 존재/활성화된 공격 ID, NaN/Infinity, 음수 시간, 외형 요건 등을 검토한다.
- `PlacementCells`는 배치 위치를 노출한다. 현재 보스 검증은 같은 페이즈의 활성 기믹들 사이에서 이 위치가 겹치는 것을 오류로 처리한다. 영역 겹침이 필요한 새 규칙이면 이 의미를 먼저 검토한다.
- 개수·좌표·주기·외형·연결 공격 등은 데이터로 저장하고 실행 상태는 저장하지 않는다. 고정 개수 상수나 보스 이름/ID별 분기로 새 기믹을 구현하지 않는다.

### 실행 상태: BossMechanicRuntime

| 계약 | 현재 의미와 책임 |
|---|---|
| `Advance(float delta)` | 세션이 전달하는 게임 시간으로 진행. 타이머, 반복 공격, 상태 전이를 처리한다. |
| `OnPlayerAttack(IReadOnlyCollection<Vector2Int>)` | 완성된 경로 공격의 타일 집합을 받는다. 이동/클릭마다 호출되는 이벤트가 아니다. |
| `MinimumBossHealth` | 이 기믹이 요구하는 HP 하한. 기본값 0, 여러 기믹 중 최댓값이 적용된다. |
| `RequiredCells` | 활성 필수 장치 등을 타임라인 벽이 덮지 못하게 보호할 칸. 자동으로 벽을 만드는 속성이 아니다. |
| `Status` | 세션 상태 문자열에 합쳐지는 진행 표시. 게임 HP 라벨은 현재 모든 프레임 갱신되는 구조가 아니므로 실시간 카운트다운 등은 표시 갱신 경로도 검토한다. |
| `Dispose()` | 생성한 공격 실행기·장치·표시·벽·구독을 모두 정리. 반복 호출과 부분 생성 실패도 안전하게 처리한다. |

Runtime마다 진행도·타이머·활성 장치와 lease를 독립 보관한다. Definition이나 공유 공격 데이터에 실행 상태를 쓰지 않는다. `Advance(0)`과 큰 delta, 한 프레임의 여러 경계 통과, 중복 해제, 비활성/종료 상태를 다룬다. 무한 반복이나 0초 주기로 멈추지 않게 검증한다.

### 컨텍스트, 공격과 표시

`MechanicContext`에는 `IPatternHost Host`, `ResolvePattern(id)`, `ShowDevice(...)`가 있다. `ShowDevice`가 반환한 `IPatternLease`는 Runtime이 보관하고 종료 때 해제한다. 게임의 장치 표시에는 RectTransform 기반 UI 프리팹 또는 Sprite를 사용한다. 물리 충돌이나 상호작용이 프리팹 배치만으로 생기지는 않는다.

장치가 공격할 때는 기존 `EncounterPattern`과 `PatternRunner`를 재사용한다. 예를 들어 다음 형태로 **장치마다 독립 컨텍스트**를 만든다. 이 코드는 연결 방식의 예시이며 타이밍·검증·취소 코드는 별도로 필요하다.

```csharp
var pattern = context.ResolvePattern(attackPatternId);
var runner = new PatternRunner(pattern,
    new PatternContext(context.Host, deviceCell, 0, context.ResolvePattern));
runner.Advance(0);
// Runtime의 Advance에서 runner.Advance(delta)를 호출하고,
// 완료/교체/비활성화/Dispose에서 runner.Dispose()로 정리한다.
```

- 장치 중심 공격은 `TileAnchor.Origin`(에디터의 기믹 / 호출 위치)을 사용한다. `Center`는 전장 기준, `Player`는 플레이어 기준이므로 혼동하지 않는다.
- 같은 타격의 Warning/Damage/VFX는 영역과 `snapshotKey`를 맞추고, 서로 다른 타격은 독립 키를 쓴다. 장치별 컨텍스트는 동일 공격을 공유하면서 영역 스냅샷과 소환 키를 분리한다.
- 단발 `Damage`와 지속 `Hazard`를 구분한다. Damage의 duration만 늘려도 접촉 장판이 되지는 않는다.
- 패턴 이벤트가 만든 자원은 `PatternContext.Own`에 등록하고, 기믹이 직접 만든 자원은 Runtime이 소유한다. 한 패턴 종료가 기믹 전체를 지우거나 다른 장치의 효과를 제거하지 않도록 한다.
- 기믹마다 반복 주기의 의미와 공격 중첩 허용 여부를 정한다. 수정 봉인은 시작→다음 시작 주기이고 공격 길이보다 짧은 주기를 금지하지만 이것을 모든 새 기믹의 필수 규칙으로 가정하지 않는다.

## 6. 새 규칙에서 반드시 확인할 현재 한계

다음은 현재 자동 제공되지 않는다. 필요하면 보스 전용 임시 분기가 아니라 최소 범위의 공통 연결을 설계하고 게임·미리보기·테스트에 함께 반영한다.

1. **경로 내부의 순서:** 게임은 `model.Trail`을 `HashSet<Vector2Int>`로 복사해 기믹에 전달한다. 한 공격에서 A→B→C를 어떤 순서로 밟았는지 보장되지 않는다. 여러 번의 공격 순서는 Runtime 상태로 관리할 수 있지만, 한 경로 내부의 순서 판정은 순서 있는 경로 정보 전달 계약이 추가로 필요하다.
2. **타일 진입·이탈/버튼 입력:** Runtime에 전용 콜백이 없다. 현재 PlayerCell 관측만으로 충분한지, 모든 이동을 놓치지 않는 입력/이동 이벤트 연결이 필요한지 결정한다.
3. **보스 HP 임계치/강제 페이즈 전환/피해 배율:** 현재 MechanicContext는 보스 HP 조회나 이런 명령 API를 제공하지 않는다. 필요하면 명시적 계약을 설계한다. `Host.Damage(reason)`는 플레이어 피격 요청이지 보스 피해가 아니다.
4. **패턴에서 기믹 호출:** `SignalEvent`는 존재하지만 게임의 `PatternSignal` 이벤트를 발행할 뿐, 기믹 세션에 자동 전달하지 않는다. 미리보기는 현재 로그만 남긴다. 신호 기반 시작/완료가 필요하면 대상 식별·구독/해제·프리뷰 전달을 구현하고 검사한다.
5. **기믹 시작 조건:** 세션은 페이즈 진입 때 활성 Definition의 Runtime을 즉시 만든다. 특정 시간/상태까지 대기하는 것은 Runtime의 대기 상태 또는 새 연결 기능으로 구현해야 하며, 공통 시작 조건 선택기가 이미 있다고 가정하지 않는다.
6. **마지막 해제 공격의 피해:** `BossMechanicSession.ResolvePlayerAttack`은 모든 기믹의 HP 하한을 먼저 저장하고, 경로 콜백을 처리한 다음 피해를 계산한다. 새 기믹이 해제 공격으로 즉시 처치 가능해야 한다면 별도 정책 설계가 필요하다. 수정 봉인의 마지막 공격 보호를 전역 변경으로 깨뜨리지 않는다.
7. **맵 배치 UI:** 메뉴 검색과 기본 직렬화 필드 표시는 자동이지만 오른쪽 보드의 추가·선택·이동·삭제는 현재 `CrystalSealMechanic` 전용이다. 새 타입을 추가하는 것만으로 클릭 배치나 순서 화살표가 생기지 않는다. 필요한 전용 UI 또는 작은 편집 어댑터를 추가한다.
8. **미리보기 동등성:** 규칙 Runtime은 공유하지만 프리뷰의 장치/VFX는 표시용 마커이고 오디오·신호 등은 로그다. 벽의 실제 게임 연결성 검사와 사망 동작까지 완전히 같지 않다. 실전 검증을 대체하지 않는다.

보스 HP처럼 Context에 없는 정보를 알아내려고 새 Runtime에서 `TraceStrikeGame`의 private 필드를 reflection으로 읽거나 특정 씬 오브젝트를 검색하지 않는다. 필요한 의존성은 명시적으로 전달한다. 테스트에서 기존 private 호출부를 확인하기 위해 사용하는 reflection과 제품 코드를 구분한다.

## 7. 구현 전에 확정할 기획 양식

사용자가 자유롭게 설명하면 개발자가 아래 항목으로 정리한다. 미정 항목 중 결과를 바꾸는 부분만 묻고, 보스/적용 페이즈/필수 규칙을 임의로 결정하지 않는다.

```text
기믹 이름/한 줄 목적:
대상 보스와 페이즈: (아직 없으면 타입만 만들지, 새 보스도 만들지 결정)
시작 조건: 페이즈 즉시 / 일정 시간 / HP 조건 / 패턴 신호 등
플레이어가 해야 하는 행동: 밟기 / 완성 경로에 포함 / 순서대로 작동 등
장치 또는 영역: 개수, 위치, 통과/충돌 여부, 같은 칸 중복 여부
진행 상태: 대기 → 진행 → 성공/실패 후 어떤 상태인가?
여러 대상을 한 공격에 맞춘 경우: 동시 처리, 순서, 중복 처리 기준
시간 규칙: 제한 시간, 공격 주기, 중첩 여부, 실패 후 재시도 여부
주 보스 패턴과의 관계: 함께 진행 / 특정 구간 제한 등
보스 피해/처치 조건: 보호 여부, 해제하는 공격의 피해 처리
성공 결과 / 실패 결과:
실패·사망·재시작·페이즈 전환 때 초기화할 것:
필요한 외형·애니메이션·효과음 및 현재 준비된 리소스:
에디터에서 팀원이 수정할 값:
기존 콘텐츠 중 변경을 허용하는 범위:
```

설계 응답에서는 규칙, 상태 전이, 기존 지원/추가 연결 구분, 에디터 조작, 변경 파일 범위, 검증 계획을 설명한다. 예를 들어 ‘세 장치를 순서대로 작동’이라도 밟는 순서인지, 서로 다른 완성 공격의 순서인지, 한 완성 경로 내부의 순서인지 먼저 구분한다.

## 8. 권장 구현 순서와 에디터 완성도

1. 최신 코드와 미커밋 변경을 확인하고 기획을 정리한다. 설계 승인 또는 구체적인 구현 요청 범위에서 작업한다.
2. 새 Definition/Runtime과 최소 단위 검사를 추가한다. Namespace와 직렬화 타입을 안정적으로 유지하고 `.meta`도 생성·보존한다.
3. 필요한 게임 이벤트/명령 연결만 확장한다. 다른 기믹과의 동시 실행, 종료/구독 해제, 미리보기 대체 동작을 포함한다.
4. 개별 공격은 보스 소유 `libraryPatterns` 등의 패턴 ID를 연결한다. Runtime 안에 좌표와 경고/피해 타이밍을 고정 작성하지 않는다.
5. `Trace Strike → Patterns → Boss Encounter Editor`에서 대상 페이즈의 `+ 기믹 추가`로 찾을 수 있게 한다. 기본 메뉴는 TypeCache로 새 타입을 찾는다. 기본 PropertyField/PropertyDrawer 외에 필요한 지도 편집은 명시적으로 추가한다.
6. 사용자용 설정은 한국어 명칭, 초/칸 단위, 유효성 안내를 제공한다. 가능하면 패턴 선택 드롭다운과 기존 공격 편집 이동을 재사용한다. raw ID나 내부 상태를 비개발자에게 입력시키지 않는다.
7. 배치가 필요하면 유효 바닥 클릭, 선택/이동/삭제, 선택 표시, Undo/Redo, 저장 후 재로드를 지원한다. 단순히 좌표 리스트가 보이는 것만으로 타일 배치 UI가 완료됐다고 보고하지 않는다.
8. 기믹 미리보기에서 필요한 상호작용과 진행도/성공/실패를 확인할 수 있게 한다. 기존 ‘시험 경로’는 타일 집합이며 START/END 연결 검사가 없으므로 순서/이동 기믹의 검증 도구로 그대로 쓰지 않는다.
9. 승인된 보스·페이즈에만 연결한다. 대상이 미정이면 자동으로 크림슨 골렘에 붙이지 않는다. 새 보스를 실제 플레이 대상으로 만들 때는 BossCatalog 등록도 확인하되 시작 보스 변경은 따로 합의한다.
10. 단독 기믹과 정상 전투 모두 검증하고 사용법·현재 연결 콘텐츠·한계를 문서에 남긴다.

수정 봉인 UI를 통째로 복사해 여러 타입에 수정 전용 라벨과 로직을 남기지 않는다. 실제로 공유되는 조작만 작게 공통화하고 이번 기믹에 필요하지 않은 범용 시스템 재설계는 피한다.

## 9. 저장, 수명, 자원 안전성

- 실제 전투는 페이즈 진입 때 세션을 만들고, 주 패턴 교체에서는 유지한다. 사망·클리어·페이즈 전환·재시작·허브/튜토리얼 진입·컴포넌트 종료 때 정리한다. 해당 호출부를 새 연결에서도 검사한다.
- `Advance`는 게임의 scaled delta를 받으며 `inputLocked` 동안 타임라인과 함께 멈춘다. 별도 MonoBehaviour.Update나 코루틴 시계로 기믹만 진행하지 않는다. 정지 중에도 흘러야 하는 특수 시간은 먼저 정책을 정한다.
- `RequiredCells`는 현재 게임의 타임라인 벽 배치 제외에 사용된다. 전장 크기/바닥 변경이나 모든 프리뷰 충돌을 자동 방어하는 속성은 아니다.
- 에셋 편집에는 Undo, SetDirty/SerializedObject, 대상 저장과 재로드 검사를 사용한다. 기존 보스 ID, 패턴 ID, 프리팹 GUID를 유지한다.
- YAML 직접 편집이 불가피하면 기존 타입과 rid 연결을 보존하고 Unity 재로드를 검증한다. managed-reference rid는 64비트이므로 JavaScript Number/부동소수점으로 변환하지 않는다.
- 직렬화된 타입/필드 이름 변경에는 기존 에셋 호환/마이그레이션이 필요하다. 새 규칙 개발을 이유로 기존 타입을 이름만 바꿔 교체하지 않는다.
- 장치 비활성화·실패·성공·예외 중간에도 경고, 공격, 벽, 오디오, 소환물, 이벤트 구독이 남지 않게 한다. 반복 Dispose와 여러 기믹의 자원 소유 분리를 검사한다.

## 10. 검증과 인수 기준

### 반드시 확인할 동작

- 직렬화/Unity 재로드/Undo·Redo 후 구체 기믹 타입과 설정, 패턴 링크가 유지되는가?
- 잘못된 위치·중복·빈 필수 목록·누락 공격·비활성 공격·잘못된 시간에 명확한 오류가 나오는가?
- 시작 조건 전에는 시작하지 않고, 조건 충족 시 한 번만 시작하는가?
- 진행·성공·실패·재시도와 동시/중복 입력이 합의한 규칙대로 동작하는가?
- 게임 일시정지/입력 잠금, 큰 프레임 간격, 여러 기믹 동시 실행이 안전한가?
- 공격의 원점과 타일 편집 위치가 실제 장치 위치와 일치하는가?
- 보스 HP 보호, 해제 공격 처리, 다음 공격, 다음 페이즈/클리어가 맞는가?
- 실제 페이즈 진입, 사망·재시작·전환·프리뷰 종료 후 자원과 구독이 정리되는가?
- 기존 수정 봉인의 5개 배치, 통과, 경로 해제, 마지막 공격 보호와 다음 공격 처치가 보존되는가?
- 화면상 구분·경고 가독성·회피 가능성과 START/END 경로를 실제 게임에서도 확인했는가?

테스트는 가능하면 임시 보스 데이터를 구성해 규칙을 검사한다. 편집 가능한 실제 패턴의 표시 이름이나 임의 좌표를 불필요하게 고정 기대값으로 만들지 않는다. 에셋 연결 검사는 안정적인 ID와 현재 설정을 사용한다.

### 도구와 범위

프로젝트 루트에서 실행하는 기존 보조 검사:

```powershell
python Tools/ValidatePatternAssets.py
powershell -ExecutionPolicy Bypass -File Tools/VerifyPatternCompilation.ps1
powershell -ExecutionPolicy Bypass -File Tools/RunPatternHeadlessTests.ps1
```

- Python 명령은 사용 가능한 설치 경로로 바꿀 수 있다. 정적 검사는 **BossData_CrimsonGolem.asset의 참조/시간 등의 검사**이고 새 보스나 모든 기믹 규칙을 자동 검증하지 않는다.
- 컴파일 도구에는 설치된 Unity와 임포트된 Library/Bee 응답 파일이 필요하다. 헤드리스 도구보다 먼저 실행한다.
- 헤드리스 하네스는 현재 `PatternRunnerTests`, `ArenaConfigurationTests`의 일부 씬 독립 검사만 실행한다. 새 기믹 검사나 Unity Test Runner를 대신하지 않는다.
- Unity EditMode Test Runner에서 새 기믹 검사와 `BossMechanicTests`, `CrystalVisualTests`, 변경 범위에 따른 PatternRunner/PatternAsset/Arena/BossPresentation 검사를 실행한다.
- 일부 EditMode 검사는 Play Mode로 진입한다. UI/RenderTexture를 쓰는 검사는 그래픽 장치가 필요하므로 `-nographics` 실행으로 충분하다고 가정하지 않는다.
- 사용 중인 원본 Unity를 닫거나 같은 프로젝트로 두 번째 에디터를 띄우지 않는다. 필요하면 최신 Assets/Packages/ProjectSettings를 포함한 **새 격리 검증 사본**을 사용한다. 과거 임시 경로가 계속 존재한다고 가정하지 않는다.
- 배치 실행은 `-runTests -testPlatform EditMode -testFilter ... -testResults ... -logFile ...`를 사용한다. 그래픽/실행 권한을 확인한다. Unity 실행 명령 반환만으로 성공을 판정하지 말고 해당 실행의 XML 결과와 로그를 확인한다.
- 기믹 프리뷰는 선택한 기믹만 실행한다. 일반 패턴의 `게임에서 실행`은 기믹을 중지하므로, 전체 기믹 결합 검사는 정상 전투의 대상 페이즈에서도 수행한다.

### 기존 검증 기록 — 새 변경의 결과로 재사용 금지

- 2026-09-20 배치 조정 후 `BossMechanicTests` + `CrystalVisualTests` **23/23 통과**. 해당 로그가 남아 있으면 `Logs/PatternValidation/CrystalPentagonLayoutGraphics.xml`에서 확인한다.
- 그 이전 관련 9개 분류 실행은 113개 중 112개 통과였다. `AttackStepEditingTests.ExistingCrossIsRecognizedWithoutChangingData`는 실제 에셋에서 사용자가 바꾼 표시 이름 `P1_Cross_0`를 찾다가 실패했다. 이전 검증의 알려진 불일치이며 새 실패는 원인을 다시 확인해야 한다. 사용자 콘텐츠 이름을 되돌려 테스트를 통과시키지 않는다.
- 그래픽 비활성 검사에서 UI/RenderTexture 오류가 난 적이 있다. 그래픽 활성 재실행과 규칙 실패를 구분한다.
- 이 인수인계 문서 작성은 런타임 변경 작업이 아니다. 기존 기록이 새로운 기믹의 동작·수동 플레이·성능 검증을 보증하지 않는다.

### 완료 보고

추가한 기믹 타입, 적용 보스/페이즈, 확정 규칙, 팀원의 편집 방법, 수정 파일, 새로 확장한 공통 계약, 실제 검사 결과, 미검증 사항을 보고한다. 새 기믹의 사용/개발 문서를 추가하고 이 가이드 또는 [BossMechanics.md](../BossMechanics.md)에 연결한다. 기존 콘텐츠가 실제로 바뀌면 [현재 패턴 목록](../PatternHandoff/CurrentPatternCatalog.md)도 관련 부분만 갱신한다.
