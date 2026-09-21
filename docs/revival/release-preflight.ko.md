# 출시 후보 사전 검사와 16KB 호스트 준비

이 도구는 설정·산출물을 읽고 결과를 기록한다. 빌드, 서명, 키 생성, 업로드, 정책 게시 또는 Windows 설정 변경은 수행하지 않는다.

## 구현한 검사

`python tools/revival/verify_release_candidate.py source`는 고정 Unity, 운영 앱 ID 보존, min24/target36,
ARM64/IL2CPP, 전역 하네스 심볼 부재, 명시적인 IAP/UGS 자동 초기화 해제, 활성 첫 씬 Login,
legacy 빌드 marker 부재를 검사한다. 현재 10개 모두 통과했고 versionCode는 26으로 보존했다.
소스 설정과 최종 산출물을 구분하여 `releaseReady=false`를 출력한다.

`aab` 모드는 다음 검토 입력이 모두 있어야 실행할 수 있다.

- 실제 후보 AAB 경로와 선택한 versionCode
- Play Console에서 확인한 모든 트랙의 최대 사용 versionCode
- 현재 승인된 업로드 인증서의 공개 SHA-256 지문

실행 예시는 값을 결정한 뒤 사용하는 형식이며 현재 출시 번호나 인증서를 제안한 것이 아니다.

```powershell
python tools/revival/verify_release_candidate.py aab --aab <후보.aab> --version-code <후보번호> --published-max-code <확인한최대번호> --upload-cert-sha256 <공개인증서SHA256>
```

고정 bundletool 해시와 bundle validate, 실제 base manifest의 운영 ID·번호·API·debug/testOnly,
JarFile 전체 payload 서명 및 공개 인증서 일치, ARM64 ELF/LOAD/RELRO를 검사한다.
기존 `VerifyAabSignature.java`의 기본 debug 검사는 보존하고 명시적인 release 지문 모드만 추가했다.
release 모드는 지문이 일치해도 Android Debug 인증서를 거부한다. 임시 합성 인증서로 정상/변조/
unsigned 추가/지문 불일치/모드 불일치를 시험했다. 독립 검토에서 찾은 출력 경로의 입력 덮어쓰기와
big-endian ARM64 오인 문제도 수정했다. 같은 경로·hardlink는 검사 전에 거부하고 ELF는 little-endian을 요구한다.
관련 회귀를 포함한 Python 101개가 통과했다. 기존 debug AAB도 실제 bundletool manifest 단계에서 거부했다.

검사에 제공한 지문과 최대 번호가 실제 Console 상태인지는 이 로컬 도구가 인증하지 않는다.
split ZIP 정렬·실제 ARM64 16KB·정책·운영 인증/진행도 쓰기/구매 복원·트랙 승인도 별도다.
현재는 운영 release 인증서나 신규 후보 AAB를 사용하지 않았다. 정적 RELRO 실패는 실행 중 crash 증명이 아니다.

## 16KB 호스트 점검 이력과 가속 진단 대기

현재 상태(2026-09-21): 사용자의 재부팅은 완료됐지만 9월 14일 후속 확인에서 HypervisorPresent=false, 가속 검사 exit6이었다. BCD 읽기는 접근 거절로 미확인이다. 아래 활성화 당시의 재부팅 대기를 현재 요청으로 반복하지 않는다. [전체 실행 목록](recovery-execution-backlog.ko.md)의 후속 상태를 따른다.

### 일반 안내 기한과 개별 앱 요건

2026-09-12에 [Android 공식 16KB 안내의 Google Play compatibility requirement](https://developer.android.com/guide/practices/page-sizes#google-play-compatibility-requirement)를 직접 확인했다. API35 이상을 대상으로 하는 Google Play 앱은 64비트 기기에서 16KB 페이지를 지원해야 하며, 안내는 미지원 **업데이트**를 2027-02-01부터 출시할 수 없다고 명시한다. 해당 문단은 신규 앱에 같은 유예가 적용된다고 별도로 명시하지 않으며 앱별 예외나 연장 승인을 제시하지 않는다. 이를 신규 테스트 앱의 업로드 가능 판정이나 Tamer의 개별 Console 경고 해제로 확대하지 않는다. 실제 제출 전에는 대상 앱·트랙·후보의 Console 요구사항을 별도로 확인해야 한다.

저장소 Markdown/JSON과 README에서 기존 2025-11-01 또는 2027-02-01 기한 문구는 발견되지 않아 날짜 일괄 치환은 하지 않았다. 최신 일반 안내는 현재 RELRO 실패, 실제16KB 실행 및 전달 APK ZIP 정렬 미검증을 해소하지 않는다. [AAB RELRO 분석](iap-relro-analysis.ko.md)을 함께 따른다.

2026-09-11 읽기 점검: Windows 11 Pro build26200, BIOS virtualization/SLAT/VM monitor 모두 true,
HypervisorPresent=false. CIM `HypervisorPlatform`, `VirtualMachinePlatform`, `Microsoft-Hyper-V-All`은
모두 InstallState=2(Disabled)이고 AEHD/GVM 서비스가 없다.
기존 Emulator의 accel-check는 6과 가속 드라이버 미설치를 보고했다.
BCD 조회는 권한 부족으로 exit1이어서 hypervisorlaunchtype을 확인하지 못했다.
미조회 값을 off로 추정하지 않는다. [Microsoft InstallState 정의](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-optionalfeature)

재현용 읽기 도구:

```powershell
./tools/revival/Get-16KbHostReadiness.ps1 -EmulatorPath <기존SDK/emulator/emulator.exe>
```

호스트 결과 파일은 기존 파일을 덮어쓰지 않는다. 재실행할 때는 `-OutputPath`로 새 JSON 경로를 지정한다.

위 값은 활성화 전 읽기 점검 이력이다. 사용자 승인 후 2026-09-11 09:14:03–09:14:06 UTC에 관리자 PowerShell에서 다음 명령을 실행했다.

```powershell
Enable-WindowsOptionalFeature -Online -FeatureName HypervisorPlatform -All -NoRestart
```

`-All`은 필요한 부모 기능을 포함하고 `-NoRestart`는 자동 재시작/재시작 요청을 억제한다.
실행 결과 Success=true, FeatureState=Enabled, RestartNeeded=true, RebootExecuted=false를 확인했다. 후속 CIM에서도 HypervisorPlatform은 InstallState=1, VirtualMachinePlatform과 Hyper-V 전체는2, HypervisorPresent=false였다. 원시 실행 결과는 Git 제외 로컬 기록에 보관한다. 이 결과는 재부팅 후 가속 성공을 뜻하지 않는다. 모든 작업을 저장하고 사용자와 재부팅 시점을 정한 뒤 별도로 재부팅해야 한다.
현재 BIOS가 이미 켜져 있으므로 BIOS 변경, Hyper-V 전체 역할/WSL 설치, BCD 쓰기를 추가 제안하지 않는다.
재부팅 후에도 가속이 실패하면 관리자 권한의 BCD 읽기부터 다시 확인하고 별도 변경을 결정한다.
[Google WHPX 절차](https://developer.android.com/studio/run/emulator-acceleration),
[Microsoft 기능 활성화와 NoRestart](https://learn.microsoft.com/en-us/powershell/module/dism/enable-windowsoptionalfeature)

이는 기존 x86_64 16KB AVD의 가속 준비다. 성공해도 PAGE_SIZE=16384·프로세스 ABI·번역/호환 모드·앱 실행을
다시 관찰해야 하며 ARM64 직접 실행과 같다고 보지 않는다. 기존 software 부팅 timeout/crash를 반복하지 않았다.
WHPX 활성화만 실행했고 재부팅·BCD 변경·다른 Windows 기능 활성화는 수행하지 않았다. 2026-09-12 작업 재개를 재부팅 승인으로 해석하지 않는다.

## 사용자가 결정해야 하는 최소 항목

기존 [공개 개인정보처리방침](../../README.md#개인정보처리방침)에 운영자 **AeDeong**, 일반 문의 **doeud1410@gmail.com**, 시행일 **2024년 9월 30일**이 명시되어 있다. 이 값은 이미 게시된 사실로 재사용하며 같은 정보를 다시 묻지 않는다. README와 기존 GitHub 정책 URL은 출시 때 사용한 공개 정책 페이지로 보존한다. 기존 정책 URL을 개발 문서로 대체하거나 링크를 깨뜨리지 않는다. 일반 문의 주소가 있다는 사실을 실제 삭제 접수 서비스가 구현됐다는 뜻으로 확대하지 않는다.

1. 재부팅은 완료됐다. 관리자 PowerShell에서 `bcdedit /enum`, `Get-WindowsOptionalFeature -Online -FeatureName HypervisorPlatform | Select-Object FeatureName,State`, `Get-CimInstance Win32_ComputerSystem | Select-Object HypervisorPresent`의 읽기 결과가 필요하다. 미조회 BCD 값을 추정하지 않으며, 결과를 검토하기 전에 추가 재부팅·BCD 쓰기·OS 기능 변경을 요청하지 않는다.
2. 업로드 키의 암호화 보관 위치·비밀번호 보관소·별도 기기 백업 위치 및 키 reset 신청 승인.
3. 기존 정책에 없는 실제 삭제 접수 경로, 보관 기간·삭제 범위와 기존 No Ads 복원·재가입 방침.
4. 전용 PlayFab 타이틀·미공개 Google 테스트 앱·catalog·Google add-on·테스터·상품·업로드와 수동 계정 연결은 완료됐다. 격리 앱의 로그인·무료 구매·동일 설치 복원은 [실제 IAP 결과](iap-test-bundle.ko.md#무료-테스트-구매복원재시작-검증-완료)를 따른다. 과거 PlayerCreationDisabled는 현재 차단 요인이 아니다. 남은 독립 acknowledgment 조회에는 해당 앱에 접근 가능한 Android Publisher 인증이, 취소/실패 결제에는 승인된 미구매 테스트 조건이 필요하다. 기존 구매 권한을 삭제하거나 초기화해 조건을 만들지 않는다. 다른 기기·삭제 후 복원은 별도 미검증이다. PlayFab 내장 영수증 검증을 위해 VPS를 새로 요구하지 않는다.
5. 광고 차단 유지로 먼저 복구할지와 신규 No Ads 판매 범위는 [광고 복구 결정안](ad-recovery-decision.ko.md#사용자가-결정할-항목)의 미답 선택이다. 기존 구매 권한·복원은 모든 선택에서 보존한다. 광고 수익 재개를 선택할 때만 이용자 연령 처리 선택을 추가로 받는다.

공개 설정값이나 보관 기간을 임의 생성하지 않는다. 삭제 UI·합성 처리 서버·C# loopback HTTP 연결과 receipt staging 패키지는 PR131–135로 통합했다. 이는 운영 인증·삭제·실제 구매 검증을 대체하지 않는다. 독립 구현 공백이 없으면 합성 하네스를 추가하지 않고, 위 결정을 받아 실제 환경 연결을 진행한다. 최종 릴리스 번호·인증서·트랙 제출 승인은 해당 단계의 실제 Console 상태와 후보가 준비된 뒤 묶어 확인한다. 일반적인 작업 재개 지시는 미답 운영 결정의 승인으로 간주하지 않는다.
