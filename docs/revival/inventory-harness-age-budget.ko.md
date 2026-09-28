# 격리 인벤토리 하네스의 연령 확인 대기와 준비 시간 분리

세 번째 기기 시도의 private operator20초와 하네스 Main Ready40초가 모두 연령 창 확인/입력에 소비될 수 있었습니다. 단순 timeout 확대 대신 두 시간을 분리하는 준비 변경입니다. 새 Editor/APK/기기 실행 없이 작성했습니다. 실제 인벤토리 검증 완료 또는 다음 기기 실행 승인으로 표현하지 않습니다.

## 하네스 source 변경

`RevivalGameplayHarness.WaitForScene`은 빌드 한정 `TAMER_INVENTORY_RESTORE`에서만 Main의 실제 원본 manager·살아 있는 Player·카메라/조이스틱/HUD·씬 전환 종료를 먼저 확인합니다. 그 상태에서 timeScale0이며 AgeSelection.NeedsQuestion이고, 원본 PopupManager에 바인딩된 활성 AgeChoicePresenter의 `_open`·`_modal.activeInHierarchy`·열린 현재 scene handle·광고/팝업 manager 소유와 해당 modal의 `_flowOwners` 등록을 reflection으로 읽기 확인한 경우에만 연령 창 대기 시간을 Ready40초 활동 예산에서 제외하고 `WAIT_AGE_CHOICE`를 기록합니다. 창이 닫힌 후 남은 활동 예산을 사용합니다. 단순 로드 지연이나 연령 질문 없는 일시정지는 계속40초에 종료됩니다.

하네스가 연령을 선택하거나 연령 PlayerPrefs를 쓰지 않습니다. 제품 `AgeChoicePresenter`, 광고/IAP 정책, Time.timeScale 변경 코드도 수정하지 않았습니다. `TAMER_AGE_CHOICE` 등 다른 하네스의 기존 흐름은 그대로입니다. 빌드 스크립트가 global inventory define을 거부하는 기존 경계도 유지합니다.

실제 `RevivalGameplayReadyBudget` source를 추출하여 시스템 .NET SDK9.0.301/target net9.0로 독립 컴파일·실행한 5경계가 통과했습니다: 120초 연령 대기를 활동 예산에서 제외, 선택 후 남은40초 유지, 40초 준비 실패 만료, 다른 일시정지 제외 없음, 음수 시간 거부. Unity Editor나 Android build로 컴파일한 결과가 아니며 전체 Unity 통합·실제 age 판정은 미검증입니다. 특히 다른 일시정지의 순수 bool=false 시험은 Unity predicate가 실제 다른 popup을 구분한다는 검증이 아닙니다. 원본 presenter/PopupManager의 private 필드가 없거나 비활성·다른 scene·소유/입력 blocker 불일치이면 예산을 멈추지 않는 조건을 소스로 추가했지만 Unity/IL2CPP에서 실행하지 않았습니다.

## private operator 단계

실행한 세 번째 helper/source는 비공개 별도 파일로 보존하고, 수정된 prepared helper를 별도로 작성했습니다. 현재 추가 사용자 승인 환경값과 **수정 하네스가 포함된 새 APK provenance**가 없으면 기기 설치 전에 중단합니다. 기존 APK로 이 시간 분리 변경을 검증할 수 없으므로 다음 실제 검증에는 새 APK가 필요합니다. 아직 빌드하지 않았습니다.

원래 연령 창 캡처를 private 파일로 보존하고, 앱ID·APK SHA·매 실행의 session token·설치 경로 SHA를 가진 operator-ready 자료를 만든 다음 기기 없는 신호 파일을 기다립니다. 이 별도 단계는 최대300초이며 취소 파일·own 설치 경로 변경·전면 앱 상실을 확인하면 own 앱 정리로 종료합니다. 각 대기 반복에서 소유권을 다시 확인합니다. 취소와 소유 가드의 우선순위는 유지하되 느린 가드 뒤 및 정상 신호 수락 직전에 monotonic deadline을 다시 확인하여, 이미 만료된 정상 신호도 거부합니다. install 성공 뒤 설치 경로를 확보하지 못하면 삭제를 추정하거나 시도하지 않고 `cleanupRefusedOrFailed=true`·`ownFreshInstallRemoved=false`와 정리 미확인/삭제 거부 상태를 결과에 기록하도록 준비했습니다. 이 준비 경로는 실제 기기에서 실행한 결과가 아닙니다. 신호가 없을 때 무한 대기하지 않습니다.

다음 승인된 절차에서 입력 담당자는 **그 실행의 실제 화면**에서 응답하지 않음 버튼을 확인하고, READY 자료와 현재 설치 경로 SHA/전면 앱을 대조한 한 도구 셀에서 해당 버튼 입력1회와 앱ID/SHA/session token이 일치하는 신호 기록을 연속 수행해야 합니다. 개인 연령을 선택하거나 제품 동의를 프로그램에서 우회하는 단계는 없습니다. 버튼 선택 뒤에만 write/restart 관찰의45초 시계가 시작합니다. Ready40초 활동 예산은 원래 연령 질문 대기와 분리됩니다. 신호 뒤에도 앱 소유 경로를 확인합니다.

private 실제 신호 helper의 독립 경계 5/5 통과(0.058초): 신호 부재 timeout, 다른 session 거부, 정상 신호보다 명시 취소 우선, 정상 신호보다 소유 경로 변경 우선, 닫힌 stdin에서 정상 신호 수락. deadline 수락 경계 변경에 필요한 추가 2/2도 통과(0.031초)했습니다: 느린 소유 가드 뒤 늦은 정상 신호 거부, 기한 내 정상 신호 수락. 앞의5경계를 변경 없이 재실행하지 않았습니다. 실제300초 대기나 실제 기기 왕복을 실행한 결과가 아닙니다. 전체 보조 절차의 Unity/기기 통합도 미검증입니다.

## 검증 기록과 한계

- source checkout `C:/Users/pc_17/.codex/worktrees/unity-lts-transition/Tamer`, branch `codex/inventory-harness-age-budget`, 기준 main `4a2d55e81a960e98c4d12e8c6996f121d4655eed`.
- 하네스 코드 커밋 `2230dba`. pure source 검증 및 private operator5경계 결과는 `Logs/revival/lts-readiness-budget-check*`와 `lts-operator-prepared-tests.txt`에 비공개 보존합니다.
- 기존meta/GUID·원본checkout·계정/서명·설정·실행된 APK와 이전1/2/3차 증거를 보존했습니다.
- 이번 변경의 Unity 전체 컴파일0·새 APK0·기기 설치0·네 번째 시도0·추가 RELRO 검사0입니다. 순수 로직 시험 성공을 실제 게임 렌더링/write/restart 성공으로 대체하지 않습니다.

앞의 기기 실패 중단 규칙은 그대로입니다. 다음 APK/기기 통합 검증은 이 수정의 리뷰와 구체적인 실행 범위 승인 후 수행해야 하며, 준비 변경만으로 추가 실행을 승인된 것으로 취급하지 않습니다.
