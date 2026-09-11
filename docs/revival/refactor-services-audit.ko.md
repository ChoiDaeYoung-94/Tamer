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
| Managers/EquipmentManager.cs | Player 장비 목록·GameObject mapping | Init의 Dictionary.Add는 Player 재생성 시 중복 키 가능. 현재 Player.Awake 의존. 수명 PR에서 장비/PlayerPrefs 의미를 바꾸지 않고 [#118](https://github.com/ChoiDaeYoung-94/Tamer/issues/118)로 분리 | 전용 장비 회귀 없음. 재생성/unknown 장비·슬롯 교체를 별도 fixture로 검증 필요 |
| Managers/GameManager.cs | Main/Game 전환·Player/카메라/UI 조정 | 강한 씬 객체 의존 확인. 중복 전환 guard는 SceneManager 소유 UI 담당이 해당 메서드에 추가. 나머지 게임 규칙은 변경하지 않음 | UI 담당 전환 회귀, 실제 씬 객체 조합은 통합 책임 |
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
Unity 실행 결과와 exact source는 후속 검증 기록에 연결한다. 이번 변경으로 서버 영수증 검증,
계정 삭제 서비스, 데이터 보관/수집 정책 또는 운영 Public 데이터 이행이 완료되지는 않는다.
