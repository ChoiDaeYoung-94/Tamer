# 개발 진단 로그의 빌드 조건 수정 — 2026-10-26

공통 `DebugLogger`가 Unity 기본 빌드 심볼과 다른 `Conditional("Debug")`를 사용해 일반적인 개발 빌드에서도 호출을 생략했습니다. Editor 또는 Development 빌드에서만 로그를 포함하도록 수정했습니다. 과거 로그인 실패 전체의 원인을 이 문제로 단정하지 않습니다.

## 변경

- 공개8개 함수와 내부 공통 출력의 조건은 `UNITY_EDITOR` 또는 `DEVELOPMENT_BUILD`입니다. 일반 release 호출은 인수 평가와 함께 제외되며 `Debug`/`DEBUG`만 정의해 활성화하지 않습니다.
- 함수 이름·인수·출력 형식은 유지했습니다. 일반 release에서도 필요한 기존 로그인 단계 진단은 별도 경로 그대로입니다. 계정·영수증·키를 새로 출력하는 기능은 추가하지 않았습니다.
- 풀의 의도된 잘못된 입력 시험에서 기대하는 오류 로그 조건도 맞췄습니다. `.meta`와 GUID는 보존했습니다.

## 검증과 한계

대상 checkout은 `D:/codex/worktrees/tamer-working/Tamer`, 실제 Unity 검증 source는 `6db37d53b75e62fb479f11bed7cdc65da5f38819`입니다. Unity6000.3.25f1/e1dba0a9aba4, Android target, CLI1.0.0-beta.8 환경입니다.

| 검사 | 실제 결과 | 범위 |
| --- | --- | --- |
| 원본 오류 재현 | Development 심볼에서 예상8개 대신 로그0·인수 평가0으로 기대값 실패1회 | 원본 문제를 재현한 결과이며 수정 후 통과에 더하지 않음 |
| C# 동작 검사 | 새 PowerShell7.6.5/.NET10.0.11 compiler assembly의6조건 통과 | 실제 logger 소스·가짜 Unity 로그 수신부·호출부를 함께 컴파일. 속성 문자열만 비교한 시험이 아님 |
| 빌드 심볼 행렬 | 심볼 없음·DEBUG·Debug는 보통 호출/인수 평가0. Editor·Development·둘 다는 로그8/인수 평가16 | reflection으로 몸체를 강제 호출하는 경우는 보통 호출의 생략 계약과 다름. 모든 출력의 보안 경계로 표현하지 않음 |
| Unity EditMode | 관련 `RevivalPoolTests` **7/7**, 실패·skip0 | 예상 오류를 포함한 실제 Editor 경로. 전체900개 재실행이나 새 Android player 검증이 아님 |
| 실행 후 보존 | 보호59개 원본 바이트 확인, Editor가 바꾼4개 설정 복원, clean·소유 Editor 종료 | 다른 프로젝트 Editor·worktree·기존 APK·Library·계정/저장·구매권한은 조작하지 않음 |

총괄이 실행 HEAD 인자를 잘못 지정한1회는 Editor·설정 쓰기 전에 보호검사에서 중단됐습니다. 준비 기록과 실제 HEAD를 대조해 수정한 뒤 위 Unity 시험을 한 번 실행했습니다. 이 사전 차단을 Unity 시험 실패나 통과로 합산하지 않습니다.

이후 문서 보충과 병합을 해당 시험의 재실행으로 표현하지 않습니다. 최종 운영 AAB·실제 제공물 검증은 [전체 잔여 목록](remaining-recovery-work.ko.md)을 따릅니다.
