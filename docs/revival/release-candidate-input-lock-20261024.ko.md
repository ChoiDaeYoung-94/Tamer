# 출시 AAB 검사 입력의 동일성 고정 — 2026-10-24

기존 `candidate_checks()`는 같은 경로의 AAB를 bundle 검증·manifest·서명·native·마지막 SHA 단계에서 다시 열었습니다. 그 사이 파일이 바뀌면 서로 다른 파일의 결과가 성공 보고서에 섞일 수 있었습니다. 실제 키/서명 명령 없이 합성 ZIP과 외부 명령 mock으로 재현했습니다. 기존 코드에서는 original-A를 검사했다고 주장하는 manifest/서명 결과와 replacement-B의 native/SHA가 `artifactChecksPassed=true`로 함께 반환됐습니다.

수정된 AAB 검사는 AAB·bundletool JAR·Java 실행 파일·서명 검사 Java 소스를 검사 시작부터 반환까지 Windows 읽기 핸들에 고정합니다. 부모 경로의 rename/delete도 막고, 처음 읽은 실제 bundletool 바이트를 기존 공식 pin과 대조합니다. native 검사와 SHA는 열린 같은 AAB stream에서 읽고, 끝에서 경로/핸들의 파일 객체와 각 입력의 SHA가 시작 값과 같은지 다시 확인합니다. 성공 결과의 artifact SHA는 처음 고정한 AAB의 값입니다.

입력에 이미 쓰기 핸들이 열려 있거나 검사 명령이 실패하면 결과 보고서를 만들지 않습니다. 기존 보고서/입력 충돌 검사와 exclusive 출력 생성은 유지합니다. manifest·서명·ARM64·LOAD/ZIP·보수적 RELRO layout의 검사 조건과 `releaseReady=false`·실기기/스토어 미검증 표시는 변경하지 않았습니다.

## 최소 검증

- 관련 Python 검사 **17/17 PASS**, 0.599초. 기존 native/RELRO 판단 회귀와 신규 Windows 입력 보호3개를 포함하며 skipped0입니다.
- 합성 AAB를 각 외부 명령/검사 후 단계에서 write/rename/replace하거나 부모를 바꾸려는 시도가 거절됐고 원본/대체 파일은 그대로였습니다. 세 도구 파일에도 write/delete/rename/replace/부모 rename을 시도해 같은 보존을 확인했습니다. 공격 대상과 목적지는 모두 fixture 임시 폴더 안에만 둡니다.
- 기존 writer/명령 예외는 보고서 부재·입력 원바이트 보존·핸들 해제를 확인했습니다. 전체 digest를 mock하여 pin 보호를 우회하지 않고 실제 합성 JAR의 pin을 사용합니다.
- 실제 설치된 JDK의 Java 자식 프로세스가 Java 실행 파일·합성 source·합성 AAB/JAR를 읽기 잠금으로 고정한 동안 source-file mode로 실행되어 ZIP과 실제 SHA를 읽는 별도 확인도 통과했습니다. 키/실제 서명/운영 AAB는 사용하지 않았습니다.
- 독립 정적 검토에서 추가 확정 P1/P2는 없었습니다. source·도구 검사만 변경하여 Unity/SDK/기기 회귀를 반복하지 않았습니다.

## 범위와 한계

AAB 모드는 기존 Play 제공물 검사와 같은 **Windows 핸들 계약**을 사용합니다. 입력 경로의 reparse point는 거절하므로 검증한 실제 경로를 명시합니다. 기존 호환 연결을 삭제하거나 일괄 치환하지 않습니다. source/ELF 독립 검사는 이 변경으로 운영 출시 결과가 되지 않습니다.

네 파일의 동일성을 고정한 것이며 전체 JDK/모든 종속 파일의 무결성 인증이나 관리자에 대한 ACL 보장은 아닙니다. 실제 운영 AAB·업로드 인증서·모든 트랙 최대 code·Play 제공물·16KB runtime은 여전히 별도 검증입니다. Chrome 보류와 서명/서비스 비밀 자료 보호도 유지했습니다.
