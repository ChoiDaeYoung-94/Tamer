# 삭제 접수 후 종료·명시적 재로그인 격리 검증

2026-10-24, 실제 구현 checkout `D:/codex/worktrees/tamer-working/Tamer`의 source `21e02ab13ca97e52ed491abccf934824d1b0a5b2`를 Android로 빌드했습니다. 원래 Login/Main/설정 화면과 DataManager·ServerManager·Player·ShopMan을 사용하되, 인증·서버 응답은 로컬 가상 계정 A/B/C로 교체한 개발 시험입니다.

## 정확한 산출물과 범위

| 항목 | 값 |
| --- | --- |
| Unity / CLI | `6000.3.25f1` / `1.0.0-beta.8` |
| Android | min25 / target36 / ARM64 / IL2CPP / development·debug 서명 |
| 별도 앱 ID | `com.AeDeong.MonsterTamer.revival.deletiongameplayv3` |
| 빌드 진입점 | `RevivalGameplayBuild.BuildDeletionGameplayAndroid` |
| APK | `Build/revival/Tamer-deletiongameplay.apk`, 110,757,915bytes |
| SHA-256 | `2856cc3623710c4ce83859adf6f8f11a03afefd7527e990ba364e398e0745baf` |
| 실행 | LDPlayer의 정확한 `Tamer` 인스턴스, Android14·4KB·ARM 번역 환경 |

설치된 base APK와 로컬 파일 해시가 일치했습니다. INTERNET·네트워크 상태·BILLING·GMS AD_ID·AdServices 3개 권한과 native Play Games/AdMob 초기화 provider가 없고, backup=false와 transfer 제외 규칙을 확인했습니다. SDK 코드 전체 제거 또는 모든 네이티브 동작의 부재를 뜻하지 않습니다.

시험 전용 define과 정확한 개발 앱 ID 검사를 적용했습니다. 사용자 연령 확인을 자동 제출하지 않으며 이 패키지에만 합성 Declined를 준비합니다. 삭제·Retry 단계는 package 전용 개발 명령으로 **원래 uGUI 버튼 이벤트**를 호출했습니다. 실제 터치·Google 인증·PlayFab 계정 삭제·Store 복원 검증은 아닙니다. 운영 계정·저장·No Ads 구매와 서명키는 접근·변경하지 않았습니다.

## 관측 결과

| 단계 | 실제 결과 |
| --- | --- |
| A 준비 | 원래 Main의 장비1·상점 보유1을 확인하고, 별도 B inventory와 소유 미상 legacy 3종 원본의 hash를 보존했습니다. |
| 요청 미리보기 | 원래 Request 이벤트 후 “아직 확정되지 않았다”는 확인 화면을 관측했습니다. 이 단계 제출0회입니다. |
| 접수·정리 | Confirm 이벤트로 로컬 가상 응답1회, 외부 제출0회. A 진행도·inventory·생성 백업·이전 권한 보관 파일 정리, 자격정보·Player 세션 무효화, B와 legacy hash 보존을 확인했습니다. |
| 늦은 콜백 | 실제 ServerManager의 보류된 쓰기 완료와 이전 Login 콜백을 전달했습니다. 쓰기 후속 반영·예약 조회는 실행되지 않았고 이전 로그인은 StaleRejected였습니다. 제출 직전/정리 후 reads는 각각2회입니다. |
| 접수 화면 | `PHASE1_ACCEPTED_UI_PASS`, “Your deletion request was accepted. You have been signed out.”와 Exit game 표시, Unity 경고·오류0을 관측했습니다. 삭제 완료라는 안내는 하지 않습니다. |
| 원래 종료 | Exit game 이벤트 뒤 PID4254의 종료와 Android launcher 복귀를 확인했습니다. 이 경계에서는 force-stop을 사용하지 않았습니다. 종료 직후 즉시 PID 부재를 단정하지 않고 실제 소멸을 기다렸습니다. |
| 새 프로세스 | PID4684에서 원래 Login의 pause/Retry 화면, 자동 인증0·조회0·쓰기0을 확인했습니다. marker는 새 프로세스 시작 때 소비되어 중간 실패 후 자동 재시드하지 않습니다. |
| 명시적 Retry | 원래 Retry 이벤트1회 뒤 별도 C 프로필을 채택했습니다. Gold41·닉네임/HUD 일치, 빈 도감·장비·아이템·동료·상점 보유와 비활성 장비 모델, B/legacy 보존을 확인했습니다. `PHASE2_C_PLAYER_SHOP_SNAPSHOT_PASS`, Unity 오류0입니다. C는 준비된 가상 프로필이며 서버 신규 가입 증거가 아닙니다. |

최종 상태를 보존한 뒤 해당 개발 앱만 중지했습니다. 기존 실패 실행의 별도 앱·데이터·APK·원시 기록은 지우지 않았습니다.

## 검사와 수정 이력

- source `81aa3c2`에서 관련 Editor `RevivalDeletion` 94/94 PASS, AdServices 권한 제거를 보완한 `cb5b23f`에서 `RevivalGameplayIsolationTests` 34/34 PASS입니다. 서로 다른 실행을 하나의 전체 회귀 통과로 합치지 않습니다. 최종 Android source는 위 `21e02ab`입니다.
- 각 Editor/빌드 실행 전 원본55개를 보관하고 실행 후 실제 변경을 별도 보존·검토한 뒤 원본55/55 일치·Editor 종료·Git clean을 확인했습니다. 설정·manifest·SDK resources·폰트·meta와 기존 키를 정리 목적으로 삭제하지 않았습니다.
- 최초 APK에서 병합된 AdServices 권한3개를 발견해 오프라인 manifest의 제거 목록과 기존 검사를 보완했습니다. LD의 push 방식 설치는 `Failed to restorecon`으로 실패했고, 공간·설치 위치 확인 뒤 streaming 설치로 성공했습니다. 보안 설정을 변경하지 않았습니다.
- 앞선 실행의 연령 화면 차단은 합성 Declined를 준비한 별도 패키지로 해결했습니다. 이후 사후 검사 실패는 Main의 정상 씬 전환 조회를 삭제 이후 조회에 포함한 **시험 기준 시점 결함**이었습니다. 제출 직전으로 기준을 옮기고 항목별 invariant와 실제 카운터 진단을 추가했습니다. 실패 원본은 성공으로 바꾸지 않았습니다.

## 미검증과 다음 범위

- C의 실제 상점 내부 가격·잠금·미착용 표시 화면과 `C_SHOP_VISIBLE` 관측은 미완료입니다. 위 상점 보유 목록의 메모리 검사와 구분합니다.
- 원래 Retry 화면에 기존 네트워크 오류 문구와 삭제 후 명시 로그인 안내가 함께 보이는 표시 문제가 있어 후속 UI 정리가 필요합니다. 자동 인증 중단 자체는 위 결과대로 통과했습니다.
- 실제 인증/삭제 서비스의 접수·최종 삭제, 운영 계정의 구매 복원, 실제 ARM64/16KB, 최종 운영 AAB·Play 제공·광고 정책 통과는 이 결과로 대체하지 않습니다. 현재 최종 운영 AAB는 없습니다.

실패·미완료·수정 조건·각 APK/source·프로세스·로그·화면과 원본 복원 근거는 비공개 관리 기록에 보존합니다. 공개 문서에는 계정·토큰·영수증·기기 등록 해시·게시자 설정 원문을 포함하지 않습니다.
