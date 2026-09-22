# 격리 앱을 통한 CloudScript 삭제 시험 기록

## 범위와 현재 상태

사용자가 승인한 시험 타이틀의 신규 폐기 계정 한 개에 한정한다. 기존 PGS/IAP 계정과 운영 타이틀, master 삭제는 대상이 아니다. 별도 서버/DB를 배포하지 않는다. 현재 기록은 **계정 생성·기기 로그인·미게시 revision2의 비활성 config 확인까지**이며 삭제 성공 기록이 아니다. 기기 연결 확인 2회 실패 후 중단했고, 추가 승인된 ADB 서버 재시작도 종료 명령 timeout으로 실패했다. Live1·삭제 옵션 off·내부 gate 없음은 유지되며 실제 삭제 요청은 0회이다.

## 구현과 검증 대상

- checkout: `C:\Users\pc_17\.codex\worktrees\7299\Tamer`
- branch: `codex/cloudscript-live-test`, base `9015873aebdac85299e4db34e508f624cdfd8617`
- 구현 `e2ddf09cb015be5342ed8ac9e3c3e75a8a66ecbc`, 최종 빌드 소스 `f95bd452c0078195fb3d71cb49b745d17a7965b1`.
- Unity6000.0.81f1 / CLI1.0.0-beta.8 / Android min24 target36 ARM64 / build-tools36.0.0. 기기 SM-S938N Android16. 에셋 verified4561/copied0.
- 별도 `com.AeDeong.MonsterTamer.deletiontrial` debug 앱, 격리 RevivalSmoke 씬, 수동 기존 계정 로그인. CreateAccount=false, 전용 CustomId 형식·expected PlayFabId·entity type·ticket을 확인한다. 광고/결제/PGS·주기 저장 초기화를 호출하지 않는다.
- 기존 `DeletionPresenter`→runtime factory→Live config/Specific confirm 및 durable journal, DataManager의 pending/owner 정리 경로를 사용한다. 시험용 소유 계정 로컬 marker만 기록한다. UI 닫기는 Canvas/입력만 숨기고 presenter/flow를 비활성화하지 않는다.
- APK 검증은 정확한 새 파일/앱 ID/debug 서명/ARM64/network 허용/BILLING·AD_ID·광고 provider 제외/backupfalse와 18개 cloud/device 제외 규칙을 확인한다.

## 로컬 비밀 입력 도구

`tools/revival/deletion_trial_console.py`는 사용자 직접 입력용 마스킹 Tk 창이다. title secret을 argv/env/파일/로그/브라우저에서 읽지 않고 입력 후 widget을 비워 프로세스 메모리에만 보관한다. 입력 자체는 원격 요청을 실행하지 않는다. 네트워크는 고정 시험 origin, redirect 없음, 25초 timeout, retry 없음, 예외 원문 미출력이다.

명령은 고정 allowlist의 revision 읽기/신규 폐기 계정 생성/불확실 생성 조회뿐이며 삭제·옵션·게시 함수는 없다. 시작 시 기존 inbox를 거부하고 세션 nonce가 다른 명령을 거부한다. 신규 생성 전 비공개 guard에 title·임의 CustomId·attempt를 exclusive/fsync 기록하여 재시작 후 재생성을 차단한다. 이 CustomId는 비공개 로그인 증거이며 공개하지 않는다. title secret/SessionTicket/EntityToken은 기록하지 않는다. 신규 응답의 NewlyCreated=true와 유효 ID를 요구하고, 불확실 생성 조회는 같은 ID의 CreateAccount=false만 허용하며 삭제 가능 상태로 승격하지 않는다.

## 실행 결과

- helper 오프라인 테스트 첫 **4/4** 통과. Editor 하네스 입력/신규 생성 차단 검사 첫 **3/3** 통과, 재컴파일 errors=[] 확인.
- 첫 APK 빌드/검증은 성공했으나 백업 규칙 생성 callback의 trial ID 누락을 독립 검토에서 발견했다. 첫 산출물은 증거로 보존하고 ID allowlist를 수정했다. 기존 캐시 의존을 제거한 두 번째 빌드/검증도 성공했다. 빌드 실패는 0회이며 변경 때문에 재빌드했다.
- 최종 APK `Build/revival/Tamer-deletion-trial.apk`, 93460588 bytes, SHA-256 `a9f294666e927b821705d225f9f5d3c63c85ed5d930bb5695a2d206cf58e9de1`. 설치 전에 해당 앱 ID 부재를 확인했고 기존 앱을 교체하거나 지우지 않았다.
- 해당 checkout Editor만 정상 종료 및 batch 후 설정 snapshot 복원, working tree clean/Editor0 확인. 통합 담당자가 APK hash/JSON·설정 복원 hash를 독립 대조했다.
- 사용자 입력 후 읽기 API 성공으로 version1/published1/latest1 확인. 브라우저의 현재 원본 14044문자도 이전 백업과 동일하다.
- 승인된 신규 계정 한 개 생성 응답 NewlyCreated=true/시험 title 일치. 정확 ID 검색 결과1명, 생성 시각 일치, Custom 연결만, 구매 결과 없음/$0, legacy inventory 결과 없음을 비공개로 기록했다. 기존 계정을 선택·재사용하지 않았다.
- 기기 첫 로그인 버튼 시도는 서버 호출 전에 입력 형식 오류로 멈췄다. 키보드 포커스 후 가림 입력을 다시 넣고 native password field 길이68을 확인한 뒤 두 번째 시도에서 로그인 성공/기존 삭제 UI Idle 표시를 확인했다. 첫 원인은 입력 타이밍에 의한 누락 가능성이 있으나 확정하지 않는다. 이 시험은 1실패 후 2차 통과이며 세 번째 시도는 없다.
- 사용자 직접 파일 선택 후 후보를 미게시 revision2로 한 번 업로드했다. 원본 14 handler prefix는 그대로이며 원격 전체 내용은 줄바꿈 정규화 후 로컬 후보와 일치한다. Live는 revision1이다.
- 신규 시험 계정의 Specific revision2에서 읽기 함수 getCurrentPlayerDeletionConfigV1만 한 번 실행했다. available=false/protocol=tamer-title-deletion-v1, Error=null, APIRequestsIssued=1, HttpRequestsIssued=0을 확인했다. 삭제 함수는 호출하지 않았다.
- 15분·한 계정 gate는 UI에 입력했으나 저장 전에 기기 연결 확인이 두 번 실패했다. 첫 .81 SDK adb devices는 무응답으로 해당 읽기 프로세스만 취소했고, 현재 서버와 같은 .25 SDK adb devices는 15초 timeout이었다. 원인은 미확정이다. 프로젝트 규칙에 따라 후속 활성화/삭제 시험을 중단하고 미저장 gate 입력을 취소했다.
- 사용자가 공용 ADB 서버 재시작 및 연결 재확인 1회를 추가 승인했다. 통합 담당자의 ADB 작업 없음 확인 후 .25 SDK adb(34.0.5)의 kill-server를 실행했으나 20초 timeout이었다. 후속 start-server/devices는 실행하지 않았고 추가 반복, 다른 PID 강제 종료, Unity Editor 종료도 하지 않았다.
- 브라우저 새로고침 후 서버 기준 내부 title data 비어 있음, ServerDeletePlayer off, Client CustomId 신규 생성 차단 on, Live1/미게시2를 다시 확인했다. 이 설정들은 애초 변경하지 않았으므로 원복 완료로 표현하지 않는다. 만료된 미저장 gate 값은 재사용하지 않는다.
- 로컬 비밀 입력 helper를 정상 종료했고 ready=false/closed=true/프로세스 부재를 확인했다. 키를 파일이나 다른 저장소에서 복구하지 않는다. 재개 시 새 구체적 기기 복구 결정과 필요시 사용자 직접 키 재입력이 필요하다. 신규 계정/미게시 revision2/비공개 증거는 보존했다.

## 비공개 증거

원시 파일/계정·CustomId·스크린샷은 `Logs/revival/`에 보존하며 커밋하지 않는다.

| 파일 | SHA-256 |
|---|---|
| deletion-trial-console-tests.log | 6d0272d5c0c5b0e641e4234b7246062dc18ffe21e33d13c57c15c9910f89ebce |
| deletion-trial-editor-tests.json | cb001e04fde6c764302f678f0baa0b6021edd128753455795cf67095a8601ef2 |
| deletion-trial-apk-verification.json | fba57e85469fb2df52671af62addd7157fcc8cb159c4c9e7c692abf4a7c65d3f |
| deletion-trial/created-account.json | 82ad21cfc1c1ae9497acdce0004cce035386cf185a524db6bbc7a37acec242e4 |
| deletion-trial/account-before-private.txt | 6c342738660ab9d28ad85b91ccc777064dd3d19cf2dc4ca2c3691af6a47ac049 |
| deletion-trial/purchases-before-private.txt | 7c199752c593cb8559aa65dc4a658fe392860688fc4242d9d7123d1b3c4ae075 |
| deletion-trial/inventory-before-private.txt | e4842dec46dd5f0950487fbc7a9d6645bd0b3152c9f3e2e4db4b8da78d05203a |
| deletion-trial/phone-login-second.png | e1592e6f86c51e05ade3f1fa1f27fb50acc6b9a6394be948f7a3de37d0d29d10 |
| deletion-trial/candidate-gated.js | be0dbbec9085e94d4b1a0c7ced484ebcdf857f2e217e90de77d9e03070c0a36f |
| deletion-trial/remote-revision2.js | 5d44a6014070718fa66986625bd5c3beb08b28e05d16fdb30b00d871de971099 |
| deletion-trial/specific-config-off.txt | 1996ea335975346849a279fd2662493691522a85b0c4f50de807d15b36af1146 |
| deletion-trial/internal-data-after-cancel.txt | 63c2d27e32a5b5cbe3741dbd650d668f697b7beb38a07ed63155fd04ca226941 |
| deletion-trial/options-recheck.txt | e13eb7332ac3329472f8add13f2ecbf6094b8fc6eadad06736bf458953f1b283 |
| deletion-trial/adb-approved-restart.json | deb4c58f40e993d07b8f5fb5214db324aad0268a28a2198f060558ada953e351 |
| deletion-trial/helper-closed.json | 292f4276c4b39fc81447fd8b53cb071e45c49713134e2e15706a4f20b7011a49 |

후보의 원본 prefix·기존14handler·추가gate 원문 일치는 독립 검토됐다. 정적 enabled=true/title pin 후보이지만 내부 runtime gate 미설정이면 거부한다. 미게시 후보 Specific config 비활성 검사는 완료했다. 현재 앱 Live→Specific 실제 연결 시험과 한 계정15분 gate/타이틀 전체 ServerDeletePlayer 허용·원복은 아직 수행하지 않았다. 기기 연결 복구 승인과 준비 상태 재대조 전에는 진행하지 않는다. 최대1분 설정 캐시와 in-flight 회수 불가, Live 복원은 Specific 폐기가 아니라는 제한은 [시험 후보 문서](cloudscript-test-candidate.ko.md)를 따른다. 이 결과는 실제 삭제 접수/완료·최종 출시 AAB·스토어 검증 증거가 아니다.
