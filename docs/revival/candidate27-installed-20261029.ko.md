# 후보27 설치 확인과 운영 기능·심사 준비 — 2026-10-29

실제 구현 checkout은 `D:/codex/worktrees/tamer-working/Tamer`, 브랜치 `codex/adult-candidate-plan-1029`, source `eba77a66f75122f0c0c539ed0d5b006c73d28253`입니다. Unity6000.3.25f1/revision e1dba0a9aba4, Android min25/target36/ARM64, Unity CLI1.0.0-beta.8 기준입니다. 이 단계는 새 Editor/빌드나 소스 변경 없이 이미 받은 Play 제공 파일을 연결했습니다.

## 확인한 설치 결과

Play 앱서명 범용 APK는77,283,195bytes, SHA-256 `670052ce2c55359d13dac89c3ece5fe966bbaeb450d8b5d0bab7c592091fa553`입니다. [Play 파일 정적 근거](play-candidate27-static-20261029.ko.md)와 같은 파일을 읽기 잠금으로 고정하고, 정확한 Tamer 인스턴스·운영 package의 현재/보존된 미설치 등록 부재·게임 프로세스 부재를 확인했습니다.

15:30 KST에 plain install1회가 성공했습니다. 덮어쓰기·다운그레이드·일괄 권한 부여·데이터 삭제·제거 옵션을 쓰지 않았습니다. 설치된 base.apk SHA-256이 원본과 같고 versionCode27/versionName1.0.5/min25/target36/primaryCpuAbi arm64-v8a를 확인했습니다. nativeLibraryDir 필드는 null, legacyNativeLibraryDir는 lib 경로로 표시됐으며 실제 추출된 네이티브 파일 배치까지 검사한 결과로 쓰지 않습니다.

환경은 LD Android14/API34·4,096bytes 페이지·ARM64 번역 지원입니다. Google Play services26.23.34와 Play Store52.4.41-34의 버전 메타를 읽었습니다. 운영 게임을 실행하지 않았고 설치 후 게임 프로세스도 없었습니다. 기존 Google 설정의 표시계정이 이전 확인한 소유계정과 같은 것만 읽었으며 계정 선택/추가·삭제/인증/보안·개인정보 설정을 변경하지 않았습니다. 표시계정은 앱별 PGS 로그인 신원의 증거가 아닙니다.

HOME 복귀와 정상 에뮬레이터 종료 후 자기 host/VM 프로세스·Tamer 창 부재를 확인했습니다. 설치한 후보와 다른 시험앱·계정/진행·No Ads·키·Library·원본 자료는 보존했습니다. GUI 실제06:29:06Z→06:35:02Z(15:29~15:35 KST), 시작 계정 주간53%/반환 후54%였습니다. 이 파일은 Play 제공 APK의 직접 설치 확인이며 Play Store를 통한 배달이나 게임 실행 성공의 증거가 아닙니다.

## 첫 실행과 필요한 운영 확인

다음은 사용자가 기존 게임 계정으로 직접 첫 실행·필요한 인증/연령/개인정보 선택을 하고, 초기 진행 읽기 및 필요한 화면을 확인하는 단계입니다. 새게임 생성 안내가 나오면 기존 계정 확인을 위해 멈추며 새 결제나 기존 계정 삭제로 조건을 만들지 않습니다. 현재 운영 로그인/저장·실제 광고/동의·기존 구매와 영구 No Ads·실제 삭제 접수는 미검증입니다.

소스의 기본 PGS→PlayFab 로그인은 CreateAccount=false입니다. 연결된 게임 계정을 찾지 못한 경우의 “Start a new game”→“Create new game account” 명시 경로는 빈 진행의 새 계정 생성이며 기존 계정/구매 복구로 안내하지 않습니다. 로그인 성공 뒤에도 서버 진행과 로컬 장비의 소유 경계를 구분하고 모든 도감/장비의 클라우드 복원을 보장하지 않습니다.

실제 ARM64 16KB 실행은 기존 결정대로 보류·미검증입니다. LD4KB/ARM 번역·정적 LOAD·Play 표시/업로드 수락으로 대체하지 않습니다. 광고5초 추가 조사와 CI/CD 활성화 보류도 유지하며 모든 과거 시험을 반복하지 않습니다.

## 심사 준비와 남은 조건

[Data safety 공식 저장](data-safety-saved-20261029.ko.md)은 인간 승인 뒤 완료했고 성공 UI와 저장 CSV782행·수집10/공유6/상위 답안 일치를 확인했습니다. 설치나 신고 저장을 실제 심사전송·심사 승인·배포/트랙 재개로 계산하지 않습니다. 원래 내부 테스트 출시 초안과 준비한 한국어 출시 노트는 별도로 보존했습니다.

Google의 [심사 준비 안내](https://support.google.com/googleplay/android-developer/answer/9859455?hl=en)와 [로그인 정보 요구사항](https://support.google.com/googleplay/android-developer/answer/15748846)에 맞춰 영문 앱 접근 안내의 내부 초안을 준비했습니다. 실제 Console AppAccess와 지정 review 계정이 모든 필요한 기능에 접근할 수 있는지는 아직 확인 전이므로 완성된 심사 접근정보로 기입하거나 제출하지 않습니다. 개인 비밀번호·OTP·토큰을 공개자료로 복제하거나 인증 보호를 낮추지 않습니다.

**작업 상태:** 정확 파일 설치/설치물 대조·정상 종료 완료, 실제 운영 기능·심사 접근/전송 미완료. **다음 우선순위:** 기존 계정 수동 첫실행·필요 화면 확인과 현재 AppAccess 대조. **사용자 할 일/질문:** 다음 기기 차례의 직접 로그인 가능시점 질문1건 대기. **전체 남은 작업:** [잔여 목록](remaining-recovery-work.ko.md)의 운영 기능·제공/심사·권한/백업·문서/시험 자원 정리·저장소 이름 전환을 유지합니다. 오늘21시 또는 계정70% 중 먼저 오는 경계 전에 복구·인계·보고합니다.
