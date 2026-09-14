# 회사폰 로컬 합성 진행도 재시작 검증

2026-09-14, SM_N986N 회사 테스트폰에서 PR162의 별도 진행도 APK를 검증했다. 대상 패키지 설치 경로와 패키지 목록이 모두 비어 있음을 확인한 뒤 교체 옵션 없이 신규 설치했다. 다른 앱을 삭제하거나 덮어쓰지 않았다.

APK 소스는 `e712627`, SHA-256은 `8a09b37cd5cedba53a3b42eda54d1e02d37531b65dda0844f6831bd2abe91ebd`다. 이번에는 코드 변경·재빌드·Editor 재시험을 하지 않았다. Unity 6000.0.81f1 / CLI 1.0.0-beta.8 / ARM64 빌드 근거와 strict RELRO 5건 실패의 한계는 [PR162 검증](progress-only-validation.json)을 유지한다.

| 실행 | 관찰 결과 |
| --- | --- |
| 최초 실행 → LOCAL 모드 | No dedicated test progress saved |
| 단계 1 저장/재읽기 | 단계 1 표시 |
| 단계 2 저장/재읽기 | 단계 2 표시 |
| 대상 앱 force-stop | 대상 PID 없음 |
| 앱 재실행 | No progress session; 자동 인증·저장 시작 없음 |
| LOCAL 재연결 | 단계 2 복원 |
| 검증 종료 force-stop | 대상 PID 없음; 앱과 합성 파일 보존 |

**전용 로컬 파일이 프로세스 재시작 후 유지됨을 확인했다.** 화면의 모드 설명은 LOCAL SYNTHETIC FILE — no PlayFab persistence evidence였다. 공용 probe의 하위 결과 문구에 `Test cloud progress read`가 표시돼도 이번 데이터는 로컬 파일이며 실제 클라우드 결과가 아니다.

Cloud 인증 버튼, CustomID 입력, 계정 생성·연결, Google 계정, 구매·복원은 조작하지 않았다. 외부 서버 인증·저장이나 전체 게임 DataManager 복원을 검증했다고 주장하지 않는다. 패킷 캡처를 수행하지 않았으므로 SDK 전체 트래픽 0이라는 주장도 하지 않는다. 별도 테스트 인증 승인은 계속 필요하다.

화면 6장의 원본은 로컬 `Logs/revival/progress-local-device`에 보존하고 공개 기록에는 해시·관찰 시각만 담았다. 기기 serial이나 사용자 식별자는 공개하지 않았다. [검증 집계](progress-local-device-validation.json)를 통합 담당자에게 전달하고 폰 슬롯을 반환했다.
