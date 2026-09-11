# 서비스 초기화·종료 감사 (#117 / #90)

기준 main `f68fb61`, 작업 브랜치 `codex/manager-lifecycle-refactor`.
계정·저장·No Ads 키, 직렬화 필드와 GUID를 보존하는 수명 리팩토링이다.
운영 로그인·구매·데이터 변경·SDK/정책 변경·CI 실행을 포함하지 않는다.

## 파일별 책임과 판단

| 파일 (Assets/Scripts 기준) | 책임·의존성 | 감사 결과·처리 | 회귀 근거/제한 |
| --- | --- | --- | --- |
| Managers/Managers.cs | 서비스 조립, static 접근, persistent owner | 중복 Init이 owner를 교체하던 문제 수정. 단일 소유권·멱등 초기화/종료, 초기화 실패 시 owner 해제, 종료 시 소유 서비스 정리. 이름/직렬화 참조 유지 | 신규 preview 테스트: 중복/종료/재획득 차단/서버 늦은 응답. 실제 로그인 씬 재진입은 별도 |
| Managers/DataManager.cs | 로컬 영구 저장·계정 귀속·cloud patch, Resource/Server/Player | 반복 초기화가 진행 중인 메모리 상태를 다시 읽지 않도록 멱등화. Shutdown에서 timer·서버 요청 중지, 종료 후 쓰기 거부. 파일/backup/미확인 구매 권한을 삭제하지 않음 | 기존 DataSync 42 + 종료 후 저장 문자열/No Ads 보존 회귀 |
| Managers/ServerManager.cs | 계정별 직렬 요청·bounded retry·timeout | production 콜백을 생성 시 DataManager에 바인딩. Dispose가 요청 generation·queue·timer 수명을 닫음. 종료 후 새 요청/늦은 성공/재시도 무효 | 기존 ServerRequest 14 + 종료/재시도/ACK 회귀. 이미 서버가 수락한 쓰기는 취소로 되돌릴 수 없음 |
| Login/Login.cs | GPGS/기기/custom 로그인·프로필·씬 진입, Data/Server/UI | OnDestroy가 새 singleton 계정을 중지하지 않도록 Start의 Dataowner 캡처. await 서버 대기·취소도 기존 서버 참조 사용. 인증 방식/식별자/캐시 규칙은 유지 | 기존 LoginContinuity 56, 새 owner 교체 후 파괴 회귀. 실제 인증/기기 계정 복원 별도 |
| Managers/PlayerDataSyncPolicy.cs | 순수 merge·값 검증·키별 revision | 수명/외부 구독 없음. 기존 pending revision·No Ads 합집합 정책이 맞아 변경하지 않음 | 기존 DataSync/SaveQueue 회귀 |
| Managers/ResourceManager.cs | Resources load/instantiate·오류 보고 | 소유 loop·구독·mutable cache 없음. DI 계층 추가 없이 유지 | 기존 smoke/GUID 검증; 개별 resource 누락은 asset 검증 책임 |
| Managers/SoundManager.cs | AudioSource·mixer·설정, PlayerPrefs | coroutine/외부 구독 없음. 광고 재개 callback은 소유 객체 유효성·재생 revision을 검사. 변경하지 않음 | 기존 광고 오디오 동작 계약; 모든 실제 clip 재생은 별도 |
| Managers/EquipmentManager.cs | Player 장비 목록·GameObject mapping | [#118](https://github.com/ChoiDaeYoung-94/Tamer/issues/118)의 반복 Dictionary.Add 예외 수정. dictionary 참조는 유지하면서 현재 Player의 장비로 재바인딩. 기존 장착 목록/저장/효과 불변 | 신규 preview 회귀 2개: 반복 Init·Player 교체·장착 목록/이전 객체 보존·owner 없음. unknown 장비·슬롯 교체 규칙은 변경하지 않음 |
| Managers/GameManager.cs | Main/Game 전환·Player/카메라/UI 조정 | UI 담당의 IsTransitioning 계약을 사용해 GameOverGoLobby도 Player 초기화 전에 중복 전환을 차단. SwitchMainOrGameScene guard는 UI 담당 변경 | 전환 중 Player/UI 접근 없이 반환하는 신규 회귀. 실제 씬 객체 조합은 통합 책임 |
| Managers/IAPManager.cs | store 이벤트·pending 주문·durable grant→confirm | IDisposable·취소 token·SDK 이벤트 해제·disposed guard 존재. 이번에는 owner가 기존 Dispose를 호출. 실제 파일 소유는 SDK 담당 | 기존 IAP 30 및 SDK 담당 후속 검증. 서버 영수증 검증은 별도 |
| Managers/IapConsentDefaults.cs | 초기 동의 기본값 | 정적 순수 변환 + 시작 hook, 수명 자원 없음. 정책 변경 없이 유지 | 기존 IAP 설정 테스트 |
| Managers/TapjoyManager.cs | 과거 Tapjoy 참고 코드 | 파일 전체 주석으로 실행되지 않음. 재활성화하거나 제거하지 않음 | 실행 코드 없음; 구형 광고 통합 완료로 간주하지 않음 |
| Managers/Sub/RuntimeInitialize.cs | Editor 시작 씬/개발 console | loop·구독 없음. smoke/ad harness 제외 규칙 유지, SDK 초기화로 확장하지 않음 | 기존 격리 시작 씬 빌드 근거. console 참조 없는 개발 오브젝트 구성은 별도 검사 필요 |
| Managers/PoolManager.cs, Sub/PoolObject.cs | pool/root/대여 객체 | 통합 담당 소유. Dispose 계약을 읽기 리뷰하고 Managers 종료에 연결 | 통합 담당 Pool 회귀. 별도 root/외부 parent 보존 확인 |
| Managers/UpdateManager.cs | UniRx 매프레임 publisher | 통합 담당 소유. AddTo(this)로 자체 구독 해제, subscriber는 캡처 후 해제해야 함 | Player는 통합, Popup은 UI 담당이 해제 책임 |
| Managers/GoogleAdMobManager.cs | 광고 session·callback·cleanup | 통합/광고 담당 소유. 수명 PR에서 SDK 초기화/요청 정책 변경 없음 | 기존 광고 회귀 및 광고 담당 harness |
| Managers/PopupManager.cs, Sub/PopupObject.cs, SceneManager.cs | UI·씬 전환 | UI 담당 소유. Popup의 UpdateM 구독 해제 누락 발견·전달, 직접 수정하지 않음 | UI 담당 회귀/PR |

이 디렉터리에 InputManager/MonsterManager.cs는 없다. 파일을 추정해 새 서비스를 만들지 않았다.
전체 프로젝트 감사표와 게임플레이 범위는 통합 담당 문서에서 연결한다.

## 테스트 파일별 감사

2026-09-11 main `41f1dc0be79722ab7de2ae9a860e34b60941263b`에서 아래 6개 파일의 전체 본문과
fixture 생성·정리·호출 경로를 읽었다. 이번 후속 감사는 문서만 추가하며 테스트 코드를 변경하지 않는다.
`Revival.EditorTests.asmdef`는 Editor/TestAssemblies 범위이고, 런타임 Assembly-CSharp 타입은 reflection으로 호출한다.

| 파일 (Assets/Tests/Editor 기준) | 책임·검증하는 계약 | 격리·정리 | 미수정 이유와 검증 한계 |
| --- | --- | --- | --- |
| RevivalDataSyncTests.cs (43) | 서버 우선 merge, pending revision, No Ads 토큰 합집합, 계정 귀속, backup 원문 보존, 잘못된 값/레코드 거부, 영속 저장 실패 rollback, Shutdown 이후 쓰기 차단 | GUID로 만든 OS 임시 폴더에 파일 경로를 주입하고 합성 UserData record를 사용. HideAndDontSave DataManager는 InitializeData를 호출하지 않으며 현재 Awake/Start/OnEnable 초기화도 없음. using Dispose가 객체와 자기 임시 폴더를 정리 | 메모리뿐 아니라 저장 원문·backup·pending·owner를 비교해 계약을 확인하므로 유지. atomic replacement 실패는 WindowsEditor에서만 실행하며 다른 플랫폼은 Ignore. Android 파일시스템·실제 앱 종료 중 전원 손실까지 검증하지 않음 |
| RevivalLoginContinuityTests.cs (56) | 기존 GPGS ID와 device/custom 선택 보존, 모호한 레거시 선택 거부, 신규 등록 조건, 닉네임/로그인 gate, 늦은·중복 callback과 선행 취소 | 순수 정책/gate 객체와 합성 PlayFabError만 생성. 취소 CTS는 using으로 정리. Login의 static predicate/응답 분류만 호출하고 로그인·PlayerPrefs·씬 생성 없음 | 계정 선택 경계와 gate 상태 전이를 유지할 근거가 있어 변경하지 않음. 실제 GPGS 인증, PlayFab 응답, 재설치 후 계정 복구를 대신하지 않음 |
| RevivalSaveQueueTests.cs (3) | 실제 DataManager+ServerManager 조합에서 100→90→100 revision과 이전 ACK/후속 조회 경합, 마지막 쓰기 실패·재시도, 쓰기 성공 후 조회 실패 | 고유 임시 저장 폴더와 메모리 cloud, 6개 delegate 전송/시계 주입. callback을 순서대로 직접 해제. Dispose가 요청 취소·Data 객체 파괴·자기 폴더 삭제 | 값만 같고 revision이 다른 손실을 저장 파일까지 검사하는 통합 회귀라 유지. 가짜 scheduler는 동작하지 않으므로 timeout/backoff 책임은 ServerRequestTests에 둠. 실제 서버/시계/Managers singleton 없음 |
| RevivalServerRequestTests.cs (16) | 요청 복사·10키 청크·직렬화, bounded retry/timeout, account/generation 차단, Dispose, ACK 순서·단일 적용, null/적용 실패, Private 요청 생성 | 모든 전송 callback과 시계가 fixture 내부 목록. Advance가 가짜 시간을 진행하며 무한 실행 상한 검사. production CreateWriteRequest도 합성 인증 context로 DTO만 만들고 전송하지 않음 | 전달값·호출 수·결과 상태를 확인해 회귀 가치가 있음. 일반 harness는 외부 timer/handle을 만들지 않으며 fixture 종료 후 참조 해제; Dispose 동작은 별도 두 사례가 검사. DeleteData 사례는 null 키 제거 payload 계약일 뿐 실제 삭제/계정 삭제 시험 아님 |
| RevivalManagerLifecycleTests.cs (6) | 중복 owner 차단, 멱등 Shutdown과 static 해제, Pool/IAP 종료, 늦은 서버 응답, 이전 Login의 새 Data 취소 방지, production Data 바인딩, GameOver 전환 중 조기 반환 | 비활성 PreviewScene의 Managers/Data/Login으로 Init/Start를 건너뜀. 이전 singleton을 보관하고 TearDown finally에서 복원. 생산 Server 생성자는 delegate 바인딩만 수행하며 적용 callback을 update=false로 직접 호출; 요청/파일 저장 없음 | owner 교체와 종료 경계를 확인해 유지. 실제 Awake→Init→Start 순서, 초기화 실패 시 Destroy 타이밍, 전체 Main/Game 왕복은 검증하지 않음. GameOver 사례는 전환 중 접근 차단 검사이며 Player reset의 정상 경로 검사가 아님 |
| RevivalEquipmentLifecycleTests.cs (2) | 반복 Init과 Player 교체 후 동일 dictionary 참조·새 객체 mapping, 기존 장착 문자열/목록·이전 객체 상태 보존, Player 없음 시 기존 mapping 보존 | 비활성 PreviewScene Player와 합성 장비 객체. Awake/PlayerPrefs 미실행, 기존 Player singleton 보관. scene 정리 후 finally에서 singleton 복원 | 장비 효과/저장 규칙을 건드리지 않는 재바인딩 회귀라 유지. 실제 prefab 직렬화 연결, 능력치 효과, 미지의 장비/슬롯 교체 정책은 범위 밖 |

확정된 추가 결함은 없었다. 사례 수는 서비스 검증 XML의 126개 합계이며, 실행 전체 결과와 구별한다.
위 파일은 통합 테스트 소스 `300d427e4c557fd3cc4cda347b255a22a9a53bc7`와 감사 기준 사이에 변경이 없음을
git diff로 확인했다. 통합 담당의 [317/317 검증 기록](gameplay-followup-validation.json)을 재사용하며
이번 문서 감사에서 Unity를 다시 실행하지 않았다. 이전 서비스 자체 실행은 아래 283/283 기록이다.
반영 범위가 다른 두 실행의 전체 수를 합산하지 않는다. 운영 인증·저장·구매 또는 Android 빌드 성공을 추가 주장하지 않는다.

## 소유권과 호환 경계

Managers가 없는 시점의 accessor는 null을 반환한다. 기존 이름·반환 타입은 같고,
서비스가 필요한 실행 경로는 유효한 owner에서 호출해야 한다. 서버 기본 생성자도 생성 시점의
DataManager를 캡처하므로 초기화 전 사용은 실패하며, Managers는 명시적 DataManager 생성자를 쓴다.
테스트용 6개 delegate 생성자는 그대로 유지한다.

중복 Managers가 같은 GameObject에 붙은 경우 그 component만 제거하고, 다른 GameObject의
중복이면 해당 객체를 비활성화한 뒤 제거한다. 중복의 종료가 정식 owner의 서비스에 접근하지 않는다.
초기화는 Data → Pool → Popup → Ads 순서, Sound는 Start 시점을 유지한다.
종료는 저장을 더 만들지 않고 background 작업과 콜백을 멈춘다. 서비스 dispose는 멱등이며
한 cleanup의 예외가 다른 서비스의 정리를 건너뛰지 않게 처리한다.

## 검증

합성 transport·clock, 임시 파일과 비활성 preview component만 사용한다.
최종 소스 `b0d3bd2bba1a582998fdc0ecbfeb514030118015`에서 Unity 6000.0.81f1 / CLI 1.0.0-beta.8
Revival EditMode **283/283 통과**, 신규 서비스 회귀 11개를 포함한다.
첫 실행의 UI fixture 2개는 eager 서버 생성 가정 때문에 실패하여, 운영 요청 없이 명시적으로
서버를 주입하도록 수정했다. 재실행에서 모두 통과했다. 에셋 4,561개 검증 및 GUID 132개/미해결 0개,
자기 checkout Editor 0개와 PlayerSettings 복원을 확인했다.
XML 해시·실행 시각·제한은 [검증 기록](refactor-services-validation.json)에 있다.
이번 변경으로 서버 영수증 검증,
계정 삭제 서비스, 데이터 보관/수집 정책 또는 운영 Public 데이터 이행이 완료되지는 않는다.
