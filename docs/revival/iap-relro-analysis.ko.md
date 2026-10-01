# 테스트 AAB RELRO 3건 분석

## 2026-10-01 새 개발 APK의 소스 생성 라이브러리 2개 검증

아래의 링크 설정 준비 후 실제 격리 개발 APK를 1회 빌드했다. 검증 checkout은
`C:/Users/pc_17/.codex/worktrees/ad-production-completion/Tamer`, 브랜치는
`codex/ad-production-compile`, 실행 source는
`4ba1422850c0bb2bb29182e177d160c5ed5ac0ac`이며 시작 시 미커밋 변경은 없었다.
이 source의 전체 tree는 통합 main `dfb7b67437a1795e2f49525e24a16ec3e0bf71a8`과 같다.
Unity `6000.3.25f1`·revision `e1dba0a9aba4`, CLI `1.0.0-beta.8`,
NDK `27.2.12479018`, Android min25/target36/ARM64를 사용했다.

빌드는 Succeeded·exit0·errors0이다. 최종 APK는 110,899,064바이트,
SHA-256 `f3efa94bbb12b930231afcb07bf473abcf9687d8dba09ff213e54dbc871d4533`이며
Build Summary의 집계 크기와 구분한다. 실제 IL2CPP link rsp와 FramePacing의
생성 `build.ninja`에서 `max-page-size=16384`와 `common-page-size=16384`가
각각 1회 전달되고 기존 링크 인자가 보존된 것을 확인했다.
최종 APK에서 읽은 새 소스 생성 파일의 결과는 다음과 같다.

| 파일 | LOAD 정렬 | strict RELRO 끝 조건 |
| --- | --- | --- |
| libil2cpp.so | 통과 | 통과, 끝 주소 %16384 = 0 |
| libswappywrapper.so | 통과 | 통과, 끝 주소 %16384 = 0 |

SDK 담당과 독립 검토자가 원자료를 별도로 읽고 같은 결과를 확인했다.
빌드 후 자동 변경 11개를 정확한 사전 바이트로 복원하고 새 생성 파일 2개를
비공개 보존한 뒤 원경로에서 정리했다. 사후 독립 검토에서 보호 파일 4,705개,
Assets 6,825개·meta 3,602개의 정확한 집합·해시, source/helper·빌드 원증거와
APK 보존, clean HEAD 및 해당 Editor 종료를 확인했다. Library는 보존했다.

이번 결과는 위 두 파일만의 LOAD/RELRO 검증이다. 새 APK의 사전 빌드
`libmain.so`·`libc++_shared.so`와 전체 strict·ZIP·서명 검사는 실행하지 않았다.
아래 과거 산출물의 사전 빌드 파일 미해결 이력은 보존하며, 새 APK에서도 같은
실패를 확인한 것으로 표현하지 않는다. 실기기·ARM64 16KB 실행·최종 출시 AAB·
스토어 검증과 전체 해결은 미완료이며 `productionBinary16KBVerified=false`를 유지한다.
원시 로그·실행 manifest·링크/ELF 읽기·복원 증거는 비공개로 보존한다.
LTS의 기존 검증과 이번 결과의 구분은 [LTS 전환 검증](lts-transition-validation.ko.md)을 참조한다.

## 2026-10-01 소스 링크 설정 보정 준비

main `2e822906`의 격리 `codex/relro-linker-flags`에서 실제 빌드 없이 두 경로를 보정했다.
기존 LTS APK `9f7cd0be899f436daea7d045e59e3950756d25a3a8936ca3ebe02c0cf516b068`의
4개 RELRO 실패를 통과로 바꾸거나 재검사하지 않았다. 아래는 기존 캐시의 읽기 조사다.

| 실패 파일 | 공급/빌드 경로 | 최소 조치와 이번 패치 범위 |
| --- | --- | --- |
| libil2cpp.so | Unity IL2CPP/Bee로 프로젝트 C++를 링크 | 기존 rsp는 max-page-size=16384만 포함. RevivalBuild baseline과 BuildScript APK/AAB의 공통 IDisposable scope에 common-page-size=16384를 추가하고 using 종료 시 추가 인자를 복원. 기존 linker 인자/환경변수가 있으면 충돌 검토 전 중단 |
| libswappywrapper.so | Unity Source/FramePacing CMake + androidx.games:games-frame-pacing:2.1.2 prefab static lib | flexible page ON인데 실제 build.ninja는 max-page-size만 포함. mainTemplate의 CMAKE_SHARED_LINKER_FLAGS에 common-page-size 추가. 외부 AAR의 완성 .so를 그대로 복사하는 경로가 아님 |
| libmain.so | Unity 6000.3.25f1 Development ARM64 제공 바이너리 | 기존 APK .so SHA와 설치본이 정확히 일치. 프로젝트 링크 플래그로 재링크 불가. 공급자의 정렬된 동일 역할 바이너리/승인된 별도 수정본 필요. Release variation의 최종 후보는 별도 확인 대상 |
| libc++_shared.so | 고정 NDK r27c의 사전 빌드 STL, CMake obj 및 merged native 경유 | 프로젝트 링크 플래그로 STL 자체 재링크 불가. 공급자 호환 STL 또는 승인된 별도 NDK 평가가 필요. strip/후속 캐시로 APK와 원본 SHA는 불일치하므로 정확한 원본 동일성까지 확정하지 않음 |

고정 NDK `27.2.12479018`의 android-legacy.toolchain은 flexible page 옵션에서 max-page-size만 추가한다.
AGP/ZIP 패키징 설정은 사전 빌드 ELF의 RELRO 끝을 바꾸지 않는다.
[Android 공식 안내](https://developer.android.com/guide/practices/page-sizes#compile-your-app-using-16-kb-elf-alignment)는
r27 이하에 max-page-size와 common-page-size를 함께 요구하며, 사전 빌드 라이브러리도 별도로 다시 빌드해야 한다.
[Unity 추가 IL2CPP 인자 API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PlayerSettings.SetAdditionalIl2CppArgs.html)와
설치된 il2cpp.exe의 --help에서 --linker-flags 지원을 확인했다. 추가 인자는 실험적 기능이며 실제 전달/성공은 미검증이다.

현재 패치는 FramePacing과 baseline/BuildScript IL2CPP의 **새 링크 조건 준비**다. 다른 하네스 빌더나 Editor 직접 빌드에는 IL2CPP scope를 자동 적용하지 않는다. 전역 설정을 영속 변경하거나 공급자 바이너리를 교체하지 않았다. 전체 4개 해결 또는 출시 준비 완료를 뜻하지 않는다.
기존 실패 횟수의 담당 간 누적은 미확정이며 같은 실패 산출물을 재실행하지 않았다.
실제 빌드 전에 이 새 source 조건·캐시 재링크 범위와 실패 이력을 총괄 담당이 확인해야 한다.
이번 구간은 diff 검사만 수행하며 C#/Gradle 컴파일, 새 빌드, 실제 16KB, 서명/키·SDK/Unity 버전 변경은 수행하지 않는다.

2026-09-12, Unity6000.0.81f1 고정. 대상은 [PR146 검증 AAB](iap-test-bundle-validation.json)의 SHA-256 `aef2ec2212b09415294d6b88d5de21c78e7730bf59ecabbc23514a36354aa37a`다. 기존 debug APK 분석을 반복하지 않고 신규 비디버그 AAB의 차이와 생성 경로를 확인했다. 코드·검사기·바이너리·Editor/NDK 설치·운영 설정은 변경하지 않았으며 재링크/기기 조작도 하지 않았다.

## 결론

현재 검사기의 RELRO 3건 실패는 수치상 맞으므로 유지한다. 그러나 끝 주소 나머지 하나만으로 이 AAB가 실제 16KB 환경에서 반드시 충돌한다고 단정할 근거는 부족하다. 세 구간 모두 하나의 LOAD 전체와 일치하며 보호 범위 확장으로 추가로 읽기 전용이 되는 선언된 writable LOAD 바이트는 0이다. 실제 실행 성공도 입증하지 않았다.

Unity 고정 상태에서 `libil2cpp.so`의 추가 링크 옵션 실험은 가능하다. 반면 `libmain.so`와 `libc++_shared.so`는 사전 빌드 파일이어서 같은 프로젝트 링크 옵션으로 수정되지 않는다. 세 건 모두를 해결하는 확정된 프로젝트 설정 변경은 이번 분석에서 찾지 못했다. 무조건 Unity 업그레이드나 ELF 헤더 패치, 검사 완화는 제안하지 않는다.

## readelf로 확인한 구간

설치된 NDK r27c(27.2.12479018)의 `llvm-readelf -Wl`을 AAB에서 추출한 읽기용 복사본 6개에 실행했다. Python 검사기의 값과 일치했다. 모든 LOAD의 p_align은 0x4000이며 파일 오프셋/가상 주소 합동 조건을 만족한다.

| 라이브러리 | RELRO VirtAddr | MemSiz | 끝 주소 | 끝 %0x4000 | 16KB 보호 범위 |
| --- | --- | --- | --- | --- | --- |
| libc++_shared.so | 0x1392f0 | 0x9d10 | 0x143000 | 0x3000 | [0x138000,0x144000) |
| libil2cpp.so | 0x6139bd0 | 0x51d430 | 0x6657000 | 0x3000 | [0x6138000,0x6658000) |
| libmain.so | 0x8f20 | 0x10e0 | 0xa000 | 0x2000 | [0x8000,0xc000) |

3건 모두 PT_GNU_RELRO의 시작/메모리 크기가 해당 RW LOAD와 정확히 같다. 보호 범위는 시작을 내림하고 끝을 올림하여 계산했다. 확장된 앞·뒤 부분과 원 RELRO 외부의 PF_W LOAD 구간을 교차 검사했고 겹침은 0이었다. 동적 메모리 접근이나 모든 Android 로더 버전을 시뮬레이션한 결과는 아니다. 수치·비교 요약은 [분석 JSON](iap-relro-analysis.json), 원시 readelf 출력과 바이너리는 비공개 Logs에 보관한다.

## 공식 기준과 실행 의미

Android 가이드는 RELRO에 대해 `(VirtAddr + MemSiz) % 0x4000 == 0`을 확인하도록 안내한다. 현재 strict 검사도 이 조건을 적용한다. 가이드가 설명하는 충돌 예시는 보호 범위 확장으로 쓰기 가능 데이터까지 읽기 전용이 되는 경우다. LOAD 정렬, APK ZIP 배치와 실제 16KB 실행도 별도의 확인 대상이다. [Android 16KB 가이드](https://developer.android.com/guide/practices/page-sizes#check-the-relro-security-flag)

Android16 QPR2 Bionic의 일반 로더 `_phdr_table_set_gnu_relro_prot`는 RELRO가 닿는 페이지를 경계로 확장해 mprotect한다. `_extend_gnu_relro_prot_end`는 전체 LOAD를 덮는지와 부분 RW 데이터가 남는지를 구분한다. 이 소스와 위 정적 배치를 종합하면, 가이드의 writable-tail 충돌 예시를 현재 3개 파일에 그대로 적용할 수 없다는 **추론**이 가능하다. 특정 OS에서의 성공이나 Play 승인 보장은 아니다. [Bionic 일반 로더 소스](https://android.googlesource.com/platform/bionic/+/android16-qpr2-release/linker/linker_phdr.cpp)

Unity6000.0 공식 문서는 6000.0.38f1 이상에서 16KB 지원을 안내한다. 현재81f1은 그 범위 안에 있다. 버전 번호만으로 개별 네이티브 파일과 앱의 호환성까지 확정할 수는 없다. [Unity 6.0 Android 호환성](https://docs.unity3d.com/6000.0/Documentation/Manual/android-requirements-and-compatibility.html)

## debug APK와의 차이

| 라이브러리 | 기존 IAP debug APK 끝 나머지 | 신규 AAB 끝 나머지 |
| --- | --- | --- |
| lib_burst_generated.so | 0 | 0 |
| libc++_shared.so | 0x3000 | 0x3000 |
| libil2cpp.so | 0x2000 | 0x3000 |
| libmain.so | 0x2000 | 0x2000 |
| libswappywrapper.so | 0x1000 | 0 |
| libunity.so | 0x3000 | 0 |

비교 APK는 SHA-256 `5424cfffe431bae18c2d2c4e5ca09bbb8c140c2c4e9f0231e9af4221cd9b5bd6`다. 5건→3건 변화는 검사 완화가 아니라 release 바이너리 선택/생성 결과의 변화다. libc++만 두 산출물에서 바이트가 동일했다.

## 생성 경로와 고정 버전에서 가능한 다음 실험

| 파일 | 확인한 생성 경로 | 고정 Unity에서의 판단 |
| --- | --- | --- |
| libmain.so | 설치된 AndroidPlayer/Variations/il2cpp/Release/Libs/arm64-v8a 파일과 바이트 동일 | 프로젝트 IL2CPP 플래그로 재링크되지 않는다. 공급자 근거/지원 경로가 필요하다. |
| libc++_shared.so | 설치된 NDK aarch64 libc++와 Build ID 일치. AAB는 stripped되어 파일 전체는 다름 | prebuilt 계열이라는 근거다. IL2CPP 옵션만으로 내부 배치가 바뀌지 않는다. NDK 파일 교체/정적 런타임 전환은 ABI·지원범위 검토 없는 즉시 수정안이 아니다. |
| libil2cpp.so | 실제 Bee 링크 rsp에 relro와 max-page-size=16384 존재, common-page-size 명시 없음 | 현재 도구로 추가 옵션 재링크 실험은 가능하나 아직 실행하지 않았다. |

Android는 NDK r27 이하에 max-page-size와 common-page-size를 모두16384로 지정하도록 안내한다. 설치된 IL2CPP `--help`는 `--linker-flags`와 `--linker-flags-file`을 제공하고 Unity는 추가 IL2CPP 인자를 설정하는 API를 문서화한다. [Android 링크 옵션](https://developer.android.com/guide/practices/page-sizes#compile-your-app-using-16-kb-elf-alignment), [Unity 추가 IL2CPP 인자 API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings.SetAdditionalIl2CppArgs.html)

검토 가능한 실험 입력은 동일 소스·동일6000.0.81f1/r27c·동일 테스트 빌드에서 기존 링크 인자를 보존하고 common-page-size=16384만 추가하는 것이다. 기대 효과는 생성되는 libil2cpp의 RELRO 끝 정렬 변화다. 실제 rsp에 옵션이 들어갔는지와 새 readelf/LOAD/RELRO를 다시 확인해야 한다. libmain/libc++ 실패까지 사라질 것으로 기대해서는 안 된다. 변경 시 추가 IL2CPP 인자도 finally 복원하고 별도 산출물로 원본을 보존해야 한다. 현재는 실험 구현·실행을 하지 않았다.

2026-09-14 당시에는 사용자님께서 사용 가능하다고 알려주시기 전까지 휴대폰 조작을 보류했다. 2026-09-23에는 기기 연결·사용을 허락받았지만, 실제 PAGE_SIZE와 호환 모드 조건을 확인한 별도 16KB 검증은 여전히 필요하다. 보류 중이던 잠금 해제나 계정 입력을 완료로 추정하지 않는다.

## 2026-09-23 재감사

위 3건은 9월 12일의 특정 AAB에서 **모두 존재하는 GNU_RELRO의 끝 주소 불일치**다. Android 공식 안내는 불일치가 16KB 환경에서 충돌한다고 설명하므로 strict 실패를 유지한다. 다만 세 RELRO가 각각 쓰기 가능 LOAD 전체와 일치하고 추가 writable 영역과 겹치지 않는다는 이 AAB의 배치, Android 16 Bionic의 whole-LOAD 처리 경로를 근거로 실제 충돌 여부는 해당 바이너리의 ARM64 네이티브 16KB 실행으로 확정해야 한다. RELRO가 **없는** 라이브러리는 공식 안내에서 이 정렬 항목에 적합하다고 보지만, 이 3건에는 해당하지 않는다. [Android RELRO 검사](https://developer.android.com/guide/practices/page-sizes#check-the-relro-security-flag), [Bionic 16KB 호환 경로](https://android.googlesource.com/platform/bionic/+/android16-qpr2-release/linker/linker_phdr_16kib_compat.cpp).

현재 고정 NDK r27c의 `libc++_shared.so`와 Unity의 `libmain.so`는 사전 빌드 파일이다. `libil2cpp.so`에 `common-page-size=16384`를 더하는 실험이 성공해도 앞의 두 파일과 최종 AAB의 strict 실패가 함께 해결되지는 않는다. 공급자 호환 바이너리와 승인된 후반 Unity LTS 전환 뒤 새 AAB로 각 파일을 재검사해야 한다. 9월 23일 x86_64 16KB 게스트 부팅은 확인했으나 ARM64 코드는 번역 계층으로 표시됐고 앱을 설치하지 않았으므로, 위 세 라이브러리의 실제 실행 판정에는 사용하지 않는다. [호스트 재점검](release-preflight.ko.md#2026-09-23-가속-및-격리-게스트-관측)을 참조한다.

추가 읽기 확인: Unity 제공 libmain Development/Release/Release_ThinLTO 모두 RELRO 끝 나머지 8192, whole-LOAD=true다. Release 변형 선택만으로 이 조건이 해소되지 않는다. NDK toolchain 636행 CACHE 설정은 FORCE가 없고 650행은 ANDROID_LINKER_FLAGS와 전달된 CMAKE_SHARED_LINKER_FLAGS를 연결한다. FramePacing CMake도 wrap 옵션을 기존 문자열에 추가한다. 따라서 현재 생성 인자에 기존 동일 -D가 없는 조건에서는 NDK flags를 유지한다. 같은 변수를 별도 커스텀화한 프로젝트로 확대 적용할 때는 중복 -D 검토가 필요하다. Gradle 인자는 세미콜론 없는 단일 문자열이며 IL2CPP는 --help에서 옵션 이름만 확인했다. nested quote의 실제 parser/링크 전달과 C# 컴파일은 새 빌드 전 검증 대상으로 남긴다.
