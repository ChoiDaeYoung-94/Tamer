# 전체 복구 실행 목록

사용자는 리팩토링뿐 아니라 남은 복구 작업 전체의 진행을 요청했다. 이 목록의 완료는 코드 PR 수나 빌드 성공만으로 판정하지 않는다. Unity 6000.0.81f1 유지/6.3 설치 보류, CI·App Center 구성 보존/비활성은 명시된 제약이다.

| 작업 | 현재 실행·소유 | 완료에 필요한 실제 근거 |
| --- | --- | --- |
| 자체 C# 전체 감사·리팩토링 | PR119–129 및 삭제 core/UI PR132–133 통합, 파일별 감사 113개·meta 누락0. 최신 삭제 포함 Editor355/355 | 실전투·포획·사망 등 아직 실행하지 않은 기기 경계를 별도 검증; 신규 코드마다 감사 목록 유지 |
| Families 광고·consent | PR128 UMP gate 및 sample/control 실기기 확인. startup/control 요청0, 보상1회·Home/취소 복귀 확인 | 운영/지역 동의 UI와 설정 검증. 첫 native 표시→X 관측 오차 구간 4.980722–5.029033초이므로 5초 준수 확정 불가 |
| Data safety·개인정보처리방침·계정 삭제/보관 | 기존 README 공개 정책과 URL 보존. 게시된 운영자 AeDeong·일반 문의 주소는 재사용. PR132–135 삭제 core/서버/UI·합성 HTTP 계약13/13 검증, 기본 운영 비활성 | 기존 정책·현재 Console/실제 처리 대조, 미정인 삭제 접수 경로·범위·보관/재가입/No Ads 방침 결정, 운영 인증/HTTP/provider 연결과 실제 처리 검증·게시 |
| 테스트 로그인·진행도·구매 복원·서버 영수증 | PR129 오프라인 원본 Main↔Game 기기 왕복2회. PR131 staging 서버 실행·백업·복구와 실제 로컬 소켓/CLI7개 통과 | 별도 Play 테스트 앱/PlayFab 타이틀·계정·HTTPS 호스트 확보, 실제 로그인·변경 진행 저장·구매/복원·영수증 검증. 오프라인 기기 시험은 서버 write0 |
| 기존 Public→Private | 오프라인 audit/preflight/postflight 준비 완료 | 대상·백업·모든 쓰기 주체·동시성 통제, 제한 시험 후 승인된 실제 이행·재조회 |
| 업로드 키 reset | 보안 담당 구체안 준비 완료, 소유자 결정 대기 | 안전한 암호화 보관/비밀번호·off-PC 경로, 새 키·공개 PEM 신청 승인, 활성 통지·빌드검증 |
| 실제 16KB 실행 | 4KB ARM64 smoke 완료. PR130 읽기 점검: firmware virtualization 지원, WHPX 비활성·hypervisor 없음·가속 드라이버 없음. OS 변경 미실행 | WHPX 활성화·재부팅 시점 결정 후 환경 확인. 실제 PAGE_SIZE=16384·ABI·호환 모드·APK/split 실행 필요. x86_64 AVD는 ARM64 증명이 아니며 정적 RELRO5 실패도 남음 |
| 최종 출시 AAB·내부 테스트·신고·심사 | PR130 소스10항목 및 후보 AAB 버전·공개 인증서·서명·ABI/LOAD/RELRO 검사 도구 통합. 실제 출시 AAB 미생성, releaseReady=false | 정책·서명·런타임 충족 후 실제 후보 검사, 승인된 업로드/트랙/신고·제출 및 실제 심사 결과 |

정책 선택·로그인·키 회전·운영 이행·스토어 제출은 해당 단계에 진입할 때 검토 가능한 초안과 영향을 먼저 완성하고 필요한 정보·승인을 요청한다. 대기 중에도 독립적인 구현·검증은 계속한다. 리팩토링 PR 종료를 전체 복구 종료로 표현하지 않는다.

README의 개인정보처리방침은 게임 출시 때 마련한 공개 정책 페이지다. 정책 본문과 기존 GitHub URL을 임의 삭제하거나 개발 안내로 대체하지 않는다. 운영자 AeDeong·일반 문의 doeud1410@gmail.com·2024년 9월 30일 시행은 기존 정책에 명시되어 있으므로 재질문하지 않는다. 삭제 접수 서비스나 구체적 보관 기간이 이미 제공된 것으로 추정하지는 않는다. 정책 원문·현재 스토어 설정 대조는 개인정보 담당이 수행한다. 중복 질문을 제외한 [최소 운영 결정](release-preflight.ko.md#사용자가-결정해야-하는-최소-항목)만 남기며, 일반적인 작업 재개를 미답 결정의 승인으로 해석하지 않는다.

2026-09-11 통합 기준은 `9623e648a20816d3c47663bc3833b99a63d8c20d`이다. 삭제 UI 실행 source `fff397e1eae26266c415212e4a80011c180d5cbf`의 Editor355/355 XML을 독립 대조했고 병합까지 해당 C# 차이가 없다. 서버/도구 통합 `d467cfc76524f02fd0a15086d48d893604c72035`에서 `python -m unittest discover -s tools/revival -p 'test_*.py'` 122/122(7.978초)를 재실행했다. 별도 영수증 runtime7개는 담당자의 실제 소켓 실행 결과이며 이 122개에 포함하지 않는다. CI는 계속 비활성이다.

근거: [파일별 감사](first-party-audit.ko.md), [광고 동의](consent-gate-validation.json), [오프라인 기기 왕복](gameplay-harness.ko.md), [삭제 core/server](account-deletion-validation.json), [삭제 UI](deletion-ui-validation.json), [영수증 staging](receipt-staging-validation.json), [출시·호스트 점검](release-preflight.ko.md).

추가 합성 HTTP 검증: `e4dcbc8`에서 .NET9 실제 소켓13/13 및 Python122/122(8.123초). Unity C# 동일 소스의 Editor355/355(4.652초)를 별도로 확인했다. Unity 화면 HTTP 클릭·Android HTTP·운영 인증/provider 연결은 미실행이다. [합성 HTTP 근거](deletion-loopback-http-validation.json).
