# Tamer 복구 구현·통합 검증 결과

2026-09-11 1차 구현에서는 아래 8개 PR을 **main에 병합**했다. 로컬 구현·회귀 테스트·격리 개발 APK 검증을 완료한 기록이며, 실제 기기·운영 서비스·스토어 복구 완료를 뜻하지 않는다.

| 영역 | 변경 전 → 변경 후 | 병합 PR |
| --- | --- | --- |
| 복원·빌드 | 공개 clone의 에셋 누락 → 권한 있는 원본 해시 검증, GUID 보존, 격리 APK 재현 절차 | [#95](https://github.com/ChoiDaeYoung-94/Tamer/pull/95) |
| 개발 문서 | 오래된 실행 안내 → 고정 CLI/Pipeline과 새 작업본·검증·PR 절차 | [#93](https://github.com/ChoiDaeYoung-94/Tamer/pull/93) |
| 저장소 보안 | 접근·자동화 권한 점검 필요 → main PR 필수, 강제 push·삭제·bypass 제한, Actions 비활성/읽기 권한 | [#103](https://github.com/ChoiDaeYoung-94/Tamer/pull/103) |
| 계정·저장 | 계정 전환·늦은 응답·전체 업로드 위험 → 서버 확인, 계정별 원자 저장, 변경 키·요청 세대·재시도, 향후 Private 쓰기 | [#100](https://github.com/ChoiDaeYoung-94/Tamer/pull/100) |
| 전투·몬스터 | 중복 전투 루프·보스 참조 잔류·생성 초과 → 이전 작업 취소, 역할·참조 정리, 지휘관 포함 생성 예산 | [#101](https://github.com/ChoiDaeYoung-94/Tamer/pull/101) |
| 객체 풀 | 중복 반환·재진입으로 이중 대여, 중복 사전 생성 → 보관 상태 추적과 생성 전 중복 검사 | [#102](https://github.com/ChoiDaeYoung-94/Tamer/pull/102) |
| 광고 | 전역 보상·종료 순서 의존 → 요청자·씬별 일회성 지급, 지연 보상·팝업·BGM 정리. 미검증 운영 광고 API 기본 차단, No Ads 유지 | [#104](https://github.com/ChoiDaeYoung-94/Tamer/pull/104) |
| SDK·구매 | 구형 SDK·구매 처리 → 고정 호환 조합, IAP v5 권한 영속 저장 후 구매 확인, 재시도·복원 회귀 | [#106](https://github.com/ChoiDaeYoung-94/Tamer/pull/106) |

실제 실행 버전은 다음과 같다. 출처와 이행 범위는 [SDK 감사](sdk-audit.ko.md), 재현 명령은 [개발 안내](../development.ko.md)에 있다.

| 구성 | 검증 버전 |
| --- | --- |
| Unity / CLI / Pipeline | `6000.0.81f1` (`6238fec1e98f`) / `1.0.0-beta.8` / `0.6.0-exp.1` |
| Android 빌드 도구 | Platform 36, Build Tools `36.0.0`, NDK `27.2.12479018` (r27c), JDK `17.0.18+8`, Gradle `9.1.0`, AGP `9.0.0` |
| IAP / Services Core / Billing | `5.4.3` / `1.18.0` / `9.0.0` |
| UniTask / PlayFab / EDM4U | `2.5.11` / `2.242.260805` / `1.2.189` |
| Play Games Unity / Android | `2.2.1` / `22.0.0` |
| Mobile Ads Unity / Android Standard / UMP | `11.5.0` / `25.4.0` / `4.0.0` |

1차 통합 검증 소스는 [`f5b7e404d7bd2c797aed2c9a3c17416a2ef50ccf`](https://github.com/ChoiDaeYoung-94/Tamer/commit/f5b7e404d7bd2c797aed2c9a3c17416a2ef50ccf)다. 우리 checkout에서 `Run-SdkValidation.ps1` 종료 코드 0을 확인했다. 최종 기능 병합 main `7cba8dbbcfb733b4e7b8fc8eb818cb330003504a`와 이 소스의 `Assets`·`Packages`·`ProjectSettings`·`tools` 차이는 없다. 1차 최종 문서는 이 소스를 기준으로 한다. 이후 2차 코드·도구 변경과 검증 범위는 아래에 구분했다.

| 실제 검증 | 결과 |
| --- | --- |
| Unity EditMode | **248/248**, 실패 0 / 건너뜀 0; 광고 추가 84·몬스터 6·풀 5 포함 |
| Python / 에셋 | **31/31**, 복원 파일 **4,561개** 해시 확인 |
| GUID | YAML 132개 미해결 0; Assets meta 정의 3,508개 형식 오류·중복 0 |
| Android 개발 APK | **Succeeded / errors 0**, 실제 파일 **104,785,252 bytes** |
| APK 설정·서명 | min24 / target36 / ARM64 / `1.0.5`·code26, `com.AeDeong.MonsterTamer.revival`, debug 서명 확인 |
| 네이티브 정적 검사 | 6개 `.so`·LOAD 23개 및 zipalign 통과; 별도 strict RELRO **5개 실패 / 종료 1** |

APK SHA-256: `0d51f218491d90aa7e963b0e42b3ebf5606fc8e0d91f4b710d246fb8223eeb27`.

정확한 checkout·테스트 fixture·XML 해시·APK·네이티브 수치는 [integration-validation.json](integration-validation.json)에 있다. 처음에는 이 checkout의 Library가 없었으며, 최종 재실행은 여기서 생성한 자체 캐시를 사용했다. 광고 파괴 테스트의 Mono native crash는 종료 정리 수정 후 단일 실험과 전체 회귀에서 해소됐지만 Mono 내부 원인까지 확정하지 않았다. APK는 격리 시작 씬과 기존 게임 씬 5개를 포함하며 운영 서비스에 자동 진입하지 않는다. APK·구매 에셋·민감 설정·원시 로그는 공개하지 않았다.

16KB 검사는 통과 범위를 구분해야 한다. 6개 모두 RELRO가 전체 LOAD와 일치하고 16KB 보호 범위 확장이 원 RELRO 밖의 선언된 쓰기 가능 바이트와 겹치는 경우는 0개였다. 이 정적 배치만으로 실행 불가를 단정할 수 없지만, **공식 가이드의 끝 주소 검사 실패와 실제 16KB 실행 미검증은 그대로 남는다.** [네이티브 검사 해석과 후속 검증](native-alignment.ko.md)

## 2차 검증·도구 보완

PR [#108](https://github.com/ChoiDaeYoung-94/Tamer/pull/108)·[#109](https://github.com/ChoiDaeYoung-94/Tamer/pull/109)·[#110](https://github.com/ChoiDaeYoung-94/Tamer/pull/110)·[#111](https://github.com/ChoiDaeYoung-94/Tamer/pull/111)을 main에 병합했다.

| 영역 | 완료한 범위와 근거 |
| --- | --- |
| 개인정보 | [Data safety 후보 답안](data-safety-draft.ko.md), [Public→Private 오프라인 사전·사후 비교](userdata-private-migration.ko.md). 운영 데이터 변경·삭제·서버 영수증 검증은 수행하지 않음 |
| AAB | debug AAB와 bundletool split 생성·서명·LOAD/ZIP 검사. 601개 payload 서명 확인, manifest 순서 경고와 strict RELRO 5개 실패는 유지. [정확한 소스·해시](aab-16kb-validation.ko.md) |
| 광고 | 공식 샘플 App ID를 쓰는 별도 sample/control APK 빌드·서명·앱 ID 확인. 테스트 요청 추적 UI와 시작 경로·제출 초안. [빌드 근거와 미검증 범위](families-store-readiness.ko.md) |
| 보안·복원 | 로컬 JKS 공개 인증서는 Console upload cert와 일치하고 Play signing cert와 다름. 비공개 백업 4,204개 생성, fresh clone 4,561개 복원 확인. 누락 meta 추적 수정. [인증서·백업 근거](signing-and-private-backup.ko.md) |
| 통합 | `d1f6a2505cdd820059284077ee4bd9df7d29abe9`에서 EditMode **249/249**, Python **71/71**, 에셋 **4,561개** 통과. `Run-Baseline -TestsOnly` 종료 0, Editor 시작 전후 ProjectSettings.asset 바이트 일치 |

공통 wrapper가 Editor 시작 전 설정을 백업하고 종료 후 복원하도록 보완했다. 이 보호 범위는 ProjectSettings.asset이며, 최종 실행의 SceneTemplate 재직렬화와 두 에셋의 줄바꿈 변화는 Editor 종료 후 별도 복원했다. 전체 APK/AAB는 재빌드하지 않았고 각각의 빌드 소스·해시를 유지했다. 통합 담당이 두 광고 APK의 실제 메타데이터·서명·해시와 AAB 해시를 재확인했다. [2차 통합 데이터](phase2-validation.json)

16KB 에뮬레이터는 두 번 모두 부팅하지 못해 PAGE_SIZE·ABI·설치·실행을 관찰하지 못했다. Windows 가상화 기능·드라이버·재부팅은 변경하지 않았다. 비공개 백업은 같은 PC의 별도 디스크에 있으며 암호화·오프사이트 재해 백업을 뜻하지 않는다.

## 실제 4KB 기기 실행

PR [#114](https://github.com/ChoiDaeYoung-94/Tamer/pull/114)에서 SM-N986N / Android 13(API 33) / ARM64 / 실제 `PAGE_SIZE=4096`의 격리 APK와 기기 사양에 맞춘 AAB split 실행을 확인했다. APK는 5초, split은 10초 동안 관측했으며, 설치·Unity 네이티브 로딩·전경 화면·이번에 설치한 앱 제거가 성공했다. 두 화면은 SDK 담당과 통합 담당이 각각 확인했다. 첫 APK의 전경 확인 실패는 원인 미확정으로 별도 보존했다.

새 기기 관측 도구를 통합한 Python 회귀는 **75/75** 통과했다. 기존 APK·AAB의 빌드 소스는 유지하며 최신 main 재빌드나 전체 게임 검증으로 표현하지 않는다. [기기 실행 범위와 근거](device-smoke-validation.ko.md), [각 실행 데이터](device-smoke-validation.json). 실제 16KB 실행은 아직 미검증이다.

남은 작업은 다음과 같다.

- **기기·결제:** 기존 계정 로그인·저장 실패/재시도·No Ads 구매/복원·광고/음악·전투, 16KB Android에서의 APK·AAB/split 실행. 서버 영수증 검증은 이번 구현에 포함되지 않았다.
- **광고·스토어 [#91](https://github.com/ChoiDaeYoung-94/Tamer/issues/91):** Families 5초 닫힘, 공급자·consent 설정과 심사. Console의 거절 연결 번들26을 현재 제공 버전으로 단정하지 않는다. 광고 API 차단이 native SDK의 모든 자동 통신 차단을 증명하지는 않는다.
- **개인정보 [#105](https://github.com/ChoiDaeYoung-94/Tamer/issues/105):** 실제 PlayFab 로그인·진행 저장과 Data safety 선언 대조, 보관·삭제·계약·동의 결정. 향후 Private 쓰기는 기존 Public 키와 구버전의 재공개를 자동 해결하지 않는다. [기술 감사](privacy-data-safety-audit.ko.md)
- **소유자 확인:** upload key 재설정 결정·잔여 OAuth 필요성([#94](https://github.com/ChoiDaeYoung-94/Tamer/issues/94)), 구매 에셋의 비공개 보관 운영. 각 결정은 해당 작업에 진입할 때 구체적인 안과 함께 확인한다.

Unity는 사용자 방침에 따라 **6000.0.81f1을 유지**한다. 6.3 설치는 보류하며 현재 필수 잔여 작업이 아니다. 이전 다운로드·UAC 중단은 [보관 기록](unity63-handoff.ko.md)으로 남기고, 실제 Unity 버전 관련 차단이 확인될 때만 재개를 검토한다.

원본 작업본·기존 서명키·계정·진행도·No Ads 권한을 보존했다. **CI/CD와 기존 App Center 구성은 보존·비활성 상태이며, 운영 서비스 호출·스토어 변경·배포를 수행하지 않았다.**
