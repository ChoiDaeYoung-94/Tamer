# CloudScript 삭제 시험 후보와 복구 범위

## 현재 상태

PR #212 이후 시험 연결 준비이다. 기존 `account-deletion.js`의 enabled=false를 유지한다. `deletion-test-gate.js`는 **시험 후보에만** 함께 붙이는 제한이며 운영 배포에 승계하지 않는다. 신규 서버/DB/영수증 서비스는 없다. 실제 시험 타이틀을 읽기 확인하여 원본을 보존한 비활성 결합 후보까지 만들었다. 원격 업로드·활성화·계정 생성·삭제는 수행하지 않았다.

## 기존 소스 보존과 후보 구성

1. 승인된 시험 타이틀을 화면과 요청 대상 양쪽에서 확인한다. 운영 타이틀과 기존 구매/PGS 계정은 제외한다. 실제 타이틀/계정 식별자는 비공개 작업 기록에만 보존한다.
2. [GetCloudScriptVersions](https://learn.microsoft.com/en-us/rest/api/playfab/admin/server-side-cloud-script/get-cloud-script-versions?view=playfab-rest)로 현재 published와 latest의 version/revision을 구분하고, [GetCloudScriptRevision](https://learn.microsoft.com/en-us/rest/api/playfab/admin/server-side-cloud-script/get-cloud-script-revision?view=playfab-rest) 또는 Game Manager에서 원본 파일명/내용·IsPublished·SHA-256을 비공개로 보존한다. published와 latest가 다르면 미게시 변경 소유자를 확인한다.
3. 기존 소스를 그대로 prefix로 보존하고 `account-deletion.js`, `deletion-test-gate.js` 순서로 덧붙인다. 기존에 동일 handler/config 이름이 있거나 top-level 코드·공유변수가 영향을 받으면 자동 결합하지 않는다. 기존 모든 handler의 이름과 내용이 유지되는지 독립 대조한다. `UpdateCloudScript` 문서는 현재 한 파일 제출을 요구하므로 임의로 파일을 추가 분리하지 않는다.
4. 후보의 title pin만 승인된 시험 타이틀로 고정한다. 계정과 활성 시간 미확정 동안 enabled=false를 유지한다. 원격 쓰기를 진행하게 될 때는 published/latest가 백업 시점 그대로인지 재확인한 뒤 [UpdateCloudScript](https://learn.microsoft.com/en-us/rest/api/playfab/admin/server-side-cloud-script/update-cloud-script?view=playfab-rest)의 Publish=false 후보를 먼저 만든다. HTTP/도구 응답 유실 시 중복 업로드하지 않고 revision 목록을 읽어 확인한다.
5. 새 revision을 다시 읽어 파일 hash와 기존 prefix 보존을 대조한다. Specific revision의 config 읽기만으로 비활성 응답을 확인한다. 기존 handler가 읽기 전용인지 확인되기 전 임의 실행하지 않는다. 앱의 Live preflight를 시험하려면 별도의 게시 단계가 필요하며 미게시 Specific 검사와 구분한다.

## 시험 전용 동적 제한

비공개 Title Internal Data 키 `TamerDeletionDisposableTestV1` 한 개를 [Server/GetTitleInternalData](https://learn.microsoft.com/en-us/rest/api/playfab/server/title-wide-data-management/get-title-internal-data?view=playfab-rest)로 매 config/삭제 호출마다 읽는다. 클라이언트가 설정을 쓰거나 삭제 대상을 전달하지 않는다. 키가 없거나 읽기/파싱 오류이면 거부한다.

값은 JSON 문자열이며 아래는 **합성 예시**이다. 시간은 서버 `Date.now()`와 비교하는 Unix 밀리초이다. enabled가 boolean true이고 titleId가 현재 script 및 고정 config와 같으며 playerId가 인증 currentPlayerId와 정확히 같아야 한다. startsAt부터 expiresAt까지 최대 900000ms(15분), 만료 시각은 포함하지 않는다. 실제값에는 토큰·티켓·이메일·CustomId·비밀키를 넣지 않는다.

```json
{"enabled":false,"titleId":"TEST_TITLE","playerId":"DISPOSABLE_PLAYER","startsAt":0,"expiresAt":0}
```

기존 비공개 키가 있다면 덮어쓰지 않고 소유자/값을 확인한다. [Admin/SetTitleInternalData](https://learn.microsoft.com/en-us/rest/api/playfab/admin/title-wide-data-management/set-title-internal-data?view=playfab-rest)는 서버·CloudScript용 비공개 설정이며 기존 키 변경은 별도 원격 쓰기이다. 이번 후보 생성은 이를 호출하지 않는다.

공식 GetTitleInternalData 문서는 최신 값 반영에 최대 1분 지연이 있을 수 있다고 설명한다. 따라서 enabled=false 반영은 즉시 중단 보장이 아니며, 이미 gate를 통과한 요청과 접수된 삭제를 취소할 수도 없다. 짧은 서버 시간 만료와 단일 폐기 계정 제한은 이 잔여 영향을 제한하기 위한 것이다. 서버 idempotency를 추가한 것은 아니다.

## 복구 가능한 것과 불가능한 것

- 이전 published version/revision을 [SetPublishedRevision](https://learn.microsoft.com/en-us/rest/api/playfab/admin/server-side-cloud-script/set-published-revision?view=playfab-rest)으로 복원하면 이후 Live 선택을 되돌릴 수 있다. 이미 받은 Specific revision을 폐기하는 효과는 없다.
- 시험 gate의 비공개 설정을 비활성화하면 해당 값을 새로 읽은 호출을 거부한다. 캐시 지연과 진행 중 요청은 남으며, 추가 호출을 보내서 삭제 경로 중단을 확인하지 않는다. config 읽기와 서버 만료 경과로 확인한다.
- 시험 앱 중단은 해당 기기에서의 실행을 멈추는 조치이며 서버 차단 보장이 아니다.
- Server/DeletePlayer API 옵션의 실제 존재·현재 값·타이틀 전체 영향은 읽기 확인 후 판단한다. 변경하지 않은 옵션을 임의로 되돌리지 않는다. 이 옵션의 원복으로 즉시 차단된다고 사전 단정하지 않는다.
- 접수된 삭제와 이미 지워진 계정은 revision 원복으로 복구되지 않는다. 미게시 후보도 Specific 호출 가능성을 고려하고, 고정 비활성 또는 만료 제한을 유지한다.

## 삭제 전용 신규 계정과 격리 앱 범위

계정은 아직 선정·생성하지 않았다. 필요한 대상은 승인된 시험 타이틀에 **새로 만든 폐기 가능 계정 한 개**이다. 기존 PGS/IAP/구매·No Ads·게임 저장 시험 계정과 분리하고 Google/PGS/이메일/휴대폰 및 구매 연결이 없어야 한다. 기존 식별자 재사용이나 기존 회원 중 선택은 하지 않는다.

생성 전에는 전용 임의 CustomId 방식 등 가능한 신규 계정 생성 경로와 타이틀 정책을 확인한다. 생성 후에는 새 계정 생성 결과, 정확한 PlayFabId/title, 생성시각, 외부연결 없음, 구매·실사용 저장 없음, 기존 시험 계정과 불일치를 비공개로 대조한다. 이 PlayFabId 한 개만 gate 후보에 넣고 총괄에 삭제 범위를 보고한다. 신규 생성 성공을 실제 삭제 승인으로 취급하지 않는다.

격리 앱은 별도 applicationId·debug signing·격리 시작 씬·전용 로컬 저장 공간을 사용하고 PGS/광고/결제 초기화를 배제한다. 기존 receipt/PGS/game-save 앱을 재사용하거나 데이터를 지우지 않는다. 신규 계정 생성은 검토된 별도 준비 단계이고 앱 재로그인은 CreateAccount=false 및 expected PlayFabId 정확 일치를 요구한다. 현재 기존 harness만으로 실제 삭제 UI 전 과정을 검증할 수 있다고 주장하지 않는다. 후보 revision과 실제 계정 준비 뒤 필요한 최소 harness 구현 범위를 확정한다.

검증 순서는 비파괴 config 확인 → 검토된 폐기 계정과 정상 접수 범위 확인 → 승인된 명시 삭제 한 번 → 정상 접수 시 로그아웃/owner 정리 및 불명확 응답 시 데이터 보존/저장 차단으로 나눈다. timeout을 이유로 재삭제하지 않는다. 응답 유실은 별도 합성 경로로 먼저 확인하고 실제 계정 삭제를 반복하지 않는다. 각 테스트 2회 실패 시 중단한다.

## 로컬 검증 증거

### 시험 타이틀 읽기와 비활성 결합 후보

2026-09-22 별도 브라우저 탭으로 승인된 시험 타이틀만 확인하고 종료했다. 기존 PGS/Cloud/PlayFab 탭과 휴대폰은 변경하지 않았다. Classic revision 목록에는 **수정 버전 1(실시간)** 하나만 표시되었다. UI 코드 편집기의 전체 선택/복사로 원본 14044문자와 14개 handler를 비공개로 보존했고 후보의 네 가지 이름과 충돌하지 않았다. API의 version 번호·원본 Filename은 UI에서 확보하지 않았으므로 추정하지 않는다.

API 기능의 **서버가 플레이어 계정을 삭제하도록 허용은 unchecked**, **클라이언트/LoginWithCustomId를 사용하여 플레이어 만들기 비활성화는 checked**였다. 설정 변경/저장은 하지 않았다. 따라서 원격 삭제 실행은 현재 설정 그대로 진행할 수 없으며, 클라이언트 신규 생성 차단을 풀지 않는 서버 측 계정 준비 경로를 먼저 검토해야 한다. 옵션 활성화는 타이틀 전체 Server/DeletePlayer 호출에 영향을 줄 수 있고 후보의 단일 계정 gate와 범위가 다르다. 임의로 켜지 않는다.

비공개 `cloudscript-test-merged-disabled.js`는 원본을 그대로 prefix로 두고 세미콜론 경계를 넣은 뒤 title pin을 지정한 삭제 소스와 시험 gate를 붙였다. 기본 비활성이다. Node 구문 검사와 VM 등록 검사에서 원본 prefix 일치, 기존 14개 handler의 함수 소스 동일, 새 handler 2개만 추가, enabled=false를 확인했다. handler 자체를 실행하지 않았고 원격 요청은 0이다. 이 검사는 원격 엔진·기존 실제 호출의 무영향을 보증하지 않으며, 게시 전 최신 원본과 다시 대조해야 한다.

모든 파일은 `Logs/revival/` 아래 비공개이며 실제 타이틀 pin이 있는 후보와 원본을 커밋하지 않는다.

| 파일 | SHA-256 |
|---|---|
| cloudscript-test-existing-revision1.js | e3ea3f0a07948523d1a4b93b64d5642b97cfa28cf94a1b7483fbecdf90397604 |
| cloudscript-test-api-features.txt | 2c24d448fdff3d9874df9cb64e363d36a936a09e8dece6387923f93974d6e72a |
| cloudscript-test-merged-disabled.js | 5e5091fb141013f554c6a6815311d311ae727e74393f21e0d020fa769786dabf |
| cloudscript-test-merge-check.log | 295a67d5139dea9ef07484cbe1f5528c7a8516e8a391ddc6d8bf88b5da22966c |

### 코드 검사

- checkout `C:\Users\pc_17\.codex\worktrees\7299\Tamer`, branch `codex/cloudscript-test-candidate`, base `21cabeb2bb3e9ab9689757bded26e4e9f2a4a48a`.
- 2026-09-22, Node v22.20.0, `node tools/revival/test_cloudscript_deletion_gate.cjs`: 첫 실행 **15/15 통과**, 실제 요청0. 미설정/오류/다른계정/다른타이틀/미래시작/만료/장시간/형식오류 거부, 기존 handler 보존, config 비삭제, 매 호출 재읽기를 확인했다.
- 비공개 `Logs/revival/cloudscript-test-gate-checks.log` SHA-256 `dadf8b7f09abb35084d5d0ec6b99068869cc4cbbdeca63ad6154768882382b76`.
- Unity/Android 실행 및 APK 생성 없음. Unity 기준6000.0.81f1/CLI1.0.0-beta.8/Android min24 target36 ARM64는 유지하며 APK SHA-256은 해당 없음. 기존 PR212 검증을 이 후보의 실제 배포·기기 검증으로 대체하지 않는다.
