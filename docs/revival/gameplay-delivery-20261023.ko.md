# 10월23일 일반 오프라인 게임 APK 전달 준비

오늘의 수정·검증을 반영한 gameplay 부모 후보에서 일반 오프라인 게임 APK를 새로 빌드했습니다. **빌드·정적 검사·로컬 전달 패키지는 완료, 이 정확한 파일의 새 설치/실행과 개인 Drive 업로드는 미완료**입니다. 앞선 변형의 LD 실행 결과를 이 새 파일의 실행 결과로 쓰지 않습니다.

| 항목 | 실제 결과 |
| --- | --- |
| checkout/source | `D:/codex/worktrees/tamer-working/Tamer` / `6a2f0f9187d78f764218bd6384621da381da0cd2` |
| 실행 | BuildAndroid, 2026-10-23 19:30:57~19:33:49 KST, exit0/APK 생성 |
| 도구 | Unity6000.3.25f1/revision e1dba0a9aba4, CLI1.0.0-beta.8, min25/target36/ARM64 |
| 앱 | `com.AeDeong.MonsterTamer.revival.gameplay`, version1.0.5/code26/debug signing. 운영 앱·IAP Test32와 별도 |
| APK | 110729527bytes, SHA-256 `feb3a938abf7a0055032d942521cebb371b18824a32f09b857bcbc6a823ed371` |
| 검사 | 고정 SDK의 manifest/API/ABI/debug 서명·PGS/광고 초기화 provider 부재·INTERNET/BILLING/GMS광고ID 부재 확인. AdServices 권한 일부 잔존 |
| 보호 | 원본53개 보존·사후 읽기 검토·지정 복구1회·원바이트/clean/소유 Editor 종료·IAP 임시2개 부재 |
| 전달 상태 | 로컬 APK·SHA256SUMS·한국어 설치/첫 확인 안내3개 준비/복사 해시 일치. 개인 경로·Drive 주소는 비공개 관리 기록에만 보존 |

일반 변형은 자동 씬 왕복 없이 원래 Main에서 대기하고 Enter Game/Return to Main 및 원래 조이스틱·전투/Capture를 사용할 수 있습니다. 프로세스마다 새 합성 프로필을 사용하므로 기존 게임 계정·저장/No Ads를 불러오거나 재시작 보존을 검증하는 앱이 아닙니다. Gold20000 상점 UI·캐릭터 선택 전용 변형과 구분합니다. 그 변형들의 구매/선택 재시작은 각 별도 소스·앱 폴더의 결과입니다.

이 파일은 Play Console에 올리는 AAB가 아니며 새 내부 트랙 버전을 준비·업로드한 것으로 쓰지 않습니다. 이전 파일·빌드/실패 로그·원본 저장을 보존했습니다. 패키징 도중 Downloads의 기존 deployment 부모가 없음을 확인해 새 비공개 폴더를 만들었고, 프로젝트 복사나 기존 호환 경로 변경은 하지 않았습니다.

개인 Drive의 기존 확정 폴더는 연결 도구에서 metadata404를 반환했습니다. Chrome 목록은 여러 창의 탭을 합쳐 반환해 지정 작업 창을 확정하지 못했고, 임의의 다른 창/계정에 업로드하지 않았습니다. 탭 목록 읽기 외 브라우저 조작·새 탭·공유 권한 변경은0입니다. 로컬 파일이 준비된 상태에서 대상 창 확인 뒤 전달을 이어갈 수 있습니다.

다음 기기 확인은 설치 파일·첫 Main/errors0를 먼저 연결한 뒤 필요한 최소 사례를 정합니다. 운영 앱이나 IAP Test를 삭제/초기화하지 않고, 서명 충돌이 있으면 오류를 보존합니다. 실ARM64/16KB/Play 전달·정책/운영 출시와 main 최종 통합은 별도이며 `releaseReady=false`를 유지합니다.
