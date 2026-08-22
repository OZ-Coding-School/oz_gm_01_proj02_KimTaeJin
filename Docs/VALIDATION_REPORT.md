# 검증 보고서

## 최종 결과

| 항목 | 결과 |
|---|---:|
| 테스트 플랫폼 | EditMode |
| 전체 테스트 | 12 |
| 통과 | 12 |
| 실패 / 건너뜀 | 0 / 0 |
| 실행 시간 | 0.738초 |
| 제품 코드 컴파일 오류 | 0 |

Unity Test Runner가 생성한 원본 NUnit XML은 [TestResults/editmode-results.xml](TestResults/editmode-results.xml)에 보관합니다.

## 검증 범위

- `GridSystemTransactionTests` 6개: 범위·중복·충돌 검증, 원자적 예약과 footprint 교체
- `GridDataServiceTransactionTests` 5개: 뷰 없는 배치, 삭제 소유권, 업그레이드 성공과 rollback, 전체 초기화
- `MainSceneIntegrityTests` 1개: 실제 `Assets/0. Scenes/Main.unity`의 missing component 검사

## 실행 환경

- 원본 프로젝트 버전: Unity `2022.3.43f1`, URP 14
- 검증 에디터: Unity `6000.3.7f1`
- 실행 방식: 원본 저장소를 변경하지 않은 임시 복사본에서 Unity Test Framework EditMode 실행
- 결과 시각: 2026-08-22 22:19 KST

## 환경 차이와 알려진 경고

원본에 포함된 `Assets/Samples/Universal RP/14.0.10`의 DepthBlit 예제는 Unity 6에서 바뀐 렌더링 API와 호환되지 않아 `Material`/`Shader` 타입 오류를 냈습니다. 이 코드는 게임 제품 코드가 아닌 Unity 2022용 패키지 샘플이므로, 검증용 임시 복사본에서 `Assets/Samples`만 제외했습니다. 원본 저장소의 샘플은 수정하거나 삭제하지 않았습니다.

또한 원본 에셋 팩 사이에 중복 GUID 경고가 있으며, Unity 6에서 obsolete API 및 headless 렌더링 경고가 발생합니다. 테스트와 개선 대상 제품 코드의 컴파일 및 실행 결과에는 영향을 주지 않았지만, Unity 버전 업그레이드 시에는 샘플 재가져오기와 GUID 정리가 필요합니다.

## 재현 명령 예시

```powershell
Unity.exe -batchmode -nographics `
  -projectPath <validation-copy> `
  -runTests -testPlatform EditMode `
  -testResults <validation-copy>\editmode-results.xml `
  -logFile <validation-copy>\editmode.log
```

Unity 2022.3 LTS에서 실행할 때는 원본의 URP 14 샘플을 제외할 필요가 없습니다.
