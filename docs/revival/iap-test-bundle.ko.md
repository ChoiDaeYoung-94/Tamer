# 스토어 테스트 AAB 준비

테스트앱 `com.AeDeong.MonsterTamer.iaptest`의 AAB만 생성한다. 이 빌드가 운영 배포나 실결제 승인을 뜻하지 않는다. Google Play 업로드는 수행하지 않는다.

## 빌드와 서명

`tools/revival/Run-IapTestBundle.ps1 -PrepareOnly`는 고정된 `.revival-local/iap-signing`에 전용 RSA3072/JKS 업로드 키를 생성한다. 사용자 SID만 허용하는 DACL을 적용하고 암호는 현재 Windows 사용자 DPAPI로 암호화한다. 재실행 시 키·인증서 해시와 테스트앱 표식을 검사하며, 불완전한 생성 결과는 덮어쓰지 않는다. 비공개 폴더와 키를 운영 keystore와 혼용하지 않는다. 암호는 명령행 인자로 넘기지 않고 프로세스 환경 변수로 전달한 뒤 정리한다.

`-TestTitle <테스트 타이틀> -ProductionTitle <운영 타이틀> -Catalog iap-test-v1`은 명시한 별도 타이틀 설정으로 번들을 빌드한다. `RevivalIapBuild.BuildStoreTestBundle`은 기존 debug APK 경로와 별도로 비디버그 AAB를 생성한다. 빌드 한정 `TAMER_IAP_STORE_TEST`가 비디버그 하네스를 허용하며 기존 패키지/타이틀 검사, 원래 로그인·광고 차단, 수동 구매 버튼과 메모리 게임 서버는 유지한다. C# finally와 외부 ProjectSettingsSnapshot이 서명·프로젝트 설정을 복원한다.

## 검증

`python tools/revival/verify_iap_test_bundle.py`는 고정 bundletool로 구조·manifest를 검사하고 기존 `VerifyAabSignature.java`로 모든 payload의 서명 및 로컬 테스트 인증서 일치를 확인한다. `.iaptest`, 비디버그, testOnly 아님, target36, ARM64, INTERNET/BILLING 필수, GMS AD_ID/MobileAdsInitProvider 없음이 조건이다. AdServices 관련 권한은 별도로 남아 있을 수 있다. 네이티브 ELF LOAD와 RELRO 결과는 서명/manifest 성공과 구분한다.

전용 업로드 서명은 기존 debug APK 서명과 다르므로 기기의 같은 패키지에 직접 업데이트할 수 없다. Play App Signing으로 배포된 APK 역시 별도 인증서를 사용할 수 있다. 기존 앱 삭제는 테스트 식별자/저장 파일을 잃을 수 있으므로, 현재 CustomID 연결과 보존 절차 확인 전에는 삭제·재설치하지 않는다. 이 작업에서는 기기 설치를 하지 않았다.

## 업로드 전 남은 조건

- 사용자가 대기 중인 CustomID 연결을 완료하고 같은 테스트 계정으로 앱 로그인 성공을 확인해야 한다. 브라우저 보안 거절을 우회하거나 입력 완료로 추정하지 않는다.
- 실제 사용할 Google Play 계정과 기존 승인 라이선스 테스터 등록을 확인해야 한다. 임의 이메일 추가나 외부 초대는 하지 않는다.
- 신규 테스트앱의 Play App Signing/업로드 키 설정과 AAB 처리 결과를 확인해야 한다. 새로운 법적 동의가 나타나면 별도로 확인한다.
- 결제 권한이 인식된 후 기존 No Ads SKU의 테스트용 일회성 상품과 테스트 접근 범위를 설정해야 한다.
- 구매 화면의 TEST 결제 표시 확인 후에만 승인된 무료 라이선스 테스트를 진행한다. 실제 유료결제·프로덕션 출시·운영키 사용은 이 작업 범위가 아니다.


## 실제 검증 결과

Unity6000.0.81f1/CLI1.0.0-beta.8에서 Editor370/370/0skip(4.8386584초), manifest 회귀7/7, 기존 서명 회귀9/9를 확인했다. 전용 키 재사용/DPAPI 복호화와 폴더·파일7개의 현재 사용자 전용 ACL도 확인했다. 빌드 종료 후 자체 Editor0, 원래 프로젝트 설정 복원, inventory121/meta0이다.

AAB는71,272,522바이트이며 SHA-256은 `aef2ec2212b09415294d6b88d5de21c78e7730bf59ecabbc23514a36354aa37a`이다. bundletool1.18.3 구조 검사와 전용 인증서에 대한 payload601개 서명 검증을 통과했다. 비디버그/min24/target36/ARM64, 네트워크·결제 권한과 광고 요청 차단 구성을 확인했다.

ELF6개의 LOAD 검사는 통과했다. `libc++_shared.so`, `libil2cpp.so`, `libmain.so`의 RELRO 끝 주소는 16384 정렬 조건을 충족하지 않았다. Play가 전달할 APK의 ZIP 정렬, 실기기16KB 동작, Play 수락, 로그인 및 결제는 이 결과로 검증되지 않았다. 세부 근거는 [검증 기록](iap-test-bundle-validation.json)에 있다.
