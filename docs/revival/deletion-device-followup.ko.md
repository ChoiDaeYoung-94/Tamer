# 삭제 로컬 정리와 불확실 응답의 기기 재시작 검증

2026-09-28, SM-N986N / Android 13(API 33)에서 격리 debug 앱의 합성 시나리오 두 단계를 통과했다. 서버 인증·계정 생성·삭제 호출은 실행하지 않았다. 실제 계정 삭제 접수나 제품 설정 화면의 온라인 검증 결과로 해석하지 않는다.

검증 checkout은 `C:\Users\pc_17\.codex\worktrees\7299\Tamer`, 브랜치는 `codex/deletion-device-followup`이다. 기준 `1106805`에서 최신 `2f81fc96f3c40d726c62f0d5ae23bbf7a177fe82`까지 문서 2개만 변경됐음을 확인한 후 해당 작업 checkout만 fast-forward했다. 검증 시작 HEAD는 `2f81fc9`와 하네스 2개 파일의 미커밋 변경이며, 최종 소스 커밋 `b45b8691498ccf8a655ad0c94a2c0ad291741b6d`의 두 Git blob을 빌드 전 해시와 대조했다. 후속 문서 변경으로 검증을 재실행하지 않았다.

Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android min24 / target36 / ARM64, build-tools `36.0.0` 기준을 유지했다. 원본은 읽기 전용 복원 source로 사용하여 에셋 verified4561/copied0, 서비스 설정 제외를 확인했다. 다른 Editor·원본 checkout·서명 값을 변경하지 않았다.

기존 삭제 시험 APK와 로그를 보존한 뒤 새 APK를 빌드했다. 첫 빌드는 C#·IL2CPP 성공 후 Gradle Java의 native mmap/Windows1455 메모리 부족으로 실패했다. 두 번째만 실행하여 성공했다. 해당 프로세스의 `JAVA_TOOL_OPTIONS`에 `-Xms64m -Xmx1024m -XX:ActiveProcessorCount=2 -XX:+UseSerialGC`를 적용하고 종료 후 환경을 복원했다. Gradle의 daemon 최대 힙 설정 자체는 변경하지 않았다. OS 페이징 설정·다른 앱·Editor를 변경하거나 종료하지 않았다. 새 APK의 검증으로 앱 ID `com.AeDeong.MonsterTamer.deletiontrial`, debug 서명, ARM64, BILLING·AD_ID·광고 초기화 provider 제외, backupfalse와 18개 추출 제외 규칙을 확인했다. 설치 전 해당 패키지가 없음을 확인했고 기존 앱을 교체하거나 제거하지 않았다.

APK는 `Build/revival/Tamer-deletion-trial.apk`, 132845097 bytes, SHA-256 `05d32884a3204781ef0b3aabd458e272cfed76e6d770818339b4d081e9956b7b`이다. build wrapper의 설정·씬·manifest snapshot 복원 후 변경은 하네스 두 파일뿐이며 해당 checkout Editor는 종료됐다.

합성 시나리오는 `OfflineDeletionChecks` 아래 고정 phase와 `synthetic-` owner만 사용한다. 기존 `DeletionFlow`·`CloudScriptDeletionGateway`에 네트워크를 호출하지 않는 결과 대역을 주입하고, 실제 `DataManager`의 제출·접수·파일 정리 callback과 `FileDeletionRecoveryStore`를 실행한다. 인증 ticket/token은 만들지 않는다. 일반 출시 플레이어에서 이 하네스와 DataManager 보조 메서드는 제외된다.

- 첫 단계: 확인 전 합성 제출 0회, 확인 후 접수 대역 1회. 소유 진행도·생성 형식 백업·현재 세션 inventory·기존 소유 보관 사본 삭제, 새 보관 사본 0개, 로그아웃·saveReadyfalse 확인. 타인·owner 불명·형식 불량 백업은 원문 바이트를 보존했다.
- 불확실 응답: 합성 제출 1회 후 `SubmissionUnknown`, journal의 `SubmissionStarted=true` 유지. Request/Confirm/Refresh 재전송, 로컬 쓰기와 `BeginAccountSession` 재바인딩이 차단됐다. 서버 로그인 차단이나 제공자 계정 삭제를 시험한 것은 아니다.
- 두 번째 단계: 해당 trial 패키지만 force-stop하고 PID 부재를 확인한 뒤 새 PID로 실행했다. 파일 journal로 `SubmissionUnknown`을 복원해 같은 차단과 합성 제출 0회를 확인했다. 기기에서 수집한 기존 파일 7개의 해시가 첫 단계와 일치했고 재시작 통과 기록만 추가됐다.

두 기기 단계는 각각 첫 실행에 통과했다. 입력 준비 중 패키지 부재의 정상 exit1 처리와 잘못된 종료 명령을 바로잡았으며, 실제 PID 부재·변경 확인 전의 재실행을 프로세스 재시작 증거로 사용하지 않았다. 최종 trial 앱만 종료하고 설치·합성 파일·미해결 journal을 보존했다. 기기 serial·계정 식별자·원시 로그·스크린샷·로컬 감사 자료는 공개하지 않는다.

이 결과는 기존 [온라인 폐기 계정 시험](cloudscript-live-trial.ko.md), [운영 연결](cloudscript-operating-activation.ko.md)과 별개다. 이전 유일한 승인 폐기 계정은 이미 삭제 접수와 후속 조회 부재를 확인했으므로 재사용하지 않았다. 최신 APK의 새로운 서버 삭제 시험은 새 폐기 계정과 짧은 시험 gate 범위가 확정돼야 진행한다. 운영 인증 preflight·운영 계정 삭제, 제품 설정 진입의 실제 온라인 흐름, 스토어 구매/삭제 후 새 계정의 No Ads 복원, 최종 출시 AAB·16KB 검증은 이 시험으로 보증하지 않는다.
