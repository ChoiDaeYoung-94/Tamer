# 손님 진입의 심사·데이터 안내 준비안 — 미저장

손님 플레이는 [승인된 구현](mixed-audience-login-20261102.ko.md)에 연결하는 후속입니다. **이 안내를 Console에 저장하거나 공개 정책/시행일을 교체한 문서가 아닙니다.** 손님 선택이 없는29/30과 달리 [후보31은 내부 제공·정상 Play 설치까지 확인](internal31-live-20261103.ko.md)했지만 LD 첫 실행이 흰 화면이어서 운영 동선·심사 계정 권한은 미검증입니다. [후보32 운영 검증 순서](candidate32-operating-validation-20261103.ko.md)와 같이 실제 제공 화면/계정·연령별 광고를 확인한 뒤 아래 문구를 적용합니다.

Google의 [심사 로그인 세부정보 요건](https://support.google.com/googleplay/android-developer/answer/15748846?hl=en-en)은 재사용 가능한 접근과 Google 등 외부 계정을 쓰는 경우의 구체적인 안내를 요구합니다. [심사 준비 도움말](https://support.google.com/googleplay/android-developer/answer/9859455?hl=en)에 따라 App access의 추가 안내를 맞추되, 현재 저장된 전용 심사 계정의 비밀값을 이 문서에 복제하지 않습니다. 손님 추가만으로 기존 심사 접근 신고를 ‘모든 기능에 특별 접근 불필요’로 바꾸지 않습니다.

## 적용 전에 실제로 확인할 값

| 확인 | 현재 상태 |
| --- | --- |
| 새 제공판에 Google/손님 선택이 있음 | 소스·격리 검사/후보31 서명 생성·패키지 문구/내부 제공 완료. 실제 운영 화면/입력은 미검증 |
| 운영 타이틀에서 신규 손님 계정과 전체 게임·서버 저장이 가능 | 미검증. 이번 승인은 구현·격리 검사이며 실제 운영 계정을 생성하지 않음 |
| 기존 심사 계정의 반복 로그인·지역/추가 인증·유료 기능 접근 | 사용자가 전용 계정을 입력/추가하고 전체 접근 가능 회신. 실제 최종 게임 로그인은 미검증 |
| 해당 계정의 No Ads 보유 여부 | 사용자 ‘확인 필요’ 회신. 아직 확정하지 않음 |
| 선택적 PlayFab 기기정보/이용시간 보고 차단 | 일반 Player 로그인 전에 두 SDK flag를 적용하는 소스 보완/가짜 경계22+9 및 새 Editor96/96·일반 Android scripts68개 컴파일 확인. 실제 제공판 통신 대조 전이며 PGS/다른 SDK 무통신이나 공식 신고 유형 감소로 계산하지 않음 |
| 연령 차단 자체를 검사할 광고 가능 계정 | No Ads가 없는 계정으로 미성년/성인 둘 다 대조해야 함. No Ads만으로 광고가 안 나온 결과와 구분 |
| 성인 동의/광고·미성년 및 응답하지 않음 동선 | 최종 제공판에서 새로 확인. 광고 표시 차단과 모든 SDK 처리 중지는 별개 |
| 메뉴/문구 | 소스 기준 설정 톱니 → `Account & privacy` → `연령대 다시 선택` / `광고 개인정보 설정 확인`. 실제 제공 화면과 최종 대조 |

## App access 추가 안내 영문 초안

아래는 새 최종 제공판 확인 후 사용할 본문 후보입니다. 대괄호로 빈 비밀번호를 넣거나 저장된 값을 복사하지 않습니다. Google 경로의 자격 정보는 이미 마련한 Play Console의 전용 입력란에서 관리합니다.

> Google sign-in is optional for a new player. On a fresh installation with no saved game account, select “Google로 계속” (Continue with Google) or “Google 없이 시작” (Start without Google).
>
> For the Google route, use the dedicated review account supplied in the sign-in fields. Existing game accounts keep their original sign-in method. A failed sign-in does not automatically create a guest account or transfer saved progress or purchases.
>
> The guest route creates a server-backed game account without requiring Google game sign-in. It uses the same game features and server-save flow. Reopening the app on the same installation resumes that account. Recovery of progress and purchases after app removal or a device change may be limited. Use a fresh review test device/profile for this route if another game account is already saved; preserve that existing account.
>
> After entering the game, the age field is blank. Enter an age and confirm, or select “응답하지 않음” (Prefer not to answer). To change it, open the gear icon, “Account & privacy”, then “연령대 다시 선택” (Choose age again). This stores an age group on the device, not the entered age or date of birth.
>
> Users under 18, users of unknown age, and users who decline to answer do not receive ads and can continue playing. Test the under-13, 13–15, and 16–17 groups separately. For the adult ads route, use an account without No Ads, enter 18 or above, complete any privacy choices offered for the region, and use the optional rewarded-buff point in the village. Ads may be unavailable because of consent, connectivity, or ad inventory; this does not block normal gameplay.
>
> An account that owns No Ads receives the applicable buff without playing an ad. That account cannot demonstrate the adult ad-playback route. Privacy options are available through “광고 개인정보 설정 확인” (Check ad privacy settings).

영문 초안의 손님 전체 기능·재시작·광고 지점/상태는 아직 운영 실행 근거가 없으므로 현재 그대로 공식 저장하지 않습니다. 실제 광고 지점의 화면 설명과 심사 계정의 No Ads 정보를 확인해 보완합니다. 신규 결제·기존 권한 회수·계정 데이터 삭제로 시험 조건을 만들지 않습니다. 인증·나이 입력은 기존 사용자 직접 조작 규칙을 유지합니다.

## 공개 개인정보·Data safety 보완 후보

- 로그인·계정 문구: **신규 이용자는 Google Play Games로 계속하거나 Google 없이 손님 게임 계정을 만들 수 있습니다. 손님을 선택하면 앱이 만든 무작위 사용자 지정 식별자를 기기에 보관하고, 게임 계정 인증과 서버 진행도 저장에 사용합니다. 이 신규 손님 식별자는 Android 기기 식별자나 광고 ID를 바탕으로 만들지 않습니다. 기존 Google·기기·사용자 지정 방식의 계정은 해당 복원 경로를 유지합니다. 앱 삭제·기기 변경 뒤에는 손님 게임 진행과 구매 권한 복구가 제한될 수 있습니다.**
- 공급자/기능 문구: **Google 없이 시작해도 게임 계정·서버 저장을 위한 PlayFab 처리와 사용 기능에 따른 서비스 처리가 발생합니다. Google 로그인 선택과 광고 연령/개인정보 설정은 별개입니다.** Google/광고·모든 SDK 데이터 처리 없음으로 안내하지 않습니다.
- 계정 생성 방식은 현재 저장한 `OAuth + 기타`와 대조합니다. 기타 설명 후보는 **Google Play Games 인증 또는 사용자가 명시적으로 선택한 무작위 사용자 지정 식별자 기반 게임 계정. 기존 기기 로그인 계정은 원래 방식으로 복원**입니다. 새 손님에게 Android 기기 ID를 쓴다고 쓰지 않습니다.
- 무작위 손님 ID도 계정 인증·진행 귀속을 위해 기기 밖으로 전송되므로 익명·개인정보 수집 없음으로 신고하지 않습니다. Google [Data safety 안내](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en)의 유형/목적에 맞춰 기존 사용자 ID 항목과 공급자 처리를 최종 대조합니다. 현재 수집10/공유6 답안을 이 문구만으로 추가·삭제하지 않습니다.
- 11월2일 재확인한 Google [Families 데이터 지침](https://support.google.com/googleplay/android-developer/answer/11043825?hl=en)은 어린이/나이 미상의 AAID 등 전송 제한과 시작 시 기본 차단, 어린이 대상 미승인 API/SDK를 필수 로그인으로 요구하지 않는 조건을 설명합니다. 새 무작위 손님 ID는 광고/기기 식별자를 읽지 않는 계정 인증 경로이지만, 이 소스 분리만으로 PlayFab·PGS·모든 SDK의 어린이 데이터 처리 적합성을 판정하지 않습니다. 최종 제공물의 SDK 시작/전송과 실제 정책 신고를 함께 확인합니다.
- 기존 삭제 URL·소유 확인·접수와 공급자 삭제 완료의 구분을 유지합니다. 손님도 동일한 삭제 동선에 접근하는지 최종 제공판에서 확인합니다. 기존 Google 계정으로 자동 연결/병합하거나 다른 계정의 No Ads를 옮기는 기능은 추가하지 않았습니다.

[미게시 공개 정책 초안](privacy-policy-release-draft.ko.md), [기존 공식 Data safety 저장 범위](data-safety-saved-20261029.ko.md), [전체 잔여](remaining-recovery-work.ko.md)를 함께 적용합니다. 실제 처리와 제공판을 확인한 뒤 공식 신고/정책을 구체화하며 최종 심사 전송·프로덕션 출시는 별도입니다.
