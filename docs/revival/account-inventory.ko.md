# 소유 불명 로컬 도감·장비·아이템 처리 결정

2026-09-21 사용자 명시 결정: 공용 PlayerPrefs의 `AllyMonsters`(요청의 AlIyMonsters는 실제 키 기준 정정), `playerEquippedItems`, `LocalItem`은 원본을 보존한다. 이를 현재 계정에 자동 가져오거나 다른 계정으로 옮기지 않고, 계정별 신규 도감·장비·아이템은 빈 상태에서 시작한다. No Ads·기존 서버 진행 8키·서버 저장은 이번 변경에서 제외한다. 특히 서버 진행의 동명 `AllyMonsters`는 동행 몬스터 값이며 로컬 도감 PlayerPrefs와 구분한다.

## 구현 범위

- `PlayerInventorySnapshot`의 계정 소유자·도감 목록·아이템 소유·Sword/Shield 슬롯 검증을 재사용한다.
- 현재 진행 파일 옆 `<PlayerData 경로>.inventory/<계정 SHA-256>.json`에 세 영역을 함께 저장한다. 경로 해시는 암호화가 아니며 JSON에는 소유자 식별자가 포함된다. 다른 계정 파일은 읽거나 덮어쓰지 않는다.
- 인증 이후 서버 진행 준비가 완료된 현재 계정/세대에만 읽기·쓰기를 허용한다. 계정 미확정·중단·삭제 접수 중·이전 세대의 callback은 저장하지 못한다.
- 파일이 처음부터 없을 때만 빈 snapshot을 반환한다. 정상 신규 자료는 재실행·계정 복귀 때 다시 읽으며 초기화하지 않는다. 손상·소유자 불일치·접근 실패는 빈 자료로 대체하거나 기존 파일을 덮어쓰지 않는다.
- 검증된 전체 snapshot을 같은 디렉터리의 새 임시 파일에 기록하고 Flush(true) 후 Move/Replace한다. 메모리 값과 장비 효과는 성공한 저장 이후 반영한다. 실패 시 원본은 보존되며 임시 파일이 남을 수 있다.
- 장비 교체는 해제와 착용 두 번의 저장 대신 한 번의 슬롯 교체로 저장한다. 계정 세션을 중단하면 메모리 도감·장비·상점 아이템과 대기 구매를 비우고, 새 준비 세션에서 해당 계정 자료를 다시 읽는다.
- 전환 시 전투·포획 대상을 무효화한다. 몬스터 선택 시 계정/세대를 캡처하고 사망 알림의 도감 등록 시 비교하여 이전 계정 대상이 새 도감에 기록되지 않게 한다. 기존 골드 처리 등 8키 callback 경로의 보증으로 확대하지 않는다.

기존 세 PlayerPrefs를 삭제하거나 수정하는 코드, 서버 업로드, No Ads 이전은 추가하지 않았다. 신규 파일은 로컬 저장이며 다른 기기 복원이나 위변조 방지 기능이 아니다. 기존 진행 데이터의 계정 불일치 보호도 그대로이므로 이번 변경만으로 기존 서버 진행 계정 전환을 자동 허용하지 않는다. 신규 inventory 파일의 삭제 요청 연동·보관기간은 별도 정책 확정 항목이며 현재 삭제 완료를 주장하지 않는다.

## 검증

검증 checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`, branch `codex/account-inventory`, 소스 `3a289793d1e6e112f0f9778cbfb11a85ecde522d`, 실행 직전 dirty 없음. Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android 기준 min24/target36/ARM64 변경 없음.

`RevivalInventory` Editor 필터 **21/21 통과, 0.48초**, 실패·skip 0, 재시도 0. 기존 snapshot 검증 16건과 신규 저장 시험 5건으로, 합성 계정/임시 디렉터리의 저장·재실행·계정 분리·잘못된 자료 보존·파일 잠금 실패·세대 보호를 확인했다. 실제 공용 PlayerPrefs 값은 읽거나 변경하지 않았다. console 16개는 모두 Log였으며 오류·경고는 없었다. 비동기 `test_status`의 completed 및 개별 결과로 완료를 확인했다.

비공개 증거 `Logs/revival/account-inventory-tests.json` SHA-256: `9bd6eb3082bc8ffd5d6f9c1d653fec6830e6b7ed71df385ae9bd2f01a3ce286f`. 실행 전 상태는 `account-inventory-preflight.json`, console/scenes/hierarchy 자료도 같은 디렉터리에 보존한다. 해당 Editor PID82804 정상 종료 확인 후 자동 변경된 alias 설정을 복원했다.

독립 리뷰에서 발견한 전환 전 몬스터 알림의 도감 오염 경로를 소스 `c87acf6c3c40af914e3804f7ec39c7354c6b6632`로 보완했다. 변경 경로의 추가 시험 `Revival_InventoryTransitionClearsTargetsAndRejectsOldCollectionCallback`만 **1/1 통과, 0.12초**, 실패·skip·재시도 0. 앞의 21건을 반복하지 않았다. 실행 직전 같은 checkout/branch에서 source가 HEAD와 일치했고 dirty는 이 결과 문서뿐이었다. `Logs/revival/account-inventory-callback-tests.json` SHA-256: `0d9e1161a8446804724c508e557a573ac4ab5d2bbfb194a7f9c2281b687c4dc5`. 별도 callback-preflight/console 기록을 보존했다. console16개 모두 Log, 해당 Editor PID23184 정상 종료·alias 복원 완료.

운영 계정·외부 서버 호출·기기 시험·APK 생성은 없으며 새 APK SHA-256은 없다. 실제 제품 계정 전환 UI·장시간 gameplay·Android 파일시스템/IL2CPP 동작은 이번 검증에 포함되지 않는다. 기존 서버 저장과 구매 거래 전체의 원자성을 새로 보장하는 변경도 아니다.
