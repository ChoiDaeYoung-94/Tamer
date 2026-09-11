# 복원 에셋과 공개 관리 범위

기준 원본/원격 main: `c47c90217d45e8c6538c57a0924fa852abfbd736`.
원본은 읽기 전용 소스로 사용했고 Library, Temp, Logs, obj를 복사하지 않았다.

| 대상 | 확인된 버전/출처 | 관리 |
| --- | --- | --- |
| PlayFabSDK | 복원 기준 2.138.220621에서 **2.242.260805**로 갱신, [공식 UnitySDK 태그](https://github.com/PlayFab/UnitySDK/releases/tag/2.242.260805), Apache-2.0 | 공식 패키지 소스·LICENSE와 기존 GUID를 Git으로 관리; 출처/변경은 [SDK 감사](sdk-audit.ko.md) |
| PlayFabEditorExtensions | 원본 파일 해시로 고정, 배포 버전은 미확인, [공식 저장소](https://github.com/PlayFab/UnityEditorExtensions), Apache-2.0 | SDK 코드/리소스 포함. Avalon 폰트는 별도 권한 미확인으로 비공개 복원 |
| ThirdPartyAssets | 원본 약 532.94 MiB, 개별 패키지 버전·구매 증빙 미확인 | 전체 비공개 복원. 공개 Git/LFS에 올리지 않음 |
| 기존 Tests | Test.cs 및 Test.unity 원본 약 14.70 MiB | 기존 샘플은 비공개 복원. 신규 Editor 테스트와 격리 검증 씬만 Git 관리 |
| PlayFabSharedSettings / PlayFabEditorPrefsSO | 서비스·개발자 계정 설정 | 원본 값/해시 공개 제외. 새 환경에서는 계정 값 없는 템플릿 생성 |
| PDB / SDK 설치용 unitypackage | 재생성 디버그 심볼·중복 설치 아카이브 | 복원 필수 목록에서 제외 |

파일별 원본 경로, 크기, SHA-256, meta GUID, 라이선스 판정과 관리 방식은
`assets-manifest.json`에 기록했다. 원본에 들어 있던 파일이라는 사실은 구매 에셋의 재배포 허가 증거가 아니다.
정확한 패키지 구매 버전과 권한은 소유자의 Asset Store 구매 기록으로 후속 보완해야 한다.
PlayFab SDK는 고정 공식 배포물로 갱신했으며 기존 GUID와 중립 서비스 설정을 보존한다. Editor Extensions 원본은 공식 배포 태그와 전부 동일하다고 주장하지 않는다.

## 복원

Unity가 이 작업본을 열기 **전에** 실행한다. `--source`는 원본과 같은 상대 경로를 가진
권한 있는 로컬 원본 또는 비공개 아카이브를 펼친 경로다.

```powershell
python tools/revival/restore_assets.py --source '<권한 있는 원본 경로>'
python tools/revival/restore_assets.py --verify
python -m unittest discover -s tools/revival -p test_restore_assets.py
```

스크립트는 전체 사전 해시 검사 후 누락 파일만 생성한다. 기존 파일이 다르면 덮어쓰지 않고 실패한다.
`git-sdk` 파일은 현재 커밋의 Git checkout에서 복원한다. 이 파일이 누락되면 구형 비공개 원본에서 복사하지 않고 실패하므로 SDK가 조용히 내려가는 일을 막는다.
원본 삭제·이동을 하지 않는다. 원본 설정 파일은 복사하지 않고 중립 템플릿만 사용한다.
기존 작업본에 이미 있는 설정은 덮어쓰지 않으므로 실제 서비스를 연결할 때는 소유자가 별도로 확인해야 한다.
검증 씬에서는 로그인·저장·구매·광고를 시작하지 않는다.

`--inventory`는 유지보수자가 검증한 원본으로 manifest를 **갱신**하는 명령이다.
일반 복원 시 사용하면 안 된다. 해시가 다르다는 이유로 manifest를 자동 갱신하지 않는다.
새 환경의 소유자는 비공개 원본/아카이브를 별도로 확보해야 한다. 현재 공개 저장소만으로 구매 에셋을 다운로드할 수는 없다.

## 민감한 기존 파일

기존 공개 이력에 있는 서명키는 변경하거나 삭제하지 않았다. 개발 빌드는 debug signing을 사용한다.
2026-09-11 후속 대조에서 기존 JKS는 현재 Play 업로드 인증서와 일치하고 앱 서명 인증서와 다름을 확인했다.
키 변경 없는 대응 초안과 사본 검증 절차는 [서명·비공개 백업](signing-and-private-backup.ko.md)에 있다.
계정 설정, 기존 로그인 코드의 자격정보, 서명 값, 원시 Editor 로그를 공개 문서에 복사하지 않는다.
이미 추적되던 `.utmp`, `UserSettings`, PlayFab Editor 계정 설정은 이 브랜치에서 Git 추적만 해제했다.
현재 작업본의 파일과 D: 원본은 보존한다. 과거 공개 이력은 없어지지 않으므로 별도 후속 점검이 필요하다.
