# 개인정보 창 최초 오류 안내와 명시 재시도 — 2026-10-24

관련 후속: [#419](https://github.com/ChoiDaeYoung-94/Tamer/issues/419). 이전 cold 시험의 실제 UMP `FormFailed/code3`과 사전 로딩 진단은 [원 관측](privacy-cold-20261024.ko.md)에 보존합니다. 이 변경은 해당 실제 오류를 성공으로 바꾸거나 SDK 준비 완료를 추정하지 않습니다.

개인정보 창을 열지 못하면 준비 중이거나 연결에 문제가 있을 수 있음을 안내하고, 잠시 후 같은 버튼을 직접 다시 누르도록 설명합니다. code3 전체를 사전 로딩 오류로 분류하지 않습니다. 기존 `RequestPrivacySettings`와 fresh Required owner 재사용 경로를 유지하며 자동 Show 반복·임의 지연·동의 reset·운영 승인 변경을 추가하지 않습니다.

## 최소 검증

Unity6000.3.25f1(`e1dba0a9aba4`), Android 활성 대상 min25/target36/ARM64, CLI1.0.0-beta.8, 실제 checkout `D:/codex/worktrees/tamer-working/Tamer`에서 수행했습니다.

- source `a0dad0291ee087b7a0d3c245eedfc346c404cce1`: 기존 광고 manager/consent와 연령 UI를 포함한 Editor74/74 PASS(0.786초).
- 안내 길이를 줄인 최종 code source `c4a5e200404ac88ec21c51306c29cdf67c13dcf4`: 영향받은 UI 통합1/1 PASS(0.355초).74개 전체를 이 source에서 다시 실행한 것으로 합치지 않습니다.
- 새 통합 사례는 실제 manager·상태 큐·`AgePrivacyOptionsEntry`·원래 uGUI 이벤트를 사용하고 SDK adapter만 가상 구현으로 대체합니다. fresh Update1→Show1→실패 후 버튼 재활성→프레임 갱신만으로 추가 호출0→명시 클릭 후 Update 추가0/Show2→진행 중 버튼 비활성→이전 콜백 무시→직접 중복 이벤트의 SDK 추가 호출0→실제 owner 콜백에 해당하는 가상 응답 후 OptionsClosed를 확인했습니다.
- 가상 SDK가 광고 가능을 반환해도 개인정보 전용 owner는 광고를 허용하지 않았습니다. 광고 초기화·load·rewarded 객체와 Managers bootstrap은 없었습니다. 기기 연령 저장은 변경하지 않았습니다.
- 최종 안내의 TMP preferred height가 기존1080 기준 폭864/높이110 상태 영역 안에 들어옴을 확인했습니다. 두 실행에서59개 원본/실행 후 snapshot을 각각 보존했고, font glyph의 시험 중 증가·Pipeline 제어·Editor 설정 변경을 검토한 뒤59개 원본 byte 일치와 clean·Editor 종료를 확인했습니다. 비공개 snapshot·로그는 공개하지 않습니다.

이는 가상 adapter를 사용한 Editor 검증입니다. 새 Android 바이너리·실제 UMP 최초 실패 후 폼 재표시·native 동의 선택·실제 No Ads 구매권한 결합은 미검증입니다. #419는 실제 폼 표시 후속이 남아 있어 열린 상태로 유지합니다. 기존 warm 성공을 이 cold 조건의 성공으로 사용하지 않습니다. Chrome이 필요한 운영 자료와 승인 false/null도 보류 상태를 유지합니다.
