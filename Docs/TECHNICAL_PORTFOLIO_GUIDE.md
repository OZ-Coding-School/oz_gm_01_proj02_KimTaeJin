# 기술 포트폴리오 작성 가이드

## 프로젝트 대표 주제

**비정형 다중 셀 타워 배치의 원자적 트랜잭션과 상태 소유권 분리**

동방 프로젝트의 오브젝트 풀링, 롤토체스 프로젝트의 전투 명령/리플레이와 겹치지 않는 주제입니다. 타워디펜스 장르의 핵심 규칙인 배치 정확성을 다루고, 단순 기능 구현보다 상태 일관성과 실패 안전성을 보여줄 수 있습니다.

## 한 문단 요약

Unity 타워디펜스에서 비정형 footprint 타워의 데이터 저장과 grid 점유가 표현 계층에 분산돼, 뷰가 비활성화되거나 업그레이드 확장 셀이 충돌하면 상태가 불일치할 수 있는 구조를 개선했습니다. 전체 셀의 bounds·중복·충돌을 선검증한 뒤 한 번에 반영하는 `TryOccupyAll`과 기존 footprint를 보존하며 교체하는 `TryReplaceAll`을 구현하고, `GridDataService`가 타워별 셀 소유권을 관리하도록 변경했습니다. 이를 통해 배치·업그레이드·삭제가 뷰와 무관하게 전부 성공하거나 전부 실패하도록 만들고, 단위·통합 테스트와 실제 메인 씬 무결성 검사로 검증했습니다.

## 포트폴리오 본문 순서

### 1. 장르와 문제 상황

- 이동하는 거점을 방어하며 타워를 배치·업그레이드하는 3D 타워디펜스
- 1x1뿐 아니라 직사각형·비정형 mask와 확장 업그레이드를 지원
- 한 타워의 논리 상태가 여러 셀과 GameObject에 걸쳐 존재

### 2. Before 구조

| 항목 | 기존 상태 | 위험 |
|---|---|---|
| 데이터 저장 | `GridDataService`가 먼저 저장 | 이후 prefab 생성 실패를 알 수 없음 |
| 셀 점유 | `PlacementVisualizer`가 셀별 호출 | 뷰 비활성 시 누락, 중간 실패 시 부분 점유 |
| 삭제/업그레이드 | visualizer와 entity에 분산 | 이중 해제와 책임 불명확 |
| 범위 검증 | 일괄 커밋 API 없음 | 호출부마다 검증 품질이 달라짐 |

### 3. 해결 설계

- `GridSystem`: 원자적 셀 집합 예약과 교체
- `GridDataService`: tower data와 owner map의 authoritative owner
- `PlacementVisualizer`: 이벤트를 받아 prefab을 그리는 projection
- `TowerEntity`: 정상 경로에서는 data service에 삭제를 위임

### 4. 실패 시나리오

포트폴리오에서는 성공 사례보다 다음 실패 사례를 보여주는 편이 좋습니다.

```text
현재 Lv.1 footprint: (2,3)
Lv.2 확장 예정:      (2,3), (2,4)
다른 타워 점유:              (2,4)

결과: 업그레이드 실패
보존: Lv.1 데이터 + (2,3) 점유
```

### 5. 검증 자료

- `GridSystemTransactionTests`: 원자성·경계·중복·충돌
- `GridDataServiceTransactionTests`: 뷰 없는 통합 흐름·업그레이드 rollback·소유권
- `MainSceneIntegrityTests`: 실제 메인 씬 missing component 검사
- `Docs/TestResults/editmode-results.xml`: Unity Test Runner 원본 결과

## 코드 설명 순서

면접이나 발표에서 다음 순서로 파일을 보여주면 흐름이 자연스럽습니다.

1. `GridDataService.TryApplyPlacement`
2. `GridSystem.TryOccupyAll`
3. `GridSystem.TryReplaceAll`
4. `_occupiedByTower`와 `TryRemove`
5. 점유 책임이 제거된 `PlacementVisualizer.Placement`
6. 충돌 실패를 검증하는 테스트

## 예상 질문과 답변 방향

1. **Unity는 main thread인데 왜 트랜잭션이 필요한가요?**
   동시성만이 아니라 여러 상태를 함께 변경하는 중간 실패가 문제입니다. 이벤트 구독 해제, prefab 누락, upgrade footprint 충돌도 원자성이 필요합니다.

2. **검증 후 커밋 사이에 상태가 바뀌면 어떻게 하나요?**
   `TryOccupyAll`과 `TryReplaceAll`이 커밋 직전에 자체 검증을 다시 수행하므로 호출부의 preview 평가만 신뢰하지 않습니다.

3. **왜 rollback 코드를 쓰지 않고 선검증했나요?**
   셀 집합은 작고 검증 비용이 낮습니다. 변경 후 되돌리기보다 변경 전에 전체 조건을 확인하는 편이 예외 경로와 복구 버그를 줄입니다.

4. **왜 `PlacementVisualizer`가 점유하면 안 되나요?**
   visualizer는 꺼질 수 있는 표현 계층입니다. 게임 규칙 상태의 생명주기와 렌더링 생명주기를 결합하면 headless test와 저장/로드도 불안정해집니다.

5. **타워별 소유 셀을 왜 따로 저장하나요?**
   definition이 바뀌거나 mask가 수정돼도 커밋 당시 실제 소유 셀만 정확히 해제하기 위해서입니다.

6. **업그레이드 시 자기 셀과 겹치는 것은 어떻게 처리하나요?**
   `currentCells`를 허용 집합으로 만들고 새 footprint의 기존 점유가 그 집합에 포함될 때만 허용합니다.

7. **업그레이드가 실패하면 무엇이 보존되나요?**
   기존 occupied set, tower id, level, owner map이 모두 변경되지 않습니다.

8. **중심 건물 셀까지 같이 지워질 위험은 없나요?**
   `ClearAll`과 `TryRemove`는 `_occupiedByTower`에 기록된 셀만 해제하므로 외부 예약을 보존합니다.

9. **중복 셀 입력을 왜 거부하나요?**
   동일 셀 중복은 잘못된 mask나 호출부 오류를 숨기고 소유 셀 수·해제 의미를 모호하게 하므로 invalid transaction으로 취급합니다.

10. **시간 복잡도는 어떻게 되나요?**
    footprint 크기를 F라 하면 검증과 커밋은 평균 O(F)이고, `HashSet` membership은 평균 O(1)입니다.

11. **메모리 비용은요?**
    타워마다 실제 footprint의 `Vector2Int[]`를 하나 보관합니다. footprint가 작은 장르 특성상 비용은 제한적이며 안전한 소유권이 더 중요합니다.

12. **이 구조가 저장/로드에는 어떤 도움이 되나요?**
    load 시 data service가 definition에서 footprint를 계산해 같은 transaction API로 복구할 수 있고, visualizer는 이후 projection만 재생성하면 됩니다.

13. **이벤트 리스너에서 예외가 나면 어떻게 되나요?**
    authoritative data와 occupancy는 이벤트 전에 이미 일관된 상태로 커밋됩니다. 표현 복구는 이벤트 재발행이나 전체 rebuild로 처리할 수 있습니다.

14. **멀티플레이로 확장하면 무엇을 추가하겠나요?**
    placement command ID, server-authoritative sequence, reservation version을 추가해 낙관적 동시성 검사를 하겠습니다.

15. **Unity 6 검증에서 샘플을 제외한 이유는 무엇인가요?**
    원본은 Unity 2022.3 프로젝트이며, 포함된 URP 14 예제 하나가 Unity 6 API와 호환되지 않았습니다. 제품 코드와 테스트를 검증하기 위해 임시 복사본에서 예제만 제외했고 원본은 수정하지 않았습니다.

16. **다음 개선 우선순위는 무엇인가요?**
    owner ID를 `GridSystem`까지 전달해 셀 자체가 소유자를 추적하도록 만들고, save/load round-trip과 scene PlayMode 테스트를 추가하겠습니다.
