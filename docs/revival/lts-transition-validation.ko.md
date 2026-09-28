# Unity 6.3 LTS 격리 전환 검증

2026-09-28 `origin/main`의 `78226365cb71ff0f4b355bf1c0da9434e4811b8d`에서 `C:/Users/pc_17/.codex/worktrees/unity-lts-transition/Tamer`, `codex/unity-lts-transition`을 생성했습니다. 현재 main과 소유자의 `D:/meee/git/Tamer`는 전환하지 않았습니다. 복원은 원본 읽기만 사용했으며 기존 파일 362개 해시 확인·비공개 에셋 4,201개 복사를 완료했습니다. 실제 검증은 이 기준 커밋에 도구 버전 변경 및 Editor 자동 migration이 있는 작업 트리에서 수행했습니다. 코드 기록 커밋은 `fc5a0a6a2585d57bfa93cec415284762a16bba22`이며, 이후 서식 정리와 이 문서를 추가했습니다.

## 설치 및 무결성

실행 직전 공식 CLI와 [공식 릴리스](https://unity.com/releases/editor/whats-new/6000.3.25f1)를 대조했습니다. 설치 대상은 Windows x86_64 `6000.3.25f1`, 리비전 `e1dba0a9aba4`, 출시일 2026-09-24입니다. CLI는 기존 고정 `1.0.0-beta.8`, Pipeline은 `0.6.0-exp.1`을 유지했습니다. 기존 Unity 6000.0.81f1·6000.0.62f1·6000.0.56f1·6000.0.25f1을 보존했으며 기본 Editor·설치 루트·전역 계정은 변경하지 않았습니다.

공식 Windows 설치 파일은 4,201,959,888바이트이며 [릴리스 API](https://services.api.unity.com/unity/editor/release/v1/releases?limit=1&version=6000.3.25f1)의 `md5-+ezovoFvNL0wHeYt/x1BEQ==`와 실제 MD5 `f9ece8be816f34bd301de62dff1d4111`이 일치했습니다. Authenticode 상태 `Valid`, 서명자 Unity Technologies SF를 확인한 뒤 실행했습니다. 로컬 SHA-256은 `d70a5d8f9a7a463017c056a838fe393c97207f34972732794543c53368bfbf01`이며 공식 SHA-256과 대조한 결과가 아닙니다. 첫 관리자 승인 요청은 취소/시간 초과로 종료됐고, 해당 프로세스 종료 확인 후 재요청이 승인되어 설치가 완료됐습니다.

`--changeset`을 사용한 Editor 설치 결과에는 Android 모듈이 없었습니다. 별도 `install-modules`로 Android Build Support, OpenJDK 17.0.18+8, NDK r27c, CMake 3.22.1, SDK build/platform-tools 36.0.0, SDK platforms 34/35/36/37.0, command-line tools 16.0, SDK & NDK Tools의 12개 항목 설치를 완료했습니다. 구조 검사에서 Editor 포함 13개 항목 모두 `ok`였고 실제 NDK `27.2.12479018`, Gradle launcher `9.3.1`, SDK build tools `36.0.0` 및 JDK 실행 버전을 확인했습니다. SDK 37 모듈 설치는 앱 target SDK 변경 승인이 아니며 target36을 유지했습니다. Unity Personal 활성 라이선스와 저장된 OAuth 계정을 확인해 이 worktree에만 프로젝트 계정 pin을 설정했습니다.

## 실제 변경과 독립 검증

Editor가 URP/Core/ShaderGraph `17.0.4→17.3.0`, SpriteShape `10.1.1→13.0.0`, Authentication `3.7.3→3.7.4` 등을 자동 갱신하고 lock을 해석했습니다. manifest를 손으로 편집하거나 광고/IAP SDK를 별도로 업그레이드하지 않았습니다. IAP `5.4.3`, Burst `1.8.30`, Collections `2.6.8`, Cinemachine `2.10.7`, Pipeline pin은 유지했습니다. URP global settings·renderer·GraphicsSettings와 TextureImporter migration을 보존했습니다. 기존 Android 템플릿의 AGP `9.0.0` 및 GMA 중복 namespace 대응 `android.uniquePackageNames=false`도 유지했습니다.

검증 도구의 Editor/JDK 경로와 버전 guard를 6000.3.25f1로 갱신했습니다. 사용자는 2026-09-28 최소 지원 API25 상향과 후속 작업을 승인했습니다. toolchain·C# guard·APK/AAB 검사·fixtures를 min25로 함께 갱신했습니다. 아래 결과는 최종 출시 승인을 의미하지 않습니다.

| 항목 | 새 버전에서 확인한 결과 |
| --- | --- |
| 최초 Android 대상 import | C# `error CS` 0, shader error 0. 기존 min24 기준 검증은 실제 min25 자동 변경 때문에 1회 실패. 사용자 승인 후 min25 guard를 반영한 두 번째 실행 통과 |
| 라이브 Editor | `-batchmode -nographics` Editor에서 ready·컴파일 완료·Play Mode 중지 확인, autotick 및 콘솔/씬/계층 기록. 정상 종료 확인 |
| 광고 EditMode | `RevivalAd` 123/123 통과, 0.16초, failed/skip 0. 비동기 `test_status=completed` 확인 |
| IAP EditMode | `RevivalIAP` 40/40 통과, 0.06초, failed/skip 0. 비동기 완료 확인 |
| Python 출시 사전 검사 | 최초 7/7 통과. min25 변경 후 관련 4개 모듈 24/24 통과 |
| 새 JDK 합성 AAB 서명 검사 | 9/9 통과. 합성 키·작은 payload만 사용, 운영 keystore 서명 없음 |
| PowerShell 구문 | 변경된 7개 스크립트, 오류 0 |
| GUID | 패키지 해석 전 첫 검사 unresolved39 실패. 해석 후 두 번째 검사 132 YAML·unresolved0 통과. 세 번째 실행 없음 |
| migration된 meta | 68개 모두 원래 GUID 보존 |
| 기존 sprite fileID | 참조되는 54개 GUID/fileID를 새 Editor AssetDatabase 하위 에셋 목록과 대조, 54개 존재·누락0 |

일반 GUI Editor 열기 이후 프로세스가 유지되지 않은 원인은 미확정입니다. 해당 오류 창이나 Windows Application 오류 기록은 확인되지 않았으며, headless Editor 검증과 GUI·렌더링 시각 검증을 구분합니다. 자동 startup가 지운 signing alias는 Editor 정상 종료 후 비공개 원본 snapshot의 해당 행만 복구했습니다. keystore SHA-256과 나머지 서명·버전·target SDK·ARM64 계약 필드는 원본과 일치합니다. 빈 YAML 값 뒤의 Editor 자동 공백은 종료 후 서식만 정리했습니다. 비공개 에셋·설정·계정 pin·원시 로그는 커밋 대상에서 제외했습니다.

## APK·네이티브·실기기 결과

두 번째 APK 빌드는 source `fc176d630837d1931985bbfd2a68af746ec19592`에서 성공했습니다. 빌드 전 dirty 항목은 문서였으며 빌드 중 Editor가 DefaultVolumeProfile의 기본 URP Volume 컴포넌트와 URP global runtime 설정 참조를 추가 직렬화했습니다. 이 자동 결과도 후속 커밋에 보존했습니다. Editor 종료 후 서식만 정리했으며 이 기록 커밋을 빌드 직전 source로 표현하지 않습니다.

APK는 `Build/revival/Tamer-development.apk`, 110,897,768바이트, SHA-256 `9f7cd0be899f436daea7d045e59e3950756d25a3a8936ca3ebe02c0cf516b068`입니다. 실제 manifest min25/target36/ARM64 전용·별도 앱 ID·debug 서명·version1.0.5/code26 검사에 통과했습니다. 네이티브 6개 라이브러리의 기본 LOAD/ZIP 검사도 통과했습니다. 6개 `.so` 모두 압축 저장되어 ZIP mmap offset 정렬 검사 대상은 없었으며, 이 결과를 비압축 라이브러리의 ZIP offset 검증으로 표현하지 않습니다. 추가 strict RELRO 조건 1회는 `libc++_shared.so`, `libil2cpp.so`, `libmain.so`, `libswappywrapper.so`의 끝주소 modulo16384 조건 4개 실패이며 재실행하지 않았습니다. 기본 성공과 추가 실패를 구분합니다.

Android13/API33·ARM64·PAGE_SIZE4096 물리 기기에 격리 debug 앱만 새 설치하여 30초 실행했습니다. Unity/IL2CPP native mapping·전면 실행·Unity 시작 로그를 확인했고 native translation 및 fatal signal은 없었습니다. 스크린샷에서 회색 큐브·하늘이 보이며 검은 화면이나 magenta는 없었습니다. 새로 설치한 격리 앱만 삭제 완료했습니다. 기존 앱·운영 계정·저장·광고·구매·기기 네트워크 설정은 변경하지 않았습니다. 이는 격리 씬 시각 관찰이며 실제 게임 전체 렌더링이나 16KB 실행 검증이 아닙니다.

## 승인된 지원 범위와 렌더링 전환

[사전평가](lts-transition-preflight.ko.md)는 Unity 6.3의 Android 최소 지원 OS를 확인하지 못했습니다. 실제 import와 [공식 요구사항](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html)은 Android 7.1/API25 이상입니다. 사용자 승인으로 min25를 수용했으며 Android 7.0/API24 기기는 이 전환 버전의 지원 대상에서 제외됩니다. 기존 앱·저장·No Ads 권한은 변경하지 않았습니다. 스토어 기기별 업데이트 제공 결과는 미검증입니다.

[GMA 안내](https://developers.google.com/admob/unity/quick-start)는 min24이며 공식 Maven Billing9.0.0·AndroidX Core1.18.0 AAR manifest는 min23입니다. 확인한 항목은 엔진 min25보다 높은 최소 SDK를 강제하지 않습니다. 최종 APK manifest는 실제 빌드 결과로 별도 확인합니다.

첫 APK 빌드는 URP17.3 전처리에서 기존 Compatibility Mode 때문에 실패했습니다. [Unity6.3 업그레이드 안내](https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuideUnity63.html)에 따라 Editor SerializedObject로 해당 설정을 껐습니다. renderer feature 목록은 비어 있고 사용자 정의 ScriptableRenderPass/Feature 코드도 없었습니다. 임시 `URP_COMPATIBILITY_MODE` define을 추가하지 않았습니다. 보조 스크립트는 내부 타입 접근 컴파일 오류 1회 후 공개 Object 로드로 수정하여 두 번째 실행 성공했습니다. 설정 커밋 `69cd3f4`와 추가 자동 변환 커밋 `fc176d6`을 기록하고 두 번째 APK 빌드를 수행했습니다.

새 APK·실기기 결과는 위에 기재했습니다. 실제 ARM64 16KB 실행·최종 출시 AAB·스토어 검증은 미완료입니다. 기존 Unity 결과를 새 버전 결과로 대체하지 않습니다. 5초 닫기 추가 조사는 보류하고 rewarded/No Ads 구조를 유지합니다.

원시 증거는 이 checkout의 비공개 `Logs/revival/lts-*`에 보존했습니다. [공개 요약](lts-transition-validation.json)은 실제 기준·실행 범위·실패/미검증 사항만 기록합니다.
