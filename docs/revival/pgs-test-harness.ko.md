# 수동 PGS 시험 로그인 구성

시험 대상은 PlayFab `12B656`, 앱 `com.AeDeong.MonsterTamer.iaptest`로 고정한다. 기존 IAP 하네스와 Login.cs는 수정하지 않는다. 게임 MonoBehaviour가 없는 기존 RevivalSmoke 씬에 PGS 시험 컨트롤만 추가하며, `TAMER_REVIVAL_SMOKE`와 `TAMER_PGS_HARNESS`는 빌드 한정 define이다.

앱 시작 시 인증하지 않는다. 명시적 버튼 입력에만 Google 인증 → 새 서버 코드 → 시험 title native PGS 로그인(CreateAccount=false)을 한 번 실행한다. 중복·늦은 콜백은 단계와 시도 번호로 차단하고 60초 후 만료한다. 응답은 공용 PlayFab context나 DataManager에 연결하지 않고 저장·조회·광고·IAP를 호출하지 않는다. SDK 세션 객체는 게임에 공개하거나 재사용하지 않는다. 계정ID·code·ticket·secret을 출력하지 않으며 격리 플레이어의 Unity logger와 GPGS logger를 비활성화하고 고정 UI 문구만 표시한다. Google 자체/OS 로그까지 비밀 출력이 없다는 검증은 수행하지 않았다.

## 로컬 입력과 빌드

`TAMER_PGS_TEST_TITLE=12B656`, `TAMER_PGS_WEB_CLIENT_ID=<승인된 Web OAuth client ID>`, `TAMER_PGS_GAME_ID=<승인된 PGS 게임 ID>`를 프로세스 로컬 환경변수로 전달한다. 모두 식별 설정이며 secret 입력은 받지 않는다. 누락·잘못된 title·형식 오류는 빌드 전 명확한 메시지로 중단한다. 기존 PlayGamesSettings 또는 임시 설정이 있으면 덮어쓰지 않고 중단한다.

고정 CLI의 `run <절대 checkout> --editor-version 6000.0.81f1 -- -buildTarget Android -executeMethod RevivalPgsBuild.BuildAndroid -logFile <비공개 로그>`로 준비된 빌드를 실행할 수 있다. 실행 전후 `ProjectSettingsSnapshot.ps1`의 저장/복원으로 Editor 시작과 종료 시 직렬화 부작용까지 보호해야 한다. 실제 승인된 OAuth 구성이 없는 현재는 빌드를 실행하지 않는다.

빌더는 설정 에셋을 일시 생성하고 manifest의 게임ID만 선택값으로 설정한다. 앱ID는 시험패키지, signing은 debug, 출력은 `Build/revival/Tamer-pgs-test.apk`다. BILLING/AD_ID 및 MobileAdsInitProvider를 제거하고 자동백업을 끈다. 기존 manifest·앱ID·backend·서명선택·번들선택을 finally에서 복원하고 임시 에셋을 제거한다. 서비스계정키나 OAuth secret은 포함하지 않는다.

**이 debug APK는 기존 Play 설치 .iaptest와 서명이 다르므로 업데이트 설치하면 안 된다.** 기존 앱 삭제/데이터 지우기를 해결책으로 사용하지 않는다. 실제 기기 검증에는 별도 승인된 환경과 debug 서명용 Android OAuth 대응 또는 기존 시험 업로드키/Play 배포 경로를 추가 검토해야 한다. 이 변경은 Store AAB 제작·업로드·설치·인증·계정연결을 수행하지 않는다.

## 검증 범위

설정/title/package/코드 요청과 manifest의 최소 EditMode 테스트 및 `RevivalPgsBuild.CompileScripts`의 Android 플레이어 분기 컴파일을 사용한다. 후자는 로컬 실제 OAuth 설정 없이 합성 코드 경계를 컴파일할 뿐 서비스 인증을 실행하지 않는다. 전체 APK, 실행화면, GPGS 외부설정, 실제 인증 성공은 미검증이다.

## 2026-09-21 검증 기록

- checkout `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`, 기준 main `953b7b6878188e5adc990f491e8ca97f2bf63862`, 코드 `771a1e55748faa16adaa5881826ef2f679e7ba19`. 이후 하네스 BOM/끝 빈줄 정리만 수행했다.
- Unity6000.0.81f1 / CLI1.0.0-beta.8 / GPGS2.2.1. Android min24/target36/ARM64, NDK 기준27.2.12479018(이번 NDK 실행 없음).
- 에셋 4,561개 일치/복사0. RevivalPgsTests 1회 13/13 통과, 실패0/skip0, CLI exit0. XML `Logs/revival/pgs-harness-tests.xml`, SHA256 `f234f82e60cf19dba31c9fc00e2a5d4b021b0cc213ac621fdcf82990d14a452d`.
- 테스트 이후 Android 전용 URL만 시험 HTTPS 주소로 고정했다. 테스트 대상 설정/요청/manifest는 동일하다. 최종 Android 소스는 `RevivalPgsBuild.CompileScripts` 1회 통과(exit0/PGS_TEST_COMPILE_OK).
- player 조건 Android/DevelopmentBuild/TAMER_REVIVAL_SMOKE/TAMER_PGS_HARNESS, UNITY_EDITOR 제외. 결과 `Logs/revival/pgs-player-20260921-084516`, DLL SHA256 `9a2bdb20a12e83a8fcf2f92f0139bbb8b9c5c9a670b22c68ba5ff6b21727593b`. 로그 `Logs/revival/pgs-harness-compile.log` 비공개 보존.
- Editor PID0/ProjectSettings 스냅샷 복원 확인. 전체/변경없는 테스트 반복 없음. APK SHA256 해당없음(미생성).
- 실제 전체 빌드의 manifest 병합/복원, APK 권한/서명, UI 표시, SDK/OS 자동 동작과 로그, 실제 인증 성공은 미검증이다.
