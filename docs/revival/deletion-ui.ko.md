# 계정 삭제 UI와 합성 흐름 검증

2026-09-11, `codex/privacy-deletion-ui`. 실행 소스 `fff397e1eae26266c415212e4a80011c180d5cbf`, 서비스 담당 core `73f868a1af9b65b463d170b36d05859fe0d8d824`를 통합했다. core/server는 PR #132 소유이며 이 문서는 기존 설정 진입과 runtime UI를 다룬다.

## 화면과 동작

기존 `PopupManager.PopupSetting()`에서 `Account & privacy` 버튼과 개인정보 모달을 한 번만 만든다. 기존 설정의 BGM/SFX/OK, 프리팹 직렬화·GUID는 보존했다. 실제 설정 프리팹의 TMP 폰트를 재사용하고 영어 UI 패턴을 따른다. 기존 팝업 스택을 사용하므로 개인정보 화면을 닫으면 설정으로 돌아가며, 자체적으로 게임 시간을 재개하거나 다른 팝업을 닫지 않는다.

운영 기본값은 `UnavailableDeletionGateway`다. 삭제를 요청할 수 없다는 설명과 비활성 요청 버튼, 정상 동작하는 설정 복귀 버튼을 보여 준다. 메뉴를 열어도 로그인·네트워크·계정·로컬 저장·No Ads 변경을 수행하지 않는다. 삭제 정책, 처리 기간, 지원 URL/이메일 또는 운영 endpoint를 새로 정하지 않았다.

`ConfigureSynthetic`은 Editor/Development에서 `IsSynthetic`인 흐름만 받는다. 합성 화면에는 항상 TEST ONLY와 실제 계정 변경이 없다는 설명을 표시한다. 요청 버튼에서 core의 명시적 재인증을 거쳐 확인 화면으로 진행하며, 별도 확인 버튼 이후 접수·처리 중·실패 상태를 구분한다. 접수나 처리 중은 삭제 완료로 표현하지 않는다. 완료 상태와 `CompletionEvidence`가 모두 있는 합성 결과만 테스트 완료로 표시한다. 운영 완료 화면은 이번 구현에서 활성화하지 않는다.

확인·취소·조회·재인증 실패의 재시도는 직전 동작을 보존한다. 동작 중 추가 요청은 차단하며, 화면 비활성화 시 core를 dispose하고 구독과 재시도 delegate를 해제한다. 늦은 응답은 새 화면에 전달되지 않는다. 화면 닫기는 서버 요청 취소를 의미하지 않는다. 현 UI는 화면을 다시 열면 지원 불가 기본 상태로 돌아가며 운영 요청 복구를 구현했다고 주장하지 않는다.

## 검증 근거

Unity **6000.0.81f1**, CLI **1.0.0-beta.8**, `Run-Baseline.ps1 -TestsOnly` 최종 **355/355 PASS**. UTC 08:25:18–08:25:22, 실패/스킵0. 상세 소스와 XML·이미지 SHA-256은 [검증 JSON](deletion-ui-validation.json)에 기록했다.

- UI 8개: 지원 불가, 재인증 대기/중복 클릭, 확인→접수→처리→증거 있는 합성 완료, 확인·취소 실패 재시도, 닫은 화면의 늦은 응답, 완료 증거 누락, 진입점 멱등성, 렌더/화면 경계.
- core 12개: 서비스 담당 회귀를 함께 실행했다. 실제 서버/계정 요청을 실행한 결과가 아니다.
- `tools/revival/RenderDeletionUI.cs`를 Unity pipeline `run_script`로 실행해 기존 설정 UI만 preview scene에 복제했다. 기존 프리팹 설정/지원 불가/합성 확인 화면을 1080×1920으로 렌더해 직접 확인했다. 최초 안내의 줄바꿈 누락을 수정했다. 기존 프리팹이나 씬을 저장하지 않으며 생성한 preview scene과 render 자원을 종료 시 정리한다.
- 최초 검은 카메라 이미지는 시각 근거로 채택하지 않았다. preview scene 카메라 지정과 Canvas 밖 카메라 배치 후 실제 텍스트 픽셀 검사를 통과했다. live 전체 검사의 기존 baseline 환경 실패와 batch 전용 스킵은 최종 clean batch 성공과 구분해 JSON에 남겼다.
- 자체 Editor 종료 후 임시 font cache, 광고 설정, 서명 alias, scene/template/timestep 직렬화 변경을 원복했다. 최종 제품 소스 외 설정 변경은 없다. 기기/APK 작업은 하지 않았다.

원시 로그와 기존 비공개 폰트가 포함된 렌더 이미지는 로컬 `.revival-local/deletion-ui`에 보관한다. 공개 문서에는 해시만 남긴다. 실제 재인증/삭제 운영 연결, 게시 정책·지원 경로, 기기 터치, 실제 서버 완료는 미검증이다.
