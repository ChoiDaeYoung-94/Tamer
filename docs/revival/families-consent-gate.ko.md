# Families 후속: 명시적 테스트 광고의 동의 확인

2026-09-11, branch `codex/families-consent-gate`, base `18d6e48`, 통합 main `da11757`, 실행 소스 `d67684fd92a85c3fcd768b55d5fc347932f8ff63`. UI 수명 리팩토링 후 이어지는 #91 작업이다. 운영 광고는 계속 차단하며 Console/AdMob 설정을 저장하지 않는다.

## 구현 범위

`AdConsentGate`는 Unity/Google SDK에 의존하지 않는 단일 요청 상태 관리이고, `GoogleUmpConsentClient`가 설치된 UMP 4.0.0 API를 연결한다. 시작이나 일반 release의 차단 요청에서 UMP를 만들거나 호출하지 않는다. 허용된 환경의 첫 명시적 sample Load/Show 요청에만 다음 순서로 실행한다.

1. UMP `Update`에 `TagForUnderAgeOfConsent=true`를 전달한다.
2. 성공하면 `LoadAndShowConsentFormIfRequired`를 호출한다.
3. 오류가 없고 UMP `CanRequestAds()`가 true여야 Mobile Ads 요청 설정 및 초기화로 진행한다.
4. Mobile Ads에도 별도로 아동 태그 true / 동의 연령 미만 true / 최대 등급 G를 적용한다.
5. 초기화 이후 Load/Show도 현재 동의 요청 가능 상태를 확인한다.

이 true 태그는 기존의 보수적 테스트 취급이며 사용자 나이를 추정하거나 수집·저장한 결과가 아니다. 아동에게 동의를 요구하기 위한 구현도 아니다. UMP의 TFUA=true는 동의 요청을 억제하며, 이 값은 광고 SDK로 자동 전달되지 않는다. [Google UMP GDPR 안내](https://developers.google.com/admob/unity/privacy/gdpr)

SDK callback은 기존 manager queue에서 처리한다. 중복 요청/콜백은 한 번만 완료되며 파괴된 소유자의 늦은 callback은 무시한다. update/form 오류는 이전 세션의 허용값이 있더라도 보수적으로 차단하고 사용자의 다음 명시적 요청에서 다시 확인한다. 이는 Google 안내의 이전 세션 허용값 사용 가능성보다 엄격한 테스트 정책이다. 앱 자체 consent 문자열/PlayerPrefs 캐시 또는 동의 reset을 사용하지 않는다.

개인정보 옵션이 Required이면 harness에 진입점을 표시한다. 옵션을 열기 전에 로드된 광고를 폐기하며, 옵션 종료 후 광고를 자동 요청하지 않는다. 이 진입점은 운영 UI 연결 완료를 의미하지 않는다. UMP network Update는 기존 30초 deadline이 지나면 실패로 완료하고 callback 세대를 무효화하여 명시적 재시도가 가능하다. SDK 폼이 열려 있는 동안에는 이 타임아웃으로 폼 완료를 가정하거나 새 폼을 중복 열지 않는다.

## 운영 적용 전에 필요한 결정과 근거

Google은 앱 실행마다 consent 정보를 갱신하고 필요한 폼과 개인정보 옵션을 제공한 뒤 요청 가능 여부를 확인하도록 안내한다. 현재는 운영 광고가 차단된 테스트 경로라 첫 명시적 요청까지 갱신을 지연한다. 운영 활성화 시에는 시작 흐름·접근 가능한 실제 설정 UI·대상 연령 처리·게시 메시지와 함께 다시 검토해야 한다. [공식 UMP 설정 절차](https://developers.google.com/admob/unity/privacy)

9월 11일에는 중립적 연령 확인과 전체 이용자 아동 취급의 운영 타당성을 미확정으로 남겼다. 9월 21일 Console 대상 연령은 여전히 9–12 / 13–15 / 16–17로 확인됐다. 최신 [Families 정책의 Ads SDKs 항목](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en)은 혼합 연령 앱에 중립적 연령 화면 등의 연령 확인 조치를 요구한다. 전체 이용자 아동 태그만으로 이 요구까지 충족했다고 주장하지 않는다. 기존 Console 연령은 유지한다.

## 2026-09-21 운영 연결 전 검토안

사용자가 확정한 목표는 정상 광고와 기존 NoAds 상품을 함께 복구하고 신규 구매·기존 권한·복원을 보존하는 것이다. 아래 연령 화면과 저장 방식은 아직 공개하거나 운영 연결하지 않은 구체안이다. 광고 차단은 검증 중 안전장치이며 판매 중단 출시안이 아니다.

### 중립적 연령 화면

- 제목: **연령대 확인**
- 설명: **연령에 맞는 광고 및 개인정보 설정을 적용하기 위해 연령대를 선택해 주세요. 생년월일은 저장하지 않습니다.**
- 동일 크기·색상·강조의 선택지: **13세 미만 / 13~15세 / 16~17세 / 18세 이상 / 지금 선택하지 않음**. 기본 선택 없음. 보상·광고량·혜택 차이를 선택지에 표시하지 않는다. 18세 이상 선택지는 실제 이용자 분류용이며 Console 대상 연령 확대가 아니다.
- 광고 및 UMP 처리 전에 표시한다. 건너뛰어도 게임·NoAds 구매 권한·복원·무광고 보상을 제한하지 않는다. 건너뜀은 연령 미상이며 성인으로 추정하지 않는다.
- 최소 저장안: 선택 구간은 프로세스 메모리에만 보관하고 종료 시 지운다. 생년월일·정확 나이·계정 연결·서버 전송·분석 이벤트를 추가하지 않는다. 앱을 다시 실행하면 다시 확인한다. 영속 저장은 반복 질문을 줄이지만 개인정보 안내와 수정/삭제 관리가 추가되므로 이 초기안에는 넣지 않는다.
- 설정의 **연령대 다시 선택**은 진행 중 광고/폼이 끝난 뒤 사용할 수 있다. 변경 시작 즉시 신규 광고를 막고 기존 캐시·콜백 세대를 무효화한다. 다시 선택하지 않으면 연령 미상으로 남는다. 화면 재진입을 반복해 광고·보상을 중복 획득할 수 없게 한다.
- **광고 개인정보 선택**은 UMP가 Required로 판단하면 표시한다. 연령 구간 변경은 동의 철회와 같지 않다. UMP 개인정보 옵션을 통해 철회하고 캐시를 폐기하며 선택 직후 광고를 자동 로드하지 않는다. 운영에서 `ConsentInformation.Reset()`으로 동의 이력을 임의 삭제하지 않는다.

### 구간·SDK·지역 처리 제안

| 입력 | 광고 처리 제안 | UMP 제안 |
|---|---|---|
| 연령 미상 / 지금 선택하지 않음 | 연령 선택을 마칠 때까지 광고 초기화·요청 0. 기존 NoAds 보상·신규 구매·복원은 유지 | UMP 시작 0. 기존 동의 이력은 임의 삭제하지 않음 |
| 13세 미만 | 보수적 Child 및 등급 G, 인증 공급원만. 5초 종료 미검증이면 전면 광고 차단 | TFUA=true. 동의 폼을 받지 않는 것을 동의 획득으로 기록하지 않음 |
| 13~15세 | 국가별 동의 연령을 앱이 임의 추정하지 않도록 초기안에서는 보수적 Child/G 사용. 실제 법적 나이가 아동이라고 단정하는 값이 아님 | TFUA=true의 보수 처리 제안. 이 보호 수준은 최종 정책 검토 대상 |
| 16~17세 | 지원되는 Teen 보호 적용, 초기 광고 등급은 G 유지 | TFUA=false로 Update 후 SDK가 지역/메시지/필요 폼을 판단. false 자체는 동의가 아님 |
| 18세 이상 | Unspecified, 초기 광고 등급 G 유지. 개인화 강제 없음 | TFUA=false, 같은 지역별 UMP 절차 |

이 표는 국가별 법적 판단을 대신하지 않으며 배포 대상 지역과 Google 지원 계약을 대조한 후 확정한다. EEA·영국·스위스 메시지와 미국 주 규정 메시지는 각각 실제 게시 범위·공급자·개인정보 URL을 검토한다. 언어/기기 로캘로 지역을 추정하지 않고 UMP의 필요 여부를 사용한다. `CanRequestAds`는 요청 가능성이지 개인화 동의 여부와 동의어가 아니다. [UMP GDPR](https://developers.google.com/admob/unity/privacy/gdpr)

9월 19일 갱신 [Unity targeting 문서](https://developers.google.com/admob/unity/targeting)는 `AgeRestrictedTreatment`의 Child/Teen/Unspecified를 제시하고 기존 광고 TFCD/TFUA를 deprecated로 표시한다. 문서 갱신일은 기능 최초 출시일이 아니다. 현재 pin은 plugin11.5.0/native25.4.0이며 DLL에 새 심볼이 있는 것까지 SDK 담당자가 확인했다. 정확 서명·enum·Android 전달 계약을 확인한 뒤 적용하며 업그레이드가 필수라고 단정하지 않는다. UMP의 TFUA는 별도이고 광고 SDK에 자동 전달되지 않는다.

실행 순서는 연령 결정 → 광고 요청 설정 → 실행별 UMP Update → 필요한 폼 → CanRequestAds 확인 → SDK 초기화 → 사용자가 요청한 광고 로드다. 연령 미결정·오류·타임아웃·필수 메시지 누락은 광고 차단으로 끝내고 일반 게임과 기존 NoAds 보상은 유지한다. 현재 테스트 `Update(true)`를 운영 사용자 전체의 확정 연령으로 재사용하지 않는다.

### 최소 검증 및 승인 경계

운영 연결 없는 순수 core는 연령 입력→처리 계획과 동의 상태를 분리한다. 최소 검증은 미상/네 구간 매핑, 오류 시 요청0, 연령 변경 후 과거 callback 무효화, 중복 동의/로드0, 철회 시 캐시 폐기, NoAds 기존 보상 경로 불변이다. 실제 UI·지역 폼과 5초 종료는 슬롯을 먼저 배정받은 별도 기기 검증으로 확인한다. 같은 테스트 두 번 실패 시 해당 경로를 중단하고 보고하며 세 번째 실행은 승인 전 하지 않는다.

남은 제품 검토는 위 화면·구간·세션 한정 저장 및 13~15세 보수 처리안을 승인할지에 한정한다. 신규 판매 유지 여부와 Console 연령을 다시 묻지 않는다. 메시지 게시/운영 설정 저장은 구체 화면·문구·대상 지역을 제시한 뒤 별도로 승인받으며 이 문서에서는 실행하지 않았다.

같은 날 공식 self-certified 목록은 Google AdMob `play-services-ads` 19.0.0 이상을 기재한다. 현재 Android 25.4.0은 이 버전 범위에 해당하지만, 목록 등재가 실제 소재나 앱 전체의 준수를 보증하지는 않는다. 최종 배포 직전에 목록과 포함 SDK를 다시 대조한다. [Families self-certified SDK 목록](https://support.google.com/googleplay/android-developer/answer/12955712)

AdMob의 현재 읽기 결과는 [실기기 검증 문서](families-device-validation.ko.md)의 2026-09-11 세션을 따른다. 승인 계정 접근, 보상형 단위 1개, 단위에 운영 미디에이션 0개/캠페인 0개, 계정 목록 AdMob 기본 그룹을 확인했다. 개인정보 메시지는 새 메시지 만들기로 표시됐으며 게시 근거는 확인하지 못했다. 과거 문서의 가입 화면 차단은 당시 세션 관찰이다.

## 5초 닫기와 실기기 후속

Families 정책은 아동 또는 나이 미상 이용자의 정상 사용을 방해하는 광고에 대해 보상형/선택형도 5초 후 닫을 수 있어야 한다고 명시한다. UMP/아동 태그는 닫기 UI 시간의 증거를 대신하지 않는다. [Families 광고 형식 요구](https://support.google.com/googleplay/android-developer/answer/9893335)

이전 sample 영상은 시작 프레임부터 광고가 보여 native 첫 화면 기준점이 없었다. 약 5초 구간 X 및 보상 전 취소를 확인했지만 정확한 5초 판정과 운영 소재 적합성은 계속 미입증이다. 이번 녹화는 harness 화면부터 시작해 아래 원본 PTS 근거를 확보했다. 프레임 오차와 샘플 소재 한계를 유지한다.

표시 중 중복 요청을 안전하게 실행하도록 harness에 `duplicate show` 예약 동작을 추가했다. 광고 위를 두 번 터치하는 대신 opened 이후 Unity 쪽에서 같은 요청 API를 한 번 더 호출한다. 표시 중 Home 복귀와 native 실패 callback은 별도 실제 관찰로 구분하며 주입 callback을 native 관찰로 표기하지 않는다.

## 검증

순수 fake client 회귀는 시작 시 무호출, update→form→요청 가능 순서, TFUA 전달, 중복 callback/요청, 오류와 SDK 예외, 소유자 파괴, 수동 재시도 후 이전 callback, 개인정보 재선택을 다룬다. SDK API 서명은 로컬 UMP DLL에서 확인했다. 실행 결과와 해시는 [검증 JSON](consent-gate-validation.json)에 기록했다.


### 실행 결과 (2026-09-11)

- Unity 6000.0.81f1 / CLI 1.0.0-beta.8, 위 실행 소스의 Editor 회귀 **332/332 PASS** (UTC 07:33:48–07:33:53). 별도 순수 consent fake 14개도 통과했다.
- sample/control APK 두 개를 빌드했고 manifest/debug 서명 검사를 통과했다. 통합 담당자가 같은 APK의 bytes/SHA와 설정을 독립 확인했다. ARM64, min24/target36, code26/1.0.5이며 control은 debuggable=false다. 두 APK 모두 공식 sample App ID를 사용했다.
- Samsung Note20 Ultra 5G, Android 13/API33, ARM64, **4KB 페이지** 실기기에서 실행했다. 16KB 기기 증거가 아니다.
- control: 시작 managed SDK idle, 명시적 Load/Show도 UMP·Initialize·Load 호출 없이 PolicyBlocked, 보상0.
- sample: 시작 managed SDK idle. 명시적 Load에서 Update(34317.977) → update 성공(34318.507) → 필요한 폼 처리(34318.507) → 요청 허용(34318.541) → 별도 광고 태그 설정/Initialize(34318.551) → 초기화 callback(34319.366) → Load(34319.372) → 성공(34321.137). 숫자는 callback 단조 시각(초)이다. 실제 폼/개인정보 옵션 UI는 나타나지 않았으며 성인·EU 폼 검증으로 해석하지 않는다.
- 중복 표시: request1 표시 중 예약된 request2는 accepted=false. request1만 Rewarded 완료1/보상1을 기록했다.
- 표시 중 Home/복귀: request3의 pause 후 기존 task 복귀에서 native closed callback을 받았고 Cancelled 완료1, 보상 증가0, BGM 복원. 광고가 계속 재생된 결과로 기록하지 않는다.
- 보상 전 X/확인창 닫기: request4 Cancelled 완료1, 추가 보상0. 최종 보상1/완료3, BGM 재생을 확인했다. opened→closed callback 6.333초는 닫기 UI 출현 시각이 아니다.
- 양쪽 테스트 패키지는 uninstall Success 및 pm path 빈값으로 제거를 확인했다. 해당 원격 녹화 파일도 삭제했다. 원시 로그·영상·기기 식별자는 비공개 로컬에만 보관한다.

### 원본 영상의 닫기 시간 판정

영상은 harness가 보이는 PTS 0부터 시작한다. 원본 프레임 번호는 1부터 세며 재표본화하지 않은 PTS를 사용했다.

| 관찰 | 이전 프레임 PTS | 최초 관측 프레임 PTS |
| --- | --- | --- |
| native 테스트 광고 라벨/검은 배경 | 67: 2.061578초 | 68: 2.086656초 |
| 완성된 Flood-It! 소재 화면 | 92: 2.294300초 | 93: 2.313278초 |
| 닫기 X | 335: 7.067378초 | 336: 7.090611초 |

가장 이른 native 광고 표시는 라벨/검은 배경으로 잡았다. 이 기준 관측차는 **5.003955초**이며 프레임 사이의 출현 불확실성을 포함하면 약 **4.980722–5.029033초**다. 따라서 이번 영상만으로 정확한 5초 이하 통과 또는 명확한 초과를 단정하지 않는다. 뒤늦게 완성된 소재 화면이나 opened callback으로 기준을 옮겨 통과 처리하지 않았다. X의 실제 터치/확인창/보상 없는 종료는 별도 조기 취소 실행으로 확인했다.

운영 소재의 닫기 적합성, 실제 지역별 동의/개인정보 옵션 UI, native 실패 callback, 16KB 실기기는 미검증이다. 운영 광고 차단을 해제하지 않았고 Families 전체 통과를 선언하지 않는다.

통합 담당자가 두 APK verifier를 독립 실행했고, 영상 경계 프레임 67/68·92/93·335/336도 독립 열람하여 위 시간 판정에 동의했다.
