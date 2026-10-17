# Core 17.3.0 GLES3 FSR 수정 후보

Unity 6000.3.25f1/e1dba0a9aba4의 Core 17.3.0에서 GLES3에 기존 FP32 FSR 구현을 선택하는 **로컬 수정 후보**입니다. 공식 Unity 수정판이 아닙니다. 2026-10-17 UPM 연결과 개발 APK 빌드에서 FinalPost GLES3 변형48개 새 컴파일/오류0을 확인했으며, 실제 기기 검증은 남아 있습니다. [검증 소스와 결과·미검증 범위](../../../../../docs/revival/urp-gles3-fsr.ko.md)를 확인해 주세요. provenance.json의 status는 후보 패키징 당시 상태로 보존합니다.

## 변경

`fsr-gles3-fp32.patch`는 `Runtime/PostProcessing/Shaders/FSRCommon.hlsl`의 조건 한 줄만 변경합니다.

```diff
-#if REAL_IS_HALF && !defined(SHADER_API_D3D11)
+#if REAL_IS_HALF && !defined(SHADER_API_D3D11) && !defined(SHADER_API_GLES3)
```

별도의 metadata 변경으로 package.json의 원본 생성 `_fingerprint`를 제거했습니다. 수정판을 원본 fingerprint로 인증한 것처럼 보이지 않게 하기 위한 조치이며, 원본 값은 provenance.json에 보존했습니다. name/version(17.3.0)/unity/dependencies는 동일합니다. package.json도 원본과 달라진다는 사실을 한 줄 셰이더 patch와 구분합니다.

그 외 2,995개 파일은 원본과 bytes가 같습니다. 모든 C# 코드·.meta/GUID·Editor/Runtime/PrivateShared·ShaderLibrary/Shaders·ApiUpdater~·Tests·Samples~·Documentation~·라이선스와 파일 내부 저작권 고지를 보존했습니다. URP의 Renderer Shader User Value Samples가 Core의 Samples~ 두 디렉터리에 의존하므로 용량 절감을 위한 샘플 생략을 하지 않았습니다.

## 파일과 고정 출처

- 원본: 고정 Editor의 설치된 builtin Core package cache, fingerprint `3f80e92e91c2611f1415e75e9b791f6aeb165f26`.
- 원본 수/크기: 2,997 files / 57,585,783 bytes. 설치본 원본과의 별도 전수 대조나 공식 서명 확인을 했다는 뜻은 아닙니다.
- 원본 FSR SHA-256: `395727f6d8f0fbd9d84003f9fcf85f40bc25bb1ed60e534c40d2b301df96bb49`.
- `upstream-files.sha256.tsv`와 `patched-files.sha256.tsv`: 상대 POSIX 경로로 정렬한 `path<TAB>size<TAB>sha256<LF>` 목록. 원본 목록 SHA-256은 `f81b66a6949b5fb7b56ce0ede1a355a7554eea382f88dc55789068ee8a13ec2f`입니다.
- tgz: `com.unity.render-pipelines.core-17.3.0-tamer-gles3-fsr.tgz`, 21,619,622 bytes, SHA-256 `2360687d2de0f5c43e671868981851aa106a654cdd20f65128b0f6f34c0ec3cc`.
- `provenance.json`: 원본/수정본 목록·tgz·patch·변경 두 파일의 해시 및 패키징 규칙. 생성 시각·개인 절대 경로·호스트명·계정정보를 넣지 않습니다.

Core LICENSE.md와 THIRD PARTY NOTICES.md, AMD FSR/ffx_a의 파일 내부 MIT 고지는 tarball 안에 원본 그대로 있습니다. Core의 [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license)와 제3자 별도 조건을 적용하며, 원본의 고지/참조를 삭제하거나 자체 코드인 것처럼 재표기하지 않습니다. 아래 파일은 게임의 비공개 구매 에셋·계정 설정·서명키·원시 로그를 포함하지 않습니다.

## 재현과 검증

Python 3.12.14 / zlib 1.3.2에서 생성했습니다. 이 스크립트는 Editor/UPM/설치/빌드/Git/네트워크를 실행하지 않습니다. 패키지 생성은 한 번 준비하는 작업이며 checkout 또는 Editor 시작 시 자동 실행하지 않습니다. clone에서는 이미 저장된 tgz를 UPM이 사용합니다.

```powershell
python -B tools/revival/package-overrides/core-17.3.0/pack.py --source '<고정 Core 원본 절대 경로>'
python -B tools/revival/package-overrides/core-17.3.0/pack.py --source '<동일 원본 절대 경로>' --verify
```

출력은 스크립트와 같은 폴더의 지정된 다섯 파일뿐입니다. 이미 있는 파일이 다르면 덮어쓰지 않고 중단합니다. 다른 원본은 count/bytes/전수 목록 hash/FSR hash/원본 조건 검사에서 중단합니다. symlink/reparse point·비정규 파일·절대/상위 경로·중복 tar member를 거부합니다. 파일 내용을 메모리에서 한 번 읽고, 정렬된 `package/` 아래 regular member로만 저장합니다. uid/gid/mtime=0, owner명 없음, mode=0644, gzip mtime=0/filename 없음, 긴 경로에 필요한 PAX path만 허용합니다. 압축 후 전수 roundtrip bytes와 metadata도 대조합니다. 압축기 버전이 다르면 동일 내용이라도 tgz bytes가 달라질 수 있으며, 기존 파일이 있으면 조용히 교체하지 않고 중단합니다.

2,997개 중 변경은 HLSL 조건과 package.json metadata 두 파일만 허용합니다. 원본 전체를 복사한 작업 디렉터리나 cache 수정이 필요하지 않습니다. `--verify`는 모든 산출물을 다시 계산하여 저장된 bytes와 비교하고 쓰지 않습니다.

## 총괄 적용 및 완료 조건

총괄만 지정 checkout/HEAD/소유권·Editor 상태·설정 보호를 확인한 뒤 기존 Pipeline `package_add`로 아래 identifier를 적용합니다. [공식 Client.Add](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PackageManager.Client.Add.html)의 local tarball 입력을 사용하며 manifest/lock은 수동 편집하지 않습니다.

```text
file:../tools/revival/package-overrides/core-17.3.0/com.unity.render-pipelines.core-17.3.0-tamer-gles3-fsr.tgz
```

`confirm=true`와 비동기 `package_status` 완료, `recompile_status` 완료를 확인해야 합니다. Packages 기준 상대 경로를 유지하고 UPM result의 source/version/resolvedPath, 생성 manifest/lock, 실제 FSR와 meta hash를 대조합니다. builtin/간접 Core에 Client.Embed를 시험 호출하거나 직접 의존성 요건을 우회하지 않습니다. 예상 밖 패키지 버전 변경/중복 assembly/추가 source 변경이면 검증을 중단합니다.

원래 FinalPost/GLES3/Subshader0/Vertex `_DITHERING _FILM_GRAIN _POINT_SAMPLING` 경로가 새 소스를 처리하고 FP32 분기를 선택한 증거가 필요합니다. cache hit·CLI exit0·APK 생성만으로 셰이더 해결을 선언하지 않습니다. GLES3의 FSR 연산 비용/정밀도 변화는 남은 확인 항목이며 다른 API 분기는 소스상 그대로입니다. Unity 버전/API/variant/Library를 변경·삭제하여 통과시키지 않습니다.

현재 같은 셰이더 오류 누적은 1회입니다. 실제 다음 재실패는 누적 2회 STOP이며 3차 실행과 의존 빌드를 중단합니다. 기존 clang AAB/strict RELRO STOP과 운영 계정·저장·구매·광고·스토어 경로는 재개하지 않습니다. 패키징 roundtrip의 최초 1회 실패(긴 경로 PAX path를 과도하게 거부)는 안전 검사 수정 후 성공했으며 셰이더 실패와 별도입니다.

upstream의 해당 오류 수정이 공식적으로 확인되고 같은 요구 범위 검증이 준비되면 이 override 제거를 별도 검토합니다. cache나 Library를 삭제하여 제거를 대신하지 않습니다.
