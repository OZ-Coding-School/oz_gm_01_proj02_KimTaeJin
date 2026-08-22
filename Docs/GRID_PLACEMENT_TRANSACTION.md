# 다중 셀 배치 트랜잭션 개선

## 1. 문제 정의

타워 한 개는 1칸만 차지하지 않습니다. 직사각형 footprint뿐 아니라 `FootprintMaskSO`로 정의한 비정형 셀 집합을 점유할 수 있고, 업그레이드 시 footprint가 커질 수도 있습니다. 따라서 배치는 단순한 `Dictionary.Add`가 아니라 다음 상태가 함께 성공하거나 함께 실패해야 하는 트랜잭션입니다.

- anchor cell의 `TowerData`
- `GridSystem`의 전체 occupied cell
- 타워별 소유 셀
- 화면의 배치 인스턴스

## 2. Before

기존 흐름은 다음과 같았습니다.

```text
GridDataService.TryApplyPlacement
  └─ TowerData를 먼저 변경
      └─ OnDataChanged 이벤트
          └─ PlacementVisualizer가 prefab 생성
              └─ footprint를 한 칸씩 TryOccupy
```

이 구조에는 세 가지 문제가 있었습니다.

1. `PlacementVisualizer`가 비활성화되거나 참조가 끊기면 데이터만 저장되고 셀은 점유되지 않습니다.
2. 여러 셀을 순서대로 점유하면서 반환값을 확인하지 않아 중간 충돌 시 부분 점유가 남을 수 있습니다.
3. 삭제·업그레이드의 셀 해제가 `PlacementVisualizer`와 `TowerEntity` 양쪽에 분산되어 상태의 실질적 소유자가 불명확했습니다.

## 3. After

개선 후 흐름은 다음과 같습니다.

```text
GridDataService.TryApplyPlacement
  ├─ placement rule 평가
  ├─ 전체 footprint 계산
  ├─ GridSystem 원자적 예약/교체
  ├─ TowerData + owner map 커밋
  └─ OnDataChanged
      └─ PlacementVisualizer는 표현만 갱신
```

### 원자적 신규 예약

`TryOccupyAll`은 모든 셀에 대해 아래 조건을 먼저 검사합니다.

- 셀 집합이 비어 있지 않은가
- 모든 셀이 grid bounds 안인가
- 중복 셀이 없는가
- 이미 점유된 셀이 없는가

검증을 모두 통과한 뒤에만 `_occupied`를 변경하므로 실패 시 부분 상태가 남지 않습니다.

### 원자적 업그레이드

`TryReplaceAll(currentCells, nextCells)`은 현재 타워가 소유한 셀과 새 footprint가 겹치는 것을 허용합니다. 새로 확장되는 셀 중 하나라도 다른 객체가 점유하고 있으면 기존 셀을 해제하지 않고 실패합니다. 성공할 때만 기존 집합을 제거하고 새 집합을 추가합니다.

### 명시적 소유권

`GridDataService`는 anchor cell별 `Vector2Int[]`를 `_occupiedByTower`에 보관합니다. 삭제 시 footprint를 다시 추측하지 않고 실제 커밋 당시 소유한 셀만 해제합니다. 중심 건물처럼 외부 시스템이 점유한 셀은 건드리지 않습니다.

## 4. 보장하는 불변식

- `TowerData`가 존재하면 그 타워의 전체 footprint가 점유돼 있다.
- 한 타워의 배치·업그레이드는 전부 성공하거나 아무 변화도 만들지 않는다.
- 업그레이드 실패 시 기존 level, definition, footprint는 유지된다.
- 타워 삭제는 해당 타워가 소유한 셀만 해제한다.
- 표현 계층의 활성 여부가 게임 규칙 상태를 결정하지 않는다.
- grid bounds 밖의 셀은 단일 API와 일괄 API 모두 점유할 수 없다.

## 5. 테스트 전략

### GridSystem 단위 테스트

- 전체 셀 동시 커밋
- 기존 점유 셀 충돌 시 부분 예약 없음
- 중복·범위 초과 입력 거부
- 겹치는 셀을 유지하며 footprint 교체
- 확장 충돌 시 기존 예약 보존

### GridDataService 통합 테스트

- `PlacementVisualizer` 없이도 다중 셀 배치가 완결되는지
- 삭제가 중심 건물 셀을 보존하는지
- footprint 확장 업그레이드 성공
- 확장 충돌 시 level·definition·점유 상태 rollback
- 전체 초기화가 타워 소유 셀만 해제하는지

### 씬 무결성 테스트

실제 빌드 대상인 `Assets/0. Scenes/Main.unity`를 열어 missing component가 없는지 검사합니다.

## 6. 트레이드오프와 후속 개선

타워별 셀 배열을 저장하므로 footprint 셀 수만큼 메모리가 추가됩니다. 하지만 일반적인 타워 footprint는 매우 작고, 삭제 시 안전한 소유권 판단과 재계산 제거의 이점이 더 큽니다.

현재 Unity gameplay mutation은 main thread에서 실행된다는 전제를 사용합니다. 멀티스레드 배치나 네트워크 동기화를 도입한다면 reservation token과 명령 순번을 추가하고, 데이터 저장까지 포함한 transaction object로 확장할 수 있습니다.
