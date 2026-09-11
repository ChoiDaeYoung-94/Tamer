# Monster Tamer

Unity로 만든 Android 3D 게임입니다. 플레이어가 몬스터를 동료로 모아 전투하고, 마을에서 장비와 캐릭터를 관리합니다. [WildTamer](https://play.google.com/store/apps/details?id=com.percent.wildtamer&hl=ko)의 플레이를 3D로 재구성한 프로젝트입니다.

[트레일러](https://github.com/user-attachments/assets/243e5193-5e8f-4966-8b98-c724b62767dd) · [기존 Google Play 페이지](https://play.google.com/store/apps/details?id=com.AeDeong.MonsterTamer) · [복구 로드맵 #90](https://github.com/ChoiDaeYoung-94/Tamer/issues/90)

## 현재 개발 상태

복구 기준은 **Unity 6000.0.81f1**, Android **min API 24 / target API 36 / ARM64**, 앱 **1.0.5 / versionCode 26**입니다. 정확한 버전은 [도구 잠금](tools/revival/toolchain.json), [UPM 잠금](Packages/packages-lock.json), [의존성 문서](docs/revival/dependencies.ko.md)를 확인합니다.

최신 [구현·통합 검증 요약](docs/revival/completion-summary.ko.md)과 [2차 통합 검증 데이터](docs/revival/phase2-validation.json)에 병합 PR, 실제 SDK 버전, EditMode 249개·Python 71개 통과와 격리 빌드 결과를 기록했습니다. [초기 기준 빌드 기록](docs/revival/baseline.ko.md)은 별도 이력입니다. 네이티브 LOAD/ZIP 검사는 통과했지만 엄격 RELRO 검사 5개와 실제 16KB 기기 실행은 남아 있습니다. [AAB·split 정적 검사](docs/revival/aab-16kb-validation.ko.md)와 [광고 샘플·대조 APK](docs/revival/families-store-readiness.ko.md)는 완료했고, 실제 기기 검증과 구분합니다. 기기에서의 로그인·저장·구매·광고와 Google Play 심사 통과도 별도이며, [Families 광고 이슈 #91](https://github.com/ChoiDaeYoung-94/Tamer/issues/91)은 빌드 성공만으로 종료하지 않습니다.

**CI/CD는 보류 중입니다.** 기존 App Center 설정은 보존하며 자동 빌드·배포를 실행하지 않습니다. 과거 다운로드 링크를 현재 검증된 빌드로 안내하지 않습니다.

[실제 4KB ARM64 기기](docs/revival/device-smoke-validation.ko.md)에서 기존 격리 APK와 기기 사양에 맞춘 AAB split의 설치·Unity 화면·정리를 확인했습니다. 16KB 기기나 전체 게임 플레이 검증은 아니며, 추가 도구를 포함한 Python 회귀는 75개가 통과했습니다.

## 시작하기 — Windows PowerShell

필요한 환경은 Git, Python 3.11 이상, Unity **6000.0.81f1**과 Android Build Support(SDK/NDK/OpenJDK), 활성 Unity 라이선스입니다. 구매 에셋은 공개 저장소에 들어 있지 않으므로 권한 있는 원본 또는 비공개 아카이브도 필요합니다.

```powershell
git clone https://github.com/ChoiDaeYoung-94/Tamer.git
Set-Location Tamer

# Editor를 열기 전에 원본 경로를 실제 경로로 바꿉니다.
python tools/revival/restore_assets.py --source 'D:/path/to/authorized-original'
if ($LASTEXITCODE -ne 0) { throw '에셋 복원 실패' }
python tools/revival/restore_assets.py --verify
if ($LASTEXITCODE -ne 0) { throw '에셋 검증 실패' }
python tools/revival/install_cli.py
if ($LASTEXITCODE -ne 0) { throw 'CLI 설치 실패' }

# 이 checkout의 Editor를 닫은 상태에서 실행합니다.
./tools/revival/Run-SdkValidation.ps1
```

성공 시 `Build/revival/Tamer-development.apk`와 `Logs/revival`의 결과를 확인합니다. APK는 별도 앱 ID(`com.AeDeong.MonsterTamer.revival`), debug 서명, 격리 시작 씬을 사용합니다. 기존 운영 앱의 업데이트나 스토어 제출용 빌드가 아닙니다. Library는 checkout마다 새로 생성합니다.

해시 충돌, 설치 경로 변경, 라이선스, Editor 연결과 일상 개발은 [개발 안내](docs/development.ko.md)를 따릅니다. 원본 에셋을 공개 Git/LFS에 추가하거나 복원 manifest를 임의 재생성하지 않습니다.

## 코드와 디렉터리

| 경로 | 역할 |
| --- | --- |
| `Assets/Scripts/Creatures` | Player, Monster, Creature의 이동·전투·동료·버프 |
| `Assets/Scripts/Managers` | 데이터·서버·광고·IAP·씬·풀·사운드 관리자 |
| `Assets/Scripts/Login`, `Main`, `Game`, `SetCharacter`, `NextScene` | 씬별 진입과 화면 흐름 |
| `Assets/Scripts/UI`, `Cameras`, `MiniMap`, `Effects` | 조작 UI, 카메라, 미니맵, 효과 |
| `Assets/Scripts/Editor` | 기존 빌드 코드와 격리 `RevivalBuild` 진입점 |
| `Assets/Scenes`, `Prefabs`, `Resources`, `Settings` | 게임 씬·프리팹·런타임 리소스·렌더링 설정 |
| `Assets/Tests/Editor`, `Assets/Tests/Scenes` | 자동 회귀 테스트와 `RevivalSmoke` 검증 씬 |
| `Assets/ThirdParty` | 선별 공개 SDK와 로컬 서비스 설정 |
| `Assets/ThirdPartyAssets` | 권한 있는 원본에서 복원하는 비공개 게임 에셋 |
| `Packages`, `ProjectSettings` | UPM 버전 잠금과 Unity 프로젝트 설정 |
| `tools/revival`, `docs/revival` | 복원·빌드·APK 검사 도구와 근거 기록 |

기존 게임 씬 흐름은 `Login → SetCharacter/Main → Game`이며 `NextScene`을 전환 씬으로 사용합니다. 로그인/동기화는 Play Games·PlayFab, 결제는 Unity IAP, 광고는 Google Mobile Ads를 사용합니다. 패키지 업그레이드와 실제 서비스 호환성 검증은 단계별 PR로 관리합니다.

## 개발과 기여

[AGENTS.md](AGENTS.md)와 [개발 안내](docs/development.ko.md)를 먼저 읽습니다. 작은 이슈·브랜치·PR에 변경 이유, 재현 입력, 테스트 결과, 미검증 범위를 남기고 검증한 커밋을 병합합니다. `.meta`/GUID, 기존 계정·진행도·No Ads 권한을 보존합니다. 공개 clone 및 외부 PR 제안은 원본 저장소의 push/merge 권한과 다릅니다. 접근 정책은 [#94](https://github.com/ChoiDaeYoung-94/Tamer/issues/94)에서 추적합니다.

주요 개발 문서:

- [구현·통합 검증 요약과 남은 작업](docs/revival/completion-summary.ko.md)
- [개발·검증·PR 작업 안내](docs/development.ko.md)
- [복원 에셋과 라이선스 범위](docs/revival/dependencies.ko.md)
- [기준 빌드와 검증 근거](docs/revival/baseline.ko.md)
- [복구 계획과 단계별 상태](docs/revival/plan.ko.md)

## 개인정보처리방침

아래는 기존에 게시된 정책입니다. 복구 중인 코드와 Play Console/AdMob/PlayFab의 실제 설정, 보관·삭제 방식 및 Data safety 대조는 남아 있습니다. 이 문서 개편은 그 검증을 완료했다는 의미가 아닙니다.

**AeDeong**는 [Monster Tamer]를 운영합니다. 이 내용는 귀하가 앱을 사용할 때 개인 데이터의 수집, 사용 및 공개에 대한 정책을 알려주기 위해 만들어졌습니다.

## 1. 수집하는 정보

우리는 다음과 같은 유형의 정보를 수집할 수 있습니다:
- 개인 식별 정보 (이름, 이메일 주소 등)
- 사용 데이터 (앱 사용 패턴, 로그 데이터 등)
- 디바이스 정보 (기기 유형, 운영 체제 등)

## 2. 정보 사용 목적

수집된 정보는 다음과 같은 목적으로 사용됩니다:
- 서비스 제공 및 유지
- 사용자 지원 제공
- 사용 패턴 분석을 통한 서비스 개선
- 법적 의무 준수

## 3. 정보 공개

우리는 다음과 같은 경우에 귀하의 정보를 공개할 수 있습니다:
- 법적 요구가 있을 때
- 서비스 제공을 위한 제3자와의 공유 (예: 분석 서비스 제공업체)

## 4. 보안

우리는 귀하의 개인 정보를 보호하기 위해 상업적으로 허용되는 수단을 사용합니다. 그러나, 인터넷을 통한 전송 방법이나 전자 저장 방법은 100% 안전하지 않다는 점을 유의하시기 바랍니다.

## 5. 개인정보 보호정책의 변경

우리는 개인정보 보호정책을 수시로 업데이트할 수 있습니다. 변경 사항은 이 페이지에 게시됩니다. 변경 사항을 정기적으로 확인하는 것이 좋습니다.

## 6. 문의

이 개인정보 보호정책에 대해 질문이나 제안이 있으시면, doeud1410@gmail.com 으로 연락해 주십시오.

이 정책은 2024년 9월 30일부터 유효합니다.
