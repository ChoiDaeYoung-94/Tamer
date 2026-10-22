# 복원 후 게임 통합 후보 APK

2026년10월22일 기존 구매 복원·저장·재시작 보존 관측을 마친 부모 통합 후보에서 별도 오프라인 게임 APK를 준비했습니다. **빌드와 APK 정적 검사는 완료했지만 실제 기기 실행·상점·장착/HUD·게임 왕복 검증은 아직 하지 않았습니다.** 이전 게임/호스트·가입 UI 중단 이력과 기존8Draft를 보존하며 `releaseReady=false`를 유지합니다.

## 기준과 완료한 범위

- 실제 checkout: `D:/codex/worktrees/tamer-working/Tamer`, 작업 브랜치 `codex/gameplay-candidate-1022`.
- 실제 빌드 소스: `f30a9e65e344a656e72bbe3c4c5b237f900f8c40`. 이는 PR395를 `codex/revival-candidate-1020`에 Squash한 커밋이며 main 출시 병합이 아닙니다.
- PR395 최종 head0b76e11과 squash의 tree 동일성을 확인했습니다. 원격 관측 브랜치는 저장소 자동 삭제 설정으로 제거된 뒤 prune했고, 로컬 tip은 후보가 main에 포함될 때까지 보존합니다. 이슈351은 원래 수정 PR352의 main 포함과 실제 복원/저장/재실행 근거를 남겨 완료했습니다.
- Unity6000.3.25f1/revision e1dba0a9aba4·CLI1.0.0-beta.8, Android min25/target36/ARM64.
- 14:25:27~14:30:25 KST 첫 빌드 종료0, 빌드 로그의 C# 오류0·셰이더 오류0. 런타임 오류나 모든 그래픽 변형의 검증 결과로 확대하지 않습니다.

## 실제 APK 검사

| 항목 | 결과 |
| --- | --- |
| 앱 ID | `com.AeDeong.MonsterTamer.revival.gameplay` — 운영 앱/IAP 시험 앱과 별도 |
| 버전/서명 | 1.0.5/code26, 개발(debuggable) APK, Android Debug 서명 검증 통과 |
| 크기 | 110,710,064bytes |
| SHA256 | `eb92ab0a45f27d5f1bf8f967fda8409c6f4aac79028bd7698e686f31b3c55269` |
| 기본 권한/제공자 | INTERNET·ACCESS_NETWORK_STATE·BILLING·GMS AD_ID·MobileAdsInitProvider 부재 검사 통과 |
| 남은 경계 | AdServices 권한은 남아 모든 광고 권한 제거로 표현하지 않음. 실제16KB·ELF/ZIP·Play 전달·운영 출시 검증은 실행하지 않음 |

기존 `RevivalGameplayBuild.BuildAndroid`를 사용했습니다. 합성 계정/메모리 서버와 매 실행 별도 저장 경로, 로그인·광고·구매 차단은 기존 하네스의 경계입니다. 실제 운영 계정·No Ads 권한을 가져오는 앱이 아닙니다. 이번에는 실행하지 않았으므로 코드의 격리 경계를 실제 통신 측정 결과로 표현하지 않습니다. default 개발 하네스는 재실행마다 새 격리 저장 경로를 만들며, 앞선 IAP32번의 재시작 보존 시험과 혼동하지 않습니다.

## 소스 보존

기존 무조건 원복 wrapper를 실행하지 않고, 빌드 전 보호53개와 기존 IAP 출력4개를 보존했습니다. 빌드 후 원본51개·캡처51개·GUID10개와 범위 밖 추적 변경0을 별도 읽기 대조했습니다. IAP 임시 설정2개가 생성되지 않은 것도 확인했습니다.

실제 파일 ID/해시를 고정한 원본9개+보조42개 복구를 한 번 수행했습니다. 포트2개를 포함한 보호53개 원바이트, 임시2개 부재, Git clean과 해당 Tamer Editor 종료를 확인했습니다. 기존 Library·키·계정·다른 Editor와 worktree는 정리하지 않았습니다. 게임 코드 변경이 없어 동일 Editor 회귀를 반복하지 않았으며 이번 컴파일/산출물 검사와 과거 개별 시험 결과를 구분합니다. strict RELRO 및 중단된 기기·가입 UI 시험 재실행은0입니다.

## 전달과 다음 순서

로컬 파일은 `C:/Users/user/Downloads/deployment/tamer-gameplay-20261022/Tamer-gameplay-f30a9e6.apk`이며 같은 폴더에 SHA256SUMS.txt와 후속 검증 안내.ko.txt를 준비했습니다. **Play Console의 Monster Tamer IAP Test에 올리는 AAB가 아닙니다.** 기존 IAP 시험 앱을 삭제하거나 데이터를 지울 필요가 없습니다. 외부 업로드·폰 설치·실행은 아직 하지 않았습니다.

다음에는 휴대폰 전달 방법과 과거 중단 이력을 확인한 구체 실행 범위를 정해, 이 APK에서 Main/캐릭터/HUD·원래 상점의 단일 구매·장착 모델/수치·생존 Main→Game→Main·전투/원래 Game Over·Home/복귀를 구분해 관측합니다. 합성 계정이므로 일반 가입 UI·실제 로그인·서버 계정 복원·운영 구매·광고 정책까지 통과한 것으로 처리하지 않습니다. 서명 충돌이 있으면 기존 앱을 임의 삭제하지 않고 상태를 보존해 진단합니다.
