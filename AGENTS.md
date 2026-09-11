# Tamer 작업 규칙

- Unity는 `6000.0.81f1`로 고정한다. 도구/SDK 기준은 `tools/revival/toolchain.json`과 `Packages/packages-lock.json`을 따른다.
- 현재 구현 checkout은 이 폴더이며 `D:\meee\git\Tamer`는 소유자의 별도 원본이다. 원본의 브랜치 변경·동기화·삭제·이동을 하지 않는다.
- `.meta`와 GUID를 보존한다. 에셋 복원은 Editor를 열기 전에 `python tools/revival/restore_assets.py --source <권한 있는 원본>`으로 수행한다. 해시 충돌을 임의 덮어쓰기/manifest 재생성으로 숨기지 않는다.
- 구매/출처 불명 에셋과 Avalon 폰트는 비공개 복원 대상이다. 공개 LFS에도 올리지 않는다. PlayFab 설정·개발자 토큰·서명 값·로컬 감사 메모·원시 로그를 공개 이슈나 커밋에 넣지 않는다.
- `src/AeDeong.keystore`와 기존 계정/저장/No Ads 권한을 보존한다. 기준 개발 APK는 별도 앱 ID, debug signing, 격리 시작 씬을 사용한다. 운영 로그인·저장·구매·광고로 자동 테스트하지 않는다.
- CLI: `tools/.local/unity-cli/1.0.0-beta.8/unity.exe`. 설치는 `python tools/revival/install_cli.py`. 프로젝트의 `unity-cli`, `unity-pipeline` skill을 따른다.
- 모든 Editor 명령에 `--project-path` 절대 경로를 지정한다. 같은 Editor 쓰기는 직렬화한다. batch 전에 해당 checkout Editor만 정상 종료한다. 다른 Editor/PID를 종료하지 않는다.
- 라이브 검증: `command editor_status`, `set_autotick --enable true`, `get_console_logs`, `list_open_scenes`, `get_scene_hierarchy`, `run_tests --mode editor --filter Revival --async_tests true`, `test_status`. 성공 응답만으로 비동기 완료를 가정하지 않는다.
- 로컬 전체 검증은 `./tools/revival/Run-Baseline.ps1`. 추가 검사: `python tools/revival/audit_guids.py`, `python tools/revival/verify_apk.py`, `python -m unittest discover -s tools/revival -p test_restore_assets.py`.
- 라이선스 문제는 `unity auth status`, `unity license status`로 확인한다. `license activate`의 빈 products 응답은 활성 라이선스가 있다는 증거가 아니다. 프로젝트 계정 pin을 사용하며 다른 프로젝트의 기본 계정을 바꾸지 않는다.
- CI/CD는 개인용으로 보류한다. 기존 App Center 설정을 보존하고 활성화/dispatch/새 서비스 이행/배포를 하지 않는다. Distribution 서비스 종료와 구성 보존을 구분한다.
- 접근권한 점검은 #94. 공개 열람/clone과 push/merge/관리 권한을 구분한다. 별도 요청 없이 권한을 변경하지 않는다.
- 검증 문서에는 실제 checkout/커밋, Unity/Android/CLI 버전, 테스트 결과, APK SHA-256, 미검증 항목을 기록한다. #92 기준 빌드와 #91 광고 정책·기기/스토어 검증을 구분한다.
