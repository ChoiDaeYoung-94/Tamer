# 삭제 접수 HTTP·Unity 연결 검증

2026-09-21. checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`, 검증 소스 `a564affae9305292eb3e8964399952d0ab8eac9d`. 실제 운영 endpoint와 재인증 공급자가 미구성이므로 기본 비활성이다. 실제 사용자 삭제/유료 자원 생성/배포/권한 변경은 하지 않았다.

## 연결한 실행 경로

서버는 `create_app(IntakeService(database, title_id, authenticate, resolve_target, submit, verify_target, policy))`로 기존 WSGI 경로와 SQLite 영속 provider를 연결한다. 필수 callable 하나라도 없으면 승인 플래그가 있는 Policy를 전달해도 비활성이다. 완료 worker 없이 confirm 요청 안에서 제출 전 unknown을 영속 기록하고 접수 결과를 반환한다. request/status는 삭제를 제출하지 않는다.

`authenticate`는 신뢰된 서버 인증 어댑터로서 독립 검증된 proof를 `Principal(account, reauthenticated_at, title_entity_id)`로 변환해야 한다. 본문에는 계정 ID/시각/entity를 받지 않는다. 기존 300초 fresh 검사와 서버가 검증한 entity를 함께 적용한다. 클라이언트가 보고한 시각, 캐시 ticket 유효성, 기존 기기 ID, 기존 앱의 고정 로그인 비밀번호는 이 검증 구현을 대체하지 않는다. **실제 공급자 verifier/재인증 교환은 이번에 구현하지 않았다.** 콜백이 있다는 이유로 운영 인증이 완성됐다고 하지 않는다.

동일 계정 ID의 재가입이라도 서버 인증 entity가 바뀌면 이전 accepted를 새 데이터 정리에 사용하지 않는다. 정상 접수된 이전 요청을 별도 terminal accepted로 분리해 새 확인 요청을 만들고, 이전 요청의 status/confirm을 새 entity proof로 호출하면 거절한다. unknown 요청은 새 entity를 관측해도 자동 재제출하지 않는다. accepted는 소거 완료가 아니다.

Unity `DeletionPresenter.ConfigureService(httpsOrigin, freshAuthentication)`는 `HttpDeletionGateway`와 현재 DataManager의 계정/세대를 연결한다. 설정 호출이 없거나 실패하면 기존 unavailable 화면이다. HTTPS origin만 받고 redirect·URL credential·query를 허용하지 않는다. proof는 JSON body로만 전달하며 오류에 원문 응답을 노출하지 않는다. `submissionState=accepted`만 Accepted로, `submission_unknown`은 별도 안내 상태로 투영한다.

confirm 직전에 DataManager의 삭제 진행 표시와 서버 요청 취소로 로컬 쓰기·새 로그인 적용을 막는다. late 응답은 Flow의 owner/account/session 세대 검사로 차단한다. 정상 accepted에서는 자격을 제거하고 해당 계정의 디스크 owner를 다시 대조해 활성 PlayerData 파일만 정리한다. GooglePlay 권한 문자열은 owner와 함께 별도 파일에 먼저 보존하고 새 계정에 자동 적용하지 않는다. 이 파일은 새로운 구매 영수증/스토어 검증 결과를 만들어내는 것이 아니다. owner 없는 legacy/다른 계정 파일/기존 백업/로그인 PlayerPrefs는 지우지 않는다. 정리하지 않은 파일의 owner 제한도 다음 로그인까지 보존한다.

unknown은 성공 안내·로그아웃·파일 정리를 하지 않는다. accepted 뒤 로컬 정리 실패는 재삭제 요청으로 복구하지 않으며 접수 사실과 로컬 정리 실패를 구분해 안내한다. 다음 로그인 화면에서는 자동 로그인 대신 명시적 재시도를 요구한다. 신규 가입/구버전 API 우회/실제 AccountDeleted 응답 안내의 전체 기기 흐름은 아직 검증 전이며, 완료 증거가 없다는 이유의 영구 재가입 차단 정책은 추가하지 않았다. 현재 명시적 재가입 UX와 기존 login continuity의 생성 허용 조건을 완전히 연결한 것은 아니다.

## 검증과 비공개 원본

| 범위 | 결과 | 원본 SHA-256 |
| --- | --- | --- |
| HTTP intake, 재시작·unknown·fresh/entity·재가입 격리 | 보강 후 7/7 | `b57c69a4becc3b68448e9f54cefdb1adc66e0584acb58eef9245c0795bf64a4e` |
| 기존 서버 core | 21/21 | `b279b2bd7c9ef9b04ad148a1bb81dcbebf6d0a3c570f1d601c1964f0bc6b6e4a` |
| 기존 title provider | 15/15 | `6b81ab7632e3c6b3db0ee3e3275effc095dea0d1f59c9dc7b762d742f9e78ed4` |
| Unity Flow/UI/소유별 정리 | 27/27 | `2632667391439b00d7d57166f2ff3556be0b34265274706e695a5116a9d0ea1a` |
| Unity HTTPS gateway 응답 투영·origin 제한 | 5/5 | `2d5d6e9747adb92f7846749767ad9fc2fb47acb2fc9be68610f145d36463c0cc` |
| 타계정 파일의 후속 owner 제한 보강 후 해당 정리 범위만 | 3/3 | `8713a438df69fd34a177691dd8dfb2522a3e6d314066a8598d87c98097961034` |

원본은 `Logs/revival/deletion-intake-http-tests-final.log`, `deletion-intake-core-regression.log`, `deletion-intake-provider-regression.log`, `deletion-intake-editor-tests.json`, `deletion-intake-gateway-tests.json`, `deletion-intake-owner-final-tests.json`이다. 최초 HTTP6/6에서 entity 검사 추가 후7/7, 최초 owner3 포함27/27에서 보존 파일의 후속 로그인 제한 추가 후 해당3/3을 수행했다. 실패0·실패 재시도0, 변경 없는 전체 반복 없음. Python은 합성 인증/transport와 실제 임시 SQLite·WSGI, Unity HTTP는 가짜 HttpMessageHandler를 사용했다. 운영 TLS·실제 인증·PlayFab 삭제 성공을 검증한 결과는 아니다.

Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android min24/target36/ARM64 기준 유지. Editor async 완료 결과를 확인했고 자체 PID33532/56640 종료와 ProjectSettings 복원을 완료했다. 기기/APK 실행·생성 없음, 새 APK SHA 없음.

남은 실제 연결 입력은 신선한 인증 공급자와 서버 검증 방식, 승인된 고정 title/endpoint, 호스팅과 영속 저장이다. 현 SQLite는 영속 파일을 가진 WSGI 호스트용이며 Functions의 임시 로컬 파일 저장으로 배포하지 않는다. Azure Table 변환·Functions packaging·비용/구독 확정·공개 웹 인증 및 명시 재가입/백업 보관 정리는 미완료다. 운영 배포 전에 이 입력과 남은 경로를 검토하며 완료 증거 수집용 서비스로 범위를 다시 넓히지 않는다.
