# 개발 기준 버전27 검증 — 2026-10-27

승인된 운영 producer는 ProjectSettings의 versionCode를 그대로 사용합니다. 10월26일 Console 전체 업로드 최대26 관측에 따라 **명시 준비 후보와 현행 개발 기준을27**로 맞췄습니다. 실제 운영 후보 생성 직전에는 최신 최대값을 다시 확인해야 하며 자동 증가하지 않습니다.

## 대상과 변경

- checkout: `D:/codex/worktrees/tamer-working/Tamer`
- 실제 빌드 소스: `12218e9101670b7c171b403d71f947ea6a91a34b`, `codex/baseline-version-27-1027`, [PR #452](https://github.com/ChoiDaeYoung-94/Tamer/pull/452)
- 버전: 이름1.0.5 유지, code26→27. 설정·현행 baseline 검사/보고15파일과 관련 fixture4파일만 변경했습니다. ProjectSettings는 줄바꿈/기타 필드를 보존한 한 바이트 변경입니다.
- 과거26 기록·legacy BuildInfo 계산·별도 IAP 명시 번호·privacy29/retry 입력은 보존했습니다. 뒤에 추가한 문서 커밋을 실제 빌드 소스로 표현하지 않습니다.

## 실제 검증 결과

| 항목 | 결과와 범위 |
| --- | --- |
| 변경 관련 Python | 4모듈31/31 PASS. 별도 소스 범위·AST·줄바꿈/바이트 대조 통과 |
| 사전 입력 검사 | 준비된 비공개 로컬 Title·PGS·선택 approved entry·27>마지막 관측26 검사 통과. `releaseReady=false` 유지. 현재 Console 새 조회나 운영 승인 통과가 아님 |
| 실제 개발 빌드 | 17:55:34~17:59:44 KST 단1회, `RevivalBuild.BuildAndroidDevelopment`, CLI exit0·Succeeded·오류0 |
| 도구 | Unity6000.3.25f1/e1dba0a9aba4, CLI1.0.0-beta.8, Pipeline0.6.0-exp.1, Android API36/Build Tools36.0.0, NDK27.2.12479018, OpenJDK17.0.18+8, Python3.12.14 |
| 실제 APK | 111,035,700바이트, `com.AeDeong.MonsterTamer.revival`, 1.0.5/code27, min25/target36, ARM64만, debuggable·debug 서명 확인 |
| 기본 native 검사 | 6개 네이티브 라이브러리의 LOAD/ZIP 및 별도 bounded RELRO layout 통과 |
| 추가 strict RELRO | **exit1, 끝 정렬2개 실패**. `libc++_shared.so`, `libmain.so`는 보존한 이전 baseline과 각각 byte/hash 동일. 버전 번호 변경으로 해결되거나 새로 생긴 실패가 아님 |
| 원본 복구 | 실제 변경8파일을 검토한 뒤 보호59개 원시 바이트 복원/대조, `.meta`·비공개 Title 보존, Git clean·해당 Editor 종료 확인 |

APK SHA-256: `d5e179ffa906a2aca66a17b71042a488652e0b3e9b4b0c4533ec565056dedfc9`.

실제 명령·에디터 로그·59파일 snapshot·복구/검사 JSON은 해당 checkout의 비공개 `Logs/revival/baseline27-1027-smoke1`에 보존했습니다. 입력 소유권 검사를 거친 Pipeline 자동 시작의 일시 중지와 CLI의 명시 dirty build는 그 owned staging에만 허용했고 복구했습니다. 다른 프로젝트 Editor·기기·전역 설정을 변경하지 않았습니다. 무거운 빌드 구간은 다른 두 프로젝트 총괄과 조율하고 종료 후 명시 반환했습니다.

## 미검증과 다음 단계

이 결과는 격리 시작 씬을 쓰는 **개발 APK 생성·정적 검사**입니다. 해당 APK의 설치/실행·새 운영 키 사용·운영 서명 AAB·실제 ARM64/16KB 실행·Play 제공물/스토어 심사는 이번에 수행하지 않았습니다. 과거900/900 시험을 이 커밋에서 반복한 것으로 계산하지 않습니다. 추가 RELRO 실패는 [#97](https://github.com/ChoiDaeYoung-94/Tamer/issues/97)에 남기며 기본 통과와 구분합니다.

다음은 운영 광고/지역·연령 source/resource 승인과 개인정보 신고 정합, 실제 build HEAD의 새 plan/source/runtime/인증서 고정 및 단일 입력 연결입니다. **새 운영 AAB는 없으며 현재 사용자 업로드 요청도 없습니다.** 전체 잔여는 [전체 목록](remaining-recovery-work.ko.md)을 따릅니다.
