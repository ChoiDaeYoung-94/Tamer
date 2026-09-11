# Android 네이티브 16KB 정렬 검증

검증일: 2026-09-11. SDK/IAP 및 계정 저장 #100을 포함한 최종 SDK 작업 브랜치 APK를 검사했다. 광고 #104의 통합 APK는 별도다. **LOAD/ZIP 검사는 통과했지만, Android 가이드의 추가 RELRO 끝 주소 검사는 통과하지 않았다. 실제 16KB 기기 실행 및 스토어 승인은 미검증이다.**

## 검사 대상과 재현

- checkout: 이 저장소의 `codex/revival-sdk-iap` 작업 브랜치
- 검증 소스 HEAD: `521e8b719058129681fa7e810d593f74e76c2480`; 이후 문서·검증 증거만 수정했다. 기존 Library 캐시를 사용했으며 APK 빌드 결과는 [sdk-validation.json](sdk-validation.json)에 연결한다.
- APK: `Build/revival/Tamer-development.apk`
- 크기: `143217843` bytes
- APK SHA-256: `b8f18c4bd1696dfac466661feb93726bc99c881ebba65d35a644e5cacc694e70`
- Unity: `6000.0.81f1`, Android NDK: `27.2.12479018` (`r27c`), Android SDK Build Tools: `36.0.0`
- ABI: `arm64-v8a`만 포함

이 해시는 **위 소스에서 생성한 최종 SDK APK**의 식별자다. 다른 브랜치 또는 Unity 버전으로 만든 APK/AAB에는 이 판정을 그대로 적용하지 않는다. APK와 원시 로그는 공개 저장소에 추가하지 않는다.

저장소 루트에서 실행한다. `zipalign`은 해당 Unity 설치에 포함된 Android SDK Build Tools `36.0.0` 실행 파일을 사용한다.

```powershell
python -m unittest discover -s tools/revival -p test_verify_native_alignment.py -v
python tools/revival/verify_native_alignment.py
python tools/revival/verify_native_alignment.py --strict-relro --output Logs/revival/native-alignment-strict.json
zipalign -c -P 16 4 Build/revival/Tamer-development.apk
```

## 실제 결과

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

SDK `zipalign`의 성공은 압축된 `.so` 내부 ELF의 RELRO를 확인하지 않는다. 압축 라이브러리는 설치 시 추출하는 패키징 경로이므로 ZIP 내부 데이터 시작 위치가 16KB 배수가 아니어도 해당 ZIP 정렬 조건은 적용되지 않는다. 최종 APK manifest의 `extractNativeLibs=true`도 aapt2로 확인했다. AAB에서 생성된 split APK는 검사하지 않았다. [Android ZIP 정렬 도구](https://developer.android.com/tools/zipalign), [Android 라이브러리 패키징 안내](https://developer.android.com/guide/practices/page-sizes#update-the-packaging-of-your-shared-libraries)

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

2026-09-10 갱신된 Android 가이드는 RELRO 끝 주소를 16384로 나눈 나머지가 0인지 확인하도록 설명한다. 그 조건만 적용하면 위 4개 파일은 추가 검사 실패다. 가이드가 설명하는 충돌은 RELRO를 페이지 끝까지 읽기 전용으로 보호하면서, 같은 영역에 남아 있는 쓰기 가능한 데이터를 함께 보호하는 경우다. NDK r27 이하에서는 `max-page-size=16384`와 `common-page-size=16384` 링크 옵션을 함께 안내한다. [Android 16KB 가이드](https://developer.android.com/guide/practices/page-sizes#check-relro)

실제 링커는 배치도 구분한다. AOSP `android16-qpr2-release`의 `phdr_table_get_relro_min_align`은 RELRO와 LOAD 시작 주소가 같고 RELRO가 LOAD 전체를 덮으면 추가 끝 정렬 검사를 적용하지 않는다. 일부만 덮는 RELRO prefix 뒤에 쓰기 가능한 데이터가 있는 경우의 정렬을 별도로 다룬다. [AOSP 링커 구현, 423–470행](https://android.googlesource.com/platform/bionic/+/android16-qpr2-release/linker/linker_phdr_16kib_compat.cpp#423)

이번 APK는 6개 모두 RELRO 시작·크기가 해당 LOAD와 정확히 같았다. 또한 뒤따르는 LOAD의 시작은 RELRO 끝을 16KB로 올림한 주소 이후였다. **이 배치 증거와 위 AOSP 구현으로 볼 때, 끝 modulo 실패 4개만으로 Unity `6000.0.81f1`이 16KB 실행을 막는다고 단정할 수 없다.** 이는 정적 자료에 근거한 추론이며, 다른 OS 링커 구현·실제 런타임·Play 심사를 검증한 결론은 아니다. 현재 결과만으로 Unity 버전 전환이나 기존 네이티브 바이너리 변경을 강제하지 않는다.

## 도구의 판정 범위

schema 2는 모호한 `staticChecksPassed`를 사용하지 않는다.

- `loadZipChecksPassed`: LOAD 정렬·합동 조건, 압축하지 않은 네이티브 ZIP 데이터 정렬, 검사 대상 구조와 기대 ABI의 결과. 기본 종료 코드는 이 결과만 따른다.
- `relroChecksPassed`: 각 라이브러리의 RELRO 존재 및 **가이드의 단순 끝 modulo 조건**을 검사한 별도 결과. `false`를 숨기거나 LOAD 통과로 대체하지 않는다.
- `--strict-relro`: 위 두 조건을 모두 만족해야 종료 코드 0. 전체 LOAD와 일치하는 예외적 배치도 가이드 끝 검사에서 제외하지 않는다.
- `matchesWholeLoadSegment`, `endRemainder16KB`: 결과 해석에 필요한 배치 증거. 완전한 ELF 링커 시뮬레이션이나 실행 성공 판정은 아니다.
- `runtime16KBVerified`: 항상 `false`. 실제 16KB 환경에서의 시작·게임 진행·SDK 경로 검증은 별도 작업이다.

기본 JSON은 `Logs/revival/native-alignment.json`, 엄격 모드 JSON은 위 명령의 `Logs/revival/native-alignment-strict.json`에 저장된다. 네이티브 경로·해시·세그먼트별 수치가 포함되며, 공개 문서에는 이 요약만 기록한다.
