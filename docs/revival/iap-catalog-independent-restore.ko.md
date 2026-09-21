# 상품 조회 실패와 기존 구매 복원 분리

2026-09-21, `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`의 소스 `f3928aa56fc8c3426902aa94603c569d75c8efe1`에서 검증했다. Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, IAP `5.4.3`, Android target/NDK `27.2.12479018` 기준이며 이번에는 APK/AAB를 만들지 않아 신규 SHA-256이 없다.

기존 `IAPManager.OnProductsFetched`는 No Ads가 목록에 없으면 `FetchPurchases` 전에 반환했고 상품 조회 실패도 같은 결과였다. 수동 `RestorePurchases` 역시 신규 구매 준비 완료를 요구했다. 따라서 스토어 연결이 살아 있어도 상품 조회 실패가 기존 구매 조회 시도를 막았다.

이제 상품 조회 결과가 비어 있거나 실패해도 연결 상태라면 기존 주문 조회를 시도한다. 수동 복원은 연결을 요구하되 상품 판매 가능 여부를 요구하지 않는다. 초기 조회·구매·복원이 진행 중이면 중복 복원이나 초기화 재시도를 막는다. 신규 구매는 계속 상품 조회와 주문 조회 성공을 모두 요구한다. 주문 검증·영수증 확인·내구 저장 후 Confirm 경로는 변경하지 않았다. 신규 판매 중단이나 Console 상품 변경을 포함하지 않는다.

설치된 SDK 소스의 `PurchaseService.TryFetchPurchases`는 스토어 연결을 검사한 후 `FetchPurchasesUseCase`에 위임하며, 상품 조회 성공을 선행 조건으로 검사하지 않는다. [Unity 초기화 문서](https://docs.unity.com/en-us/iap/set-up-in-app-purchasing)와 [복원 문서](https://docs.unity.com/en-us/iap/restore-purchases)를 함께 대조했다. 공개 문서가 설명하는 이전 SDK의 metadata 미매칭 누락 제한과 별개로, 설치된 5.4.3의 `GooglePurchaseConverter`는 캐시/역조회로 상품을 식별하지 못하면 `ProductType.Unknown`을 사용할 수 있다. 이 수정은 **조회 시도 차단을 해소**하며 모든 상품 장애에서 복원이 성공한다고 보장하지 않는다. Unknown·혼합 SKU·잘못된 수량을 임의 No Ads 권한으로 승격하지 않고 기존 저장 권한도 회수하지 않는다.

검증은 네트워크를 시작하지 않는 합성 `IPurchaseService`를 실제 `StoreController` 전달 메서드에 주입했다. 빈 목록/상품 조회 실패 후 소유 주문 조회, 신규 구매 비활성 유지, 판매 준비 없는 수동 복원, 조회·복원 중복 방지 및 조회 예외 후 재시도 가드 해제를 확인했다.

- 1차 `RevivalIAPOrderTests|RevivalIAPFulfillmentTests`: 31/31 통과, 실패/skip 0.
- 이후 복원 중 초기화 재시도 차단을 추가한 최종 소스에서 `RevivalIAPOrderTests`: 18/18 통과, 실패/skip 0, 0.0442391초. 변경이 있는 주문 테스트만 재실행했다.
- 최종 XML: 비공개 `Logs/revival/iap-restore-catalog-final-0921.xml`, SHA-256 `2e4445feef207a0a1e4c62614cdc16b1c41a2864351a8cd168dab5ba2a28e7ea`.
- 자산 검증 4,561개/복사 0, 실행 전후 ProjectSettings snapshot 복원, 종료 후 해당 checkout Editor PID 0, `git diff --check` 통과.

CLI `test`의 절대 project 인수와 `--mode EditMode --filter RevivalIAPOrderTests --editor-version 6000.0.81f1`을 사용하고 `-buildTarget Android`를 전달했다. 실제 스토어 상품 미반환·실패 상황의 기기 재현, 새 구매·독립 acknowledgment·16KB 실행은 이번 검증 범위가 아니다. OAuth 자격 환경, 미보유 승인 테스터 및 관리자 BCD 판독 결과의 추가 제공은 확인되지 않았으며 기존 미해결 조건을 유지한다.
