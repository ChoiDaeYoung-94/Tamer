# 실제 게임플레이 수동 검증 준비

2026-09-12, `codex/gameplay-device-followup`, main `40b23cc` 이후 하네스 후속이다.

## 기존 결과와 이번 변경

[기존 기록](gameplay-harness.ko.md)은 이전 APK `f2ab1d9`에서 Main/Game 왕복 2회, owner 보존, 몬스터·미니맵 표시, Home 복귀, 오류0, 메모리 read6/write0을 확인한 결과다. 해당 결과를 새 APK 실행으로 옮겨 적지 않는다. 실제 전투·포획·사망 후 복원과 진행도 쓰기는 계속 미검증이다.

기존 `RevivalGameplayHarness`의 자동 왕복은 그대로 유지했다. 별도 하네스나 씬을 만들지 않고 다음만 추가했다.

- `Enter Game (manual play)` / `Return to Main (manual)` 버튼은 기존 `GameManager.SwitchMainOrGameScene()`을 호출하고 Ready 및 동일 Player를 확인한다. 전환 중, 시간 정지 중, HP0 상태에서는 수동 버튼을 비활성화한다. 사망 복귀는 원래 게임오버 UI로 검증해야 한다.
- HP·Gold·동료 수·메모리 read/write를 0.25초 간격으로 읽고 값이 바뀔 때만 로그로 기록한다. 표시는 관찰값이며 체력·소환·골드·포획 결과를 강제로 바꾸지 않는다.
- 시작의 운영 로그인 차단, 합성 계정/메모리 서버, 고유 로컬 저장 경로, 광고·구매 차단과 별도 Android 패키지 경계를 보존한다. 정상 제품에는 하네스를 포함하지 않는다. 기존 씬/프리팹/GUID를 수정하지 않았다.

## 실행 범위

실행 소스 `060863de6f98b715af79002ce381cb140cb5b475`, Unity 6000.0.81f1 / CLI 1.0.0-beta.8에서 Editor **370/370 PASS**(실패·스킵0, UTC 09:54:59–09:55:03)를 확인했다. 최종 결과와 APK 식별 정보는 [검증 JSON](gameplay-device-followup-validation.json)에 기록했다. Editor 통과와 APK 정적 검증은 기기의 실제 게임플레이 완료를 의미하지 않는다.

실기기 검증은 사용자 요청으로 보류하며 **2026-09-14 이후 사용자의 재개 알림**을 기다린다. 사용자께서 다시 알려주시기 전까지 모든 휴대폰 조작·잠금 상태 재조회·기기 검증·잠금 해제 요청을 보류한다. 자동 재시도나 알림을 등록하지 않는다. 이번 작업의 새 APK는 기기에 설치하거나 실행하지 않는다.

## 사용자 알림 이후 남은 검증

원래 조이스틱으로 적에게 접근해 실제 HP 감소, 적 사망·골드 증가를 관찰한다. 원래 포획 효과와 버튼을 사용해 동료 수 및 격리 로컬 저장의 AllyMonsters 변화가 일치하는지 확인한다. Main 복귀 이후 메모리 서버 write와 로컬 값을 비교하되 운영 클라우드 쓰기 증거로 해석하지 않는다. 실제 전투로 HP0이 된 뒤 게임오버 UI·원래 복귀 버튼·HP 복원·재진입을 확인한다.

운영 계정·로그인·저장·광고·구매, 장비 구매, 재설치 계정 복원, 16KB 실기기 및 출시 AAB의 검증을 이 작업에 포함했다고 주장하지 않는다. 기기 원시 증거가 생성되면 비공개 로컬에만 보관한다.


## APK 정적 검증과 정리

APK는 **105,422,768 bytes**, SHA-256 `d9bfe7e5286c958239045e3b2f77f6d7764ad5123fe5051a4b18b1d48f218ed8`다. `com.AeDeong.MonsterTamer.revival.gameplay`, debug 서명, debuggable=true, ARM64, min24/target36, 1.0.5/code26을 확인했다. 최종 manifest에서 INTERNET/ACCESS_NETWORK_STATE/BILLING/`com.google.android.gms.permission.AD_ID`와 MobileAdsInitProvider가 없는지 기존 verifier를 실행해 통과했다.

통합 담당자가 소스 `060863d`를 독립 검토했고 차단 결함은 없었다. 빌드 후 자체 Editor 0, wrapper 설정 복원, URP/Graphics의 줄바꿈만 변경된 5개 파일 복원을 확인했다. 기기 검증 보류 지시 이후 ADB·휴대폰 조회나 조작은 하지 않았다. 새 APK의 실제 화면·전투·포획·사망 결과는 없다.

통합 담당자가 APK 해시·패키지·서명·ABI·권한 부재를 독립 확인했다. `android.permission.ACCESS_ADSERVICES_AD_ID`, `ACCESS_ADSERVICES_ATTRIBUTION`, `ACCESS_ADSERVICES_TOPICS` 3개 권한은 남아 있다. 따라서 광고 관련 모든 권한이 없다고 표현하지 않는다. billing 서비스 query 문자열이 남는 것과 BILLING uses-permission 부재도 구분한다.
