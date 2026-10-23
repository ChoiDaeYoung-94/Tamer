# GLES3 FinalPost 셰이더 컴파일 보완

2026-10-17 Unity 6000.3.25f1(e1dba0a9aba4)/URP17.3.0의 Android GLES3 FinalPost 빌드 오류를 기존 FP32 FSR 경로로 보완했습니다. 공식 Unity 수정판이 아닌 프로젝트의 로컬 패치이며 실제 기기 표시·비용 검증은 별도입니다.

## 변경과 재현

Core 전체 파일을 로컬 UPM tarball로 보존하고 FSRCommon.hlsl의 half 사용 조건에 `!defined(SHADER_API_GLES3)`를 추가했습니다. 원본 생성 `_fingerprint`만 제거한 metadata 변경도 명시합니다. Unity와 패키지 내부 버전17.3.0, 그래픽 API·variant·FSR 기능·C#649개·.meta1,521개·라이선스·샘플을 보존합니다. UPM manifest/lock은 Package Manager Client.Add API가 생성했습니다.

[패치·원본/수정본 해시·재현 절차](../../tools/revival/package-overrides/core-17.3.0/README.ko.md)를 함께 보존합니다. 커밋된 tarball은21,619,622bytes/SHA-256 `2360687d2de0f5c43e671868981851aa106a654cdd20f65128b0f6f34c0ec3cc`입니다. 해당 디렉터리는 Git 줄바꿈 변환 없이 파일 바이트를 유지합니다. cache 직접 수정·Library 삭제·API/variant 제거를 해결책으로 사용하지 않았습니다.

## 실제 실행 근거

- checkout: `D:/codex/worktrees/tamer-working/Tamer`, `codex/urp-gles-fsr-1017`.
- 검증 소스: `4dac486e226386045c5ccfbeb22c8a783b8c8b55`. 이 문서의 후속 커밋은 문서 변경이며 같은 소스 검증을 다시 실행했다고 표현하지 않습니다.
- 도구: Unity6000.3.25f1, CLI1.0.0-beta.8, Android min25/target36/ARM64, NDK27.2.12479018/r27c, JDK17.0.18+8, Gradle9.3.1/AGP9.0.0.
- UPM 완료/로드된 패키지17.3.0 LocalTarball 및 수정 헤더 SHA-256 `a573e375fd8717208ad3c762449f137ac950fe89a21fecbb3aad7f276beea199` 확인. 설치본의 package.json에는 UPM이 새 `_fingerprint`를 생성하므로 tarball 자체 hash와 구분합니다. 나머지2,996개 파일은 수정본 목록과 바이트가 일치했습니다.
- Editor의 재컴파일 확인은 `up_to_date`, 열려 있던 격리 Smoke scene은 변경 없음으로 확인하고 해당 Editor만 정상 종료했습니다. 소유 설정44파일의 원본/변경본과 복원 근거는 로컬에 보존했습니다.
- 개발 APK 빌드1회: 2026-10-17 15:04:44~15:06:32 KST, CLIexit0/UnitySucceeded/errors0. FinalPost GLES3 vertex pass는 cache hit0, **48variants 새 컴파일**, shader error0입니다. 단순 cache hit나 APK 생성만으로 오류 해소를 판정하지 않았습니다.
- APK: 별도 `com.AeDeong.MonsterTamer.revival`, debug signing, version1.0.5/code26, 실제154,117,746bytes/SHA-256 `77bc9a2ae5a576e9183c70f0be83d9f5614e6ef975b47fdb50d17ff0eded6c16`. 개발 격리 산출물이며 운영 배포/다운로드가 아닙니다.
- APK 정보·서명 검사 통과. GUID132YAML/미해결0. Native6개/모두압축, 기본LOAD/ZIP 검사통과.
- 보호한 설정 복원 후 Gitclean 확인. raw로그·기존 키·비공개 자료는 커밋하지 않습니다.

기존 통합 후보의139개 회귀는9877574의 실제 결과로 보존합니다. 이번 변경에서 게임 C#와 Core C#는 그대로여서 동일 회귀를 반복하지 않았으며,139개를4dac486에서 새로 실행했다고 표시하지 않습니다.

## 남은 범위와 제한

- 실제 GLES3 기기의 표시/postprocessing·FP32 성능/정밀도 영향, 다른 API의 실제 실행은 미검증입니다. 기존 FSR 구현을 사용한다는 사실을 기기 결과로 대체하지 않습니다.
- 별도 RELRO endalignment2개는 미충족입니다. 기본 검사 통과와 구별하며 기존 strict RELRO 중단 시험을 재개하지 않았습니다. 실제16KB 기기/Play 전달 split/최종 출시AAB/스토어 수락은 미검증입니다.
- APK 컨테이너 크기는 이전 개발APK보다 증가했으나 ZIP활성entry595개와 실제압축데이터 총합은 거의 같습니다. ZIP인덱스가 참조하지 않는 파일간 영역 증가를 로컬에서 확인했습니다. 셰이더/SDK 데이터가43MB 늘었다고 판단하지 않으며, 최종 배포용 fresh 산출물의 크기와 컨테이너는 별도 확인합니다.
- 호스트의 과거 WHEA/0x3B/clang 실패를 이번 빌드 하나로 해결 선언하지 않습니다. 운영 계정·구매·광고로 자동 확인하지 않았습니다.

최초 shader 오류1회와 패치 후 성공을 구분하고 실패 기록을 지우지 않습니다. 당시 사용한 누적2회 중단 조건은 과거 운영 이력이며, 현재 재시도는 2026-10-23 개정 AGENTS의 원인 진단·변경 조건·실행 원장 기준을 따릅니다. 공식 해결 버전이 확인되면 실제 검증 후 이 로컬 패치의 제거를 검토합니다.
