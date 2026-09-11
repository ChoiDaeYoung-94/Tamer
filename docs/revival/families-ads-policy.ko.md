# Families 광고 정책과 복구 검증 범위

공식 출처 확인일: 2026-09-11. #91 광고 형식 위반 수정에 사용하는 근거이며,
Play Console 선언 변경, 운영 광고 요청 또는 스토어 승인 완료를 의미하지 않는다.

## 5초 종료 요건

[Google Play Families 정책](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en)은
정상적인 앱 사용이나 게임 진행을 방해하는 광고에 대해 5초 후 닫을 수 있어야 한다고 명시한다.
보상형 광고와 사용자가 시청을 선택한 광고도 포함한다. 아동 전용 앱에서는 전체 사용자에게,
아동과 성인이 함께 대상인 앱에서는 아동 및 연령 미확인 사용자에게 해당 광고 형식 요건이 적용된다.

[일반 광고 정책](https://support.google.com/googleplay/android-developer/answer/9857753?hl=en-GB)의
15초 전면 광고 제한에는 명시적으로 선택한 보상형 광고 예외가 있지만,
그 예외를 Families의 5초 요건에 적용할 수 없다.

닫기 버튼의 위치와 가시성, 5초 후 터치 가능 여부, 종료까지 필요한 단계를 실제 기기에서 확인해야 한다.
앱이 보상을 정상 처리하거나 SDK가 자기 인증 목록에 있다는 사실만으로 이 검증이 대체되지 않는다.

## SDK 및 광고 공급자 확인

| 항목 | 저장소에서 확인한 선언 | 공식 근거와 판정 |
| --- | --- | --- |
| Google Mobile Ads Unity | 9.1.1 manifest | [공식 9.1.1 릴리스](https://github.com/googleads/googleads-mobile-unity/releases/tag/v9.1.1) |
| Android GMA | `com.google.android.gms:play-services-ads:23.2.0` | XML, Gradle, resolver 선언과 공식 릴리스가 일치 |
| Android UMP | `com.google.android.ump:user-messaging-platform:2.2.0` | XML과 공식 릴리스가 일치 |
| iOS GMA | `Google-Mobile-Ads-SDK ~> 11.6.0` | iOS 빌드의 최종 해석 버전은 미검증 |
| Families 자기 인증 | AdMob Android 19.0.0 이상 | 현재 23.2.0은 [공식 목록](https://support.google.com/googleplay/android-developer/answer/12955712?hl=en)의 범위에 포함 |
| Android GMA 지원 상태 | 23.x Deprecated | [공식 일정](https://developers.google.com/admob/android/deprecation): 2026-02-17 Deprecated, 2027-06-30 Sunset 예정. 24.x와 25.x는 Supported |

로컬 `Assets/GoogleMobileAds/CHANGELOG.md`의 23.1.0 표기는 실제 의존성 선언 및 공식 릴리스와 다르다.
이 문서는 XML 및 Gradle에 선언된 23.2.0을 기준으로 한다. 최종 산출물의 의존성은 빌드 시 별도로 확인한다.
SDK 업데이트는 의존성 담당 변경과 통합하며 이 광고 흐름 변경에서 vendor, Packages, Gradle을 수정하지 않는다.

현재 조사한 의존성 파일에서는 별도 mediation adapter 선언을 발견하지 못했다.
AdMob Console의 mediation 그룹, custom event, 실제 응답 네트워크는 확인하지 않았으므로
광고 공급자가 모두 검증되었다고 주장하지 않는다.
[AdMob Families 안내](https://support.google.com/admob/answer/6223431?hl=en)는
mediation 및 custom event에서 사용하는 네트워크의 인증과 아동 적합성 확인도 개발자의 책임으로 설명한다.

## 코드에서 보장하는 범위

`AdRequestPolicy`는 운영 광고를 기본 차단한다. 재활성화하려면 Families 검증 근거를 갖춘 별도 코드 리뷰 PR이 필요하다.
테스트 광고 요청은 Editor, development player 또는 명시적으로 `TAMER_TEST_ADS`를 켠 빌드에서만 허용한다.
실제 관리자는 Editor와 Android로 제한한다. iOS 공식 테스트 ID도 정책 유틸리티에 보관하지만,
프로젝트의 iOS AdMob 앱 ID가 비어 있고 native 설정이 미검증이므로 iOS 광고 실행은 허용하지 않는다.
batch 실행에서는 모든 광고 요청을 막는다.
명시적 테스트 광고 옵션도 운영 광고를 허용하는 옵션이 아니다.

차단 대상은 앱 코드의 `MobileAds.Initialize`, `RewardedAd.Load`, `Show` 호출이다.
SDK가 Android manifest의 provider 등을 통해 수행하는 native 시작 동작·측정까지
이 런타임 변경만으로 중단했다고 주장하지 않는다. SDK/manifest 담당의 측정 지연 설정과 최종 병합 APK를 별도로 검증한다.

[Unity 9.1.1 공식 샘플](https://raw.githubusercontent.com/googleads/googleads-mobile-unity/v9.1.1/samples/HelloWorld/Assets/Scripts/RewardedAdController.cs)의
보상형 테스트 광고 ID만 사용한다.

| 플랫폼 | 테스트 보상형 광고 ID |
| --- | --- |
| Android | `ca-app-pub-3940256099942544/5224354917` |
| iOS | `ca-app-pub-3940256099942544/1712485313` |

요청 가능 여부와 ID 선택은 `RevivalAdRequestPolicyTests`에서 검증한다.
테스트 결과와 전체 광고 흐름의 검증 결과는 해당 PR의 실제 실행 증거에 기록한다.

[9.1.1 RewardedAd API 소스](https://raw.githubusercontent.com/googleads/googleads-mobile-unity/v9.1.1/source/plugin/Assets/GoogleMobileAds/Api/RewardedAd.cs)에는
종료 버튼 표시 시간을 지정하거나 광고를 강제로 닫는 공개 `Close()` API가 없다.
`Destroy()`는 광고 객체 정리 기능이므로 5초 타이머에서 호출하는 방식으로
실제 전면 광고가 닫히거나 종료 버튼이 보인다고 보장하지 않는다.

## 연령 및 Console 설정

실제 타깃 연령과 기존 Play Console 선언은 미확인이다. 정책 회피를 위해 이를 추정하거나 변경하지 않는다.
혼합 연령으로 확정되어 광고를 제공한다면 중립적인 연령 확인과 미확인 사용자 처리까지 함께 검토해야 한다.

현재 테스트 요청에는 9.1.1 SDK의 `TagForChildDirectedTreatment.True`,
`TagForUnderAgeOfConsent.True`, `MaxAdContentRating.G`를 초기화·로드 전에 적용한다.
이는 연령 미확인 개발 테스트에 대한 보수적 처리이며 운영 사용자 연령을 분류한 결과가 아니다.
이는 광고 요청 처리 설정이며 실제 사용자의 나이나 Console 선언을 대신하지 않는다.
운영 광고는 차단하므로 UMP 폼 표시나 운영 consent 획득을 구현 완료로 취급하지 않는다.
재활성화 시에는 실제 연령·지역별 consent 선행 조건과 mediation 전파를 별도 검증해야 한다.
[최신 targeting 안내](https://developers.google.com/admob/unity/targeting?hl=en)는
새 `AgeRestrictedTreatment` API도 설명하므로 최신 예제를 9.1.1에 그대로 복사하지 않는다.

[AdMob High-engagement 설정 안내](https://support.google.com/admob/answer/15525707?hl=en)는
설정을 꺼도 일반 보상형 광고의 reward skip time이 최대 30초일 수 있다고 설명한다.
따라서 이 설정을 끄는 것만으로 Families의 5초 종료 요건을 충족했다고 판정할 수 없다.

## 별도로 남은 검증

- Play Console의 타깃 연령, Families 대상 여부와 기존 거절 사유의 광고 배치 확인.
- AdMob 앱 설정, 광고 단위, mediation/custom event와 공급자별 인증 버전 확인.
- 테스트 기기에서 광고 시작 5초 후 종료 버튼의 가시성·터치 가능성 확인.
  Android 시스템 바, 화면 크기·방향 등으로 버튼이 가려지지 않는지도 포함한다.
- 테스트 기기에서 보상 획득, 보상 전 닫기, 로드·표시 실패, 화면 전환, 음악 복구 확인.
- 운영 재활성화 변경의 코드 리뷰와 스토어 심사 결과는 구현·빌드 성공과 구분해 기록.

이 작업의 자동 테스트에서는 운영 광고를 요청하지 않는다. 원시 광고 응답, 원시 Editor 로그,
서비스 설정 및 사용자 식별정보를 공개 이슈·문서·커밋에 첨부하지 않는다.
