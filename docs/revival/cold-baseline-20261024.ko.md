# Unity 6.3 LTS 새 작업본 복구 재현 검사 — 2026-10-24

실행 소스는 `cf3a519676911f07475c54a7ee5f0291df34b0f1`이며, 새 격리 작업본의 detached HEAD를 시험·빌드 동안 고정했습니다. Login 안내 수정과 개인정보 오류 안내, Android 관측, 기준 검증 입력 보완을 포함한 main입니다. 이후 문서 커밋을 실행 소스로 표현하지 않습니다.

## 시작 조건과 복원

- 새 작업본의 `Library`, `.utmp`, `UserSettings`와 로컬 CLI가 없는 상태에서 시작했습니다. 기존 구현 작업본과 Library는 보존했습니다. 시스템의 Unity/UPM/Gradle 공용 캐시는 유지했으므로 모든 캐시를 비운 검사로 표현하지 않습니다.
- 권한 있는 비공개 원본으로 최초 복원한 결과는 `verified=362, copied=4201, sdkMetaMigrations=7, privateMetaMigrations=0`입니다. Editor 실행 전 엄격한 재검사는 `verified=4561, copied=0, sdkMetaMigrations=7, privateMetaMigrations=0`으로 통과했습니다.
- 고정 도구는 Unity `6000.3.25f1` / revision `e1dba0a9aba4`, CLI `1.0.0-beta.8`, Pipeline `0.6.0-exp.1`입니다. Android min25/target36/ARM64 기준입니다. 실제 `toolchain.json`, ProjectVersion, 도구 실행 파일을 대조했습니다.
- 최초 준비에서 로그 부모 경로가 없었던 두 오류는 Editor/제품 시험 실패와 구분해 보존했습니다. 이후 소유한 로그 부모만 만들고 복원을 진행했습니다.

## 최초 임포트와 Editor 검사

동일 소스의 단독 batch에서 `Revival` 전체 EditMode 검사를 한 번 수행했습니다. 실제 XML은 **900개 중 900통과, 실패0·건너뜀0·미확정0, 16.4392176초**입니다. UTC `2026-10-24 02:45:26`부터 `02:45:43`까지이며 child exit0과 해당 Editor 종료도 확인했습니다. 과거 895통과/1실패 및 영향2사례 재검증 결과와 합산하지 않습니다.

기존 설정 59개, 원본 manifest 전체, 추가 비공개 에셋·메타, 기존 서명 파일, 출력/임시 경로를 포함한 4,624경로를 먼저 보존했습니다(실제 파일4,621·최초 부재3). 검사 후 변경14개/신규4개/삭제0을 검토했고, 의도하지 않은 기존 설정9개를 복구하여 원본59개를 검증했습니다. 비공개 importer 메타1개와 새 Editor 설정4개는 별도 보존하고 자동 삭제·덮어쓰기를 하지 않았습니다. 에셋 본문·기존 서명 파일·GUID를 보존했습니다. 추적 파일 변경0/Editor 종료를 확인한 뒤에만 빌드 단계로 진행했습니다.

검사 실행기는 XML·command metadata·원본/실행 후 스냅샷·복구 영수증과 실제 checkout/HEAD를 대조합니다. Windows 합성 검사7/7 및 독립 정적 검토를 마쳤습니다. 디렉터리 잠금을 같은 사용자의 새 파일 생성까지 막는 보장으로 설명하지 않으며, 총괄의 단독 실행 소유권을 유지했습니다.

## Android 개발 빌드

최초 임포트/Editor 검사와 복구를 마친 동일 작업본에서 `RevivalBuild.BuildAndroidDevelopment`를 수행했습니다. 따라서 이 APK 빌드 자체가 Library까지 비어 있는 상태로 시작한 것은 아닙니다. 빌드 summary `Succeeded/errors=0`, child exit0 및 해당 Editor 종료를 확인했습니다. 실제 APK는 다음과 같습니다.

| 항목 | 실제 결과 |
| --- | --- |
| 파일 | `Build/revival/Tamer-development.apk` |
| 실제 파일 크기 | 110,991,548 bytes |
| SHA-256 | `20ed56077b47aad37daa5852f9f985b8cdb9a4b75a58bfcca89225ecd010da07` |
| 앱 ID | `com.AeDeong.MonsterTamer.revival` |
| 버전 | `1.0.5`, code26 |
| Android | min25 / target36 / ARM64만 포함 |
| 서명 | Android Debug 인증서 확인, debuggable |
| 정적 네이티브 검사 | 라이브러리6개, LOAD/ZIP 통과, 보수적 RELRO layout 통과 |

엄격한 RELRO 끝 정렬 진단은2건을 남깁니다. 위 정적 검사 통과를 실제16KB 환경 실행이나 Play 판정 통과로 표현하지 않습니다. 라이브러리6개는 압축되어 있어 ZIP 직접 mmap 정렬만으로 기기 실행을 판단하지 않습니다. 빌드 summary의 `bytes`는 Unity BuildReport의 `totalSize`이며 APK 파일 크기로 사용하지 않았습니다.

실제 Gradle JDK는 호스트의 기존 외부 도구 설정 경로를 사용했습니다. Java 실행 파일과 `lib/modules`가 해당 Unity Android 모듈의 파일과 각각 SHA-256 일치함을 확인했습니다. JDK17.0.18+8, NDK27.2.12479018, SDK/Build Tools36 기준이며 다른 프로젝트 설정과 도구를 수정하지 않았습니다.

이 기준 APK에는 PGS/Google Mobile Ads 네이티브 초기화 provider가 들어 있습니다. 앞서 검증한 오프라인 전용 variant와 다르므로 자동 설치·실행하지 않았습니다. 새 기기 실행이나 외부 서비스 호출이 없었다는 결과로 오해하지 않도록, 이번 APK의 runtime은 미검증으로 남깁니다.

빌드 후 변경13개/신규1개/삭제0을 검토했습니다. 기존59범위의 설정10개를 복구하고 원본59개를 검증했습니다. 본문이 동일한 비공개 설정2개의 identity 변경과 신규 build summary는 보존했습니다. 기존 서명 파일 불변·추적 파일 변경0·Editor 종료·파일 삭제0을 확인했습니다.

## 임포트 후 비공개 에셋 재검사

기본 옵션의 엄격한 검사는 알려진 importer 메타 변경1개를 거절했습니다. 이를 통과로 바꾸거나 원본 메타로 덮어쓰지 않았습니다. 기존에 명시 승인된 정확히 같은1건임을 승인 원본/이력·old/new 바이트·GUID·에셋 본문·권한 원본·현재 checkout/Unity·새 작업본 안의 원본/current 보존 자료로 다시 대조했습니다. 새 migration 또는 새로운 사용자 승인을 추정하지 않았고, 기존 승인 ledger와 원본을 수정하지 않았습니다.

새 작업본의 ignored/untracked 비공개 경로에 현재 checkout 전용 custody 근거를 보존하고 기존 승인 검증기의 명시 옵션으로 검사했습니다. 결과는 **`verified=4561, copied=0, sdkMetaMigrations=7, privateMetaMigrations=1, serviceSettings=excluded`**입니다. 일반 기본 검사와 다른 비공개 충돌의 거절 조건은 유지합니다. 명시 승인 범위 밖에 이를 확대하지 않습니다. [기존 승인 경계와 검증 계약](private-meta-restoration-approval.ko.md).

후속 GUID 검사는 YAML133파일·미해결GUID0, 출시 소스 사전검사는12항목 통과(`releaseReady=false`)입니다. 검사 뒤 메타/본문/기존 서명 파일 불변과 Git clean을 확인했습니다. 소스 검사 결과 보고서와 별도 command 영수증 이름이 겹친 기록용 보조 코드 오류1건은 exclusive 생성으로 원래 보고서를 보존했습니다. 해당 command exit 영수증은 누락됐으며, 실제 `sourceChecksPassed=true` 결과 보고서를 근거로 기록했습니다. 검사 전체를 다시 실행하지 않았습니다.

위 결과는 원본 보존과 단계별 검토를 위해 개별 실행한 명령들의 결과입니다. `Run-Baseline.ps1` 또는 `Run-SdkValidation.ps1` 전체를 한 번에 실행한 것으로 표현하지 않습니다.

## 검증 범위

별도 앱 ID·debug signing·격리 시작 씬의 개발 APK와 Editor 검사는 운영 출시 결과와 구분합니다. 실제 기기/16KB 커널·실제 Google 인증·운영 저장·구매 복원·광고 및 UMP 네이티브 폼·운영 서명 AAB·Play Console 판정은 이 검사로 대체하지 않습니다. Chrome이 필요한 작업은 사용자 요청에 따라 보류했습니다. 최종 운영 AAB가 준비됐다는 의미가 아닙니다.

구매 에셋 원시 식별자·메타 내용·비공개 승인 ledger·서명 값·Editor 계정 정보·원시 로그는 공개 문서와 커밋에 넣지 않습니다. 실행 후 생성된 비공개 설정과 Library는 정리 목적으로 삭제하지 않습니다.
