# Classic CloudScript 계정 삭제 요청

## 구현 범위

별도 유료 서버나 SQLite 영수증 서비스를 배포하지 않고 기존 PlayFab Classic CloudScript를 사용한다. `server/cloudscript/account-deletion.js`는 **기본 비활성화된 배포 후보**이며 현재 운영 타이틀에 업로드하거나 활성화하지 않았다.

앱에서 사용자가 삭제를 선택하면 읽기 전용 `getCurrentPlayerDeletionConfigV1`을 Live revision으로 호출한다. 활성 여부와 프로토콜을 확인한 뒤 최종 확인 시 해당 Specific revision의 `requestCurrentPlayerDeletionV1`을 한 번 호출한다. 인증 context는 복사하고 호출 전후 현재 세션을 확인한다. 새 인증을 수행했다고 표현하지 않는다.

서버는 인증된 `currentPlayerId`만 `Server/DeletePlayer`에 전달한다. 클라이언트 인수는 `confirmed: true`와 임의 requestId 두 필드뿐이며 다른 계정·타이틀 지정은 거부한다. 설정된 titleId와 `script.titleId`가 일치해야 한다. Admin/DeleteMasterPlayerAccount를 호출하지 않는다.

정상 envelope의 함수명·revision·프로토콜·requestId·title scope·boolean accepted가 모두 일치해야 접수로 인정한다. 접수는 삭제 완료가 아니다. 접수 후 기존 owner/session 검증을 거쳐 로그아웃하고 해당 계정의 로컬 저장을 정리하며 다른 계정 데이터와 No Ads 권한 증거를 보존한다.

요청 직전 account/title에 결합된 로컬 journal을 영속화한다. 저장 실패 시 서버 요청을 보내지 않는다. timeout·응답 유실·CloudScript 오류·불명확한 응답은 접수 성공으로 간주하지 않는다. 데이터는 보존하고 저장을 중단하며 자동 재전송·상태 조회·재확인 버튼을 제공하지 않는다. 동일 설치에서 다시 열거나 로그인해도 pending 상태로 차단한다. 문의 버튼은 `doeud1410@gmail.com`으로 연결하며 계정 식별자나 토큰을 자동 첨부하지 않는다.

**한 번만 전송하는 보장은 이 기기의 로컬 journal 범위이다.** requestId는 응답 결합용이며 서버 idempotency 키가 아니다. 다른 기기나 앱 데이터 삭제·재설치 후의 중복 호출을 서버에서 차단한다고 보장하지 않는다. 별도 완료 관찰자나 비인증 영수증 복구는 없다. 삭제 처리 중 재로그인의 `AccountDeleted`는 새 계정 생성으로 우회하지 않고 문의로 안내해야 한다.

## 공식 근거

- [Classic CloudScript 작성](https://learn.microsoft.com/en-us/xbox/playfab/live-service-management/service-gateway/automation/cloudscript/writing-custom-cloudscript): currentPlayerId와 script context, 신뢰할 수 없는 입력 인수.
- [Client/ExecuteCloudScript](https://learn.microsoft.com/en-us/rest/api/playfab/client/server-side-cloud-script/execute-cloud-script?view=playfab-rest): 인증된 플레이어, Live/Specific revision과 별도의 실행 오류.
- [Server/DeletePlayer](https://learn.microsoft.com/en-us/rest/api/playfab/server/account-management/delete-player?view=playfab-rest): 기본 비활성 API 옵션, 비동기 삭제 접수 및 빈 결과 객체. 타이틀 데이터가 삭제 대상이며 publisher/master 계정·연결·이메일·친구·PlayStream 기록까지 전부 삭제한다고 설명하지 않는다.

## 배포 전 남은 검증

1. 승인된 폐기 가능한 **테스트 타이틀/테스트 계정**을 확정한다. 기존 CloudScript revision을 비공개로 백업하고 후보의 두 handler를 기존 handler와 병합한다. 전체 스크립트를 교체하지 않는다.
2. 후속 배포 승인 범위에서만 테스트 titleId를 고정하고 enabled를 켠 후보 revision을 게시한다. 해당 테스트 타이틀의 Server/DeletePlayer 옵션도 확인한다. 운영 설정·자격 증명을 이 저장소에 넣지 않는다.
3. 실제 Client 호출에서 preflight revision 고정, 인증된 본인만 삭제, 타인 target 인수 거부, 정상 접수 및 바깥 ExecuteCloudScript 오류/응답 유실을 확인한다. 서버 삭제 접수 이후에도 외부 응답 전달이 보장된다고 가정하지 않는다.
4. 실제 기기에서 timeout 후 재시작·재로그인·저장 차단·재전송 없음, 정상 접수 후 owner 한정 정리·No Ads 보존, AccountDeleted 처리 및 문의 버튼을 확인한다. 운영 계정으로 자동 검증하지 않는다.
5. 실제 APK/AAB·기기·스토어 검증은 별도이며 이 문서의 Editor 결과로 대체하지 않는다. #92 기준 빌드와 #91 정책/기기/스토어 검증을 구분한다.

## 2026-09-22 로컬 검증

- checkout: `C:\Users\pc_17\.codex\worktrees\7299\Tamer`
- branch: `codex/cloudscript-account-deletion`, base `fbe6625301e4f4f2bd6b057b487fa204185a3717`
- 구현 커밋: `92c30a8c14a57e783d29fd1c8080fde109efc815`. 최종 검증은 이 커밋에 UI assertion 수정과 Presenter 주석 수정이 미커밋 상태로 추가된 소스에서 수행했다. 동작 코드 변경은 없다.
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Pipeline `0.6.0-exp.1`, Node `v22.20.0`. Android 기준 min24/target36/ARM64이며 이번 실행은 Editor 전용으로 Android SDK 빌드 도구를 실행하지 않았다.
- 권한 있는 원본에서 에셋 복원 verified4561/copied0. 격리 RevivalSmoke 씬에서 검증했고 운영 로그인·저장·구매·광고·삭제 요청은 하지 않았다.
- Node offline 서버 검사 **17/17**, 실제 요청 **0**.
- 신규 C# 최초 **8개 중 7통과/1실패**. UI 검사가 unknown 안내의 “was accepted”까지 성공으로 오인했다. 불확실성 문구 존재 및 정확한 성공 문장 부재를 검사하도록 수정한 뒤 **같은 테스트 두 번째 실행 1/1 통과**. 나머지 7개는 변경 없이 재실행하지 않았다.
- 기존 Flow **17/17**, inventory owner/restart **3/3**, pending login **1/1**, cleanup/No Ads **3/3** 통과. `|` 결합 filter 시도는 미지원으로 0건이어서 통과 집계에 넣지 않고 단일 filter로 실행했다.
- 재컴파일 completed/failed=false/errors=[] 확인. 마지막 Console snapshot은 Log57개이며 최초 실패를 없었던 것으로 표현하지 않는다.
- 해당 checkout Editor PID35928만 정상 종료하고 PID 없음 확인. Editor가 비운 AndroidKeyaliasName만 기존 값으로 복원했으며 ProjectSettings 차이는 없다. 원본·다른 Editor·브라우저·폰은 조작하지 않았다.
- 이번 변경의 APK/AAB는 생성하지 않았으므로 APK SHA-256은 해당 없음. 실제 배포/삭제 완료/응답 유실 기기 검증은 미검증이다.

비공개 증거는 checkout의 `Logs/revival/`에 보존하며 원시 로그는 커밋하지 않는다.

| 파일 | SHA-256 |
|---|---|
| cloudscript-deletion-node.log | 2180ea295aebe6cac58987670c106665944fd706bfa91c24f166192a9d901034 |
| cloudscript-deletion-tests.json | 1bf865eb94dbb7c069414a41dd92aeaa13fcced2cf381a2dadd5837e72baf385 |
| cloudscript-deletion-ui-final-tests.json | 28188815b323ae8f6ec7505f6ff25ac50c2d72616f508281a3670e9edd1542c2 |
| cloudscript-deletion-flow-tests.json | 5741624fe464b26365a31f58f2f8e504f41de88771e8e1a3275c60489e81e778 |
| cloudscript-deletion-inventory-tests.json | 05f2269b5260333450d20389c69c551d84f7635fd0b50c247baf063106f1777f |
| cloudscript-deletion-login-tests.json | e25b90f915d273506b18ac5b807549f6e65a11a1a276b893b9b0a86d14228ada |
| cloudscript-deletion-cleanup-tests.json | b291ae88fdd2a581ffb3245172338d976ae59212702103cc080973ac7f47890a |

## 후속 정리 후보

기본 runtime 구성은 CloudScript로 전환했지만 기존 HTTP gateway, 독립 Python/SQLite 서버, 영수증/AndroidKeyStore 경로 및 관련 검증 코드는 이번에 삭제하지 않았다. 새 경로의 실제 타이틀/기기 검증 및 과거 pending 기록 처리 방침을 확정한 후 미사용 구성 진입점과 전용 테스트부터 제거할 수 있다. 공유 owner/session·원자적 로컬 journal·인벤토리 보호 코드는 유지한다. `.meta`/GUID·비공개 증거·기존 사용자 저장과 권한은 정리 대상에 넣지 않는다.
