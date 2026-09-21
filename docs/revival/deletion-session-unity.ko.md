# Unity 삭제 세션 확인과 동일 요청 복구

아래는 최초 세션 기반 연결의 검증 기록이다. 접수 응답 유실 후 티켓까지 무효화된 복구 결함은 후속 [로그인 없는 접수 영수증 복구](deletion-receipt-recovery.ko.md)에서 보완했다. 현재 상태 조회·보호 키·로그인 전 진입·기기 검증의 최종 계약은 후속 문서를 따른다.

2026-09-21, checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`, 기준 main `bc260e308be1c745cbb60dbe3d1e8aa2152a300d`. 기본 구현 `c760a11b089d98944607a26b1a9470ffdaf8909e`, UI 검사 추가 `91ca4faa1fd0d392de83a1d117634b0527fcdd0a`, 관리자 교체 가드 보강 `967580740da4700d419a760d8731b2514196a1d0`, 파일 접근 오류 구분 `a712744892796c3146ba6d99297c71d46eb6ab1b`. 서버 계약은 [세션 확인 문서](deletion-session-confirmation.ko.md)를 따른다.

## 연결과 명시 확인

앱 bootstrap에서 로그인 전에 `DeletionPresenter.ConfigureSessionService(httpsOrigin, titleId)`를 명시적으로 호출한다. 기본 호출·기본 endpoint·서버키 자동탐색은 없다. 현재 저장소에는 운영 bootstrap 호출과 실제 HTTPS 서비스 설정이 없으므로 기능은 기본 비활성이다. 기존 provider 재인증 adapter인 `ConfigureService`와 별개로 구성하며, 이 두 경로를 동시에 교체해 사용하지 않는다.

현재 인증된 PlayFab 계정·title_player_account entity·title이 맞는 세션만 ticket을 메모리에서 읽는다. 서버 config의 `available=true`, `synthetic=false`, `evidenceKind=session_confirmation`을 확인한 뒤 기존 `/session-challenge`에 flow의 내부 clientKey를 보낸다. nonce를 받은 것만으로 `/session-confirm`을 보내지 않는다. 사용자가 **Confirm current game session**을 눌러야 `confirmed=true`로 교환한다. 서버 proof와 같은 clientKey로 기존 request를 준비하고, 별도 **Confirm deletion request** 버튼이 실제 제출을 시작한다.

세션 확인을 Google 재로그인이나 사람의 추가 신원 확인으로 표시하지 않는다. 비지원 계정 유형은 명확한 안내 상태로 끝내며, 임의의 다른 provider나 계정으로 바꾸지 않는다. 실제 대상 계정/entity/유형 판단은 서버가 ticket을 검증해 수행한다.

## 닫기·재시작 복구

- `persistentDataPath/DeletionRecovery`에 origin·title·계정의 SHA-256을 파일명으로 사용한다. 내용은 버전, origin·title·계정·entity 바인딩 hash, 동일 clientKey, requestId, 정책 revision, 제출 시작 여부뿐이다. ticket/proof/nonce/삭제 challenge 및 계정/entity 원문은 저장·로그하지 않는다.
- nonce 요청 전 clientKey를 보관하고, 요청 준비 응답 후 requestId를 보관한다. 제출 시작 표시를 임시 파일 `Flush(true)` 및 교체로 저장한 뒤에만 외부 confirm을 호출한다. 저장 실패는 외부 제출을 막는다. 파일 손상·entity 불일치는 원본을 보존하고 새 의도로 덮어쓰지 않는다.
- 팝업을 닫으면 인증 비밀과 gateway는 폐기한다. 다시 열거나 앱을 다시 시작하면 새 세션 확인을 명시적으로 거친다. 제출 전 요청은 같은 clientKey로 challenge를 다시 얻는다. **한 번 제출을 시작한 기록은 status만 조회**하며 새 request/confirm을 보내지 않는다. 오류만으로 성공·취소·로컬 정리를 판정하지 않는다.
- 로그인 전에 설정한 recovery guard는 해당 계정의 pending 기록을 읽어 DataManager 저장/전송을 다시 잠근다. `Login.OnLoggedIn`은 인증 context를 복구 용도로 보관한 뒤 정상 프로필/게임 씬 흐름을 진행하지 않고 삭제 복구 전용 Canvas를 연다. 닫은 뒤 Retry도 같은 복구 화면을 다시 연다. 기존 계정 세대·삭제 epoch를 우회해 로그인 흐름을 재활성화하지 않는다.
- 현재 관리자 객체가 교체되거나 계정·세션·title·entity가 달라진 늦은 응답은 적용하지 않는다. 정상 accepted만 기존 owner 검증 정리 및 로그아웃을 수행하고 journal을 지운다. No Ads 근거·owner 없는 legacy·다른 계정 파일의 기존 보존 동작을 유지한다. 확인된 cancellation만 잠금을 해제한다.

## 검증

Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Pipeline `0.6.0-exp.1`. Android 기준 min24/target36/ARM64는 변경하지 않았다. 운영 씬을 재생하지 않고 빈 씬에서 Editor 테스트를 실행했다. HTTP handler는 모든 응답을 합성하며 실제 PlayFab/HTTPS 요청을 하지 않는다.

| 소스와 범위 | 결과 | 비공개 원본 SHA-256 |
| --- | --- | --- |
| `c760a11`, 삭제 flow/gateway/UI/소유자 정리/nonce·복구·로그인 전용 진입 | 46/46, 0.69초 | `d6b561b38afa78980fa172df44da90b4da0b0afc064cd5652e08964dd1ab07a9` |
| `91ca4fa`, 신규 명시 세션 버튼 및 unknown 재제출 금지 | 1/1, 0.09초 | `590c6999be9c4fbcb12696a9491c4e7f81936446e743cebb130a9fb7834d1f33` |
| `91ca4fa`, 세션 확인 상태를 추가한 1080×1920 렌더 | 1/1, 0.32초, 위 46 중 1건 보강 재검사 | `344e37966fd09fc5509fd48bb0fde0bfbb20b70fe10069c33311b6f008834770` |
| `9675807`, 실제 runtime factory의 관리자 교체 차단, HTTP 없음 | 1/1, 0.03초 | `db6ec548c0e2f3a1e6f5ba307bc4afbe71ac2cc89c1d51812a23c928599477ae` |
| `a712744`, 읽을 수 없는 journal을 기록 없음으로 처리하지 않음 | 1/1, 0.03초 | `918010da92081b19196959050f28714701421b950125822f76cd154865822f3a` |

원본은 `Logs/revival/deletion-session-unity-tests.json`, `deletion-session-ui-buttons-tests.json`, `deletion-session-ui-render-tests.json`이다. 세션 확인 PNG를 직접 열어 본문·버튼이 잘리지 않는 것을 확인했다. 비공개 `.revival-local/deletion-ui/session-confirmation.png` SHA-256은 `2d5a74c1414b3abaa371c026a5b1de7d7174f12155c689776cfbd82cbfec52c9`이다. 원본 로그·이미지는 공개 커밋에 넣지 않는다.

관리자 교체와 파일 접근 검증 원본은 `Logs/revival/deletion-session-owner-tests.json`, `deletion-session-store-tests.json`이다. 고유 테스트 49건 모두 통과했으며, 렌더 1건만 새 상태를 추가해 재검사했다. 실패0·실패 재시도0. 세 번의 해당 checkout Editor 세션은 각각 정상 종료 후 PID 소멸을 확인하고 ProjectSettings snapshot을 복원했다.

## 한계와 미검증

- 재시작 이후 상태 조회에는 다시 인증 가능한 동일 계정/entity가 필요하다. 이미 삭제되어 더 이상 세션을 얻을 수 없거나 proof 만료 후 재확인이 불가능하면 접수를 확정하지 않는다. 원문 proof를 장기 저장하거나 장기 완료 관측 서비스를 추가하지 않았다. 이 경우 새 계정 자동생성·재제출·추정 cleanup을 하지 않는다.
- journal은 앱 저장소의 비밀 없는 복구 metadata이며 서버 권한 증거가 아니다. 앱 데이터 삭제/재설치, 악의적 저장소 변조, 여러 앱 프로세스, 전원 손실의 기기별 파일시스템 내구성까지 보장하지 않는다. origin/title/저장 경로 변경 시 기존 pending 자료의 별도 이전 검토가 필요하다.
- 실제 호스팅/서버키/HTTPS ingress/rate limit, 운영 계정 유형, 실제 ticket·nonce·DeletePlayer 왕복, Android 기기/IL2CPP APK 및 스토어 검증은 하지 않았다. APK 생성 없음, 새 APK SHA-256 없음. 기존 서버 SQLite 단일 호스트 제약도 그대로다.
- accepted는 삭제 **접수**다. 물리적 소거 완료 관측·최종 통지·지원 작업자를 필수 기능으로 추가하지 않는다. 명시적 재가입 및 실제 AccountDeleted 처리의 별도 정책을 완료 관측 부재로 대체하지 않는다.
