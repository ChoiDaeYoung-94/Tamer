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
- Unity Editor/runner·IL2CPP/APK/AAB·원복 finally 실실행·게시자 네트워크/기기·메시지 게시는
  **미실행/미검증**이다. 새 APK SHA-256은 해당 없음. raw 구성 결과는 ignored 로컬 경로에 보존한다.
- production/지역 false, 기존 연령별 fullscreen 제한, 보상/No Ads 계약은 변경하지 않았다.

기능 코드 리뷰와 위 참조 구성 실패에 대한 다음 실행 범위 판단이 끝나기 전 빌드하지 않는다.
