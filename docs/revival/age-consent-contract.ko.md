# 연령 처리와 실행별 동의 계약 준비

2026-09-21, checkout `C:\Users\pc_17\.codex\worktrees\e5e5\Tamer`, branch `codex/age-consent-contract`, base `15df0fd87addcce51918bde574385615325b2933`.

## 구현과 차단 경계

`AgeTreatmentPolicy.TryCreatePlan`은 저장된 구간을 조건부 보호 계획으로 바꾼다. 13세 미만/13~15세는 Child와 UMP TFUA=true, 16~17세는 Teen과 false, 18세 이상은 Unspecified와 false다. 미상/거절/지원하지 않는 값은 계획을 만들지 않는다. 이는 기존 검토안의 코드 표현이며 국가별 법적 연령이나 지역 계약을 확정한 것이 아니다. `RegionalConsentReviewed=false`를 별도로 유지하며 `IsReviewed`를 통과해야 manager가 설정·UMP·초기화에 진입한다. PlayerPrefs, 기기 로캘, 디버그 플래그로 이 계약을 우회하지 않는다. `ProductionAdsEnabled=false`와 공식 sample 단위만 사용하는 기존 정책도 유지한다.

설치된 plugin11.5.0 Core DLL에서 nullable `RequestConfiguration.AgeRestrictedTreatment`와 Child/Teen/Unspecified enum을 확인했다. Android DLL의 `RequestConfigurationClient.BuildRequestConfiguration`은 `com.google.android.gms.ads.AgeRestrictedTreatment`와 `setAgeRestrictedTreatment`로 전달한다. native dependency는 GMA25.4.0, UMP4.0.0이며 업그레이드하지 않았다. [고정 버전 RequestConfiguration 소스](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/source/plugin/Assets/GoogleMobileAds/Api/Core/RequestConfiguration.cs), [공식 targeting 안내](https://developers.google.com/admob/unity/targeting).

manager의 설정 생성은 새 AgeRestrictedTreatment와 최대 등급 G만 사용하며 deprecated 광고 TFCD/TFUA는 설정하지 않는다. UMP의 bool은 계획에서 별도로 전달한다. UMP TFUA는 광고 SDK로 전달되지 않으며 true로 폼이 생략된 것을 동의 획득으로 기록하지 않는다. Teen에 Child와 같은 식별자 차단 효과가 있다고 주장하지 않는다. [UMP GDPR 안내](https://developers.google.com/admob/unity/privacy/gdpr).

실행마다 새 manager/gate를 만들고 `Init`에서 갱신을 시작하도록 연결했다. 연령 질문 중에는 아무 호출도 하지 않으며 유효 선택 완료 후 새 문맥으로 갱신한다. 순서는 보호 설정 → UMP Update → 필요한 폼 → SDK 요청 가능 판단이다. 이후 명시적인 광고 요청에만 Mobile Ads 초기화/로드가 이어진다. 시작 동의 작업 중 요청은 대기열에 쌓지 않으며 작업 완료 후 다시 명시적으로 요청한다. 이미 완료된 같은 실행의 gate는 재사용하지만 새 실행은 SDK에 이전 허용값이 남아 있어도 Update를 건너뛰지 않는다. 현재 지역 게이트 때문에 이 준비 경로는 실행되지 않는다. [실행별 Update 공식 안내](https://developers.google.com/admob/unity/privacy).

Required 개인정보 옵션은 광고 허용값과 독립적으로 접근할 수 있다. 옵션을 열기 전에 로드 세대·늦은 보상 세대를 무효화하고 광고 캐시를 폐기한다. 종료 후 자동 로드는 없다. 미상/거절에서는 UMP 옵션 호출도 0이며 동의 Reset은 사용하지 않는다. 연령 재선택은 기존 gate를 폐기하고 새 보호 설정으로 시작한다. 업데이트/폼 오류·타임아웃은 이전 허용값이 있어도 차단하며 열린 폼에는 네트워크 타임아웃을 적용하지 않는다. NoAds 신규 판매/복원 코드와 기존 보상 우선 분기는 변경하지 않았다.

## 지역 검토를 완료하기 위한 구체 입력

현재 지원 국가 목록과 게시 메시지 근거가 확정되지 않았다. 아래 항목을 확인하기 전 `RegionalConsentReviewed`를 true로 바꾸지 않는다.

| 확인 대상 | 필요한 근거 |
|---|---|
| 실제 배포 국가 | Play의 현재 배포 국가/지역 목록, 신규 확대 없는 범위 확인. 언어·기기 로캘로 대체하지 않음 |
| 연령별 보호 | 해당 배포 범위에서 위 구간/Child·Teen/TFUA 조합을 적용할 근거. 특히 13~15세 전체의 보수적 Child/TFUA 처리와 16~17세 TFUA=false 조건 |
| EEA·영국·스위스 | 운영 앱에 연결된 유럽 규정 메시지 게시 상태, 개인정보처리방침 URL, 공급자·목적·언어·필요한 선택/철회 경로 |
| 미국 해당 주 | 지원 지역에 적용되는 메시지 게시 범위와 선택/옵트아웃 설정, 유럽 메시지와 별도 확인 |
| 나머지 배포 지역 | UMP 지원 범위 밖 의무·연령 기준에 대한 추가 처리 필요 여부. UMP NotRequired를 전 세계 적법성으로 간주하지 않음 |
| 실제 SDK 결과 | 검토된 테스트 조건에서 Update/폼/Required 옵션/철회·재실행 결과. CanRequestAds는 개인화 동의나 메시지 게시 완료 증명이 아님 |

필수 메시지 누락 여부를 현재 SDK bool 하나로 검출할 수 있다고 주장하지 않는다. 위 게시 확인을 소스 검토 계약으로 막아 둔다. 메시지 게시/Console 저장과 운영 광고 활성화는 이 변경에 포함되지 않는다. Families 5초 종료는 별도 미해결이며 이 계약을 승인하더라도 해결되지 않는다.

## 검증 범위

기존 Editor 테스트에 구간 매핑, 명시적 TFUA true/false, 새 실행의 캐시 우회 방지, SDK 설정 객체의 새 태그/G/구 태그 미지정, 모든 연령의 manager 무호출, 광고 차단 중 Required 옵션 접근과 늦은 보상 차단을 추가했다. 새 네트워크 harness는 만들지 않았다.

**이전 중단 기록 — 동일 테스트 2회 실패.** 1차 소스 `bd64a23ad61a3ec94810e33bd0b65623e32cde5d`, UTC05:50:12, 4개 fixture 93건 중 89 PASS/4 FAIL. 실패는 `Revival_PinnedSdkConfigurationUsesNewAgeTreatmentAndSeparateRating`의 네 구간이며 `MaxAdContentRating.ToString()`을 문자열 G로 가정한 assertion 오류다. 2차 소스 `1d3ac08a7cb9c6e0eb4e54849a453fdf10287722`, UTC05:51:03, 실패한 4건만 실행해 0 PASS/4 FAIL. 테스트의 G 비교만 변경했고 제품 코드는 동일했다. `GetField("G")`가 null이라 테스트에서 NullReferenceException이 발생했다.

2차 후 DLL 읽기 전용 조사로 `G`가 필드가 아닌 속성이며 매번 새 객체를 반환하고, 문자열 값은 공개 `Value` 속성임을 확인했다. 이는 에이전트의 테스트 작성 오류다. 추가 수정·3차 실행·검증 완료 PR 진행을 중단했다. 공개 Value를 검사하도록 수정 후 동일 4건만 재검증할지는 사용자 승인 대상이다. 기존 89건은 재실행하지 않는다. 두 XML의 경로/해시는 [검증 기록](age-consent-contract-validation.json)에 보존했다. Editor는 종료됐고 ProjectSettings snapshot 복원을 마쳤다. CLI의 라이선스 validation 경고와 별개로 실제 XML의 테스트 실패가 존재하므로 라이선스 오류로 돌리지 않는다.

**재개 승인 후 결과:** 사용자의 계속 진행 요청을 전달받아 공개 Value 속성 기준 수정·동일 4건 재검증을 재개했다. 설치 DLL의 G getter → 생성자 → Value 저장 및 public Value getter를 읽어 확인한 뒤 실제 설정 객체의 Value를 `G`와 비교하도록 테스트만 수정했다. 소스 `aada6804dd39ee865d3853e93a5482816ace570e`, UTC06:20:19, **4/4 PASS**, 실패0·skip0. 과거 두 실패를 지우지 않으며 이번 승인 이후 첫 실행에서 통과했다. 1차 이후 제품 코드 변경은 없고 앞서 통과한 89건은 재실행하지 않았다. 단일 최종 실행의 93/93 통과로 표기하지 않는다. Editor0·설정 복원·슬롯 반환을 확인했다.

Unity6000.0.81f1 / CLI1.0.0-beta.8 / Android min24·target36·ARM64 pin 유지. APK를 생성하지 않아 새 APK SHA-256은 없다. 실제 Android 새 태그 전달, 지역별 폼·철회 UI, 운영 소재·5초 종료는 미검증이다. 공식 sample 네트워크 검증도 통합 담당자의 조건 검토 전에는 실행하지 않는다. [공식 지원 문의 초안](families-ads-policy.ko.md#공식-지원-문의-초안--미발송)은 제출 경로와 질문을 준비했으며 미발송이다.
