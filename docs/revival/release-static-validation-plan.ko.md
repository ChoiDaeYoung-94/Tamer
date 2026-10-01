# 최종 AAB 정적 검증 준비와 실행 범위

읽기 기준은 main `d0d7dc2d490f3654a4b0add747ec9f41ffedecfd`이며,
이 문서는 새 빌드·바이너리 검사·실기기 실행 결과가 아니다.

## 기존 증거와 남은 native 항목

| 산출물 | 실제 확인 범위 | 미완료 범위 |
| --- | --- | --- |
| LTS 개발 APK `9f7cd0be899f436daea7d045e59e3950756d25a3a8936ca3ebe02c0cf516b068` | 기본 LOAD/ZIP 통과. native 6개 모두 압축 저장으로 비압축 mmap ZIP offset 대상은 없음. 별도 strict RELRO는 4개 끝 정렬 실패 | 전체 strict 통과·최종 출시 AAB의 결과가 아님 |
| 링크 보정 후 개발 APK `f3efa94bbb12b930231afcb07bf473abcf9687d8dba09ff213e54dbc871d4533`, source `4ba1422850c0bb2bb29182e177d160c5ed5ac0ac` | 새 생성 `libil2cpp.so`·`libswappywrapper.so`의 LOAD/RELRO 통과 및 실제 링크 인자 확인 | 이 APK의 prebuilt2·전체 strict·ZIP·서명 검사는 해당 관측 범위 밖. 최신 main의 최종 AAB 결과로 합산하지 않음 |
| 과거 IAP 테스트 AAB `aef2ec2212b09415294d6b88d5de21c78e7730bf59ecabbc23514a36354aa37a` | 당시 고정 Unity의 LOAD 통과·RELRO 끝 3개 실패 이력 | 전달 split ZIP·최신 Unity/소스의 최종 출시 증거가 아님 |

남은 prebuilt2는 IAP 패키지의 전용 바이너리가 아니라 Unity 공급
`libmain.so`와 고정 NDK 공급 `libc++_shared.so`다.
기존 읽기에서 Unity Development/Release/Release_ThinLTO `libmain`의
RELRO 끝 나머지가 모두 8192였으므로 Release 선택만으로 해결됐다고 할 수 없다.
NDK STL도 프로젝트 IL2CPP/CMake 링크 옵션으로 자체 재링크되지 않는다.
원본 STL과 최종 APK 사이 strip/cache 차이를 정확한 전체 파일 동일성으로 표현하지 않는다.
공급자 바이너리·지원 NDK 호환성 평가가 필요하며, 현재 자료에서 프로젝트 코드만으로
안전하게 해결하는 추가 변경은 확인하지 못했다. ELF 헤더 변경·RELRO 조건 완화·임의 STL 교체는 하지 않는다.
세부 source별 기록은 [RELRO 분석](iap-relro-analysis.ko.md)을 따른다.

## 최종 서명 AAB 생성 전 조건

기존 키의 실제 사용 가능성, Git 밖 keystore 절대 경로·alias·비공개 암호와
Play Console의 현재 활성 업로드 인증서 대조가 먼저 필요하다.
외부 백업 다운로드 무결성은 키 암호 검증·복호화 복구를 대신하지 않는다.
모든 트랙에서 최대 사용 versionCode를 다시 확인하고 그보다 큰 후보를 검토한다.
[서명 입력 준비](signing-and-private-backup.ko.md#aab-빌드-입력-준비)의 변수·검사를 유지하며
실제 값은 명령행·공개 로그에 쓰지 않는다.

빌드 담당은 의도한 절대 checkout·브랜치·HEAD·clean 상태·검증 커밋을 기록하고
운영 앱 ID, Login 첫 씬, 전역 하네스 심볼 부재, min25/target36/ARM64/IL2CPP,
고정 Unity/NDK/CLI와 production BuildScript의 RELRO scope를 대조한다.
Editor 시작 전 설정·에셋·meta 보호 snapshot 및 신규 산출물 경로를 준비하며,
실행 후 변경을 사전 바이트와 대조해 복원하고 원증거·Library를 보존한다.
현재 실행 환경 보류를 최종 native 검증 통과로 바꾸지 않는다.

## 이후 도구 실행 계획

1. `verify_release_candidate.py --output <새 비공개 JSON> source`로 위 checkout의
   정적 source 조건을 확인한다. 이 결과는 빌드·정책·실기기 승인 증거가 아니다.
2. 별도 승인된 빌드 담당이 검토한 입력으로 최종 AAB를 한 번 만들고 SHA-256과
   실제 source·도구 버전을 기록한다. 이 문서와 도구 패치에서는 빌드를 실행하지 않는다.
3. `verify_release_candidate.py --output <다른 새 비공개 JSON> aab --aab <후보 절대 경로>
   --version-code <검토한 번호> --published-max-code <실제 최대 번호>
   --upload-cert-sha256 <활성 업로드 인증서 SHA-256>`를 실행한다.
   고정 bundletool validate, base manifest, 전체 payload 서명·인증서와 모든 bundled
   ARM64 ELF의 LOAD·존재하는 RELRO 끝을 검사한다. 기존 보고서는 덮어쓰지 않는다.
4. AAB의 ZIP offset은 전달 APK의 mmap 정렬이 아니다. 정확한 후보·기기 spec으로 생성한
   전달 split마다 서명, `zipalign -c -P 16 4`, `verify_native_alignment.py`의
   LOAD/ZIP 및 별도 `--strict-relro` 결과를 기록한다. 현재 `verify_bundle.py`는
   별도 debug 앱 ID·code26·debug 키 전용이므로 운영 AAB에 그대로 사용하지 않는다.
   최종 전달 split용 도구/서명 계획을 독립 검토한 뒤 실행하고 파일은 비공개 보존한다.
5. 실제 ARM64 16KB 실행과 스토어 검증은 별도 미검증으로 남긴다.
   번역 AVD의 첫 readiness timeout 및 기존 실패 이력을 보존하며 추가 부팅·재시도하지 않는다.

`artifactChecksPassed`는 AAB 정적 검사 범위만 의미한다.
보고서의 `deliveredApkZipAlignmentVerified`, `arm64NativeRuntimeVerified`,
`productionBinary16KBVerified`, `releaseReady`는 이 도구에서 false를 유지한다.
prebuilt RELRO 미해결과 16KB 실행 환경 보류는 서로 다른 항목이다.
이번 변경의 검증은 보고서 보존·검사 범위 표시의 관련 합성 테스트만이며,
기존 실제 산출물 재검사·Editor·빌드·ADB·SDK 설치·서명·업로드는 0회다.
