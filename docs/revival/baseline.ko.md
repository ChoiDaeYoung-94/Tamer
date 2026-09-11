# 로컬 복구·기준 빌드

검증일: 2026-09-11. 관련: [#90](https://github.com/ChoiDaeYoung-94/Tamer/issues/90),
[#92](https://github.com/ChoiDaeYoung-94/Tamer/issues/92),
[접근권한 #94](https://github.com/ChoiDaeYoung-94/Tamer/issues/94).

## 작업본과 원본

- 구현: `C:\Users\pc_17\Documents\ChatGPT\Tamer`, `codex/revival-baseline-build-92`.
- 소유자 원본: `D:\meee\git\Tamer`, `main`. 읽기 전용 복원 소스로 사용했으며 checkout·동기화·삭제하지 않았다.
- 시작 시 등록 폴더에는 커밋 없는 빈 Git 저장소만 있었다. 원격 main과 원본 HEAD가 모두 `c47c90217d45e8c6538c57a0924fa852abfbd736`임을 확인하고 작업본을 구성했다. 원본 Library는 복사하지 않았다.
- 별도 검증 clone: `.revival-local/restore-validation`. Git 파일과 공개 복원 스크립트로 원본을 복원하고, 이 clone의 Library 없이 임포트·배치 테스트·개발 빌드를 검증한다.
- 준비 문서 [PR #93](https://github.com/ChoiDaeYoung-94/Tamer/pull/93)은 별도 미병합 draft이며 이 구현에서 병합하지 않았다.

## 고정 도구

| 항목 | 실제 확인 |
| --- | --- |
| Unity | 6000.0.81f1 / 6238fec1e98f |
| Unity CLI | 1.0.0-beta.8, 공식 Windows x64 배포본 SHA-256 고정 |
| Pipeline | com.unity.pipeline 0.6.0-exp.1, UPM lock 포함 |
| skills | CLI 내장 unity-cli, Pipeline 패키지의 unity-pipeline을 프로젝트 로컬에 설치 |
| Android | Platform 36 / Build Tools 36.0.0 / NDK r27c (27.2.12479018) |
| Java / Gradle / AGP | Temurin JDK 17.0.18+8 / 9.1.0 / 9.0.0 |
| Python | 표준 라이브러리만 사용, 3.11 이상; 이 PC에서 3.14 실행 |

CLI는 `tools/.local`에 설치하여 전역 PATH나 다른 프로젝트 MCP 설정을 덮어쓰지 않는다.
`toolchain.json`과 `install_cli.py`는 공식 버전 manifest와 바이너리 해시를 모두 확인한다.
실행 중 Editor 제어는 shell CLI를 사용하며 MCP 등록은 하지 않았다.

근거: [CLI 릴리스](https://docs.unity.com/en-us/unity-cli/release-notes),
[공식 설치 스크립트](https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1),
[Pipeline 변경 이력](https://docs.unity3d.com/Packages/com.unity.pipeline@0.6/changelog/CHANGELOG.html),
[Unity 공식 skills](https://github.com/Unity-Technologies/skills).

## 재현 명령

소유자가 사용할 수 있는 원본/비공개 아카이브와 활성 Unity 라이선스가 필요하다.
새 clone에서 Editor를 열기 전에 [의존성 복원](dependencies.ko.md)을 수행한다.

```powershell
python tools/revival/restore_assets.py --source '<권한 있는 원본 경로>'
python tools/revival/restore_assets.py --verify
python tools/revival/install_cli.py
./tools/revival/Run-Baseline.ps1
python tools/revival/audit_guids.py
```

기본 Editor 설치 경로가 다르면 `-EditorPath '<Unity.exe 절대 경로>'`를 전달한다.
Android 검사 도구가 별도 위치면 `verify_apk.py --android-player '<AndroidPlayer 경로>'`로 확인한다.
같은 checkout의 Editor를 닫고 실행한다. 다른 프로젝트 Editor를 종료하지 않는다.
빌드 기본 명령은 다음과 같다.

```powershell
& ./tools/.local/unity-cli/1.0.0-beta.8/unity.exe build '<프로젝트 절대 경로>' `
  --editor-version 6000.0.81f1 --target Android `
  --execute-method RevivalBuild.BuildAndroidDevelopment `
  --log-file '<프로젝트 절대 경로>/Logs/revival/android-build.log' `
  --no-tail --non-interactive
```

버전/ABI/SDK가 기준과 다르거나 legacy marker가 있으면 빌드 진입점이 실패한다.
기존 BuildScript의 날짜 기반 버전 증가·marker/reload·운영 서명 경로는 호출하지 않는다.
앱 ID를 `com.AeDeong.MonsterTamer.revival`로 분리하고 debug signing을 사용한다.
기존 게임 씬 5개를 포함하되, 첫 씬은 게임 MonoBehaviour가 없는 `RevivalSmoke`다.
`TAMER_REVIVAL_SMOKE`와 `TAMER_TEST_ADS`, Development 옵션을 사용한다.
빌드 후 앱 ID/서명 사용 여부/backend/App Bundle 설정을 복원한다.
Unity/EDM이 별도로 재직렬화하는 설정·캐시 파일이 있을 수 있으므로 Git diff를 확인한다.

첫 CLI 배치 테스트 후 다른 저장 계정의 라이선스를 사용하여 다음 Editor가 exit198로 중단된 사례가 있었다.
`auth status`와 `license status`를 확인하고 **Tamer에만** 기존 라이선스 계정을 pin한 뒤 기존 Personal 라이선스를 활성화하여 해결했다.
`license activate`가 `products: []`를 반환한 것은 활성화 성공 증거로 사용하지 않았다.
계정/라이선스 정보는 프로젝트 Git에 저장하지 않는다.

## 검증과 결과

기계 판독 결과는 `validation.json`, 로컬 상세 로그는 `Logs/revival`에 있다.

- 복원: 공개 SDK와 비공개 에셋 원본 **4,537개**의 해시 확인. 서비스 설정 2개는 제외하고 중립 템플릿을 제공한다.
- 복원 스크립트: 3개 테스트 통과 — 재실행/원본·GUID 보존, 충돌 시 전체 복사 전 중단, 경로 이탈 거부.
- Unity EditMode: 3개 테스트 통과 — 기준 설정/marker, 검증 씬 저장·재열기, 원래 게임 씬 5개의 누락 스크립트 검사.
- Pipeline: 프로젝트/Editor 식별, autotick, Console 오류 읽기, 씬 목록·hierarchy, C# 호출, 재컴파일 완료, 비동기 테스트 완료 확인.
- 캡처: Edit Mode camera 방식 1280×720 PNG 생성·육안 확인. CLI의 `save_path Logs/...`가 실제로는 `Assets/Logs/...`로 해석되어 그 경로에서 읽은 후 로컬 Logs에도 보관했다.
- 참조 검사: 자체 YAML 132개 검사. 구형 URP XR 참조 1개를 null로 정리한 뒤 미해결 외부 GUID 0개. 로컬 fileID/시각적 외관/모든 구매 에셋의 사용하지 않는 데모 씬까지 보장하는 검사는 아니다.
- 첫 Android 개발 APK: BuildReport `Succeeded`, errors 0. APK 자체의 min24/target36/ARM64/1.0.5/code26/debug 서명을 aapt2/apksigner로 확인했다.
- 연결된 Android 기기 0대. APK 설치·실행, 실제 로그인/저장/구매/광고, 스토어 검증은 수행하지 않았다.

주요 로컬 파일:

| 파일 | 용도 |
| --- | --- |
| `Build/revival/Tamer-development.apk` | 격리 개발 APK |
| `Logs/revival/build-summary.json` | Unity BuildReport 요약 |
| `Logs/revival/apk-verification.json` | 실제 APK 해시·ID·SDK·ABI·서명 확인 |
| `Logs/revival/editmode.xml` | NUnit 테스트 결과 |
| `Logs/revival/pipeline-tests.json` | CLI 비동기 테스트 완료 결과 |
| `Logs/revival/guid-audit.json` | YAML 외부 GUID 검사 |
| `Logs/revival/smoke.png` | 검증 씬 캡처 |

원시 로그는 계정/기기/로컬 경로 정보를 포함할 수 있으므로 자동 공개 업로드하지 않는다.
테스트 과정에서 수정한 최초 컴파일 오류와 테스트 teardown 오류는 최종 통과 결과와 구분한다.

## CI/CD와 남은 범위

사용자의 2026-09-11 지시에 따라 **CI/CD는 개인용으로 보류**한다.
이 브랜치에서는 기존 `.github/workflows/cicd.yml`의 push trigger를 제거하고 첫 job을 비활성화했다.
Unity 2020 macOS 경로, App Center/fastlane 구성과 의존 job들은 기록으로 보존한다.
실제 workflow dispatch, runner 등록, 배포, 새 서비스 이행은 하지 않았다.
이 변경은 PR이 병합되기 전까지 원격 main의 workflow 파일에 반영된 것은 아니다.

App Center **Distribution 서비스 자체는 종료되어 설정 파일을 복원해도 되살릴 수 없다**.
Analytics/Diagnostics 지원 연장과 구분한다. [Microsoft 종료 공지](https://learn.microsoft.com/en-us/appcenter/retirement)

#92의 CI 실행 완료 조건은 사용자 방침상 보류한다. 구매 에셋의 정확한 구매 버전/라이선스 증빙,
비공개 배포 아카이브 운영, 기존 서명키의 스토어 관계는 후속 확인 대상이다.
[#91](https://github.com/ChoiDaeYoung-94/Tamer/issues/91)의 Families 광고 문제를 이 빌드 성공으로 해결 처리하지 않는다.

공개 열람/clone을 유지하면서 외부의 임의 push/merge/관리 권한을 차단하는 점검은
[#94](https://github.com/ChoiDaeYoung-94/Tamer/issues/94)에서 진행한다.
관리 작업의 예비 조회는 `main protected:false`, `rulesets:[]`이며 collaborator 목록은 미확인이다.
이는 무권한 방문자가 병합할 수 있다는 뜻이 아니다. 이 구현에서는 저장소 권한을 변경하지 않았다.
