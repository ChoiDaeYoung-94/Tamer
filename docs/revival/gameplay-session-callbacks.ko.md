# 게임 세션별 몬스터 콜백 경계

같은 계정의 재로그인으로 generation이 바뀌거나 DataManager가 교체되었을 때, 이전 몬스터의 사망 애니메이션 이벤트가 새 세션의 Gold 또는 AllyMonsters를 변경하지 않도록 한다. 기존 BeginAccountSession은 다른 소유자의 정상 로그인을 거부하므로, 일반적인 A→B 계정 전환이 항상 가능하다는 전제는 두지 않는다.

몬스터는 활성화 때 DataManager 참조·계정 소유자·generation·풀 활성화 회차를 묶는다. 사망 이벤트와 포획은 현재 세션 및 해당 회차를 확인하고, 처치 보상은 한 번만 소비한다. 플레이어가 선택한 공격 대상과 달라도 현재 세션의 정상 처치에는 골드를 지급한다. 풀 재활성화는 이전 사망 상태와 애니메이터 상태를 초기화한다.

Player.ClearInventorySession은 전투와 이전 동료를 정리하고 골드 캐시를 비운다. 새 ready 세션의 RefreshInventorySession은 관리자 참조까지 비교하여 골드와 동료 목록을 저장값에서 다시 구성한다. 목록에 없는 동료의 사망 콜백은 저장 목록을 다시 쓰지 않는다. 이를 통해 새 세션의 주기적 저장에 이전 메모리 값이 섞이는 것을 막는다.

삭제 확인 대기의 ready=false/DeletionInProgress는 실제 세션 교체와 구분한다. 같은 관리자·소유자·generation이면 몬스터를 영구 반환하지 않고 동작을 멈춘다. 이때 도착한 사망 이벤트는 보류하고, 취소 후 같은 세션이 ready로 돌아오면 한 번 처리한다. 실제 Suspend/새 generation은 이전 콜백을 거부한다. 세션 종료로 반환하는 몬스터는 캡처한 원래 생성기의 활성 목록과 보스 참조에서도 해제한다. 교체된 생성기의 목록은 변경하지 않는다.

## 2026-09-21 검증

- checkout: `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`, branch: `codex/session-reward-guards`, 기준 main: `3d34d3032b102e9a394babe0f0eee04ba067555f`.
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`; Android 기준 min24/target36/ARM64, NDK `27.2.12479018`. 이번에는 NDK 빌드나 기기 실행을 하지 않았다.
- 비공개 에셋 해시 4,561개 검증, 복사 0. `.meta`/기존 GUID 및 계정·저장·구매 설정은 보존했다.
- 소스 `171958c92ad1fbad8208236e599951f169a42c82`: 새 세션 가드·몬스터 라이프사이클·기존 인벤토리 전환 EditMode 53/53 통과(첫 실행), 실패/skip 0. 결과 `Logs/revival/session-reward-tests.xml`, SHA256 `bde59171e4a8a7a9619645e84826eb10106cdddddb6cfd247e7f0fd492b59707`.
- 리뷰 후 생성기 등록해제와 삭제 대기 복귀를 보완했다. 추가 필터의 첫 시도는 NUnit 개수 비교 표현식 미지원으로 테스트 실행 전 컴파일 실패했다. 표현식만 수정한 소스 `d4af2a55847208f84509d67e52e1c0e672fb76a1`에서 두 번째 시도 6/6 통과, CLI exit0, 실패/skip0. 변경 없는 53건 전체는 반복하지 않았다.
- 추가 결과 `Logs/revival/session-reward-followup-tests.xml`, SHA256 `0015381b860696fbb26c1a9b6edb7325aba62ca9a98843a9510214b826756e5b`. 첫 실패 로그와 모든 원본 결과는 비공개 로컬에 보존한다.
- 추가 검증은 원래 생성기만 등록해제/반복해제, 보스 정리, 삭제 취소 후 동료·회차 유지, 지연 사망의 같은 세션 1회/새 generation 0회, 주입된 EditMode fixture의 `Update→AfterDie→Gold` 저장 1회를 포함한다.
- 해당 checkout Editor PID0, ProjectSettings 스냅샷 복원 및 자동 직렬화 변경 복원 확인. 최초 복원 래퍼의 인자 오류는 올바른 인자로 백업 복원을 수행해 해결했다.

실제 애니메이션 재생, NavMesh 전투, 기기에서의 재로그인·삭제 취소 흐름, 서버 통신과 스토어 동작은 미검증이다. APK는 생성하지 않았으므로 APK SHA256은 해당 없다. 이 검증은 #92 기준 빌드 또는 #91 광고 정책·스토어 검증 완료를 의미하지 않는다.
