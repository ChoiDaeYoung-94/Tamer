# Unity 6.3 LTS 인벤토리 하네스 시각 검증 — 기기 실행 중단 기록

2026-09-28 병합된 `origin/main`을 조회하고 `a5c1bb77de819358bad6001caf8dc6fc514d98c5`를 확인했습니다. 해당 checkout Editor0·clean 확인 후 `C:/Users/pc_17/.codex/worktrees/unity-lts-transition/Tamer`에서 `codex/lts-inventory-visual`을 생성했습니다. 기존 LTS 전환/비공개 증거·소유자 원본·다른 작업의 checkout은 보존했습니다.

Unity `6000.3.25f1`/revision `e1dba0a9aba4`, CLI `1.0.0-beta.8`, Pipeline `0.6.0-exp.1`, JDK `17.0.18+8`, NDK `27.2.12479018`, Gradle `9.3.1`, build tools `36.0.0`으로 기존 `Run-GameplayHarness.ps1 -Variant inventoryrestore` 빌드 **1회 성공**했습니다. C# 런타임 변경이나 APK 재빌드는 없습니다. 빌드 직전 source는 위 main SHA, dirty0이며 Editor 종료 후 자동 YAML 공백만 정리했습니다.

APK `Build/revival/Tamer-inventoryrestore.apk`는 110,603,451바이트, SHA-256 `ff0ef16b1368082ecf8f0a317968b4f1886f4f55d68ab3fc35947d1dd388f313`입니다. 별도 앱 ID `com.AeDeong.MonsterTamer.revival.inventoryrestore`, debug 서명, min25/target36/ARM64/version1.0.5/code26와 INTERNET/ACCESS_NETWORK_STATE/BILLING/AD_ID·MobileAdsInitProvider 제거, 백업·기기 전송 제외를 실제 APK로 확인했습니다. 운영 계정·상점 결제·실광고·기존 앱 데이터는 사용하지 않았습니다.

## 기기 실행 두 번과 원인

Android13/API33·PAGE_SIZE4096 물리 기기에서 별도 패키지 부재를 확인한 뒤 fresh 설치했습니다. 첫 실행은 `ISOLATION_OK`와 Main scene/HP100/Gold1000/transitioning=false 기록 뒤 40초에 `GAMEPLAY_HARNESS FAIL Main entry`가 발생했습니다. 테스트 앱만 삭제했습니다. 삭제 전 화면은 캡처하지 못했지만 해당 패키지의 process-start 이벤트에서 소유 PID를 식별하여 그 PID 로그만 비공개 복원했습니다. 관찰 로그에 게임 예외나 fatal signal은 없었습니다.

[기존 인벤토리 기기 절차](inventory-restore-device.ko.md)는 새 앱 연령 창에서 **응답하지 않음**을 선택했습니다. 이번 자동 절차에는 그 단계가 누락됐습니다. `AgeChoicePresenter`는 최초 Main에서 연령 창을 열고 timeScale을0으로 만들며 하네스 Ready는 timeScale1을 요구하므로, 창을 닫지 않으면 Main entry 검사에 실패합니다. SDK/렌더링 회귀로 확정할 근거가 없습니다.

두 번째 실행은 같은 APK를 fresh 설치하여 실제 Main의 연령 창·응답하지 않음 버튼·한국어 문구가 표시되는 화면을 캡처했습니다. 그러나 private 보조 스크립트의 `input()`이 plain pipe 실행에서 EOFError로 종료됐습니다. 버튼 선택·write/restart·캐릭터/장비/HUD 시각 검증 전에 중단됐고 finally에서 테스트 앱 삭제를 확인했습니다. 이 실패는 게임 런타임 오류가 아니라 검증 자동화 오류입니다. 추가 앱이나 설정은 변경하지 않았습니다.

두 기기 검증 모두 실패로 기록하며 **세 번째 실행은 하지 않았습니다**. 연령 창이 실제로 렌더링됐다는 좁은 관찰만 있고, 캐릭터/장비/UI 전체 검증이나 인벤토리 저장·재시작 성공으로 표현하지 않습니다. 기존 Unity의 성공 결과도 새 LTS 결과로 대체하지 않습니다.

## 준비한 수정과 남은 조건

private 보조 스크립트의 stdin 대기를 로컬 resume 파일 대기로 바꾸고 입력 오류가 결과에 남도록 준비했습니다. 기기를 사용하지 않는 독립 경계 검증은 3/3 통과(0.058초)했습니다: 신호 부재 timeout, 다른 앱/해시/동작의 신호 거부, stdin DEVNULL에서 정상 신호 수락입니다. 재개 신호는 별도 앱 ID·확정 APK SHA·응답하지 않음 화면 확인 동작과 일치해야 하고 다음 단계 전에 own 설치 경로를 다시 확인합니다. 실제 기기 검증은 재실행하지 않았으며 별도 이름의 유사 검증으로 실패 횟수를 우회하지 않았습니다. 사용자 승인 후 세 번째 시도에서는 같은 APK·별도 fresh 앱·실제 연령 창 화면을 확인하고 응답하지 않음만 선택한 다음, Main의 write/restart 마커·캐릭터/장비/HUD·인벤토리 파일 전후를 보존하고 own 앱만 삭제해야 합니다. 별도 APK 빌드는 필요하지 않습니다.

[프로젝트 규칙](../../AGENTS.md)의 “같은 테스트가 2회 이상 실패하면 해당 테스트와 그 결과에 의존하는 작업을 중단한다”에 따라 종속 검증을 중단했습니다. 원시 로그·화면·기기 식별자·private 보조 스크립트는 비공개 `Logs/revival/lts-inventory-*`에 보존했습니다. [공개 요약](lts-inventory-visual-validation.json)은 APK/증거 해시와 실패·미검증 범위만 기록합니다.

strict RELRO 재실행0, 실제 16KB 실행·출시 AAB·스토어 검증0입니다. [LTS 전환 APK의 별도 결과](lts-transition-validation.ko.md)는 그대로 보존하며 이번 inventory APK의 정렬 성공으로 바꾸어 표현하지 않습니다. 다음 사용자 결정은 준비한 입력 방식으로 같은 기기 검증을 세 번째 실행할지 여부입니다.

## 승인된 후속 시도 기록

이 문서의 1·2차 실패와 독립 경계 시험은 당시 기록으로 보존합니다. 이후 사용자가 세 번째1회를 승인했고, [세 번째 시도 기록](lts-inventory-third-attempt.ko.md)에 실제 결과와 중단 상태를 별도로 기록했습니다.
