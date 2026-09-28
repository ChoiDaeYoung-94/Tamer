# 기존 기록을 보존하는 온라인 삭제 시험과 접수 화면 수정

2026-09-28 사용자가 승인한 신규 구매 없는 폐기 계정 1개로 별도 debug 앱의 온라인 삭제 확인을 1회 실행했다. 앱의 접수·로그아웃 표시, 해당 소유 로컬 파일 제거, 시험 타이틀에서 계정 검색 결과 0명, 서버 설정 원복을 확인했다. 접수 뒤 화면 렌더링 예외도 발견하여 아래 별도 코드와 오프라인 UI 테스트로 수정했다. 실제 온라인 시험 전체를 예외 없는 PASS로 기록하지 않는다.

checkout `C:\Users\pc_17\.codex\worktrees\7299\Tamer`, 브랜치 `codex/deletion-online-followup`, 시작 HEAD/검증 기준 `ad2e89ae3ed84b6e244951f21765e133297d2949`와 미커밋 하네스·도구 6개 파일이다. 앞선 `b1085a7` 이후 차이는 문서 2개뿐이며 런타임 코드는 같다. 빌드 전 원시 해시가 6개 파일에 그대로 유지됐음을 확인하고, 줄바꿈을 정규화한 내용이 소스 커밋 `ebb60137aa2ccc25dbad78ac2c5b86a4ff3dc8cb`의 Git blob과 모두 일치함을 확인했다. 첫 직접 blob 해시 비교에서는 한 파일의 혼합 줄바꿈 때문에 불일치가 발생했으며 내용 변경은 없었다.

Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android min24 / target36 / ARM64, build-tools `36.0.0`를 유지했다. 복원 verified4561/copied0, 서비스 설정 제외를 확인했다. 기존 원본과 다른 프로젝트 Editor는 변경하지 않았다. 해당 checkout Editor가 없는 상태에서 시작했고 빌드 wrapper의 설정·manifest·씬 snapshot 복원 후 코드 6개 파일만 변경됨을 확인했다.

`Run-DeletionTrial.ps1 -Online`은 별도 `com.AeDeong.MonsterTamer.deletiontrial.online` debug 앱과 `Tamer-deletion-online-trial.apk`를 선택한다. `TAMER_DELETION_ONLINE_TRIAL`은 해당 player 빌드의 extra define만 사용하며 기존 앱 ID와 전역 define을 변경하지 않는다. 해당 앱에서는 오프라인 합성 실행 버튼을 제외한다. 현재 앱 인증 경로는 계속 지정 CustomId/expected ID와 CreateAccount=false를 요구한다. 일반 출시 앱과 기존 시험 앱에 이 define을 적용하지 않는다.

계정 생성 도구의 `--online-followup`은 새 고정 전용 디렉터리를 사용해 이전 생성 guard와 폐기 계정 증거를 보존한다. 승인된 시험 타이틀과 기존 API allowlist·exclusive/fsync 생성 guard·unknown 후 재생성 차단·token 제외·masked 사용자 입력만 유지한다. 키를 argv/env/파일/브라우저에서 읽지 않는다. 도구의 관련 오프라인 테스트는 준비 단계 첫 실행 **4/4 통과**했고 별도 원본 로그 파일은 저장하지 않았다. 사용자가 가림 입력 창에서 키를 직접 입력했다. 입력 버튼 자체는 서버 호출을 하지 않으며, 준비 상태와 nonce를 확인한 뒤 도구가 신규 계정 생성 1회를 실행하여 `NewlyCreated=true`와 시험 타이틀 일치를 확인했다. 시험 종료 뒤 창의 정상 종료 상태와 프로세스 부재를 확인했다.

별도 APK는 첫 빌드에 성공했다. 이전 성공 조건의 process-local Java 초기 메모리/CPU 제한을 적용하고 종료 후 환경을 복원했다. OS·Editor 전역 설정은 변경하지 않았다. 앱 ID/debug 서명/ARM64/network 권한, BILLING·AD_ID·광고 초기화 provider 제외, backupfalse와 18개 추출 제외 규칙을 검증했다. APK 93475611 bytes, SHA-256 `947fc13be3eaf8485658f604e4d273bda0ec5aaac761e0441a0d2f61f0f2e3db`이다.

SM-N986N Android13(API33)에서 새 패키지 부재를 확인한 후 기존 앱과 나란히 설치했다. 시작 시 marker/saveReady/pending/signedIn=false와 오프라인 버튼 부재를 확인했다. 실제 실행 checkout은 같은 폴더의 `codex/deletion-online-followup`, clean HEAD `69f7119c08c5c7e8171adbaf47a10b571aca960a`이다. 원격 main `731ab3527c5246ad5849605c9543fe3928ad7723`을 조회·대조했으며 두 커밋의 차이는 문서와 작업 규칙뿐이고 실행 코드·도구·패키지·ProjectSettings 차이는 없다. 해당 checkout Editor는 없었고 다른 프로젝트 Editor는 건드리지 않았다. 이전 APK 해시가 같아 재빌드하지 않았다.

새 콘솔 조회에서 정상 인증을 확인하고 현재 Live 버전과 API 옵션·내부 gate를 읽었다. 미배포 후보 코드의 전체 내용을 읽기 전용 Editor에서 복사하여 LF 정규화 해시가 준비한 후보와 일치함을 확인했다. 사용자 원래의 미저장 설정 탭은 재로드하거나 변경하지 않았다. 신규 계정만 최대 15분 동안 허용하는 내부 gate를 저장하고 시험 타이틀의 삭제 API를 일시 허용한 뒤 검토된 후보를 Live로 게시했다. 변경 후 API와 Live 버전을 다시 읽었다.

앱은 지정 CustomId와 예상 ID로 기존 계정 로그인 1회를 수행했다(`CreateAccount=false`). 실제 제품 삭제 화면에서 Live 사전 확인을 거쳐 범위 확인 화면을 표시했고, 별도 확인 버튼을 1회만 눌렀다. 검증한 앱 소스는 사전 확인의 revision을 `Specific` 제출에 고정하고 동일 revision 접수 응답을 검사한다. 앱은 접수와 로그아웃 안내를 표시했고, 삭제 전 owner가 신규 계정과 일치했던 `DeletionTrialPlayer.json`은 사라졌다. 접수 journal 디렉터리는 비어 있었다. 추가 삭제·새 계정 재생성·재로그인은 하지 않았다.

성공 안내 직후 `DeletionPresenter.Render()`의 NullReferenceException을 발견했다. 격리 하네스는 Managers 인스턴스만 만들고 PopupManager를 초기화하지 않는데, 접수 분기가 인스턴스 존재만 확인하고 PopupManager에 접근했다. 메시지와 종료 캡션을 갱신한 뒤 예외가 발생하여 나머지 버튼 갱신을 중단했다. 삭제 접수와 로컬 정리 결과는 이 예외와 구분한다. 이를 전체 온라인 성공으로 표현하거나 수정 후 기기 재검증 결과로 바꾸지 않는다.

원복은 예외 조사보다 먼저 수행했다. 삭제 API 비활성 저장 후 재로드하여 다시 확인했고, 이번 내부 gate를 제거하여 데이터가 비어 있음을 확인했으며, 원래 Live 버전을 재게시했다. 도구의 마지막 읽기 요청에서도 원래 PublishedRevision과 미배포 LatestRevision을 확인했다. 신규 계정 ID의 콘솔 검색 결과는 0명이었다. 이는 해당 타이틀의 조회 관측이며 제공자 로그·백업·publisher/master 전체 소거 완료를 증명하지 않는다. 기존 오프라인 앱의 파일 8개와 미해결 journal은 [앞선 검증](deletion-device-followup.ko.md) 및 시험 전 복사본과 시험 후 해시가 모두 같았다. 앱은 Exit game으로 정상 종료했고 설치·기기 데이터·비공개 증거를 보존했다. 사용자가 입력 도구를 정상 종료하여 ready=false/closed=true 및 프로세스 부재를 확인했다. 담당자가 새로 연 완료 서버 탭은 닫았고 사용자 원래 탭은 보존했다.

예외 수정은 최신 main에서 만든 `codex/deletion-accepted-popup-guard`, clean 코드 커밋 `8e1bf5c3445a829079b3149084228e5deb7b89fc`에서 별도로 검증했다. 실제 PopupManager가 존재할 때만 blocker를 등록하도록 최소 수정했다. 미초기화 Managers와 PopupManager 없음/있음 두 경우를 원격 호출 없는 fake gateway로 재현하여 접수 메시지·Exit game·불필요한 버튼 숨김·기존 blocker 유지·제출 및 cleanup 각 1회를 검사했다. Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android target36으로 관련 `RevivalDeletionUITests`만 첫 실행 **12/12 통과**, 실패 0을 확인했다. XML SHA-256은 `395d95ee3b14e22bfd79a407cd3b2851be17cbe3bf56d9cba435c366b12503f0`이다. 검증 전 에셋 verified4561/copied0·서비스 설정 제외, 검증 후 해당 Editor 종료·snapshot 설정 복원·clean 상태를 확인했다. 수정된 코드는 새 APK로 빌드하거나 실제 온라인 삭제를 반복하지 않았다.

운영/기존 PGS·IAP 계정과 회사 Google 결제는 이번 시험에 사용하지 않았다. 구매 복원·운영 계정 인증·제공자 전체 보관 데이터 소거·수정 후 기기 UI·스토어·최종 출시 AAB·16KB 검증은 미검증이다. APK SHA-256은 위 온라인 시험 소스의 결과이며 예외 수정 커밋의 APK 결과가 아니다. 계정 식별자·키·개인 화면·원시 로그·정확한 원격 설정 내용과 로컬 감사 기록은 공개하지 않는다.
