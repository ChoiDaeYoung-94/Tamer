# 기존75검사의 동기 EditMode/raw 실행 확인 — 2026-11-08

앞선 상점·회복 검사에서 테스트/화면/컴파일 산출물은 통과했지만, HTTP RPC 종료의 ThreadAbort/HttpListenerDisposed2개가 화면 전용 실행에서도 재현돼 당시 전체 실행은 실패로 보존했습니다. 동일 조건을 반복하는 대신 설치된 Test Framework1.6.0의 공식 `TestRunnerApi`/`ExecutionSettings.runSynchronously`를 raw CLI `executeMethod`로 호출해 HTTP 요청을 사용하지 않는 경로를 확인했습니다. 동기 모드는 여러 프레임이 필요한 검사를 제외할 수 있으므로 원래 실행한75개 이름을 전부 고정해 대조했습니다. [Unity 공식 API](https://docs.unity.cn/Packages/com.unity.test-framework@1.6/api/UnityEditor.TestTools.TestRunner.Api.ExecutionSettings.html)

## 실제 결과

checkout `D:/codex/worktrees/tamer-working/Tamer`, branch `codex/deletion-lifecycle-arm64-1108`, 검증 HEAD `89bb77c18ede3bad497b223b147e0d4d72534e30`입니다. Unity6000.3.25f1/e1dba0a9aba4·CLI1.0.0-beta.8·기존 Android min25/target36/기본ARM64·SDK/JDK를 유지했습니다. 테스트 원문/패키지/asmdef는 변경하지 않았습니다.

- 원래 XML에서 추출한 같은75개: UI9·게임 세션66. 실행된 이름 집합·중복 없음·각 XML testcase의 Passed·콜백 Started/Finished 각1회를 확인했습니다.
- 결과75Passed/Failed0/Skipped0/Inconclusive0. 새 coverage나150검사로 합산하지 않습니다.
- private C# 소스는282ref로 사전 컴파일0, 실제 compiler response에서 TestRunner 참조가 확인된 기존 Editor test assembly에 임시cs/meta2개를 정확히 배치해 Editor1회 실행했습니다. 새 Player/기기/운영키0입니다.
- 새 실행의 구조화 오류0·기존 RPC/fatal 패턴0입니다. CLI0 뒤30초 남은 소유 자식만 해당 제한 작업 안에서 종료했습니다. 일반 shutdown 진단을 로그 전체0으로 표현하지 않습니다.
- 보호7173파일·보호 집합·키/JDK·두 ADB종료 옵션 원본값·임시2파일의 바이트 보존/정확제거·Editor/소유job0·공유49852를 확인해17:25:03.949391UTC 실제반환했습니다. Library를 보존했습니다.

첫 보조도구에는 옛20분 빌드의1170초 최소 여유 조건이 남아, C# 컴파일 뒤 Editor0에서 중단됐습니다. 최초 실패와 복구 자료를 보존했습니다. C# 컴파일을 반복하지 않고 같은 원5/8 창(actual17:21:08.528853/new17:26:08.528853/full17:29:08.528853UTC) 안에서 시간 조건/내부CLI예산을180초로 줄여 최초Native를17:24:01.622042UTC에 실행했습니다. 원기한을 연장하거나 실패를 통과로 덮어쓰지 않았습니다. 소유 경계는 최소meta형식/기존파일/경로이탈/해시충돌/미확인meta를 포함한7경우로 확인했습니다.

## 범위와 남은 사항

Editor의 원 DungGeunMo SDF 조회는 U+2026 존재=true였고 이75검사 중 Ellipsis 경고의 객체 문맥은0개였습니다. 실물 Player의 경고1개가 해소됐다는 근거는 아니며 [실물 진단](deletion-lifecycle-physical-arm64-20261108.ko.md)과 원TTF/SDF 보존을 유지합니다.

이 검증은 대체 실행 조건의 결과입니다. 기본 CLI test/HTTP RPC 구현을 수정하거나 SDK를 교체하지 않았으며, 과거 전체 실행 실패는 그대로 남깁니다. 저장소 전체 필수 검사·일반 실행 경로와 실제 운영 기능/정상Play·No Ads/연령별SDK/통신·16KB·최종 UI/스토어/정책·서명/심사/프로덕션을 대신하지 않습니다. [현재 현황](current-recovery-status.ko.md) · [전체 잔여](remaining-recovery-work.ko.md)
