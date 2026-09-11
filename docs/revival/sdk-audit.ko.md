# SDK 호환성 조사 및 vendor 갱신

조사일: 2026-09-11. 기준 Editor: **Unity 6000.0.81f1**, Android minSdk 24 / targetSdk 36.
이 문서는 공개 SDK의 버전·출처·변경 범위를 기록한다. 운영 계정 연결, 결제, 광고 노출, 실제 기기와 스토어 승인은 별도 검증이다.

## 버전과 선택 근거

| SDK | 이전 상태 | 확인한 최신 버전 / 이번 선택 | 호환성 및 변경점 |
| --- | --- | --- | --- |
| UniTask | 2.5.10 | **2.5.11** | Unity 2018.4 이상. Unity 패키지의 실질 변경은 버전 선언과 Unity 6.2 이상용 Tracker TreeView 타입 별칭 두 파일이다. Unity 6.0 동작은 유지한다. |
| Google Play Games | 2.1.0 | **2.2.1** | minSdk 24는 프로젝트와 일치한다. Games v2 22.0.0, Nearby 18.5.0. Game Stats API 추가, Java 호환성 수정. 로컬 Maven/srcaar 방식이 직접 bridge AAR + 원격 Maven 의존성으로 바뀐다. |
| EDM4U | 1.2.182 | **1.2.189** | Unity 6 package 경로(1.2.183), AGP packaging 문법(1.2.184–185), Editor DLL 활성화와 중첩 m2repository(1.2.187) 수정이 포함된다. 1.2.188–189는 iOS resolver 수정이다. |
| Google Mobile Ads | Unity 9.1.1 / Android XML 23.2.0 / UMP 2.2.0 | 최신 Unity **11.5.0**, 선택 가능한 legacy Android **25.4.0**, UMP **4.0.0** | 플러그인 11.1부터 legacy/NextGen 선택이 가능하다. 11.5 소스의 기본값은 Standard다. 광고 담당과 API 변경을 조율한다. |
| PlayFab UnitySDK | 2.138.220621 | **2.242.260805** | 공식 태그의 Packages/UnitySDK.unitypackage를 기존 경로에 반영했다. 게임이 사용하는 8개 Client API의 요청/성공/오류 callback 서명과 기존 GUID를 유지한다. |
| Unity IAP | 5.0.1 / v4 호환 API | **5.4.3 / v5 API** | UPM Client.AddAndRemove로 고정 설치. services.core 1.18.0, Google Play Billing **9.0.0**(설치 패키지 changelog와 생성 Gradle 확인). pending 저장 성공 후 확인 및 비소모품 복원 경로를 구현한다. |

UniTask, GPGS, EDM, GMA, PlayFab은 기존 `Assets` 배치를 유지한다. UPM 이동과 중복 설치를 섞지 않는다. IAP은 기존 UPM 관리 방식을 유지한다.

IAP 5.4.3 공식 [변경 기록](https://docs.unity3d.com/Packages/com.unity.purchasing@5.4/changelog/CHANGELOG.html)과 패키지 registry의 최소 Unity 2022.3 조건을 확인했다. UPM 설치 시 assembly reload로 비동기 요청의 callback이 사라지는 경우에도 명시적 설치 명령에 한해서 등록된 패키지 버전을 재확인하고 종료한다.

PlayFab 공개 SDK 파일의 새 버전 해시만 복원 manifest에 갱신했다. 구매 에셋의 원본 해시는 유지한다. `git-sdk` 파일 누락은 Git에서 복원해야 하며, 구형 비공개 에셋 아카이브를 새 SDK의 대체 출처로 사용하지 않는다. PlayFab 서비스 설정은 계속 중립 템플릿/비공개 로컬 파일이고 원본 값은 복사하지 않았다.

공식 출처:

- [UniTask 2.5.11 릴리스](https://github.com/Cysharp/UniTask/releases/tag/2.5.11), [2.5.10→2.5.11 diff](https://github.com/Cysharp/UniTask/compare/2.5.10...2.5.11)
- [GPGS 2.2.1 릴리스](https://github.com/playgameservices/play-games-plugin-for-unity/releases/tag/v2.2.1), [2.2.0 minSdk 변경](https://github.com/playgameservices/play-games-plugin-for-unity/releases/tag/v2.2.0), [2.2.1 Android 의존성](https://github.com/playgameservices/play-games-plugin-for-unity/blob/v2.2.1/Assets/Public/GooglePlayGames/com.google.play.games/Editor/GooglePlayGamesPluginDependencies.xml)
- [EDM 1.2.189 변경 기록](https://github.com/googlesamples/unity-jar-resolver/blob/v1.2.189/CHANGELOG.md)
- [GMA 11.5.0 릴리스](https://github.com/googleads/googleads-mobile-unity/releases/tag/v11.5.0), [legacy 의존성](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/source/plugin/Assets/GoogleMobileAds/Editor/GoogleMobileAdsDependencies.xml), [Architecture 기본값](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/source/plugin/Assets/GoogleMobileAds/Editor/GoogleMobileAdsSettings.cs)
- [PlayFab 2.242.260805 릴리스](https://github.com/PlayFab/UnitySDK/releases/tag/2.242.260805), [공식 서비스 릴리스 노트](https://learn.microsoft.com/en-us/xbox/playfab/release-notes/), [Client API 소스](https://github.com/PlayFab/UnitySDK/blob/2.242.260805/ExampleTestProject/Assets/PlayFabSDK/Client/PlayFabClientAPI.cs)

## Families 인증과 GMA API 경계

조사 당시 [공식 Families 인증 목록](https://support.google.com/googleplay/android-developer/answer/12955712)의 AdMob 항목은 **`com.google.android.gms:play-services-ads` 19.0.0 이상**이다. NextGen 좌표인 **`com.google.android.libraries.ads.mobile.sdk:ads-mobile-sdk`**는 해당 목록에 없다. 따라서 이 증거로 인정할 수 있는 범위는 legacy 좌표이며, NextGen의 인증을 추정하지 않는다. 앱/광고 전체의 정책 적합성이 SDK 좌표만으로 증명되지는 않는다. 출시 시점에 목록을 다시 확인해야 한다.

11.5.0 플러그인에서 `overrideDefaultGmaAndroidSdk=true`, `selectedGmaAndroidSdk=0`으로 Standard를 명시하고, 최종 Gradle 의존성에 NextGen 좌표가 없는지 검증할 수 있다. 두 필드는 Git이 추적하는 GMA 설정에 포함되므로 새 checkout에서도 별도 수동 Configure 없이 적용된다. 기존 서비스 ID는 보존하며 에셋 복원 스크립트는 이 설정을 덮어쓰지 않는다. 이것은 최신 플러그인과 legacy SDK를 함께 사용하는 선택이다.

- 10.7에서 `RaiseAdEventsOnUnityMainThread`가 obsolete 처리됐다. Unity 객체 조작은 `MobileAdsEventExecutor.ExecuteInUpdate`로 넘기는 방식이 공식 권고다.
- 11.3에서 `AgeRestrictedTreatment`가 추가되고 기존 TFCD/TFUA 필드가 obsolete 처리됐다. 기존 아동 대상 제한을 보존하면서 광고 담당이 전환을 검증해야 한다.
- v25 Android의 주요 삭제 API는 mediation/native 쪽에 집중되어 있다. 최신 Unity wrapper와 의존성을 함께 맞춰야 한다.

출처: [GMA 10.7 릴리스](https://github.com/googleads/googleads-mobile-unity/releases/tag/10.7.0), [GMA 11.3 릴리스](https://github.com/googleads/googleads-mobile-unity/releases/tag/v11.3.0), [Android 릴리스 노트](https://developers.google.com/admob/android/rel-notes), [Unity 초기화·스레드 가이드](https://developers.google.com/admob/unity/quick-start).

## GPGS 설정 및 GUID 보존

2.2.1은 `GameInfo`의 상수 대신 Resources의 `PlayGamesSettings`를 읽는다. 공식 SDK는 기존 `GameInfo` 호출을 유지하는 obsolete 속성 shim과 `GPGSUpgrader.EnsureMigrated()`를 제공한다.

이 checkout의 기존 `GameInfo.cs`에는 placeholder가 있고, `ProjectSettings/GooglePlayGameSettings.txt`에 기존 앱/OAuth 설정이 있다. 값은 문서와 테스트 출력에 기록하지 않는다. 공식 upgrader는 해당 설정을 읽어 `Assets/GooglePlayGames/Resources/PlayGamesSettings.asset`을 생성한다. 생성된 서비스 설정과 그 meta는 공개 추적 대상에서 제외하며, 기존 원본 설정의 값과 계정 연동을 바꾸지 않는다.

회귀 테스트 `RevivalSdkMigrationTests`는 Android target에서 다음을 확인한다.

1. 기존 설정, 생성된 Resources 설정, `GameInfo` shim의 값이 동일하다. Assert에는 비교 결과만 전달하므로 실패 로그에도 서비스 값이 출력되지 않는다.
2. `EnsureMigrated()` 반복 호출 전후 asset bytes와 GUID가 같다.

공식 출처: [GPGS upgrader](https://github.com/playgameservices/play-games-plugin-for-unity/blob/v2.2.1/Assets/Public/GooglePlayGames/com.google.play.games/Editor/GPGSUpgrader.cs), [설정 생성](https://github.com/playgameservices/play-games-plugin-for-unity/blob/v2.2.1/Assets/Public/GooglePlayGames/com.google.play.games/Editor/GPGSUtil.cs), [GameInfo shim](https://github.com/playgameservices/play-games-plugin-for-unity/blob/v2.2.1/Assets/Public/GooglePlayGames/com.google.play.games/Runtime/Scripts/GameInfo.cs).

갱신 시 기존 경로의 `.meta` GUID와 importer 설정을 보존한다. EDM/GPGS의 기존 `gvh_version-*` 라벨만 새 버전으로 맞춘다. 추가 파일은 공식 `.unitypackage`의 meta를 사용한다. EDM 버전 폴더는 기존 폴더 GUID를 새 버전 폴더로 유지한다. 없어진 버전별 DLL, GPGS srcaar/POM, 버전 manifest는 SDK의 확인된 정확한 경로에서 제거한다. GPGS 2.2.1 패키지에 포함된 이전 2.2.0 installer 아카이브는 프로젝트에 재포함하지 않는다.

## 다운로드 출처와 SHA-256

| SDK | 공식 배포물 | SHA-256 |
| --- | --- | --- |
| UniTask 2.5.11 | [Unity package](https://github.com/Cysharp/UniTask/releases/download/2.5.11/UniTask.2.5.11.unitypackage) | `199d012067c902113a5a31e3c9a0158b779368916d4cc634a85bf1b1453a7fff` |
| GPGS 2.2.1 | [tag 고정 Unity package](https://github.com/playgameservices/play-games-plugin-for-unity/blob/v2.2.1/current-build/GooglePlayGamesPlugin-2.2.1.unitypackage) | `09c350e28c6dcab994bde47e9e3d14d77e826b0bdbb179bb6f20851bb09cc817` |
| EDM 1.2.189 | [tag 고정 Unity package](https://github.com/googlesamples/unity-jar-resolver/blob/v1.2.189/external-dependency-manager-1.2.189.unitypackage) | `bef79e97dfa0774ad01473f804d8c07892d05b7fbf21e6397d07a853be629d8c` |
| GMA 11.5.0 | [Unity package](https://github.com/googleads/googleads-mobile-unity/releases/download/v11.5.0/GoogleMobileAds-v11.5.0.unitypackage) | `770c34bb7485e8db20a888cc008a14fdc94ffe8d34b4d2ee24434a7be91e08ce` |
| PlayFab 2.242.260805 | [공식 tag source archive](https://github.com/PlayFab/UnitySDK/archive/refs/tags/2.242.260805.zip)의 Packages/UnitySDK.unitypackage | `2b9ac23ec711edcca0c63d12754ae74d65ae91de9441db61fcf1602e901b4b7b` |
| IAP 5.4.3 | [공식 registry tarball](https://download.packages.unity.com/com.unity.purchasing/-/com.unity.purchasing-5.4.3.tgz) | `a61b395c43051363f39b1f47d48a706f7908e0a8d8ee0f07f3c29829da98e098` |

라이선스 원문은 UniTask MIT 및 GPGS Apache-2.0을 이 문서 옆 `sdk-licenses` 폴더에 보존한다. EDM은 `Assets/ExternalDependencyManager/Editor/LICENSE`의 Apache-2.0와 포함 MiniJSON 고지를 보존한다. GMA Unity wrapper와 PlayFab UnitySDK도 공식 저장소 LICENSE가 Apache-2.0이며, Android 서비스 SDK와 운영 서비스 약관을 Unity wrapper 라이선스로 대체하지 않는다.

라이선스 출처: [UniTask](https://github.com/Cysharp/UniTask/blob/2.5.11/LICENSE), [GPGS](https://github.com/playgameservices/play-games-plugin-for-unity/blob/v2.2.1/LICENSE), [EDM](https://github.com/googlesamples/unity-jar-resolver/blob/v1.2.189/LICENSE), [GMA wrapper](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/LICENSE), [PlayFab](https://github.com/PlayFab/UnitySDK/blob/2.242.260805/LICENSE).

## Android 16KB 검증 범위

Unity는 [6000.0.38f1부터 16KB 지원을 명시](https://unity.com/releases/editor/whats-new/6000.0.38f1)했으므로 기준 6000.0.81f1을 유지할 수 있다. 전체 결과는 Editor 버전만으로 판단하지 않는다.

읽기 전용 archive 검사에서 현재 vendored GMA/GPGS/IngameDebugConsole AAR, 최신 GPGS 2.2.1 bridge AAR, `play-services-games-v2:22.0.0`, `play-services-ads:25.4.0`에 `.so` 항목이 없음을 확인했다. 이것은 해당 archive에 대한 조사이며 모든 transitive dependency와 최종 Unity APK의 적합성 증명은 아니다.

[Android 공식 검증 절차](https://developer.android.com/guide/practices/page-sizes)에 따라 최종 APK의 모든 64-bit `.so`에 대해 ELF LOAD alignment(최소 `2**14`), RELRO 경계, APK ZIP alignment를 확인하고 16KB 환경에서 실행해야 한다. NDK r27 이하 빌드의 RELRO 마지막 경계도 별도로 확인한다. 코드 반영·APK 정적 검사·16KB 기기 실행·스토어 승인 증거를 구분한다.

적용 직후 정적 확인 결과:

- 기존 meta GUID 보존: UniTask 163개, GPGS 82개, EDM 4개. 해당 SDK가 포함된 중복 GUID는 0개다.
- 배포물과 적용 소스/추가 meta의 불일치는 0개다. 소스의 CRLF/LF 차이는 비교에서 제외했다. 기존 importer 설정과 버전 라벨 보존 규칙은 위와 같다.
- `python tools/revival/audit_guids.py`: YAML 에셋 132개 검사, 미해결 GUID 0개.
- 공식 GPGS 배포물의 `PluginVersion.cs`가 여전히 2.1.0을 선언하는 오류를 2.2.1/0x20201/20201로 보정했다. 패키지 본체는 공식 2.2.1이다. `git diff --check`가 보고한 SDK 소스와 meta의 줄 끝 공백만 정리했으며 PlayFab 공개 SDK의 해당 해시도 반영했다. 구매 에셋은 수정하지 않았다.
- 위 항목은 vendor 적용 직후의 정적 조사 범위다. 이후 SDK 작업 브랜치에서 Android import, GPGS 값 보존 및 GMA Standard 선택/NextGen 거부 테스트를 실행했다. 정확한 최종 테스트 수와 빌드 코드 SHA는 아래 검증 기록에서 확인한다.

Editor import, 회귀 테스트와 APK 검증 결과는 [sdk-validation.json](sdk-validation.json), [IAP 이행](iap-v5.ko.md), [네이티브 정렬 검증](native-alignment.ko.md)에 기록한다.

## 재현 절차

구매 에셋은 `python tools/revival/restore_assets.py --source <소유자의 원본 경로>`로 복원하고 고정 CLI는 `python tools/revival/install_cli.py`로 준비한다. 원본은 읽기 전용으로 사용하며 새 SDK 파일은 Git에서 가져온다. Unity `6000.0.81f1`와 Android 모듈이 설치된 상태에서 해당 프로젝트의 Editor를 닫고 다음을 실행한다. 같은 호스트의 대규모 Editor import/build는 다른 담당과 순서를 조율한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/revival/Run-SdkValidation.ps1
```

명령은 Python 테스트, 복원 파일 검증, Android target EditMode 테스트, 격리 Development APK 빌드, APK ID/서명/API/ABI, GUID 및 네이티브 LOAD/ZIP 검사를 실행한다. 추가 RELRO 끝 검사는 별도 결과이며 실제 16KB 실행을 대신하지 않는다. 서비스 설정 재입력, 운영 로그인, 구매, 광고 요청은 이 절차에 필요하지 않다.
