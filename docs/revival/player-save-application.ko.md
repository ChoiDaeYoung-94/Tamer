# Player 저장 데이터 적용 경로와 숫자 형식 수정

2026-09-21 조사에서 서버의 점 소수 문자열을 현재 기기 문화권으로 읽는 결함을 확인했다. `de-DE`에서 `0.6`의 점은 천 단위 구분자로 해석될 수 있고, `fr-FR`에서는 점 소수 읽기가 실패할 수 있다. 동기화 검증과 Player 기본 능력치 읽기에 `InvariantCulture`와 `NumberStyles.Float`를 함께 적용했다. 쉼표 소수와 천 단위 구분자를 추측해 변환하지 않으며, 잘못된 저장값은 기존 복구 거부 경로로 보존한다. Gold 검증도 고정 정수 형식을 사용한다.

## 기존 8키의 적용 경로

| 값 | 적용 지점 |
| --- | --- |
| NickName | PlayerUICanvas.StartInit의 이름 및 정보 표시 |
| Sex | SetCharacterState.Handle의 Player_Man/Player_Woman prefab 선택 |
| Tutorial | CheckTutorialState.Handle의 분기. 실제 튜토리얼은 TODO 상태 |
| Gold | Player.Init의 정수 읽기 |
| Power / AttackSpeed / MoveSpeed | Player.Awake → Creature.Settings의 기본 능력치 읽기 |
| AllyMonsters | Player.Init → SettingAllyMonster의 pool 소환과 AllySetting |

이후 장비가 능력치에 더해지며 JoyStick에 이동 속도가 적용된다. 서버 AllyMonsters는 현재 동료 목록이고, 같은 이름의 PlayerPrefs 키는 수집 목록이다. 둘은 동일한 의미의 저장소가 아니다.

## 검증

- checkout: `C:/Users/pc_17/.codex/worktrees/7299/Tamer`
- 수정 소스: `53f31ead78e5039c9c3af8a381aa8fb088b9a235`
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Editor EditMode
- 관련 `RevivalDataSyncTests` 62/62 통과, 실패 0회. 신규 5개 포함, 실행 1회
- 신규 검증: en-US/de-DE의 단계 1, fr-FR의 단계 2 합성값을 Merge 후 실제 Player 인스턴스의 Creature.Settings로 읽고 Power/AttackSpeed/MoveSpeed를 확인. 두 모호한 숫자 구분자 거부도 확인
- 모든 테스트 객체를 비활성 상태로 만들고 서비스 초기화·Player.Awake·PlayerPrefs 접근을 실행하지 않았다. 임시 Managers 참조와 문화권은 finally에서 복원했다. 운영 SDK/로그인/구매/광고 호출 없음
- 비공개 결과 `Logs/revival/player-stat-culture-tests.json`, SHA-256 `6b3d30978afd0fb1ff31fcc7fc17b52419c94f3d4dc91bcedc857568aae9fb82`
- 본인 Editor 정상 종료 후 시작 과정에서 비워진 Android key alias만 원복했다. 에셋과 GUID 변경 없음

이번에는 APK를 빌드하거나 Android 기기에서 수정 코드를 실행하지 않았다. 따라서 새 APK SHA-256과 Android 실행 결과는 없다. PR #175의 Android 16 Cloud 검증은 이전 APK의 별도 근거다. 실제 씬 전체의 prefab 생성·동료 소환·장비·UI 동작, 16 KB 런타임, #91 광고/스토어 및 #92 기준 빌드를 이번 테스트로 판정하지 않는다.

## PlayerPrefs 이관 정책의 미결정 사항

수집 목록 `AllyMonsters`, 장착 목록 `playerEquippedItems`, 소유 아이템 `LocalItem`에는 계정 소유권 근거가 없다. 자동 업로드·병합은 구현하지 않았다.

권장안은 legacy 값을 기기에 보존하면서 신규 계정별 저장소를 별도로 시작하는 것이다. 기존 목록 이전이 꼭 필요하면 사용자가 원본과 대상 계정을 확인한 뒤 목록을 미리 보고 명시적으로 선택하는 일회성 가져오기를 별도로 설계할 수 있다. 어느 경우에도 소유권 없는 목록을 로그인한 계정에 자동 귀속시키지 않는다. 실제 이관 구현 전에 이 두 정책 중 선택이 필요하다.
