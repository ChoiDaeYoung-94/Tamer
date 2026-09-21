# 자동 타이틀 삭제 어댑터와 운영 연결 경계

후속 [최소 운영 연결 검토안](deletion-deployment-plan.ko.md)은 공급자 완료 근거, 호스팅·비용, 삭제 후 상태 인증과 재생성 차단, 미발송 지원 문의를 구체화한다. 실제 배포나 호스팅 결정은 아니다.

2026-09-21 사용자가 이메일 수동 처리 대신 **자동 삭제** 방식을 선택했다. 인증된 본인의 Tamer 타이틀과 자체 관련 데이터가 대상이며 Google·마스터·퍼블리셔 계정 삭제는 포함하지 않는다. 재가입은 새 진행도, No Ads는 명시적 복원과 유효 스토어 구매 검증으로 복구하는 방향을 유지한다. 운영 배포·권한 변경·실제 사용자 삭제·유료 인프라 생성은 이번 작업이 아니다.

## 구현과 합성 검증

`server/privacy/title_deletion_provider.py`는 기존 DeletionService의 provider 계약을 사용한다. Python은 기존 로컬 서비스의 영속 계약을 검증하는 경계이며, 별도 Python 서버 임대를 확정하거나 Classic JS와 병렬 운영하는 결정이 아니다. 네트워크 transport·신선한 인증·완료 확인 callback은 신뢰된 서버 구성에서 주입해야 한다. 기본 서비스/UI는 여전히 비활성이다.

- `bind_confirmed`: 같은 SQLite의 실제 확인된 queued/processing 요청과 계정·title 범위를 대조한다. 서버가 인증한 title/account/title entity를 고정하고, 대상·정책·완료 대상 집합의 변경을 거부한다. 클라이언트 HTTP body에는 이 바인딩 경로가 없다.
- `ServerDeletePlayerSubmission`: 고정된 title의 `/Server/DeletePlayer`와 `PlayFabId` payload만 만든다. master/publisher 삭제나 임의 route를 받지 않는다. 키 로딩·기본 endpoint·실제 HTTP 구현은 없다.
- 삭제 전 `submission_unknown`을 먼저 영속 기록한다. 두 worker 중 하나만 제출하며, timeout·비정상 응답·재시작 후 자동 재제출하지 않는다. 명시적인 미접수 거절만 다시 ready로 되돌린다. 설정 비활성 응답을 보고 자동으로 설정을 변경하지 않는다.
- 동일 계정도 재가입하면 새 title entity일 수 있으므로 제출 직전 서버의 대상 일치·삭제 진행 중 쓰기/재생성 차단 확인을 요구한다. DeletePlayer 자체에는 entity generation 조건이나 중복 방지 키가 없으므로 이 callback을 항상 true로 대체해서는 안 된다.
- `title_player`와 `tamer_owned_data`를 포함한 명시적 완료 대상별 증거를 따로 기록한다. 완료한 대상은 재시작 후 반복하지 않고 남은 대상을 재조정한다. callback은 동일 요청에 대해 멱등이어야 한다.
- 완료 증거는 요청·title·계정·entity·대상 이름이 모두 같아야 한다. 큐 접수, 시간 경과, 계정 조회 부재는 증거가 아니다. 모두 확인돼야 기존 DeletionService가 completed로 전환한다. 클라이언트에는 원시 증거 대신 증거 묶음의 해시를 반환한다. 해시는 서버를 대신하는 독립 서명 검증이 아니다.

`tools/revival/test_title_deletion_provider.py` 최초 실행 **12/12 통과**, 실패0·재시도0. 실제 PlayFab 호출 없이 가짜 transport와 임시 SQLite를 기존 DeletionService에 연결했다. 확인 전/타인/타이틀/정책 변경 차단, 큐 접수와 완료 구분, 두 worker 경합, 응답 유실·재시작, 부분 완료 재처리, 잘못된 증거를 검사했다.

- checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`
- 기준 커밋 `15df0fd87addcce51918bde574385615325b2933`, 검증한 소스 `6db14238d611204a386acdd36991a011782fdd4a`
- 비공개 결과 `Logs/revival/title-deletion-provider-tests.log`
- SHA-256 `6018b58922daeb5889edf62150f2f3f95acce0101b95b4bcab3fa31458a2430a`
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android min24/target36/ARM64 기준은 변경하지 않았다. Editor·Android·APK 실행/생성 없음, 이번 APK 해시 없음

## PlayFab 호스팅 가능성과 실제 관측

[Classic CloudScript](https://learn.microsoft.com/en-us/gaming/playfab/live-service-management/service-gateway/automation/cloudscript/quickstart)는 PlayFab 호스팅 JavaScript이며 Server API에 접근한다. 따라서 삭제 접수에 전용 VM 임대가 필수인 것은 아니다. 공식 [Server/DeletePlayer](https://learn.microsoft.com/en-us/rest/api/playfab/server/account-management/delete-player?view=playfab-rest)는 title 계정 삭제를 큐에 넣고 즉시 반환하며 기본 비활성 기능이다. PlayStream 이력과 publisher 계정은 지우지 않는다. 설치 SDK에서도 같은 계약을 확인했다.

Chrome에서 읽기로 확인한 `12B656`은 **Tamer IAP Test**다. 운영 `67C9A`의 상태로 일반화하지 않는다. 2026-09-21 확인 결과:

- Functions 화면은 등록된 함수 없음
- Classic 수정 버전은 revision 1(live), 기본 샘플에 `context.currentEntity`와 `entity.SetObjects` 호출 존재
- API 기능의 `AllowServerToDeleteUsers`는 꺼짐
- 실행·업로드·배포·저장·secret 조회는 수행하지 않음. 샘플 존재는 실행 성공이나 사용자별 Entity 접근권한 증거가 아님

## 활성화 전에 반드시 연결할 경계

1. **신선한 재인증:** 기존 DeletionFlow는 재인증 adapter와 동일 계정 proof를 요구한다. [AuthenticateSessionTicket](https://learn.microsoft.com/en-us/rest/api/playfab/server/authentication/authenticate-session-ticket?view=playfab-rest)은 유효 티켓의 계정을 확인하지만 그 자체가 방금 사용자가 다시 인증했다는 증거는 아니다. 클라이언트 계정 ID·시각·캐시·LastLogin으로 fresh proof를 만들지 않는다. 인증 공급자 재확인 결과와 서버 발급 일회성 challenge를 묶는 구현이 필요하다.
2. **삭제 후 상태 접근:** 플레이어 자체 저장소에만 대장을 두면 삭제 때 사라진다. 삭제 대상 밖의 접근 제한 영속 대장과 worker 재처리 실행 주체가 필요하다. 상태 조회는 삭제 전에 발급한 고엔트로피 요청 전용 토큰의 해시를 저장하고, 조회 전용·요청 한정·만료/폐기 정책을 적용하는 방향이다. 이 토큰으로 삭제 확인·다른 요청 조회·재가입을 허용하지 않는다. 현재 core는 토큰 발급/보존을 구현하지 않았고 삭제 후 재인증에 의존하는 상태 조회를 해결하지 않았다.
3. **완료 확인:** DeletePlayer 응답에는 작업 ID·완료 영수증이 없다. 공식 완료 확인 수단과 자체 데이터/처리자 증거를 확보하기 전에는 `CompletionProof`를 생성하지 않는다. 프로필 없음만 관측되면 processing을 유지한다. 이번 합성 증거는 실제 소거 증명이 아니다.
4. **안전한 재가입/다기기:** 삭제 진행 중 재업로드·자동 재생성을 막는 서버 제어, 완료 뒤 명시 재가입, 소유자가 확인된 로컬 저장·백업 정리와 구매 복원 경계를 연결해야 한다. 현재 앱 파일/PlayerPrefs/No Ads를 삭제하지 않았다.
5. **배포 검토:** 정확한 대상 title·기능 허용·접근 정책·대장 호스팅·worker·인증 구성·보관 만료를 묶어 통합 검토한다. 요청 대장도 개인정보 처리 대상이며 임의 법정 보관기간이나 무기한 보관 승인을 만들지 않는다. API 허용만 켜서 자동 삭제가 완성됐다고 표시하지 않는다.

Classic에 연결할 때는 위 상태 전이/대상 바인딩/불확실성/증거 계약을 재사용한다. 별도 서버나 Azure가 필요한지는 대장·worker·재인증 구현 수단을 대조해 결정하며, 현재 미배포 Python 계약이 그 선택을 강제하지 않는다. 운영 gateway 활성화, 웹 외부 접수, 실제 기기 UI, 실제 삭제·재가입·스토어 복원은 미검증이다.
