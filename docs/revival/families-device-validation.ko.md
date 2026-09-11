# 광고 실기기 검증 — 2026-09-11

#91의 3차 검증이다. [2차 검증](families-ads-validation.ko.md)의 빌드 기록은 그대로 보존한다. 운영 광고 활성화나 출시 승인을 의미하지 않는다.

## 대상과 재현 기준

- checkout: `C:/Users/pc_17/.codex/worktrees/e5e5/Tamer`, branch `codex/families-device-validation-91`.
- 빌드 소스: `5e276549913f28436b503fd7841277db42fce07d` (base `6a45ff8`). 이후 SDK main 변경을 포함한 통합 빌드는 아니다.
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, GMA Unity `11.5.0` / Android `25.4.0`, UMP `4.0.0`.
- Galaxy Note20 Ultra 5G / SM-N986N, Android 13/API 33, ARM64, 실제 페이지 크기 **4096 bytes**. 16KB 기기 검증은 하지 않았다.
- 두 APK 모두 별도 application ID, 공식 sample App ID, debug 서명, ARM64 only, minSdk 24 / targetSdk 36, version 1.0.5/code 26. 계정·저장·결제 없는 격리 harness만 실행했다.

| APK | bytes | SHA-256 | debuggable |
|---|---:|---|---|
| Tamer-ads-sample.apk | 130886198 | `e5cdbf8284fe591f0cd78d6d7a437c12884051fd15ee3dfae8caf520c5ec38c3` | true |
| Tamer-ads-control.apk | 63581281 | `9dbd3870cd5527691f5afe6ff923b1cce1dcc52e428d81982a5c8b5c54d66811` | false |

두 빌드 및 manifest/signature 검증이 성공했고 통합 담당자가 해시와 검증 결과를 독립 확인했다. nondevelopment control도 debug 서명이다. MobileAdsInitProvider와 AD_ID 권한은 두 APK에 남아 있다.

## 실제 관찰

기기에서 드러난 카메라 없는 harness의 잔상을 전용 검은 배경 카메라로 고쳤다. GUI matrix 복원과 BGM 상태 로그를 추가했다. 변경은 Editor 또는 TAMER_AD_TEST_HARNESS 조건에 한정된다. 새 두 APK에서 UI가 정상 표시되었다.

| 시나리오 | 관찰 결과 |
|---|---|
| sample 시작/명시적 Load | 시작 시 managed SDK idle. 요청 플래그 설정 → Initialize → Load 성공 순서 확인. |
| 정상 시청/닫기 | earned 후 닫기에서 보상 1회, Rewarded 완료. 광고 동안 기존 BGM 정지, 닫은 뒤 재개. |
| 로드되지 않은 Show | Unavailable 완료, 보상 증가 없음. 명시적 Show 경로의 후속 Load 성공. |
| Android Back | 광고 시작 약 1초 뒤 Back으로는 닫히지 않음. 이후 정상 시청/닫기 성공. |
| X로 보상 전 취소 | 약 5.4초 대기 후 보이는 X와 확인 창을 확인하여 닫음. opened→closed 6.629초, earned 없음, Cancelled, 보상 4 유지, BGM 재개. |
| 광고 중 owner 파괴 | opened 약 2초 뒤 파괴. 이후 earned/closed가 와도 해당 요청 보상·완료 콜백 없음. |
| 광고 중 scene A-B-A | 약 2초 뒤 전환. 이후 earned/closed가 와도 해당 요청 보상·완료 콜백 없음. |
| 광고 중 BGM 교체 | 새 BGM 재생이 earned/closed 이후에도 유지됨. 정상 보상 1회. |
| 광고 중 manager 파괴 | 약 2.05초 뒤 파괴. 닫은 뒤 보상 5/완료 7 유지, BGM 재생, Load/Show 비활성. |
| 광고 종료 후 Home 왕복 | 약 3초 background: pause true/BGM false → pause false/BGM true. |
| 새 control 설치/실행/Load/Show | can_request false, PolicyBlocked, 보상 0. managed initialize/load trace 없음. |

표시된 creative는 공식 테스트 표시가 있는 Flood-It!이었다. 네이티브 광고 위에서도 Unity Update가 실행되어 예약 파괴/씬 전환이 실제 광고 표시 중 수행됐다.

첫 광고 동영상의 독립 프레임 검토에서 약 5초 구간에 X가 보이며 일부 인접 프레임에는 보이지 않았다. 영상 첫 프레임부터 이미 광고가 표시되어 정확한 native first pixel 기준점은 없다. 단일 5.77초 캡처에서 X가 없다는 사실만으로 닫기 불가라고 판단하지 않는다. **약 5초 구간 X 표시와 보상 전 취소는 확인했지만, 정확히 5.000초 이내라는 판정 및 운영 creative의 Families 적합성은 미확정**이다. 수동 확인이 늦어 보상을 받은 두 시도는 조기 취소 성공에 포함하지 않았다.

## AdMob 읽기 전용 확인

같은 날 사용자가 열어 둔 Chrome 세션에서 승인된 계정 및 Monster Tamer Android 앱에 접근했다. 과거 가입 화면으로 접근이 막혔던 관찰은 현재 세션에는 적용되지 않는다.

- 보상형 버프 광고 단위 1개. 운영 미디에이션 그룹 0개, 사용 설정된 캠페인 0개. 광고 단위/앱 수준 게재빈도 제한 없음 표시.
- 미디에이션 그룹 목록에는 AdMob(기본) 1행만 표시.
- 개인정보 및 메시지 홈에서 유럽 규정/미국 주 규정/IDFA 모두 새 메시지 만들기 표시. 게시된 메시지를 확인하지 못했다. 생성 흐름이나 설정 변경은 수행하지 않았다.
- AdMob의 별도 아동 연령 관련 설정은 이번 읽기에서 확인하지 않았다. 앱 요청 플래그 검증과 Console 설정 검증을 구분한다.
- 광고 단위 생성/수정, 계정/약관/설정 변경, 운영 광고 요청은 하지 않았다. 계정 식별자·운영 광고 ID·수익 정보는 이 기록에서 제외했다.

## 한계와 정리

실기기에서 중복 활성 요청, native failed callback, **광고 표시 중** background 복귀, 실제 No Ads 권한은 미검증이다. 이전 자동 테스트와 구분한다. 이번 변경 후 새 두 APK 빌드와 실제 UI/콜백을 검증했으며, 이전 249/249 테스트를 새 실행으로 표기하지 않는다.

패킷/필드 단위 네트워크 관찰은 하지 못했다. sample Load 성공은 확인했지만 control의 managed trace 부재가 네트워크 부재나 SDK 정보 접근 부재를 증명하지 않는다. UMP 앱 호출 및 운영 동의 흐름은 별도 미완료 항목이다.

원시 PID 로그, 스크린샷, 동영상은 ignored `.revival-local/ads-device/`에만 보관했다. 기기 식별자는 공개하지 않는다. 테스트 sample/control 앱은 모두 제거하고 `pm path`가 비었음을 확인했으며, 이번에 만든 기기 내 녹화 파일도 제거했다. 개인 앱/데이터/네트워크 설정을 변경하지 않았다. 이 checkout의 Editor는 종료 상태이며 다른 checkout의 Editor는 건드리지 않았다.
