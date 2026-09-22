# 현재 제작된 패턴 목록

2026-09-22: [에셋 파일명 정리](../AssetNaming.md)를 적용했다. 아래 파일 경로는 새 이름으로 갱신했지만 과거 검증 수치·해시는 각 기록 시점의 값이다. 이번 이름 변경으로 맵·패턴·기믹 설정은 바꾸지 않았다.

기준일: **2026-09-20**. 이 문서는 실제 저장된 에셋을 읽은 스냅샷이며 자동으로 갱신되지 않는다. 다음 제작 작업 완료 시 변경 기록과 목록을 함께 갱신한다.

**수정 봉인 이식 시점 안내:** 기존 맵/공격에 후속 사용자 편집이 있어 아래 과거 맵 크기·좌표·공격 수량은 현재 에셋과 다를 수 있다. 이번 작업은 그 편집을 보존하고 2페이즈 기믹과 보조 공격만 추가했다. 최신 기믹은 6절 및 [보스 기믹 가이드](../BossMechanics.md)를 기준으로 한다.

- 프로젝트: `C:/GameMake/Unity6/LEE`
- 원본: `Assets/Resources/Patterns/BossData_CrimsonGolem.asset`
- 카탈로그: `Assets/Resources/Patterns/BossCatalog_Main.asset`
- 확인 당시 HEAD: `091d4c0`. **미커밋 사용자 편집과 이전 이펙트 작업을 포함한 작업 폴더**를 기준으로 했으므로 HEAD 내용과 같지 않다.
- 확인 당시 BossData_CrimsonGolem.asset SHA-256: `FB901919CA9992D5B22581F8CDE9D14242228C4B3796CA2ECC269571B2C49482`
- 기초 설명: [게임 설명](GameOverview.md), 변경 절차: [패턴 제작 가이드](PatternAuthoringGuide.md).

## 1. 보스·전장 현황

| 항목 | 현재 저장값 |
|---|---|
| 보스 | 크림슨 골렘 / `crimson-golem` |
| 등록 상태 | BossCatalog의 유일한 보스. startingBoss=0 |
| 전장 | size=39, Custom. 편집 영역은 39×39지만 전부 바닥은 아님 |
| 바닥 | 유효 바닥 747칸. 좌표 범위 x=0~38, y=0~19. y=20~38은 바닥 없음 |
| 패턴 Center | (19,19). 칠한 바닥의 시각적 중심으로 자동 보정되지 않음 |
| 플레이어 스폰 | 고정 켜짐, (19,2). 유효 바닥 위 |
| START / END | 생성 영역 제한 모두 켜짐. 원시 목록 각각 490칸, 실제 바닥과 교차하면 각각 481칸. 두 목록은 같은 집합. 생성되는 두 타일은 서로 다름 |
| 표시 배율 | cameraZoom=2.019 / playerSizeRatio=0.78 |
| 타일 이미지 | 기본 Sprite 미지정, 팔레트와 개별 이미지 목록 비어 있음. 기존 게임 타일 사용 |
| 보스 외형 | `Assets/Art/Bosses/CrimsonGolem/BossVisual_CrimsonGolem.prefab`, portrait 미지정. 사용자 제공 12프레임 시트 사용 |
| 외형 배치 설정 | (19,8), size=1, animationLayer=0, idleState=`Base Layer.Idle` |
| 중앙 비우기 | x=18~20, y=8~10의 3×3 영역은 바닥에서 제거되어 있다. 보스 원점 (19,8)도 빈 곳. 논리 Center=(19,19)는 바닥 위 |

모든 좌표는 논리 타일 좌표다. 현재 맵을 17×17 Rounded 기본값으로 되돌리거나, 보스 외형 좌표를 모든 Center 패턴의 기준으로 대체하지 않는다.

2026-09-20 낙석 추가 시 재확인한 사용자 편집: 이전 목록 이후 바닥 9칸 제거, 스폰 (19,2), 기존 P1 패턴 5개 비활성, Cross 첫 공격의 원시 영역 동기화가 저장되어 있었다. Unity의 미저장 보스 위치 (19,8)는 사용자에게 유지 승인을 받은 뒤 저장된 값 그대로 보존했다. 이 변경들을 낙석 제작이 임의로 되돌리지 않았다.

## 2. 페이즈와 실행 순서

| 페이즈 | 체력 | 첫 대기 | 기본 반복 대기 | 완료당 감소 | 최저 대기 | 선택 | background | legacyCrystals |
|---|---:|---:|---:|---:|---:|---|---|---|
| PHASE 1 | 150 | 2.4초 | 1.85초 | 0.1초 | 0.45초 | 저장 순서 순환 | 꺼짐 | 꺼짐 |
| PHASE 2 | 150 | 0.5초 | 1.05초 | 0.1초 | 0.45초 | 저장 순서 순환 | 꺼짐 | 꺼짐 (수정 봉인 기믹으로 대체) |

패턴 완료 후 대기는 `max(최저 대기, 기본 반복 대기 - 완료 수×0.1)`이다. 따라서 첫 패턴 뒤 대기는 P1=1.75초, P2=0.95초다. 패턴 자체 길이는 별도로 더해진다.

주 패턴 19개(7+12)와 보조 패턴 2개로 **저장된 인라인 타임라인 21개**, 직접 이벤트/managed reference는 **170개**다. P1의 기존 Cross/Diagonal/Diamond 5개 패턴은 꺼져 있으며 Fist Ripple과 Alternating Rockfall만 켜져 있다. P2 12개와 보조 2개, 직접 이벤트 170개는 enabled가 켜져 있다. 두 페이즈의 비어 있는 Background는 `backgroundEnabled=0`이므로 집계에 포함하지 않았다. 기존 상대 순서와 활성 상태를 보존하고 새 패턴을 P1 마지막에 추가했다. 현재 정상 P1 순환은 Fist Ripple → Alternating Rockfall이다.

## 3. 1페이즈 주 패턴

아래 순서가 실제 저장 순서다. 1~5번은 비활성이고 6~7번만 일반 P1 전투에서 실행된다. `P1_Cross_1`은 현재 에셋에 없다. 기본 생성기에 있다는 이유로 복원하지 않는다.

| 순서 | 표시 이름 / ID | 실제 길이 | 직접 이벤트 수 | 주 경고 타일 수* | 내용 |
|---|---|---:|---:|---|---|
| 1 | `P1_Cross_0` / `p1-cross-0` | 6.4초 | 14 | 49 / 42 / 45 | 직접 칠한 3연타. 첫 두 공격 Center, 세 번째 Absolute |
| 2 | `P1_Diagonal_0` / `p1-diagonal-0` | 2.3초 | 6 | 35 | Center 대각선 |
| 3 | `P1_Diagonal_1` / `p1-diagonal-1` | 2.3초 | 6 | 35 | Center 대각선. 0번과 같은 구성 |
| 4 | `P1_Diamond_0` / `p1-diamond-0` | 2.3초 | 6 | 11 | Center 마름모 테두리 r=5 |
| 5 | `P1_Diamond_1` / `p1-diamond-1` | 2.3초 | 6 | 17 | Center 마름모 테두리 r=8 |
| 6 | 주먹 내려치기 · Fist Ripple / `p1-fist-ripple` | 3.2초 | 21 | 5 / 10 / 16 | (19,8)에서 마름모 테두리 r=2→3→4, 빨강→자주→보라. 빈 바닥 때문에 일부 잘림 |
| 7 | 교차 낙석 · Alternating Rockfall / `p1-alternating-rockfall` | 4.6초 | 18 | 374 / 373 | 2×2 묶음 체커 A → 반대 묶음 B. 두 번 내려치기 |

\* 타일 수는 현재 바닥과 교차시킨 **주 Warning 영역**의 크기이며, 플레이어 위치에 따른 ensureEscape 제외 전이다. 특정 상황에서 실제 표시 수는 줄 수 있다. Cells 필드의 길이와 실제 유효 타일 수를 혼동하지 않는다.

기존 Diagonal/Diamond 4개는 다음 공통 구성이다.

| 시간 | 이벤트 |
|---|---|
| 0~2.0초 | Warning + Warning SFX |
| 2.0~2.3초 | Damage + Impact SFX + Impact VFX + Camera |

주 영역은 Center/offset=(0,0), `snapshotKey=glyph`, ensureEscape=true. Damage.escapeGrace=0.18초, SFX.volume=0.7, Camera.shake=12다. VFX 프리팹과 Sprite는 비어 있어 기존 주황색 UI 대체 표시를 사용한다. P1_Diagonal_0/1은 현재 이벤트/영역/시간 구성이 같다. 이름의 0/1이 자동으로 방향을 반전시키지 않는다.

### P1_Cross_0 — 실제로는 3연속 사용자 편집 패턴

이름은 Cross지만 현재 Shape는 계산형 Cross가 아니라 **직접 칠한 Cells**다. minimumDuration=2.3초이지만 마지막 활성 이벤트가 약 6.4초에 끝나므로 실제 패턴 길이는 약 6.4초다. YAML의 5.1000004/6.1000004초는 아래에서 5.1/6.1로 표시한다.

| 공격 | 영역 / 키 | 경고 | Damage | 부가 연출 |
|---|---|---|---|---|
| 1 | Center, 유효 49칸(원시 52), `glyph`, 탈출 보조 켜짐 | 0~2.0 | 2.0~2.3 | 경고/타격 SFX, 카메라, VFX_DirtAreaExplosion |
| 2 | Center, 유효 42칸(원시 44), `attack_020f4cb4ef184521ba5682cee5125056`, 탈출 보조 켜짐 | 2.55~4.55 | 4.55~4.85 | 경고/타격 SFX, 카메라, 프리팹 없는 기본 VFX |
| 3 | Absolute, 45칸, `attack_a7605c4fd3a1473fb851392ef5ca7601`, 탈출 보조 꺼짐 | 5.1~6.1 | 6.1~6.4 | 현재는 경고/타격 두 이벤트만 있음 |

세 Damage 모두 escapeGrace=0.18초다. 1·2번은 각각 이벤트 6개, 3번은 2개로 총 14개다.

영역을 다시 확인할 때 사용할 좌표 요약:

- 공격 1, **Center 상대 좌표**: 가로줄 `y=0, x=-16~16`와 세로줄 `x=0, y=-19~-1`. (19,19)를 더하면 절대 좌표가 된다.
- 공격 2, **Center 상대 좌표**: 가로줄 `y=0, x=-16~-8 / -6~9 / 11~16`; 세로줄 `x=0, y=-19,-18,-17,-16,-14,-12,-11,-10,-7,-6,-4,-2,-1`.
- 공격 3, **Absolute 좌표**:

| y | x 목록/범위 |
|---:|---|
| 5 | 11~27 |
| 6 | 9~10 |
| 7 | 9 |
| 8 | 9~10 |
| 9 | 10, 16 |
| 10 | 10 |
| 11 | 10~11, 21~24, 28 |
| 12 | 12~20, 28 |
| 13 | 28 |
| 14 | 18, 27 |

현재 저장 상태에서 알아둘 점:

1. 공격 1의 Warning/Damage/VFX 원시 Cells는 현재 모두 52개다(이전 문서의 52/48/51과 다름). `glyph`를 공유하고 Warning이 먼저 계산하므로 실행은 Warning의 스냅샷 영역을 재사용한다. 현재 바닥과 교차한 주 영역은 탈출 보조 제외 전 49칸이다.
2. 공격 1의 VFX는 `VFX_DirtAreaExplosion.prefab`로 지정되어 있다. 이벤트가 0.3초에 끝나므로 최대 0.5초짜리 효과의 끝부분은 잘린다. 필요하면 **VFX만** 0.5초 이상으로 조정할 수 있지만 이 문서 작성에서는 수정하지 않았다.
3. 해당 효과는 타일당 47개의 UI 입자를 만들므로 49칸에서는 탈출 보조 제외 전 최대 2,303개가 생성된다. 이것은 입자 수 계산이지 성능 측정 결과는 아니다. 성능/가독성을 실제 플레이에서 확인해야 한다.

### 주먹 내려치기 · Fist Ripple / `p1-fist-ripple`

- 목적: 보스의 준비 동작을 읽고, 한 칸씩 퍼지는 파동을 피한다. 최초 설계는 지나간 안쪽으로 회피하는 방식이었으나, 이후 사용자 편집으로 중앙 바닥이 제거되어 모든 타격 칸에서 안쪽 한 칸 회피가 가능한 상태는 아니다.
- 위치: 모든 파동은 `Absolute`, offset=(19,8), `Diamond`. 논리 Center=(19,19)와 구별한다. 보스 배치·전장·스폰·START/END 영역은 변경하지 않았다.
- 이름/연결: 간편 공격 설계에 3개 공격으로 표시되며, 각 Warning/Damage/VFX의 영역 데이터와 키는 일치하는 독립 복사본이다. 소리/카메라에도 attackGroupKey/attackAtImpact를 저장했다.

| 공격 / 독립 키 | 반경 / 현재 바닥 수 | 경고 | 단발 Damage | UI VFX |
|---|---|---|---|---|
| 1 · 붉은 내측 파동 / `fist_ripple_1` | r=2 / 5칸 | 0.20~1.20 | 1.20~1.50 | 1.20~1.55, VFX_CrimsonGolem_FistRipple1 |
| 2 · 자주색 중간 파동 / `fist_ripple_2` | r=3 / 10칸 | 0.75~1.75 | 1.75~2.05 | 1.75~2.10, VFX_CrimsonGolem_FistRipple2 |
| 3 · 보라색 외측 파동 / `fist_ripple_3` | r=4 / 16칸 | 1.30~2.30 | 2.30~2.60 | 2.30~2.65, VFX_CrimsonGolem_FistRipple3 |

각 Damage의 escapeGrace=0.18, ensureEscape=false. 경고는 겹치지만 타격 구간은 분리되어 있다. 지속 Hazard/장애물/Targeted 호출은 없다. 현재 (17,8)/(21,8) 등은 안쪽 인접 칸이 빈 바닥이므로 최초의 일괄적인 안쪽 회피 설명을 적용하지 않는다. 이번 낙석 작업에서는 기존 파동이나 맵을 수정하지 않았다. 경고 중 미리 이동하는 것이 기본 회피이며 유예 시간에 의존하는 설계가 아니다. P1에만 배치되어 정상 전투에서 P2 수정과 동시에 실행되지 않는다. P2로 옮기면 수정 위치·별도 타이머와의 조합 검증이 추가로 필요하다.

보스 외형은 사용자가 제공한 `cave_golem_ground_slam_sheet.png`를 `Assets/Art/Bosses/CrimsonGolem/CaveGolemGroundSlam.png`에 원본 그대로 복사했다. 960×80 PNG를 80×80 12개 Sprite로 분할했고 Point 필터, 24 PPU, 공통 pivot=(0.5,0.125)를 사용한다. 이미지 생성 시안은 채택하지 않았다.

- 0~1.20초: `BossAnimationEvent` → `Base Layer.FistWindup`, 준비/들어올리기/내려치기 프레임.
- 1.20초: 타격 이벤트보다 먼저 `Base Layer.FistContact`로 전환. 0부터 세는 프레임 7(시트의 8번째, 착지 자세)에서 시작해 회복한다. transition=0, speed=1.
- 1.20~1.70초: 충격 중심 한 칸에 VFX_DirtAreaExplosion을 지정해 두었으나, 현재 중심 (19,8)이 빈 바닥이므로 실제 유효 대상은 0칸이다. 시각 효과 이벤트이며 피해/안전 판정은 세 파동 영역에만 있다.
- 경고색과 파동 프리팹 색은 빨강→자주→보라. 기존 Damage 표시는 빨강이지만 그 위의 전용 Canvas VFX가 파동별 색을 표현한다. 각 타일당 Flash 1+Spark 3개이며 BossVfx용 프리팹이 아니다.
- 보스 프리팹에는 고정 `GroundImpact` 소켓만 있다. Sprite 프레임 애니메이션이므로 움직이는 주먹 뼈대/손 소켓이 있다고 가정하지 않는다.
- 2.65초에 모든 공격 VFX가 끝나고 3.20초 패턴 종료/취소 때 Idle로 복귀한다. P1의 기존 완료 후 대기가 추가된다. 새 런타임 공격 타입이나 보스/패턴 ID 분기는 없다.
- 팀원 편집: 간편 UI에서 각 파동의 영역/경고/타격/연출을 수정한다. 첫 타격 시각을 바꾸면 고급 편집의 `주먹 착지 / 회복` 시작도 같은 시각으로 맞추고 Windup 클립도 조정해야 한다. BossAnimation은 간편 공격 묶음에 자동 연결되지 않는다.
- 현재 보스 밑 3×3은 빈 바닥이다. 보스 외형 자체는 충돌을 만들지 않으며 실제 이동 가능 여부는 바닥 데이터로 정한다. 프레임이 크게 지연되면 중간 모션의 화면 표시가 생략될 수 있다.

### 교차 낙석 · Alternating Rockfall / `p1-alternating-rockfall`

- 보스/배치: `crimson-golem`, PHASE 1 저장 목록 7번, enabled=true, 최소/실제 길이 4.6초. 기존 패턴 상대 순서·활성 상태·페이즈 체력/대기는 보존했다.
- 목적: 그림의 2×2 빨간 묶음이 먼저 떨어지고, 다음에는 처음 비어 있던 묶음이 공격되므로 안전 구역을 한 번 바꾸게 한다. 그림의 맵을 복사하지 않고 실제 747개 바닥에 맞췄다.
- 영역: `Cells`/`Absolute`, offset=(0,0). A는 `((x/2 + y/2) & 1)==0`인 374칸, B는 나머지 373칸이다(현재 좌표는 음수 없음). 저장된 직접 칠하기 목록이므로 이후 에디터에서 수정할 수 있으며, 맵 변경 시 새 바닥이 자동으로 목록에 추가되지는 않는다.
- 연결: 독립 키 `rockfall_a`/`rockfall_b`. 각 공격의 Warning/Damage/하강 VFX/착지 VFX는 같은 영역·snapshotKey를 갖는 독립 복사본이다. 간편 공격 이름과 7개 구성원 연결을 유지한다. 전체 18개 이벤트 중 14개는 간편 공격 2개, 4개는 BossAnimation이다.

| 공격 / 키 | 경고 + 준비 동작 | 낙석 하강 VFX | 단발 Damage + 착지 동작 | 착지 VFX |
|---|---|---|---|---|
| 1 · 빨간 묶음 낙석 / `rockfall_a` | 0~1.20 | 0.85~1.20 | 1.20~1.50 | 1.20~1.60 |
| 2 · 빈 묶음 반전 낙석 / `rockfall_b` | 2.20~3.40 | 3.05~3.40 | 3.40~3.70 | 3.40~3.80 |

- 회피: 처음 B에서 피하고 A의 판정이 끝난 1.50초 이후 A로 이동해 두 번째 공격을 피한다. 매 경고는 1.20초, Damage.escapeGrace=0.18초, ensureEscape=false. 747칸 전부를 BFS로 검사했으며 각 묶음의 타격 칸에서 반대 영역까지 최대 두 칸 이동이다. 각 공격마다 가장자리 2칸은 두 번 이동해야 한다. 경고 중 이동할 시간을 둔 설계이지 유예나 ensureEscape만으로 무조건 회피를 보장하는 주장은 아니다.
- 겹침: 첫 착지 VFX도 1.60초에 끝나며 다음 경고는 2.20초에 시작한다. 지속 Hazard, 장애물, 하위 패턴, Background는 없다. P1에만 배치되어 P2 수정과 동시에 실행되지 않는다. P2로 옮길 경우 수정 장애물/별도 공격 타이머와의 합동 검사가 필요하다.
- 보스 연출: 기존 사용자 제공 시트·프리팹·Animator를 그대로 사용한다. 0/2.20초에 `Base Layer.FistWindup`, 1.20/3.40초에 `Base Layer.FistContact`, 모두 transition=0/speed=1. 각 접촉 이벤트를 같은 시각의 Damage보다 먼저 실행해 시트의 8번째 착지 프레임 `Slam_07`로 전환한다. 첫 회복 끝부분은 2.20초의 두 번째 준비 동작이 인계한다. 새 공격 전용 런타임 코드나 ID 분기는 없다.
- 타일 연출: `VFX_CrimsonGolem_RockfallFalling.prefab`은 타일 위에서 내려오는 돌색 Chunk 1개(0.35초), `VFX_CrimsonGolem_RockfallImpact.prefab`은 착지 Flash 1 + 작은 Chunk 1개(최대 0.4초)다. 기존 `UiEffectPlayer`를 사용하는 **Canvas 타일 VFX**이며 BossVfx/손 소켓 효과가 아니다. 1차 최대 하강 374개/착지 748개 입자다. 이는 수량 계산이지 성능 측정 결과가 아니다. 경고/타격 SFX 및 shake=7도 연결했다.
- 종료: 두 번째 착지 VFX는 3.80초에 끝나고, 마지막 회복 뒤 4.60초에 패턴이 끝난다. 완료·취소 때 각 lease가 표시/소리/카메라 자원을 해제하고 보스가 Idle로 복귀한다. 그 후 기존 P1 완료 후 대기가 붙는다.
- 팀원 편집: 간편 공격 2개에서 영역/경고/타격/VFX를 수정한다. 타격 시각을 바꾸면 **고급 편집의 해당 BossAnimation 준비/착지 시각도 함께 맞춰야 한다**. 하강 VFX는 타격 연결(`attackAtImpact=true`)과 -0.35초 상대 오프셋을 갖는다. 하강 길이를 바꾸면 프리팹의 lifetime/offset/velocityY도 함께 조정해야 접촉 시점에 도달한다.
- 최종 반영: 미저장 편집 보존 승인 후 Unity 종료와 잠금 해제를 확인하고 에셋에 신규 패턴/이벤트 블록만 추가했다. 기본 생성기로 기존 보스를 재생성하지 않았다. `CrimsonGolemRockfallAuthoring`은 명시적으로 호출하는 1회 추가 도구이며 기존 ID가 있으면 다시 생성하지 않는다.

## 4. 2페이즈 주 패턴

| 순서 | 표시 이름 / ID | 실제 길이 | 직접 이벤트 수 | 주 경고 타일 수* | 내용 |
|---|---|---:|---:|---|---|
| 1 | `P2_Combined_0` / `p2-combined-0` | 1.3초 | 7 | 57 | Center 십자 + 마름모 테두리 r=5 |
| 2 | `P2_Combined_1` / `p2-combined-1` | 1.3초 | 7 | 57 | Combined 0번과 같은 구성 |
| 3 | `P2_Cross_0` / `p2-cross-0` | 1.3초 | 7 | 49 | Center 십자 |
| 4 | `P2_Cross_1` / `p2-cross-1` | 1.3초 | 7 | 49 | Cross 0번과 같은 구성 |
| 5 | `P2_Diagonal_0` / `p2-diagonal-0` | 1.3초 | 7 | 35 | Center 대각선 |
| 6 | `P2_Diagonal_1` / `p2-diagonal-1` | 1.3초 | 7 | 35 | Diagonal 0번과 같은 구성 |
| 7 | `P2_Diamond_0` / `p2-diamond-0` | 1.3초 | 7 | 11 | Center 마름모 테두리 r=5 |
| 8 | `P2_Diamond_1` / `p2-diamond-1` | 1.3초 | 7 | 17 | Center 마름모 테두리 r=8 |
| 9 | `P2_Horizontal_0` / `p2-horizontal-0` | 1.3초 | 7 | 375 | Center 기준 한 줄 건너 가로줄 전체 |
| 10 | `P2_Horizontal_1` / `p2-horizontal-1` | 1.3초 | 7 | 375 | Horizontal 0번과 같은 구성 |
| 11 | `P2_Vertical_0` / `p2-vertical-0` | 1.3초 | 7 | 369 | Center 기준 한 열 건너 세로줄 전체 |
| 12 | `P2_Vertical_1` / `p2-vertical-1` | 1.3초 | 7 | 369 | Vertical 0번과 같은 구성 |

모든 패턴은 다음 공통 구성을 가진다. 표의 타일 수는 주 Warning만 세며, 별도로 조준 보조 패턴이 겹친다.

| 부모 기준 시간 | 이벤트 |
|---|---|
| 0~1.0초 | 주 Warning + Warning SFX |
| 0.15~1.0초 | Targeted 보조 패턴 호출. 0.15초의 플레이어 위치를 원점으로 전달 |
| 0.15~0.8초 | 호출된 Targeted의 한 타일 Warning |
| 0.8~1.0초 | Targeted의 한 타일 Damage + SFX |
| 1.0~1.3초 | 주 Damage + Impact SFX + Impact VFX + Camera |

주 영역은 Center/offset=(0,0), `snapshotKey=glyph`, ensureEscape=true다. 주 Damage의 grace=0.18초, SFX.volume=0.7, Camera.shake=12이며, 주 VFX는 프리팹 없는 기본 표시다.

Combined의 두 항목 모두 radius=5다. Cross/Diagonal/Horizontal/Vertical의 radius=5 저장값은 해당 모양의 범위를 자르지 않는다. Diamond만 0번 r=5 / 1번 r=8이다. Diamond를 제외한 각 0/1 쌍은 현재 이벤트/영역/시간 구성이 같지만 **별도 패턴 ID이며 두 항목 모두 순서대로 실행**된다. 자동 중복 제거 대상으로 간주하지 않는다.

가로/세로 격자는 현 전장에서 각각 375/369칸을 한꺼번에 경고한다. 여기에 무거운 VFX를 전 타일 적용하기 전에 타일 수를 확인한다.

## 5. 보조 패턴

### Targeted / `targeted`

- 최소/실제 길이: 0.85초. 직접 이벤트 3개.
- 영역: Cells=[(0,0)], Anchor=Origin, offset=(0,0), snapshotKey=`target`, ensureEscape=false.
- 0~0.65초: 보라색 Warning (r=0.7, g=0.3, b=1, a=0.7).
- 0.65~0.85초: Damage, grace=0.18초, reason=Targeted; 같은 시간 Impact.wav, volume=0.7.
- 전용 VFX는 없다.
- 모든 P2 주 패턴이 0.15초에 anchorToPlayer=true로 호출한다. 따라서 **호출 순간의 플레이어 한 칸을 고정 조준**하며 지속 추적하지 않는다.
- 이 보조 패턴만 단독 프리뷰하면 호출자가 전달할 플레이어 원점이 없으므로 같은 조준 상황을 재현한다고 가정하지 않는다.

### Example_Devices / `example-devices`

길이 6초, 직접 이벤트 6개. 도구 기능을 설명하는 예제이며 현재 다른 패턴에서 호출되지 않아 **일반 전투에서 자동 실행되지 않는다**.

| 시각/구간 | 내용 |
|---|---|
| 0초 | key=device 소환, Origin+(3,0), persist=true. 프리팹/Sprite 없음, 청록 UI 표시 |
| 1~3초 | key=wall 장애물. Center+(0,3)+Cells[(0,0),(2,-2)], persist=false |
| 2초 | key=pool 장판 생성. Center 정사각형 radius=1, persist=true |
| 4초 | pool 제거 |
| 5초 | device 제거 |
| 5.5초 | `device-cycle-complete` Signal. argument 빈 값 |
| 6초 | 패턴 종료/남은 자원 정리 |

현재 Center=(19,19)에서 벽 후보는 (19,22), (21,20)이며 둘 다 바닥 밖이다. 따라서 **현 맵에서 이 예제 벽은 유효 대상이 없다**. 새 제작자가 예제를 사용하려면 실제 전장에 맞춰 좌표를 정해야 한다. Signal은 수신 로직이 있어야 별도 장치 행동을 일으킨다.

## 6. 수정 봉인 기믹 (기존 수정 공격 대체)

2페이즈 `mechanics`에 `CrystalSealMechanic`을 등록했다. `legacyCrystals`는 꺼져 있으며 체력 절반 재배치와 독립 코루틴 공격은 정상 전투에서 실행하지 않는다. 위의 기존 패턴 수량/맵 수치는 작성 당시 기록이며 현재 에셋을 우선한다.

- 지정 위치 5개: (17,5), (28,13), (24,27), (10,27), (6,13). 중앙 보스에서 약 12칸 거리의 좌우 대칭 오각형으로 배치했다. 2페이즈 진입 즉시 생성되며 에디터에서 추가·이동·삭제 가능하다.
- 수정 타일은 통과 가능. 완성된 경로 공격에 포함된 수정들을 모두 비활성화한다. 활성 수정이 남아 있던 공격은 보스 체력을 1 아래로 내릴 수 없다. **마지막 수정 해제 공격 다음 공격부터 처치 가능**하다.
- 공유 공격 ID `crystal-seal-attack`: Origin 기준 2칸 체크무늬, 경고 0~0.7초, 타격 0.7~1.0초(유예 0.18초), VFX_CrystalSparks VFX 0.7~1.05초.
- 기본 첫 대기/공격 시작 간격은 모두 5초. 수정별로 공격과 시간을 따로 지정할 수 있다. 주 타임라인과 같은 inputLocked 일시정지 규칙을 사용한다.
- 비활성화는 진행 중 경고·타격을 정리하고 어두운 외형을 남긴다. 모두 해제해도 자동 처치는 하지 않는다.
- 편집·미리보기·개발 확장 규칙: [보스 기믹 가이드](../BossMechanics.md).
- 검증: 신규 기믹 19/19, 외형/실전 통합 4/4. 관련 113개 중 112개 통과, 기존 `P1_Cross_0` 이름 가정 검사 1개는 작업 전 에셋에서도 실패한다. 기존 공격과 맵은 해당 검사에 맞춰 변경하지 않았다.

### 2페이즈 수정 외형 — 2026-09-20 교체

- 표시 프리팹: `Assets/Resources/Art/Crystals/MechanicVisual_CrystalSeal.prefab`. 사용자가 제공한 `red.png`를 `Body`, `hole.png`를 `Shadow`로 사용한다. 두 파일은 원본과 SHA-256이 같은 64×64 PNG이며 별도 가공하지 않았다. Point 필터, mipmap 없음, 압축 없음으로 임포트한다.
- 두 이미지의 원래 64×64 배치를 동일한 좌표계로 겹친다. 실제 수정 높이는 기본 약 0.80타일, 그림자는 약 0.46×0.11타일이다. 하단 끝점은 타일 중심보다 약 0.14타일 아래에 놓인다. 현재 타일 크기로 프리팹 루트를 다시 맞추므로 전장 줌에도 함께 대응한다.
- `Shadow`는 `Body`보다 먼저 그리며 Image 색의 **A=0.42**로 설정했다(42% 불투명도). 원본 검은색을 반투명 접촉 그림자로 표시한다. `Body`는 원본 색상/불투명도 그대로이며 기존 붉은 Outline은 제거했다.
- 수정 아래 타일 전체를 어둡게 덮던 기존 색상 덮어쓰기도 제거했다. 실제 바닥 이미지 위에 그림자가 보이게 한 표시 변경이며 바닥/장애물 데이터는 그대로다.
- 기존 맥동 주기/세기는 유지하되 본체만 하단 끝점을 축으로 확대·축소한다. 그림자와 루트 위치/크기는 맥동하지 않아 그림자가 바닥에 고정된다. `CrystalVisual`은 표시만 담당하며 배치·충돌·공격·피해 판정 로직을 갖지 않는다.
- 수정 재배치 시 본체/그림자가 같은 루트로 이동하고, P1 재시작/허브에서는 함께 숨는다. 이는 지속 표시용 Canvas UI이며 타일 타격 VFX 또는 BossVfx 프리팹이 아니다.
- 팀원 조정: 위 프리팹을 열어 `Shadow > Image > Color > A`에서 진하기를, `Body`/`Shadow`의 RectTransform에서 위치와 크기를 수정한다. 본체의 낮은 pivot은 맥동 시 하단 끝점을 고정하기 위한 값이다.
- 기존 공용 `Assets/Resources/Art/red_attack_crystal.png`는 허브/보스 아이콘에도 사용되므로 변경하지 않았다. 수정 위치·장애물·공격 주기·재배치 규칙, CrimsonGolem/BossCatalog 및 모든 패턴 데이터도 변경하지 않았다.

## 7. 구현된 기능과 현재 사용 여부

| 기능 | 현재 콘텐츠에서의 상태 |
|---|---|
| 여러 번 공격하는 단일 패턴 | P1_Cross_0, Fist Ripple, Alternating Rockfall에서 사용 |
| 플레이어 위치 고정 조준 / 하위 패턴 호출 | P2 → Targeted에서 사용 |
| 장판/벽/소환/제거/Signal | Example_Devices에 예제로 저장, 일반 주 패턴에서는 미사용 |
| 타일 이펙트 프리팹 | P1_Cross_0의 VFX_DirtAreaExplosion, Fist Ripple 3색 파동, Rockfall 하강/착지. 기타 주 VFX는 기본 표시 |
| BossAnimation/BossVfx/BossMotion | Fist Ripple에 BossAnimation 2개, Rockfall에 4개. BossVfx/BossMotion은 미사용. 같은 프리팹과 3개 Animator 상태 재사용 |
| Background timeline | 두 페이즈 모두 비활성/빈 목록 |
| 공유 PatternSequence 호출 | 현재 보스에 사용 없음 |

공용 UI 프리팹 4종과 권장 시간은 [이펙트 안내](../EffectPrefabs.md)에 있다. 보스 전용 FistRipple 3종/Rockfall 2종은 위 패턴 항목을 참조한다. 기능이 구현되어 있다는 것과 그 기능을 쓰는 콘텐츠가 현재 존재한다는 것을 구분한다.

### 공통 타일 위험 표시 — 2026-09-20 교체

- 프리팹: `Assets/Resources/Art/Warnings/TileVisual_AttackWarning.prefab`. 사용자 제공 `danger_indicator_128x128.png`를 원본 그대로 사용한다(SHA-256 `D806DD699F324E1DD960A4CCC7E8ED0C86F8FC56E047BCFF09C75AEAF87FC8F4`). 128×128 Single Sprite, Point 필터, 압축/mipmap 없음, 투명 배경 유지.
- 적용 대상은 타임라인의 `WarningEvent`와 기존 수정/조준/문양 경고다. 두 표시 경로가 같은 `TileWarningVisual` 프리팹을 사용한다. 보스나 패턴 ID를 분기하지 않으며 각 공격의 영역·snapshotKey·경고 시간·피격 유예는 변경하지 않았다.
- 빨간 테두리와 느낌표는 경고 시작부터 온전한 크기로 보인다. 타일 안쪽 여백과 위치는 기존 경고 표시를 따른다. 이미지 자체의 색은 흰색 tint/불투명도 1로 원본 빨강을 유지하고, 뒤쪽 `Warning Progress`만 커져 남은 경고 시간을 표시한다. 타임라인은 15→100%, 기존 수정 경고는 0→100% 진행 크기를 사용한다.
- 패턴 에디터의 Warning Color는 진행 표시 색으로 계속 적용한다. 진행 표시 알파는 기존 경고색 알파×`progressOpacity`(프리팹 기본 0.65)다. 느낌표를 보라색으로 곱하여 원본 색이 검게 변하는 방식은 사용하지 않는다. 팀원은 프리팹의 `Indicator` Sprite/Color와 `Progress Opacity`를 조정할 수 있다.
- 단발 Damage, 지속 Hazard, 장애물, 타격 VFX/SFX와 미니맵 표시는 기존 방식이다. 에디터의 영역 선택용 색칠도 변경하지 않았다. 경고 표시는 피해를 추가하지 않으며 각 타임라인 lease 또는 수정 경고 소유자가 종료·취소·재시작 시 정리한다.
- 보스 종합 데이터·맵·패턴 및 앞서 적용한 수정 본체/그림자 에셋은 변경하지 않았다.

## 8. 검증 기록과 향후 갱신

### 타일 위험 표시 교체 — 2026-09-20, 현재 코드·프리팹

- Unity 6000.5.3f1의 격리 복사본에서 최종 전체 검사 **146개 중 144개 통과, 2개 실패, 스킵 0**. 결과: `Logs/PatternValidation/DangerIndicator/FinalFullTests.xml` 및 `.log`. 신규 `TileWarningVisualTests` **5개 모두 통과**했으며 별도 관련 검사도 5/5 통과했다(`FocusedTests.xml`).
- 실패 2개는 작업 시작 전에도 존재한 `FistRippleTests`의 현재 바닥 수량 차이(내측 8→5, 첫 VFX 9→5)다. 맵/파동 데이터나 기존 기대값을 이 작업에서 변경하지 않았다. 초기 신규 검사의 CLR 객체 참조 비교는 Unity 에셋 GUID/로컬 파일 ID/경로 대조로 보완한 뒤 위 전체 검사를 다시 실행했다.
- 신규 검사는 원본 스프라이트 참조·Point/무압축/투명 임포트, 진행값 0~1과 96/160 크기의 타일에서 이미지 크기/원색 유지, 경고색의 진행 표시 적용, 실제 낙석 경고 영역 수와 겹친 경고의 독립 정리, 경고→타격 전환, 기존 Damage 표시 보존, 프리뷰 중단/재시작을 확인했다. 실제 P2 진입 후 수정 폭발 경고의 표시·정상 종료·재시작 정리도 확인했다.
- 최종 원본에서 `Tools/VerifyPatternCompilation.ps1` 런타임/에디터 컴파일 통과(기존 경고 7개). `Tools/ValidatePatternAssets.py`도 **21개 인라인 타임라인/170개 이벤트**의 GUID/타이밍/타입/호출 검사 통과.
- 사용자 PNG와 프로젝트 PNG의 SHA-256 일치, 검증 복사본과 원본 코드/프리팹 일치를 확인했다. `BossData_CrimsonGolem.asset`, `BossCatalog_Main.asset`, `MechanicVisual_CrystalSeal.prefab`은 작업 전후 해시가 동일하며 기존 미커밋 변경을 보존했다.
- 실제 게임 호스트의 낙석/수정 경고를 자동 렌더하여 투명 배경과 타일 배치를 확인했다. `Logs/PatternValidation/DangerIndicator/RockfallWarning.png`, `CrystalWarning.png`. 캡처를 위한 플레이어/시점과 시간 고정은 테스트 인스턴스에만 적용했으며 저장 에셋의 스폰/줌/시간표는 변경하지 않았다.
- 미검증: 수동 조작 실시간 플레이, 모든 패턴 조합의 시각 가독성, 극단적 프레임 지연 및 저사양·장시간 성능. 편집기의 영역 색칠을 게임 렌더 검증으로 대신하지 않았다.

### 수정 외형 교체 — 2026-09-20, 현재 코드·프리팹

- Unity 6000.5.3f1의 격리 프로젝트에서 최종 코드·프리팹을 재임포트하고 전체 검사를 실행했다. **141개 중 139개 통과, 2개 실패, 스킵 0**. 결과: `Logs/PatternValidation/CrystalVisuals/FinalFullTests.xml` 및 같은 이름의 `.log`.
- 신규 `CrystalVisualTests` **4개 모두 통과**. 원본 Sprite/임포트 설정, 그림자 A=0.42와 그리기 순서, 96/160 크기의 타일 대응, 본체 맥동 시 끝점·그림자 고정, 실제 P1→P2 전환의 생성/위치/장애물 표시, 재배치 시 인스턴스 재사용, P1 재시작·허브 복귀 시 숨김을 검사했다. 수정 아래 바닥색 보존과 기존 공격 타이머 설정도 확인했다.
- 실패 2개는 이번 변경 전에도 존재했던 `FistRippleTests`의 현재 바닥과 이전 기대 수량 차이다. `EveryHitCellHasOneStepInwardEscapeOnActualFloor`는 기대 8/실제 5, `RuntimeWavesRenderAndCleanUpWithVisiblePrefabHealthBar`는 기대 9/실제 5다. 이번 외형 작업에서 맵·공격 영역이나 해당 기대값을 변경하지 않았다.
- 최종 원본에서 `Tools/VerifyPatternCompilation.ps1`의 런타임/에디터 컴파일을 실행했다(기존 경고 7개, 오류 없음). `Tools/ValidatePatternAssets.py`도 실행해 **21개 인라인 타임라인/170개 이벤트**의 GUID/시간/타입/호출 검사를 통과했다.
- `BossData_CrimsonGolem.asset`과 `BossCatalog_Main.asset`의 SHA-256이 작업 시작 시점과 동일하다. PNG 2개는 각각 사용자 원본과 해시가 일치하며, 최종 Unity 검사에 사용한 런타임 코드도 원본과 동일하다. 기존 사용자 미커밋 변경을 보존했다.
- 실제 게임 호스트에서 2페이즈 진입과 수정 재배치 후 화면을 자동 렌더하여 본체/접촉 그림자 배치를 확인했다. 파일: `Logs/PatternValidation/CrystalVisuals/PhaseTwoCrystal_Entry.png`, `PhaseTwoCrystal_Relocated.png`. 캡처용 플레이어/시점 조정은 테스트 인스턴스에만 적용했으며 저장된 스폰·줌·맵은 변경하지 않았다.
- 미검증: 수동 조작 실시간 플레이, 장시간 전투 및 주 패턴과 수정 공격의 모든 조합, 효과음 청취, 저사양 성능. 과거 패턴 제작 당시 통과 기록을 이번 외형 검증으로 대체하지 않았다.

### 낙석 추가 — 2026-09-20, 현재 에셋

- Unity 버전은 `ProjectSettings/ProjectVersion.txt`의 6000.5.3f1과 실행 버전이 일치한다. 최종 저장 에셋으로 격리 복사본에서 재임포트·컴파일 및 전체 137개 검사를 실제 실행했다. **135개 통과, 2개 실패, 스킵 0**. 결과: `Logs/PatternValidation/Rockfall/AppliedAssetTests.xml`.
- 실패 2개는 기존 `FistRippleTests`의 이전 바닥 기준 수량 검사다. 내측 기대 8칸이 실제 5칸이며, 첫 타격 VFX 기대 9개(파동 8+중심 1)가 현재 5개(파동 5+중심 0)다. 테스트의 '항상 마지막 패턴' 가정만 목록 포함 여부로 수정했다. 기존 맵/파동을 테스트에 맞춰 되돌리거나, 실패를 숨기기 위해 수량 기대값을 변경하지 않았다. 앞 절의 기존 파동 회피 주의와 함께 별도 검토가 필요하다.
- 신규 `RockfallPatternTests` 5개는 전체 실행에서 모두 통과했다. 간편 공격 2개, 영역 독립성/일치, 실제 바닥 374+373의 상보 관계, 모든 타격 칸의 최대 두 칸 탈출, 안전 묶음을 바꾸면 무피격/계속 머물면 2차 피격, 착지 경계 `Slam_07`, 하강 위치, VFX 수량, 완료/취소 시 Idle와 리소스 정리를 검사했다.
- 자동 캡처의 중간 준비 모션까지 정확하게 진행하도록 테스트 시간 간격을 최대 0.01초로 보정한 뒤 최종 관련 검사 **5/5, 실패·스킵 0**으로 재확인했다. 결과: `Logs/PatternValidation/Rockfall/AppliedRockfallFinal.xml`. 최종 테스트에 로드한 에셋과 원본 에셋의 SHA-256도 일치한다.
- 정적 `Tools/ValidatePatternAssets.py`를 최종 원본에서 실제 실행해 **21개 인라인 타임라인/170개 이벤트**, GUID/타이밍/타입/호출 관계를 확인했다. 변경 전 저장본 `Logs/PatternValidation/Rockfall/CrimsonGolem.saved-before.asset`에서 비교하여 새 패턴 1개와 해당 18개 managed event 블록을 제외한 **나머지 원문 전체 일치**를 확인했다. BossCatalog는 변경하지 않았다.
- 렌더는 테스트에서 임시 플레이어 시점을 보스 옆으로 옮겨 실제 게임 호스트를 작은 시간 간격으로 전진시킨 자동 캡처다. 에셋의 스폰·줌을 변경한 것이 아니다. 파일: `Logs/PatternValidation/Rockfall/Rockfall_0.98.png`, `Rockfall_1.20.png`, `Rockfall_1.70.png`, `Rockfall_2.90.png`, `Rockfall_3.18.png`, `Rockfall_3.40.png`.
- 미검증: 수동 조작/연속 실시간 플레이, 효과음 청취, 장시간·저사양 성능, P2 수정과 합동 동작, 극단적인 프레임 지연. 과거 파동/이펙트 테스트 통과를 낙석 검증으로 대체하지 않았다.

### 이전 주먹 파동 제작 당시 기록 — 현재 바닥 변경 이전

- 2026-09-20: `ValidatePatternAssets.py` 실제 실행 통과(20개 타임라인/152개 이벤트). Unity 참조 기반 런타임/에디터 컴파일 통과(기존 경고 7개), 헤드리스 29개 통과.
- Unity 6000.5.3f1 그래픽 장치 활성 상태 전체 검사 132개 중 131개 통과, 신규 캡처 검사 1개 실패. 실패 원인은 검사 코드가 세 번째 타격의 저장 시각 2.3000002보다 이른 2.30에서 멈춘 것이었다. 실제 저장 경계를 사용하도록 검사만 수정한 뒤 관련 5개 모두 재검사 통과(실패·스킵 0). 전체 결과 `Logs/PatternValidation/FistSlam/FullTests.xml`, 최종 관련 결과 `FistRippleFinal.xml`. 전체 132개를 한 번에 모두 통과했다고 표현하지 않는다.
- 신규 검사: Unity 재로드/간편 공격 3개 인식/영역 독립 및 동일성, 실제 바닥 36개 타격 칸의 한 칸 안쪽 회피, 착지 프레임과 타격 경계, 완료/취소 시 Idle·리소스 정리, 실제 Play Mode 호스트에서 8→12→16개 VFX 생성, 체력바 표시 및 재시작 정리를 확인했다.
- 실제 게임 렌더 캡처 `Logs/PatternValidation/FistSlam/FistRipple_1.05.png`, `FistRipple_1.20.png`, `FistRipple_1.75.png`, `FistRipple_2.30.png`를 확인했다. 자동 검사에서 시간을 고정해 실제 게임 호스트를 전진시킨 화면이다. 수동 조작 플레이, 연속 실시간 모션/효과음 청취, 장시간 성능, P2 수정과의 조합 및 극단적 프레임 지연 시 시각적 가독성은 검증하지 않았다.
- 변경 전 작업 폴더 에셋 사본과 비교해 전장 원문, 기존 131개 managed event 정의, 기존 패턴 시간표와 페이즈 설정 보존을 확인했다. 카탈로그 및 기존 상대 순서도 그대로다. Unity 검사 도구가 삭제한 기존 PerformanceTestRunInfo/Settings 파일 4개는 작업 시작 당시 상태로 복원했다.
- 이전 이펙트 구현 당시의 127개 통과 기록(`Logs/PatternValidation/EffectPrefabsFresh.xml`)은 이 변경의 검증으로 사용하지 않았다.
- 카탈로그 수치는 생성기 기본값이 아닌 저장 에셋 기준이다. 새로운 패턴/맵/프리팹 변경 후에는 실제 파일과 다시 대조한다.

다음 작업에서 갱신할 항목: 기준일/소스 해시, 새 패턴의 ID와 페이즈 순서, 경고/타격 시간, 위치 기준/키/영역, 호출 관계, VFX/SFX, 정상 종료/취소 정리, 실제 검증 범위.

| 날짜 | 변경 | 검증/비고 |
|---|---|---|
| 2026-09-19 | 실제 저장 데이터 기준의 첫 인수인계 목록 작성 | 문서만 추가. 기존 보스/맵/패턴 수정 없음 |
| 2026-09-20 | P1 끝에 `p1-fist-ripple` 추가, 사용자 제공 골렘 시트/프리팹/Animator 및 3색 Canvas VFX 제작. 프리팹 외형 사용 시 체력바가 숨는 공통 표시 문제 수정 | 정적/컴파일/헤드리스 29개 통과. Unity 전체 131/132 후 검사 시간값 보정, 관련 5/5 재통과. 실게임 호스트 화면 확인. 맵·기존 패턴 보존 |
| 2026-09-20 | P1 끝에 `p1-alternating-rockfall` 추가. 실제 바닥의 2×2 묶음 2회 낙석, 기존 내려치기 재사용, Canvas 하강/착지 프리팹 2개. 사용자 미저장 위치 (19,8) 유지 | 최종 원본 정적 21/170 통과, Unity 전체 135/137(기존 파동 수량 검사 2개 실패), 신규 5개 통과. 기존 에셋 원문 보존 확인. 카탈로그를 현재 바닥/스폰/활성 상태에 맞게 갱신 |
| 2026-09-20 | 2페이즈 수정 외형을 사용자 제공 red/hole PNG의 전용 UI 프리팹으로 교체. 그림자 A=0.42, 본체만 끝점 기준 맥동 | 신규 외형 검사 4/4, 전체 139/141(기존 파동 수량 검사 2개 실패). 보스·패턴 에셋 및 기존 수정 게임 규칙 보존 |
| 2026-09-20 | 사용자 제공 느낌표 이미지를 공통 타일 경고에 적용. 타임라인/수정 경고가 같은 프리팹 사용, 경고색·진행 표시 유지 | 신규 5/5, 전체 144/146(기존 파동 수량 검사 2개 실패). 낙석·수정 자동 렌더 확인. 공격 데이터·수정 외형 보존 |

새 패턴 기록 양식:

```text
이름 / ID:
보스 / 페이즈 / 목록 순서:
의도 / 회피 방법:
최소 길이 / 실제 길이:
각 공격: 영역(Anchor/Cells/Shape/offset/radius), snapshotKey, 경고, 타격, grace
동시 기믹 / 호출 관계:
VFX / SFX / 보스 연출:
생성 자원 / 종료·취소 시 정리:
기존 패턴과 차이:
검증한 것 / 미검증한 것:
작성일 / 관련 파일:
```
