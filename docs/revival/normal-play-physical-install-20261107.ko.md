# 실물 휴대폰의 정상 Play 설치 확인

## 현재 — 2026-11-11 새 SM-A175N

사용자가 USB 인증·네트워크·Play 준비를 마친 새 Samsung SM-A175N/Android17(API37)/ARM64/4KB에서 공식 Monster Tamer/애뎡과 현재 내부 테스터 안내를 확인했습니다. 무료 설치1회 뒤 실제 수신은 **code31/1.0.5, installer=com.android.vending, primaryCpuAbi=arm64-v8a**입니다. HOME 복귀·소유 도구 종료·기존 시험3앱 UID/User0·보호 원본/키와 공유 ADB 신원 보존을 확인하고 **03:02:19KST(UTC18:02:19.177906)**에 전체/자원PASS로 반환했습니다.

게임 실행·인증/동의/나이 입력·운영 계정 데이터 읽기/복사/초기화·운영 앱 강제종료는0입니다. stopped=true/notLaunched=true·운영 PID 부재를 확인했습니다. 새 휴대폰의 정상 Play 설치가 실제 로그인/서버 저장·No Ads·Play Integrity의 게임 수락을 검증한 것은 아닙니다.

비공개 보존한 split4개는 총88,268,603bytes이며, 아래11월7일의 이전 검증 원본4개와 각각 이름/크기/전체SHA256이 일치합니다. 이전 원본도 다시 전체 해시로 대조했고 해당 바이트의 Play 앱서명·정적/native 검사 근거를 재사용했습니다. 추가 Java/apksigner/aapt2 실행은0으로, 새로운 서명 검사를 실행했다고 보고하지 않습니다. base min32/target36 관측과 원AAB/프로젝트 min25의 구분·실제16KB 미검증은 유지합니다.

첫 설치 시도는 windows-filtered dump의 focus 부재로 클릭 전에 중단(설치 입력0)됐습니다. 원실패/실제 반환을 보존하고, 전체 WindowManager dump에서 정확한 Play 전경을 읽는 새 조건에서 설치했습니다. 계정 아바타가 있는 원화면과 실제 APK/기기 신원은 비공개이며 공개 문서/스토어 제출 이미지가 아닙니다.

현재31과 [새 ARM64 손님/인트로 검증](phone-arm64-20261111.ko.md)의 최신 소스는 서로 다른 산출물입니다. 다음 통합 후보의 fresh Console 번호/인증서·기존 키 직접 입력·AAB/기호·수동 업로드/내부 제공·정상 Play 전달과 실제 운영 기능 검증이 남아 있습니다. 이번 Console 신규 조회/저장·정책 심사·프로덕션은0입니다.

## 과거 — 2026-11-07 SM-N986N

사용자가 Play 스토어 로그인을 마친 Samsung SM-N986N/Android13(API33)에서 공식 앱 상세 링크를 열었습니다. Monster Tamer/애뎡·기존 내부 테스터 표시를 확인하고 현재 기기의 설치 버튼만 한 번 눌렀습니다. 다른 기기 목록·계정 전환·신규 초대/동의·인증·나이 입력은 조작하지 않았습니다.

실제 수신은 **code31/1.0.5, installer=com.android.vending, primaryCpuAbi=arm64-v8a**입니다. base/config.arm64_v8a/gpdeku/gpdeku.config.arm64_v8a 분할4개를 비공개 보존했습니다. 모두 기존 Play 앱 서명 인증서와 같으며, SHA가 검증된 원래 AAB31의 native6개와 실제 설치 payload의 바이트가 일치합니다. 추가 libgpdeku까지 native7개의 LOAD/보수적 RELRO 배치를 검사했습니다. 압축 저장·manifest 추출 활성화 구성이며 raw RELRO 끝주소 modulo 진단3개는 남습니다. 원시 ZIP16KB 정렬·실제16KB 실행 통과를 뜻하지 않습니다.

설치된 기기용 base APK는 minSdk32/target36을 보고했습니다. 이를 원본 AAB의 min25와 같다고 가정한 첫 정적 가드는 실패로 보존했습니다. 이후 원본 AAB31을 pinned bundletool1.18.3으로 직접 dump해 **min25/target36**을 확인했습니다. 설치 base의 리소스 타입136개 중36개에는 실제 sparse flag가 있습니다. 공식 도구의 [SDK32 sparse 변형 생성](https://github.com/google/bundletool/blob/1.18.3/src/main/java/com/android/tools/build/bundletool/splitters/SparseEncodingVariantGenerator.java)과 [1.18.3 기본 최적화 안내](https://github.com/google/bundletool/releases/tag/1.18.3)에 부합하는 기기별 산출물로 해석합니다. 이 설명은 로컬 관측과 공개 소스에 근거한 추론이며 Google의 내부 생성 설정을 직접 읽은 결과는 아닙니다. 프로젝트/AAB의 지원 하한을32로 바꾸지 않았고 API25 단말 실행을 검증한 것도 아닙니다.

게임 실행 명령은0회이며 최종 package 상태의 **stopped=true/notLaunched=true**와 운영 프로세스 없음을 확인했습니다. 계정/게임 저장 데이터는 읽거나 복사하지 않았습니다. HOME 복귀·소유 임시 XML0·공유 ADB 동일 신원을 확인하고18:57:41KST에 실제 반환했습니다.

이로써 실물 휴대폰의 정상 Play 설치 준비를 확보했습니다. 실제 첫 로그인·기존 저장/NoAds·손님 계정/서버·연령별 SDK/광고·삭제는 사용자 직접 인증/나이 조작 뒤 이어서 확인해야 합니다. 수신31과 최신 소스의 오프라인 인트로 검증은 서로 다른 산출물입니다. 기존 전체32 AAB의 업로드나 새 첫 프레임 보완의 운영 제공을 뜻하지 않으며 Console 신규 조회/저장·정책 심사·프로덕션0입니다.
