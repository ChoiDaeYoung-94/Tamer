# 운영 빌드 입력 사전 검사

`tools/revival/audit_operating_build_inputs.py`는 기존 대화형 `Build/AOS/AAB` 메뉴를 실행하기 전에 PlayFab·Google Play Games 설정과 다음 legacy 버전 계획을 읽기 전용으로 검사하는 보조 도구입니다. `verify_release_candidate.py`의 `source`·`aab` 계약을 바꾸지 않습니다. 성공해도 `releaseReady`는 항상 `false`이며, 운영 빌드 실행이나 입력 수정 권한을 부여하지 않습니다.

빈 PlayFab TitleId 템플릿, `BuildInfo`의 code 0으로부터 계산되는 다음 code 1, 현재 `ProjectSettings`의 code를 다음 legacy code로 오인하는 문제를 실행 전에 드러내는 것이 목적입니다. 부족한 입력을 복원하거나 seed를 작성하지 않습니다.

## 호출과 결과

Windows Python 3.11 이상과 종료된 해당 checkout의 Editor를 전제로 총괄이 실행을 직렬화합니다. 프로젝트와 보고서에는 절대 로컬 경로를 지정합니다. 보고서 부모 디렉터리는 호출자가 미리 준비한 비공개 디렉터리여야 하며, 이름은 `.private.json`으로 끝나야 합니다. checkout 내부에서는 `Logs` 아래만 허용합니다. root와 출력 부모의 Windows 정규 경로를 비교하므로 8.3 짧은 이름 등 서로 다른 경로 표기를 섞어도 이 제한을 적용합니다. 보고서를 root 자체에 지정하면 입력을 열기 전에 고정 오류로 거절합니다. 경로 접근권한을 자동으로 설정하거나 비공개 ACL을 입증하는 기능은 없습니다.

아래 TitleId·게시 최대 code·커밋은 **합성 예시**입니다. 실제로 확인한 값으로 교체해야 합니다. 게시 최대 code를 모르면 실행하지 않습니다.

```powershell
python tools/revival/audit_operating_build_inputs.py `
  --project-path 'D:/path/to/owned-checkout/Tamer' `
  --expected-playfab-title 'ABC123' `
  --published-max-code 26 `
  --source-head 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' `
  --output 'D:/private-evidence/operating-inputs-unique.private.json'
```

필수 인자는 `--project-path`, `--expected-playfab-title`, `--published-max-code`, `--output`입니다. 같은 옵션을 중복 지정하면 거절합니다. `--source-head`는 선택 사항으로, 호출자가 기록한 40자리 소문자 SHA를 보고서에 남깁니다. 도구는 Git·Console·계정 API를 호출하지 않으며 이 주장을 독립 검증한 것으로 표시하지 않습니다.

종료 code는 다음과 같습니다.

| code | 의미 |
| --- | --- |
| 0 | 이 시점의 입력 검사 통과. 출시 준비 완료나 후속 실행 승인이 아닙니다. |
| 1 | 입력 스냅샷을 읽고 새 보고서를 만들었으나 의미 검사가 실패했습니다. `checks`·`issues`를 확인합니다. |
| 2 | CLI 인자 또는 경로·잠금·I/O 보호 조건이 실패했습니다. argparse의 인자 오류도 2입니다. 유효한 새 보고서가 없을 수 있으며 기존 자료를 보존합니다. |

표준 출력에는 통과 여부, `releaseReady=false`, 고정된 문제 code만 나옵니다. 비공개 보고서에는 입력의 논리적 상대 경로·identity·SHA-256, 고정한 실행 시각, 버전 계획과 검사 결과가 들어갑니다. 보고서와 그 해시·identity를 공개 이슈·PR·커밋에 복사하지 않습니다. I/O 예외의 원문 경로는 표준 출력에 덤프하지 않습니다. 보고서 쓰기 자체가 중단되면 불완전한 새 파일이 남을 수 있으므로 이를 성공 근거로 사용하지 않습니다.

## 읽는 범위와 보호

다음 일곱 경로만 입력으로 엽니다. 완료 표시만 없을 수 있으며 나머지 입력이 없으면 거절합니다.

| 입력 | 검사 범위 |
| --- | --- |
| `Assets/Scripts/Editor/BuildScript.cs` | 검토한 소스 pin, 버전 prefix·epoch·입출력 상수 |
| `Assets/Scripts/Editor/LegacyBuildOutputPlan.cs` | 검토한 소스 pin으로 다음 버전 계산 의미 고정 |
| `BuildInfo/buildinfo.txt` | `week,build,code` 세 비음수 int32 숫자, 다음 AAB code |
| `BuildInfo/finishversionsetting.txt` | 일반 파일 존재 여부와 비공개 스냅샷. 내용은 버전 의미로 해석하지 않음 |
| `Assets/ThirdParty/PlayFabSDK/Shared/Public/Resources/PlayFabSharedSettings.asset` | 단일 `TitleId`의 16진 표기와 전달한 예상값의 정확한 일치만 검사 |
| `ProjectSettings/GooglePlayGameSettings.txt` | 단일 비공백 `proj.AppId`·`and.ClientId`, 단일 `android.SetupDone=true` |
| `Assets/GooglePlayGames/Resources/PlayGamesSettings.asset` | 단일 비공백 `mAppId`·`mWebClientId`와 기존 Settings 값의 정확한 일치 |

PlayFab 설정 파일을 읽을 때 같은 파일의 다른 직렬화 필드가 bytes에 포함될 수 있습니다. 필드 추출·해석은 `TitleId`만 허용하며 credential·토큰을 추출하거나 보고하지 않습니다. 이 파일 전체의 해시와 identity는 비공개 보고서 안에서만 다룹니다. 원본 keystore·DER·비밀번호 입력과 `ProjectSettings/ProjectSettings.asset`은 열거나 해시하지 않습니다.

PGS 규칙은 기존 `private_ads_producer.py`의 PlayFab TitleId 제한과 `PrivateProductionAdsBuild.cs`/`test_private_privacy_gpgs_settings.py`의 설정 일치 조건을 참고한 좁은 정적 검사입니다. legacy text는 strict UTF-8(선두 BOM 허용)만 읽고, 실제 플러그인의 `StreamReader.ReadLine()`과 같은 CRLF·CR·LF 경계만 줄로 처리합니다. 줄·키·값 주변에서는 ASCII 공백과 탭만 제거하고 필드 키를 정확히 비교합니다. 첫 빈 줄에서 읽기를 끝내므로 그 뒤의 필드는 적용하지 않습니다. 비ASCII 공백과 허용한 탭·CR·LF 이외의 C0/C1 제어문자, U+2028/U+2029는 분리·키 보정 전에 입력 오류로 거절합니다. 이는 플러그인의 모든 인코딩·Unicode Trim 입력을 허용하는 파서가 아니라 좁은 허용 부분집합입니다. URL 디코딩 결과의 제어문자도 거절합니다. app/client ID 자체의 원문은 보고하지 않습니다. Unity import나 `Resources.Load` 대상 동일성, 실제 Google OAuth·PlayFab 서버 연결은 입증하지 않습니다. 다른 중복 Resources 에셋을 전체 검색하지 않습니다.

입력 부모 경로는 `windows_owned_files`의 Windows handle 잠금으로 junction·symlink를 거절하고, 입력은 쓰기·삭제 공유 없이 읽습니다. 파일별 identity·SHA-256을 잠금 중 다시 확인하며 hardlink와 입력 간 identity 중복도 거절합니다. 보고서와 입력의 경로 충돌, 기존 보고서, 기존 계획 AAB 출력과 비정상 상위 경로를 거절합니다. 보고서 경로는 전달받은 원래 root·출력 부모와 그 상위 경로에서 junction·symlink를 먼저 거절한 뒤, 기존 디렉터리의 `os.path.realpath(strict=True)` 결과로 root 포함 관계와 `Logs` 제한을 대조합니다. 정규 경로를 먼저 만들어 reparse 검사를 건너뛰지 않습니다. 보고서는 exclusive create로 만들어 검사 직후 생긴 다른 보고서를 덮어쓰지 않습니다. 출력 부모 디렉터리를 생성하지 않습니다.

디렉터리 잠금은 새 자식 파일 생성을 차단하지 않습니다. 없던 완료 표시가 스냅샷 중 생기면 재확인에서 거절하지만, 부재 상태를 원자적으로 예약하지는 않습니다. 계획 AAB 경로 역시 이 시점의 충돌 검사이며 이후 빌드의 출력 예약이 아닙니다. 입력 잠금은 짧은 스냅샷이 끝나면 해제하고, 보고서 작성 시 root와 출력 부모를 다시 잠근 상태에서 원래 경로와 정규 경로 관계를 재검사합니다. 디렉터리 전체 원자성, 관리자·raw volume 접근에 대한 보안 경계, 후속 Unity 실행 안전성을 보장하지 않습니다.

## legacy AAB 버전 계산

검토한 C# 소스의 UTF-8 BOM과 CRLF/LF 차이를 제외한 SHA-256을 고정했습니다. 소스가 달라지면 계산을 거절합니다. 후속 코드 변경 시 기존 pin만 새 해시로 바꾸지 말고 실제 계산·완료 표시·출력 계약을 함께 검토해야 합니다.

현재 정의는 `VERSION="1.0."`, `DAY_CALCULATEVERSION="01/21/2023 00:00:00"`입니다. 기준은 로컬 시각의 2023-01-21 00:00:00입니다. 도구는 시작 시 로컬 시각을 **한 번** 고정해 보고서와 모든 계산에 사용합니다. C# `DateTime.Now`와 같이 로컬 달력 시각의 경과 정수 일수를 7로 나눈 주차를 사용합니다. 기준 시각 이전은 거절합니다. 실제 Unity의 culture에 따른 날짜 문자열 파싱이나 import 성공까지 시험하지는 않습니다.

- 완료 표시가 없으면 현재 주차를 계산하고, 이전 주차와 같을 때 build를 1 늘립니다. 주차가 바뀌면 build는 0입니다. AAB code는 이전 code에 1을 더합니다.
- 완료 표시가 있으면 세 숫자를 그대로 재사용합니다. `01,02,027`이면 versionName은 `1.0.01`, 파일명은 `1.0.01.02.aab`, Android code는 숫자 27입니다. 재사용에는 `--allow-finish-reuse`가 필요하고, code가 게시 최대보다 커야 하며 계획 AAB 출력이 없어야 합니다. 표시가 없는데 이 옵션을 지정해도 실패합니다.
- `versionName`은 `1.0.<week>`이며 파일명은 `1.0.<week>.<build>.aab`입니다. build를 versionName에 포함시키지 않습니다.
- 음수·형식 오류·int32 overflow·Android code 범위 초과를 거절합니다. 정상 Android code 범위는 1~2,100,000,000입니다. CLI의 게시 최대 code는 0~2,100,000,000의 관찰값이고, 후보는 이 값보다 **엄격하게 커야** 합니다.

| 입력과 조건 | 계산·판정 |
| --- | --- |
| BuildInfo code 0, 완료 표시 없음, 게시 최대 26 | 다음 code 1이므로 실패합니다. 현재 ProjectSettings code 26을 대신 seed로 쓰지 않습니다. |
| BuildInfo code 26, 완료 표시 없음, 게시 최대 26 | 다음 code 27입니다. 다른 모든 검사가 통과할 때만 입력 검사 통과입니다. 도구는 seed 26을 작성하지 않습니다. |
| BuildInfo code 26, 완료 표시 있음, 게시 최대 26 | 재사용 code 26이므로 확인 옵션이 있어도 실패합니다. |
| BuildInfo code 27, 완료 표시 있음, 게시 최대 26 | 재사용 확인 옵션과 출력 부재 등 다른 조건을 만족할 때만 입력 검사 통과입니다. |

실제 메뉴도 준비와 실행 사이 reload·주차 경계를 고려해 실행 직전에 새 계획을 만듭니다. 이 사전 보고서의 고정 시각은 후속 메뉴 시각을 고정하지 않습니다. 날짜·주차 경계, 입력 또는 게시 최대가 바뀌면 총괄이 새 입력 스냅샷을 확인해야 합니다. 같은 조건의 성공 시험을 기록 목적으로 반복하지 않습니다.

## 검증과 다음 단계

2026-10-26 구현의 관련 합성 시험은 **34개 통과·실패0·skip0**입니다. 독립 리뷰에서 발견한 PGS 특수 줄바꿈 해석과 Windows 경로 별칭 문제를 수정했으며, 같은 호스트의 실제8.3 별칭으로 `Assets` 출력 거절과 `Logs` 출력 허용을 확인했습니다. 초기23개 결과와 수정 후34개 결과를 합산하지 않습니다.

실제 입력 읽기 검사는 source `289d8ba1889aff153b86355ea259dd60afe68c48`에서 **1회, 예상 거절 exit1**입니다. 빈 PlayFab Title과 legacy 다음 code1/명시 관측 최대26의 충돌을 함께 검출했고 PGS 값 일치는 통과했습니다. 이는 도구가 미준비 입력을 찾아낸 결과이며 운영 빌드 준비 완료가 아닙니다. 입력 파일은 변경하지 않았고 실제 키·암호·Editor·빌드·계정 API를 실행하지 않았습니다. 원문·해시/identity 보고서는 비공개로 보존합니다. 이후 문서 보충을 이 검사나 제품 시험의 재실행으로 표현하지 않습니다.

```powershell
python -m unittest discover -s tools/revival -p test_operating_build_inputs.py -v
```

시험은 임시 디렉터리의 가짜 설정과 공개 C# 복사본만 사용합니다. code 0/26, 주차 경계, 완료 표시 재사용, 잘못된 ID·PGS 불일치·중복·missing, overflow, 출력 충돌·late race, 입력 보존·짧은 잠금, hardlink·junction·가능한 symlink 거절과 비공개 출력 제한을 검증합니다. 추가 회귀는 CRLF·CR·LF/첫 빈 줄/제어문자/키·인코딩, 경로 정규화 실패와 별칭 제한의 논리 mock, 정규화 전 junction 거절, 최종 root 잠금 재검사와 root 동일 출력의 고정 CLI 오류를 확인합니다. 실제 WinAPI `GetShortPathNameW`로 얻은 8.3 별칭 검사는 논리 mock과 별도이며, OS가 서로 다른 짧은 이름을 제공하지 않으면 그 시험만 skip합니다. 볼륨의 8.3 정책은 바꾸지 않습니다. 기존 symlink fixture는 생성 권한이 없을 때 그 분기를 실행하지 않으므로 전체 skip0만으로 실제 symlink 시험 실행을 주장하지 않습니다. 실제 키·설정·계정·Editor·ADB·빌드·스토어를 호출하지 않습니다.

입력 검사 이후에도 기존 `verify_release_candidate.py source`, 승인/입력 binding, 실제 운영 서명 AAB 및 `aab` 검사, upload 인증서·게시 최대의 Console 대조, 운영 계정·저장·No Ads·구매 복원, 실제 ARM64/16KB 기기·스토어 전달 검증은 별도입니다. 이 문서는 최초 복구 계획 전체의 완료 보고가 아닙니다. 전체 잔여 작업은 [최신 전체 잔여 목록](remaining-recovery-work.ko.md)을 유지합니다.
