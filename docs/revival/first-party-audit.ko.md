# 자체 C# 파일별 감사 진행표

범위: 추적 중인 Assets/Scripts, Assets/Tests, Assets/GPGSIds.cs의 105개 파일. SDK 디렉터리는 별도 공급망 감사 범위다. Scripts/MiniMap/FogOfWar는 출처만으로 제외하지 않고 포함한다. 신규 파일은 통합 시 inventory를 다시 생성해 추가한다.

아래 의존성은 소스의 lexical 참조이며 전체 호출 그래프가 아니다. 최신 통합 기준은 `e66acbcc745d8489339e6661393a1722d9aad1b8`이고 Editor 335/335를 통과했다. 기존 98개 감사와 317/332 결과의 실행 시점은 각 근거에 유지하며, UMP 3개·격리 gameplay 4개 신규 파일을 추가했다. 실제 기기 APK 소스는 `f2ab1d9`로 통합 Editor 소스와 구분한다.

근거: [통합 317/317](gameplay-followup-validation.json), [gameplay 판단](gameplay-followup-audit.ko.md), [UI 감사](refactor-ui-audit.ko.md), [서비스와 테스트 감사](refactor-services-audit.ko.md), [영수증 검증](receipt-verification.ko.md). SDK 담당은 IAPManager/지급 core/검증 core/HTTP transport와 IAP 테스트 4파일의 전체 본문·격리 경로를 확인했고, 통합 담당은 해당 변경과 owner 불일치 수정·회귀를 검토했다.

| 파일 | 책임 | 참조(Managers / Singleton) | 담당·판단 | 회귀·검증 |
| --- | --- | --- | --- | --- |
| `Assets/GPGSIds.cs` | 생성된 GPGS 식별자 계약 |  /  | 통합: 유지, 생성 파일/기존 플랫폼 식별자를 재작성하지 않음 | 기존 플랫폼 구성 검사 |
| `Assets/Scripts/Advertising/AdEntitlement.cs` | No Ads 토큰 판정 |  /  | 통합: 정확한 저장 토큰 일치와 null/빈 값 계약 유지 | RevivalAdEntitlementTests; 통합317 |
| `Assets/Scripts/Advertising/AdRequestPolicy.cs` | 테스트 광고 요청·공식 sample ID 제한 |  /  | 통합: batch/production 차단 및 명시적 Android test 조건 유지 | RevivalAdRequestPolicyTests; 통합317; UMP PR128/332 및 통합335; gameplay 빌드에서는 항상 false |
| `Assets/Scripts/Advertising/RewardedAdSession.cs` | 보상 수령자·1회 지급·완료 상태 |  /  | 통합: 원래 owner/scene 유효성, 지연 reward와 표시 종료 분리 유지 | RevivalRewardedAdSessionTests; 통합317 |
| `Assets/Scripts/Cameras/CameraManage.cs` | 영속 Cinemachine 타깃 |  / Player | 통합: 자신의 singleton만 종료 시 해제, 이전 객체가 새 owner를 지우지 않음 | PR125; 통합 317/317, gameplay-followup-validation.json. 당시 실제 왕복 미실행; 후속 격리 왕복 2회는 gameplay-harness.ko.md 참조 |
| `Assets/Scripts/Creatures/BuffingMan.cs` | 광고/No Ads 버프 진입 UI | DataM, GoogleAdMobM / Player, PlayerUICanvas | 통합: 기존 광고 세션 계약 유지, 자신의 singleton 종료 처리 | PR125; 통합 317/317, gameplay-followup-validation.json. 당시 실제 왕복 미실행; 후속 격리 왕복 2회는 gameplay-harness.ko.md 참조 |
| `Assets/Scripts/Creatures/Creature.cs` | 전투 상태/능력치/피해·애니메이션 연결 | DataM, PoolM, SoundM / Player, PlayerUICanvas | 통합: 전투 CTS 교체/종료 확인, animation 이벤트와 serialized 필드 유지. locale별 숫자 파싱은 후속 경계 | 기존 battle restart 회귀; 실전투 시각 검증 필요 |
| `Assets/Scripts/Creatures/GameMan.cs` | 플레이어 진입 시 portal 표시 |  /  | 통합: 단순 트리거 유지; serialized 참조 보존 | 직접 회귀 없음 |
| `Assets/Scripts/Creatures/Monster.cs` | AI·탐지·전투·포획·풀 복귀 | DataM, PoolM / MonsterGenerator, Player | 통합: 사망/비활성/미배치 agent Update 차단, 탐지 재시작 시 이전 CTS 취소·해제 | PR125; 통합 317/317, gameplay-followup-validation.json. 당시 실제 왕복 미실행; 후속 격리 왕복 2회는 gameplay-harness.ko.md 참조 |
| `Assets/Scripts/Creatures/Player.cs` | 플레이어 전투·이동·보유 데이터 | DataM, EquipmentM, GameM, Instance, PoolM, SoundM, UpdateM / BuffingMan, CameraManage, JoyStick, Managers, PlayerUICanvas | 통합: publisher 소유 구독 정리, 저장 no-op 원문 보존, 조회 대상의 Gold 반환 | PR125; 통합 317/317, gameplay-followup-validation.json. 당시 실제 왕복 미실행; 후속 격리 왕복 2회는 gameplay-harness.ko.md 참조 |
| `Assets/Scripts/Creatures/ShopMan.cs` | 상점 구매·장착·목록 | EquipmentM, IAPM, PopupM, SoundM / Player | 통합: 구매 확인 1회 소비, 금액/잔액/보유 재검사, singleton 소유권 정리 | PR125; 통합 317/317, gameplay-followup-validation.json. 당시 실제 왕복 미실행; 후속 격리 왕복 2회는 gameplay-harness.ko.md 참조 |
| `Assets/Scripts/Editor/BuildScript.cs` | 보존한 legacy build/menu/version 경로 |  /  | 통합: batch guard와 CI 비활성 유지. legacy define/버전 파싱의 일반 개선을 위해 보존 경로를 활성화하지 않음 | 소스 감사; 현재 빌드는 RevivalBuild 사용, legacy 운영 빌드 미실행 |
| `Assets/Scripts/Editor/RevivalAdHarnessBuild.cs` | sample/control 광고 격리 빌드 |  /  | UI: 유지, allowlist/별도ID/자동IAP금지/설정 byte복원 확인 | 기존 및 UMP sample/control build/verifier/실기기 완료; consent-gate-validation.json |
| `Assets/Scripts/Editor/RevivalBuild.cs` | 격리 APK/AAB build·settings/scene guard |  /  | 통합: 유지, dirty-scene 거절/격리 첫씬/debug서명/finally 복원 확인 | 기준 scene 회귀 및 기존 실제 APK/AAB 근거 |
| `Assets/Scripts/Editor/RevivalPackageUpgrade.cs` | 명시적 pinned UPM 업그레이드 |  /  | 통합: 유지, CLI 명시 호출에만 reload 재개/timeout·결과처리 확인 | 이전 SDK upgrade 실행 근거 유지 |
| `Assets/Scripts/Editor/RevivalSdkValidation.cs` | Android 광고 artifact preflight |  /  | 통합: 유지, 명시적 configure와 build-time pinned검사를 분리 | RevivalSdkMigrationTests |
| `Assets/Scripts/Effects/EffectActiveControl.cs` | 애니메이션 이벤트로 효과 비활성 |  /  | 통합: 유지: 외부 애니메이션 호출 가능하여 dead-code 삭제 금지 | 애니메이션 연결 시각 검증 필요 |
| `Assets/Scripts/Foundations/DebugLogger.cs` | 조건부 개발 로그 |  /  | 통합: Debug 조건과 legacy DEBUG define의 대소문자 차이 확인. 현재 로그 정책을 보존하며 일괄 활성화하지 않음 | 기준 조건부 로그 회귀; 호출부 소스 확인 |
| `Assets/Scripts/Foundations/Extension.cs` | GetOrAddComponent 확장 API |  /  | 통합: 유지: Utility 단일 구현에 위임 | 기준 회귀 |
| `Assets/Scripts/Foundations/GameConstants.cs` | 저장/씬/상품 enum 계약 |  /  | 통합: 유지: 이름·순서 변경은 저장 및 serialized 호환 위험 | 기준 계약 회귀 |
| `Assets/Scripts/Foundations/Initialize.cs` | 빈 초기화 순서 템플릿 |  /  | 통합: 현재 no-op 유지; 직렬화된 MonoBehaviour 계약을 삭제할 근거 없음 | 소스 감사; 직접 회귀 없음 |
| `Assets/Scripts/Foundations/ScreenSize.cs` | 화면 배경 크기 |  /  | 통합: RectTransform 및 구성된 기준 크기를 전제로 유지; 현재 사용처의 확정 오류 근거 없음 | 소스 감사; 다양한 화면 크기 시각 검증 미실행 |
| `Assets/Scripts/Foundations/ScreenSizeRatio.cs` | Canvas 기준 배경 크기 |  /  | 통합: isOn 및 구성된 기준 크기 계약 유지; 잘못된 0 크기 설정의 일반 방어는 별도 | 소스 감사; 다양한 화면 크기 시각 검증 미실행 |
| `Assets/Scripts/Foundations/SetFPS.cs` | 프레임 목표 설정 |  /  | 통합: 유지: 직렬화된 FPS 적용 단일 책임 | 기기 성능 별도 |
| `Assets/Scripts/Foundations/TimeUtility.cs` | 시간 포맷·보상 기간 |  /  | 통합: 잘못된 날짜의 0 반환과 저장 포맷 유지; 형식/문화권 변경은 저장 호환 검증 필요 | 기준 시간 회귀; 운영 시간대 전환 미검증 |
| `Assets/Scripts/Foundations/Utility.cs` | JSON·component·군집 계산 |  /  | 통합: 유지: 중복 구현 없음; API/계산 규칙 보존 | 기준 utility/군집 회귀 |
| `Assets/Scripts/Game/InitializeGame.cs` | MiniMap/game/monster 초기화 | GameM, PopupM / MiniMap, MonsterGenerator | UI: 빈 enum 반복은 현재 무동작. 직렬화 배열과 편집기 UI는 보존, gameplay 담당 초기화와 중복 재작성하지 않음 | 267/267 소스1f648f5; 소스 감사 |
| `Assets/Scripts/Game/Map.cs` | 배경 랜덤 배치 |  /  | 통합: 비어 있지 않은 prefab 배열의 설정 계약 유지. 잘못된 배열 구성 가능성과 실제 결함을 구분 | 소스 감사; 실제 배치/빈 배열 방어 검증 없음 |
| `Assets/Scripts/Game/MonsterGenerator.cs` | 생성 수·군집·생성 루프 | PoolM / Player | 통합: 반복 Init/Disable 멱등, CTS 정리와 군집 목록 snapshot 회수 | PR125; 통합 317/317, gameplay-followup-validation.json. 당시 실제 왕복 미실행; 후속 격리 왕복 2회는 gameplay-harness.ko.md 참조 |
| `Assets/Scripts/Game/Portal.cs` | 이동·치유 트리거 | PopupM / Player | 통합: 자신의 singleton만 종료 시 해제; 이동/치유 규칙 유지 | PR125; 통합 317/317, gameplay-followup-validation.json. 당시 실제 왕복 미실행; 후속 격리 왕복 2회는 gameplay-harness.ko.md 참조 |
| `Assets/Scripts/Login/Login.cs` | GPGS/기기/custom 로그인·프로필·씬 진입, Data/Server/UI | DataM, Instance, SceneM, ServerM, SoundM / Managers, PlayGamesPlatform | 서비스: OnDestroy가 새 singleton 계정을 중지하지 않도록 Start의 Dataowner 캡처. await 서버 대기·취소도 기존 서버 참조 사용. 인증 방식/식별자/캐시 규칙은 유지 | PR121/283 통과; 기존 LoginContinuity 56, 새 owner 교체 후 파괴 회귀. 실제 인증/기기 계정 복원 별도 |
| `Assets/Scripts/Main/IAPItem.cs` | No Ads 구매 항목 표시 | DataM / ShopMan | 통합: 등록한 ShopMan에서 해제; 기존 No Ads 저장 키 계약 유지 | PR125; 통합 317/317, gameplay-followup-validation.json. 당시 실제 왕복 미실행; 후속 격리 왕복 2회는 gameplay-harness.ko.md 참조 |
| `Assets/Scripts/Main/InitializeMain.cs` | LoginCheck→BuffingMan→UI/game 초기화 | GameM, PopupM /  | UI: 기존 초기화 순서/직렬화 배열 유지. 문자열 기반 초기화는 서비스 초기화 작업과 연계해 검토 | 267/267 소스1f648f5; 소스/연결 감사, 실제 로그인 실행 안 함 |
| `Assets/Scripts/Main/Item.cs` | 상점 아이템 표시·선택 | DataM, EquipmentM / Player, ShopMan | 통합: 등록한 ShopMan에서 해제, 잠금 상태를 양방향 갱신 | PR125; 통합 317/317, gameplay-followup-validation.json. 당시 실제 왕복 미실행; 후속 격리 왕복 2회는 gameplay-harness.ko.md 참조 |
| `Assets/Scripts/Main/LoginCheck.cs` | 첫 진입 Player/UI 생성 및 tutorial 확인 | DataM, GameM, ResourceM /  | UI: 계정 연속성 담당 경계, 읽기만. UI 생성 순서 보존 | 267/267 소스1f648f5; 기존 로그인 회귀 유지 |
| `Assets/Scripts/Managers/DataManager.cs` | 로컬 영구 저장·계정 귀속·cloud patch, Resource/Server/Player | ResourceM, ServerM / Player | 서비스: 반복 초기화가 진행 중인 메모리 상태를 다시 읽지 않도록 멱등화. Shutdown에서 timer·서버 요청 중지, 종료 후 쓰기 거부. 파일/backup/미확인 구매 권한을 삭제하지 않음 | PR121/283 통과; 기존 DataSync 42 + 종료 후 저장 문자열/No Ads 보존 회귀 |
| `Assets/Scripts/Managers/EquipmentManager.cs` | Player 장비 목록·GameObject mapping |  / Player, PlayerUICanvas, ShopMan | 서비스: [#118](https://github.com/ChoiDaeYoung-94/Tamer/issues/118)의 반복 Dictionary.Add 예외 수정. dictionary 참조는 유지하면서 현재 Player의 장비로 재바인딩. 기존 장착 목록/저장/효과 불변 | PR121/283 통과; 신규 preview 회귀 2개: 반복 Init·Player 교체·장착 목록/이전 객체 보존·owner 없음. unknown 장비·슬롯 교체 규칙은 변경하지 않음 |
| `Assets/Scripts/Managers/GameManager.cs` | Main/Game 전환·Player/카메라/UI 조정 | PopupM, SceneM / CameraManage, JoyStick, Player, PlayerUICanvas | 서비스: UI 담당의 IsTransitioning 계약을 사용해 GameOverGoLobby도 Player 초기화 전에 중복 전환을 차단. SwitchMainOrGameScene guard는 UI 담당 변경 | PR121/283 통과; 전환 중 Player/UI 접근 없이 반환하는 신규 회귀. 실제 씬 객체 조합은 통합 책임 |
| `Assets/Scripts/Managers/GoogleAdMobManager.cs` | 광고 SDK·callback queue·세션·오디오 소유 | DataM, Instance, SoundM / BuffingMan, Managers, Player, PlayerUICanvas | 통합: 지연 callback/보상 owner와 UMP update→form→allowed 순서 확인, update만 30초 제한 | PR128/332, 통합335; sample/control 및 native 경계 독립 확인, 정확 5초 판정 미확정 |
| `Assets/Scripts/Managers/IAPManager.cs` | IAP v5 연결·상품·주문·복원·지급 조립 | DataM, Instance / Managers, ShopMan | SDK: 전체 소스 감사, 검증 session과 실제 Data owner 일치 후 지급. 기본 store-trust 유지, 주입한 경우에만 서버 검증 | PR120/299 및 통합317; 실제 인증/구매/복원·환불 미검증 |
| `Assets/Scripts/Managers/IapConsentDefaults.cs` | 초기 동의 기본값 |  /  | 서비스: 정적 순수 변환 + 시작 hook, 수명 자원 없음. 정책 변경 없이 유지 | PR121/283 통과; 기존 IAP 설정 테스트 |
| `Assets/Scripts/Managers/Managers.cs` | 서비스 조립, static 접근, persistent owner |  /  | 서비스: 중복 Init이 owner를 교체하던 문제 수정. 단일 소유권·멱등 초기화/종료, 초기화 실패 시 owner 해제, 종료 시 소유 서비스 정리. 이름/직렬화 참조 유지 | PR121/283 통과; 신규 preview 테스트: 중복/종료/재획득 차단/서버 늦은 응답. 실제 로그인 씬 재진입은 별도 |
| `Assets/Scripts/Managers/PlayerDataSyncPolicy.cs` | 순수 merge·값 검증·키별 revision |  /  | 서비스: 수명/외부 구독 없음. 기존 pending revision·No Ads 합집합 정책이 맞아 변경하지 않음 | PR121/283 통과; 기존 DataSync/SaveQueue 회귀 |
| `Assets/Scripts/Managers/PoolManager.cs` | GO/UI 풀 소유·대여·회수 | Instance / Managers, MonsterGenerator | 통합: 대여 중 객체까지 소유 자원 종료, 외부 parent 보존, 종료 후 사용 거부 | PR122 및 통합 317/317; RevivalPoolTests |
| `Assets/Scripts/Managers/PopupManager.cs` | 팝업 순서, 입력 차단; Update/Sound/Game/광고 | GameM, GoogleAdMobM, SoundM, UpdateM / Player | UI: 중복 등록 및 비활성/파괴 객체 제거. reset 전에 스택 snapshot/clear. 객체별 차단 소유자와 기존 명시적 전역 차단을 분리. 구독 원본 보관/해제 | 267/267 소스1f648f5; 광고 popup 회귀 + 외부 disable/중복/겹친 blocker/reset |
| `Assets/Scripts/Managers/ResourceManager.cs` | Resources load/instantiate·오류 보고 |  /  | 서비스: 소유 loop·구독·mutable cache 없음. DI 계층 추가 없이 유지 | PR121/283 통과; 기존 smoke/GUID 검증; 개별 resource 누락은 asset 검증 책임 |
| `Assets/Scripts/Managers/SceneManager.cs` | 중간 씬/저장 대기/목적지 로드; Data/Server/Sound | DataM, PopupM, ServerM, SoundM /  | UI: 중복 요청과 CTS 덮어쓰기 방지, 목적지 snapshot, 소유 async finally에서 dispose. native activation 잠금 제거, realtime 대기 | 267/267 소스1f648f5; 중복 NextScene/GoScene, 취소된 load가 native 실행 전 종료 |
| `Assets/Scripts/Managers/ServerManager.cs` | 계정별 직렬 요청·bounded retry·timeout | DataM /  | 서비스: production 콜백을 생성 시 DataManager에 바인딩. Dispose가 요청 generation·queue·timer 수명을 닫음. 종료 후 새 요청/늦은 성공/재시도 무효 | PR121/283 통과; 기존 ServerRequest 14 + 종료/재시도/ACK 회귀. 이미 서버가 수락한 쓰기는 취소로 되돌릴 수 없음 |
| `Assets/Scripts/Managers/SoundManager.cs` | AudioSource·mixer·설정, PlayerPrefs |  /  | 서비스: coroutine/외부 구독 없음. 광고 재개 callback은 소유 객체 유효성·재생 revision을 검사. 변경하지 않음 | PR121/283 통과; 기존 광고 오디오 동작 계약; 모든 실제 clip 재생은 별도 |
| `Assets/Scripts/Managers/Sub/PoolObject.cs` | 풀 대상 marker |  /  | 통합: 유지: 타입 자체가 풀 계약 | RevivalPoolTests |
| `Assets/Scripts/Managers/Sub/PopupObject.cs` | 팝업 enable/disable 연결 | PopupM /  | UI: 등록받은 manager를 보관해 동일 객체를 해제. 자체 닫기 버튼이 다른 top을 닫지 않음. teardown에서 singleton 재조회 안 함 | 267/267 소스1f648f5; 아래 popup 버튼/상위 유지, singleton 제거 후 disable |
| `Assets/Scripts/Managers/Sub/RuntimeInitialize.cs` | Editor 시작 씬/개발 console |  /  | 서비스: loop·구독 없음. smoke/ad harness 제외 규칙 유지, SDK 초기화로 확장하지 않음 | PR121/283 통과; 기존 격리 시작 씬 빌드 근거. console 참조 없는 개발 오브젝트 구성은 별도 검사 필요 |
| `Assets/Scripts/Managers/TapjoyManager.cs` | 과거 Tapjoy 참고 코드 | PopupM / Btn_Setting | 서비스: 파일 전체 주석으로 실행되지 않음. 재활성화하거나 제거하지 않음 | PR121/283 통과; 실행 코드 없음; 구형 광고 통합 완료로 간주하지 않음 |
| `Assets/Scripts/Managers/UpdateManager.cs` | 공통 프레임 이벤트 |  /  | 통합: 영속 publisher의 EveryUpdate 및 Destroy 시 구독 해제 유지. disable에 따른 scheduler 의미 변경은 하지 않음 | 소스 감사; Player/MiniMap/UI publisher 종료 회귀 포함 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWar.cs` | 시야/차단 cell 집계·가시성 이벤트 |  /  | 통합: 계산 규칙 유지. callback 목록 변경 위험을 조사했으나 추적 scene/prefab의 OnVisibilityChange 연결은 발견하지 못함 | 소스/직렬화 참조 검색; 시야 시각 회귀 미실행 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarData.cs` | 안개 cell 집합/격자 공유 데이터 |  /  | 통합: 유지, Clear가 동일 집합을 비우므로 참조 보존 | renderer 회귀에서 실제 데이터 사용 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarEntity.cs` | 시야 차단물 등록/바운드/가시성 이벤트 |  /  | 통합: 등록/해제 대칭 유지; 현재 직렬화 callback 연결 없음, 동적 재진입 일반 지원은 검증하지 않음 | 소스/직렬화 참조 검색 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarGrid.cs` | 격자 위치/경계 cell 계산 |  /  | 통합: 실제 FoW_Data의 CellSize=1 계약 유지; 임의 CellSize로 일반화하지 않음 | 소스와 구성 에셋 확인; 직접 좌표 회귀 없음 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarMouseVision.cs` | 로컬 좌표 반경 시야 제공 |  /  | 통합: 로컬 좌표 API 유지; 월드 좌표 UnitVision과의 차이를 임의 통합하지 않음 | 소스 감사; 실제 mouse vision 배치 검증 없음 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarRenderer.cs` | 안개 cell texture/blur 렌더링 |  /  | 통합: Texture2D/Color 버퍼 재사용, 소유 material/texture/RT 정리, 임시 RT finally 반환 | PR125; 통합 317/317, gameplay-followup-validation.json. 당시 실제 왕복 미실행; 후속 격리 왕복 2회는 gameplay-harness.ko.md 참조 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarUnitGroup.cs` | 시야·entity 등록 집합 |  /  | 통합: 유지, 집합을 교체하지 않는 Clear 계약 | 소스 감사 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarUnitVision.cs` | 월드 좌표 반경 시야 제공 |  /  | 통합: 유지, LineOfSight 단일 구현에 위임 | 시야 시각검증 필요 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarVisionBase.cs` | 시야 제공자 등록/해제 계약 |  /  | 통합: 유지, OnEnable/Disable 대칭; serialized unitGroup 변경 지원은 요구되지 않음 | 소스 감사 |
| `Assets/Scripts/MiniMap/FogOfWar/LineOfSight.cs` | 격자 원·차단 ray 추적 |  /  | 통합: 대각 차단과 기존 계산 규칙을 유지; 동작 변경 근거 없음 | 소스 감사; 별도 알고리즘/시야 시각 회귀 없음 |
| `Assets/Scripts/MiniMap/FogOfWar/SliderValueToText.cs` | 안개 slider 숫자 표시 |  /  | 통합: Start 초기화 계약 유지; 추적 scene/prefab에 해당 callback 연결 없음 | 소스/직렬화 참조 검색; Start 이전 동적 호출 미검증 |
| `Assets/Scripts/MiniMap/MiniMap.cs` | 지도 아이콘·드래그·일시정지 | DataM, Instance, UpdateM / JoyStick, Managers, Player, PlayerUICanvas | 통합: 캡처한 publisher에서 해제, 저장한 timeScale을 멱등 복구. 중첩된 0 pause owner의 전역 조정은 지원하지 않음 | PR125; 통합 317/317, gameplay-followup-validation.json. 당시 실제 왕복 미실행; 후속 격리 왕복 2회는 gameplay-harness.ko.md 참조 |
| `Assets/Scripts/MiniMap/MiniMapCanvas.cs` | 지도 닫기 중계 |  / MiniMap | 통합: OnEnable에서 캡처한 MiniMap owner만 OnDisable에서 닫음 | PR125; 통합 317/317, gameplay-followup-validation.json. 당시 실제 왕복 미실행; 후속 격리 왕복 2회는 gameplay-harness.ko.md 참조 |
| `Assets/Scripts/NextScene/NextScene.cs` | 중간 씬 Start→GoScene | SceneM /  | UI: 단일 위임 유지. 중복 방지는 SceneManager의 책임 | 267/267 소스1f648f5; SceneManager gate 회귀 |
| `Assets/Scripts/Purchasing/NoAdsPurchaseFulfillment.cs` | No Ads 영속 저장 후 confirm |  /  | SDK: 순수 core 유지. paid 주문만 처리, 저장 성공 전 confirm 금지, 복원은 재confirm하지 않음 | Fulfillment13; 통합317. 영수증 진위나 실제 디스크 원자성 증명 아님 |
| `Assets/Scripts/RevivalAdHarness.cs` | 격리 sample/control 광고 수동 실험 UI | Instance / Managers | 통합: test-only owner/scene/manager 파괴 실험과 event 추적 유지; 운영 진입 경로 아님 | PR128 기기 중복 Show/Home/조기 취소 확인; consent-gate-validation.json |
| `Assets/Scripts/RevivalAdReceiptOwner.cs` | 광고 callback 수명 marker |  /  | UI: 유지, harness/editor 조건부 타입 | 기존 owner파괴 광고 회귀/기기검증 |
| `Assets/Scripts/SetCharacter/CanvasSelectCharacter.cs` | 선택 이동/저장 확인; Server/Data/Animator | DataM, PopupM, SceneM, ServerM, SoundM /  | UI: 이동 single-flight를 bool로 관리해 pooled UniTask Status 재조회 제거. destroy token으로 이동 취소. 저장/성별/계정 로직 유지 | 267/267 소스1f648f5; 이미 취소된 이동에서 대상 접근 없이 busy 해제 |
| `Assets/Scripts/UI/JoyStick.cs` | 포인터/키 입력을 Player 이동으로 연결 |  / Player | UI: 초기화 전 camera/player 접근 방지. disable/focus 상실 시 입력 방향/거리/handle 초기화. 자기 singleton 파괴 정리 | 267/267 소스1f648f5; 초기화 전 FixedUpdate, disable/focus held-input 해제 |
| `Assets/Scripts/UI/Loading.cs` | 빈 MonoBehaviour |  /  | UI: 직렬화된 script 연결을 깨뜨릴 이유 없어 유지. 책임을 새로 만들지 않음 | 267/267 소스1f648f5; 소스 감사 |
| `Assets/Scripts/UI/PlayerUICanvas.cs` | HUD/정보/버프 표시; Player/Data/광고/Update | DataM, GameM, GoogleAdMobM, PopupM, SoundM, UpdateM / MiniMap, Player | UI: 기존 publisher 보관, 재초기화 시 이전 구독 제거, 파괴 시 해제 및 자기 singleton만 정리 | 267/267 소스1f648f5; 원래 publisher의 버프 callback 해제 |
| `Assets/Scripts/UI/TMP_Damage.cs` | 풀링 피해 텍스트; TMP/DOTween/Pool | PoolM /  | UI: transform 대상 Kill 대신 소유 Sequence Kill. 하위 fade/위치 tween과 이전 pool-return callback 함께 종료 | 267/267 소스1f648f5; 실제 sequence 활성→Clear→비활성 |
| `Assets/Tests/Editor/IAP/RevivalIAPConfigurationTests.cs` | 상품·자동 초기화·동의 기본값 3개 |  /  | 전체 소스 감사 완료: 정적 카탈로그/순수 기본값 확인, SDK 초기화 없음 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/IAP/RevivalIAPFulfillmentTests.cs` | 지급·confirm 순서와 재시도 13개 |  /  | 전체 소스 감사 완료: 메모리 저장 delegate; 실제 영수증 진위 검증 아님 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/IAP/RevivalIAPOrderTests.cs` | v5 Order/Cart와 IAP 상태 14개 |  / BindingFlags | 전체 소스 감사 완료: 합성 receipt와 reflection, StoreController/Init 호출 없음 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalAdEntitlementTests.cs` | No Ads 토큰 판정 |  /  | 전체 소스 감사 완료: 순수 문자열 입력, 저장/광고 호출 없음 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalAdManagerTests.cs` | 광고 manager callback·종료·오디오 |  / BindingFlags | 전체 소스 감사 완료: PreviewScene/fake receipt와 수동 callback, worker queue; batch guard, SDK 요청 없음 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalAdPopupTests.cs` | 광고 popup과 owner 상실 | Awake, OnDestroy / BindingFlags | 전체 소스 감사 완료: 비활성 Managers 및 명시적 Server fixture; Init/실제 서버 요청 없이 정리 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalAdRequestPolicyTests.cs` | 광고 허용 환경과 sample ID |  /  | 전체 소스 감사 완료: 순수 정책 입력, SDK/네트워크 없음 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalBaselineTests.cs` | 저장·설정·씬·utility 기본 계약 |  /  | 전체 소스 감사 완료: dirty scene 보호 및 batch fixture; 운영 인증 없음 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalDataSyncTests.cs` | 저장·계정·merge 43개 |  / BindingFlags | 전체 소스 감사 완료: 고유 임시 폴더/합성 record; OS 저장 실패 회귀와 Android 한계 구분 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalLoginContinuityTests.cs` | 계정 선택·login gate 56개 |  / BindingFlags | 전체 소스 감사 완료: 순수 정책/합성 오류/CTS; 실제 인증 없음 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalMonsterLifecycleTests.cs` | 전투·탐지·풀·상점·지도 수명 |  / BindingFlags | 전체 소스 감사 완료: PreviewScene/reflection, CTS/텍스처/시간 상태 정리; 실제 전투 왕복 아님 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalPoolTests.cs` | 풀 재사용·소유·종료 |  /  | 전체 소스 감사 완료: 격리 GameObject/외부 parent 구성과 정리, 실제 prefab 전체 조합 아님 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalRewardedAdSessionTests.cs` | 보상·완료·owner 유효성 |  /  | 전체 소스 감사 완료: 가짜 callback으로 1회 지급/지연 순서 검사 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalSaveQueueTests.cs` | Data/Server revision 경합 3개 |  / BindingFlags | 전체 소스 감사 완료: 임시 저장과 메모리 cloud, callback 순서 주입; 실제 서버 없음 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalSdkMigrationTests.cs` | SDK 설정·버전·마이그레이션 |  /  | 전체 소스 감사 완료: 일시적 SDK 설정을 finally에서 복원, 식별자 값을 공개하지 않음 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalServerRequestTests.cs` | 직렬 요청·retry·timeout·종료 16개 |  /  | 전체 소스 감사 완료: 가짜 전송/시계, Private DTO만 생성; 운영 쓰기 없음 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalUILifecycleTests.cs` | UI publisher/input/tween/move | 실제 component reflection | 전체 소스 감사 완료: PreviewScene/static 복원과 실제 소유 tween 정리; DOTween fixture flag는 finally 복원 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Scripts/IapReceiptHttpVerifier.cs` | 명시적 HTTPS 검증 transport |  /  | SDK: URI/redirect/nonce/account/product/응답크기/cancel 경계 검토 | PR120/299 및 통합317 통과; 실제endpoint미배포 |
| `Assets/Scripts/Purchasing/ReceiptVerification.cs` | 영수증 검증·지급 owner/session 계약 |  /  | SDK: 검증 계정과 실제Dataowner 불일치P1 보완확인 | SDK receipt16 회귀, 통합317 |
| `Assets/Tests/Editor/IAP/RevivalReceiptVerificationTests.cs` | 검증 session/owner/취소 16개 |  /  | 전체 소스 감사 완료: FakeVerifier와 TCS, unsafe URI/선행 취소 검사; 실제 HTTP 없음 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalEquipmentLifecycleTests.cs` | 장비 재바인딩 2개 |  /  | 전체 소스 감사 완료: 비활성 Player/합성 장비, 기존 singleton 복원; 실제 효과 계산 미검증 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Tests/Editor/RevivalManagerLifecycleTests.cs` | manager/login/scene owner 수명 6개 |  / BindingFlags | 전체 소스 감사 완료: 비활성 PreviewScene, singleton finally 복원; 실제 Awake/씬 왕복 아님 | 통합317/317에 포함; 서비스 6파일은 refactor-services-audit.ko.md, IAP 4파일은 SDK 감사 근거 |
| `Assets/Scripts/Advertising/AdConsentGate.cs` | 순수 consent 상태/세대/타임아웃 | IAdConsentClient | UI·통합: 중복/늦은 callback과 갱신 실패 차단, 열린 폼은 강제 종료하지 않음 | fake14 및 통합335 |
| `Assets/Scripts/Managers/GoogleUmpConsentClient.cs` | 설치된 UMP API 어댑터 | GoogleMobileAds.Ump | UI·통합: TFUA와 필요 폼/옵션 API 연결, 차단 경로에서 생성하지 않음 | 실기기 Update→allowed→SDK load; 지역별 실제 폼 미관찰 |
| `Assets/Tests/Editor/RevivalAdConsentTests.cs` | 동의 요청/실패/종료/재시도 회귀 | AdConsentGate | 전체 소스 검토: fake client/queue, SDK·네트워크 없음 | 14개 및 통합335 |
| `Assets/Scripts/RevivalGameplayIsolation.cs` | 메모리 서버·고유 저장 경로 | ServerManager/DataManager | 통합·서비스: 합성 ID만 허용, 실행별 새 저장 경로, 일반 player 제외 | 격리3 회귀/실제 local file 생성; 기기 write0 |
| `Assets/Scripts/RevivalGameplayHarness.cs` | 기존 씬 왕복·owner·오류 관찰 | Managers/Player/SceneManager | 통합·서비스: 별도 Android ID 요구, 원래 씬/owner 확인, 합성 계정만 사용 | 기기 왕복2/Home복귀/errors0; 운영 계정 아님 |
| `Assets/Scripts/Editor/RevivalGameplayBuild.cs` | 오프라인 APK 조립·원복 | BuildPipeline/manifest | 통합·서비스: 별도ID/debug서명/빌드전용심볼, network·billing·provider 제거 | 실제 APK verifier와 설정 원복; 원래4씬 변경 없음 |
| `Assets/Tests/Editor/RevivalGameplayIsolationTests.cs` | 격리 저장·대역·manifest 회귀 | PreviewScene/reflection/XML | 통합·서비스: 고유 임시 폴더/비활성 Data/메모리응답, finally 정리 | 3개 및 통합335; Android 권한은 실제APK 별도검증 |

105개 파일의 소스 감사 판단을 연결했다. 통합 Editor 335개 회귀와 별도 오프라인 APK의 실제 씬 왕복 2회를 구분한다. 원래 씬을 실행했지만 합성 계정·메모리 서버로 격리했으며 운영 인증/진행도 쓰기/구매/정책/16KB 완료를 뜻하지 않는다. 최신 근거는 [격리 gameplay 검증](gameplay-harness.ko.md)과 [UMP 검증](families-consent-gate.ko.md)에 있다.
