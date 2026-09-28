# 계정 인벤토리의 원본 Player 적용과 Android 재시작 복원

2026-09-28, 원본 Main 씬에서 보유 아이템과 장비 슬롯을 저장하고 **앱 프로세스를 종료한 뒤 새 프로세스에서 복원**했다. 실제 계정 인증이나 서버 쓰기 없이 기존 GameplayHarness와 메모리 서버를 재사용한 좁은 오프라인 검증이다.

## 기존 증거와 확인 범위

[클라우드 8키 왕복](game-save-cloud-device.ko.md)은 실제 시험 계정의 저장·읽기 검증이고, [Player 복원](playerrestore-offline.ko.md)은 Woman/능력치/동료/HUD 적용 검증이다. 당시 Player 복원 APK 소스 `169ab82`에는 이후 계정별 인벤토리 코드 `c87acf6`이 포함되지 않았다. 기존 하네스의 매 부팅 GUID 저장 경로도 프로세스 재시작 복원 증거로 사용할 수 없다.

[세션 콜백 기기 검증](session-gameplay-device.ko.md)의 소스 `67a0be6`에는 계정별 인벤토리 코드가 포함되지만, 당시 결과와 보존한 진행 JSON은 Gold/동료 경계만 입증한다. 보유 아이템·장비 파일 또는 재시작 복원 결과는 없었다. 해당 소스와 이번 기준 main 사이 Player/ShopMan/GameplayHarness/GameplayIsolation은 동일했고 DataManager 차이는 삭제 정리 경로였다. 옛 APK 결과를 최신 코드의 검증으로 바꾸어 표현하지 않았다.

이번에는 기존 하네스에 `inventoryrestore` 빌드 모드만 추가했다. 새 씬·새 인증 계정·외부 서비스는 추가하지 않았다. 원본 `ShopMan.SaveItem`과 `EquipmentManager.Equip`으로 아이템 보유와 장비 교체를 실행한다. **상점 구매 버튼, Gold 차감 또는 실제 상품 구매 검증은 아니다.**

## 격리와 결과

- checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`, branch `codex/inventory-device-restart`, 기준 main `a6e52139b9490a615c100b7609794216a8479734`. 실행 중 조회한 main `81a47b3`은 문서 4개만 달랐다.
- 최종 빌드/기기 소스 `aa6bb9157225309fec74969a2b0715a99d45fb76`, 빌드 직전 clean 및 해당 checkout Editor0. 일반 제품 런타임 변경 없이 빌드 한정 `TAMER_INVENTORY_RESTORE`/기존 격리 define을 사용한다.
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android ARM64/IL2CPP/min24/target36, 회사 SM-N986N/Android13(API33).
- 별도 앱 `com.AeDeong.MonsterTamer.revival.inventoryrestore`, debug 서명, INTERNET/ACCESS_NETWORK_STATE/BILLING/AD_ID 및 MobileAdsInitProvider 제거, 백업/기기 전송 제외 18개 규칙 적용. APK 정적 검사 통과.
- 이 앱만 고정 저장 경로 `<persistentDataPath>/RevivalInventoryRestore/PlayerData.json`을 사용한다. 합성 owner `revival-offline-gameplay`만 사용하며 기존 세 PlayerPrefs가 발견되면 읽기·삭제 없이 중단한다. 다른 하네스의 저장 경로는 변경하지 않았다.
- 첫 실행의 새 앱 연령 창은 **응답하지 않음**을 선택했다. 개인 연령이나 기존 앱의 설정을 변경하지 않았다.

| 경계 | 결과 |
| --- | --- |
| 원본 저장 메서드를 통한 보유·장비 교체 | `phase=write` 통과 |
| 보유 아이템 | SimpleSword, MasterSword, SimpleShield 3개 |
| 검 슬롯 교체 및 방패 슬롯 | MasterSword + SimpleShield, 실제 모델 2개 활성 |
| 실제 Player 능력치 | HP150, Power60, AttackSpeed1.5, MoveSpeed3 |
| Gold / 도감 | Gold1000 유지 / 빈 도감 유지 |
| 프로세스 종료 → 다른 PID로 부팅 | `phase=restart` 통과, 추가 아이템 부여 없이 자동 복원 |
| 디스크 인벤토리 전후 | owner/버전/도감/보유/슬롯 동일, 세션 binding만 새 세션으로 갱신 |
| 검증 구간 오류 / 서비스 차단 | 오류0, 광고 요청 불가 및 IAP unavailable |

두 성공 프로세스 모두 하네스 `Awake` 구독 **이전**에 Development PlayerConnection 소켓 blocking 2건과 multicast 설정 실패 1건(E Unity)이 있었다. 인터넷 권한을 제거한 개발 플레이어의 초기 진단이며 전체 앱 로그 오류0으로 주장하지 않는다. 검증 구간에는 추가 오류나 예외가 없었다.

## 시도 이력과 증거

빌드는 2회 모두 성공했다. 첫 소스 `88814c5`의 기기 실행은 검증 호출을 연령 확인 전용 조건문 안에 둔 **하네스 작성 오류로 기대한 인벤토리 검사 자체가 실행되지 않았다**. 게임 결함이나 검증 통과로 기록하지 않는다. 당시 파일/로그/화면/APK를 보존한 뒤 호출을 Main 준비 이후로 수정했다. 기존 시험 namespace의 도감·보유·슬롯이 비어 있음을 확인했으며 데이터를 초기화하지 않았다. 두 번째 APK에서 write와 필수 restart 단계가 모두 통과했다. 세 번째 빌드나 동일 조건 재시도는 하지 않았다.

기존 APK verifier Python 시험은 **5/5 통과**했다. 최초 `unittest` 모듈 호출은 import 경로 오류 1건으로 시험을 실행하지 못했고, 파일 직접 호출로 통과했다. 보조 소스 대조 스크립트도 최초 BOM 미처리 오류 1건 후 UTF-8 BOM을 처리하여 정적 대조를 완료했다. 이 두 실행 준비 오류는 기기 인벤토리 결과와 구분한다. 기존 전체 Editor 시험이나 클라우드 8키 시험은 반복하지 않았다.

이번 빌드에 별도 메모리 제한을 적용하지 않았다. 실제 생성 Gradle 설정은 `-Xmx4096M`, 첫 빌드 관측 Unity working set 약2557MB/시스템 가용 RAM 약21247MB였다. 과거 다른 빌드의 제한값이나 성공을 이번 실행 조건으로 주장하지 않는다.

최종 APK **105,583,059 bytes**, SHA-256 `0a1fa58a5b9de69a268c4a86c6fd667487a00d3e5a766b65d8114870cf4c36db`; 설치된 base.apk도 동일했다. 비공개 증거는 `Logs/revival/inventoryrestore-*`와 로컬 `Build/revival`에 보존한다.

| 비공개 증거 | SHA-256 |
| --- | --- |
| 저장 단계 원시 로그 | `f86c959e82964efc665234e82534d3b12d97753deee8acb0f35321bd787a6285` |
| 재시작 단계 원시 로그 | `cb791482d441974a12380304b0b44a4b929526c60cca0b93cca4d313d35ca908` |
| 저장 후 인벤토리 파일 | `c34bbd66ae9388d83a1b2d2a07dfc8c1f88f7f1264d1592b3d8d270151e1b37e` |
| 재시작 후 인벤토리 파일 | `f653a4293ab2db0a3bd3f430d8770a975c5b56764d98b4292b7e0aeaa30c0e70` |
| 저장 단계 화면 | `2993229397bcf739f345f980b2701156f8c5bc4eb2cdbe57b85c6bed774f7ca4` |
| 재시작 단계 화면 | `d720926690446df841ec45539b4063c55aa1e383818e41080a4c8f8892340bca` |

해당 Editor 종료와 설정 스냅샷 복원, URP/Graphics의 줄바꿈만 발생한 자동 변경 복원 후 clean을 확인했다. 시험 앱을 종료하고 외부 files 전체·앱 내부 tar·설치 APK를 비공개 보존한 뒤, 승인된 정확한 새 패키지만 제거했다. 기존 Tamer 패키지 목록은 시험 전과 동일하며 기존 앱 데이터는 변경하지 않았다. 작업 중 만든 기기 화면 파일도 로컬 증거 보존 후 정리했다. Editor/기기 독점 사용은 반환했다.

실제 상점 UI 구매와 Gold 차감, 비어 있지 않은 도감의 전투 획득·재시작 복원, 실제 인증/클라우드와 원본 Player의 하나의 통합 흐름, 다른 기종·출시 AAB·#91 광고/스토어는 이 결과에 포함되지 않는다. 로컬 인벤토리를 원격 복원이나 결제 전체 원자성의 보증으로 확대하지 않는다.
