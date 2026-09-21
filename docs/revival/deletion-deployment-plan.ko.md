# 자동 삭제의 최소 운영 연결 검토안

2026-09-21 최종 사용자 범위 갱신: **정상 삭제 API 접수 안내 후 로그아웃·소유 확인된 해당 계정 로컬 정리로 흐름을 종료**한다. 실제 소거 완료라고 표시하지 않는다. 완료 observer/완료 통지/지원 문의/완료 대기 worker는 필수 작업에서 제외한다. 아래 이전 완료 증거·삭제 후 조회·완료 후 재가입 조건은 과거 강한 보장 검토 이력이며 현재 출시 필수 조건이 아니다. unknown은 접수 성공으로 표시하거나 로컬 데이터를 정리하지 않고 중복 재제출을 막는다. 정상 접수 뒤 완료 증거 부재만으로 재가입을 영구 차단하지 않는다. Google/master 계정·owner 없는 legacy·독립 스토어 권한 증거는 정리하지 않는다. 현재 연결 산출물과 미검증 범위는 [접수 전용 연결 묶음](../../server/privacy/deployment/README.ko.md)을 따른다.

2026-09-21. PR #190의 영속 provider 계약을 실제 서비스에 연결하기 위한 **미배포 검토안**이다. 기준 main `daacc3e8b189b682749ca3a34fccdd7d9017d152`, checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`. 호스팅 선택·예산·정책 변경은 확정하지 않았다. 코어/합성 테스트를 추가하지 않았으며 Editor·기기·APK 실행 없음, 새 APK 해시 없음.

## 먼저 해소할 차단 조건

현재 확정 가능한 결론은 **호스팅을 선택해도 타이틀 삭제 완료 증거 문제가 자동으로 해결되지는 않는다**는 것이다. [Server/DeletePlayer](https://learn.microsoft.com/en-us/rest/api/playfab/server/account-management/delete-player?view=playfab-rest)는 비동기 큐 접수와 빈 결과만 문서화한다. [공식 삭제 안내](https://learn.microsoft.com/en-us/gaming/playfab/data-analytics/privacy-compliance/gdpr-deleting-player-data)의 JobReceiptId·완료 이메일·Webhook은 DeleteMasterPlayerAccount 설명이다. [title_deleted_master_player](https://learn.microsoft.com/en-us/xbox/playfab/api-references/events/playeridentity/title-deleted-master-player)의 PlayerId도 master ID다. 이 근거를 타이틀 삭제에 적용하거나 완료 확인을 얻기 위해 마스터 삭제로 확대하지 않는다.

공개 API·이벤트 문서 조사에서 타이틀 전용 완료 확인 계약은 찾지 못했다. 존재하지 않는다는 단정은 아니다. 아래 지원 문의 답변으로 요청/대상 세대와 결합 가능한 완료 근거를 확보하기 전에는 `title_player` 증거를 발행하지 않는다. 계정 조회 부재, 경과 시간, 재가입 성공, Scheduled Task 완료는 소거 완료 증거로 쓰지 않는다. 재가입을 관측 목적으로 시도하지 않는다.

## 기존 PlayFab에서 가능한 부분과 남는 부분

| 구성 | 확인된 기능 | 운영 연결에 부족한 부분 |
| --- | --- | --- |
| 삭제 제출 | [Classic CloudScript](https://learn.microsoft.com/en-us/gaming/playfab/live-service-management/service-gateway/automation/cloudscript/quickstart)는 PlayFab에서 실행하고 Server API를 호출할 수 있다 | 신선한 본인 재인증, 대상 entity 고정, 완료 observer, 재생성 차단은 별도 연결 필요 |
| worker 실행 | [CreateCloudScriptTask](https://learn.microsoft.com/en-us/rest/api/playfab/admin/scheduled-task/create-cloud-script-task?view=playfab-rest)는 함수/인자를 UTC 일정으로 실행한다 | 요청별 영속 작업 목록·동시성·실패 재처리·만료는 직접 구현해야 한다. task 완료는 함수 실행 완료일 뿐이다 |
| 대장 후보 | [SetObjects](https://learn.microsoft.com/en-us/rest/api/playfab/data/object/set-objects?view=playfab-rest)는 entity와 ExpectedProfileVersion 조건을 받는다 | 삭제 대상 밖 entity의 권한·수명·용량·색인/대기열·실제 Classic 호출 권한이 입증되지 않았다. 플레이어 UserData와 삭제될 player entity에는 대장을 두지 않는다 |
| 삭제 후 상태 | 삭제 전 인증된 플레이어의 Classic 호출은 후보 | 삭제된 플레이어 로그인에 의존하지 않는 공개 HTTPS 조회 경로와 요청 전용 토큰 검증이 필요하다. 새 익명 플레이어를 만들어 조회시키지 않는다 |

따라서 Classic은 삭제 제출과 일정 실행에 활용 가능하지만, **현재 확인한 구성만으로 PlayFab 단독 완결은 입증되지 않았다**. 공개 TitleData에 요청/토큰을 저장하거나 서버 키를 앱·웹에 넣는 우회는 사용하지 않는다.

## 최소 연결 후보

사용자가 **기존 서버가 없다고 답변한 상태**다. 기존 HTTPS/DB 보유를 다시 질문하거나 재사용을 선행 조건으로 두지 않는다. 신규 최소안은 **Azure Functions 앱 하나 + 외부 영속 대장 하나**를 검토 후보로 둔다. HTTP 함수가 웹/앱 접수·재인증·상태 조회를 맡고, timer worker가 같은 대장의 진행 요청을 재조정한다. 별도 Classic 제출 서비스와 Python 운영 서비스를 동시에 두는 구성을 기본으로 삼지 않는다. 외부 서버가 직접 Server/DeletePlayer를 호출한다면 고정 title의 비밀을 서버 비밀 저장소에만 둔다. 공급자 완료 계약과 신규 구성의 권한·지역·비용·배포 산출물을 구체화한 뒤 해당 구성 생성을 승인받는 순서이며, 현재 유료 생성은 승인되지 않았다.

Azure Table Storage를 선택하는 경우 [ETag와 같은 partition의 원자적 일괄 변경](https://learn.microsoft.com/en-us/azure/storage/tables/table-storage-design)을 사용해 계정별 진행 요청 하나, 요청/정책/entity 바인딩, 제출 전 unknown, 완료 대상별 증거를 보존해야 한다. 현재 SQLite 구현을 Functions 임시 파일에 복사해 운영하지 않는다. 이는 저장 adapter 전환이 필요한 후보이며 현재 코드가 곧바로 배포 가능하다는 뜻이 아니다. 여러 리전/별도 메시지 큐/VM은 현재 최소안에 포함하지 않는다.

구현 순서는 다음과 같다.

1. 공급자 완료 계약과 실제 인증 공급자의 재확인 수단을 확정한다. 서버 challenge에 연결한 일회성 인증 결과를 검증하고, 캐시된 PlayFab ticket이나 클라이언트 시각을 fresh proof로 쓰지 않는다. 기기 ID/CustomID 보유만으로 재인증을 대체하지 않는다.
2. 삭제 확인 트랜잭션에서 요청·대상 세대·정책·쓰기 차단을 묶고, 진행 중인 기존 쓰기가 종료됐음을 확인한 뒤 제출한다. 서버 worker는 제출 전 unknown을 영속 기록한다. 실패 시 차단을 자동 해제하거나 동일 계정 ID로 재제출하지 않는다.
3. 삭제 전에 256-bit 난수 상태 토큰을 발급하고 서버에는 해시·requestId·만료·폐기 상태만 보존한다. HTTPS body/보안 헤더로 전달하며 URL·로그·분석 이벤트에 넣지 않는다. 삭제 후에는 이 토큰으로 해당 요청의 최소 상태만 조회한다. 확인/취소/재가입 권한은 없으며, 유실 복구와 만료 기간은 운영 정책 확정이 필요하다.
4. 독립 대장에서 worker가 남은 완료 대상만 처리하고, 공급자 근거와 자체 데이터 처리 증거가 모두 맞을 때 완료로 바꾼다. 근거 미도착은 processing/운영 확인 대상으로 유지한다. status 토큰 만료가 작업 취소나 재가입 허용을 뜻하지 않는다.
5. 완료 후 새 진행도 재가입은 별도 사용자 동작과 서버 허가로 처리한다. 상태 토큰을 재가입 인증으로 사용하지 않는다. No Ads는 별도 명시적 스토어 복원 흐름을 유지한다.

`Assets/Scripts/Login/Login.cs`의 LoginWithAndroidDeviceID/LoginWithCustomID는 현재 client API와 CreateAccount 분기를 사용하고, `Assets/Scripts/Managers/ServerManager.cs`의 WriteToPlayFab은 client UpdateUserData를 호출한다. **현재 앱 경로에는 서버 대장 차단이 적용되지 않는다.** UI 버튼 비활성만으로 다른 기기·구버전·진행 중 쓰기를 막을 수 없다. 삭제 gateway를 열기 전에 로그인/재가입 및 데이터 쓰기 진입점과 PlayFab API 정책의 우회 차단안을 함께 검토해야 한다. 정책 변경·기존 사용자 로그인 차단은 이번 조사에서 실행하지 않았다. 공급자의 AccountDeleted 임시 오류는 자체 서버 차단을 대체하지 않는다.

## 무료 범위와 비용 조건

아래는 2026-09-21 공개 문서 관측이며 계정의 계약·지역별 견적이 아니다.

| 후보 | 공식 무료 범위 | 과금/불확실성 |
| --- | --- | --- |
| PlayFab 기존 Development | [Development 문서](https://learn.microsoft.com/en-us/xbox/playfab/pricing/development-mode): 누적 생성 1,000명, 최대 10개 title, CloudScript 200,000회/20,000 GB-s 등 제한 | 같은 문서의 도입부 100명과 표/본문 1,000명이 불일치한다. 2026-03-11 Foundation 대체 안내도 있으므로 실제 기존 title의 mode/한도를 확인해야 한다. 삭제로 누적 생성 한도가 복구된다고 가정하지 않는다 |
| PlayFab Live PAYG | [가격표](https://developer.microsoft.com/en-us/games/products/playfab/pricing/): 월 최소 없음 | Classic $0.22/백만 실행 + $0.022/천 GB-s, 별도 데이터/이벤트 미터. Standard는 $99/월이며 지원 티켓 포함, PAYG는 티켓 미포함. 무료 티켓을 위해 임의 업그레이드하지 않는다 |
| Azure Functions | [가격표](https://azure.microsoft.com/en-us/pricing/details/functions/): Flex on-demand 구독 합산 월 250,000회/100,000 GB-s; 기존 Consumption은 1백만 회/400,000 GB-s | 무료 할당은 유료 consumption 구독 조건이 붙는다. Storage는 제외되며 네트워크도 별도, Always Ready는 별도 미터다. 지역별 단가가 공개 페이지에서 `$-`로 표시돼 현재 계정 최종 금액은 확정할 수 없다 |

월 작업량 견적은 요청 수 × HTTP/worker 재처리 횟수 + timer 기본 실행 수, 평균 메모리×실행 시간, DB 읽기/쓰기·보관량, 로그/네트워크를 각각 계산해야 한다. 무료 compute 내라도 저장/로그 비용까지 0원이라고 약속하지 않는다. [Foundation 안내](https://learn.microsoft.com/en-us/xbox/playfab/get-started/foundation-onboarding)는 기존 Tamer의 적용 자격·이전 성공을 증명하지 않으며 Android 운영 무료 보장으로 쓰지 않는다.

읽기 접근 확인 결과: PlayFab의 기존 로그인으로 타이틀 목록에 접근했고 Tamer와 Tamer IAP Test 모두 개발 표시를 확인했다. 화면의 생성 한도는 각각 100K와 1K로 서로 달랐다. 공개 문서의 단일 한도를 실제 계정에 덮어 적용하지 않는다. 청구 요약은 로드 중 상태에서 요금제를 확인하지 못했으며, 결제 정보는 열지 않았다. Azure Portal은 로그인 화면이어서 기존 구독의 존재·권한·무료 할당·지역을 확인하지 못했다. 자격 입력이나 추가 로그인, 설정 변경, 자원 생성은 하지 않았다. 계정 원시 화면/감사 로그는 공개 문서에 첨부하지 않는다.

## 공급자 지원 문의 초안 — 미발송

2026-09-21 [공식 지원 안내](https://learn.microsoft.com/en-us/xbox/playfab/pricing/support)를 확인했다. 발송 경로별 승인 범위는 아래처럼 구분한다. 현재 어느 경로에도 발송하지 않았으며 새로운 Chrome 접근·가입·업그레이드를 수행하지 않았다.

| 경로 | 정확한 목적지와 수신 대상 | 승인할 행동과 현재 제약 |
| --- | --- | --- |
| 공개 커뮤니티 기술 질문 | 공식 문서가 연결한 [Microsoft/Xbox Game Dev Discord 초대](https://discord.gg/xboxgamedev), 이동 목적지 `https://discord.com/invite/xboxgamedev`; 가입 후 PlayFab category의 support 채널, 커뮤니티 참여자가 열람 | **아래 기술 문의 초안을 공개 커뮤니티에 게시하는 승인**을 별도로 받는다. 무료 Discord 계정이 필요하며 로그인/참여 여부와 정확한 채널 URL은 미확인이다. 제목·본문만 게시하고 이용자 ID·계정 화면·키·로그를 첨부하지 않는다. 가입/규약 동의가 필요하면 게시 승인과 별개로 처리한다 |
| 인증된 기술 지원 티켓 | [PlayFab Game Manager](https://developer.playfab.com/) → 해당 title Overview → 오른쪽 위 `?` → `Contact Us`, PlayFab 기술 지원팀 | **아래 초안을 인증된 기술 티켓으로 제출하는 승인** 대상이다. 공식 최소 요금제는 Standard이며 실제 계정 요금제/티켓 권한은 미확인이다. 문서화된 진입 경로까지만 확인했고 내부 폼 URL은 추측하지 않는다. 권한이 없으면 중단하며 업그레이드하거나 공개 게시로 자동 전환하지 않는다 |
| 계정/요금제 문의 | Game Manager → Studio `...` → `Account Help`, PlayFab 계정 지원팀 | 모든 개발자 계정에 제공되는 비기술 경로다. 기술 초안을 그대로 보내는 대체 수단이 아니다. 필요 시 별도 문안 **“현재 스튜디오 요금제와 기술 지원 티켓 사용 자격을 확인해 주세요. 요금제 변경이나 업그레이드는 요청하지 않습니다.”**를 이 경로로 보내는 승인을 받는다 |

현재 별도 유료 전환 없이 기술 문의를 승인받을 수 있는 후보는 공개 Discord 게시다. 비공개 기술 티켓을 선호하면 기존 권한 확인을 전제로 위 티켓 경로를 선택한다. 지원 경로 선택/문의 발송 승인은 서버 생성·유료 요금제 전환 승인을 포함하지 않는다. 커뮤니티 답변은 참고 근거로 구분하고, 완료 계약은 공식 문서 또는 공급자의 명확한 확인과 대조한다.

제목: Server/DeletePlayer의 타이틀 전용 완료 증거와 재생성 경계 확인

Tamer 타이틀의 인증된 이용자 자동 삭제를 구현하고 있습니다. 삭제 범위는 해당 title_player_account와 타이틀 데이터로 제한하며 master/publisher/다른 타이틀 계정을 삭제하지 않습니다. Server/DeletePlayer는 큐 접수 후 빈 결과를 반환하고, 공개 완료 알림 안내는 DeleteMasterPlayerAccount에 해당하는 것으로 확인했습니다.

1. Server/DeletePlayer 완료를 서버에서 검증할 수 있는 공식 이벤트·상태 API·영수증 또는 권장 방법이 있습니까? 요청/PlayFabId/title entity 세대와의 결합 방식, 전달 인증, 중복·지연·유실 시 조회/재전달 계약도 부탁드립니다.
2. GetUserAccountInfo의 부재 또는 AccountDeleted 오류 해제가 모든 해당 타이틀 데이터 삭제 완료를 의미한다는 보장이 있습니까? 없다면 어떤 관측을 완료 근거로 삼아야 합니까?
3. 응답 유실 뒤 같은 publisher 계정이 타이틀에 재가입했을 때 이전 삭제 요청이 새 title entity에 영향을 주는지, 기존 세대만 조건부 삭제하거나 안전하게 접수 여부를 확인할 방법이 있습니까?
4. 타이틀 범위 Entity Objects/Files, Economy 데이터, 백업·하위 처리자·PlayStream 잔존의 정확한 처리 범위와 타이틀만 대상으로 하는 후속 절차를 확인하고 싶습니다. 마스터 삭제로 범위를 확대하지 않는 방법이 필요합니다.
5. 기존 client login/create와 UpdateUserData를 쓰는 구버전에 대해 삭제 진행 중 재생성/쓰기 차단을 서버에서 강제하는 지원 방법이 있습니까?

실제 이용자 ID, 토큰, 키, 원시 로그는 첨부하지 않았습니다. 공개 커뮤니티 게시나 지원 티켓 발송은 별도 승인 전 실행하지 않습니다.

## 배포 검토에 넘길 산출물

공급자 답변 → 인증 공급자/호스팅·예산 선택 → 최소 권한과 API 차단 diff → 외부 대장 schema/원자성 및 token 만료 정책 → 미활성 배포 manifest/롤백 절차 → 승인된 비운영 계정의 end-to-end 시험 순서다. 활성화 전에는 unavailable을 유지한다. kill switch는 새 접수·제출을 중지하되 이미 접수된 요청의 상태/증거를 지우지 않아야 한다. unknown 요청을 초기화하거나 전체 재시도하는 롤백은 허용하지 않는다.
