# 수동 PGS 시험 로그인 구성

시험 대상은 PlayFab `12B656`, 앱 `com.AeDeong.MonsterTamer.revival.pgs`로 고정한다. 기존 IAP 하네스와 Login.cs는 수정하지 않는다. 게임 MonoBehaviour가 없는 기존 RevivalSmoke 씬에 PGS 시험 컨트롤만 추가하며, `TAMER_REVIVAL_SMOKE`와 `TAMER_PGS_HARNESS`는 빌드 한정 define이다.

앱 시작 시 인증하지 않는다. 명시적 버튼 입력에만 Google 인증 → 새 서버 코드 → 시험 title native PGS 로그인(CreateAccount=false)을 한 번 실행한다. 중복·늦은 콜백은 단계와 시도 번호로 차단하고 60초 후 만료한다. 응답은 공용 PlayFab context나 DataManager에 연결하지 않고 저장·조회·광고·IAP를 호출하지 않는다. SDK 세션 객체는 게임에 공개하거나 재사용하지 않는다. 계정ID·code·ticket·secret을 출력하지 않으며 격리 플레이어의 Unity logger와 GPGS logger를 비활성화하고 고정 UI 문구만 표시한다. Google 자체/OS 로그까지 비밀 출력이 없다는 검증은 수행하지 않았다.

## 로컬 입력과 빌드

`TAMER_PGS_TEST_TITLE=12B656`, `TAMER_PGS_WEB_CLIENT_ID=<승인된 Web OAuth client ID>`, `TAMER_PGS_GAME_ID=<승인된 PGS 게임 ID>`를 프로세스 로컬 환경변수로 전달한다. 모두 식별 설정이며 secret 입력은 받지 않는다. 누락·잘못된 title·형식 오류는 빌드 전 명확한 메시지로 중단한다. 기존 임시 설정이나 복수 PlayGamesSettings가 있으면 중단한다. 단일 기존 PlayGamesSettings는 asset/meta 원본을 비공개 백업하고 빌드 동안만 승인된 입력으로 바꾼 뒤 바이트 단위로 복원한다. 기존 에셋을 삭제하거나 GUID를 재생성하지 않는다.

고정 CLI의 `run <절대 checkout> --editor-version 6000.0.81f1 -- -buildTarget Android -executeMethod RevivalPgsBuild.BuildAndroid -logFile <비공개 로그>`로 준비된 빌드를 실행할 수 있다. 실행 전후 `ProjectSettingsSnapshot.ps1`의 저장/복원으로 Editor 시작과 종료 시 직렬화 부작용까지 보호해야 한다. OAuth client secret은 빌드 입력이 아니며 수집하지 않는다. Editor 시작 전 외부 ProjectSettings·기존 PGS asset/meta·manifest 스냅샷도 보존해야 한다.

빌더는 설정 에셋을 일시 생성하고 manifest의 게임ID만 선택값으로 설정한다. 앱ID는 시험패키지, signing은 debug, 출력은 `Build/revival/Tamer-pgs-test.apk`다. BILLING/AD_ID 및 MobileAdsInitProvider를 제거하고 자동백업을 끈다. 기존 manifest·앱ID·backend·서명선택·번들선택을 finally에서 복원하고 임시 에셋을 제거한다. 서비스계정키나 OAuth secret은 포함하지 않는다.

**현재 PGS APK는 기존 Play 설치 `.iaptest`와 별도 패키지로 공존하도록 분리했다.** 기존 앱 삭제/데이터 지우기/업데이트 설치를 사용하지 않는다. 실제 인증에는 새 `.revival.pgs` 패키지와 실제 debug 인증서 SHA-1에 대응하는 Android OAuth 자격 증명 및 승인된 개인폰/시험 계정 조건이 필요하다. 패키지 분리만으로 외부 인증 설정이나 계정 연결이 완료되지는 않는다. Store AAB 업로드·기기 설치·인증·계정 연결은 이번 빌드 준비 범위에 포함하지 않는다.

## 검증 범위

설정/title/package/코드 요청과 manifest의 최소 EditMode 테스트 및 `RevivalPgsBuild.CompileScripts`의 Android 플레이어 분기 컴파일을 사용한다. 후자는 로컬 실제 OAuth 설정 없이 합성 코드 경계를 컴파일할 뿐 서비스 인증을 실행하지 않는다. 전체 APK, 실행화면, GPGS 외부설정, 실제 인증 성공은 미검증이다.

## 2026-09-21 검증 기록 (기존 .iaptest 구성의 역사적 결과)

- checkout `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`, 기준 main `953b7b6878188e5adc990f491e8ca97f2bf63862`, 코드 `771a1e55748faa16adaa5881826ef2f679e7ba19`. 이후 하네스 BOM/끝 빈줄 정리만 수행했다.
- Unity6000.0.81f1 / CLI1.0.0-beta.8 / GPGS2.2.1. Android min24/target36/ARM64, NDK 기준27.2.12479018(이번 NDK 실행 없음).
- 에셋 4,561개 일치/복사0. RevivalPgsTests 1회 13/13 통과, 실패0/skip0, CLI exit0. XML `Logs/revival/pgs-harness-tests.xml`, SHA256 `f234f82e60cf19dba31c9fc00e2a5d4b021b0cc213ac621fdcf82990d14a452d`.
- 테스트 이후 Android 전용 URL만 시험 HTTPS 주소로 고정했다. 테스트 대상 설정/요청/manifest는 동일하다. 최종 Android 소스는 `RevivalPgsBuild.CompileScripts` 1회 통과(exit0/PGS_TEST_COMPILE_OK).
- player 조건 Android/DevelopmentBuild/TAMER_REVIVAL_SMOKE/TAMER_PGS_HARNESS, UNITY_EDITOR 제외. 결과 `Logs/revival/pgs-player-20260921-084516`, DLL SHA256 `9a2bdb20a12e83a8fcf2f92f0139bbb8b9c5c9a670b22c68ba5ff6b21727593b`. 로그 `Logs/revival/pgs-harness-compile.log` 비공개 보존.
- Editor PID0/ProjectSettings 스냅샷 복원 확인. 전체/변경없는 테스트 반복 없음. APK SHA256 해당없음(미생성).
- 실제 전체 빌드의 manifest 병합/복원, APK 권한/서명, UI 표시, SDK/OS 자동 동작과 로그, 실제 인증 성공은 미검증이다.

## 2026-09-22 별도 패키지 빌드 검증

- checkout `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`, branch `codex/pgs-isolated-package`, 기준 main `8ef495b`. 패키지 분리 시험 소스 `426bec65a1f7513b0094c36e621c593304f4f320`, 실제 APK 소스 `3b0c8e5ae11c296783f133cba1e9ff8cdac97383`. 각 Editor 실행 직전 경로/브랜치/HEAD/clean 상태를 비공개 preflight에 기록했다. 빌드 중에는 문서만 수정했다.
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, GPGS `2.2.1`; Android IL2CPP/ARM64/min24/target36, NDK 기준 `27.2.12479018`.
- 비공개 에셋 4,561개 일치/복사0. RevivalPgsTests 첫 실행 **14/14 통과**, 실패/skip0. 기존 `.iaptest` 거부 사례를 포함한다. XML SHA256 `6005785eaf472430d2d85dd67ac82d2a0677dbce7489d219a506edf7105c0b5f`. 이후 변경 없는 14건은 반복하지 않았다.
- APK 첫 시도는 기존 SDK Resources의 단일 PlayGamesSettings를 발견한 보호 검사에서 중단했다. 설정 변경 전 실패했으며 기존 설정은 보존했다. 원래 확인한 Assets/Resources 외의 경로도 조사해야 한다는 점을 기록한다.
- 복원 분기를 보완하여 단일 기존 asset/meta를 비공개 백업하고 빌드 동안 승인된 비밀 아닌 Web client/game ID를 적용했다. 임시 설정 제거 후 잔존 검사, 각 복원 단계의 독립 실행과 마지막 오류 보고를 추가했다. 기존 에셋을 삭제하거나 GUID를 바꾸지 않는다. OAuth secret은 입력·조회·수집하지 않았다.
- **두 번째 APK 빌드 성공**, CLI exit0/PGS_TEST_BUILD_OK. 새 패키지 `com.AeDeong.MonsterTamer.revival.pgs`, ARM64/debug 서명, debuggable=true, 1.0.5/code26. APK **93,447,907 bytes**, SHA256 `b5dfd75dfa3b6956708d9f762fecba9db02ca56e8a2afc6609bd8f95d208fcb3`.
- 최종 manifest는 인증용 INTERNET을 유지하고 BILLING/AD_ID/MobileAdsInitProvider를 포함하지 않으며 allowBackup=false다. 광고 관련 모든 권한을 제거했다는 의미는 아니다. PGS APP_ID는 승인된 game ID와 일치했고, 승인된 Web client ID가 packaged player data 안에 존재함을 확인했다. 런타임의 settings/config 동등성 검사는 기존 Awake에 남아 있으며 기기에서 실행하지는 않았다.
- 실제 APK 인증서의 SHA-1/SHA-256과 패키지 쌍은 `Logs/revival/pgs-isolated-apk-private.json` 및 `pgs-isolated-signing-private.txt`에 비공개 보존했다. Web ID/서명 원문은 공개 문서에 옮기지 않는다. 새 Android OAuth credential은 만들지 않았다.
- 기존 PGS asset/meta와 두 manifest는 **외부 복원 전부터 원본 해시와 일치**하여 빌더 내부 복원을 확인했다. AndroidResolverDependencies/SceneTemplateSettings는 Editor가 변경하여 외부 백업으로 복원했다. ProjectSettings는 Editor 시작 전 스냅샷 해시로 복원했다. 이 둘의 복원 근거를 혼동하지 않는다.
- 해당 checkout Editor0, 임시 config/생성 settings와 meta 없음, URP/Graphics/Smoke 씬의 줄바꿈 자동 변경 복원을 확인했다. 원본 설정·GUID·keystore·기존 앱 데이터는 보존했다.

결과 APK는 준비 완료이며 **기기 설치·adb 조작·Google 인증·PlayFab 로그인은 수행하지 않았다**. 실제 인증을 진행하려면 개인폰 확인, 새 package와 실제 debug 인증서에 맞는 Android OAuth credential 생성 승인, PGS 테스터/시험 계정 및 시험 title의 기존 연결 계정 조건이 필요하다. CreateAccount=false이므로 새 계정 자동 생성이나 자동 연결로 우회하지 않는다. 이 결과는 로그인 성공, 출시 AAB, #91 스토어 검증 완료를 의미하지 않는다.

## 2026-09-22 안전한 인증 오류 진단

기존 하네스의 PlayFab 실패 문구는 계정 미연결과 OAuth 설정 오류를 구분하지 못했다. `PlayFabError.Error` enum만 별도 함수에 전달하고, 허용 목록에 있는 값만 고정 UI 문구로 표시한다. null·미등록 enum·그 외 오류는 공통 실패 문구로 처리한다. `ErrorMessage`·`ErrorDetails`·전체 오류 객체·인증 코드·세션 티켓·사용자 ID는 표시하거나 기록하지 않는다. 기존 CreateAccount=false, 중복 콜백 차단과 세션 격리는 유지한다.

- checkout `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`, branch `codex/pgs-safe-auth-diagnostics`, 코드 `14814831305efe8890ac50cfc74ddb41b49d0423`. 테스트·빌드 시작 직전 clean 상태와 대상 커밋을 비공개 preflight에 기록했다.
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android min24/target36/ARM64/IL2CPP, SDK Build Tools `36.0.0`, NDK 기준 `27.2.12479018`.
- 비공개 에셋 4,561개 일치/복사0. `RevivalPgsTests` 1회 **16/16 통과**, 실패/skip0. 계정 오류/OAuth 오류 구분, null·미등록·허용하지 않은 enum의 공통 문구 처리를 포함한다. XML SHA256 `4043dcda3c8dd256c765b281e1a3572533a9aa682a3a40afbb6c8a60c90d894a`.
- 이전 APK의 개인폰 수동 인증은 총 1회 실패했다. 코드 흐름상 Google 인증과 비어 있지 않은 서버 코드 발급 후 시험 title의 PlayFab 오류 콜백에 도달했지만, 당시 고정 문구로는 정확한 enum을 확정할 수 없다. 진단 수정으로 이 실패 횟수를 초기화하지 않는다.
- 새 APK의 실제 로그인은 별도 조율 전 실행하지 않는다. 다음 동일 로그인 시험은 누적 2차이며, 다시 실패하면 사용자 승인 전 3차 시도를 하지 않는다. 계정 신규 생성·연결·ForceLink로 우회하지 않는다.
- 새 APK 빌드 1회 성공(exit0), 소스는 위 `1481483`이며 빌드 중 문서만 작성했다. APK `Build/revival/Tamer-pgs-test.apk`, **132,798,787 bytes**, SHA256 `df73294b7984ab062e1fd6c964027280b01fbc13ecd9b798065a0296472ee030`.
- 패키지 `.revival.pgs`, 1.0.5/code26, debuggable/ARM64와 기존 debug 인증서 유지 확인. 최종 manifest에서 BILLING/AD_ID/MobileAdsInitProvider 없음, allowBackup=false 확인. 이전 APK는 별도 비공개 파일로 보존했다.
- 기존 PGS asset/meta와 두 manifest는 외부 복원 전 원본 해시와 일치했다. Editor 직렬화로 변경된 ProjectSettings/Resolver/SceneTemplate/렌더링 설정은 외부 스냅샷으로 복원했다. Smoke 씬과 전역 URP 설정의 줄바꿈 변경도 복원했다. 해당 checkout Editor0, 임시 PGS config/settings 및 meta 잔존 없음.
- 이번 새 APK의 설치·실제 로그인과 정식 출시 AAB·스토어 검증은 미수행이다. 민감한 외부 설정/계정 연결 조회의 원문은 공개 문서·PR에 포함하지 않는다.

## 2026-09-22 OAuth 세부 오류의 제한된 분류

`GoogleOAuthError`만으로 원인을 구별할 수 없는 경우에 한해 응답 텍스트를 메모리 안에서 검사한다. `ErrorDetails`의 정확한 `error` 필드 값과 메시지의 JSON 형태 `error` 문자열 필드를 우선하며, 허용 토큰은 invalid_client / invalid_grant / redirect_uri_mismatch / access_denied 네 가지다. 일반 메시지는 URL·경로·코드 형태의 이웃 문자를 제외한 정확한 토큰 경계로 검사한다. 복수 상충·미등록 구조화 값·4,096자 초과 메시지·예외는 Unknown이다.

출력은 고정 enum 문구뿐이다. 원문·키·URL·인증 코드·티켓을 UI·로그·파일·예외로 전달하지 않는다. 일반 오류 경로는 기존 enum-only 진단을 유지한다. 이 분류는 서버가 전달한 제한된 힌트이며 원인 확정이 아니다. PlayFab가 세부 오류를 주지 않거나 인식하지 못하는 형식이면 Unknown으로 끝난다. 설정 추측 변경, 비밀 재입력, 신규 계정 생성·연결은 하지 않는다.

- checkout `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`, branch `codex/pgs-oauth-safe-classifier`, 소스 `484cbf6fb2c63ae362f823b64894144e47aa4953`; clean preflight와 설정 복원 증거는 비공개 보존.
- Unity `6000.0.81f1` / CLI `1.0.0-beta.8`. 관련 EditMode **18/18 통과**, 1회 실행/실패0. 합성 입력만 사용해 정확값·상충·유사 문자열·미등록·장문·비밀 포함 문구의 고정 출력 경계를 확인했다.
- APK 및 실제 로그인은 PR 독립 검토 후 별도로 진행한다. 이전 실제 로그인 실패2회를 유지하고, 승인된 다음 3차는 정확히1회만 실행한다. 재실패 시 추가 승인 전4차를 실행하지 않는다.
- 독립 검토에서 문자열이 아닌 JSON error 값/중복 필드의 prose fallback 가능성을 발견하여 `cbbb91cba484423f8f3100c890aa29a04ea179f8`에서 보강했다. JSON 형식은 중복 필드 오류 모드로 파싱하고 비문자열·중복·미완성·escape 포함 입력은 Unknown으로 처리한다. 예외 내용은 출력하지 않는다.
- 보강 후 해당 분류 **2/2 통과**, 실패0(나머지16건 반복 없음). XML SHA256 `643a97f20dd1560b34ea58838c6198ffc779cef00e57e26d503dee18aabb2618`. 앞선18건 XML SHA256 `ceff56f103ccda5f92871526a0a976bb1a304c7ba48fac1cd2a9675baabe6e78`.
