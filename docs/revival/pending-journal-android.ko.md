# 실제 DataManager의 Android 저장 저널 검증

전용 오프라인 하네스는 실제 `DataManager`의 저장·로드·서버 스냅샷 병합과
실제 `ServerManager`의 쓰기 확인 경로를 사용한다. 요청 delegate만 합성 서버로 주입하며
PlayFab SDK 호출, 계정 로그인, Google·광고·IAP 초기화를 하지 않는다.

패키지는 `com.AeDeong.MonsterTamer.revival.journal`이다. 유일한 저장 대상은 해당 앱의
`Application.persistentDataPath/JournalHarnessV1/JournalPlayerData.json`이며,
기존 파일이 있으면 초기 준비를 거부한다. DataManager의 기존 백업 기능은 같은 폴더 아래
`PlayerDataBackups`에 원본을 보존한다. 기존 게임의 `PlayerData.json` 경로는 변경하지 않았다.

`DataManager.JournalHarness.cs`와 `RevivalJournalHarness.cs` 전체는
`UNITY_EDITOR || TAMER_JOURNAL_HARNESS`로 감쌌다. 일반 플레이어에는 주입 API와 하네스가 포함되지 않는다.
전용 빌더는 씬 하나와 extra define으로만 활성화하고, 전역 TAMER define을 거부하며
인터넷·네트워크 상태·결제·광고 ID 권한과 광고 초기화 provider를 manifest에서 제거한다.

## 재현 절차

1. 새 설치에서 `Prepare fresh pending 20`: 합성 서버 Gold=10을 읽고 실제 DataManager로 Gold=20을 저장한다. 업로드는 하지 않는다.
2. 해당 앱만 force-stop하고 PID 소멸을 확인한 뒤 새 프로세스로 실행한다.
3. `Verify pending after process restart`: 디스크에서 Gold=20/pending=1을 읽고 옛 합성 서버 Gold=10을 적용한다. 결과는 Gold=20/pending=1이어야 한다.
4. `Acknowledge synthetic upload`: 실제 요청 큐의 성공 callback과 DataManager ack를 실행한다. Gold=20/pending=0을 파일에 기록한다.
5. 다시 해당 앱만 force-stop·재실행한다. `Verify acknowledged process restart`로 Gold=20/pending=0을 확인하고 합성 서버 Gold=20을 읽는다. 추가 쓰기는 0이어야 한다.

원본 JSON·화면·프로세스 기록은 비공개 로컬에만 보존한다. 전용 테스트 파일과 설치된 앱은 검증 후에도 보존한다.

## 검증 범위

2026-09-14 회사폰 SM-N986N(Android 13, ARM64, 4096바이트 페이지, 사용자 0)에서
위 다섯 단계를 모두 통과했다. 실제 파일은 준비·첫 재시작·옛 응답 적용 후
Gold=20/pending Gold=20/revision=1을 유지했고, ack 후와 두 번째 재시작에는
Gold=20/pending 빈 객체/revision=1을 유지했다. 마지막 확인의 합성 쓰기 수는 0이었다.
두 번 모두 종료 전 PID, 종료 후 PID 없음, 새로운 PID를 기록했다. 최종 전용 앱은 종료하고 보존했다.

- checkout: `C:/Users/pc_17/.codex/worktrees/7299/Tamer`
- APK 소스 커밋: `d7bd6be42693ca7e2d4cc40d4d2f035522e7a2fd`
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android min SDK 24 / target SDK 36, debug ARM64
- APK SHA-256: `c0ebc7bb4a6dd7f3cb09effaf868ce5573e297053f4d0491b6f6fc064be5880d`
- `Run-Baseline.ps1 -TestsOnly`: **420/420 통과**. 새 인스턴스 세 개를 사용하는 회귀, 패키지 거부, manifest 제거, 소스 컴파일 가드를 포함한다.
- `verify_journal_apk.py`: 최종 APK 패키지·debug 서명·ABI·권한 검사 통과
- GUID 검사: 132개 파일, unresolved 0
- 네이티브 LOAD/ZIP 검사 통과. 추가 `--strict-relro` 검사는 라이브러리 5개의 RELRO 끝 정렬 조건에서 실패했으며 16 KB 실행 성공으로 해석하지 않는다.
- 원본 파일·화면 해시와 촬영 시각: [검증 JSON](pending-journal-android-validation.json)

일반 플레이어 제외는 소스의 조건부 컴파일 경계와 전용 define을 검사했다. 일반 운영 APK를 별도로 재빌드한 검증은 아니다.

이 검증은 PR #166의 실제 저장 저널이 Android 파일에서 프로세스 재시작을 견디는지 확인한다.
기존 `RevivalProgressProbe`의 단일 Cloud 키 검증과 별개다. 실제 외부 서버, 운영 계정,
전체 게임 진행도, 구매·No Ads 복원, 쓰기 도중 강제 종료, 물리 전원 차단,
16 KB 페이지 기기 및 스토어 검증은 포함하지 않는다. 회사폰 전체 재부팅이나 데이터 초기화는 하지 않는다.
