# 가족 정책 재심사 준비 대조 — 2026-10-30

최신 통합 후보29는 source `6bb7b37c53a3be764e032b1e1c880b1af752da3c`, AAB SHA-256 `eecea981f569fcccd2609fa20124ea2faf45c46bd7d4a19855eb4c7b03b2c663`이며 [Console 분석·내부 초안 저장](candidate29-console-analysis-20261030.ko.md)을 완료했습니다. 아래 Android 시작 경로의 정적 관측은 당시 후보28/cache를 대상으로 했고,29에는 별도 [IAP AdsIntent 보호](iap-advertising-intent-20261030.ko.md)도 포함됩니다. **정책 승인·심사 전송 가능 판정은 아닙니다.** 비공개 문의·계정 자료는 싣지 않습니다.

## 제출 전에 연결할 근거

18:44 KST 읽기 후속: Console 대상 연령은9~12·13~15·16~17·18+이고 교사 추천 프로그램 미포함, 광고 ID 사용·광고 포함 신고입니다. 대상 연령은 검토 전송 준비 상태로 표시됩니다. 관리 화면의 기존 선택과 개요 상세를 대조했고 값 변경/저장·미성년 선언 확인·심사 전송은 하지 않았습니다. 혼합 대상 신고와 성인 전 ID/SDK 처리의 실제 검증·최종 심사 승인을 구분합니다.

| 항목 | 확인한 범위 | 남은 확인 |
| --- | --- | --- |
| 연령별 광고 | 혼합 대상 앱이며 성인만 광고 경로를 허용합니다. `AgeTreatmentPolicy.IsReviewed`는 미성년3구간 false/Adult true, Unknown·Declined는 미승인입니다. `CanBeginConsent`와 실제 init/load/show가 이 경계를 사용합니다. | 정확한 최종 제공물에서 미성년/미상/거절·수정 중과 성인 경로를 관측합니다. 성인 전 광고 표시 차단만으로 모든 SDK 데이터 처리가 없다고 쓰지 않습니다. |
| 중립 연령 화면 | `LocalAgeChoice`의 빈/잘못된 값은 Unknown이며 성인 기본값이 없습니다. `AgeChoicePresenter`의5개 선택은 동일 생성 함수/높이를 쓰고 게임에 성인 선택이 필요하다는 문구가 없습니다. | 최종 화면/실제 제공과 Target audience 신고를 대조합니다. 저장된 자기 신고를 법정 연령·보호자 동의 인증으로 확대하지 않습니다. |
| 심사 접근 | 기존 심사 계정과 로그인 안내의 공식 저장은 완료했습니다. | 두 연령 경로의 정확한 메뉴·보상 지점·성인 동의·재시도와 No Ads 예외를 안내에 보완하고 해당 계정으로 가능한 범위를 확인합니다. 비밀번호를 문서·원로그로 복제하지 않습니다. |
| 이전 제공 코드 | 후보29 분석/내부 초안 저장, 프로덕션24 제공·26 거절·내부 일시중지를 확인했습니다. | 제출 시 모든 활성 release의 포함/미포함 목록을 확인합니다.26 거절·내부15 미포함만으로 모든 release에서 제외됐다고 쓰지 않습니다. |
| 공개 안내 | Data safety의 수집10/공유6과 기존 공개 정책·삭제 안내를 대조했습니다. | PGS의 게이머 신원 항목을 명확히 한 최소 정책 문구, 공급자 실제 보관/삭제 범위와 최종 제공 상태를 확정합니다. |

## Android 시작 진입점의 정적 확인

실제 후보28 manifest에는 `MobileAdsInitProvider`와 AD_ID/AdServices 권한이 있습니다. **provider나 권한이 존재한다는 사실만으로 성인 선택 전 SDK 초기화·광고 ID 조회·통신을 판정하지 않습니다.** 반대로 존재를 무시하고 차단 완료라고 주장하지도 않습니다.

로컬 빌드 캐시의 Google 공식 `play-services-ads`/`play-services-ads-api` **25.5.0 AAR**에서 `classes.jar`를 읽어 Java bytecode를 확인했습니다. AAR·추출 jar SHA는 비공개 감사 자료에 기록했습니다. `MobileAdsInitProvider.attachInfo`는 `zzev.attachInfo`로 전달하며, 그 메서드의 직접 경로는 앱 metadata 읽기·App ID 형식 검사·옵션 로그·상위 provider 연결입니다. 두 클래스의 `onCreate`는 false를 반환합니다. 읽은 메서드에 직접적인 `MobileAds.initialize`·광고 ID 조회·광고 요청 호출은 없었습니다.

이는 지정된 캐시 AAR의 해당 메서드에 관한 정적 관측입니다. R8 처리 후 최종 dex 전체·Google Play 서비스 동작·로그 함수의 모든 종속 코드·최종 기기 통신을 독립 검증한 결과가 아닙니다. provider를 임의로 제거하거나 오래된 `DELAY_APP_MEASUREMENT_INIT` 플래그를 추가하는 것으로 해결 완료 처리하지 않았습니다. Google SDK 지원의 [2025년 설명](https://groups.google.com/g/google-admob-ads-sdk/c/73Vk7nADtbk)은 해당 지연 설정이 예전 app measurement 기능과 연결됐다고 설명하며, 현재25.5의 보호 근거를 대신하지 않습니다.

게임의 일반 SDK 진입은 `GoogleAdMobManager`의 `SetRequestConfiguration`→UMP 경로와 `InitializeConfiguredSdk`를 대조했습니다. 연령/환경 gate를 먼저 검사하고, 명시 보상 요청에는 준비된 계정·No Ads·최신 동의도 요구합니다. PGS/PlayFab 로그인과 Unity IAP·엔진의 처리는 이 광고 gate와 별개입니다. 레거시 기기 로그인에는 Android 기기 식별자 경로가 남아 있으므로 이를 광고 ID와 혼동하거나 모든 식별자 전송 없음으로 표현하지 않습니다. 운영 계정을 임의 이행·초기화하지 않습니다.

추가로 저장소의 GMA/UMP DLL10개를 실행 없이 Mono.Cecil0.11.5로 읽었습니다. 자동 시작 attribute가 붙은 메서드4개 중3개는 static 상태 초기화이며, `Insight.CacheBaseProperties`는 플랫폼·앱 ID/버전·Unity 버전·OS·기기 모델을 로컬 필드에 캐시합니다. 이4개 메서드의 직접 호출 목록에는 광고 초기화·광고 ID·네트워크 호출이 없었습니다. 따라서 **성인 전 SDK 내부 처리가 전혀 없음**이라고 쓰지 않습니다. Android client/UMP client의 static 생성자와 native 객체 경로는 자동 시작 메서드와 구분하며, 이 assembly 메타데이터 검사도 IL2CPP 최종 실행·간접 호출 전체를 대신하지 않습니다.

## 최종 Play APK의 지정 진입점 후속

Google 앱 서명 APK29/SHA`5e10b8632d56fb4b9aa20bac58ae3501d87b13d79f5ea3b37f4a9ef0811bd44b`의DEX3개를 설치된 공식 Android SDK build-tools36.0.0 `dexdump -d -n`으로 읽었습니다. checksum/verification 생략 옵션은 사용하지 않았고20,256클래스 중 지정 provider/delegate2개를 찾아 발췌했습니다. 두 `onCreate`는 정수0을 반환하며, `attachInfo`의 직접 호출은 metadata/앱ID 형식 검사·로그·상위 provider 연결입니다. 읽은 두 클래스의 직접 호출 목록에 광고 ID 조회·MobileAds.initialize·네트워크 호출은 관측하지 못했습니다.

이는 앞선 캐시 AAR 관측을 실제 최종 DEX의 지정 진입점에 연결한 정적 근거입니다. 방문한20,256클래스 전체를 분석했다는 뜻이 아니며 간접 종속·OS/Play 서비스·IL2CPP 실행·기기 통신·모든 영구 식별자 차단은 미검증입니다. 기존 로그인의 기기 식별자 경로도 광고 gate와 별도이며 이를 지우거나 계정을 이행하지 않았습니다. [내부29 제공](internal29-live-20261030.ko.md)은 운영 검증을 준비하는 단계로 정책 승인과 구분합니다.

## 실제 심사 동선에 적어야 할 내용

1. 제공된 Google/Play Games 계정으로 기존 저장을 불러옵니다. 새 게임 계정 생성·저장 초기화·구매 권한 변경을 요구하지 않습니다.
2. 첫 연령 화면은 선택 전 광고가 없어야 합니다. 이후 **Settings → Account & privacy → 연령대 다시 선택**에서 같은 연령 화면으로 돌아갑니다.
3. **13세 미만 / 13~15세 / 16~17세 / 응답하지 않음**은 광고를 제공하지 않습니다. 연령을 수정 중인 상태도 광고 요청을 막습니다. 광고 없이 마을·전투를 이용할 수 있어야 합니다.
4. **18세 이상**은 지역별로 필요한 개인정보 절차를 완료한 뒤 마을의 광고 보상 포털에 접근합니다. 명시 클릭으로 로드하며 준비 전 첫 시도에는 로드 실패/대기가 표시될 수 있습니다. 로드 완료 후 다시 눌러 재생합니다. 실제 광고 확보·지역 전달은 최종 후보에서 확인해야 합니다.
5. **No Ads 계정은 광고 대신 버프를 받습니다.** 해당 계정의 광고 미표시는 성인 광고 경로의 재생 검증이 아닙니다. 기존 심사 계정의 실제 권한·버프 잔여를 확인하고 광고 재생 검토에 쓸 수 있는 정당한 계정 조건을 준비합니다. 기존 구매를 취소/회수하거나 운영 저장을 지워 만들지 않습니다.

현재 LD의 Play 설치 확인 차단과 미인증 상태는 유지합니다. 같은 조건의 운영 설치/로그인·실광고 호출을 반복하지 않았고 이 문서 때문에 새 빌드·서명·Console 저장/심사 전송을 실행하지 않았습니다. 사용자 복귀 후 정상 제공과 필요한 계정 확인을 연결합니다. [전체 잔여](remaining-recovery-work.ko.md)

공식 기준: [Google Play Families 정책](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en), [Google UMP Unity 설정](https://developers.google.com/admob/unity/privacy), [광고 연령 처리](https://developers.google.com/admob/unity/targeting). 중립 연령 처리·아동/미상 광고 및 식별자 제한과 SDK 초기화 전 요청 설정을 각각 대조하며, 문의의 방향 확인을 앱 전체 정책 승인으로 계산하지 않습니다.
