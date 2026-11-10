# 별도 LD 손님 첫 선택·재시작 시험

기존 손님 시험 앱에는 이미 저장된 설정이 있을 수 있어, 첫 선택을 다시 확인할 때 기존 데이터를 지우지 않는 별도 x86_64 프로필을 추가합니다. 운영 로그인·서버 계정 생성은 수행하지 않는 오프라인 UI 시험입니다.

- 새 앱 ID는 `com.AeDeong.MonsterTamer.revival.guestentry1110x86`입니다. 기존1102 시험 앱과 운영 앱을 보존합니다.
- `BuildFreshX86Android`는 기존 smoke 장면·release engine·debug signing·정확x86_64만 사용합니다. 기본 ARM64·전역 define·운영 키는 유지합니다.
- 출력은 고유 GUID 폴더를 사용하고 기존 폴더·APK를 덮어쓰지 않습니다. Started/빌드완료·복구대기/최종완료를 구분하며 원설정 복구 뒤에만 Succeeded를 기록합니다.
- Android 원manifest·광고설정·Gradle과 각 meta를 보존합니다. x86 제외행의 정확한 변환만 사용하고 finally에서 원아키텍처와 바이트를 복구합니다.
- 앱은 실제 `InitialAccountChoiceView`를 사용하되 Google 버튼은 모의 실패로만 연결합니다. 명시 손님 선택의 로컬 설정 저장·같은 ID 해시 재시작을 검증하며 운영 인증 어댑터나 게임 manager를 만들지 않습니다.

개발/Editor/비Android/다른 앱 ID 거절을 포함한 관련 NUnit38개와 ordinary Android 컴파일·IL 검사를 통과했습니다. 시험과 Player 빌드를 별도 Editor 실행으로 나누어 미저장 장면 보호를 유지했습니다. 기존 공통 인트로 APK와 이 손님 프로필의 결과를 구분합니다.

실제 Google/손님 서버 계정·진행 저장·No Ads/구매/광고·실물 ARM64/16KB와 최종 심사/프로덕션은 이 변경의 완료 근거가 아닙니다.

## 실제 검증

source9201baa에서 NUnit38/38(실패/skip/inconclusive0·XML38고유 항목), 새 ordinary Android68·DLL/PDB136 보존·IL16을 확인했습니다. 추가 메타데이터 읽기에서 일반 Player의 손님 시험 타입/새 샌드박스ID 문자열 제외를 확인했습니다. CLI0/Job0·강제0·원7535파일/키/JDK/공유옵션·서버 복구로05:28:27UTC에 전체/자원PASS로 반환했습니다.

같은 소스의142개 증거SHA를 재사용하고 public 빌드 메서드만 직접 호출했습니다. 시험/ordinary 반복0·임시entry0·Player1, APK75,691,810bytes/SHA-256 `b7dd25bb0600fb122ad1324cb985533614653f6ddcc480862d8a5716aa27e6f4`의 정확x86_64/release engine/debug 인증서·오프라인 권한/provider 제거·백업18/native 검사를 통과했습니다. 원ARM64/Gradle/meta·키/JDK·소유 프로세스 종료와 원7535파일을 확인해05:40:17UTC 전체/자원PASS로 반환했습니다.

첫 LD 사전점검은 Android34의 Package와 Queries에 각각 있는 User0 행을 혼합한 검사기에서 설치/시작/버튼0으로 중단됐습니다. 원실패·원시 출력을 보존하고 정확한 패키지의 들여쓰기 범위 및 appId/legacy userId를 구분하는 별도 검사기를 실제2개·합성positive3/negative5로 확인했습니다.

후속 LD88에서 새 설치→첫 선택→Google 모의 실패(자동 손님0·modeempty/IDnone)→명시 손님 로컬 저장→새 프로세스의 같은ID SHA 복원을 확인했습니다. 한글 본문·오류 안내·두 버튼과 저장/복원 원본 화면4장을 직접 관찰했습니다. 실제 인증·서버 계정/진행·나이 조작0이며 앱정지/HOME·소유PID 부재·운영/공통 시험 UID/User0상태·원본/공유신원 보존을 확인해2026-11-10T06:12:44.148668Z 전체/자원PASS로 반환했습니다.
