# 광고 복구 구현과 검증 (#91)

작업 브랜치: `codex/revival-families-ads-91`. 기준은 #95의 `c1613ab`에서 시작해
안전성 보완을 포함한 main `c845364`를 병합했다. 정책 근거는
[Families 정책 조사](families-ads-policy.ko.md)에 기록한다.

최신 Editor 검증: 광고 수정 `b4764b4`를 포함한 SDK 11.5.0 통합 커밋
[`f5b7e404`](https://github.com/ChoiDaeYoung-94/Tamer/commit/f5b7e404d7bd2c797aed2c9a3c17416a2ef50ccf)에서
**248/248 통과, 실패 0, 건너뜀 0**을 확인했다. 같은 소스의 격리 개발 APK 빌드·메타데이터·
서명·LOAD/ZIP 검증도 완료했다. 엄격한 RELRO 검사와 실제 16KB 기기 검증의 경계는 아래에 구분한다.

## 변경된 게임 동작

| 조건 | 처리 |
| --- | --- |
| 일반 Android release, No Ads 미보유 | 광고 SDK API 초기화·로드·표시를 호출하지 않으며 광고 버프/회복 중단 안내 |
| 기존 No Ads 보유 | 기존 `GooglePlay` 쉼표 저장값의 `ProductNoAds` 권한을 유지하고 즉시 버프/회복; 광고 요청 없음 |
| Editor/Android 개발 또는 `TAMER_TEST_ADS` 빌드 | 명시적 상호작용으로만 Google 샘플 광고 로드; 첫 미준비 요청은 재시도 안내 |
| batch, 지원하지 않는 플레이어 | 테스트 플래그와 관계없이 광고 요청 차단 |
| 보상 후 닫힘 | 음악 복구와 광고 객체 정리 후 원래 요청자에게 한 번 지급 |
| 보상 이벤트 없이 닫기 | 음악 복구, 재시도/게임 계속 가능; 지연된 실제 보상 이벤트는 원래 요청자가 유효하면 한 번 지급 |
| 표시 실패 | 보상 요청 무효화, 음악 복구, 재시도/게임 계속 가능 |
| 화면 전환 또는 요청자 파괴 | 기존 요청 무효화; 새 씬의 다른 대상에게 지급하지 않음 |
| 중복 클릭/중복 콜백/오래된 콜백 | 진행 중 두 번째 요청 거절; 중복 보상 없음, 이전 요청이 새 요청의 전면 상태나 음악을 정리하지 않음 |
| 초기화·로드 30초 무응답 | 요청 세대 무효화 후 수동 재시도 허용; 오래된 로드 결과 정리 |
| 앱 백그라운드/복귀 | 일시정지/포커스 자체를 보상·닫힘으로 추정하지 않음; 실제 SDK 이벤트를 Unity Update에서 처리 |

버프는 요청 당시 `BuffingMan`, 회복은 요청 당시 `Player`와 씬 handle에 귀속한다.
전역 `IsReceived`와 현재 씬 이름을 통한 보상 전달은 제거했다. 저장 키/포맷, 기존 계정,
No Ads 권한, 에셋 GUID, 앱 서명/버전은 변경하지 않는다.

광고가 멈춘 BGM은 `AudioSource.UnPause()`로 재생 위치를 유지하며 한 번만 복구한다.
광고 중 씬 전환 또는 다른 BGM 재생/일시정지가 발생했다면 이전 광고가 새 음악을 다시 시작하지 않는다.
광고가 닫히지 않은 상태를 5초 타이머로 닫힘 처리하거나 보상을 조작하지 않는다.
늦은 회복 보상은 회복 팝업만 `ClosePopup(target)`으로 닫고 뒤로 가기 목록에서도 제거한다.
그 위에 새로 열린 다른 팝업의 표시 상태와 순서는 보존한다.

## SDK 콜백 순서

Android의 Google 직접 공급 광고는 native 단계에서 보상을 닫힘 전에 알린다.
[공식 Android 문서](https://developers.google.com/admob/android/rewarded).
그러나 Unity SDK 11.5.0 Android bridge는 두 이벤트를 서로 다른 스레드에서 C#으로 전달하므로
관리자 큐에 도착하는 순서까지 보장되지 않는다.
[버전 고정 Android bridge](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/source/plugin/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/src/main/java/com/google/unity/ads/UnityRewardedAd.java).

이 저장소의 9.1.1 Editor placeholder는 닫힘 후 같은 호출 묶음에서 보상을 알린다.
[버전 고정 소스](https://raw.githubusercontent.com/googleads/googleads-mobile-unity/v9.1.1/source/plugin/Assets/GoogleMobileAds/Platforms/Unity/RewardingAdBaseClient.cs)
및 설치 DLL의 IL을 대조했다. 11.5.0 Editor는 countdown 완료에서 보상을 알리고 닫기 버튼은
닫힘만 알린다.
[11.5.0 Editor 소스](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/source/plugin/Assets/GoogleMobileAds/Platforms/Unity/RewardingAdBaseClient.cs).

관리자는 한 Update에서 콜백 큐를 비운 뒤 전면 상태·음악을 정리한다. 닫힘 완료 통지는 한 번이며,
그 뒤에 도착한 실제 earned 이벤트도 같은 요청의 보상 함수만 한 번 실행할 수 있다.
다음 광고의 상태나 음악은 건드리지 않는다. 실패, 요청자 파괴, 관리자 파괴 또는 씬 전환은
보상을 무효화한다. 씬 전환 세대를 비교하므로 원래 씬으로 돌아와도 이전 보상이 살아나지 않는다.
닫힌 요청을 보관하는 별도 대기 목록이나 보상 타이머는 없다.

자동 검사는 같은 프레임의 두 순서, 다른 프레임의 보상, 실패 경합, 새 광고와 이전 보상의 격리를 다룬다.
실제 공급자가 콜백 자체를 전달하지 않는 경우까지 복구하지는 않는다. 운영 재활성화 전에 공급자별
기기 동작을 검증해야 한다. 현재 운영 요청과 iOS 기기 요청은 차단되어 있다.

## 재현 및 증거

권한 있는 원본에서 `python tools/revival/restore_assets.py --source <원본 경로>`로 복원한다.
다른 작업의 Library를 복사하지 않는다. CLI는 `python tools/revival/install_cli.py`로 버전 고정 설치한다.
자신의 checkout Editor가 닫힌 상태에서 실행한다.

```powershell
python -m unittest discover -s tools/revival -p 'test_*.py'
./tools/revival/Run-Baseline.ps1 -ProjectPath <이 작업 checkout 절대 경로> -TestsOnly
python tools/revival/audit_guids.py
```

아래 표는 2026-09-11 광고 checkout `worktrees/e5e5/Tamer`, source `21a00f3`까지의 기록이다.
Unity는 6000.0.81f1, CLI는 1.0.0-beta.8이며 이 checkout의 SDK는 GMA 9.1.1이다.

| 검사 | 확인된 결과 |
| --- | --- |
| 순수 C# NUnit | 보상 세션 20 + 요청 정책 18 + No Ads 권한 20 = **58/58 통과** (`1f55198`의 순수 소스, 이후 해당 소스 변경 없음) |
| Python 사전 검사 | 복원/빌드 사전 검사 **4/4 통과** |
| 에셋 복원 | 최종 실행 전 4,537개 검증, 추가 복사 0, 서비스 설정 제외 |
| GUID 참조 감사 | 132개 씬/프리팹 검사, 미해결 참조 0. 신규 테스트 meta의 33자리 오타는 별도로 32자리로 수정 |
| 별도 API 컴파일 | 실제 Unity/GMA/Unity bundled NUnit 참조 manager·테스트 컴파일 통과. 게임 의존성 stub 사용으로 전체 Editor 실행과 구분 |
| 실제 Unity 컴파일 | `8a56d17` 전체 스크립트 컴파일 후 테스트 실행 진입 확인 |
| 구 SDK 실제 Unity 테스트 | 이 checkout 실행에서는 최종 통과 판정 없음. 아래 실패 및 수정 후 SDK 11.5.0 합본 통과 기록 참조 |

첫 Editor 실행은 지원하지 않는 `NonParallelizable` 속성 때문에 컴파일에서 종료되어 속성을 제거했다.
다음 실행은 62개 중 42개 통과, 20개가 준비 단계에서 실패했다. batch runner의 미저장 씬에서는
additive 새 씬을 만들 수 없어서 테스트 격리를 preview scene으로 바꿨다. 당시 별도 20개 보상 테스트는
신규 meta GUID 오타로 Unity가 제외한 상태였으며 `8a56d17`에서 수정했다.

이후 Editor는 보상 테스트도 포함해 컴파일했으나 파괴 후 콜백 테스트 중 Mono native crash로
새 결과를 내지 못했다. 제한된 진단에서 종료 handler의 잠금·구독 해제·광고 정리·대기 콜백 폐기는
모두 반환했지만 정확한 crash 원인은 확정하지 않았다. `21a00f3`은 EditMode에서 자동 전달되지 않는
종료 handler를 명시적으로 호출하고 파괴한 컴포넌트의 reflection 조회를 제거한 테스트 보완이다.
이 fixture를 받은 통합 담당도 SDK 11.5.0의 새 Library에서 동일 native crash
(`-1073741819`, CLI 6)를 재현했다. 따라서 구 SDK 9.1.1만의 문제로 분류하지 않는다.
앞선 XML을 최신 성공 증거로 재사용하지 않았다.

통합 담당은 `OnDestroy`의 중첩 `finally` 안에 있던 콜백 정리 루프를 분리한 실험에서
같은 단일 테스트 **1/1 통과**를 확인했다. 공식 수정
[`b4764b4`](https://github.com/ChoiDaeYoung-94/Tamer/commit/b4764b4635304328762810ea07ededc7a96f6d9e)는
세션 종료, 로드된 광고 정리, 대기 콜백 폐기를 각각 예외 기록 후 계속하는 순차 단계로 바꾼다.
정리 동작은 유지하면서 중첩 예외 처리 구조를 단순화한 변경이다. 동일 사례의 수정 전 중단과
수정 후 통과는 확인했지만, Unity Mono 내부의 결함 원인까지 확정한 것은 아니다.

공식 수정을 병합한 `f5b7e404d7bd2c797aed2c9a3c17416a2ef50ccf`,
통합 checkout `worktrees/7819/Tamer`의 `Logs/revival/editmode.xml`을 읽기 전용으로 확인했다.
Unity 6000.0.81f1/GMA Unity 11.5.0 합본에서 **248/248 통과**, 실패 0, 건너뜀 0,
테스트 실행 시간 3.5548825초다. 순수 보상 20·정책 18·No Ads 20·manager 20·popup 6이
모두 포함되어 통과했다. baseline 4를 합친 광고 측 88개 전체도 이 결과에 포함된다.
통합 담당은 Python 31개 및 복원 에셋 4,561개 검증 통과도 보고했다.
`b4764b4` 독립 코드 재검토에서는 추가 P1/P2를 발견하지 않았다.

광고 측 최종 검사 대상은 순수 58 + manager 20 + popup 6 + baseline 4 = 88개다.
manager/popup 검사는 실제 컴포넌트의 handler를 명시 호출하며 게임 씬 실행이나 실제 광고·오디오를
대체하지 않는다. SDK 11.5.0·광고·데이터 합본의 전체 Editor 회귀는 위와 같이 통과했으며,
구 SDK 단독 APK 빌드는 생략하고 같은 통합 소스로 만든 아래 APK를 최종 산출물 근거로 사용한다.

## 최종 통합 APK

통합 소스는 `f5b7e404d7bd2c797aed2c9a3c17416a2ef50ccf`다. 통합 담당의
`Logs/revival/apk-verification.json`, `build-summary.json`, `native-alignment.json`,
`native-alignment-strict.json`, `relro-protection.json`을 읽기 전용으로 대조했으며,
APK 파일의 크기와 SHA-256을 별도로 계산해 일치를 확인했다.

| 항목 | 결과 |
| --- | --- |
| 빌드 | Succeeded, errors 0, 전체 검증 스크립트 종료 0 |
| 산출물 | 통합 checkout의 `Build/revival/Tamer-development.apk`, **104,785,252 bytes** |
| SHA-256 | `0d51f218491d90aa7e963b0e42b3ebf5606fc8e0d91f4b710d246fb8223eeb27` |
| 앱 ID | 격리된 `com.AeDeong.MonsterTamer.revival` |
| SDK/ABI | min SDK 24, target SDK 36, ARM64 전용 |
| 서명·시작 | debug 서명 확인, debuggable, 격리 `RevivalSmoke` 시작 씬 |
| 네이티브 정렬 | 6개 라이브러리의 LOAD 23개 검사 통과, zipalign 통과 |
| 엄격한 RELRO 검사 | 종료 주소 정렬 실패 5개를 별도 기록. 통과로 덮어쓰지 않음 |
| 추가 정적 기하 검사 | RELRO 6개 모두 전체 LOAD 구간과 일치, 반올림 보호 범위의 선언된 writable LOAD 중첩 0 |
| 실행·배포 경계 | 실제 16KB 페이지 기기 실행, AAB/split 및 스토어 승인은 미검증 |

빌드 요약의 총 처리 바이트와 실제 APK 파일 크기는 다르므로 위 크기는 실제 파일을 기준으로 한다.
추가 RELRO 정적 검사에서 중첩이 없다는 결과는 전체 16KB 호환성이나 엄격한 검사의 통과를 뜻하지 않는다.
계정·저장·No Ads 운영 검증과 실제 광고의 5초 닫힘도 이 격리 개발 APK의 정적 검사로 대체하지 않는다.

## 통합 및 미검증

- SDK/vendor/Packages/Gradle은 의존성 담당 PR 소유. 해당 PR의 SDK11.5.0 표준 Android 선택과
  이 런타임 코드를 병합한 최종 APK는 통합 담당이 별도로 검증한다.
- 단위·Editor 검사는 계정 로그인·저장 동기화·구매·운영 광고를 실행하지 않는다.
- 실제 광고의 5초 닫기, 네트워크 단절, 백그라운드 복귀, 청각적인 음악 복구,
  실제 No Ads 구매 복원과 각 게임 화면의 기기 검증은 남아 있다.
- Console 읽기 전용 확인: 위반은 검토·거절 통지의 프로덕션26(1.0.5)/SDK36/2026-08-13 첫 게시 번들에 연결되며
  기존 타깃은9~12/13~15/16~17세다. 위반은 여전히 표시된다. 광고포함/Data safety/개인정보 URL
  선언 값은 정책 문서에 기록했다. 실제 동의/광고 공급자·소재와 선언의 사실 적합성,
  전체 트랙의 실제 제공 버전·비준수 번들 잔류 여부 및 스토어 해소 승인은 미검증이다.
- SDK 자동 native 시작 동작의 차단은 앱 API 호출 차단과 구분한다.
  SDK11.5.0에는 구형 measurement 지연 옵션이 제거되어 과거 플래그를 새로 추가하지 않는다.
- #91은 코드 병합으로 종료하지 않는다. 비준수 광고 중단은 의도된 기능 영향이며,
  재활성화는 실제 공급원·연령/consent·형식·기기 증거를 갖춘 후 별도 PR로 진행한다.
- 개인정보/신고 비교는 [#105 기술 대조](privacy-data-safety-audit.ko.md)에 기록했다.
  이는 수정된 Console 신고나 개정 개인정보처리방침을 제출한 결과가 아니다.
- CI/CD, 스토어 제출, 운영 광고 요청은 이 작업에서 수행하지 않는다.
