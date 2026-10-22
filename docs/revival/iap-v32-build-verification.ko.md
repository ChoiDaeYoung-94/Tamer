# 시험32번 복원·저장 관측 후보

2026년10월22일 데이터 초기화 후 새 로컬 프로필을 준비한 기존 시험 계정에서, 기존 구매 복원을 한 번 수행하고 저장 상태를 관측할 후보를 준비했습니다. **빌드·산출물 검사와 소스 복구는 완료했지만 업로드·32번 폰 실행·복원·재시작 검증은 아직 하지 않았습니다.** `releaseReady=false`와 PR395 Draft를 유지합니다.

## 소스와 빌드

- 실제 checkout: `D:/codex/worktrees/tamer-working/Tamer`, 브랜치 `codex/iap-retention-observation-1022`.
- 빌드 소스: `22e82862d5e817c795ce8429c700fb06b267fa4d`. 이후 이 문서 추가는 빌드 소스 변경으로 표현하지 않습니다.
- Unity6000.3.25f1 / revision e1dba0a9aba4 / CLI1.0.0-beta.8 / Android min25·target36·ARM64 / non-debug.
- versionCode32 / versionName1.0.5 / 시험 앱 `com.AeDeong.MonsterTamer.iaptest`. 운영 앱이나 서버 설정은 변경하지 않았습니다.
- 빌드 실행: 2026-10-22 11:46:45~11:49:59 KST, 종료0, 첫 시도 성공.
- 시험31번과 게임 코드 차이는 없고, 이번 빌드에만 복원 재개 define을 포함했습니다. 생성 IL2CPP의 ResumeBuildApproved getter가 정확히 한 곳에서 true인 것을 검사했습니다.
- 런타임 복원 예산1, 사용자 명시 체크/버튼과 내구성 원장이 필요합니다. 신규 구매·자동 재시도는 차단하고 실제 Store 실행은 하지 않았습니다. 사용자 데이터 삭제를 과거 원장 이력 초기화나 무제한 재시도 승인으로 취급하지 않습니다.

## 검사와 보존

- 고정 bundletool의 AAB 유효성·manifest·버전/SDK/비디버그 설정, 기존 시험 업로드 인증서와 서명 payload601개 검사를 통과했습니다. 새 서명키는 생성하지 않았습니다. GMS AD_ID 권한은 없지만 AdServices 관련 권한은 남아 있으며 모든 광고/개인정보 수집이 차단됐다는 검사 결과로 표현하지 않습니다.
- 네이티브 ELF6개 LOAD 및 보수적 RELRO 배치 검사는 통과했습니다. 원시 strict RELRO 결과는 false이며 실제16KB·Play 전달 APK/split·ZIP 정렬 실행 검증과 구분합니다.
- 앞선 코드2427383의 관련 Editor 시험25/25 PASS를 보존합니다. 해당 소스 이후 Assets·Packages·tools 변경이0인 것을 확인해 동일 테스트를 반복하지 않았습니다.32번 실행 define과 산출물은 위 별도 검사로 확인했으며 폰 실행 통과로 대체하지 않습니다.
- 빌드 전 보호53개를 기록하고 이전 산출물4개를 보존했습니다. 빌드 후 별도 읽기 검사에서 원본51개·캡처53개·GUID10개 일치와 범위 밖 추적 변경0을 확인했습니다.
- 실제 캡처 파일 ID·해시와 백업을 고정한 소유 파일 복구를 한 번 수행해 원본9개+보조42개·포트2개를 원바이트로 확인했습니다. 임시 설정2개 부재, Git clean과 해당 Tamer Editor 종료를 확인했습니다. 다른 프로젝트 Editor·Library·기존 비공개 증거는 정리하지 않았습니다.

## 전달 파일

- 파일명: `Tamer-iap-test-v32-22e8286.aab`.
- 크기: 73,987,594bytes.
- SHA256: `3e2ded882bcfdb0e66a85ecfde796e99b4039886abc21267105ba4e537a67e0e`.
- 로컬 전달 폴더: `C:/Users/user/Downloads/deployment/tamer-iap-v32-20261022`.
- 같은 폴더에 `SHA256SUMS.txt`와 `업로드 안내.ko.txt`를 제공했습니다. 키·암호·계정 ID·CustomID·영수증은 포함하지 않습니다.

## 사용자 진행 순서

1. **Monster Tamer IAP Test** 내부 테스트에32번을 수동 업로드·제공하고, 현재 앱을 삭제하거나 데이터를 지우지 않고 업데이트합니다.
2. 기존 두 입력값을 사용하고 이번에는 **Reauthenticate the previously committed recovery profile**을 체크해 Authenticate를 한 번 누릅니다.11:38에 이미 만든 새 로컬 프로필을 다시 여는 단계이므로 다른 빈 프로필을 만들지 않습니다. 최초 No Ads absent는 현재 프로필의 예상 상태입니다.
3. 같은 기존 시험 계정/기존 구매임을 확인한 뒤 **Coordinator has recorded user approval…** 체크를 켜고 **Run approved recovery ONCE / fetch owned purchases**를 한 번만 누릅니다. 새 결제는 하지 않습니다.
4. Receipt check 결과가 나오면 **Read saved No Ads (no Store call)**를 눌러 로컬 표시를 갱신한 뒤 Receipt check와 No Ads local 표시를 Slack에 공유합니다. 이 표시는 자동 갱신 값이 아니므로 복원 전 absent를 복원 후 결과로 읽지 않습니다. 읽기 버튼은 Store를 다시 호출하지 않습니다. 실패·원장 이미 사용 안내가 나오면 복원 버튼을 반복하지 않고 결과를 공유합니다.
5. 결과 확인 후 강제 중지·재실행·같은 프로필 재인증으로 재시작 전 디스크와 현재 No Ads가 유지되는지 관측합니다. 이때 복원 버튼은 다시 누르지 않습니다.

31번의 Store 잠금과32번의 명시적 단일 실행 후보를 구분합니다. 실제 기기·저장 유지 외에도 게임8Draft, 광고·정책·16KB·운영 출시·최종 README와 시험 자원 정리는 남아 있으며 이 빌드 성공으로 완료 처리하지 않습니다.
