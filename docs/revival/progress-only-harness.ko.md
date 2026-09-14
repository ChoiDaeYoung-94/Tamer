# 결제 없는 별도 진행도 검증 앱

2026-09-14. PR161의 전용 키 probe를 회사폰 일반 검증과 IAP 세션에서 분리했다. 패키지는 `com.AeDeong.MonsterTamer.revival.progress`, 진입은 게임 동작이 없는 기존 smoke 씬과 `TAMER_PROGRESS_HARNESS` bootstrap이다. 일반 플레이어에는 harness/probe가 포함되지 않는다. 기존 IAP 앱과 설치·저장 경로를 공유하지 않는다.

## 실행 모드

앱 시작은 인증이나 저장을 자동 실행하지 않는다. 사용자가 로컬 모드 또는 별도 승인된 테스트 인증을 명시적으로 선택한다.

- **로컬 합성:** 새 패키지의 persistentDataPath 안 `ProgressProbeSyntheticV1.txt` 하나만 사용한다. 기존 `IapTest`, `PlayerData.json`, 설치 identity, No Ads 데이터에 접근하지 않는다. 파일 저장·재읽기에 기존 ServerManager 큐와 probe의 값 검사를 사용하지만 서버 응답은 대역이다. 패널에서 LOCAL SYNTHETIC FILE로 표시하며 실제 PlayFab 복원 근거가 아니다.
- **테스트 PlayFab:** `12B656`에 새로 준비된 `progress-probe-<32자리 소문자 GUID>` 식별자만 수동 입력받는다. instance API의 `LoginWithCustomID(CreateAccount=false)`를 명시 버튼에서 호출한다. 자동 계정 생성이나 계정 생성 정책 변경이 없다. 인증 후 `RevivalProgressProbeV1` 단일 키만 Private patch/read한다. 세션 교체·disconnect·종료 후 늦은 결과는 거부한다.

전용 패키지 guard를 적용하며 progress player에서는 IAP 패키지를 허용하지 않는다. 기존 ServerManager 큐·재시도·시간 제한·Private 요청 생성기를 공유한다. 실제 게임 DataManager 저장/복원 경로 전체를 검증한 것은 아니다. 식별자 입력은 화면에서 마스킹하며 제출 후 비운다. 계정/세션 티켓을 로그·파일·공개 문서에 기록하지 않는다.

## 구매와 운영 데이터 경계

게임 Managers를 생성하지 않으며 상점·구매·복원·Google 계정 연결 코드는 실행하지 않는다. 빌드에서 IAP/UGS 자동 초기화 설정이 꺼져 있는지 검사한다. Android manifest는 BILLING, 광고 ID 권한 및 광고 초기화 provider를 제거한다. 명시 PlayFab 요청용 INTERNET은 유지한다. SDK 코드가 프로젝트에 존재한다는 사실과 해당 기능 초기화는 구분한다.

운영 타이틀, 기존 IAP 계정과 구매, 개인폰 앱·데이터를 변경하지 않는다. 회사폰에 별도 앱을 설치·사용하는 것은 통합 슬롯 배정 후 진행하며, 이 PR에서는 기기 조회·설치를 하지 않았다. 민감한 외부 인증은 일반 격리 앱 승인과 별개다.

## 실제 서버 검증 전에 필요한 최소 작업

현재 외부 계정 연결·생성·로그인·데이터 쓰기는 0회다. root에 다음 지원 경로를 보고했으며 실제로 실행하지 않았다.

1. 담당자가 테스트 title에 진행도 검증 전용 플레이어를 별도로 준비하고 새 namespace의 CustomID를 수동 연결할지 결정한다. 기존 구매 계정을 재사용하지 않는다.
2. 승인된 담당자가 새 테스트 식별자를 전용 앱에 수동 입력한다. 과거 CustomID나 파일을 읽어 전달하지 않으며, 브라우저·클립보드·서버 등을 통한 보안 거절 우회를 하지 않는다.
3. 명시 로그인 → 전용 키 읽기 → 단계 1 저장/재조회 → 단계 2 저장/재조회 → 앱 재시작 후 같은 승인 식별자로 다시 인증·조회한다. 생성 금지 정책 때문에 미등록 식별자는 실패해야 한다.
4. 다른 계정의 실제 경계 검증은 추가 전용 계정 준비를 승인받았을 때만 진행한다. 코드의 합성 세션 교체 시험을 실제 계정 간 서버 접근 검증으로 확대하지 않는다.

지금 가능한 작업은 계정 없는 로컬 합성 시험이다. 실제 서버 시험에 필요한 별도 계정 준비가 승인되지 않으면 그 부분은 미검증으로 남긴다. 정책 확대나 자동 재가입으로 해결하지 않는다.

## 빌드와 근거

`RevivalProgressBuild.BuildAndroid`가 별도 패키지, debug 서명, ARM64 IL2CPP 개발 APK를 만든다. 기존 ProjectSettings와 임시 manifest/광고 설정은 빌드 후 복원한다. 빌드는 글로벌 harness define이 있으면 거부하고 `extraScriptingDefines`만 사용한다.

Editor 회귀와 APK 정적 확인은 [검증 기록](progress-only-validation.json)에 기록한다. 기기 실행·실제 서버 지속 저장·실제 인증 성공은 코드 시험이나 APK 생성만으로 완료 처리하지 않는다.

소스 `e712627`에서 Editor 400/400 통과, 자산 4561개 확인, GUID 132개 파일 미해결 0개다. APK는 93,329,500 bytes이며 SHA-256은 `8a09b37cd5cedba53a3b42eda54d1e02d37531b65dda0844f6831bd2abe91ebd`다. min SDK 24 / target SDK 36 / ARM64 / debug 서명을 확인했다. 최종 manifest의 BILLING·광고 ID 권한과 광고 초기화 provider는 없다. 기본 LOAD/ZIP 검사는 통과했지만 strict RELRO의 종료 정렬 조건은 5개 라이브러리에서 실패했다. 이를 16 KB 기기 실행 성공 또는 스토어 승인으로 확대하지 않는다.

생성된 IL2CPP 코드에서 ProgressHarness/Probe 포함과 IapHarness/Isolation 제외를 확인했다. 빌드가 끝난 뒤 Editor 0개, ProjectSettings 스냅샷과 생성 에셋 변경 복원을 확인했다. 로컬 합성 파일의 실제 프로세스 재시작 보존도 아직 기기에서 실행하지 않았다.
