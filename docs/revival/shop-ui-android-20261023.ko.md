# 원래 장비 상점 UI·구매 결과 재시작 — 2026-10-23

별도 오프라인 저장에서 **원래 조이스틱→상점 NPC→장비 목록→상세 YES→확인 OK→완료 OK**를 직접 조작했습니다. 방패와 검의 가격·실제 골드 차감·장착·모델·능력치·HUD·새 프로세스 보존이 일치했습니다. 자동 구매나 장애 주입으로 UI를 대신하지 않았습니다.

| 단계 | 실제 결과 |
| --- | --- |
| 초기 | Gold20000, 보유/장착0, HP100/Power10/AttackSpeed0.5/MoveSpeed3 |
| SimpleShield5000G | 확인 전20000G 유지, 확인 뒤15000G/방패 보유·자동 장착/방패 모델 활성/HP150. 완료 OK 뒤 동일 |
| MasterSword15000G | 확인 전15000G 유지, 확인 뒤0G/검·방패 보유·자동 장착/검·방패 모델 활성/HP150·Power60·AttackSpeed1.5·MoveSpeed3. 완료 OK 뒤 동일 |
| 소유 목록 | 두 항목에 Equipped 표시. 장착 중인 검을 다시 눌러도 상세/거래 재실행 없음, 진행·인벤토리·합성 서버 파일의 바이트 동일 |
| 강제 종료→재시작 | PID9444→9967, cloudRestored=true/initialCloudGold0. Gold0·보유/장착2·모델/위 능력치·HUD 일치. 진행·합성 서버 바이트 동일, 인벤토리는 세션 fence 갱신 때문에 바이트가 달라도 내용 일치 |

변경 시점마다 메모리 Gold와 로컬 진행 Gold, 저장 인벤토리·장착, 네 모델 활성 여부, 능력치 및 `Gold - 값` HUD 일치를 관측했습니다. 각 단계의 게임 오류/관측 totalErrors는0이었습니다. 장비 목록과 상세·확인·완료·Equipped 표시는 화면으로도 대조했습니다.

기존 UI에서는 완료 OK 후 상세창이 남으며 NO로 상세창을 닫으면 목록으로 돌아옵니다. 실제로 그 경로를 사용했고 추가 차감은 없었습니다. 상세 문구의 줄바꿈과 개발 진단 오버레이 겹침은 시각적 제한으로 남깁니다. 이 기록을 UI 미관 개편이나 모든 해상도 검증으로 표현하지 않습니다.

## 소스·산출물·보호

- checkout `D:/codex/worktrees/tamer-working/Tamer`, source `37271aecce470f7b281d5db0ac150d2ea19c4300`. 변경은 전용 빌드/독립 저장/관측 경로이며 원래 ShopMan·Item·PlayerUICanvas의 UI·구매 동작을 수정하지 않았습니다.
- `BuildShopUiAndroid`, `TAMER_SHOP_UI` 및 기존 offline define을 해당 APK에만 추가합니다. 전역 시험 define 혼입은 거부합니다. UI 분기가 자동 거래 시험보다 먼저 종료되어 자동 지급·중단 hook·자동 왕복은 실행하지 않습니다.
- 앱 `com.AeDeong.MonsterTamer.revival.shoppurchase`, 고정 전용 `RevivalShopManualUi`. 이전 P/I·실패 fixture 폴더를 보존하고 데이터 삭제 없이 update했습니다. 설치 전후 base 해시를 대조했습니다.
- APK153904641bytes/SHA-256 `e1e3ef97047ad8e4c9b4e0d345278b0921d16a3bd5609144f3020af216dd0744`, Unity6000.3.25f1/revision e1dba0a9aba4/CLI1.0.0-beta.8/min25/target36/ARM64/code26/debug 서명.
- 인터넷·결제·GMS광고ID 권한 및 PGS/MobileAds 초기화 provider 부재·백업/기기전송 제외 확인. AdServices 권한 잔존을 전체 SDK 처리 부재로 확대하지 않습니다.
- 보호53개 사전 보존/빌드 후 읽기 검토/지정1회 복구/원바이트·Git clean·소유 Editor 종료 확인. 원시 화면·로그·파일은 비공개 보존, 마지막에 이 앱만 종료했습니다. 운영 계정·기존 저장·No Ads·키·다른 LD 인스턴스 변경0.

소스 읽기 검토·Android 컴파일·위 실제 UI와 새 프로세스 관측·diff check를 수행했습니다. 공통 격리 Editor34/34는 선행 source919408b의 결과이며 이번 source 전체 Editor 회귀라고 쓰지 않습니다. 관리용 편집 스크립트의 인수/따옴표 오류는 빌드 전 수정됐고 제품 UI 실행 실패는0입니다.

LD Tamer index1/emulator-5556의 Android14/API34/x86_64 ARM64 번역·4KB 페이지 결과입니다. 실ARM64·16KB·Play 제공·운영 광고·실제 결제·원격/다기기 거래·다른 장비 조합 전체는 미검증입니다. 통합 대상은 부모 gameplay 후보이며 main·운영 출시는 별도입니다.
