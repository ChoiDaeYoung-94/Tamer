# 전체 복구 실행 목록

사용자는 리팩토링뿐 아니라 남은 복구 작업 전체의 진행을 요청했다. 이 목록의 완료는 코드 PR 수나 빌드 성공만으로 판정하지 않는다. Unity 6000.0.81f1 유지/6.3 설치 보류, CI·App Center 구성 보존/비활성은 명시된 제약이다.

| 작업 | 현재 실행·소유 | 완료에 필요한 실제 근거 |
| --- | --- | --- |
| 자체 C# 전체 감사·리팩토링 | PR151까지 게임플레이 수정 통합, 파일별 감사 121개·meta 누락0. source 76c4b8a의 Editor381/381·실패/skip0, 실제 포획·로컬 저장·메모리 서버 반영·무적 해제 후 사망 복귀 확인 | 원래 포획 확률·밸런스와 운영 서버 진행도는 별도 미검증; 신규 코드마다 감사 목록 유지 |
| Families 광고·consent | PR128 UMP gate 및 sample/control 실기기 확인. startup/control 요청0, 보상1회·Home/취소 복귀 확인 | 운영/지역 동의 UI와 설정 검증. 첫 native 표시→X 관측 오차 구간 4.980722–5.029033초이므로 5초 준수 확정 불가 |
| Data safety·개인정보처리방침·계정 삭제/보관 | 기존 README 공개 정책과 URL 보존. 게시된 운영자 AeDeong·일반 문의 주소는 재사용. PR132–135 삭제 core/서버/UI·합성 HTTP 계약13/13 검증, 기본 운영 비활성 | 기존 정책·현재 Console/실제 처리 대조, 미정인 삭제 접수 경로·범위·보관/재가입/No Ads 방침 결정, 운영 인증/HTTP/provider 연결과 실제 처리 검증·게시 |
| 테스트 로그인·진행도·구매 복원·서버 영수증 | PR155까지 격리 Store 설치·CustomID 수동 연결·로그인·무료 구매·PlayFab 성공 구매1/활성 아이템1·로컬 No Ads·동일 설치 Restore/재로그인 유지 확인 | 독립 acknowledgment 조회, 취소/실패 결제, 삭제 후·다른 기기 복원은 미검증. 개인폰 앱 삭제/초기화 없이 가능한 경계부터 진행. 실제 운영 진행도 서버 저장과 구분하며 내장 영수증 검증에 VPS는 필수가 아님 |
| 기존 Public→Private | 오프라인 audit/preflight/postflight 준비 완료. 승인된 기존 타이틀 계정76개 삭제 후 2026-09-11 10:14 UTC 일반 목록/세그먼트0 확인 | 삭제된76개를 이행 대상으로 유지하지 않음. 이후 실제 대상이 확인되면 모집단·백업·모든 쓰기 주체·동시성 통제를 다시 평가. 현재 시점의 모집단이나 모든 부속 데이터 소거를 과거 UI0으로 단정하지 않음 |
| 업로드 키 reset | 보안 담당 구체안 준비 완료, 소유자 결정 대기 | 안전한 암호화 보관/비밀번호·off-PC 경로, 새 키·공개 PEM 신청 승인, 활성 통지·빌드검증 |
| 실제 16KB 실행 | 4KB ARM64 기기 실행 및 Store 전달 APK2개의 서명/ZIP 16KB 정렬 검사 통과. WHPX 활성화 후 사용자 재부팅 완료, 9월14일 HypervisorPresent=false/가속 검사 exit6, BCD 읽기는 접근 거절로 상태 불명 | 호스트 읽기 진단 후 필요한 변경의 원인·구체 명령을 먼저 검토. 실제 PAGE_SIZE=16384·ABI·호환 모드·실행 근거 필요. release AAB strict RELRO3 실패와 debug APK5 실패를 구분 |
| 최종 출시 AAB·내부 테스트·신고·심사 | PR130 소스10항목 및 후보 AAB 버전·공개 인증서·서명·ABI/LOAD/RELRO 검사 도구 통합. 실제 출시 AAB 미생성, releaseReady=false | 정책·서명·런타임 충족 후 실제 후보 검사, 승인된 업로드/트랙/신고·제출 및 실제 심사 결과 |

정책 선택·로그인·키 회전·운영 이행·스토어 제출은 해당 단계에 진입할 때 검토 가능한 초안과 영향을 먼저 완성하고 필요한 정보·승인을 요청한다. 대기 중에도 독립적인 구현·검증은 계속한다. 리팩토링 PR 종료를 전체 복구 종료로 표현하지 않는다.

README의 개인정보처리방침은 게임 출시 때 마련한 공개 정책 페이지다. 정책 본문과 기존 GitHub URL을 임의 삭제하거나 개발 안내로 대체하지 않는다. 운영자 AeDeong·일반 문의 doeud1410@gmail.com·2024년 9월 30일 시행은 기존 정책에 명시되어 있으므로 재질문하지 않는다. 삭제 접수 서비스나 구체적 보관 기간이 이미 제공된 것으로 추정하지는 않는다. 정책 원문·현재 스토어 설정 대조는 개인정보 담당이 수행한다. 중복 질문을 제외한 [최소 운영 결정](release-preflight.ko.md#사용자가-결정해야-하는-최소-항목)만 남기며, 일반적인 작업 재개를 미답 결정의 승인으로 해석하지 않는다.

과거 기준(2026-09-12): 당시 확인한 통합 기준은 `40b23cc954a2ea9a9a36eca9a58c5ad79ca246da`이다. PR143의 source `db32fcb25342ec07c40bf5c1b13426fe6cfaf0ef`에서 Editor370/370/0skip(3.6837712초), XML SHA-256 `b80efc3a54de77c48fb68aa83125990ee8d90e1b95cffb64862a6f061b4e9598`를 독립 대조했고 병합까지 C# 차이가 없다. 격리 APK SHA-256은 `5424cfffe431bae18c2d2c4e5ca09bbb8c140c2c4e9f0231e9af4221cd9b5bd6`이며 빌드·설치·기동과 로그인 정책 거절을 구분한다. 이번 문서 정정으로 전체 테스트를 반복하지 않았다. 이전 서버/도구 source `d467cfc`의 Python122/122 및 별도 영수증 runtime7개는 해당 실행 시점의 근거이며 현재 main 전체 재실행 결과로 합산하지 않는다. CI는 계속 비활성이다.

근거: [파일별 감사](first-party-audit.ko.md), [광고 동의](consent-gate-validation.json), [오프라인 기기 왕복](gameplay-harness.ko.md), [삭제 core/server](account-deletion-validation.json), [삭제 UI](deletion-ui-validation.json), [영수증 staging](receipt-staging-validation.json), [출시·호스트 점검](release-preflight.ko.md).

최신 후속 근거: [격리 IAP 앱·검증](playfab-iap-test.ko.md), [IAP 실행 수치](playfab-iap-test-validation.json), [기존 타이틀 삭제와 최종 UI 대조](title-player-deletion-execution.ko.md). 기존 타이틀 일괄 삭제는 상시 이용자 삭제 접수 서비스가 운영된다는 증거가 아니다.

과거 준비 완료(2026-09-12, PR146–148): [전용 서명 테스트 AAB](iap-test-bundle.ko.md) 빌드·정적 검사, [수동 게임플레이 하네스와 APK](gameplay-device-followup.ko.md) 준비, [RELRO 원인 분석](iap-relro-analysis.ko.md)과 독립 검토를 통합했다. AAB의 ELF6개 LOAD는 통과하고 strict RELRO3개는 실패한다. 기존 debug APK의 RELRO5개 실패와 산출물을 구분한다. 테스트 계정1개는 관리자 생성됐으나 CustomID 연결과 실제 로그인은 미완료다. 이는 당시 준비 상태이며, 이후 실제 검증 완료 범위는 위 표와 아래 9월14일 기록을 따른다.

2026-09-12의 폰 작업 보류는 9월14일 사용자 명시 재개로 해제됐다. 새 개인폰에서 격리 앱만 검증했으며 앱/데이터 삭제·계정 전환·보호 설정 변경은 하지 않았다. 현재 폰·Chrome·Editor 쓰기는 담당자 간 슬롯을 직렬 배정한다. 업로드 키 reset과 미정 정책·운영 제출은 별도 조건을 유지하며 일반 재개 요청을 승인으로 확대하지 않는다.

추가 합성 HTTP 검증: `e4dcbc8`에서 .NET9 실제 소켓13/13 및 Python122/122(8.123초). Unity C# 동일 소스의 Editor355/355(4.652초)를 별도로 확인했다. Unity 화면 HTTP 클릭·Android HTTP·운영 인증/provider 연결은 미실행이다. [합성 HTTP 근거](deletion-loopback-http-validation.json).


## 2026-09-14 최신 통합과 다음 실행

기준 main은 `731d9cfa3fe66960b2d8096b04295e4b220a16b6`(PR155)이다. [포획 수명 및 실기기 검증](gameplay-capture-lifecycle.ko.md)은 source `76c4b8ada7c24c91ae50e1f661ae37c45bdc0c6a`와 APK/381개 테스트 해시를 기록한다. 보조 무적·고정 roll을 사용한 포획 처리 경로와 원래 확률 검증을 구분한다. [IAP Store 검증](iap-test-bundle.ko.md)은 source `39c2b253e3d6d57e1c3b2e4fc8f2dafe1adac6ef`의 AAB·전달 APK 해시 및 기존370개 테스트와 새 실기기 결과를 구분한다. Confirm 호출은 소스와 관측 결과에 부합하지만 직접 콜백 추적·독립 acknowledgment 상태는 아직 확보하지 못했다.

- SDK: 기존 테스트 주문의 독립 확인, 취소/실패 및 다른 기기 복원 준비를 수행한다. 신규 유료 결제나 개인폰 초기화를 포함하지 않는다.
- 게임플레이/광고: Families·UMP 설정 읽기 감사와 승인된 격리 샘플의 취소·배경 전환·보상 경계를 검증한다. 운영 광고 호출이나 정책 게시로 확대하지 않는다.
- 호스트: 16KB 실행을 막는 가속 환경을 읽기로 진단하고 추가 권한·OS 변경이 필요하면 원인과 구체 명령을 제시한다.
- 통합: 후속 근거와 PR을 검토하며 개인정보·키 보관·출시 결정은 기존 구체안과 연결해 남긴다. 이 백로그 정정은 문서만 변경하며 전체 SDK/Editor 검증을 재실행하지 않았다.
