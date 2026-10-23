# 아이템·장착 상태의 프로세스 재시작 보존 — 2026-10-23

별도 오프라인 inventoryrestore 앱에서 원래 `ShopMan.SaveItem`과 `EquipmentManager.Equip` 경로를 실행한 뒤, 앱 데이터 삭제 없이 강제 종료하고 새로운 프로세스에서 재로드했습니다. 두 단계 모두 기대 상태와 일치했습니다.

| 항목 | 첫 저장 | 새 프로세스 복원 |
| --- | --- | --- |
| 보유 아이템 | SimpleSword, MasterSword, SimpleShield 정확히3개 | 같은3개, 상점 소유 목록과 일치 |
| 장착 | MasterSword, SimpleShield 정확히2개 | 같은2개 |
| 모델 | SimpleSword/MasterShield 비활성, MasterSword/SimpleShield 활성 | 동일 |
| 능력치 | HP150, Power60, AttackSpeed1.5, MoveSpeed3 | 동일 |
| Gold / 도감 | 1000 / 비어 있음 | 동일 |
| 판정 | `INVENTORY_RESTORE_PASS phase=write`, 게임·전체 오류0 | `phase=restart`, 게임·전체 오류0 |

첫 실행의 원래 연령 모달에서 시험 앱의 응답하지 않음을 선택했습니다. 복원 실행에서는 저장된 선택을 사용했습니다. 운영 사용자 연령을 설정하거나 바꾼 시험이 아닙니다. 첫 프로세스와 재시작 프로세스가 다름을 확인했고 각 프로세스의 로그·화면을 별도로 보존했습니다. 마지막에는 이 시험 앱만 강제 종료하고 프로세스 부재를 확인했습니다.

## 대상과 산출물

- checkout: `D:/codex/worktrees/tamer-working/Tamer`, 실행 소스 `bf18dab5ee9fb6f741ef07d5cf72c3c694c4c61f`. 코드 변경 없이 기존 `BuildInventoryRestoreAndroid` 변형을 빌드했습니다.
- Unity6000.3.25f1/revision e1dba0a9aba4, CLI1.0.0-beta.8, Android min25/target36/ARM64/version1.0.5 code26/debug 서명.
- 별도 앱 `com.AeDeong.MonsterTamer.revival.inventoryrestore`, 앱 전용 `RevivalInventoryRestore` 저장 폴더. 설치 전에 이 앱 부재를 확인했으며 다른 시험/운영 앱의 데이터 삭제·재설치는 하지 않았습니다.
- APK110728443bytes, SHA-256 `f2e2b74356820b46ca0cb3c89f31053e788c3a286d86192a846e85edcce6409c`. 설치된 base APK를 읽어 같은 바이트 해시인지 대조했습니다.
- Manifest의 allowBackup=false/fullBackupContent=false와 cloud-backup/device-transfer 제외 규칙을 포함합니다. 인터넷·결제·GMS광고ID 권한 및 PGS/MobileAds 초기화 provider 부재를 검사했습니다. AdServices 권한은 남아 있으므로 전체 SDK 처리 부재로 확대하지 않습니다.
- LDPlayer14.0.32.0의 Tamer 인스턴스, Android14/API34/x86_64의 ARM64 번역/4096byte 페이지입니다. 실ARM64·16KB·스토어 검증과 구분합니다.
- 빌드별 보호53개 보존·사후 읽기 대조 후 지정 복구1회, 원바이트·Git clean·소유 Editor 종료·IAP 임시2개 부재를 확인했습니다. 기존 산출물과 비공개 원시 근거는 보존했습니다.

## 범위와 한계

이 시험은 같은 설치의 로컬 소유 인벤토리·장착 복원 검증입니다. 초기 아이템은 시험 코드가 원래 API로 준비했으며 골드 구매 UI·거래 중단 복구·원격/다기기 원자성·앱 데이터 삭제 후 복원·실제 Google Play 결제는 검증하지 않았습니다. 메모리 서버의 기본 Gold1000은 재생성되므로 Gold 재시작 내구성의 근거로 사용하지 않습니다. 운영 계정·기존 저장·No Ads·서명키는 사용하거나 변경하지 않았습니다.

설치 전에 연결 준비 확인1회가 불일치했고, 로컬 실행 스크립트의 미설치 앱 조회 및 비공개 폴더 재사용 처리 오류도 수정했습니다. 모두 앱 실행 전의 확인 도구 오류이며 제품 저장 시험 실패로 합산하거나 숨기지 않습니다. 실제 첫 저장1회와 새 프로세스 복원1회는 모두 통과했습니다.

이 기록의 통합 대상은 부모 `codex/gameplay-candidate-1022`이며 main/운영 출시 완료가 아닙니다. 다음은 #361의 게임 Gold 구매 성공·중복·중단 복구 증거입니다.
