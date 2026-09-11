# PlayFab 내장 No Ads 검증 연결

`PlayFabIapReceiptVerifier`는 별도 테스트 타이틀과 `.iaptest` 패키지, 명시한 Legacy catalog만 받는다. 매 요청마다 `PlayFabClientInstanceAPI`의 독립 설정·세션 context를 만들며 운영 `PlayFabSettings.staticSettings/staticPlayer`를 변경하지 않는다. 기본 `Managers` 구매 구성에는 아직 연결하지 않았다.

호출자는 테스트 타이틀에서 로그인한 현재 계정/티켓을 `ReceiptSession`으로 제공하고 기존 `IAPManager(verifier, currentSession)` 생성자에 주입한다. 타이틀 ID 문자열 차이 검사는 원격 리소스의 실제 격리 증명이나 계정 바인딩 검증을 대체하지 않는다. 배포 전에 패키지·타이틀·카탈로그와 계정 세션을 함께 확인한다.

GooglePlay receipt의 원문 `Payload.json`과 `Payload.signature`를 `Client.ValidateGooglePlayPurchase`에 전달한다. 성공 응답의 지정 catalog/No Ads item이 유효할 때만 검증을 승인한다. `ReceiptAlreadyUsed`는 성공으로 간주하지 않고 **같은 캡처 세션**의 `GetUserInventory`에서 지정 catalog의 유효한 No Ads item을 확인한다. inventory 지연·오류·권한 없음은 지급하지 않으며 기존 IAP 재시도로 복구한다. 계정 교체/세션 갱신/취소 이후 결과는 기존 `ReceiptVerification`과 실제 저장 소유자 검사에서 차단한다.

No Ads SKU는 `com.aedeong.monstertamer.no_ads`, 상품은 `NonConsumable`을 유지한다. 원격 지급 확인 → 내구성 있는 로컬 저장 → Unity `ConfirmPurchase` 순서를 유지하며 Google consume 호출을 추가하지 않는다. SDK 요청 자체는 취소되지 않으므로 취소 후 원격 검증이 완료될 수 있다. 늦은 결과를 로컬 지급에 쓰지 않고 다음 재시도에서 원격 inventory로 복구한다.

이 경로는 Legacy Economy의 검증/지급/중복 방지를 사용한다. Python staging의 Google productsv2 `TEST` 조건·서버 allowlist·obfuscated account binding 검사와 동일하다고 주장하지 않는다. 서명 전 JSON 필드 비교는 입력 필터이며 신뢰 근거는 PlayFab 검증 응답이다. 실제 타이틀의 Google add-on, catalog, 별도 앱/라이선스 테스터와 실기기 검증이 필요하다.

전용 PlayFab 타이틀은 새 플레이어 네임스페이스로 생성했으며 개발 모드/초기 플레이어 0명과 무료 시작 안내를 확인했다. 운영 타이틀의 설정·계정을 재사용하지 않는다. 실제 ID/Console 링크는 공개 코드 대신 작업 인계에서 관리한다. Google Play 미공개 무료 테스트 앱 생성에 필요한 정책/수출법 선언은 사용자가 승인했다. 앱·상품 준비 및 실제 검증 상태는 후속 검증 기록으로 확정한다.

참고: [PlayFab API 계약](https://learn.microsoft.com/en-us/rest/api/playfab/client/platform-specific-methods/validate-google-play-purchase?view=playfab-rest), [Google 구매 지급·승인 순서](https://developer.android.com/google/play/billing/integrate).
