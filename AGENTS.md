# Tamer 작업 규칙

- 테스트는 기본 검증을 유지하되 토큰과 실행 비용을 줄이도록 변경과 관련된 최소 필요 범위로 수행한다. 변경 없는 재실행과 근거 없는 전체 회귀 반복을 하지 않는다.
- 같은 테스트가 2회 이상 실패하면 해당 테스트와 그 결과에 의존하는 작업을 중단한다. 실패 원인(미확정이면 그 사실), 지금까지의 시도와 결과, 필요한 사용자 결정을 먼저 보고하고 사용자 승인 전에는 3차 재시도를 하지 않는다. 테스트 이름 변경이나 실질적으로 같은 조건의 유사 테스트 실행으로 실패 횟수를 우회하지 않는다. 중단된 결과에 의존하지 않는 독립 작업은 계속할 수 있다.

- Git 커밋 제목과 본문은 한국어로 작성한다. Conventional Commits 접두사(feat/fix/refactor/docs 등), 코드 식별자·경로·제품명은 필요시 원문을 유지하되 변경 목적과 설명은 한국어로 쓴다. squash/merge 커밋 메시지도 직접 지정할 수 있으면 한국어로 작성한다. 이미 공유된 기존 커밋은 이 규칙 적용만을 위해 amend/rebase/force-push로 재작성하지 않는다.
- PR 병합 확인 후 불필요한 원격 작업 브랜치를 삭제하고 `git fetch --prune`으로 추적 참조를 정리한다. 삭제 직전에 최신 `origin/main`에 브랜치 tip이 포함되는지, 관련 PR이 merged인지, 열린 PR이나 병합 후 추가 커밋이 없는지 확인한다. 미사용 병합 로컬 브랜치는 `git branch -d`로 정리한다. `main`·기본 브랜치·미병합·작업 중 브랜치와 다른 worktree가 체크아웃한 로컬 브랜치는 보존하며 강제로 체크아웃을 바꾸지 않는다. 사용 중인 worktree의 원격 참조는 담당자가 추가 작업이 없다고 확인한 뒤에만 삭제한다. worktree 파일·Library·비공개 증거는 삭제하지 않는다. 정리 결과와 남긴 브랜치의 이유를 기록한다.
- 원격 브랜치 삭제에는 확인한 tip SHA를 명시한 expected lease를 사용한다. 확인 이후 tip이 바뀌면 삭제를 중단하고 다시 검토한다.
- 현재 복구·검증 단계의 Unity는 `6000.0.81f1`로 유지한다. 도구/SDK 기준은 `tools/revival/toolchain.json`과 `Packages/packages-lock.json`을 따른다. 사용자 승인된 후반 작업으로 기능·광고·저장 복구 안정화 후, 최종 출시 AAB·스토어 검증 전에 실행 시점의 최신 정식 LTS와 안정 패치를 공식 자료로 확인하고 SDK·플러그인·Android 호환성을 평가하여 격리 브랜치에서 업그레이드한다. 지금 설치·버전을 변경하거나 정확한 후속 버전을 추정하지 않는다. 전환 후 영향 범위 테스트·실기기·16KB·최종 AAB 검증을 수행하며 이전 Unity의 결과로 대체하지 않는다. 일반적인 업그레이드 작업은 이미 승인됐으며 호환 불가 등 중대한 변경만 구체적으로 보고한다.
- 현재 구현 checkout은 이 폴더이며 `D:\meee\git\Tamer`는 소유자의 별도 원본이다. 원본의 브랜치 변경·동기화·삭제·이동을 하지 않는다.
- `.meta`와 GUID를 보존한다. 에셋 복원은 Editor를 열기 전에 `python tools/revival/restore_assets.py --source <권한 있는 원본>`으로 수행한다. 해시 충돌을 임의 덮어쓰기/manifest 재생성으로 숨기지 않는다.
- 구매/출처 불명 에셋과 Avalon 폰트는 비공개 복원 대상이다. 공개 LFS에도 올리지 않는다. PlayFab 설정·개발자 토큰·서명 값·로컬 감사 메모·원시 로그를 공개 이슈나 커밋에 넣지 않는다.
- `src/AeDeong.keystore`와 기존 계정/저장/No Ads 권한을 보존한다. 기준 개발 APK는 별도 앱 ID, debug signing, 격리 시작 씬을 사용한다. 운영 로그인·저장·구매·광고로 자동 테스트하지 않는다.
- CLI: `tools/.local/unity-cli/1.0.0-beta.8/unity.exe`. 설치는 `python tools/revival/install_cli.py`. 프로젝트의 `unity-cli`, `unity-pipeline` skill을 따른다.
- 모든 Editor 명령에 `--project-path` 절대 경로를 지정한다. 같은 Editor 쓰기는 직렬화한다. batch 전에 해당 checkout Editor만 정상 종료한다. 다른 Editor/PID를 종료하지 않는다.
- Unity를 열거나 검증하기 직전에 절대 checkout 경로, 현재 브랜치, HEAD, 미커밋 변경, 검증 대상 커밋을 대조하고 증거에 기록한다. Hub 최근 프로젝트나 기본 cwd로 대상을 추정하지 않는다. 프로세스 확인 시 명령행 전체를 출력하지 말고 PID·projectPath·Editor 버전만 추출한다.
- 최신 통합 상태를 검증할 때는 원격 main을 조회한 뒤 의도한 통합 checkout만 fast-forward한다. 그 checkout이 dirty이면 갱신을 중단하고 변경 소유자를 확인한다. PR 검증은 해당 head와 미커밋 변경을 명시하고, 검증을 위해 무조건 main으로 전환하지 않는다. 원본과 다른 담당자의 checkout은 임의로 전환·갱신하지 않는다.
- 해당 checkout의 Editor가 실행 중일 때는 checkout·merge 등 소스를 바꾸는 Git 작업을 하지 않는다. 소스 수정 후 검증하려면 재컴파일 완료를 확인한다. 기존 PR 커밋의 검증 결과를 이후 최신 main의 검증 결과로 표현하지 않는다.
- 라이브 검증: `command editor_status`, `set_autotick --enable true`, `get_console_logs`, `list_open_scenes`, `get_scene_hierarchy`, `run_tests --mode editor --filter Revival --async_tests true`, `test_status`. 성공 응답만으로 비동기 완료를 가정하지 않는다.
- 로컬 전체 검증은 `./tools/revival/Run-SdkValidation.ps1`이며 Python·기준 테스트/APK·GUID·네이티브 LOAD/ZIP 검사를 포함한다. 테스트만 실행할 때는 `Run-Baseline.ps1 -TestsOnly`를 사용한다. 공식 추가 RELRO 조건은 `python tools/revival/verify_native_alignment.py --strict-relro`로 별도 확인하며 기본 검사의 성공과 구분한다.
- 라이선스 문제는 `unity auth status`, `unity license status`로 확인한다. `license activate`의 빈 products 응답은 활성 라이선스가 있다는 증거가 아니다. 프로젝트 계정 pin을 사용하며 다른 프로젝트의 기본 계정을 바꾸지 않는다.
- CI/CD는 개인용으로 보류한다. 기존 App Center 설정을 보존하고 활성화/dispatch/새 서비스 이행/배포를 하지 않는다. Distribution 서비스 종료와 구성 보존을 구분한다.
- 접근권한 점검은 #94. 공개 열람/clone과 push/merge/관리 권한을 구분한다. 별도 요청 없이 권한을 변경하지 않는다.
- 검증 문서에는 실제 checkout/커밋, Unity/Android/CLI 버전, 테스트 결과, APK SHA-256, 미검증 항목을 기록한다. #92 기준 빌드와 #91 광고 정책·기기/스토어 검증을 구분한다.
