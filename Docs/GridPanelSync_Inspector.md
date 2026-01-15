# 그리드/패널 동기화 인스펙터 설정

목표: 패널그리드와 게임그리드가 동일 기준으로 동기화되어 1칸도 어긋나지 않게 유지.

## GameRoot (Scene: Main / GameRoot)
- Build Cell Size: 2 (TileReal 기준)
- Build Cell Size Z Scale: 0.8235294 (70/85, Z = X * 비율)
- Auto Build Cell Size: Off
- Build Cell Size Prefab: TileReal (Road 자동 프리팹 fallback용)
- Build Anchor: MainCastle/GridAnchor
- Build Width/Height: 9 / 10
- Build Anchor Offset: (0, 0, 0.8235294) (Height가 짝수일 때 Z 반칸 보정)
- Build Center: On

## RunScope / GridSystem (Scene: RunScope)
- GridSystem
  - Cell Size: 2 (GameRoot에서 복사)
  - Cell Size Z Scale: 0.8235294
  - Width/Height: 9 / 10
  - Anchor: MainCastle/GridAnchor
  - Center On Anchor: On
  - Anchor Offset: (0, 0, 0.8235294)

## BaseFootprintReserver (Scene: RunScope)
- Use Fixed Footprint: On
- Use Footprint Mask: On
- Fixed Footprint Mask: MainCastle Mask (3x3, pivot 1,1)
- Fixed Footprint Size: 3 x 3
- Even Footprint Bias Positive: On

## MainCastle Prefab
- FootprintVisualBaker
  - Mask: MainCastle Mask
  - Use Grid Cell Size: On
  - Cell Size: 2 (Grid 미지정 시)
  - Cell Size Z Scale: 0.8235294 (Grid 미지정 시)
  - Auto Place Grid Anchor: On
  - Create Grid Anchor If Missing: On
  - BaseTilePrefab: TileReal
  - Use Base Tile Bottom Offset: On (TileReal 높이 보정)
  - Normalize Anchor To Cell: On (Wall X 간격 보정)
  - Center Footprint On Root: On
  - AnchorVisual (Gate)
    - Center To Cell: On
    - Rotate Offset: On
- GridAnchor 위치는 Baker가 자동 중앙 배치

## BuildMenuPanel / GridRoot
- PanelGridView
  - Width/Height: 9 / 10
  - Cell Width/Height: 85 / 70
  - Auto Resize: On
  - Auto Fit Cell Size From Rect: Off
  - GridRoot Size: 765 x 700

## PanelPreview3D
- Use Grid Cell Size: On
  - Grid 미지정 시 Cell Size + Cell Size Z Scale을 사용
- Use Grid View Cell Aspect: On
- Use Grid Root Rect For Aspect: Off
- Use Detached Preview Root: On
- Auto Fit RawImage To Grid: On
- Use Orthographic: On
- Exclude Preview Layer From Main Camera: On
- GridSystem: RunScope.Grid
- Center Prefab: MainCastle
- Center Footprint: 3 x 3
- Footprint Node Name: BasePlateBounds
- Preview Layer/Light: Preview layer only
  - Road Tile Prefab: TileReal (필수)
  - Road Match Grid Tile Settings: On (권장)
  - Road Use Bottom Offset: On (권장)
  - Road Tile Y Offset/Scale/Grid Offset: 비움 (Match Grid 사용 시 무시됨)
  - Match Placed To Road Height: On (권장, 패널에서 타워/센터 TileReal 높이 맞춤)
  - Match Center To Road Height: On (권장)
  - Placed Preview Y Offset: 0 (Match Off일 때만 사용)

## GridRoadSystem (Scene: RunScope)
- GridRoadSystem 컴포넌트 추가 (RunScope에 권장)
- Grid: 비움 (RunScope.Grid 자동)
- BaseFootprint: 비움 (RunScope.BaseFootprintReserver 자동)
- Road Tile Prefab: 비워도 됨 (GameRoot.BuildCellSizePrefab에 TileReal 할당 시 자동)
- Road Root: 비움 (자동 생성)
- Road Tile Y Offset: 0 (권장)
- Road Tile Grid Offset: 0,0 (권장)
- Normalize Tile To Cell: On (권장, Z 스케일 보정)
- Center Tile To Cell: On (권장)
- Tile Scale Multiplier: 1 (기본)
- Use Bottom Offset: On (권장)
- Use Build Cell Prefab Fallback: On (권장)

## 타워 프리팹 (예: Cannon_1) 기본 세팅
### 1) TowerEntity (Cannon_1 루트)
- Rotate To Target: On
- Yaw Pivot: Cannon_1_Gun (빈 오브젝트, 회전 0/0/0 권장)
- Muzzle: ShootPoint
- 메시가 -90도 회전이 필요하면 그 회전은 시각 메시 쪽에만 적용하고, Pivot은 0 회전 유지 권장

### 2) 구조 권장
- Cannon_1 (루트): Position 0,0,0 / Rotation 0,0,0 / Scale 1,1,1 권장
- Cannon_1_VisualRoot (자식): 원하는 스케일 (예: 2,2,2)
- Cannon_1_Base, Cannon_1_Mid: VisualRoot 아래
- Cannon_1_Gun (Yaw Pivot): VisualRoot 아래, scale 1,1,1, rotation 0
- Cannon_1Gun_Visual: Cannon_1_Gun 아래, 필요 시 X -90
- ShootPoint: Cannon_1_Gun 아래, 총구 위치

### 3) TowerDefinitionSO
- Prefab: Cannon_1 (TowerEntity)
- Footprint: 1x1 또는 실제 점유 크기에 맞게 설정
- Footprint Mask: 비정형이면 FootprintMaskSO 지정 (FootprintVisualBaker와 동일하게)

### 4) (선택) FootprintVisualBaker로 TileReal 생성
- Mask: Tower용 FootprintMaskSO (필수)
- BaseTilePrefab: TileReal
- Use Base Tile Bottom Offset: On (TileReal 높이 보정)
- Center Footprint On Root: On (타워는 루트 기준 중앙 정렬)
- Normalize Tile To Cell: On
- Build Bounds Tiles: On (PanelPreview3D 정렬 기준)
- Center Tile To Cell: On
- Tile Scale Multiplier: 1
- Build Bounds Tiles: On (Footprint Node Name이 BasePlateBounds일 때 필요)
- Hide Bounds Tiles: On
- Visual Root: Cannon_1_VisualRoot (자동 높이 맞춤 필요 시)
- Use Grid Cell Size: On + Grid: RunScope/Grid
  - 프리팹 베이크는 "씬에 임시 배치 → Grid 지정 → Bake To Prefab" 순서 권장

## 메모
- 배치 좌표는 base footprint 기준 앵커로 계산됨.
- 비율 변경 시 MainCastle과 타워의 FootprintVisualBaker에서 Rebuild/Bake로 BasePlate/Anchor/Bounds를 갱신.
- GridAnchor나 AnchorVisual 변경 후에도 반드시 Rebuild 필요.
- 길 생성 규칙: 새 타워는 "중심건물 + 먼저 배치된 포탑" 중 가장 가까운 대상에 L자(맨해튼) 경로로 연결(대각선 경로 없음). 기존 길은 유지됨.

## 새 대화 템플릿 (Codex 지침)

```text
[프로젝트 개요]
- Unity 2022 LTS 3D / URP
- 장르: Top-Down Defense / Survival
- 레퍼런스: Monsters are Coming
- Cinemachine (Perspective Camera) 사용
- 작업 언어: C#(.cs) 위주

[핵심 제약]
- 프리팹 구조 파괴 금지
- 기존 필드명 변경 금지
- 자동화는 "추가만", 강제 변경 금지
- 어떤 작업이든 필요 시 반드시 사전 허가 요청

[작업 방식]
- 기존 로직/구조 재사용 (중복 구현 금지)
- EditorWindow는 FootprintVisualBaker 내부 함수 호출만 (로직 중복 금지)
- 필요한 수동 입력 최소화, 나머지 자동 생성/자동 할당

[작업 진행도 보고 규칙 (중요)]
- 내가 "작업 진행도"를 물으면 아래 규격 그대로 보고
- 각 항목 체감 충족도(0~5) + 현재 상태
  0/5: 손도 안 댐
  3/5: 구조 잡힘 / 일부 동작
  5/5: 목표 충족 + 재사용 가능
- 보고 형식 예시:
  1) 항목명 - n/5 : 상태 요약
  2) 항목명 - n/5 : 상태 요약
  3) 항목명 - n/5 : 상태 요약

[현재 진행 항목]
1) FootprintVisualBaker 자동화 (현재 체감 충족도: 0/5)
- 목표: 필수 항목만 수동 입력, 나머지 자동 생성/자동 할당, 원클릭 Rebuild
- 절대: 기존 구조 파괴 금지, 프리팹/씬 둘 다 대응
- 필수 수동 항목(최소 안전선)
  - FootprintMaskSO (Mask)
  - BaseTilePrefab
  - VisualRoot (필요 시 자동 높이 보정)
  - (선택) Grid -> 씬 임시 배치 후 Bake 시 사용
- 자동화 대상
  - Bounds 생성/갱신
  - Anchors 자동 생성
  - GridAnchor 생성 + 중앙 정렬 기본값 자동 세팅
  - Anchor Offset Normalize / Centering
- 구현 방향 (정확히 할 일)
  - FootprintVisualBaker에 필수 누락 체크 로직 추가
  - 누락 시 Rebuild 비활성화 + 경고 표시
  - EnsureStructure() 추가: BasePlateBounds/BaseAnchors/GridAnchor 없으면 생성, 있으면 재사용
  - AutoAssignDefaults() 추가: Normalize Anchor To Cell + Center Footprint On Root
  - Rebuild() 전 필수 체크 + EnsureStructure + AutoAssignDefaults
  - Editor 버튼 제공: "Auto Setup + Rebuild"

2) 전용 EditorWindow (현재 체감 충족도: 0/5)
- 목표: Baker 자동화 로직 재사용, 다수 타워 프리팹 일괄 관리
- EditorWindow 이름: TowerToolsWindow
- 탭 구성
  - Tab 1: Baker 자동화
    - 드래그&드롭 등록
    - "Auto Setup + Rebuild" 버튼
  - Tab 2: Batch Rebuild
    - 등록된 모든 프리팹 일괄 Rebuild
    - 실패 프리팹 목록 표시
- 핵심 원칙
  - FootprintVisualBaker 내부 함수 재사용
  - EditorWindow는 호출만, 로직 중복 금지

3) 타워 업그레이드 설계 (현재 체감 충족도: 0/5)
- 목표: 동일 타워 중첩 시 업그레이드, 총 3단계, 비용 없음
- 데이터 구조
  - TowerDefinitionSO에 UpgradeNext 참조 추가
  - Stage 1 -> Stage 2 -> Stage 3 체인
- 배치 로직
  - 동일 위치 기존 타워 탐색
  - TowerDefinitionSO 비교로 동일 타워면 기존 타워 제거
  - UpgradeNext 프리팹 배치
- 업그레이드 조건
  - 비용 없음
  - 최종 단계 도달 시 업그레이드 불가
- UI (선택)
  - 배치 시 "업그레이드 예정" 간단 표시

[최종 지침]
- 구조 깨지지 않게 수정
- .cs 위주 변경
- 자동화는 추가만, 강제 변경 금지
```
