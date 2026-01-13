# 그리드/패널 동기화 인스펙터 설정

목표: 패널그리드와 게임그리드가 동일 기준으로 동기화되어 1칸도 어긋나지 않게 유지.

## GameRoot (Scene: Main / GameRoot)
- Build Cell Size: 2 (TileReal 기준)
- Build Cell Size Z Scale: 0.8235294 (70/85, Z = X * 비율)
- Auto Build Cell Size: Off
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
