# RELRO 끝 나머지와 LOAD 배치 판정 분리 — 2026-10-20

기존 검사기의 끝 주소 나머지 측정은 유지합니다. 다만
[현재 Android 공식 안내](https://developer.android.com/guide/practices/page-sizes#check-the-relro-security-flag)는
RELRO가 LOAD 전체를 차지하고 페이지 보호 확장부가 후속 쓰기 가능 데이터와
겹치지 않으면, 나머지가 0이 아니어도 허용하는 예시를 명시합니다.
기존 도구는 이 배치까지 일괄 거절했으므로 수치 진단과 배치 판정을 분리합니다.

## 필드와 검사 계약

| 필드 | 의미 |
| --- | --- |
| `endAligned16KB`, `endRemainder16KB` | 원시 끝 주소와 modulo 측정. 값 변경 없음 |
| `relroChecksPassed`, `relroErrors` | 기존 원시 끝 modulo 진단. 이전 결과와 호환 |
| `--strict-relro` | 기존의 추가 원시 modulo 제약과 종료 코드 유지. 새 배치 예외로 이 과거 strict 실패를 지우지 않음 |
| `relroLayoutChecksPassed`, `relroLayoutErrors` | 별도 보수적 배치 검사. native APK 보고서 schema 4 |
| `relroLayoutPassed` | AAB 각 ELF의 배치 판정. `relroPassed` 원시 진단도 유지 |
| `artifactChecksPassed` | AAB schema 2에서 서명·manifest·ARM64 LOAD와 새 배치 판정을 결합. 출시 승인 아님 |
| `elfRelroLayoutChecksPassed` | 시험 AAB 요약에 추가. 기존 `elfRelroChecksPassed`와 종료 코드 계약은 유지 |

배치 검사는 양수 길이·주소 폭·포함 LOAD 하나를 요구합니다. 끝이 정렬되거나
전체 LOAD와 일치해야 하며, 16KB 단위 보호 범위가 RELRO 밖의 LOAD 바이트와
앞뒤 어느 쪽으로도 겹치면 거절합니다. 중복 소유 LOAD, 범위 초과, 포함 LOAD
없음, 지원하지 않는 다중 RELRO도 통과시키지 않습니다.

이 검사는 PF_W를 추정하지 않고 **모든 LOAD와의 교차를 보수적으로 거절**하므로
원래 읽기 전용 LOAD와 겹치는 일부 안전한 배치도 거절할 수 있습니다.
전체 ELF 로더나 실제 메모리 접근을 시뮬레이션하지 않습니다. RELRO가 없는
파일은 기존처럼 정렬 조건을 통과하지만 RELRO 보안 보호가 있다는 증거는 아닙니다.
LOAD/ABI/ZIP·서명·manifest 검사는 별도로 유지합니다.

## 실제 시험 AAB 30 읽기 대조

- AAB source: `db9a67ba7d8df708c014ba6dfa86edc1945d459d`.
- AAB SHA-256: `c51ada640a66be328e4b80ed59f7294c80b0d6613fad2ec84baf70f3a57786e2`.
- AAB 길이: 73,992,427 bytes. 새 빌드·재서명·업로드 없음.
- 대조 검사 코드: `468b757840fa4ae10b512c0b7b69525c9509b16c`.
- ELF 6개 LOAD 통과, 새 보수적 RELRO 배치 6개 통과.
- 원시 끝 나머지 3개는 그대로입니다. 아래 3개는 전체 LOAD와 일치하고 보호
  확장부에 다른 LOAD 바이트가 없어 예외 조건으로 분류됩니다.

| 파일 | 원시 끝 나머지 | 새 배치 판정 |
| --- | --- | --- |
| `libc++_shared.so` | `0x3000` | 전체 LOAD 예외 통과 |
| `libmain.so` | `0x2000` | 전체 LOAD 예외 통과 |
| `libunity.so` | `0x3000` | 전체 LOAD 예외 통과 |

고정 SHA의 AAB를 읽고 전후 동일성을 확인했으며, 기존 빌드·native 검사 JSON을
덮어쓰지 않았습니다. 과거 strict CLI를 재실행하거나 기존 실패 횟수를 초기화하지
않았습니다. 관련 Python 합성 검사 60개가 한 번에 통과했고, 전체 LOAD 예외의
앞뒤 교차·prefix·포함 불일치·주소 초과·ELF32/64·중복/복수 RELRO·LOAD/ZIP 실패와
AAB 소비자 판정을 검증했습니다. 구매 SDK·비공개 에셋·실제 키는 fixture가 아닙니다.

## 남은 검증

실제 ARM64 16KB 실행, code30 Play 전달 APK/split의 ZIP 정렬·설치·실행,
계정·저장·구매 복원과 정책·스토어 심사는 남아 있습니다. `releaseReady=false`,
`productionBinary16KBVerified=false`, `deliveredApkZipAlignmentVerified=false`를
유지합니다. AAB 내부 ELF의 배치 통과를 전체 16KB 호환 또는 출시 완료로 확대하지 않습니다.

기존 libunity 엔진 링크의 `common-page-size` 인자가 없다는 관측만으로
제품 장애나 재빌드 필요성을 단정하지 않습니다. 현재의 배치와 전달·실행 근거를
먼저 확인하며, Unity·NDK 버전이나 링크 설정은 이번 변경에서 수정하지 않습니다.
