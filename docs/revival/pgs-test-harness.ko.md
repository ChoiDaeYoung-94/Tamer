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
