# 실물 ARM64 인트로→로그인 연결 검증 — 2026-11-07

운영 계정 대신 기존 오프라인 메모리 fixture를 사용한 Android APK입니다. source `7c46a320f39d6e9f4d94e098541598ff700420d4`, Unity6000.3.25f1/min25/target36/ARM64이며 운영 키를 사용하지 않았습니다. canonical QuadDIntro 뒤 원래 Login·SetCharacter·Main·Game·NextScene 여섯 씬을 포함합니다.

새 APK1회와 Debug 서명/오프라인 권한·provider 차단, allowBackup/fullBackupContent=false, 실제 패키지의 cloud-backup/device-transfer 각9개 제외, 어두운 Android 시작색과 원래 영상 payload1개, native LOAD/ZIP·보수적 RELRO 배치를 검사했습니다. 원본7,146파일/보호 집합6,961파일·키/JDK·두 ADB 옵션 원값1과 Git clean을 복구하고 모든 Editor/소유 자식0을 확인한 뒤 기기 검증으로 넘어갔습니다.

Samsung SM-N986N/Android13/실제4KB 페이지에서 설치 APK SHA/ARM64 선택을 대조하고 세 경로를 각 새 프로세스로 실행했습니다. 실제 인증·나이 입력/확인/거절은 하지 않았습니다.

| 경로 | 검증 결과 |
| --- | --- |
| 정상 재생 | 원래 영상 프레임 관측, Intro1회→Login1회→완료1회→Main 메모리 fixture 진입 |
| 디코더 오류 모사 | 시험 observer가 오류 callback을 호출한 뒤 같은 목적지/완료 조건 충족 |
| 재생 정지 모사 | 시험 observer가 영상을 멈추고 기한을 단축한 뒤 같은 목적지/완료 조건 충족 |

세 경로 모두 목적지에서 영상/오디오 정지, 운영 로그인 차단1회·메모리 읽기, 서버 fixture 준비·광고 차단·IAP Unavailable을 확인했습니다. 완료 callback 순간의 native 정지 상태와 실제 목적지의 정지 상태는 구분합니다. 뒤 두 경로는 실제 단말 디코더 고장을 재현한 것이 아닌 명시적인 시험 모사입니다.

앱을 중지하고 HOME으로 돌아왔으며 공유 ADB 서버 신원과 사용자/다른 프로젝트 앱을 보존했습니다. 원래 연령 창이 표시돼도 응답하지 않고 검증을 마칩니다. 이 결과는 실제 Google/손님 계정 생성·서버 저장/구매NoAds·연령별 광고/SDK 통신이나 실물16KB 통과가 아닙니다. 기존 후보32 전체 APK/AAB에는 첫 프레임 보완이 들어 있지 않습니다. 최종 운영 후보의 서명·Play 전달과 사용자 최종 UI 검토는 별도입니다.

APK: `109677318` bytes / SHA-256 `8f25097129bfe74a13e9c6723180284cb8ef1d605bb0ba7c088c77246cb4f0fa`. 공개 저장소에는 APK·원시 기기 로그·비공개 설정을 넣지 않습니다.
