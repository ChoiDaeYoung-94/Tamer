# Tamer 복구 구현·통합 검증 결과

2026-09-11 기준, 아래 8개 PR을 **main에 병합**했다. 로컬 구현·회귀 테스트·격리 개발 APK 검증을 완료한 기록이며, 실제 기기·운영 서비스·스토어 복구 완료를 뜻하지 않는다.

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

통합 검증 소스는 [`f5b7e404d7bd2c797aed2c9a3c17416a2ef50ccf`](https://github.com/ChoiDaeYoung-94/Tamer/commit/f5b7e404d7bd2c797aed2c9a3c17416a2ef50ccf)다. 우리 checkout에서 `Run-SdkValidation.ps1` 종료 코드 0을 확인했다. 최종 기능 병합 main `7cba8dbbcfb733b4e7b8fc8eb818cb330003504a`와 이 소스의 `Assets`·`Packages`·`ProjectSettings`·`tools` 차이는 없다. 이후 통합 변경은 문서와 검증 기록이다.

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

16KB 검사는 통과 범위를 구분해야 한다. 6개 모두 RELRO가 전체 LOAD와 일치하고 16KB 보호 범위 확장이 원 RELRO 밖의 선언된 쓰기 가능 바이트와 겹치는 경우는 0개였다. 이 정적 배치만으로 실행 불가를 단정할 수 없지만, **공식 가이드의 끝 주소 검사 실패와 실제 16KB/AAB 미검증은 그대로 남는다.** [네이티브 검사 해석과 후속 검증](native-alignment.ko.md)

남은 작업은 다음과 같다.

- **기기·결제:** 기존 계정 로그인·저장 실패/재시도·No Ads 구매/복원·광고/음악·전투, 16KB Android 실행과 최종 AAB/split 검사. 서버 영수증 검증은 이번 구현에 포함되지 않았다.
- **광고·스토어 [#91](https://github.com/ChoiDaeYoung-94/Tamer/issues/91):** Families 5초 닫힘, 공급자·consent 설정과 심사. Console의 거절 연결 번들26을 현재 제공 버전으로 단정하지 않는다. 광고 API 차단이 native SDK의 모든 자동 통신 차단을 증명하지는 않는다.
- **개인정보 [#105](https://github.com/ChoiDaeYoung-94/Tamer/issues/105):** 실제 PlayFab 로그인·진행 저장과 Data safety 선언 대조, 보관·삭제·계약·동의 결정. 향후 Private 쓰기는 기존 Public 키와 구버전의 재공개를 자동 해결하지 않는다. [기술 감사](privacy-data-safety-audit.ko.md)
- **소유자 확인:** 서명키와 Play 인증서 관계·OAuth 필요성([#94](https://github.com/ChoiDaeYoung-94/Tamer/issues/94)), 구매 에셋의 비공개 보관 운영. Unity `6000.3.24f1`은 공식 다운로드·체크섬 확인 후 Windows UAC `ELEVATION_CANCELLED`로 설치되지 않았다([#86](https://github.com/ChoiDaeYoung-94/Tamer/issues/86), [재개 안내](unity63-handoff.ko.md)).

원본 작업본·기존 서명키·계정·진행도·No Ads 권한을 보존했다. **CI/CD와 기존 App Center 구성은 보존·비활성 상태이며, 운영 서비스 호출·스토어 변경·배포를 수행하지 않았다.**
