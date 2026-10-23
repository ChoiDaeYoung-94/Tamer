# Play 원본 AAB·APK 정적 검사

`tools/revival/verify_play_artifacts.py`는 명시한 원본 AAB SHA-256, Play APK 신원과 앱 서명, native payload를 읽기 전용으로 대조합니다. Python 3.11 이상, Windows, 신뢰할 수 있는 AndroidPlayer의 SDK build-tools와 OpenJDK가 필요합니다. APK/AAB 실행·설치·업로드나 계정 접근은 하지 않습니다.

모든 신원 기대값에는 기본값이 없습니다. 아래 PowerShell 변수에는 검증 대상의 **절대 경로**와 독립적으로 확인한 기대값을 넣습니다. `$reportPath`의 상위 폴더는 기존 비공개 폴더여야 하며, 출력 파일은 새 이름이어야 합니다. 결과에는 로컬 경로·인증서 지문이 있으므로 공개 커밋/이슈에 넣지 않습니다.

```powershell
python tools/revival/verify_play_artifacts.py `
  --original-aab $originalAabPath --play-apk $playApkPath `
  --android-player $androidPlayerPath --output $reportPath `
  --expected-aab-sha256 $originalAabSha256 `
  --expected-package $packageId --expected-version-code $versionCode `
  --expected-version-name $versionName `
  --expected-min-sdk $minSdk --expected-target-sdk $targetSdk `
  --expected-debuggable false --expected-abi arm64-v8a `
  --expected-app-cert-sha256 $playAppSigningCertSha256
```

`--android-player`는 `SDK`와 `OpenJDK`를 포함하는 AndroidPlayer 루트입니다. build-tools 기본 선택은 `36.0.0`이며 `--build-tools-version`으로 명시 변경할 수 있습니다. 인증서 SHA-256은 콜론 없는 64자리 hex입니다. 기대 앱 서명 집합을 여러 개 명시할 때만 `--expected-app-cert-sha256`을 반복합니다. 기대 ABI도 같은 방식으로 반복하며 관측 집합과 정확히 같아야 합니다.

앱 서명은 `apksigner verify --print-certs`의 exit 0과 `Signer #N certificate SHA-256 digest:` 전체 집합의 정확한 일치를 모두 요구합니다. Source Stamp는 별도 필드이며 앱 서명 대신 사용할 수 없습니다. 누락·중복·모호한 signer 기록은 실패합니다. AAB 업로드 키와 APK 앱 서명 키는 별도 역할이고, 이 도구는 AAB 서명 자체를 검증하지 않습니다. [apksigner 공식 설명](https://developer.android.com/tools/apksigner)과 [출력 구현](https://android.googlesource.com/platform/tools/apksig/+/refs/heads/main/src/apksigner/java/com/android/apksigner/ApkSignerTool.java)을 참고하세요.

`aapt2 dump badging`의 첫 `package:` 행을 필드 경계로 파싱합니다. `compileSdkVersionCodename` 끝의 `name`이나 label 안의 기대 문자열은 package name을 대신하지 않습니다. SDK·debuggable·ABI는 각각 독립된 행에서 검사하며, 중복 메타데이터도 거부합니다. 기대 min24로 과거 파일을 확인할 수 있어도 **현재 min25/target36/ARM64 기준을 낮추거나 현재 출시 조건 통과로 판정하지 않습니다**.

원본 AAB의 `base/lib/<abi>/*.so`와 APK의 `lib/<abi>/*.so` 전체 경로 집합 및 각 SHA-256이 같아야 합니다. `verify_native_alignment.inspect_apk(..., strict_relro=False)`의 동일 APK 해시·LOAD/ZIP 성공도 요구합니다. RELRO 결과는 별도 진단으로 남기며 실패를 숨기지 않습니다. strict RELRO 옵션은 제공하지 않습니다. runtime 16KB·기기별 split·정책·실제 구매/복구·스토어 승인·전체 출시 준비는 이 검사 범위 밖이며 결과의 관련 필드는 `false`입니다.

검사 실패는 exit 1이며 새 성공 JSON을 쓰지 않습니다. 기존 출력은 덮어쓰지 않고 원본도 변경하지 않습니다. CLI는 원시 도구 로그를 출력하지 않으며, 모듈 API `verify_artifacts(...)`는 guard 실패를 예외로 전달합니다. 2026-10-23 AGENTS 기준으로 고정된 두 번 실패 중단 규칙은 제거됐습니다. 승인된 범위에서 가설·원인·변경 조건·대상 SHA·원시 로그·실패 누적을 보존하고 필요한 재검증을 진행합니다. 파일 이름만 바꾼 동일 조건 반복이나 과거 native 결과를 새 실행 결과로 기록하는 일은 하지 않습니다. 운영 계정·저장·No Ads·서명키 보호와 총괄의 실행 직렬화는 유지합니다.

최소 합성 회귀(실제 SDK 도구는 mock, ELF/ZIP은 합성 데이터):

```powershell
python -m unittest discover -s tools/revival -p test_verify_play_artifacts.py -v
```

이 합성 회귀는 실제 SDK/Play 아티팩트 통합 검사 결과가 아닙니다. 실제 파일 검사는 총괄의 직렬 실행 소유권 아래 필요한 경우에만 수행합니다.
