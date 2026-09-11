# Families 후속: 명시적 테스트 광고의 동의 확인

2026-09-11, branch `codex/families-consent-gate`, base `18d6e48`. UI 수명 리팩토링 후 이어지는 #91 작업이다. 운영 광고는 계속 차단하며 Console/AdMob 설정을 저장하지 않는다.

## 구현 범위

`AdConsentGate`는 Unity/Google SDK에 의존하지 않는 단일 요청 상태 관리이고, `GoogleUmpConsentClient`가 설치된 UMP 4.0.0 API를 연결한다. 시작이나 일반 release의 차단 요청에서 UMP를 만들거나 호출하지 않는다. 허용된 환경의 첫 명시적 sample Load/Show 요청에만 다음 순서로 실행한다.

1. UMP `Update`에 `TagForUnderAgeOfConsent=true`를 전달한다.
2. 성공하면 `LoadAndShowConsentFormIfRequired`를 호출한다.
3. 오류가 없고 UMP `CanRequestAds()`가 true여야 Mobile Ads 요청 설정 및 초기화로 진행한다.
4. Mobile Ads에도 별도로 아동 태그 true / 동의 연령 미만 true / 최대 등급 G를 적용한다.
5. 초기화 이후 Load/Show도 현재 동의 요청 가능 상태를 확인한다.

이 true 태그는 기존의 보수적 테스트 취급이며 사용자 나이를 추정하거나 수집·저장한 결과가 아니다. 아동에게 동의를 요구하기 위한 구현도 아니다. UMP의 TFUA=true는 동의 요청을 억제하며, 이 값은 광고 SDK로 자동 전달되지 않는다. [Google UMP GDPR 안내](https://developers.google.com/admob/unity/privacy/gdpr)

SDK callback은 기존 manager queue에서 처리한다. 중복 요청/콜백은 한 번만 완료되며 파괴된 소유자의 늦은 callback은 무시한다. update/form 오류는 이전 세션의 허용값이 있더라도 보수적으로 차단하고 사용자의 다음 명시적 요청에서 다시 확인한다. 이는 Google 안내의 이전 세션 허용값 사용 가능성보다 엄격한 테스트 정책이다. 앱 자체 consent 문자열/PlayerPrefs 캐시 또는 동의 reset을 사용하지 않는다.

개인정보 옵션이 Required이면 harness에 진입점을 표시한다. 옵션을 열기 전에 로드된 광고를 폐기하며, 옵션 종료 후 광고를 자동 요청하지 않는다. 이 진입점은 운영 UI 연결 완료를 의미하지 않는다. SDK 폼이 열려 있는 동안 기존 광고 로드 타임아웃으로 폼 완료를 가정하거나 새 폼을 중복 열지 않는다.

## 운영 적용 전에 필요한 결정과 근거

Google은 앱 실행마다 consent 정보를 갱신하고 필요한 폼과 개인정보 옵션을 제공한 뒤 요청 가능 여부를 확인하도록 안내한다. 현재는 운영 광고가 차단된 테스트 경로라 첫 명시적 요청까지 갱신을 지연한다. 운영 활성화 시에는 시작 흐름·접근 가능한 실제 설정 UI·대상 연령 처리·게시 메시지와 함께 다시 검토해야 한다. [공식 UMP 설정 절차](https://developers.google.com/admob/unity/privacy)

현재 확인한 대상 연령은 9–12 / 13–15 / 16–17이며 중립적 연령 확인 또는 모든 이용자에 대한 아동 취급의 운영 타당성을 확정하지 않았다. 동의 SDK 추가만으로 연령·지역 정책 결정이나 Families 준수가 완료되지 않는다. 실제 메시지 게시, 아동 연령 설정 및 운영 ID 적용은 별도 검토 대상이며 이번 작업에서 변경하지 않는다.

AdMob의 현재 읽기 결과는 [실기기 검증 문서](families-device-validation.ko.md)의 2026-09-11 세션을 따른다. 승인 계정 접근, 보상형 단위 1개, 단위에 운영 미디에이션 0개/캠페인 0개, 계정 목록 AdMob 기본 그룹을 확인했다. 개인정보 메시지는 새 메시지 만들기로 표시됐으며 게시 근거는 확인하지 못했다. 과거 문서의 가입 화면 차단은 당시 세션 관찰이다.

## 5초 닫기와 실기기 후속

Families 정책은 아동 또는 나이 미상 이용자의 정상 사용을 방해하는 광고에 대해 보상형/선택형도 5초 후 닫을 수 있어야 한다고 명시한다. UMP/아동 태그는 닫기 UI 시간의 증거를 대신하지 않는다. [Families 광고 형식 요구](https://support.google.com/googleplay/android-developer/answer/9893335)

이전 sample 영상은 시작 프레임부터 광고가 보여 native 첫 화면 기준점이 없었다. 약 5초 구간 X 및 보상 전 취소를 확인했지만 정확한 5초 판정과 운영 소재 적합성은 계속 미입증이다. 다음 녹화는 harness 화면부터 시작하는 원본 프레임과 프레임 시간 기준을 확보하고, native 첫 광고 프레임·첫 닫기 UI·확인 창·실제 종료를 구분한다. 프레임 오차와 샘플 소재 한계를 유지한다.

미검증인 표시 중 중복 요청을 안전하게 실행하도록 harness에 `duplicate show` 예약 동작을 추가했다. 광고 위를 두 번 터치하는 대신 opened 이후 Unity 쪽에서 같은 요청 API를 한 번 더 호출한다. 표시 중 Home 복귀와 native 실패 callback은 별도 실제 관찰로 구분하며 주입 callback을 native 관찰로 표기하지 않는다.

## 검증

순수 fake client 회귀는 시작 시 무호출, update→form→요청 가능 순서, TFUA 전달, 중복 callback/요청, 오류와 SDK 예외, 소유자 파괴, 수동 재시도 후 이전 callback, 개인정보 재선택을 다룬다. SDK API 서명은 로컬 UMP DLL에서 확인했다. Editor/샘플·대조 APK/기기 결과는 실제 실행 후 이 문서와 별도 JSON에 기록한다.
