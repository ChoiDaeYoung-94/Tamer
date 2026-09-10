# Tamer 복구 계획과 자동화 준비

조사일: 2026-09-10

기준 커밋: `c47c90217d45e8c6538c57a0924fa852abfbd736`

상태: 저장소·로컬 원본 정적 조사 및 공식 문서 검토 완료. Unity 실행, 빌드, 기기 테스트, Play Console 계정 확인은 아직 수행하지 않았다.

추적: [전체 로드맵 #90](https://github.com/ChoiDaeYoung-94/Tamer/issues/90), [광고 정책 #91](https://github.com/ChoiDaeYoung-94/Tamer/issues/91), [에셋 복원·기준 빌드 #92](https://github.com/ChoiDaeYoung-94/Tamer/issues/92).

## 1. 방향

첫 목표는 **새 환경에서도 재현되는 기준 빌드 확보와 Google Play 광고 정책 문제 해결**이다. 이후 SDK·엔진을 단계적으로 업데이트하고, 저장 데이터와 구매 권한을 유지하면서 코드를 리팩토링한다.

자동화 구성은 **Codex + 공식 Unity CLI + Unity Pipeline + 필요한 공식 skills + GitHub + Android 검증 도구**를 추천한다. 기존 코드의 파일 수정과 GitHub 이력 관리는 Codex가 수행하고, Editor 내부 검증은 Unity CLI/Pipeline이 담당한다.

대규모 엔진 변경, 광고 공급자 교체, 인증 체계 변경을 한 PR에 묶지 않는다. 각 변경에 재현 방법, 변경 이유, 검증 결과, 되돌리는 방법을 남긴다.

## 2. 확인된 기준선

| 항목 | 실제 상태 | 의미 |
| --- | --- | --- |
| Unity Editor | 6000.0.81f1 | README의 2022.3.52f1 표기는 오래됨 |
| Android | min SDK 24 / target SDK 36 / ARM64 | target API 숫자 변경은 이미 반영됨. 최종 바이너리는 별도 검증 |
| 앱 설정 | 1.0.5 / versionCode 26 | Play에 실제 제출된 버전과 같은지는 미확인 |
| 자체 코드 | Assets/Scripts 아래 C# 63개, 8,111줄 | 주석·공백 포함. 파일 수가 많기보다 전역 연결과 책임 집중이 핵심 |
| 씬 | Login, Main, Game, SetCharacter, NextScene | 로그인→캐릭터 설정/마을→전투 및 전환 흐름 |
| Unity 패키지 | URP 17.0.4, IAP 5.0.1, Cinemachine 2.10.7, Test Framework 1.6.0 등 | 패키지 설치와 실제 API 이행 완료를 구분 |
| Google Mobile Ads | Unity 9.1.1 / Android 23.2.0 / UMP 2.2.0 | Unity 플러그인과 Android 의존성을 함께 관리 |
| Google Play Games | 2.1.0 | 로그인·기존 계정 연결 회귀 검증 필요 |
| UniTask | 2.5.10 | Assets에 직접 포함되어 있음 |
| EDM4U | 1.2.182 | Android 의존성 해석기까지 버전 관리 필요 |
| PlayFab | 로컬 원본의 SDK 2.138.220621 | Assets/ThirdParty 전체가 ignore되어 공개 clone에 없음 |
| 자동 테스트 | 추적된 자체 테스트를 찾지 못함 | 로컬 Assets/Tests/Test.cs는 빈 MonoBehaviour, 자동 테스트가 아님 |
| CI | self-hosted buildpc, Unity 2020.3.25f1 macOS 경로, App Center 배포 | 현재 프로젝트와 불일치 |

근거: [ProjectVersion](https://github.com/ChoiDaeYoung-94/Tamer/blob/c47c90217d45e8c6538c57a0924fa852abfbd736/ProjectSettings/ProjectVersion.txt), [패키지](https://github.com/ChoiDaeYoung-94/Tamer/blob/c47c90217d45e8c6538c57a0924fa852abfbd736/Packages/manifest.json), [Android 설정](https://github.com/ChoiDaeYoung-94/Tamer/blob/c47c90217d45e8c6538c57a0924fa852abfbd736/ProjectSettings/ProjectSettings.asset), [광고 의존성](https://github.com/ChoiDaeYoung-94/Tamer/blob/c47c90217d45e8c6538c57a0924fa852abfbd736/Assets/GoogleMobileAds/Editor/GoogleMobileAdsDependencies.xml), [CI](https://github.com/ChoiDaeYoung-94/Tamer/blob/c47c90217d45e8c6538c57a0924fa852abfbd736/.github/workflows/cicd.yml).

기존 [PR #87](https://github.com/ChoiDaeYoung-94/Tamer/pull/87)은 Unity 6.0 패치와 API 36을 반영했다. 관련 이슈 제목의 Unity 6.3 목표가 달성된 것은 아니다. [PR #89](https://github.com/ChoiDaeYoung-94/Tamer/pull/89)는 로그인·동기화 타임아웃과 복구 경로를 수정했다. 이슈가 닫혔다는 사실을 기기 검증이나 스토어 승인 증거로 사용하지 않는다.

## 3. Unity CLI, MCP, skills 선택

| 구성 | 역할 | 결정 |
| --- | --- | --- |
| 공식 Unity CLI + com.unity.pipeline | Editor 관리, 열린 Editor의 씬·프리팹·Console·캡처, 빌드·테스트 | 우선 도입 후보 |
| CLI 내장 MCP | 같은 Editor 기능을 MCP tool로 노출 | 필요에 따라 연결 |
| 공식 Unity skills | 명령 사용법과 작업 절차 | 필요한 항목만 프로젝트 단위로 설치 |
| CoplayDev/unity-mcp | 커뮤니티 Editor MCP, Python/uv 추가 필요 | 공식 경로에서 실제 부족함을 확인할 때 대안 |
| IvanMurzak/Unity-MCP | Editor/Runtime 제어, 별도 로컬·클라우드 구성 | 초기 도입 보류, 필요할 때 비교 |
| 예전 com.unity.ai.assistant 내장 MCP | 구형 공식 Editor MCP | 신규 구성에 사용하지 않음 |

공식 CLI는 무료이며 Unity AI 구독과 별개다. 실행 중 Editor 제어에는 Unity 6.0 이상이 필요하므로 현재 Tamer가 조건을 충족한다. Unity는 shell을 쓸 수 있는 에이전트에는 직접 `unity command`/`unity eval` 호출을 권장한다. 로컬 Editor 서버는 localhost에서 동작한다. [공식 전환 가이드](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli)

CLI 문서상 실험 단계이며 조사 시 릴리스 노트 최상단은 1.0.0-beta.6이다. Pipeline 최신 문서는 0.6.0-exp.1이다. 실제 설치 전 가용 버전·호환성을 확인하고 고정한다. 최신 문서의 모든 기능이 6000.0.81f1에서 동작한다고 가정하지 않는다. [CLI 릴리스](https://docs.unity.com/en-us/unity-cli/release-notes), [Pipeline 변경 기록](https://docs.unity3d.com/Packages/com.unity.pipeline@0.6/changelog/CHANGELOG.html)

설정 절차는 CLI 설치 → CLI 인증/라이선스 점검 → 기준 프로젝트에서 Pipeline 설치 → 연결 확인 → 필요한 skill 설치 → 선택적 MCP 연결이다. 공식 명령에 `unity skill install codex`, `unity mcp configure codex`가 있다. 설치 시 `--help`, `--list`, `--dry-run`으로 해당 버전의 프로젝트 범위 옵션을 확인하고 적용한다. [Codex 연동 명령](https://github.com/Unity-Technologies/skills/blob/main/skills/unity-cli/references/integration-advanced.md), [Pipeline 설치](https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package)

우선 skill은 `unity-cli`, `unity-package-management`, 기존 UI에 맞는 `ui-ugui` 등이다. 결제 작업에는 `implement-in-app-purchases`를 검토한다. 광고 SDK가 AdMob인 현재 프로젝트에 LevelPlay 스킬을 설치하는 것이 문제 해결의 전제는 아니다. 9월 9일 발표된 Unity 공식 통합 플러그인은 Claude Code용이지만 Codex는 CLI와 skills를 직접 사용할 수 있다. [공식 skills](https://github.com/Unity-Technologies/skills), [Unity 발표](https://unity.com/blog/unity-plugin-for-claude-code)

연결 완료 기준은 프로젝트 경로·Editor 버전 확인, Console/씬 읽기, 캡처, 테스트 씬의 저장·재열기, 테스트 실행이다. `--project-path`를 명시하고 동일 Editor에 대한 쓰기는 직렬화한다. 컴파일 오류로 Pipeline이 로드되지 않는 상황을 위해 파일 편집·Editor 로그·배치 실행 경로도 유지한다.

Android 빌드는 Build Profile 또는 검증된 custom build method가 필요하다. CLI 설치만으로 Tamer의 기존 빌드 코드가 자동으로 고쳐지지는 않는다. [빌드/테스트 가이드](https://github.com/Unity-Technologies/skills/blob/main/skills/unity-cli/references/build-run-test.md)

## 4. 첨부된 Google Play 문제

첨부 화면은 가족 광고 형식 위반으로, 정상 이용을 방해하는 광고를 5초 후에도 닫을 수 없다고 지적한다. 이전 버전 사용 가능이라는 문구가 있다. 적용일에는 연도가 없고, 거절 AAB의 versionCode·광고 소재·공급 네트워크·현재 Console 상태는 확인되지 않았다.

**Families의 이 규칙은 rewarded/opt-in 광고에도 적용된다.** 아동/연령 미상 이용자에게 제공되는 실제 광고 형식·내용·SDK가 모두 적합해야 한다. SDK를 업데이트했다는 사실만으로 해결됐다고 판단할 수 없다. [Google Families 정책](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en)

현재 `GoogleAdMobManager.cs` 정적 검토에서 확인한 항목:

- 초기화 전에 아동/연령 미상 처리, 최대 콘텐츠 등급, consent 준비 상태를 설정하는 자체 코드가 보이지 않는다. Console 측 설정과 SDK 기본 동작은 별도 확인이 필요하다.
- 광고 선택 전처리기는 `Debug`, APK 빌드 정의는 `DEBUG`로 서로 다르다. 개발 빌드에서 운영 광고 ID를 선택할 수 있으므로 첫 수정 후보다.
- 종료·실패 경로에서 BGM 복원 처리가 일관되지 않고, 실패 처리가 특정 씬의 BuffingMan에 직접 연결되어 있다.
- 보상 결과를 전역 bool과 현재 활성 씬으로 전달한다. 광고를 요청한 위치와 완료 시점의 위치가 달라지는 경우, 중복·실패 콜백을 검증해야 한다.

이 항목들은 코드에서 확인한 위험이며, 5초 닫힘 위반의 확정 원인이나 실제 기기 재현 결과는 아니다. [광고 코드](https://github.com/ChoiDaeYoung-94/Tamer/blob/c47c90217d45e8c6538c57a0924fa852abfbd736/Assets/Scripts/Managers/GoogleAdMobManager.cs), [빌드 코드](https://github.com/ChoiDaeYoung-94/Tamer/blob/c47c90217d45e8c6538c57a0924fa852abfbd736/Assets/Scripts/Editor/BuildScript.cs)

해결 순서:

1. 거절 versionCode·트랙·대상 연령·AdMob mediation 설정과 증빙을 연결한다.
2. 개발은 테스트 광고로 수행하고, 제출 조건 검증에서는 실제 공급 광고의 닫힘/복귀를 확인한다.
3. 실제 제품에 맞는 연령 처리와 SDK 초기화 순서를 정하고, 인증된 Android SDK 및 adapter 조합을 고정한다.
4. 5초 닫힘, 조기 종료, 광고 미수신, 네트워크 실패, 백그라운드 복귀, 보상 1회 지급을 기기에서 검증한다.
5. 적합한 광고 형식을 확보하지 못하면 해당 광고 노출을 중단하는 복구안을 비교한다. 임의 오버레이 닫기 버튼이나 연령 신고 변경만으로 해결하지 않는다.
6. 개인정보처리방침·Data safety·광고 신고를 구현과 맞추고 테스트 트랙부터 검증한다.

최신 AdMob 문서는 `AgeRestrictedTreatment`를 안내하지만 현재 9.1.1에 그대로 붙여 넣지 않는다. 설치할 버전의 API와 mediation 전달 규칙을 맞춘다. [타기팅](https://developers.google.com/admob/unity/targeting), [인증 SDK 목록](https://support.google.com/googleplay/android-developer/answer/12955712?hl=en)

## 5. 의존성 업데이트 원칙

| 대상 | 현재 | 조사 시 확인한 후보/작업 |
| --- | --- | --- |
| Unity | 6000.0.81f1 | 기준 빌드 확보 후 6.3 LTS의 지원 패치로 이행 검토 |
| Google Mobile Ads Unity | 9.1.1 | 최신 릴리스 v11.5.0 확인. Next-Gen Android SDK 전환을 포함하므로 인증·adapter 호환성 확인 전 목표 확정 금지 |
| Play Games | 2.1.0 | 최신 v2.2.1 확인. 기존 계정·OAuth·서명 인증서 검증과 함께 이행 |
| UniTask | 2.5.10 | 최신 2.5.11 확인. 취소·씬 수명 관련 회귀 테스트 |
| PlayFab | 2.138.220621 | 버전/라이선스/실제 사용 API 조사 후 복원·업데이트 |
| IAP | 5.0.1 | 코드에 IStoreListener/UnityPurchasing.Initialize 구형 API 사용. 구매 복원·영수증·중복 처리 검증 포함 |
| URP/Cinemachine/기타 | manifest 및 Assets 혼합 | 엔진 호환 행렬에 따라 묶고, 장면·카메라·셰이더 변경은 별도 검증 |
| UniRx 등 vendored 라이브러리 | 파일 포함 | 소스 버전·실사용부터 확인. 교체가 필요한지 판단 |

최신 릴리스는 설치 권고와 같지 않다. 지원 상태, Families 인증, 플랫폼 호환성을 만족하는 최신 조합을 선택한다. 특히 기존 Android `play-services-ads:23.2.0`은 현재 인증 목록의 하한에 들어가므로, 단순히 “미인증 SDK라 거절됨”이라고 단정할 근거가 없다. 새 Next-Gen SDK도 동일 인증이 자동 승계된다고 가정하지 않는다.

출처: [GMA v11.5.0](https://github.com/googleads/googleads-mobile-unity/releases/tag/v11.5.0), [GPGS v2.2.1](https://github.com/playgameservices/play-games-plugin-for-unity/releases/tag/v2.2.1), [UniTask 2.5.11](https://github.com/Cysharp/UniTask/releases/tag/2.5.11).

Unity 공식 지원표상 6.0 LTS 일반 지원은 2026년 10월, 6.3 LTS는 2027년 12월까지다. 6.0은 복구 기준선이며 장기 고정 목표가 아니다. [Unity 지원표](https://unity.com/releases/unity-6/support)

API 36은 설정에 반영되어 있지만 최종 merged manifest와 AAB를 검사해야 한다. 16 KB는 모든 native 라이브러리와 패키징·실행을 함께 검증한다. 최신 Android 가이드는 업데이트 차단 시점을 2027-02-01로 표시하므로, 과거 기한을 복사하지 않고 실제 Console 안내도 대조한다. [Target API](https://support.google.com/googleplay/android-developer/answer/11926878?hl=en), [16 KB 가이드](https://developer.android.com/guide/practices/page-sizes)

## 6. Git에서 빠진 파일 관리

로컬 원본에서 ignore된 `Assets/ThirdParty`는 약 4.12 MiB/346파일, `Assets/ThirdPartyAssets`는 약 532.94 MiB/4,186파일이다. 용량은 .meta 등을 포함한 논리 파일 크기다. `Tests/`, `ThirdParty/`, `ThirdPartyAssets/`라는 광범위 패턴이 원인이다.

목표는 **필요한 원본과 복원 절차를 빠짐없이 관리하는 저장소**다.

| 파일 종류 | 관리 방식 |
| --- | --- |
| 자체 C#, 씬, 프리팹, 설정, .meta, 실제 테스트 | Git에 포함 |
| 재배포 가능한 SDK | UPM/버전 잠금 우선, 직접 포함 시 라이선스와 불필요 파일 확인 |
| 큰 자체 원본 에셋 | 필요한 경우 Git LFS |
| 구매한 에셋 원본 | 재배포 권한 확인 후 비공개 에셋 저장소/권한 있는 아카이브 + 자동 복원 |
| Library, Logs, obj, 생성 csproj/sln, 임시 .utmp | 제외하고 재생성 |
| 계정 설정·서명 재료 | 별도 보관 및 빌드 시 주입, 공개 업로드 전 파일 단위 검토 |

공개 저장소의 Git LFS도 공개 배포다. 에셋을 직접 올릴 수 없는 경우에도 버전·해시·필요 경로를 manifest로 기록해 자동 복원할 수 있다. 이번 조사에서는 ignore 해제, 대량 추가, 원본 이동을 수행하지 않았다.

## 7. 리팩토링 순서

1. **계정·저장 데이터 보존**: 신규 설치, 재설치, 기존 계정, 네트워크 단절, 다중 기기 동기화를 테스트한다. 저장 스키마·마이그레이션·충돌 정책을 먼저 정한다.
2. **광고·결제 경계 분리**: IAdsService/IPurchaseService 같은 얇은 경계를 두어 가짜 결과로 테스트한다. 보상/구매 권한은 한 번만 반영한다.
3. **초기화와 씬 수명 정리**: Managers의 초기화 순서, DontDestroyOnLoad 중복, 이벤트 해제, 비동기 취소를 명확히 한다.
4. **전투·플레이어·몬스터 책임 분리**: 이동, 전투, 능력치, 버프, 군집, UI 연동을 단계적으로 분리한다. 기존 prefab 직렬화와 .meta GUID를 유지한다.
5. **성능 및 구조 개선**: 기준 기기의 CPU/GPU·메모리·프레임 수치를 기록한 뒤 실제 병목을 수정한다. 필요가 확인된 경계부터 asmdef를 도입한다.

전면 재작성이나 새 DI/ECS/Addressables 프레임워크 도입 자체를 목표로 삼지 않는다. 데이터·구매·플레이 경험의 호환성을 우선한다.

## 8. 실행 단계와 완료 기준

| 단계 | 산출물 | 완료 기준 |
| --- | --- | --- |
| P0 기준선/에셋 복원 | 의존성 manifest, 재현 절차, 기준 로그 | 새 작업 복제본에서 누락 참조 없이 컴파일/개발 빌드 |
| P1 자동화 연결 | CLI/Pipeline 버전 고정, 필요한 skills, 작업 규칙 | 프로젝트 식별·Console·캡처·테스트 호출 성공 |
| P2 스토어 복구 | 광고 수정 PR, SDK 선정 근거, 기기 증빙 | 5초 닫힘 등 재현 항목 통과, 수정 AAB 테스트 트랙 검증 |
| P3 지원 버전 이행 | SDK별 PR, 엔진 이행 PR | 계정·구매·씬·성능·16 KB 회귀 검증 |
| P4 코드 리팩토링 | 기능 경계별 PR와 테스트 | 기존 데이터·권한·게임플레이 유지 |
| P5 CI/README 완성 | 자동 빌드/테스트·배포 기록·개발 문서 | 새 환경에서 문서대로 재현, 제출본 추적 가능 |

CI는 기준 빌드 확보 직후부터 만들고 각 단계의 검증에 활용한다. App Center Distribution을 GitHub artifact/Play 내부 테스트 등의 현재 경로로 대체한다. App Center의 배포 기능은 2025-03-31 이후 종료되었다. Analytics/Diagnostics 지원 연장과 구분한다. [Microsoft 공지](https://learn.microsoft.com/en-us/appcenter/retirement)

Unity 테스트는 매 PR에 필요한 EditMode/PlayMode를 실행하고, Android 빌드는 전용 작업 복제본과 직렬화된 runner에서 수행한다. 기기 QA에는 로그인, 저장, No Ads 구매/복원, 버프/회복 광고, 마을↔전투, 앱 재시작을 포함한다. 자동 GUI 테스트가 광고/결제/심사 결과를 모두 보장한다고 가정하지 않는다.

## 9. 사람이 필요한 부분을 최소화하는 방법

코드 수정, 도구 설치/설정 작업, 의존성 정리, 테스트, 로그 분석, 커밋, 이슈, PR, README 작성은 에이전트가 수행한다. 저장소의 AGENTS.md에는 빌드·테스트 명령, .meta 규칙, 기존 데이터 보존, 검증 증거 형식을 명시할 예정이다. 실제 자동화 규칙은 검증한 명령만 기록한다.

필요 시 최초 Unity/Google 계정 로그인·2FA, 라이선스 확인, 구매 에셋 접근 권한, 서명 비밀 주입, 실제 기기 USB 디버깅 허용처럼 소유자에게만 가능한 단계가 남는다. 비밀번호를 채팅에 적는 방식은 사용하지 않는다. 기존 로그인/권한을 우선 재사용한다.

Play Console, AdMob, PlayFab의 현재 접근 상태는 이번 조사에서 확인하지 않았다. Google Play Developer API로 가능한 빌드/트랙 작업과 Console에서만 가능한 정책 확인을 구분해 연결한다. 최종 대상 연령·과금 정책과 같은 제품 의사결정은 제안 근거를 준비해 소유자가 판단할 수 있게 한다.

이슈→작은 브랜치→검증→PR 흐름을 유지한다. PR에는 재현 입력, 변경 전후, 테스트 결과, 미검증 항목, 빌드 SHA/versionCode를 남긴다. “코드 반영”, “빌드 성공”, “기기 통과”, “스토어 승인”을 별도 상태로 관리한다. README는 실제 확인한 환경·복원·빌드·문제 해결 절차로 단계적으로 갱신한다.
