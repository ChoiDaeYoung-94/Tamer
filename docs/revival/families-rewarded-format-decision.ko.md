# Families 5초 닫기와 보상형 광고 형식 결정 (2026-09-23)

검토 기준 checkout은 `C:/Users/pc_17/.codex/worktrees/rewarded-close-review/Tamer`의
`origin/main` `f8ea309`(#229와 #230 포함)이다. 기존 Play Console 대상 연령
기록은 9~12, 13~15, 16~17세이며 이 작업에서 선언을 변경하거나 Console을
새로 조회하지 않았다. 최종 목표인 정상 광고와 No Ads 신규 구매·기존 권한·복원은
유지한다. 광고를 보지 않은 사용자에게 버프나 회복을 무료로 주는 변경은 하지 않는다.

## 확인한 경계

[Google Play Families 정책](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en)은
아동 또는 연령 미상 사용자에게 게임을 방해하는 보상형·동의형 광고도 5초 뒤에는
닫을 수 있어야 한다고 명시한다. 현재 버프·회복 배치는 게임을 가리는 네이티브
전면 `RewardedAd.Show()`다. 공용 테스트폰의 공식 샘플 한 회에서는 첫 광고 픽셀부터
실제 X까지 약 8초가 걸렸다. 샘플 결과를 운영 소재의 보편적 동작으로 추정하지
않지만, 현재 형식의 5초 보장을 주장할 수 없다.

[AdMob 보상형 단위 설명](https://support.google.com/admob/answer/7311747?hl=en)은
보상 전 건너뛰기 시간을 최대 30초, 고참여 설정 사용 시 최대 60초로 설명한다.
[고참여 설정 표](https://support.google.com/admob/answer/15525707?hl=en)은
설정을 꺼도 보상형 30초와 최대 한 단계의 종료 절차를 제시한다. 앱에서 이 설정을
끄는 것만으로 실제 X가 5초 안에 나타난다는 보장은 없다. 앱 타이머나
`RewardedAd.Destroy()`로 네이티브 광고 X를 만들어 준다는 공식 API 근거도 없다.

형식을 바꾸는 경우의 현재 판단은 다음과 같다.

| 형식 | 실제 가능 범위와 남은 문제 |
|---|---|
| 기존 rewarded / rewarded interstitial | 보상을 위한 형식이나 전면 표시이며, 현재 SDK·설정에서 아동/미상 이용자의 5초 X를 보장할 근거가 없다. 고참여 OFF도 해결책이 아니다. |
| 일반 interstitial / app open | [고참여 설정 표](https://support.google.com/admob/answer/15525707?hl=en)에 더 짧은 건너뛰기 시간이 있으나 보상형 인벤토리가 아니며 기존 버프·회복 보상 계약을 그대로 옮길 수 없다. 실제 X와 추가 종료 단계도 확인해야 한다. |
| 배너 / native overlay | [AdMob 형식 설명](https://support.google.com/admob/answer/6128738?hl=en)의 일반 인라인 광고 후보로, 게임을 가리지 않는 배치에서 별도 검토할 수 있다. 기존 보상형 버튼의 시청 대가로 전환하지 않는다. [AdMob 비보상 인벤토리 정책](https://support.google.com/admob/answer/48182?hl=en)은 비보상 광고 시청을 대가로 보상을 약속하는 행위를 금지한다. |
| Unity Ads의 건너뛸 수 있는 rewarded | [Unity 프로젝트 설정 안내](https://docs.unity.com/en-us/grow/dashboard/get-started/project/settings)는 아동 친화 프로그램의 보상형 광고가 5초 후 닫혀야 하며 rewarded 단위는 기본적으로 건너뛸 수 없다고 명시한다. [FAQ](https://docs.unity.com/en-us/grow/ads/references/faq)와 [단위 관리 안내](https://docs.unity.com/en-us/grow/dashboard/ad-units/manage)는 Dashboard에서 건너뛰기와 종료 화면 설정을 변경할 수 있다고 설명한다. 따라서 **격리 실험할 만한 대안**이지만, 현재 프로젝트에 SDK/광고 단위가 없고 실제 모든 소재의 X·추가 종료 단계를 검증하지 않아 운영 가능 판정은 아니다. |

일반 광고를 새로 배치하려면 실제 광고 단위·소재·UI 위치와 현재 고정 SDK에서의
지원 여부, 아동 요청 태그·G 등급·인증 공급원, 연령 미상 처리와 UMP/지역별 동의,
No Ads 권한을 별도 설계·검증해야 한다. [AdMob Families 안내](https://support.google.com/admob/answer/6223431?hl=en)는
아동/미상 요청의 child-directed 처리와 최대 G 등급, 미디에이션 공급원 책임을
설명한다. 이 검토 없이 일반 광고 단위를 새로 연결하지 않는다.

Unity Ads 대안을 검증할 경우에도 현재 Unity/Android 도구 버전을 임의 변경하지
않는다. [Play Families 인증 SDK 목록](https://support.google.com/googleplay/android-developer/answer/12955712?hl=en)에서
선택한 **실제** Unity Ads Android 버전의 인증 여부를 확인하고, 격리 빌드에서
Dashboard의 rewarded 건너뛰기를 5초 이하로 설정해야 한다. 해당 계정의
프로젝트/광고 단위·설정 권한, 연령 처리·동의 방식, No Ads 연계, 두 보상 배치의
완료/건너뛰기 콜백을 확인한다. [Unity 보상형 구현 안내](https://docs.unity.com/en-us/grow/ads/android-sdk/rewarded-ads)는
`COMPLETED`에만 보상을 주고 `SKIPPED`에는 주지 않는 경로를 보여 준다. 영상
첫 픽셀→X 표시·실제 터치 가능 시각과 종료 카드까지 공용 기기에서 측정한 뒤에만
운영 연결을 논의한다. Unity Ads Dashboard 계정이나 임의 Game ID를 만들지 않았다.

구현 경로는 아직 하나로 확정할 수 없다. [Unity 6.3 패키지 설명](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-ads)은
2026-01-31부터 `com.unity.ads` 직접 통합의 수익화 지원이 종료됐다고 명시한다.
반면 [Unity Grow 통합 안내](https://docs.unity.com/grow/ads/unity-sdk)는
직접 통합 앱에도 광고 fill은 계속되지만 2026-04-01부터 광고 성과가 낮아질 수
있다고 설명한다. 두 문구는 지원 범위와 실제 게재를 구분하며, 현재 사용하는
Unity `6000.0.81f1`에서 Legacy 직접 연결을 지속 가능한 기본 해법으로
가정하지 않는다. 다른 후보인
[AdMob의 Unity Ads mediation 안내](https://developers.google.com/admob/unity/mediation/unity)는
rewarded bidding과 adapter를 지원하지만 waterfall 신규/수정은 종료됐고,
현재 고정 Google Mobile Ads plugin과 adapter/Unity Ads SDK의 실제 호환성
확인이 필요하다. Unity의 권장 경로인 LevelPlay bidder 역시 별도 SDK·계정·
연령/동의 계약을 요구한다. **mediation에 Unity Ads를
추가하는 것만으로 Google 등 다른 공급원의 5초 미보장 광고가 제외되지는 않는다.**
어느 경로든 Unity Ads 단위의 skip/종료 설정과 공급원 제한을 확인하고, 테스트
모드에서 출처별 화면을 측정해야 한다. 현재 저장소 `Packages/manifest.json`과
lockfile에는 Unity Ads SDK/adapter가 없으므로 버전이나 Game ID를 임의로
추정해 설치하지 않았다. 현재 AdMob만 연동된 상태의 No Ads 경로와
보상 완료/건너뛰기 구분을 어댑터 설계에서 그대로 보존해야 한다.

## 이번 변경의 검증

기준 `origin/main`은 `f8ea3093d997bf2389d4cac29fab428bcb58f589`이다.
테스트 후 PR 브랜치를 최신 `origin/main` `ac13de1861ebad575f4c799c2272f34ee4c83613`로
재기반화했다. 사이에 병합된 #222는 광고 코드·테스트 파일을 변경하지 않았고
`PopupManager`에서는 설정 팝업 닫힘 처리만 추가했다. 재기반화 뒤의 Unity
테스트를 새로 실행한 결과로 표현하지 않는다.
Unity `6000.0.81f1`, Unity CLI `1.0.0-beta.8`, 기존 Android 도구와
`Packages/packages-lock.json`을 변경하지 않았다. 관련 EditMode 검사인
`RevivalAgeChoiceTests` 29/29(결과 XML SHA-256
`8634cf48622e535fcec83c15cb19d8a229c23774f9a70c78b1d8370ce8eb3871`)과
`RevivalAdManagerTests` 51/51(결과 XML SHA-256
`7dc4f1f7aec592d0e47a20570c59a0ec2f980e042d33b6a829e8c87e8d7f617d`)
통과, 실패·건너뜀 0을 확인했다. 이번 방어선은 SDK/설정/에셋을 추가하지 않아
APK나 실기기 광고를 다시 빌드·표시하지 않았다. 실제 공급자 전환 빌드·기기·
운영 소재·5초 X/스토어 판정은 미검증으로 남는다.

## 이번 코드 방어선

`AgeTreatmentPolicy.AllowsFullscreenRewarded`는 연령만 보는 **후보 필터**다.
9~12·13~15·16~17, 미선택·거부를 모두 차단한다. 성인 자기신고도 현재 Play 대상
연령 선언 밖에 있으므로 이 함수만으로 운영 허용을 뜻하지 않는다. 실제
`GoogleAdMobManager.CanRequestAds`는 이 필터와 별도로 지역 검토,
플랫폼·테스트 빌드 조건을 모두 요구하고, UMP 동의 gate는 그 다음 단계에 있다.
지역 검토 `RegionalConsentReviewed=false`, 릴리스에서 거부되는 테스트 전용 요청
정책 및 `AdRequestPolicy.ProductionAdsEnabled=false`를 유지한다. 기존 격리 샘플
계측 패키지·씬에서만 테스트 예외를 유지한다. No Ads 보유자 경로는 요청 gate보다
먼저 처리되며 구매 SKU·권한·복원 코드는 변경하지 않는다.

이 방어선은 아직 운영 광고를 복구하지 않는다. 실제 정상 광고를 연결하려면
9~17세/미상에게 제공할 **비방해형 일반 광고 배치**와 기존 보상형 버튼의
광고 기반 보상을 어떻게 유지할지에 대한 별도 제품·정책 설계가 필요하다.
특히 현재 공식 AdMob 보상형 네이티브 X가 5초 내 나타난다는 근거가 없으므로
아동 대상 전면 보상형을 켜서는 안 된다. 운영 소재·기기별 첫 픽셀→실제 X,
조기 닫기 무보상·BGM 복구, 연령/UMP/No Ads 흐름, 최종 SDK·AAB·스토어 확인은
모두 후속 검증이다. 공급자 지원 문의는 보내지 않았다.
