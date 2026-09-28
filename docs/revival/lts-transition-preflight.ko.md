# 후반 Unity LTS 전환 사전평가 (2026-09-28)

이번 기록은 전환 준비다. 현재 복구 Editor `6000.0.81f1`과 `tools/revival/toolchain.json`, `Packages/packages-lock.json`을 유지하며 Editor 설치·실행, 버전 전환, APK/AAB 생성, 스토어·대시보드 설정 변경은 하지 않았다. 기존 rewarded/No Ads 동작을 보존하고 5초 닫기 추가 조사는 보류한다. 일반적인 후반 LTS 업그레이드는 기존 승인 범위이며 현재 버전 고정 지침이 적용되는 이번 단계에서는 설치하지 않는다.

## 기준과 실행 시점 재조회

`7b9b/Tamer`는 detached HEAD `8b8d211c25527e2fa872aa1e1cffc264771186d8`, clean 상태였다. 해당 checkout의 Editor는 없었고 다른 프로젝트의 `6000.0.25f1` Editor는 보존했다. 원격 main을 조회해 `a6e52139b9490a615c100b7609794216a8479734`를 확인한 뒤 별도 `C:/Users/pc_17/.codex/worktrees/lts-transition-preflight/Tamer`, `codex/lts-transition-preflight`에서 문서만 작성했다. 원본 `D:/meee/git/Tamer`는 변경하지 않았다.

고정 CLI `1.0.0-beta.8`의 읽기 명령 `releases --lts --limit 5 --format json`과 [공식 LTS 지원 안내](https://unity.com/releases/unity-6/support), [릴리스 노트](https://unity.com/releases/editor/whats-new/6000.3.25f1)를 대조했다. 이번 조사에서 최신 정식 LTS 패치는 **Unity 6.3 LTS `6000.3.25f1`**, 2026-09-24 출시, revision **`e1dba0a9aba4`**였다. 최신 Supported인 6.6 및 6.7 beta와 구분한다. 문서에 미리 나타난 `6000.3.26f1+` 의존성 행은 출시 증거로 사용하지 않았다. 실제 전환 직전에 공식 릴리스 목록·패치·known issues를 다시 조회하고 그때의 정확한 revision을 고정한다.

[이전 6.3.24 설치 인계](unity63-handoff.ko.md)는 당시 UAC 취소·캐시 기록이다. `6.3.24` 캐시나 과거 다운로드 해시를 `6.3.25` 설치 검증에 재사용하지 않는다. 최신 후보의 공식 metadata·다운로드 integrity, 직접 SHA-256, Editor/Android 모듈 존재를 새로 확인한다. 이번에 설치 파일을 내려받거나 실행하지 않았다.

## 현 SDK와 전환 영향

| 대상 | 현재 기준 | 전환 시 확인할 사항 |
|---|---|---|
| Android 도구 | NDK r27c `27.2.12479018`, JDK 17, build-tools 36.0.0, AGP 9.0.0 사용자 템플릿 | [6.3 공식 의존성 표](https://docs.unity3d.com/6000.3/Documentation/Manual/android-supported-dependency-versions.html)도 NDK r27c/JDK17이다. `6.3.25` 노트의 Gradle은 **9.3.1**. 기존 AGP9 선언과 generated Gradle·resolver 결과를 대조하며 NDK r28 자동 전환으로 추정하지 않는다. |
| GMA/UMP | Unity 11.5.0 / Android Standard 25.4.0 / UMP 4.0.0, EDM4U 1.2.189 | [Google 최소 조건](https://developers.google.com/admob/unity/quick-start)은 Unity2019.4+, Android min24/target35+로 현 설정은 범위 안이다. 이는 6.3.25의 빌드/기기 보장이 아니다. 기존 Assets 방식과 GUID·SDK 선택을 유지하고 resolver·manifest·D8/Gradle을 검증한다. Unity Ads/LevelPlay는 최신 main에서 철회됐으므로 다시 추가하지 않는다. |
| IAP | 5.4.3, Billing9.0.0, services.core1.18.0 | [공식 5.4.3 변경 기록](https://docs.unity3d.com/Packages/com.unity.purchasing@5.4/changelog/CHANGELOG.html)과 현 v5 구현을 기준으로 유지한다. 연결·상품 조회·주문 조회·검증·내구 저장 후 Confirm 순서, 기존 No Ads, 복원 및 중복 지급 방지를 전환 후 확인한다. 새 commerce/Analytics 기능을 자동 활성화하지 않는다. |
| GPGS/PlayFab/UniTask | 2.2.1 / 2.242.260805 / 2.5.11 | [기존 SDK 감사](sdk-audit.ko.md)의 공식 출처와 현 lock/Assets 배치를 유지한다. 최신 LTS와의 compile·Android bridge 및 저장/세션 동작은 아직 미검증이며 숫자만으로 호환 확정하지 않는다. |
| 렌더링·UPM | URP/core/ShaderGraph17.0.4, Burst1.8.30, Collections2.6.8, Pipeline0.6.0-exp.1 | 전환 Editor의 지원 패키지와 lock diff를 검토한다. 강제로 구 lock을 덮거나 패키지를 한꺼번에 최신화하지 않는다. URP serialized migration·셰이더·폰트·SpriteAtlas·씬 GUID와 Pipeline compile을 확인한다. |

[Unity 6.3 업그레이드 안내](https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuideUnity63.html)는 custom Gradle 템플릿과 AGP9 SDK 검토, Android16 대화면의 Game category, SerializeField 대상 제한을 안내한다. `baseProjectTemplate.gradle`은 이미 AGP9.0.0이며 현 min24/target36·ARM64·서명 설정을 보존한다. `6.3.25` known issues의 Vulkan swapchain crash, 2D Renderer+Bloom 검은 화면 및 Graphics Jobs의 async shader 종료 deadlock은 프로젝트 사용 조건과 대조할 후보다. 릴리스 노트만으로 Tamer에서 발생한다고 단정하지 않는다.

도구도 함께 전환해야 한다. `Run-Baseline.ps1`의 `EditorPath`만 바꾸면 내부 test 명령은 여전히 `--editor-version 6000.0.81f1`이다. `Run-AdHarness.ps1`, `Run-GameplayHarness.ps1`, `Run-IapTestBundle.ps1`, `verify_release_candidate.py`와 여러 APK verifier에도 버전/AndroidPlayer 경로가 고정돼 있다. 실제 전환 PR에서 toolchain·ProjectVersion·명시적 실행 Editor·검증기의 같은 버전을 대조하고 업데이트한다. 기존 검증기에서 실패를 피하기 위해 pin 검사만 제거하지 않는다.

## 16KB: 정적 검사와 ARM64 실행의 분리

[기존 RELRO 분석](iap-relro-analysis.ko.md)은 특정 6000.0.81f1 AAB의 `libmain.so`, `libc++_shared.so`, `libil2cpp.so` strict RELRO 끝 정렬 실패 3건을 기록한다. 이전 LOAD/ZIP 성공과 별도 항목이다. 6.3도 r27c를 사용하므로 **업그레이드만으로 3건이 해결된다고 보장하지 않는다.** `libmain`·`libc++`의 prebuilt 공급 경로와 새 IL2CPP 링크 rsp·라이브러리 해시를 확인한 뒤 새 산출물 전체를 다시 판정한다. 추가 링커 플래그만으로 prebuilt 파일까지 수정됐다고 표시하지 않는다.

[Android 공식 16KB 안내](https://developer.android.com/guide/practices/page-sizes)에 따라 새 APK/AAB의 모든 ARM64 `.so` LOAD 및 GNU_RELRO 끝 정렬, AAB page alignment, 실제 전달 split ZIP 정렬을 확인한다. 기본 `verify_native_alignment.py` 성공과 `--strict-relro` 결과를 구분하며 실패를 완화하거나 제거하지 않는다. 최종 AAB와 실제 전달 APK의 SHA-256·서명·SDK/NDK/AGP·Unity commit을 새로 기록한다.

9월23일의 [WHPX/격리 AVD 관측](release-preflight.ko.md#2026-09-23-가속-및-격리-게스트-관측)은 PAGE_SIZE16384인 **x86_64 게스트 부팅**이다. `libndk_translation.so`가 있고 앱을 설치하지 않았으므로 ARM64 네이티브 앱 실행이 아니다. 이전 AVD 부팅·같은 실패 실험은 이번에 반복하지 않았다.

| 후속 실행 경로 | 사용할 근거와 한계 |
|---|---|
| ARM64 실기기 16KB mode | 소유자가 사용을 승인한 공유/전용 기기에서 PAGE_SIZE16384, 프로세스 ARM64, linker compatibility mode 여부를 직접 기록. 개인폰·운영 계정을 자동 사용하지 않는다. |
| ARM64 호스트의 ARM64 16KB Emulator 또는 Cuttlefish | [공식 ARM64 Cuttlefish 경로](https://source.android.com/docs/core/architecture/16kb-page-size/getting-started-cf-arm64-pgagnostic), [호스트/이미지 ABI 조건](https://developer.android.com/studio/run/emulator-acceleration)을 확인하고 별도 환경에서 생성한다. 현재 Intel Windows WHPX가 ARM64 이미지를 같은 방식으로 가속한다고 추정하지 않는다. |
| 승인된 원격 기기 | Android 가이드가 소개하는 [Samsung Remote Test Lab](https://developer.samsung.com/remote-test-lab)을 포함해 16KB·ARM64 조건과 app upload 공개/보관 범위를 확인한다. 업로드·계정 생성·약관 동의는 이번에 하지 않는다. |
| 현 x86_64+ARM64 번역 AVD | 설치·UI sanity 확인의 보조 경로로만 사용할 수 있다. 실제 ARM64 네이티브 16KB 및 최종 스토어 호환성 판정을 대체하지 않는다. |

## 전환 실행 순서와 종료 기준

1. 기능·광고·저장 복구 상태 및 미검증 목록을 통합 담당과 대조하고, 그때의 main/정식 LTS를 다시 조회한다. dirty checkout은 업데이트하지 않는다. 별도 전환 worktree에서 원본 `.meta`·GUID·keystore를 보존하고 Editor 시작 전에 승인 원본으로 에셋 복원 검증한다.
2. 설치가 허용된 실행 단계에서 공식 LTS와 Android 모듈을 기존 Editor와 나란히 준비한다. 기본 계정·다른 프로젝트 Editor·Hub 기본 버전을 변경하지 않는다. 해당 프로젝트 license pin과 설치 integrity를 확인한다.
3. 실제 새 Editor 절대 경로와 checkout/branch/HEAD/미커밋 변경을 증거에 남긴다. import/compile, lock·serialized diff 및 위 고정 검증 경로를 먼저 검토한다. 같은 Editor 쓰기는 직렬화한다.
4. 바뀐 Editor/패키지 때문에 필요한 최소 EditMode와 baseline 검증을 1회 수행한다. 새 Android 격리 build 및 전체 SDK 검증은 변경 범위에 맞춰 수행하며 과거 결과를 재실행하지 않는다. 동일 테스트가 2회 실패하면 해당 경로·의존 작업을 중단하고 원인/시도/필요 결정을 먼저 보고한다.
5. 새 runtime에서 시작·씬 전환·저장 재시작·삭제 접수 화면·No Ads 기존 권한/복원·rewarded SDK callback/BGM 복귀를 확인한다. 운영 로그인·결제·광고를 자동 실행하지 않는다. 5초 추가 조사 보류와 기존 보상 구조를 유지한다.
6. 새 최종 AAB·전달 split·ARM64 16KB 실행과 필요한 스토어 검증을 끝내기 전에는 출시 완료로 표시하지 않는다. 실패 시 전환 worktree를 보존해 원인을 기록하고, 기존 6000.0 결과와 파일을 rollback 기준으로 유지한다.

## IAP: 추가 키·개인폰 없이 준비한 범위

현재 `IAPManager`는 StoreController v5에서 기존 주문 조회 및 검증 성공/내구 저장 후 Confirm 경로를 유지한다. [독립 acknowledgment 잔여](iap-followup-readiness.ko.md)에서 Console 처리됨·미환불 관측은 직접 ACK 필드 조회와 다르며, 직접 읽기는 기존 테스트 앱용 Android Publisher 인증과 기존 구매 토큰이 있어야 가능하다. 이번에는 토큰 추출·자격 생성·API Explorer 동의·기존 주문 환불·권한 회수를 하지 않았다. 새 인증을 사용자에게 즉시 요구하지 않는다.

승인된 환경이 추후 있으면 GET `purchases.productsv2.getproductpurchasev2`에서 acknowledgment 상태만 비공개 확인한다. 구매/acknowledge 쓰기 API는 이 읽기 절차에 포함하지 않는다. 다른 설치·삭제 후 No Ads 복원은 승인된 격리 설치와 같은 Store 소유 계정, 새 게임 계정의 권한 연결을 먼저 확인해야 한다. 추가 사용자 키나 개인폰이 없는 이번 단계에서 실제 복원 완료로 보고하지 않는다. 기존 주문/복원 테스트는 재실행하지 않았다.

이번 PR은 문서만 변경하므로 Unity 테스트·빌드·기기 실행은 0회다. 새 APK 및 SHA-256은 없다. 검증은 기준 Git 상태와 공식 자료·소스의 읽기 대조 및 문서 diff 점검에 한정한다.

## 후속 실제 전환 기록과 정정

이 문서는 설치 전 사전평가 기록입니다. 2026-09-28 PR #252의 단계 전환 뒤 수행한 설치·격리 import·독립 검증과 Android 최소 API24→25 지원 축소는 [실제 전환 검증](lts-transition-validation.ko.md)을 확인합니다. 위 사전평가에서 Unity 6.3의 최소 지원 OS 확인이 누락됐음을 정정합니다. 사용자는 최소 API25 상향을 승인했으며 관련 검증 도구도 함께 갱신했습니다.
