# 손님 선택 UI — 실물 ARM64 격리 검증

2026-11-07 SM-N986N(Android13/API33, 페이지4KB)에서 기존 ARM64 Release 시험 APK를 재빌드 없이 검증했습니다. 이 APK의 원본 소스는 `4a530751796e75b3c88157c3d662052af916c56c`입니다. 현재 `62e1661`과 `InitialAccountChoiceView.cs`·`LoginGuestEntry.cs`의 Git blob이 같고, 시험 harness의 후속 차이는 다른 진단 앱 ID 허용 추가뿐임을 대조했습니다.

## 결과

| 확인 | 실제 결과 |
| --- | --- |
| APK | 71,778,144bytes, SHA256 `4ed01d55be003f36ea30a2cfa538e385df56801ea1b6d4ad615f08d845936d29` |
| 설치·실행 | 별도 앱 ID, 설치 APK SHA/선택 CPU `arm64-v8a` 일치. 흰 화면 정지 없이 한글 선택 화면 표시 |
| 초기 UI | Google/손님 버튼과 계정·복구 제한 안내가 화면 안에 표시됨. 초기 로그인 방식은 비어 있고 식별자 없음 |
| 모의 Google 실패 | 화면에 명시된 오프라인 모의 버튼1회 입력. 실패 문구와 재선택 표시, 방식/식별자는 계속 비어 있음 |
| 명시 손님 선택 | 로컬 무작위 식별자를 생성·PlayerPrefs 저장/flush/read-back, `custom-pending` 확인 |
| 새 프로세스 복원 | 자체 앱 중지 뒤 새 PID에서 `restored`와 동일 식별자 SHA 확인. 새 식별자로 교체하지 않음 |
| 격리 | INTERNET/네트워크 상태·광고 ID/AdServices·결제 권한 및 GMA/PGS 초기 provider 없음, debug 서명·non-debuggable, 백업/전송 제외18항목 |
| 종료 | 자체 앱 중지/PID0·HOME, 원격 임시파일0·공유 ADB 서버 신원 유지. 16:14:36KST 실제 전체 반환 |

초기 자동 입력은 Samsung의 foreground 출력 필드명이 예상과 달라 **탭 전에** 중단됐습니다. 실제 `topResumedActivity`/`ResumedActivity`를 읽고 기존 시도·로그를 보존한 새 시도에서 재개했습니다. 앱/실제 Google 인증 실패와 구분하며, 모의 Google 탭은 성공한1회뿐입니다.

이전 동일 ARM64 시험 APK의 LD 흰 화면 관측과 이번 실물 성공은 별도 결과입니다. LD 실패를 모든 실물 ARM64의 실패로 일반화하지 않으며, 정상 Play31/새 전체 후보의 실행 성공으로도 확대하지 않습니다.

## 남은 범위

실제 Google 인증·운영 손님 계정 생성·서버 저장·기존 계정/구매·No Ads·연령별 SDK/광고·삭제는 이 fixture에서 호출하지 않습니다. 이 결과는 로컬 UI/선택값 보존 검증이며 서버 복원이나 실물16KB 통과가 아닙니다. 현재 [심사 안내 초안](guest-review-instructions-draft-20261102.ko.md)의 운영 미검증 조건과 [전체 잔여](remaining-recovery-work.ko.md)를 유지합니다. 시험 ID 원문·기기 식별자·원시 로그는 비공개로 보존했습니다.
