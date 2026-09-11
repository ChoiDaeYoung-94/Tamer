# 광고 복구 구현과 검증 (#91)

작업 브랜치: `codex/revival-families-ads-91`. 기준은 #95의 `c1613ab`에서 시작해
안전성 보완을 포함한 main `c845364`를 병합했다. 정책 근거는
[Families 정책 조사](families-ads-policy.ko.md)에 기록한다.

## 변경된 게임 동작

| 조건 | 처리 |
| --- | --- |
| 일반 Android release, No Ads 미보유 | 광고 SDK API 초기화·로드·표시를 호출하지 않으며 광고 버프/회복 중단 안내 |
| 기존 No Ads 보유 | 기존 `GooglePlay` 쉼표 저장값의 `ProductNoAds` 권한을 유지하고 즉시 버프/회복; 광고 요청 없음 |
| Editor/Android 개발 또는 `TAMER_TEST_ADS` 빌드 | 명시적 상호작용으로만 Google 샘플 광고 로드; 첫 미준비 요청은 재시도 안내 |
| batch, 지원하지 않는 플레이어 | 테스트 플래그와 관계없이 광고 요청 차단 |
| 보상 후 닫힘 | 음악 복구와 광고 객체 정리 후 원래 요청자에게 한 번 지급 |
| 보상 이벤트 없이 닫기 | 음악 복구, 재시도/게임 계속 가능; 지연된 실제 보상 이벤트는 원래 요청자가 유효하면 한 번 지급 |
| 표시 실패 | 보상 요청 무효화, 음악 복구, 재시도/게임 계속 가능 |
| 화면 전환 또는 요청자 파괴 | 기존 요청 무효화; 새 씬의 다른 대상에게 지급하지 않음 |
| 중복 클릭/중복 콜백/오래된 콜백 | 진행 중 두 번째 요청 거절; 중복 보상 없음, 이전 요청이 새 요청의 전면 상태나 음악을 정리하지 않음 |
| 초기화·로드 30초 무응답 | 요청 세대 무효화 후 수동 재시도 허용; 오래된 로드 결과 정리 |
| 앱 백그라운드/복귀 | 일시정지/포커스 자체를 보상·닫힘으로 추정하지 않음; 실제 SDK 이벤트를 Unity Update에서 처리 |

버프는 요청 당시 `BuffingMan`, 회복은 요청 당시 `Player`와 씬 handle에 귀속한다.
전역 `IsReceived`와 현재 씬 이름을 통한 보상 전달은 제거했다. 저장 키/포맷, 기존 계정,
No Ads 권한, 에셋 GUID, 앱 서명/버전은 변경하지 않는다.

광고가 멈춘 BGM은 `AudioSource.UnPause()`로 재생 위치를 유지하며 한 번만 복구한다.
광고 중 씬 전환 또는 다른 BGM 재생/일시정지가 발생했다면 이전 광고가 새 음악을 다시 시작하지 않는다.
광고가 닫히지 않은 상태를 5초 타이머로 닫힘 처리하거나 보상을 조작하지 않는다.

## SDK 콜백 순서

Android의 Google 직접 공급 광고는 native 단계에서 보상을 닫힘 전에 알린다.
[공식 Android 문서](https://developers.google.com/admob/android/rewarded).
그러나 Unity SDK 11.5.0 Android bridge는 두 이벤트를 서로 다른 스레드에서 C#으로 전달하므로
관리자 큐에 도착하는 순서까지 보장되지 않는다.
[버전 고정 Android bridge](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/source/plugin/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/src/main/java/com/google/unity/ads/UnityRewardedAd.java).

이 저장소의 9.1.1 Editor placeholder는 닫힘 후 같은 호출 묶음에서 보상을 알린다.
[버전 고정 소스](https://raw.githubusercontent.com/googleads/googleads-mobile-unity/v9.1.1/source/plugin/Assets/GoogleMobileAds/Platforms/Unity/RewardingAdBaseClient.cs)
및 설치 DLL의 IL을 대조했다. 11.5.0 Editor는 countdown 완료에서 보상을 알리고 닫기 버튼은
닫힘만 알린다.
[11.5.0 Editor 소스](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/source/plugin/Assets/GoogleMobileAds/Platforms/Unity/RewardingAdBaseClient.cs).

관리자는 한 Update에서 콜백 큐를 비운 뒤 전면 상태·음악을 정리한다. 닫힘 완료 통지는 한 번이며,
그 뒤에 도착한 실제 earned 이벤트도 같은 요청의 보상 함수만 한 번 실행할 수 있다.
다음 광고의 상태나 음악은 건드리지 않는다. 실패, 요청자 파괴, 관리자 파괴 또는 씬 전환은
보상을 무효화한다. 씬 전환 세대를 비교하므로 원래 씬으로 돌아와도 이전 보상이 살아나지 않는다.
닫힌 요청을 보관하는 별도 대기 목록이나 보상 타이머는 없다.

자동 검사는 같은 프레임의 두 순서, 다른 프레임의 보상, 실패 경합, 새 광고와 이전 보상의 격리를 다룬다.
실제 공급자가 콜백 자체를 전달하지 않는 경우까지 복구하지는 않는다. 운영 재활성화 전에 공급자별
기기 동작을 검증해야 한다. 현재 운영 요청과 iOS 기기 요청은 차단되어 있다.

## 재현 및 증거

권한 있는 원본에서 `python tools/revival/restore_assets.py --source <원본 경로>`로 복원한다.
다른 작업의 Library를 복사하지 않는다. CLI는 `python tools/revival/install_cli.py`로 버전 고정 설치한다.
자신의 checkout Editor가 닫힌 상태에서 실행한다.

```powershell
python -m unittest discover -s tools/revival -p 'test_*.py'
./tools/revival/Run-Baseline.ps1 -ProjectPath <이 작업 checkout 절대 경로> -TestsOnly
python tools/revival/audit_guids.py
```

2026-09-11 현재 독립 검증: 순수 C# NUnit 58/58, 복원/빌드 사전검사 Python 4/4 통과.
Manager 및 통합 테스트는 설치 Unity 6000.0.81f1/GMA 9.1.1/Unity bundled NUnit DLL을 참조한
별도 컴파일을 통과했다. 첫 Editor 실행은 NUnit 속성 호환 오류로 테스트 판정 전에 종료했으며,
지원하지 않는 `NonParallelizable` 속성을 제거했다.
게임 의존성 stub을 사용한 컴파일은 전체 Unity 컴파일·실행을 대신하지 않는다.
Unity EditMode는 공유 실행 순서에 따라 수행 후 이 문서와 PR에 결과를 갱신한다.
구 SDK 단독 APK 빌드는 생략하고 통합 담당이 SDK·광고·데이터 변경을 합친 최종 APK를 검증한다.

## 통합 및 미검증

- SDK/vendor/Packages/Gradle은 의존성 담당 PR 소유. 해당 PR의 SDK11.5.0 표준 Android 선택과
  이 런타임 코드를 병합한 최종 APK는 통합 담당이 별도로 검증한다.
- 단위·Editor 검사는 계정 로그인·저장 동기화·구매·운영 광고를 실행하지 않는다.
- 실제 광고의 5초 닫기, 네트워크 단절, 백그라운드 복귀, 청각적인 음악 복구,
  실제 No Ads 구매 복원과 각 게임 화면의 기기 검증은 남아 있다.
- Console 읽기 전용 확인: 위반은 검토·거절 통지의 프로덕션26(1.0.5)/SDK36/2026-08-13 첫 게시 번들에 연결되며
  기존 타깃은9~12/13~15/16~17세다. 위반은 여전히 표시된다. 광고포함/Data safety/개인정보 URL
  선언 값은 정책 문서에 기록했다. 실제 동의/광고 공급자·소재와 선언의 사실 적합성,
  전체 트랙의 실제 제공 버전·비준수 번들 잔류 여부 및 스토어 해소 승인은 미검증이다.
- SDK 자동 native 시작 동작의 차단은 앱 API 호출 차단과 구분한다.
  SDK11.5.0에는 구형 measurement 지연 옵션이 제거되어 과거 플래그를 새로 추가하지 않는다.
- #91은 코드 병합으로 종료하지 않는다. 비준수 광고 중단은 의도된 기능 영향이며,
  재활성화는 실제 공급원·연령/consent·형식·기기 증거를 갖춘 후 별도 PR로 진행한다.
- CI/CD, 스토어 제출, 운영 광고 요청은 이 작업에서 수행하지 않는다.
