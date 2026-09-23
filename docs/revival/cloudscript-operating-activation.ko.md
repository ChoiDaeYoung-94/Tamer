# 운영 Classic CloudScript 삭제 연결 — 비파괴 검증

2026-09-23 사용자가 승인한 운영 backend 연결을 수행했다. 기존 Classic CloudScript 원문과 handler를 보존한 병합 후보를 단계적으로 게시하고 필요한 API 설정을 적용했다. 변경 전후 서버 화면에서 배포 상태와 설정 저장 결과를 다시 읽어 확인했다. 정확한 운영 설정값·revision 이력·원문 해시·후보 파일·원시 증거는 Git 외부 비공개 기록에만 보존한다.

저장소의 `server/cloudscript/account-deletion.js`는 재사용을 위한 기본 비활성 템플릿이다. 운영에는 기존 handler를 보존하고 삭제 handler 두 개를 추가한 병합본을 사용했다. 시험 계정에만 적용했던 동적 gate는 운영 병합본에 넣지 않았다. 이 작업은 별도 삭제 서버나 운영 계정 데이터 변경을 포함하지 않는다.

서버의 배포·설정 재조회는 기능 구성의 확인이다. `getCurrentPlayerDeletionConfigV1`은 인증된 `currentPlayerId`가 필요하므로 운영 계정 자동 로그인이나 Client 함수 실행을 하지 않았다. 운영 계정 생성·삭제 요청, `Server/DeletePlayer` 직접 호출, 삭제 완료 관찰도 수행하지 않았다. 시험 타이틀의 폐기 계정 한 건 성공을 운영 결과로 대신하지 않는다. [PlayFab Server/DeletePlayer](https://learn.microsoft.com/en-us/rest/api/playfab/server/account-management/delete-player?view=playfab-rest)는 타이틀 삭제를 비동기로 접수하며 PlayStream 이벤트·publisher/master 계정 연결까지 지우지 않는다.

중단 시에는 타이틀 전체에 적용되는 삭제 API 허용을 먼저 끄고 서버값을 재조회한 뒤 이전 Live 스크립트를 복원·재조회한다. 이미 접수됐거나 진행 중인 삭제는 되돌릴 수 없다. Live 복원만으로 과거 Specific revision의 호출 가능성이 없어지지 않으므로, 옵션을 다시 켜기 전에는 해당 revision을 재검토한다. 설정 반영 지연과 진행 중 요청의 즉시 중단도 보장하지 않는다. [SetPublishedRevision](https://learn.microsoft.com/en-us/rest/api/playfab/admin/server-side-cloud-script/set-published-revision?view=playfab-rest)은 Live 지정을 바꾼다.

문서 변경 checkout은 `C:/Users/pc_17/.codex/worktrees/cloudscript-operating-record/Tamer`, 기준 `origin/main` `958ecc91d9f35ca1b24ba69e937dc0bf20ca2c59`이다. Unity Editor·Android SDK·APK/AAB를 실행하지 않았다. 프로젝트 기준 Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android min24/target36을 바꾸지 않았고 새 테스트 결과·APK SHA-256은 해당 없다. 운영 인증 Client preflight·삭제 접수/완료, 실기기·최종 AAB·스토어 검증은 미검증이다.
