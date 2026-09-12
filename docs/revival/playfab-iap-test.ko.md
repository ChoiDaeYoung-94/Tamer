# PlayFab 내장 No Ads 검증 연결

`PlayFabIapReceiptVerifier`는 별도 테스트 타이틀과 `.iaptest` 패키지, 명시한 Legacy catalog만 받는다. 매 요청마다 `PlayFabClientInstanceAPI`의 독립 설정·세션 context를 만들며 운영 `PlayFabSettings.staticSettings/staticPlayer`를 변경하지 않는다. `TAMER_IAP_HARNESS` 전용 Managers 구성에만 연결하며 일반 플레이어의 기본 구성은 유지한다.

호출자는 테스트 타이틀에서 로그인한 현재 계정/티켓을 `ReceiptSession`으로 제공하고 기존 `IAPManager(verifier, currentSession)` 생성자에 주입한다. 타이틀 ID 문자열 차이 검사는 원격 리소스의 실제 격리 증명이나 계정 바인딩 검증을 대체하지 않는다. 배포 전에 패키지·타이틀·카탈로그와 계정 세션을 함께 확인한다.

GooglePlay receipt의 원문 `Payload.json`과 `Payload.signature`를 `Client.ValidateGooglePlayPurchase`에 전달한다. 성공 응답의 지정 catalog/No Ads item이 유효할 때만 검증을 승인한다. `ReceiptAlreadyUsed`는 성공으로 간주하지 않고 **같은 캡처 세션**의 `GetUserInventory`에서 지정 catalog의 유효한 No Ads item을 확인한다. inventory 지연·오류·권한 없음은 지급하지 않으며 기존 IAP 재시도로 복구한다. 계정 교체/세션 갱신/취소 이후 결과는 기존 `ReceiptVerification`과 실제 저장 소유자 검사에서 차단한다.

No Ads SKU는 `com.aedeong.monstertamer.no_ads`, 상품은 `NonConsumable`을 유지한다. 원격 지급 확인 → 내구성 있는 로컬 저장 → Unity `ConfirmPurchase` 순서를 유지하며 Google consume 호출을 추가하지 않는다. SDK 요청 자체는 취소되지 않으므로 취소 후 원격 검증이 완료될 수 있다. 늦은 결과를 로컬 지급에 쓰지 않고 다음 재시도에서 원격 inventory로 복구한다.

PlayFab 테스트 catalog 역시 소비 횟수나 만료 시간을 두지 않는 영구 No Ads 권한으로 설정해야 한다. 신규 테스트 타이틀에 `iap-test-v1` catalog와 기존 No Ads SKU를 영구·비누적·거래 불가·RM99 기준가로 저장했다. 신규 Google 앱의 패키지와 RSA 공개 라이선스 키만으로 Google add-on 설치됨을 확인했다. OAuth/서비스 계정 자격 증명은 추가하지 않았다. 이 설정만으로 실제 구매 검증이 완료된 것은 아니다.

이 경로는 Legacy Economy의 검증/지급/중복 방지를 사용한다. Python staging의 Google productsv2 `TEST` 조건·서버 allowlist·obfuscated account binding 검사와 동일하다고 주장하지 않는다. 서명 전 JSON 필드 비교는 입력 필터이며 신뢰 근거는 PlayFab 검증 응답이다. 실제 타이틀의 Google add-on, catalog, 별도 앱/라이선스 테스터와 실기기 검증이 필요하다.

전용 PlayFab 타이틀은 새 플레이어 네임스페이스로 생성했으며 개발 모드/초기 플레이어 0명과 무료 시작 안내를 확인했다. 운영 타이틀의 설정·계정을 재사용하지 않는다. 실제 ID/Console 링크는 공개 코드 대신 작업 인계에서 관리한다. Google Play 미공개 무료 테스트 앱 생성에 필요한 정책/수출법 선언은 사용자가 승인했다. 앱·상품 준비 및 실제 검증 상태는 후속 검증 기록으로 확정한다.

참고: [PlayFab API 계약](https://learn.microsoft.com/en-us/rest/api/playfab/client/platform-specific-methods/validate-google-play-purchase?view=playfab-rest), [Google 구매 지급·승인 순서](https://developer.android.com/google/play/billing/integrate).

## 테스트 리소스 생성 결과

Google Play에서 `Monster Tamer IAP Test`를 `com.AeDeong.MonsterTamer.iaptest` 패키지의 무료 게임(en-US)으로 생성했다. 패키지 사용 가능 여부 확인과 사용자가 승인한 개발자 프로그램 정책/미국 수출법 선언 후 신규 앱 대시보드의 게시 준비 상태를 확인했다. 자동 보호 기본값을 유지했으며 빌드 업로드·테스트 트랙 배포·프로덕션 신청은 수행하지 않았다.

전용 PlayFab `Tamer IAP Test`와 Google 앱, 영구 catalog 및 Google add-on을 준비했다. Google 상품 생성 화면은 결제 권한이 있는 APK 업로드를 선행 조건으로 표시했다. Google 상품/라이선스 테스터와 실제 로그인·구매 검증은 남아 있다.

## 격리 앱 구성

`RevivalIapBuild.BuildAndroid`는 환경 변수 `TAMER_IAP_TEST_TITLE`, `TAMER_IAP_PRODUCTION_TITLE`, `TAMER_IAP_TEST_CATALOG`를 검증하고 빌드 중에만 로컬 Resource를 만든다. 운영 PlayFab 설정과 static 인증을 변경하지 않는다. Login 씬 하나, 전용 앱 ID, debug 서명, Development 옵션과 빌드 한정 `TAMER_IAP_HARNESS`/`TAMER_REVIVAL_SMOKE`를 사용한다. 임시 Resource와 manifest/광고 설정은 finally에서 정리·복원한다. 호출자는 기존 PowerShell settings snapshot으로 Editor 시작 전후 서명 설정도 복원해야 한다.

원래 로그인과 광고 요청은 차단한다. 테스트 로그인은 설치별 임의 CustomID와 신규 타이틀의 독립 instance API를 사용하며, 같은 앱 저장소의 타이틀별 파일에 동일 계정을 유지한다. 다른 계정으로 반환되면 기존 DataManager 소유자 검사가 저장을 거부한다. 게임 데이터 읽기/쓰기는 메모리 서버이고, 영수증 검증/소유 inventory만 실제 테스트 타이틀을 사용한다. CustomID 계정 생성이 타이틀 정책에서 거부되면 오류를 표시하며 정책 권한을 자동으로 확대하지 않는다.

앱은 로그인, 스토어 연결, 복원, 구매 창 열기 버튼을 제공하며 자동 구매는 없다. 반드시 승인된 Google 라이선스 테스터와 TEST 결제 표시를 확인해야 한다. debug APK는 로컬 검증용이며 Play 업로드에 필요한 별도 서명 빌드와 구분한다. [Google 앱 서명 안내](https://developer.android.com/studio/publish/app-signing), [상품의 결제 권한 요구](https://support.google.com/googleplay/android-developer/answer/1153481?hl=en).


## 실기기 초기화 오류 수정

첫 APK는 기동했지만 수동 로그인에서 `TitleNotSet` 동기 예외가 발생했다. SDK `PlayFabHTTP.InitializeHttp()`가 instance 요청에서도 전역 TitleId만 검사했기 때문이다. HTTP 초기화에 요청의 apiSettings를 전달하는 overload를 추가하고 기존 인자 없는 API는 전역 설정 경로를 유지했다. vendor 파일의 기존 LF/GUID를 보존하고 assets-manifest에 원본 upstreamSha256와 의도한 localPatch를 기록했다. 로그인 동기 예외는 재시도 가능한 상태로 복구한다.

가짜 transport와 임시 settings SO를 사용하는 3개 회귀가 독립 타이틀/빈 전역 타이틀 성공, 양쪽 빈 타이틀 실패, 기존 인자 없는 초기화 호환을 검증한다. 실제 설정 파일·인증 티켓·네트워크는 테스트에 사용하지 않는다. 기존 회귀를 포함해 Editor370/370/0skip을 통과했다.

수정 APK의 실제 manifest에서 인터넷/결제 권한, `.iaptest` 패키지, ARM64, debug 서명을 확인했다. 제거한 광고 권한은 `com.google.android.gms.permission.AD_ID`이며 `ACCESS_ADSERVICES_AD_ID`, `ATTRIBUTION`, `TOPICS`는 여전히 포함되어 있다. MobileAdsInitProvider는 제거됐고 앱의 광고 요청 코드도 차단한다. 전체 광고 관련 권한을 제거한 빌드라고 주장하지 않는다.


수정 APK를 실기기에 설치·기동하고 수동 로그인했다. SDK 초기화 예외는 없어졌고 신규 테스트 타이틀이 `PlayerCreationDisabled`를 반환했다. 앱은 재시도 상태를 표시하고 스토어는 NotInitialized로 유지됐다. 테스트 계정 생성은 타이틀 정책에 막혀 있으며 계정 생성 허용 설정은 변경하지 않았다. Google 구매 버튼/실제 결제는 실행하지 않았다. 다음 단계는 테스트 계정 사전 생성 또는 허용된 타이틀 정책 결정, 승인된 라이선스 테스터 확정, 업로드용 테스트 서명 빌드 준비다.


## 테스트 계정 사전 생성 후속

신규 테스트 타이틀의 관리자 '새 플레이어' 화면에서 전용 계정 1개를 생성했다. 익명 클라이언트 계정 생성 정책은 변경하지 않았다. 동일 계정에 기기의 harness CustomID를 연결하는 단계가 남아 있으며, 연결 전에는 로그인 성공이나 실제 구매 검증을 완료한 것으로 보지 않는다. 추가 계정을 생성하지 않고 이 전용 계정을 이어서 사용한다.
