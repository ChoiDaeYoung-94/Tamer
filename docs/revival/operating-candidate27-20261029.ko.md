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

compiled resource·config SHA와 직렬화 값은 prebuild/scene/postbuild receipt에 연결됐습니다. **10월29일 후속 읽기에서 최종 AAB의 named resource 바이트를 독립 추출해 exact plan의 payload와 SHA 일치를 확인했습니다.** 실제 ARM64 16KB·Play APK/split ZIP/설치·운영 광고/동의·계정/저장/구매·정책/심사는 이 기본 정적 검사의 범위 밖입니다. `binaryVerified`, `distributable`, `releaseReady`는 false로 유지합니다.

## 2026-10-29 포함 SDK와 현재 신고 대조

10월29일 현재 Console의 수집/공유 No와 가족 정책 약속 Yes·미리보기 수집/공유 없음을 값을 바꾸지 않고 읽었습니다. 암호화 질문은 No 분기에 표시되지 않아 No로 해석하지 않습니다. 같은 후보27 AAB metadata에서 GMA25.5/UMP4/IAP5.4.3/Billing9/Core1.18/Auth3.7.4/Games22를 확인했습니다. [10월29일 수정표](data-safety-submission-plan-20261029.ko.md)는 이 불일치와 실제 SDK·제공24의 적용 범위, PlayFab 처리자 공유 예외 근거 및 남은 유형별 답안을 연결합니다. Console 저장/제출·업로드·실제 운영 기능/심사 완료는 별도입니다.

최종 AAB에서17,187bytes의 선별 metadata/properties·dependency protobuf를 읽어 완전한 Maven87·Unity66 좌표를 기록했습니다. 개별 라이브러리 metadata는 SDK 선언·버전 연결 근거이며 실행·실제 송신을 입증하지 않습니다. 이 작은 읽기에서는 기존 전체 SHA 검증 파일의 동일 identity·크기/mtime와 writer/deleter 차단을 연결했고 전체 재해시·추출을 반복하지 않았습니다. Unity app metadata6000.3.25f1와 주요 Maven properties도 좌표와 일치했습니다.

## 2026-10-29 바이너리 리소스·manifest 후속 대조

같은 AAB의 resource meta GUID에 대응하는 player-data entry 한 개(1,688 bytes)를 읽었습니다. `RevivalPrivateAdsRelease` 이름과 정수 길이·정렬 프레임 뒤의 실제 payload를 독립 추출해 exact plan의 원래 resource와 바이트/SHA가 같음을 확인했습니다. 이 검사는 전체 type tree 해석이나 실행 중 `Resources.Load` 성공을 증명하지 않습니다. 기존 전체 SHA 검증 파일의 동일 identity·크기·mtime와 읽기 handle의 writer/deleter 차단을 확인했고, 뒤 manifest 검사에서 전체 AAB SHA도 전후 다시 일치했습니다. 리소스 바이트·GUID/entry·내부 설정값은 비공개 증거에 보존했습니다.

최종 manifest를 기존 고정 Java/bundletool로 한 번 읽어 새 조건만 대조했습니다. 실제 실행은 child1·최대 heap256MiB·CPU1로0.516초였으며 새 빌드·앱 실행·키 사용·SDK 설치는 없었습니다.

| 실제 manifest 조건 | 관측과 한계 |
| --- | --- |
| GMA App ID | 복구된 동일 source 설정과 일치. 실제 값은 공개하지 않음 |
| GMA `MobileAdsInitProvider` | 포함. 존재만으로 실제 동의 전 송신을 확정하지 않음 |
| `DELAY_APP_MEASUREMENT_INIT` | 항목 없음. 이것만으로 SDK 결함·개인정보 위반이나 실제 초기화 시점을 판정하지 않음 |
| 초기화/광고 로드 최적화 | 두 metadata 모두 true. 동의·수집 중단 설정으로 표현하지 않음 |
| Unity metadata | `6000.3.25f1` |
| cleartext / network security config | 두 application 속성이 명시돼 있지 않음. 전체 송신 TLS 보장·실제 평문 송신의 증거로 확대하지 않음 |
| 권한 | INTERNET/네트워크 상태·wake/foreground, Billing, 광고 ID 및 AdServices ID/attribution/topics, 비공개 receiver 권한 포함. 선언만으로 각 데이터의 실제 수집·선택 여부를 확정하지 않음 |

`binaryResourceIndependentlyExtracted=true`, `runtimeResourceLoadVerified=false`이며 최종 `binaryVerified/distributable/releaseReady=false`는 유지합니다. 다음 단계의 운영 동의·기능·신고 확인을 대신하지 않습니다.

## 실패 보존과 정확 복구

첫 launcher는 숨긴 기동 방식 때문에 입력창을 관측하지 못해 실제 producer/키 challenge·SDK 실행 전에 소유 parent만 종료했습니다. 첫 plan/attempt·로그를 보존한 뒤 Normal 표시 기동으로 조건을 바꾼 새 run을 준비했습니다. 기존 claim/예산을 초기화하지 않았고 실제 AAB 호출은 한 번입니다. 새 plan에서도 도구156소스와907pin/runtime732파일을 확인했으며 기존 시험의 입력 연결과 새 실제 빌드를 구분합니다. 회귀 시험을 기록 목적으로 반복하지 않았습니다.

종료 뒤 actual delta·staged owner의 파일 식별/해시를 검토했습니다. 원본 변경9개 중 PGS/PlayFab Editor prefs2개는 내용이 같고 식별정보가 달라진 항목이었습니다. 기존7필드 recovery 계약으로 원본9개를 복원하고 원래 미존재하던 소유 staging9개만 제거했습니다. missing/late writer0, 보호7,062파일의 원래 내용 inventory와 clean HEAD/branch·active journal 부재·소유 parent/bridge/CLI/Editor0·AAB SHA 불변을 확인했습니다. 원래 파일 ID 자체 복원과 내용/GUID 복원은 구분하며, 디렉터리·Library·원키·원본 snapshot·모든 비공개 증거를 보존했습니다.

## 다음 연결

binary resource 독립 추출은 완료했으며 제공24+후보의 Data safety/공개 안내 정합을 필요한 범위로 마친 뒤, 준비된 AAB의 사용자 수동 업로드와 같은 Play 제공물·필요 기능/정책 검증으로 이어갑니다. 실제 키 입력이 성공했다는 이유로 백업 복호화·외부 복구나 출시 전체를 완료 처리하지 않습니다. [전체 잔여 목록](remaining-recovery-work.ko.md), [현황](current-recovery-status.ko.md), [단일 입력 경계](single-input-signing-20261027.ko.md)를 유지합니다.
