# No Ads 서버 영수증 검증: 테스트 전용 구현

## 현재 범위

Unity `6000.0.81f1`, IAP `5.4.3`, PlayFab `2.242.260805`를 유지한다. 기존 `IAPManager()`는 Unity 주문을 신뢰하는 현재 동작을 유지한다. 새 생성자에 `IReceiptVerifier`와 세션 공급자를 명시적으로 주입할 때만 서버 검증을 사용한다. **배포·운영 활성화·실제 구매/복원은 아직 수행하지 않았다.**

기존 `GooglePlay` CSV의 `ProductNoAds` 권한과 `TryGrantNoAds()`의 영속 저장 후 `ConfirmPurchase` 순서를 보존한다. 네트워크 검증 실패를 기존 권한 회수로 사용하지 않는다. 앱의 기존 로컬 권한/Client UserData 구조 전체를 서버 권위 구조로 바꾸는 작업도 아니다.

현재 로그인은 GPGS에서 확인한 ID를 기존 PlayFab 이메일 계정과 연결하는 호환 경로 및 기존 device/custom 경로다. `LoginWithGooglePlayGamesServices`로 자동 이행하지 않는다. 계정 연속성과 인증 방식 강화는 데이터 담당과 별도 검증한다. Codeless 카탈로그는 존재하지만 자동 초기화는 꺼져 있다.

## 구현 계약

1. 명시적 테스트 composition root가 승인된 HTTPS `/v1/no-ads/verify` URL과 세션 공급자를 넣는다. 공급자는 `IsServerDataReady`, PlayFab 로그인 상태, DataManager의 ID와 인증 context ID가 일치할 때만 `ReceiptSession`을 반환해야 한다. 공유 Managers/Data/Server 파일은 이번 변경에서 수정하지 않는다.
2. 구매 직전에 `SHA256("tamer-iap-v1:" + PlayFabId)`를 Google obfuscated account ID로 설정한다. 서버는 클라이언트가 보낸 ID를 신뢰하지 않고 PlayFab `AuthenticateSessionTicket` 결과의 ID를 사용한다.
3. 서버는 고정 package/SKU, 실제 Google `purchases.productsv2.getproductpurchasev2` 응답의 PURCHASED, 테스트 카드, 계정 binding, 단일 영구 상품·수량 1·미소비·미환불 수량 1을 검사한다. SDK receipt의 서명 문자열 자체를 검증했다고 주장하지 않으며 Google 서버 조회가 진위 판단 근거다.
4. 허용된 테스트 계정만 받을 수 있다. SQLite 트랜잭션으로 token SHA-256을 단일 계정에 귀속시키고 영속 기록한 뒤 성공을 응답한다. 같은 계정 재시도는 중복 기록하지 않으며 재시도마다 Google 상태를 다시 조회한다. 원시 token/ticket/receipt를 DB에 저장하지 않는다.
5. 클라이언트는 검증 시작 시 실제 DataManager 소유자와 세션을 함께 캡처하며 HTTPS 성공 응답의 nonce·계정·SKU를 대조한다. 비동기 대기 중 계정/세션 교체, IAP store 계정 교체, manager 종료 또는 주문 객체 교체가 있으면 늦은 응답을 적용하지 않는다. 검증 성공 후 저장 직전에 현재 DataManager가 캡처한 동일 인스턴스인지, 준비 상태·계정 ID·세션이 모두 일치하는지 다시 확인하고 캡처한 소유자에게만 로컬 영속 저장, 이후 pending 확인을 수행한다. confirmed 복원도 검증을 거친다.

서버 확인/권한 기록이 실패하면 새 권한과 acknowledgement가 모두 보류된다. No Ads는 비소비 상품이므로 consume API를 호출하지 않는다. 이 구현의 서버는 acknowledgement/환불/계정 데이터 쓰기를 호출하지 않고, 확인은 기존 Unity IAP 경로가 담당한다. 오래된 binding 없는 구매의 최초 귀속은 자동 허용하지 않으며 기존 저장 권한은 유지한다.

PlayFabId와 token 해시도 계정·거래에 연결되는 식별자이며 익명 데이터가 아니다. opt-in 활성화 전에 접근 통제와 보관·삭제 정책 및 개인정보 고지를 확정해야 한다.

## 로컬 재현

2026-09-11 main `f4405c80`을 포함한 소스 `4b7b06388c9a8655ccadc77e7a0459e84f309a55`에서 Unity EditMode **299/299**(신규 receipt 16개)를 확인했다. Python **88/88**(서버 13개)은 직전 통합 소스 `11c3ac9`에서 실행했으며 이후 C# 취소 테스트의 예외 타입 조건만 수정했다. 에셋 복원 4,561개를 확인했고, YAML GUID 132개 미해결 0은 최초 구현 소스 `50ec779`의 검사다. `Run-Baseline -TestsOnly` 종료 0이며 APK는 새로 빌드하지 않았다. [정확한 검증 데이터](receipt-verification-validation.json)

`python -m unittest discover -s tools/revival -p test_receipt_server.py`는 합성 ticket/receipt와 임시 DB만 사용한다. 실제 서비스 호출·소켓 리스너·계정 생성이 없다. Unity `RevivalReceiptVerificationTests`는 지연 성공/거절/오류/세션 교체/취소/HTTPS 구성 및 저장→확인 계약을 검증한다.

서버 host는 `Upstream(title_id, secret_key, access_token_provider)`, `Ledger(private_db_path)`, `Verifier(..., package, test_accounts)`, `create_app(verifier)`를 조합한다. `access_token_provider`는 서버의 ADC/workload identity 또는 비밀 저장소를 사용해 androidpublisher OAuth token을 반환해야 한다. **실제 host/자격 증명 주입/배포 구성은 미제공**이며 import만으로 서버나 네트워크가 시작되지 않는다. WSGI factory 자체는 TLS·rate limit·관리자 인증·가용성 운영 계층을 제공하지 않는다.

독립 리뷰에서 세션 공급자는 이전 계정 A를 반환하지만 실제 저장 대상이 새 DataManager B일 수 있는 문제를 보완했고, 지급·확인 차단 회귀를 추가했다. 첫 통합 실행 298/299의 한 실패는 취소 예외의 정확한 타입을 기대한 테스트 문제였다. 하위 `TaskCanceledException`도 허용하도록 수정한 뒤 299/299를 확인했다. 이전 실행 XML은 비공개 경로에 보존했다.

## 실제 테스트 전에 필요한 구체적 설정

- 소유자가 승인한 별도 PlayFab 테스트 타이틀/계정 목록과 서버 전용 secret 접근. 회원 목록을 탐색하거나 운영 비밀키를 클라이언트에 넣지 않는다.
- Google Play에 등록된 테스트 package와 동일한 설치 package, 활성 No Ads 테스트 상품, license tester 계정, 해당 계정으로 로그인된 전용 테스트 기기/프로필. 현재 `.revival` smoke 성공은 이 구성을 증명하지 않는다. 기존 운영 앱을 덮어쓰지 않으려면 별도 등록된 테스트 package 또는 별도 기기가 필요하다.
- Google Play Developer API 활성화 및 해당 앱 구매 조회에 필요한 서비스 계정 권한. Google 공식 문서의 금융 데이터 조회/주문 관리 권한은 범위가 넓을 수 있으므로 앱 단위로 소유자가 검토한다. 신규 credential 생성/권한 부여는 아직 하지 않았다.
- 별도 HTTPS staging host, secret 주입, 영속 DB/백업/동시성/보관·삭제 정책, TLS/요청 크기/rate limit, request body·ticket·upstream token URL 로그 제외. SQLite 구현은 단일 host용이며 여러 host 배포는 공유 DB 트랜잭션 설계가 필요하다.
- 승인 후 테스트 카드 결제→검증→영속 저장→확인, 앱 재시작 복원, 통신 실패 재시도, 세션 전환, 거절·pending·환불을 확인한다. 실제 과금 선택지 또는 운영 계정이 보이면 진행하지 않는다. 운영 배포와 상품/계정 설정 변경은 별도 단계다.

## 남은 보안·호환 범위

테스트 전용 검증 구현은 운영 부정결제 방지 완료가 아니다. 기존 무결합 구매 migration, 서버 권위 entitlement 조회/게임 적용, RTDN/voided purchase 환불 재조정, 서버 acknowledgement와 로컬 durable 계약 통합, Play Integrity/abuse 방어, iOS 검증은 별도다. 기존 계정 인증 강도와 Client UserData 쓰기 권한도 서버 구매 진위 확인만으로 강화되지 않는다.

16KB 실행도 별도다. 현재 폰은 4KB이며 기존 에뮬레이터 두 실행은 부팅 실패했다. 현실적인 다음 선택은 실제 ARM64 Android 15/16의 16KB 기기를 확보하거나, Windows 하이퍼바이저/BIOS 가상화 상태를 소유자와 확인한 뒤 필요한 Windows 기능 변경·재부팅을 승인받는 것이다. 기존 UAC/Windows 기능/재부팅을 자동 재시도하지 않는다. Unity 6.3 전환은 현재 필수 조건으로 판단하지 않는다.

## 공식 근거 (2026-09-11 확인)

- [Google 구매 검증·계정 결합·중복 방지](https://developer.android.com/google/play/billing/security)
- [Google ProductPurchaseV2 응답 및 상태](https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.productsv2)
- [Google 구매 조회 endpoint](https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.productsv2/getproductpurchasev2)
- [PlayFab 서버 세션 인증](https://learn.microsoft.com/en-us/rest/api/playfab/server/authentication/authenticate-session-ticket?view=playfab-rest)
- [Google license testing의 package·계정 조건](https://developer.android.com/google/play/billing/test)
- [Google API/서비스 계정 설정](https://developers.google.com/android-publisher/getting_started)
- [PlayFab Economy v2 redemption 대안](https://learn.microsoft.com/en-us/xbox/playfab/economy-monetization/economy-v2/marketplace/marketplace-redemption/google): 별도 catalog/marketplace 구성과 inventory 이행이 필요하므로 기존 CSV entitlement를 자동 교체하지 않았다.
