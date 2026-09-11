# UI / 팝업 / 씬 생명주기 감사

2026-09-11, base `f68fb61` (`codex/ui-lifecycle-refactor`). Unity `6000.0.81f1`, CLI `1.0.0-beta.8`. 이 문서는 전체 복구 중 UI 담당 범위다. Families/consent/운영 적용 검증의 완료를 의미하지 않는다.

## 책임과 변경

| 파일 (`Assets/Scripts/` 기준) | 책임 / 의존성 | 판단과 처리 | 검증 |
|---|---|---|---|
| `Managers/PopupManager.cs` | 팝업 순서, 입력 차단; Update/Sound/Game/광고 | 중복 등록 및 비활성/파괴 객체 제거. reset 전에 스택 snapshot/clear. 객체별 차단 소유자와 기존 명시적 전역 차단을 분리. 구독 원본 보관/해제 | 광고 popup 회귀 + 외부 disable/중복/겹친 blocker/reset |
| `Managers/Sub/PopupObject.cs` | 팝업 enable/disable 연결 | 등록받은 manager를 보관해 동일 객체를 해제. 자체 닫기 버튼이 다른 top을 닫지 않음. teardown에서 singleton 재조회 안 함 | 아래 popup 버튼/상위 유지, singleton 제거 후 disable |
| `Managers/SceneManager.cs` | 중간 씬/저장 대기/목적지 로드; Data/Server/Sound | 중복 요청과 CTS 덮어쓰기 방지, 목적지 snapshot, 소유 async finally에서 dispose. native activation 잠금 제거, realtime 대기 | 중복 NextScene/GoScene, 취소된 load가 native 실행 전 종료 |
| `Managers/GameManager.cs` | 게임 상태와 씬 전환; Player/UI | 담당자 승인한 Switch 시작 gate 한 줄만. 목적지 토글 전에 중복 거절 | Scene gate 회귀, 나머지는 서비스 담당 소유 |
| `UI/PlayerUICanvas.cs` | HUD/정보/버프 표시; Player/Data/광고/Update | 기존 publisher 보관, 재초기화 시 이전 구독 제거, 파괴 시 해제 및 자기 singleton만 정리 | 원래 publisher의 버프 callback 해제 |
| `UI/JoyStick.cs` | 포인터/키 입력을 Player 이동으로 연결 | 초기화 전 camera/player 접근 방지. disable/focus 상실 시 입력 방향/거리/handle 초기화. 자기 singleton 파괴 정리 | 초기화 전 FixedUpdate, disable/focus held-input 해제 |
| `UI/TMP_Damage.cs` | 풀링 피해 텍스트; TMP/DOTween/Pool | transform 대상 Kill 대신 소유 Sequence Kill. 하위 fade/위치 tween과 이전 pool-return callback 함께 종료 | 실제 sequence 활성→Clear→비활성 |
| `UI/Loading.cs` | 빈 MonoBehaviour | 직렬화된 script 연결을 깨뜨릴 이유 없어 유지. 책임을 새로 만들지 않음 | 소스 감사 |
| `SetCharacter/CanvasSelectCharacter.cs` | 선택 이동/저장 확인; Server/Data/Animator | 이동 single-flight를 bool로 관리해 pooled UniTask Status 재조회 제거. destroy token으로 이동 취소. 저장/성별/계정 로직 유지 | 이미 취소된 이동에서 대상 접근 없이 busy 해제 |
| `NextScene/NextScene.cs` | 중간 씬 Start→GoScene | 단일 위임 유지. 중복 방지는 SceneManager의 책임 | SceneManager gate 회귀 |
| `Main/InitializeMain.cs` | LoginCheck→BuffingMan→UI/game 초기화 | 기존 초기화 순서/직렬화 배열 유지. 문자열 기반 초기화는 서비스 초기화 작업과 연계해 검토 | 소스/연결 감사, 실제 로그인 실행 안 함 |
| `Game/InitializeGame.cs` | MiniMap/game/monster 초기화 | 빈 enum 반복은 현재 무동작. 직렬화 배열과 편집기 UI는 보존, gameplay 담당 초기화와 중복 재작성하지 않음 | 소스 감사 |
| `Main/LoginCheck.cs` | 첫 진입 Player/UI 생성 및 tutorial 확인 | 계정 연속성 담당 경계, 읽기만. UI 생성 순서 보존 | 기존 로그인 회귀 유지 |
| `Login/Login.cs` | 인증/데이터 준비/씬 선택 | 계정 담당 소유, 호출 경계 읽기만 | 기존 로그인 회귀 유지 |

`Main/Item.cs`와 `Main/IAPItem.cs`의 ShopMan 목록 등록/해제 수명, `GameManager.GameOverGoLobby`의 중복 초기화는 해당 소유자에게 전달했다. Map/MonsterGenerator/Portal의 게임플레이와 Managers 서비스 전반은 통합/데이터 담당 범위다.

## 동작 차이와 보존 사항

팝업 stack은 enable 순서로 유지되며 같은 객체가 중복 쌓이지 않는다. 직접 disable 또는 늦은 광고 보상으로 특정 popup을 닫아도 나머지 순서는 유지한다. Exception/Flow 팝업 하나의 disable이 다른 활성 blocker를 풀지 않는다. 기존 `SetException/ReleaseException`, `SetFlow/ReleaseFlow` API는 별도 전역 상태로 유지했다.

씬 loading의 2초 대기는 기존처럼 progress 0.9 뒤에 추가하는 대신 native load 시작 **전** realtime으로 수행한다. 따라서 timeScale 0에서도 기다림이 끝나고, 취소된 managed 대기가 Unity의 `allowSceneActivation=false` 잠금을 남기지 않는다. native load가 시작된 뒤에는 Unity 자체 작업을 취소할 수 없으며 취소 토큰은 후속 managed 처리만 중단한다. 저장 요청/완료 판단/실패 UI 정책은 변경하지 않았다.

기존 serialized 필드명, enum 값, MonoBehaviour 이름 및 public UnityEvent 메서드를 유지했다. 수정한 기존 script `.meta`를 base와 대조해 보존을 확인했다. prefab/scene 구조를 재작성하지 않았고 계정·구매·저장·광고 정책 코드와 운영 설정은 변경하지 않았다. HUD와 gameplay의 데이터 의존성을 전면 교체하지 않고 확인된 소유권/수명 결함을 먼저 줄였다.

## 검증 기록

- `09c45fa`: 전체 Revival EditMode **258/258** 통과. popup fixture는 PreviewScene과 비활성 Managers bridge를 사용하며 계정 manager Awake를 실행하지 않는다.
- `804f29f`: UI 추가 검증 **263/264**, DOTween fixture 1개 실패. DLL 및 컴파일 IL 확인 결과 EditMode에서 DOTween Init이 생략되어 Kill이 no-op이었다. 제품 코드의 sequence Kill 호출은 올바르게 컴파일됐다.
- 테스트 fixture는 DOTween의 runtime-ready 상태만 동기 범위에 설정하고 finally로 원복한다. 실제 Kill 경로를 확인하며 player/account/global tween update를 시작하지 않는다. 실패를 숨기기 위해 제품 코드를 우회하지 않았다.
- 실제 운영 Main/Game/로그인 씬 왕복, 화면 배치, Android APK는 이번 리팩토링에서 실행하지 않았다. 격리 EditMode/PreviewScene 검증과 실기기 광고 검증 기록은 구분한다.

최종 소스 `3be9954`에서 **264/264 통과** (07:03:15–07:03:18 UTC). [구조화 검증 기록](refactor-ui-validation.json)에 전체 소스 SHA와 XML 해시를 기록했다. 기존 광고·구매·저장 회귀도 같은 필터에 포함된다.

Run-Baseline의 PlayerSettings snapshot 복원이 완료되고 이 checkout Editor가 0개임을 확인했다. GoogleMobileAdsSettings와 RevivalSmoke는 내용 diff 없는 줄바꿈 변경, SceneTemplateSettings는 Unity import에 따른 editor template 필드 축약이었다. 테스트 전 clean이었던 이 3개 파일만 HEAD로 복원했다. 실제 scene/prefab 구조와 운영 설정 변경은 커밋에 포함하지 않는다. 전체 복구의 남은 서비스/게임플레이 및 Families 작업은 각 소유자가 계속한다.
