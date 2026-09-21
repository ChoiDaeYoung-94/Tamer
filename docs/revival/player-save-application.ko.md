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

## 실제 씬 검증 공백 조사

기존 `RevivalGameplayHarness`는 원본 Main/Game 씬을 실행하지만 seed가 남성·Tutorial=done·동료 없음으로 고정되어 있다. Ready/왕복 검사만으로 여성 prefab 또는 저장 동료 소환, HUD/팝업의 저장값 표시 성공을 판정할 수 없다.

`SetCharacterState`는 Sex로 prefab 경로를 선택하고, Player는 저장 동료의 이름마다 pool에서 꺼내 AllySetting을 실행한다. 코드 경로 확인만으로 실제 prefab 참조·NavMesh 배치 성공을 판정하지 않는다. 현재 조사에서 여성 전용 분기나 저장 동료에 대한 확정 결함은 찾지 못했다.

튜토리얼은 `InitializeMain → LoginCheck → SetCharacterState → CheckTutorialState`로 연결된다. 저장값이 `null`이면 TODO 로그만 남기며 시작 화면·단계 실행·완료 기록 기능은 이 경로에 없다. `done` 복원은 이 분기를 건너뛰는 것까지이며, 새 튜토리얼 제품 기능을 이번 복구에 추가하지 않는다.

후속 실제 씬 검증은 기존 오프라인 gameplay 하네스에서 여성·저장 동료가 있는 fixture로 실행하고 prefab/UI/동료 수·NavMesh를 관측하는 범위가 필요하다. 기존 앱의 owner 없는 장비/수집 PlayerPrefs가 영향을 줄 수 있으므로 해당 저장소 보존·격리 방식부터 확정해야 한다. 운영 서비스 연결이나 legacy 자동 병합으로 이 공백을 채우지 않는다.

구체적 후속안은 기존 gameplay 빌더·하네스에 검증 모드 하나를 추가하여 별도 패키지 `com.AeDeong.MonsterTamer.revival.playerrestore`를 사용하는 것이다. 기존 앱 업데이트나 삭제 없이 OS 앱 저장소 자체를 분리하고 자동 백업/복원도 끈다. 첫 실행에 세 legacy 키가 존재하면 읽기·삭제·이관 없이 검증을 중단한다. 기존 메모리 transport의 seed만 여성·동료 fixture로 선택하며 원본 Main/Game 경로와 네트워크 제거 manifest를 재사용한다. 패키지 허용 검사는 해당 모드에서만 확장한다. 이 안은 아직 구현·빌드하지 않았고 실제 씬 성공 증거가 아니다.

## HUD·팝업 표시 후속 검증

동일 checkout, Unity `6000.0.81f1` / CLI `1.0.0-beta.8`에서 소스 `6d78d9c52618f5ee361f710d50f81e21998e6a34`의 신규 테스트 2개만 실행해 2/2 통과했다(실패 0, 실행 1회). 기존 `RevivalGameplayIsolationTests`를 확장했으며 런타임 코드나 별도 하네스를 추가하지 않았다.

비활성 Player의 기본 능력치를 실제 Settings로 읽고 Gold는 합성값으로 주입한 뒤, 실제 `PlayerUICanvas.DataSettings`를 호출했다. 실제 TMP 텍스트 컴포넌트에서 두 단계 닉네임·Gold의 HUD/팝업 표시, 팝업 Power/AttackSpeed/MoveSpeed, 기본 HP 표시를 확인했다. Gold 초기화 전체, 동료 소환과 화면 렌더링 자체는 이 테스트의 범위가 아니다. preview scene과 임시 singleton/문화권을 복원하며 PlayerPrefs와 외부 서비스에 접근하지 않았다.

비공개 결과 `Logs/revival/player-hud-restore-tests.json`의 SHA-256은 `27a4cb1db4106053dd3f8e027afd64422c3b1da7e12e649106d7561e3cf6d941`이다. Editor 종료/PID 없음 확인 후 시작 시 비워진 key alias를 원복했다. 새 APK·Android 실행과 해당 SHA-256은 없으며 기존 PR #175의 기기 증거를 재사용해 이번 코드 실행으로 주장하지 않는다.

## PlayerPrefs 이관 정책의 미결정 사항

수집 목록 `AllyMonsters`, 장착 목록 `playerEquippedItems`, 소유 아이템 `LocalItem`에는 계정 소유권 근거가 없다. 자동 업로드·병합은 구현하지 않았다.

권장안은 legacy 값을 기기에 보존하면서 신규 계정별 저장소를 별도로 시작하는 것이다. 기존 목록 이전이 꼭 필요하면 사용자가 원본과 대상 계정을 확인한 뒤 목록을 미리 보고 명시적으로 선택하는 일회성 가져오기를 별도로 설계할 수 있다. 어느 경우에도 소유권 없는 목록을 로그인한 계정에 자동 귀속시키지 않는다. 실제 이관 구현 전에 이 두 정책 중 선택이 필요하다.
