# Android 네이티브 16KB 정렬 검증

검증일: 2026-09-11. **최종 통합 APK는 LOAD/ZIP 검사를 통과했지만 공식 Android 가이드의 RELRO 끝 주소 modulo 검사는 5개 실패했다. 실제 16KB 실행·AAB split·스토어 승인은 미검증이다.** 아래 SDK 단독 APK의 4개 실패 기록과 구분한다.

## 최신 최종 통합 APK

계정 저장 #100, 게임플레이 #101, 풀 #102, 광고 #104, SDK/IAP #106을 포함한다. 빌드·테스트와 도구 버전의 전체 증거는 [integration-validation.json](integration-validation.json)에 기록했다.

- checkout: `C:/Users/pc_17/.codex/worktrees/7819/Tamer`
- 검증 소스: `f5b7e404d7bd2c797aed2c9a3c17416a2ef50ccf`
- APK: `Build/revival/Tamer-development.apk`, `104785252` bytes
- SHA-256: `0d51f218491d90aa7e963b0e42b3ebf5606fc8e0d91f4b710d246fb8223eeb27`
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android API/Build Tools `36`/`36.0.0`, NDK `27.2.12479018` (`r27c`)
- ARM64 전용 개발 APK, 별도 앱 ID `com.AeDeong.MonsterTamer.revival`, debug signing

| 검사 | 최종 통합 결과 |
| --- | --- |
| LOAD/ZIP | 6개 `.so`, 23개 LOAD 검사 통과; 기본 종료 코드 0 |
| 패키징 | 6개 모두 압축, `extractNativeLibs=true`; 직접 mmap용 ZIP 16KB 정렬 N/A |
| SDK `zipalign -c -P 16 4` | 종료 코드 0 |
| 공식 가이드 RELRO 끝 modulo | 5개 실패, `relroChecksPassed=false`; strict 종료 코드 1 |
| RELRO와 LOAD 전체 범위 일치 | 6개 전부 |
| RELRO 보호 범위를 16KB로 내림/올림했을 때 원 RELRO 밖 쓰기 가능 LOAD 바이트와 겹침 | 앞·뒤 확장부 모두 검사, 0개 |
| 실제 16KB 실행·AAB split·스토어 승인 | 미검증 |

끝 modulo 실패는 `libc++_shared.so`, `libil2cpp.so`, `libmain.so`, `libswappywrapper.so`, `libunity.so`다. SDK 단독 기록에서 통과했던 `libil2cpp.so`의 최종 통합 끝 나머지는 `0x2000`이다. `lib_burst_generated.so`는 통과했다. 겹침 0개는 선언된 LOAD 범위의 정적 계산이며, 실제 로더·메모리 접근의 성공 판정이 아니다.

## 이전 SDK 단독 APK 기록: 검사 대상과 재현

이하 두 표는 SDK/IAP 및 계정 저장 #100 소스의 역사 기록이며 광고 #104를 포함한 최신 통합 결과가 아니다.

- 당시 checkout: `C:/Users/pc_17/.codex/worktrees/7b9b/Tamer`, `codex/revival-sdk-iap` 작업 브랜치
- 검증 소스 HEAD: `521e8b719058129681fa7e810d593f74e76c2480`; 이후 문서·검증 증거만 수정했다. 기존 Library 캐시를 사용했으며 APK 빌드 결과는 [sdk-validation.json](sdk-validation.json)에 연결한다.
- APK: `Build/revival/Tamer-development.apk`
- 크기: `143217843` bytes
- APK SHA-256: `b8f18c4bd1696dfac466661feb93726bc99c881ebba65d35a644e5cacc694e70`
- Unity: `6000.0.81f1`, Android NDK: `27.2.12479018` (`r27c`), Android SDK Build Tools: `36.0.0`
- ABI: `arm64-v8a`만 포함

이 해시는 **위 소스에서 생성한 최종 SDK APK**의 식별자다. 다른 브랜치 또는 Unity 버전으로 만든 APK/AAB에는 이 판정을 그대로 적용하지 않는다. APK와 원시 로그는 공개 저장소에 추가하지 않는다.

다음 명령은 실행하는 checkout의 현재 APK를 검사한다. 이전 SDK 결과를 재현하려면 위 SHA-256의 APK를 지정해야 한다. `zipalign`은 해당 Unity 설치에 포함된 Android SDK Build Tools `36.0.0` 실행 파일을 사용한다.

```powershell
python -m unittest discover -s tools/revival -p test_verify_native_alignment.py -v
python tools/revival/verify_native_alignment.py
python tools/revival/verify_native_alignment.py --strict-relro --output Logs/revival/native-alignment-strict.json
zipalign -c -P 16 4 Build/revival/Tamer-development.apk
```

## 이전 SDK 단독 APK 결과

| 검사 | 결과 |
| --- | --- |
| 합성 ELF/ZIP 단위테스트 | 26개 통과; 구매 에셋·SDK 바이너리 fixture 없음 |
| LOAD 세그먼트 | 6개 `.so`의 23개 전부 `p_align=16384`, `p_offset`/`p_vaddr` 합동 통과 |
| APK 구조 및 ABI | 검사 대상 ELF·ZIP 구조, CRC, ARM64 일치 통과 |
| 네이티브 ZIP 정렬 | 6개 모두 DEFLATE 압축; 직접 mmap용 ZIP 16KB 정렬은 **6개 모두 N/A** |
| SDK `zipalign -c -P 16 4` | 종료 코드 0 |
| `loadZipChecksPassed` | `true`; 기본 실행 종료 코드 0 |
| `relroChecksPassed` | `false`; 6개 모두 RELRO 존재, 4개 끝 주소 modulo 검사 실패 |
| `--strict-relro` | 종료 코드 1; 위 추가 RELRO 검사 실패를 그대로 반영 |
| `runtime16KBVerified` | `false`; 기기·에뮬레이터 실행 없음 |

SDK `zipalign`의 성공은 압축된 `.so` 내부 ELF의 RELRO를 확인하지 않는다. 압축 라이브러리는 설치 시 추출하는 패키징 경로이므로 ZIP 내부 데이터 시작 위치가 16KB 배수가 아니어도 해당 ZIP 정렬 조건은 적용되지 않는다. 해당 SDK APK manifest의 `extractNativeLibs=true`도 aapt2로 확인했다. AAB에서 생성된 split APK는 검사하지 않았다. [Android ZIP 정렬 도구](https://developer.android.com/tools/zipalign), [Android 라이브러리 패키징 안내](https://developer.android.com/guide/practices/page-sizes#update-the-packaging-of-your-shared-libraries)

모든 파일의 ZIP 경로는 `lib/arm64-v8a/` 아래다. `RELRO 끝`은 `p_vaddr + p_memsz`다.

| 파일 | LOAD 수 | RELRO 끝 | 끝 `% 0x4000` | 가이드 끝 정렬 | RELRO와 LOAD 전체 범위 일치 |
| --- | ---: | --- | --- | --- | --- |
| `lib_burst_generated.so` | 4 | `0x88000` | `0` | 통과 | 예 |
| `libc++_shared.so` | 4 | `0x143000` | `0x3000` | 실패 | 예 |
| `libil2cpp.so` | 4 | `0x85c4000` | `0` | 통과 | 예 |
| `libmain.so` | 4 | `0xa000` | `0x2000` | 실패 | 예 |
| `libswappywrapper.so` | 3 | `0x39000` | `0x1000` | 실패 | 예 |
| `libunity.so` | 4 | `0x1feb000` | `0x3000` | 실패 | 예 |

## RELRO 결과의 해석

2026-09-10 갱신된 공식 Android 가이드는 `(p_vaddr + p_memsz) % 16384 == 0`을 요구한다. 따라서 최신 통합 5개, 이전 SDK 단독 4개 실패를 유지한다. 가이드가 설명하는 충돌은 RELRO 보호 범위를 페이지 끝까지 확장하면서 계속 써야 하는 데이터를 읽기 전용으로 만드는 경우다. [Android RELRO 가이드](https://developer.android.com/guide/practices/page-sizes#check-relro)

AOSP Android 15/16 일반 로더는 RELRO 시작을 페이지 아래로, 끝을 위로 반올림한 뒤 `mprotect`한다. 끝 modulo 자체를 거부하는 검사는 아니다. [Android 15 일반 로더](https://android.googlesource.com/platform/bionic/+/android15-release/linker/linker_phdr.cpp#1124), [Android 16 일반 로더](https://android.googlesource.com/platform/bionic/+/android16-release/linker/linker_phdr.cpp#1340)

AOSP Android 16 QPR2의 `phdr_table_get_relro_min_align`은 같은 시작 주소에서 RELRO가 LOAD 전체를 덮으면 추가 정렬 값을 반환하지 않는다. 이는 **호환 모드 전환에 사용할 최소 정렬 판단**이며, whole-LOAD 바이너리를 QPR2에서만 허용하는 규칙이 아니다. [QPR2 호환 모드 판단](https://android.googlesource.com/platform/bionic/+/android16-qpr2-release/linker/linker_phdr_16kib_compat.cpp#423)

최신 통합 APK의 whole-LOAD 6개 및 앞·뒤 쓰기 가능 바이트 겹침 0개는 위 특정 충돌이 없을 가능성을 뒷받침한다. 따라서 **끝 modulo 실패만으로 실행 불가를 단정할 수 없고, 이 배치에서는 런타임 실패 예측의 false positive일 수 있다.** 정적 범위 계산은 로더의 추가 패딩·mmap 순서·재배치·실제 쓰기 및 페이지 크기 가정을 시뮬레이션하지 않는다. 공식 가이드 검사 통과나 전체 16KB 호환성을 뜻하지 않는다.

직접 링크하는 코드는 NDK r27 이하의 `max-page-size=16384`, `common-page-size=16384` 옵션을 검토할 수 있다. 공급자 prebuilt `.so` 내부는 프로젝트 링크 옵션만 추가해도 바뀌지 않는다. 수정 필요성이 확인되면 공급자의 공식 재빌드·대체본이 필요하다. [Android 링크·prebuilt 안내](https://developer.android.com/guide/practices/page-sizes#compile-your-app-using-16-kb-elf-alignment)

Unity는 `6000.0.38f1`에서 16KB 지원 수정과 NDK r27c 전환을 기록했다. 현재 `6000.0.81f1`이라는 버전만으로 실행 불가나 버전 전환 필수를 단정하지 않는다. [Unity 공식 릴리스 노트](https://unity.com/releases/editor/whats-new/6000.0.38f1)

최종 AAB와 생성 split APK를 별도로 검사하고, 실제 ARM64 Android 15/16에서 `adb shell getconf PAGE_SIZE`가 `16384`인지 확인한 뒤 호환 모드에 의존하지 않는 시작·게임 진행·안전한 SDK 경로를 검증해야 한다. 개발 APK의 압축 패키징과 zipalign 성공은 이를 대신하지 않는다. 스토어 판정도 별도 미검증이다. [Android 테스트 안내](https://developer.android.com/guide/practices/page-sizes#test-your-app-in-a-16-kb-environment)

## 도구의 판정 범위

schema 2는 모호한 `staticChecksPassed`를 사용하지 않는다.

- `loadZipChecksPassed`: LOAD 정렬·합동 조건, 압축하지 않은 네이티브 ZIP 데이터 정렬, 검사 대상 구조와 기대 ABI의 결과. 기본 종료 코드는 이 결과만 따른다.
- `relroChecksPassed`: 각 라이브러리의 RELRO 존재 및 **가이드의 단순 끝 modulo 조건**을 검사한 별도 결과. `false`를 숨기거나 LOAD 통과로 대체하지 않는다.
- `--strict-relro`: 위 두 조건을 모두 만족해야 종료 코드 0. 전체 LOAD와 일치하는 예외적 배치도 가이드 끝 검사에서 제외하지 않는다.
- `matchesWholeLoadSegment`, `endRemainder16KB`: 결과 해석에 필요한 배치 증거. 완전한 ELF 링커 시뮬레이션이나 실행 성공 판정은 아니다.
- `runtime16KBVerified`: 항상 `false`. 실제 16KB 환경에서의 시작·게임 진행·SDK 경로 검증은 별도 작업이다.

기본 JSON은 `Logs/revival/native-alignment.json`, 엄격 모드 JSON은 위 명령의 `Logs/revival/native-alignment-strict.json`에 저장된다. 네이티브 경로·해시·세그먼트별 수치가 포함되며, 공개 문서에는 이 요약만 기록한다.
