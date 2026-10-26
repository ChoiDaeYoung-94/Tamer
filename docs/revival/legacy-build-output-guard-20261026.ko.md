# 기존 Android 빌드 출력 보호

2026-10-26 변경입니다. 구형 `Build/AOS/APK`, `Build/AOS/AAB` 메뉴의 출력 충돌 보호를 보완했습니다. 실제 운영 키를 사용하거나 APK/AAB를 생성한 검증은 아닙니다.

## 변경

기존 메뉴는 준비·완료 표시를 보호했지만, 실제 출력 경로의 기존 파일을 검사하지 않았습니다. `LegacyBuildOutputPlan`은 메뉴 준비 전에 예상 출력을 계산하고, domain reload 뒤 실제 실행 진입에서도 다시 계산·검사합니다. 두 초기 검사에서 이미 있는 출력은 버전·설정 쓰기 전에 거절합니다.

실행 계획은 시작 시각을 한 번 읽어 적용 버전과 출력 파일명을 함께 고정합니다. 주 단위 계산식, 같은 주 build 증가, AAB만 versionCode 증가, finish 표시에 따른 원래 버전 재사용은 유지합니다. 이전 코드는 target 전환 뒤 시각을 읽었으므로 전환 도중 주 경계가 지나는 드문 경우까지 시각 선택이 동일하다는 보장은 아닙니다.

계획 후 버전 입력이나 finish 존재 상태가 바뀌면 적용을 거절합니다. 실제 BuildPlayer 직전에도 같은 출력을 재검사합니다. 대상 파일·디렉터리, 부모 파일, 정션을 경유하는 출력은 거절합니다. 기존 SessionState 준비 토큰과 자기 표시·define 정리 계약은 유지합니다.

## 검증 근거

- checkout: `D:/codex/worktrees/tamer-working/Tamer`
- branch: `codex/legacy-build-output-guard-1026`
- 실제 실행 소스: `493d83910dc940dd58723f66a176f16138eee5a8`, 시작 시 clean. 이후 검증 기록만 추가했습니다.
- Unity `6000.3.25f1` / Android Editor 대상 / CLI `1.0.0-beta.8`.
- 기존 승인 importer ledger를 명시한 에셋 검사: 4,561개 확인, 복사 0개. 첫 옵션 누락 검사는 충돌을 보존하고 중단했으며 원본·manifest를 수정하지 않았습니다.
- 독립 순수 C# 합성 검사 20건 통과. 첫 참조 어셈블리 구성 오류로 fixture 실행 전 컴파일이 실패했고, 실제 설치 host의 참조를 명시해 성공했습니다. NUnit engine/Unity 실행과 구분합니다.
- 실제 Unity EditMode **12/12 통과**: 새 출력 계획 시험 8건과 기존 `RevivalUploadSigningTests` 4건. 신규 helper와 수정 BuildScript의 실제 컴파일을 포함합니다. 서명 시험은 일회용 합성 자료만 사용했습니다.
- Editor 실행 전후 보호 파일 59개 대조. 실행 중 URP importer 버전 2개, 시험용 Pipeline 시작 설정 1개, ProjectSettings의 직렬화·alias 초기화 변경을 확인하고 해당 4개를 원래 바이트로 복원했습니다. 전체 59개 일치, checkout clean, 해당 Editor 종료를 확인했습니다.
- 기존 운영 출력 덮어쓰기 재현, 실제 서명·BuildPlayer·휴대폰·구매·광고·스토어 시험은 실행하지 않았습니다. 이 변경 때문에 같은 기기 시험을 반복할 필요는 없습니다.

## 보호의 한계와 다음 단계

Editor와 출력 경로는 총괄이 직렬 소유해야 합니다. 검사 직후 외부 프로세스의 동시 생성을 원자적으로 잠그는 기능은 없습니다. 버전 적용 뒤 최종 검사에서 충돌하거나 I/O가 실패하면 기존 출력은 보존하되 이미 적용한 버전·finish·일반 설정 전체의 rollback을 보장하지 않습니다.

자동 버전의 최초 입력, 운영 versionCode 선택, 서명 입력·현재 Console 업로드 인증서 대조는 별도 준비입니다. 출력 보호 통과만으로 새 운영 AAB 또는 출시 준비가 완료되지는 않습니다. [전체 남은 복구 작업](remaining-recovery-work.ko.md)을 따릅니다.
