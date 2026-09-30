# 비공개 광고 단위 준비 도구와 합성 주입 검증


## 최신 검증 범위 — 2026-09-30

검증 대상은 `codex/private-ads-build-injection`의
`66510b41d5699cfa2db836103faee72edec8cafa`다. Unity `6000.3.25f1`·revision
`e1dba0a9aba4`·CLI `1.0.0-beta.8`에서 승인된 최소 emit1회와 다섯 번째 합성 호출1회가
통과했다. 실제 asset import된 합성 콜백의 pre/scene/post 완료 및 공통 주입1개와
고정 공개 테스트 App ID·격리 package·min25/target36·ARM64·기존 Debug 인증서의
AAB 검증을 확인했다. 이는 비공개 운영 값의 실제 빌드 주입·운영 콜백 계약·최종
직렬화 광고 값/Player gate·배포 가능성의 증거가 아니다.
`productionContractVerified`·`binaryVerified`·`distributable`은 모두 false다.

병합 평가 대상은 **생산 실행이 차단된 준비 도구와 감독하의 합성 검증 도구**다.
`private_ads_build.py --execute`의 무조건 거절과 production/regional/age gate OFF를
유지한다. 운영 key/ID·광고·기기·계정·저장·구매 작업은0이다. 공통 씬 주입은 합성
콜백에서 검증됐지만 실제 production callback은 별도 미완료다. 자동 복구가 완성된
도구로 표현하지 않는다. shipped `recover-reviewed`는 이번 canonical 변경10을
허용하지 않으며 Git 정규화 diff8와 실제 바이트 변동10 차이도 고려하지 못하므로,
이번에는 별도 고정 계획·독립 검토·같은 핸들의 정확10 수동 복원 절차를 사용했다.
원 증거와 스냅샷 보존 및 사용자 승인 후의 1회 실행/실패 중단 규칙을 유지한다.
아래 최초 초안·실패·교체 기록은 당시 근거로 보존하며 최신 결과와 구분한다.

2026-09-29, 기준 커밋 `28a5493a405d9863ba433564c8ef5ff51f5504ac`.
구현 checkout은 `C:\Users\pc_17\.codex\worktrees\e5e5\Tamer`,
작업 브랜치는 `codex/private-ads-build-injection`이다. 아래 결과는 이 기준에
미커밋 도구 초안을 추가한 상태의 검증이며 최신 main 또는 Unity 검증 결과가 아니다.

## 최초 초안 상태와 당시 근거

`tools/revival/private_ads_build.py`의 `--execute`는 무조건 거절한다.
최초 C# 계약 검증 두 번 실패 후 중단했고, 사용자의 명시적 승인과 독립 읽기
리뷰를 거쳐 세 번째 합성 검증을 한 번 실행하여 통과했다. Windows 핸들 정리도
독립 리뷰 후 임시 폴더 전용 6개 검증을 통과했다. 실제 실행 경로는
`036fe8f` 소스의 Editor 참조 Roslyn emit-only 컴파일도 한 번 통과했다. 이후 아래의
Editor/Player define 구분 수정 `e9a86c4`도 별도 emit-only 컴파일 한 번을 통과했다.
정상 Unity asset/asmdef
컴파일·콜백 등록/호출·최종 바이너리·보존 폴더의 수동 확인이 남아 계속 차단한다.
이 초안은 병합·실행 준비 완료 상태가 아니다.

- 첫 번째: .NET 표준 출력의 UTF-8 한글을 Python 기본 cp949로 읽다가 실패했다.
  C# 입력 계약 결과는 확인하지 못했다.
- 두 번째: 출력 인코딩 수정 후 순수 C# 템플릿은 .NET으로 컴파일되었으나,
  `JsonReaderWriterFactory` 기반 파서가 후행 객체와 마지막 쉼표를 허용했다.
  Python의 엄격한 JSON 계약과 불일치하여 실패했다. 아래 승인된 교체로 보강했다.
- 별도 Python 합성 lifecycle 9개는 첫 실행에서 통과했다. 소유 파일 정리,
  강제종료를 모사한 잔여 journal 차단, meta 변조·미소유 파일 보존,
  원본 변경 보존/스냅샷 유지, 부분 staging 실패 복구, journal 변조,
  경로 이탈, 설정 digest 변경 거절을 포함한다.
- 독립 읽기 보안 리뷰 후 실제 배타 생성에 성공한 파일/폴더 identity를 별도로
  기록하고, 삭제 직전 identity/해시 재대조 및 최종 branch 일치를 추가했다.
  동일 바이트 타인 파일/타인 빈 폴더 보존과 `--execute` 차단을 추가한 Python
  lifecycle **12개가 통과**했다. 이 실행에는 중단된 C# 계약 검증을 포함하지 않았다.

초기 구현·합성 검증 단계에서는 Unity Editor/CLI 실행, 실제 Assets hook 설치, APK/AAB 생성, 기기 접근,
서명 변경, 실제 광고 요청, Console 제출은 수행하지 않았다.
후속 Editor emit-only 검증은 문서 마지막 절의 별도 결과다. 씬 콜백·최종 바이너리는 미검증이다.
초기 단계의 버전 지침 충돌은 자체 판단으로 변경하지 않았다. 후속 컴파일은 총괄 배정에 따라
설치된 버전과 프로젝트 기준 일치를 확인했으며 새 Editor 설치/전환은 없다.

## 구현 경계

템플릿 두 파일은 `tools/revival/private_ads_build/`에 있어 Unity가 자동 컴파일하지 않는다.
래퍼의 기본 동작은 명시한 절대 checkout/설정 경로, 예상 HEAD, Editor 버전,
clean Git 상태와 기존 비활성 승인 계약의 읽기 전용 검사다.
설정 값은 private 파일에 유지하고 출력/예외 메시지에는 넣지 않는다.

미완성 실행 초안은 자기 checkout의 Editor가 닫힌 상태에서만 소유 journal을
배타 생성한 뒤, 고정된 `Assets/Scripts/Editor/RevivalPrivateAdsPreparation` 경로와
meta를 설치하도록 작성했다. 다른 프로젝트의 Editor를 종료하거나 원본 checkout을
변경하지 않는다. 자식 환경에만 opt-in을 전달하고 CLI 출력/Editor 로그는 private
실행 폴더로 보낸다. `--allow-dirty-build`는 설치한 hook 때문에 필요하며 실행 직전
추가 변경을 재검사한다. 버전은 명시값과 ProjectVersion 일치를 요구하고 자동 설치하지 않는다.

씬·프리팹·코드·ProjectSettings·Packages 등의 원본 바이트/해시를 private 실행 폴더에
보관한다. hook은 build-scene Login의 manager 정확 한 개만 수정하며 원본 저장,
프리팹 적용, 설정/서명 변경 코드는 없다. 기존 rewarded/No Ads와 production/regional/
연령별 승인 플래그는 그대로 OFF다. AAB 설정과 서명 준비는 별도 승인된 소유 절차가
먼저 완료해야 한다. hook 자체는 해당 설정을 바꾸지 않는다.

`finally`는 자기 소유 해시와 일치하는 hook/meta만 제거한다. 원본 변경이나 예상 밖
파일이 생기면 덮어쓰지 않고 journal/스냅샷을 보존한다. 강제종료로 finally가 생략되면
다음 실행은 journal/hook 잔여물로 중단한다. 이는 동시 사용자 편집을 막는 OS 잠금이
아니므로 실제 실행 시 해당 checkout의 독점 사용도 필요하다.
초기 경로 재대조/unlink 구현의 경합은 아래 Windows handle 정리 구현으로 보강했다.
폴더 소유권을 mkdir 직후 조회만으로 증명하지 않고 자동 rmdir를 제거했다.
남은 폴더/active journal은 수동 확인 대상으로 보존하며 실행 차단을 유지한다.
Git ignore는 OS ACL/백업 동기화 접근 제한을 제공하지 않는다.

## 수동 복구 절차

1. 해당 checkout의 Editor가 닫혔고 다른 담당자가 사용하지 않는지 확인한다.
2. `.revival-local/private-ads-build-active.json`의 checkout/HEAD/runId와 소유 목록을
   확인한다. JSON 손상·소유 불명 상태에서는 자동 정리를 실행하지 않는다.
3. journal의 owned 파일을 실제 파일 해시와 대조한다. 일치하는 자기 hook/meta만
   제거하고 폴더는 비었을 때만 제거한다. 해시 불일치·목록 밖 파일은 보존한다.
4. sources 원본 해시와 각 private snapshot을 현재 파일과 비교한다. 동시 변경의
   소유자를 확인하기 전에는 원본을 덮어쓰지 않는다. 복구 승인 시에만 개별 파일을 복원한다.
5. Git HEAD/브랜치/변경 상태와 원본 복구를 확인한 뒤 journal을 해당 run 폴더로
   보존 이동한다. 스냅샷·로그·Library·다른 worktree는 삭제하지 않는다.

## 결과 해석

hook 영수증의 `buildSceneValueMatched`와 `injectedManagers`는 콜백 결과다.
`configuredAndroidDefines`는 Android 설정의 define이며 실제 player 컴파일 define의
증거가 아니다. `compiledEditorGatesDisabled`도 Editor 어셈블리 범위다.
최종 AAB 직렬화 단위, merged manifest, player define/금지 harness 포함 여부,
player gate, 서명/ABI 검증은 별도 필요하다. 이 초안은 이를 구현했다고 주장하지 않으며
모든 결과에서 `binaryVerified=false`, `distributable=false`를 유지한다.

실제 산출물이 없어 APK/AAB SHA-256 및 Android 도구/기기 결과는 없다.
합성 검증 환경은 Python 3.14, .NET SDK 9.0.301이며 Unity 버전/CLI 버전 결과를
대체하지 않는다. 세 번째 C# 계약 검증 승인 전에는 해당 테스트를 이름을 바꾸거나
동등한 입력으로 다시 실행하지 않는 중단 규칙을 지켰고, 아래 명시적 승인 후
세 번째 한 번만 실행했다. 변경 없는 재실행은 하지 않는다.

## 승인된 파서 교체와 세 번째 검증

검증 코드 집합은 `099607b4aeb88b1671a3174b1b3222fc10ba983e`에 기록했다.
실행 당시에는 `16ce8e5` 위에 아래 세 파일의 미커밋 변경이 있었고, 통과 후
그대로 해당 커밋에 기록했다. 당시 후속 `898922c`의 wrapper 변경은 차단 메시지/
주석만 갱신했으며, 별도 Windows 정리 보강은 아래 `e4d833e` 결과와 구분한다.

- `tools/revival/private_ads_build/PrivateAdsContract.cs`
- `tools/revival/validate_private_ads_preparation.py`
- `tools/revival/test_private_ads_build.py`

독립 보안 담당자가 위 세 파일을 읽기 검토한 뒤, 작성자가
`python -m unittest test_private_ads_build.CSharpContractTests -v`를 **한 번** 실행했다.
종료 코드 **0**, 테스트 **1개 통과**, 합성 입력 **59개 판정 일치(허용 5/거절 54)**다.
검증은 순수 C# 계약 템플릿을 .NET SDK 9.0.301로 컴파일하고 프로젝트에 이미 있는
Newtonsoft 3.2.2 Runtime DLL을 참조했다. 새 패키지 설치는 없다.
중복 decoded 키(루트/중첩/escaped), 후행 객체, 객체/배열 마지막 쉼표,
잘못된 bool 타입/누락/값, 중첩·문자열 키 위장, 주석·숫자 확장 문법을 포함한다.
기존 Python lifecycle 검증은 반복하지 않았다. 최초 두 실패는 위 이력에 보존한다.

이 결과는 **해당 59개 입력의 판정 일치**이며 파서 전체 동치를 입증하지 않는다.
C#의 1MB/깊이64 제한은 Python에 동일하게 적용되어 있지 않고 BOM·극단 수치·
Unicode·경로 정규화 전 범위도 검증하지 않았다. Unity 콜백 코드는 컴파일/실행하지
않았고 실제 AAB/기기/운영 광고/Console 설정 작업은 없다.

프로젝트 lock에는 `com.unity.nuget.newtonsoft-json` **3.2.2**가 이미 있으며
현재 PackageCache의 Runtime Newtonsoft.Json DLL/XML 문서도 존재한다.
새 패키지 추가나 PlayFab SDK의 파서 변경은 필요하지 않다.
PlayFab `SimpleJson.TryDeserializeObject`는 ParseValue 이후 전체 소비를 확인하지
않고, ParseObject는 같은 키에 재대입하므로 이 계약의 대체로 사용하지 않는다.

교체한 구현은 기존 Newtonsoft의 JObject 구조/타입 검사와
`JsonLoadSettings.DuplicatePropertyNameHandling = Error`를 사용하여 중첩을 포함한
decoded 키 중복을 거절하는 것이다. 루트의 실제 Boolean/String 타입을 확인하고
명시한 세 bool 값 및 checkout/동일 게시자 규칙을 유지한다.
Newtonsoft도 확장 JSON 문법을 받아들이므로 단독 교체만으로 엄격함을 주장하지 않는다.
그 앞에 깊이/입력 크기 제한을 둔 작은 JSON 문법 검사기를 두어 다음을 요구한다.

- 객체/배열의 쉼표 뒤에는 반드시 다음 멤버/값이 있고 최종 닫는 괄호 뒤에는
  JSON 공백(공백, 탭, CR, LF)과 EOF만 존재한다.
- 키/문자열은 이중 따옴표와 JSON escape만 허용하며 제어문자, 주석, 작은따옴표,
  bare name을 거절한다. 숫자는 JSON number 문법만 허용하고 NaN/Infinity는 거절한다.
- 문자열 해석과 객체 생성은 기존 Newtonsoft에 맡기고, 문법 검사에서 값이나
  식별자를 오류 문자열에 포함하지 않는다. Newtonsoft 파서 예외도 generic 처리한다.

Python도 `parse_constant` 거절을 명시하여 미사용 evidence 필드 안의
NaN/Infinity까지 거절하도록 보강했고 이번 합성 입력에 포함했다.
승인 범위인 **교체 후 같은 C#↔Python 계약 검증 한 번**을 완료했다.
새 Unity 실행·AAB 빌드·기기 검증은 승인 범위에 포함하지 않는다.

## Windows 소유 파일 정리 보강

`windows_owned_files.py`는 volume root부터 각 ancestor 디렉터리를 열린 핸들로
유지하고 DIRECTORY/REPARSE 속성을 확인한다. share READ만 허용하여 해당
디렉터리 자체의 일반적인 쓰기/rename/delete 핸들과 충돌하면 중단한다.
자식 파일의 생성 전체를 막는 독점 디렉터리 잠금이라고 표현하지 않는다.

소유 hook/meta와 journal은 `GENERIC_READ | DELETE`, share READ,
`OPEN_EXISTING | FILE_FLAG_OPEN_REPARSE_POINT`로 열고 같은 `os.fstat` 표현의
identity와 같은 핸들로 읽은 바이트 해시를 비교한다. 전부 확보/검증하기 전에는
삭제하지 않으며 `SetFileInformationByHandle(FileDispositionInfo)`로 검증한 핸들의
파일만 삭제한다. pathname unlink/rmdir, 공유 위반 재시도/강제종료/fallback은 없다.
이 공유 모드와 삭제 접근권한은 Microsoft의 [CreateFile 문서](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew)와
[SetFileInformationByHandle 문서](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle)에 근거한다.

receipt와 journal 사본은 exclusive-create로 예약해 기존 파일을 덮어쓰지 않는다.
여러 파일 삭제는 transaction이 아니므로 중간 실패 시 일부 자기 파일만 제거될 수
있으며 원래 active journal은 보존한다. 실패 때 생긴 빈/부분 receipt는 성공 증거가
아니다. 핸들은 예외에서도 닫는다. 자기 파일 정리 후에도 폴더는 자동 삭제하지 않아
`manualRecoveryRequired=true`와 active journal을 유지한다. 실행 초안도 이 상태를
성공 완료로 반환하지 않고 수동 확인 필요 오류로 종료한다.

보장 범위는 일반 사용자 파일 I/O 경합이다. 관리자/커널/원시 볼륨 접근, 기존 writable
mapping, stage와 cleanup 사이 파일 ID 재사용까지 전면 방어한다고 주장하지 않는다.
재파싱 경로는 거절한다. 실제 프로젝트 파일 삭제로 검증하지 않으며 새 검증은 Windows
임시 폴더에만 한정한다. 기존 lifecycle 12개 PASS는 `16ce8e5`의 이전 정리 구현
결과이고 이번 결과로 재표현하지 않는다. 기존 테스트의 폴더 보존 기대값은 갱신했지만
반복 실행하지 않았다. JSON 59개 판정 검증도 반복하지 않았다.

### 변경된 정리 경로 검증

검증 코드 집합은 `e4d833e9f91126d31786afe6d4c95b50e321998b`다.
실행 당시 `898922c` 위에 해당 커밋의 미커밋 코드 변경이 있었고, 실행 후 그대로
기록했다. 독립 읽기 리뷰의 `FILE_DISPOSITION_INFO.DeleteFile` 지적에 따라
`ctypes.Structure`의 **c_ubyte(1바이트 BOOLEAN)**로 수정한 뒤,
`python -m unittest test_windows_owned_files -v`를 한 번 실행했다.
종료 코드 **0**, **6개 통과**, 실패/재시도는 없다.

- 잠금 중 파일 쓰기·교체와 부모 디렉터리 rename 거절, 같은 핸들 대상 삭제
- 내용은 같아도 교체된 다른 identity의 파일 거절
- own 파일 정리 뒤 빈 폴더·active journal·원본 보존
- 기존 writer의 공유 위반 시 fallback 없이 모든 대상 보존
- 두 번째 삭제 표식에서 합성 실패 시 일부 own 파일만 정리하고 journal 보존/핸들 해제
- 기존 결과 파일을 덮어쓰지 않음

Python 3.14/Windows 로컬 임시 폴더에서만 수행했다. 실제 checkout의 hook/meta/
journal 삭제나 Unity/기기/빌드 작업은 하지 않았다. 최초 lifecycle 12개와
승인된 JSON 59개 검증을 이 단계에서 반복하지 않았다.

검증 후 독립 읽기 리뷰에서 `e4d833e`의 실제 1바이트 ABI 수정과 시험 범위가
확인되었고 추가 차단 사항은 발견되지 않았다. 리뷰 담당자는 테스트를 실행하지
않았으며 6 OK/exit0 수치는 작성자 실행 보고와 이 문서에 근거했다. 원 실행 로그를
독립 재열람하여 확인한 결과라고 표현하지 않는다.

## Editor 참조 Roslyn emit-only 컴파일 1회

2026-09-29 실제 소스는 `036fe8ff37359903bcb43ed2c96500783e178197`,
checkout은 본 문서 상단의 e5e5, 브랜치는 `codex/private-ads-build-injection`이다.
실행 전 tracked clean 및 해당 checkout Editor 0을 확인했다.
설치된 Unity **6000.3.25f1 / e1dba0a9aba4**와 ProjectVersion/toolchain,
CLI **1.0.0-beta.8**의 고정 SHA-256을 대조했다. 기존 비공개 에셋 복원 승인 자료가
유지되고 추가 충돌/누락이 없음을 확인했으며 새 설치나 버전 전환은 없었다.
활성 라이선스를 확인했고 누락된 이 checkout의 계정 pin만 기존 로그인 계정으로
설정했다. 다른 프로젝트나 전역 활성 계정은 바꾸지 않았다.

프로젝트에 설치된 unity-pipeline 스킬과 `RunScriptCommand.cs`/
`RoslynCompilationService.cs`를 읽어 `dry_run`이 emit 후 어셈블리를 로드하지 않고
entry 탐색/호출 전에 반환하는 경로임을 확인했다. 통합/보안 독립 읽기 리뷰 후
아래 두 템플릿을 Assets 밖 private 결합 파일로 준비했다.

- `tools/revival/private_ads_build/PrivateAdsContract.cs`
- `tools/revival/private_ads_build/PrivateProductionAdsBuild.cs`

단순 상단 using 선언만 합쳤고 클래스 본문은 보존했다. 이번 두 실제 소스에
conditional/alias/late using이 없음을 읽기 확인했고 원본 SHA-256, using 치환 후 본문 해시, `#line` 매핑을 private
manifest에 기록했다. 원본 1,089개 파일의 스냅샷도 보존했다. 새 템플릿은 Assets에
설치하지 않았고 meta 생성이나 소유 파일 삭제는 **0**이다.

명령은 고정 절대 checkout에 대해 `unity run <checkout> --editor-version 6000.3.25f1
--command run_script --timeout 900 --non-interactive --format json -- --file <private combined.cs>
--mode ephemeral --dry_run true`였다. 실행 직전 CLI 해시/정확한 명령/소스와 원본
스냅샷 해시를 재검사하고, 자식 환경에서 모든 `TAMER_PRIVATE_ADS_*`를 대소문자와
무관하게 제거했다. 고유 nonce와 exclusive 생성 표식으로 같은 실행의 재호출을 막았다.

결과는 **CLI exit 0 및 실제 command success=true**, compile **1,033ms**,
execute **0ms**, assemblyName **null**, 명시적 dry-run 미로드/미실행 응답이다.
진단은 **오류 0, CS0162 unreachable-code 경고 1**이다. Editor 활성 define을 사용한
테스트/하네스 거절 분기 주변의 경고이며 경고를 숨기거나 새 컴파일로 없애지 않았다.
`reusedRunningEditor=false`였고 종료 후 해당 checkout Editor 0을 확인했다.
보안 리뷰 담당자가 보존된 원 응답과 receipt를 직접 읽어 위 결과를 확인했다.

이 결과는 **실행 중 Editor의 참조·define을 사용한 Roslyn emit-only 컴파일**이다.
Unity의 정상 asset/asmdef 컴파일 파이프라인 편입, 콜백 등록/호출, 씬 주입,
APK/AAB 생성·서명·실기기·운영 광고 성공의 증거가 아니다. `--execute` 차단은 유지한다.

### Editor 초기화 변경과 제한된 복원

새 소스의 미로드와 별개로 Editor 초기화/import가 기존 tracked 파일 세 개를 수정했다.
공백을 제외한 변경은 ProjectSettings의 `AndroidKeyaliasName`과 URP Low/Medium의
`k_AssetPreviousVersion`이었다. 설정 값과 원시 로그는 공개하지 않고 변경 후 바이트를
private 증거로 보존했다. 처음부터 변경 없는 실행이었다고 표현하지 않는다.

독립 읽기 리뷰에서 실행 전 clean/HEAD, 현재 after-import 해시, index=HEAD,
Settings 스냅샷 해시를 확인했다. 복원 직전에 Editor 0/HEAD/현재 해시를 다시 대조한 뒤
**ProjectSettings 한 개는 사전 스냅샷 바이트로**, **URP 두 개는 정확한 경로의 Git HEAD
기준으로** 복원했다. Settings는 배타 파일 핸들 안에서 현재 해시를 확인하고 복원했다.
URP 두 파일은 사전 바이트 스냅샷 목록에 없으므로 exact 사전 바이트 복원이라고
주장하지 않는다. 전체 restore/reset은 하지 않았다.

사후 **Git clean**, Settings 스냅샷 해시 일치, 해당 Editor 0을 확인했다.
컴파일/Editor import를 다시 실행하지 않았고 private manifest·응답·변경 전후 증거·
복원 receipt를 보존했다. 이 단계의 추가 빌드/기기/Console/운영 ID 주입은 0이다.

## 후속 최소 수정: Editor와 Player define 구분

`036fe8f` emit-only의 unreachable 경고는 Editor에 적용된 테스트 define 분기 뒤에서
나왔다. Editor의 `UNITY_INCLUDE_TESTS`만으로 Player 빌드까지 무조건 거절하지 않도록
그 심볼을 **Editor용 #if에서만** 제거했다. 기존 Editor TAMER 하네스 거절은 유지한다.

Android `PlayerSettings.GetScriptingDefineSymbols`의 테스트/하네스 심볼 거절과
`BuildOptions.None` 요구는 바꾸지 않는다. 개발 빌드/IncludeTestAssemblies 등 다른
옵션을 허용하지 않는다. 추가로
[`CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies)`](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/compilation/assembliestype/playerwithouttestassemblies)
의 Player 계획에서 다음을 확인한다.

- Android 활성 target 및 비개발 설정, 비어 있지 않은 어셈블리 목록
- 각 어셈블리의 UNITY_ANDROID 포함, UNITY_EDITOR와 기존 TEST/HARNESS/TAMER/
  DEVELOPMENT_BUILD 금지 심볼 없음, 중복 어셈블리명 없음
- GoogleAdMobManager/AdRequestPolicy/AgeTreatmentPolicy 소스가 이 checkout의 정규화된
  절대 경로와 정확히 일치하여 각 한 번 포함됨(Windows 대소문자 무시, 그 외 구분)

정렬한 어셈블리별 define 계획을 불변 snapshot에 보존하고 각 콜백에서 다시 대조한다.
receipt에는 `prospectivePlayerDefinePlanSha256`로 기록하여 최종 Player 어셈블리나
바이너리 검증과 구분한다. 이 추가 검사는 잘못된/누락된 Player 계획을 거절하며
실제 컴파일 성공을 가정하지 않는다. 새 패키지·flags 활성화·시험용 우회는 없다.
이 수정의 독립 읽기 리뷰 직후에는 새 실행을 하지 않았으며, 아래 별도 배정된
emit-only 검증을 수행했다. 정상 assembly 등록/합성 AAB 빌드는 수행하지 않았다.
앞 절 `036fe8f`의 결과를 이 변경된 콜백 소스의 결과로 재표현하지 않는다.
계획 SHA는 어셈블리명과 define만 고정하며 전체 source/reference graph의 고정이나
최종 Player gate의 컴파일 결과를 입증하지 않는다.

## 변경된 `e9a86c4` 소스의 emit-only 컴파일

총괄의 후속 배정에 따라 `e9a86c4292b74327d82d0695c75882800762962b`의
두 템플릿에 대해 검토된 private 외부 결합/run_script 경로를 재사용했다.
이전 소스를 변경 없이 반복한 시험이 아니라 Editor/Player define 수정 소스의
**첫 실행 1회**다. using/소스/CLI/명령/checkout 해시를 새로 대조하고 이번에는
URP 두 파일까지 포함한 원본 **1,091개** 바이트 스냅샷을 확보했다.
동일 Unity 6000.3.25f1/e1dba0a9aba4, CLI 1.0.0-beta.8을 사용했다.

실제 응답은 **CLI exit 0 / command success=true / diagnostics=[]**,
compile **1,246ms**, execute **0ms**, assemblyName **null**이다.
명시적 dry-run 미로드/미실행 문구가 있으며 **오류 0, 경고 0**이다.
이전 CS0162를 숨기지 않았고 새 소스의 해당 emit-only 진단에는 나오지 않았다.
보안 담당자가 원 응답을 직접 읽어 확인했다.

Editor 초기화가 이전과 같은 ProjectSettings/URP Low/Medium 세 파일을 변경했다.
변경 후 바이트를 보존하고 보안 독립 읽기 확인 후, Editor 0/HEAD/현재 after 해시를
다시 대조하여 세 파일 모두 배타 핸들 안에서 **각 사전 스냅샷 바이트**로 복원했다.
이번에는 URP도 사전 스냅샷이 있으므로 세 해시 모두 원본과 일치함을 확인했다.
사후 **Git clean / 해당 Editor 0**, 추가 컴파일/Editor 재실행 0이다.

Assets 임시 설치/삭제, 콜백 실행, 빌드/설치/기기/운영 광고/Console 작업은 0이다.
특히 `ReadPlayerCompilationPlan`은 컴파일되었을 뿐 **실제 API를 호출한 결과가 아니다**.
정상 Unity assembly 편입·콜백 등록과 최종 Player/AAB 검증은 여전히 남아 있으며
`--execute` 차단과 Draft/병합 보류를 유지한다.

## Player 계획 읽기 probe 첫 실행 — 실패

checkout HEAD `ddeb01ec8943eaa15b099948af69e1ffa4f08031`에서 콜백 템플릿의
ReadPlayerCompilationPlan/ForbiddenDefine/Rejected 본문을 그대로 복사한 private
외부 probe를 독립 읽기 리뷰 후 한 번 실행했다. 원본 템플릿/본문 해시와 실행 파일의
raw 해시를 고정했다. 준비 과정의 LF 문자열 해시와 Windows CRLF 파일 해시 차이는
실행 전에 명시적으로 구분·정정했고, 검토한 본문은 바꾸지 않았다.

probe는 callback 인터페이스나 Build 메서드를 포함하지 않는다. 이 단계는 emit-only와
달리 probe 어셈블리를 로드하고 읽기 메서드 Inspect를 호출했다. Assets 설치/삭제,
설정 쓰기, 콜백 템플릿 로드/등록, 빌드/기기 실행은 없다.

**CLI exit 0이지만 실제 inner command success=false / Runtime Error이므로 실패**다.
compile 1,145ms, 진단 0, execute 39ms이며 ReadPlayerCompilationPlan 내부의
일반 거절 예외로 끝났다. 스택 64행은 메서드 닫는 괄호이므로 특정 검사 분기나
앞단 조건 통과를 입증하지 않는다. 처음 소스 count 실패로 추정한 보고는 즉시
정정했고 **실제 거절 조건은 미확정**으로 남긴다.

세 핵심 소스는 실제 checkout에 존재한다. 기존 Bee rsp는 이전 9월 28일 Editor/
하네스 빌드 자료라 이번 API의 반환값이나 실패 이유를 증명하지 않는다.
시험을 다시 실행하거나 상태 조건을 완화하여 통과시키지 않았다.

초기화로 변경된 같은 세 설정 파일은 원시 after 증거를 보존하고 독립 읽기 검토 후,
Editor 0/현재 해시/HEAD를 확인해 세 사전 스냅샷으로 배타 복원했다.
사후 세 해시 일치/Git clean/Editor 0, 이번 probe 호출 **1회·재시도 0**이다.

후속 진단 준비는 조건을 바꾸지 않고 아래 고정 코드만 추가한다. 실제 경로·심볼·
식별값을 예외에 넣지 않으며 예상 밖 API/경로 예외도 P11로 숨긴다.
자체 private 예외형만 고정 코드를 보존하고 API의 BuildFailedException을 포함한
나머지 예외는 P11로 바꾼다. 실제 Build/Guard의 외곽 generic 처리는 여전히 일반
실패만 반환하므로 고정 reason 관측은 이 메서드를 직접 부르는 읽기 probe 범위다.

| 코드 | 거절 조건 |
| --- | --- |
| P01 | Android 활성 target 아님 |
| P02 | development 설정 켜짐 |
| P03 | Player 목록 누락/비어 있음 |
| P04 | 어셈블리 메타데이터 누락/잘못된 항목 |
| P05 | Android define 누락 또는 Editor define 포함 |
| P06 | 빈 define 또는 기존 금지 테스트/하네스/개발 define 포함 |
| P07 | 어셈블리명 중복 |
| P08/P09/P10 | Manager/요청 정책/연령 정책의 정확한 소스 집계가 1 아님 |
| P11 | 예상 밖 API 또는 경로 처리 예외 |

이 진단 수정은 독립 읽기 리뷰 후 아래 두 번째 배정에서 실행했다.

## Player 계획 두 번째 실패 — P06, 재시도 중단

총괄이 배정한 두 번째 1회는 `fbca8054c0dea14c94141f5fd75b96ce77f6c320`의
고정 코드/전용 예외형까지 그대로 복사하고 raw hash를 고정한 probe로 수행했다.
원래 검사 조건은 완화하지 않았다. **inner command success=false, P06**으로
다시 실패했다. CLI exit 0을 성공으로 취급하지 않는다.
compile 1,244ms, diagnostics=[], execute 37ms이며 읽기 probe 어셈블리는 로드됐다.

확인된 거절 범주는 Player 어셈블리 define의 **null/empty 또는 금지된 TEST/HARNESS/
TAMER/DEVELOPMENT 심볼**이다. P06에서 정확한 심볼이나 어셈블리를 반환하지 않아
어느 항목인지까지 확인되지는 않았다. 첫 실행의 구체 원인도 소급 확정하지 않는다.

동일 검증이 두 번 실패했으므로 **세 번째 조회, 이름을 바꾼 동등한 probe,
정상 콜백 등록/합성 빌드 등 이 결과에 의존하는 작업을 중단**했다.
조건을 삭제하거나 설정을 바꿔 통과시키지 않았다. 다음 조회는 사용자 승인 전
실행하지 않는다. 이전 JSON 세 번째 성공/Windows 정리 성공/emit-only 성공은
이 Player 계획 API 성공을 대체하지 않는다.

두 번째 실행의 초기화 변경 세 파일도 독립 읽기 확인 후 Editor 0/현재 after 해시를
대조하여 각 사전 스냅샷으로 배타 복원했다. 세 해시는 일치하며 해당 Editor는 종료됐다.
복원 직후에는 이 실패 기록 문서만 수정 상태였고 실제 프로젝트 설정 변경은 남지 않았다.
원 응답과 전후 증거를 보존했으며 API/컴파일/빌드 추가 실행은 없다.

### 승인 전 준비한 세 번째 진단 범위

공식 `PlayerWithoutTestAssemblies` 문서는 테스트 어셈블리를 제외하는 필터를 설명할
뿐 반환되는 모든 define에 TEST 문자열이 없다는 보장은 하지 않는다.
Unity의 공개 [UnityEngine 프로젝트 정의](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Projects/CSharp/UnityEngine.csproj)에는
`ENABLE_MARSHALLING_TESTS`도 존재한다. 로컬의 **과거** rsp에도 이 심볼이 있지만
현재 P06의 원인으로 확정하거나 이를 허용하도록 보호 조건을 바꾸지는 않았다.
`UNITY_INCLUDE_TESTS`가 이번 반환값에 있었는지도 아직 확인하지 못했다.

필요한 세 번째 범위는 기존 조건과 단일 API 조회를 유지한 채 P06에서만
빈 항목/UNITY_INCLUDE_TESTS/DEVELOPMENT_BUILD/TAMER/기타 TEST/기타 HARNESS의
고정 분류와 개수를 수집하는 읽기 진단 한 번이다. 필요하면 원문 대신 심볼/어셈블리
SHA-256과 핵심 세 소스의 역할 코드만 기록하여 후속 읽기 대조에 사용한다.
raw define/전체 경로/광고 값은 공개 출력하지 않는다. 같은 조회 결과로 기존 거절
판정도 함께 수행하고 성공으로 바꾸지 않는다. 이 계획은 아직 실행하지 않았으며,
사용자 승인 전에는 컴파일을 포함한 동등한 진단 호출을 하지 않는다.


## 사용자 승인 제3차 분류 진단 — 분류 완료, 원계약 P06 유지

2026-09-30 사용자가 총괄의 일괄 질문에 승인한 **제3차 단일 진단**을
`C:\Users\pc_17\.codex\worktrees\e5e5\Tamer`,
`codex/private-ads-build-injection`, HEAD
`3f4ad862b9a87724341d6bbfe03b7ee0347c45ee`의 clean 상태에서 수행했다.
Unity `6000.3.25f1`/revision `e1dba0a9aba4`, CLI `1.0.0-beta.8`의 기존
고정 SHA-256, 활성 라이선스와 checkout 계정 pin을 실행 전에 확인했다.
Android 기준은 min25/target36/ARM64 그대로이며, 실제 Player 빌드는 실행하지 않았다.

production 템플릿은 수정하지 않았다. Assets 밖 private probe의 기존 P01~P11
검사와 순서를 유지하고, P06이 성립하면 **같은 단일 GetAssemblies 반환값**을
분류한 뒤 같은 거절을 발생시켰다. Inspect는 자체 고정 예외만 받아 거절 여부와
고정 분류 개수를 반환했다. 원문 plan/define/어셈블리명/경로/광고 값은 반환하지
않았다. probe raw SHA-256은
`ff1f16c8930ba7b74af6091c80374ff013b3d2430832b734e2ff0ea80bf953e0`이며,
source-map·복사본·runner·1,091개 사전 snapshot을 고정하고 독립 보안 읽기 검토를 거쳤다.

CLI exit 0, outer/data/inner success=true, diagnostics=[], compile 1,110ms,
execute 103ms이다. 이는 **진단 반환 완료**를 뜻한다. 반환된 원계약 결과는
**contractAccepted=false / P06**으로 검증 통과가 아니다.

| P06 고정 분류 | define 발생 횟수 |
| --- | ---: |
| null/empty | 0 |
| UNITY_INCLUDE_TESTS | 0 |
| DEVELOPMENT_BUILD | 0 |
| TAMER_ 접두사 | 0 |
| 기타 TEST 포함 | 67 |
| 기타 HARNESS 포함 | 0 |

분류는 표의 순서로 상호 배타 적용하며, 대소문자 규칙은 기존 차단 조건과 같다.
67은 **어셈블리별 define 항목의 총 발생 횟수**다. 고유 심볼 수나 어셈블리 수가
아니며 정확한 심볼명은 수집하지 않았다. 첫 번째 실패 원인을 소급 확정하지 않는다.
P06 이후 P07~P10 조건도 이번 결과로 검증됐다고 표현하지 않는다.

추가 실행 없이 기존 Bee rsp를 정적으로 조사했다. TEST/HARNESS/TAMER/개발
항목이 있는 rsp 206개의 수정 시각은 모두 이번 진단 이전이며, 최신 것도
2026-09-28 자료였다. 과거 자료의 `ENABLE_MARSHALLING_TESTS` 존재는 이번
API의 67회가 해당 심볼이라는 증거가 아니다. 따라서 정확 심볼은 미확정으로 남긴다.

초기화는 기존과 같은 세 파일만 변경했다. URP Low/Medium의
`k_AssetPreviousVersion`과 ProjectSettings의 `AndroidKeyaliasName` 및 공백
변화를 비공개 증거에 보존했다. 독립 검토 후 해당 Editor 0, HEAD/index,
현재 after 해시와 snapshot 해시를 확인하고 세 파일의 배타 핸들을 모두 확보한
상태에서 각각 사전 바이트로 복원했다. 세 복원 해시뿐 아니라 **1,091개 보호
snapshot 전부 일치 / Git clean / 해당 Editor 0**을 확인했다.
복원 helper의 Editor 검사에 cwd 의존성이 있다는 추가 읽기 지적은 복원 후 받았다.
실제 실행 cwd는 위 e5e5 checkout이었고 사후 검증도 같은 대상이었다. helper는
명시적 절대 checkout 인자를 사용하도록 수정했으며 복원을 재실행하지 않았다.

제3차 진단은 1회, 제4차/동등한 추가 조회는 0회다. 콜백 등록·실행, Assets
설치·삭제, 실제 Build/AAB/APK/기기/광고/Console 작업은 0회이며 배포물 SHA-256은
없다. `--execute` 차단과 Draft/병합 보류를 유지한다.

### 필요한 최소 후속 판단

현재 근거만으로 TEST 부분문자열 차단을 해제하거나 특정 심볼을 허용하지 않는다.
원인을 더 좁히려면 별도 사용자 승인을 받은 단일 진단에서 금지 항목의 비식별
SHA-256과 발생 개수를 수집하여 알려진 공식 심볼과 정적으로 대조하는 것이 최소
범위다. 그 뒤에만 실제 프로젝트 테스트/하네스와 Unity 기본 심볼을 구분할 수 있는
명시적 조건 변경안을 검토할 수 있다. 이 후속 진단이나 조건 변경은 실행하지 않았다.
이전 두 실패 및 이번 원계약 거절 이력은 그대로 보존하며, 정상 콜백 등록과 빌드
등 종속 검증도 보류한다.


## 사용자 승인 제4차 해시 진단 — 이번 차단 심볼 확인

2026-09-30 총괄이 구체적으로 제안한 정의 SHA-256/발생수 단일 진단에 대한 사용자
재개 승인으로 제4차를 **한 번** 수행했다. checkout은 위 e5e5, 브랜치는
`codex/private-ads-build-injection`, 검증 HEAD는
`87781e7fc1bcfe5b948f47e685571f4862568e11`의 clean 상태다. Unity
`6000.3.25f1`/`e1dba0a9aba4`, CLI `1.0.0-beta.8`/고정 SHA, Android
min25/target36/ARM64 기준, 활성 라이선스와 프로젝트 계정 pin을 다시 확인했다.

실행 당시 production 템플릿과 원래 검사 조건은 그대로였다. 승인된 외부 probe는
같은 단일 API 반환값에서 차단된 nonempty 정의의 **정확한 UTF-8 SHA-256**별
발생 횟수만 추가 수집했다. 빈 값은 별도 고정 개수로 유지했다. raw define,
어셈블리명, 경로, plan은 반환하지 않았다. probe raw SHA-256은
`d6c4c35a363e31725af5bf20181f309fe05778f53781e8909a89615aca56b7cf`이며
실행 전 독립 읽기 검토와 source/CLI/1,091 snapshot 검증을 거쳤다.

CLI exit 0, outer/data/inner success=true, diagnostics=[], compile 1,359ms,
execute 82ms였다. **진단 완료지만 원계약 contractAccepted=false / P06**이다.
고정 분류는 기타 TEST 67회, 다른 다섯 범주 0회이며 반환된 비어 있지 않은 차단
정의 해시는 한 종류다.

- SHA-256: `61ce8b9a5562471741f54abf05a90c9774cecb2618d4c626c8274f3394f11539`
- 발생 횟수: 67회
- 정확 후보 대조: `ENABLE_MARSHALLING_TESTS`의 UTF-8 SHA-256과 일치

보안 담당도 원 응답과 후보 해시를 독립 계산·대조했다. 공식
[UnityCsReference의 UnityEngine.csproj](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Projects/CSharp/UnityEngine.csproj)
DefineConstants에도 동일 토큰이 있다. 읽은 공개 원문 사본 SHA-256은
`762e149b81c57e46fa1a0b628ea314804d03f2def4c5b487273dff984dabdc2a`로
비공개 증거에 보존했다. 이 공개 master 자료는 **6000.3.25f1 빌드의 출처나
바이너리 안전성 증명은 아니다**. 해시 대조는 이번 API에서 차단된 정의의 이름을
확인한 것이며, 과거 첫 실패 원인을 소급 확정하지 않는다. 67은 여전히 정의 발생
횟수이고 어셈블리 개수라고 표현하지 않는다. P07~P10은 거절 이후라 미검증이다.

초기화가 변경한 같은 세 파일의 before/after를 보존하고 독립 읽기 검토 후,
명시적 checkout/Editor 0/HEAD/index/현재 after 해시를 확인하여 세 배타 핸들에서
정확한 사전 바이트로 복원했다. **복원 직후 1,091개 보호 snapshot 전체 일치,
Git clean, 해당 Editor 종료**를 확인한 뒤 아래 의도적 소스 수정에 착수했다.

### 확인 근거에 따른 최소 guard 수정 — 정적 검토 범위

수정은 P06의 API 메타데이터 판정에 `ForbiddenPlayerDefine`을 분리하는 데 한정했다.
`Application.unityVersion`이 정확히 `6000.3.25f1`이고 정의가 정확히
`ENABLE_MARSHALLING_TESTS`일 때만 이 메타데이터 분기의 예외로 취급한다.
두 비교는 모두 Ordinal이다. null/empty는 거절하고, 다른 버전·대소문자·접미사·
다른 TEST/HARNESS/TAMER/DEVELOPMENT 정의는 기존 `ForbiddenDefine`으로 판정한다.

사용자 지정 Android `Defines.Split(';').Any(ForbiddenDefine)`는 변경하지 않았다.
따라서 같은 심볼을 사용자 지정 설정에 추가하면 기존 엄격 차단이 그대로 적용된다.
nondevelopment, `BuildOptions.None`, 빈 extraScriptingDefines, 원래 P01~P11의
나머지 조건, gate OFF, 원본 보호 및 `--execute` 즉시 거절도 유지한다.

이 분리는 'API 반환에 이 이름이 있다'와 '사용자가 테스트 심볼을 설정했다'를 같은
문자열 휴리스틱으로 일괄 거절하던 문제를 좁게 다루는 수정안이다. 메타데이터 이름만으로
주입 경로나 출처를 완전히 인증하지는 못하며, 실제 Player에 테스트 코드가 없다는
보장으로 사용하지 않는다. 최종 바이너리 검증이 별도로 필요한 이유도 그대로다.

이 소스 변경은 실행 없이 정적으로 검토했다. **수정 후 C# 컴파일/Player API 조회/
정상 콜백 등록/Build/기기/광고/Console 실행은 하지 않았다.** 제5차·동등한 재검증은
0회이며 새 조건 통과를 주장하지 않는다. Draft/병합 보류와 binaryVerified=false,
distributable=false를 유지한다.

### 다음 최소 단일 재검증의 목적과 범위 — 아직 실행하지 않음

별도 승인 후 수정된 메서드를 포함하는 Assets 밖 probe를 한 번 컴파일·호출하여
같은 API를 한 번만 조회한다. 목적은 좁은 P06 판정이 관측 환경에서 더 이상 해당
심볼을 오탐하지 않는지와, 이어지는 P07 어셈블리명 중복 및 P08~P10 핵심 세 소스의
정확 집계가 통과하는지 확인하는 것이다. 사용자 설정 정의에는 기존 엄격 판정을
적용했는지도 같은 진단에서 읽기 확인한다. 반환은 고정 거절 코드/수락 여부와
필요한 비식별 요약만으로 제한한다.

동일하게 사전 snapshot/source hash를 고정하고 초기화 변경을 제한 복원한다.
한 번의 결과가 실패하면 추가 조회 없이 멈춘다. 성공해도 정상 Unity 콜백 편입과
실행 또는 AAB의 직렬화/manifest/Player define/gate 안전성을 입증하지 않는다.
실제 Build/기기/운영 광고/서명/Console 작업은 이 재검증 범위에 포함하지 않는다.


## 사용자 승인 제5차 — 수정 후 Player 계획 읽기 통과

2026-09-30 사용자 승인으로 e5e5 checkout/`codex/private-ads-build-injection`,
HEAD `33cfb50ff62f1f2b2a46397505cc659ff8004322` clean에서 수정 후 진단을
한 번 실행했다. Unity `6000.3.25f1`/`e1dba0a9aba4`, CLI `1.0.0-beta.8`의
고정 해시, Android min25/target36/ARM64, 라이선스/프로젝트 pin을 확인했다.
production의 ReadPlayerCompilationPlan부터 ForbiddenDefine까지 본문을 그대로
복사하고 사용자 설정 정의의 엄격 판정을 별도 읽기로 추가했다. 추출 준비 중 포함된
불필요 helper는 실행 전에 제거하고 source-map/실행 복사본 해시를 다시 고정했다.
이 준비 과정에 컴파일 실행은 없었다. 최종 probe SHA-256은
`dc02cec0c5501bff4a4427c7741b72e33979a70ea045b737da66bcc44ebcf56c`이다.

독립 사전 읽기 검토 후 단일 compile+Inspect/API 조회를 수행했다.
CLI exit 0, outer/data/inner success=true, compile **1,213ms**, execute **93ms**,
diagnostics=[]이며 다음 결과가 반환됐다.

| 읽기 판정 | 결과 |
| --- | --- |
| playerPlanAccepted | true |
| configuredDefinesAccepted | true |
| 두 판정의 conjunction인 contractAccepted | true |
| reasonCode | NONE |
| callbackExecuted / buildExecuted / binaryVerified | 모두 false |

이번 관측은 수정 P06 및 이어지는 P07 중복 어셈블리명/P08~P10 핵심 세 소스의
정확 집계와 현재 사용자 지정 Android 정의의 엄격 판정 통과 근거다.
**contractAccepted는 이 두 읽기 판정의 결합일 뿐 전체 Snapshot/build 계약의
통과가 아니다.** 새 조건의 모든 부정 입력 검증이나 최종 바이너리 안전성을 뜻하지
않는다. 이전 실패 이력은 그대로 보존한다.

같은 세 초기화 변경 파일의 before/after를 보존하고 독립 귀속 검토 후 명시적
checkout/HEAD/index/Editor 0/현재 after 해시를 재확인했다. 세 배타 핸들에서
정확 사전 바이트로 복원하고 **보호 snapshot 1,091개 전부 일치, 복원 직후 clean,
해당 Editor 종료**를 확인했다. 이후 변경은 이 결과 문서뿐이다. 제5차 1회,
제6차·추가 재시도 0회, Assets staging/삭제·콜백 등록·실제 Build·기기·광고·Console
0회다. Draft와 운영 `--execute` 차단, binaryVerified=false/distributable=false를 유지한다.

### 다음 비운영 등록·합성 빌드 단계의 코드 검토와 최소 계획

현재 `private_ads_build.py.execute()`는 즉시 거절하며 이를 해제하지 않는다.
도달 불가 `_execute_after_contract_review()`를 직접 호출하는 우회도 사용하지 않는다.
현재 production 계약은 실제 inventory 확인과 비샘플 형식의 ID, Login 씬/AAB를
요구한다. 따라서 가짜 inventory 승인이나 임의의 운영형 ID를 넣어 같은 경로를
통과시키는 방식은 적절하지 않다. 기존 RevivalAdHarnessBuild도 별도 하네스
define/씬/APK를 사용하므로 이번 production 콜백 검증을 대체하지 못한다.

다음 단계는 아래 두 부분을 분리하여 준비한다. 아직 실행하거나 새 실행 경로를
구현하지 않았으며 production 보호 조건은 그대로다.

1. **정상 Editor 등록 확인:** 소유 journal로 production 템플릿/메타를 예약 경로에
   일시 설치한다. 정상 Unity 컴파일 후 세 인터페이스 등록 여부와 메서드 해시를
   읽기 확인하되 Build/콜백을 호출하지 않는다. 외부 Roslyn probe의 로드 성공과
   정상 asset/asmdef 편입 성공을 구분한다. import 오류면 실행을 멈추고 증거를 보존한다.
2. **명시적으로 분리된 합성 검증 경로:** production parser/entry를 재사용해 우회하지
   않는 별도 synthetic runner와 계약을 만든다. 고정 별도 package
   `com.tamer.revival.privateads.synthetic`, 비운영 debug signing, 고정된 공개 테스트
   ID만 허용하고 실제 private config/운영 ID/서명 환경 변수 입력은 거절한다.
   Android Development 옵션은 끄고 `BuildOptions.None`을 유지한다. debug signing과
   Development 빌드 옵션은 서로 다른 개념이다. 빌드에는 격리된 비활성 fixture 씬만
   넣고 운영 Login/계정/저장/구매 경로를 호출하지 않는다. 기기에 설치·실행하지 않는다.

합성 경로는 pre/scene/post 순서·빌드용 직렬화 값 변경·원본 무변경을 확인하도록
좁은 공통 동작을 추출하거나 동작 대응표를 만든 뒤 독립 코드 리뷰를 거쳐야 한다.
production 콜백을 복사한 별도 구현만 시험한다면 실제 production 콜백 통과라고
보고할 수 없다. production 등록 확인과 합성 콜백 실행 결과를 별도 항목으로 기록한다.
운영 계약의 Login/AAB 전제와 synthetic fixture 차이를 감추지 않는다.

실행 설계는 원본 source/ProjectSettings/Packages 및 변경할 설정값을 사전 보존하고,
설정 변경은 Unity API로 수행하며 finally에서 되돌린다. 별도 출력 경로/고정 테스트
package/debug 인증서를 산출물에서 확인하고 SHA-256을 기록한다. AAB debug 서명
지원과 최종 서명 검사는 구현 전에 기존 도구와 대조하며 운영 키 fallback은 금지한다.
모든 로그·산출물은 ignored private 경로에 보존하고 distributable=false로 표시한다.

소유 파일 정리는 이미 검토된 identity/hash/Windows 배타 핸들 경로만 사용한다.
현재 finalize는 빈 디렉터리와 active journal을 수동 확인 대상으로 남긴다. 이 상태를
완전 복원 성공으로 숨기지 않고, 후속 실행 전에 담당자가 소유권과 남은 상태를 검토한다.
원래 파일은 post-import 해시와 snapshot 귀속을 검토한 정확 경로만 복원하며 임의로
dirty 전체를 되돌리지 않는다. 다른 checkout·Editor·기기·계정·운영 광고 작업은 없다.

이 계획의 준비/정적 검토는 완료했지만 실제 등록 및 synthetic runner 구현·검증은
아직 남아 있다. Player 계획 API의 변경 없는 반복 실행은 필요하지 않다.


## 비운영 검증 준비 구현 — 실행 결과와 분리

제5차 결과는 `1b381ac`에 먼저 기록했다. 이후 총괄 지시에 따라 최소 공통 동작과
별도 검증 도구를 구현했다. 기존 production `execute()`의 즉시 거절은 유지한다.

- `PrivateAdsSceneInjection`: production의 빌드용 scene 직렬화 주입 동작만 추출했다.
  manager 개수/빈 기존 값/반영 후 대조/비대상 씬의 manager 거절은 공유한다.
  production의 Login 횟수·순서·Snapshot Guard는 원래 경로에 남아 있다.
- `PrivateAdsRegistrationProbe`: 정상 Library/ScriptAssemblies의 유일한 production
  타입, 세 인터페이스 구현과 메서드 IL 해시를 읽는다. 이는 metadata 확인이며
  Unity dispatcher가 실제 콜백을 호출했다는 증거가 아니다.
- `PrivateAdsSyntheticCallbacks/Build/Invoke`: production 타입과 동시에 설치하지
  않는다. 고정 package/공개 테스트 App·Rewarded Unit/단일 비활성 fixture만 허용한다.
  fixture는 비활성화 후 manager를 추가한다. 설정·컴파일된 하네스·prospective Player
  define을 검사하고 관측 Unity 버전의 exact 메타데이터 예외만 유지한다.
  nondevelopment/BuildOptions.None/debug signing을 요구하고 고정된 새 AAB 경로를
  Prepare에서 보존하여 각 BuildReport 경로와 대조한다. 빌드 전 운영 서명/광고 환경
  입력을 거절하며 finally에서 수정 설정별 독립 복구를 시도한다.
- `private_ads_validation.py`: production과 별개의 준비/단일 실행/검토 후 복구 도구다.
  mode별 source/staged/tool/snapshot 집합과 staged-source/entry-source 해시를 연결한다.
  1회 marker/after/manifest/runner SHA를 연결하고 child의 운영 광고·서명 환경을 제거한다.
  부분 실패도 재시도하지 않고 after 증거를 보존한다. 실제 등록과 synthetic mode는
  서로 다른 검증이며 아직 이 runner를 실행하지 않았다.
- `verify_private_ads_synthetic.py`: 고정 package·공개 sample App·min25/target36·ARM64·
  nondevelopment manifest를 검사한다. 사전 고정한 기존 debug key에서 공개 DER만
  내보내며 기존 Java 검증기의 모든 payload 서명 및 debug subject 검사 결과를
  정확 DER SHA와 대조한다. AAB/DER를 읽기 핸들로 잠근 동안 모든 검사를 수행한다.
  이 검사는 설치/실행·production 계약·최종 직렬화 검증을 대체하지 않는다.

복원은 기존 Windows 핸들 도구에 기본 동작을 유지하는 writable 모드를 추가했다.
모든 파일의 reparse/type/fstat identity/hash를 먼저 확인하고 동일 배타 핸들로
snapshot을 쓴다. 동일 바이트의 다른 identity도 거절한다. 정리는 실제 생성 기록이
있는 템플릿 파일만 수행하며 디렉터리·생성 fixture·journal은 보존하고
manualRecoveryRequired=true로 남긴다. 후속 담당자 검토 없이 이를 완전 clean이나
다음 단계 실행 가능으로 취급하지 않는다. 새 PowerShell 복원 초안은 폐기하고
기존 handle 구현을 재사용한다.

독립 읽기 리뷰가 지적한 identity/reparse, 증거 연결, 고정 집합, 동일 AAB 바이트,
staged-source/entry-source 연결을 보완했다. 새 writable/read-lock 임시 파일 검사는
첫 모듈 호출에서 cwd 때문에 import 실패했고 본문은 실행되지 않았다. 명시 파일
경로로 고친 두 번째 호출에서 1개 검사가 0.020초에 통과했다. 같은 핸들 복원,
writer/replace 차단, 동일 SHA 다른 identity 거절, 복수 파일 중 한 검증이 실패하면
전체 쓰기 미진입, 읽기 잠금의 동시 읽기 허용/쓰기 차단을 확인했다. 세 번째 호출은 없다.
Python AST와 diff 검사를 수행했다. 새 C# 전체의 emit-only 컴파일은 별도 한 번으로
확인할 예정이며, 이 시점에 정상 staging/등록/콜백/Build 실행 결과는 없다.


### 새 C# 7파일 emit-only 첫 시도 — CS0104

`8df0ac1ad6172e535ab6629a538971bf795e22e7` clean에서 새 파일 일곱 개를
using 선언만 모아 하나의 외부 소스로 만든 뒤 승인된 emit-only 1회를 수행했다.
CLI exit 0과 달리 **inner success=false**이며 compile904ms/execute0ms였다.
SyntheticBuild의 `SceneManager.sceneCount`와 `SceneManager.GetSceneAt` 두 참조가
프로젝트 `AD.SceneManager`와 Unity 타입 사이에서 모호하여 CS0104 두 진단이 발생했다.
콜백·Build를 실행하지 않았다. 반환 assemblyName은 컴파일 시도용 이름일 뿐 이 실패에서
어셈블리 로드 성공의 근거가 아니다.

원 응답·7source/합성 SHA·before/after 증거를 보존하고 독립 검토 후 동일 세 설정
파일을 정확 snapshot으로 복원했다. 보호1091 해시 일치/clean/해당 Editor0을 확인했다.
후속 수정은 두 참조를 `UnityEngine.SceneManagement.SceneManager`로 명시하는 것뿐이다.
총괄이 배정한 두 번째 emit-only 1회에서 이를 확인하며, 두 번째도 실패하면 해당 검사와
종속 작업을 중단하고 승인 없는 세 번째 시도를 하지 않는다.


### 새 C# 7파일 두 번째 emit-only — 통과

두 참조 수정 후 `7c26835ef2abac5b181077810ecd2d9be8f889ed` clean에서 배정된
두 번째 컴파일을 한 번 수행했다. CLI exit0, inner success=true, diagnostics=[],
compile **1,084ms**, execute **0ms**, assemblyName=null이다. 도구도 아무것도
로드/실행하지 않은 dry run임을 명시했다. 새 코드 일곱 파일의 Editor 참조 컴파일이
통과했으며 정상 asset 등록이나 실제 dispatcher/합성 Build 성공으로 확대하지 않는다.
첫 CS0104 실패는 보존하며 세 번째 컴파일 호출은 없다.

7개 source와 합성 소스/원 응답 SHA, 1,091 snapshot 및 세 after 파일의 귀속을
독립 확인했다. 명시 checkout/Editor0/HEAD/index/현재 after 해시를 재확인하고
기존 검토된 세 파일 배타 복원 도구로 정확 사전 바이트를 복원했다. 이후 보호
**1,091개 해시 모두 일치 / Git clean / 해당 Editor 종료**를 확인했다.
이번 복원은 새 validation recovery 실행 성공 근거가 아니다. 새 runner의 실제
staging·metadata 확인·검토 후 복구와 synthetic Build는 아직 실행하지 않았다.

다음 최소 실행은 registration mode만 준비하여 source/staged/entry 연결,
1회 marker/manifest 귀속과 정확 복구 경로를 다시 읽기 리뷰한 뒤 정상 Editor
어셈블리의 인터페이스 구현 metadata를 확인하는 것이다. Build·기기·광고 실행은 없다.
운영 --execute 차단은 유지하고 빈 디렉터리/journal 잔존은 수동 검토 상태로 기록한다.


## 정상 Editor 어셈블리 metadata 확인 — registration 첫 실행 통과

`b61c1507f52f97cc5a24fb466e04e80dfe059561` clean에서 registration mode의
고정 source11/stage7/entry 및 snapshot1091을 준비하고 독립 귀속 검토를 거쳤다.
production 계약·공통 주입·production 콜백의 세 템플릿과 해당 meta만 일시 설치했다.
synthetic 템플릿/fixture/debug key는 사용하지 않았다. manifest SHA는
`932236dcbe3426ff6ed54689fc6229edce7f42d8e72c6c191ee08233372a05e7`이다.

단일 정상 Editor 초기화 뒤 외부 읽기 도구가 Library/ScriptAssemblies에서 유일한
production 타입과 세 인터페이스 구현 및 IL 해시 세 개를 확인했다.
CLI/inner success=true, diagnostics=[], 읽기 도구 compile1172ms/execute2ms,
normalEditorAssembly=true/interfaceCount=3이다. 이 시간은 외부 읽기 도구의
Roslyn 컴파일/실행 시간으로, 정상 Unity 전체 import/compile 소요 시간은 아니다.
**callbackExecuted/buildExecuted/binaryVerified는 모두 false**다. 정상 어셈블리의
인터페이스 구현 metadata를 확인했으며 dispatcher 호출 성공이라고 표현하지 않는다.

실행 후 독립 검토에서 manifest/runner/marker/after 연결, source11, before1091,
현재 after의 정확 세 파일 hash·identity, 실제 생성된 stage7의 hash·identity가
모두 일치함을 확인했다. after SHA는
`8cc30d09c7f7b62ffb99f1021444f52638504178973f3f938bd220b218768f3e`다.
이 SHA를 지정한 recover-reviewed를 **한 번** 실행해 검토된 세 설정 파일을 동일
배타 핸들로 정확 snapshot 복원하고 소유 stage7을 검증된 핸들로 삭제했다.
보호1091 전체 해시 일치/해당 Editor0/Git clean을 확인했다.

빈 `Assets/Scripts/Editor/RevivalPrivateAdsValidation` 디렉터리와 비공개 journal·
증거는 보존하여 manualRecoveryRequired=true다. 이번 모드에서는 fixture가 생성되지
않았다. 복구 도구의 일반적인 retainedFoldersAndGeneratedFixture 필드는 보존 정책이며,
실제 잔존물은 별도 post-recovery 기록에서 구분했다. 다음 실행 전 이 빈 폴더의
수동 귀속 검토가 필요하며 stale 검사를 이름 변경으로 우회하지 않는다.


registration 복구 후 독립 검토에서 빈 예약 폴더의 정확 경로·일반 디렉터리 속성·
children0·해당 Editor0을 재확인했다. 이 **현재 빈 폴더만** 비재귀
Directory.Delete(path,false) 한 번으로 수동 정리했다. private journal/증거는 모두
보존했고 원 보호1091 해시 일치/fixture 없음/예약 경로 부재를 기록했다.
자동 디렉터리 삭제 기능을 추가하거나 stale 경로 검사를 우회하지 않았다.

다음 synthetic 준비 전 기존 GMA ManifestProcessor가 원 AndroidManifest.xml을
저장한다는 코드를 확인했다. 해당 파일은 원래 snapshot 대상이며, 검토 후 복원의
고정 allowlist에 그 정확한 한 경로만 추가했다. GoogleMobileAds/GoogleUmp 의존성
XML 두 파일도 snapshot에 명시적으로 추가하되, 예상치 못한 의존성 변경을 자동
복원 허용하지 않는다. 새 보호 대상은1093개다.
또한 run_script의 실제 기본 dispatcher 제한이30초임을 패키지 소스에서 확인해
synthetic 실행에는 timeout_ms=1500000(25분), CLI 제한은1800초를 고정했다.
registration은30000ms/900초다. 이 준비 수정은 Python runner/문서에 한정하며
변경 없는 C# emit-only를 반복하지 않는다.

## 합성 AAB 첫 실행 실패와 정확 범위 복원

2026-09-30, checkout `C:/Users/pc_17/.codex/worktrees/e5e5/Tamer`,
branch `codex/private-ads-build-injection`, clean HEAD
`e7a1a3164e9c3088a64bfd6d4734db3265f2d66e`에서 합성 실행을 한 번 수행했다.
Unity `6000.3.25f1`/revision `e1dba0a9aba4`, CLI `1.0.0-beta.8`,
Android min25/target36/ARM64, 고정 별도 package와 공개 Google 테스트 광고 ID,
debug signing을 사용했다. 운영 ID·키·광고·로그인·저장·구매·기기 설치는 사용하지 않았다.
bundletool 미설치로 staging 전 준비 한 번이 거부됐고, 기존 설치 도구로 고정
`1.18.3`을 설치한 뒤 준비했다. 실제 Unity 합성 실행은 한 번뿐이다.

외부 CLI exit0이지만 inner success=false/runtime error였다.
외부 코드 compile850ms/execute326323ms이며, Unity 로그에는 Build Success가 있지만
두 generic catch가 내부 원인을 숨겨 콜백 결과 읽기 또는 설정 복원 중 어느 지점인지
확정할 수 없다. 성공한 합성 호출로 재분류하지 않는다.

AAB SHA-256은 `22ea58fc3a3fae2533eacd3ebf882669c3fc26b19c04c596d22a2aef692c26d8`,
크기는65,986,529바이트다. 사후 읽기 전용 bundletool/manifest/signature 검사가 한 번
통과했고 고정 합성 package·공개 sample App ID·non-development·min25/target36·ARM64,
사전 debug 인증서 일치와 서명 payload858개를 확인했다. 이 결과는 콜백 완료,
production 계약, 광고 실행, 16KB/실기기·스토어 검증을 증명하지 않는다.
`productionContractVerified/binaryVerified/distributable=false`를 유지한다.
실패 AAB·fixture·stage·비공개 원로그와 journal을 보존했다.

실행 뒤 보호 snapshot 대상 여덟 파일과 보호 밖 두 설정 에셋의 변경을 발견했다.
원 after와 manifest는 변경하지 않았다. 수동 복원 준비가 Git normalized diff8과
실제 byte drift10을 동일시해 두 번 쓰기 전 거부됐고, 중단 후 사용자 승인을 받아
수정 준비를 한 번 실행했다. GMA settings/GraphicsSettings는 개행 차이만 있었다.
추가 HighQuality/global settings는 공백 차이만 확인됐지만 사전 snapshot이 없어,
기록된 HEAD raw blob과 checkout-filtered baseline을 별도 보존했다.

독립 검토한 plan SHA `b3a858f160df927f8a5f7143f5fc4d234e99563d7b8c79b1d51ef8107d31561c`로
열 파일 전체의 현재 identity·SHA를 먼저 확인하고 같은 배타 핸들로 한 번 복원했다.
여덟 파일은 exact pre-invocation snapshot 복원, 추가 두 파일은 Git HEAD 기준 재구성이다.
추가 두 파일의 정확한 실행 전 바이트를 증명했다고 표현하지 않는다.
사후 보호1093 전체와 추가 두 파일의 계획 baseline hash, Editor0/index0/추적 변경0,
원 manifest/after·source12·stage8·fixture/meta·AAB 불변을 독립 검토했다.
untracked 검증용 파일은 남아 있으므로 완전 clean이나 다음 실행 준비 완료가 아니다.

후속 정적 변경은 실패 지점 고정 코드 `S00/S10/S20/S30/S40/S50/S90`와
콜백 완료 조건 `C01`–`C05`만 허용한다. 예외 원문·경로·ID·키 값은 출력하지 않는다.
빌드 동작이나 기존 복원 동작은 바꾸지 않으며, 새 코드의 컴파일·실행은 아직 검증하지 않았다.
새 준비부터 추가 두 에셋도 snapshot에 포함해 보호 대상1095개로 보완했다.
두 번째 실제 빌드는 아직 실행하지 않았으며, 두 번 실패하면 해당 경로를 중단한다.

## 진단 컴파일 통과, 두 번째 합성 호출 실패와 중단

위 첫 실패 후 진단 변경 커밋 `ead483133587a5901e3fb40dd801367790f17d0e`에서
정확한 소유 검증 파일10개/빈 폴더2개만 검토·정리했다. 원로그·journal·백업을 보존하고
첫 AAB는 비공개 증거 폴더로 동일 바이트/SHA를 보존한 뒤 기존 출력의 같은 핸들만
정리했다. 첫 AAB의 읽기 전용 검사 결과는 원 도구 출력의 전사 기록이며, 원 stdout
파일이나 검토자의 독립 재검증 결과라고 표현하지 않는다.

새 진단 C#7파일의 최소 emit-only를 한 번 실행해 outer/inner success=true,
diagnostics=[], compile943ms/execute0/assemblyName=null을 확인했다.
코드는 로드·실행하지 않았다. 초기 import의 세 설정 파일을 사전 바이트로 복원하고
보호1095 해시 일치/Editor0/Git clean을 독립 확인했다. 복원 helper 복사 준비는
기본 cp949로 UTF-8 BOM 읽기가 한 번 거부됐고, UTF-8을 지정한 두 번째 복사가
성공했다. 이 파일 복사 준비는 Unity 컴파일 재실행이 아니다.

같은 clean HEAD에서 source12/stage8/보호1095/entry/tool/key hash와 고정 명령을
독립 검토한 뒤 **두 번째 실제 합성 호출을 한 번** 실행했다. CLI exit0/outertrue지만
innerfalse, diagnostics=[], compile1174ms/execute34455ms, 고정 `S90`이다.
이는 `finally`의 설정 복원 중 하나 이상이 예외를 던졌다는 뜻이다. 구체적인 복원
항목이나 예외 원인은 로그·기존 receipt만으로 확정하지 못했다. Unity 로그의 Build
Success 문구만으로 콜백 완료·호출 성공을 주장하지 않는다. 자동 artifact-check는
실행되지 않았고 두 번째 AAB는 **미검증 실패 산출물**로 보존했다.
SHA-256 `74eb348129b708168a18191de1452a61b4cf0475c503c3a0a5fad33e56017b3f`,
크기65,986,530바이트다. 첫 AAB의 검사 결과를 이 AAB에 적용하지 않는다.

같은 합성 호출이 두 번 실패했으므로 해당 경로와 종속 Unity 실행을 즉시 중단했다.
세 번째 빌드·동등 우회 검증·후속 emit은 수행하지 않았다. 실패 후 복구는 별도 작업으로,
이번 변경10개는 모두 실제 실행 전 snapshot에 존재한다. 독립 검토한 plan SHA
`40f0e7680015bfbfdefd4cfa7ee5e3c088dfd7f765ec46023bb6add6e2fd147f`로
현재 열 파일의 identity/hash 전체 선검사 후 같은 배타 핸들로 한 번 복원했다.
**이번 열 파일 모두 exact pre-invocation snapshot 복원**이며 첫 실행의 HEAD 기준
추가 두 파일 재구성과 구분한다. 보호1095 해시와 원 manifest/after·stage/source·두
AAB 보존을 확인했다. 실제 운영 광고·계정·키·기기 설치는 계속0이다.

복원 사후 독립 확인 후 검토된 stage8과 고정 fixture/meta2를 한 번 정리하고 빈
예약 폴더2개만 비재귀 제거했다. stage8은 원 생성 식별자 기록에 연결되며,
fixture/meta2는 원 `after.created`에 없어 실패 뒤 관찰한 현재 식별자·해시와
별도 백업으로 검토했다. 두 종류의 소유 근거를 동일하게 표현하지 않는다.
두 AAB·원로그·journal·복원 및 정리 백업·진단 제안 파일은 보존했다.

다음 진단 수정안은 기존 열 복원 항목의 순서와 전체 시도를 유지하면서 처음 실패한
항목을 고정 `R01`–`R10`으로 구분한다. 원 예외 메시지·ID·경로·키 값을 전달하지 않는다.
이 수정안의 컴파일/실행과 세 번째 합성 호출은 별도 사용자 승인 없이는 진행하지 않는다.
수정안은 비공개 제안 파일 두 개로만 보존했으며 tracked 코드에는 적용하지 않았다.
정적 검토는 열 항목의 순서·전체 복원 시도·정확한 문자열 허용 목록 유지에 한정한다.
이는 진단 개선 제안이며 S90 원인 해결 또는 특정 실패 항목을 증명하지 않는다.
원래 Build 예외가 finally의 복원 예외로 가려질 수 있는 제한도 남아 있다.

## 복원 항목 진단 재시도 승인

2026-09-30 사용자가 구체 재시도 질문에 작업 계속을 승인했다.
검토된 보존 제안 두 파일의 SHA를 확인해 `R01`–`R10` 진단안을 그대로 적용했다.
승인 범위는 최소 emit-only1회와 통과 후 같은 합성 Build의 세 번째1회다.
복원 순서·열 항목 전체 시도·고정 문자열만 출력하는 경계는 유지한다.
세 번째 실패 시 네 번째 실행은 하지 않으며 복원·정리·문서 작업만 계속한다.
이 승인은 현재 원인 해결 또는 새 컴파일·실행 성공의 증거가 아니다.

## 승인된 세 번째 호출 — R10 확인과 원상 복원

진단안 적용 커밋 `c39511749c5586d20ad958f99055a3768c002363`의 두 소스는
보존 제안 SHA와 동일하며 독립 diff 검토를 통과했다. clean checkout에서 최소
emit-only1회를 실행해 outer/inner success=true, diagnostics=[], compile1084ms,
execute0/assemblyName=null을 확인했다. 코드는 로드하거나 실행하지 않았다.
import3 파일의 사전 바이트 복원/보호1095/Editor0/Git clean을 독립 확인했다.
인증 상태 조회는 loggedIn=true/sessionState=stale, 라이선스는 active/signedIn=true와
라이선스 목록2개였다. 이를 fresh 인증으로 표현하지 않으며 계정 기본값은 변경하지 않았다.

두 번째 실패 AAB도 동일 바이트·SHA를 비공개 증거 폴더에 보존한 뒤 고정 출력의
같은 핸들만 정리했다. source12/stage8/entry/tool/보호1095/고정 명령과 출력 경계를
독립 검토하고 승인된 **세 번째 같은 합성 호출1회**를 실행했다.
CLI exit0/outertrue지만 innerfalse, diagnostics=[], compile1132ms/execute31302ms,
고정 `R10`이었다. 코드상 `EditorSceneManager.RestoreSceneManagerSetup(oldSceneSetup)`가
최초 복원 예외 항목이다. 앞 아홉 복원 action은 예외를 던지지 않았지만, 이 사실만으로
설정의 의미적 복원 전체나 Build 계약 완료를 증명하지 않는다. 세부 원래 예외는 미확정이며
finally의 복원 예외가 원래 Build 예외를 가릴 수 있는 제한은 남아 있다.

자동 artifact-check는 실행되지 않았다. 세 번째 AAB의 SHA-256은
`b0c4d0977efa73a72a00bfadbe423d1a379feee0d9fdedac2471d9831f91d024`,
크기65,986,538바이트이며 미검증 실패 출력으로 보존했다. 이전 AAB 검사를 이 출력에
적용하지 않는다. 네 번째 Build·추가 emit·동등 runtime 조회는 중단했다.

독립 검토한 plan SHA
`86cbb29c166f6e069bbc04d27d0b040ef2621b250d4e9fc7df3d9c403c095e07`로
현재10 파일 전체의 identity/hash를 먼저 확인하고 같은 배타 핸들로 한 번 복원했다.
모두 exact pre-invocation snapshot이며 HEAD baseline은0개다. 보호1095/Editor0와
원 증거·source12·실패 산출물 보존을 확인했다. 검토된 stage8/관찰한 fixture·meta2를
원본/분리 백업에 연결해 정확10 파일 및 빈 폴더2만 한 번 정리했다. 실패 AAB3개,
원로그·journal·복원/정리 백업·미실행 후속 제안은 보존했다.

후속 가설은 빈 초기 씬 구성이다. [Unity6000.3 공식 API 문서](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.EditorSceneManager.RestoreSceneManagerSetup.html)는
복원 배열에 로드된 씬과 활성 씬을 요구하지만, 현재 코드는 초기 빈 배열을 배제하지 않는다.
이번 실행의 초기 setup 길이·loaded/active·sceneCount 기록이 없어 실제 빈 배열이었다고
확정할 수 없다. 초기 setup이 비어 있고 sceneCount도0인 batch에만 복원 대상 없음으로
해당 API 호출을 생략하고, 비어 있지 않으면 loaded/active 요건을 사전 검사해 기존 복원을
유지하는 최소 제안을 비공개 파일로 보존했다. 빈 초기 씬 상태는 API 복원 완료와 같지 않으며
해당 batch 종료 및 디스크 스냅샷 복원이 확인돼야 한다. 제안은 원인 해결 증거가 아니고
tracked 코드 적용·컴파일·실행은0이다. 다음 동일 빌드 재시도는 별도 사용자 승인이 필요하다.

## SceneBaseline 최소안 재시도 승인

2026-09-30 사용자가 구체 질문에 작업 계속을 승인했다. 보존 제안 SHA
`d7865e01689462f66f168ca9a306db34284f5ba3697ff7383481261933540c01`를 확인해 적용하고,
초기 setup/scene/loaded/active 개수만 고정 형식으로 기록하도록 보완했다.
씬 경로·ID·실제 설정값·키는 기록하지 않는다. 승인 범위는 최소 emit1회와 통과 후
같은 합성 네 번째 Build1회다. 실패 시 다섯 번째·추가 emit은 수행하지 않는다.
빈 baseline 분기의 batch 종료 의존과 원 Build 예외 masking 한계는 유지하며,
이번 적용·승인을 원인 해결 또는 실제 검증 성공으로 표현하지 않는다.

## 승인된 네 번째 호출 — 초기 상태 거부와 비용 축소

적용 커밋 `a93d191168f012d6e6042cc964b2f1ffb51bcf59`는 보존 제안에 초기 개수
고정 로그를 추가한 최종 소스이며 원 제안 SHA와 동일하다고 표현하지 않는다.
최소 emit1회가 outer/inner true, diagnostics=[], compile879ms/execute0,
assemblyName=null으로 통과했다. 코드는 로드하거나 실행하지 않았다. import3 사전
바이트 복원·보호1095·Editor0·clean을 독립 확인하고 세 번째 실패 AAB도 동일
바이트·SHA로 보존했다. 인증 조회는 loggedIn=true/sessionState=fresh 및 활성
라이선스/로그인 상태와 목록2개였으며 계정 기본값을 변경하지 않았다.

source12/stage8/보호1095/고정 명령을 독립 검토한 네 번째 같은 합성 호출1회는
CLI exit0/outertrue지만 innerfalse `S00`, diagnostics=[], compile1114ms/execute4ms다.
초기 고정 관측은 **setup=0, scene=1, loaded=0, active=0**이다. loaded/active 개수는
setup 배열에 대한 값이며 실제 Scene의 loaded/active 상태라고 확대하지 않는다.
초기 빈 배열·scene0 분기에 해당하지 않고 nonempty 요건 guard에서 거부되는
소스 흐름과 결합해 fixture 생성/BuildPipeline 이전에 중단됐다고 판단했다.
**네 번째 새 AAB·fixture/meta·artifact-check는 모두 없다.** 이번 관측을 앞선
세 호출의 초기 상태나 R10의 세부 원인으로 소급 확정하지 않는다.

동일 경로의 네 번째 호출 실패 뒤 다섯 번째·추가 emit·새 runtime 조회는 중단했다.
변동은 정확한 import3뿐이었다. 독립 검토한 after SHA
`ac1befe2e73e4ea797efb74693efc9a61f3038c2aa29a595111bae48498766db`로
기존 recover-reviewed를 한 번 실행해 전체 대상 identity/hash 선검사·같은 핸들 복원,
보호1095 확인 후 stage8의 원 생성 식별자·해시를 같은 핸들로 검사·삭제했다.
fixture 파일은 없었고 검토된 ordinary 빈 디렉터리2만 비재귀로 한 번 정리했다.
최종 사후 독립 검토에서 clean/Editor0/보호1095/원 증거·source12 불변과 실패
AAB archive3개 보존, stage8·폴더2 및 새 AAB/fixture 부재를 확인했다.

후속 미실행 제안은 두 개다. placeholder 안은 setup0/scene1만으로 허용하지 않고,
실제 Scene의 validLoaded/pathEmpty/clean/roots0/active를 모두 검사한다. 허용된
깨끗한 rootless·unnamed placeholder는 R10에서 EmptyScene/Single로 재생성하고
반환 Scene의 같은 구조와 sceneCount1을 사후 확인한다. 이는 원 핸들의 정확 복원이
아닌 구조 재구성이다. 이번 호출에는 이 속성들의 관측이 없어 실제 placeholder라고
확정할 수 없다. zero-scene의 batch 종료 의존 및 원 Build 예외 masking 한계도 남는다.

반복 full Build 비용을 줄이기 위한 우선 제안은 **Build 없는 초기 씬 상태 읽기1회**다.
고정 checkout·Unity 버전·batch에서 setup/count와 위 비식별 bool/root 개수만 반환하는
읽기 도구를 준비했다. BuildPipeline·NewScene·SaveScene 등 씬 변경 API와 실제
경로·ID·키·예외 상세 출력은 없다. 코드가 읽기 전용이어도 Editor import의 디스크
변동이 가능하므로 source/manifest 고정·보호1095 snapshot·사후 종료/파일 복원
검증이 필요하다. 두 제안 모두 정적 검토만 통과했으며 tracked 적용·컴파일·실행은0이다.
읽기 진단 성공은 Build/production 계약/바이너리 검증이 아니고, 별도 사용자 승인
전에는 이 runtime 진단도 수행하지 않는다. 다음 결정에는 다섯 번째 full Build를
포함하지 않는다.


## 승인된 읽기 진단과 빈 placeholder 최소 구현

2026-09-30 사용자의 명시 승인에 따라 Build 없는 읽기 진단1회를 실행했다.
실행 checkout은 `C:\Users\pc_17\.codex\worktrees\e5e5\Tamer`, branch는
`codex/private-ads-build-injection`, 검증 대상 HEAD는
`6e463bf74e4411fd7510022545c324740b25d551`이며 실행 전 clean/Editor0였다.
Unity `6000.3.25f1`·revision `e1dba0a9aba4`·CLI `1.0.0-beta.8` 기준이다.
source1/using-only combined/보호 snapshot1095와 runner·복구·CLI·환경 필터·파일 보호
도구 해시를 고정한 실제 manifest의 독립 검토 후 실행했다. manifest SHA는
`3e504c928c92fa9bcdbd37a33a7f6d3998650e12cbce167b7b405813ea6a5ae3`다.

CLI exit0/outer·inner success, diagnostics=[], compile1136ms/execute2ms였다.
setup0/scene1/setup 배열 loaded0·active0이며 실제 Scene은 validLoaded/pathEmpty/
clean/active 모두 true, roots0였다. emptyPlaceholderCandidate=true다. 이는 이번
독립 진단의 시작 상태를 관측한 결과이며 앞선 네 호출의 실제 Scene 속성이나
세 번째 R10의 원인을 소급 확정하지 않는다. 읽기 진단은 Build·callback·production
계약·바이너리 성공 증거가 아니다. Build0/별도emit0/씬 쓰기0/기기·운영 계정 작업0이다.
CLI 응답 SHA는 `906c38e8076c72e4694c4409e4164522ba7beb3a92acab6653acb3235891eb2c`,
사후 receipt SHA는 `0bb554bbfa8cdd650d0363430b8a48ffc08aaf866ac7cb32c860611da472c86a`다.
원로그와 manifest·스냅샷·복원 자료는 비공개로 보존한다.

Editor 종료와 known import3(low/medium 품질 asset 및 ProjectSettings)만 변동한
사후 귀속을 독립 확인했다. reviewed receiptSHA를 고정한 정확 복원1회로 전체
대상 identity/hash 선검사 뒤 같은 핸들에 사전 바이트를 복원했고 보호1095/Editor
종료를 확인했다. 새 AAB·fixture·Assets 임시 코드 생성은 없다.

관측에 근거하여 이전 정적 검토 placeholder 최소안을 tracked 소스에 적용했다.
setup0/scene1인 경우에도 실제 validLoaded/pathEmpty/clean/roots0/active 요건을
모두 만족해야만 허용한다. R10에서는 EmptyScene/Single로 빈 장면을 재생성하고
같은 구조와 sceneCount1을 확인한다. 이는 원 장면 핸들의 정확 복원이 아닌 구조
재구성이며, batch 종료와 외부 디스크 스냅샷 복원 검증을 계속 요구한다.
다른 상태의 기존 guard·복원 및 운영 차단은 유지했다. 적용 소스 SHA는
`c9f7f1f323e66b9e06d0f57f8b4b044a297d6195f2bc57f69e4edf3700764c61`다.
이 변경은 정적 구현이며 적용 후 컴파일·Build 실행은0이다. zero-scene batch 종료
의존과 원 Build 예외 masking 한계도 남는다. 다섯 번째 같은 합성 Build와 새 emit은
이번 승인 범위에 없으며 별도 구체 승인 전 실행하지 않는다. PR은 Draft·병합 금지다.


## 승인된 최소 emit 및 다섯 번째 합성 빌드 성공

2026-09-30 사용자의 현재 구체 대기 작업 전체 승인에 따라 최소 emit1회와 통과 후
같은 합성 다섯 번째 호출1회를 실행했다. 실행 checkout·branch·HEAD는 위 최신 검증
범위와 같다. 실행 전에 각 실제 manifest/source/tool/snapshot/command의 독립 귀속
검토를 수행했다. 최신 인증 조회는 loggedIn=true/sessionState=fresh 및
license active=true/signedIn=true/licenses2였다. 기본 계정과 서명키는 변경하지 않았다.

최소 emit은 source7의 using-only combined 변환과 보호1095를 고정해 dry_run으로
실행했다. outer/inner true, diagnostics=[], compile837ms/execute0/assemblyName=null로
통과하며 코드는 로드하거나 실행하지 않았다. manifest SHA
`40293f6093d6f1761adaacb990a62affa1e2cf8269423e5996e12a766f604c82`,
CLI SHA `f8ae2dadd65ab52300415d1313b59ea4ad40b1695b731201b343be0fca230c9a`,
receipt SHA `18c57bba10343c2ecdca8b814e808045d4b30a8b07276f3b5cf82c65028a60db`다.
known import3의 사후 귀속 검토 뒤 exact 복원1회와 보호1095/clean/Editor0를 독립 확인했다.

다섯 번째 실제 준비는 source12/stage8/snapshot1095/고정 Entry·CLI·Java tool·debug key
hash를 보존했으며 source Build SHA는
`c9f7f1f323e66b9e06d0f57f8b4b044a297d6195f2bc57f69e4edf3700764c61`다.
manifest SHA `c7e27dbdcb83e2bbf8d284c2694f02323945b4f5bdbd09ef622f671638713360`의
raw bytes를 실행 직전 재대조하고 기존 one-attempt marker로 한 번 실행했다.
CLI exit0/outer·inner true, diagnostics=[], compile1114ms/execute34197ms였다.
원콜백 결과 syntheticCallbacksCompleted=true/injectedManagers1은 ReadResult의
prepared/preprocessed/postprocessed/injections1/scenes1 요건을 통과한 근거다.
운영 콜백을 실행한 결과가 아니고 이전 실패 네 번을 성공으로 재분류하지 않는다.
CLI SHA는 `a6c1a81374c40fc4413bfa12daaa9a8a0127ff17b298b97d24fcbe1e275df67a`,
after SHA는 `f04045a2f9773518dee2ced1cd89a5501556a747d7d19a011db830135bfce144`다.

이번 AAB는 65,986,521바이트, SHA-256
`49ad610f8c14fdea025e53df9d15cf4e5df544d90c7008454f1796e20e81d99c`다.
고정 verifier1회가 bundletool validate/merged manifest의 격리 package·공개 샘플 App ID·
nondevelopment·min25/target36, ARM64만 포함, 기존 Debug key에서 내보낸 인증서 일치 및
서명된 payload858을 확인했다. verifier도 source12에 귀속됐다. 독립 reviewer는 원결과·
실제 AAB hash·certificate receipt·verifier/runner source를 대조했으며 verifier를 다시
실행하지 않았다. 이 검사는 최종 serialized rewarded field나 stripped Player gate,
production contract 또는 배포 승인 검증이 아니다. 새로운 기기 설치/광고 실행,
16KB/RELRO/스토어 검증으로 확대하지 않는다.

사후 변화10개는 모두 exact 실행 전 snapshot에 존재한다. reviewed plan SHA
`8241c6ac06a92745bc01f8aa3e72e278797da26762f22419e3490d565833ffa5`로 전체 파일
identity/hash를 쓰기 전 잠금 검사하고 같은 핸들에 정확 사전 바이트를 복원했다.
복원 결과 restored10/exactSnapshot10/HEADBaseline0/protected1095/EditorClosed=true다.
원본 재구성으로 대체하지 않았다. reviewed cleanup plan SHA
`069040b68f2d908b1698e7a5db1152ba5ced522f9831edd79cd55ac642e4189e`로 stage8 및
fixture/meta2의 전체 생성/관측 identity·hash를 먼저 검사한 뒤 같은 핸들에서 삭제하고
ordinary 빈 폴더2를 비재귀로 정리했다. 성공 AAB는 `synthetic-validation.aab`로 배타
복사·fsync·잠금 해시 확인 후 원래 출력의 검증된 핸들만 삭제했다. archive의 길이와
SHA는 위 성공 AAB와 같으며 syntheticValidationOnly=true 및 productionContract/
binary/distributable=false를 receipt에 유지했다. 원로그·manifest·스냅샷과 실패
archive3개도 보존했다. 사후 독립 검토에서 clean HEAD66510b4/Editor0/보호1095/
source12·원증거 불변, stage·fixture·폴더2·원 출력 부재, 성공 archive1개 및 실패
archive3개 동일 해시를 확인했다. 복원·cleanup·archive는 각각 한 번만 수행했다.

## 남은 실제 운영 주입 조건

준비 도구의 병합과 실제 운영 광고 활성화/출시 승인은 분리한다. 비공개 설정의 정확
계약·승인/출처·동의 분류 및 실제 App/Unit 연계, production callback 등록·pre/scene/post
실행, Login에 정확1개 manager 주입/원본 씬 불변, 실제 production Player compile plan,
최종 serialized rewarded field/Player gate/merged manifest·서명 및 정책·기기·스토어
검증은 완료되지 않았다. 최종 생산 실행 wrapper는 계속 무조건 거절한다. 이전 결과로
새 운영 빌드의 검증을 대체하지 않는다. 생산 전용 callback의 엄격한 Player define 검사와
합성 경로의 제한된 ENABLE_MARSHALLING_TESTS 처리도 별도로 재검토해야 한다.

추가 무제한 합성 재시도나 운영 실행 승인으로 해석하지 않는다. 새 Build/emit은
필요한 구체 실행 범위에 따라 판단하며 변경 없는 통과 테스트를 반복하지 않는다.
R10의 빈 placeholder 처리는 구조 재구성이며 원 scene handle identity 복원이 아니다.
원 Build 예외 masking/zero-scene batch 종료 의존 및 감독 복원 제약은 그대로 남는다.
