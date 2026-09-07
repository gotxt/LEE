# 보스 인카운터·패턴 에디터

## 시작

Unity 6000.5.3f1에서 `Trace Strike > Patterns > Boss Encounter Editor`를 연다.
`Assets/Resources/Patterns`의 Boss Encounter Definition 에셋을 더블 클릭해도 열린다.
한 보스 에셋이 전장, 공용 보조 패턴, 페이즈, 페이즈별 공격 패턴과 배경 타임라인을 모두 소유한다.
기본 크림슨 골렘 인카운터와 BossCatalog가 포함되어 있으므로 별도 생성은 필요 없다.

1. 왼쪽 트리에서 Arena, Phase 또는 Pattern을 선택한다.
2. 페이즈 오른쪽 `+`로 공격 패턴을, HELPER PATTERNS의 `+`로 재사용할 보조 패턴을 추가한다.
3. `Add Event`에서 이벤트 종류를 선택한다.
4. 각 막대를 드래그해 시작 시간을 바꾸고 오른쪽 끝을 드래그해 지속 시간을 바꾼다.
5. 아래 상세 패널에서 패턴과 선택 이벤트의 값을 편집한다. 저장은 에셋에 즉시 반영되며 Undo/Redo를 지원한다.

다른 보스에서도 공유해야 하는 예외적인 패턴만 `Create > Trace Strike > Shared Pattern Sequence`로 만들고
`Trace Strike > Patterns > Shared Pattern Timeline Editor`에서 편집한다. 일반적인 보스 공격은 인카운터 안에 두는 것이 기본이다.

각 행은 독립 이벤트다. 막대가 겹치는 동안 동시에 실행된다. 순차 실행을 원하면 앞 이벤트의 끝에 다음 이벤트를 배치한다.
동일 시각에는 기존 이벤트가 먼저 종료되고 새 이벤트가 목록 순서대로 시작한다. 같은 키의 소환과 이동처럼 의존하는 이벤트는 이 순서를 따른다.
시간은 초 단위이고 Snap은 기본 0.05초다. Snap을 0으로 하면 자유롭게 이동한다.
`minimumDuration`은 이벤트가 없는 구간을 포함해 패턴의 최소 길이를 정한다. 실제 길이는 마지막 이벤트 끝과 이 값 중 큰 값이다.

## 타일 지정

Warning, Damage, Hazard, Obstacle, VFX의 `tiles`가 공통 선택 설정이다.

- `shape`: 직접 선택(Cells), 전체, 십자, 마름모, 대각선, 복합, 가로/세로 격자, 사각 영역, 체커.
- `anchor`: 전장 중심(Center), 이벤트 시작 시 플레이어(Player), 패턴 호출 원점(Origin), 절대 좌표(Absolute).
- `offset`: 원점으로부터의 타일 오프셋.
- `radius`: 마름모/사각/체커 등의 반경. 전장 크기와 독립된 타일 수다.
- `cells`: Cells 모드의 상대 좌표 목록. 미리보기 격자에서 왼쪽 클릭으로 칠하거나 지울 수 있다.
- `snapshotKey`: 같은 시퀀스 실행 안에서 처음 선택한 타일 집합을 재사용한다.
- `ensureEscape`: 이 선택이 플레이어의 인접 탈출 칸을 전부 막으면 가능한 인접 칸 하나를 제외한다. 다른 모든 공격과 합친 전역 안전 경로 보장은 아니다.

예: 플레이어 위치를 경고한 뒤 해당 위치에 폭발시키려면 Warning과 Damage의 `snapshotKey`를 모두 `aim`으로 설정한다.
Warning은 `anchor=Player`, start=0, duration=1. Damage는 같은 선택 설정에 start=1, duration=0.2, escapeGrace=0.18로 설정한다.
그 사이 플레이어가 이동해도 처음 경고한 위치가 폭발한다. 스냅샷은 다음 패턴 실행으로 넘어가지 않는다.
중첩 패턴도 별도 스냅샷 공간을 사용하며 `anchor=Origin`으로 호출자가 전달한 위치를 사용할 수 있다.

## 이벤트

| 이벤트 | 동작 |
| --- | --- |
| WarningEvent | 지정 타일의 경고가 지속 시간 동안 커진다. 종료 시 제거된다. |
| DamageEvent | 시작 시 플레이어 좌표를 저장하고 escapeGrace 후 한 번 피격을 검사한다. 발동 순간 밖에 있었던 플레이어의 늦은 진입은 피격되지 않는다. duration은 grace 이상이어야 한다. |
| HazardEvent | 지속 접촉 데미지 장판. 생성 순간 또는 장판 안에 머무르는 플레이어가 피격된다. |
| ObstacleEvent | 이동 불가 타일 생성. 플레이어·START·END·현재 경로는 제외하며 남은 전장을 끊는 배치는 거절하고 Console에 경고한다. |
| SpawnEvent | 키를 가진 프리팹/스프라이트 오브젝트 소환. 프리팹이 없으면 색상과 스프라이트로 UI 오브젝트를 만든다. |
| RemoveResourceEvent | 같은 시퀀스 실행의 key로 생성된 장판·벽·소환물 모두를 제거한다. |
| VfxEvent | 선택 타일 각각에 효과 프리팹/스프라이트를 생성하고 종료 시 제거한다. |
| SfxEvent | AudioClip을 재생한다. 양수 duration을 사용하며 이벤트 종료/취소 시 재생을 종료한다. |
| MoveObjectEvent | `$boss` 또는 같은 실행에서 소환한 key의 오브젝트를 목적지까지 보간한다. 목적지는 전장 중심 상대 타일 좌표다. |
| CameraEvent | 카메라 추적 위치에 화면 오프셋과 흔들림을 합산한다. 겹친 효과는 합산되고 각자 종료 시 제거된다. |
| WaitEvent | 지정 시간만 차지한다. 다른 병렬 이벤트를 정지시키지는 않는다. |
| CallEncounterPatternEvent | 같은 보스 인카운터 안의 패턴 ID를 호출한다. 보조 패턴 재사용에 쓰며 duration은 하위 패턴 전체 길이 이상이어야 한다. |
| CallPatternEvent | 별도 Shared Pattern Sequence 에셋을 호출한다. 여러 보스가 정말 같은 시퀀스를 공유할 때 사용한다. |
| SignalEvent | 게임의 PatternSignal 이벤트를 발행한다. 별도 컴포넌트의 장치 동작 등에 연결할 수 있다. |

플레이어는 현재 게임 규칙대로 체력 1이므로 양쪽 데미지 이벤트는 피격 시 사망한다.
현재 보스는 UI 표현이므로 MoveObjectEvent는 그 표시 오브젝트를 이동한다. 별도의 적 길찾기나 몸체 충돌 규칙을 추가하는 이벤트는 아니다.
소환물과 VFX는 게임의 ScreenSpaceCamera Canvas 전장에 생성된다. UI 프리팹이 가장 직접적으로 대응한다. ParticleSystem 등은 해당 카메라, 렌더러, 레이어와 정렬 설정에 맞게 제작한다.

## 수명과 중단

Hazard/Obstacle/Spawn의 `persist`가 꺼져 있으면 이벤트 종료 시 제거한다.
켜져 있으면 이벤트 막대가 끝나도 남아 있으며, RemoveResourceEvent 또는 그 시퀀스의 종료/취소 시 제거된다.
다른 시퀀스 실행의 키에는 영향을 주지 않는다. 동일 실행에 살아 있는 Spawn 키를 중복 사용하면 오류다.

MoveObjectEvent는 막대 끝에서 목적지를 유지하고 시퀀스 종료/취소 때 이전 위치로 복원한다.
같은 오브젝트에 여러 이동 이벤트가 겹치면 나중에 시작한 이동이 표시 우선권을 가진다.
패턴이 완료되면 모든 소유 리소스가 정리된다. 장치를 오래 유지하려면 긴 background 시퀀스 안에서 소환하고 제거 시간을 명시한다.
사망, 스테이지 재시작, 페이즈 전환, 컴포넌트 비활성화 및 게임 프리뷰 중단도 같은 정리 경로를 사용한다.

타임라인은 게임의 scaled time을 사용한다. 플레이어 공격 연출 등 inputLocked 상태에서는 타임라인 전체가 멈춘다.
이동 봉인만 걸린 상태에서는 보스 공격 시간이 계속 흐른다. 골렘의 기존 수정 코루틴은 호환 동작상 독립된 타이머를 유지한다.
프레임이 크게 지연되어도 시작/종료 경계를 순서대로 통과하므로 짧은 이벤트가 누락되지 않는다.
실제 충돌 위치는 화면 보간 위치가 아닌 TrailFieldModel의 논리 좌표다.

## 미리보기

- Play/Pause/Reset과 시간 슬라이더/눈금 클릭으로 타일 경고, 데미지, 장판, 벽을 확인한다.
- 격자 오른쪽 클릭으로 가상의 플레이어 위치를 지정한다. 시간을 되감으면 모델을 새로 만들고 해당 시각까지 다시 실행한다.
- 이 미리보기는 안전한 별도 모델이다. 실제 프리팹, 오디오, 오브젝트 이동, 카메라 효과와 Signal은 실행 기록으로 보여 준다. 게임 장면의 시각 렌더링과 같다고 간주하지 않는다.
- Unity Play Mode에서 `Run in game`은 현재 보스를 재시작하고 선택한 패턴을 한 번 실행한다. 다른 타임라인은 실행하지 않으며 기록 저장을 제외한다.
- `Stop in game`은 현재 스테이지를 다시 시작하여 정상 패턴 실행으로 돌린다. 이때 현재 전투 진행은 초기화된다.
- 미리보기의 벽 색칠은 의도된 배치를 보여 준다. 실제 게임 연결부의 예약 타일/연결성 보호에 의해 일부 배치가 제외되거나 거절될 수 있다.

## 보스 추가

1. Boss Encounter Editor의 `New Encounter` 또는 `Create > Trace Strike > Boss Encounter Definition`으로 에셋을 만든다.
2. 고유한 id, 표시 이름과 초상화를 설정한다.
3. Arena에서 전장 크기/모양, 카메라 배율, 플레이어 표시 크기를 설정한다. 지원 크기는 5~17의 홀수다.
4. `Add Phase`로 페이즈를 만들고 체력, 첫 대기, 순차/무작위 선택, 반복 간격과 가속을 설정한다.
5. 페이즈 오른쪽 `+`로 공격 패턴을 만든다. 페이즈마다 별도 전장 기믹이 필요하면 `Background timeline`을 추가한다.
6. 반복해서 쓰는 조각은 HELPER PATTERNS에 만들고 CallEncounterPatternEvent로 호출한다.
7. Resources/Patterns/BossCatalog의 bosses 목록에 추가한다. startingBoss 인덱스를 변경하면 그 보스로 일반 게임을 시작한다.

새 보스 추가만으로 기존 휴면 허브의 스테이지 선택 UI/잠금 정책이 확장되지는 않는다. 현재 진입 선택은 BossCatalog가 담당한다.
첫 페이즈 체력이 0이면 다음 페이즈로 전환하고 마지막 페이즈에서 0이 되면 클리어한다.
최고 기록은 보스 id별로 분리하며 기본 골렘은 기존 PlayerPrefs 기록 키를 유지한다.

기존 골렘의 주 공격 문양과 위치 지정 견제는 이제 `CrimsonGolem.asset` 하나에 인라인 패턴으로 저장해 실행한다.
2페이즈의 수정 4개/체력 절반 재배치 기능은 `legacyCrystals` 호환 옵션으로 남겼다. 이 부분의 기존 코루틴은 완전한 타임라인 이식 대상에서 제외했으며 새 보스의 일반 장치에는 Spawn/Obstacle/CallPattern/background를 사용한다.
기존 위치 지정 견제 코루틴은 문양 예고를 잠시 지연시켰지만 새 패턴은 작성한 절대 시간대로 병렬 발동한다. 전체 난이도가 이전과 프레임 단위로 동일하다는 의미는 아니다.
기본 타일 공격 효과음은 에셋으로 편집할 수 있도록 Warning/Impact WAV가 포함되어 있다.

## 이벤트 타입 확장

런타임 폴더에 `[Serializable]`인 `PatternEvent` 서브클래스를 추가하고 `Create`에서 매 실행마다 새로운 `PatternAction`을 반환한다.
공개 매개변수 없는 생성자를 유지한다. 에디터의 Add event 메뉴는 TypeCache로 발견하므로 메뉴나 중앙 switch를 수정하지 않는다.
설정 데이터는 이벤트에, 실행 중 상태는 action에 둔다. 같은 패턴 에셋을 동시에 실행해도 상태가 공유되어서는 안 된다.

```csharp
[System.Serializable]
public sealed class DeviceCommandEvent : NHN.TraceStrike.Patterns.PatternEvent
{
    public string command = "activate";
    public override NHN.TraceStrike.Patterns.PatternAction Create(
        NHN.TraceStrike.Patterns.PatternContext context, float duration)
        => new CommandAction(context, command);

    private sealed class CommandAction : NHN.TraceStrike.Patterns.PatternAction
    {
        private readonly NHN.TraceStrike.Patterns.PatternContext context;
        private readonly string command;
        public CommandAction(NHN.TraceStrike.Patterns.PatternContext context, string command)
        { this.context = context; this.command = command; }
        public override void Begin() => context.Host.Signal("device", command);
    }
}
```

Create는 부작용 없이 실행 객체만 만들고, 실제 효과는 Begin/Tick/End에서 수행한다.
자원을 얻을 때 context.Own(lease, key)로 등록하고 필요한 이벤트 종료 시 lease.Dispose()를 호출한다. Dispose는 여러 번 호출해도 안전해야 한다.
외부 효과는 IPatternHost를 통해 게임 연결부로 보낸다. 새로운 엔진 기능이 필요하면 해당 연결부와 프리뷰 호스트를 확장하거나 PatternSignal 구독 컴포넌트로 처리한다.
PatternAction.End(cancelled)는 자연 종료/취소를 구분한다. 종료 콜백 하나가 실패해도 나머지 소유 리소스의 정리를 시도한다.
실행기 최대 호출 깊이는 16이며 에셋 검증은 순환 호출과 부족한 하위 패턴 시간을 거절한다.

## 검증 상태와 재실행

- Unity 6000.5.3f1 자체 임포트와 런타임/에디터 스크립트 컴파일 통과.
- Unity EditMode 전체 77개 테스트 통과. 통합 보스 에셋 로드, 인라인 SerializeReference 타입 보존, 내부/공유 패턴 순환 호출 검사가 포함된다.
- 별도 Mono 장면 독립 테스트 15개 통과. 시간 경계, 동시 실행, 자원 정리, 데미지 유예, 내부 패턴 호출과 전장 크기/형태를 검사한다.
- 실제 PlayMode 화면·오디오·입력 감각과 보스 난이도는 에디터에서 별도로 플레이 확인해야 한다.

독립 컴파일: `Tools/VerifyPatternCompilation.ps1`
장면 독립 테스트: `Tools/RunPatternHeadlessTests.ps1`
통합 YAML 정적 검사: `Tools/ValidatePatternAssets.py`
기본 효과음 재생성: `Tools/GeneratePatternAudio.ps1` (Warning/Impact WAV를 다시 생성함)
검증 산출물은 Logs/PatternValidation에 저장된다.
