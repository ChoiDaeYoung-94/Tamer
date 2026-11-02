# 손님 설정 Android 격리 검증 준비 — 2026-11-02

상태는 **source `4a53075`의 일반 실행 엔진 변형 Editor21/21·별도 IL2CPP APK1회·정확 복구 완료, 새 변형의 기기 실행 대기**입니다. 이전 Development APK의 LD 첫 실행은 선택 화면 전 흰 화면으로 미완료였습니다. [현재 구현·검증](guest-entry-validation-20261102.ko.md)의 Editor/가짜 경계 결과에 더해 실제 Android의 PlayerPrefs 저장과 강제 종료 후 복원을 확인하기 위한 범위입니다. 운영 계정 생성·실제 Google 인증·심사/배포는 포함하지 않습니다.

## 17:20 KST 일반 실행 엔진 변형 생성·복구

`4a530751796e75b3c88157c3d662052af916c56c`에서 엔진별 ID/context와 global define 오염 검사21/21·C# 오류0을 확인했습니다. 기존12개와 새21개를 더한 별도 전체 회귀로 표시하지 않습니다. Unity/CLI/Pipeline·min25/target36/ARM64는 아래 기준 그대로입니다.

새 APK는 `com.AeDeong.MonsterTamer.revival.guestentry1102release`, code30/1.0.5·71,778,144B/SHA`4ed01d55be003f36ea30a2cfa538e385df56801ea1b6d4ad615f08d845936d29`입니다. 일반 실행 엔진으로 debuggable=false이나 **debug 서명 유효·운영 인증서와 다름**을 실제 산출물에서 확인했습니다. INTERNET/NETWORK_STATE/BILLING/AD_ID/AdServices·GMA/PGS 자동 provider 부재와 백업/이전 제외18항목도 대조했습니다. 초기 managed subsystem 표시의 runtime 등록은 정적으로 확인했으며 실행 관측은 아직 전입니다.

첫 산출물 검사는 release 최적화 후 원래 XML 경로를 가정해 `failed to find file`로 중단했습니다. APK를 다시 빌드하지 않고 manifest의 dataExtractionRules 리소스 ID→실제 `res/dH.xml` 연결을 따라 새 검사 시도에서18개 제외를 확인했습니다. 최초 로그와 수정한 검사 결과를 구분해 보존했습니다. native6개의 LOAD/ZIP·RELRO 배치는 통과했고 raw end-modulo 진단2건은 미충족으로 유지합니다. 실제16KB 실행은 미검증입니다.

빌드가 직렬화한7개 출력 보존/원본 바이트 복구, 보호6,960파일 SHA/집합·키·Git clean·소유Editor0·추적90프로세스 잔존0을 대조했습니다. 이전 Development APK와 AAB30·Library/원시 증거는 보존했습니다. 실제 인수08:12:14.800 UTC/17:12:14.800 KST→실제 반환08:20:40.931 UTC/17:20:40.931 KST이며 FPS/Paw에 통지했습니다. 기기 확인은 새 범위의 fresh 동의 뒤 수행합니다.

## 16:36 KST 기기 결과와 다음 변경 조건

공식 list2의 정확한 `Tamer`/index1과 indexed ADB serial을 대조했습니다. LD Android14/API34·ARM64 번역·4KB이며 실제 ARM64/16KB 검증이 아닙니다. 시작 당시 모든 VM이 꺼져 있어 index1만 열었습니다. 구형 `faceliftui1029` 시험 앱1개는 기존 기록의 APK SHA와 일치함을 확인하고 APK·내부/외부 비캐시 시험 자료를 비공개 보존/검증한 뒤 제거했습니다. 실제 재설치/자료 복원은 하지 않았습니다.

새 시험 APK는1회 설치했고 설치된 base APK의 SHA·code30/1.0.5·debug 표시가 위 산출물과 일치했습니다. 존재하지 않는 패키지의 `pm path` 종료1을 사전 도구가 오류로 처리한1회는 설치 호출 전이며, 정확한 package 목록 대조로 고친 후 설치1회에 성공했습니다. 실행 후4분 이상 흰720×1280 화면이 유지됐고 `GUEST_ENTRY_UI` 첫 표시·손님 ID/모드가 없었습니다. UnityMain 스레드의 CPU 사용은 증가했지만 원인은 미확정입니다. 같은 조건 재실행·선택/계정 생성·나이 입력0이며 실제 PlayerPrefs 손님 저장/재시작은 **미검증**입니다. native stack 진단은 root 요구로 불가했으며 승격/설정 우회는 하지 않았습니다.

원본 화면/로그·신규 앱의 설정·설치/보존 자료를 비공개 보존했습니다. 다른 Tamer6개 APK와 운영 앱의 해시는 불변이며 운영 계정/저장/No Ads 자료를 읽거나 실행하지 않았습니다. 새 시험 앱만 중지하고 이번에 연 index1을 정상 종료한 뒤 host PID+시작시각 잔존0·모든 VM정지·자체 transport0을 확인했습니다. ADB daemon 종료/reset은0입니다. 실제 인수07:28:44.548 UTC/16:28:44.548 KST, 실제 반환07:36:03.633 UTC/16:36:03.633 KST이며 FPS/Paw에 통지했습니다.

다음 가설은 **Development 엔진/초기화와 guest fixture 자체를 분리**하는 것입니다. 현재 설치된 사진 앱을 추측한 과거 사본이 아닌 실제 base SHA로 대조하니 일반 실행 엔진이며, 새 앱과 native5/6종이 다릅니다. 첫 과거 사진 사본 불일치 대조는 입력 검증에서 중단했고 실제 설치 SHA와 맞는 사본으로 바로잡았습니다. 이 차이만으로 흰 화면의 원인을 단정하지 않습니다. Unity의 [Android manifest 안내](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/introducing/manifest)는 development에서 Internet Access를 Require로 설정하는 기본 동작을 설명하지만, 이번 증상의 원인이나 네트워크 권한 추가를 정당화하는 증거는 아닙니다.

진단 변형은 별도 ID `com.AeDeong.MonsterTamer.revival.guestentry1102release`, 일반 실행 엔진·**기존 운영 키와 무관한 debug 서명**·동일한 offline/백업 제외·빈 smoke 사본을 사용합니다. release/development 변형은 각각의 ID·실행 엔진을 교차 거부하고, 추가 release macro는 build-local guest/smoke 변형에서만 허용합니다. 최초 managed subsystem/화면 생성 단계 표시를 더해 멈춘 경계를 구분합니다. APK 경로도 `Tamer-guest-entry-ui-release-1102.apk`로 분리하며 기존 산출물을 보존합니다. 일반 실행 엔진의 앱은 `run-as` 접근을 전제하지 않고 실제 새 프로세스의 PlayerPrefs 읽기/동일 ID SHA·화면으로 저장을 대조합니다. 위21개/빌드 완료와 아직 남은 기기 검증을 구분합니다.

## 16:21 KST 빌드 체크포인트

Unity6000.3.25f1/revision e1dba0a9aba4·pinned CLI1.0.0-beta.8·Pipeline0.6.0-exp.1, min25/target36/ARM64를 사용했습니다. APK는98,959,190B/SHA`a21baead5430135f035fde9a22e7365d503c852ac7a0996939a6f4248799952a`, code30/1.0.5의 **별도 시험 앱**입니다. 실제 APK에서 debug 서명 유효/운영 인증서와 다름, 아래 권한/provider 부재와 백업·이전 제외18항목을 확인했습니다. 운영 후보30의 번호나 파일을 교체하지 않습니다.

native6개의 LOAD/ZIP 검사와 제한된 RELRO 배치 검사는 통과했습니다. 추가 `--strict-relro` 진단은 `libc++_shared.so`·`libmain.so`의 raw end-modulo 조건2건으로 실패했습니다. 이 진단과 whole-LOAD 예외를 반영한 배치 검사는 구분하며 실제16KB 기기 실행·정책 승인으로 계산하지 않습니다. 이 결과만으로 SDK/도구 버전을 바꾸거나 재빌드하지 않았습니다.

첫 `2bbb781` 시도는 검사용 builder의 `AD.SceneManager`/Unity `SceneManager` 이름 충돌 CS0104로 검사/APK 실행 전에 중단했습니다. 원문과6,960입력 복구를 보존하고 Unity 타입을 명시한 `2663d27`에서12개 통과와 APK1회 성공을 확인했습니다. 빌드가 직렬화한 설정/씬7개 출력을 보존한 후 원본 바이트로 복구했고, 전체6,960파일 SHA/집합·기존 키·Git clean·소유Editor0·추적139프로세스 잔존0을 대조했습니다. smoke 원본 씬은 줄바꿈 외 의미 변화0입니다. Library/원시 로그와 기존 AAB30은 보존했습니다.

자원 실제 인수07:08:42.4739889 UTC/16:08:42.473 KST, 실제 반환07:21:11.2607457 UTC/16:21:11.260 KST입니다. FPS/Paw에 반환을 통지했고, LD/ADB 기기 범위는 별도 fresh 조율 전까지 시작하지 않습니다.

| 경계 | 준비한 제한 |
| --- | --- |
| 앱 | `com.AeDeong.MonsterTamer.revival.guestentry1102`, 별도 Android 앱 저장소·development/debug 서명 |
| 시작 | 기존 빈 smoke 씬 한 개의 **빌드 사본**에만 fixture를 주입. 원본 씬/실제 Login·Managers 없음 |
| 코드 | build-local `TAMER_REVIVAL_SMOKE` + `TAMER_GUEST_ENTRY_UI_HARNESS`만 사용. global Tamer define 거부, 다른 게임/IAP/광고 fixture와 결합 금지 |
| 실제 연결 | 제품의 `InitialAccountChoiceView`·선택 gate·고정 ID 저장 helper와 실제 `PlayerPrefs.SetString/Save/GetString` 사용 |
| 가짜 연결 | Google 버튼은 모의 실패 후 선택 복귀, 손님 저장 뒤에는 상태 확인만 제공. 실제 인증 어댑터나 서버 계정 요청을 연결하지 않음 |
| Android manifest | 기존 offline 변환으로 INTERNET/NETWORK_STATE/BILLING/AD_ID/AdServices 권한과 GMA/PGS 자동 provider 제거. 백업·기기 이전 제외 |
| 정보 | 화면에 격리 상태를 표시하고 로그에는 ID 원문 대신 SHA만 기록. 모르는 기존 설정/legacy 키가 있으면 보존하고 중단 |
| 보호 | 기존 키·설정·manifest 원본은 빌드 후 복구. 전체 입력 SHA/집합·원키·소유 프로세스/Editor 종료를 별도 확인 |

네트워크 권한 제거와 계정 요청 미연결은 SDK의 모든 로컬 처리나 플랫폼 동작이 없다는 판정이 아닙니다. 기존 IAP consent 기본값 등 로컬 자동 시작과 운영 인증/계정 호출을 구분합니다. 이 APK를 실제 Play 제공·정책·서버 저장/구매·기존 No Ads 검증으로 사용하지 않습니다.

## 실행 전후 완료 조건

1. 관련 Editor 격리 조건12개를 실행합니다. 실제 앱 ID/플랫폼/development·global define 오염을 검사하며 시험 코드가 운영 앱에서 실행되지 않아야 합니다.
2. 권한 있는 기존 비공개 에셋/도구·소스/브랜치/HEAD·변경·현재 Editor·원키/이전 산출물을 점검하고 스냅샷을 보존합니다. FPS/Paw의 실제 반환과 fresh 동의 후 빌드1회를 시작합니다.
3. APK의 별도 ID/현재 toolchain 버전·ARM64/min25/target36·debug 서명/운영 서명 미사용·위 권한/provider 부재·백업 제외를 실제 산출물에서 확인합니다. 소스 조건만으로 통과시키지 않습니다.
4. 사용자가 지정한 LD `Tamer` 인스턴스만 확인합니다. 새 패키지가 없음을 확인하고 설치하며, 완료된 불필요 시험 앱은 APK/SHA·시험 저장/설정/증거를 먼저 보존한 뒤 식별한 패키지만 정리합니다. 운영/Google/시스템·실제 계정/No Ads·후속 확인용 앱과 다른 인스턴스는 보존합니다.
5. 최초 화면→모의 Google 실패 후 재선택→손님 선택의 실제 UI/저장을 확인합니다. 앱을 정상 중지/새 프로세스로 재실행해 동일 ID SHA와 custom-pending 상태가 유지되고 새 ID/서버 요청이 없음을 대조합니다. 실제 로그인창/나이 입력은 열지 않습니다.
6. 원본 APK/manifest·서명/로그/화면·재시작 결과를 비공개 보존하고 해당 시험 앱만 종료합니다. 앱 삭제/데이터 초기화로 성공 조건을 만들지 않으며 실제 복원 증거와 단순 백업 보존을 구분합니다.

`Build/revival/Tamer-guest-entry-ui-1102.apk`가 이미 있으면 보존하고 재실행을 멈추도록 준비했습니다. 이 앱 ID의 버전 코드는 toolchain을 따르는 격리 값이며 운영 후보 번호를 예약하거나 기존 AAB30을 교체하지 않습니다. 실패 시 원문·예외/단계·변경 조건을 기록하고 같은 조건으로 반복하지 않습니다. [전체 잔여](remaining-recovery-work.ko.md)와 [미저장 심사 안내](guest-review-instructions-draft-20261102.ko.md)를 유지합니다.
