# No Ads 구매 API 이행

추적: [SDK #97](https://github.com/ChoiDaeYoung-94/Tamer/issues/97), [저장 API #100](https://github.com/ChoiDaeYoung-94/Tamer/pull/100). Unity IAP **5.4.3**과 기존 Google Play / Apple 플랫폼 결제를 사용한다. 실제 결제와 운영 계정으로 테스트하지 않았다.

## 권한과 주문 처리

상품 ID `com.aedeong.monstertamer.no_ads`, 비소모품 타입과 기존 `GooglePlay` CSV의 `ProductNoAds` 토큰을 유지한다. Codeless/UGS 자동 초기화는 끄고 기존 Shop 호출이 명시적으로 초기화한다. 구형 `IStoreListener`/`UnityPurchasing.Initialize` 경로는 제거했다.

`PendingOrder`는 `DataManager.TryGrantNoAds()`가 동일 계정의 권한을 원자적으로 저장한 뒤에만 `ConfirmPurchase`로 확인한다. 저장 API는 초기 계정 동기화 전이나 파일 오류에서 false를 반환하며, 권한은 집합 병합하고 cloud에는 변경 키만 예약한다. 로컬 저장 성공과 cloud 전송 성공을 구분한다. 파일 저장 실패는 주문을 미확정 상태로 남겨 재시도한다.

복원된 `ConfirmedOrder`도 동일한 멱등 저장 경로를 사용한다. 지연·실패 주문, 빈 영수증, 거래 ID가 없는 `PendingOrder`, 미등록 상품, 혼합 카트, 잘못된 수량/상품 타입은 권한을 지급하지 않는다. `ConfirmedOrder`에는 거래 ID가 없어도 복원용 키를 허용한다. 확인 실패는 저장된 권한을 지우지 않고 재시도한다. UI 갱신 실패도 이미 저장한 구매를 되돌리지 않는다.

Android가 앱 복귀 때 전달하는 pending 이벤트를 유지한다. SDK의 자동 전달과 명시적 구매 목록 조회 양쪽에서 주문을 받아 동일 세션의 중복 확인을 막는다. 앱 매니저 재생성과 SDK 서비스 캐시의 수명이 달라도 재조회로 미완료 주문을 복구한다. 재시도 루프는 5초마다 돌며, 이미 진행 중인 확인은 60초 동안 중복 전송하지 않는다. 연결/상품/구매 조회 실패 후 초기화를 재시도하되 이전 초기화 시작에서 최소 30초 간격을 둔다. 매니저 파괴 시 이벤트와 재시도 루프를 정리한다.

이는 기존 클라이언트 구매 권한을 보존하는 API 이행이다. **서버 영수증 검증, 환불/취소 반영, 실제 스토어 복원 및 계정 변경은 완료했다고 주장하지 않는다.** SDK가 전달한 결제 완료 주문을 신뢰하며 서버 검증 서비스나 공개 키를 임의로 새로 구성하지 않았다.

## 동의와 데이터 처리

IAP 5.4는 [Developer Data 동의 처리](https://docs.unity.com/en-us/iap/upgrade-to-iap-v5)가 추가된 버전이다. 현재 앱에 동의 UI가 없으므로 UnityConsent의 미지정 Ads/Analytics 목적을 Denied로 설정하고 이미 명시된 선택은 보존한다. IAP용 UGS 자동/직접 초기화는 제거했다.

이 설정이 모든 SDK 네트워크나 데이터 수집을 차단하는 것은 아니다. 5.4.3의 `PurchaseEventEmitter`는 Unity 6000.0에서 별도 구매 이벤트 전송 경로를 갖고 있으며, 진단·구매 처리·PlayFab 저장/로그인과 동의 목적은 각각 검토해야 한다. Data safety 및 개인정보 문서는 [#105](https://github.com/ChoiDaeYoung-94/Tamer/issues/105)에서 실제 동작과 대조한다. 격리 빌드의 시작 씬과 자동 테스트에서는 스토어 생성·로그인·구매를 시작하지 않는다.

## 검증

IAP 테스트 30개는 순수 지급 경계, Codeless catalog, 동의 기본값, 실제 v5 주문 모델, disconnected 재시도 큐를 검증한다. Unity IAP 서비스는 만들지 않고 가짜 영수증·거래 ID와 임시 저장소만 사용한다. 최종 SDK/저장 통합 테스트와 APK 결과는 `sdk-validation.json`에 기록한다.

기기 후속 검증은 Google Play 테스트 계정/테스트 결제로 새 구매, 재설치 복원, 승인 지연 후 복귀, 중복 콜백, 앱 종료 중 미확정 주문, 다른 계정 전환을 확인해야 한다. 운영 결제를 자동으로 실행하지 않는다.
