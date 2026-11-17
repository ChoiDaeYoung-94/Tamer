# Unity 보안 수정판의 격리 검증 준비

스토어 복구 범위19번의 후속입니다. [Unity 공식 공지](https://activation.unity3d.com/security/unity-emudhra-compromise)는6000.3의 첫 수정판을6000.3.27f2/revision22188f149405로 안내합니다. 기존6000.3.25f1 설치와 공용 SDK/JDK 설정을 덮어쓰지 않고 새 구성을 먼저 검증합니다.

공식 API와 pinned CLI의 `install --dry-run`에서 Windows x64 Editor4,236,703,648B·Android 지원1,549,421,200B 및 하위 모듈을 대조했습니다. 내려받은 두 패키지는 제공 MD5와 크기가 일치합니다. 파일·서명·구성 읽기와 실제 설치/등록·Editor 실행·제품 검증은 구분해 기록합니다.

[Adoptium 공식 안내](https://adoptium.net/news/2026/10/temurin-october-2026-emudhra/)의 별도 Temurin17.0.21+9도 준비했습니다. 공식 Windows x64 ZIP191,117,380B/SHA-256 `98518ecf554465057ee5509462631deeee374bc368c12a411d225cd29381f84b`와 일치하며 실제 Java 버전·기본 신뢰 저장소를 읽기 전용으로 확인했습니다. Unity 수정판의 설치 metadata에 있는17.0.18+8과 구분합니다. 이 단계의 전역 설정/OS 신뢰 저장소 변경·운영 키 사용은0입니다.

이 브랜치는 ProjectVersion·toolchain·기준 빌드 guard와 실제 실행 도구/검증기의 기본 버전을27f2로 맞추는 준비 상태입니다. 소스 검증기의 관련 순수 회귀24개는 통과했습니다. 구형 패키지 override의 생성 provenance와 과거 실행 근거는 원래 버전으로 보존합니다.25f1에서 관측했던 `ENABLE_MARSHALLING_TESTS` 예외는 아직27f2에 허용하지 않았습니다. 새 실제 컴파일 결과가 필요합니다.

후속 읽기 검토에서 실제 C# 참조/JDK를 호출하는 검사17개가 이전25f1 경로를 가리키던 부분을27f2로 맞췄습니다. 새 버전 검사에서 옛 SDK를 사용해 성공처럼 보이지 않도록 하는 변경입니다. 실제 경로 상수만 바꿨고 합성 입력의 버전·과거 package provenance·25f1 전용 define 예외는 보존했습니다.17개 Python 파일의 구문과 diff를 확인했으며 새 Editor/컴파일러/JDK 검사는 설치 전이라 실행하지 않았습니다. 기존 순수 회귀24개를 이17개 실제 SDK 검사 성공으로 합산하지 않습니다.

새 Editor/JDK에서 필요한 통합 검사·Android 컴파일·최신 LD APK를 묶어 검증할 계획이며, 변경 없는 양쪽 기기의 전체 반복은 하지 않습니다. 새 엔진의 실제 성공 전에는 채택이나 운영 빌드 완료로 표시하지 않습니다. 운영 producer의 실제 경로/도구 pin·새 키 활성 확인·독립 검토·사용자 직접 암호 입력, 최종 정책 심사·프로덕션은 후속 단계입니다.
