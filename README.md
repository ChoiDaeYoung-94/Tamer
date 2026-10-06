# Monster Tamer

**몬스터를 동료로 모으고, 함께 싸우며 성장하는 Android 3D 게임**

몬스터와 함께 대륙을 탐험하는 사냥꾼이 되어 보세요. 필드에서 만난 몬스터는 전투 상대가 되기도 하고, 포획 후 함께 싸우는 동료가 되기도 합니다.

**[▶ 기존 플레이 영상 보기](https://github.com/user-attachments/assets/243e5193-5e8f-4966-8b98-c724b62767dd)** · **[Google Play 페이지 열기](https://play.google.com/store/apps/details?id=com.AeDeong.MonsterTamer)** · [개발 시작하기](docs/development.ko.md)

**[앱 삭제 후에도 계정 및 관련 데이터 삭제 요청하기](docs/account-deletion.ko.md)** — Monster Tamer 이용자는 기존 문의 이메일로 삭제를 요청할 수 있습니다.

## 어떤 게임인가요?

- **전투와 포획:** 필드를 이동하며 몬스터와 싸우고 포획합니다.
- **동료와 성장:** 모은 몬스터를 동료로 데리고 전투하며 캐릭터를 성장시킵니다.
- **마을에서 준비:** 장비와 캐릭터를 관리하고 다음 탐험을 준비합니다.

Unity로 제작했으며, [WildTamer](https://play.google.com/store/apps/details?id=com.percent.wildtamer&hl=ko)의 플레이를 3D로 재구성한 프로젝트입니다.

## 실제 게임 화면

<p align="center">
  <img src="docs/images/gameplay-capture.png" width="280" alt="필드에서 몬스터 포획 버튼이 표시된 실제 게임 화면">
  <img src="docs/images/gameplay-ally.png" width="280" alt="Bat 동료 한 마리가 플레이어를 따라오는 실제 마을 화면">
  <img src="docs/images/gameplay-village.png" width="280" alt="Monster Tamer 마을의 실제 게임 화면">
</p>

필드의 포획 화면 · Bat 동료가 따라오는 마을 · 마을 전경입니다. 기존 테스트 환경에서 촬영한 화면으로 테스트 표시가 포함되며, 현재 스토어 버전이나 자연 포획 확률을 보여 주는 자료는 아닙니다. [촬영 조건·출처](docs/images/README.md)

## 기존 플레이 영상

[▶ Monster Tamer 플레이 영상 재생](https://github.com/user-attachments/assets/243e5193-5e8f-4966-8b98-c724b62767dd)

기존에 공개한 약 2분 35초 영상으로 캐릭터 선택과 마을 플레이를 확인할 수 있습니다. 현재 복구 중인 개발 빌드와 화면·동작이 다를 수 있습니다.

## 다운로드

**[Google Play에서 Monster Tamer 확인하기](https://play.google.com/store/apps/details?id=com.AeDeong.MonsterTamer)**

Monster Tamer의 기존 공개 스토어 페이지입니다. 설치 가능 여부는 해당 페이지에서 사용 중인 기기로 확인해 주세요. 현재 복구 빌드의 공개 배포는 아직 완료되지 않았으며, 이 저장소의 개발·내부 테스트 APK는 일반 다운로드로 제공하지 않습니다.

<details>
<summary><strong>개발 환경·복구 진행 상황·기여 안내</strong></summary>

## 복구 진행 상황

게임 복구와 실제 기기 검증을 진행 중입니다. 아래 결과는 각 문서에 명시된 소스·격리 앱 기준이며 운영 서비스 전체의 검증 완료를 뜻하지 않습니다.

| 확인한 내용 | 근거 |
| --- | --- |
| 실제 포획 → 동료 추가 → 로컬 저장·메모리 서버 반영, 사망 후 복귀 | [게임플레이 검증](docs/revival/gameplay-capture-lifecycle.ko.md) |
| 격리 앱의 무료 테스트 구매, No Ads 저장·동일 설치 복원·재로그인 유지 | [스토어·구매 검증](docs/revival/iap-test-bundle.ko.md) |
| 샘플 광고 보상 후 Home 복귀, 보상 1회·BGM 복귀, 일반 release 광고 요청 차단 | [광고 검증](docs/revival/families-ad-followup-20260914.ko.md) |
| 결제 없는 진행도 검증 앱과 Editor 회귀 400개 통과 | [진행도 검증 준비](docs/revival/progress-only-harness.ko.md) |

새 개발 APK의 소스 생성 라이브러리 2개는 LOAD·RELRO 검사에 통과했습니다. 사전 빌드 라이브러리 문제와 실제 ARM64 16KB 실행·최종 출시 AAB·스토어 검증은 별도로 남아 있습니다. 번역 AVD 참고 시도는 앱 설치 전 중단되어 앱 실행 근거가 아닙니다. [최종 후보 준비 조건](docs/revival/release-preflight.ko.md)과 [전체 실행 목록](docs/revival/recovery-execution-backlog.ko.md)에서 기능·운영·정책을 포함한 남은 범위를 확인할 수 있습니다.

개발 기준은 **Unity 6000.3.25f1 · Android min API 25 / target API 36 · ARM64**입니다. [도구 잠금](tools/revival/toolchain.json)과 [UPM 잠금](Packages/packages-lock.json)을 따릅니다. **CI/CD는 보류 중**이며 기존 App Center 설정을 보존합니다.

## 시작하기 — Windows PowerShell

필요한 환경은 Git, Python 3.11 이상, Unity **6000.3.25f1**과 Android Build Support(SDK/NDK/OpenJDK), 활성 Unity 라이선스입니다. 구매 에셋은 공개 저장소에 들어 있지 않으므로 권한 있는 원본 또는 비공개 아카이브도 필요합니다.

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

- [최신 복구 실행 목록](docs/revival/recovery-execution-backlog.ko.md)
- [개발·검증·PR 작업 안내](docs/development.ko.md)
- [복원 에셋과 라이선스 범위](docs/revival/dependencies.ko.md)
- [기준 빌드와 검증 근거](docs/revival/baseline.ko.md)
- [복구 계획과 단계별 상태](docs/revival/plan.ko.md)

</details>

## 개인정보처리방침

**AeDeong**는 **Monster Tamer**를 운영합니다. 이 방침은 게임 이용, 계정·진행도 저장, 구매·광고 및 문의 과정에서 처리되는 정보를 설명합니다. 개인정보처리방침은 기존의 이 페이지에서 계속 제공합니다.

### 1. 처리하는 정보와 목적

게임은 계정과 진행 정보를 서버에 저장하므로 개인정보를 전혀 수집하지 않는 앱은 아닙니다. 사용하는 로그인 방식과 기능에 따라 처리 항목이 달라집니다.

| 구분 | 정보와 이용 목적 |
| --- | --- |
| 로그인·계정 | 게임 계정 ID, Google Play Games 인증 정보 또는 기기·사용자 지정 로그인 식별자를 계정 연결과 진행도 로딩에 사용합니다. 기기 로그인에는 운영체제·기기 모델 정보가 포함될 수 있습니다. |
| 프로필·게임 진행 | 이용자가 정한 닉네임, 캐릭터·튜토리얼·동료·장비·게임 재화·보상 상태 등을 기기와 PlayFab에 저장하여 게임을 제공하고 진행도를 유지합니다. 닉네임은 별칭이며 캐릭터 성별은 이용자의 실제 성별 정보로 취급하지 않습니다. |
| 구매·복원 | 상품·거래·구매 또는 복원 상태와 No Ads 권한을 처리합니다. 결제는 Google Play에서 진행하며 게임에 결제수단 전체 번호를 입력하도록 요구하지 않습니다. 구매는 선택 기능이지만 기존 구매 조회와 SDK의 구매·진단 정보 처리는 새 구매 버튼을 누르는 경우에만 한정되지 않을 수 있습니다. |
| SDK·광고 | 사용 중인 SDK와 설정에 따라 기기·설치·계정 식별자, IP 주소와 IP 기반 대략적 위치, 앱·광고 상호작용, 성능·진단 및 구매 이벤트가 서비스 제공·안정성 개선·분석·광고·부정행위 방지에 사용될 수 있습니다. |
| 지원·삭제 문의 | 이메일로 보내신 회신 주소, 문의 내용과 계정 소유 확인에 필요한 최소 자료를 문의 응답 및 삭제 요청 처리에 사용합니다. |

### 2. 함께 사용하는 서비스

다음 서비스가 해당 기능에 필요한 정보를 처리합니다. 게임의 서비스 제공을 대신하는 처리와 공급자가 자체 서비스·광고·진단 목적으로 처리하는 범위를 구분합니다. 자세한 내용은 각 공급자의 안내를 확인해 주세요.

- **Microsoft PlayFab:** 게임 계정, 닉네임과 진행도 저장·조회 및 계정 삭제 처리. [Microsoft 개인정보처리방침](https://www.microsoft.com/ko-kr/privacy/privacystatement)
- **Google Play Games·Google Play:** 게임 인증, 스토어 구매·복원 및 Google 서비스의 계정·진단 처리. [Google 개인정보처리방침](https://policies.google.com/privacy?hl=ko)
- **Unity IAP:** 구매 처리·권한 복원과 SDK의 구매·진단 정보 처리. [Unity 개인정보처리방침](https://unity.com/legal/privacy-policy) · [Unity IAP 데이터 처리 안내](https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety)
- **Google AdMob·User Messaging Platform(UMP):** 광고와 광고 개인정보 선택 처리. 광고 SDK는 광고·분석·부정행위 방지를 위해 식별자·IP·상호작용·진단 정보를 처리할 수 있습니다. 실제 광고 파트너와 선택 항목은 제공되는 개인정보 메시지에서 확인해 주세요. [광고 SDK 데이터 처리 안내](https://developers.google.com/admob/android/privacy/play-data-disclosure)

광고 미표시, 개인화 거부 또는 No Ads 구매가 로그인·구매·모든 SDK의 정보 처리를 중단하는 것은 아닙니다. 게임 계정 삭제와 공급자의 다른 서비스 계정·데이터 삭제도 구분됩니다.

### 3. 연령 및 광고 개인정보 선택

지역과 제공 기능에 따라 광고 개인정보 메시지에서 허용·거부 등의 선택을 안내합니다. 제공 중인 앱과 준비 중인 새 버전의 기능은 다를 수 있습니다.

**새 버전 준비 안내:** 생년월일 대신 연령 구간 또는 응답하지 않음을 선택하는 기능과 지역별 개인정보 메시지를 준비하고 있습니다. 선택한 연령 구간은 기기 내 설정에 저장하며 게임 계정·진행 데이터로 서버에 업로드하지 않습니다. 광고 동의 기능을 사용할 때에는 선택에 따른 아동·청소년 처리 신호와 동의 요청·선택 상태를 SDK에 전달할 수 있습니다. 연령 구간 선택을 법적 동의 연령이나 보호자 동의의 확인으로 보지 않습니다. SDK가 요구하는 경우 광고 개인정보 옵션에서 선택을 다시 변경하도록 준비하며, 이 준비 기능은 아직 제공 중인 모든 앱에 적용된 서비스가 아닙니다.

### 4. 보관과 삭제 요청

계정과 진행 정보는 게임 계정·진행 저장 기능에 사용합니다. 계정 삭제 처리 후 별도 보관용 게임 계정·진행 사본을 만들지 않는 원칙을 적용하며, 요청 처리에 필요한 이메일 원문과 소유 확인 자료는 처리 종료 후 삭제하고 별도로 보관하지 않습니다. 서버·백업·처리 기록·메일 서비스와 공급자에 실제로 남는 자료의 삭제 범위와 기간은 아직 확정되지 않았으며, 확인 후 안내하겠습니다.

앱을 삭제했거나 실행할 수 없어도 **[doeud1410@gmail.com](mailto:doeud1410@gmail.com)**으로 Monster Tamer 계정 및 관련 데이터 삭제를 요청하실 수 있습니다. 앱을 재설치할 필요는 없습니다. [삭제 요청 방법·소유 확인·대상과 제한](docs/account-deletion.ko.md)을 확인해 주세요. **이메일 발송이나 요청 접수는 삭제 완료를 뜻하지 않습니다.** 운영자 AeDeong이 문의 메일을 직접 처리하며 계정 소유 여부를 확인합니다. 메일 요청은 2주 이내 처리를 목표로 하며, 추가 확인이나 지연이 있으면 그 안에 확인된 상태와 필요한 다음 단계를 답장으로 안내합니다. 2주는 운영자의 메일 요청 처리 목표이며 서비스 제공 업체의 모든 기록·로그·백업이 2주 안에 소거된다는 보증은 아닙니다. 확인되지 않은 다른 사람의 계정이나 파일은 임의로 삭제하지 않습니다.

Google 계정, 다른 게임의 계정과 Google Play 구매 기록 자체는 이 게임의 삭제 대상이 아닙니다. No Ads 구매와 구매 복원 절차는 게임 계정 삭제와 구분되며, 삭제된 게임 계정의 관련 데이터를 구매권한 보존을 이유로 별도 보관하지 않습니다. 삭제 후 새 게임 계정에서 구매를 복원할 수 있는 조건은 아직 확정되지 않았습니다. 접근할 수 없는 기기의 파일은 이메일 요청만으로 원격 삭제할 수 없습니다.

### 5. 보안

기본 PlayFab 연결은 HTTPS를 사용하며 Google·Unity의 SDK 안내도 전송 암호화를 설명합니다. 서비스별 전송 보호와 기기 내 저장 파일의 보호는 다르며, 인터넷 전송과 전자 저장의 완전한 안전을 보장할 수는 없습니다.

문의나 삭제 요청에 비밀번호, 일회용 인증번호, 로그인 토큰, 신분증 또는 결제수단 전체 정보를 보내지 마세요. 소유 확인에 필요한 자료는 담당자가 안내하며 공개 저장소 이슈에 요청 자료를 게시하지 마세요.

### 6. 변경 및 문의

처리 항목이나 기능이 변경되면 기존의 이 페이지에 내용을 갱신합니다. 최초 정책 게시일은 **2024년 9월 30일**, 본문 보완 기준일은 **2026년 9월 28일**입니다.

운영자: **AeDeong** · 게임: **Monster Tamer** · 개인정보 및 삭제 문의: **[doeud1410@gmail.com](mailto:doeud1410@gmail.com)**
