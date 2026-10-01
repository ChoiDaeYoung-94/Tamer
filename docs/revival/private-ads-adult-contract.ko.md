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
