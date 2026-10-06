# 실제 광고 관리자의 개인정보 옵션 재시작 경계

2026-10-06, PR #314 병합 `0ea5181730462a11a229285f59638b1dd6911385` 기준 소스 조사다.
이번 조사는 Editor·기기·SDK 실행을 추가하지 않았다. 실제 게임 UI나 앱 재시작의
네이티브 동작을 검증한 결과가 아니다.

## 확인한 경로

| 경계 | 현재 구현 | 확인 수준 |
| --- | --- | --- |
| 연령 질문 열기 | `AgeChoicePresenter.Open` → `LocalAgeChoice.BeginEdit` → `Changed` | 소스 |
| 연령 선택 저장 | 버전 있는 연령 문자열만 PlayerPrefs에 저장, 동의 정보는 저장하지 않음 | 소스 |
| 연령 변경 | `InvalidateAgeContext` → `SuspendAndRetainPrivacy`, 광고·늦은 보상 무효화 | 소스 및 기존 공유 helper 검사 |
| 기존 Required 옵션 | active 또는 retained gate의 현재 client Required를 사용 | 소스 |
| 설정 메뉴 | `AgePrivacyOptionsEntry`가 manager Required에 따라 옵션 버튼 표시 | 소스 |
| 명시적 옵션 열기 | 기존 gate `OpenPrivacyOptions`; 완료 후 광고 자동 load 없음 | 소스 |
| 관리자 종료 | `OnDestroy`에서 active·retained gate 모두 Dispose | 소스 |
| 새 관리자 | 연령은 다시 읽지만 개인정보 gate는 새로 시작; 이전 owner 복원 없음 | 소스 |

관련 소스는 `Assets/Scripts/Managers/GoogleAdMobManager.cs`,
`Assets/Scripts/Advertising/AdConsentGate.cs`, `LocalAgeChoice.cs`,
`Assets/Scripts/UI/AgeChoicePresenter.cs`, `AgePrivacyOptionsEntry.cs`,
`DeletionSettingsEntry.cs`다.

## 남아 있는 재시작 문제

성인 세션에서 Required 옵션을 발견한 뒤 연령을 미성년 또는 거절로 바꾸면,
현재 관리자 안에서는 기존 privacy-only owner가 옵션 접근을 유지한다.
그러나 관리자 종료 뒤에는 owner가 없다. 새 관리자에서 연령·환경 검토 조건이
`BeginConsent`를 차단하면 새 gate도 생성하지 않아 개인정보 옵션 버튼이 숨겨진다.
따라서 기존 warm-session 보존을 cold restart 복구 완료로 해석할 수 없다.
같은 프로세스 안의 manager 재생성과 실제 프로세스 재시작도 구분해야 한다.

현재 `ProductionAdsEnabled`, `RegionalConsentReviewed`, 각 연령 검토 값은 false다.
운영 승인 계약도 이 조사에서 변경하지 않았다. 저장된 성인 연령만으로 SDK 갱신이나
광고 초기화가 허용되지 않는다. No Ads 구매 문자열·계정 데이터 경로는 변경하지 않았다.

## SDK 요구와 구현 선택

[Google UMP Unity 안내](https://developers.google.com/admob/unity/privacy)는 매 실행에
`Update`로 최신 동의·개인정보 옵션 필요 여부를 확인하도록 안내한다. 앱에 저장한
동의 값은 오래될 수 있으며, 옵션 필요 여부도 Update 후 확인해야 한다.
`CanRequestAds`는 Update 호출 전 false다. 이는 SDK 지침이며 현재 프로젝트의
미성년 처리 검토가 완료됐다는 의미는 아니다.

과거 Required bool을 저장해 현재 Required 또는 광고 허용으로 복원해서는 안 된다.
SDK getter만 새 owner에 연결하는 방식도 새 실행의 상태 갱신을 대신하지 못한다.
또한 개인정보 복구를 위해 기존 광고 요청 gate를 우회하거나 새 미성년에게 자동
Update/Gather를 시작하는 변경은 현재 승인 범위에 포함하지 않는다.

검토할 구현안은 과거 옵션 존재를 **접근 안내용 힌트**로만 저장하고,
사용자가 명시적으로 선택할 때 별도 privacy-only 경로에서 Update 후 실제 status에
따라 옵션을 여는 것이다. 힌트는 현재 Required·동의·광고 eligibility를 뜻하지 않는다.
이 경로는 자동 Gather와 Mobile Ads init/load를 하지 않고 기존 광고 gate를 유지해야 한다.
현재 미성년·거절·연령 미선택에 사용할 TFUA 처리와 privacy-only 실행 환경 조건은
SDK·정책 검토가 필요하다. 이 문서는 그 안을 구현하거나 운영 승인하지 않는다.
힌트 영속화는 SDK 요구가 아니라 선택 가능한 접근 안내 방식이다. 별도 힌트 없이
명시적 개인정보 상태 확인 진입점을 항상 제공하는 대안도 있다. 어느 방식이든
진입점 표시를 현재 SDK Required 또는 표시 가능한 native form의 증거로 취급하지 않는다.

## SDK 조사로 좁힌 조건

[Unity GDPR 안내](https://developers.google.com/admob/unity/privacy/gdpr)는 TFUA=true
갱신을 지원하지만, 과거 성인 동의의 철회 양식을 현재 미성년에게 다시 제공하거나
기존 동의를 삭제한다는 보장은 확인하지 못했다. Unknown/Declined는 앱 선택 상태이며
공식 SDK 연령 매핑이 아니다. 기존 성인 태그를 재사용하거나 임의로 true에 매핑할
근거로 사용할 수 없다. 현재 16–17의 TFUA=false도 모든 미성년=true로 바꾸지 않는다.

[Android ConsentInformation 참조](https://developers.google.com/admob/android/reference/privacy/kotlin/com/google/android/ump/ConsentInformation)에
따르면 Update 시작 직후 과거 실행의 status가 나타날 수 있다. 이번 명시 요청의
성공 callback 이후에만 최신 status를 읽는다. 실패 후 읽힌 Required를 근거로
양식을 열지 않는다. 성공+Required만 Show 후보이며 NotRequired는 현재 SDK 양식이
요구되지 않는다는 뜻이지 과거 동의 철회 완료가 아니다. Unknown/실패는 확인 실패다.

[Android UserMessagingPlatform 참조](https://developers.google.com/admob/android/reference/privacy/kotlin/com/google/android/ump/UserMessagingPlatform)는
개인정보 양식 자동 preload와 미준비·표시 실패 오류를 구분한다. Required여도 실제
표시 성공을 보장하지 않는다. 개인정보 경로에서 Gather를 preload 용도로 호출하지
않으며 같은 클릭의 자동 재시도 없이 이후 명시 요청으로만 재시도한다.

공식 [v11.5.0 Android bridge](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/source/plugin/Assets/GoogleMobileAds/Ump/Platforms/Android/ConsentFormClient.cs)는
UI runnable 예외를 로그로 남기고 dismissed callback을 호출하지 않는 경로가 있다.
callback 부재를 host timeout으로 native form 종료라고 처리하거나 다른 owner의
양식을 겹쳐서는 안 된다. 새 gate 생성도 native consent reset을 뜻하지 않는다.

현재 별도 개인정보 SDK 환경·연령 검토 조건은 정의되지 않았다. 광고 unit이나
광고용 승인 계약을 개인정보 호출의 승인 근거로 사용해서는 안 된다. 미검토 상태의
plumbing과 실제 운영 refresh 활성화 완료를 구분해야 한다. 명시 요청만 허용하는
안은 기본 매 실행 Update 지침을 전체 구현했다고 주장하지 않는다.

## 후속 검증 범위

처리 조건이 결정되면 실제 manager에 합성 consent client를 연결해 연령 변경,
저장 실패, manager 종료·재생성, 명시 refresh 실패·성공, Required/NotRequired,
중복 선택과 늦은 callback을 최소한으로 확인해야 한다. 앱 힌트가 광고 허용을
복구하지 않는지도 확인해야 한다. 기존 No Ads 권한 경로는 보존한다.

실제 게임 UI와 프로세스 재시작 검증은 별도 목적·기기 보존 계획이 필요하다.
PR #314의 기존 기기 등록·부트·EEA 폼 시험을 반복하거나 재시작 증거로 사용하지 않는다.
이번 조사로 runtimePrivacyVerified·binaryVerified·distributable을 true로 올리지 않는다.

## 후속 구현: 명시적 확인 진입점과 합성 검증

위 내용은 PR #315의 조사 기록이다. 이후 `645ac25bdc0cc367c31bcf36ec6683bf51175b99`
기준으로 기존 설정 메뉴의 개인정보 확인 버튼을 항상 표시하도록 연결했다.
새 진입점은 현재 Required나 native form 준비 상태를 뜻하지 않으며 힌트를 저장하지 않는다.
manager가 없거나 작업 중이면 버튼을 사용할 수 없다. 현재 확인 결과는 계정 삭제 상태와
별도 label에 표시한다. 같은 component의 재바인딩은 자기 listener만 교체한다.

기존 warm-session Required owner가 있으면 기존 명시적 옵션 접근을 유지한다.
cold-session의 연령 미선택·거절·편집 상태에서는 기존 연령 선택 화면으로 안내하고
네트워크 요청을 하지 않는다. 선택이 끝나도 이전 클릭을 자동으로 이어가지 않는다.
알려진 연령도 별도 `PrivacySdkEnvironmentReviewed=false`와 기존 regional/cohort review가
차단하므로 현재 운영 SDK 호출은 0이다. 광고 unit/광고 release contract는 개인정보
호출의 허용 근거로 사용하지 않는다. No Ads 권한·계정·키 데이터 경로는 변경하지 않았다.

검토된 환경과 연령을 가정한 순수 경로는 명시 Update 한 번 → 이번 성공 callback →
fresh Required → Show 순서다. 이전 bool Required는 성공 전·실패·Unknown·interface
부재·getter 예외 뒤에 접근 허용으로 사용하지 않는다. fresh refresh owner는 항상
privacy-only이며 광고 Request와 eligibility를 복구하지 않는다. Gather·Mobile Ads
init/load·자동 재시도는 없다. 이번 refresh를 위해 이전 active 광고 owner도 중단한다.

Update timeout과 연령 변경은 늦은 Update callback을 버린다. 표시 중인 native form은
실제 callback까지 busy를 유지하며 host timeout이나 연령 변경으로 닫혔다고 가정하지
않는다. 결과 안내는 양식 불필요·상태 확인 실패·양식 표시 실패·창 닫힘을 구분하고
어느 결과도 과거 동의 철회 완료라고 표시하지 않는다. SDK client 생성 예외도 확인 실패로 안내한다.

`PrivateAdsPrivacyRestartChecks.cs`의 신규 순수 상태 시나리오 11개가 첫 실행에서 통과했다.
실제 gate/policy와 합성 client로 fresh status 순서, 실패 뒤 cached Required 차단,
Unknown/interface 부재/getter 예외, 기존 TFUA 유지, 중복·늦은 callback, timeout 후
명시 재시도, 연령 변경 중 busy 소유권, Dispose/새 gate 경계와 warm 결과 구분을 확인했다.
운영 false 조건과 연령 미선택·거절·편집 차단도 순수 정책에서 확인했다.

실제 manager/UI를 포함한 현재 runtime source와 Advertising source는 Unity 참조를
사용한 Roslyn 컴파일을 통과했다. 기존 Bee RSP는 참조 입력으로만 사용했으며 이번에
Unity가 새 RSP를 생성한 증거가 아니다. 합성 실행은 manager·게임 UI를 직접 실행하지
않았고, 컴파일한 Unity runtime assembly도 로드하지 않았다. 새 tests 성공으로 기존
실패 기록을 지우거나 이전 기기 성공을 반복하지 않았다.

실제 화면의 label 높이·배치, manager lifecycle/클릭 동작, 실제 SDK refresh와 native form,
cold-process 재시작은 미검증이다. 운영 개인정보 refresh 활성화도 미완료다.
`runtimePrivacyVerified/binaryVerified/distributable=false`를 유지한다.
