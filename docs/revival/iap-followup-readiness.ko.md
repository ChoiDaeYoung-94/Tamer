# IAP 후속 읽기 검증과 재개 조건

2026-09-14, checkout `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`, 기준 커밋 `731d9cfa3fe66960b2d8096b04295e4b220a16b6`에서 수행했다. Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, IAP `5.4.3`, NDK `27.2.12479018`을 유지했다. 코드 변경·빌드·Editor 실행·폰 입력은 없었다. 실행 바이너리와 AAB/전달 APK SHA-256 및 Android 16/ARM64/4KB 환경은 [기존 구매 검증](iap-test-bundle.ko.md)을 따른다. 새 APK는 없다.

## 기존 주문의 독립적인 Console 관측

Google Play Console의 주문 관리에서 해당 격리 앱의 기존 무료 테스트 주문만 상세 조회했다. 제품은 `No Ads Test`, 패키지는 `com.AeDeong.MonsterTamer.iaptest`, SKU는 `com.aedeong.monstertamer.no_ads`, 옵션은 `no-ads-test`, 수량은 1이었다.

주문 기록은 02:41:51 UTC의 `Order received`/대기 중, 02:41:52 UTC의 `This order was processed`/처리됨이었다. 약 03:07 UTC 재조회에서도 주문 상태는 **처리됨**이었고 해당 주문 기록에 환불 이벤트는 없었다. 정가/합계 KRW 1,000, 예상 수익 KRW 0 표시가 있었으나 금액 열을 실제 청구 증거로 해석하지 않았다. 무료 테스트의 근거는 앞선 Google 테스트 결제 표시다. 주문 식별자와 구매 토큰은 공개 기록에 포함하지 않았다.

[Google 테스트 문서](https://developer.android.com/google/play/billing/test)는 미승인 라이선스 테스트 구매가 3분 후 환불되며 Console 주문 탭에서 확인할 수 있다고 설명한다. 이번 **약 25분 후 처리됨·미환불 관측은 앱/PlayFab과 독립된 간접 근거**다. Console의 `처리됨` 자체를 acknowledgment 필드와 동일시하지 않으며, 직접 콜백 순서나 `ACKNOWLEDGEMENT_STATE_ACKNOWLEDGED`를 관측한 것으로 기록하지 않는다.

직접 조회용 공식 경로는 [purchases.productsv2.getproductpurchasev2](https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.productsv2/getproductpurchasev2)의 GET이며, [응답](https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.productsv2)에 `acknowledgementState`가 있다. 패키지·구매 토큰과 `androidpublisher` OAuth 범위가 필요하다. 기존 테스트 설정은 공개 라이선스 키로 구성되어 있고 이 API용 자격 증명은 제공되지 않았다. 브라우저 세션에서 토큰을 추출하거나 구매 토큰 복사, API Explorer 동의, 서비스 계정/권한 생성을 하지 않았다. 따라서 직접 API 검증은 미완료다. 이후 소유자가 이미 승인된 테스트 앱 전용 API 환경을 제공한다면 GET 응답에서 상태만 비공개로 확인하고 민감 값을 제거한 결과를 남길 수 있다.

## 실패·취소 결제와 복원 재개 조건

현재 동일 Google 계정은 비소모성 No Ads를 보유한다. 공식 문서는 비소모성 재구매 테스트에 환불 및 권한 회수를 안내하지만, 이는 기존 권한 보존 조건과 충돌하므로 수행하지 않았다. 앱 데이터 삭제나 다른 폰 사용만으로 Google 소유권이 사라지지도 않는다.

다음 시나리오는 **해당 SKU를 아직 보유하지 않은, 별도로 승인된 라이선스 테스트 계정/기기**가 준비된 뒤 수행한다. 현재 계정을 자동 전환하거나 테스터를 추가하지 않는다.

| 시나리오 | 실행과 합격 기준 |
|---|---|
| 사용자 취소 | 격리 앱·무료 테스트 표시를 확인한 구매 시트를 닫고 실패/취소 결과, 신규 No Ads 지급 없음, 재시도 가능한 UI를 확인 |
| 즉시 결제 실패 | 항상 거절 테스트 수단으로 0원 테스트를 수행하고 신규 서버 아이템/로컬 권한 지급이 없음을 확인 |
| 지연 취소 | 지연 후 거절 테스트 수단을 사용하고 대기 중·재시작 후·취소 후 모두 신규 지급 없음 확인 |
| 다른 기기 복원 | 기존 테스트폰을 재연결하고 정확한 테스트 앱/Store 계정 및 새 설치 식별자의 테스트 플레이어 연결을 먼저 확인한 뒤 Store 소유 구매 fetch/restore와 권한·중복 지급을 확인 |
| 삭제 후 복원 | 개인폰을 삭제하지 않는다. 소유자가 삭제 가능하다고 명시한 별도 테스트 설치와 데이터 범위가 준비되어야 함 |

새 약관 동의가 나타나면 그 시점의 승인을 받는다. 새 테스트 계정이 없으면 기존 주문을 회수해서 실패 화면을 만들지 않는다. 다른 기기 복원에도 기존 테스트폰의 실제 재연결이 필요하며 현재 미검증이다.

이번 로컬 검증은 `python -m unittest tools.revival.test_receipt_server` **13/13 통과**다. 합성 응답으로 PENDING/CANCELLED/알 수 없는 구매의 지급 거부, 승인된 비소모성 복원, 재처리 멱등성과 GET 조회 경로를 검사한다. 이는 별도 서버 구성의 테스트이며 현재 Store 바이너리의 PlayFab 경로나 실제 Google 실패 결제를 실행한 증거가 아니다. 기존 Unity Editor 테스트에도 미지급 주문의 저장/승인 차단 및 확정 실패 후 저장 보존 검사가 있으나 이번에는 재실행하지 않았다.

## 16KB 호스트 읽기 결과와 다음 사용자 조치

03:05:50 UTC에 `Get-16KbHostReadiness.ps1`를 다시 실행했다. Windows 11 Pro build 26200, 펌웨어 가상화/SLAT/VM Monitor=true, `HypervisorPlatform.InstallState=1`, `HypervisorPresent=false`, AEHD/GVM 없음, Emulator 가속 검사 exit 6을 관측했다. 기존 결과와 동일하며 AVD 부팅을 반복하지 않았다. 원시 결과는 비공개 `Logs/revival/host-16kb-readiness-20260914-followup.json`에 보존했다.

현재 셸은 비상승 상태다. `bcdedit /enum '{current}'`는 exit 1/잘못된 항목 유형 메시지를 반환했고, 식별자를 생략한 `bcdedit /enum`은 BCD 저장소 접근 거부를 반환했다. 두 실패를 구분하며 `hypervisorlaunchtype=Off`로 추정하지 않는다. 기존 수집기의 exit 1만으로 접근 거부의 상세 원인을 단정할 수도 없다.

다음에 필요한 사용자 조치는 **관리자 PowerShell에서 아래 읽기 명령으로 BCD와 기능 상태를 확인하는 것**이다. 전체 출력을 공개하지 않고 Windows 부팅 로더의 `hypervisorlaunchtype`과 해당 기능의 State만 전달한다. 값이 없으면 `없음`, 읽기 실패면 그대로 실패로 기록한다.

```powershell
bcdedit /enum
Get-WindowsOptionalFeature -Online -FeatureName HypervisorPlatform | Select-Object FeatureName, State
Get-CimInstance Win32_ComputerSystem | Select-Object HypervisorPresent
```

이 결과를 검토하기 전에는 BCD 쓰기·OS 기능 변경·재부팅·보안 기능 비활성화를 진행하지 않는다. WHPX는 [Android 공식 가속 지침](https://developer.android.com/studio/run/emulator-acceleration)의 권장 경로이며, `-accel-check`가 usable/exit 0을 확인한 후에만 별도 조율된 격리 AVD 검증을 재개한다. 실제 16KB 실행 미검증 및 strict RELRO 3건 실패는 그대로 남는다.

판독 분기는 다음과 같다. `Off`가 실제로 확인되면 시작 설정이 원인 후보이지만 변경 명령을 자동 실행하지 않는다. `Auto`인데 HypervisorPresent=false이면 기능의 Enabled/EnablePending 여부와 부팅 진단을 추가 확인한다. 값이 없거나 읽기가 계속 실패하면 시작 설정 미확인을 유지한다. 기능이 Disabled 또는 EnablePending이면 각각 기능 설정 또는 재부팅 검토가 필요한 상태로 보고하며, 소유자가 별도로 승인하기 전에는 실행하지 않는다.
