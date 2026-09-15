# 보스 본체 연출 제작

보스마다 `BossActor` 외형 프리팹 하나를 등록한다. 여러 공격 모션은 같은 프리팹의 Animator 상태로 전환하고, 이펙트만 별도 프리팹으로 사용한다. 이벤트는 기존 보스 패턴 타임라인에 함께 배치한다.

## 1. 외형 프리팹과 배치

1. 프로젝트에서 사용할 Sprite를 선택한 뒤 `Trace Strike > Patterns > Create Boss Actor Prefab`을 실행한다. 이미 만든 외형 프리팹에는 루트에 `BossActor`를 추가해도 된다.
2. `Body`의 SpriteRenderer에 보스 스프라이트를 지정한다. 프리팹 루트의 위치·회전은 0, 크기는 1을 권장한다.
3. 애니메이션을 사용할 경우 `Body`에 Animator와 Controller를 추가하고 루트 `BossActor.animator`에 연결한다. Animator는 Body와 그 자식만 움직이도록 만든다.
4. Boss Encounter Editor의 보스 설정 또는 `Arena > 보스 외형 / 배치`에서 Prefab을 등록한다. Position은 타일 좌표, Size는 프리팹 1유닛을 몇 타일로 표시할지 정하는 배율이다.
5. `보스 배치` 도구로 위치를 클릭하면 B 표시가 이동한다. 소수 좌표도 Position으로 지정할 수 있다. 예를 들어 50×50 정중앙은 (24.5, 24.5)다.
6. 보스와 겹치지 않을 만큼 바닥을 지운다. 보스 배치는 이동 가능한 타일 목록과 독립적이며 바닥을 자동 삭제하거나 충돌체를 생성하지 않는다. 플레이어가 돌아다닐 바닥은 상하좌우로 연결해야 한다.

Animator를 연결했다면 Animation Layer와 기본 대기 상태(Idle State)를 선택한다. 상태 목록에는 `Base Layer.Charge`, `Base Layer.Attacks.Slam`처럼 중첩 상태의 전체 경로가 표시된다. AnimatorOverrideController도 기본 Controller의 상태 목록을 사용한다. 이벤트로 직접 상태를 재생하므로 패턴별 전환 조건이나 트리거는 필요하지 않다. Controller에 만든 자동 전환은 여전히 작동하므로 타임라인 연출을 방해하지 않게 구성한다.

프리팹을 지정하지 않은 기존 보스는 종전 UI 외형을 사용한다. 보스 이벤트를 추가한 패턴은 보스 프리팹이 필요하다. 기존 `MoveObjectEvent`의 `$boss`는 새 프리팹 보스에 사용하지 않고 `BossMotionEvent`를 사용한다.

## 2. 보스 이벤트

타임라인의 이벤트 추가 메뉴에서 `Boss`를 선택한다.

| 이벤트 | 설정 | 종료 처리 |
|---|---|---|
| Boss Animation | Animator 상태, 전환 시간(초), 재생 속도 | 다음 애니메이션 이벤트가 전환하거나 소유 패턴이 종료/취소될 때 기본 대기 상태 복귀 |
| Boss Vfx | 효과 프리팹, 부착 지점, Follow, 상대 위치, 크기 | 이벤트 종료/취소 시 제거 |
| Boss Motion | 이동·회전·크기·흔들기 선택, 목표값, 변화 곡선, 복귀 시간 | 이벤트 종료/취소 시 기본 자세로 복귀 |

애니메이션 클립의 길이를 이벤트 길이에 억지로 맞추지 않는다. Speed로 속도를 조절하며, 애니메이션 이벤트의 구간이 끝나도 Animator 상태는 다음 상태 요청이나 소유 패턴 종료까지 계속된다. 루트 패턴 안에서 호출한 자식 패턴의 애니메이션은 자식 패턴 수명에 묶인다.

같은 보스의 애니메이션 요청이 겹치면 마지막에 시작한 요청이 우선한다. 이전 요청의 정리가 새 모션을 끊지 않으며, 새 요청 종료 후 이전 요청을 다시 재생하지 않는다. 기본 대기 상태로 복귀한다.

모션의 Translation은 배치 위치 기준 타일 단위, Rotation은 Z축 각도, Scale은 기본 크기에 대한 배율이다. 이동·회전·크기·흔들기는 각각 독립된 항목이다. 다른 항목은 동시에 적용되고, 같은 항목을 새 이벤트가 차지하면 이전 이벤트가 그 항목을 다시 덮어쓰지 않는다. 흔들기는 이동값에 더해진다.

Return Time은 전체 이벤트 길이에 포함된다. 예를 들어 Duration=1초, Return Time=0.3초면 0.7초 동안 목표 자세로 변한 뒤 0.3초 동안 복귀한다. 곡선은 0→1 진행에 적용한다. 0초 복귀는 이벤트 종료 때 즉시 복귀하며, 취소도 즉시 복귀한다. 뼈대/스프라이트 모션은 Animator, 전체 외형의 일시적인 이동은 바깥 MotionRoot에서 처리한다.

## 3. 이펙트 부착 지점

프리팹 안에 빈 Transform을 만들고 `BossActor > Sockets`에 이름과 Transform을 등록한다. 손과 입 등이 움직이면 해당 뼈대/Body 아래에 지점을 둔다. 이름은 프리팹 안에서 고유해야 한다.

- 지점 미선택: 보스 프리팹 루트를 기준으로 생성한다.
- Follow 켜짐: 지점의 위치·회전·크기를 따라간다.
- Follow 꺼짐: 생성 시점의 지점 월드 변환을 유지한 채 전장에 남는다.

Offset은 지점의 로컬 좌표이며 Scale은 효과 프리팹 배율이다. 효과의 지속 시간은 이벤트 Duration으로 설정한다. 자연스러운 소멸이 필요하면 파티클의 방출·수명이 Duration 안에 끝나도록 제작한다. 종료 시 남은 입자는 제거된다.

## 4. 패턴 예시

| 시간 | 이벤트 |
|---|---|
| 0.0초 | Boss Animation: Charge |
| 0.0~1.0초 | Boss Vfx: 오른손 충전 효과, Follow 켜짐 |
| 0.2~1.0초 | Warning: 주변 타일 경고 |
| 1.0초 | Boss Animation: Attack |
| 1.0~1.4초 | Boss Vfx: 타격 효과, Follow 꺼짐 |
| 1.0~1.3초 | Damage: 경고와 같은 타일 선택 |

SFX와 카메라 흔들기는 기존 이벤트를 사용한다. 보스 본체의 효과는 중앙에 바닥이 없어도 동작한다. 타일 경고와 데미지는 이동 가능한 전장 타일에 적용된다.

## 5. 미리보기와 렌더링

패턴을 선택하면 타일 미리보기 위에 실제 SpriteRenderer·Animator·ParticleSystem 보스 외형이 합성된다. 재생/일시 정지와 시간 슬라이더를 지원한다. 시간을 옮기거나 데이터를 바꾸면 별도 미리보기 인스턴스를 새로 만든 뒤 시작부터 1/60초 단위로 재생한다. 파티클 난수 시드는 고정되어 같은 시간의 결과를 비교하기 쉽다. 큰 시간으로 이동하면 다시 시뮬레이션하는 비용이 든다.

편집 미리보기는 커스텀 MonoBehaviour와 오디오를 비활성화하고 Animator의 AnimationEvent 호출을 막는다. 스크립트 기반 효과, Animator의 사용자 StateMachineBehaviour, 외부 입력/물리/실시간 의존 효과는 정확한 스크러빙 지원 대상이 아니다. 실제 게임에서는 보스 프리팹 스크립트가 실행되므로 `Run in Game`으로 함께 확인한다. 프리팹에 별도 게임 카메라·AudioListener를 넣지 않는다.

현재 UI 전장과 SpriteRenderer를 함께 표시하기 위해 보스와 전용 이펙트를 투명 RenderTexture에 그려 전장 UI에 합성한다. 런타임에서는 전장의 크기와 카메라 이동을 따르며, 에디터에서는 별도 Preview Scene을 사용한다. 보스 렌더 인스턴스는 내부적으로 31번 레이어를 사용하므로 이 레이어에 의존하는 사용자 스크립트는 피한다. 외형 소재는 URP의 스프라이트/파티클 Unlit 소재를 권장한다. 별도 장면의 조명에 의존하는 소재는 해당 렌더 공간에 조명이 필요하다.

## 확장 지점

`BossEvent`를 상속하면 이벤트 메뉴의 Boss 범주에 표시된다. 게임과 미리보기는 `IBossPatternHost`를 통해 동일한 `BossPresentation`으로 연결된다. 새 이벤트는 Begin에서 리소스를 얻고 `PatternContext.Own`에 등록하며, 종료/취소 시 자신의 리소스만 정리한다. 보스별로 실행기 코드를 복제하거나 패턴 ID에 따른 분기를 추가하지 않는다.

검증: Unity 6000.5.3f1에서 그래픽 장치를 사용하는 격리 프로젝트 전체 테스트 103개 통과. 결과는 `Logs/PatternValidation/BossPresentationFinal.xml`에 저장된다. 전용 검증은 `BossPresentationTests`와 `ArenaRuntimeTests.PrefabBossRunsPatternOnEmptyCentreAndCleansUpOnRestart`에서 확인할 수 있다.
