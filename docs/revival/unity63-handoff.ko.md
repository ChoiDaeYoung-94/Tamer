Unity 6.3 설치 재개 인계 (2026-09-11)

현재 사용자 방침은 **Unity 6000.0.81f1 유지, 6.3 설치 보류**다. 아래 내용은 이전 설치 시도의 보관 기록이며 현재 실행해야 할 필수 단계가 아니다. 실제 검증에서 Unity 버전으로 인한 차단이 확인된 경우에만 그 근거와 함께 설치 재개 승인을 요청한다.

대상은 **Unity 6000.3.24f1 LTS, Windows x86_64, 리비전 `4e7b9b5b6244`**와 Android Build Support 및 하위 모듈이다. Editor 다운로드와 검증은 완료됐지만, 공식 Windows 설치기의 관리자 승인 단계에서 `ELEVATION_CANCELLED`로 종료됐다. Editor와 Android 모듈은 아직 설치되지 않았다. 기존 6000.0 Editor와 프로젝트는 변경하지 않았으며, 임시 설치 경로 설정은 원래 값으로 복원했다.

캐시 파일은 PowerShell 기준 `Join-Path $env:APPDATA 'UnityHub\downloads\UnitySetup64-6000.3.24f1.exe'`에 있다.

| 항목 | 확인값 |
| --- | --- |
| 실제 크기 | `4,127,507,400` 바이트 — 공식 메타데이터와 일치 |
| 공식 API integrity | `md5-ENjr/zMUIPxT59bVnQVV8g==` |
| 직접 계산한 MD5 | `10d8ebff331420fc53e7d6d59d0555f2` — 공식 Base64 값을 디코딩한 결과와 일치 |
| 직접 계산한 SHA-256 | `7dbc333402d1948ff6e995672d3cf8953d3319ffb7cf1d428a7d3b8a99229829` |

공식 API는 이 설치 파일에 MD5만 제공한다. SHA-256은 로컬 파일 식별용 계산값이며 공식 SHA-256과 대조한 결과가 아니다. 출처는 [Unity 공식 릴리스 API](https://services.api.unity.com/unity/editor/release/v1/releases?limit=1&version=6000.3.24f1)와 해당 리비전의 [Windows x86_64 설치 파일](https://download.unity3d.com/download_unity/4e7b9b5b6244/Windows64EditorInstaller/UnitySetup64-6000.3.24f1.exe)이다.

사용자가 Windows 관리자 승인을 할 수 있는 PowerShell 세션에서 저장소 루트를 현재 디렉터리로 선택한 뒤 아래 명령으로 재개한다. 아래 절차는 설치 경로의 기존 값을 보관하고 성공·실패 뒤 복원한다. 실행 중 다른 Editor 설치와 설정 변경은 직렬화한다.

```powershell
$taskCli = (Resolve-Path 'tools/.local/unity-cli/1.0.0-beta.8/unity.exe').Path
$taskPathState = & $taskCli install-path --get --format json | ConvertFrom-Json
if (-not $taskPathState.success) { throw '기존 설치 경로 조회 실패' }
$taskOriginalPath = $taskPathState.data.path
$taskInstallExit = 1
try {
    & $taskCli install 6000.3.24f1 --architecture x86_64 --module android --cm --resume --yes --accept-eula --format ndjson
    $taskInstallExit = $LASTEXITCODE
} finally {
    & $taskCli install-path --set $taskOriginalPath --format json
    if ($LASTEXITCODE -ne 0) { throw '설치 경로 복원 실패: 보관한 기존 값으로 복원 필요' }
}
if ($taskInstallExit -ne 0) { throw '설치 미완료: CLI 결과의 실패 항목 확인 필요' }
& $taskCli editors verify 6000.3.24f1 --architecture x86_64 --format json
& $taskCli editors module list 6000.3.24f1 --architecture x86_64 --format json
& $taskCli editors --installed --format json
& $taskCli install-path --get --format json
```

명령 근거는 고정 CLI `1.0.0-beta.8`의 `install --help`다. 저장소에 고정된 CLI를 준비한 뒤 `& $taskCli install --help`로 같은 설명을 확인할 수 있다. `--resume`은 캐시 다운로드를 재사용하고, `--cm`은 Android 하위 모듈을 포함한다. `--yes --accept-eula`는 CLI 선택 및 라이선스 수락용이며 Windows 관리자 승인을 대신하지 않는다. `--no-elevate`는 사용하지 않는다. 기존 Editor 삭제·교체나 기본 Editor 변경 명령은 포함하지 않았다.

이 호스트의 원래 설치 루트는 `Join-Path $env:ProgramFiles 'Unity\Hub\Editor'`였다. 세션 강제 종료로 `finally`가 실행되지 않았다면, 보관한 기존 경로를 `& $taskCli install-path --set $taskOriginalPath --format json`으로 복원한다. 세션 변수가 사라졌다면 이 호스트에서 확인한 원래 경로를 사용한다.

완료 판정은 CLI 설치 성공, 구조 검증 성공, Android 모듈 목록 및 기존 6000.0 설치 유지 확인까지다. 구조 검증은 파일의 존재를 확인하며 프로젝트 호환성이나 빌드 성공을 보증하지 않는다. Editor 실행과 6.3 프로젝트 전환은 별도 worktree/PR에서 진행한다.
