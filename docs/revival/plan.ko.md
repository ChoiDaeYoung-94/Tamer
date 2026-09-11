# Tamer 복구 계획과 단계별 상태

최초 조사: 2026-09-10. 갱신: 2026-09-11.
초기 기준: `c47c90217d45e8c6538c57a0924fa852abfbd736`.
실행 명령은 [개발 안내](../development.ko.md), 최신 병합·검증·남은 작업은 [통합 요약](completion-summary.ko.md)과 [통합 데이터](integration-validation.json)를 따른다. [기준 기록](baseline.ko.md)과 [초기 검증 데이터](validation.json)는 당시 이력이다.

이 문서는 최초 준비 PR #93의 계획을 실제 작업 상태로 갱신한 것이다. 과거 조사 시점의 상태는 Git 이력에서 확인할 수 있다. “Editor/빌드/도구 설치 미실행”은 최초 조사 당시의 기록이며 현재 전체 상태를 의미하지 않는다.

## 목표와 구분

권한 있는 새 작업본에서 에셋을 복원해 게임을 재현 가능하게 빌드하고, 기존 계정·진행도·No Ads 권한을 보존하면서 광고·저장·결제·게임플레이 문제를 작은 PR로 수정한다.

검증은 **코드 반영 / 빌드 성공 / 기기 통과 / 스토어 승인**을 따로 기록한다. Editor 테스트와 격리 APK가 성공해도 실제 서비스와 정책 문제가 해결되었다고 보지 않는다. 사용자 방침에 따라 CI/CD와 자동 배포는 보류한다.

## 확인된 복구 기준

| 항목 | 기준 | 근거 |
| --- | --- | --- |
| Unity | 6000.0.81f1 / 6238fec1e98f | ProjectVersion.txt, toolchain.json |
| Android | min24 / target36 / ARM64 | 설정과 격리 APK 실제 검사 |
| 앱 버전 | 1.0.5 / code26 | 복구 빌드는 자동 증가하지 않음 |
| Unity CLI | 1.0.0-beta.8 | 공식 배포 SHA-256 고정 설치 |
| Pipeline | 0.6.0-exp.1 | UPM manifest/lock |
| 복원 | SDK 이행 후 파일 4,561개 해시 확인, 서비스 설정 제외 유지 | assets-manifest.json 및 integration-validation.json |
| 자동화 | Python 71개, Unity EditMode 249개, 격리 APK 메타데이터/서명·GUID·LOAD/ZIP 통과 | tools/revival 및 phase2-validation.json; strict RELRO 5개 실패 별도 |
| 기기·스토어 | Android 13 ARM64 4KB에서 격리 APK·split 화면 확인; 전체 게임·16KB·스토어는 별도 | device-smoke-validation.json; 최초 전경 확인 실패와 재시도 성공 구분 |

초기 SDK는 GMA Unity9.1.1/Android23.2.0/UMP2.2.0, GPGS2.1.0, UniTask2.5.10, PlayFab2.138.220621, IAP5.0.1이었다. SDK/IAP 이행 PR #106은 병합됐으며 실제 최종 버전은 [통합 요약](completion-summary.ko.md)에 기록했다. 현재 checkout의 manifest/lock과 vendor 파일이 실행 기준이다. 사용자 방침에 따라 Unity 6000.0.81f1을 유지하고 6.3 설치는 보류한다. 이전 UAC 중단과 조건부 재개 절차는 [#86 기록](unity63-handoff.ko.md)에 보관한다.

## 작업과 완료 기준

| 단계 | 작업 | 현재 추적과 완료 기준 |
| --- | --- | --- |
| P0 | 에셋/SDK inventory와 새 작업본 기준 빌드 | #92 / #95: 해시 복원, 깨끗한 Library에서 테스트·격리 APK. 구매 권한/비공개 보관 증빙은 별도 |
| P1 | CLI/Pipeline 자동화 | #92 / #95: 버전 고정, 프로젝트/Console/씬/캡처/비동기 테스트 완료 결과 |
| P2 | Families 광고 복구 | #91: 요청/보상/복귀 회귀, 실제 제출 조건 5초 닫힘과 Console 위반 해소 |
| P3 | SDK/Android 현대화 | #97: 호환 조합과 IAP v5, 실제 native16KB 검사, 계정/권한 회귀. 엔진 이행은 별도 |
| P4 | 저장·인증과 게임플레이 리팩토링 | #96: 서버 진행도와 계정 연속성. #98: 전투 루프/보스/생성 수명 |
| P5 | 개발 문서와 운영 준비 | #93: README/개발 절차, #94: GitHub 접근 점검. CI/CD는 사용자 방침상 보류 |

구현 PR #95·#93·#103·#100·#101·#102·#104·#106은 main에 병합했다. 2차 PR #108·#109·#110·#111도 병합해 개인정보 초안·오프라인 감사, AAB/split 정적 검사, 인증서 역할·비공개 복원 검증, 광고 격리 APK를 추가했다. 위 단계의 실제 기기·정책·서비스·소유자 확인까지 완료한 것은 아니며, 정확한 완료 범위와 미검증 항목은 통합 요약을 기준으로 한다.

## 병렬 작업과 통합

기준 빌드 담당은 복원·도구·검증 진입점을 소유한다. 광고 담당은 광고 요청과 보상 진입점, 저장 담당은 계정·동기화, SDK 담당은 패키지·IAP·Android 검사, 통합 담당은 문서·충돌 해결·최종 회귀와 병합을 맡는다. 후속 Player/Monster/Managers 변경은 중복 파일 여부를 확인하고 작은 이슈로 나눈다.

각 담당은 자신의 checkout만 수정하고 최신 SHA/이슈/PR/검증/의존성을 통합 담당에게 직접 전달한다. Editor 경로와 PID를 명시하고 heavy import/build를 직렬화한다. 서로의 Library를 공유하지 않는다. 기준 PR과 후속 PR이 겹치는 경우 변경을 중복 적용하지 않고 의존성 순서로 병합한다.

## 보존할 데이터와 에셋

- `.meta`와 기존 GUID, prefab의 직렬화 필드 호환성을 유지한다.
- 기존 계정 식별자/인증 방식, 저장 키, 진행도, No Ads 구매 권한을 유지한다.
- 소유자 원본은 읽기 전용 복원 소스로 사용한다. 원본의 Git 상태나 Library를 변경하지 않는다.
- 구매 에셋·출처 미확인 폰트·서비스 설정·서명 재료·원시 로그는 공개 Git/LFS에 올리지 않는다.
- 복원 manifest와 충돌 검증으로 원본 버전을 확인하며 해시 불일치를 자동 재생성으로 숨기지 않는다.

## 광고·기기·스토어의 남은 확인

#91의 원래 문제는 정상 사용을 방해하는 광고가 5초 뒤에도 닫히지 않는다는 Families 형식 위반이다. 거절 AAB/versionCode/트랙, 대상 연령, 실제 공급 네트워크·소재와 Console 상태를 연결해야 한다. 최신 SDK를 설치했다는 사실이나 테스트 광고의 정상 종료만으로 이 문제를 종료하지 않는다.

실제 검증은 앱 재시작·기존 계정·동기화 실패/재시도·No Ads 구매/복원·보상 1회 지급·광고 실패/종료·백그라운드 복귀·마을/전투 전환을 포함한다. 운영 서비스를 자동 테스트에 사용하지 않는다. 개인정보처리방침/Data safety는 실제 SDK 설정·수집·보관·삭제와 대조해야 하며 기존 공개 정책만으로 확인 완료 처리하지 않는다.

## 개발과 배포 경계

로컬 전체 검증은 `Run-SdkValidation.ps1`과 격리 앱을 사용한다. strict RELRO는 별도 검사로 기록한다. 기존 BuildScript·App Center·fastlane 구성은 과거 배포 경로로 보존한다. workflow dispatch, runner 등록, 자동 배포, 서비스 이행을 하지 않는다. 이 보류는 로컬 구현·테스트·PR·검증 후 병합을 막는 조건이 아니다.

기기 연결, 계정 2FA/소유자 재인증, Asset Store 구매 증빙, 배포 서명·Console 제품 결정처럼 소유자만 가능한 단계가 남으면 구체적으로 기록한다. 코드와 도구로 해결 가능한 작업은 계속 수행한다.

## 추적 링크

- [전체 로드맵 #90](https://github.com/ChoiDaeYoung-94/Tamer/issues/90)
- [광고 정책 #91](https://github.com/ChoiDaeYoung-94/Tamer/issues/91)
- [기준 복원/빌드 #92](https://github.com/ChoiDaeYoung-94/Tamer/issues/92), [구현 PR #95](https://github.com/ChoiDaeYoung-94/Tamer/pull/95)
- [개발 문서 PR #93](https://github.com/ChoiDaeYoung-94/Tamer/pull/93)
- [GitHub 접근 점검 #94](https://github.com/ChoiDaeYoung-94/Tamer/issues/94)
- [저장/인증 #96](https://github.com/ChoiDaeYoung-94/Tamer/issues/96)
- [SDK/IAP/Android #97](https://github.com/ChoiDaeYoung-94/Tamer/issues/97)
- [게임플레이 수명 #98](https://github.com/ChoiDaeYoung-94/Tamer/issues/98)
