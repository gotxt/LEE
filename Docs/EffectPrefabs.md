# 패턴 이펙트 프리팹

기존 `TraceStrikeGame`의 UI 조각 기반 효과를 프리팹으로 사용할 수 있게 분리했다. 새 이미지를 제작한 것이 아니라, 기존 불꽃·흙 파편·먼지·충격파의 색과 운동식을 재사용한다. 기존 전투 코드와 사용자가 편집한 패턴은 그대로 보존한다.

## 폴더

```text
Assets/
├─ Resources/Effects/
│  ├─ Prefabs/Impact/
│  │  ├─ TileImpact.prefab
│  │  ├─ CrystalSparks.prefab
│  │  ├─ DirtLaneEruption.prefab
│  │  └─ DirtAreaExplosion.prefab
│  └─ Audio/
│     ├─ Warning.wav
│     └─ Impact.wav
├─ Scripts/Effects/UiEffectPlayer.cs
└─ Editor/Effects/UiEffectPlayerEditor.cs
```

보스 데이터는 기존 `Resources/Patterns`에 남아 있다. 사운드 두 개만 `Effects/Audio`로 이동했으며 `.meta`의 GUID를 유지해 기존 패턴 참조가 보존된다. `GeneratePatternAudio.ps1`과 새 보스 패턴 생성기도 새 경로를 사용한다. 원본 타일·캐릭터 아트와 다른 게임 폴더는 이동하지 않았다.

## 어떤 프리팹을 선택할까?

| 프리팹 | 효과 | 권장 VFX Duration |
|---|---|---|
| TileImpact | 기존 Impact VFX의 주황색 타일 섬광 | 0.3초 이상 |
| CrystalSparks | 수정 공격에서 사용하던 불꽃 파편 4개 | 0.12초 이상 |
| DirtLaneEruption | 흙먼지 4개와 튀어 오르는 흙·돌 파편 12개 | 0.4초 이상 |
| DirtAreaExplosion | 충격파·먼지 6개·방사형 파편 40개 | 0.5초 이상 |

1. 보스 패턴 에디터 → **고급 이벤트 편집**에서 `Impact VFX`를 선택한다.
2. `Selected Event → Element → Action → Prefab`에 위 프리팹을 드래그한다.
3. **VFX 이벤트의 Duration**을 표에 맞춘다. Damage 이벤트의 판정 시간을 함께 늘릴 필요는 없다.
4. Unity 재생 모드에서 **게임에서 실행**으로 실제 효과를 확인한다.

기존 패턴에 자동으로 프리팹을 덮어쓰지는 않았다. 새로 생성하는 기본 골렘 패턴은 `TileImpact`를 기본 VFX로 참조한다.

## 주의 사항 / 수정

- 대상 **타일마다 한 개씩** 생성된다. 흙 폭발을 넓은 영역 전체에 쓰면 파편이 매우 많아진다. 큰 폭발은 적은 수의 타일에 사용하고, 넓은 영역에는 TileImpact나 CrystalSparks를 권장한다.
- 생성 시 타일 크기에 맞춰 크기가 조정된다. 전체 크기는 프리팹의 **Ui Effect Player → Size Multiplier**에서 조정한다.
- 효과 자체의 수명이 끝나면 사라져 보이며 반복하지 않는다. 이벤트가 먼저 끝나면 중간에 제거된다. 패턴 취소 시에도 생성된 객체가 정리된다.
- 프리팹 Inspector에 권장 재생 시간이 표시된다. 색·파편 개수·수명·운동은 **Emitters**에서 설정한다. 다른 효과를 만들 때는 프리팹을 복제해 수정한다.
- 프리팹이 지정된 VFX에서는 이벤트의 `Sprite`·`Color` 대신 프리팹 내부 설정을 사용한다.
- 이 프리팹은 **Canvas UI용**이다. `Impact VFX`/일반 VFX 이벤트에 사용한다. SpriteRenderer·ParticleSystem 기반의 보스 소켓용 `Boss VFX`와는 용도가 다르다.
- 편집 창의 타일 미리보기는 기존처럼 영역 표시만 한다. 실제 효과는 재생 모드에서 확인한다.
- 순수 시각 효과이며 피해 판정은 포함하지 않는다. 일반 이벤트 확장 구조와 타일 선택 방식은 변경하지 않았다.
