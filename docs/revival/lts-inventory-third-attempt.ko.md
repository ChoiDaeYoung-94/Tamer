# Unity 6.3 LTS 인벤토리 하네스 — 승인된 세 번째 기기 시도

2026-09-28 사용자는 [두 번 실패한 기기 검증](lts-inventory-visual-validation.ko.md)의 수정된 재개 절차로 세 번째 실행 한 번을 승인했습니다. 이 기록은 앞의 실패/미완료 근거를 대체하지 않습니다.

## 고정 대상과 실행

- checkout `C:/Users/pc_17/.codex/worktrees/unity-lts-transition/Tamer`, branch `codex/lts-inventory-visual-retry`, 기록 기준 `origin/main` `4a2d55e81a960e98c4d12e8c6996f121d4655eed`. 최신 원격 조회·clean·해당 Editor0를 확인했습니다.
- 실제 APK source는 `a5c1bb77de819358bad6001caf8dc6fc514d98c5`이며 이전 확정 APK를 그대로 사용했습니다. 110,603,451바이트, SHA-256 `ff0ef16b1368082ecf8f0a317968b4f1886f4f55d68ab3fc35947d1dd388f313`. SHA와 기존 오프라인 manifest/debug/min25/target36/ARM64 검사 증거가 일치했습니다. 재빌드0·새 Editor 실행0입니다.
- Unity6000.3.25f1/revision e1dba0a9aba4, CLI1.0.0-beta.8, Pipeline0.6.0-exp.1, JDK17.0.18+8, NDK27.2.12479018, Gradle9.3.1, build tools36.0.0인 기존 APK입니다.
- Android13/API33/PAGE_SIZE4096 물리 기기에서 별도 `com.AeDeong.MonsterTamer.revival.inventoryrestore` 패키지 부재를 확인한 뒤 fresh 설치했습니다. 기존 앱·데이터·운영 계정·실구매·실광고·네트워크 설정은 변경하지 않았습니다.

## 결과와 실패 원인

실제 연령 확인 창을 캡처하고 한국어 설명·응답하지 않음 버튼·격리 앱 overlay가 표시됨을 직접 확인했습니다. stdin을 쓰지 않는 절차에서 명시적 재개 신호를 기다렸지만 **모델/도구 화면 왕복 중 20초 timeout이 먼저 만료**됐습니다. 실패 원문은 `Local operator did not resume after inspecting the age modal`입니다. 수정안의 기기 없는 경계 3건 통과는 보존하며, 그 시험이 실제 모델/도구 왕복 시간의 충분성을 검증한 것으로 표현하지 않습니다.

timeout의 finally가 설치한 앱만 삭제했습니다. 이후 준비된 응답하지 않음 입력은 전면 앱 확인 가드에서 거부되어 **버튼 입력0·재개 신호0**입니다. 다른 앱에 좌표 입력을 보내지 않았습니다. 이번 실패는 검증 자동화의 짧은 신호 상한이며 게임 crash나 Unity LTS 렌더링 회귀로 단정하지 않습니다. 연령 창의 좁은 시각 관찰만 있고 캐릭터·장비·HUD와 `phase=write/restart`, 인벤토리 파일 전후 증거는 없습니다.

[공개 요약](lts-inventory-third-attempt.json)에 source/SHA/실행 횟수/증거 해시를 기록했습니다. 실제 화면·private 보조 스크립트·결과 JSON·preflight는 비공개 `Logs/revival/lts-inventory-device-3/*`와 `lts-inventory-preflight-3.json`에 보존했습니다.

## 중단 상태

APK 빌드는 전체1회 성공, 기기 실행은 전체3회 실패, 네 번째 실행0입니다. 이번 사용자 승인은 세 번째1회만이므로 추가 기기 검증은 수행하지 않았습니다. 단순히 signal timeout만 확대하면 기존 하네스의 Main Ready 40초 제한과도 충돌할 수 있어 근거 없는 재시도를 준비 완료로 표현하지 않습니다. 추후 변경은 화면 확인과 입력/신호의 실제 도구 왕복 시간을 함께 해결하고 사용자 추가 승인으로 별도 결정해야 합니다.

strict RELRO 재검사0·실제 ARM64 16KB 실행0·출시 AAB/스토어 검증0입니다. 기존 APK 및 이전 버전의 결과를 이번 인벤토리 시각/복원 성공으로 표현하지 않습니다. 독립 문서 정리 외 종속 작업은 중단했습니다.
