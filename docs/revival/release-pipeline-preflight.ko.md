# 출시 소스의 Pipeline 런타임 설정 검사

`verify_release_candidate.py source`는 기존10항목에 개발용 Pipeline 런타임 설정과 생성 리소스2항목을 추가해 총12항목을 검사합니다. 이 검사 때문에 Editor·빌드·서버를 실행하거나 설정을 바꾸지는 않습니다.

고정 Pipeline0.6.0-exp.1은 authored `RuntimePipelineConfig.json`의 `enableInBuilds`가 true이면 Player에 서버 설정을 포함할 수 있습니다. 패키지의 비개발 빌드 경고는 batch mode에서 빌드를 거절하지 않습니다. 따라서 개발 하네스 전역 심볼 부재와 별도로 이 입력을 확인합니다. 현재 checkout은 해당 authored 설정과 생성 리소스가 없는 상태이며, 현재 운영 앱에서 서버가 노출됐다는 발견은 아닙니다.

| 검사 | 통과 조건 |
| --- | --- |
| `pipelineRuntimeDisabled` | `ProjectSettings/Packages/com.unity.pipeline/RuntimePipelineConfig.json`이 없거나, JSON 객체의 `enableInBuilds`가 없거나 정확한 boolean false. true·비boolean·중복 키·파싱 실패·잘못된 UTF-8·비표준 숫자는 실패 |
| `noStagedPipelineResources` | Assets의 모든 중첩 Resources 경로에 `RuntimePipelineConfig.asset`/`RuntimePipelineBuildInfo.asset`이 없음. `Assets/Settings/Pipeline/Resources`도 포함. 발견한 파일을 자동 삭제하지 않음 |

새 설정 경로와 공급자 생성 경로2개는 보고서 출력 충돌 보호에도 포함합니다. 아직 없는 설정 경로를 `--output`으로 지정해 검사 보고서가 설정 파일로 생성되는 일도 거절합니다. 원본·기존 보고서를 변경하거나 덮어쓰지 않습니다.

빌드 전 source 검사와 최종 `aab` 검사는 별도 단계입니다. `aab` 모드는 이 소스 검사를 다시 수행하지 않으며 `artifactChecksPassed`가 이번 항목을 포함하지도 않습니다. 같은 검토 소스·설정과 실제 최종 산출물을 연결해야 합니다. 소스12항목 통과로 최종 AAB의 HTTP 서버 부재나 모든 개발 코드 stripping·정책/스토어 승인을 입증하지 않으며 `releaseReady=false`를 유지합니다.

회귀는 임시 파일의 켜짐/꺼짐·모호한 입력·중첩 Resources·비Resources 동명 자산 보존·보고서 충돌과 기존 manifest/서명/ELF 계약을 확인합니다. 실제 SDK·Editor·기기 실행을 대신하지 않습니다.

```powershell
python -m unittest discover -s tools/revival -p test_release_candidate.py -v
```

이전 [10월23일 소스10항목 실행](release-readiness-20261023.ko.md)은 당시 도구의 근거입니다. 새12항목 실행과 혼합하지 않고 새 비공개 출력에 소스·도구 SHA와 결과를 기록합니다.
