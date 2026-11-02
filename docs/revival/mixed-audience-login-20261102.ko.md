# 혼합 대상의 선택형 Google 로그인 보완안 — 2026-11-02

상태는 **제품 결정 대기·설계안**입니다. 로그인/계정 생성 코드·실제 계정·서버·신고를 변경하지 않았습니다. 후보30의 서명/기본 검사와 이번 정책 경계의 해결 여부를 구분합니다.

## 확인한 문제와 근거

Google [API Services User Data Policy의 Child-directed apps](https://developers.google.com/terms/api-services-user-data-policy#child-directed-apps)는 혼합 대상 앱에서 Google 로그인을 선택 기능으로 제공하고 Google 계정 로그인 없이 앱 전체를 사용할 수 있어야 한다고 설명합니다. 어린이의 감독 계정도 부모 승인으로 Play Games 프로필을 만들 수 있다는 [Family Link 안내](https://support.google.com/families/answer/7106960?hl=en)는 이 선택 요구를 대신하지 않습니다.11월2일 공식 원문을 읽었습니다.

현재 신고의 마지막 관측은9~12·13~15·16~17·18+ 혼합 대상입니다. 실제 빌드 소스`0c5d9047108b6dccbd6ae3f6229b910811c948b0`의 [Android 로그인](../../Assets/Scripts/Login/Login.cs)은 다음과 같습니다.

- 저장된 Android/Custom 방식은 해당 기존 계정의 복원 경로로 유지됩니다.
- 저장 방식/이력이 없는 최초 Android 실행은 `LoginOnAndroidAsync`에서 Google 인증을 시도합니다. 인증 신원이 없으면 `MissingSelectedIdentity`로 재시도 화면에 머무릅니다.
- `SelectDeviceMode`와 CustomID API 코드는 있으나 최초 Android 화면에 사용자가 Google 없이 시작하는 선택은 없습니다. 인증 실패를 장치 계정 생성으로 자동 전환하지 않는 보호는 의도된 기존 경계입니다.
- [연령 창](../../Assets/Scripts/UI/AgeChoicePresenter.cs)은 Main/Game에서 열립니다. 광고 gate의 미성년 차단만으로 로그인 선택 문제를 해결했다고 볼 수 없습니다.

이는 공개 소스와 공식 정책의 대조입니다. 새 거절 메일을 받은 사실이나 후보30을 실제 Google 미인증 계정으로 실행한 결과가 아닙니다. 신규 계정 기능의 선택 방식은 사용자 결정 후 구현합니다.

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

승인 후 이 공식 설정과 새 최초 화면을 연결하는 범위를 검토합니다. 플래그를 자동 PGS 인증·모든 데이터 처리 차단으로 해석하거나 provider를 임의 제거하지 않습니다. 기존 Google 프로필 사용자의 실제 로그인과 프로필이 없는 사용자의 비강제 진입은 각각 확인합니다. SDK 버전 업그레이드나 기존 계정 자격/서명 설정 변경은 이 안에 포함하지 않습니다.

PlayFab [LoginWithCustomID](https://learn.microsoft.com/en-us/rest/api/playfab/client/authentication/login-with-custom-id?view=playfab-rest)는 앱이 만든 고유 ID로 로그인하며 `CreateAccount`가 계정 생성 여부를 정합니다. 실제 운영 타이틀에서 해당 생성이 허용되는지는 미검증입니다. 이 설계 때문에 운영 API를 호출하거나 타이틀 보안 설정을 바꾸지 않습니다. 기존 `LoginWithDeviceAsync`의 Android ID 자동 선택을 신규 손님 경로로 그대로 전용하지 않습니다.

## 구현·검증 완료 조건

1. 순수 선택 정책/가짜 저장·인증 어댑터로 빈 첫 상태·기존 소유/진행·알 수 없는 이력·삭제·pending·오래된 callback·중복 클릭을 검사합니다. 신규 생성은 명시 손님 선택과 하나의 고정 신원에서만 가능합니다.
2. 기존 로그인 연속성·계정/저장·삭제·No Ads 관련 시험 중 영향을 받는 범위만 실행합니다. 운영 SDK/계정/구매를 자동 시험하지 않습니다.
3. 승인된 Unity6000.3.25f1에서 컴파일/관련 Editor 시험을 직렬 실행하고 원본·키·Library와 보호 입력을 복구합니다. 격리 앱에서 실제 최초 선택과 실패/재시작 UI를 확인합니다.
4. 새로운 최종 후보는 현재 Console 번호를 새로 확인해 준비합니다. 기존 AAB30과 소진한 서명 계획은 보존하며 수정된 코드가 들어 있다고 표시하거나 같은 산출물을 덮지 않습니다.
5. 실제 제공물에서 기존 계정 연속성 및 Google 없이 전체 게임 접근·서버 저장·기존 구매/No Ads 경계·연령별 광고/개인정보를 필요한 범위로 확인합니다. 인증·나이 입력은 기존 사용자 직접 조작 규칙을 유지합니다.
6. App access의 Google/손님 두 진입 안내와 연령별 동선, Data safety의 계정 생성 방식/수집 목적, 공개 정책·삭제 안내·스토어 문구를 최종 제공판에 맞춥니다. 이 문서는 공식 신고 저장·최종 심사·출시 승인이 아닙니다.

혼합 대상 정책의 로그인 선택 경계에 대한 보완안이며, 이 기능만으로 Families 전체 준수나 최종 심사 통과를 보장하지 않습니다. [전체 잔여](remaining-recovery-work.ko.md)의 운영 데이터/SDK·실기기·UI·스토어·최종 출시 검토는 유지합니다.
