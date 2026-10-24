# 저장 연령·합성 No Ads와 UMP 재시작 관측

2026-10-24 source `c9ce5759c60457b648b73a1f637b6f7dfa800cfe`, 실제 checkout `D:/codex/worktrees/tamer-working/Tamer`에서 별도 개발 앱을 빌드했습니다. 실제 GoogleAdMobManager의 공유 privacy refresh와 Google UMP adapter에 시험 전용 계측을 붙인 결과입니다. 운영 광고·지역·성인·개인정보 승인은 활성화하지 않았습니다.

## 산출물과 사전 확인

| 항목 | 값 |
| --- | --- |
| Unity / CLI | `6000.3.25f1` / `1.0.0-beta.8` |
| Android | min25 / target36 / ARM64 / IL2CPP / debug 서명·development |
| 패키지 | `com.AeDeong.MonsterTamer.revival.privacycold` |
| 진입점 | `RevivalAdHarnessBuild.BuildUmpPrivacyColdIsolated` |
| APK | `Build/revival/Tamer-ads-ump-publisher-privacy-age-pgs-isolated-privacy-cold-no-backup.apk` |
| SHA-256 / 크기 | `2d4477407f39529a8b9acc5496662c877e3b3c2495207cd067015b77b3c40aee` / 98,633,571bytes |

LDPlayer `Tamer`에 새 패키지로 설치했고 설치 base 해시가 일치했습니다. min/target/ABI/debug 서명, native PGS provider·PGS app ID 부재, publisher 입력 일치, allowBackup=false를 검사했습니다. UMP용 INTERNET과 SDK의 다른 권한·provider는 남아 있습니다. 기기 간 transfer 규칙과 네이티브 초기화 전체 부재는 검증하지 않았습니다. ZIP의 Pipeline 이름 entry 부재도 최종 바이너리의 HTTP 코드 제거 증거가 아닙니다.

게임 Managers·Server·IAP·PGS를 생성하지 않고 관리 코드의 광고 초기화/로드/표시 소유 상태가 비어 있음을 관측했습니다. 이 사실을 SDK 내부 네이티브 동작·통신 전체0으로 확대하지 않습니다. No Ads는 package 내부 문자열 `ProductNoAds`를 실제 공용 `AdEntitlement.HasNoAds`로 해석한 **합성 입력**이며 실제 구매나 영수증 복원이 아닙니다.

## 네 프로세스의 결과

각 단계는 명시적 개발 fixture API로 실행했습니다. 시작 때 Adult/No Ads를 자동 기록하거나 SDK 요청을 자동 실행하지 않습니다. 명령은 현재 token/PID/증가 sequence/동일 저장 fixture ID를 확인하며, 실제 연령 UI나 네이티브 폼 선택을 자동 조작하지 않습니다.

Adult 갱신에는 등록된 같은 패키지의 SDK 시험 hash와 고정 EEA debug geography를 사용했습니다. 실제 이용자 지역·연령이나 운영 지역 검토를 증명하지 않습니다.

| 단계 | 실제 관측 |
| --- | --- |
| SDK 등록, PID5122 | 같은 패키지에서 TFUA=true Update1회·성공 콜백1회, Show0. SDK가 출력한 등록값1개를 비공개 보관했습니다. 임의 hash나 과거 휴대폰 값을 사용하지 않았습니다. |
| 합성 상태 준비, PID5518 | 실제 LocalAgeChoice API로 Adult와 합성 No Ads를 저장했습니다. parser=true, Update0·Show0, Managers 없음입니다. |
| cold Adult, PID5753 | 이전 PID 소멸 후 같은 APK/data의 새 프로세스에서 Adult/합성 No Ads를 복구했고 자동 Update0입니다. 명시 요청 후 Update1·성공 콜백1 → 최신 status Required 읽기1 → Show1 → 실제 오류 콜백1 순서를 관측했습니다. |
| 오류 후 저장 | SDK 결과는 **FormFailed / code3**, 사전 로딩 중이라는 native 진단입니다. busy=false와 Required 유지, 합성 No Ads=true였습니다. 실제 오류 콜백이 끝난 뒤 LocalAgeChoice API로 합성 Declined를 저장했습니다. 네이티브 폼에서 거절을 선택한 결과가 아닙니다. |
| cold Declined, PID6180 | 이전 PID 소멸 후 새 프로세스에서 Declined/합성 No Ads를 복구했습니다. 명시 요청은 AgeRequired로 차단됐고 Update0·Show0·Required=false·busy=false였습니다. 관리 광고 소유 상태 없음과 Managers 없음도 유지됐습니다. |

호출 수는 이 adapter의 명시적 UMP API와 그 콜백·상태 결정 읽기 계측입니다. SDK 내부 호출·전체 네트워크 요청 수가 아닙니다. 마지막 정상 종료 지점에서 해당 시험 앱만 중지했고 설치물·저장값·실패/원시 로그를 보존했습니다.

## 폼 로딩 오류의 의미

Google의 [공식 showPrivacyOptionsForm 설명](https://developers.google.com/admob/android/reference/privacy/com/google/android/ump/UserMessagingPlatform#showPrivacyOptionsForm(android.app.Activity,com.google.android.ump.ConsentForm.OnConsentFormDismissedListener))은 폼을 SDK가 미리 로딩하며 그 과정에서는 오류 콜백이 발생할 수 있다고 설명합니다. 이번 로그도 Update 성공→Required→즉시 Show→로딩 오류→뒤이은 load_complete 순서였습니다. **폼 표시 성공으로 채점하지 않았습니다.** 자동 반복 Show·동의 초기화·가짜 콜백으로 통과시키지 않았습니다. 기존 명시적 warm 재열기 성공 이력과도 구분합니다.

최초 로딩 오류 안내와 같은 후보의 실제 폼 관측은 [이슈 #419](https://github.com/ChoiDaeYoung-94/Tamer/issues/419)에서 후속 추적합니다.

## 검사·보호와 남은 범위

- 동일 source의 관련 Editor `RevivalAd;RevivalAgeChoice` **178/178 PASS**, private input/source 검사 Python **6/6 PASS**, Android 전용 분기 컴파일·빌드 성공입니다. 전체 프로젝트 Editor 회귀 통과라는 뜻은 아닙니다.
- 새 조건부 `#error` 가드를 비활성 광고 source 검사기가 읽지 못한 호환 문제를 수정했습니다. 닫힌 조건부 가드만 허용하고 무조건 오류·조건부 gate 선언·알 수 없는 directive 거부 검사를 유지했습니다. 운영/지역 false를 true로 바꾸지 않았습니다.
- 각 실행 전후57개 원본을 보존·대조·복원했습니다. 이 범위에는 기존55개와 비공개 harness scene/meta2개가 포함됩니다. UI 테스트의 동적 폰트/TimeManager 변경도 원본으로 복원했고 최종 Git clean과 Editor 종료를 확인했습니다.
- Publisher 입력은 기존 확인 사실과 현재 source ID2곳의 일치를 바탕으로 새 checkout에 묶은 비활성 시험 입력입니다. 새 Console 읽기·현재 운영 승인·resource/binding 생성이 아닙니다. 원본과 새 입력의 hash·출처는 비공개 기록에 있습니다.
- **미검증:** 네이티브 폼 표시/실제 선택/전체 철회, 실제 구매권한과의 결합, 실제 ARM64·16KB·Play 제공물·운영 정책·지역/성인 승인, 최종 운영 AAB입니다. 화면 캡처는 이 SDK 결과의 픽셀 근거로 사용하지 않았습니다.

제품의 기존 구매권한·No Ads 판매·보상 버프 방식과 기존 계정/저장/키는 변경하지 않았습니다. 이 문서는 관측 경로와 실제 오류 처리·cold 차단 근거이며 운영 활성화나 스토어 정책 통과 승인서가 아닙니다.
