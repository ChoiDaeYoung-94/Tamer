# 혼합 대상의 선택형 Google 로그인 보완안 — 2026-11-02

상태는 **사용자 승인 후 구현·제품 Editor96/96·소스 추출22/22·Android release C#·Editor 화면2비율, 별도 x86 Android의 실제 UI/PlayerPrefs 저장·새 프로세스 동일 ID 복원 확인 완료**입니다. 제품 구현 체크포인트는 `9f9324eee24d89cd2feb69b4f58de1c64d65a986`, x86 진단 실행 소스는 `0754b25`(격리33/33·APK1회)이며 [검증 범위와 남은 조건](guest-entry-validation-20261102.ko.md)을 따릅니다. 새 ARM64 네 변형의 시작 차단 원인은 미확정으로, 운영 ARM64/16KB·정상 Play 제공·실제 인증/서버 저장은 남습니다. 실제 운영 계정·서버·신고는 변경하지 않았습니다. 후보30에는 이 손님 보완이 없습니다.

첫 화면·선택 토큰은 기존 계정/진행·불명확한 이력·삭제/receipt-only·기존 생성 대기 상태를 거부합니다. 손님 ID와 custom-pending을 성공적으로 저장한 뒤 기존 CustomID 로그인으로 진행하며, 저장이 불확실하면 프로세스가 끝날 때까지 인증을 막습니다. 새 ID는 기존 기기 식별자와 무관한 무작위 GUID입니다. 새 손님/기존 Custom 방식에는 managed PGS 활성화와 Android 기기 ID 읽기를 수행하지 않습니다. 자동 PGS 프로필 생성 억제의 공식 manifest 항목도 추가했습니다. 이 소스 변경은 기존 AAB30에 들어 있지 않으며 실제 SDK 통신/정책 승인 결과가 아닙니다.

## 로그인 후 선택적 SDK 보고 보완 — 21:31 KST

설치 SDK의 `PlayFabDeviceUtil.SendDeviceInfoToPlayFab`은 서버가 `GatherDeviceInfo`를 요청하고 `DisableDeviceInfo=false`이면 `PlayFabDataGatherer`를 생성합니다. 이 생성자는 Android에서 `Settings.Secure.android_id`를 읽는 `PlayFabSettings.DeviceUniqueIdentifier`를 포함합니다. 따라서 로그인 요청에서 기기 ID를 쓰지 않는 CustomID 경로도 선택적 보고까지 자동으로 차단한 것은 아닙니다. 이는 소스에서 확인한 가능 경로이며 실제 운영 전송 관측이나 새 정책 거절은 아닙니다.

`RunLoginAsync`의 네트워크 대기/인증 전에 `DisableDeviceInfo`와 `DisableFocusTimeCollection`을 켜고 두 값의 적용을 확인하도록 보완했습니다. 연령 창은 Main/Game에 있으므로 모든 일반 Player 로그인에 적용하며 성인도 이 선택적 보고를 다시 켜지 않습니다. Editor/기존 오프라인 삭제 fixture는 제외합니다. 개인 설정 에셋·서버 설정/타이틀, 기존 인증 방식/ID·서버 저장·구매/No Ads는 바꾸지 않았습니다. 기존 기기 기반 계정의 인증 식별자를 이전하는 변경은 포함하지 않습니다.

실제 새 로그인 메서드/흐름을 가짜 UI/auth/설정 경계에 연결한22조건과 SDK 보고 메서드의 가짜 식별자/요청 경계9조건을 통과했습니다. 보고 차단이 없을 때 식별자 읽기/요청이 발생하는 음성 대조와 차단 후 정적/instance API·이용시간 전송 없음, 적용 실패의 거부를 검사했습니다. 첫 SDK probe의 namespace alias 누락1건은 수정 후 통과했습니다. 이 단계의 새 Editor/Android 전체 Player 컴파일·실제 SDK/계정/네트워크 실행은0이며 이전96/96·68 assemblies·x86 동선은 이 수정 전의 근거로 구분합니다. 최종 제공판의 실제 통신 확인과 모든 공급자 데이터 정합은 계속 남습니다.

PGS 프로필 생성 억제는 플랫폼 인증 전체의 중지를 뜻하지 않습니다. [Google의 PGS v2 안내](https://developer.android.com/games/pgs/android/android-signin)는 시작 시 자동 인증을 설명합니다. 캐시된22.0.0 AAR의 native provider 등록/초기화와 Unity 플러그인의 명시 초기화 코드도 읽었지만 이를 운영 패킷이나 Google 무통신 증명으로 계산하지 않습니다. 공식 데이터 보안10개 수집/6개 공유를 이 보완만으로 줄이거나 저장하지 않습니다.

## 확인한 문제와 근거

Google [API Services User Data Policy의 Child-directed apps](https://developers.google.com/terms/api-services-user-data-policy#child-directed-apps)는 혼합 대상 앱에서 Google 로그인을 선택 기능으로 제공하고 Google 계정 로그인 없이 앱 전체를 사용할 수 있어야 한다고 설명합니다. 어린이의 감독 계정도 부모 승인으로 Play Games 프로필을 만들 수 있다는 [Family Link 안내](https://support.google.com/families/answer/7106960?hl=en)는 이 선택 요구를 대신하지 않습니다.11월2일 공식 원문을 읽었습니다.

현재 신고의 마지막 관측은9~12·13~15·16~17·18+ 혼합 대상입니다. 실제 빌드 소스`0c5d9047108b6dccbd6ae3f6229b910811c948b0`의 [Android 로그인](../../Assets/Scripts/Login/Login.cs)은 다음과 같습니다.

- 저장된 Android/Custom 방식은 해당 기존 계정의 복원 경로로 유지됩니다.
- 저장 방식/이력이 없는 최초 Android 실행은 `LoginOnAndroidAsync`에서 Google 인증을 시도합니다. 인증 신원이 없으면 `MissingSelectedIdentity`로 재시도 화면에 머무릅니다.
- `SelectDeviceMode`와 CustomID API 코드는 있으나 최초 Android 화면에 사용자가 Google 없이 시작하는 선택은 없습니다. 인증 실패를 장치 계정 생성으로 자동 전환하지 않는 보호는 의도된 기존 경계입니다.
- [연령 창](../../Assets/Scripts/UI/AgeChoicePresenter.cs)은 Main/Game에서 열립니다. 광고 gate의 미성년 차단만으로 로그인 선택 문제를 해결했다고 볼 수 없습니다.

이는 공개 소스와 공식 정책의 대조입니다. 새 거절 메일을 받은 사실이나 후보30을 실제 Google 미인증 계정으로 실행한 결과가 아닙니다. 사용자가 손님 플레이 구현·격리 검증을 승인해 위 최신 소스에 반영했습니다. 아래 표는 승인한 설계와 후속 운영 완료 조건입니다.

## 권장하는 변경 범위

신고의 혼합 대상을 유지하면서 **새 사용자에게 Google 없이 시작하는 손님 플레이를 명시적으로 제공**하는 안입니다. 손님은 제한된 데모가 아니라 기존 게임·서버 저장·상점·연령/개인정보·삭제 동선에 접근하도록 합니다. Google 로그인은 선택 항목으로 유지하며 기존 게임 계정을 자동 연결/합병/이전하지 않습니다.

| 사용자 상태 | 제안하는 동작 |
| --- | --- |
| 정상 로드된 빈 최초 상태 | Google로 계속 / Google 없이 시작을 선택. 선택 전 managed Google 인증이나 새 서버 계정 생성을 시작하지 않음 |
| 사용자가 Google 없이 시작을 선택 | 서버 저장용 무작위 CustomID와 pending 상태를 먼저 기기에 보존한 뒤 그 한 ID로 PlayFab 로그인/새 계정 생성. 신규 손님 식별에 Android 기기 ID나 광고 ID를 사용하지 않음 |
| 기존 Google·Android·Custom 계정 | 원래 선택과 정확한 소유 계정을 계속 사용. 실패 시 재시도/복구 안내, 자동 손님 전환·새 계정 생성 없음 |
| 계정 소유 불명·로드 실패·기존 진행/구매 흔적 | 신규 손님 제안으로 기존 자료를 덮지 않고 현재 복구/차단 흐름 유지 |
| 불확실한 생성 결과·중단 후 재시작 | 보존한 같은 ID/pending 상태로만 조회·필요 재시도. 다른 ID를 생성하거나 다른 방식으로 fallback하지 않음 |
| 삭제·receipt-only·세션 교체 중 | 기존 자동 재인증/재생성 차단과 세대 보호 유지. 삭제 후 새 시작을 제공할 경우에도 별도 명시 의사와 새 소유 상태가 필요하며 자동 허용하지 않음 |

UI 초안은 `Google로 계속` / `Google 없이 시작`이며, 손님 선택에는 서버 저장용 게임 계정이 생성됨과 기기 변경/앱 삭제 후 복구 제한을 짧게 알립니다. 손님의 다른 기기 자동 복원이나 Google 계정 연결을 구현 전부터 약속하지 않습니다. 기존 계정 실패 뒤 손님을 기본 선택하거나 성인 선택을 유도하지 않습니다.

Google을 선택하지 않아도 계정 기능에 필요한 PlayFab 처리는 발생합니다. Google/광고 데이터 전송 없음이나 모든 SDK 처리 없음이라고 안내하지 않습니다. 기존 No Ads 권한·영수증 검증/복원·다른 계정 간 권한 이전 보호를 유지하고, 원래 구매를 취소/회수해 시험하지 않습니다.

## 기술 입력과 함께 확인할 사항

현재 Play Games Unity 플러그인은2.2.1, 실제 AAB의 Android PGS는22.0.0입니다. 공식 [Unity PGS 안내](https://developer.android.com/games/pgs/unity/unity-start#prevent-auto-triggered-profile-creation)는2.1.0 이상에서 `com.google.android.gms.games.SUPPRESS_GAME_PROFILE_CREATION=true`로 자동 프로필 생성 창을 억제하고 명시 인증 시점에 프로필을 만들 수 있다고 설명합니다. 후보30 manifest에는 이 항목이 없습니다.

승인 후 이 공식 설정과 새 최초 화면을 연결했습니다. 플래그를 자동 PGS 인증·모든 데이터 처리 차단으로 해석하거나 provider를 임의 제거하지 않습니다. 기존 Google 프로필 사용자의 실제 로그인과 프로필이 없는 사용자의 비강제 진입은 각각 후속 확인합니다. SDK 버전 업그레이드나 기존 계정 자격/서명 설정 변경은 이 안에 포함하지 않습니다.

PlayFab [LoginWithCustomID](https://learn.microsoft.com/en-us/rest/api/playfab/client/authentication/login-with-custom-id?view=playfab-rest)는 앱이 만든 고유 ID로 로그인하며 `CreateAccount`가 계정 생성 여부를 정합니다. 실제 운영 타이틀에서 해당 생성이 허용되는지는 미검증입니다. 이 설계 때문에 운영 API를 호출하거나 타이틀 보안 설정을 바꾸지 않습니다. 기존 `LoginWithDeviceAsync`의 Android ID 자동 선택을 신규 손님 경로로 그대로 전용하지 않습니다.

## 구현·검증 완료 조건

1. 순수 선택 정책/가짜 저장·인증 어댑터로 빈 첫 상태·기존 소유/진행·알 수 없는 이력·삭제·pending·오래된 callback·중복 클릭을 검사합니다. 신규 생성은 명시 손님 선택과 하나의 고정 신원에서만 가능합니다.
2. 기존 로그인 연속성·계정/저장·삭제·No Ads 관련 시험 중 영향을 받는 범위만 실행합니다. 운영 SDK/계정/구매를 자동 시험하지 않습니다.
3. 승인된 Unity6000.3.25f1에서 컴파일/관련 Editor 시험을 직렬 실행하고 원본·키·Library와 보호 입력을 복구합니다. 격리 앱에서 실제 최초 선택과 실패/재시작 UI를 확인합니다.
4. 새로운 최종 후보는 현재 Console 번호를 새로 확인해 준비합니다. 기존 AAB30과 소진한 서명 계획은 보존하며 수정된 코드가 들어 있다고 표시하거나 같은 산출물을 덮지 않습니다.
5. 실제 제공물에서 기존 계정 연속성 및 Google 없이 전체 게임 접근·서버 저장·기존 구매/No Ads 경계·연령별 광고/개인정보를 필요한 범위로 확인합니다. 인증·나이 입력은 기존 사용자 직접 조작 규칙을 유지합니다.
6. App access의 Google/손님 두 진입 안내와 연령별 동선, Data safety의 계정 생성 방식/수집 목적, 공개 정책·삭제 안내·스토어 문구를 최종 제공판에 맞춥니다. 이 문서는 공식 신고 저장·최종 심사·출시 승인이 아닙니다.

혼합 대상 정책의 로그인 선택 경계에 대한 보완안이며, 이 기능만으로 Families 전체 준수나 최종 심사 통과를 보장하지 않습니다. [전체 잔여](remaining-recovery-work.ko.md)의 운영 데이터/SDK·실기기·UI·스토어·최종 출시 검토는 유지합니다.
