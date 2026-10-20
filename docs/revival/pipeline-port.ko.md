# 여러 Unity Editor가 실행될 때 Tamer Pipeline 포트

Tamer의 Editor Pipeline 설정은 `Assets/Settings/TamerEditorPipeline.asset`에서
포트 **7849**를 사용합니다. `com.unity.pipeline` **0.6.0-exp.1**의
`EditorPipelineManager` 설정 형식이며 Unity Editor API로 생성했습니다.

## 변경 이유

2026-10-20에 Tamer가 wildcard 주소의 기본 포트를 사용하던 중 다른 Editor가
같은 포트의 loopback 주소를 점유했습니다. Tamer의 descriptor에 맞춰 호출해도
요청이 다른 listener에 도달해 HTTP 401이 발생했습니다. 이를 인증 해제나
권한 변경으로 해결하지 않고 Tamer 설정의 포트만 분리했습니다.

이 설정은 다른 프로젝트의 Editor, 포트 또는 인증 설정을 변경하지 않습니다.
브라우저 클라이언트 허용, 원시 eval 소스 저장, 요청·응답 로깅은 꺼져 있습니다.
포트만으로 소유권을 판단해서는 안 되며 다른 프로젝트가 7849를 사용할 가능성도
있으므로 기존 [소유권 사전 점검](pipeline-connection-preflight.ko.md)을 유지합니다.

## 확인한 범위

- Unity `6000.3.25f1` / revision `e1dba0a9aba4`, CLI `1.0.0-beta.8`.
- 통합 후보 `d401668416d01ab1994fb67d5b953d846fc52209`에서 설정 생성 후
  정확한 프로젝트를 지정한 `editor_status`가 ready / Play stopped를 반환했습니다.
- 해당 Editor를 정상 종료하고 같은 후보를 batchmode로 다시 열어 설정 지속성을
  확인했습니다. 실제 설정 경로, 지정 포트와 listener 포트가 모두 7849였으며
  브라우저 클라이언트 허용·원시 소스 저장·요청 로깅은 모두 false였습니다.
- Play mode, 계정·Store API, 구매 복원은 실행하지 않았습니다. 종료 후 자동
  serialization 변경 3개를 검토한 보존본으로 복구하고 보호 대상 51개 원본의
  바이트 일치를 확인했습니다. 설정과 `.meta`는 보존했습니다.
- 에셋과 PackageCache가 복원된 통합 작업본의 GUID 검사: 133개 YAML,
  미해결 GUID 0. 설정 GUID의 중복 없음과 고정 Pipeline 스크립트 참조도
  확인했습니다. 비공개 에셋과 PackageCache가 없는 문서용 작업본에서는
  미해결 155개가 보고됐으며, 이를 복원 완료 환경의 결과와 혼동하지 않습니다.
- 같은 시각의 오프라인 사전 점검은 다른 Unity 프로세스에 프로젝트 인자가 없어
  `process_project_argument_invalid`로 거부했습니다. 이를 통과로 표시하거나
  검사 조건을 완화하지 않았습니다. 포트 연결 성공과 호스트 전체 소유권 점검은
  별개의 결과입니다.

이 변경은 Editor 연결 설정입니다. 이미 제작한 시험용 AAB 30의 소스와 SHA는
변경되지 않으며 앱 기능·기기·스토어 검증을 대신하지 않습니다.
