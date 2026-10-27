# UMP 최초 개인정보 표시 오류의 범위와 추가 계측

2026-10-27 기준입니다. 과거 privacy-only 시험의 첫 오류는 SDK 사전 로딩 중인 상태와 연결됐지만 **일반 동의 경로 전체의 결함이나 해결 완료로 판정하지 않습니다.** 이번 변경은 다음 필요한 실제 후보에서 원인을 구분할 선택적 로그뿐입니다.

## 확인한 원인과 한계

일반 `AdConsentGate.Request`는 Update 성공 뒤 Gather/`LoadAndShowConsentFormIfRequired`를 호출합니다. 과거 cold fixture는 Gather를 의도적으로 제외하고 명시 개인정보 클릭의 Update→fresh privacy Required→`ShowPrivacyOptionsForm`만 시험했습니다. 그 결과 첫 code3 실패 뒤 실제 `load_complete`, 같은 owner의 명시 재진입과 native 선택·callback 성공을 확인했습니다. [당시 실제 결과](privacy-cold-explicit-retry-20261024.ko.md)와 [원래 제품 UI warm 시험](privacy-product-real-ump-20261024.ko.md)을 구분합니다.

[Google Android UMP 계약](https://developers.google.com/admob/android/reference/privacy/com/google/android/ump/UserMessagingPlatform)은 개인정보 폼을 자동 사전 로딩하며 로딩 중이거나 아직 표시할 수 없으면 오류 callback이 가능하다고 설명합니다. `PrivacyOptionsRequirementStatus.Required`는 진입점 필요 상태이며 폼 준비 완료 신호가 아닙니다. [Unity API](https://developers.google.com/admob/unity/reference/class/google-mobile-ads/ump/api/consent-form)와 v11.6.0 공개 bridge에서 앱이 사용하는 별도 privacy preload-ready API는 확인하지 못했습니다. 기존 오류 안내와 사용자 명시 재시도는 유지합니다.

[ConsentStatus](https://developers.google.com/admob/android/reference/privacy/com/google/android/ump/ConsentInformation.ConsentStatus)는 별도 상태입니다. `Required`는 필요한 응답이 아직 없는 상태이고 `Obtained`는 응답을 얻었다는 뜻이며 모든 목적·개인화 허용을 뜻하지 않습니다. 과거 시험에는 이 값이 기록되지 않았습니다. 따라서 당시 초기 동의 API를 썼으면 오류가 사라졌을 것이라고 단정하거나, 이미 Obtained인 사용자의 재선택을 표시 없이 끝날 수 있는 초기 API로 바꾸지 않습니다. 임의 타이머·polling·자동 재표시·동의 reset·승인 true 전환을 추가하지 않았습니다.

## 이번 계측 변경

`GoogleUmpConsentClient`의 기존 cold/product 관측 define 안에서만 `status_read` 한 event에 `consent_status`를 덧붙입니다. 기존 privacy 결정 읽기·매핑·callback·Show/Gather 경로와 최종 `sdk_sequence`는 보존합니다.

```text
status_read value=Required consent_status=Obtained sdk_sequence=3
```

위 값은 **형식 예시**이며 이번 실제 SDK 관측값이 아닙니다. 보조 getter가 실패하면 `consent_status=ReadFailed`만 기록하고 원래 개인정보 결과·예외에 영향을 주지 않습니다. 일반 운영 branch에는 새 읽기가 없습니다. `statusReads`는 기존 privacy 결정 읽기 수이며 추가 getter까지 포함한 모든 SDK getter 수라는 뜻이 아닙니다.

기존 cold/retry/product parser의 prefix·trailing-space 판정·sequence suffix·owner/ordinal과 알 수 없는 event 거부를 보존합니다. 과거 필드가 없는 로그는 **미기록**이며 `Unknown`으로 소급하지 않습니다.

## 검증

- 실제 adapter 전체 소스와 inert SDK로 관측9개 시나리오34항목, 일반 branch1개 시나리오2항목을 확인했습니다. 네 상태·privacy 매핑·보조 읽기 실패·원래 읽기 예외·observer 예외와 Update/Gather/Show 미호출을 대조했습니다.
- 현재 로컬 GMA Unity11.6.0 UMP DLL을 참조한 C# 컴파일1회가 통과했습니다. 실제 SDK 실행이나 Android 빌드가 아닙니다.
- 첫 일반 branch probe는 빈 `CompilerOptions`를 거부한 PowerShell wrapper 설정 실패로0개 시나리오만 실행됐습니다. 인자를 생략하도록 보완하고 그 미실행 branch만 확인했으며 성공한 관측 branch는 반복하지 않았습니다.
- 독립 읽기 검토에서 해당 소스 delta와 기존 parser 호환에 확정 미해결 P1/P2가 없었습니다. 정확한 source/로그 SHA와 실패·후속 실행을 비공개 원장에 보존했습니다.

이 필드만을 위해 같은 Android 시험·빌드를 반복하지 않았습니다. 다음 실제 후보가 원래 필요할 때 상태와 같은 SDK 오류·preload/명시 재진입을 연결합니다. 운영 최초 진입·실제 구매/No Ads·연령/지역·전체 공급자 선택·운영 서명 AAB/Play는 여전히 별도 범위입니다. [전체 복구 잔여](remaining-recovery-work.ko.md)와 [#419](https://github.com/ChoiDaeYoung-94/Tamer/issues/419)는 유지합니다.
