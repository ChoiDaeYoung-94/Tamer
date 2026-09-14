# 스토어 테스트 AAB 준비

테스트앱 `com.AeDeong.MonsterTamer.iaptest`의 AAB만 생성한다. 이 빌드가 운영 배포나 실결제 승인을 뜻하지 않는다. Google Play 업로드는 수행하지 않는다.

## 빌드와 서명

`tools/revival/Run-IapTestBundle.ps1 -PrepareOnly`는 고정된 `.revival-local/iap-signing`에 전용 RSA3072/JKS 업로드 키를 생성한다. 사용자 SID만 허용하는 DACL을 적용하고 암호는 현재 Windows 사용자 DPAPI로 암호화한다. 재실행 시 키·인증서 해시와 테스트앱 표식을 검사하며, 불완전한 생성 결과는 덮어쓰지 않는다. 비공개 폴더와 키를 운영 keystore와 혼용하지 않는다. 암호는 명령행 인자로 넘기지 않고 프로세스 환경 변수로 전달한 뒤 정리한다.

`-TestTitle <테스트 타이틀> -ProductionTitle <운영 타이틀> -Catalog iap-test-v1`은 명시한 별도 타이틀 설정으로 번들을 빌드한다. `RevivalIapBuild.BuildStoreTestBundle`은 기존 debug APK 경로와 별도로 비디버그 AAB를 생성한다. 빌드 한정 `TAMER_IAP_STORE_TEST`가 비디버그 하네스를 허용하며 기존 패키지/타이틀 검사, 원래 로그인·광고 차단, 수동 구매 버튼과 메모리 게임 서버는 유지한다. C# finally와 외부 ProjectSettingsSnapshot이 서명·프로젝트 설정을 복원한다.

## 검증

`python tools/revival/verify_iap_test_bundle.py`는 고정 bundletool로 구조·manifest를 검사하고 기존 `VerifyAabSignature.java`로 모든 payload의 서명 및 로컬 테스트 인증서 일치를 확인한다. `.iaptest`, 비디버그, testOnly 아님, target36, ARM64, INTERNET/BILLING 필수, GMS AD_ID/MobileAdsInitProvider 없음이 조건이다. AdServices 관련 권한은 별도로 남아 있을 수 있다. 네이티브 ELF LOAD와 RELRO 결과는 서명/manifest 성공과 구분한다.

전용 업로드 서명은 기존 debug APK 서명과 다르므로 기기의 같은 패키지에 직접 업데이트할 수 없다. Play App Signing으로 배포된 APK 역시 별도 인증서를 사용할 수 있다. 기존 앱 삭제는 테스트 식별자/저장 파일을 잃을 수 있으므로, 현재 CustomID 연결과 보존 절차 확인 전에는 삭제·재설치하지 않는다. 이 작업에서는 기기 설치를 하지 않았다.

## 초기 빌드 시점의 업로드 전 조건

- 사용자가 대기 중인 CustomID 연결을 완료하고 같은 테스트 계정으로 앱 로그인 성공을 확인해야 한다. 브라우저 보안 거절을 우회하거나 입력 완료로 추정하지 않는다.
- 실제 사용할 Google Play 계정과 기존 승인 라이선스 테스터 등록을 확인해야 한다. 임의 이메일 추가나 외부 초대는 하지 않는다.
- 신규 테스트앱의 Play App Signing/업로드 키 설정과 AAB 처리 결과를 확인해야 한다. 새로운 법적 동의가 나타나면 별도로 확인한다.
- 결제 권한이 인식된 후 기존 No Ads SKU의 테스트용 일회성 상품과 테스트 접근 범위를 설정해야 한다.
- 구매 화면의 TEST 결제 표시 확인 후에만 승인된 무료 라이선스 테스트를 진행한다. 실제 유료결제·프로덕션 출시·운영키 사용은 이 작업 범위가 아니다.


## 실제 검증 결과

Unity6000.0.81f1/CLI1.0.0-beta.8에서 Editor370/370/0skip(4.8386584초), manifest 회귀7/7, 기존 서명 회귀9/9를 확인했다. 전용 키 재사용/DPAPI 복호화와 폴더·파일7개의 현재 사용자 전용 ACL도 확인했다. 빌드 종료 후 자체 Editor0, 원래 프로젝트 설정 복원, inventory121/meta0이다.

AAB는71,272,522바이트이며 SHA-256은 `aef2ec2212b09415294d6b88d5de21c78e7730bf59ecabbc23514a36354aa37a`이다. bundletool1.18.3 구조 검사와 전용 인증서에 대한 payload601개 서명 검증을 통과했다. 비디버그/min24/target36/ARM64, 네트워크·결제 권한과 광고 요청 차단 구성을 확인했다.

ELF6개의 LOAD 검사는 통과했다. `libc++_shared.so`, `libil2cpp.so`, `libmain.so`의 RELRO 끝 주소는 16384 정렬 조건을 충족하지 않았다. Play가 전달할 APK의 ZIP 정렬, 실기기16KB 동작, Play 수락, 로그인 및 결제는 이 결과로 검증되지 않았다. 세부 근거는 [검증 기록](iap-test-bundle-validation.json)에 있다.


RELRO 3건의 정확한 구간, debug와의 차이, prebuilt/IL2CPP 생성 경로 및 고정 Unity에서의 재링크 실험 범위는 [후속 분석](iap-relro-analysis.ko.md)에 정리했다. 검사 실패와 실제16KB 실행 미검증은 유지한다.

## 2026-09-14 수동 업로드 후 Store 준비

사용자의 수동 업로드 완료 후 신규 격리 앱의 Play Console에서 버전 `26 (1.0.5)`가 내부 테스터에게 제공됨을 확인했다. UI의 게시 시각은 9월 14일 오전 11:08이며, 패키지 `com.AeDeong.MonsterTamer.iaptest`, 최소 API 24, target SDK 36, ARM64가 기존 AAB 기록과 일치한다. 업로드 인증서 SHA-256도 로컬 테스트 업로드 인증서와 일치했다. 앱 자동 보호는 활성 상태로 유지했다.

Console은 해당 번들에 **16KB 지원**을 표시했다. 이는 Play의 정적 판정이며, 별도 strict RELRO 3건 실패나 실제 16KB 실행 미검증을 대체하지 않는다. 이전 검증 JSON의 `uploaded=false`는 빌드 당시 기록으로 보존한다.

지정 계정이 기존 라이선스 테스터 목록과 새 개인폰의 현재 Play Store 계정에 일치함을 읽기로 확인했다. 신규 내부 트랙에는 그 계정 한 명만 포함하는 전용 목록을 저장했다. 기존 목록 및 운영 앱/트랙은 변경하지 않았다. 이메일·기기 식별자·인증용 CustomID는 공개 기록에서 제외한다.

신규 격리 앱에 `com.aedeong.monstertamer.no_ads` 상품을 생성했다. 이름은 `No Ads Test`, 구매 옵션은 `no-ads-test`이며 구입/이전 버전 호환/단일 수량으로 설정했다. 대한민국만 사용 가능하고 기준 가격은 KRW 1,000이며, 활성 상태를 확인했다. 이 가격 설정은 실제 결제 실행을 뜻하지 않는다. 테스트 결제 화면에서 무료 라이선스 테스트 표시를 확인해야 한다.

개인폰에서 내부 테스트 참여 링크를 여는 명령은 자동 승인 정책에 의해 실행 전에 거절되었다. 같은 동작을 다른 경로로 우회하지 않았으며 수동 참여·Store 설치가 남았다. 새 설치의 CustomID를 기존 관리자 생성 테스트 계정 한 개에 연결하는 단계도 별도로 남아 있다. 과거 폰 식별자 재사용, 클라이언트 계정 생성 정책 변경, 기존 앱/데이터 삭제는 하지 않았다.

이번 후속 작업은 `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`에서 수행했다. Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, NDK `27.2.12479018` 및 AAB 소스 커밋 `39c2b253e3d6d57e1c3b2e4fc8f2dafe1adac6ef`를 유지했으며 재빌드·Editor 테스트는 하지 않았다. 위 AAB SHA-256을 재확인했고, 새 APK는 생성하지 않았다. Store 설치·로그인·영수증 검증·권한 복원·무료 구매·전달 APK 서명과 ZIP 정렬은 여전히 미검증이다.

### 같은 날 설치 실패 진단

사용자가 직접 참여 링크로 도달한 Play Store의 정확한 `.iaptest (unreviewed)` 화면을 관측했다. 설치 버튼 1회로 다운로드 전에 “문제가 발생했습니다. 다시 시도해 주세요.”라는 일반 오류가 재현됐다. 이전 정책 차단 링크 열기는 재시도하지 않았다.

기본 사용자 영역에 `.iaptest`는 미설치이며 `/data` 여유 공간은 약 271GB였다. 최근 로그를 필요한 Finsky/PackageInstaller/PackageManager 범위로 확인했으나 구체적인 `INSTALL_FAILED` 또는 서명 충돌 코드는 관측되지 않았다. 로그 부재만으로 해당 원인을 완전히 배제하지는 않는다.

Console에서 내부 트랙 활성, v26 제공 및 지정 테스터 한 명의 선택 상태를 확인했다. 실제 기기의 코드명 `pa3q`로 조회한 Samsung Galaxy S25 Ultra 모델은 Android 15–16에서 **지원됨**이었다. 모델명 검색 결과 0건을 미지원으로 오해하지 않도록 코드명과 대조했다.

원인은 아직 확정되지 않았다. 현재 증거로 APK/RELRO/서명 결함을 단정하거나 재빌드하지 않았다. 참여·다운로드 제공 단계의 상태 확인이 더 필요하며, 기존 앱/데이터·Store 캐시·계정·보호 설정은 변경하지 않았다. 오류 화면과 필요한 로컬 증거는 비공개로 보존했다. 설치 이후 검증은 계속 미완료다.
