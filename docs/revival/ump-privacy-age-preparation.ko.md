# 연령 변경 개인정보 옵션의 격리 UMP 시험 준비

2026-10-06 main `6ce2b63df10dfbf384e4b69ada712656f92bc0aa`에 병합한
개인정보 owner 보존 수정의 실제 SDK 검증을 준비했다. checkout은
`C:/Users/pc_17/.codex/worktrees/ad-production-completion/Tamer`, 작업 브랜치는
`codex/ump-privacy-age-device`다. 운영 광고·로그인·저장·구매는 실행하지 않았다.

기존 UMP-only 시험 화면은 연령 버튼에서 gate를 폐기하므로 새 보존 경로를 실행하지
못했다. 새 명시적 시험 버튼은 실제 `AdConsentGate.SuspendAndRetainPrivacy`를 호출해
합성 Adult를 Declined로 바꾸고 기존 Required owner를 보존한다. 이후 새 Update는
차단하며 개인정보 옵션만 명시적으로 열 수 있다. 다른 시험 조건과 reset은 프로세스
재시작까지 잠근다. Adult의 기존 owner가 NotRequired이면 다음 폼을 예약하는 버튼도
차단해 진입할 수 없는 잠금 상태를 피한다.

다음 Gather/Privacy 호출 후 2초에 연령을 바꾸는 선택 경로는 전후 busy·Required·광고
허용 상태와 실제 callback으로 busy가 해제되는 시점을 기록한다. 이 호출 trace는
native 화면 표시의 증거가 아니다. 실제 폼 표시와 busy 유지가 관측되지 않으면
native-open 시험 성공으로 판정하지 않는다. 화면의 컨트롤 개수는 조건에 따라 바꾸지
않으며, manager 없는 기존 격리 UMP-only 경로를 유지한다.

`BuildUmpPrivacyAge`는 기존 게시자 UMP 빌드의 opt-in·별도 package·debug signing·
development·IL2CPP·격리 씬·finally 경로를 사용한다. 출력만 새 파일로 분리해 기존
publisher APK와 session APK를 보존했다. production·지역·연령 승인 false와 readonly
null binding, manager·No Ads·보상 로직·SDK 설정·서명키는 변경하지 않았다.

## 실제 Unity 컴파일 결함과 빌드 결과

첫 실제 Unity 빌드는 소스 `69b65d022b65f4c4b33e024acda13c0e296cff01`에서 CLI exit6으로
실패했다. `AD.Advertising.asmdef`가 `noEngineReferences:true`인데 최근 추가된
`PrivateAdsReleaseContract`는 UnityEngine 런타임 API를 사용한 것이 원인이다.
이전 선택 소스의 순수 컴파일은 이 실제 어셈블리 참조 경계를 대체하지 못했다.
실패 당시 보호 파일 변경·새 Assets·누락은 0이고 Editor 종료와 clean을 확인했다.
원래 manifest·receipt·원시 로그는 비공개로 보존했다.

이미 존재하는 런타임 의존성을 정확히 선언하도록 `noEngineReferences:false` 한 값만
수정했다. 정책 함수의 판단이나 광고 허용을 늘리지 않으며 순환 참조·소스 경로 이동·
binding 변경도 없다. 독립 검토와 총괄의 두 번째 빌드 승인 후 실제 소스
`28614976963853804befd430e1f3cf43e0c7947d`에서 두 번째 빌드가 CLI exit0으로 성공했다.
세 번째 실행은 0이다. 다른 기존 두 실패 원장이나 승인된 재시도 이력을 지우지 않았다.

- Unity `6000.3.25f1`, revision `e1dba0a9aba4`, CLI `1.0.0-beta.8`.
- APK `Build/revival/Tamer-ads-ump-publisher-privacy-age.apk`, 98,532,311 bytes.
- SHA-256 `75ea6ab27ab4e08c2b44c4768624e553859471b9531d83e3e49ab597f97e6263`.
- 별도 package `com.AeDeong.MonsterTamer.revival.umppublisher`, debug certificate·debuggable,
  version `1.0.5` / code26, min25·target36·ARM64-only: 새 APK 정적 검사 첫 실행 PASS.
- 실제 merged manifest의 게시자 App ID와 비공개 입력 일치를 확인했으며 값은 공개하지
  않았다. MobileAdsInitProvider와 AD_ID는 존재하므로 모든 native 통신·수집 0의 근거는 아니다.
- 새 player의 Advertising·Assembly-CSharp response에 현재 contract/harness 소스,
  UnityEngine.CoreModule 및 네 개 격리 define가 포함되고 UNITY_EDITOR가 없음을 확인했다.
  response가 이번 invocation 이후 갱신된 것도 확인하고 비공개로 보존했다.
- 실제 변경 8개와 생성 2개를 독립 검토한 뒤 동일 handle로 원바이트 복원·생성 파일 보관
  후 제거했다. 보호 6,865개와 전체 Assets 6,829개의 대조·GUID 보존·clean·Editor 종료를
  확인했다. 기존 APK·key·Library·Logs·다른 checkout은 보존했다.

## 남은 기기 검증과 증거 범위

기존 승인 테스트 기기와 ADB 목록의 단일 기기 일치는 확인했으나 상태는 `unauthorized`였다.
그 상태에서 설치 APK·앱 데이터 조회, 설치·실행·UMP Update·폼·광고 실행은 0이다.
추가 폴링은 하지 않았다. 시험 기기의 USB 디버깅 신뢰 승인 후 설치된 격리 앱의 출처·
APK SHA·debug 서명과 데이터 보존 범위를 먼저 확인해야 한다.

다음 최소 관측은 기존 EEA 시험 지역과 합성 Adult에서 발견한 Required 옵션을 기준으로
같은 프로세스의 Declined 전환·명시 옵션 접근·native 폼이 열린 동안의 전환과 실제
completion을 확인하는 것이다. 운영 광고 활성화나 production flags true가 필요하지 않다.

이는 UMP-only gate와 공유 owner transfer의 native SDK 관측이다. 실제 production manager·
LocalAgeChoice edit/Select·게임 개인정보 UI·앱 재시작·지역별 정책 적합성·No Ads 전체 흐름의
검증으로 확대하지 않는다. 다른 sample manager의 고정 Adult 설정도 이 흐름의 대체 증거가
아니다. 프로세스 재시작 시 owner는 사라지므로 cold restart의 기존 옵션 복구는 별도 미검증이다.
`runtimePrivacyVerified/productionManagerFlowVerified/coldRestartPrivacyVerified/regionalPolicyVerified/
distributable=false`를 유지하며 debug APK 정적 성공을 출시 AAB·스토어·16KB strict 승인으로
표현하지 않는다.
