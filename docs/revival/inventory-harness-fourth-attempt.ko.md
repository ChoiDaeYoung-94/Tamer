# Unity 6.3 격리 인벤토리 — 승인된 새 APK와 네 번째 기기 결과

2026-09-28 사용자 승인으로 [하네스 수정](inventory-harness-age-budget.ko.md)의 최종 리뷰 head `4b6726fbd5016bcd782d864296d97f8c810e5a2c`에서 새 APK 빌드1회와 네 번째 기기 검증1회를 수행했고 통과했습니다. [1·2차](lts-inventory-visual-validation.ko.md) 및 [3차](lts-inventory-third-attempt.ko.md)의 실패 이력은 그대로 보존합니다.

## 실제 source·빌드

checkout `C:/Users/pc_17/.codex/worktrees/unity-lts-transition/Tamer`, branch `codex/inventory-harness-age-budget`. 실행 직전 HEAD·remote PR head가 위 SHA와 일치했고 dirty0·해당 Editor0를 확인했습니다. 최신 main 조회 값은 `40754ec71a61674eb02d64e4f94ef32267a8b62c`이며 검증하려고 main으로 전환하지 않았습니다.

Unity6000.3.25f1/revision e1dba0a9aba4·CLI1.0.0-beta.8·Pipeline0.6.0-exp.1·JDK17.0.18+8·NDK27.2.12479018·Gradle9.3.1·build tools36.0.0으로 기존 Run-GameplayHarness의 inventoryrestore 빌드를1회 실행했습니다. 이 수정 source의 전체 Unity 컴파일/IL2CPP 및 Android APK 빌드를 처음 수행한 결과이며 `GAMEPLAY_BUILD_OK originalScenes=4 offline=true`와 exit0를 확인했습니다. Editor 종료 후 원래 서명/광고/manifest/settings snapshot을 복원하고 자동 YAML 공백만 정리했습니다. 후속 문서 커밋을 빌드 직전 source로 표현하지 않습니다.

새 APK `Build/revival/Tamer-inventoryrestore.apk`는153,659,813바이트, SHA-256 `253d5ffd3c40143a2cfd133f5f4f88677b2eccb4ae0a77f06c076b7975d26660`입니다. 실제 APK에서 별도 앱ID·debug 서명·min25/target36/ARM64/version1.0.5/code26, INTERNET/ACCESS_NETWORK_STATE/BILLING/AD_ID·MobileAdsInitProvider 제거 및 백업/전송 제외18개를 확인했습니다. 기존 1~3차 APK와 verifier는 비공개 original-artifact 폴더로 보존했습니다. 새 빌드 이후의 검사 결과를 기존 `ff0ef16...` APK 결과로 표현하지 않습니다.

## 네 번째 기기 실행과 시각 관찰

Android13/API33/PAGE_SIZE4096 물리 기기에서 별도 패키지 부재를 확인한 뒤 fresh 앱만 설치했습니다. 실제 연령 창과 `WAIT_AGE_CHOICE` overlay를 직접 확인했습니다. 이는 새 Unity/IL2CPP에서 원본 presenter/open/modal/scene/owner/blocker의 **정상 조건이 관찰된** 첫 결과입니다. 로그의 WAIT_AGE_CHOICE→write PASS 간격은42.827초로, 기존40초보다 긴 이 구간에서도 실제 연령 질문 대기를 제외한 흐름이 진행됐습니다. 이 간격 전체를 순수 연령 창 표시 시간으로 측정한 것은 아닙니다. 비활성/누락/다른 소유 등 부정 조건 기기 시험을 수행한 결과는 아닙니다.

PNG 작성이 끝나기 전 첫 로컬 미리보기는 부분 파일 오류가 있었고 완료 handoff 확인 뒤 동일 캡처를 읽었습니다. 새 캡처나 기기 재실행은 없었습니다. 화면에 보이는 응답하지 않음 버튼만 선택하기 직전, 한 도구 셀에서 새 APK SHA·현재 전면 앱·설치 경로 SHA를 대조하고 버튼 입력1회와 앱ID/SHA/해당 session token의 신호를 연속 기록했습니다. 개인 연령 선택이나 운영 앱의 설정 변경은 없습니다.

| 경계 | 실제 결과 |
| --- | --- |
| 첫 실행 | `phase=write` 통과 |
| 앱 프로세스 종료→다른 PID의 새 프로세스 | `phase=restart` 통과, 재부여 없이 복원 |
| 보유/검·방패 모델 | SimpleSword/MasterSword/SimpleShield 보유3, MasterSword+SimpleShield 장비2, 원본 모델2 활성은 하네스 검사로 확인 |
| 능력치/Gold/도감 | HP150/Power60/AttackSpeed1.5/MoveSpeed3, Gold1000 유지, 빈 도감 |
| 직접 화면 | 두 단계 Main 캐릭터·검·환경·HP150/150·Gold1000·조이스틱/설정 HUD 표시, 검은 화면/magenta 없음 |
| 디스크 전후 | player 파일 bytes/해시 동일, 인벤토리 Version/Owner/Collection/OwnedItems/Equipped 동일·Session만 갱신 |
| 네이티브/정리 | 두 단계 Unity/IL2CPP mapping, native translation 없음, fatal signal 없음, own 새 설치 제거 완료 |

두 프로세스의 `errors=0`은 하네스의 로그 구독 이후 검증 카운터입니다. 앞선 Development PlayerConnection 초기화에는 각각 Socket blocking 실패2줄과 multicast socket setup 실패1줄(E Unity)이 있었습니다. **전체 앱 로그 오류0으로 주장하지 않습니다.** 사후 로그 대조의 최초 읽기는 Windows 기본 cp949로 UTF-8 로그를 읽어 실패했고, UTF-8을 지정해 읽었습니다. 이 대조는 기기 재실행이나 검사 재시도가 아닙니다.

실제 상점 구매/Gold 차감·비어 있지 않은 도감·Game 전투·운영 인증/저장·실광고/결제·기기 네트워크 설정은 변경하거나 검증하지 않았습니다. 검/방패 전체 모양을 모든 카메라 방향에서 시각 확인한 결과나 전체 gameplay 시각 보증으로 확대하지 않습니다.

## 증거와 남은 범위

[공개 요약](inventory-harness-fourth-attempt.json)에 source/SHA/16개 증거 해시를 기록했습니다. 두 화면·해당 앱 PID 로그·maps·player/inventory JSON·operator signal과 action은 비공개 `Logs/revival/lts-inventory-device-4/*`, 빌드/preflight/검사 자료는 `lts-inventory-corrected-*`에 있습니다. 비공개 에셋/키/설정/기기 식별자/원시 자료는 커밋하지 않았습니다.

이번 승인 범위는 새 빌드1회·기기4차1회이며 추가 빌드/5차 실행0입니다. strict RELRO 재검사0·실제 ARM64 16KB 실행0·최종 출시 AAB/스토어 검증0이고, [기존 전환 APK의 기본/strict 결과](lts-transition-validation.ko.md)를 이 새 APK의 정렬 결과로 바꾸지 않습니다. 이전 실패를 삭제하거나 이후 최신 main을 검증했다고 표현하지 않습니다. 검증 후 Editor0·설정 복원·테스트 앱 제거를 확인했으며 로컬 worktree/Library/비공개 증거를 보존했습니다.
