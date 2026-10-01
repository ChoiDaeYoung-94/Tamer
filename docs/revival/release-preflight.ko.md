# 출시 후보 사전 검사와 16KB 호스트 준비

## 2026-10-01 결정 반영과 최종 후보 준비 조건

읽기 대조 기준은 main `81b67926f8dccf2e175679076556848ddd2fcfef`다.
아래는 기존 증거와 사용자 결정을 정리한 준비 조건이며 새 검사·빌드 결과가 아니다.
과거 결과는 각 source·산출물 기준으로 보존하고 최신 main의 재실행 결과로 합산하지 않는다.

| 항목 | 확인된 범위 | 남은 준비·검증 |
| --- | --- | --- |
| 새 LTS·생성 네이티브 | [PR #284](https://github.com/ChoiDaeYoung-94/Tamer/pull/284)의 실제 개발 APK에서 libil2cpp·FramePacing LOAD/RELRO 통과와 사후 정확 복원·독립 검토 PASS | 새 APK의 prebuilt2·전체 strict/ZIP/서명은 미검사. 최종 AAB와 전달 split, ARM64 native 16KB 실행은 미완료. 개발 APK를 출시 후보로 사용하지 않음 |
| 16KB 실행 환경 | ARM64 native 16KB 환경이 없다는 사용자 확인. 10월 1일 번역 AVD 참고 시도는 설치 전 부팅 상태 조회 timeout으로 중단 | 새 PAGE_SIZE·앱 실행은 미검증. 자기 프로세스 정리를 독립 확인했고 추가 시도는 보류함. 번역 관측으로 native 16KB 검증을 대체하지 않고 나머지 복구를 계속함 |
| PlayFab 수집 | 현재 집중 시간·기기 정보·전체 IP 저장·새 수집 기능 자동 실행 모두 ON을 읽기 확인했고 사용자는 그대로 유지하기로 결정 | [Data Safety 후보](data-safety-draft.ko.md)에 설정과 소스 사실을 구분해 반영. 실제 제공 버전별 전송·필수/선택·목적·제공자 보관 범위는 미확정 |
| 삭제 문의 | [PR #285](https://github.com/ChoiDaeYoung-94/Tamer/pull/285)에 운영자 기존 메일 직접 처리·2주 이내 메일 처리 목표와 처리 종료 후 자료 삭제 원칙 반영 | 소유 확인·지연/상태 회신 운영, Console 삭제 URL 등록값·공급자 잔존 범위 확인. 2주를 모든 로그·백업 소거 보증으로 사용하지 않음 |
| 기존 키 백업·서명 | 로컬 암호문·receipt 존재, 기존 기록의 verified/sourceUnchanged=true. 10월 1일 사용자가 두 파일의 Drive 업로드 완료를 보고함. 새 백업 암호와 기존 키 암호는 별개 | 재다운로드·외부 사본 복구는 미검증으로 offsiteVerified=false·keystorePasswordVerified=false 유지. 업로드 사용자 보고를 독립 복구 성공으로 확대하지 않으며 기존 키 사용 가능성·현재 활성 업로드 인증서 대조 필요 |
| 정책·신고 | 기존 README 정책 URL·시행일 보존, [정책 본문](privacy-policy-release-draft.ko.md)·Data Safety는 검토 초안 | 실제 제공 앱/SDK·처리 목적·보관·암호화 근거를 확정한 뒤 공개 본문과 답안 완성. 공개 전 확인란이 남은 초안을 그대로 게시하거나 Console 제출하지 않음 |
| 운영 광고·기능 | 기존 rewarded/No Ads 유지, 5초 추가 조사 보류. 준비 receipt·합성 시험과 운영 계약/바이너리는 구분 | 운영 연령·지역·동의·로그인/저장·권한/구매 복원·영수증 권위의 실제 검증 및 알려진 미해결 기록. productionContractVerified/binaryVerified/distributable=false 유지 |
| 배포·CI | 개인용 CI/CD 보류와 기존 App Center 구성 보존 | 최종 후보 검증·승인 후 트랙/신고/심사·공개 설치 확인. CI 활성화·dispatch·배포 서비스 이행은 하지 않음 |

최종 AAB는 [서명 입력 준비](signing-and-private-backup.ko.md#aab-빌드-입력-준비)의
기존 keystore·alias·비공개 암호·현재 활성 업로드 인증서 대조를 먼저 충족해야 한다.
현재 모든 트랙의 최대 사용 versionCode를 새로 확인하고 후보 번호를 검토하며,
운영 앱 ID·Login 첫 씬·하네스 심볼 부재·min25/target36/ARM64/IL2CPP를 유지한다.
실제 후보에 bundle validate·전체 payload 서명/인증서·manifest·전체 ELF/LOAD/RELRO를
검사하고 전달 split의 ZIP 및 기기 실행도 별도로 확인한다. 검사 도구의
artifactChecksPassed는 출시 승인이나 releaseReady=true를 뜻하지 않는다.
이번 문서 작업에서 signing·Unity·Console·메일·운영 삭제·백업 업로드는 실행하지 않는다.
과거 e5e5 검증은 [LTS 기록](lts-transition-validation.ko.md)의 보고 범위이며,
해당 비공개 원로그를 현재 접근하거나 다시 확인했다고 주장하지 않는다.

## 2026-10-01 번역 AVD 참고 관측 중단

한국시간 12:01:15–12:01:28에 기존 WHPX x86_64/ARM64 번역 AVD를 1회 시작했다.
대상은 `C:/Users/pc_17/.codex/worktrees/ad-production-completion/Tamer`의 source
`4ba1422850c0bb2bb29182e177d160c5ed5ac0ac`에서 생성한 개발 APK이며,
SHA-256은 `f3efa94bbb12b930231afcb07bf473abcf9687d8dba09ff213e54dbc871d4533`이다.
이 source와 main `dfb7b67437a1795e2f49525e24a16ec3e0bf71a8`의 전체 tree 동일성을
대조한 뒤 참고 실행을 시도했다. 관측 실행 checkout은
`C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`이며 Unity 명령은 실행하지 않았다.

WHPX 사용 가능·AVD 식별 뒤 `sys.boot_completed` 단일 조회가 5초 제한으로
timeout되어 중단됐다. 새 PAGE_SIZE 조회와 설치 전 오프라인 확인에는 도달하지
않았고 APK 설치0·앱 실행0이다. 앱 화면·앱 로그 관측도 없다. 초기 ADB 연결 오류가
있었지만 지연과 게스트 문제 중 원인은 미확정이며 게스트 부팅 실패나 앱 실패로
단정하지 않는다. 과거 9월 23일 PAGE_SIZE=16384를 이번 APK 실행 증거로 재사용하지 않는다.

자기 launcher와 부모 연결·생성 시각·정확한 AVD/port로 확인한 qemu만 종료했다.
독립 사후 읽기 검토에서 남은 관련 프로세스와 cleanup 오류가 없고 소유 프로세스가
모두 종료됐음을 확인했다. 자동 재시도는 없으며 사용자 결정에 따라 추가 부팅·진단을
보류하고 나머지 복구를 계속한다. `runtime16KBVerified=false`,
`arm64NativeRuntimeVerified=false`, `prebuiltRelroResolved=false`를 유지한다.
기존 prebuilt2 RELRO 미해결은 이 환경 관측 중단과 별개다. 원시 증거는 비공개로 보존한다.

## 2026-09-30 최신 출시 준비 상태

대조 main은 `5864d5fb49a34068012e7b7c4f4f01605ef1ee1a`다. [PR #273](https://github.com/ChoiDaeYoung-94/Tamer/pull/273)은 준비 도구·승인된 감독 합성 주입 1회 성공·원복을 통합했으나 운영 실행은 차단하며 `productionContractVerified=false`, `binaryVerified=false`, `distributable=false`를 유지한다. [PR #277](https://github.com/ChoiDaeYoung-94/Tamer/pull/277)의 RELRO 부재 판정 보정은 아래 기록을 따르며 기존 LTS 끝 정렬 4개 실패와 실제16KB·최종AAB 미검증을 해소하지 않는다.

[PR #278](https://github.com/ChoiDaeYoung-94/Tamer/pull/278)의 [LTS 기록](lts-transition-validation.ko.md)은 소스 `85892a9`의 저장·삭제 6개 클래스 1회 114/114 통과와 정확한 원복·clean·Editor 종료를 확인했다. fake gateway·임시 파일의 Editor 결과이며 실제 로그인 준비·실기기 UI·운영 삭제·삭제 후 구매 복원은 미완료다. [PR #279](https://github.com/ChoiDaeYoung-94/Tamer/pull/279)의 [Data safety](data-safety-draft.ko.md)와 [개인정보처리방침](privacy-policy-release-draft.ko.md)은 미제출·미게시 초안으로 README 공개 정책·URL·시행일과 Console을 변경하지 않았다.

9월 30일 Console 읽기는 제공24/1.0.3·최대26 미승인, 9+·성인 포함 연령 변경의 검토 전송 준비와 기존 수집/공유 NO·Families YES·암호화되지 않음 선언을 확인했다. 삭제 URL 등록값은 현재 분기에서 확인하지 못했다. 실제 제공 SDK·메일 처리·공급자 잔존·서명과 외부복구·기기·최종AAB/전달split·정책/README·트랙/신고/심사는 남아 있다. 모든 최종 검증 뒤 시험 자원 정리, 저장소 이름 변경은 마지막이다. 아래 날짜별 실패·산출물 이력과 기존 사용자 결정을 보존하며 이번 변경은 새 테스트·빌드·게시 결과가 아니다.

## 2026-09-30 RELRO 부재 판정 보정

기준 main `85892a9ec299a36b011c6d8045b2342e91ba93cb`, 격리 checkout
`C:/Users/pc_17/.codex/worktrees/relro-diagnostic-release/Tamer`, 브랜치
`codex/relro-release-diagnostics`에서 Python 정적 검사만 수정했다.
Unity 기준은 `6000.3.25f1`, Android min25/target36/ARM64이며 Unity/CLI/ADB를 실행하지 않았다.

이전 호출 흐름은 `candidate_checks` → `inspect_bundle_libraries` → `inspect_elf`이며,
출시 후보의 `artifactChecksPassed`는 모든 `.so`의 LOAD와 RELRO 검사 통과를 요구한다.
`inspect_elf`가 RELRO 부재까지 실패로 처리해, 정렬된 LOAD만 있는 합성 AAB도 거절했다.
[Android 공식 RELRO 안내](https://developer.android.com/guide/practices/page-sizes#check-the-relro-security-flag)는
RELRO가 없는 파일을 이 정렬 조건에 적합하다고 명시하므로 이 부재 조건만 보정했다.

- RELRO가 없으면 끝 정렬 검사는 통과한다. `relroPresent=false`와 APK의
  `relroAbsentLibraryCount`로 부재를 표시하며, RELRO 보안 보호가 입증됐다는 뜻은 아니다.
- RELRO가 있으면 `(VirtAddr + MemSiz) % 16384 == 0` 검사를 계속 요구한다.
  전체 LOAD와 범위가 같아도 끝 불일치는 실패다. `--strict-relro`와 출시 AAB의
  `artifactChecksPassed`에서 이 실패를 유지한다.
- LOAD 정렬/합동/범위, ABI, APK ZIP 정렬, AAB 서명·인증서와 manifest/version 검사는 그대로다.
  AAB 내부 ZIP 배치는 전달 split APK의 ZIP 정렬 증거가 아니므로 split 검증도 별도로 필요하다.
- [Bionic 특수 경로](https://android.googlesource.com/platform/bionic/+/android16-qpr2-release/linker/linker_phdr_16kib_compat.cpp)의
  전체 LOAD RELRO 처리는 배치 진단 근거다. 공식 끝 정렬 조건을 면제하거나 native 16KB 적합을 확정하지 않는다.
  기존 실패 기록은 수정하지 않았고 `runtime16KBVerified=false`, `releaseReady=false`를 유지한다.

JSON의 부재 판정 의미 변경은 native report `schemaVersion=3`으로 구분한다.
출시 후보에는 파일별 `relroPresent`와 `relroErrors`를 추가하여 실패 원인을 보존한다.
기존 LTS APK SHA-256 `9f7cd0be899f436daea7d045e59e3950756d25a3a8936ca3ebe02c0cf516b068`의
LOAD/ZIP 통과·RELRO 끝 불일치 4개 기록은 이번 보정으로 해소되지 않는다. 해당 APK를 다시 검사하지 않았다.

변경 후 최소 합성 검사 1회: `test_verify_native_alignment.py` 26개,
`test_release_candidate.py` 8개 모두 통과했다. 부재 strict 통과, 존재하는 비정렬 RELRO 실패,
LOAD 부족 실패, `releaseReady=false`를 확인했다. 합성 AAB의 외부 도구 응답은 mock이며
실제 서명·빌드·다운로드·계정·기기 설치·ARM64 16KB 실행·스토어 승인 증거는 새로 생성하지 않았다.

2026-09-30 원격 main `a28b1d526a5fab818f65ac224b29002598c35415`의 문서 대조: 아래 검사 결과는 각 기록의 소스·산출물 기준이며 최신 `main`에서 재실행한 결과가 아니다. Unity6000.3.25f1/min25 전환은 통합됐고 남은 영향 범위·실제 ARM6416KB·최종 출시 AAB·스토어 검증은 미완료다. 완료된 삭제 경로·보관 원칙과 기존 광고형 보상 유지 결정은 마지막 항목에서 구분하며, 이번 문서 변경으로 새 테스트·APK 결과를 추가하지 않는다.

아래 PR #247/#248 관측의 과거 대조 기준 main은 `a6e5213`이다. [PR #248 온라인 삭제 시험과 UI 수정](deletion-online-followup.ko.md)은 폐기 계정 1개의 접수·로컬 정리·타이틀 검색 부재와 서버 원복을 확인했다. 기존 APK의 접수 후 예외는 후속 Editor UI 테스트 12/12 통과와 구분하며 수정 기기 검증·운영 계정·구매 복원·제공자 전체 소거는 미검증이다. [PR #247 UMP 샘플 기기 검증](ump-only-device-validation.ko.md)은 성인 EEA 폼·개인정보 옵션 재진입과 Under13 처리를 확인했지만 게시자 실제 메시지·운영 연령 계약·전체 native 트래픽 검증은 남아 있다.

5초 닫기 추가 조사는 사용자 결정으로 보류했고 나머지 복구는 계속한다. 실제 닫기·운영 정책 적합성은 미해결 알려진 이슈로 후보와 함께 기록한다. 이를 해결 완료·심사 승인으로 간주하거나 추가 조사를 현재 복구 작업의 필수 선행 단계로 다시 요구하지 않는다. 이 결정으로 운영 광고 gate나 연령·동의 보호를 해제하지 않는다.

[게시자 UMP](publisher-ump-harness-preparation.ko.md)의 PR #270 관측과 PR #271 사전 검사 이후에도 운영 gate는 OFF다. [PR #273](https://github.com/ChoiDaeYoung-94/Tamer/pull/273)의 준비 도구·감독 합성 검증은 main에 통합됐으나 운영 실행·주입 계약·출시 바이너리 검증은 미완료다. 합성 산출물을 출시 후보로 사용하지 않는다. 정책/기능·서명·실기기·최종 후보와 스토어 검증을 마친 뒤 시험 자원 정리, 마지막 저장소 이름 소문자 변경 순서를 따른다.

이 도구는 설정·산출물을 읽고 결과를 기록한다. 빌드, 서명, 키 생성, 업로드, 정책 게시 또는 Windows 설정 변경은 수행하지 않는다.

## 후반 LTS 전환과 최종 후보 순서

2026-09-21 승인 후 공식 자료 대조·격리 전환과 min25 상향을 수행했으며, 현재 통합 기준은 Unity `6000.3.25f1`/CLI `1.0.0-beta.8`/Android min25·target36이다. [LTS 전환 검증](lts-transition-validation.ko.md)의 실제 소스·APK·기기 결과를 따른다. `6000.0.81f1` 유지·설치 보류는 전환 전 이력이며 재설치를 요구하는 현재 조건이 아니다.

toolchain·패키지·검증 도구의 전환 기준은 통합됐으며, 아래 이전 검사 수치를 새 버전 결과로 재사용하지 않는다. 새 Unity의 영향 범위 테스트·실기기·16KB·최종 AAB 검증은 필수이고 과거 결과로 대체할 수 없다. 테스트 두 번 실패 시 중단 규칙을 유지한다. 통상 업그레이드는 이미 승인됐으며 중대한 호환성 변경만 구체적으로 보고한다. [전환 완료 조건과 최종 정리 순서](recovery-execution-backlog.ko.md#후반-최신-lts-전환--필수-완료-조건-2026-09-21-사용자-추가)를 따른다.

## 구현한 검사

아래 10개 통과와 Python101개 수치는 도구 최초 구현 당시의 이력이다. 현재 도구 기준은 toolchain의 Unity6000.3.25f1/min25·target36이며 이번 정리에서는 재실행하지 않았다.

`python tools/revival/verify_release_candidate.py source`는 고정 Unity, 운영 앱 ID 보존, 당시 min24/target36,
ARM64/IL2CPP, 전역 하네스 심볼 부재, 명시적인 IAP/UGS 자동 초기화 해제, 활성 첫 씬 Login,
legacy 빌드 marker 부재를 검사한다. 당시 10개 모두 통과했고 versionCode는 26으로 보존했다.
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

## 16KB 호스트 점검 이력과 가속 진단

2026-09-21까지의 상태: 사용자의 재부팅은 완료됐지만 9월 14일 후속 확인에서 HypervisorPresent=false, 가속 검사 exit6이었다. BCD 읽기는 접근 거절로 미확인이다. 아래 활성화 당시의 재부팅 대기를 현재 요청으로 반복하지 않는다. [전체 실행 목록](recovery-execution-backlog.ko.md)의 후속 상태를 따른다. 9월 23일 새 관측은 아래에 구분한다.

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

### 2026-09-23 가속 및 격리 게스트 관측

checkout `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`, 기준 `origin/main` `a6aaaac22237aba5577a5fd20423604ad422a645`, Unity `6000.0.81f1`, 별도 SDK Emulator `37.1.11` / API36 `google_apis_ps16k` x86_64 이미지 revision 7이다. 새 읽기 점검에서 `HypervisorPresent=true`, `HypervisorPlatform` 활성화, `emulator -accel-check` exit0 및 `WHPX(10.0.26200) is installed and usable`을 확인했다. BCD 읽기는 여전히 권한 부족으로 실패해 `hypervisorlaunchtype`을 추정하지 않는다. 이번 점검에서 OS 기능·BCD·재부팅은 변경하지 않았다. 비공개 결과 `Logs/revival/host-16kb-readiness-20260923-sdk.json` SHA-256 `56361f579f90c31b4f9a6ea4e7c879ade4d1b346f75aed28edaa1fcf03a2f8d8`.

기존 AVD를 보존하고 새 격리 `Tamer_16KB_Audit_20260923`을 한 번 부팅했다. AVD 이름과 API36, `sys.boot_completed=1`, `adb shell getconf PAGE_SIZE=16384`를 직접 확인했다. ABI 목록은 `x86_64,arm64-v8a`이며 native bridge는 `libndk_translation.so`다. 따라서 **16KB x86_64 게스트의 부팅 증거**이고 ARM64 네이티브 라이브러리 실행 성공이나 앱 호환성·스토어 승인 증거가 아니다. 앱은 설치하지 않았다. 자기 AVD만 종료했고 ADB 기기 목록과 emulator/qemu 프로세스는 0으로 확인했다. 비공개 직접 관측은 `Logs/revival/16kb-audit-avd-observation-private.json`(SHA-256 `6a2cbece6e589af2c9f3ff4428f94386fdb90d6a536357c9017f3dc8a3a30008`)에 있다.

이번 비공개 자동 probe 복사본은 새 ADB serial `5582`를 기다리면서 기동 포트 `5580`을 그대로 사용해 거짓 실패를 기록했다. 실제 연결 포트의 AVD 이름과 부팅 완료를 별도로 확인했고 AVD를 다시 부팅하지 않았다. 저장소의 원래 도구는 serial에서 기동 포트를 산출하도록 보강하고 합성 테스트만 실행했다. [Android 16KB 게스트 확인 절차](https://developer.android.com/guide/practices/page-sizes#test), [Android WHPX 및 호스트와 이미지 ABI 조건](https://developer.android.com/studio/run/emulator-acceleration), [Unity의 에뮬레이터 지원 범위](https://docs.unity3d.com/6000.0/Documentation/Manual/android-requirements-and-compatibility.html)를 따른다. ARM64 네이티브 16KB 실행은 적합한 실기기나 ARM64 호스트·원격 기기에서 별도로 확인해야 한다.

## 사용자가 결정해야 하는 최소 항목

기존 [공개 개인정보처리방침](../../README.md#개인정보처리방침)에 운영자 **AeDeong**, 일반 문의 **doeud1410@gmail.com**, 시행일 **2024년 9월 30일**이 명시되어 있다. 이 값은 이미 게시된 사실로 재사용하며 같은 정보를 다시 묻지 않는다. README와 기존 GitHub 정책 URL은 출시 때 사용한 공개 정책 페이지로 보존한다. 기존 정책 URL을 개발 문서로 대체하거나 링크를 깨뜨리지 않는다. 일반 문의 주소가 있다는 사실을 실제 삭제 접수 서비스가 구현됐다는 뜻으로 확대하지 않는다.

1. 재부팅은 완료됐고 2026-09-23 읽기 점검에서 WHPX 사용 가능 및 16KB 격리 x86_64 게스트 부팅을 확인했다. BCD 값은 권한 부족으로 계속 미조회이나 현재 에뮬레이터 가속의 차단 요인은 아니다. 추가 재부팅·BCD 쓰기·OS 기능 변경을 요청하지 않는다. ARM64 네이티브 16KB 기기에서의 실행 확인은 별도로 남아 있다.
2. [기존 키 로컬 암호화 백업](signing-and-private-backup.ko.md)은 완료됐다. 향후 사용자가 암호문·receipt를 Drive에 직접 업로드한 뒤 내려받은 사본과 로컬 암호 입력으로 외부 복구를 확인한다. 현재 Drive 업로드·재다운로드는 미확인이다. 기존 JKS 암호는 여전히 미상이며 새 백업 암호와 구분한다. 기존 키 보존 선택을 reset·새 키 생성 승인 질문으로 다시 전환하지 않는다. 최종 서명 단계에는 기존 개인키 사용 가능성과 실제 Console 공개 인증서·versionCode·트랙을 대조한다.
3. Classic CloudScript 접수와 [외부 이메일 요청](../account-deletion.ko.md), 삭제 후 자체 계정·진행 데이터의 별도 보관 사본을 만들지 않고 요청 이메일 원문·소유 확인 자료를 처리 종료 후 삭제하는 원칙은 승인됐다. [운영 연결](cloudscript-operating-activation.ko.md)의 설정 읽기와 [승인된 폐기 계정 1개의 온라인 시험](deletion-online-followup.ko.md)은 구분한다. 후자는 접수·로컬 정리·타이틀 검색 부재와 원복 관측이며 운영 계정의 전체 소거·메일 운영과 공급자 백업·로그·메일 서비스의 잔존 범위·처리 기간은 별도 검증이 필요하다. 구매 복원 기능은 유지하지만 삭제 후 새 게임 계정에서의 실제 No Ads 복원은 미검증이다. 확정된 경로·원칙과 완료한 시험의 승인을 다시 묻지 않는다.
4. 전용 PlayFab 타이틀·미공개 Google 테스트 앱·catalog·Google add-on·테스터·상품·업로드와 수동 계정 연결은 완료됐다. 격리 앱의 로그인·무료 구매·동일 설치 복원은 [실제 IAP 결과](iap-test-bundle.ko.md#무료-테스트-구매복원재시작-검증-완료)를 따른다. 과거 PlayerCreationDisabled는 현재 차단 요인이 아니다. 남은 독립 acknowledgment 조회에는 해당 앱에 접근 가능한 Android Publisher 인증이, 취소/실패 결제에는 승인된 미구매 테스트 조건이 필요하다. 기존 구매 권한을 삭제하거나 초기화해 조건을 만들지 않는다. 다른 기기·삭제 후 복원은 별도 미검증이다. PlayFab 내장 영수증 검증을 위해 VPS를 새로 요구하지 않는다.
5. 2026-09-21 사용자가 정상 광고와 No Ads 신규 구매·기존 권한·복원 유지를 확정했다. 광고 차단·신규 판매 중단 임시 출시안은 채택하지 않았으므로 판매 범위를 다시 묻지 않는다. 2026-09-28 사용자는 AdMob 유지·Unity Ads 철회 방향에서 기존 광고형 보상 유지(A)를 확정했고 일반 광고·게임 보상 분리(B)는 채택하지 않았다. [결정 기록과 조기 닫기 조사](admob-product-options.ko.md)를 따르며 제품 선택을 다시 묻지 않는다. 광고 닫기와 SDK 보상 취득 시점은 별개이며 앱 타이머로 보상을 지급하는 변경은 승인되지 않았다. 후속 5초 조사 보류 결정에 따라 나머지 복구를 계속하고 실제 닫기·정책 적합성은 미해결로 기록한다. [확정 실행 기준](ad-recovery-decision.ko.md)의 과거 기술 해결 문구를 추가 조사 재개의 필수 조건이나 새로운 제품 선택 질문으로 사용하지 않는다.

공개 설정값이나 보관 기간을 임의 생성하지 않는다. 삭제 UI·합성 처리 서버·C# loopback HTTP 연결과 receipt staging 패키지는 PR131–135로 통합했다. 이는 운영 인증·삭제·실제 구매 검증을 대체하지 않는다. 독립 구현 공백이 없으면 합성 하네스를 추가하지 않고, 위 결정을 받아 실제 환경 연결을 진행한다. 최종 릴리스 번호·인증서·트랙 제출 승인은 해당 단계의 실제 Console 상태와 후보가 준비된 뒤 묶어 확인한다. 일반적인 작업 재개 지시는 미답 운영 결정의 승인으로 간주하지 않는다.
