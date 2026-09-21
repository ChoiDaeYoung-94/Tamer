# 도감·보유·착용의 계정 검증 코어와 서버 쓰기 경계

2026-09-21. 기존 [미승인 제안](playerprefs-cloud-design.ko.md)의 후속 조사다. 아직 저장 형식·서버 키·이관 방식을 확정하거나 실행 경로를 연결하지 않았다.

## 이번 구현

`PlayerInventorySnapshot.Validate`는 인증된 계정 ID와 저장 owner가 정확히 일치하는 입력만 받는다. owner 없음, 다른 계정, 알 수 없는 이름, 중복 목록, 미보유 장비, 다른 슬롯 장비, 누락된 슬롯을 거부한다. 원문을 임의 정규화하거나 합집합으로 복구하지 않는다. 검증된 목록·슬롯은 복사 후 읽기 전용으로 제공하여 호출자가 원래 컬렉션을 바꾸더라도 검사 결과가 변하지 않는다.

도감/아이템 카탈로그는 호출자가 주입하며 슬롯은 현재 EquipmentManager의 Sword/Shield 두 종류다. 이 코어는 계정 인증이나 아이템 획득의 정당성을 증명하지 않는다. 신뢰된 인증·서버 경계에서 검증한 owner와 카탈로그를 전달해야 한다. 같은 ID가 여러 title에서 존재할 수 있으므로 실제 저장 계층은 title과 계정을 함께 격리해야 한다.

PlayerPrefs, 파일, Unity 객체, SDK, 기존 DataManager·Player·ShopMan 호출 경로에 연결하지 않았다. 새 JSON 스키마나 로컬 경로, 서버 키, 임의 revision도 추가하지 않았다. GooglePlay/No Ads 및 기존 서버 8키의 초기화는 범위에 없다.

## 기존 SDK와 API의 조건부 갱신

| 저장 API | 확인한 계약 | 적용 한계 |
| --- | --- | --- |
| Client.UpdateUserData | 설치 SDK `PlayFabClientModels.UpdateUserDataRequest`에는 Data/KeysToRemove/Permission/CustomTags만 존재. 결과 DataVersion은 조회 변경 감지용 | 예상 버전 비교 인자가 없다. JSON revision 증가나 클라이언트 큐 직렬화로 다른 기기 쓰기를 차단할 수 없다 |
| Data.GetObjects / SetObjects | 설치 SDK에 GetObjectsResponse.ProfileVersion과 SetObjectsRequest.ExpectedProfileVersion 존재. 서버는 이전 profile version과 다르면 갱신을 거부 | 기존 UserData와 별도 Entity Objects 저장소. Entity ID/type와 EntityToken, 실제 title의 접근권한 검증이 필요하다. SDK 존재는 운영 허용 증거가 아니다 |

공식 [UpdateUserData](https://learn.microsoft.com/en-us/rest/api/playfab/client/player-data-management/update-user-data?view=playfab-rest), [GetObjects](https://learn.microsoft.com/en-us/rest/api/playfab/data/object/get-objects?view=playfab-rest), [SetObjects](https://learn.microsoft.com/en-us/rest/api/playfab/data/object/set-objects?view=playfab-rest)를 2026-09-21 확인했다. SetObjects의 조건부 갱신은 서버가 제공한 ProfileVersion을 사용하며, EntityProfileVersionMismatch 또는 ConcurrentEditError는 재조회·재평가 대상이다. 충돌 뒤 최신 버전만 붙여 과거 전체 값을 다시 쓰는 방식은 사용하지 않는다. 파일/통계 등 같은 entity profile의 다른 변경도 충돌 원인이 될 수 있다.

현재 ServerManager는 UserData용 세션 큐와 HTTP 상태 중심 실패 콜백을 사용한다. Entity 경로를 추가한다면 인증 컨텍스트에 대응하는 entity를 고정하고 계정 전환/늦은 응답을 차단해야 하며, PlayFab 오류 코드를 보존해 경쟁쓰기와 일시 실패를 구분해야 한다. 현재 큐를 변경 없이 재사용할 수 있다고 가정하지 않는다.

## 실제 게임 동작의 트랜잭션 경계

| 동작 | 현재 순서 | 분리 저장 시 문제 |
| --- | --- | --- |
| 장비 구매 | ShopMan.CheckSuccessBuy → SaveItem(LocalItem) → Equip(착용 PlayerPrefs·능력치) → MinusGold(UserData Gold) | 아이템/착용만 남거나 Gold만 반영될 수 있다. 인벤토리 객체 CAS 하나로 UserData Gold까지 원자화되지 않는다 |
| 장비 교체 | EquipmentManager.CheckSlotAndEquip → 기존 착용 RemovePrefs → 새 착용 SavePrefs → 능력치 적용 | 이전 제거와 신규 추가 사이에 중단되면 중간 상태가 남는다. 슬롯 교체는 한 스냅샷 변경으로 검증하고 확정해야 한다 |
| 도감 기록·전투 보상 | Player.NotifyPlayerOfDeath → 도감 SavePrefs → Gold 증가/UpdateLocalData | 도감과 Gold의 저장 경계가 다르다. 서버가 전투 보상을 인정하는 증거·중복 처리 규칙은 별도 필요하다 |
| 동료 구매 | BuyAllyMonster → AddAllyMonster(서버 AllyMonsters) → MinusGold | 도감과 다른 동행 목록이다. 인벤토리 3종만 서버화해도 이 구매 경계는 해결되지 않는다 |

현재 `_purchasePending`은 한 실행 내 중복 클릭을 줄이는 장치이며, 재시작이나 다른 기기까지 아우르는 서버 거래 ID가 아니다. 불확실한 응답 이후 같은 구매가 재전송돼도 중복 지급·차감되지 않도록 서버가 거래 ID와 결과를 함께 기록해야 한다.

## 다음 연결의 대안

| 대안 | 가능한 범위 | 선행 결정/작업 |
| --- | --- | --- |
| 계정별 로컬 보존과 검증부터 연결 | 다른 계정으로 데이터가 섞이는 문제를 분리해서 해결 | legacy 정책, 계정 전환의 원자 저장·복구 규칙. Cloud 동기화/거래 보장은 별도다 |
| 인벤토리만 Entity Objects CAS로 동기화 | 동일 인벤토리의 경쟁쓰기 거부 | Gold와의 분산 저장 공백이 남아 실제 구매 연결의 완료안으로는 부족하다. 직접 클라이언트 지급을 서버 권위로 부르면 안 된다 |
| 신뢰된 서버 명령이 Gold·인벤토리·관련 동행 목록과 거래 결과를 한 기록에서 조건부 확정 | 구매·장착·보상의 일관성 및 재전송 처리의 설계 후보 | Gold/동행의 기존 8키 소비 경로 이행, 거래 보관 한도, 지급 검증, 접근정책, 서버 실행환경 필요. 기존 UserData는 독립 쓰기 원본으로 남기지 않고 이행/파생 조회 역할을 설계해야 한다 |

서버에서 UserData를 읽고 비교한 뒤 UpdateUserData를 호출하는 것만으로 CAS가 생기지는 않는다. 서버 함수끼리도 경쟁할 수 있으므로 조건부 쓰기 저장소 또는 해당 범위를 직렬화하는 신뢰된 트랜잭션 계층이 필요하다. 서버 배포·키 생성·권한 변경은 이번에 수행하지 않았다.

## 사용자에게 필요한 최소 결정

권장안은 소유자 없는 기존 PlayerPrefs 3종을 그대로 보존하고, 새 계정별 저장을 빈 도감·빈 보유 목록·미착용 상태로 시작하는 것이다. 대안은 사용자가 목록과 대상 계정을 확인하는 별도의 일회성 가져오기다. 후자는 귀속 증거와 서버 기존값 충돌 정책을 추가로 설계해야 하며 자동 합집합은 하지 않는다. 기존 서버 계정 삭제 사실은 로컬 목록의 소유권 증거가 아니다. 답변 전에는 어느 정책도 실제 경로에 적용하지 않는다.

## 검증 범위

합성 `RevivalInventorySnapshotTests`는 계정 경계·잘못된 목록/착용 관계·불변 복사를 대상으로 한다. 최초 실행 16/16 통과, 실패 0회·재시도 0회이며 test_status의 completed 결과를 확인했다. 콘솔 오류는 없었다.

- checkout: `C:/Users/pc_17/.codex/worktrees/7299/Tamer`
- 소스 커밋: `6aec8194eeba33bbbd3362fb1f1dad85d6b137d1` (기준 `5b2de27a3b024503b3aa6d80e93fa77421463e01` 위에서 검증한 동일 코드)
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`. Android는 실행하지 않았다. 기준 min SDK24/target SDK36/ARM64 설정 유지
- 비공개 결과: `Logs/revival/inventory-snapshot-tests.json`
- 결과 SHA-256: `1dd9b93c710a6036e11725b094920062907487a88af9abce9968f79f0ae9179b`
- 본인 Editor 종료/PID 없음 확인 후 시작 전 ProjectSettings 스냅샷 복원. 신규 C# 두 파일의 .meta는 Editor에서 생성했으며 기존 GUID 변경 없음

실제 카탈로그 연결, JSON 처리, 저장/재시작, Player 씬 적용, Entity 인증·권한·실제 서버 동시성·지급 보안·Gold 원자성은 아직 검증하지 않았다. APK를 생성하거나 기기 테스트를 반복하지 않았으므로 이번 변경의 APK SHA-256은 없다.
