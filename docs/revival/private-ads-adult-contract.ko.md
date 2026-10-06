# 성인 전용 비공개 광고 승인 계약의 소스 준비

2026-10-01, PR #304의 Phase A 이후 `PrivateAdsReleaseContract`를 최소 소스로 구현했다.
이번 변경은 승인 계약 판독기와 검증 코드이며 운영 광고 활성화나 실제 release 빌드가 아니다.
기존 `ProductionAdsEnabled`, 지역 및 모든 연령별 gate는 false이고 compiled 승인 binding은
readonly null이다. 실제 운영 resource와 승인 true 자료를 만들지 않았으며 SDK/정책 caller에
연결하지 않았다. 이 판독기의 존재를 기존 UMP·No Ads·보상 버프 결합 완료로 표현하지 않는다.

## 계약과 적용 경계

resource 이름은 `RevivalPrivateAdsRelease`로 고정한다. Android nondevelopment Player에서
Editor/batch/debug/test/harness가 아닌 경우에만 한 개의 TextAsset을 읽을 수 있다. 없거나
둘 이상이면 닫힌 상태를 유지하고, 형식이 잘못되거나 예외가 나도 해당 프로세스에서는 다시
읽지 않는다. resource나 승인 binding을 바꾸는 공개 setter, PlayerPrefs, locale, debug 또는
새 서버 override는 없다. 현재 승인 binding이 없고 운영 gate가 false여서 resource를 읽기
전에 false를 반환한다. 신뢰 경계는 검토된 source와 immutable 빌드 내용이다.

schema 1은 다음 18필드만 허용하는 UTF-8 flat JSON object다. 타입·중복된 decoded key,
unknown/missing field, trailing comma/comment/추가 JSON, 잘못된 숫자·UTF-8·16KiB 초과를
거절한다. source와 runtime에 비밀 값이나 승인 문서 본문을 넣지 않는다.

| 필드 | 요구 조건 |
| --- | --- |
| `schema`, `mode` | integer `1`, `adult_only_release` |
| `packageId`, `androidAppId`, `productionRewardedAdUnit` | 형식·같은 실제 publisher·실제 consumer context 일치, Google sample 제외 |
| `countryCodes` | 비어 있지 않은 uppercase 두 글자 코드, 중복 없는 정렬된 `;` 목록 |
| `sourceHead` | 독립 검토된 **소스 baseline** 40자리 해시와 binding 일치 |
| `inventorySha256`, `countryContractSha256` | inventory·배포국가 계약의 64자리 해시와 binding 일치 |
| `activationReviewSha256`, `regionalReviewSha256`, `adultReviewSha256` | 별도 승인 근거의 64자리 해시와 binding 일치 |
| `productionActivationApproved`, `regionalReviewApproved`, `adultConsentReviewed` | boolean true만 허용하는 승인 계약 형태; 현재 실제 자료는 생성하지 않음 |
| `under13ConsentReviewed`, `from13To15ConsentReviewed`, `from16To17ConsentReviewed` | boolean false만 허용 |

resource 전체 바이트 SHA-256도 compiled binding에 정확히 일치해야 한다. 위 해시는 검토
자료를 식별하기 위한 provenance이며 전자서명이나 법적·정책적 승인을 자동으로 입증하지
않는다. 최종 build commit을 자기 자신의 resource hash에 순환 바인딩하지 않는다.
`sourceHead`는 승인 검토 baseline이고 실제 build source/provenance는 별도 artifact receipt에
기록한다. private 자료가 실제 검토·승인되었다는 판단을 임의의 true JSON이나 해시 형태로
대체할 수 없다.

`countryCodes`의 문법과 resource 바이트 binding은 검토된 배포선언 확인이다. 실제 ISO 목록,
사용자 위치 판정 또는 UMP 동의 조건을 대신하지 않는다. 이후 실제 배포국가 설정과
country 계약의 일치, 지역별 공개 메시지·개인정보 검토는 해당 근거로 확인해야 한다.
성인 외의 Under13/13–15/16–17, Unknown, Declined 및 미정의 enum은 허용하지 않는다.

## 실제 실행을 허용할 때 필요한 근거

1. 사용자가 승인한 실제 inventory·배포국가·지역·성인·activation 근거를 비공개 자료로
   고정하고 서로의 정확한 해시를 검토한다. 현재 자료를 자동으로 true로 바꾸지 않는다.
2. 해당 자료에 묶인 immutable release resource와 compiled binding을 별도 source review로
   고정한다. 기존 운영 master/지역/성인 gate 변경은 그 승인 범위에 포함되어야 한다.
3. consumer 연결은 `Application.identifier`, 실제 SDK App ID와 설정 Unit의 일치, 저장된
   유효 성인 선택, 기존 UMP 완료/요청 허용, fullscreen 형식, No Ads와 보상 완료 조건을
   유지하는 별도 변경으로 검토한다. 이 클래스의 true 결과만으로 SDK를 호출하지 않는다.
4. disabled candidate와 분리된 승인 production producer mode를 구현·검토한다. 현재 Phase A
   여섯 필드 parser나 false gate 검사를 느슨하게 만들어 운영 mode로 사용하지 않는다.
5. 명시적으로 허용된 단일 실제 빌드와 기존 릴리스 서명 근거, source/tool/설정 고정,
   actual delta 및 별도 reviewed recovery, 실제 AAB의 resource/직렬화 unit·merged manifest·
   인증서·compile provenance를 해당 artifact SHA에 연결한다. 별도 기기·개인정보·스토어
   검증을 완료하기 전에는 runtime/privacy/artifact/distribution PASS로 표현하지 않는다.

이 목록은 이번 소스 준비의 미완료 경계를 구체화한 것이며, 새로운 서버 승인 조건이나
성인 전용/No Ads/보상 버프 선택을 다시 요구하는 문서가 아니다. 승인 변경이나 실제 빌드가
필요해질 때 구체 실행 범위를 총괄 담당자에게 보고한다.

## 변경 관련 최소 검증

Unity API를 inert stub으로 대체한 순수 실행 fixture가 새 20개 check를 수행한다. 실제 운영
값은 없고 true 계약은 메모리의 합성 fixture뿐이다. 최초 compile은 테스트 runner의
`netstandard` facade 참조 누락으로 실패하여 본문 실행0이었다. 원 실패 로그를 보존하고
참조를 보완한 동일 검증의 두 번째 시도에서 compile0/20 PASS를 확인했다. 기존 parser59·
게임·GUI·SDK 시험은 반복하지 않았다. 새 denylist의 단독 `TAMER_IAP_STORE_TEST` 및
`TAMER_UMP_ONLY_HARNESS` 조건은 소스 조건 평가로 별도 확인하며 artifact 검사와 구분한다.

추가로 Unity `6000.3.25f1`의 392개 참조와 `UNITY_ANDROID`를 사용해 실제 Unity API를
참조하는 새 클래스만 순수 library compile을 한 번 수행하여 exit0를 확인했다. Unity를
열거나 assembly를 로드하지 않았다. source 및 원 manifest/result/log는 ignored 근거에
보존한다. 실제 Unity build·SDK·광고 요청·private 승인 자료 수정은 0이며, source 준비와
실제 승인·binary·runtime/privacy 검증을 구분한다.

## consumer 연결과 운영 생산 모드의 소스 준비 — 2026-10-06

manager의 운영 환경은 기존 production policy **그리고** immutable 계약을 요구한다.
`AllowsCurrentAndroidRelease`는 master/성인·지역 검토/compiled binding/Android nondev를
순수 조건으로 먼저 확인하고 test/harness를 거절한다. 그 뒤에만 실제 설치 앱 manifest의
`com.google.android.gms.ads.APPLICATION_ID`를 JNI로 한 번 읽는다. resource의 자기 App ID를
SDK 설정의 증거로 삼지 않는다. 실패/불일치를 캐시하고 package/App/Unit을 정확히 비교한다.
현재 master/지역/성인 false와 binding null을 유지하므로 실제 JNI/resource 읽기도 없다.

No Ads는 `CanRequestAds`, 초기화와 로드를 차단하며 UMP 상태를 직전에 재확인한다.
`CanBeginConsent`와 privacy entry는 No Ads로 막지 않아 동의 관리와 Required privacy
발견 경로를 유지한다. 기존 즉시 reward 분기와 session/buff 처리는 그대로다. 성인 선택과
계약은 UMP 동의나 보상 완료를 대신하지 않는다. privacy 선택 후 자동 load도 하지 않는다.

producer는 기본 `disabled_candidate`와 별도 `approved_adult_release`를 구분한다.
비활성 여섯 필드 parser/false audit는 그대로이며, 운영 mode에는 외부 승인된 18필드 raw
resource와 별도 `private_ads_approved.py` preflight가 필요하다. 실제 source master/지역/성인
true·미성년 세 gate false, 정확한 일곱 literal binding, package/App/inventory 일치를 요구한다.
source 선언은 단일 무조건 선언만 허용하며 문자열/주석 위장·조건부·불균형 지시문을 거절한다.
JSON/env/CLI로 compiled 승인을 만들 수 없다. 현재 false/null에서는 staging/marker 전에 거절한다.

prepare/build-once의 mode와 integer `receiptModeVersion=1`은 exact 검토 계획에 묶인다.
운영 mode는 승인 resource 원본 바이트를 수정 없이 전달한다. 별도
`BuildApprovedAdultRelease`는 settings 변경 전 compiled gate/binding과 strict parser를 다시
검사한다. settings scope/원래 씬·injection/보호 inventory/actual delta·별도 검토 복구는
공유하되 config 검증을 섞지 않는다. receipt의 mode와 `approvedBuildContractMatched`를
대조하고 운영 callback의 `compiledEditorGatesDisabled=false`를 명시한다. 이는 실제 Player
전체 또는 최종 artifact 검증이 아니므로 productionContract/binary/distributable false다.

실제 운영 실행에는 승인 자료와 별도 source 변경 검토, exact plan/source/tools/signing
검토와 명시적인 단일 실행 허용이 필요하다. 실제 callback/복구와 최종 resource·serialized
unit·merged manifest·인증서, 개인정보·기기·스토어 검증을 artifact SHA에 연결해야 한다.
이번 source 경로 연결을 실제 운영 executor·privacy/runtime 검증 PASS로 표현하지 않는다.
운영 승인 true 자료/resource/Unit을 생성하거나 주입하지 않았다.

새 Python 검사 최초 5개, binding/spoof·mode version 후속 2개, receipt mode 혼용 거절 1개가
각각 첫 실행 PASS였다. 기존 회귀는 반복하지 않았다. 순수 JNI stub 5개는 false/null에서
객체/resource 접근0을 확인했다. isolated ignored 합성 source만 true/literal binding으로
변환한 fixture는 정상/context 불일치/manifest 불일치/예외 4개 시나리오에서 1회 캐시와
재시도 차단을 확인했다. tracked 실제 source hash는 불변이며 실제 JNI/SDK0다. Python의
No Ads/privacy 검사는 guard 순서 읽기이며 실기기 동작 검증은 아니다.

manager 순수 compile1은 별도 assembly에서 internal UMP client를 참조하여 `CS0122`로
실패했다. compile2는 작성자가 client의 경로를 Advertising으로 잘못 지정하여 `CS2001`로
실패했다. 두 근거를 보존하고 규칙에 따라 중단했으며, 사용자 명시 승인 후 실제
`Assets/Scripts/Managers/GoogleUmpConsentClient.cs`를 같은 source assembly에 포함한
compile3 1회가 exit0이었다. 의존 producer 템플릿은 첫 순수 compile exit0였다. assembly
load/본문 실행은 없고 실제 Editor/build·native SDK·광고·운영 승인 자료 변경은 0이다.
