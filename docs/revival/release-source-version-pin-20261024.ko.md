# 출시 소스의 Unity 버전·revision 정확 대조 — 2026-10-24

기존 `unityPinned`는 `m_EditorVersion: 6000.3.25f1` 부분 문자열만 찾았습니다. 합성 fixture에서 다른 버전 `6000.3.25f10`, 주석 안의 기준 문자열과 실제 `6000.3.26f1`, 잘못된 revision이 모두 전체 소스 검사 통과로 반환되는 것을 재현했습니다. 실제 프로젝트 버전이나 설치 Editor를 바꿔 재현하지 않았습니다.

현재 승인 기준 `6000.3.25f1` / `e1dba0a9aba4`와 다음 세 가지를 정확히 대조하도록 보완했습니다.

- ProjectVersion의 실제 `m_EditorVersion` 필드가 정확히1개이고 기준 버전과 같음
- 실제 `m_EditorVersionWithRevision` 필드가 정확히1개이고 버전·revision 모두 같음
- `tools/revival/toolchain.json`의 버전·revision이 같은 기준과 일치함

주석/접미 버전·중복 필드·누락 파일·다른 revision·모호한 JSON은 거절합니다. 기존 BOM/CRLF 입력은 허용하며 검사기가 입력을 수정하지 않습니다. toolchain 파일을 보고서 충돌 방지 대상에도 포함했습니다. 12항목의 분모나 다른 Android/서명/서비스 정책을 변경하지 않았습니다.

관련 Python 검사 **18/18 PASS(0.638초)**와 합성 before/after를 확인했습니다. 기존에 오판한3개는 거절하고 정확한 메타데이터는 통과했습니다. 실제 현재 작업본도 소스12/12 PASS, `releaseReady=false`입니다. 실행 기록/보고서는 서로 다른 새 파일에 보존했고 Unity·SDK·기기·키·운영 AAB 실행은 없었습니다.

이 검사는 소스 메타데이터의 정확한 일치만 확인합니다. 실제 Editor 실행 파일 무결성·설치 모듈·런타임·서명·최종 AAB·Play 판정을 대신하지 않으며, 다음 승인된 Unity 전환 때는 이 기준도 명시적으로 갱신해야 합니다. Chrome 보류와 기존 복구/서명 자료는 그대로 유지합니다.
