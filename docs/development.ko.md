# Tamer 개발·검증 안내

이 문서는 Windows PowerShell 기준이다. 현재 동작하는 복구 도구와 확인된 한계를 설명한다.
최신 실행 결과와 남은 작업은 [통합 검증 요약](revival/completion-summary.ko.md)과 [통합 데이터](revival/integration-validation.json), 초기 이력은 [기준 빌드 기록](revival/baseline.ko.md), 단계별 추적은 [로드맵](revival/plan.ko.md)을 따른다.

## 1. 새 작업본 준비

1. Git, Python 3.11 이상, Unity Hub와 **6000.0.81f1** Editor를 준비한다.
2. 같은 Editor에 Android Build Support, SDK/NDK Tools, OpenJDK 모듈을 설치하고 Unity 라이선스를 확인한다.
3. 저장소를 자신의 개발 폴더로 clone한다. 기존 원본은 별도 위치에 보존한다.
4. 권한 있는 원본 또는 비공개 아카이브를 준비한다. 아카이브 루트 아래에 `Assets/ThirdPartyAssets` 등 manifest와 같은 상대 경로가 있어야 한다.
5. **Editor를 열기 전에** 아래 명령을 실행한다.

```powershell
git clone https://github.com/ChoiDaeYoung-94/Tamer.git
Set-Location Tamer
python --version
python tools/revival/restore_assets.py --source 'D:/path/to/authorized-original'
if ($LASTEXITCODE -ne 0) { throw '복원 중단: 원본과 충돌 파일을 확인하세요.' }
python tools/revival/restore_assets.py --verify
if ($LASTEXITCODE -ne 0) { throw '복원 검증 실패' }
python tools/revival/install_cli.py
if ($LASTEXITCODE -ne 0) { throw 'CLI 설치 실패' }
$projectPath = (Resolve-Path .).Path
$unityCli = Join-Path $projectPath 'tools/.local/unity-cli/1.0.0-beta.8/unity.exe'
& $unityCli --version
```

복원은 누락된 파일만 생성하며 원본과 기존 수정 파일을 덮어쓰지 않는다. 해시 불일치가 나면 원본 버전과 파일을 비교한다. `--inventory`로 현재 파일을 정답으로 바꾸지 않는다. 상세 분류와 서비스 설정 제외 규칙은 [에셋 복원 문서](revival/dependencies.ko.md)에 있다.

CLI 설치기는 공식 manifest와 고정 SHA-256을 확인하고 프로젝트의 `tools/.local`에 실행 파일을 둔다. CLI/Pipeline 버전은 [toolchain.json](../tools/revival/toolchain.json)에 고정되어 있다. Pipeline은 이미 UPM manifest와 lock에 포함되므로 새 환경에서 임의 최신 버전을 설치할 필요가 없다.

공개 저장소만으로 구매 에셋을 취득할 수는 없다. PlayFab 설정은 계정 값 없는 템플릿으로 만들어지므로 clone 직후 실제 로그인은 구성되어 있지 않다. 운영 키를 문서·채팅·커밋에 넣지 않는다.

## 2. 기본 검증

프로젝트별 `Library`를 독립 생성한다. 다른 checkout의 Library를 복사하거나 공유하지 않는다. 처음 import와 Android IL2CPP 빌드는 오래 걸릴 수 있다. 여러 작업을 병렬 진행할 때는 무거운 import/build 실행 시간을 조정한다.

```powershell
# 이 checkout의 Editor를 닫은 다음 실행한다.
# 테스트만 필요한 경우:
./tools/revival/Run-Baseline.ps1 -TestsOnly

# 전체 검증이 필요한 경우(위 테스트를 별도로 반복할 필요 없음):
./tools/revival/Run-SdkValidation.ps1

# 공식 가이드의 추가 RELRO 조건은 별도로 확인한다.
python tools/revival/verify_native_alignment.py --strict-relro --output Logs/revival/native-alignment-strict.json
git status --short
git diff --check
```

전체 `Run-SdkValidation.ps1`은 Python 회귀 → `Run-Baseline.ps1`의 복원 검증·EditMode·격리 APK 빌드·메타데이터/서명 검사 → GUID → 네이티브 LOAD/ZIP 검사를 순서대로 수행한다. 기본 Editor 경로가 다르면 `-EditorPath 'C:/path/to/Editor/Unity.exe'`를 전달한다. 테스트는 고정 버전으로 Editor를 찾고 빌드는 지정한 실행 파일을 사용한다.

전체 스크립트 성공은 추가 RELRO 검사 통과를 뜻하지 않는다. 최종 통합 APK의 `--strict-relro`는 5개 끝 주소 조건 실패로 종료 코드 1이며, 실제 16KB 기기·AAB 검증도 남아 있다. 판정 범위는 [네이티브 검사 문서](revival/native-alignment.ko.md)를 따른다.

| 결과 | 확인 위치 |
| --- | --- |
| 테스트 실행 결과 | `Logs/revival/editmode.xml` |
| Unity 빌드 결과 | `Logs/revival/build-summary.json` |
| APK | `Build/revival/Tamer-development.apk` |
| APK 해시·ID·API·ABI·debug 서명 | `Logs/revival/apk-verification.json` |
| 자체 YAML 외부 GUID 검사 | `Logs/revival/guid-audit.json` |
| 네이티브 LOAD/ZIP 및 별도 RELRO | `Logs/revival/native-alignment.json`, `native-alignment-strict.json` |

검증 APK의 첫 씬은 게임 스크립트가 없는 `RevivalSmoke`다. 기존 게임 씬 5개도 빌드에 포함하지만 자동으로 운영 로그인·저장·구매·광고에 진입하지 않는다. 개발 빌드는 별도 앱 ID와 debug 서명을 사용하므로 운영 앱/진행도와 분리된다. 기존 `src/AeDeong.keystore`를 수정·교체하지 않는다.

APK 메타데이터 검사는 기기 설치/실행, 모든 native 라이브러리의 16 KB 호환성, 실제 사용자 계정, 구매 복원, 광고 닫힘이나 심사 승인을 보장하지 않는다. 각 검증은 별도의 근거로 기록한다.

## 3. Editor와 Pipeline 사용

복원 검증이 끝나면 Unity Hub에서 **자신의 checkout 경로**를 6000.0.81f1로 연다. 여러 작업이 열려 있어도 다른 작업본의 Editor/PID를 조작하지 않는다. 아래 `$projectPath`와 `$unityCli`는 1절에서 설정한 같은 PowerShell 세션의 값이다.

```powershell
& $unityCli status --format json
& $unityCli command --project-path $projectPath
& $unityCli command editor_status --project-path $projectPath --format json
& $unityCli command set_autotick --enable true --project-path $projectPath --format json
& $unityCli command get_console_logs --project-path $projectPath --format json
& $unityCli command list_open_scenes --project-path $projectPath --format json
& $unityCli command get_scene_hierarchy --project-path $projectPath --format json
```

`status`의 연결 목록에서 project 경로와 Editor를 확인한다. 상태가 ready이고 명령 목록에 실제로 노출된 명령을 사용한다. C# 컴파일 오류로 Safe Mode가 되면 Pipeline도 로드되지 않을 수 있다. 이때 연결 실패를 프로젝트가 없다는 의미로 해석하지 말고 해당 checkout의 컴파일 로그부터 확인한다.

라이브 테스트 전에 열린 모든 씬을 저장한다. 저장하지 않은 씬이 있으면 기준 씬 전환을 거부한다. 라이브 테스트는 다음과 같이 실행한다. 비동기 요청의 성공 응답은 완료 결과가 아니다.

```powershell
& $unityCli command run_tests --mode editor --filter Revival --async_tests true --project-path $projectPath --format json
& $unityCli command test_status --project-path $projectPath --format json
```

반환된 작업 상태와 최종 pass/fail 및 테스트 개수를 확인한다. 배치 검증은 같은 checkout의 Editor를 정상 종료한 뒤 실행한다. 열린 씬·프리팹·직렬화 에셋은 Editor/Pipeline을 통해 수정하고 저장한다. C# 코드는 파일에서 수정한 뒤 컴파일 완료와 Console 오류를 확인한다.

CLI는 설치 시 프로젝트 로컬 `unity-cli` 스킬을 제공한다. Pipeline import 후 `& $unityCli skill install codex --local --yes --non-interactive`를 다시 호출하면 가용 스킬을 반영할 수 있다. 실제 설치된 `.agents/skills`의 명령 문서를 사용한다. 별도 MCP 등록은 현재 절차의 필수 조건이 아니다.

## 4. 일상적인 코드 변경과 PR

1. [#90](https://github.com/ChoiDaeYoung-94/Tamer/issues/90)과 열린 이슈/PR을 확인해 중복 작업을 피한다.
2. 최신 기준에서 `codex/<작업명>` 브랜치를 만들고 담당 파일과 의존 PR을 공유한다.
3. 기존 `.meta`, 직렬화 필드와 저장 키의 호환성을 유지하며 작은 변경을 만든다.
4. 변경 동작을 검증할 수 있는 운영 서비스 없는 회귀 테스트를 실행한다. 관련 런타임/의존성 변경은 격리 APK도 검사한다.
5. 생성 캐시·원본 에셋·서비스 설정·원시 로그가 staging에 없는지 확인한다.
6. 변경 파일만 명시적으로 stage하고 커밋·push·PR을 생성한다.
7. 검토와 필요한 검사가 통과한 **같은 SHA**를 병합한다. 통합 담당이 있는 병렬 작업에서는 담당자에게 인계한다.

```powershell
git switch -c codex/my-change
git diff --check
git diff --stat
git status --short
# 검토한 파일 경로만 git add에 지정한다.
```

PR에는 문제를 일으키는 입력과 변경 후 동작, 관련 이슈·선행 PR, 브랜치/SHA, 실행한 검사·실패·미검증, 되돌리는 방법을 적는다. 예를 들어 “EditMode 통과/격리 APK 검사 통과/기기 미검증/스토어 미확인”을 분리한다. 선행 PR이 갱신되거나 병합 충돌을 해결했다면 영향을 받는 검사를 다시 실행한다.

Unity가 기존 설정을 재직렬화했을 때는 diff를 확인하고 의도한 변경인지 판단한다. 변경을 숨기기 위한 일괄 `git add -A`, 원본 reset, 타 작업 branch 전환을 하지 않는다. 신규 C# 파일에도 고유 `.meta`가 필요하다.

## 5. 문제 해결

| 증상 | 확인 순서 |
| --- | --- |
| `Existing file differs` / source hash mismatch | 경로와 원본 버전을 확인한다. 수정 파일은 보존하고 manifest와 비교한다. |
| 복원 전 누락 에셋/컴파일 오류 | Editor를 닫고 복원을 완료한다. `.meta`가 원본과 같은지 확인한다. |
| Pipeline에 프로젝트가 없음 | 경로, Editor 실행 여부, Safe Mode/C# 오류, UPM 복원을 확인한다. |
| `Close this project Editor before batch validation` | 해당 checkout의 Editor만 저장 후 종료한다. |
| Unity 라이선스/exit 198 | CLI `auth status`/`license status`와 현재 프로젝트 계정 선택을 확인한다. 기존 기본 계정을 전역 변경하지 않는다. |
| 테스트 CLI exit 8 | 테스트가 실행되어 실패한 결과다. XML의 실패 원인을 수정한다. |
| 테스트 CLI exit 6 / 결과 XML 없음 | 컴파일·라이선스·시간 초과 등 실행 실패다. 테스트 통과로 기록하지 않는다. |
| legacy marker 오류 | `Build/AOSSettingAPK.txt`, `AOSSettingAAB.txt`, `checkedBuilding.txt`와 이전 작업 소유권을 확인한다. 자동 삭제로 우회하지 않는다. |
| APK 검증 실패 | 실제 ID/API/ABI/서명과 `build-summary.json`을 대조한다. 오래된 APK를 성공 산출물로 사용하지 않는다. |
| GUID audit 실패 | UPM import 완료와 복원 해시를 확인하고 JSON의 참조 경로를 조사한다. |

비공개 설정과 원시 로그는 `Logs` 등 로컬에서 조사한다. 공개 PR에는 비밀이 없는 요약, 커밋과 artifact SHA-256만 남긴다. 확인되지 않은 계정·구매 증빙이나 실제 기기 검증 결과를 추정해 채우지 않는다.

## 6. 빌드·배포 범위

현재 권장 로컬 진입점은 `RevivalBuild.BuildAndroidDevelopment`다. 기존 `BuildScript` 메뉴와 외부 `unity-cicd` 도구는 날짜 기반 버전 변경, marker/reload, 운영 서명 흐름을 사용하므로 복구 검증 명령으로 사용하지 않는다. iOS 빌드는 검증하지 않았다.

기존 `.github/workflows/cicd.yml`/fastlane/App Center 구성은 기록으로 남긴다. CI/CD는 소유자 방침으로 보류했고 workflow dispatch, runner 등록, 자동 배포와 서비스 이행을 하지 않는다. 향후 배포 작업은 서명 관계·스토어 설정·대상 AAB/versionCode·트랙·검증 근거를 따로 갖춰 진행한다.
