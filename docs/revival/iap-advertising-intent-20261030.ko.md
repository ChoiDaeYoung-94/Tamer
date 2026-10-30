# IAP의 지원하지 않는 광고 동의 경로 차단 — 2026-10-30

## 최신 빌드 연결 — 17:02 KST

이 보완을 포함한 [후보29](candidate29-built-20261030.ko.md)를 source`6bb7b37c53a3be764e032b1e1c880b1af752da3c`에서 생성·기본 검사/정확 복구했습니다. 아래6개 Editor 시험은54873ac의 실제 실행 근거이며 현재 빌드의 Assets/Packages와 동일합니다. 실제 최종 운영 GAID/네트워크·로그인/저장·기존 구매/No Ads와 정상 Play 제공·정책/심사는 미검증입니다. 아래의 새 후보 준비 필요/키 입력 없음은 당시 소스 시험 단계의 이력입니다.

검증 소스는 `codex/iap-ads-consent-1030@54873acbc60438edbbeaea68c8bfa0a72e1fa492`입니다. UI 기반 `62b48b2`에서 IAP 동의 매핑과 해당 Editor 검사만 변경했습니다. **기존 서명 AAB28/sourcef5d7ab4에는 이 수정이 포함되지 않습니다.**

## 수정한 경계

이 게임은 Unity IAP의 광고 식별자 처리에 대한 별도 opt-in을 수집하지 않습니다. 기존 `IapConsentDefaults`는 미지정 AdsIntent를 거부하지만 이미 `Granted`인 값은 보존했습니다. 실제 사용자의 값이 Granted였거나 식별자를 전송했다는 관측은 아닙니다.

현재 잠긴 IAP5.4.3의 `PlayerData.CreatePlayerIdentityAsync`는 AdsIntent가 Granted이고 원격 연결 설정에 gaid가 포함되면 Android 광고 ID 조회 경로를 사용합니다. 따라서 앱의 연령이 미상인 동안에도 기존/native Granted 값이 해당 선택 처리를 허용하는 조건이 될 수 있었습니다.

수정 후에는 `WithSafeDefaults`가 IAP의 AdsIntent를 항상 Denied로 정합니다. 기존 BeforeSceneLoad 적용과 StoreController 생성 직전 적용 위치를 유지합니다. AdMob 연령/UMP 흐름을 Unity IAP 광고 데이터의 동의로 전용하지 않습니다. AnalyticsIntent는 미지정일 때 Denied, 명시된 선택은 기존대로 유지합니다. 이 매핑이 모든 SDK의 기본·진단·구매 데이터 처리를 중지한다는 뜻은 아닙니다.

No Ads 상품·비소모성 유형·계정 권한, 구매/복원·Store 연결, GMA 성인 광고·연령/UMP 소스는 변경하지 않았습니다. 해당 변경 없음과 실제 운영 동작 검증 완료를 구분합니다.

## 실제 검사와 복구

- Unity6000.3.25f1/e1dba0a9aba4, CLI1.0.0-beta.8, Android target, EditMode의 `RevivalIAPConfigurationTests` **6/6 통과**, 실패0·컴파일 오류0입니다. 기존 상품/자동 시작 설정·미지정 거부·명시 Analytics 선택과 기존 광고 Granted의3가지 Analytics 상태 회귀를 포함합니다.
- 순수 매핑과 카탈로그만 검사했습니다. native 동의를 직접 변경하거나 광고 ID를 읽지 않았고 Store/계정·구매·실광고를 시작하지 않았습니다. 새 APK/AAB·키 입력·기기 조작은 없습니다.
- 일반 에셋 사전검사는 기존 GUI.psd.meta의 승인된 importer 차이 때문에 거절돼 Editor를 시작하지 않았습니다. 기존의 정확한 checkout/source/Unity/단일 meta·동일 GUID 승인 근거를 연결한 읽기 검사는4561개/복사0으로 통과했습니다. manifest나 원본을 덮어써 예외를 숨기지 않았습니다.
- 실행 전 Assets/Packages/ProjectSettings의6946파일 SHA와 복구용 사본을 보존했습니다. Editor 종료 후 URP Low/Medium 및 ProjectSettings의3개 직렬화 출력만 별도 보존·복구했고6946개 전체 원래 SHA/파일 집합과 Git clean·소유 Editor0을 확인했습니다. 전체 회귀나 같은 성공 시험을 반복하지 않았습니다.

## 다음 후보·심사와 한계

현재 AAB28은 인트로·UI 검토본으로 보존합니다. 새 수정의 운영 검증에는 최종 소스·Console 전체 번호·승인 자료를 다시 대조한 별도 후보가 필요하며 기존28의 서명 계획/receipt를 재사용하지 않습니다. 아직 새 후보의 런타임/GAID·네트워크, 기존 계정/서버 저장·구매/No Ads와 Play 제공 검증은 수행하지 않았습니다.

사용자는 심사 계정의 No Ads 권한에 대해 확인이 필요하다고 답했습니다. 계정 준비 완료/비밀값 직접 입력 이력은 유지하고 실제 권한은 미확인으로 둡니다. No Ads 소유자의 무광고 결과를 성인 광고 재생 검사로 계산하거나, 기존 구매/저장을 제거해 조건을 만들지 않습니다. [심사 동선과 SDK 경계](families-pre-review-20261030.ko.md), [전체 잔여](remaining-recovery-work.ko.md)를 따릅니다.

공식 참고: [Unity AdsIntent API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/UnityConsent.ConsentState.AdsIntent.html), [IAP5.4 이상 Google Play 데이터 안내](https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety). 매핑·소스 경로·범위별 검사는 정책 승인이나 모든 정보 처리 중지의 증거가 아닙니다.
