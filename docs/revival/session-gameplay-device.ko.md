# 게임 세션 콜백의 오프라인 Android 검증

2026-09-21, [세션 콜백 가드](gameplay-session-callbacks.ko.md)의 PR #205 이후 기기 검증이다. 기존 GameplayHarness/메모리 서버와 원본 Login/Main/Game/NextScene, Player, Monster 프리팹 및 NavMesh를 재사용한다. 새 씬이나 구매 에셋을 커밋하지 않는다.

## 격리와 재현 방식

`Run-GameplayHarness.ps1 -Variant sessionguard`는 빌드 한정 `TAMER_SESSION_HARNESS`/`TAMER_GAMEPLAY_HARNESS`/`TAMER_REVIVAL_SMOKE`를 사용한다. 앱은 `com.AeDeong.MonsterTamer.revival.sessionguard`, debug 서명, 백업/기기 전송 금지이며 INTERNET/ACCESS_NETWORK_STATE/BILLING/AD_ID 및 MobileAdsInitProvider를 제거한다. 일반 플레이어의 Monster 변경은 해당 define 안의 읽기 전용 사망 이벤트 관찰 호출뿐이다.

하네스는 자동 전투/생성 루프를 잠시 멈추고 원본 풀에서 Bat를 가져와 NavMesh에 배치한다. 합성 피해는 실제 `GetDamage`를 호출하며, 정상 보상은 원본 Die 클립이 자연 재생하여 AnimationEvent를 보낸 결과로 확인한다. 재현 가능한 늦은 이벤트와 삭제 대기 이벤트는 같은 원본 Animator를 `Update`로 진행시킨다. `AfterDie`를 reflection으로 직접 호출하거나 API mock의 성공값으로 판정하지 않는다. 관찰 hook은 도착 횟수만 기록하고 콜백 허용 여부를 바꾸지 않는다.

같은 합성 계정의 generation 교체는 실제 `BeginAccountSession`과 기존 메모리 서버 읽기를 사용한다. 삭제 대기/취소는 실제 `BeginDeletionSubmission`/`FinishCancelledDeletion` 로컬 경계를 호출하며 삭제 서비스 요청은 보내지 않는다. 늦은 이벤트를 다음 Monster.Update 이전에 전달하여 실제 rejection을 관찰한 뒤, 원래 생성기의 목록 해제와 새 generation의 원본 SpawnMonsters를 확인한다. 동료 재개는 실제 Player.AllyMove와 NavMesh 이동 거리로 확인한다.

## 결과

- checkout `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`, branch `codex/session-gameplay-harness`, 기준 main `27b3209`, 실제 빌드/기기 소스 `67a0be652b5f3cf94d31e5437bae41a8e0363bbf`(clean).
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android ARM64/IL2CPP/min24/target36, NDK 기준 `27.2.12479018`.
- APK 정적 verifier Python 5/5 통과, 비공개 에셋 4,561개 해시 일치/복사0. 원본 콘솔 결과와 별도로 로컬 요약을 남겼으며, 요약을 원시 시험 로그로 표현하지 않는다.
- 빌드 **1회 성공**, 기기 실행 **1회 성공**, 실패 재시도0. Company SM-N986N/Android13/user0을 설치 및 실행 전 다시 확인했다. 새 패키지는 미설치 상태였고 기존 앱 업데이트/삭제/데이터 지우기는 하지 않았다.
- 첫 실행의 실제 연령 선택 창에서 **응답하지 않음**을 선택했다. 이는 새 격리 앱의 선택이며 개인 연령이나 기존 앱의 선택값을 변경하지 않는다.
- `SESSION_DEVICE_PASS checks=10 errors=0`, 결과 파일 passed=true. 합성 Gold 1000→1025→1050, 최종 AllyMonsters=Bat, 화면 HUD와 저장 파일이 일치했다.

| 확인 경계 | 결과 |
| --- | --- |
| 원본 Game 씬과 로그인/IAP/광고 차단 | 통과 |
| Bat 자연 Die AnimationEvent의 현재 세션 보상 | +25, 한 번 |
| 자연 사망 후 보상값 유지 | 추가 증가 없음 |
| generation 교체 후 원본 Animator로 전달한 늦은 이벤트 | 도착 확인, Gold/동료 저장 변경 없음 |
| 이전 몬스터 비활성화와 원래 생성기 목록 제거 | 통과 |
| 새 generation의 원본 몬스터 생성 | 현재 세션 몬스터 생성 확인 |
| 삭제 대기 이벤트 보류와 기존 동료 유지 | 동일 동료/회차/활성 상태 유지 |
| 삭제 취소 후 실제 Update의 보류 보상 재개 | +25, 한 번 |
| 삭제 취소 후 동료 NavMesh 이동 | 0.2m 이상 이동, 보상 중복 없음 |
| 게임 검증 중 오류 및 서비스 차단 | 구독 이후 오류0, 광고/IAP 계속 차단 |

오류 카운터의 범위는 하네스 Run 구독 이후다. 시작부터 종료까지의 원본 로그를 별도로 검토했으며 초기 Development PlayerConnection의 소켓 blocking 실패 2건과 multicast 설정 실패 1건(E Unity)이 있었다. 인터넷 권한을 제거한 개발 플레이어의 초기 연결 진단이며, 전체 앱 로그 오류0으로 주장하지 않는다. 게임 검증 구간의 예외나 추가 오류는 발견하지 못했다. 실제 서비스 로그인·구매·광고 네트워크 성공 검증은 수행하지 않았다.

## 증거와 종료 상태

APK는 **105,575,287 bytes**, SHA256 `08f0dc4d56ec5fdb18fa04718e2c779c04db4a783f424e4e6e5c6c6d916dddb4`이며 설치된 base.apk도 같은 해시다. 패키지/ARM64/debug 서명/min24/target36 및 금지 권한·provider 부재, 백업/전송 제외 규칙을 verifier로 확인했다. 광고 관련 모든 권한이 없다는 의미는 아니다.

로컬 비공개 증거는 `Logs/revival/sessionguard-*`에 보존한다. 기기 식별자·합성 저장 원본·로그·구매 에셋이 보이는 화면은 공개 커밋에 넣지 않는다.

- 기기 결과 JSON SHA256: `be84c93026c60bc1cbe0b6840008da223c032af70da0d6abdb70c6d0ba411fd7`.
- 시작부터 종료 전까지 로그 SHA256: `1976503ddec3480f9f734bac9992407c05bbcb81c756787286a65bb617e1c7bb`.
- 결과 화면 SHA256: `7b01213dd90470f824c7c39b80de5433b2ede830eaea435b9e13052fe1570d56`.

빌드 후 해당 checkout Editor0, 설정 스냅샷 복원 및 URP/Graphics 줄바꿈 자동 변경 복원, git clean을 확인했다. 검증 앱만 force-stop하여 PID가 없고, 새 앱과 그 증거를 포함한 모든 앱 데이터는 유지했다. 전후 Tamer 패키지 목록은 새 sessionguard 추가 외에 동일하다.

이 결과는 합성 세션의 실제 사망 이벤트·저장·생성기·NavMesh 경계를 확인한다. 실제 인증 전환, 원격 삭제 서비스, 일반 전투 밸런스, 다른 기종, 출시 AAB, #91 광고 정책/스토어 검증까지 완료했다는 의미는 아니다.
