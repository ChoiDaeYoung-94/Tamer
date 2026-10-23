# 최신 통합 후보 Editor 검증 결산 — 2026-10-23

계정·저장·장비 구매·씬 수명 변경을 합친 최신 후보에서 단일 Editor 통합 검증을 수행했습니다. 최초 실행은 Pipeline 요청 오류 때문에 실패했으며, 제품/시험 코드를 유지하고 간섭 조건을 바꾼 최소 재검증으로 해당 사례를 확인했습니다. 두 실행을 하나의 전체 통과 결과로 합치지 않습니다.

## 대상과 실제 결과

- checkout: `D:/codex/worktrees/tamer-working/Tamer`.
- 검증 source: `05b9d83a5b6806a67fed209f66f1e59d195df435`, `codex/receipt-recovery-guidance-1023`.
- Unity `6000.3.25f1`/revision `e1dba0a9aba4`, CLI `1.0.0-beta.8`, Android build target/min25/target36/ARM64 설정의 EditMode 실행입니다. Android Player 실행은 아닙니다.
- 실행 자식에만 `DOTNET_PROCESSOR_COUNT=1`을 사용했습니다. Store 복원·신규 결제·운영 계정·기기/브라우저 API 실행은 없습니다.

| 실행 | 시각(KST) | 결과 | 해석 |
| --- | --- | --- | --- |
| 전체 `Revival` | 20:08:35~20:09:28 | 896개 중895통과/1실패, CLI exit8 | 최초 실패 XML·로그를 보존했습니다. |
| 실패 메서드 False/True | 20:22:03~20:22:27 | 2/2통과, CLI exit0 | 제품/시험 source는 동일하며 Pipeline 자동 시작만 이 batch에서 차단했습니다. |

유일한 실패는 `RevivalGameplaySessionTests.Revival_EquipmentShop_ReplacementPlayerCannotUseOrCommitPreviousPlayersConfirmation(False)`에서 발생한 unhandled Error입니다. Pipeline의 `/api/exec`가 잘못된 `editor status` 명령을 받아 registry에서 찾지 못했습니다. 등록 명령은 `editor_status`입니다. XML 스택은 Pipeline의 명령 해석과 ThreadPool을 가리키며 구매 결과 assert의 값 차이가 실패 원인은 아닙니다. 발신 클라이언트의 정체는 이 근거만으로 확정하지 않습니다.

패키지가 지원하는 `EditorPipelineManager.AutoStart=false`를 단독 `-runTests` batch 시작 전에만 적용했습니다. 재검증 중 HTTP 서버 시작 및 같은 오류가 없었고 두 사례가 통과했습니다. 제품 assert·`LogAssert`를 완화하거나 오류를 무시하지 않았습니다. 종료 후 Pipeline 설정을 사전 원바이트로 돌려놓았습니다. 이는 라이브 Pipeline `run_tests`에 적용할 설정이 아닙니다.

현재 남은 해당 사례 실패는 없지만 **896/896이 한 실행에서 통과했다고 표현하지 않습니다.** 기존 source의 분산된 Editor·LD 결과도 이번 수치에 더하지 않습니다.

## 안내 수정과 기존 근거 대조

`ReceiptOnly`는 삭제 접수 기록만 확인하고 일반 로그인·가입·게임 서비스를 열지 않는 세션입니다. 이때 `Return to sign-in`과 기존 계정 로그인 권유가 실제 차단 동작과 어긋나므로, 닫기를 `Close recovery`로 바꾸고 로그인 불가·로컬 처리 후 재시작을 안내했습니다. 정상 세션의 기존 안내와 계정·삭제·저장 알고리즘은 유지했습니다.

기존 `8144a0a`의 `RevivalDeletionReceiptTests`20/20에는 실제 `Managers.Init`/`DataManager.InitializeData`/`Login.Start`를 사용하는 ReceiptOnly 사례12개가 포함됩니다. 현재 테스트 blob과 핵심 receipt 소스의 동일성을 대조했습니다. 이번 전체 실행에서도 이 경로를 포함하므로 오래된 “소스 검토만/Editor 미실행” 표현과 구분합니다. 실제 Android 제품 LoginCanvas의 표시·버튼·native PGS 활성화 시점은 별도 미검증입니다.

## 보호와 복구

- 각 실행에서 기존 보호 원본9개·보조42개·Pipeline 설정/meta2개를 사전 보존하고 사후 읽기 대조·한정 복구를 수행했습니다. 임시 IAP 설정2개 부재와 소유 Editor 종료·Git clean을 확인했습니다.
- 최초 전체 실행에서 TMP 동적 폰트 asset의 추가 직렬화 변화1개가 보호 범위 밖에서 발견돼 복구 준비를 중단했습니다. 현재 원바이트와 meta를 보존하고 기존 글리프·GUID 참조·객체 식별자를 대조한 뒤, 고정 manifest/after ID·hash와 동일 쓰기 handle을 사용해 해당 파일만 같은 HEAD의 filtered 표현으로 복구했습니다. **사전 원바이트를 알고 복구한 것으로 표현하지 않습니다.**
- 재검증에서는 해당 폰트와 meta도 사전 보존했고 실제 바이트 변경이 없었습니다. 실패·중단·원시 자료·Library·기존 계정·No Ads·서명키를 삭제하지 않았습니다.
- 에셋 사전검사는 기존에 승인된 private importer migration ledger를 명시한 검사에서 verified4561/copied0/SDK meta7/private meta1로 통과했습니다. 최초 옵션 누락에 따른 보존 중단과 후속 검사를 구분하고 manifest·원본을 덮어쓰지 않았습니다.

## 통합과 출시의 구분

PR409는 위 안내 수정을 gameplay 부모 후보에 Squash `49dc23a2b24e534af29d2bcbc5519dfc4cd5743a`로 반영했습니다. 검증 source05b9d83과 이 부모 tree의 diff0을 확인했습니다. 이 문서의 추가 변경은 검증 근거와 운영 규칙 설명이며 같은 Editor/LD 실행을 다시 했다고 표시하지 않습니다.

main 통합 전에는 최종 base/head·필수 검사·review thread와 tree 대응을 별도로 확인합니다. 실제 ARM64/16KB·운영 광고/연령동의·공개방침/Data Safety·최종 운영 AAB·Play 전달/심사는 남아 있습니다. 정확한 새 일반 APK의 설치/첫 화면과 개인 Drive 업로드도 아직 미완료입니다.
