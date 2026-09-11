# 자체 C# 파일별 감사 진행표

범위: 추적 중인 Assets/Scripts, Assets/Tests, Assets/GPGSIds.cs의 93개 파일. SDK 디렉터리는 별도 공급망 감사 범위다. Scripts/MiniMap/FogOfWar는 출처만으로 제외하지 않고 포함한다. 신규 파일은 통합 시 inventory를 다시 생성해 추가한다.

아래 의존성은 소스의 lexical 참조이며 전체 호출 그래프가 아니다. `검토 대기`는 완료로 집계하지 않는다. 담당 감사 문서/실제 테스트 결과를 통합하면서 판단을 갱신한다. 기준 소스: `14f8ee0667c06f9369bcb921adb26fbfe31f85bd`.

| 파일 | 책임 | 참조(Managers / Singleton) | 담당·판단 | 회귀·검증 |
| --- | --- | --- | --- | --- |
| `Assets/GPGSIds.cs` | 생성된 GPGS 식별자 계약 |  /  | 통합: 유지, 생성 파일/기존 플랫폼 식별자를 재작성하지 않음 | 기존 플랫폼 구성 검사 |
| `Assets/Scripts/Advertising/AdEntitlement.cs` | AdEntitlement |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Advertising/AdRequestPolicy.cs` | AdRequestPolicy |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Advertising/RewardedAdSession.cs` | RewardedAdOutcome / RewardedAdSession |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Cameras/CameraManage.cs` | 영속 Cinemachine 타깃 |  / Player | 통합: singleton 종료 처리 후속 | scene 교체 회귀 필요 |
| `Assets/Scripts/Creatures/BuffingMan.cs` | 광고/No Ads 버프 진입 UI | DataM, GoogleAdMobM / Player, PlayerUICanvas | 통합: 기존 광고 세션 회귀 유지; 소유 singleton 해제 후속 | 광고 회귀/기기 harness; 운영 미검증 |
| `Assets/Scripts/Creatures/Creature.cs` | 전투 상태/능력치/피해·애니메이션 연결 | DataM, PoolM, SoundM / Player, PlayerUICanvas | 통합: 전투 CTS 교체/종료 확인, animation 이벤트와 serialized 필드 유지. locale별 숫자 파싱은 후속 경계 | 기존 battle restart 회귀; 실전투 시각 검증 필요 |
| `Assets/Scripts/Creatures/GameMan.cs` | 플레이어 진입 시 portal 표시 |  /  | 통합: 단순 트리거 유지; serialized 참조 보존 | 직접 회귀 없음 |
| `Assets/Scripts/Creatures/Monster.cs` | AI·탐지·전투·포획·풀 복귀 | DataM, PoolM / MonsterGenerator, Player | 통합: 확정 후속: Update OR 조건, 탐지 중복 시작 검토 | 기존 lifecycle 회귀; 후속 검증 필요 |
| `Assets/Scripts/Creatures/Player.cs` | 플레이어 전투·이동·보유 데이터 | DataM, EquipmentM, GameM, Instance, PoolM, SoundM, UpdateM / BuffingMan, CameraManage, JoyStick, Managers, PlayerUICanvas | 통합: PR122/254 통과: 캡처 Update publisher 해제; 저장 문자열 후속 점검 | RevivalMonsterLifecycleTests; Unity 대기 |
| `Assets/Scripts/Creatures/ShopMan.cs` | 상점 구매·장착·목록 | EquipmentM, IAPM, PopupM, SoundM / Player | 통합: 확정 후속: 구매 확인 중복 호출 방지/소유 singleton 해제 | 구매 골드·중복 회귀 필요 |
| `Assets/Scripts/Editor/BuildScript.cs` | 보존한 legacy build/menu/version 경로 |  /  | 통합: batch guard 유지/CI 비활성; 최신 빌드에는 RevivalBuild 사용. legacy define·버전 파싱 후속 목록 | 기준 batch 회귀; legacy 운영 빌드 미실행 |
| `Assets/Scripts/Editor/RevivalAdHarnessBuild.cs` | sample/control 광고 격리 빌드 |  /  | UI: 유지, allowlist/별도ID/자동IAP금지/설정 byte복원 확인 | 기존 sample/control hash 및 실기기 근거; consent 후속 실행 대기 |
| `Assets/Scripts/Editor/RevivalBuild.cs` | 격리 APK/AAB build·settings/scene guard |  /  | 통합: 유지, dirty-scene 거절/격리 첫씬/debug서명/finally 복원 확인 | 기준 scene 회귀 및 기존 실제 APK/AAB 근거 |
| `Assets/Scripts/Editor/RevivalPackageUpgrade.cs` | 명시적 pinned UPM 업그레이드 |  /  | 통합: 유지, CLI 명시 호출에만 reload 재개/timeout·결과처리 확인 | 이전 SDK upgrade 실행 근거 유지 |
| `Assets/Scripts/Editor/RevivalSdkValidation.cs` | Android 광고 artifact preflight |  /  | 통합: 유지, 명시적 configure와 build-time pinned검사를 분리 | RevivalSdkMigrationTests |
| `Assets/Scripts/Effects/EffectActiveControl.cs` | 애니메이션 이벤트로 효과 비활성 |  /  | 통합: 유지: 외부 애니메이션 호출 가능하여 dead-code 삭제 금지 | 애니메이션 연결 시각 검증 필요 |
| `Assets/Scripts/Foundations/DebugLogger.cs` | 조건부 개발 로그 |  /  | 통합: 조건부 Debug와 문서 DEBUG 불일치; 실제 define 사용 확인 | 로그 비밀 값 별도 감사 |
| `Assets/Scripts/Foundations/Extension.cs` | GetOrAddComponent 확장 API |  /  | 통합: 유지: Utility 단일 구현에 위임 | 기준 회귀 |
| `Assets/Scripts/Foundations/GameConstants.cs` | 저장/씬/상품 enum 계약 |  /  | 통합: 유지: 이름·순서 변경은 저장 및 serialized 호환 위험 | 기준 계약 회귀 |
| `Assets/Scripts/Foundations/Initialize.cs` | 빈 초기화 순서 템플릿 |  /  | 통합: 현 상태 no-op; binding 조회 후 제거 여부 결정 | 직접 회귀 없음 |
| `Assets/Scripts/Foundations/ScreenSize.cs` | 화면 배경 크기 |  /  | 통합: ExecuteInEditMode/RectTransform 설정 경계 후속 점검 | 화면 크기 검증 필요 |
| `Assets/Scripts/Foundations/ScreenSizeRatio.cs` | Canvas 기준 배경 크기 |  /  | 통합: 0 크기/누락 참조 경계 후속 점검 | 화면 크기 검증 필요 |
| `Assets/Scripts/Foundations/SetFPS.cs` | 프레임 목표 설정 |  /  | 통합: 유지: 직렬화된 FPS 적용 단일 책임 | 기기 성능 별도 |
| `Assets/Scripts/Foundations/TimeUtility.cs` | 시간 포맷·보상 기간 |  /  | 통합: 유지 우선: 저장 날짜 형식 변경 금지; 잘못된 날짜 반환 규칙 점검 | 기준 시간 회귀 |
| `Assets/Scripts/Foundations/Utility.cs` | JSON·component·군집 계산 |  /  | 통합: 유지: 중복 구현 없음; API/계산 규칙 보존 | 기준 utility/군집 회귀 |
| `Assets/Scripts/Game/InitializeGame.cs` | MiniMap/game/monster 초기화 | GameM, PopupM / MiniMap, MonsterGenerator | UI: 빈 enum 반복은 현재 무동작. 직렬화 배열과 편집기 UI는 보존, gameplay 담당 초기화와 중복 재작성하지 않음 | 267/267 소스1f648f5; 소스 감사 |
| `Assets/Scripts/Game/Map.cs` | 배경 랜덤 배치 |  /  | 통합: 빈 prefab 배열 입력과 retry 수명 점검 | 실제 배치 시각 검증 필요 |
| `Assets/Scripts/Game/MonsterGenerator.cs` | 생성 수·군집·생성 루프 | PoolM / Player | 통합: PR122/254 통과: 반복 Init/Disable 멱등; 군집 반환 후속 점검 | RevivalMonsterLifecycleTests; Unity 대기 |
| `Assets/Scripts/Game/Portal.cs` | 이동·치유 트리거 | PopupM / Player | 통합: 소유 singleton 해제 조건 후속 | scene lifecycle 회귀 필요 |
| `Assets/Scripts/Login/Login.cs` | Login / ApiResult / LoginContinuityPolicy / LoginOperationGate / LoginCallbackGate | DataM, Instance, SceneM, ServerM, SoundM / Managers, PlayGamesPlatform | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Main/IAPItem.cs` | No Ads 구매 항목 표시 | DataM / ShopMan | 통합: 등록 해제와 저장 키 매칭 검토 필요 | SDK 권한 회귀와 별도 UI 검증 필요 |
| `Assets/Scripts/Main/InitializeMain.cs` | LoginCheck→BuffingMan→UI/game 초기화 | GameM, PopupM /  | UI: 기존 초기화 순서/직렬화 배열 유지. 문자열 기반 초기화는 서비스 초기화 작업과 연계해 검토 | 267/267 소스1f648f5; 소스/연결 감사, 실제 로그인 실행 안 함 |
| `Assets/Scripts/Main/Item.cs` | 상점 아이템 표시·선택 | DataM, EquipmentM / Player, ShopMan | 통합: 목록 등록 해제/lock 재표시 점검 필요 | 직접 UI 회귀 없음 |
| `Assets/Scripts/Main/LoginCheck.cs` | 첫 진입 Player/UI 생성 및 tutorial 확인 | DataM, GameM, ResourceM /  | UI: 계정 연속성 담당 경계, 읽기만. UI 생성 순서 보존 | 267/267 소스1f648f5; 기존 로그인 회귀 유지 |
| `Assets/Scripts/Managers/DataManager.cs` | DataManager | ResourceM, ServerM / Player | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/EquipmentManager.cs` | EquipmentManager |  / Player, PlayerUICanvas, ShopMan | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/GameManager.cs` | GameManager | PopupM, SceneM / CameraManage, JoyStick, Player, PlayerUICanvas | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/GoogleAdMobManager.cs` | GoogleAdMobManager / PendingCallback | DataM, Instance, SoundM / BuffingMan, Managers, Player, PlayerUICanvas | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/IAPManager.cs` | IAPStatus / IAPManager | DataM, Instance / Managers, ShopMan | SDK: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/IapConsentDefaults.cs` | IapConsentDefaults |  /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/Managers.cs` | Managers |  /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/PlayerDataSyncPolicy.cs` | PlayerDataSyncPolicy / PlayerDataChanges |  /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/PoolManager.cs` | GO/UI 풀 소유·대여·회수 | Instance / Managers, MonsterGenerator | 통합: PR122/254 통과: 대여 중 객체까지 종료, 외부 parent 보존 | RevivalPoolTests; Unity 대기 |
| `Assets/Scripts/Managers/PopupManager.cs` | 팝업 순서, 입력 차단; Update/Sound/Game/광고 | GameM, GoogleAdMobM, SoundM, UpdateM / Player | UI: 중복 등록 및 비활성/파괴 객체 제거. reset 전에 스택 snapshot/clear. 객체별 차단 소유자와 기존 명시적 전역 차단을 분리. 구독 원본 보관/해제 | 267/267 소스1f648f5; 광고 popup 회귀 + 외부 disable/중복/겹친 blocker/reset |
| `Assets/Scripts/Managers/ResourceManager.cs` | ResourceManager |  /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/SceneManager.cs` | 중간 씬/저장 대기/목적지 로드; Data/Server/Sound | DataM, PopupM, ServerM, SoundM /  | UI: 중복 요청과 CTS 덮어쓰기 방지, 목적지 snapshot, 소유 async finally에서 dispose. native activation 잠금 제거, realtime 대기 | 267/267 소스1f648f5; 중복 NextScene/GoScene, 취소된 load가 native 실행 전 종료 |
| `Assets/Scripts/Managers/ServerManager.cs` | ServerManager / Operation | DataM /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/SoundManager.cs` | SoundManager |  /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/Sub/PoolObject.cs` | 풀 대상 marker |  /  | 통합: 유지: 타입 자체가 풀 계약 | RevivalPoolTests |
| `Assets/Scripts/Managers/Sub/PopupObject.cs` | 팝업 enable/disable 연결 | PopupM /  | UI: 등록받은 manager를 보관해 동일 객체를 해제. 자체 닫기 버튼이 다른 top을 닫지 않음. teardown에서 singleton 재조회 안 함 | 267/267 소스1f648f5; 아래 popup 버튼/상위 유지, singleton 제거 후 disable |
| `Assets/Scripts/Managers/Sub/RuntimeInitialize.cs` | RuntimeInitialize |  /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/TapjoyManager.cs` | TapjoyManager / customEditor | PopupM / Btn_Setting | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/UpdateManager.cs` | 공통 프레임 이벤트 |  /  | 통합: UniRx EveryUpdate는 disable 후에도 계속됨; 호출자 pause 의도 확인 | publisher 소유권 회귀 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWar.cs` | 시야/차단 cell 집계·가시성 이벤트 |  /  | 통합: 계산 규칙 유지; visibility callback의 목록 mutation 후속 점검 | 소스 감사, 직접 시야 회귀 없음 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarData.cs` | 안개 cell 집합/격자 공유 데이터 |  /  | 통합: 유지, Clear가 동일 집합을 비우므로 참조 보존 | renderer 회귀에서 실제 데이터 사용 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarEntity.cs` | 시야 차단물 등록/바운드/가시성 이벤트 |  /  | 통합: 등록/해제 대칭 확인; callback중 목록 mutation 후속 점검 | 직접 회귀 없음 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarGrid.cs` | 격자 위치/경계 cell 계산 |  /  | 통합: 단위 cell 규칙 유지; CellSize 일반화는 별도 시각 검증 필요 | 직접 좌표 회귀 없음 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarMouseVision.cs` | 로컬 좌표 반경 시야 제공 |  /  | 통합: 유지 우선, unit vision과 좌표 차이 사용처 확인 필요 | 시야 시각검증 필요 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarRenderer.cs` | 안개 cell texture/blur 렌더링 |  /  | 통합: 매프레임 Texture2D 누수 수정; 버퍼 재사용/소유 texture·material·RT 해제 (44a3caa) | 신규 실제 texture 재사용/픽셀/해제 회귀; Unity 대기 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarUnitGroup.cs` | 시야·entity 등록 집합 |  /  | 통합: 유지, 집합을 교체하지 않는 Clear 계약 | 소스 감사 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarUnitVision.cs` | 월드 좌표 반경 시야 제공 |  /  | 통합: 유지, LineOfSight 단일 구현에 위임 | 시야 시각검증 필요 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarVisionBase.cs` | 시야 제공자 등록/해제 계약 |  /  | 통합: 유지, OnEnable/Disable 대칭; serialized unitGroup 변경 지원은 요구되지 않음 | 소스 감사 |
| `Assets/Scripts/MiniMap/FogOfWar/LineOfSight.cs` | 격자 원·차단 ray 추적 |  /  | 통합: 계산 소스 검토, 대각 차단 규칙 유지 | 직접 알고리즘 회귀 추가 검토 |
| `Assets/Scripts/MiniMap/FogOfWar/SliderValueToText.cs` | 안개 slider 숫자 표시 |  /  | 통합: 초기 Start 이전 UI callback 경계 점검 필요 | 직접 회귀 없음 |
| `Assets/Scripts/MiniMap/MiniMap.cs` | 지도 아이콘·드래그·일시정지 | DataM, Instance, UpdateM / JoyStick, Managers, Player, PlayerUICanvas | 통합: 캡처 publisher 해제 및 pause 종료 후속 | 직접 회귀 없음 |
| `Assets/Scripts/MiniMap/MiniMapCanvas.cs` | 지도 닫기 중계 |  / MiniMap | 통합: 파괴 순서와 현재 singleton 재조회 후속 | 직접 회귀 없음 |
| `Assets/Scripts/NextScene/NextScene.cs` | 중간 씬 Start→GoScene | SceneM /  | UI: 단일 위임 유지. 중복 방지는 SceneManager의 책임 | 267/267 소스1f648f5; SceneManager gate 회귀 |
| `Assets/Scripts/Purchasing/NoAdsPurchaseFulfillment.cs` | PurchaseDeliveryState / PurchaseFulfillmentResult / NoAdsPurchaseFulfillment |  /  | SDK: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/RevivalAdHarness.cs` | RevivalAdHarness | Instance / Managers | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/RevivalAdReceiptOwner.cs` | 광고 callback 수명 marker |  /  | UI: 유지, harness/editor 조건부 타입 | 기존 owner파괴 광고 회귀/기기검증 |
| `Assets/Scripts/SetCharacter/CanvasSelectCharacter.cs` | 선택 이동/저장 확인; Server/Data/Animator | DataM, PopupM, SceneM, ServerM, SoundM /  | UI: 이동 single-flight를 bool로 관리해 pooled UniTask Status 재조회 제거. destroy token으로 이동 취소. 저장/성별/계정 로직 유지 | 267/267 소스1f648f5; 이미 취소된 이동에서 대상 접근 없이 busy 해제 |
| `Assets/Scripts/UI/JoyStick.cs` | 포인터/키 입력을 Player 이동으로 연결 |  / Player | UI: 초기화 전 camera/player 접근 방지. disable/focus 상실 시 입력 방향/거리/handle 초기화. 자기 singleton 파괴 정리 | 267/267 소스1f648f5; 초기화 전 FixedUpdate, disable/focus held-input 해제 |
| `Assets/Scripts/UI/Loading.cs` | 빈 MonoBehaviour |  /  | UI: 직렬화된 script 연결을 깨뜨릴 이유 없어 유지. 책임을 새로 만들지 않음 | 267/267 소스1f648f5; 소스 감사 |
| `Assets/Scripts/UI/PlayerUICanvas.cs` | HUD/정보/버프 표시; Player/Data/광고/Update | DataM, GameM, GoogleAdMobM, PopupM, SoundM, UpdateM / MiniMap, Player | UI: 기존 publisher 보관, 재초기화 시 이전 구독 제거, 파괴 시 해제 및 자기 singleton만 정리 | 267/267 소스1f648f5; 원래 publisher의 버프 callback 해제 |
| `Assets/Scripts/UI/TMP_Damage.cs` | 풀링 피해 텍스트; TMP/DOTween/Pool | PoolM /  | UI: transform 대상 Kill 대신 소유 Sequence Kill. 하위 fade/위치 tween과 이전 pool-return callback 함께 종료 | 267/267 소스1f648f5; 실제 sequence 활성→Clear→비활성 |
| `Assets/Tests/Editor/IAP/RevivalIAPConfigurationTests.cs` | 회귀 테스트 |  /  | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/IAP/RevivalIAPFulfillmentTests.cs` | 회귀 테스트 |  /  | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/IAP/RevivalIAPOrderTests.cs` | 회귀 테스트 |  / BindingFlags | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalAdEntitlementTests.cs` | 회귀 테스트 |  /  | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalAdManagerTests.cs` | 회귀 테스트 |  / BindingFlags | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalAdPopupTests.cs` | 회귀 테스트 | Awake, OnDestroy / BindingFlags | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalAdRequestPolicyTests.cs` | 회귀 테스트 |  /  | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalBaselineTests.cs` | 회귀 테스트 |  /  | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalDataSyncTests.cs` | 회귀 테스트 |  / BindingFlags | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalLoginContinuityTests.cs` | 회귀 테스트 |  / BindingFlags | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalMonsterLifecycleTests.cs` | 회귀 테스트 |  / BindingFlags | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalPoolTests.cs` | 회귀 테스트 |  /  | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalRewardedAdSessionTests.cs` | 회귀 테스트 |  /  | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalSaveQueueTests.cs` | 회귀 테스트 |  / BindingFlags | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalSdkMigrationTests.cs` | 회귀 테스트 |  /  | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalServerRequestTests.cs` | 회귀 테스트 |  /  | 통합: 테스트 의도·격리·회귀 범위 검토 대기 | 기준 전체 249 통과와 변경분 결과를 구분 |
| `Assets/Tests/Editor/RevivalUILifecycleTests.cs` | UI publisher/input/tween/move 격리 회귀 | 실제 component reflection | UI: PR119 소스/회귀 검토 완료 | 전체267/267에 포함 |

후속 gameplay `44a3caa`에서는 저장 no-op 문자열 보존, 죽음/비활성 NavMesh AI 차단, 탐지 owner 교체, 상점 확인 중복 소비 방지·금액 재검사, 상점 item 해제, 미니맵 publisher와 singleton 정리를 구현했다. 새 회귀는 아직 Unity 실행 대기다. 미니맵 pause 소유·재진입, 생성기 목록 회수 iteration, 빈 map prefab 입력, 남은 editor/harness/test 파일 감사는 진행 중이며 완료로 집계하지 않는다.
