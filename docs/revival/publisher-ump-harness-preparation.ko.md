# 게시자 UMP 전용 harness 준비 — 네트워크·게시 미실행

2026-09-28, checkout `C:/Users/pc_17/.codex/worktrees/e5e5/Tamer`, 기반
`38a284fdd0b54ccae36e6c96b314c23285d3f3ba`, Unity `6000.3.25f1` / CLI `1.0.0-beta.8` /
Android min25·target36·ARM64 기준이다. 기존 정책
`https://github.com/ChoiDaeYoung-94/Tamer#개인정보처리방침` 본문 보완은 PR #264로 병합됐다.
유럽 영어 메시지는 사용자 승인된 게시 후보이며 두 메시지는 Console 초안/게시 OFF 상태다.
이 준비는 메시지 게시나 운영 광고 활성화 승인이 아니다.

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
현재 이 명령을 실행하지 않았다. wrapper는 설치/기기 실행/메시지 게시를 하지 않는다.
기존 APK verifier를 확장해 실제 merged manifest App ID 정확1개/private config 일치/샘플혼입없음,
별도 package/debug signature/SDK/ABI를 검사한다. 결과에는 ID 자체 없이 일치 bool을 기록한다.

게시 전 가능한 검사: 소스/설정 정합, offline verifier 경계, 독립 정적 리뷰, 후속 승인된
Unity compile/build 및 APK manifest/signature 검사. 이것은 게시자 폼 표시 성공이 아니다.
게시 후 별도 범위에서 확인할 것: 실제 publisher Update·EEA 거절→Required 옵션→변경→재시작,
미국 opt-out/옵션 진입, Other 관측, 대표 TFUA=true 억제, Unknown/Declined 시작 차단.
현재 Console 미게시 상태에서는 예상 폼 성공을 단정하거나 반복 기기 요청으로 우회하지 않는다.

## 검증과 미검증

- Python offline verifier 경계 **4/4 PASS 한 번**: 합성 publisher metadata 일치/결과 ID 비노출,
  기존 sample 유지, sample/중복 manifest 거절, 누락/중복/다른 checkout private config 거절.
  실제 APK나 네이티브 실행을 사용하지 않았다.
- 별도 .NET 9 compile 확인은 두 번 참조 구성 오류로 실패했다. 1차 UnityEngine과 modules
  중복 형식, 이를 제외한 2차 UnityEditor와 CoreModule 중복 및 기존 internal
  GoogleUmpConsentClient의 외부 assembly 접근 오류다. 실제 두 소스를 링크했지만
  제품의 Unity compile 성공/실패 판정 근거가 아니다. 지시대로 3차나 유사 우회 실행을 하지 않는다.
- IL2CPP/APK/AAB·빌드 경로의 finally 실실행·게시자 네트워크/기기·메시지 게시는
  **미실행/미검증**이다. 새 APK SHA-256은 해당 없음. raw 구성 결과는 ignored 로컬 경로에 보존한다.
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

실제 APK를 설치/실행하지 않았으며 publisher UMP 네트워크·폼 표시·거절/옵션/재시작,
네이티브 전체 트래픽, 이 APK의 LOAD/ZIP/strict RELRO, AAB/스토어는 미검증이다.
메시지 Publish OFF/운영 및 지역 flags false를 유지한다. 별도 범위 확인 전 기기/게시 실행하지 않는다.
