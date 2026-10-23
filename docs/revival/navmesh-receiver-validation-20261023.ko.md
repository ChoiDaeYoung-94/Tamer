# 포획·씬 전환의 이동 수신 상태 보호 — 2026-10-23

## 문제와 수정

LD 오프라인 앱의 포획 후 Game→Main 전환에서 `SetDestination`의 활성/NavMesh 수신 조건 오류를 관측했습니다. 스택은 `SafeSetDestination → CommanderDetectionMove`였으며 특정 수신자가 동료인지 군집 구성원인지는 확정하지 않았습니다.

이동 경계에서 발신자·수신자의 활성 상태, 사망 여부, 계정/세션 수명, 씬 전환, 활성 agent와 NavMesh 상태를 확인합니다. 군집 직접 이동과 동료 이동에도 같은 조건을 적용합니다. 운영 계정·저장·No Ads 권한은 변경하지 않습니다.

추가 대조에서 시험용 NavMesh를 제거한 뒤5프레임에도 `isOnNavMesh=true`가 남았습니다. 동시에 원래 arena는 제거됐고 수신자는 그 위치에 있었으며 주변 NavMesh 조회는 실패했습니다. 따라서 해당 agentType·areaMask로 현재 위치를 조회하는 조건도 추가했습니다. 이는 관측한 환경의 결과이며 모든 Android에서 같은 내부 동작이 발생한다고 단정하지 않습니다.

## 실제 검증

구현 checkout은 `D:/codex/worktrees/tamer-working/Tamer`, 실행 소스는 `4ea43a589bd923d9451ea13597e820f2b4f92a73`입니다. Unity6000.3.25f1/revision e1dba0a9aba4, CLI1.0.0-beta.8, Android min25/target36/ARM64입니다.

| 범위 | 결과 |
| --- | --- |
| 집중 Editor `RevivalMonsterLifecycleTests` | 최신 소스40/40 PASS, 실패·skip0. 이전40개 반복 결과를 고유80개로 합산하지 않음 |
| 정상 수신자 | 실제 이동 상태 진입 허용 |
| 비활성 agent 수신자 | 이동 거부, Idle 유지 |
| 시험용 NavMesh를 제거한 활성 수신자 | 주변 메시 부재와 production guard 거부 확인, 이동 거부·Idle 유지. 오래된 native flag 자체가 false로 바뀌었다고 표현하지 않음 |
| 시험 객체 정리 | 소유 객체를 획득 즉시 추적, 각 정리 개별 실행 후 최종 오류 증가0/PASS |
| 원래 포획·씬 전환 | 시험 보조 Bat/무적/확정 확률만 사용, 원래 전투·표시된 Capture 버튼으로 동료1. Main 복귀에서도 동료1·Gold1015·HP100·오류0 |
| 추가 Main→Game→Main | `ROUNDTRIP_OK count=1 reads=6 writes=1 errors=0 totalErrors=0 playerConnectionDiagnostics=0` |

LDPlayer14.0.32.0의 사용자가 만든 Tamer 인스턴스만 사용했습니다. Android14/API34/x86_64 guest의 ARM64 번역/4KB 페이지이며 실 ARM64·16KB·운영 서비스·스토어 검증을 대체하지 않습니다. 설치된 base APK 바이트가 빌드 파일과 일치함을 확인했습니다.

APK는 별도 `com.AeDeong.MonsterTamer.revival.gameplay`, debug 서명/version26입니다. 크기153859700bytes, SHA-256 `a04fc4f0fcc602fe9a6987f79098817ac8b84a103ffa7af4223ed25b6ca3585a`입니다. 인터넷/결제/GMS광고ID 권한과 PGS/MobileAds 초기화 provider 부재를 확인했지만 AdServices 권한은 남아 있습니다. 전체 SDK 처리가 없다고 확대하지 않습니다.

## 실패 이력과 보존

- 최초 Editor 추가2case는 준비한 agent의 on-mesh 기대 조건에서 실패(39/41)했습니다. 실제 보호 동작 실패나 PASS로 바꾸지 않고, 준비 범위를 바로잡은40개 결과와 구분했습니다.
- 런타임 대조3회는 off-mesh 준비/멤버십 조건에서 실패했습니다. 세 번째 고정 상태 로그로 native flag와 실제 메시 조회의 불일치를 확인한 후에만 조건을 보완했습니다. 네 번째 대조가 위 통과 결과입니다.
- `9516965`에서 포획 버튼을 너무 일찍 누른 관측은 Main 동료0이므로 포획 후 전환 성공으로 집계하지 않습니다. 실제 동료1 관측과 구분합니다.
- 초기 하네스의 준비 실패 객체 회수·정리 전 PASS 문제는 실행 전에 검토로 발견해 수정했습니다. 원본 로그/실패 APK/임시 파일/개인 자료는 비공개 보존합니다.
- 각 Editor/빌드의 보호53개를 보존·읽기 대조하고 지정 복구를 각각1회 수행했습니다. 최신 복구 후 원바이트·Git clean·해당 Editor 종료·IAP 임시2개 부재를 확인했습니다. 최종 관측 후 해당 오프라인 앱만 정지했습니다.

## 통합 범위와 다음 단계

PR401의 대상은 부모 `codex/gameplay-candidate-1022`입니다. main/PR396·운영 출시 완료가 아닙니다. 다음은 기존 격리 inventory restore 변형을 통한 소유 아이템·장착·모델·능력치의 프로세스 재시작 보존, 원래 Gold 구매 성공/중복 처리 등 남은 게임 관측입니다. 실제 기기·스토어·광고 정책·16KB·최종 문서/정리는 별도 게이트로 유지합니다.
