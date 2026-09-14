# Cloud 전용 로그인 빌드와 수동 인계

## 구현 경계

Cloud 전용 패키지는 `com.AeDeong.MonsterTamer.revival.gamesavecloud`, 출력은 `Build/revival/Tamer-gamesave-cloud.apk`이다.
기존 offline 패키지와 APK/저장 경로를 보존한다. 인터넷 권한만 추가하며 ACCESS_NETWORK_STATE, BILLING, AD_ID 및 광고 초기화 provider는 제거한다.

`RevivalGameSaveAuthentication`만 인증 binding을 만든다. 기존 ReceiptSession을 받던 `RevivalGameSaveSession.Connect`는 제거했다.
앱의 CustomID와 예상 PlayFab ID 입력 → 새 gameplay namespace 검증 → `CreateAccount=false` → 응답 ID 일치/티켓 존재/신규 생성 아님 확인을 거친다.
그 응답의 계정과 티켓으로 만든 private instance client만 실제 DataManager/queue의 Cloud transport에 연결한다.
Title은 정확히 `12B656`이어야 한다. IAP/progress 세션을 전달하는 API는 없다.

로그아웃·재로그인·20초 인증 시간 초과 뒤의 늦은 응답은 binding을 만들지 못한다.
로그아웃/재로그인은 열린 저장 세션을 폐기하고 기존 읽기/쓰기/ack 응답을 무효화한다.
오류나 예상 ID 불일치 시 저장 경로를 만들지 않는다. 인증 후에도 사용자가 명시적으로 읽기를 시작해야 한다.
8키/Private/합성값/미지 데이터 거부 및 새 빈 restore 슬롯 규칙은 [기존 schema 기록](game-save-schema.ko.md)을 따른다.

CustomID/티켓을 파일·PlayerPrefs·로그로 기록하지 않고 입력창은 인증 시도 직후 비운다.
계정 귀속 확인용 PlayFab ID는 기존 DataManager owner metadata에만 필요한 범위로 남는다. 이 로컬 파일과 원시 응답은 비공개 증거다.
테스트용 로그인/시간 주입 생성자는 비공개이며 `UNITY_EDITOR`에서만 컴파일된다.

## 빌드와 회귀 검증 기록

- checkout: `C:/Users/pc_17/.codex/worktrees/7299/Tamer`
- 소스: `3f155a3f37867e2c97be602336fe5ee04e11bd9d`, 기준 main: `8fc8d91d9a0e74c71b59c084a852bb98ca212701`
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, debug ARM64, min SDK 24 / target SDK 36
- 신규 Cloud Live Editor **19/19**, 관련 기존 회귀 포함 batch **104/104 통과**
- XML SHA-256: `980112b8624d328ac1f1ef8d22327a99266ee44e46be7bb72319660f649d3f27`
- Cloud APK 93,366,772 bytes, SHA-256: `5d8431d55b78c579ebc140c2258ad09b740c774f42bc07fa387c5caeccc17aef`
- 기존 offline APK SHA-256 `cf571770348a312dd84b57dbed94dc8e5bd12ccf82531c8fd282e28d693c721c` 보존 확인
- 최종 패키지/debug 서명/ARM64/INTERNET만 추가/범위 밖 권한과 provider 제거 검사 통과, GUID unresolved 0
- LOAD/ZIP 통과. 이번 Cloud APK의 별도 strict RELRO 끝 정렬은 **5개 실패**이며 이전 offline APK의 4개와 구분한다.

실제 SDK의 login/read/write 요청을 가짜 transport가 수신하도록 하여 외부 통신 없이 검증했다.
검증 항목은 올바른 인증 응답의 계정·티켓 바인딩, 정확 8키 read/Private write/readback,
미승인 namespace/대상/예상 ID 거부, 응답 불일치·티켓 누락·신규 생성 응답 거부,
logout/relogin/timeout 후 늦은 로그인 응답, logout/relogin 후 늦은 read/ack 무효화다.
실제 계정 생성·외부 인증·Cloud APK 기기 설치/UI 실행·실서버 읽기/쓰기는 아직 하지 않았다.
Player 씬/PlayerPrefs 복원, 16 KB 런타임, Google·광고·구매·스토어는 미검증이다.
[기계 판독 결과와 비공개 근거 해시](game-save-cloud-validation.json)를 함께 확인한다.

## 관리자에게 전달할 빈 요청 양식

이 양식에는 실제 자격값을 적어 공개 저장하지 않는다. 현재 실제 계정 생성과 인증은 실행하지 않았다.

```text
목적: 기존 진행도 8키의 합성 저장/복원 검증
대상 Title: 12B656
계정: 새 gameplay 전용 계정 (기존 구매/progress/운영 계정 사용 금지)
CustomID 형식: gameplay-save- + 소문자 16진수 32자리
예상 PlayFab ID: 관리자가 신규 계정 화면에서 확인하여 앱에 직접 입력
허용 데이터: NickName, Sex, Tutorial, Gold, Power, AttackSpeed, MoveSpeed, AllyMonsters
추가 권한/정책 변경: 없음
생성 방식: 관리자에게 이미 허용된 정상 수동 생성 절차만 사용
```

앱은 계정을 자동 생성하지 않는다. 정상 수동 생성 절차가 없거나 거부되면 정책 확대·관리 API·기존 계정 재사용으로 우회하지 않는다.
과거 progress CustomID 파일을 복사하거나 새 namespace만 붙여 재사용하지 않는다.
별도 로컬 안내 파일 `.revival-local/gameplay-cloud-handoff/README.txt`도 자격값 없이 유지한다.

## 개인폰에서 필요한 행동과 실행 순서

빌드/회귀/통합 검토가 끝난 후 개인폰을 연결한다. 회사폰에는 Cloud 앱을 설치하거나 계정을 연결하지 않는다.
관리자는 새 전용 계정을 정상 수동 생성한 뒤 CustomID와 예상 PlayFab ID를 **앱의 두 가려진 입력창에 직접 입력**한다.
이름을 바꾼 이전 계정, 기존 구매 계정, 기존 progress 계정은 사용하지 않는다. 실제 값은 공개 대화·PR·커밋에 붙여 넣지 않는다.

1. 검토된 Cloud APK를 개인폰에 설치하고 전용 앱을 연다. 이 단계는 인증을 자동 실행하지 않는다.
2. 두 입력을 직접 채우고 Authenticate를 누른다. 예상 계정 인증 성공을 확인한다.
3. Open primary를 눌러 읽기가 끝날 때까지 기다린다. 신규 계정이면 명시 8키 snapshot이 비어 있어야 한다. 알 수 없는 기존 데이터가 있으면 중단한다.
4. 단계 1을 로컬 준비 → 명시 업로드 → 읽기 완료 후 Verify step 1 acknowledged로 확인한다.
5. 전용 앱 프로세스 종료/재실행 후 두 값을 다시 수동 입력해 재인증한다. primary 읽기와 단계 1 복원을 확인한다.
6. 새 read-only restore1을 열고 읽기 완료 후 단계 1 검증을 누른다. 쓰기 수는 0이어야 한다. 기존 primary를 삭제하지 않는다.
7. primary를 다시 열어 단계 2에 같은 저장/재시작/restore2 절차를 수행한다.
8. 인증 해제 후 앱을 종료한다. 증거와 계정은 최종 검증 종료 후 승인된 정리 목록에 따라 처리한다.

Cloud 읽기는 비동기이므로 Open 직후 성공으로 간주하지 않는다. busy=false, failed=false와 명시 Verify PASS를 함께 확인한다.
새 restore 슬롯은 재사용하면 거부되므로 기존 증거를 지우고 반복하지 않는다.
실제 Player 씬 반영, PlayerPrefs 3종 서버화, Google·구매·광고 및 스토어 검증은 이 절차에 포함하지 않는다.
