# 에셋 파일명 규칙

기준일: 2026-09-22. 파일명은 영어로 `Role_DescriptiveName` 형태를 사용한다.

## 역할별 접두사

| 역할 | 파일명 예시 | 현재 적용 범위 |
|---|---|---|
| 보스 종합 데이터 | `BossData_CrimsonGolem.asset` | 현재 보스 데이터 및 새 보스 생성 기본값 |
| 보스 목록 | `BossCatalog_Main.asset` | 게임이 불러오는 보스 카탈로그 |
| 독립 공격 패턴 데이터 | `PatternData_NewPattern.asset` | Shared Pattern 생성 기본값. 현재 보스 내부의 인라인 패턴은 별도 파일이 아님 |
| 독립 기믹 데이터 | `MechanicData_CrystalSeal.asset` | 향후 독립 에셋으로 분리할 경우의 규칙. 현재는 보스 내부 데이터이며 이 파일/생성 메뉴는 없음 |
| 보스 외형 프리팹 | `BossVisual_CrimsonGolem.prefab` | 보스 본체 |
| 기믹 외형 프리팹 | `MechanicVisual_CrystalSeal.prefab` | 수정 등의 장치 외형. 기믹 규칙 데이터와 구분 |
| 타일 표시 프리팹 | `TileVisual_AttackWarning.prefab` | 공격 위험 표시 |
| 타일 이펙트 프리팹 | `VFX_TileImpact.prefab`, `VFX_CrimsonGolem_FistRipple1.prefab` | 공용 또는 특정 보스의 시각 효과 |
| 보스 애니메이터/머티리얼 | `BossAnimator_CrimsonGolem.controller`, `BossMaterial_CrimsonGolem.mat` | 보스 외형에 연결된 보조 에셋 |

이름 변경과 저장 구조 분리는 별개다. 이번 변경은 보스 안의 패턴·기믹을 별도 에셋으로 분리하지 않는다. 에디터에서 설정하는 보스 표시 이름, 패턴 이름, 기믹 이름은 기존 값을 유지한다. 새 보스 파일을 `BossData_IceGolem.asset`로 만들면 초기 표시 이름은 `IceGolem`이며 파일 접두사가 게임 이름에 붙지 않는다.

## 변경한 에셋 16개

| 이전 파일명 | 현재 파일 |
|---|---|
| `CrimsonGolem.asset` | [BossData_CrimsonGolem.asset](../Assets/Resources/Patterns/BossData_CrimsonGolem.asset) |
| `BossCatalog.asset` | [BossCatalog_Main.asset](../Assets/Resources/Patterns/BossCatalog_Main.asset) |
| `CrimsonGolem.prefab` | [BossVisual_CrimsonGolem.prefab](../Assets/Art/Bosses/CrimsonGolem/BossVisual_CrimsonGolem.prefab) |
| `CrimsonGolem.controller` | [BossAnimator_CrimsonGolem.controller](../Assets/Art/Bosses/CrimsonGolem/BossAnimator_CrimsonGolem.controller) |
| `CrimsonGolem.mat` | [BossMaterial_CrimsonGolem.mat](../Assets/Art/Bosses/CrimsonGolem/BossMaterial_CrimsonGolem.mat) |
| `PhaseTwoCrystal.prefab` | [MechanicVisual_CrystalSeal.prefab](../Assets/Resources/Art/Crystals/MechanicVisual_CrystalSeal.prefab) |
| `TileWarning.prefab` | [TileVisual_AttackWarning.prefab](../Assets/Resources/Art/Warnings/TileVisual_AttackWarning.prefab) |
| `TileImpact.prefab` | [VFX_TileImpact.prefab](../Assets/Resources/Effects/Prefabs/Impact/VFX_TileImpact.prefab) |
| `CrystalSparks.prefab` | [VFX_CrystalSparks.prefab](../Assets/Resources/Effects/Prefabs/Impact/VFX_CrystalSparks.prefab) |
| `DirtLaneEruption.prefab` | [VFX_DirtLaneEruption.prefab](../Assets/Resources/Effects/Prefabs/Impact/VFX_DirtLaneEruption.prefab) |
| `DirtAreaExplosion.prefab` | [VFX_DirtAreaExplosion.prefab](../Assets/Resources/Effects/Prefabs/Impact/VFX_DirtAreaExplosion.prefab) |
| `FistRipple1.prefab` | [VFX_CrimsonGolem_FistRipple1.prefab](../Assets/Art/Bosses/CrimsonGolem/VFX_CrimsonGolem_FistRipple1.prefab) |
| `FistRipple2.prefab` | [VFX_CrimsonGolem_FistRipple2.prefab](../Assets/Art/Bosses/CrimsonGolem/VFX_CrimsonGolem_FistRipple2.prefab) |
| `FistRipple3.prefab` | [VFX_CrimsonGolem_FistRipple3.prefab](../Assets/Art/Bosses/CrimsonGolem/VFX_CrimsonGolem_FistRipple3.prefab) |
| `RockfallFalling.prefab` | [VFX_CrimsonGolem_RockfallFalling.prefab](../Assets/Art/Bosses/CrimsonGolem/VFX_CrimsonGolem_RockfallFalling.prefab) |
| `RockfallImpact.prefab` | [VFX_CrimsonGolem_RockfallImpact.prefab](../Assets/Art/Bosses/CrimsonGolem/VFX_CrimsonGolem_RockfallImpact.prefab) |

## 변경하지 않은 것과 호환성

- 기존 `.meta`를 그대로 이동해 GUID를 유지했다. 직렬화된 fileID, 패턴 ID, managed-reference rid와 데이터 내용은 변경하지 않았다.
- 파일명에 맞춰 해당 에셋/프리팹 루트의 이름만 변경했다. Body/Shadow/GroundImpact 같은 자식 이름, Animator의 Base Layer/Idle/FistWindup/FistContact 상태와 애니메이션 클립 이름은 유지한다.
- 맵, 플레이어 시작점, 공격 영역·시간, 기믹 규칙과 사용자의 편집 중 설정은 유지한다. 텍스처, 오디오, 셰이더, 씬, Unity 렌더 설정 및 C# 타입 이름은 이번 이름 정리 대상이 아니다.
- 프로젝트 내 Resources 경로, 에디터 생성 도구, 테스트, 검증 스크립트와 현재 사용 문서의 참조 경로를 갱신했다. 프로젝트 밖의 개인 스크립트가 옛 Resources 경로를 문자열로 사용한다면 새 이름으로 갱신해야 한다.
- 문서의 과거 검증 기록/해시는 해당 검증일 당시 기록이다. 경로/파일명만 현재 이름으로 안내한 경우에도 과거 해시가 현재 파일의 해시라는 뜻은 아니다.
- 새 기믹/패턴 파일을 분리할 경우 위 규칙을 적용하되 사용자가 요청하지 않은 데이터 마이그레이션은 진행하지 않는다.

## 검증 방법

`AssetNamingTests`는 16개 에셋의 GUID, 파일과 루트 이름, Resources 로딩, 보스 목록→보스 데이터→외형/기믹 참조와 생성 기본 이름을 검사한다. `Tools/ValidatePatternAssets.py`의 대상도 새 보스 파일명으로 변경했다.

2026-09-22 검증 결과:

- Unity 6000.5.3f1 격리 프로젝트에서 이름/참조 검사 23개, PatternRunner 19개, 관련 VFX·수정 외형 검사 9개, 총 **51/51 통과**. 결과: `Logs/PatternValidation/AssetNaming20260922.xml`.
- 런타임·에디터·테스트 컴파일 및 정적 에셋 검사 통과(22개 인라인 패턴, 278개 managed-reference 항목).
- 변경 직전 백업과 비교해 16개 에셋의 내용이 최상위 이름 외에는 동일하고, 각 `.meta`는 바이트 단위로 동일함을 확인했다. 사용자 미커밋 기믹 설정도 포함해 보존했다.
- 전체 정상 전투/페이즈 통합 검사는 이번 실행에 포함하지 않았다. 아래 편집 중 설정의 누락은 이름 변경과 별개다.

파일명 변경 검사와 콘텐츠 유효성 검사는 별개다. 2026-09-22 착수 시점의 사용자 데이터에는 1페이즈에 새 수정 봉인이 추가되어 있으나 공격 패턴 연결이 비어 있었다. 이 편집 중 상태를 자동으로 삭제하거나 채우지 않았다. 정상 전투를 실행하려면 사용자가 공격을 연결하거나 해당 미완성 기믹을 비활성화해야 한다.
