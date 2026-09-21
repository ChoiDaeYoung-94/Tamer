# Families 광고 정책과 복구 검증 범위

## 2026-09-21 현재 상태와 해결 실행안

사용자 확정 목표는 **정상 광고와 NoAds 신규 구매·기존 권한·복원을 함께 복구**하는 것이다. 판매 차단이나 상품 비활성화를 적용하지 않는다. 이 절은 최신 읽기 조사이며 아래 9월 11일 기록을 소급 변경하지 않는다.

- 현재 Console 대상 연령: 9~12세, 13~15세, 16~17세. 교사 추천 프로그램 미포함. 기존 선언을 유지한다.
- 정책 목록: 가족 광고 형식별 요건, 거부됨 **2026년 8월 26일**. 상세 사유는 앱 사용을 방해하며 5초 후에도 닫을 수 없는 광고다. 상세 화면의 적용일 표기 오류와 달리 목록에서 연도를 확인했다. 특정 소재나 두 게임 배치 중 어느 배치인지 상세 문구만으로 확정하지 않는다.
- AdMob: 고참여 광고 ON을 스위치 값으로 읽고 변경 없이 취소했다. 미디에이션 목록에는 AdMob(기본) 1행. 개인정보 메시지 홈의 유럽/미국 규정은 모두 새 메시지 만들기 진입으로 표시됐고 게시된 메시지 근거는 확보하지 못했다. 대체 메시지 설정의 현재 값은 이번에 재확인하지 않았다.
- 설정 저장·운영 광고 요청·기기 테스트·외부 문의 발송은 하지 않았다.

### 두 광고 배치에 대한 형식 판단

`BuffingMan.RequestAdReward`의 10분 버프와 `PopupManager.Heal`의 HP 회복은 모두 `GoogleAdMobManager.ShowRewardedAd`의 native 전면 rewarded 표시를 사용한다. [Families 원문](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en)의 Ads format requirements는 아동과 연령 미상 이용자에게 게임을 방해하는 rewarded/opt-in 광고도 5초 후 닫을 수 있어야 한다고 명시한다. 게임을 방해하지 않는 통합 영상 등의 예외를 전면 표시인 두 배치에 적용할 근거는 없다. 사용자의 자발적 클릭은 면제 사유가 아니다.

보상 취득 시간, 닫기 UI 최초 표시, 실제 터치 가능 시각, 앱 복귀 시각은 다른 값이다. 보상 미취득 조기 종료는 기존처럼 무보상으로 처리하고 NoAds 구매자의 버프·회복은 유지한다.

[고참여 설정 표](https://support.google.com/admob/answer/15525707?hl=en)는 rewarded의 보상 skip 상한을 OFF30초/ON60초, 종료 단계를 OFF1/ON3으로 안내한다. OFF는 종료 복잡도를 줄이는 검토안이지만 **5초 native 닫기를 보장하는 설정이라는 근거가 아니다**. 표의 interstitial 5초 값을 rewarded에 옮기거나, 전면 광고로 형식을 바꿔 보상을 주는 우회도 해결안으로 채택하지 않는다. 앱 타이머의 `Destroy()` 역시 native 광고 종료 보증이 아니다.

실행 순서:

1. 현재 pin의 인증 SDK 범위 및 Child/Teen API 계약, 아동·미상 G 등급과 공급원 전달을 확인한다. 정확 SDK 호출은 초기화 전에 적용한다. [AdMob Families 안내](https://support.google.com/admob/answer/6223431?hl=en)
2. 기존 연령을 유지한 중립 연령 화면과 지역별 동의안을 검토한다. [구체 문구·구간·저장·철회안](families-consent-gate.ko.md#2026-09-21-운영-연결-전-검토안)을 따른다. 전체 아동 태그만으로 혼합 연령 확인 요구 완료를 주장하지 않는다.
3. 아래 문의 초안으로 공급자의 rewarded 조기 종료 지원·설정·소재 제외 방법을 확인할 준비를 한다. 답변 전에는 고참여 OFF나 SDK 업데이트만으로 해결 완료라고 표시하지 않는다.
4. 배정받은 기기에서 버프/회복 각각 첫 native 표시부터 5초의 X 표시·실제 터치·복귀·취소 무보상을 분리 측정한다. 공식 sample로 입력/콜백 계측부터 검증하되 운영 소재 준수 증거로 대체하지 않는다. 운영 요청은 별도 승인된 테스트 기기/광고 설정으로 제한하고 자동 반복 요청하지 않는다.

### 공식 지원 문의 초안 — 미발송

수신 후보는 AdMob 지원의 광고 게재/소재 담당이다. Play 정책 지원에는 거절이 연결된 배치/소재 근거를 요청하는 별도 질문을 보낸다. 사용자 승인 전에 어느 채널에도 제출하지 않는다.

2026-09-21 제출 경로를 구체화했다. 1차 수신 서비스는 [AdMob 공식 지원](https://support.google.com/admob/gethelp)의 광고 게재/소재 조사 담당이며 제목은 **Families 대상 rewarded 광고의 보상 전 5초 native 종료 지원 조건 문의**다. 2차는 [Google Play 개발자 지원](https://support.google.com/googleplay/android-developer/gethelp)의 정책 담당이며 제목은 **2026-08-26 Families 광고 형식 거절의 검토 배치·소재 근거 요청**이다. 공개 지원 URL은 로그인/Console 지원으로 연결된다. 특정 담당자·이메일·사건 번호·로그인 후 선택 항목은 확인되지 않았으므로 만들지 않았다. 두 문의는 별도 건이며 아래 본문만 준비했고 제출하지 않았다.

> Monster Tamer의 대상 연령은 9~12, 13~15, 16~17세입니다. Google Play에서 2026-08-26 Families 광고 형식 사유로 업데이트가 거부됐으며, 상세 사유는 앱 사용을 방해하는 광고가 5초 뒤에도 닫히지 않는다는 것입니다. 게임에는 사용자 선택형 전면 보상 광고 두 배치(10분 버프, HP 회복)가 있습니다. 새 복구 구현의 고정 SDK는 Unity plugin11.5.0/Android GMA25.4.0이며, 현재 AdMob 고참여 설정은 ON입니다. 이 SDK 좌표를 거절된 번들의 실제 좌표로 주장하지는 않습니다.
>
> 아동/연령 미상 요청에서 보상 획득과 별개로 5초 이내 native 닫기가 제공되도록 지원되는 rewarded 설정·요청 태그·소재 제한이 무엇인지 확인 부탁드립니다. 고참여 OFF 표의 rewarded30초는 보상 skip 설명이므로 5초 종료 보장으로 해석하지 않고 있습니다. 아동 처리와 G 등급으로도 종료 불가 소재가 공급되면 어떤 ResponseInfo/소재 식별 근거를 제공해야 조사·제외할 수 있나요? 두 배치 모두에 적용되는 공식 지원 조건과 알려진 제한도 알려주세요.

Play 정책 지원 추가 질문 초안: **거절된 versionCode26의 검토에서 사용된 광고 배치, 화면/영상, 최초 표시 및 종료 시도 근거를 제공할 수 있는지 확인 부탁드립니다. 자발적 rewarded도 적용 대상임을 이해하며, 광고 공급자와 원인을 특정하려고 합니다.** 이의신청이나 준수 완료 주장은 포함하지 않는다.

문의에 붙일 자료 수집안: 승인된 테스트의 앱/번들 버전·SDK 좌표·광고 형식/배치·요청 보호 설정·OS·검증 지역 조건·UTC 시각·응답/소재 ID·원본 영상 PTS와 X 실제 입력·callback 결과를 비공개 보관한다. 기기 광고 ID, 계정, 영수증, PlayFab 식별자, 원시 전체 로그는 첨부하지 않는다. 지원팀이 요구한 최소 응답 식별자와 가린 영상만 구체 발송안에 열거한 뒤 승인을 받는다. 현재 거절문만으로 소재 ID를 추정하지 않는다.

초기 문의의 첨부는 **없음**으로 준비한다. 본문에는 위 공개 가능한 제품/배치/SDK 좌표/거절 일자와 질문만 포함한다. 운영 App ID·광고 단위 ID·계정 식별자·소재 ID는 현재 초안에 넣지 않는다. 지원팀의 구체 조사 요구가 생기면 별도 비공개 첨부 목록을 검토한다. 추가 확인 질문은 (1) Child/Teen 처리별 종료 조건 차이, (2) 5초에 X 표시뿐 아니라 입력 가능해야 하는지와 확인창 단계, (3) G 등급·고참여 OFF로도 보장되지 않는 경우 지원되는 소재 제외 절차, (4) 기존 운영 단위를 유지하면서 적용 가능한 정확한 설정 경로다. 공식 sample 측정은 운영 소재 적합성 증거가 아니라는 점을 함께 명시한다.

코드와 설정을 바꾸지 않은 읽기/문서 작업이므로 테스트를 반복하지 않았다. 향후 검증은 변경 관련 최소 범위로 제한하고 동일 테스트 두 번 실패 시 해당 경로를 중단·보고한다.

공식 출처 확인일: 2026-09-11. #91 광고 형식 위반 수정에 사용하는 근거이며,
Play Console 선언 변경, 운영 광고 요청 또는 스토어 승인 완료를 의미하지 않는다.
통합 이후 Console 재확인·SDK 시작 경로·격리 샘플 APK 결과는
[스토어 준비 검증](families-store-readiness.ko.md)에 기록한다.

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
| Google Mobile Ads Unity | 11.5.0, main `4e8c039` 기준 | [공식 11.5.0 릴리스](https://github.com/googleads/googleads-mobile-unity/releases/tag/v11.5.0) |
| Android GMA | `com.google.android.gms:play-services-ads:25.4.0` | 현재 dependency XML 선언 |
| Android UMP | `com.google.android.ump:user-messaging-platform:4.0.0` | 현재 dependency XML 선언; 앱의 UMP 동의 UI 호출은 없음 |
| iOS GMA | `Google-Mobile-Ads-SDK ~> 13.9` | iOS 빌드의 최종 해석 버전은 미검증 |
| Families 자기 인증 | AdMob Android 19.0.0 이상 | 현재 25.4.0은 [공식 목록](https://support.google.com/googleplay/android-developer/answer/12955712?hl=en)의 범위에 포함 |
| Android GMA 지원 상태 | 25.x Supported | [공식 일정](https://developers.google.com/admob/android/deprecation); 인증 범위와 실제 광고 형식 준수는 별도 |

1차 작업 시작 시 9.1.1/23.2.0/UMP 2.2.0이었으며 SDK 담당 변경이 main에 통합되었다.
현재 표는 통합 후 선언이다. 최종 산출물의 의존성은 빌드 시 별도로 확인한다.
광고 흐름 변경에서 vendor, Packages, Gradle을 수정하지 않는다.

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
이 런타임 변경만으로 중단했다고 주장하지 않는다. SDK/manifest 담당 변경과 최종 병합 APK의
native 시작 동작을 별도로 검증한다. 통합된 SDK 11.5.0에서는 구형 측정 지연 옵션이 제거되어
과거 플래그를 다시 추가하지 않는다.

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

2026-09-11 기존 로그인 세션의 Play Console을 읽기 전용으로 확인했다.
설정 변경, 저장, 심사 제출은 수행하지 않았다. 공개 요약에는 계정 식별자·원시 화면/응답을 포함하지 않는다.

| Console 항목 | 확인된 기존 값 |
| --- | --- |
| 위반 연결 App Bundle | `26 (1.0.5)`, 프로덕션, target SDK 36, 첫 게시일 2026-08-13 |
| 정책 상태 | 앱 사용을 방해하며 5초 후에도 닫을 수 없는 광고; 이전 버전 사용 가능 |
| 연결된 Google Play 통지 | 앱 상태 거부됨, versionCode 26, 변경사항 미게시 및 이전 버전 제공 안내 |
| 적용일 표시 | 화면에 `오류, 8월 26일`로 표시되어 연도는 확정하지 않음 |
| 대상 연령 | 9~12세, 13~15세, 16~17세 |
| 광고 선언 | 광고 포함 |
| Data safety 요약 | 데이터를 수집 또는 공유하지 않음, 데이터가 암호화되지 않음, Families 준수 약속 |
| 개인정보처리방침 URL | `https://github.com/ChoiDaeYoung-94/Tamer` (저장소 루트) |
| 위 네 콘텐츠 선언의 목록상 최종 수정 | 2024-09-30 |

조회 위치는 Monster Tamer의 정책 상태 > 해당 위반 > App Bundle 보기/연결 통지,
앱 콘텐츠 > 조치됨 > 각 선언 세부정보다. 26을 현재 모든 이용자에게 제공되는 버전으로
단정하지 않으며 실제 제공 트랙/버전 목록은 별도 확인해야 한다.

대상 연령에 아동이 포함되어 있음을 확인했으며, 이를 정책 회피 목적으로 변경하지 않는다.
실제 이용자의 나이를 분류한 것은 아니다. 광고 재활성화 전 중립적인 연령 확인,
연령 미확인 사용자 처리와 지역별 consent 적용 범위를 검토해야 한다.

Data safety는 Console에 적힌 **선언 값**이며 실제 데이터 처리의 검증 결과가 아니다.
현재 및 업데이트 후 SDK의 식별자/진단정보·PlayFab 계정/진행도·구매 검증 데이터,
전송 암호화, 삭제/보존, 제3자 공유와 저장소 루트의 개인정보 안내를 대조해야 한다.
이 대조는 SDK·데이터 PR 통합 후 기기에서 요청 경로를 검증하고, 다음 제출 전에 다시 수행한다.
후속 추적은 [#105](https://github.com/ChoiDaeYoung-94/Tamer/issues/105)에서 진행한다.

현재 Google 세션에서 AdMob은 기존 관리 화면 대신 신규 가입·약관 수락 화면으로 연결됐다.
가입/약관 수락은 진행하지 않았다. 기존 AdMob 소유 계정, mediation/custom event,
실제 광고 공급자·소재 및 앱/광고 단위 설정은 여전히 미확인이다.

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

- 확인한 Console 선언과 실제 앱/SDK 동작 대조 및 위반을 일으킨 광고 배치·소재 확인.
- AdMob 앱 설정, 광고 단위, mediation/custom event와 공급자별 인증 버전 확인.
- 테스트 기기에서 광고 시작 5초 후 종료 버튼의 가시성·터치 가능성 확인.
  Android 시스템 바, 화면 크기·방향 등으로 버튼이 가려지지 않는지도 포함한다.
- 테스트 기기에서 보상 획득, 보상 전 닫기, 로드·표시 실패, 화면 전환, 음악 복구 확인.
- 운영 재활성화 변경의 코드 리뷰와 스토어 심사 결과는 구현·빌드 성공과 구분해 기록.

이 작업의 자동 테스트에서는 운영 광고를 요청하지 않는다. 원시 광고 응답, 원시 Editor 로그,
서비스 설정 및 사용자 식별정보를 공개 이슈·문서·커밋에 첨부하지 않는다.
