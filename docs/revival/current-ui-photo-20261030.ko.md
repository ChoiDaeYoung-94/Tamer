# 현재 UI 촬영 앱·버전 검사 보완 — 2026-10-30

최신 UI를 검토할 수 있도록 별도 오프라인 촬영 APK를 만들고 LD플레이어 `Tamer`에서 마을·설정·전투의 원본 PNG3개를 확보했습니다. 합성 계정이며 사용자께서 연령 화면의 `응답하지 않음`을 직접 선택했습니다. **운영 계정/서버 저장·구매/No Ads·광고·정상 Play 제공 검증이나 최종 디자인 승인은 아닙니다.**

## 실패 원인과 변경

첫 촬영 빌드는 source`95ff33abb63b2d28a0aef45c98d3b066afe748a7`의 기준 검사기가 version code27을 고정해, 현재 승인된29에서 `BuildFailedException`으로 중단됐습니다. APK는 생성되지 않았습니다. 원본 ProjectSettings1개·보호6946파일의 SHA/집합·Git clean·소유 Editor0을 확인하고 실패 자료를 보존했습니다.

source`f422fe43338a3e538f0af6fb9c38f9786e1103f2`, `codex/photo-ui-preparation-1030`에서 `RevivalBuild.ValidateBaseline`을 보완했습니다. `tools/revival/toolchain.json`의 버전명과 Android 코드를 PlayerSettings와 정확히 대조하며 빈/누락/잘못된 문서는 허용하지 않습니다. Unity 버전·SDK/ABI·레거시 marker 검사도 유지합니다. 변경 코드는 Editor 도구/Editor 검사2파일이며 운영 runtime 변경은 없습니다.

Unity6000.3.25f1/revision e1dba0a9aba4·CLI1.0.0-beta.8·Android target/EditMode에서 실제 기준 검사1개와 버전 쌍 일치/불일치·필드 누락·잘못된 JSON10개, **11/11·실패0·컴파일 오류0**입니다. 직렬화된 설정1개와 보호6946파일을 정확 복구하고 Editor0을 확인했습니다. 후속 문서 통합은 실행 소스 불변으로 연결했으며 같은 성공 시험을 반복하지 않았습니다.

## 새 촬영 APK와 복구

| 항목 | 결과 |
| --- | --- |
| 빌드 소스 | `f422fe43338a3e538f0af6fb9c38f9786e1103f2` |
| 환경 | Unity6000.3.25f1 / CLI beta8 / Pipeline0.6.0-exp.1 / min25·target36·ARM64 |
| 앱/버전 | `com.AeDeong.MonsterTamer.revival.gameplay` / 29 / 1.0.5 |
| 분리 | Android debug 서명, debuggable=false, 별도 앱 저장소·합성 계정 |
| APK | 82,272,720bytes / SHA-256 `e1f07cab17ee21f846cded6ddf5af2dca602f3268a24975146fb5c8597c495ca` |
| 권한/시작 | INTERNET·ACCESS_NETWORK_STATE·BILLING·AD_ID·MobileAdsInitProvider 부재 검사 |
| 빌드 결과 | 수정 뒤 성공1회. 앞선 검사 단계 실패1회와 구분 |
| 복구 | 검토된 설정6개 복구·보호6946파일 SHA/집합 일치·clean·원키 불변·관측 소유17프로세스 종료/Editor0 |

현재 Tamer에서 새 패키지 부재를 확인하고 한 번 설치했습니다. 기존 보호6패키지의 버전/설치 경로와 운영 앱 미실행을 확인했고, 다른 인스턴스·데이터 초기화·자동 권한 부여·불필요 앱 추가 제거는 없었습니다. 이전 앱 정리의 APK/저장/설정 백업은 유지합니다.

## 원본 화면과 한계

| 원본 | 크기 | SHA-256 |
| --- | --- | --- |
| town.png | 720×1280 / 227,573bytes | `0cde377e883ae2a81f971f580b1773ea5558c1fe86da43750ec953a8a9b2592e` |
| settings.png | 720×1280 / 148,796bytes | `85f0a1ddd11984d3c6aec2d3a7a0390be4faab5680044fd0a60c387fdfd9cefc` |
| combat.png | 720×1280 / 74,649bytes | `aa648bd116e5106b39c8b9f5b89c8ea051321acb30185e476548178b7bdf6053` |

Android SDK의 `screencap -p`가 반환한 PNG 원본 바이트를 저장하고 시각 상태를 확인했습니다. 크기 변환·자르기·보정·AI 재생성은 하지 않았습니다. 기존 PHOTO 버튼은 진단 렌더링만 숨기며 일반 UI/로그/게임 규칙은 유지합니다. 전투 씬은 시험 메뉴의 수동 이동으로 진입했고 촬영 보조의 몬스터 생성·무적·포획 확률 고정은 사용하지 않았습니다. 이는 정상 포털 동선이나 포획 확률의 검증이 아닙니다. 시작 자동 Main/Game/Main 왕복은 errors0·writes0으로 관측했습니다.

상점의 최신 촬영본·실제 노치/다른 launcher·최종 사용자 검토는 남습니다. 이전 격리 앱의 상점/동료/저장 검증은 [UI 기록](ui-facelift-20261029.ko.md)에 별도로 유지합니다. 원본 PNG/APK/로그는 비공개이며 스토어/공개 README에는 업로드하지 않았습니다. 촬영 뒤 해당 시험 앱만 종료하고 설치/자료·LD 인스턴스·다른 앱과 소리 설정은 보존했습니다.

작업 상태: 버전 검사 수정·11개 시험·새 촬영 빌드/복구·3화면 확보 완료. 다음: 사용자 디자인 검토와 필요한 상점 화면, 정상 Play 제공/최종 운영 검증. 사용자 즉시 할 일·질문: 없음. PR #496은 부모 #495와 함께 Draft로 유지하며 [전체 잔여](remaining-recovery-work.ko.md)를 따릅니다.
