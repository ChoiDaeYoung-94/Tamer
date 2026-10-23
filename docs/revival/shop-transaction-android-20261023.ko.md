# Gold 구매·중복 요청·중단 복구 — 2026-10-23

같은 설치의 로컬 거래를 별도 오프라인 Android 앱에서 검증했습니다. 원래 상점 UI의 동료 구매, 정상 장비 구매, 같은 intent 재호출, P(진행·Gold) 확정 직후와 I(아이템·장착) 저장 직후의 중단·새 프로세스 복구가 통과했습니다. 서버/다기기 거래 원자성이나 실제 Google Play 구매 검증은 아닙니다.

## 실제 관측

| 범위 | 중단/요청 전후 | 결과 |
| --- | --- | --- |
| 원래 상점 UI | Bat100G, 1030G/동료1/writes1 → 930G/동료2/writes2 | 준비 창·확인·구매 완료·결과 OK를 직접 조작. 결과 OK 후 값 유지, 게임·전체 오류0 |
| 동료 동일 intent | 격리 fixture20100G에서 Bat100G 구매 → 20000G/동료1/성공 콜백1 | 같은 intent는 거부, 추가 차감·동료·표시·진행 파일 변경 없음 |
| 장비 정상/완료 중복 | SimpleShield5000G → 15000G/HP150/표시1 | 같은 intent는 AlreadyCommittedCompleted. 추가 차감·능력치·표시·파일 변경 없음 |
| P 확정 직후 중단 | MasterSword15000G 거래의 진행 파일 교체 뒤 예외 주입. P=0G/Materializing, 합성 서버=15000G | RecoveryRequired, 준비 상태 해제, Player 재요청 거부. 데이터 삭제 없이 강제 종료 |
| P 이후 새 프로세스 | PID8163 → 8333 | 같은 거래 Complete/0G/보유2·장착2·모델 일치, HP150/Power60/AttackSpeed1.5/MoveSpeed3, 오류0 |
| I 저장 직후 중단 | 진행0G/Materializing, 합성 서버15000G, MasterSword·SimpleShield 보유/장착 | 미완료 동일 intent의 직접 재요청은 CommittedPendingMaterialization. 진행·인벤토리 바이트 동일, 성공 콜백 추가0 |
| I 이후 새 프로세스 | PID8619 → 8808 | 같은 거래 Complete/0G/보유2·장착2·동료1·도감Bat·모델/위 능력치 일치, 오류0. 재구매 intent 생성 거부 |

두 복구 모두 합성 서버 파일을 새 프로세스에서 읽었고 **초기 서버 Gold15000**을 별도로 고정 관측했습니다. 메모리 기본 Gold를 재생성해 로컬 내구성을 판정하지 않았습니다. 복구 후 정상 업로드로 합성 서버 Gold가0이 될 수 있으므로 최종 서버값만으로 이전 상태를 추정하지 않습니다. 완료 receipt의 Owner/sequence/transactionId/requestHash와 로컬 진행·아이템·모델을 대조했습니다.

원래 상점 UI 실행 소스는 `4ea43a589bd923d9451ea13597e820f2b4f92a73`이고 설치 APK SHA-256은 `a04fc4f0fcc602fe9a6987f79098817ac8b84a103ffa7af4223ed25b6ca3585a`입니다. 동일 intent·중단 시험은 원래 거래 API를 호출하는 별도 시험 진입점입니다. 장비 상점의 터치 동선 전체를 검증했다고 확대하지 않습니다.

## 시험 코드와 산출물

- checkout: `D:/codex/worktrees/tamer-working/Tamer`, 부모 `codex/gameplay-candidate-1022`에 대한 후보입니다. main·운영 출시는 별도 게이트입니다.
- P 이후 통과 소스 `20ee12d206f191950fa2e7b67f63c9b06f396959`, APK153899364bytes, SHA-256 `ad4e946cba0c17db631406f683d08b8be804e36032f9df72cdb686e4bf9929a0`.
- I 이후 통과 소스 `ecf0b442622bb88bad5f51fbdf52f4a5e70426d4`, APK153899937bytes, SHA-256 `a2b4bf8a32802298d3203ed102f09e711a7d3581c08ad15b42cea89df7fae181`.
- Unity6000.3.25f1/revision e1dba0a9aba4, CLI1.0.0-beta.8, min25/target36/ARM64/debug signing/code26. 설치된 base APK의 바이트 해시 일치도 확인했습니다.
- 두 변형은 전용 `com.AeDeong.MonsterTamer.revival.shoppurchase`를 사용하며 P용 `RevivalShopPurchaseV2`와 I용 `RevivalShopAfterInventory` 저장을 분리했습니다. 데이터 삭제 없이 업데이트했고 이전 실패 폴더도 보존했습니다.
- 인터넷·결제·GMS광고ID 권한과 PGS/MobileAds 초기화 provider 부재, backup=false/fullBackupContent=false 및 cloud/device-transfer 제외를 검사했습니다. AdServices 권한은 남아 있으므로 전체 SDK 수집 부재로 확대하지 않습니다.
- 각 빌드의 보호53개 사전 보존·사후 읽기 검토·지정 복구1회 후 원바이트/Git clean/소유 Editor 종료 확인. 마지막 시험 앱도 종료했습니다. 운영 계정·기존 저장·No Ads·서명키·다른 LD 인스턴스는 사용·변경하지 않았습니다.

새 hook은 `UNITY_EDITOR || TAMER_SHOP_HARNESS` 조건에서만 포함됩니다. 정상 Player의 거래 알고리즘은 이번 PR에서 변경하지 않습니다. 빌드 전역 시험 define 혼입은 거부하며, 오프라인 시험 빌드에만 추가 define을 줍니다. 합성 서버는 SavePath 생성 이후 지연 초기화하고, 복구 marker가 있는데 서버 파일이 없으면 초기값으로 덮지 않고 중단합니다.

공통 격리 분류 Editor 회귀는 source919408b의34/34 PASS입니다. 이후 조건부 shop 코드는 각 실제 Android 빌드와 위 실행으로 확인했습니다. 공통 Editor 결과를 최종 HEAD의 전체 회귀라고 표현하지 않습니다. 기존 구매 실패 경계의 Editor 합성은 source8144a0a/9877574의 해당 test-case Passed 근거를 유지합니다. 전자의 전체 실행250개 중12실패와 후속 영향 범위139/139 통과 이력도 구분합니다.

## 실패 원인과 한계

첫 실제 source1741ef1의 새 프로세스 종합 판정은1회 실패했습니다. 정상 Shield·중복·P 이후 중단은 통과했고 실제 복구 파일은0G/Complete/같은 거래/보유2·장착2였습니다. 원인은 완료 receipt에서 의도적으로 생략하는 ItemId/결제 상세를 시험 판정이 요구한 것입니다. 완료 Owner/sequence/ID/requestHash 및 실제 진행·아이템·모델 대조로 수정했으며 원본 실패 기록·APK·저장 폴더를 보존했습니다. 실패를 삭제하거나 최초 실행부터 통과한 것으로 쓰지 않습니다.

실행 전 검토에서는 SavePath 초기화 순서와 합성 서버 재로드 판정 누락도 수정했습니다. 준비 도구의 ADB 일시 offline은 읽기 전용 준비 확인으로 구분하며 같은 조건의 제품 재시도로 세지 않습니다. 의도한 cut 예외와 실제 판정 실패도 구분합니다.

LDPlayer14/API34/x86_64의 ARM64 번역·4096byte 페이지 결과입니다. 실ARM64·16KB·Play 전달 파일·원격/다기기 동시 소비·재설치/데이터 삭제 후 아이템 복원·신규 결제·환불은 검증하지 않았습니다. 저장 전 실패·세션 전환·표시 실패 등의 모든 합성 분기를 Android에서 반복하지 않았고 기존 Editor 근거만 있는 범위는 그대로 남깁니다. 기존 Client.UpdateUserData가 CAS가 되거나 서버 원자 거래가 생긴 것은 아닙니다.
