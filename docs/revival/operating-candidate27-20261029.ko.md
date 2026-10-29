# 운영 후보27 AAB 생성과 기본 검사 — 2026-10-29

**운영 후보 AAB 1회 생성·기본 정적검사·작업본 복구를 완료했습니다. 최종 운영 기능·정책·Play 제공물·출시는 아직 완료하지 않았습니다.** 원본 AAB와 키/인증서·소스 검토·실행 로그는 비공개로 보존하며 바이너리를 공개 Git/LFS에 올리지 않았습니다.

## 정확한 대상

| 항목 | 실제 대상 |
| --- | --- |
| source | `eba77a66f75122f0c0c539ed0d5b006c73d28253`, `codex/adult-candidate-plan-1029` |
| checkout | `D:/codex/worktrees/tamer-working/Tamer` |
| Unity / CLI | 실제 로그 `6000.3.25f1 (e1dba0a9aba4)`, 고정 CLI `1.0.0-beta.8` |
| 실행 경로 | 검토된 `approved_adult_release`와 exact plan/source-review·현재 upload 인증서 |
| package / version | `com.AeDeong.MonsterTamer`, code27 / 1.0.5, min25 / target36 / ARM64, non-debug / non-test |
| AAB 크기 | 76,868,171 bytes |
| AAB SHA-256 | `961730db025bfec34ba2a8cca405bb2c60d646dcbf8fd2cdc64f627456e8af83` |

기존 producer의 파일명은 `disabled-preparation.aab`이지만 실제 mode는 plan·callback의 `approved_adult_release`로 확인했습니다. 이름만으로 disabled/privacy/debug 경로를 혼합하지 않습니다. code27 파일은 생성됐지만 Console에 예약·업로드·제출된 번호는 아닙니다.

## 실제 확인한 결과

빌드 직전 정상 Console 읽기로 전체20번들 최대26·production24 제공/26 미승인과 현재 upload 공개 인증서를 확인했습니다. 현재 공개 DER는 기존 바인딩과 같았습니다. 프로젝트 계정 pin과 fresh 로그인·활성 라이선스2를 읽기 확인했으며 계정/라이선스를 변경하지 않았습니다.

사용자가 한 로컬 마스킹 창에서 store/alias 암호를 직접 입력했습니다. 기존 JKS의 실제 개인키 challenge가 현재 공개 인증서·exact plan/head에 귀속한 신규 signing receipt를 만든 뒤 동일 입력의 소유 producer가 AAB를 한 번 생성했습니다. CLI returnCode0, `producer_completed`/`actualBuildExecuted=true`, pre/postbuild와 approved build 계약·scene 값 및 scoped settings 복원 결과를 확인했습니다. 키 원본을 복제하거나 암호를 채팅/clipboard·상위 환경에 등록하지 않았습니다.

독립 읽기 검사에서 bundle validate, production package/code27/min25/target36·non-debug/non-test, versionName1.0.5와 현재 **업로드용** signer의 payload822개 전체 서명을 확인했습니다. Play가 나중에 제공하는 APK의 앱 서명 검증과 구분합니다. ARM64 native6개 모두 ELF LOAD·보수 RELRO 배치 검사를 통과했습니다.

추가 strict RELRO end-modulo16384 진단은 `libc++_shared.so`, `libmain.so`, `libunity.so`의3개 항목이 미충족입니다. 기본 검사 통과와 분리해 기록하며, 이 진단만으로 실제 runtime crash나 스토어 거절을 단정하지 않습니다. 개발27의2건은 별도 아티팩트 이력이며 이번 결과와 합치지 않습니다.

compiled resource·config SHA와 직렬화 값은 prebuild/scene/postbuild receipt에 연결됐습니다. **최종 binary resource의 독립 추출은 아직 하지 않았습니다.** 실제 ARM64 16KB·Play APK/split ZIP/설치·운영 광고/동의·계정/저장/구매·정책/심사는 이 기본 정적 검사의 범위 밖입니다. `binaryVerified`, `distributable`, `releaseReady`는 false로 유지합니다.

## 실패 보존과 정확 복구

첫 launcher는 숨긴 기동 방식 때문에 입력창을 관측하지 못해 실제 producer/키 challenge·SDK 실행 전에 소유 parent만 종료했습니다. 첫 plan/attempt·로그를 보존한 뒤 Normal 표시 기동으로 조건을 바꾼 새 run을 준비했습니다. 기존 claim/예산을 초기화하지 않았고 실제 AAB 호출은 한 번입니다. 새 plan에서도 도구156소스와907pin/runtime732파일을 확인했으며 기존 시험의 입력 연결과 새 실제 빌드를 구분합니다. 회귀 시험을 기록 목적으로 반복하지 않았습니다.

종료 뒤 actual delta·staged owner의 파일 식별/해시를 검토했습니다. 원본 변경9개 중 PGS/PlayFab Editor prefs2개는 내용이 같고 식별정보가 달라진 항목이었습니다. 기존7필드 recovery 계약으로 원본9개를 복원하고 원래 미존재하던 소유 staging9개만 제거했습니다. missing/late writer0, 보호7,062파일의 원래 내용 inventory와 clean HEAD/branch·active journal 부재·소유 parent/bridge/CLI/Editor0·AAB SHA 불변을 확인했습니다. 원래 파일 ID 자체 복원과 내용/GUID 복원은 구분하며, 디렉터리·Library·원키·원본 snapshot·모든 비공개 증거를 보존했습니다.

## 다음 연결

binary resource 독립 추출과 제공24+후보의 Data safety/공개 안내 정합을 필요한 범위로 마친 뒤, 준비된 AAB의 사용자 수동 업로드와 같은 Play 제공물·필요 기능/정책 검증으로 이어갑니다. 실제 키 입력이 성공했다는 이유로 백업 복호화·외부 복구나 출시 전체를 완료 처리하지 않습니다. [전체 잔여 목록](remaining-recovery-work.ko.md), [현황](current-recovery-status.ko.md), [단일 입력 경계](single-input-signing-20261027.ko.md)를 유지합니다.
