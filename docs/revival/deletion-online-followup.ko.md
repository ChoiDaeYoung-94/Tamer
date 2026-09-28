# 기존 기록을 보존하는 온라인 삭제 시험 준비

2026-09-28 사용자가 신규 구매 없는 폐기 계정 1개와 그 계정에 한정한 짧은 시험 gate, 시험 타이틀 API 일시 허용·검토 revision 게시·삭제 요청 1회·시험 후 원복 범위를 승인했다. 이 문서는 독립 앱 준비 결과다. 새 계정 생성·앱 인증 preflight·삭제 접수·원격 설정 변경은 아직 실행하지 않았다.

checkout `C:\Users\pc_17\.codex\worktrees\7299\Tamer`, 브랜치 `codex/deletion-online-followup`, 시작 HEAD/검증 기준 `ad2e89ae3ed84b6e244951f21765e133297d2949`와 미커밋 하네스·도구 6개 파일이다. 앞선 `b1085a7` 이후 차이는 문서 2개뿐이며 런타임 코드는 같다. 빌드 전 원시 해시가 6개 파일에 그대로 유지됐음을 확인하고, 줄바꿈을 정규화한 내용이 소스 커밋 `ebb60137aa2ccc25dbad78ac2c5b86a4ff3dc8cb`의 Git blob과 모두 일치함을 확인했다. 첫 직접 blob 해시 비교에서는 한 파일의 혼합 줄바꿈 때문에 불일치가 발생했으며 내용 변경은 없었다.

Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android min24 / target36 / ARM64, build-tools `36.0.0`를 유지했다. 복원 verified4561/copied0, 서비스 설정 제외를 확인했다. 기존 원본과 다른 프로젝트 Editor는 변경하지 않았다. 해당 checkout Editor가 없는 상태에서 시작했고 빌드 wrapper의 설정·manifest·씬 snapshot 복원 후 코드 6개 파일만 변경됨을 확인했다.

`Run-DeletionTrial.ps1 -Online`은 별도 `com.AeDeong.MonsterTamer.deletiontrial.online` debug 앱과 `Tamer-deletion-online-trial.apk`를 선택한다. `TAMER_DELETION_ONLINE_TRIAL`은 해당 player 빌드의 extra define만 사용하며 기존 앱 ID와 전역 define을 변경하지 않는다. 해당 앱에서는 오프라인 합성 실행 버튼을 제외한다. 현재 앱 인증 경로는 계속 지정 CustomId/expected ID와 CreateAccount=false를 요구한다. 일반 출시 앱과 기존 시험 앱에 이 define을 적용하지 않는다.

계정 생성 도구의 `--online-followup`은 새 고정 전용 디렉터리를 사용해 이전 생성 guard와 폐기 계정 증거를 보존한다. 승인된 시험 타이틀과 기존 API allowlist·exclusive/fsync 생성 guard·unknown 후 재생성 차단·token 제외·masked 사용자 입력만 유지한다. 키를 argv/env/파일/브라우저에서 읽지 않는다. 도구의 관련 오프라인 테스트는 **4/4 통과**했다. helper의 비밀 입력이나 네트워크 명령은 아직 실행하지 않았다.

별도 APK는 첫 빌드에 성공했다. 이전 성공 조건의 process-local Java 초기 메모리/CPU 제한을 적용하고 종료 후 환경을 복원했다. OS·Editor 전역 설정은 변경하지 않았다. 앱 ID/debug 서명/ARM64/network 권한, BILLING·AD_ID·광고 초기화 provider 제외, backupfalse와 18개 추출 제외 규칙을 검증했다. APK 93475611 bytes, SHA-256 `947fc13be3eaf8485658f604e4d273bda0ec5aaac761e0441a0d2f61f0f2e3db`이다.

SM-N986N Android13(API33)에서 새 패키지 부재를 확인한 후 기존 앱과 나란히 설치했다. 신규 패키지의 로그인 입력 화면에서 marker/saveReady/pending/signedIn=false와 오프라인 버튼 부재를 확인했으며 로그인 버튼은 누르지 않았다. 기존 오프라인 앱의 파일 8개와 미해결 journal은 [앞선 검증](deletion-device-followup.ko.md) 때와 해시가 모두 같다. 신규 앱만 종료했으며 설치·데이터·원시 증거를 보존한다. 계정·기기 식별자와 화면·원시 로그·운영 설정은 공개하지 않는다.

콘솔 로그인 중 Microsoft 서비스 계약 업데이트 안내가 표시되어 사용자가 처리할 화면 앞에서 멈췄다. 콘솔 복귀 후 시험 타이틀의 현재 revision·원본·API 옵션·내부 gate를 재조회해야 한다. 보존한 시험 후보는 현재 저장소의 삭제 template와 gate 코드에 일치하고 Node 문법 검사도 통과했지만, 이를 현재 원격 상태의 증거로 쓰지 않는다. 원격 변경을 시작하지 않아 이번 준비 단계에서 원복한 설정은 없다.

로그인 복귀 후 이미 승인된 단일 신규 폐기 계정·최대 15분 gate 범위로 진행한다. 삭제 요청은 1회만 보내고 실패·unknown이면 자동 재전송 없이 중단한다. 시험 후 변경한 API 옵션·내부 gate·Live 상태를 순서대로 원복·재조회한다. 운영/기존 PGS·IAP 계정과 회사 Google 결제는 대상에서 제외한다. 현재 온라인 접수·로그아웃·소유 로컬 정리·후속 서버 조회 결과는 미검증이다. 운영 인증 preflight·스토어 복원·최종 출시 AAB·16KB 검증도 이 준비 결과로 보증하지 않는다.
