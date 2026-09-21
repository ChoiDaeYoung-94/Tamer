# PlayerPrefs 진행도 Cloud 확장 조사와 미승인 제안

2026-09-21 후속: [계정 검증 코어와 서버 트랜잭션 경계](inventory-account-core.ko.md)에서 실제 SDK의 조건부 갱신과 Gold 원자성 공백을 조사했다. 아래 UserData 키·JSON revision은 과거의 미승인 제안이며, 서버 경쟁쓰기 보장이나 확정된 저장 형식으로 사용하지 않는다.

## 코드에서 확인한 사실

기준은 `8fc8d91d9a0e74c71b59c084a852bb98ca212701`이다. 이번 Cloud 로그인 변경은 아래 데이터를 서버화하지 않는다.

| 현재 키 | 저장 타입/의미 | 읽기·쓰기 근거 |
|---|---|---|
| PlayerPrefs `AllyMonsters` | 쉼표로 구분한 string, 몬스터 도감 | Player.InitPrefs / SavePrefs / NotifyPlayerOfDeath |
| PlayerPrefs `playerEquippedItems` | 쉼표로 구분한 string, 착용 장비 | Player.InitPrefs / SavePrefs / RemovePrefs, EquipmentManager.CheckSlotAndEquip |
| PlayerPrefs `LocalItem` | 쉼표로 구분한 string, 보유 아이템 | ShopMan.Init / SaveItem |

Player는 빈 항목을 제외해 CSV를 목록으로 읽고, 장비 변경 시 같은 카테고리의 기존 장비를 제거한다.
아이템 이름은 ItemsData의 SimpleSword/MasterSword/SimpleShield/MasterShield이며 EquipmentManager가 Sword/Shield 카테고리를 정의한다.
도감의 이름은 MonstersData와 대조할 수 있다. 서버의 `AllyMonsters`는 **동행 몬스터 목록**이므로 도감과 합치면 안 된다.

세 PlayerPrefs 키에는 계정별 namespace, owner, revision 또는 생성 시점의 인증 증거가 없다.
DataManager의 `__TamerAccountOwner`는 JSON 파일의 소유자를 확인할 뿐 세 PlayerPrefs 키를 묶지 않는다.
Login의 `AD_CustomId`, `AD_LastGpgsId`, `AD_LoginMode`는 로그인 선택 캐시이며, 과거 PlayerPrefs 값이 현재 계정의 것임을 입증하지 못한다.

## 미승인 설계 제안

신규 서버 키 **`GameplayInventoryV1`** 하나에 아래 JSON 객체를 문자열로 저장하는 방안을 제안한다.
세 종류를 하나의 값으로 묶어 보유/착용 정보가 서로 다른 갱신 단계로 남는 것을 피한다.

```json
{
  "version": 1,
  "revision": "0",
  "collection": [],
  "ownedItems": [],
  "equipped": { "Sword": null, "Shield": null }
}
```

- collection/ownedItems: 중복 없는 string 배열. 실제 데이터 목록의 이름만 허용한다.
- equipped: 슬롯별 string 또는 null. 장착값은 ownedItems에 있어야 하고 카테고리와 맞아야 한다.
- revision: 정수 문자열. 기기 시각을 충돌 판정 근거로 쓰지 않는다.
- 현재 클라이언트 queue는 계정·세션별 요청 직렬화를 제공하지만, 여러 기기의 경쟁 쓰기를 막는 서버 측 조건부 갱신을 제공하지 않는다. 해당 정책은 별도 구현·검증이 필요하다.

기존 PlayerPrefs는 귀속 증거가 없으므로 자동 업로드·현재 계정 귀속·자동 합집합을 하지 않는다.
신규 별도 테스트 계정의 합성 데이터부터 검증하고, legacy 이행은 별도로 승인된 귀속 증거와 사용자의 명시적 선택이 있을 때만 설계한다.
서버에 값이 있으면 서버 값을 기준으로 하며, 소유자/버전/내용이 충돌하면 덮어쓰지 않고 중단한다.
보유 아이템은 단순 문자열 합집합으로 소유권을 확대하지 않는다.

이행 완료 표시는 서버 저장 및 재읽기 확인 후에만 남기고, 그 전 실패/재시작은 기존 pending journal처럼 재시도 가능한 상태로 관리한다.
원본 PlayerPrefs는 보존한다. 별도 로컬 이행 백업도 계정 귀속과 검증 기록이 필요하다.

## 결정과 검증이 필요한 사항

신규 키/타입, 허용할 귀속 증거, legacy 사용자의 선택 방식, 서버 측 충돌 제어, 완료 후 원본 보존 정책은 아직 승인되지 않았다.
이번 PR은 Cloud 전용 로그인과 기존 서버 8키만 다룬다. 위 키 생성·운영 마이그레이션·PlayerPrefs 수정·계정 변경을 실행하지 않았다.
