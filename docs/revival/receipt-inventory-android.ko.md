# 오프라인 Android receipt v2·인벤토리 정리 검증

2026-09-21, checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`, branch `codex/receipt-inventory-android`. 기준 main `108a12b7d75475071b2543f18c38f09510d71bb2`, 실제 빌드·기기 소스 `8b6b2ca97b12ecdc985b6bf3b24f5c9fe0a4096c`. 기동 전 HEAD/clean/해당 Editor0을 기록했다. Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android min24/target36, IL2CPP ARM64. 에셋 4,561개 대조·복사0, 서비스 설정 제외.

## 실행 범위와 결과

기존 `RevivalDeletionReceiptHarness`만 확장했다. 빌더·서명·패키지 `com.AeDeong.MonsterTamer.revival.receipt`·격리 시작 씬을 유지하고, 기존 v1 `ReceiptHarness`와 별도인 `ReceiptHarnessInventoryV2` 디렉터리를 사용한다. 새 시나리오에 기존 파일이 있는데 pending record가 없으면 재생성하지 않고 중단한다.

adb로 새로 확인한 테스트폰은 **SM-N986N / Android13 / user0**이었다. 기존 설치 APK를 읽기 백업하고 새 APK와 Android Debug 인증서 SHA-256 `7031d6897177b817d72e02cc9f07b9141314fd4d93bbb39fc95d3b001c850910` 일치를 확인한 뒤 `install -r`로 데이터 유지 업데이트했다. uninstall·clear·운영 앱 조작은 없었다.

최초 빌드·기기 시나리오 **1회 성공, 실패·재시도0**, Editor 시험 반복0.

1. PID14291: 합성 세 계정의 인벤토리를 만들고 한 계정은 새 세션으로 다시 저장했다. 실제 AndroidKeyStore의 내보낼 수 없는 키로 v2 조회 권한을 등록하고 기록을 저장했다. 원격 서비스 대신 고정 오프라인 gateway만 사용했다.
2. 해당 테스트 앱을 force-stop하고 프로세스가 없어진 것을 확인했다.
3. PID14511: 저장한 기록·기존 Android 키로 accepted 상태를 복구했다. 조회1회, 새 등록0회, 서버 삭제 호출0회. 요청의 계정·세션이 맞는 인벤토리를 삭제하고 receipt journal 및 해당 요청 키를 정리했다.
4. 다른 owner의 삭제와 오래된 세션의 삭제는 거절했다. 보존 대상 두 파일은 phase1/phase2의 기기 SHA-256이 일치했다. 기존 v1의 complete·server-verifier 파일도 실행 전후 SHA-256이 일치했다. 완료 화면을 캡처한 뒤 테스트 앱을 다시 force-stop했다. 앱과 보존 자료는 설치 상태로 남겼다.

APK manifest 검사에서 INTERNET·ACCESS_NETWORK_STATE·BILLING·AD_ID 및 MobileAdsInitProvider가 없고, 런타임도 INTERNET 권한 부재를 확인했다. 로그인·광고·IAP·운영 계정·원격 삭제 호출은 하지 않았다.

기기 로그에는 INTERNET 권한이 없는 Development Player의 개발 연결 소켓 오류와 기존 JNI Byte 사용 관련 obsolete 경고가 관측됐다. 하네스의 두 단계 성공과 파일/키 결과는 위 증거로 확인했지만 로그 전체가 오류·경고0이거나 네트워크 연결 시도 자체가 전혀 없었다고 주장하지 않는다. 개발 연결 오류·JNI 경고의 일반 제품 영향은 이번 격리 시험에서 확정하지 않았다. 원시 로그는 비공개로 보존한다.

## APK와 비공개 증거

`Build/revival/Tamer-receipt.apk`: **132,761,685 bytes**, SHA-256 **`a6d85bc6cb7cda95b0722b35441923713c2523972f03152ea24d5543db42b39f`**. versionName1.0.5/versionCode26, debuggable, debug 서명 확인. APK·로그·화면·기기 식별 자료는 커밋하지 않았다.

| 로컬 증거 (`Logs/revival/`) | SHA-256 |
| --- | --- |
| `receipt-v2-device-result.json` | `0f8df62246e99a568d66e8833004525fc2ea25896467e9ec0ccc292488c2e598` |
| `receipt-v2-phase1.log` | `d8e6e0e7a39fe496175eca22416d4a1868b419978785f23db652cdaaa7d77482` |
| `receipt-v2-phase2.log` | `89c0a71a2b291b5c1cad39912285d524049627a99ba30ac09ddfabf4277176d9` |
| `receipt-apk-verification.json` | `c9806414f9318fac826bf9ffe9eccbd68e96e2eec9ddbceb6182e23a3e213501` |
| `receipt-v2-device.png` | `87548eb2cac4ea5367a3fd4dcc9cfc032b5ff8345b5e37ada87a2bae438a78ee` |

사전 상태는 `receipt-v2-build-preflight.json`; 인증서는 `receipt-v2-installed-signing.txt`/`receipt-v2-new-signing.txt`; 파일 보존 대조는 `receipt-v2-protected-before.txt`/`after.txt`, `receipt-v2-original-files-before.txt`/`after.txt`에 있다. 기존 로컬 APK와 고정 이름 빌드 자료는 실행 전 `receipt-before-v2-20260921-182922/`에 복사해 보존했다. 설치되어 있던 APK도 `receipt-v2-installed-before.apk`로 보존했다.

빌드 wrapper 종료코드0·APK 검사 성공, 해당 Editor PID33712 종료·설정 snapshot 복원·clean 확인 후 Editor/기기 슬롯을 반환했다. 다른 프로젝트 Editor나 원본 checkout은 변경하지 않았다.

## 결과의 한계

실기기에서 확인한 연결은 **ReceiptClient → AndroidKeyStore → PlayerInventoryStore.DeleteBound**다. DataManager의 전체 UI 삭제 흐름, 운영 인증/서버/최종 소거, Android 잠금·권한 오류의 모든 유형, OS 백업 복원·앱 재설치·기기 변경·downgrade는 이번 실행에 포함되지 않는다. 기존 Editor의 오류·원본 보존 시험을 실기기 시험으로 바꾸어 표현하지 않는다. [삭제 정리 구현](inventory-deletion-cleanup.ko.md)의 계정/세션 경계 및 v2 생성 후 구클라이언트 복구 제한은 유지된다. 이 debug 격리 APK는 출시 AAB나 스토어 검증 결과가 아니다.
