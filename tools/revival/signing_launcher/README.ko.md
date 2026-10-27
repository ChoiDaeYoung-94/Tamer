# 단일 입력 서명 launcher

검토된 `approved_adult_release` producer용 Windows 연결입니다. 기본 호출은
암호창과 빌드를 시작하지 않습니다. 실제 운영 입력·서명·빌드·배포 통과는
이 코드와 합성 검증만으로 선언하지 않습니다. 기존 광고/성인/지역 승인
false/null과 source-review 미완료 항목을 채우는 기능은 없습니다.

## 실행 연결

1. 총괄이 exact build HEAD/branch와 clean checkout, Editor 종료, 현재 upload
   certificate/키 소유권 독립 근거, 승인 config, 신규 producer plan, exact
   source review와 도구 검토를 준비합니다. resource `sourceHead`의 검토
   baseline과 plan/signing receipt의 실제 build HEAD는 각 계약대로 구분합니다.
2. PowerShell bootstrap은 private binding/source manifest를 공유 읽기만 허용하는
   동일 handle에서 snapshot→SHA→JSON 순서로 읽습니다. reparse·hardlink와
   비공개 ACL을 확인하고 ancestor/source handle을 계속 보유한 채 검토된 C#만
   컴파일합니다. 암호는 이 metadata에 없습니다.
3. C#는 원본 `src/AeDeong.keystore`, 현재 DER, immutable producer/helper 도구,
   config/plan/source review와 Python runtime의 identity/ACL/SHA를 잠급니다.
   Assets/ProjectSettings/Packages의 예정 쓰기 대상은 장기 파일 잠금에서
   제외하며 producer의 inventory 재검사를 유지합니다.
4. Python gate는 existing producer의 exact plan/source/approved preflight와
   설치 도구·원본 key SHA·신규 run을 검사하고 `launcher-attempt.private.json`을
   새로 만듭니다. 실패하면 창을 열지 않습니다. 취소/실패한 attempt도 재사용하지
   않으며 새 plan/run 준비는 총괄의 별도 작업입니다.
5. 한 WinForms 창에서 store/alias 두 암호를 마스킹 입력받습니다. 빈 값, NUL,
   짝이 없는 surrogate는 거부합니다. Unicode와 줄바꿈은 UTF-16BE framing으로
   보존하며 최대 길이는 각 1024 UTF-16 code unit입니다. deadline/minimum start
   budget 소진은 `TimeoutException`, 사용자 취소는 `OperationCanceledException`으로
   구분하고 확인 버튼 후에도 시간·문맥을 다시 검사합니다.
6. native `JOB_LIST`와 `HANDLE_LIST`로 private non-breakaway
   `KILL_ON_JOB_CLOSE` Job에 원자적으로 속한 suspended Python child를 생성합니다.
   membership 확인·resume 뒤 stdin으로만 암호를 보냅니다. 생성/Job 거부 시
   실행/전송은 0입니다. 부모 환경·user/system 환경·인수·파일·clipboard에 암호를
   등록하지 않습니다.
7. 소유 Python child는 Java worker로 원본 JKS integrity/alias private key와
   현재 DER의 난수 challenge를 한 번 검사합니다. 직접 bridge run도 challenge를
   건너뛰고 signing receipt를 만들 수 없습니다. Python→Java와 이후
   Python→CLI→Editor/빌드 자손은 같은 소유 Job의 수명을 따릅니다.
8. challenge 후 exact 문맥을 재검사하고 private run에 새
   `launcher-signing.private.json`을 만듭니다. 기존 경로 덮어쓰기는 하지 않고
   ancestor 및 receipt handle을 보유합니다. 독립 certificate/ownership review,
   exact plan/head/key/alias/cert 근거가 모두 필요한 계약입니다.
9. 두 암호는 소유 Python producer process의 환경에만 설정되며 필요한 빌드
   자손에 상속됩니다. 읽기 전용 git/pwsh probe에는 제거됩니다. 기존 producer의
   PlayerSettings 버전 소비·원본 key 계약을 유지하며 legacy 외부키/BuildInfo
   메뉴 계약을 이 경로에 섞지 않습니다.

## runtime과 출력 경계

`python -I -S -B -X pycache_prefix=<잠근 regular plan 파일>`을 사용합니다.
plan은 디렉터리가 될 수 없어 startup stdlib cache를 포함한 `.pyc` 읽기 경로가
성립하지 않습니다. repository helper 6개는 exact source bytes를 직접 compile/exec하여
기존 unchecked cache를 읽지 않습니다. Python executable, DLL/extension/stdlib
source/zip 집합은 `runtimeFiles`와 `pins`에 포함합니다. 현재 계약은 standalone
runtime만 지원합니다. executable/DLL 이름의 `._pth`, executable 폴더와 상위의
`pyvenv.cfg`가 존재하면 시작 전 거부하며 모든 후보의 정확한 부재를
`absentRuntimeFiles`로 고정합니다. OS system DLL과 관리자 권한에 대한 별도
보안 경계는 주장하지 않습니다.

stdout/stderr는 제한된 pipe로 수집하며 raw message/path/stack trace는 노출하지
않습니다. Python 일반 출력은 폐기하고 bridge 실패의 고정 schema에서
`phase`·정규화 `exceptionType`·`producerInvoked`만 전달합니다. 예를 들어
`challenge/UnrecoverableKeyException`과 `preflight/ValueError`를 구분합니다.
불완전하거나 모순된 출력은 성공으로 받지 않습니다.

`ownedChildStarted`는 raw bridge 시작, `producerInvoked`는 producer 함수 진입,
`actualBuildExecuted`는 producer의 완료 근거를 구분합니다. 시작 전 거부는 false,
실행 중 강제 중단처럼 실제 빌드 여부를 판별할 수 없으면 null입니다. 항상
`binaryVerified=false`, `distributable=false`이며 callback 완료를 최종 AAB 검증이나
출시 승인으로 올리지 않습니다.

취소·deadline·timeout·부모 종료·오류 때 소유 Job만 종료하고 자손 0을 확인합니다.
Job 종료는 파일 원복이 아닙니다. producer journal/actual delta/snapshot을 보존하고,
중단으로 actual delta가 없으면 소유 Editor/자손 종료 확인 후 총괄이 실제 변경을
관측하여 exact recovery를 별도로 검토해야 합니다. 자동 원복·재시도·checkout은
하지 않습니다. char[]/byte[]/직접 unmanaged buffer는 정리하지만 CLR/JVM/Python/OS의
불변 문자열과 환경 사본을 완전히 지웠다고 보장하지 않습니다.

## 총괄이 준비할 비공개 metadata

이 폴더에는 실사용 binding 생성기가 없습니다. 총괄은 코드 반영 후 **그 actual
build HEAD**로 새 plan/source-review를 만들고 독립 근거를 확정해야 합니다.
예전 helper의 15필드 binding이나 과거 PASS를 새 binding으로 재사용하지 않습니다.

`LauncherBinding`/`producer_bridge.binding()`이 동일한 schema 1 필드를 요구합니다.
주요 필드는 checkout/head/branch, plan·SHA, sourceReview·SHA, certificate·SHA/alias,
`currentUploadCertificateReviewed`, `existingKeyOwnershipReviewed`, python/git/powershell/
java/classDirectory/bridge, deadlineUtc/minimumStartBudgetSeconds(최소600)/
producerTimeoutSeconds, runtimeFiles/absentRuntimeFiles, pins입니다.
독립 검토 두 필드에는 실제 근거가 있을 때만 true를 넣습니다. source review의
세 완료 항목은 기존 producer가 따로 검증합니다.

각 pin은 canonical absolute `path`, SHA-256, Windows handle identity
`volume:indexHigh:indexLow:sizeHigh:sizeLow:writeHigh:writeLow`(각8자리 hex),
Owner|Group|Access SDDL을 갖습니다. gate가 요구하는 exact immutable scope와
일치해야 합니다. key는 checkout 원본으로 고정되며 external copy 결과를 전용하지
않습니다. source manifest에는 아래6개 source의 SHA만 넣고 exact bytes SHA를 별도로
검토합니다: `OwnedChild.cs`, `SigningLauncher.cs`, `OperatingKeyPreflight.java`,
`producer_bridge.py`, `Invoke-SigningLauncher.ps1`, `EditorOwnership.ps1`.

향후 총괄의 명시 실행 형태는 다음과 같습니다. placeholder는 실행 가능한 승인값이 아닙니다.

```powershell
pwsh -NoProfile -STA -File <검토된 Invoke-SigningLauncher.ps1> `
  -Binding <private binding> -BindingSha256 <exact sha256> `
  -ReviewedSources <private source manifest> -ReviewedSourcesSha256 <exact sha256> `
  -AllowReviewedBuild
```

## 합성 검증

모든 출력 디렉터리는 checkout 밖의 총괄 지정 private evidence 영역을 사용합니다.
`tests/Test-Native.ps1`은 새 source compile/AST와 synthetic child 세대 수명·Job 생성
거부·stdin/env·진단 비노출을 검사합니다. `-Delta`는 failure/startup/prompt 경계만
검사합니다. `tests/test_bridge.py`는 framing·source-only loader·실패 시 receipt/producer0·
exact 귀속 순서·Python 출력 폐기를 검사합니다. `Test-Bootstrap.ps1`은 hash/parse 사이
metadata/source 교체 거부와 정확 source compile만 확인합니다.
`Test-EditorOwnership.ps1`은 synthetic process metadata만 사용합니다.
`Test-BridgeJava.ps1`은 복사된 worker를 지정 evidence에 컴파일하고 invalid synthetic
JKS로 소유 Python→실제 Java 실패 경로와 producer/receipt0을 확인합니다. 실제 키·DER·
private config·암호창·Editor·기기·운영 API·실제 빌드를 호출하지 않습니다.

Job 원자 배정 근거: [Microsoft JOB_LIST 설명](https://devblogs.microsoft.com/oldnewthing/20230209-00/?p=107812/).
Python startup 범위: [공식 sys.path 초기화 문서](https://docs.python.org/3.12/library/sys_path_init.html).
