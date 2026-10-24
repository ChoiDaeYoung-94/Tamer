# 로그인 재시도 안내 중복 수정 — 2026-10-24

삭제 요청 접수 뒤 명시 재로그인을 기다리는 화면에 기존 네트워크 안내와 새 상태 안내가 함께 나타났습니다. `LoginCanvas`의 기존 `TMP_Contents`가 `_retryText`에 연결되지 않아 `EnsureRetryMessage`가 별도 텍스트를 생성하는 것이 원인이었습니다.

기존 안내 객체를 `Panel_Retry` 바로 아래로 옮기고 `_retryText`에 연결했습니다. 버튼 안에는 `Retry` 또는 `Contact support` 캡션만 남습니다. 연결이 없는 경우에도 패널의 기존 안내문을 먼저 재사용하며, 삭제 접수 확인 중단 콜백에서도 안내 연결을 확인합니다. 인증·계정 생성·구매 복원·삭제 요청 권한은 변경하지 않았습니다.

## 변경과 보존

- Unity Editor API로 기존 프리팹을 수정했습니다. 기존 `.meta`·GUID·객체 fileID·font/material/color·enabled 상태·RetryConnection/ClickedOK 영구 호출을 보존했습니다. 씬은 저장하지 않았습니다.
- 기존 문구의 글자 크기를 런타임 안내와 같은30으로 맞추고 버튼 캡션과 레이아웃을 분리했습니다. Unity6000.3의 프리팹 저장에 따른 TMP/Animation 직렬화 갱신이 함께 포함됩니다.
- ReceiptOnly의 loading 표시·명시 로그인 차단은 유지합니다. spinner와 긴 recovery 안내의 배치는 이 수정의 범위가 아닙니다.

## 검증 근거와 한계

실제 checkout은 `D:/codex/worktrees/tamer-working/Tamer`, Unity `6000.3.25f1`(`e1dba0a9aba4`), Android 활성 대상은 min25/target36/ARM64, CLI `1.0.0-beta.8`입니다. 검증 시 C# source는 `6245522a97767af5d6cb951d1a4aa232e538b19d`, 의도한 프리팹의 raw SHA-256은 `7d1e1c5f04ba74883bb15091a4bb3e725b3398123585a5d0bdedb1d9362ec764`이며 이후 `805e581`에 해당 프리팹을 커밋했습니다.

| 범위 | 실제 결과 |
| --- | --- |
| EditMode 프리팹 미리보기 | 기존 문구 재사용·추가 텍스트0, 접수 안내와 Retry 캡션 분리, 삭제 계정 안내와 Contact support 캡션 분리.1080×2340 이미지 직접 확인. |
| ReceiptOnly 미리보기 | 재시도 패널 숨김·loading 안내 유지. 로그인/계정 API 호출 없이 상태 확인. |
| RevivalLoginContinuityTests |61개 중61개 통과,0.55초. |
| RevivalDeletionReceiptTests |20개 중20개 통과,2.58초. 위 검사와 별도 실행. |
| Editor 오류 | 위 검증 후 console Error/Exception/Assert0. |
| 원본 보호 |59개 before/after 보존, 의도한 프리팹1개를 제외한58개 원본 일치 확인. Editor에서 발생한 비의도 ProjectSettings 변경1개 복구. 씬/meta 보존. |

첫 Pipeline 복수 필터 실행은 세미콜론이 단일 부분일치 문자열로 처리되어0개가 실행됐습니다. 이를 통과로 계산하지 않고 두 fixture를 각각 실행했습니다. Editor 종료는 정상 요청 후20초 관측에서 timeout이었으나 후속 관측에서 실제 종료를 확인했으며 강제 종료하지 않았습니다.

미리보기는 EditMode의 별도 preview scene에서 수행했고 게임 Play Mode·인증·메일 열기·Store·외부 계정 삭제를 실행하지 않았습니다. 이 수정판의 새 Android APK 빌드·실제 기기 화면·native 터치·실계정 로그인은 미검증입니다. 이전 삭제 lifecycle APK의 결과를 새 프리팹의 실행 결과로 전용하지 않습니다. 비공개 미리보기·원시 로그·보호 snapshot은 공개 저장소에 추가하지 않았습니다.
