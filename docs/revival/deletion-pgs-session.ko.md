# PGS 단일 연결 계정의 삭제 세션 확인

2026-09-21, 기준 main `06ee23f7e79d141ae7416a63251d8b4a69755474`, 코드·테스트 `6bc530a4218ca20c68787c2a9be778d31f865ad1`. checkout `C:\Users\pc_17\.codex\worktrees\7299\Tamer`, branch `codex/privacy-pgs-session`. 이 변경은 **현재 PlayFab 세션의 명시적 삭제 의도 확인** 지원 유형을 추가합니다. 사람의 신원 확인, fresh Google 재인증, PGS 로그인으로 발급된 ticket임을 증명하거나 과거 legacy ticket을 무효화하는 기능이 아닙니다.

## 기본 비활성과 동일 검증 경계

`Policy.google_play_games_session_confirmation_enabled`는 기본 `False`입니다. WSGI 환경 변수 `TAMER_DELETION_GOOGLE_PLAY_GAMES_SESSION_CONFIRMATION_ENABLED`도 누락 시 `false`이며 정확한 `true`/`false`만 받습니다. 기존 서비스 활성화·정책 승인·세션 확인 조건을 대체하지 않습니다. import/compose만으로 외부 요청은 발생하지 않습니다.

옵션을 켠 경우 서버 응답에 `GooglePlayGamesInfo`만 연결되어 있고 `GooglePlayGamesPlayerId`가 비어 있지 않은 문자열일 때 허용합니다. 서버의 입력 제한으로 ASCII 1~1024자, 공백·제어 문자 없는 ID만 수용하며 숫자로만 구성된다는 가정은 하지 않습니다. 이는 공식 ID 형식을 추가로 단정하는 것이 아니라 이 어댑터의 보수적인 수용 범위입니다.

기존 AndroidDevice/CustomId 단일 연결은 그대로 허용합니다. PGS와 이들 유형이 함께 있거나 `GoogleInfo`, 이메일/Username, OpenId, 미상 연결이 있으면 계속 거부합니다. `GoogleInfo.GoogleId`는 PGS ID로 대체하지 않습니다. PGS 표시명·avatar·클라이언트가 주장한 provider/owner/entity/title/시각을 인증 근거로 받거나 저장하지 않습니다.

`PlayFabSession.authenticate`, `resolve_target`, `verify_target` 모두 같은 유형 판정을 사용합니다. title은 서버 설정과 PlayFab HTTPS endpoint에 고정되고 owner/`title_player_account` entity는 서버 응답에서 정합니다. begin/confirm 때 각각 ticket을 검증하고 `IsSessionTicketExpired`가 정확히 false여야 합니다. `evidenceKind=session_confirmation`, `reauthenticated_at=None`을 유지합니다. 서버 OAuth secret 추가 수집과 auth code 교환은 없습니다.

## nonce와 보존 범위

기존 120초 nonce의 title·owner·entity·ticket hash·clientKey·purpose·policy hash 및 원자적 일회 소비를 유지합니다. PGS는 기존 `account_type` TEXT 열에 `google_play_games:<PGS ID SHA-256>`을 저장하여 begin과 confirm 사이의 연결 ID 변경도 거부합니다. 원본 PGS ID를 DB에 저장하지 않습니다. 해시는 추가 비밀이나 별도 재인증 증거가 아닙니다.

옵션을 켜면 policy hash에 PGS 허용 표식을 넣습니다. 기존 false 상태의 hash는 그대로라 기본 익명 nonce/proof는 이 코드 추가만으로 무효화되지 않습니다. false↔true 설정 변경 시 이전 설정에서 발급한 nonce/proof는 현재 hash와 맞지 않아 거부됩니다. 새 테이블·열·migration·행 삭제를 추가하지 않았습니다. 익명 `account_type` 값도 그대로입니다.

확인 후 발급하는 proof는 기존처럼 최대 300초 동안 한 intent의 재시도·접수 상태 확인에 사용합니다. **PGS 주체 해시 비교는 nonce의 begin→confirm 구간에 한정**됩니다. 이후에는 고정된 게임 계정 owner/entity/title과 현재 허용된 단일 연결 여부를 검사하며, 같은 게임 계정의 PGS ID나 허용 유형 변경을 지속해서 추적하는 기능은 아닙니다. 접수 영수증은 별도의 상태 조회 권한이며 Google 인증을 대체하지 않습니다. `accepted`를 최종 소거 완료로 바꾸거나 완료 worker를 추가하지 않습니다.

## legacy 세션과 실제 활성화의 한계

공식 `AuthenticateSessionTicket` 응답은 ticket의 유효성 및 현재 계정 연결 정보를 제공합니다. 특정 ticket의 발급 로그인 API·인증 시각·인증 수준을 알려주는 필드는 문서화되어 있지 않습니다. `Origination`, 계정의 로그인 시각, 클라이언트 login mode를 그 증거로 쓰지 않습니다.

PGS+legacy 연결이 남아 있는 계정은 혼합 유형으로 거부합니다. 그러나 과거 legacy 연결을 해제한 뒤 현재 PGS만 연결된 계정이라도, 과거 ticket이 아직 유효한 경우까지 이 검사로 구별할 수는 없습니다. 연결 해제가 모든 기존 ticket을 폐기한다는 보장도 가정하지 않습니다. 취약 로그인 경로 정비나 legacy ticket 배제가 필요한 경우 별도 서버 로그인 증거/세션 폐기·전환 정책을 검토해야 합니다. 실제 운영 설정은 이 코드 병합으로 켜지지 않습니다.

외부 요청 페이지, 실제 PGS/PlayFab 응답·권한, TLS 배포, 운영 삭제·기기 전체 삭제 흐름은 이번 검증 대상이 아닙니다.

## 검증

CPython 3.14.7, Waitress 3.0.2, 실제 임시 SQLite와 fake transport를 사용했습니다. urllib 실제 네트워크 호출을 차단했습니다. 아래 검증은 커밋 직전 동일 코드·테스트로 수행했고 이후 문서만 추가했습니다.

```text
python -m unittest tools.revival.test_deletion_session_confirmation tools.revival.test_deletion_runtime.RuntimeTests.test_pgs_runtime_opt_in_validation_and_accepted_receipt_restart -v
```

**21/21 통과, 0.408초, 첫 실행 성공·재시도 없음.** 기존 관련 세션 테스트 16개 + 신규 PGS 4개 + runtime 연결 1개입니다. 기본 opt-in 거부/명시 허용, 세 검증 진입점, 혼합·잘못된 ID 거부, owner/entity/type/PGS 주체 변경, 세션 교체·동시소비·재시작 replay 거부, 정책 hash와 기존 익명 proof 보존을 확인했습니다. runtime의 fake 접수→앱 재구성→영수증 읽기에서 DB 내용 보존과 fake DeletePlayer 1회도 확인했습니다.

비공개 원본 `Logs/revival/deletion-pgs-session-tests.log` SHA-256: `2f6a79f6cd5178291795ba46e76c9c7b7f0cfe6fbe70eb9d98d8dca25910f58e`. 원시 로그는 커밋하지 않습니다. 실 계정 조회·운영 요청·OAuth token 교환·서비스 활성화 0회입니다. Unity `6000.0.81f1`/CLI `1.0.0-beta.8`/Android min24·target36·ARM64 기준은 변경하지 않았고 Editor·기기 테스트 및 APK 생성은 하지 않았습니다.

공식 근거:

- [AuthenticateSessionTicket](https://learn.microsoft.com/en-us/rest/api/playfab/server/authentication/authenticate-session-ticket?view=playfab-rest): ticket 검증과 `UserInfo.GooglePlayGamesInfo.GooglePlayGamesPlayerId`.
- [GetUserAccountInfo](https://learn.microsoft.com/en-us/rest/api/playfab/server/account-management/get-user-account-info?view=playfab-rest): 같은 계정 연결 필드와 title entity.
- [LoginWithGooglePlayGamesServices](https://learn.microsoft.com/en-us/rest/api/playfab/client/authentication/login-with-google-play-games-services?view=playfab-rest): 로그인 auth code 입력과 SessionTicket 반환. 이 API를 삭제 서버에서 새로 호출하지 않습니다.
