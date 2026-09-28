# 비공개 importer 메타 1개 명시 승인 복원

2026-09-28 사용자가 비공개 메타1개 예외 처리와 중단된 복원검사3차1회를 승인했습니다. 원본 manifest·공개 SDK migration ledger·권한 있는 원본은 변경하지 않습니다. 일반 복원의 엄격한 해시 검사도 유지합니다.

`restore_assets.py --source <권한 있는 원본> --private-meta-approval <비공개 로컬 ledger>`를 명시한 경우에만 정확1개 `private-restore` 메타를 허용합니다. ledger는 해당 checkout의 ignored·untracked `Logs/revival` 안에 있어야 하며 checkout/source 절대경로·현재 toolchain와 ProjectVersion의 Unity 버전이 일치해야 합니다. manifest old SHA/bytes/GUID와 실제 원본, 승인된 new SHA/bytes/GUID와 현재 메타, 비공개로 보존한 old/current 메타, 대응 에셋 본문의 manifest/local/source SHA·bytes가 모두 일치해야 합니다. 기존 GUID를 유지해야 하며 대상 아닌 private 충돌은 기존 preflight 단계에서 거절합니다. 옵션 없이 동작할 때 private migration을 허용하지 않습니다.

승인 ledger는 도구가 생성하거나 추정하지 않습니다. 사용자가 승인한 근거를 담당자가 별도 비공개로 보존합니다. 파일을 덮어쓰기/manifest 재생성으로 성공시킨 것이 아니며 새 public 예외 목록을 만들지 않습니다. 출력의 `privateMetaMigrations` 수는 원본 manifest 해시 일치와 구분합니다. 구매 에셋의 원시 경로·GUID·해시·메타 내용·승인 ledger는 공개 커밋에 포함하지 않습니다.

구현 checkout `C:/Users/pc_17/.codex/worktrees/unity-lts-transition/Tamer`, branch `codex/private-meta-restore-approval`, 기반 `a153f9e8fb30f0108c3f6078d839dd686ab84b2b`, Editor0 상태에서 도구와 합성 테스트만 수정했습니다. 최종 단위검사5/5 PASS는 정상 exact 승인/바이트 보존, 경로·버전·레코드 거절, source/current/asset/backup 변조 거절, 공개/추적 ledger 거절, 다른 private 충돌의 복사 전 중단을 확인합니다. 승인 레코드를 반환해 restore 본 검사 시점에 new 해시·크기를 다시 대조하도록 수정한 뒤 필요한5개를 다시 검증했습니다. 이전 SDK migration10개 검사를 반복하지 않았습니다.

이 문서는 실제 복원3차 결과가 아닙니다. 독립 리뷰 이후 해당 checkout에서 승인된3차1회를 수행하고 결과를 별도로 기록합니다. 다른 checkout은 같은 파일명이나 해시만으로 승인된 것으로 간주하지 않으며 그 checkout 경로/상태/원본/current 보존 근거를 새로 확인해야 합니다. 도구 구현 중 Unity/API/APK/기기 실행0이고, 미니맵 경고 수정 검증도 아직 시작하지 않았습니다.
