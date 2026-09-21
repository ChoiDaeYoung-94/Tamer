# 익명 계정 세션의 삭제 의도 확인

2026-09-21 후속 변경: [명시적 PGS 단일 연결 계정 지원](deletion-pgs-session.ko.md)이 추가됐습니다. 기본값은 계속 익명 두 유형만 허용하며, 아래 최초 구현 기록의 native PGS 미지원 범위는 별도 옵션이 꺼진 경우에 적용합니다. 추가 기능도 현재 세션 확인이며 Google 재인증을 주장하지 않습니다.

2026-09-21, checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`, 코드 `01236f5d3392c4f59dead4ec8d7614d9cbf3f995`, 기준 main `d895c1229e0d4fd7e0c0046c87544f931a5b00a8`. 이 코드는 기존 익명 계정의 인증 수준에 맞춘 명시적 삭제 확인이다. 사람의 신원 확인이나 Google/MFA 재인증을 주장하지 않는다. 운영 계정은 시험하지 않았다.

## 실행 가능한 서버 연결

`server.privacy.session_confirmation.compose(database, title_id, secret_key, policy)`가 고정 title의 서버 ticket 검증, SQLite 확인 증거, 기존 IntakeService와 Server/DeletePlayer adapter를 연결한다. 반환한 `(service, confirmation)`을 `create_app(service, session_confirmation=confirmation)`에 전달한다. import/생성만으로 네트워크나 listener를 시작하지 않는다. 외부값 자동탐색이나 기본 endpoint/자격증명도 없다. 기존 Policy 승인 조건과 `session_confirmation_enabled=True`가 모두 필요하며 기본값은 비활성이다.

PlayFab 경로는 `Server/AuthenticateSessionTicket`, `Server/GetUserAccountInfo`, `Server/DeletePlayer`만 허용한다. title은 서버 설정으로 고정하고 클라이언트의 대상 ID·title·계정유형·시각은 입력받지 않는다. HTTPS 기본 인증서 검증, redirect 금지, ambient proxy 미사용, 10초 timeout, 응답 64KiB 제한을 적용한다. 오류에 원문 body·ticket·서버키를 넣지 않는다.

서버 ticket 응답의 `UserInfo.PlayFabId`, `TitleInfo.TitlePlayerAccount`를 사용한다. entity type은 `title_player_account`여야 한다. 허용 유형은 현재 서버 응답에 **AndroidDeviceInfo 또는 CustomIdInfo 하나만 있는 경우**다. 사용자명·로그인 이메일·다른 provider·복수 연결·미상 연결은 `account_type_unsupported`로 거절한다. `Origination`은 생성 이력이므로 현재 연결 유형으로 사용하지 않는다. device/custom ID 자체를 삭제 API의 대상 ID로 받지 않는다. 공유 비밀번호로 접근 가능한 이메일 계정을 익명 계정으로 승격하지 않는다.

| 요청 | 정확한 JSON 필드 | 결과 |
| --- | --- | --- |
| `POST /v1/deletion/session-challenge` | `sessionTicket`, `clientKey` | 120초 nonce, `purpose=delete_title_account`, `evidenceKind=session_confirmation` |
| `POST /v1/deletion/session-confirm` | `sessionTicket`, `nonce`, `confirmed` | `confirmed`가 boolean true일 때만 서버 재검증 후 제한된 proof 발급 |
| 기존 request/confirm/status/cancel | 기존 필드 유지 | 해당 proof 및 동일 clientKey의 의도에 한정해 기존 접수 provider 사용 |

nonce는 title·계정·entity·ticket SHA-256·서버 계정유형·의도 clientKey·purpose·정책에 묶인다. confirm에서는 원격 세션 검증 후 SQLite `BEGIN IMMEDIATE` 안에서 만료/미소비를 다시 확인하고, nonce 소비와 proof 생성을 함께 commit한다. 동시에 confirm해도 하나만 성공한다. 저장 실패는 둘 다 rollback한다. 같은 세션이 새 nonce를 요청하면 이전 미소비 nonce는 무효화된다.

proof는 300초 동안 해당 의도만 request/confirm/status/cancel할 수 있는 capability다. `Principal.evidence_kind=session_confirmation`, `confirmed_at=서버 확인 시각`, `reauthenticated_at=None`이다. 기존 `provider_reauthentication`의 재인증 시각과 섞지 않으며 클라이언트 시각을 채택하지 않는다. 잘못된 evidence kind·NaN·bool 시각도 거절한다. 확인 응답의 proof 안에는 opaque grant와 caller가 이미 가진 ticket이 있고, 서버 저장에는 그 원문 대신 hash만 남는다.

확인 이후 이 capability의 유효기간 안에서는 PlayFab ticket이 삭제로 무효화돼도 accepted/unknown 상태를 읽을 수 있다. 매 요청마다 같은 PlayFab ticket의 유효성을 다시 요구하지 않으므로 이 짧은 capability 자체가 인증 수단이다. 만료 후 복구는 새 세션 확인이 필요하며, 계정 삭제로 더 이상 세션 확인을 할 수 없는 경우의 장기 상태 복구는 구현하지 않았다. 소거 완료 관측 서비스를 추가하지 않는다. 정상 접수와 unknown 처리/재제출 방지는 기존 provider를 그대로 사용한다.

후속 [기기 보호 접수 영수증 복구](deletion-receipt-recovery.ko.md)는 이 짧은 session proof와 별개로, 삭제 제출 전에 단일 intent의 상태 조회 전용 권한을 등록한다. 해당 경로는 재로그인 없이 접수 상태를 복구하며 소거 완료를 관측하지 않는다.

## 검증

Python `3.14.0`, 표준 라이브러리만 사용했다. 실제 임시 SQLite·WSGI와 합성 PlayFab 응답으로 검증했으며 테스트 중 실제 urllib 네트워크 호출을 차단했다.

| 범위 | 결과 | 비공개 원본 SHA-256 |
| --- | --- | --- |
| nonce/동시소비/만료/세션·계정·entity·유형/증거구분/HTTP 접수·unknown/저장 rollback | 16/16, 0.307초 | `74619f340e0f5f2f17d10bf71ba2d90f37e32727f6284f047ca9fea9c57dd089` |
| 기존 core 21 + intake 7 + provider 15 | 43/43, 0.544초 | `b3e68e1d1d5870a375c70dc06a106f0f0d8cc412e673932be37ae93bc719f1c3` |
| 빈 타 provider 객체도 모호한 연결로 거절하도록 보강한 유형 검사 | 1/1, 0.012초, 위 16 중 중복 | `8623f6721105445d9ccc79c59947cb45c09547003e0905b021631189531d8274` |

원본은 각각 `Logs/revival/deletion-session-confirmation-tests.log`, `deletion-session-regression-tests.log`, `deletion-session-type-final-tests.log`다. 실패0·실패 재시도0, 변경 후 해당 유형 검사만 재실행했다. Unity/Android/C# 변경이 없으므로 Editor/기기/APK 테스트는 실행하지 않았다. 저장소의 Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android min24/target36/ARM64 기준은 유지했다. APK 생성 없음, 새 APK SHA-256 없음.

## 실제 연결 전 남은 범위

- SQLite 영속 파일이 있는 단일 호스트용이다. 여러 프로세스의 원자적 소비는 같은 DB 파일의 잠금에 의존한다. Azure Functions 임시 파일/여러 호스트에 그대로 배포할 수 없으며 Table adapter의 조건부 원자적 갱신은 미구현이다.
- 만료 nonce/proof 정리는 새 challenge가 시작될 때 수행한다. 유휴 상태의 별도 정리 작업이나 운영 보관 정책은 구성하지 않았다. 운영 HTTPS ingress/접근 제어/요청량 제한·호스팅·서버키 주입도 미구성이다.
- Unity nonce UI/HTTP 교환과 동일 clientKey의 닫기/재시작 복구는 후속 [Unity 연결 기록](deletion-session-unity.ko.md)에 구현·검증 범위를 기록했다. 서버 또는 Unity 코드 병합만으로 앱 삭제 기능이 활성화되지 않으며 명시적인 서비스 구성이 필요하다.
- native PGS, Xbox, 일반 PlayFab 이메일, 복수 연결 계정은 이 익명 확인 경로의 지원 대상으로 자동 편입하지 않는다. 별도 계정유형에 맞는 확인 경로가 필요하다. 현재 운영 콘솔의 계정은 검증 fixture가 아니다. 신규 로그인 공유 비밀번호 문제는 별도 작업이며 이 확인 경로로 해결됐다고 하지 않는다.
- 실제 PlayFab 응답·TLS·인증·삭제는 실행하지 않았고, 공개 웹 경로 및 비운영 배포 검증도 하지 않았다. 환경 입력/정책이 없으면 비활성이다.

공식 계약은 [AuthenticateSessionTicket](https://learn.microsoft.com/en-us/rest/api/playfab/server/authentication/authenticate-session-ticket?view=playfab-rest), [GetUserAccountInfo](https://learn.microsoft.com/en-us/rest/api/playfab/server/account-management/get-user-account-info?view=playfab-rest), [익명 로그인과 복구 한계](https://learn.microsoft.com/en-us/xbox/playfab/identity/player-identity/login/login-basics-best-practices)를 기준으로 대조했다. 익명 확인은 현재 계정을 제어하는 인증 세션의 삭제 의도를 확인하는 설계 판단이며, 새 provider 인증이나 사람의 추가 신원증명에 해당한다는 공식 보장으로 표현하지 않는다.
