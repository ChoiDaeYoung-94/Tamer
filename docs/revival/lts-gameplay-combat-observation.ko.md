# Unity 6.3 실제 Game 피해·사망 후 Main 복귀 관측

2026-09-28 승인된 새 빌드1회·기기1회에서 원본 Main→Game, 자연 전투 피해, Game Over의 원래 OK 버튼→Main 복귀를 확인했습니다. 예정한 **생존 상태의 수동 Return 버튼 복귀**는 수행하지 못했고, 그 마커를 요구하는 비공개 coordinator의 자동 판정은 실패입니다. 재빌드·기기 재실행은 하지 않았습니다. Game에는 미니맵 출력 RenderTexture의 깊이 설정과 관련된 Render Graph 경고가 반복되어 후속 수정이 필요합니다.

## 변경과 실제 빌드 대상

기존 development 하네스의 8초 자동 전환을 기존 Enter Game/Return to Main 버튼의 수동 대기로 바꿨습니다. 실제 연령 질문의 입력 소유권을 읽는 기존 인벤토리 대기 조건을 development의 Main/Game 대기에도 적용했습니다. 기본 development Game에만 0.25초 주기의 player HP·가까운 적4개의 instance ID/type/HP/거리 읽기 관측을 추가했습니다. 제품 빌드·photo의 자동 왕복·다른 격리 변형의 앞선 종료 흐름은 유지합니다. 무적·HP 변경·시험 몬스터 생성·저장·Prefs·timeScale 쓰기는 추가하지 않았습니다. FindObjects/거리 정렬 비용은 정적 리뷰로 성능 보증하지 않습니다.

실제 checkout은 `C:/Users/pc_17/.codex/worktrees/unity-lts-transition/Tamer`, branch는 `codex/lts-gameplay-roundtrip`, 빌드 source는 `579649515de43d34cfe88e1ae200af4e756d5171`입니다. 최신 main 조회 기준은 `34f2aea5ec673168e0f17af17eb2f80a1feac9e2`이며 빌드 직전 HEAD/검증 target/branch 일치·dirty0·해당 Editor0를 기록했습니다. 후속 문서 커밋이나 이후 main을 이 빌드의 source로 표현하지 않습니다. 통합 담당자의 해당 source 정적 리뷰에서 실행 차단 결함은 발견되지 않았습니다.

Unity6000.3.25f1/revision e1dba0a9aba4·CLI1.0.0-beta.8·Pipeline0.6.0-exp.1·JDK17.0.18+8·NDK27.2.12479018·Gradle9.3.1·build tools36.0.0으로 Run-GameplayHarness의 development 빌드1회가 성공했습니다. 새 APK는110,624,443바이트이며 SHA-256은 `375b48b7df6dfe0a5759f9554fb33fadad2e5495c4be0788a08cbb30b52249b9`입니다. 별도 앱ID `com.AeDeong.MonsterTamer.revival.gameplay`, debug 서명·debuggable·min25/target36/ARM64/version1.0.5/code26 및 INTERNET/ACCESS_NETWORK_STATE/BILLING/AD_ID·MobileAdsInitProvider 제거를 실제 APK에서 확인했습니다. 이 기본 development 변형의 백업/전송 제외 검사는 수행하지 않았습니다.

Editor 종료 후 wrapper의 서명/광고/manifest/settings snapshot 복원을 확인했습니다. Unity가 URP 에셋4개에 생성한 trailing whitespace만의 diff 때문에 기기 실행 전 source guard가 한 번 중단됐습니다. 실제 의미 변경0을 비교하고 비공개 diff를 보존한 뒤, Editor0 상태에서 해당 공백만 복원했습니다. 이 중단 시점에 설치/기기 실행은0이었고 APK를 재빌드하지 않았습니다.

## 단일 기기 흐름과 경계

Android13/API33/PAGE_SIZE4096 물리 기기에서 별도 패키지 부재 확인 후 fresh 앱만 설치했습니다. 완성된 PNG handoff 뒤 연령창의 응답하지 않음과 Main/Game 화면을 직접 확인했습니다. 입력마다 현재 own 설치 경로 해시·전면 앱·새 APK 해시를 대조했고 phase 신호는 앱ID/SHA/nonce/action을 대조했습니다. 조작 대기는300초·명시적 취소로 제한했고 stdin을 사용하지 않았습니다. 입력은 연령 응답1회, 기존 Enter Game1회, 원본 gameplay 위치에4초 swipe1회, 원래 Game Over OK 위치에60ms press1회입니다. capture assist/무적/고정롤/시험 spawn 버튼 사용0입니다.

| 항목 | 이번 실행에서 확인한 결과 |
| --- | --- |
| 자연 피해 | player HP100→90→80→…→0, 동일 Chest instance ID의 HP70→60→50→40→30 |
| 원래 사망 복귀 | Game Over 화면과 OK를 직접 확인, 원래 GameOverGoLobby/ResetPlayer/Switch 흐름 후 Main HP100/Gold1000 |
| 읽기·쓰기/저장 | Main 시작 reads2→Game3→Main 복귀4, writes0; 격리 player 파일 bytes·SHA 동일 |
| 시각/네이티브 | Main 환경·캐릭터/HUD/조이스틱, Game 미니맵·적·HP0/Game Over, 복귀 Main 표시; Unity/IL2CPP maps 확인, native translation/fatal 없음 |
| 자동 criterion | `MANUAL_READY Main` 마커 부재로 coordinator exit1/자동 판정 false; 원래 사망 복귀를 수동 하네스 마커 통과로 바꾸지 않음 |
| 정리 | fresh own 앱 제거 성공, 해당 Editor0, 슬롯 반환, 로컬 worktree/Library/비공개 증거 보존 |

원본 몬스터들이 대기 중 접근해 사망했고, swipe 입력은 로그 시각상 HP0 이후였습니다. 따라서 살아있는 조이스틱 이동·적 처치·Gold 보상·저장 쓰기·생존 수동복귀는 이번 결과로 검증하지 않습니다. HP100 복구는 관측한 원래 Game Over 처리의 동작이며 시험 도구의 HP 조작이 아닙니다. coordinator의 최종 마커 timeout 실패 후 같은 조건 재시도는0입니다.

## 로그 경고와 남은 수정

직접 관측 overlay의 `errors=0`은 하네스 로그 구독 이후 카운터입니다. own PID 로그에는 구독 전 Development PlayerConnection의 Socket blocking 실패2줄·multicast setup 실패1줄(E Unity)이 있습니다. fatal event/harness FAIL marker는 없지만 전체 로그 오류0을 주장하지 않습니다.

Game 구간에는 `In the render graph API, the output Render Texture must have a depth buffer.` 경고가 보존된 복귀 로그에서3,104회 나타났습니다. 읽기 확인으로 `Assets/Scenes/Game.unity`의 camera target이 GUID `1b3b360c665b4c7438ead554d27a61ee`인 `Assets/Scripts/MiniMap/minimapRenderTexture.renderTexture`를 가리키며 해당 에셋 `m_DepthStencilFormat`이0임을 확인했습니다. 이는 경고와 일치하는 후속 수정 후보이며 이번 실행에서 depth 변경·호환모드 복귀·추가 Editor 검증을 하지 않았습니다. Game의 경고 누적을 완전 성공으로 숨기지 않습니다.

[공개 요약과 증거 해시](lts-gameplay-combat-observation.json)에 경계별 결과를 기록했습니다. raw 화면·own PID 로그·maps·player 파일·신호·operator/coordinator는 비공개 `Logs/revival/lts-gameplay-device-1/*` 및 `lts-gameplay-*`에 보존합니다. 운영 계정/구매/광고/저장 실행0, 기기 네트워크 설정 변경0, 인벤토리 재시작 검증 반복0, strict RELRO 재실행0, 실제 ARM64 16KB 실행0, 최종 출시 AAB/스토어 검증0입니다. 이전 APK의 정렬 결과는 이번 APK 결과로 대체하지 않습니다.
