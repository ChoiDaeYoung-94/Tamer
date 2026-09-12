# 전체 복구 실행 목록

사용자는 리팩토링뿐 아니라 남은 복구 작업 전체의 진행을 요청했다. 이 목록의 완료는 코드 PR 수나 빌드 성공만으로 판정하지 않는다. Unity 6000.0.81f1 유지/6.3 설치 보류, CI·App Center 구성 보존/비활성은 명시된 제약이다.

| 작업 | 현재 실행·소유 | 완료에 필요한 실제 근거 |
| --- | --- | --- |
| 자체 C# 전체 감사·리팩토링 | PR143까지 통합, 파일별 감사 121개·meta 누락0. source db32fcb의 Editor370/370·실패/skip0 확인 | 실전투·포획·사망 등 아직 실행하지 않은 격리 기기 경계는 게임플레이 담당이 검증; 신규 코드마다 감사 목록 유지 |
| Families 광고·consent | PR128 UMP gate 및 sample/control 실기기 확인. startup/control 요청0, 보상1회·Home/취소 복귀 확인 | 운영/지역 동의 UI와 설정 검증. 첫 native 표시→X 관측 오차 구간 4.980722–5.029033초이므로 5초 준수 확정 불가 |
| Data safety·개인정보처리방침·계정 삭제/보관 | 기존 README 공개 정책과 URL 보존. 게시된 운영자 AeDeong·일반 문의 주소는 재사용. PR132–135 삭제 core/서버/UI·합성 HTTP 계약13/13 검증, 기본 운영 비활성 | 기존 정책·현재 Console/실제 처리 대조, 미정인 삭제 접수 경로·범위·보관/재가입/No Ads 방침 결정, 운영 인증/HTTP/provider 연결과 실제 처리 검증·게시 |
| 테스트 로그인·진행도·구매 복원·서버 영수증 | PR129 오프라인 Main↔Game 왕복2회. PR131 staging 소켓/CLI7개 통과. PR139–143에서 전용 PlayFab 타이틀·미공개 .iaptest 앱·영구 catalog·Google add-on 및 격리 구매 앱 구성 완료. 수정 APK의 로그인 요청은 PlayerCreationDisabled 응답, 스토어 초기화/구매0 | SDK 담당이 테스트 AAB·독립 서명 준비와 정적 검증 진행. 승인된 테스트 계정·라이선스 테스터·Google 상품이 준비된 뒤 실제 로그인·구매/복원 검증. PlayFab 내장 검증 경로에 별도 VPS/HTTPS 호스트는 필수가 아님. 게임 데이터는 메모리 서버이므로 실제 진행도 서버 저장 검증과 구분 |
| 기존 Public→Private | 오프라인 audit/preflight/postflight 준비 완료. 승인된 기존 타이틀 계정76개 삭제 후 2026-09-11 10:14 UTC 일반 목록/세그먼트0 확인 | 삭제된76개를 이행 대상으로 유지하지 않음. 이후 실제 대상이 확인되면 모집단·백업·모든 쓰기 주체·동시성 통제를 다시 평가. 현재 시점의 모집단이나 모든 부속 데이터 소거를 과거 UI0으로 단정하지 않음 |
| 업로드 키 reset | 보안 담당 구체안 준비 완료, 소유자 결정 대기 | 안전한 암호화 보관/비밀번호·off-PC 경로, 새 키·공개 PEM 신청 승인, 활성 통지·빌드검증 |
| 실제 16KB 실행 | 4KB ARM64 smoke 완료. 사용자 승인으로 2026-09-11 WHPX 활성화 성공, RestartNeeded=true. 재부팅은 미실행 | 재부팅 시점 승인 후 환경 재확인. 실제 PAGE_SIZE=16384·ABI·호환 모드·APK/split 실행 필요. x86_64 AVD는 ARM64 증명이 아니며 정적 RELRO5 실패도 남음 |
| 최종 출시 AAB·내부 테스트·신고·심사 | PR130 소스10항목 및 후보 AAB 버전·공개 인증서·서명·ABI/LOAD/RELRO 검사 도구 통합. 실제 출시 AAB 미생성, releaseReady=false | 정책·서명·런타임 충족 후 실제 후보 검사, 승인된 업로드/트랙/신고·제출 및 실제 심사 결과 |

정책 선택·로그인·키 회전·운영 이행·스토어 제출은 해당 단계에 진입할 때 검토 가능한 초안과 영향을 먼저 완성하고 필요한 정보·승인을 요청한다. 대기 중에도 독립적인 구현·검증은 계속한다. 리팩토링 PR 종료를 전체 복구 종료로 표현하지 않는다.

README의 개인정보처리방침은 게임 출시 때 마련한 공개 정책 페이지다. 정책 본문과 기존 GitHub URL을 임의 삭제하거나 개발 안내로 대체하지 않는다. 운영자 AeDeong·일반 문의 doeud1410@gmail.com·2024년 9월 30일 시행은 기존 정책에 명시되어 있으므로 재질문하지 않는다. 삭제 접수 서비스나 구체적 보관 기간이 이미 제공된 것으로 추정하지는 않는다. 정책 원문·현재 스토어 설정 대조는 개인정보 담당이 수행한다. 중복 질문을 제외한 [최소 운영 결정](release-preflight.ko.md#사용자가-결정해야-하는-최소-항목)만 남기며, 일반적인 작업 재개를 미답 결정의 승인으로 해석하지 않는다.

2026-09-12 재개 시 확인한 통합 기준은 `40b23cc954a2ea9a9a36eca9a58c5ad79ca246da`이다. PR143의 source `db32fcb25342ec07c40bf5c1b13426fe6cfaf0ef`에서 Editor370/370/0skip(3.6837712초), XML SHA-256 `b80efc3a54de77c48fb68aa83125990ee8d90e1b95cffb64862a6f061b4e9598`를 독립 대조했고 병합까지 C# 차이가 없다. 격리 APK SHA-256은 `5424cfffe431bae18c2d2c4e5ca09bbb8c140c2c4e9f0231e9af4221cd9b5bd6`이며 빌드·설치·기동과 로그인 정책 거절을 구분한다. 이번 문서 정정으로 전체 테스트를 반복하지 않았다. 이전 서버/도구 source `d467cfc`의 Python122/122 및 별도 영수증 runtime7개는 해당 실행 시점의 근거이며 현재 main 전체 재실행 결과로 합산하지 않는다. CI는 계속 비활성이다.

근거: [파일별 감사](first-party-audit.ko.md), [광고 동의](consent-gate-validation.json), [오프라인 기기 왕복](gameplay-harness.ko.md), [삭제 core/server](account-deletion-validation.json), [삭제 UI](deletion-ui-validation.json), [영수증 staging](receipt-staging-validation.json), [출시·호스트 점검](release-preflight.ko.md).

최신 후속 근거: [격리 IAP 앱·검증](playfab-iap-test.ko.md), [IAP 실행 수치](playfab-iap-test-validation.json), [기존 타이틀 삭제와 최종 UI 대조](title-player-deletion-execution.ko.md). 기존 타이틀 일괄 삭제는 상시 이용자 삭제 접수 서비스가 운영된다는 증거가 아니다.

추가 사용자 입력 없이 진행할 작업은 테스트 AAB 준비·정적 검사, 격리 전투/포획/사망 검증, 결과의 독립 검토와 문서 통합이다. SDK와 게임플레이 담당은 Editor/기기 슬롯을 직렬 사용한다. 재부팅, 업로드 키 reset, 미정 보관 정책, 실제 테스터 확정과 스토어 제출은 별도 조건이 남아 있다. 거절된 CustomID 전달을 다른 경로로 우회하지 않으며 무응답을 승인으로 취급하지 않는다.

추가 합성 HTTP 검증: `e4dcbc8`에서 .NET9 실제 소켓13/13 및 Python122/122(8.123초). Unity C# 동일 소스의 Editor355/355(4.652초)를 별도로 확인했다. Unity 화면 HTTP 클릭·Android HTTP·운영 인증/provider 연결은 미실행이다. [합성 HTTP 근거](deletion-loopback-http-validation.json).
