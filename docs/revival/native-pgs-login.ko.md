# Google Play 서버 인증 로그인 전환

## 변경 범위

기존 Google ID 기반 이메일과 공통 비밀번호 로그인·등록을 제거했다. 캐시된 Google ID는 현재 인증 ID와의 연속성 비교에만 사용하며, 인증 실패를 대신하지 않는다. Android에서는 GPGS 인증 후 `RequestServerSideAccess(false, ...)`로 받은 새 서버 코드를 `LoginWithGooglePlayGamesServices`에 한 번 전달한다. `CreateAccount=false`이며 자동 연결, ForceLink, 자동 등록, 기존 이메일 인증 fallback은 없다.

서버 코드 교환은 자동 재시도 래퍼를 사용하지 않는다. 사용자가 재시도하면 새 GPGS 인증/코드 발급부터 시작한다. 각 요청은 독립적인 PlayFab 인증 컨텍스트를 사용하고, 삭제 epoch·취소·세션 세대·콜백 만료 검사를 유지한다. 지연 응답이 공용 게임 세션을 다시 열지 못하도록 한다.

반환 PlayFabId는 이미 알려진 메모리 계정 ID와 일치해야 한다. 이후 `BeginAccountSession`이 저장 데이터 소유자를 검사한 다음에만 공용 인증 컨텍스트를 갱신한다. 소유자를 알 수 없으면서 과거 로그인 모드·Google ID·진행 데이터가 남아 있으면 계정 복구 안내로 중단한다. 깨끗한 설치에서도 이미 native PGS에 연결된 계정에만 로그인할 수 있다.

명시적으로 저장된 Android device/CustomID 익명 로그인 경로는 유지했다. GPGS 인증 실패로 익명 계정을 자동 선택하지 않는다. Editor의 공통 시험 계정 자동 로그인도 제거했으며 격리 하네스를 사용하도록 안내한다.

## 검증 기록

- checkout: `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`
- 기준 main: `d895c1229e0d4fd7e0c0046c87544f931a5b00a8`
- 검증한 코드 커밋: `a688572d1771a102ad677afa892a64022cb56791` (검증 당시 동일 소스의 미커밋 상태)
- Unity: `6000.0.81f1`; CLI: `1.0.0-beta.8`; GPGS: `2.2.1`; IAP: `5.4.3`
- Android 기준: min SDK 24 / target SDK 36 / ARM64 / NDK `27.2.12479018`. 이번에는 Android 패키징을 실행하지 않았다.
- 비공개 에셋 검증: 4,561개 일치, 복사 0개, 서비스 설정 제외.
- `RevivalLoginContinuityTests` EditMode 필터 1회: **60/60 통과**, 실패 0, 건너뜀 0, CLI exit 0.
- XML: `Logs/revival/native-pgs-login-0921.xml`; SHA-256: `373aa7287ea80f9d921167c7a8198967f250d36a0f45210465a75501afe09a0f`.
- 현재 인증 없는 캐시 ID, 계정 불일치, 소유자 없는 기존 상태, 코드 누락, 계정 생성 금지, 독립 인증 컨텍스트, 기존 익명 연속성·취소·중복 콜백을 검사했다. 실제 서비스 호출은 하지 않았다.
- 본인 checkout Editor 정상 종료(PID 0) 및 ProjectSettings 스냅샷 복원 확인.
- APK SHA-256: 해당 없음(이번 변경 APK 미생성). XML·원시 로그·비공개 설정은 커밋하지 않는다.

## 미검증 및 별도 선행 작업

EditMode 결과는 Android 전용 코드의 플레이어 빌드나 실제 GPGS→PlayFab 인증 성공을 증명하지 않는다. OAuth 설정, 실제 Android 실행, 기존 계정 복구·이전은 미검증이다. Web OAuth client ID가 GPGS와 PlayFab Google 설정에 맞게 구성돼야 하며 secret은 서버 설정에만 둔다. 설정 누락·인증 실패·미연결 계정은 오류 안내 후 중단한다. 설정값을 코드나 로그에 추가하지 않았다.

native PGS 로그인은 과거 생성 이메일 계정을 자동으로 찾아 연결하지 않는다. 기존 사용자의 무중단 로그인이나 기존 No Ads 권한의 이전 완료를 주장하지 않는다. 신뢰할 수 있는 별도 소유 증명과 명시적 계정 매핑 없이 자동 연결하지 않으며, 기존 데이터와 권한을 삭제하거나 새로운 계정으로 대체하지 않는다.

클라이언트에서 공통 비밀번호를 제거해도 이미 존재하는 서버 자격증명은 폐기되지 않는다. 과거 자격증명 폐기와 외부 OAuth 구성, 검증된 소유자 기반 이전 절차는 별도 승인·실행 대상이다. 실제 로그인, 계정 생성/연결, Console 변경, 구매/복원, 배포는 이번에 수행하지 않았다.

## API 근거

- [PlayFab native PGS 로그인 요청](https://learn.microsoft.com/en-us/rest/api/playfab/client/authentication/login-with-google-play-games-services?view=playfab-rest)
- [Unity Google 로그인 구성](https://learn.microsoft.com/en-us/xbox/playfab/identity/player-identity/platform-specific-authentication/google-sign-in-unity)
- [Google Play Games 서버 인증](https://developer.android.com/games/pgs/android/server-access)

문서 예제의 자동 계정 생성이나 인증 코드 로그 출력은 적용하지 않았다.
