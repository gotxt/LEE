# 둥근 맵 · 새 사각 타일 적용

> 이 문서는 11×11 버전 기록입니다. 현재 17×17 확장 맵과 근접 카메라 설정은 `close_camera_notes.md`를 참고하세요.

- 적용 프로젝트: `D:\codex\NHN\NHN_New` (기존 `NHN` 프로젝트는 변경하지 않음).
- 논리 맵: 11×11, 이동 가능한 89칸. 각 행의 칸 수는 3 / 7 / 9 / 9 / 11 / 11 / 11 / 9 / 9 / 7 / 3.
- 바닥 이미지: 최신 첨부 파일을 그대로 복사한 `Assets/Resources/Art/rounded_square_floor_tile.png`.
- 화면 표현: 상하좌우 사각 격자로 배치. 원본의 투명 여백은 Sprite 영역에서 제외하고, 인위적인 측면·음영 레이어 없이 첨부 타일의 테두리를 그대로 사용.
- 중앙 고대미지 구역, 경로 색상, 시작 검 / 종료 깃발, 보스 오른쪽 배치 및 각 페이즈 체력 150은 유지.
- 앞서 요청한 PC 확대 시점은 유지: 기존 셀 표시 크기의 1.62배, 캐릭터를 부드럽게 따라가는 화면 이동.
- 테스트용 `-captureArenaOverview`는 전체 배치를 보기 위해서만 축소하며 실제 플레이 카메라에는 영향을 주지 않음.

## 실행

`Builds/Windows/TraceStrike.exe`를 실행합니다. 실행 파일만 이동하지 말고 같은 폴더의 데이터 및 DLL도 함께 유지해야 합니다.

Unity에서는 `Assets/Scenes/SampleScene.unity`를 열고 Play로 확인합니다.

## 현재 사각 타일 검증 결과

- Windows 16:9 빌드 성공 (`square_tile_build.log`).
- Unity EditMode `TrailFieldModelTests`: 35개 통과, 실패 0개 (`square_tile_test_results.xml`).
- 사각 격자의 두 칸 이동 후 카메라 추적·화면 흔들림 복귀 검증 통과 (`square_follow_player.log`).
- `square_tile_overview.png`: 전체 맵 형태 확인용 축소 화면.
- `square_tile_follow.png`: 실제 확대·추적 시점.
- `square_tile_telegraph.png`: 사각 타일에 맞춘 공격 예고 확인 화면.

## 이전 입체 타일 버전 검증 기록

- Windows 16:9 빌드 성공 (`rounded_arena_build.log`).
- Unity EditMode `TrailFieldModelTests`: 35개 통과, 실패 0개 (`rounded_arena_test_results.xml`).
- 실행 빌드에서 두 칸 이동 후 카메라 추적 및 화면 흔들림 복귀 검증 통과 (`rounded_follow_player.log`).
- `rounded_arena_overview.png`: 전체 외곽 확인용 축소 화면.
- `rounded_arena_follow.png`: 실제 확대·추적 시점.
