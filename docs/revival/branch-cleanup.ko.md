# 병합 브랜치 정리 기록

2026-09-11 사용자 요청에 따라 프로젝트 규칙 `AGENTS.md`에 PR 병합 후 원격·로컬 브랜치 정리를 추가했다. GitHub `delete_branch_on_merge`는 false에서 true로 변경하고 API 재조회로 확인했다.

정리 전 원격은 main과 작업 브랜치 34개였다. 각 작업 브랜치의 tip이 최신 main `3e4e67950a0093ad86f563ff7af680791f31f01c`에 포함되고, 관련 PR이 merged이며 PR head SHA와 현재 원격 tip이 같고 열린 PR이 없는지 확인했다. 33개는 expected SHA lease와 atomic push로, baseline 1개는 소유자 종료 확인 뒤 같은 검사를 거쳐 삭제했다. 이후 fetch --prune을 완료해 원격에 main만 남았다. 이 기록 PR의 브랜치는 병합 후 자동 삭제를 재조회하고, 남아 있으면 같은 기준으로 삭제한다.

삭제한 기존 원격 브랜치의 PR: #93, #95, #100–104, #106–112, #114–116, #119–135. 총 34개이며 PR의 커밋과 병합 이력은 main에 보존된다.

기존 로컬 브랜치 35개 중 미사용·병합된 29개를 `git branch -d`로 삭제했다. 각 tip의 main 포함 및 관련 merged PR을 확인했고 강제 삭제는 사용하지 않았다. 정리 PR을 위한 새 로컬 브랜치 1개는 병합 후 이 checkout을 main으로 전환한 다음 `-d`로 정리한다.

아래 기존 로컬 브랜치 6개는 보존한다.

| 브랜치 | 유지 이유 |
| --- | --- |
| main | 기본 브랜치 |
| codex/revival-baseline-build-92 | 다른 worktree의 체크아웃 브랜치 |
| codex/security-signing-backup-94 | 다른 worktree의 체크아웃 브랜치 |
| codex/deletion-loopback-http | 다른 worktree의 체크아웃 브랜치 |
| codex/receipt-server-staging | 다른 worktree의 체크아웃 브랜치 |
| codex/privacy-deletion-ui | 다른 worktree의 체크아웃 브랜치 |

해당 worktree 소유자들은 추가 작업·미푸시 커밋·계속 사용할 계획이 없음을 확인해 원격 참조만 삭제했다. 다른 worktree의 체크아웃·파일·Library·비공개 산출물은 유지했다. 별도 원본 `D:/meee/git/Tamer`는 변경하지 않았다. 이번 변경은 Git 관리와 문서뿐이므로 Unity 테스트를 재실행하지 않았다.
