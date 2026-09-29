# 게시자 UMP 전용 harness 준비와 단계별 검증

2026-09-28, checkout `C:/Users/pc_17/.codex/worktrees/e5e5/Tamer`, 기반
`38a284fdd0b54ccae36e6c96b314c23285d3f3ba`, Unity `6000.3.25f1` / CLI `1.0.0-beta.8` /
Android min25·target36·ARM64 기준이다. 기존 정책
`https://github.com/ChoiDaeYoung-94/Tamer#개인정보처리방침` 본문 보완은 PR #264로 병합됐다.
이 단락은 2026-09-28의 준비 시점 상태다. 당시 유럽 영어 메시지는 사용자 승인된
게시 후보였고 두 메시지는 Console 초안/게시 OFF였다. 2026-09-29의 승인된 게시·기기
실측은 문서 끝의 후속 기록에서 구분한다.

## 기존 경로 확장

기존 `BuildUmpSample`은 공식 샘플 App ID를 유지한다. 새 `BuildUmpPublisher`는
명시적인 private build opt-in과 ignored `Logs/revival/production-ads-prepared/production-ads.private.json`을
요구한다. checkout·전체 App ID 형식·비샘플 게시자·기존 Settings와의 일치·비활성 승인을
검사하며 누락/중복 필드는 거절한다. 실제 값은 코드/공개 문서/검증 결과에 출력하지 않는다.
설정 JSON이나 보상형 운영 단위를 player Resources에 넣지 않는다.

별도 package `com.AeDeong.MonsterTamer.revival.umppublisher`, debug signing, development,
IL2CPP와 기존 격리 `RevivalAdHarness.unity`만 사용한다. `TAMER_UMP_ONLY_HARNESS`와
`TAMER_UMP_PUBLISHER_HARNESS`를 이 빌드에만 추가한다. publisher define 단독은 compile error다.
시작 시 Android/debug/package/scene/Managers 없음 및 기존 production/지역 false 조건을
확인하고 유효하지 않은 context는 직접 UMP 시작도 차단한다.

UMP-only는 manager Init·MobileAds Initialize·rewarded Load/Show를 실행하지 않는다.
처음부터 자동 UMP도 요청하지 않으며 기기 화면의 명시적 Update 버튼만 요청한다.
실제 publisher 메시지·지역/연령 사례는 후속 허용 범위에서만 실행한다. 기존 sample 설명과
publisher 설명/boot 로그를 구분하며 ID나 test-device hash는 managed 로그에 쓰지 않는다.
Native MobileAdsInitProvider·AD_ID 및 SDK 자체 네트워크/진단은 별개라 모든 수집0을 보증하지 않는다.

기존 build finally에서 package/signing/backend/bundle과 Settings/manifest를 원복한다.
publisher는 기존 scene/.meta 바이트도 보존·원복·대조한다. PowerShell wrapper는 Editor 시작 전
동일 파일과 ProjectSettings를 백업하고 own Editor 종료 후 복원하며 opt-in 환경변수도 원복한다.
private config는 `.meta` 복원 승인 기록이 아니며 Editor 전 복원 검사를 대신하지 않는다.

## 실행 전후 경계

빌드 준비 명령은 `Run-AdHarness.ps1 -Variant ump-publisher -PrivatePublisherOptIn`이다.
준비 시점에는 이 명령을 실행하지 않았다. wrapper는 설치/기기 실행/메시지 게시를 하지 않는다.
기존 APK verifier를 확장해 실제 merged manifest App ID 정확1개/private config 일치/샘플혼입없음,
별도 package/debug signature/SDK/ABI를 검사한다. 결과에는 ID 자체 없이 일치 bool을 기록한다.

게시 전 가능한 검사: 소스/설정 정합, offline verifier 경계, 독립 정적 리뷰, 후속 승인된
Unity compile/build 및 APK manifest/signature 검사. 이것은 게시자 폼 표시 성공이 아니다.
게시 후 별도 범위에서 확인할 것: 실제 publisher Update·EEA 거절→Required 옵션→변경→재시작,
미국 opt-out/옵션 진입, Other 관측, 대표 TFUA=true 억제, Unknown/Declined 시작 차단.
초안 시점에는 예상 폼 성공을 단정하거나 반복 기기 요청으로 우회하지 않았다.

## 검증과 미검증

- Python offline verifier 경계 **4/4 PASS 한 번**: 합성 publisher metadata 일치/결과 ID 비노출,
  기존 sample 유지, sample/중복 manifest 거절, 누락/중복/다른 checkout private config 거절.
  실제 APK나 네이티브 실행을 사용하지 않았다.
- 별도 .NET 9 compile 확인은 두 번 참조 구성 오류로 실패했다. 1차 UnityEngine과 modules
  중복 형식, 이를 제외한 2차 UnityEditor와 CoreModule 중복 및 기존 internal
  GoogleUmpConsentClient의 외부 assembly 접근 오류다. 실제 두 소스를 링크했지만
  제품의 Unity compile 성공/실패 판정 근거가 아니다. 지시대로 3차나 유사 우회 실행을 하지 않는다.
- 이 초기 준비 시점에는 IL2CPP/APK/AAB·빌드 경로의 finally 실실행·게시자
  네트워크/기기·메시지 게시가 **미실행/미검증**이었다. 당시 새 APK SHA-256은
  해당 없었다. raw 구성 결과는 ignored 로컬 경로에 보존한다.
- production/지역 false, 기존 연령별 fullscreen 제한, 보상/No Ads 계약은 변경하지 않았다.

### 사용자 승인 후 실제 Unity 컴파일 1회

두 .NET 참조 구성 실패를 보고한 후 사용자가 실제 Unity 컴파일만 **1회** 승인했다.
이는 .NET 검사 3차나 이름 변경 우회가 아니다. 실제 source
`7976b2ecec61a83e8f41a1418eb6fd1068294286`를 clean/own Editor0으로 확인했다.
이 checkout의 앞선 승인 복원 PASS 근거를 재사용했고, 승인된 메타/보존 복사본 일치 및
이후 다른 비공개 충돌·누락 없음을 확인하여 복원 명령을 반복하지 않았다.

Unity `6000.3.25f1` / revision `e1dba0a9aba4` / CLI `1.0.0-beta.8`의 `run`을
절대 프로젝트 경로와 Android 대상으로 실행했다. 임시 dirty 범위는 ProjectSettings의
Android defines뿐이며 원래 `DOTWEEN`에 `TAMER_AD_TEST_HARNESS`, `TAMER_UMP_ONLY_HARNESS`,
`TAMER_UMP_PUBLISHER_HARNESS`를 추가했다. Play/test/build/executeMethod는 사용하지 않았다.

결과: **CLI exit0 / C# compiler error0**, script compilation 14.225301초.
`Assembly-CSharp`와 `Assembly-CSharp-Editor`의 실제 Bee response에 해당 세 define와
변경 소스가 모두 들어갔고, UTC 2026-09-28 09:36:44에 각 DLL이 생성됐다.
Main DLL에 publisher-only boot 문자열이 포함된 것도 읽기로 확인하여 기존 캐시만의
성공으로 표시하지 않는다. 기존 소스의 obsolete 경고는 compile error와 구분한다.

정상 종료 후 설정 전체 바이트를 원래 백업 SHA와 대조해 복원했고 Android define는 다시
`DOTWEEN`이다. own Editor0/clean을 확인했으며 추가 import tracked 변경은 없었다.
비공개 현재 메타와 보존 자료도 유지됐다. 코드 후속 변경은 없고 이 결과 문서만 추가한다.
raw 로그/response/설정 백업은 ignored 경로에 보존하며 운영 값을 공개하지 않는다.
로그 `Logs/revival/publisher-ump-compile/unity-compile.log` SHA-256은
`770bda1f490dfdbee331efd6a361acfb4a07fac4a92738377c38abc3e63ac2e0`이다.

이 결과는 Editor의 원래 어셈블리 compile 검증이다. Android player/IL2CPP, private App ID
읽기 및 빌드 finally, 실제 merged manifest/signature, 네이티브 SDK, publisher UMP
기기/게시·운영 광고 활성 검증을 대신하지 않는다. 아래 별도 승인된 APK 단계와 구분한다.

### 후속 승인된 게시자 격리 APK 1회와 오프라인 검사

사용자 작업 계속 지시와 총괄의 구체 배정에 따라 실제 main source
`7f5e24723a2f955b19aad26f18c2f8dddb83131f`를 새 검증 브랜치에 clean/own Editor0으로
반영했다. 기존 승인 복원 자료를 재사용하고 추가 private 충돌/누락 없음을 확인했다.
위와 같은 Unity/CLI/Android 기준에서 `Run-AdHarness.ps1 -Variant ump-publisher
-PrivatePublisherOptIn`을 절대 프로젝트 경로와 함께 **1회** 실행했다.

결과: **build exit0 / IL2CPP Android APK 성공 / 오프라인 APK verifier PASS**.
`AD_HARNESS_BUILD_OK variant=ump-publisher`와 `AD_HARNESS_IDENTITY_RESTORED`를 실제
로그에서 확인했다. player `Assembly-CSharp.rsp`에는 `TAMER_REVIVAL_SMOKE`,
`TAMER_AD_TEST_HARNESS`, `TAMER_UMP_ONLY_HARNESS`, `TAMER_UMP_PUBLISHER_HARNESS`가
포함됐다. C# compiler error0이며 앞선 컴파일 전용 1회 검사를 반복한 결과로 표시하지 않는다.

APK `Build/revival/Tamer-ads-ump-publisher.apk`는 98,520,264 bytes, SHA-256
`570bc882e52b23a2ff0549e90b4557b9ad02d49187cbb9a21cbe1ea1314167a4`다.
실제 merged manifest의 App ID 정확1개/private config 일치/비샘플 게시자,
`com.AeDeong.MonsterTamer.revival.umppublisher`/debuggable/debug certificate,
version1.0.5/code26/min25/target36/ARM64-only를 확인했다. 운영 App ID는 출력하지 않는다.
실제 manifest에는 **MobileAdsInitProvider와 AD_ID 권한이 존재**한다.
managed 광고 Init/Load/Show를 실행하지 않는 소스 경로와 이 네이티브 초기화/권한을 구분한다.

wrapper 백업 7개(ProjectSettings 3개, Settings/manifest, harness scene/.meta)의 현재 바이트가
각 백업과 모두 일치했다. 원래 package/signing/backend/bundle/defines 설정과 씬/GUID를
보존했다. opt-in 환경변수는 wrapper finally 복원 후 프로세스가 종료됐으며 별도 환경값
readback receipt는 수집하지 않았다. own import가 변경한 tracked 파일 6개는 기존 clean과
비교해 private patch로 보존 후 정확히 복원했다. own Editor0/clean 및 private 승인 자료
유지를 확인했고 슬롯을 반환했다.

private 검사 결과는 `Logs/revival/ads-ump-publisher-verification.json`과
`Logs/revival/publisher-ump-apk-summary.json`, raw 로그는
`Logs/revival/ads-ump-publisher-build.log`에 보존한다. 로그 SHA-256은
`d064c05e4266ef149f88c1e4ff05228a2b10bfe120201eff3adc90bf1d8db6fb`다.

이후 총괄이 승인한 최소 기본 네이티브 검사도 같은 APK에 **1회** 실행했다.
`verify_native_alignment.py --apk Build/revival/Tamer-ads-ump-publisher.apk`는
exit0 / native library 6개 / **LOAD·ZIP 기본 검사 PASS**다. 6개 모두 압축돼 있어
ZIP의 비압축 native mmap offset 정렬 검사 대상은 없다. 원본 결과는
`Logs/revival/publisher-ump-apk-native-alignment.json`에 보존한다.

별도 RELRO 진단은 `relroChecksPassed=false`이며 `libc++_shared.so`, `libmain.so`,
`libswappywrapper.so` 3개의 end address modulo-16384 조건을 통과하지 못했다.
`--strict-relro`는 실행하지 않았다. 기본 PASS를 추가 RELRO 조건 통과나 16KB 기기
실행 성공으로 확대하지 않으며 이 진단만으로 실행 불가를 단정하지 않는다.

실제 APK를 설치/실행하지 않았으며 publisher UMP 네트워크·폼 표시·거절/옵션/재시작,
네이티브 전체 트래픽, strict RELRO 실행, 16KB 기기, AAB/스토어는 미검증이다.
이 APK 단계에서는 메시지 Publish OFF/운영 및 지역 flags false를 유지했다.
후속 게시·기기 단계는 아래에 별도로 기록한다.

### 후속 승인된 유럽·미국 메시지 게시와 테스트폰 UMP 관측

2026-09-29, checkout `C:/Users/pc_17/.codex/worktrees/e5e5/Tamer`, 실제 APK 소스
`7f5e24723a2f955b19aad26f18c2f8dddb83131f`, 게시·기기 관측 시작 checkout
`a209f7b5304e5be277b0afaaeb852271f8c41dce`다. Unity `6000.3.25f1`
(`e1dba0a9aba4`), CLI `1.0.0-beta.8`, APK min25/target36/ARM64 기준이다.
기존 빌드 APK 98,520,264 bytes, SHA-256
`570bc882e52b23a2ff0549e90b4557b9ad02d49187cbb9a21cbe1ea1314167a4`를
재사용했다. 빌드·Editor·복원·코드 테스트를 재실행하지 않았다.

AdMob 기존 **Monster Tamer Android** 앱에 연결된 `Monster Tamer - European privacy v1`
(영어 en, EEA·영국·스위스, 동의/거절/옵션 관리)과
`Monster Tamer - US state privacy v1`(영어 en-US, 현재·향후 지원 미국 주 전체,
판매·공유 거부 옵션)을 각각 게시했다. 두 목록에서 `게시됨` 및 게시 스위치 ON,
개인 정보 보호 및 메시지 개요에서 각 활성 메시지 1개를 확인했다. 기존 정책 URL은
`https://github.com/ChoiDaeYoung-94/Tamer#개인정보처리방침`이며 별도 정책/파트너/
적법한 이익/대체 메시지/계정 설정을 변경하지 않았다. 이 메시지는 기존 게시자 App ID에
연결돼 있어 실제 기존 앱 이용자에 대한 노출 가능성이 있다. Console 안내상 표시 반영에는
최대 1시간이 걸릴 수 있다. 게시 자체를 기존 앱 사용자에게 노출 0의 증거로 해석하지 않는다.

연결된 물리 기기는 Samsung SM-N986N, Android 13/API33, `PAGE_SIZE=4096`이다.
설치 전 `com.AeDeong.MonsterTamer.revival.umppublisher`가 없음을 확인한 뒤 debug
격리 패키지만 새로 설치했다. 기존 `com.AeDeong.MonsterTamer`는 이 폰에서 미설치였고
설치·로그인·저장·구매·광고 요청을 시도하지 않았다. UMP 테스트 기기 해시는 UMP가
출력한 값을 로컬 입력에만 사용하며 공개 증거에 넣지 않는다.

아래 지역 이름은 harness에서 선택한 값이다. SDK가 강제 지역을 실제 적용했다는
뜻은 아니며, 후속 읽기 조사에서 발견한 입력 오류와 관측별 한계를 다음 절에 기록한다.

| 합성 기기 사례(선택값) | 실제 관측 |
| --- | --- |
| 성인·EEA | 영어 `Monster Tamer` 제목과 Consent / Do not consent / Manage options가 실제 폰에 표시됐다. Manage options에서 선택값을 확인했고 거절 후 `UMP privacy options` 재진입과 Confirm choices 변경을 확인했다. |
| 성인·EEA 재시작 | own 패키지를 force-stop/재시작한 후 명시적 Update에서 기존 선택이 유지돼 첫 동의 폼은 다시 표시되지 않았다. 첫 입력은 로컬 테스트 해시 길이 오류로 차단됐고 문자별 재입력 후 정상 Update를 확인했다. |
| 성인·규제 미국 주 | 게시 직후 11:30대와 Console의 최대 1시간 반영 안내가 지난 12:26 KST에 각각 1회 로컬 reset 후 `RegulatedUSState` debug Update에 성공했다. 두 관측 모두 즉시 필수 폼은 없고 `PrivacyOptionsRequired` 버튼도 나타나지 않았다. 게시된 미국 거부 UI/선택/변경은 확인하지 못했다. |
| 성인·Other | 로컬 reset 후 Update 성공, 필수 폼 및 옵션 버튼 없음. |
| Under13·EEA | 로컬 reset 후 `tfua=True`로 Update 성공, 필수 폼 없음. 지역 정책 전체의 승인 근거는 아니다. |
| Unknown·Declined | 각각 `ump_blocked unknown_or_declined_age`로 UMP Update 호출 전 차단됐다. |

Google 문서상 미국 주 메시지는 앱 시작 시 필수 동의 폼이 아니라 개인정보 옵션
진입점에서 표시된다. 따라서 미국 사례에서 필수 폼이 없는 것만으로 실패라고 판정하지
않는다. 다만 12:26 KST의 추가 관측에서도 진입점이 없었고, 격리 앱 UMP 저장 상태는
두 번 모두 `privacy_options_requirement_status=NOT_REQUIRED`,
`is_pub_misconfigured=false`였다. 원인은 현재의 메시지 타기팅·게시자 설정·SDK 응답
중 어디에 있는지 확정하지 못했다. 미국 거부 플로우는 **미검증**으로 남기고 같은 조건의
세 번째 실행을 중단한다. 이 SDK 응답만으로 실제 모든 미국 주 이용자에게 메시지가
표시되지 않는다고 단정하지 않는다.
[Google 메시지 표시 시점](https://support.google.com/admob/answer/10114020)을 기준으로 해석한다.

기기에서 관측한 `can_request=True`는 Google UMP의 광고 요청 가능 신호이며
개인 맞춤 광고 동의 또는 실제 광고 요청·게재를 뜻하지 않는다. 격리 앱은 managed
Mobile Ads Init/Load/Show 경로가 없고 관련 호출 로그도 없었다. merged manifest의
MobileAdsInitProvider 및 AD_ID 권한은 존재하므로 SDK 자체 통신·전체 트래픽 0을
단정하지 않는다. raw 화면·filtered 로그는 ignored
`Logs/revival/publisher-ump-device-20260929/`에 보존한다. 이 4KB 기기 실측은
16KB 기기 실행, strict RELRO, AAB·스토어 검증을 대신하지 않는다.

두 번째 미국 관측 후 설치 전과 같은 경로의 own 격리 패키지임을 재확인하고
`com.AeDeong.MonsterTamer.revival.umppublisher`만 force-stop·uninstall했다.
재조회에서 이 패키지와 운영 `com.AeDeong.MonsterTamer`는 모두 폰에 없었고,
이번 관측용 폰 임시 화면 파일 2개도 제거했다. 원본의 다른 앱·계정·저장에는
접근하지 않았다. 검증용 로컬 화면과 로그는 비공개 ignored 경로에 남긴다.

### 후속 정정: 테스트 기기 해시 입력과 강제 지역 적용 한계

2026-09-29 읽기 조사에서 **작업자의 PowerShell 입력 스크립트가 SDK 로그의
테스트 기기 해시에 `ToLowerInvariant()`를 적용한 오류**를 확인했다. 제품 코드
`GoogleUmpConsentClient.CreateDebugSettings`는 입력을 그대로 전달하며, 실제 player의
Unity Android bridge 생성 코드에도 대소문자 변환이 없다. 제품의 지역 선택 코드
오류로 판정한 것은 아니다.

기존 UMP Android `4.0.0` 라이브러리를 읽으면 SDK 해시는 `%032X` 형식의 대문자이고,
`ConsentDebugSettings.Builder`는 등록 문자열을 대소문자 구분 없이 정규화하지 않고
목록에서 비교한다. 테스트 기기로 인식되지 않으면 요청의 debug 지역 목록은 비워진다.
또한 SDK의 테스트 기기 등록 안내 로그는 debug settings가 없거나
`isTestDevice()`가 false일 때 출력된다.

기존 native 로그에는 EEA 재시작(11:29:34), 미국 1차(11:30:08), Other(11:31:08),
Under13·EEA(11:31:26), 미국 2차(12:26:12 KST)에 이 등록 안내가 남아 있다.
따라서 이 관측들의 **Update 성공은 강제 지역 적용 성공의 근거가 될 수 없다**.
미국의 두 `NOT_REQUIRED` 응답은 보존하지만, 이를 올바르게 강제된 미국 지역에서의
메시지 설정 문제로 판정할 수 없다. Other와 Under13 사례의 지역별 결론도 유보한다.
Under13에서 `tfua=True`를 전달한 사실과 Unknown·Declined의 호출 전 차단은 별개다.

11:25:47의 EEA 폼 관측 시점에는 수집된 로그에서 같은 안내가 발견되지 않았지만,
경고 부재만으로 모든 지역 적용을 입증하지 않는다. 실제 유럽 폼·거절·옵션 변경 및
재시작 후 첫 폼이 재표시되지 않은 UI 관측은 그대로 보존한다. 전체 사례를 강제 지역
검증 성공으로 확대하지 않는다. 원시 해시·게시자 식별값은 공개하지 않는다.

공식 문서도 debug 지역은 테스트 기기에서만 동작한다고 설명한다.
[Unity UMP 테스트 기기 등록](https://developers.google.com/admob/unity/privacy#testing)과
[미국 주 메시지 테스트](https://developers.google.com/admob/android/privacy/us-iab-support)를
함께 대조했다. 이 정정에는 기기 실행·재빌드·메시지 설정 변경이 없다. 재관측이
승인된다면 기존 APK를 사용하고, SDK가 출력한 해시의 대소문자를 그대로 보존·대조한 뒤
테스트 기기 인식 근거를 먼저 확인하는 절차로 바꿔야 한다. 세 번째 실행은 승인 전 보류한다.

### 사용자 승인 후 미국 주 3차 단일 관측

2026-09-29 14:29~14:30 KST, 사용자가 원본 식별값 보존과 기존 APK 재사용을 조건으로
미국 주 3차 1회를 명시적으로 승인한 뒤 실행했다. 관측 checkout은
`C:/Users/pc_17/.codex/worktrees/e5e5/Tamer`, HEAD
`ece493d09fcc70bd5761ad9afe1d71cf6462d1c0`이며 시작 시 미커밋 변경은 없었다.
APK 실제 소스는 기존 `7f5e24723a2f955b19aad26f18c2f8dddb83131f`, Unity
`6000.3.25f1`/CLI `1.0.0-beta.8`, min25/target36/ARM64/debug signing이다.
설치 직전 APK SHA-256
`570bc882e52b23a2ff0549e90b4557b9ad02d49187cbb9a21cbe1ea1314167a4`를 다시 확인했다.
같은 SM-N986N Android13/API33·4KB 기기에 own 격리 패키지가 없음을 확인하고 새로 설치했다.

기존 SDK 등록 안내의 정확히 한 원문에서 대문자 해시를 추출했다. 입력값을 원문과
`Ordinal` 비교했고, 누락·다중 매칭·형식 변경·불일치는 중단하는 절차를 사용했다.
변환 없이 문자별로 입력한 뒤 성인·`RegulatedUSState`, `tfua=False`로 명시적
UMP Update를 **1회** 호출했다. 이번 실행 PID의 native 로그에는 테스트 기기 등록
안내가 0건이었다. 이 부재만을 성공 근거로 삼지 않고, 원본 문자열 대조와 앞 절의
SDK 비교 분기, 다음 실제 SDK 상태·화면을 함께 확인했다. 앱 안의 `isTestDevice()`
반환값 자체를 별도 계측하거나 네트워크 요청의 debug 필드를 캡처한 것은 아니다.

- SDK 업데이트 성공 후 `privacy_options_requirement_status=REQUIRED`와
  `UMP privacy options` 버튼이 나타났다.
- 버튼으로 실제 영어 `My data preferences` 화면을 열었다. 앱 아이콘과 판매·공유
  허용/거부 선택 및 `Save and close`가 표시됐다. 초기 화면은 허용이 선택돼 있었다.
- `Don't sell or share my data`로 변경하고 저장했다. 같은 세션에서 옵션 화면을 다시
  열었을 때 거부 선택이 유지됐으며, 화면을 닫는 완료 콜백도 확인했다.

이로써 **미국 개인정보 옵션 진입·거부 선택 저장·재진입 유지**를 관측했다.
앞선 두 관측과 입력 오류 이력은 보존한다. EU·Other·TFUA를 재실행하지 않았으므로
앞 절의 해당 지역 적용 한계는 그대로 남는다. UMP `CanRequestAds=True`는 실제 광고
요청이나 맞춤 광고 동의의 증거가 아니다. managed 광고 초기화·로드·표시 경로를
실행하지 않았고, 재빌드·Console 설정 변경·운영 로그인·구매도 수행하지 않았다.

완료 후 이번 설치 경로와 일치하는 own 격리 패키지만 종료·제거하고 부재를 재확인했다.
운영 패키지는 계속 미설치였고, 폰 임시 화면 파일 4개도 제거했다. 원문 SDK 로그·화면과
1회 Update/옵션 완료 2회 기록은 ignored
`Logs/revival/publisher-ump-device-20260929/us-third-*`에 비공개로 보존한다.
이 결과는 운영 광고 활성화, 지역 정책 전체 승인, 16KB 기기·최종 AAB·스토어 검증을
대신하지 않는다.
