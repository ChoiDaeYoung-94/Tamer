# Android 원자적 저장 교체 경계의 프로세스 중단

2026-09-14 회사폰 SM-N986N(Android 13, ARM64, 4 KB 페이지)에서 실제 `DataManager.WriteAtomically`의
임시 파일 기록 완료 직후와 원자 교체 직후에 외부 `adb force-stop`으로 전용 앱을 종료했다.
재실행한 실제 DataManager가 이전 또는 새 파일의 값·pending·revision을 일관되게 읽는 것을 네 경우 모두 확인했다.

## 주입과 중단 방법

`File.WriteAllText`가 임시 파일을 닫은 직후 `temporary-closed`, `File.Replace`/`File.Move` 직후 `replaced`를 호출한다.
hook 필드와 두 호출은 모두 `UNITY_EDITOR || TAMER_JOURNAL_HARNESS`에서만 컴파일된다.
callback은 정규화한 전체 경로와 선택한 checkpoint가 정확히 일치할 때 한 번 실행된다.
기존 fixture 준비에는 hook을 사용하지 않는다.

각 실험은 앱의 `AtomicBoundariesV1/<case>/JournalHarnessV1/JournalPlayerData.json`을 사용한다.
이미 case 폴더가 있으면 실행을 거부한다. marker를 닫아 기록한 뒤 앱은 최대 4초 대기하고,
종료되지 않으면 timeout 파일을 남긴다. 호스트 watcher는 사전 경로 부재, 회사폰 모델·serial,
새 marker의 case/checkpoint/파일/PID 및 현재 PID 일치를 확인한 뒤 정확한 journal 패키지만 force-stop한다.
monotonic 시간 제한, 종료 후 PID 없음, timeout 없음과 다음 실행의 새 PID를 별도로 대조한다.
기존 marker·timeout·PID 불일치·지연은 성공으로 처리하지 않으며 실패 파일을 덮어쓰지 않는다.

앱의 최종 PASS는 파일 재로드 검증이다. 실제 중단의 증명은 호스트 PID·시간·marker 근거를 함께 보아야 한다.
watcher와 원본 파일은 비공개 `Logs/revival/journal-boundaries`에 보존하며 자동 정리하지 않았다.

## 결과

| 경우 | 종료까지 시간¹ | 재실행 후 Gold | pending | revision | 임시 파일 |
|---|---:|---:|---|---:|---|
| 값 변경, 교체 전 | 0.572초 | 20 | Gold=20 | 1 | 새 Gold=30/rev2 파일 잔존 |
| 값 변경, 교체 후 | 0.405초 | 30 | Gold=30 | 2 | 없음 |
| ack, 교체 전 | 0.399초 | 20 | Gold=20 | 1 | pending 빈 객체의 새 파일 잔존 |
| ack, 교체 후 | 0.416초 | 20 | 빈 객체 | 1 | 없음 |

¹ UI 버튼 입력 직전부터 외부 force-stop 완료까지의 monotonic 측정값이다.

모든 case에서 timeout 없이 PID가 사라지고 다음 실행의 PID가 달라졌다.
최종 실행에서 네 파일을 실제 DataManager로 읽었고, 읽기 전후 저장 파일의 바이트가 동일했다.
교체 전 두 case의 임시 파일도 그대로 남았다. 기존 PR #168 저장 파일은 APK 업데이트 전후와 실험 종료 후 동일했다.
새 APK는 이전 APK와 서명 인증서가 일치하며 동일 전용 패키지를 데이터 보존 업데이트했다.
이전 APK는 `Build/revival/Tamer-journal-pr168.apk`에 보관했다. 최종 앱은 종료하고 폰·Editor·빌드 슬롯을 반환했다.

## 검증 정보와 한계

- checkout: `C:/Users/pc_17/.codex/worktrees/7299/Tamer`
- APK 소스: `e1d08a42839d7be63036bfc843a23296c72ee9bf`
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, debug ARM64, min SDK 24 / target SDK 36
- APK SHA-256: `3963cd8ebc270f62cec7f6dcf58223b6d28d76edb0b885cdc8a9d2cf2c1a5a6f`
- 관련 Editor 회귀 `Revival_Journal;Revival_Data;Revival_SaveQueue`: **69/69 통과**. 기존 전체 420개를 불필요하게 반복하지 않았다.
- malformed 원본과 불완전 임시 파일을 건드리지 않고 로드를 거부하는 별도 Editor fixture 회귀도 통과했다.
- 최종 APK의 인터넷·결제·광고 ID 권한 없음, debug 서명, 패키지와 ABI 검사 통과. GUID unresolved 0.
- LOAD/ZIP 통과, 별도 strict RELRO 끝 정렬 검사 4개 실패. 16 KB 실행은 미검증이다.
- [기계 판독 검증 기록과 원본 해시](journal-boundary-validation.json)

이는 닫힌 임시 파일의 교체 경계에서 프로세스를 종료한 검증이다. 바이트 기록 도중 종료나 물리 전원 차단의 내구성을 보장하지 않는다.
Android 실험에서 손상된 원본이 발생한 것은 아니며 malformed 보존 회귀와 구분한다.
외부 서버·계정·Google·IAP·전체 게임 진행도·스토어 검증은 하지 않았다. 일반 플레이어 제외는 소스 컴파일 가드로 확인했으며 운영 APK를 재빌드하지 않았다.
