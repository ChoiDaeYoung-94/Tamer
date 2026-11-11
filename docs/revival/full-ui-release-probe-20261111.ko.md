# 전체 UI의 격리 release 시작 비교 — 2026-11-11

앞선 단일 인트로 release APK는 영상 준비/표시를 분리해 관찰했지만, 기존 전체 UI 개발 APK와 씬 구성이 달라 제품 시작 개선을 판단할 수 없었습니다. 기존 Quad D→Login→SetCharacter/Main/Game/NextScene 여섯 씬을 그대로 담는 별도 release 엔진 시험 구성을 추가했습니다. 소스185bbb7의 APK와 SM-A175N 자연 Intro/Login/Main을 검증했으며, 원래 빈 나이 화면까지가 이번 자동 실행 범위입니다.

새 고유 ID는 `com.AeDeong.MonsterTamer.revival.faceliftintro1111release`입니다. 기존 여섯 build-only define과 `TAMER_FACELIFT_RELEASE_INTRO`의 정확 일곱 개·정확ARM64·debug서명·APK/not development만 허용합니다. 운영ID/기존 시험ID·custom key/AAB·character/x86/복합ABI·추가/누락 symbol·Editor/데스크톱을 거절합니다. 기존 개발/캐릭터/x86 경로는 유지합니다. 네트워크/provider·백업/이전 제외를 적용하고 고유 앱의 가상 서버/저장만 사용합니다. 성공은 원파일 복구 뒤 기록합니다.

공통 시험 코드의 개발빌드 조건을 새 고유ID/compile symbol에서만 release로 허용합니다. 일반 Player에는 시험 서버와 관찰기가 들어가지 않아야 합니다. 관찰기는 직접 나이·인증·구매 입력 없이 Awake/준비/재생/첫디코드/첫표시/완료와 Intro/Login/Main 진입, 가상 데이터/광고차단/IAP unavailable 상태만 기록합니다. Android launcher 표시는 Tamer UI Release이며 기존 앱의 UID/저장과 구분합니다.

부모492e2ad에서 Intro/Login 격리8개, Gameplay 격리51개, Facelift6개, 단일 인트로21개 총86개를 실제 Editor XML/실행 이름과 대조해 통과했습니다. 일반 Android68어셈블리/136DLL·PDB와18개 메타데이터/IL 항목에서 새 observer·fixture·임시 Editor entry의 일반 Player 제외를 확인했습니다. 첫 release 실기기 실행은 기존 개발용 관찰기 Boot의 ID/development 가드 예외1건으로 실패했고 원기록을 보존했습니다.

185bbb7은 기존 개발용 관찰기 파일의 전처리 첫 지시문만 바꿔 새 release 구성에서 제외합니다. 본문·Editor/일반/기존 개발 구성의 전처리 결과가 같고 직렬화 참조0임을 대조해 기존86/일반68/IL18 근거를 재사용했습니다. 변경된 정확7define의 release 구성68어셈블리/136DLL·PDB를 새로 컴파일했고 IL8개를 확인했습니다. 첫 metadata reader는 Unity enum 의존성을 해석하지 못해7/8 실패했으며, 같은 DLL/원blob을 정확 Android Core resolver로 읽어8/8 통과했습니다. 최초 실패를 성공으로 덮어쓰거나 reader 문제 때문에 Editor를 반복하지 않았습니다.

## APK와 실기기 결과

- Unity6000.3.25f1/e1dba0a9aba4, min25/target36, 정확ARM64, code32/1.0.5의 시험 APK입니다. 이 번호는 새 Console 후보 번호가 아닙니다.
- 82,591,192bytes, SHA-256 `5b8034bf3f5c5025f1b58d91af8a9b960711e902a25c99ca4889d686b67a1ccb`. debug 인증서·release 엔진, 정확 ID/런처·권한/provider 제외·백업18항목·원본 영상 payload·ELF LOAD/ZIP/보수적 RELRO 배치를 확인했습니다. 실제16KB 실행 결과는 아닙니다.
- 기존에 설치한 실패 시험 앱을 삭제하지 않고 수정 APK로 한 번 업데이트했습니다. 업데이트 전후 같은 UID/User0와 합성 클라우드 파일의 미생성 상태를 유지했습니다. 파일이 이미 존재한다고 가정한 첫 사전검사는 업데이트 전에 거절됐고 별도 반환 기록으로 보존했습니다.
- SM-A175N/Android17 API37/ARM64/4KB에서 Intro/Login/Main 각1회·영상 완료1회/frame101·오류0·가상 데이터 준비·광고 차단·IAP unavailable을 관찰했습니다. 원래 빈 나이 화면은 입력하지 않았습니다. 기존 운영31과 공통/손님/캐릭터/단일인트로 다섯 앱의 UID/User0도 같았습니다.
- 약90초 영상3,799프레임에서 전체/중앙 흰색 비율95% 초과 판정0건입니다. 원화면의 어두운 시작·회사 로고·빈 나이 안내를 직접 확인했습니다. 영상의 어두움→로고 색 추정2.908초가 남고, 앱 내부 첫 표시1.317초와는 다른 시계입니다. 캐시/씬/엔진/녹화 간격을 통제한 성능 향상 주장이나 모든 기기의 흰 화면 부재 보증으로 해석하지 않습니다.

최종 빌드 전체 반환은2026-11-11 03:38:18UTC, 기기 실행·중지/HOME·원격 임시 촬영 제거와 독립 자원 감사 완료는03:47:07UTC입니다. 원본·일시 설정/Prefs/JDK/키·공유 ADB 신원과 소유 프로세스 종료를 확인했습니다. 비공개 원본 영상/실패 로그는 공개 저장소에 올리지 않았습니다. 사용자 전달본은 지정 보관함 `02_현재검토/현재-release-UI-APK`의 APK·해시·원PNG3장·README입니다.

나이 선택 뒤 전체 게임·실제 Google/손님 서버·저장·구매/NoAds·성인 광고·실제16KB·최신 통합 Play 제공·최종 UI/심사/프로덕션은 별도입니다. 부모 PR 통합과 운영 검증 조건이 남아 Draft로 유지합니다.
