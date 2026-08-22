# 몬스터가 너무 많아

이동하는 거점을 지키며 자원을 수집하고 타워를 배치·강화하는 Unity 3D 타워디펜스 프로젝트입니다.

## 기술 하이라이트

- 비정형 `FootprintMaskSO`를 포함한 다중 셀 타워 배치
- 타워 배치·확장 업그레이드의 원자적 grid reservation
- 중심 건물·타워 연결을 계산하는 grid road system
- ScriptableObject 기반 타워·스킬·스폰 데이터
- 투사체 풀링, 비할당 충돌 검사, 이동형 월드/거점 시스템

## 포트폴리오 핵심 개선

기존에는 타워 데이터가 먼저 저장되고, 실제 셀 점유와 해제는 화면 표현 계층인 `PlacementVisualizer`가 수행했습니다. 월드 뷰가 비활성화되거나 생성에 실패하면 데이터와 그리드가 서로 다른 상태가 될 수 있었고, 다중 셀 점유 중 한 셀만 충돌해도 앞서 점유한 셀이 남을 여지가 있었습니다.

이를 다음 구조로 개선했습니다.

1. `GridSystem.TryOccupyAll`이 범위·중복·충돌을 모두 검사한 뒤 한 번에 반영합니다.
2. `TryReplaceAll`이 기존 footprint와 겹치는 업그레이드는 허용하되, 확장 영역 충돌 시 기존 상태를 그대로 보존합니다.
3. `GridDataService`가 타워별 소유 셀을 기록하고 배치·업그레이드·삭제를 하나의 트랜잭션으로 관리합니다.
4. `PlacementVisualizer`는 데이터 변경을 그리는 역할만 맡습니다.
5. 외부 소유인 중심 건물 셀은 타워 삭제·전체 초기화 시에도 보존됩니다.

상세 설계와 Before/After는 [Docs/GRID_PLACEMENT_TRANSACTION.md](Docs/GRID_PLACEMENT_TRANSACTION.md), 포트폴리오 문안과 예상 질문은 [Docs/TECHNICAL_PORTFOLIO_GUIDE.md](Docs/TECHNICAL_PORTFOLIO_GUIDE.md)를 참고하세요.

## 테스트

Unity Test Framework EditMode 테스트로 다음을 검증합니다.

- 다중 셀 예약의 성공, 범위 초과, 중복, 충돌 시 무변경 보장
- footprint가 커지는 업그레이드의 성공과 충돌 시 rollback
- 뷰가 없는 상태에서도 데이터와 점유 상태가 함께 갱신되는지
- 삭제·전체 초기화가 자신이 소유한 셀만 해제하는지
- 실제 `Assets/0. Scenes/Main.unity`의 missing component 여부

최종 결과는 **12개 전체 통과(실패 0개, 0.738초)**입니다.

검증 환경과 결과 XML은 [Docs/VALIDATION_REPORT.md](Docs/VALIDATION_REPORT.md)에 기록합니다.

## 개발 환경

- 원본 Unity: `2022.3.43f1`
- Render Pipeline: URP 14
- 테스트: Unity Test Framework
