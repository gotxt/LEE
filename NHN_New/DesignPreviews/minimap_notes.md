# 우측 상단 반투명 미니맵

대상: `D:/codex/NHN/NHN_New`만 변경. 원본 `NHN`은 유지.

- 화면 우측 상단에 280×280 UI 단위 정사각형 패널. 1600×900 창에서는 약 233×233픽셀로 표시.
- 상단·우측 여백 각각 28 UI 단위. 카메라 추적이나 화면 흔들림에 따라 패널이 이동하지 않음.
- 어두운 배경 불투명도 30% (기존 55%에서 조정), 일반 맵 셀 48%. 주요 위치 표시는 가독성을 위해 밝게 유지.
- 전체 17×17 기준 221칸 전장, 중앙 피해 구역, 기록한 경로, 시작/종료 위치, 수정과 공격 위험 구역 표시.
- 현재 플레이어는 흰색 마름모, 시작은 초록색, 종료는 주황색, 경로는 청록색.
- 시작/종료 위치는 공격 예고 중에도 식별되도록 우선 표시.
- 플레이어 표시는 실제 화면의 부드러운 이동과 동기화. 추적 카메라와 흔들림 오프셋은 제외.
- 미니맵은 입력을 가로채지 않음. 타이틀과 비활성 허브 화면에서는 숨김.
- 기존 목표 화살표, 맵 크기, 보스 체력 및 전투 규칙은 유지.

구현: `Assets/Scripts/TraceStrikeGame.cs`의 BuildDesktopMinimap, RefreshMinimap, UpdateMinimapPlayer.

진단: `-captureMinimap -validateMinimap`으로 이동 마커와 패널 고정, 정사각형, 투명도, 맵 전체 셀 및 끝점 표시를 확인. 기존 캡처 플래그에 `-validateMinimap`을 추가해 타이틀 숨김과 공격 예고 상태도 점검 가능.

## 이전 55% 버전 검증

- Windows 빌드 성공: `minimap_build_final.log`.
- EditMode 테스트 56개 통과, 실패/생략 0개: `minimap_final_test_results.xml`.
- 이동 마커와 패널 고정 검증 통과: `minimap_play_final_player.log`.
- 전체 221칸·시작/종료 표시·입력 통과·배경 반투명·우측 상단 정사각형 검증 통과.
- 타이틀에서 미니맵 숨김 검증 통과: `minimap_title_final_player.log`.
- 실제 화면 확인: `minimap_play_final.png`, `minimap_telegraph_final.png`, `minimap_title_final.png`.
- 이전 시도 로그/캡처와 구분하여 `_final` 파일을 최종 결과로 사용.

## 배경 30% 변경 검증

- 현재 최신 빌드: `minimap_30_build.log`, Windows 빌드 성공.
- 실제 실행에서 배경 알파 0.30, 마커 이동 및 패널 고정 검증 통과: `minimap_30_player.log`.
- 최종 화면: `minimap_30.png`. 크기·위치·셀 및 주요 마커 투명도는 변경하지 않음.
- 이번 변경은 빌드와 실행 캡처로 확인. 위 56개 테스트 결과는 이전 미니맵 버전의 기록.
