# TRACE STRIKE

## Opening tutorial

- The game now opens in a safe 4x4 training grid with all boss attacks disabled.
- First, connect `S` to `E` without revisiting a painted tile to learn the basic path attack.
- The tutorial then introduces special tiles by direct play in this order: yellow `+`, cyan `◆`, brown `≈`, and purple `▼`.
- Stepping on the final purple `▼` completes training and transitions directly into the Crimson Golem stage without a forced death sequence.
- Press `Esc` once to skip the tutorial immediately. The same key skips phase 1 to phase 2, then skips phase 2 to stage clear.

## Crystal Cavern art

- The first stage uses a 16-bit pixel-art crystal cave arena designed to match the supplied armored warrior sprite.
- Cave walls, cyan crystals, warm torches, cracked stone tiles, and a translucent gameplay grid keep attack warnings readable.
- Boss lane attacks rip through the floor with sequential soil bursts and rock debris; area attacks add a larger dust column, shockwave, and field shake.
- The gameplay grid uses the original clean, translucent flat tiles; danger cells remain fully opaque for readability.
- Lane attacks use 12 debris particles and four dust layers per cell; area explosions use 40 debris particles and six dust layers with shortened impact timings.
- Background asset: `Assets/Resources/Art/cave_arena_background.png`

## Fixed portrait display

- Windows builds launch in a non-resizable 540x960 window and retain a centered 9:16 camera viewport on displays with a different aspect ratio.
- Mobile builds remain locked to portrait orientation.

## HUD layout

- The minimap was removed to reduce duplicated information and visual clutter.
- The footer now uses the full width for MOVE, TRACE, and DANGER cards plus a live route-rule bar.

## Special tiles

- Four visible one-use effects are displayed as glowing pixel items instead of full-cell paint and are regenerated each round away from the player, START, and END cells.
- `+`: next attack +25 damage; `◆`: next attack x1.35 damage.
- `≈`: movement locked for 1 second while boss timers continue; `▼`: next attack x0.65 damage. The red `×` item no longer spawns during boss combat.

## Phase 2 attack crystals

- Four impassable red crystals appear one cell inside the north, east, south, and west field edges; crystal cells are excluded from START and END selection.
- Each crystal attacks a two-cell checker pattern every 5 seconds after a 0.7-second warning.
- Below 50% phase-two HP, every crystal relocates away from the player, current route, START, and END; two retain a 5-second interval and two accelerate to 4 seconds.
- Phase two cycles full-field horizontal and vertical grid glyphs separately; the overlapping combined grid pattern has been removed.

스와이프로 격자 위에 공격 궤적을 그려 보스를 쓰러뜨리는 세로형 모바일 게임 기본 빌드입니다.

## 플레이 방법

- 모바일: 화면을 상·하·좌·우로 스와이프하면 해당 방향으로 한 칸 이동합니다.
- Unity 에디터: 방향키 또는 WASD도 사용할 수 있습니다.
- 초록색 `S`(START)에서 출발해 주황색 `E`(END)까지 이동합니다.
- 지나온 칸은 하늘색으로 표시되며, 경로가 길수록 공격 피해가 증가합니다.
- 이미 칠한 칸을 다시 밟으면 경로가 즉시 초기화됩니다. 이때 START 타일로 돌아가야 경로 기록이 다시 시작됩니다.
- 공격 성공 후 플레이어는 현재 위치를 유지하며 START와 END 타일만 새 위치로 재배치됩니다.
- 필드 모양 밖으로는 이동할 수 없습니다.
- 플레이어 체력은 1이며 보스 공격에 한 번 맞으면 스테이지가 재시작됩니다.
- 붉게 표시되는 레이저 또는 폭발 영역에서 제한 시간 안에 벗어나야 합니다.

## 실행 방법

1. Unity Hub에서 이 폴더를 Unity `6000.5.3f1`로 엽니다.
2. `Assets/Scenes/SampleScene.unity`를 엽니다.
3. Play 버튼을 누릅니다.

게임 루트와 UI는 런타임에 자동으로 생성되므로 빈 샘플 씬을 별도로 편집할 필요가 없습니다.

## Android APK 빌드

Android Build Support가 설치된 Unity에서 메뉴 `Trace Strike > Build Android APK`를 선택합니다.
결과물은 `Builds/Android/TraceStrike.apk`에 생성됩니다.

## 구현된 기본 빌드 범위

- 세로 화면 및 안전 영역 대응
- 한 번의 스와이프당 상하좌우 한 칸 이동
- 원형 격자 필드로 구성된 첫 번째 스테이지
- 필드 바깥 이동 차단
- START/END/플레이어/현재 경로가 표시되는 미니맵
- 경로 중복 시 초기화 및 START 재진입 규칙
- 경로 길이에 비례하는 피해량과 보스 체력
- 1페이즈: 체력 500, 십자 → 마름모 → X 고정 문양 공격 순환 및 2초 예고
- 1페이즈 체력 소진 시 2페이즈 체력 1500으로 새로운 체력바 생성
- 2페이즈: 체력 1500, 십자 → 마름모 → X → 십자+마름모 이중 문양 및 1초 예고
- 2페이즈 문양 예고 중 현재 플레이어 한 칸을 지정하는 0.65초 보라색 견제 폭발
- 고정 문양마다 최소 한 칸의 인접 탈출 타일 보장
- 보스 공격 예고와 발동 연출 중에도 플레이어 이동 가능
- 보스 체력 50%에서 1.2초 PHASE 2 전환 연출
- 공격 횟수가 늘어날수록 패턴 사이 대기시간 감소(최소 0.45초)
- 보스 격파 시 첫 스테이지 클리어
- 사용자 제공 전사 픽셀아트 플레이어 캐릭터
- 타일 패턴, 배경 광점, 이동 파티클, 공격 슬래시, 타격 흔들림 연출
- 이동, 경계 충돌, 경로 시작·초기화, 공격, 타격, 승리 상황별 합성 효과음

## 주요 파일

- `Assets/Scripts/TrailFieldModel.cs`: 격자, 경로, 이동 규칙
- `Assets/Scripts/TraceStrikeGame.cs`: 모바일 입력, UI, 미니맵, 전투 연출
- `Assets/Editor/TraceStrikeBuildTools.cs`: 모바일 설정 및 APK 빌드 메뉴
- `Assets/Tests/Editor/TrailFieldModelTests.cs`: 핵심 규칙 테스트
