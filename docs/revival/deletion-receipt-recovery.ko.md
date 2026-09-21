# 로그인 없이 삭제 접수 영수증 복구

PR #197의 최초 구현은 닫기/재시작 후 새 PlayFab 세션 확인을 요구했다. 서버가 접수한 뒤 응답만 유실되고 티켓/계정 로그인이 무효화되면 복구할 수 없는 결함이 있어 병합을 보류하고 이 경로를 추가했다. 최종 소거 관측과 재가입 안전 판정은 범위에 포함하지 않는다.

## 권한과 영속성

서버 composition에 `recovery_origin`을 명시한 경우에만 접수 조회 복구를 제공한다. Unity `ConfigureSessionService`는 서버 config의 `receiptRecovery=true`를 요구한다. Android 이외 플랫폼 또는 보호 키 접근 불가 상태에서는 삭제 제출을 허용하지 않는다. 기본 운영 endpoint/서버 설정은 여전히 없다.

AndroidKeyStore에 intent마다 `tamer.deletion.receipt.<clientKey>` HmacSHA256 키를 생성한다. 키 원문 export와 평문 파일/PlayerPrefs fallback은 없다. 키의 `getEncoded()`가 null인지 확인한다. 별도 사용자 인증을 요구하지 않는 앱 UID 보호 키이며 하드웨어/StrongBox 탑재를 주장하지 않는다. [Android KeyStore](https://developer.android.com/privacy-and-security/keystore)와 [KeyGenParameterSpec](https://developer.android.com/reference/android/security/keystore/KeyGenParameterSpec) 계약을 따른다.

HMAC 입력은 `SHA256(length:value ...)`다. length는 각 값의 UTF-8 바이트 수이며 값 경계를 포함한다. 접근 권한 domain은 `tamer.deletion.receipt.access.v1`이고 origin, title, ownerHash, account/entity binding, clientKey, requestId, policyRevision을 차례로 묶는다. ownerHash는 서버가 `origin/title/인증된 PlayFab 계정`으로, binding은 여기에 인증된 title entity를 더해 계산한다. 로컬 terminal 증거는 별도 domain `tamer.deletion.receipt.local-terminal.v1`에 접근 입력 hash, terminal 상태, 만료 시각, 로컬 처리 여부를 묶은 MAC으로 보호한다. 로컬 MAC을 상태 조회 권한으로 사용할 수 없다.

삭제 제출 전에는 다음 순서를 지킨다.

1. alias를 journal에 기록하고 보호 키를 생성한다. KeyCreated를 먼저 내구 저장한다. 이후 키가 없어져도 새 키로 대체하지 않는다.
2. 기존 proof와 requestId로 `receipt-register`에 capability SHA-256만 등록한다. 서버는 proof의 계정/entity/intent와 결합하며 동일 verifier 재등록만 허용한다. 등록 응답 유실은 같은 키·verifier로 재시도한다.
3. 등록 성공 응답의 요청·정책·계정/entity binding을 검사하고 만료 정보 및 등록 상태를 저장한다. 디스크를 다시 읽고 동일 키로 같은 capability를 재생성할 수 있는지 확인한다.
4. 제출 시작을 내구 저장한 다음에만 기존 삭제 confirm을 보낸다.

기기 journal에는 비밀 없는 요청 metadata, alias, 서버와 일치하는 binding, 수명 및 로컬 terminal MAC만 남는다. ticket/proof/nonce/HMAC capability 원문은 디스크·PlayerPrefs·로그에 남기지 않는다. 서버는 verifier hash만 보관한다. 전송은 HTTPS POST JSON body이며 URL에 권한을 넣지 않는다. 서버 앞단의 body 로깅 차단/접근 제어는 실제 배포 설정에서 확인해야 한다.

## 상태 조회와 중간 종료

`receipt-status`는 requestId와 capability를 검사하고 기존 DB 접수 상태만 반환한다. PlayFab 재인증·계정 조회·추가 DeletePlayer·취소·새 요청 권한은 없다. 유효 capability는 분당 6회까지 반복 조회할 수 있고 더 많은 조회는 명시적으로 거절한다. status 자체로 권한을 소모하지 않으므로 응답 유실 후 다시 읽을 수 있다. 별도 완료 observer나 worker는 만들지 않는다.

accepted/cancelled 응답을 받으면 먼저 KeyStore MAC이 붙은 terminal 기록을 내구 저장한다. 소유자를 확인한 로컬 처리가 끝나면 처리 완료 상태와 MAC을 다시 저장한다. 그 뒤 `receipt-ack`를 보내고 해당 journal을 지운 다음 해당 intent alias만 삭제한다. ack는 멱등이다. ack 후 파일 삭제가 실패하거나 앱이 종료되면 봉인된 terminal 기록으로 복구하며 로컬 처리를 중복 적용하지 않는다. ack 응답 유실/만료 자체가 이미 확인·적용한 accepted를 unknown으로 바꾸지는 않는다. 다른 intent의 키는 열거하거나 삭제하지 않는다.

로그인보다 먼저 단일 pending 기록의 복구 화면을 제공한다. 복수 pending이면 임의 계정을 선택하지 않으며, 다른 계정이 현재 활성 상태면 그 자격이나 파일을 정리하지 않는다. 로그인 없이 정리할 때에는 server-derived ownerHash와 디스크 owner를 비교한다. 다른 owner·owner 없는 legacy는 보존하고, 일치한 진행도 정리 시에도 No Ads 근거는 별도 보존한다. 계정 관리자 교체도 차단한다.

## 수명과 종료 UX

`recovery_ttl_seconds`는 주입 가능한 유한 설정이다. 비운영 기본 후보는 30일, 허용 범위는 1초~30일이다. 이는 앱 재시작/오프라인 기간의 접수 영수증 회수 여유를 둔 구현 기본값이며 법정 보관 기간이나 사용자가 승인한 운영 보관 정책이 아니다. 실제 배포 전에 필요한 최소 기간을 정해야 한다. 조회/재등록으로 수명을 연장하지 않는다. DB 보관 기간과 접근 권한 TTL은 구분한다.

키 손실·만료·연결 실패는 접수 확인 불가 종료 안내를 보여 주고 화면을 닫을 수 있다. 자동 재시도 루프, 임의 cleanup, 새 삭제/새 계정 자동생성은 하지 않는다. 로그인 전 복구를 닫으면 명시적으로 기존 계정에만 로그인해 그 요청을 확인하는 선택을 제공한다. 익명 로그인도 이 경로에서는 CreateAccount=false다. 기존 계정 로그인까지 불가능하면 접수를 확정하지 않고 자료를 보존한다. 기존 pending 계정의 저장 보호를 해제해 정상 게임 진행으로 우회하지 않으며, accepted 영수증에는 소거 완료 관측을 추가로 요구하지 않는다.

## 검증 기록

checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`, 런타임 구현 `1295805`, 로그인 전 검사 `50c02f2`, 격리 빌드 실행기 `4a7809c`, 최종 서버 취소 영수증/안내 보강 `b8261a54c81b221b47bb4294f08a52f5e944a158`. Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android min24/target36/ARM64, Python `3.14.0` 기준이다.

| 범위 | 결과 | 비공개 원본 SHA-256 |
| --- | --- | --- |
| 서버 receipt 등록/accepted 응답유실+티켓 무효+재시작/ack/TTL/rate/entity | 5/5, 0.184초 | `4435a0b27764e14edada5c1719e5ae819ce309fe71b60d156cf113a8b74302f0` |
| 변경한 HTTP/Intake 기존 회귀 | 7/7, 0.129초 | `075f33ebbce6e6fd39dd5eaa9bfb993bad27504cd051bbfb2a2397b6b5b61b35` |
| Unity 삭제/receipt/키 손실/중간 종료/owner 보존 | 58/58, 0.92초 | `3edc8c7468aec49c24e73a5faa2312e2772def1e6ee4b399e9f2aa7463d664cf` |
| 로그인 전 진입 및 복수 pending 선택 금지 | 2/2, 0.10초 | `eab689cd7f64ed73d479fc11be3e874505466fd95909668be5878ad0509247a2` |
| provider에 바인딩됐지만 제출 전 취소된 영수증의 조회/ack | 1/1, 0.039초 | `45460330e4addee7ef5c5ea8fab90161b7e90ae83c2550a6a896dd8cbd41c0fa` |

원본은 `Logs/revival/deletion-receipt-server-tests.log`, `deletion-receipt-intake-regression.log`, `deletion-receipt-editor-tests.json`, `deletion-receipt-bootstrap-tests.json`, `deletion-receipt-cancelled-test.log`다. 이번 보강의 서버 13건·Unity 60건이 통과했다. 앞선 PR 최초 구현 검사 수와 합산하지 않는다. 테스트 실패0. 실제 네트워크 대신 합성 HTTP/SQLite/보호 저장소 fixture를 사용했다.

일반 Android player 컴파일은 `UNITY_ANDROID`, `UNITY_EDITOR` 없음, harness define 없음, 옵션 None, 66 assemblies로 완료했다. Pipeline eval 응답은 5초 timeout이었으나 작업을 재실행하지 않고 생성된 result.json·DLL과 완료 로그를 확인했다. 원본 `Logs/revival/android-script-compile/20260921-081255-56f218d762234678a2e924f9b4da32b8/result.json` SHA-256 `be46d249fa267e21d7933928862542e12a5827186606b196228536d9057af3bd`, Assembly-CSharp.dll `aa2f1215b35d9b8c8e66cd02bab3d94543b6dc20184b0f7a0a430b0bafab1fac`.

기기 검증은 기존 SmokeScene·OfflineManifest를 재사용한 별도 `com.AeDeong.MonsterTamer.revival.receipt` debug/IL2CPP 앱으로 수행한다. INTERNET/BILLING/광고 init이 없는 APK에서 고정 모의 서버 응답만 사용하고 기존 앱 데이터는 건드리지 않는다. 실제 PlayFab/서버 배포/삭제 및 스토어 검증은 이 검사 범위가 아니다.

2026-09-21 08:21 UTC에 회사폰 SM-N986N / Android 13 / user0에서 두 단계 검증을 완료했다. 설치 전 같은 receipt 앱 ID가 없음을 확인했다. 1단계 PID10721에서 비추출 키 생성·등록을 마친 뒤 해당 앱만 force-stop해 프로세스 소멸을 확인했다. 2단계 새 PID10932에서 같은 보호 키로 capability를 재생성하고 고정 모의 accepted 영수증을 읽었다. 상태 조회1, 새 등록0, 실제 삭제0, INTERNET 권한 없음이다. terminal 처리 후 해당 fixture intent의 키만 제거됐으며 최종 앱도 force-stop했다. 기존 패키지 목록/앱 데이터는 보존했고 테스트 앱은 설치된 상태로 남겼다. 기기 실패0. 이 검증은 실제 AndroidKeyStore 프로세스 간 지속성과 IL2CPP 경로를 확인하며, 운영 서버 연결이나 모든 기기의 하드웨어 보호 수준을 보장하지 않는다.

| 산출물 | SHA-256 |
| --- | --- |
| `Build/revival/Tamer-receipt.apk`, 93,425,419 bytes, 소스 `4a7809ce7078f8a7c06b1d9fb0fc1ebdd43aead9` | `ddec8c5ddb3a55a63bf909454a318ec1baf24a17f3f64a876d2e79b19a24a48b` |
| `Logs/revival/receipt-apk-verification.json` | `710224a5bbb6bc9ffea013b5a1c6b3209ece91e7184578dfbd402e8232fc3b9b` |
| `Logs/revival/receipt-device-result.json` | `34aceb13a194203eb25ffc351d1e33d612615f247e65de967b77e6a969a3d2a7` |
| `Logs/revival/receipt-device-phase1.log` | `8fb6455c396525c5e3d6a4c35d0f41005fb7a87bd32af16e6bae668e7ebae04a` |
| `Logs/revival/receipt-device-phase2.log` | `987756159b45548f51fcdd16fb0df7810f9e0b0212d626ad3f48fab40a972721` |

원본 APK/로그/기기 식별자는 비공개이며 커밋하지 않는다. Editor·회사폰 슬롯을 반환하고 자동 변경된 ProjectSettings·URP·SmokeScene을 복원했다. APK 이후 변경은 서버의 제출 전 취소 영수증 정규화(신규 1건 검사), 빌드 실행기의 원복 대상 목록 보강, 로컬 처리 실패 시 로그아웃 완료로 단정하지 않는 안내 문구, 검증 문서다. 기기 보호 키/receipt 클라이언트 로직과 APK 소스는 위와 같으며 문구/원복 목록 때문에 동일 기기 검사를 반복하지 않았다.
