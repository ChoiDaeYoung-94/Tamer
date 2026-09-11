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

현재 확인한 대상 연령은 9–12 / 13–15 / 16–17이며 중립적 연령 확인 또는 모든 이용자에 대한 아동 취급의 운영 타당성을 확정하지 않았다. 동의 SDK 추가만으로 연령·지역 정책 결정이나 Families 준수가 완료되지 않는다. 실제 메시지 게시, 아동 연령 설정 및 운영 ID 적용은 별도 검토 대상이며 이번 작업에서 변경하지 않는다.

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
