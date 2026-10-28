# IAP5.4.3 native 경로와 개인정보 근거의 범위

2026-10-28 source `5fb5bf1d001f50747eb7d41c146a92402022d61a`의 앱 코드와 pinned purchasing5.4.3·Core1.18.0·Authentication3.7.4를 읽었습니다. Unity6000.3.25f1 기준입니다. **소스 구성 경로를 확인했으며 실제 Android payload·전송 성공·서버 수집/보관을 측정한 결과는 아닙니다.** 소스/패키지 변경·SDK/Unity 실행·시험·빌드는 없습니다.

## 구분해야 할 경로

| 경로 | 소스에서 확인한 조건 | 확대하지 않을 결론 |
|---|---|---|
| 앱의 상점 초기화 | 활성 `ShopMan.Awake`→`IAPManager.Init`→동의 기본값→`StoreController`→native `Connect` | 새 구매 버튼만 SDK 시작 조건이라는 설명 |
| Google Play 연결 | Store service/use case→GooglePlay connection→BillingClient 연결 | 이 호출 자체가 UGS 초기화·익명 로그인이라는 설명 |
| UGS 구성 | 별도로 초기화되면 IAP callback·기존 identity component/cache·설정 조회 구성이 가능. 현재 codeless 자동 초기화 두 flag는 false | package 등록을 UGS 초기화 완료로, 서비스 생성 자체를 로그인·D2C 결제로 표현 |
| native 구매 Insights | native purchase service도 emitter를 구성. 구매/pending 처리 등 이벤트와 identity component·cache 조건에 따라 envelope/직접 gateway POST 경로 존재 | D2C 미사용·기본 Connect만으로 각각 전체 전송0·모든 필드 송신을 단정 |
| Ads/Analytics intent | Unspecified만 Denied로 정하며 기존 값은 보존. 일부 광고 식별자 조회는 Granted/원격 허용 조건이 필요. Insights 전체의 전송 차단 조건은 아님 | Denied를 모든 SDK·기본 처리 OFF로 표현 |
| 기존 진단 API | pinned Core1.18.0의 `Diagnostics.SendDiagnostic`·Metrics 송신 구현은 no-op. IAP wrapper의 queue/instance 주입과 구분 | wrapper 구성만으로 실제 진단 송신을 확정하거나, no-op을 별도 Insights·엔진/플랫폼 SDK 전체에 확대 |
| 연령 선택 | Main/Game의 광고용 질문은 상점 Awake/일반 인증 전체의 사전 gate가 아님 | 연령 미응답/거절을 IAP·로그인 전송 전체의 차단 근거로 사용 |

현재 Unity 버전에서 구매 Insights는 Unity6000.7 이상 전용 module 경로와 구분한 직접 gateway 경로를 갖습니다. 실제 이벤트·필드값·Android API/요청 성공 여부는 관측하지 않았습니다. Google Billing에서 얻는 country의 로컬 보관과 공식 표의 country 항목, 개별 Insights envelope 필드도 같은 관측으로 합치지 않습니다.

공식 [IAP5.4+ 개인정보 안내](https://docs.unity.com/en-us/iap/privacy-and-consent/overview)와 [Data safety 표](https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety)는 SDK 전체 적용 근거입니다. 실제 native 경로의 구성 조건·사용 기능·최종 제공물을 연결해야 하며, 이 정적 읽기만으로 신고 필드를 없애거나 모든 경로의 실제 수집을 확정하지 않습니다. 구버전5.0.1 적용은 [별도 버전 구분](data-safety-candidate-20261026.ko.md)을 따릅니다.

## 앱 소스 연결과 다음 순서

- [IAPManager](../../Assets/Scripts/Managers/IAPManager.cs): store 구성·Connect·기존 구매 조회 및 일반/복구 전용 pending reroute 차이.
- [동의 기본값](../../Assets/Scripts/Managers/IapConsentDefaults.cs): 기존 Granted/Denied 보존.
- [상점 진입](../../Assets/Scripts/Creatures/ShopMan.cs): 활성 씬에서 Awake 초기화.

이번 읽기에서 즉시 소스 수정이 필요한 확정 결함은 찾지 못했습니다. 같은 성공 시험을 반복하지 않고 다음 필요한 운영 후보의 실제 기능·개인정보 검증에 이 경계를 연결합니다. [전체 잔여 작업](remaining-recovery-work.ko.md)의 운영 자료·서명/제공물·정책/계정/No Ads·실제16KB 미검증 범위는 그대로입니다.
