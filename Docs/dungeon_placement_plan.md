# Dungeon Placement Plan (Kaykit Dungeon)

본 문서는 `Docs/dungeon_floor_assets.md`의 바닥 크기 정보를 기준으로, 자산 배치 순서/규칙/확률을 정리한 계획서입니다.  
GridMap 기반 절차 생성 파이프라인에 그대로 반영할 수 있도록 작성했습니다.

---

## 1) 바닥 타일 규칙 (크기 기준)

### 크기 기준 요약
- 2x2: `floor_*_small*`, `floor_tile_small*`, `floor_wood_small*`
- 4x4: `floor_*_large*`, `floor_tile_large*`, `floor_wood_large*`, `floor_tile_big_*`
- 8x8: `floor_tile_extralarge_*`

### 기본 배치 규칙
- **룸(방) 내부**: 4x4 타일을 기본 타일로 사용
  - 변형(rocks, broken, weeds)은 10~20% 확률로 섞기
- **복도**: 2x2 타일을 기본 타일로 사용
  - `floor_dirt_small_*` or `floor_tile_small_*` 중 랜덤
- **특수 룸(보스/중앙홀)**: 8x8 타일 1~2장 배치 후 주변을 4x4로 메움
- **트랩 구역**: `floor_tile_*_grate*`, `floor_tile_big_spikes` 사용

### 테마 바닥
- **목재 방**: `floor_wood_*`만 사용 (식당/숙소)
- **기초/바닥 높이**: `floor_foundation_*`는 테두리/단차 표현에만 사용

---

## 2) 벽/경계 규칙 (크기 반영)

### 기본
- 바닥 인접 경계에 `wall` 배치 (기본 4.0 x 1.0 footprint)
- 코너는 `wall_corner` (2.5 x 2.5) 또는 `wall_corner_small` (1.5 x 1.5) 사용
- 막다른 경계는 `wall_endcap` (1.067 x 1.0) 또는 `wall_half_endcap` (2.0 x 1.0)

### 변형
- `wall_cracked`, `wall_broken`는 5~10% 확률로 섞기
- 교차점/중요 구간에 `wall_pillar`, `wall_shelves` 사용

### 개구부
- 방 입구에 `wall_doorway*` 사용
- 특별 방에 `wall_gated`, `wall_arched*`, `wall_window_*` 배치

### 크기 기반 사용 규칙 (Docs/dungeon_wall_assets.md 기준)
- **기본 직선 벽**: `wall` (4.0 x 1.0) → 2x2 바닥 기준으로 2칸 길이 직선에 사용
- **짧은 벽**: `wall_half` (2.0 x 1.0) → 2x2 바닥 1칸 길이에 사용
- **엔드캡**: `wall_endcap` (1.067 x 1.0) → 얇은 끝 마감, `wall_half_endcap`은 1칸 길이 마감
- **코너**: `wall_corner` (2.5 x 2.5), `wall_corner_small` (1.5 x 1.5) → 통로 폭/코너 여유에 맞춰 선택
- **교차**: `wall_crossing` (4.0 x 4.0) → 4방 교차점 전용
- **문형 교차**: `wall_doorway_Tsplit` (8.0 x 2.5) → 넓은 교차 또는 대형 출입구 전용

### GridMap 점유 규칙 요약 (2x2 바닥 셀 기준)

| 벽 자산 | 실제 footprint (X x Z) | 2x2 셀 점유 | 비고 |
|---|---:|---:|---|
| `wall` | 4.0 x 1.0 | 2 x 0.5 | 직선 벽 기본 |
| `wall_half` | 2.0 x 1.0 | 1 x 0.5 | 짧은 벽 |
| `wall_endcap` | 1.067 x 1.0 | 0.5 x 0.5 | 얇은 마감 |
| `wall_half_endcap` | 2.0 x 1.0 | 1 x 0.5 | 짧은 마감 |
| `wall_corner` | 2.5 x 2.5 | 1.25 x 1.25 | 넓은 코너 |
| `wall_corner_small` | 1.5 x 1.5 | 0.75 x 0.75 | 좁은 코너 |
| `wall_crossing` | 4.0 x 4.0 | 2 x 2 | 4방 교차 |
| `wall_doorway_Tsplit` | 8.0 x 2.5 | 4 x 1.25 | 넓은 출입구 |

---

## 3) 계단/레벨

### 사용 시점
- 레벨 전환이 있는 구조에서만 배치
- 계단 시작/끝은 최소 2x2 공간 확보

### 추천 자산
- 기본: `stairs_modular_*`, `stairs_long_*`
- 강조: `stairs_wood_decorated`, `stairs_walled`

---

## 4) 벽 부착 장식

- **배너**: `banner_*`
  - 룸 중앙 벽에 배치, 복도에서는 희소
- **횃불**: `torch_mounted`, `torch_lit`
  - 복도마다 6~10타일 간격
- **무기 장식**: `sword_shield*` (무기고)
- **옵션 토글**: 에디터에서 `EnableWallDecor`가 false면 전혀 배치하지 않음

---

## 5) 가구/소품 룸 타입 매핑

### 창고/물류
- `barrel_*`, `box_*`, `crate*`, `trunk_*`, `shelf_*`, `shelves`

### 식당/주방
- `table_*`, `plate_*`, `bottle_*`, `candle_*`

### 숙소
- `bed_*`, `table_small`, `candle_*`, `bottle_*`

### 보물실
- `chest_gold`, `coin_stack_*`, `banner_*`

### 무기고
- `sword_shield*`, `banner_shield_*`, `wall_shelves`

### 의식/제단
- `candle_*`, `torch_lit`, `banner_pattern*`

### 폐허/전투 흔적
- `rubble_*`, `wall_broken`, `wall_cracked`
- **옵션 토글**: 에디터에서 `EnableProps`가 false면 전혀 배치하지 않음

---

## 6) 배치 확률 가이드 (예시)

### 바닥 변형
- `floor_tile_large_rocks`: 10%
- `floor_tile_small_broken_*`: 10~15%
- `floor_tile_small_weeds_*`: 10%

### 벽 변형
- `wall_cracked` / `wall_broken`: 5~10%
- `wall_window_*`: 룸 경계 5%

### 소품
- 룸당 2~6개 소품 랜덤 배치
- 복도는 최소화 (1~2개 이하)

---

## 7) 배치 순서 (실행 파이프라인)

1. 바닥 채우기 (룸 → 복도 → 특수 룸)
2. 벽 경계 생성 (기본 벽)
3. 코너/엔드캡 교체
4. 문/창/아치 배치
5. 계단 및 레벨 연결
6. 벽 부착 장식
7. 가구/소품 배치
8. 디테일/변형 타일 확률 적용

---

## 8) 구현 시 주의사항

- 바닥 크기와 GridMap 셀 크기 매칭 필수
  - 2x2 타일을 기준 단위로 삼고, 4x4는 2x2 블록으로 계산
- 통로 폭 1.5 타일 이상 확보
- 대형 소품은 벽에서 1타일 이상 거리 확보

---

## 9) 수정 메모

이 문서는 편집용입니다.  
자산 추가/삭제 시 아래를 수정하세요:
- **바닥 그룹(2x2/4x4/8x8)** 목록
- **룸 타입별 자산 매핑**
- **배치 확률 값**
