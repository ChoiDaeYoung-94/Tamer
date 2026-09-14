# 기존 서버 진행도 8키의 격리 저장·복원

## 실제 저장 범위

`Assets/Resources/Data/PlayerData.json`은 10키를 정의한다. 이번 검증은 아래 8키만 서버 읽기/쓰기 대상으로 사용한다.
`GoogleAdMob="null"`, `GooglePlay=""`는 로컬 합성 기본값으로만 유지한다. 광고·Google·구매·NoAds 호출은 하지 않는다.

| 키 | 실제 기본값 | 합성 단계 1 | 합성 단계 2 | 실제 소비/변경 경로 |
|---|---|---|---|---|
| NickName | null 문자열 | GameplayFixtureA | GameplayFixtureB | Login 서버 설정, PlayerUICanvas 표시 |
| Sex | null 문자열 | Man | Woman | CanvasSelectCharacter 서버 설정, LoginCheck 프리팹 선택 |
| Tutorial | null 문자열 | done | done | LoginCheck의 null 비교; 실제 튜토리얼 완료 구현은 TODO |
| Gold | 0 | 120 | 235 | Player 보상/차감 → DataManager 로컬 변경 및 queue |
| Power | 10 | 11 | 12 | Creature 초기화에서 float 파싱 |
| AttackSpeed | 0.5 | 0.6 | 0.7 | Creature 초기화에서 float 파싱 |
| MoveSpeed | 3.0 | 3.2 | 3.4 | Creature 초기화에서 float 파싱 |
| AllyMonsters | null 문자열 | Bat | Bat,Magma | Player 동행 추가/제거 및 복원, MonstersData 이름 검증 |

동일한 이름의 **PlayerPrefs `AllyMonsters`는 몬스터 도감**이며 서버의 동행 목록과 다른 데이터다.
PlayerPrefs `playerEquippedItems`는 착용 장비, `LocalItem`은 ShopMan의 보유 아이템이다.
이 세 가지는 현재 서버 동기화 경로가 없으며 이번 변경은 새 서버 키나 기존 저장 귀속 마이그레이션을 추가하지 않는다.
따라서 이 결과를 전체 게임 진행도 복원 또는 실제 Player 씬 반영의 완료로 해석하지 않는다.

## 격리 구현

- 전용 패키지: `com.AeDeong.MonsterTamer.revival.gamesave`, debug ARM64.
- 컴파일 가드: `UNITY_EDITOR || TAMER_GAMESAVE_HARNESS`. 일반 앱 초기화/저장 경로를 바꾸지 않는다.
- 실제 Resources 기본 schema와 Monster/Item 데이터를 읽되 Managers, Player, 로그인, 광고, 구매 서비스를 초기화하지 않는다.
- 실제 DataManager의 변경·원자 저장·pending journal·ack 및 ServerManager queue를 사용한다.
- 오프라인 transport는 전용 `GameSaveHarnessV1/offline/SyntheticServer.json`만 읽고 쓴다. 이는 PlayFab 증거가 아니다.
- 로컬 저장은 `GameSaveHarnessV1/<offline 또는 cloud>/<primary 또는 restore1 또는 restore2>/GameSavePlayerData.json`이다.
- 새 restore 슬롯은 기존 디렉터리가 있으면 거부한다. primary 저장을 삭제하거나 초기화하지 않고 별도 빈 경로에서 읽기만으로 복원한다.
- read는 빈 신규 snapshot 또는 정확한 합성 단계의 8키를 검증한다. 범위 밖/불완전/알 수 없는 응답을 거부한다.
- patch는 명시된 합성값과 8키만 허용하며 null 삭제, 예약 owner/journal, 광고/권한/PlayerPrefs 키를 거부한다.
- 준비된 PlayFab adapter는 별도 instance client, 고정 테스트 Title, 명시 8키 GetUserData와 Private UpdateUserData를 사용한다. 현재 오프라인 UI에는 로그인 기능이 없고 APK의 네트워크 권한을 제거한다.

## 2026-09-14 검증 결과

- checkout: `C:/Users/pc_17/.codex/worktrees/7299/Tamer`
- APK 소스: `c6cd91ac8b5da2acfa155f586c54244d0e4bee7d`, 기준 main: `ca6272c6b8953198b397f1b084684ae70fc23fb6`
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, min SDK 24 / target SDK 36
- 신규 Live Editor 회귀 **16/16**, 관련 Data/SaveQueue/Journal 포함 batch 회귀 **85/85 통과**
- NUnit XML SHA-256: `f4a8c835b447dc807a3f135b00fd54ba37ee4669b3c2d38c1dfb4813ff49e070`
- APK: `Build/revival/Tamer-gamesave-offline.apk`, 93,363,068 bytes
- APK SHA-256: `cf571770348a312dd84b57dbed94dc8e5bd12ccf82531c8fd282e28d693c721c`
- 패키지/debug 서명/ARM64/인터넷·결제·광고 ID 권한 부재 검사 통과. GUID unresolved 0.
- LOAD/ZIP 통과, 별도 strict RELRO 끝 정렬 **4개 실패**. 16 KB 런타임/스토어는 미검증.

회사폰 SM-N986N(Android 13, ARM64, 4 KB 페이지)에 새 전용 앱을 설치했다. 실제 서버나 계정 인증은 사용하지 않았다.

| 실기기 검사 | 복원 값 | pending 수 | revision | 결과 |
|---|---|---:|---:|---|
| 단계 1 미전송 상태 → 프로세스 종료/재실행 | 8키 단계 1 | 8 | 8 | PASS, 재실행 쓰기 0 |
| 단계 1 전송 확인 → 프로세스 종료/재실행 | 8키 단계 1 | 0 | 8 | PASS, 재실행 쓰기 0 |
| 별도 빈 restore1에서 읽기 전용 복원 | 8키 단계 1 | 0 | 0 | PASS, 쓰기 0 |
| 단계 2 미전송 상태 → 프로세스 종료/재실행 | 8키 단계 2 | 7 | 15 | PASS, 재실행 쓰기 0 |
| 단계 2 전송 확인 → 프로세스 종료/재실행 | 8키 단계 2 | 0 | 15 | PASS, 재실행 쓰기 0 |
| 별도 빈 restore2에서 읽기 전용 복원 | 8키 단계 2 | 0 | 0 | PASS, 쓰기 0 |

단계 2에서 Tutorial 값은 같으므로 7키만 변경된다. 네 번의 기록된 종료에서 PID 부재와 새 PID를 확인했다.
최초 즉시 PID 조회는 시작 지연 때문에 기록에 포함하지 않았고, 같은 미전송 상태를 유지한 채 제한된 polling으로 재수집했다.
두 단계 모두 pending/ack 파일은 재시작 읽기 전후 바이트가 동일했다. 새 restore 슬롯은 읽기 전 존재하지 않았고,
복원 중 primary 파일과 이전 restore1 파일도 보존됐다. 최종 앱과 해당 checkout Editor를 종료하고 슬롯을 반환했다.

원본 33개 파일(화면/저장 파일/PID 기록/검증 helper)은 비공개 `Logs/revival/gamesave-device`에 보존했다.
[기계 판독 결과와 원본 해시](game-save-validation.json)를 함께 확인한다.

## 다음 실제 서버 검증을 위한 관리자 인계

현재 단계에서 외부 계정 생성·인증·Cloud 쓰기는 실행하지 않았다. 회사폰 외부 인증 제한을 유지한다.
실제 실행에는 개인폰과 **새 gameplay 전용 테스트 계정**이 필요하다. 기존 구매·progress 계정은 사용하지 않는다.

관리자에게 전달할 최소 요청은 다음과 같다.

- 대상: 테스트 Title `12B656`의 별도 gameplay 저장 검증용 신규 계정. 운영 Title `67C9A`는 제외한다.
- CustomID 형식: `gameplay-save-` 뒤 소문자 16진수 32자리. 예시 문자열을 실제 계정에 재사용하지 않는다.
- 코드의 로그인 요청은 `CreateAccount=false`이다. 관리자에게 이미 허용된 정상 수동 생성 절차가 없으면 정책을 확대하거나 관리 API로 우회하지 않는다.
- 계정의 읽기/쓰기 검증 범위는 위 8개 합성 키만이며 구매·Google 연결·실제 사용자 데이터가 없는 별도 계정인지 확인한다.
- 실제 CustomID/PlayFab ID/티켓은 공개 문서·PR·로그에 넣지 않고 기존 수동 인계 보안 절차를 따른다.
- 개인폰 준비 후 Cloud 로그인 화면/네트워크 허용 전용 빌드와 정확한 실행 순서를 통합 검토받는다. 오프라인 APK로 실제 서버 검증을 시도하지 않는다.

실행 순서는 신규 계정의 명시 8키 읽기 → 단계 1 로컬 변경 → 업로드/재읽기 → 앱 프로세스 종료와 재인증 → 단계 1 복원 → 별도 빈 슬롯 읽기 전용 복원 → 단계 2에 같은 절차다.
인증 실패·알 수 없는 기존 데이터·소유자 불일치가 있으면 파일/서버를 덮어쓰지 않고 실패로 보고한다.
