# 원래 전투의 사망·로비 복귀 — 2026-10-23

기존 오프라인 gameplay APK에서 보조 무적·피해 주입 없이 적의 원래 공격을 받아 사망했습니다. 원래 Game Over 창의 OK로 로비에 돌아왔고, 같은 플레이어로 게임 재진입·피격·로비 귀환도 확인했습니다. 새 제품 코드나 시험 빌드는 추가하지 않았습니다.

| 단계 | 실제 관측 |
| --- | --- |
| 첫 Main | HP100/Gold1000/동료0/reads2/writes0 |
| Game 진입 | 원래 전투에서 HP100→90→80→…→10→0. Game Over 창 표시 |
| 원래 OK | NextScene→Main, HP100/Gold1000/동료0/reads4/writes0 |
| 사망 후 재진입 | 같은 플레이어의 MANUAL_READY Game. HP100에서 적에게 재피격해60까지 감소 |
| 수동 로비 귀환 | MANUAL_READY Main, HP100/Gold1000/동료0/reads6/writes0. 이전 generator·이전 씬 적 잔존0 |

전체 과정은 같은 PID8975였습니다. 관측 오버레이의 게임 오류·totalErrors·연결 진단 카운터는0이었습니다. **프로세스 시작 직후 하네스가 구독하기 전의 원시 로그에는 알려진 PlayerConnection 소켓 진단3건**이 있으므로 원시 Error 전체가0이었다고 표현하지 않습니다. 원시 Error는 정확히 `Socket: Failed to set blocking mode`2건과 `Unable to setup multicast socket for player connection.`1건이며 게임 예외·기타 Error는 없었습니다. 오프라인 debug 앱의 네트워크 권한 부재에 따른 진단으로 기존 분류와 연결합니다.

관리용 결과 요약기의 두 판정 오류도 분리했습니다. 첫 번째는 오버레이 전용 카운터 문자열을 로그에서 요구했고, 두 번째는 위 시작 전 소켓 진단까지 게임 오류로 취급했습니다. 실제 화면·원시 메시지·구독 시점을 대조해 기록 방식을 고쳤으며 제품 시험을 다시 실행하지 않았습니다. 실제 사망 시나리오 실패는0입니다.

## 대상·보존·한계

- checkout `D:/codex/worktrees/tamer-working/Tamer`, 문서 작성 기준 부모 `fc9881be798de71e7a3c2f3b363598d007e461c3`. 실행 소스는 기존 `4ea43a589bd923d9451ea13597e820f2b4f92a73`이며 후속 shop 변형 코드를 실행한 근거로 사용하지 않습니다.
- 앱 `com.AeDeong.MonsterTamer.revival.gameplay`, 설치된 base APK를 직접 읽어 SHA-256 `a04fc4f0fcc602fe9a6987f79098817ac8b84a103ffa7af4223ed25b6ca3585a` 일치 확인. Unity6000.3.25f1/CLIbeta.8/min25/target36/ARM64/debug APK입니다.
- LD Tamer index1/emulator-5556, Android14/API34/x86_64 ARM64 번역/4096byte 페이지입니다. 실ARM64·16KB·Play 제공·운영 계정이나 전체 캐릭터 UI 검증은 아닙니다.
- 신규 구매·광고·운영 로그인·저장·No Ads·서명키 사용0. 다른 앱·LD 인스턴스는 조작하지 않았고 앱 데이터 삭제/재설치는 하지 않았습니다. 끝에 이 앱만 강제 종료하고 PID 부재를 확인했습니다.
- 이 실행은 동료0 상태입니다. 사망 시 보유 동료의 실제 제거·재포획·기존 계정 저장까지 검증했다고 확대하지 않습니다. 그런 분기는 기존 합성 근거와 향후 최소 기기 관측 범위를 구분합니다.

이번 결과는 부모 gameplay 후보의 추가 실행 근거이며 main 통합·출시 완료를 의미하지 않습니다. 다음은 실제 기기 범위, 캐릭터/삭제 UI, 광고·개인정보/최종 제공판 및 출시 게이트입니다.
