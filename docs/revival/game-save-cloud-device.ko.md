# Cloud 8키 개인폰 저장·복원 검증

2026-09-21, PR #172의 기존 APK로 개인폰에서 두 단계 합성 데이터의 실제 서버 저장·조회와 복원을 완료했다. 실패는 0회다. 실제 Player 씬의 플레이·화면 반영을 검증한 결과는 아니다.

## 실행 기준

- checkout: `C:/Users/pc_17/.codex/worktrees/7299/Tamer`, 실행 시 커밋 `b40787c1fa78f00277deb42266f46c4adf95c381`
- APK 소스 `3f155a3f37867e2c97be602336fe5ee04e11bd9d`, 구현 병합 `d9307704dea815894d98a78816d9108ac79b7620`
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, 개인폰 SM-S938N / Android 16
- 패키지 `com.AeDeong.MonsterTamer.revival.gamesavecloud`, debug ARM64, min SDK 24 / target SDK 36
- 기존 APK SHA-256 `5d8431d55b78c579ebc140c2258ad09b740c774f42bc07fa387c5caeccc17aef`를 설치 전에 확인했다. 재빌드하지 않았다.

사용자가 새 gameplay 전용 계정을 수동 생성했다. 신규 계정 개요에서 생성일과 gameplay namespace 연결을 확인했고, 앱의 예상 계정 일치 검사를 통과한 인증 성공을 관측했다. 최초 및 두 번의 재시작 후 인증값 입력·제출은 모두 사용자가 직접 수행했다. 기존 구매·progress 계정은 재사용하지 않았다.

## 결과

최초 primary 읽기는 step=0, pending=0, reads=1, writes=0이었다. 이후 각 비동기 요청이 busy=false, failed=false로 완료된 것을 확인한 뒤 명시 Verify의 PASS를 관측했다.

| 검사 | 단계 1 | 단계 2 |
| --- | --- | --- |
| 로컬 준비와 pending 검증 | PASS, 8개 | PASS, 7개(Tutorial 동일) |
| 실제 Cloud 업로드와 재조회 | PASS, reads=2 / writes=1 | PASS, reads=2 / writes=1 |
| 앱 프로세스 종료·재실행 후 primary 복원 | PASS, reads=1 / writes=0 | PASS, reads=1 / writes=0 |
| 새 읽기 전용 저장소 복원 | restore1 PASS, writes=0 | restore2 PASS, writes=0 |

| 키 | 단계 1 | 단계 2 |
| --- | --- | --- |
| NickName | GameplayFixtureA | GameplayFixtureB |
| Sex | Man | Woman |
| Tutorial | done | done |
| Gold | 120 | 235 |
| Power | 11 | 12 |
| AttackSpeed | 0.6 | 0.7 |
| MoveSpeed | 3.2 | 3.4 |
| AllyMonsters | Bat | Bat,Magma |

각 acknowledged 검증에서 8개 값 일치와 pending=0을 확인했다. 두 재시작은 해당 앱만 force-stop한 뒤 PID 소멸과 새 PID 생성을 확인했다. 기존 primary를 삭제하지 않고 새 restore1/restore2를 각각 한 번만 열었다. 마지막에 세 저장소의 JSON을 비공개로 보존하고, 인증 해제 화면과 앱 프로세스 종료를 확인했다. 앱 데이터·계정·로컬 증거는 삭제하지 않았다. 이 검증에 추가 개인폰 입력은 필요하지 않다.

## 증거와 검증 범위

[기계 판독 결과](game-save-cloud-device-validation.json)에 비공개 화면·저장소·프로세스 기록의 SHA-256을 남겼다. 원본은 `.revival-local/gameplay-cloud-handoff/`에 보존한다. 인증값·티켓·계정 식별값·기기 일련번호와 원시 JSON은 공개하지 않는다.

이번 변경은 문서만 추가하며 기존 통과 회귀를 재실행하지 않았다. 이전 빌드의 LOAD/ZIP 통과와 strict RELRO 끝 정렬 5개 실패는 [빌드 기록](game-save-cloud-validation.json)의 별도 결과로 유지한다. 이번 Android 16 실행 성공만으로 16 KB 런타임 호환성을 판정하지 않는다.

실제 Player 씬 복원, PlayerPrefs 인벤토리 서버화, Google 로그인·구매·광고·스토어 동작은 미검증이다. #92 기준 빌드와 #91 광고 정책·기기/스토어 검증의 완료로 확대 해석하지 않는다.
