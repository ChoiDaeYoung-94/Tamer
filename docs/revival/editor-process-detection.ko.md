# 설정 복구 전 Unity Editor 판별

## 현재 동작

`tools/revival/ProjectSettingsSnapshot.ps1`의 `Get-RevivalProjectEditor`는 설정 snapshot·restore 전에 실행 중인 Unity Editor의 프로젝트를 확인합니다. 과거 `Unity.exe` 이름과 명령행의 프로젝트 경로 부분 문자열만 비교하여 프로젝트 로컬 CLI helper도 Editor로 오인했습니다. 이 수정은 [PR #375](https://github.com/ChoiDaeYoung-94/Tamer/pull/375), main 커밋 `ad030c58ff492f8683f755063cd07f671b4eb7b7`에 이미 반영되었습니다.

- CIM 조회는 `Unity.exe` 이름으로 제한합니다. `tools/.local/unity-cli/<version>/unity.exe` 위치의 CLI는 제외합니다.
- 그 외 후보는 `Editor/Unity.exe` 실행파일 위치와 Windows 인수 경계를 확인합니다. 단일 `-projectPath`의 절대 경로를 정규화한 뒤 전체 경로를 대소문자 구분 없이 비교합니다.
- 다른 프로젝트, 경로 접두사만 같은 프로젝트, `-logFile` 등 다른 인수에 등장한 경로는 현재 프로젝트로 선택하지 않습니다.
- 실행파일 위치·명령행·프로젝트 인수가 없거나 모호하고, CIM 조회·인수 파싱이 실패하면 예외로 설정 작업을 차단합니다. 실제 Editor의 명령행을 읽지 못하는 경우도 그대로 차단합니다.
- 프로세스 목록 판별이 모두 끝나기 전에는 일치 항목을 내보내지 않습니다. 일치 Editor 뒤에 모호한 항목이 있어도 부분 결과를 반환하지 않습니다.

이 판별은 경로 문자열 정규화 기준입니다. Junction·8.3 별칭의 파일 동일성, 서명·바이너리 해시 확인까지 보장하지 않습니다. 알려지지 않은 위치의 `Unity.exe`나 명시적인 `-projectPath` 없는 Editor는 자동으로 무시하지 않습니다.

## 2026-10-18 최소 회귀 검증

최신 main `6fd7f7d49ea4f449033341ae5bdab51853673a13`에서 기존 수정이 확인되어 실행 코드를 중복 수정하지 않았습니다. 이번 변경은 `test_settings_snapshot.py`의 실제 Save/Restore 호출 경계와 혼합 프로세스 목록 테스트, 이 설명 문서입니다.

소유 checkout은 `D:/codex/worktrees/editor-process-detection-1018/Tamer`, 브랜치는 `codex/editor-process-detection-1018`입니다. 위 main과 이 PR의 테스트 diff를 기준으로 Windows / Python `3.12.14` / PowerShell `7.6.5 Core`에서 다음 명령을 1회 실행하여 **7개 테스트가 모두 통과**했습니다(4.709초, 실패 0회, skip 0개).

```powershell
python -m unittest discover -s tools/revival -p test_settings_snapshot.py -v
```

테스트는 CIM을 합성 객체로 대체하고 임시 디렉터리의 합성 설정 파일만 사용합니다.

| 검증 범위 | 확인한 결과 |
| --- | --- |
| helper·다른 프로젝트·동일 프로젝트·인용 경로 | 기존 선택 테스트 유지: 공백, escaped quote, 끝 backslash, 대소문자·슬래시·`..` 정규화, 경로 접두사·다른 인수의 경로 배제 |
| 정보 누락·모호성·CIM 오류 | 기존 거부 테스트 유지: 실행파일 미확인, 명령행 없음, 프로젝트 인수 누락·중복·상대 경로, 조회 실패 차단 |
| helper와 다른 프로젝트가 존재하는 Save/Restore | 실제 판별 함수를 호출하여 snapshot 생성과 원본 SHA-256 복구 성공 |
| 같은 프로젝트 Editor·읽지 못한 명령행·미확인 실행파일 | 각 조건에서 Save와 Restore 모두 거부, 현재 설정·복구 사본 SHA-256 유지, 추가 snapshot 생성 없음 |
| helper·일치 Editor·모호한 Editor 혼합 목록 | 모호한 항목이 앞·뒤인 두 순서 모두 예외, 부분 출력 0개 |
| 기존 바이트 복구·상주 Editor·사본 변조 | 기존 세 회귀 테스트 통과 |

고정 도구 기준은 Unity `6000.3.25f1` / revision `e1dba0a9aba4`, CLI `1.0.0-beta.8`, Pipeline `0.6.0-exp.1`, Android min25/target36/ARM64입니다. `toolchain.json`과 `packages-lock.json`을 읽어 확인했으며 변경하지 않았습니다.

실제 Unity 실행·프로세스 조작·빌드·앱 원본 복구·ADB·실기기·계정 API는 검증하지 않았습니다. APK SHA-256은 해당하지 않습니다. 과거 Windows PowerShell 5.1의 실행 정책 차단 이력은 미검증으로 유지하며 재시도하거나 정책을 변경하지 않았습니다. 이 합성 테스트 결과는 호스트 중단 조건 해제나 실제 앱 복원 PASS의 근거가 아닙니다.
