# Unity Ads 입찰 어댑터 격리 도입 기록 (2026-09-23)

이 브랜치는 기존 AdMob 중재에 Unity Ads를 입찰 공급원으로 연결하기 위한 **SDK 파일만** 추가한다. 운영 광고 단위, Unity/AdMob 대시보드, 앱 ID·배치 ID, 광고 요청 차단 조건은 변경하지 않는다. 현재 Unity Editor는 `6000.0.81f1`, Google Mobile Ads Unity 플러그인은 `11.5.0`, Android 광고 SDK는 `25.4.0`, UMP는 `4.0.0`, IAP는 `5.4.3`이다.

## 출처와 라이선스

- 공급자: [Google 공식 Unity Ads 중재 통합 안내](https://developers.google.com/admob/unity/mediation/unity)의 `3.21.0` Unity package. 공식 변경 기록은 Google Mobile Ads Unity `11.5.0`에서 빌드·테스트됐고 Android 어댑터 `4.20.0.1`을 지원한다고 명시한다. `3.21.1`은 조사 시점에 `In progress`다.
- 다운로드: `https://dl.google.com/googleadmobadssdk/mediation/unity/unity/UnityAdsUnityAdapter-3.21.0.zip`
- ZIP SHA-256: `37396914fe9a70e3efc2d8b32a517b0427e2a747c6e967259d926804cdc7215b`
- 내부 `GoogleMobileAdsUnityAdsMediation.unitypackage` SHA-256: `99ae1527e30d71dcd6c7fdaa29a664d0121b5778023b4f9d72aa370a1f4cbd6d`
- 배포물의 Apache-2.0 라이선스 원문: [GoogleMobileAdsUnityAdsMediation-3.21.0-LICENSE.txt](sdk-licenses/GoogleMobileAdsUnityAdsMediation-3.21.0-LICENSE.txt). Unity Ads Android SDK는 이 어댑터의 `Editor/Dependencies.xml`이 Maven `com.unity3d.ads:unity-ads:4.20.0`으로 선언하며 별도 바이너리를 저장소에 포함하지 않는다.
- Unity package 안의 `.meta`와 GUID를 그대로 가져왔다. 이미 있던 `Assets/GoogleMobileAds` 폴더와 `.meta`는 보존했다. 가져오기 전 새 항목 23개의 경로·GUID 충돌이 없음을 확인했다.

## 범위와 남은 검증

Google의 Android 중재 어댑터 `com.google.ads.mediation:unity:4.20.0.1` 및 Unity Ads SDK `com.unity3d.ads:unity-ads:4.20.0` 선언을 추가했다. 기존 GMA·UMP·IAP 버전과 광고 차단 코드는 그대로다.

격리 checkout `C:/Users/pc_17/.codex/worktrees/unity-ads-bidder/Tamer`, 브랜치 `codex/unity-ads-bidder-bridge`, 검증 대상 기준 HEAD `3b229853f616fce2056f6739761fe1de7ef4c527`(#232 병합 `origin/main`)에 이 문서와 SDK 파일의 미커밋 변경을 더한 상태에서 다음을 확인했다. Editor를 열기 전에 `restore_assets.py --source`로 비공개 에셋 4,201개를 manifest SHA-256과 대조해 복원했다. 원본 폴더의 브랜치나 파일은 변경하지 않았다.

| 검증 | 결과 |
|---|---|
| Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, `RevivalAdManagerTests` EditMode | 51/51 통과, 실패·건너뜀 0. 결과 XML SHA-256 `4286566db315e4eafd039608cb0d294acb5d515ddae31fe5e3601ecfbafe6434`. |
| `Run-AdHarness.ps1 -Variant sample` Android 격리 빌드 | 1회 성공. resolver가 두 Maven 의존성을 `mainTemplate.gradle`, `settingsTemplate.gradle`, Android Gradle 프로젝트에 추가했다. GMA `25.4.0`, UMP `4.0.0`과 함께 Gradle 빌드 완료. |
| APK 검증 | `Build/revival/Tamer-ads-sample.apk`, 96,300,562바이트, SHA-256 `eb4a5beff9bbdceefd44a67d7d49ed49bc77e37a89d41df57fc7bbb37c732807`. 별도 앱 ID `com.AeDeong.MonsterTamer.revival.ads`, debug 서명, 공식 샘플 App ID, ARM64, min SDK 24/target SDK 36 확인. DEX에서 Google Unity 중재 어댑터·Unity Ads·GMA 클래스 확인. |

이 샘플 빌드는 IAP `5.4.3` 패키지가 있는 프로젝트의 컴파일 및 Android Gradle 호환성을 확인하지만, 격리 광고 씬에서 구매·복원 런타임을 실행하지 않았고 IAP 클래스의 APK 포함도 증명하지 않는다. 실제 광고 응답, 5초 닫기 또는 정책 적합성도 확인하지 않았다. 테스트 결과와 APK는 로컬 무시 경로에만 두고 공개 PR에는 원시 로그·기기 식별자를 포함하지 않는다.

Unity 문서는 보상형 Ad Unit이 기본적으로 건너뛸 수 없다고 명시한다. [Google Play Families 정책](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en)의 5초 닫기 조건은 실제 소재에서 첫 네이티브 픽셀부터 X가 보이고 터치 가능한 시각을 측정해야 판단할 수 있다. 이번 SDK 추가는 기존 광고 차단을 풀 근거가 아니다. 공식 AdMob 샘플 한 회에서 X가 약 8초에 보인 [기존 기기 관측](rewarded-close-device-result.ko.md)과도 구분한다.

후속 격리 테스트는 승인된 테스트 기기·분리 앱 ID·debug 서명에서 양쪽 test mode와 Ad Inspector 단일 공급원 `Unity Ads (Bidding)`을 사용한다. 두 보상 배치에서 첫 픽셀→실제 X 5초 이내, 조기 닫기 보상 없음, 완료 보상 1회, BGM 복귀, No Ads 권한 보존, 연령·지역별 UMP 흐름을 확인해야 한다. Unity Ads 프로젝트의 보상형 스킵 설정이 입찰 소재 전체에 적용되는지도 대시보드 설정과 기기 관측으로 확인하기 전에는 보장하지 않는다.

운영 연결에는 Unity Ads의 `Google Admob` 중재·bidding 배치 및 Game/Placement ID, AdMob 중재 그룹, app-ads.txt, GDPR/미국 주별 광고 파트너, 동의 신호와 Mixed Audience 지정 검토가 별도로 필요하다. 공개 저장소에 실제 ID나 동의·기기 식별자를 적지 않는다. 2026-01-31 이후 Unity Ads waterfall 배치를 새로 만들거나 수정할 수 없으므로 입찰로만 계획한다. [Google 안내](https://developers.google.com/admob/unity/mediation/unity)

`Advertisement Legacy` 설명에는 공식 문서 사이에 표현 차이가 있다. [Unity 6.0 Editor 매뉴얼](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.ads.html)은 2026-01-31부터 해당 패키지를 통한 직접 수익화 통합이 더 이상 지원되지 않는다고 적는다. 반면 [Unity Grow 안내](https://docs.unity.com/en-us/grow/ads/unity-sdk)는 2026-04-01부터 직접 통합 앱의 광고 성과가 낮아질 *수* 있으며 Unity Ads 네트워크가 직접 통합 앱에 광고 fill을 계속 제공한다고 명시한다. 지원·권장 방식과 실제 광고 제공 여부에 관한 문구를 구분하며, 두 자료만으로 광고 송출 중단을 확정하지 않는다. 이번 변경은 Legacy 패키지를 설치하지 않고 Google AdMob 중재용 입찰 어댑터를 쓴다.
