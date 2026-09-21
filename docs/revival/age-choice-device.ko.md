# 연령 선택 Android UI 검증

## 검증 구성

2026-09-21, 병합된 PR #184의 실제 Android 화면과 기기 로컬 저장을 확인한다. 기존 `RevivalGameplayHarness`와 원본 Login/Main/Game/NextScene을 재사용하며 `agechoice` 빌드 모드만 추가했다. 자동 전투를 실행하지 않고 원본 Main에서 선택·설정 UI를 직접 조작한다.

- 별도 패키지 `com.AeDeong.MonsterTamer.revival.agechoice`. 기존 playerrestore/운영 앱을 업데이트하거나 데이터를 삭제하지 않는다.
- INTERNET/ACCESS_NETWORK_STATE/BILLING/AD_ID 및 광고 초기화 provider 제거, debug 서명, 자동 백업과 cloud/device-transfer 저장 복원 제외.
- 정확한 전용 패키지/비Editor 실행과 legacy 키 부재를 확인한다. 키가 존재하면 읽기·삭제하지 않고 중단한다. 로그인·IAP·광고는 기존 하네스 guard로 차단하고 서버는 합성 메모리 transport다.
- 이 검증의 재시작 보존 대상은 연령 PlayerPrefs다. 게임 fixture 저장은 매 실행 별도 경로를 사용하는 기존 하네스 방식이므로 실제 계정 게임 저장 복원 검증이 아니다.
- checkout `C:/Users/pc_17/.codex/worktrees/e5e5/Tamer`, 실행 소스 `4216b9cec39aa337000ad32bea60ddf7bebb6088`.
- Unity 6000.0.81f1 / CLI 1.0.0-beta.8 / Android SDK build-tools 36.0.0 / ARM64 / min24·target36.
- `RevivalGameplayIsolationTests` 19/19, Python APK 검사기 4/4 통과. 이전 PR #184의 68개 통과 테스트는 반복하지 않았다.

## 연령·동의 계약 독립 확인

2026-09-21 공식 문서를 재확인했다. 광고 요청의 `AgeRestrictedTreatment`는 SDK 초기화 전에 설정하며 Child/Teen/Unspecified를 구분한다. 기존 광고 TFCD/TFUA와 함께 지정하면 더 보수적인 설정이 적용된다. 이 API 존재와 실제 연령·지역 매핑 승인 여부를 구분한다. [Unity targeting](https://developers.google.com/admob/unity/targeting)

Child는 개인화/리마케팅·제3자 광고 vendor 요청을 제한하며 AAID/IDFA를 전송하지 않는다고 명시되어 있다. Teen은 개인화/리마케팅 제한과 청소년 보호를 제공하지만 이 문서의 Child용 식별자 미전송 보장을 Teen에 그대로 확대하지 않는다. Unspecified도 성인 인증이나 개인화 동의가 아니다. [AdMob 연령 처리](https://support.google.com/admob/answer/6219315)

UMP의 `TagForUnderAgeOfConsent=true`는 동의 폼 요청을 하지 않게 하는 별도 입력이며 동의를 얻었다는 뜻이 아니다. UMP는 이를 광고 SDK에 자동 전달하지 않으므로 광고 요청의 연령 보호 설정이 별도로 필요하다. 동의 철회는 개인정보 옵션 경로이며 연령 재선택으로 대체하지 않는다. [UMP GDPR](https://developers.google.com/admob/unity/privacy/gdpr)

현재 지역별 처리, 게시 메시지 및 광고 5초 종료 해결안은 미확정이다. `AgeTreatmentPolicy.IsReviewed=false`와 운영 광고 차단은 유지한다. 본 오프라인 UI 검증 결과로 지역 동의 폼·실제 광고·구매·스토어 또는 Families 전체 준수를 주장하지 않는다.

## 첫 기기 실행과 후속 회귀 실패

APK 105462423 bytes, SHA-256 `2fad6fe2fbf005875deccc630c74ff05fdb394e9db7bfc39ebd95821dee52668`. 빌드와 최종 APK 격리/서명/백업 검사는 통과했다. LOAD/ZIP도 통과했으며 별도 RELRO 끝 정렬 5개 실패는 유지한다.

SM-N986N / Android 13 / user 0에서 전용 패키지 부재를 확인하고 최초 설치했다. 원본 Main에 진입하고 로그인·광고 차단/메모리 서버 준비 마커와 오류 0을 확인했다. 그러나 최초 연령 화면이 전체 화면을 채우지 못하고 설명·버튼이 좁게 겹쳐 **UI 실패 1회**로 판정했다. 선택하지 않고 앱을 중지했으며 연령 키 부재와 저장 파일을 보존했다.

Manager prefab 자체가 Canvas인데 자식 연령 Canvas의 RectTransform이 기본 100×100으로 남는 것이 첫 원인이다. 수정안은 부모 전체 stretch와 표시 순서 override다. 실제 Canvas 부모를 포함하도록 보강한 Editor 회귀는 modal 1080×1920 검사 후 `canvas.overrideSorting == true` 검사에서 **0/1 실패**했다. 처음에는 stack trace 61행을 버튼 너비 검사로 잘못 해석했으며, 아래 승인 후 측정에서 이를 정정했다.

기기 실패와 종속 회귀 실패를 보수적으로 2회 경계로 보고 추가 수정·빌드·기기 실행을 중단하여 담당자에게 보고했다. 수정 APK와 두 번째 기기 실행은 아직 없다. 구간 선택 저장·프로세스 재실행·설정 재선택·응답 거절 유지는 **기기 미검증**이다. 기존 68개 Editor 회귀 통과가 기기 레이아웃 성공을 보장하지 않음을 기록한다.

실패 APK·화면·로그·연령 선택 전 PlayerPrefs·XML은 비공개 로컬에 보존한다. 공개 [증거 manifest](age-choice-device-validation.json)에는 경로와 해시만 기록한다. 전용 앱을 중지하고 설치/데이터를 보존했으며, 다른 앱과 계정은 변경하지 않았다. 본인 Editor 종료·설정/폰트 복원 후 Editor와 폰 슬롯을 반환했다.

## 사용자 재시도 승인 후 측정과 보고 정정

사용자가 명시적으로 재시도를 승인해 진단을 재개했다. 이전 두 실패는 그대로 보존했다. 승인 후 첫 실행은 같은 `overrideSorting` 검사에서 실패했다. 이때 NUnit Progress 출력은 원본 결과에 남지 않았다.

두 번째 실행에서는 Unity 로그로 다음 실제 치수를 확보했다: modal **1080×1920**, content **907.2×1459.2**, 버튼 5개 너비 각각 **907.2001**, LayoutGroup enabled/active 모두 true. 따라서 버튼 너비가 좁다는 기존 Editor 실패 해석은 잘못이었다. 기존 파일의 61행은 `overrideSorting` assertion이고 너비 assertion은 62행이다. 최초 기기 APK의 100×100 문제와 stretch 수정 후의 정상 너비를 구분한다.

이어 동일 객체를 일반 Editor Scene과 비교하려던 진단은 `Cannot create a new scene additively with an untitled scene unsaved` 예외로 끝났다. 이 예외는 UI 치수 실패가 아니라 테스트 환경 제약이다. 승인 후 두 실패에 도달해 추가 실행을 중단했다. 표시 순서 설정의 활성화 시점이 미해결이며, 이를 수정했다고 아직 주장하지 않는다. 기기 두 번째 실행·수정 APK 빌드는 여전히 수행하지 않았다.

## 추가 사용자 승인 후 수정과 현재 중단 상태

표시 순서 적용을 `_modal.SetActive(true)` 직후로 옮긴 소스 `b9b3f00b895072edf080a9e104589fcc48e72dd4`의 Editor 회귀는 1/1 통과했다. 기존 assertion을 유지하고 재열기 표시 순서도 확인했다. 일반 씬 생성 진단을 제거하고 테스트 소유 Preview Scene만 생성·정리했다. 사용자 미저장 씬을 저장하거나 삭제하지 않았다.

이 APK의 기기 화면은 전체 Canvas와 버튼 배치가 정상이었으나 안내 TMP 문장이 줄바꿈 없이 가장자리로 넘쳐 승인 후 실패 1회로 기록했다. 명시적인 Normal 줄바꿈과 LayoutElement min/preferredWidth=0을 적용하여 부모 폭을 따르도록 수정했다. 소스 `08a8fefe63b989e9c89ae3f5fa2c07f91a830203`의 최소 Editor 회귀 1/1이 통과했다. 이 검사는 자식 Rect 폭 검사이며 실제 glyph 가독성은 아래 기기 관찰과 구분한다.

최종 APK는 144739418 bytes, SHA-256 `91086547b51c6ae6a194314c28e9f074fa1b82097d3532a41720ffc47b7a57d6`이다. 기존 APK와 debug 서명 일치, 정확 패키지, INTERNET/ACCESS_NETWORK_STATE/BILLING/AD_ID와 MobileAdsInitProvider 제거, 백업/복원 제외를 독립 검토했다. ACCESS_ADSERVICES 계열 권한까지 모두 제거됐다는 의미는 아니다. LOAD/ZIP 통과, 별도 RELRO 끝 정렬 5개 실패는 유지한다. 이전 대비 APK 크기 증가는 대부분 ZIP entry 사이 공간이며, 압축된 entry 총량 차이는 약 1.5KB였다. 증분 패킹 잔여라는 원인은 추정으로 남긴다.

동일 패키지를 `install -r`로 업데이트했고 저장 파일은 업데이트 전후 byte 단위 동일했다. 최종 기기 화면에서 안내 문구 전체 줄바꿈·가독성·동일한 다섯 선택지를 확인했다. `13~15세` 선택 후 질문이 닫혔으며 원시 XML 값 `1%7C13to15`가 생성됐다.

로컬 검사 스크립트가 원시 XML 값을 `1|13to15`와 직접 비교해 assertion 실패를 냈다. 이 검사기 해석 오류도 승인 후 두 번째 실패로 계수하라는 담당자 지시에 따라 중단했다. 승인 전 percent-decode 수정·재검증·앱 재시작을 하지 않았다. **정규화된 저장값 검증, 프로세스 재실행, 설정 재선택, 응답 거절 보존은 미완료**다. 정상 화면과 선택 닫힘 관찰만을 저장 roundtrip 전체 성공으로 확대하지 않는다.

현재 전용 앱은 중지했고 설치/데이터를 보존했다. Editor는 종료·설정 복원 상태이며 슬롯을 반환했다. 이전 네 실패와 이번 두 실패 및 보고 정정 이력을 모두 유지하며 PR은 Draft다.
