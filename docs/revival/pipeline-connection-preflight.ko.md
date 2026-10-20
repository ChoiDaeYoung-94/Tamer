# Pipeline 연결 전 소유권 점검

10월18일 Tamer의 `editor_status`가 두 번 timeout됐습니다. Tamer의 wildcard listener와 다른 Editor의 loopback listener가 같은7800포트를 사용한 관측은 있지만, 충돌이 timeout의 유일한 원인이라고 확정하지 않았습니다. 기존 연결 시험의 중단과 사용자 재개 승인 조건을 유지합니다.

`tools/revival/pipeline_connection_preflight.py`는 **Editor에 접속하지 않는 Windows 로컬 사전 점검**입니다. 새 listener·HTTP 요청·Unity CLI·Editor 실행·프로세스 종료·설정 변경을 하지 않습니다. 읽을 대상은 명시한 프로젝트의 descriptor, ProjectVersion, CLI 후보의 toolchain 잠금·실행 파일 해시와 Windows 프로세스·TCP Listen 메타데이터입니다.

```powershell
& 'C:/path/to/python.exe' tools/revival/pipeline_connection_preflight.py --project-path 'D:/codex/worktrees/tamer-working/Tamer'
```

Python은 현재 PC의 실제 절대 경로를 선택합니다. 사용자·다른 프로젝트의 기본 계정이나 Editor는 바꾸지 않습니다.

## 확인하는 경계

- 프로젝트 절대 경로와 marker, descriptor의 프로젝트·PID·Editor용 포트7800~7849·모드·시각·토큰 존재 여부를 확인합니다. descriptor 안의 토큰 값은 결과에 포함하지 않습니다.
- ProjectVersion의 버전/revision, descriptor의 Unity 버전, 실제 Unity.exe의 ProductVersion을 대조합니다. 다른 버전이나 revision이면 진행 가능으로 표시하지 않습니다.
- Windows 인용 규칙으로 `-projectPath`를 파싱해 부분 경로 일치를 피합니다. import worker는 대상 Editor에서 제외합니다.
- Windows에서는 CLI도 `unity.exe`로 표시됩니다. 이 프로젝트의 실제 루트 안에
  있는 `tools/.local/unity-cli/<고정 버전>/unity.exe`만 toolchain의 경로·버전·SHA-256을
  잠금 파일 읽기로 대조한 뒤 `pinned-cli`로 구분합니다. 이름·ProductVersion·명령줄
  문구만으로 제외하지 않으며, pin 변조·불명 실행 파일·명령줄 누락은 계속 거부합니다.
  CLI를 descriptor의 Editor로 선택할 수 없고, 그 PID가 대상 포트까지 점유하면
  여전히 listener 충돌로 거부합니다.
- 프로세스 목록을 listener 관측 전후로 읽어 PID·시작 시각·프로젝트의 변화와 PID 재사용을 검사합니다. 대상 프로젝트 Editor가 하나이고 그 PID만 descriptor의 포트를 듣는 경우에만 소유권 일관성을 표시합니다.
- 같은 포트에 다른 PID가 하나라도 있거나 권한 거부·누락·불명 메타데이터가 있으면 중단합니다. 권한을 추가하거나 느슨한 대체 경로로 통과시키지 않습니다.
- 정규화한 프로젝트 루트의 호환 경로는 허용하지만 descriptor까지의 내부 reparse point는 Windows 파일 핸들 검사로 거부합니다. 최대64KiB만 읽고 잠금 동안 writer/rename을 차단하므로 실행 중 descriptor의 갱신과 경합하면 통과로 처리하지 않을 수 있습니다. 장시간 반복 실행하지 않습니다.

## 결과와 한계

종료0과 `safeToAttemptConnection=true`는 **그 시점의 오프라인 소유권 메타데이터가 일치한다**는 뜻입니다. 실제 연결·명령 처리·모달·컴파일 상태·업데이트 루프의 정상 여부는 검사하지 않습니다. `controlConnectionVerified`는 항상false입니다. 기존 실패 횟수나 재개 승인 조건을 해제하지 않으며 결과만으로 CLI 명령을 자동 실행하지 않습니다.

종료2와 `safeToAttemptConnection=false`이면 `reason`의 비민감 고정 코드를 확인합니다. 대상 Editor가 닫혀 descriptor가 없는 경우도 연결 가능으로 표시하지 않습니다. 결과에는 토큰·전체 descriptor·프로세스 명령줄·다른 프로젝트의 경로·원시 오류를 출력하지 않습니다. 관리자/원시 디스크 접근에 대한 보안 경계나 사전 점검 이후까지의 원자적 실행 권한을 보장하는 도구는 아닙니다.

고정 Pipeline `0.6.0-exp.1`의 현재 구현은 소켓 수준에서 wildcard HTTP prefix를 사용하고 요청 단계에서 원격 주소의 loopback 및 bearer 인증을 검사합니다. 따라서 OS에서 `0.0.0.0`을 관측한 것만으로 원격 제어가 허용됐다고 판단하지 않습니다. 패키지에 동봉된 connectivity 설명의 물리적 loopback 바인딩 예와 실제 구현의 차이를 구분하며, 이 도구는 패키지·CLI·포트 설정을 수정하지 않습니다.

최소 합성 회귀:

```powershell
& 'C:/path/to/python.exe' -m unittest discover -s tools/revival -p test_pipeline_connection_preflight.py -v
```

테스트는 임시 프로젝트·가짜 프로세스/listener 메타데이터와 Windows 인용·읽기 핸들을 사용합니다. 실제 Editor·서버·Store·계정 API를 실행하지 않습니다. 합성 통과와 실제 연결 성공을 구분합니다. 사용 절차와 공통 실행 기준은 [기준 빌드 문서](baseline.ko.md)를 따릅니다.

## 2026-10-20 CLI와 Editor 식별 보정

당일 `process_project_argument_invalid`를 일으킨 프로세스는 프로젝트의 고정 CLI였고
ProductVersion은 `1.0.0`이었습니다. 다른 Editor를 종료하거나 인자 누락을 전반적으로
허용하지 않고 위 해시 확인만 추가했습니다. 관련 합성 22개가 통과했습니다.
실제 고정 CLI 프로세스를 구분한 뒤, 후보 `5d30afb2554acbac578656717b637b0e242dbb19`를
새 batch Editor로 열었을 때 오프라인 점검은 단일 소유 PID와 7849 포트의 일치를
확인했습니다. 이 점검 자체의 `controlConnectionVerified`는 여전히 false입니다.
이어진 실제 CLI `editor_status`는 정확한 Tamer 경로의 ready / Play stopped를
반환했습니다. 해당 Editor만 정상 종료한 뒤 자동 변경3개를 복구했고 보호
원본51개와 포트 설정2개가 일치했습니다. 실제 제어 확인은 오프라인 점검과
별도 결과이며 앱·계정·Store 검증을 대신하지 않습니다.
