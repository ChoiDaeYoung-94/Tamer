# 자체 C# 파일별 감사 진행표

범위: 추적 중인 Assets/Scripts, Assets/Tests, Assets/GPGSIds.cs의 92개 파일. SDK 디렉터리는 별도 공급망 감사 범위다. Scripts/MiniMap/FogOfWar는 출처만으로 제외하지 않고 포함한다. 신규 파일은 통합 시 inventory를 다시 생성해 추가한다.

아래 의존성은 소스의 lexical 참조이며 전체 호출 그래프가 아니다. `검토 대기`는 완료로 집계하지 않는다. 담당 감사 문서/실제 테스트 결과를 통합하면서 판단을 갱신한다. 기준 소스: `9044b6fc2027332649f326ce012d253ab831d3ca`.

| 파일 | 책임 | 참조(Managers / Singleton) | 담당·판단 | 회귀·검증 |
| --- | --- | --- | --- | --- |
| `Assets/GPGSIds.cs` | GPGSIds |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Advertising/AdEntitlement.cs` | AdEntitlement |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Advertising/AdRequestPolicy.cs` | AdRequestPolicy |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Advertising/RewardedAdSession.cs` | RewardedAdOutcome / RewardedAdSession |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Cameras/CameraManage.cs` | 영속 Cinemachine 타깃 |  / Player | 통합: singleton 종료 처리 후속 | scene 교체 회귀 필요 |
| `Assets/Scripts/Creatures/BuffingMan.cs` | 광고/No Ads 버프 진입 UI | DataM, GoogleAdMobM / Player, PlayerUICanvas | 통합: 기존 광고 세션 회귀 유지; 소유 singleton 해제 후속 | 광고 회귀/기기 harness; 운영 미검증 |
| `Assets/Scripts/Creatures/Creature.cs` | Creature / CreatureState | DataM, PoolM, SoundM / Player, PlayerUICanvas | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Creatures/GameMan.cs` | 플레이어 진입 시 portal 표시 |  /  | 통합: 단순 트리거 유지; serialized 참조 보존 | 직접 회귀 없음 |
| `Assets/Scripts/Creatures/Monster.cs` | AI·탐지·전투·포획·풀 복귀 | DataM, PoolM / MonsterGenerator, Player | 통합: 확정 후속: Update OR 조건, 탐지 중복 시작 검토 | 기존 lifecycle 회귀; 후속 검증 필요 |
| `Assets/Scripts/Creatures/Player.cs` | 플레이어 전투·이동·보유 데이터 | DataM, EquipmentM, GameM, Instance, PoolM, SoundM, UpdateM / BuffingMan, CameraManage, JoyStick, Managers, PlayerUICanvas | 통합: 9044b6f: 캡처 Update publisher 해제; 저장 문자열 후속 점검 | RevivalMonsterLifecycleTests; Unity 대기 |
| `Assets/Scripts/Creatures/ShopMan.cs` | 상점 구매·장착·목록 | EquipmentM, IAPM, PopupM, SoundM / Player | 통합: 확정 후속: 구매 확인 중복 호출 방지/소유 singleton 해제 | 구매 골드·중복 회귀 필요 |
| `Assets/Scripts/Editor/BuildScript.cs` | BuildScript / EditorCoroutine |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Editor/RevivalAdHarnessBuild.cs` | RevivalAdHarnessBuild / CatalogFlags |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Editor/RevivalBuild.cs` | RevivalBuild / Summary |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Editor/RevivalPackageUpgrade.cs` | RevivalPackageUpgrade |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Editor/RevivalSdkValidation.cs` | RevivalSdkValidation |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
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
| `Assets/Scripts/Game/InitializeGame.cs` | InitializeGame / Scripts / customEditor | GameM, PopupM / MiniMap, MonsterGenerator | UI: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Game/Map.cs` | 배경 랜덤 배치 |  /  | 통합: 빈 prefab 배열 입력과 retry 수명 점검 | 실제 배치 시각 검증 필요 |
| `Assets/Scripts/Game/MonsterGenerator.cs` | 생성 수·군집·생성 루프 | PoolM / Player | 통합: 9044b6f: 반복 Init/Disable 멱등; 군집 반환 후속 점검 | RevivalMonsterLifecycleTests; Unity 대기 |
| `Assets/Scripts/Game/Portal.cs` | 이동·치유 트리거 | PopupM / Player | 통합: 소유 singleton 해제 조건 후속 | scene lifecycle 회귀 필요 |
| `Assets/Scripts/Login/Login.cs` | Login / ApiResult / LoginContinuityPolicy / LoginOperationGate / LoginCallbackGate | DataM, Instance, SceneM, ServerM, SoundM / Managers, PlayGamesPlatform | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Main/IAPItem.cs` | No Ads 구매 항목 표시 | DataM / ShopMan | 통합: 등록 해제와 저장 키 매칭 검토 필요 | SDK 권한 회귀와 별도 UI 검증 필요 |
| `Assets/Scripts/Main/InitializeMain.cs` | InitializeMain / Scripts / customEditor | GameM, PopupM /  | UI: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Main/Item.cs` | 상점 아이템 표시·선택 | DataM, EquipmentM / Player, ShopMan | 통합: 목록 등록 해제/lock 재표시 점검 필요 | 직접 UI 회귀 없음 |
| `Assets/Scripts/Main/LoginCheck.cs` | LoginCheck / ILoginState / SetCharacterState / CheckTutorialState | DataM, GameM, ResourceM /  | UI: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/DataManager.cs` | DataManager | ResourceM, ServerM / Player | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/EquipmentManager.cs` | EquipmentManager |  / Player, PlayerUICanvas, ShopMan | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/GameManager.cs` | GameManager | PopupM, SceneM / CameraManage, JoyStick, Player, PlayerUICanvas | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/GoogleAdMobManager.cs` | GoogleAdMobManager / PendingCallback | DataM, Instance, SoundM / BuffingMan, Managers, Player, PlayerUICanvas | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/IAPManager.cs` | IAPStatus / IAPManager | DataM, Instance / Managers, ShopMan | SDK: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/IapConsentDefaults.cs` | IapConsentDefaults |  /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/Managers.cs` | Managers |  /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/PlayerDataSyncPolicy.cs` | PlayerDataSyncPolicy / PlayerDataChanges |  /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/PoolManager.cs` | GO/UI 풀 소유·대여·회수 | Instance / Managers, MonsterGenerator | 통합: 9044b6f: 대여 중 객체까지 종료, 외부 parent 보존 | RevivalPoolTests; Unity 대기 |
| `Assets/Scripts/Managers/PopupManager.cs` | PopupManager | GameM, GoogleAdMobM, SoundM, UpdateM / Player | UI: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/ResourceManager.cs` | ResourceManager |  /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/SceneManager.cs` | SceneManager | DataM, PopupM, ServerM, SoundM /  | UI: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/ServerManager.cs` | ServerManager / Operation | DataM /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/SoundManager.cs` | SoundManager |  /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/Sub/PoolObject.cs` | 풀 대상 marker |  /  | 통합: 유지: 타입 자체가 풀 계약 | RevivalPoolTests |
| `Assets/Scripts/Managers/Sub/PopupObject.cs` | PopupObject / CheckType | PopupM /  | UI: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/Sub/RuntimeInitialize.cs` | RuntimeInitialize |  /  | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/TapjoyManager.cs` | TapjoyManager / customEditor | PopupM / Btn_Setting | 서비스: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Managers/UpdateManager.cs` | 공통 프레임 이벤트 |  /  | 통합: UniRx EveryUpdate는 disable 후에도 계속됨; 호출자 pause 의도 확인 | publisher 소유권 회귀 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWar.cs` | FogOfWar |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarData.cs` | FogOfWarData |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarEntity.cs` | FogOfWarEntity / BoundsSource |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarGrid.cs` | FogOfWarGrid |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarMouseVision.cs` | FogOfWarMouseVision |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarRenderer.cs` | FogOfWarRenderer |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarUnitGroup.cs` | FogOfWarUnitGroup |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarUnitVision.cs` | FogOfWarUnitVision |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/MiniMap/FogOfWar/FogOfWarVisionBase.cs` | FogOfWarVisionBase |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/MiniMap/FogOfWar/LineOfSight.cs` | LineOfSight |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/MiniMap/FogOfWar/SliderValueToText.cs` | SliderValueToText |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/MiniMap/MiniMap.cs` | 지도 아이콘·드래그·일시정지 | DataM, Instance, UpdateM / JoyStick, Managers, Player, PlayerUICanvas | 통합: 캡처 publisher 해제 및 pause 종료 후속 | 직접 회귀 없음 |
| `Assets/Scripts/MiniMap/MiniMapCanvas.cs` | 지도 닫기 중계 |  / MiniMap | 통합: 파괴 순서와 현재 singleton 재조회 후속 | 직접 회귀 없음 |
| `Assets/Scripts/NextScene/NextScene.cs` | NextScene | SceneM /  | UI: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/Purchasing/NoAdsPurchaseFulfillment.cs` | PurchaseDeliveryState / PurchaseFulfillmentResult / NoAdsPurchaseFulfillment |  /  | SDK: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/RevivalAdHarness.cs` | RevivalAdHarness | Instance / Managers | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/RevivalAdReceiptOwner.cs` | RevivalAdReceiptOwner |  /  | 통합: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/SetCharacter/CanvasSelectCharacter.cs` | CanvasSelectCharacter | DataM, PopupM, SceneM, ServerM, SoundM /  | UI: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/UI/JoyStick.cs` | JoyStick / Mode |  / Player | UI: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/UI/Loading.cs` | Loading |  /  | UI: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/UI/PlayerUICanvas.cs` | PlayerUICanvas | DataM, GameM, GoogleAdMobM, PopupM, SoundM, UpdateM / MiniMap, Player | UI: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
| `Assets/Scripts/UI/TMP_Damage.cs` | TMP_Damage | PoolM /  | UI: 검토 대기: 담당 파일별 보고와 독립 검토 필요 | 검증 연결 대기 |
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
