# 별도 LD 손님 첫 선택·재시작 시험

기존 손님 시험 앱에는 이미 저장된 설정이 있을 수 있어, 첫 선택을 다시 확인할 때 기존 데이터를 지우지 않는 별도 x86_64 프로필을 추가합니다. 운영 로그인·서버 계정 생성은 수행하지 않는 오프라인 UI 시험입니다.

- 새 앱 ID는 `com.AeDeong.MonsterTamer.revival.guestentry1110x86`입니다. 기존1102 시험 앱과 운영 앱을 보존합니다.
- `BuildFreshX86Android`는 기존 smoke 장면·release engine·debug signing·정확x86_64만 사용합니다. 기본 ARM64·전역 define·운영 키는 유지합니다.
- 출력은 고유 GUID 폴더를 사용하고 기존 폴더·APK를 덮어쓰지 않습니다. Started/빌드완료·복구대기/최종완료를 구분하며 원설정 복구 뒤에만 Succeeded를 기록합니다.
- Android 원manifest·광고설정·Gradle과 각 meta를 보존합니다. x86 제외행의 정확한 변환만 사용하고 finally에서 원아키텍처와 바이트를 복구합니다.
- 앱은 실제 `InitialAccountChoiceView`를 사용하되 Google 버튼은 모의 실패로만 연결합니다. 명시 손님 선택의 로컬 설정 저장·같은 ID 해시 재시작을 검증하며 운영 인증 어댑터나 게임 manager를 만들지 않습니다.

개발/Editor/비Android/다른 앱 ID 거절을 포함한 관련 NUnit38개와 ordinary Android 컴파일·IL 검사를 준비합니다. 시험과 Player 빌드는 별도 Editor 실행으로 나누어 미저장 장면 보호를 유지합니다. 새 APK와 실제 LD 버튼/재시작 검증은 준비 중이며 통과로 표시하지 않습니다. 기존 공통 인트로 APK의 실행 성공과 이 손님 프로필의 검증도 구분합니다.

실제 Google/손님 서버 계정·진행 저장·No Ads/구매/광고·실물 ARM64/16KB와 최종 심사/프로덕션은 이 변경의 완료 근거가 아닙니다.
