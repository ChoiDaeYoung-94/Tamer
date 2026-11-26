# 오프라인 시험 앱의 Android 백업 차단 — 2026-11-07

기존 PHOTO APK 검사에서 allowBackup/fullBackupContent/dataExtractionRules 차단 누락을 발견했습니다. 원본과 LD 시험 자료는 보존하고 해당 APK를 실물폰에 새로 설치하지 않았습니다. 기본/PHOTO 빌드에 기존 백업 차단 manifest를 적용하고, Gradle callback이 기본·캐릭터·인트로 연결 fixture의 정확 ID에서 제외 XML을 만들도록 보완했습니다. 운영 ID와 임의 접미사는 추가 허용하지 않습니다.

source `7c46a32`에서 관련 EditMode48/48(백업 경계11개 포함)을 실제 통과했습니다. 이전 source6aa40c9의42/42(당시 새5개)는 별도 이력으로, 확대 결과와 합산하지 않습니다.

새 PHOTO ARM64 APK1회는 BuildReport Succeeded/errors0/warnings88입니다. 원래 controller는 고정 출력 APK까지 보호 원본 목록에 포함한 가드에서 exit1로 중단됐습니다. 실패와 생성된 파일을 보존한 뒤 새 APK를 별도 경로로 복사하고, 추가 빌드 없이 이전 출력과 변경된 설정6개를 정확 복구했습니다. 보호7,146파일 바이트/6,961파일 집합·키/JDK·공유 옵션 원값1/clean·소유 Job0을 대조했습니다. 원래 실패를 정상 종료로 바꾸지 않았습니다. 경고는 기존 SDK deprecated API 등의 항목이며 실제 packaged 인터넷 권한 부재를 별도로 확인했습니다.

실제 패키지는 오프라인 권한/provider 차단, Debug 인증서/Release engine, allowBackup/fullBackupContent=false, cloud/device 양쪽9개·총18개 제외, ARM64/min25/target36 및 native 기본 검사를 통과했습니다. 새 SHA-256은 `ec9c02c897f6054e357f1f9e34a6528b1d37f9bf03f3c40816de8a17915db759`입니다. 고정 Build 출력에는 이전 APK를 복구했으므로 새 산출물과 혼동하지 않습니다.

실물 Samsung SM-N986N/Android13/4KB에서 설치 SHA·선택 ARM64를 확인하고 빈 나이칸의 초점1회→숫자 키보드→뒤로 숨김1회를 확인했습니다. 같은 PID와 빈 원래 안내를 유지했고 입력/확인/거절 버튼이 키보드 위에 표시됐습니다. 나이값/확인/거절/Enter/Done과 실제 인증은0입니다. fixture 진단 overlay가 제목을 가리므로 깨끗한 운영 화면 전체를 확인한 결과로 계산하지 않습니다.

정확 ID callback의 [인트로→로그인 통합 ARM64 APK/실물 세 경로](intro-login-physical-arm64-20261107.ko.md)도 실제 확인했습니다. 원본/GUID·기존 운영 계정/저장/NoAds·키와 기본 ARM64 경계를 보존합니다. 부모 PR502 위의 Draft PR503이며 운영 기능·정책 심사/프로덕션은 별도입니다.
