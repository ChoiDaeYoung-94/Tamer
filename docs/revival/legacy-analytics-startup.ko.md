# 구매 기능을 보존하는 legacy Analytics 초기화 설정

2026-10-20, Unity **6000.3.25f1 / e1dba0a9aba4**의 실제 Editor에서 확인한 결과입니다. 최종 Android Player의 네트워크 수집이나 스토어 구매 복원 검증을 대신하지 않습니다.

## 변경

`UnityEditor.Analytics.AnalyticsSettings.initializeOnStartup=false`를 public Editor API로 저장했습니다. `PurchasingSettings.enabled=true`와 `AnalyticsSettings.enabled=true`는 유지합니다. 같은 API 저장 과정에서 현재 Editor가 생성한 Insights/CrashReporting 설정 형식도 함께 반영합니다. 진단 관련 저장 값은 0이며, 이를 모든 Unity 서비스의 데이터 처리가 중단됐다는 증거로 사용하지 않습니다.

처음에는 Analytics의 enabled와 startup을 둘 다 false로 설정했으나, 실제 getter에서 Purchasing도 false가 되어 보존 조건 검사가 실패했습니다. 즉시 public Purchasing setter로 구매 기능을 true로 복구했고 Analytics enabled도 true로 돌아오는 것을 확인했습니다. 따라서 구매 기능을 희생하면서 Analytics 전체 OFF를 주장하는 변경은 채택하지 않았습니다. 이 연동의 native 내부 구현 전체를 확인한 것은 아닙니다.

IAP 5.4.3의 `PurchasingServiceEnabler.EnableLocalSettings`에도 구매 서비스 활성화 시 `AnalyticsSettings.enabled=true`를 쓰는 코드가 있습니다. 이후 서비스 재설정 때는 startup 값과 구매 기능을 다시 확인해야 합니다.

## 검증

- 통합 후보 소스 `91d32ecb027ba4c56befbce5ae5a2772273a27fc`에 public API 변경을 적용했습니다. CLI `1.0.0-beta.8`, Pipeline `0.6.0-exp.1`과 정확한 프로젝트 경로를 사용했습니다.
- 첫 실행의 포트/PID/프로젝트 소유권 확인 후 `editor_status`는 ready, compiling=false, Play Mode stopped로 응답했습니다. 과거 두 번의 연결 timeout 이력은 남기며, 이번 성공만으로 과거의 유일한 원인을 확정하지 않습니다.
- 저장 직후와 정상 종료 후 새 Editor 프로세스로 재시작한 뒤 모두 Analytics enabled=true, initializeOnStartup=false, Purchasing enabled=true를 실제 getter로 확인했습니다. deviceStatsEnabledInBuild는 true였으며 전체 데이터 수집 OFF로 표현하지 않습니다.
- `RevivalIAPConfigurationTests`를 한 번 실행해 **3/3 통과, 실패 0, skip 0**의 비동기 완료 결과를 확인했습니다. No Ads 비소모품 카탈로그, 서비스 자동 초기화 비활성, 순수 동의 기본값/명시 선택 보존을 검사하며 실제 계정·구매·영수증 API는 호출하지 않습니다.
- Editor 시작으로 생긴 별도 PlayerSettings/URP 직렬화 변화는 비공개 원본과 실제 변경을 대조한 뒤 원바이트로 복구했습니다. 해당 변화나 서명 설정을 이 PR에 섞지 않습니다. `.meta`, GUID, 기존 키와 구매 권한을 보존했습니다.

## 남은 확인

새 Android 시험 AAB의 실제 설정·동의 동작, IAP Insights/Core/Diagnostics 각각의 처리, 기존 구매 계정의 영수증 복원과 재시작 후 No Ads 유지는 별도 검증이 필요합니다. legacy startup 한 값으로 모든 SDK 전송이 동의 전 차단된다고 안내하지 않습니다. 기존 시험 29번과 운영 제공 빌드에는 이번 설정이 들어갔다고 표시하지 않습니다.

API 의미는 [Unity 6.3 initializeOnStartup](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/analytics/analyticssettings/initializeonstartup)과 [AnalyticsSettings.enabled](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/analytics/analyticssettings/enabled)를 참고하되, 실제 이 patch의 구매 기능 연동 관측을 구분합니다.
