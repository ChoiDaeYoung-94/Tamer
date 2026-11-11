# 전체 UI의 격리 release 시작 비교 — 2026-11-11

앞선 단일 인트로 release APK는 영상 준비/표시를 분리해 관찰했지만, 기존 전체 UI 개발 APK와 씬 구성이 달라 제품 시작 개선을 판단할 수 없었습니다. 기존 Quad D→Login→SetCharacter/Main/Game/NextScene 여섯 씬을 그대로 담는 별도 release 엔진 시험 구성을 추가합니다. 현재는 구현 준비이며 실제 관련 검사·빌드·기기 결과는 아직 없습니다.

새 고유 ID는 `com.AeDeong.MonsterTamer.revival.faceliftintro1111release`입니다. 기존 여섯 build-only define과 `TAMER_FACELIFT_RELEASE_INTRO`의 정확 일곱 개·정확ARM64·debug서명·APK/not development만 허용합니다. 운영ID/기존 시험ID·custom key/AAB·character/x86/복합ABI·추가/누락 symbol·Editor/데스크톱을 거절합니다. 기존 개발/캐릭터/x86 경로는 유지합니다. 네트워크/provider·백업/이전 제외를 적용하고 고유 앱의 가상 서버/저장만 사용합니다. 성공은 원파일 복구 뒤 기록합니다.

공통 시험 코드의 개발빌드 조건을 새 고유ID/compile symbol에서만 release로 허용합니다. 일반 Player에는 시험 서버와 관찰기가 들어가지 않아야 합니다. 관찰기는 직접 나이·인증·구매 입력 없이 Awake/준비/재생/첫디코드/첫표시/완료와 Intro/Login/Main 진입, 가상 데이터/광고차단/IAP unavailable 상태만 기록합니다. Android launcher 표시는 Tamer UI Release이며 기존 앱의 UID/저장과 구분합니다.

관련 범위는 Intro/Login 격리8개, Gameplay 격리51개, Facelift6개, 단일 인트로21개로 총86개이며 새 경계/백업 경우를 포함합니다. 실제 Editor XML과 실행 이름을 대조해야 하며 소스에서 센 수를 통과로 간주하지 않습니다. 일반 Android68어셈블리와18개 메타데이터/IL 항목에서 새 observer·fixture·임시 Editor entry가 제외됐는지 확인할 계획입니다.

기기 단계는 새 고유ID 최초설치·자연시작·90초원영상/관찰JSON·원래 빈나이 화면 대기까지만 계획합니다. 기존운영31과 공통/손님/캐릭터/단일인트로 다섯 앱을 보존합니다. 인증·나이·실제계정/서버·구매/NoAds·광고·16KB·최종심사/프로덕션은 별도입니다. 단일씬과의 비교도 시계/씬·캐시/녹화 프레임 간격을 함께 기록하며 최적화 성과로 단정하지 않습니다.
