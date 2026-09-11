# Families 스토어 재검증과 샘플 실행 준비 (#91)

확인일 2026-09-11. 기준 main `4e8c039`, 작업 브랜치 `codex/families-store-readiness-91`.
Console은 기존 로그인 세션에서 읽기만 수행했다. 선언 저장·광고 활성화·스토어 제출은 하지 않았다.

## Console에서 다시 확인한 상태

| 항목 | 확인한 내용 | 제출 전 필요한 조치 |
| --- | --- | --- |
| 정책 상태 | “5초 후에도 닫을 수 없는 광고”, 이전 버전 사용 가능 | 수정 빌드와 실제 광고 형식 근거를 연결 |
| 검토 번들 상세 | 26(1.0.5), 프로덕션 표기, target 36, 최초 게시일 2026-08-13 | 거부된 검토 번들 표기를 현재 서비스 중 버전이라고 해석하지 않음 |
| 대상 연령 | 9–12, 13–15, 16–17; 교사 승인 신청 아님 | 아동 포함 선언을 전제로 Families 요구 검증. 성인 대상 선언으로 임의 변경하지 않음 |
| 광고 | 광고 포함: 예 | 운영 광고 차단 및 기존 No Ads 동작과 제출 설명 일치 확인 |
| 광고 ID | 광고 ID 사용: 예, 마지막 변경 2024-10-03 | 최종 manifest 권한·실제 식별자 사용 근거와 대조 |
| 데이터 보안 | 수집/공유 없음, 암호화되지 않음, Families 준수 약속 | #105의 SDK/서비스별 데이터 흐름 초안과 대조 후 수정 |
| 개인정보처리방침 | GitHub 저장소 루트 URL | #105에서 공개 가능한 실제 방침 페이지와 URL을 준비한 뒤 검토 |

앱 콘텐츠의 다른 주요 선언은 2024-09-30 변경으로 표시됐다. 계정 식별자, Console URL,
세션 정보, 원시 화면/로그는 공개 문서에 포함하지 않는다. 위 표는 변경안의 근거이며 새 선언값을 확정하지 않는다.

AdMob도 같은 날 재접속했으나 기존 세션이 가입/약관 화면으로 이동했다. 가입·약관 동의·계정 전환은
수행하지 않았고 확인용 탭은 닫았다. 이 세션으로 실제 앱의 메시지·mediation 설정을 확인할 수 없으며,
운영 App ID의 소유 계정이나 설정이 없다고 단정하지 않는다.

## 초기화·동의·통신의 확인 범위

앱 소스의 광고 요청 경로는 `GoogleAdMobManager` 한 곳이다. 시작 시 `Init()`은 씬 이벤트만
구독한다. 명시적 샘플 요청 시 아동 태그 true, 동의 연령 미만 true, 최대 등급 G를 설정한 뒤
`MobileAds.Initialize`와 `RewardedAd.Load`를 호출한다. 일반 release는 이 API 경로를 차단한다.

UMP 4.0.0이 포함되어 있으나 앱에는 `ConsentInformation.Update`, 동의 양식 표시,
`CanRequestAds()` 또는 개인정보 옵션 진입점 호출이 없다. Unity의 별도 `IapConsentDefaults`는
미지정 Ads/Analytics 목적을 Denied로 초기화한다. 이것은 Google UMP 절차의 구현이 아니다.
[공식 UMP 절차](https://developers.google.com/admob/unity/privacy?hl=en)를 운영 광고 활성화 전에
대상 연령·지역 및 AdMob 메시지 설정과 함께 검토해야 한다.
[초기화 안내](https://developers.google.com/admob/unity/quick-start?hl=en)에 따라 요청 플래그와
필요한 동의 처리는 수동 SDK 초기화보다 앞서야 한다.

로컬 Gradle 캐시의 `play-services-ads-api:25.4.0` AAR를 ZIP/`javap -c -p`로 확인했다.

| 근거 | 확인 결과 |
| --- | --- |
| AAR SHA-256 | `42ec36e86a7f8e326f541c6dd2afd3fe738ef278712e4e5e4f0f071ca169869c` |
| classes.jar SHA-256 | `f124d9f4d93927f75ca14d0c0fa3f5577f8973f010211750628f7566505bbf1e` |
| AAR manifest | `MobileAdsInitProvider`, initOrder 100; INTERNET, AD_ID 및 AdServices 권한 포함 |
| `MobileAdsInitProvider.onCreate` | false 반환 |
| `attachInfo` → `internal.client.zzev.attachInfo` | 앱 메타데이터 읽기, App ID 존재/형식 검사, 최적화 플래그 로그, 상위 `attachInfo` 호출 |
| 위 두 클래스 내 직접 광고 초기화/로드/네트워크 호출 | 발견하지 못함 |

이 검사는 provider가 있다는 이유만으로 광고 요청이나 전송이 발생했다고 단정하지 않게 하는
제한된 정적 근거다. 다른 클래스의 static 초기화, 다른 provider, Play services, 최종 병합 APK 및
실제 통신의 부재를 증명하지 않는다. 패킷이 보이지 않는 관찰 한 번도 “수집 없음”의 증거가 아니다.
[Google의 데이터 공개 안내](https://developers.google.com/admob/android/privacy/play-data-disclosure)와
실제 활성 기능·전송 관찰을 #105에서 함께 판정한다.

샘플 빌드의 `RuntimeInitializeOnLoads.json`과 IL2CPP 생성 코드에는
`GoogleMobileAds.Common.Insight.CacheBaseProperties`의 BeforeSceneLoad 실행이 포함된다.
이 함수는 플랫폼, 앱 패키지명/버전, Unity 버전, OS, 기기 모델을 static field에 보관한다.
해당 함수 내 직접 전송은 발견하지 못했다. **시작 시 SDK의 로컬 정보 접근은 존재**하므로
관리 광고 요청 차단을 SDK의 모든 정보 접근 차단으로 설명하지 않는다.

같은 초기화 목록에 Codeless IAP 및 UGS hook도 포함된다. `IAPProductCatalog.json`의
`enableCodelessAutoInitialization`과 `enableUnityGamingServicesAutoInitialization`은 false다.
현재 IAP 소스는 첫 값이 true일 때만 자동 store listener를 생성하며,
UGS `EnableInitializationAsync`는 초기화 요청이 없으면 반환한다. 앱의 `Managers`/로그인/저장/
`IAPManager` 시작 호출은 검증 씬에 없다. 패키지 hook 등록과 실제 서비스 연결은 구분한다.

## 별도 검증 앱

`RevivalAdHarnessBuild.BuildSample`은 개발 APK, `BuildReleaseControl`은 테스트 광고 플래그 없는
release 정책 대조 APK를 만든다. 각각 `.revival.ads`와 `.revival.adscontrol` 별도 앱 ID/debug 서명을
사용한다. 게임 씬을 포함하지 않고, 빌드 시 생성하는 씬의 MonoBehaviour를 광고 매니저·사운드 매니저·
검증 UI로 제한한다. `TAMER_REVIVAL_SMOKE`로 로그인 씬 자동 이동을 막는다.

두 APK 모두 [공식 Android 샘플 App ID](https://developers.google.com/admob/android/quick-start)
`ca-app-pub-3940256099942544~3347511713`를 빌드 직전에만 적용한다. 빌드의 finally에서 원래
SDK 설정/Android library manifest 바이트와 앱 ID·서명 사용·alias·backend·bundle 설정을 복원한다.
`Run-AdHarness.ps1`은 Editor 시작 전 파일도 보관해, Editor import가 먼저 비우는 기존 signing alias까지
실행 종료 시 원래 바이트로 복원한다.
EDM resolver가 기록하는 `AndroidResolverDependencies.xml`의 임시 앱 ID도 보존 대상이다.
CLI 종료 후에도 해당 checkout의 Editor가 남아 있으면 파일을 덮어쓰지 않고 실패를 알린다.
이때 실행 전 원본은 ignored `.revival-local/ad-harness-snapshots/<실행ID>/`에 보존한다.
최종 APK manifest에서도 샘플 ID를 별도로 확인해야 한다. 일반 배포 플레이어에는 harness 및
오디오 주입 setter가 포함되지 않는다 (`UNITY_EDITOR || TAMER_AD_TEST_HARNESS` 조건).

저장된 씬 상태와 해당 checkout의 Editor 종료를 확인한 뒤 로컬에서 재현한다. `<checkout>`은 절대 경로다.

```powershell
./tools/revival/Run-AdHarness.ps1 -ProjectPath '<checkout>' -Variant sample
```

대조 앱은 `-Variant control`로 바꾼다. wrapper는 빌드 후 APK verifier까지 실행한다.
빌드 종료 후 `git diff`로 SDK 설정·manifest·PlayerSettings 복원 상태를 확인한다. 샘플 APK는
`Build/revival/Tamer-ads-sample.apk`, 대조 APK는 `Build/revival/Tamer-ads-control.apk`에 생성된다.

실행 앱은 시작 시 관리 광고 SDK를 초기화하지 않는다. Load/Show 버튼에서만 요청한다.
생성한 음원을 실제 `SoundManager.PlayBGM`/`PauseBGMForAd`에 연결한다. 계정·PlayerPrefs·게임 저장·
IAP 매니저를 초기화하지 않는다. SDK 자체의 시작 동작은 별도 관찰 대상이다.

| 실행 시나리오 | 확인해야 할 증거 |
| --- | --- |
| 시작 후 미조작 / release 대조 앱 Load·Show | 초기화/로드 trace 부재, release `PolicyBlocked`, 보상 0; 실제 통신은 별도 기록 |
| 샘플 로드·표시·취소·완료 | 샘플 표시 확인, 보상 수신 및 종료 결과, BGM 위치/재생 여부 |
| 표시 중 요청자 삭제 | 원래 요청 번호/owner로 보상 0, UI 카운터 유지 |
| 표시 중 매니저 삭제 | 종료 결과와 오디오 정리; UI 카운터 유지 |
| 표시 중 빈 씬 A→B→A | 같은 씬으로 돌아와도 원래 보상 무효 |
| 표시 중 BGM 교체 | 이전 광고의 종료가 새 재생을 되감거나 재시작하지 않음 |
| Home/복귀, 중복 클릭 | pause/focus 로그와 실제 SDK 종료 구분, 보상 중복 없음 |

예약 동작은 opened 콜백 수신 후 최소 2초가 지나 Unity Update가 실행될 때 수행한다. native 광고가
Unity를 정지시킨 경우 닫힌 뒤에야 실행될 수 있으므로 로그로 실제 시점을 판정한다. 이 타이머는
광고를 닫거나 보상을 지급하지 않는다. 콜백의 monotonic 수신 시각, Unity 처리 시각, native 첫 화면,
실제 닫기 터치 시각은 각각 구분한다. 샘플이 5초에 닫혔다는 관찰은 운영 소재의 준수 증명이 아니다.

## 실행한 검증

코드 커밋 `643954780f2ce3e8544c9ce94fa312dafb7cf48c`, checkout `e5e5/Tamer`,
Unity 6000.0.81f1 / CLI 1.0.0-beta.8 / GMA Unity 11.5.0 / Android GMA 25.4.0 / UMP 4.0.0.

| 항목 | 실제 결과 |
| --- | --- |
| EditMode 전체 `Revival` 회귀 | 249/249 통과, 실패 0, 건너뜀 0; 새 worker trace 검사 포함 |
| 이후 UI/빌더 보완 | 스크롤·기존 씬 참조 검증·catalog auto-init 차단 추가; 최종 샘플 APK compile/build로 확인 |
| 자산 복원 검증 | 4,561개 검증, 복사 0; 서비스 설정 제외 |
| 기존 에셋 GUID 감사 | 파일 132개, 미해결 GUID 0 |
| 최종 샘플 APK | 110,122,561 bytes, 아래 SHA-256; 별도 앱 ID, ARM64, min24/target36, 버전1.0.5/code26 |
| 샘플 APK manifest/서명 | 공식 sample App ID, debuggable=true, debug 서명 검증 통과; MobileAdsInitProvider/AD_ID 포함 |
| 대조 APK | 63,583,489 bytes, 별도 `.revival.adscontrol`, ARM64, min24/target36, 버전1.0.5/code26 |
| 대조 APK manifest/서명 | 공식 sample App ID, debuggable=false, debug 서명 검증 통과; MobileAdsInitProvider/AD_ID 포함 |
| prelaunch 보존 파일 | 최종 샘플 wrapper 종료 후 SDK 설정·manifest·PlayerSettings·SceneTemplate 원복 diff 0 |
| 대조 종료 정리 | 위 4개 자동 복원, resolver 임시 앱 ID 수동 원복; 나머지 Editor 직렬화 줄바꿈 원복 |
| verifier 음성 검사 | 샘플 APK를 control로 검증하면 잘못된 앱 ID로 거절됨 |

샘플 APK: `Build/revival/Tamer-ads-sample.apk`.
SHA-256: `800cd31ec746f22aeb8c3f426e8a05bef786b079ba683b36585a7e8b6077210e`.
대조 APK: `Build/revival/Tamer-ads-control.apk`.
SHA-256: `083e5556faa1f99ef91c4e94dd905127e83006baaa87ceada0e52a9da374ac73`.
대조 빌드에서 발견한 resolver 캐시 변경은 wrapper 보존 목록에 추가했다. 이 마지막 목록 보완은
PowerShell 구문 검사로 확인했으며, 목록 보완만을 이유로 APK를 다시 빌드하지 않았다.
기기에서 샘플 광고를 표시했다는 결과나 일반 release에서 네트워크가 없었다는 결과는 아니다.

## 남은 제출 게이트

SDK 담당의 [기기 준비 및 AAB/split 근거(PR #109)](https://github.com/ChoiDaeYoung-94/Tamer/pull/109):
API 36 ps16k x86_64 rev7 / Emulator 37.1.11 환경에서 하드웨어 가속 검사 code 6(드라이버 없음).
첫 software 부팅은 600초 한도에서 adb offline, Vulkan을 끈 추가 진단은 약 5초 뒤
`0xC0000005`로 종료했다. 원인을 하드웨어 가속 하나로 확정하지 않는다. 기기 PAGE_SIZE,
앱 설치·실행, 샘플 로드·닫기·보상·BGM, 실제 네트워크 관찰은 미실행이다. Windows 기능 변경·재부팅은
수행하지 않았다. 다음 실행에는 사용 가능한 기기 또는 정상 부팅하는 가속 환경이 필요하다.

샘플 및 일반 release 정책 대조 APK의 빌드·manifest 검증을 완료했다. 실제 운영 광고는 계속 차단한다.
AdMob 메시지·mediation·custom event·계정 접근 확인, 광고 형식 근거, #105의 개인정보처리방침 및
데이터 보안 정합성, 최종 배포 산출물 검토가 끝난 뒤 통합 담당에게 제출 가능한 변경안을 전달한다.
