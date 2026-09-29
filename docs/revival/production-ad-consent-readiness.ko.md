# 운영 광고·동의 연결 준비 — 비활성 유지

## 2026-09-29 대상 연령 저장과 비활성 준비 계약

사용자는 실제 대상을 9세 이상 성인 포함으로 확정하고 기존 178개 배포 국가/지역을
유지하도록 결정했다. Console에서 기존 9~12·13~15·16~17 체크와 18세 이상 미체크를
읽은 뒤, **18세 이상만 추가**했다. 기존 아동 관련 답변·광고 답변·교사 추천 미참여를
유지했고 최종 요약의 네 연령 구간을 확인했다. 저장 완료를 확인했지만 별도 Google
검토 전송·출시는 하지 않았다. 따라서 저장된 변경안을 승인·스토어 반영 완료로 표시하지 않는다.
조회·수정용 own 탭 2개를 닫았으며 기존 사용자 탭은 보존했다.

`AgeTreatmentPolicy.IsReviewed`에 연령별 검토 스위치를 추가했다. 전역 검토만 바꿔
모든 연령의 UMP를 함께 허용하지 않도록 각 구간도 독립적으로 통과해야 한다.
전역·네 연령·운영 광고 스위치는 전부 false이며 미성년 전면 보상형 차단도 유지한다.
성인 미국 UMP 관측을 다른 연령·지역의 검토 승인으로 전이하지 않는다.

`tools/revival/validate_private_ads_preparation.py`는 기존 비공개 준비 초안을 바탕으로
만든 읽기 전용 사전 검사다. 구조 JSON 파싱으로 중복된 decoded key를 거절하고,
inventory 확인은 정확 true, 운영·지역 승인은 정확 false를 요구한다. 루트 필수 필드,
checkout, 단위 형식·게시자 일치, source Settings·manifest App ID와 비활성 코드 조건을
확인한다. 출력은 해시와 상태만 포함하며 식별값은 출력하지 않는다.

이 검사기는 **주입기나 빌드 실행기가 아니다**. C# hook의 동일한 구조 파서,
prebuild부터 postbuild까지 불변 설정 및 digest 검사, 강제종료 복구 journal,
실제 컴파일 define·최종 직렬화 단위 추출 검증은 후속 구현·검토 대상이다.
해시 영수증은 잠금이 아니며 최종 APK/AAB의 내용이나 원복 완료를 입증하지 않는다.
현재 출력의 `binaryVerified`는 false다. 비공개 ID를 tracked 씬·프리팹에 넣지 않았다.

최소 검증: 새 Python 계약 검사 5건이 최초 실행에서 통과했고 실제 private 설정의
읽기 사전 검사도 통과했다. 변경된 연령 정책과 기존 NUnit 관련 사례 11건을 직접
링크한 .NET 9.0.6 runner에서 통과했다. 첫 runner 시도는 이전 NUnit cache 경로를
참조해 컴파일에 실패하여 테스트가 실행되지 않았고, 실제 설치 경로로 정정한 두 번째 시도에서 11/11 통과했다.
Unity runner·Editor·APK 빌드·기기·실광고 시험 결과로 표현하지 않는다.
5초 추가 조사 보류와 기존 보상·No Ads 계약은 유지한다.

리뷰 후 사전 검사의 소스 판별도 보강했다. 주석·문자열을 제외하고 각 비활성 선언이
정확히 하나인지 확인하며, 해당 선언을 감싼 전처리 조건이나 지원하지 않는 토큰은
거절한다. 선언과 무관하게 앞서 종료된 조건부 블록은 허용한다. 이는 보수적인 소스
계약 검사이며 C# 컴파일·최종 binary 검증을 대신하지 않는다. 실제 true 선언과 주석의
false 조합, 문자열·비활성 분기·누락·중복·미종료 주석을 포함한 audit fixture를 추가해
Python 6건이 보강 후 최초 실행에서 통과했다.

앞선 기존 C# 11건은 master=false 상태의 보호 유지 근거이며 연령별 독립 매핑의
조합 증거는 아니다. 별도 비공개 합성 복사본에서 master=true와 한 cohort=true만
설정하는 네 변형을 만들어 각 7개 연령값(미상·거절·지원하지 않는 값 포함)을 확인했다.
.NET runner 28/28이 최초 실행에서 통과했고 제품 소스는 수정하지 않았다.
이 합성 결과를 제품 Unity 빌드나 운영 활성 검증으로 표현하지 않는다.

2026-09-28, checkout `C:\Users\pc_17\.codex\worktrees\e5e5\Tamer`, 기반
`34f2aea5ec673168e0f17af17eb2f80a1feac9e2`에서 코드와 기존 AdMob 화면을 읽었다.
현재 기준은 Unity `6000.3.25f1` / CLI `1.0.0-beta.8` / min25·target36·ARM64다.
이 작업은 새 APK·운영 광고·Console 저장/게시를 실행하지 않았다.

## 실제 연결 공백과 변경

기존 `ProductionAdsEnabled=false`는 선언만 존재했고 실제 요청 조건에 사용되지 않았다.
로드 경로도 공식 샘플 단위만 선택했다. 따라서 두 검토 bool을 true로 바꾸는 것만으로
운영 광고가 연결되지 않으며, 샘플 광고를 운영 복구 완료로 판단할 수도 없다.

이 변경은 운영 환경 조건에서 `ProductionAdsEnabled`를 실제 참조한다. Android 일반
release만 후보이고 Editor·development·batch·명시적 테스트·격리 harness는 운영 환경에서
제외한다. 비공개 출시 빌드에서 설정할 `_productionRewardedAdUnit`은 빈값이 기본이며
tracked 씬/프리팹에는 운영 값을 쓰지 않았다. 테스트 환경은 이 필드를 무시하고 공식
샘플 단위만 사용한다. 운영 선택은 빈값·형식 오류·Google 샘플 게시자·iOS를 거절하며
두 환경이 동시에 허용되거나 모두 차단되면 단위를 반환하지 않는다. 구문 검사 자체는
실제 앱 소유권·광고 형식·서버 활성 상태를 확인하는 검사가 아니다.

UMP 시작 조건은 저장 연령·지역 검토·허용 환경을 따른다. 보상형 fullscreen 형식과
유효 광고 단위 조건은 광고 요청에만 적용하여, 동의 갱신을 광고 형식이나 설정 누락에
묶지 않는다. 이미 Required로 판정된 개인정보 옵션은 미성년 형식 차단/운영 단위 빈값과
무관하게 접근할 수 있고, 옵션 완료 후 자동 로드는 하지 않는다. Unknown·Declined,
미검토 지역과 일반 release 비활성은 계속 요청 전에 차단한다.

`ProductionAdsEnabled=false`, `RegionalConsentReviewed=false`, 현재 연령별 fullscreen
제한을 모두 유지한다. 아동/청소년 보호 설정은 기존 Child/Teen/G 및 별도 UMP TFUA를
그대로 사용한다. 기존 보상 완료→10분 버프·HP 회복, No Ads 권한·판매·복원 코드는
변경하지 않았다. 5초 닫기 추가 조사와 다른 보상/수익 모델 제안은 하지 않는다.

## 게시자 메시지와 정책 URL

AdMob 앱 설정과 tracked Android App ID의 일치를 확인했다. 운영 식별자·계정값·설정
수치는 공개 기록에 중복하지 않는다. 유럽/미국 메시지 탭에는 생성 안내가 표시됐으며
앱별 게시 메시지 근거를 확보하지 못했다. 계정의 대체/자동 메시지 동작도 따로 대조했으나
현재 앱의 실제 표시·문구·파트너·정책 URL 검증을 대신하지 않는다.

[Google 대체 메시지 안내](https://support.google.com/admob/answer/17198583)는 계정 설정에 따라
TC 문자열 없는 적격 광고 요청에 대체 폼을 시도할 수 있고, 새 앱 자동 게시도 설명한다.
따라서 생성 안내만으로 모든 운영 환경에서 폼이 없다고 단정하지 않는다.
[공식 UMP 연결](https://developers.google.com/admob/unity/privacy)은 프로젝트 App ID의 메시지,
실행별 Update, 필요한 폼 및 Required 옵션 진입점을 요구한다. 샘플 App ID의
[기존 성공 기록](ump-only-device-validation.ko.md)은 게시자의 실제 메시지 성공이 아니다.

앱의 독립 정책 버튼은 기존
`https://github.com/ChoiDaeYoung-94/Tamer#개인정보처리방침`을 연다.
공개 페이지 접근과 기존 정책 제목을 확인했지만, AdMob 메시지 안의 URL 설정은
확인하지 못했다. [출시 정책 초안](privacy-policy-release-draft.ko.md)의 실제 처리자·목적·보관
설명 확정과 게시가 남아 있다. 기존 URL을 보존하는 것과 새 정책 검토 완료는 구분한다.

## 승인 후 적용할 구체 묶음

| 순서 | 준비된 적용안 | 완료 근거 |
| --- | --- | --- |
| 배포·연령 계약 | Play 실제 배포 국가와 기존 구간의 Child/Teen/TFUA 조합 대조 | 국가별 범위·검토 근거를 소스 검토 계약에 기록 |
| 유럽 메시지 | 기존 앱만 연결, EEA·영국·스위스 범위, 최초 화면 거절 ON 및 동의/옵션 유지 | 실제 앱·언어·정책 URL·파트너·선택 화면을 검토 후 게시 승인 |
| 언어/정책 | 실제 지원 언어에 맞춘 기본/추가 언어와 기존 정책 URL | 공개 정책과 선택한 모든 언어 미리보기 대조 |
| 미국 메시지 | 미국 배포가 있으면 해당 지원 주의 판매/공유 거부 메시지와 UMP/GPP 경로 | 미국 주 폼·거부·개인정보 옵션 실제 결과; 전 지역 RDP를 임의 추가하지 않음 |
| 계정 공통 설정 | 파트너·적법한 이익·대체 메시지 관계 대조 | 다른 앱에도 미치는 변경 영향과 실제 표시를 승인 묶음에 포함 |
| 비공개 출시 설정 | 검토된 기존 운영 rewarded 단위 주입, 실제 manifest App ID·게시자/단위 대조 | 공개 코드/로그에 값 없이 연결 일치 검증, 설정 없으면 차단 |
| 동의만 기기 검증 | 슬롯 배정 후 최신 LTS에서 실제 게시자 UMP를 광고 초기화/load/show 없이 검증 | Update·거절·재진입·선택 변경·다음 실행·미성년/지역 결과 |
| 운영 활성화 | 위 계약과 출시 검토 후 별도 reviewed 변경 | 샘플 define/ID 없는 최종 APK/AAB; 현재 PR에서는 활성화하지 않음 |

메시지 생성 절차와 거절/지역/언어/정책 URL 항목은
[공식 유럽 메시지 작성 안내](https://support.google.com/admob/answer/10113207)를 따른다.
[미국 주 SDK 안내](https://developers.google.com/admob/unity/privacy/us-states)는 UMP가 GPP 신호를
쓸 수 있음을 설명한다. Child/Teen 보호와 별도 UMP TFUA는
[targeting](https://developers.google.com/admob/unity/targeting),
[GDPR TFUA 안내](https://developers.google.com/admob/unity/privacy/gdpr)로 대조했다.
TFUA 폼 생략은 동의 획득이 아니며, CanRequestAds는 개인화 동의와 동의어가 아니다.

운영 단위의 비공개 빌드 주입 절차, 실제 게시자 UMP 기기 구성 및 모든 Console 게시값은
아직 적용하지 않았다. 위 표는 승인할 구체 변경안이며 이미 적용됐다는 목록이 아니다.

## 최소 검증 상태

- 실제 `AdRequestPolicy.cs`와 기존 NUnit 정책 fixture를 .NET 9.0.6에 직접 링크하고
  reflection runner로 **27/27 통과**했다. 새 운영 단위 사례는 합성 식별자만 사용했다.
  runner는 로컬 ignored 경로이며 Unity runner 실행으로 표시하지 않는다.
- 첫 runner 구성에서 상대 경로가 한 단계 잘못되어 소스/참조를 못 찾았다(테스트 미실행).
  경로를 고친 다음 실행에서 위 fixture가 통과했으며 추가 재실행은 하지 않았다.
- 신규 검사: 모든 환경 조합에서 production false 차단, 운영 단위 누락/형식/샘플 거절,
  테스트에서 운영 값 무시, 운영 선택에서 샘플 대체 없음, 모호한 환경과 iOS 차단.
- Required 옵션과 늦은 보상 무효화 manager 테스트를 네 유효 연령으로 확장했다.
  Unity 실행 결과는 아래와 같이 별도로 기록한다.
- 원시 Console 관찰은 `Logs/revival/production-consent-audit-private.ko.md`,
  .NET runner는 `Logs/revival/ad-production-policy-check/`에 비공개 보존한다.
- 새 Chrome 탭 0. 기존 AdMob 탭은 원래 광고 단위 목록으로 복귀해 보존했다.

### 새 LTS Unity 1차 결과와 중단

검토된 SDK 복원 도구 PR #261 반영 후 실제 시험 head
`6ae65e5e94b1c0bf689b863c7ab293db16f7f16a`를 clean 상태로 확인했다.
앞선 복원 검사 4561/복사0/migration7와 이후 비공개 항목 4204개의 mtime 변경0 근거를
재사용했으며 변경 없는 전체 복원 검사를 반복하지 않았다. SDK가 슬롯을 반환한 뒤
Unity `6000.3.25f1` / CLI `1.0.0-beta.8`에서 지정 4개 fixture를 **한 번** 실행했다.

- 필터: `RevivalAdRequestPolicyTests;RevivalAdManagerTests;RevivalAdConsentTests;RevivalAdEntitlementTests`
- 실제 XML: **118건 중 117 PASS / 1 FAIL / skip0**, UTC 2026-09-28 08:14:30.
- 실패: `Revival_EditorSelectionIgnoresConfiguredProductionInventoryWithoutStartingSdk`가
  batch에서도 단위 선택 True를 기대한 테스트 작성 오류. 정책은 batch를 정상 차단했다.
- 수정안: 합성 운영 필드를 넣어도 순수 테스트 선택은 공식 샘플이라는 양성 검사를 유지하고,
  실제 manager는 batch에서 False/null, 대화형 Editor에서는 공식 샘플을 선택하도록 검사한다.
  제품 코드는 추가 변경하지 않았다. 이 수정안의 후속 Unity 실행은 아직 하지 않았다.
- 로그의 LicensingClient validation 경고와 실제 assertion 실패를 구분한다.
- XML: `Logs/revival/ad-production-lts-20260928.xml`, SHA-256
  `6dee305974db6e9a3a3b46a557141e594d5cfb70ff0960adf229c2030959eea2`.

자체 Editor 종료와 ProjectSettings 복원을 확인했다. 기존 clean 근거와 비교해 own import의
tracked 설정 3개만 private patch 보존 후 복원했다. 다른 프로젝트 Editor는 유지했다.
한편 import 후 비공개 `.meta` 1개가 원본 기준 해시와 달라졌고 GUID는 보존됐다.
복원 도구의 public SDK 목록을 비공개 항목으로 확대하지 않고 해당 파일도 덮어쓰지 않았다.
따라서 지시된 충돌 중단 경계에 따라 실패 1건의 후속 Unity/복원 실행은 보류하고
슬롯을 반환했다. 통과한 117건은 재실행하지 않는다. SDK checkout의 별도 복원 실패와
광고 fixture의 assertion 실패를 같은 원인으로 표시하지 않는다.

### 승인된 복원 검사와 실패 1건의 2차 결과

PR #262 병합 `b963e366ceabbbd4a94a2a0ae52e08f9a5455bff`를 반영한 실제 시험 source는
`b8714014ab7631aeebf1f081f6e7caa6d21231f5`다. 이 checkout 전용 비공개 승인 기록과
원본/현재 메타 보존 자료를 독립 검토한 후, 명시 배정된 현재 상태 복원 검사를 **1회**
실행했다. 결과는 **verified4561 / copied0 / SDK 메타 예외7 / 비공개 메타 예외1**이다.
원본 및 현재 메타를 덮어쓰지 않았고 공개 manifest도 변경하지 않았다. SDK checkout의
승인된 3차 실행과 이 checkout의 필수 검사 1회를 구분한다.

SDK 미니맵 작업 종료와 Editor·기기 슬롯 반환 후 같은 Unity/CLI/Android 기준에서
`Revival_EditorSelectionIgnoresConfiguredProductionInventoryWithoutStartingSdk` **1건만 2차 실행**했다.
실제 XML은 **1 PASS / 0 FAIL / skip0**, UTC 2026-09-28 08:44:50이며 CLI 종료값은 0이다.
앞선 통과 117건은 재실행하지 않았다. 두 결과를 한 번의 118건 전부 통과로 표시하지 않는다.
대화형 Editor 분기의 실제 실행은 이 batch 결과로 대신하지 않는다.

XML은 `Logs/revival/ad-production-lts-single-20260928.xml`, SHA-256
`b7c8e507f5c703b2bbff4fcddf2ab7020c17dad774da7f80ec005c383548bbc4`다.
자체 Editor 종료/ProjectSettings 복원을 확인했고 own import가 만든 tracked 설정 2개만
private patch 보존 후 복원했다. 비공개 현재 메타/GUID와 원본/PSD 본문 일치 근거를 유지했다.
추가 복원·테스트·APK 실행은 하지 않았다.

미검증: 대화형 Editor 분기, IL2CPP 실행, 실제 게시자 메시지·지역 법적 연령 계약,
운영 설정 주입/요청·동의 후 재시작, 네이티브 전체 트래픽, 최종 출시 AAB/스토어.
새 APK SHA-256은 해당 없음이다. 이후 검증은 실제 source head와 슬롯 결과를 별도 기록한다.
