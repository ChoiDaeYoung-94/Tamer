# 서명 역할 확인과 비공개 에셋 복구

## 2026-09-30 실제 키의 로컬 암호화 백업 완료

사용자 승인으로 준비된 기존 키·공개 인증서·복구 메모 세 파일의 실제 백업을 한 번
수행했다. 실행 checkout은 아래와 같은 `completion-state-refresh/Tamer`, 소스 커밋은
`f44cb782c46fce7cc36186e3654abe88e7edf82b`(PR #274에 통합)이며 도구 raw SHA-256은
아래 합성 실행과 같다. 사용자가 새 Pinentry 창에서 암호를 직접 입력했다.

실행 manifest는 verified이고 receipt는 `verified=true`, `sourceUnchanged=true`다.
정확한 암호문을 메모리에서 복호화하여 세 역할과 manifest의 목록·내용 해시를 검사한 뒤
입력 snapshot 불변과 전용 agent 종료 성공을 확인하는 코드 경로로 receipt를 게시했다.
후속 읽기에서 4,955바이트 암호문과 receipt의 SHA-256 일치, 실행 도구 해시 일치,
실패 진단 파일 부재와 GPG·agent·pinentry 프로세스 0을 확인했다. 원본 키를 후속
검토에서 다시 읽거나 복호화를 반복하지 않았다. 원본 불변의 근거는 해당 실행의 검사와
receipt이며, 이후 모든 시점의 파일 불변을 보장하지 않는다.

기존 키는 준비 당시 해시와 사전 대조했다. 인증서·메모는 정확한 준비 경로를 고정하고
collect 시점 snapshot 및 종료 시 불변을 검사했으며, 준비 당시 해시가 따로 고정됐다고
주장하지 않는다. 기존 키·준비 자료·실패 증거·합성 백업은 모두 보존했다.

사용자에게 전달하는 파일은 같은 새 소유 ACL 폴더의 `recovery.zip.gpg`와
`verified-receipt.json` 두 개다. 전자는 암호화된 복구 자료이고 후자는 비밀번호·키 원문이
없는 해시 확인용 영수증이다. 정확한 개인 경로는 비공개로 안내하며 공개 저장소에
암호문·영수증 사본·원시 로그를 올리지 않는다. 비밀번호는 별도로 안전하게 보관한다.

Drive에는 사용자가 두 파일을 직접 업로드한다. 다시 내려받은 암호문의 SHA-256을
receipt의 `encryptedSha256`과 비교한 후, 새 로컬 입력으로 복호화·내용 대조가 끝나야
외부 복구 검증 완료로 기록한다. 현재 Drive 전송·재다운로드는 하지 않았으며
`offsiteVerified=false`, `keystorePasswordVerified=false`를 유지한다. 새 백업 암호는
기존 JKS 암호를 복구하거나 대체하지 않는다. 아래 준비/미완료 표현은 당시 이력이다.

## 2026-09-30 단축 GPG 홈 합성 검증

현재 결과: 사용자 승인으로 합성 자료 세 개만 사용한 로컬 pinentry 암호화·복호화
검증 한 번이 종료 코드 0으로 완료됐다. 실제 키의 검증된 암호화 백업과 Drive 복구는
아직 완료하지 않았다. 아래 9월 29일 준비/미실행 표현은 당시 이력이다.

checkout은 `C:/Users/pc_17/.codex/worktrees/completion-state-refresh/Tamer`, branch는
`codex/backup-pinentry-diagnostics`, 기반 HEAD는 `a049310d9e7031cdebbf5e706efd39ad5340df8b`다.
실행 소스는 미커밋 `prepare_encrypted_key_backup.py`이며 raw SHA-256은
`f74feb258ff6dafff8655756eb0899503941efcbfdf36ca0bf90bd62bb47c7af`다.
Python 3.14.0, 기존 Git GPG 2.4.7과 pinentry-w32를 사용했다. Unity·Android는 실행하지 않았다.

앞선 실제 키 백업 시도는 입력창을 확인하지 못하고 실패했으며 stderr가 없어 원인은
미확정이다. 별도 합성 진단에서는 agent가 browser socket 경로 길이를 거절한 원문을
확인했다. 두 실패의 원인을 같다고 확정하지 않는다. 새 홈 이름을 `g`로 줄이고 MSYS
browser socket 경로가 UTF-8 100바이트를 넘으면 실행을 거절하도록 했다. 이 수치는
보수적인 가드이며 모든 사용자 경로의 성공이나 공식 보편 한계를 보장하지 않는다.
GPG/cleanup 실패 stderr는 새 소유 ACL 폴더에만 exclusive 파일로 남긴다.

독립 읽기 검토 후 승인된 실행의 사전 검사에서 합성 파일에 없는 줄바꿈을 기대해
한 번 중단됐다. 이때 소스 호출·marker·새 폴더·GPG 실행은 0이었다. 실제 합성 bytes에
맞게 runner 기대값만 고친 뒤 exclusive marker와 소스 해시 고정으로 GPG를 한 번 호출했다.
사용자가 실제 Pinentry 창에서 직접 암호를 입력했으며 도구는 암호를 읽거나 입력하지 않았다.

- receipt `verified=true`, `sourceUnchanged=true`, 암호문 SHA-256 일치를 확인했다.
- 세 입력의 `PUBLIC SYNTHETIC <role>` bytes와 실행 소스 해시는 종료 후에도 같았다.
- 암복호화 결과의 정확한 목록·내용 해시를 대조했고 전용 agent 종료 성공 후에만 receipt를 게시했다.
- 종료 뒤 별도 읽기 관측에서 GPG·gpg-agent·pinentry 프로세스와 Pinentry 창은 0이었다.
- 실패 진단 파일은 없으며 `offsiteVerified=false`, `keystorePasswordVerified=false`다.

ignored `Logs/revival/pinentry-short-home-once.py`, 실행 manifest와 결과 JSON, 소유 ACL의
합성 암호문·영수증은 로컬에 보존한다. 이번 실행에서 실제 키 읽기/암호화·Drive 접근·
기존 성공 시험 반복은 하지 않았다. 합성 성공을 실제 키·외부 복구 성공으로 표시하지 않는다.

## 2026-09-29 기존 키 암호화 백업 준비

사용자는 기존 키 보존과 암호화 묶음 준비 후 Drive 직접 업로드를 선택했다.
기존 JKS 비밀번호는 현재 모르는 상태이며, 아래 과거 새 키/reset 초안은 이번 실행 범위가
아니다. 새 **백업 암호**는 보관 파일을 여는 암호로 기존 **JKS 암호**를 대체하지 않는다.

`tools/revival/prepare_encrypted_key_backup.py`는 이미 설치된 Git의 GPG와 Windows
pinentry를 사용한다. 기존 키·공개 인증서·복구 메모의 정확한 파일 경로 세 개를 받아
메모리에서 묶고 암호화하며 평문 ZIP을 디스크에 쓰지 않는다. 비밀번호 인자는 받지 않고
사용자가 로컬 GPG 입력창에서 직접 입력한다. 출력은 사용자 홈의 `TamerPrivateBackups`
아래 새 디렉터리로 제한하고, 현재 사용자와 SYSTEM만 접근하도록 상속 권한을 제거한다.
링크·정션과 중복 입력 파일을 거절하며 기존 파일을 덮어쓰지 않는다.

```powershell
python tools/revival/prepare_encrypted_key_backup.py --keystore '<확인한 기존 키>' --certificate '<확인한 공개 인증서>' --recovery-note '<비공개 복구 메모>'
```

실행 전 공개 인증서의 역할과 메모 내용을 확인해야 한다. 이 도구는 세 파일이 서로 같은
키를 설명하는지 인증하거나 JKS 개인키를 복호화하지 않는다. 복구 메모에는 역할·alias·
공개 인증서와 원본 파일 해시·복구 절차를 적고 암호·토큰은 넣지 않는다.

암호화 후 캐시를 사용하지 않는 복호화로 정확한 파일 목록과 각 파일 해시를 대조하고,
읽은 원본 snapshot과 종료 시 파일 identity·내용 일치까지 확인한다. 같은 암호문 bytes를
복호화·해시에 사용하고 게시 직전 파일을 재대조하며, 작업 전용 GPG agent 종료 성공 후
`verified-receipt.json`을 원자적으로 게시한다. 이는 검사 시점의 일치이며 파일 잠금이나
이후 변경 방지를 뜻하지 않는다. 영수증은 단순 존재가 아니라 정상 JSON의 `verified=true`와
현재 암호문 SHA-256 일치를 확인해야 한다. 성공 안내의 `recovery.zip.gpg` 하나만 사용자가 Drive에
직접 업로드하며 암호는 별도 암호 관리자에 보관한다. 재다운로드 사본의 해시·복호화
대조 전에는 외부 복구 완료로 표시하지 않는다. 기존 키와 증거는 보존한다.

합성 파일로 실제 GPG 스트림 왕복·암호문 변조 거절·파일 목록·원본 변경 감지 2건을
통과했다. 첫 실행은 MSYS GPG agent의 Windows 드라이브 콜론 경로 거절로 실패했고,
MSYS 경로로 변환한 두 번째 실행에서 통과했다. 실제 키 암호화, 사용자 pinentry 입력,
실제 출력 ACL 확인, Drive 업로드·다운로드 복구는 아직 수행하지 않았다.
보안 리뷰 후 실패 경로 mock 2건을 추가하여 ACL 실패 시 암호화 미호출, 입력 취소,
agent 종료 실패, 암호문 교체 시 성공 영수증 미생성과 영수증 쓰기 실패 정리를 확인했다.
기존 GPG 왕복 시험은 반복하지 않았다. 실제 실행은 ACL 설정 뒤 DACL을 다시 읽어 검사한다.
여기서 자동 정리는 해당 작업의 agent 종료와 미완 영수증 삭제만 뜻한다. 실패한 암호문과
작업 전용 GPG 홈은 점검을 위해 남으며 성공 영수증 없이 업로드하지 않는다.
후속 실제 빈 디렉터리 DACL 검증은 두 번 실패해 중단했다. 현재 셸에서 설정된 권한을
읽으면 사용자·SYSTEM FullControl 및 상속 차단이었지만, Python에서 호출한 Windows
PowerShell 검증은 exit 1이었다. EncodedCommand 변경만으로 해결되지 않았다.
호스트 pwsh 모듈 경로가 자식 Windows PowerShell에 상속되는 가능성을 확인해 표준
자식 환경에서만 PSModulePath를 제거하고 Windows PowerShell 절대 경로와 단계별
오류 코드를 사용하는 수정안을 준비했으나 원인 확정·3차 검증은
사용자 승인 전 보류다. 이 상태에서는 실제 키 백업 도구의 준비 완료를 선언하지 않는다.

이후 사용자 승인으로 `b9eed7b098eb1a4807aa7439e012c3e9d0c49087`의 수정안을 사용해
빈 신규 소유 폴더의 실제 ACL 검증을 세 번째로 **1회 실행해 통과**했다. 현재 사용자와
SYSTEM의 FullControl 두 규칙 및 상속 차단을 도구가 직접 확인했다. 비공개 결과를
보존했으며 시험 폴더 삭제는 자동 승인 검토에서 차단되어 폴더를 남겼다.
앞선 실패는 보존하며, 자식 환경 격리 후 성공한 사실과 과거 stderr가 없어 원인을
확정하지 못한 점을 구분한다. 기존 합성 GPG·mock 검증은 반복하지 않았다.
실제 키 암호화·사용자 pinentry·공개 인증서/복구 메모의 정확한 입력 확인·Drive 복구는
여전히 미실행이다.
GPG의 [대칭 암호화](https://www.gnupg.org/documentation/manuals/gnupg/Operational-GPG-Commands.html)와
[암호 캐시 옵션](https://www.gnupg.org/documentation/manuals/gnupg/GPG-Esoteric-Options.html)을 따른다.

확인일: 2026-09-11. 기준 코드: `4e8c0398c700bc133d3b1e8d6592fca24f0b4d89`.
관련: [#94](https://github.com/ChoiDaeYoung-94/Tamer/issues/94),
[#92](https://github.com/ChoiDaeYoung-94/Tamer/issues/92),
[접근 권한 감사](access-security.ko.md).

## 현재 키의 역할

**현재 Monster Tamer의 Play Console 기준으로, Git에 추적된 JKS는 업로드 인증서와 일치하고 앱 서명 인증서와 다르다.**

| 비교 | 확인 방법 | 결과 |
| --- | --- | --- |
| 로컬 JKS ↔ Play 업로드 인증서 | SHA-256 및 SHA-1 대조 | 모두 일치 |
| 로컬 JKS ↔ Play 앱 서명 인증서 | 앱 서명 카드에서 받은 공개 DER의 SHA-256 및 SHA-1 대조 | 모두 다름 |
| 공개 DER ↔ Console의 앱 인증서 정보 | 같은 앱의 디지털 애셋 링크 JSON SHA-256 대조 | 일치 |
| 키 파일 변경 여부 | 읽기 전후 SHA-256 비교 | 변경 없음 |

기존 로그인으로 `com.AeDeong.MonsterTamer`의 **Google Play로 보호됨 → Play 스토어 보호 → Play 앱 서명 관리**를 열었다.
화면의 앱 서명 키는 사용 중이었다. `deployment_cert.der`라는 파일명으로 역할을 추측하지 않고,
앱 서명 카드의 공개 인증서 다운로드와 Console 정보를 기준으로 구분했다.
전체 지문과 계정 식별자는 공개 문서에 복사하지 않았다.

공개 DER는 다운로드 폴더에서 역할이 명확한 파일명으로 정리한 뒤 worktree 밖의 고정된 소유자용
로컬 보관 영역으로 옮겼다. 복사·동일 해시·DER 읽기와 원본 불변을 검증한 뒤 이전 파일만 제거했고,
비공개 역할·이동·ACL 기록도 같은 고정 영역에 보관했다. 기존 JKS는 이동하거나 변경하지 않았다.

로컬 인증서는 고정 JDK의 `keytool -list -v -keystore <기존 JKS>`에 빈 입력을 전달해 읽었다.
JKS의 공개 인증서 1개를 읽을 수 있었으며 비밀번호 조회, 개인키 복호화·내보내기를 하지 않았다.
이 방식은 JKS 무결성 인증을 수행하지 않아 도구가 경고한다. 따라서 **공개 인증서의 역할 대조 성공**을
비밀번호 보유, 개인키 사용 가능성 또는 운영 서명 빌드 성공으로 해석하지 않는다.
공개 DER는 `keytool -printcert -file <공개 DER>`로 확인했다.

이는 이 앱의 현재 Console 상태에 대한 판정이다. 다른 스토어·앱 또는 과거 배포에서 동일 키를
사용했는지까지 조사한 결과는 아니다. 이번 작업은 업로드·키 업그레이드·reset 요청을 제출하지 않았다.

## 실행 전 검토할 업로드키 대응 초안

공개 Git 이력에 암호화된 JKS가 남아 있으므로 향후 새 업로드키로 분리하는 변경을 준비한다.
브랜치 보호, 최신 파일 삭제 또는 기존 비밀번호 변경만으로 과거 복제본의 노출을 되돌릴 수는 없다.

1. 소유자의 비공개 보관소에 기존 키의 복구 가능한 사본과 현재 인증서 관계 기록을 보존한다. 이 단계에서 기존 키를 삭제하지 않는다.
2. 새 업로드키는 Git 밖의 접근 제한·암호화된 위치에 생성하고 비밀번호는 별도 안전한 경로로 관리한다. 새 키 생성·등록은 이 초안의 실행 승인 이후 수행한다.
3. Play Console에서 새 **업로드 인증서**의 reset을 요청하고 실제 활성 시점·승인 결과를 확인한다. 기존 **앱 서명키 업그레이드**를 대신 누르지 않는다.
4. 기존 빌드는 `BuildScript`의 고정 `src/AeDeong.keystore` 경로와 alias를 사용했다. AAB 메뉴는 아래의 외부 경로·alias·기대 공개 인증서 지문을 명시적으로 받도록 준비한다. 기존 비밀번호 환경변수는 유지하되 값은 로그·코드·커밋에 남기지 않는다.
5. 새 업로드 인증서가 활성화된 뒤 로컬 서명과 허용된 비운영 검증을 수행한다. 별도 제출 승인 전에는 AAB 업로드·트랙 배포를 하지 않는다. reset 활성화 후에는 과거 업로드키로 되돌려도 업로드가 가능하다고 가정하지 않는다.
6. 새 경로로 빌드·복구할 수 있음을 확인한 뒤 추적 제외와 공개 이력 대응 범위를 통합 담당이 정한다. 이력 재작성은 열린 작업·fork·clone에 영향을 주므로 별도 계획과 조율 없이 실행하지 않는다.

업로드키 reset은 Google이 관리하는 앱 서명키를 변경하지 않는다.
따라서 Play가 배포하는 APK의 인증서와 기존 설치 앱의 업데이트 연속성은 업로드키 변경과 구분한다.
반면 로컬에서 새 업로드키로 직접 서명한 빌드를 사용하는 로그인/API 등록은 새 SHA 지문을 필요로 할 수 있다.
GPGS·OAuth·App Links 등 인증서에 묶인 연동은 실제 배포 인증서별로 대조해야 한다.
근거: [Android 앱 서명](https://developer.android.com/studio/publish/app-signing),
[Google Play 앱 서명 관리](https://support.google.com/googleplay/android-developer/answer/9842756?hl=en).

### AAB 빌드 입력 준비

`Build/AOS/AAB` 메뉴는 다음 환경 변수가 모두 있을 때만 운영 앱 ID의 AAB 빌드를 시작한다.
이 코드는 새 키를 만들거나 Play Console에 인증서를 등록하지 않는다.

| 환경 변수 | 내용 |
| --- | --- |
| `TAMER_UPLOAD_KEYSTORE_PATH` | Git 작업 폴더 밖에 있는 기존 키 저장소의 절대 경로 |
| `TAMER_UPLOAD_KEY_ALIAS` | 해당 저장소의 alias (`A-Z`, `a-z`, 숫자, `.`, `_`, `-`만 허용) |
| `TAMER_UPLOAD_CERT_SHA256` | 별도로 검토한 업로드 공개 인증서의 SHA-256 지문(64자리 16진수 또는 콜론 표기) |
| `TAMER_KEYSTORE_PASS`, `TAMER_KEYALIAS_PASS` | 기존 이름을 유지한 비밀번호 환경 변수. 실제 값은 문서·로그·커밋에 기록하지 않음 |

메뉴 선택과 실제 빌드 시작 시 각각 입력을 확인한다. 없는 파일, 상대 경로, Git 폴더 안의 경로,
심볼릭 링크·정션 경유 경로, 잘못된 alias·지문·저장소 비밀번호 또는 기대 지문과 다른 인증서라면 버전 파일이나
Player Settings를 변경하기 전에 메뉴 진입을 중단한다. 첫 검사를 통과한 뒤 입력이 바뀌어 두 번째 검사에서
실패하면 빌드 단계의 버전·서명 설정 변경 전에 중단하고 메뉴가 준비한 표시 파일과 scripting defines를 정리한다.
준비 전 define과 요청 토큰은 Editor의 `SessionState`에 저장하여 define 변경에 따른 도메인 리로드를 견딘다.
기존 APK/AAB/완료 표시가 있으면 덮어쓰지 않고 새 요청을 거절한다. 정리는 해당 토큰의 표시만 삭제하며,
현재 define이 이번 요청이 적용한 값일 때만 이전 값으로 복원한다. 사용자가 중간에 변경한 다른 define은 보존한다.
성공한 빌드의 릴리스 define은 유지한다. 실패·취소 또는 실행 도중 리로드로 중단된 요청은 자동 재시도하지 않는다.
준비 snapshot이 없는 기존·고아 표시는 자동 빌드로 재개하지 않고 보존하여 오류를 보고한다.
메뉴 준비는 batch에서 거절하며 기존 CI/CD 보류를 유지한다.
Unity가 사용하는 JDK의 `keytool -exportcert`로
alias의 **공개 DER만** 읽어 지문을 비교하며 개인키를 내보내지 않는다. 빌드 중 적용한
키 경로·alias·비밀번호는 완료 또는 실패 후 이전 Editor 설정으로 복원한다.

기대 지문은 운영자가 Play Console의 **현재 활성 업로드 인증서**와 직접 대조해야 한다.
환경 변수 두 값을 서로 맞게 입력했다는 사실만으로 Play의 reset 활성화·업로드 허용을
증명하지 않는다. 기존 `Build/AOS/APK` 개발 메뉴와 격리 `Revival*` 빌드 경로,
추적된 기존 `src/AeDeong.keystore`는 이 변경에서 유지한다.

검증 기준은 `3f155f9a0cd391886d6355cd2759a117cf737818`에서 분기한
`codex/upload-key-config-94` 작업본이다. Unity `6000.0.81f1`, Android Editor 대상,
Unity CLI `1.0.0-beta.8`에서 복원 에셋 4,561개를 검증한 뒤
`RevivalUploadSigningTests` 4개를 통과했다. 첫 실행은 신규 테스트의 Editor assembly 참조 오류로
컴파일에 실패했고, 기존 테스트와 같은 reflection 방식으로 고쳐 두 번째 실행에서 4개 모두 통과했다.
검사 전후 `ProjectSettings`는 원본 바이트로 복원했다. 검증에는 일회용 합성 JKS만 사용했으며,
운영 키 서명·APK/AAB 생성·Play Console 제출 또는 reset 활성화 검사는 수행하지 않았다.

2026-10-01의 후속 원자성 수정은 `fce74849106f5606096dd113d62cb3b99c2a872b`에서 분기한
`codex/release-build-rollback`에서 수행했다. 합성 임시 경로·define으로 기존 표시 보존, 리로드 snapshot의
두 번째 검사 실패 정리, 취소 정리, 다른 소유자의 표시/define 보존, 성공 define 유지 및 표시 생성 경합을 검증했다.
`pwsh -NoProfile -File tools/revival/test_legacy_build_preparation.ps1`의 6개 검사가 통과했다.
실제 변경 C# 두 파일은 Unity `6000.3.25f1`의 Roslyn과 광고 담당의 읽기 전용 캐시 참조 392개로
Editor 실행 없이 컴파일하여 exit 0을 확인했다(기존 API 사용의 CS0618 경고).
첫 결과 수집은 Python의 cp949 출력 해독 오류로 실패했고, UTF-8 결과 수집으로 고쳐 두 번째에 exit 0을 기록했다.
독립 검토 후 빌드 후 처리도 소유 snapshot·완료 토큰과 성공 결과가 모두 있어야 Editor 성공 종료를 예약하도록 보완했다.
`pwsh -NoProfile -File tools/revival/test_legacy_build_preparation.ps1 -PostprocessOnly`로
snapshot 없음·완료 표시 없음·실패/취소·소유 성공·다른 소유자의 표시 5조건이 통과했으며,
이 delta의 실제 두 C# 소스 컴파일도 exit 0이다. 앞선 6개 검사를 변경 없이 반복하지 않았다.
이 검증은 실제 Unity 이벤트 루프·도메인 리로드 실행이나 릴리스 빌드를 검증한 결과로 대신하지 않는다.
운영 키 접근·서명·실제 APK/AAB·물리 기기·CI 실행은 모두 0회다.

### 새 업로드키의 Google Drive 백업 준비

새 키를 생성한 뒤 소유자 전용 Drive에 **로컬에서 암호화한 단일 보관 파일**을 올린다.
Drive의 기본 전송·저장 암호화만으로 이 보관 파일의 별도 암호화를 대신하지 않는다.
현재 단계에서는 새 키 생성, Drive 업로드, Play Console reset을 수행하지 않는다.

| 보관 파일 안의 항목 | 복구에 필요한 이유 |
| --- | --- |
| 새 업로드키 저장소 원본(`.jks` 또는 `.keystore`) | 개인키가 들어 있는 실제 서명 파일. 공개 PEM만으로는 서명 키를 복구할 수 없음 |
| 해당 키의 공개 `upload_certificate.pem` | Play Console 업로드키 reset 요청 및 활성 인증서 대조용. 새 키의 같은 alias에서 내보낸 파일이어야 함 |
| 비공개 복구 메모 | 앱 ID `com.AeDeong.MonsterTamer`, 키 저장소 형식, alias, 생성일, 공개 인증서 SHA-256, 키 저장소 원본 파일의 SHA-256, Play reset 요청·활성화 확인일, 백업 검증일을 기록. 실제 값은 공개 Git 문서에 복사하지 않음 |

보관 파일을 여는 암호와 `TAMER_KEYSTORE_PASS`·`TAMER_KEYALIAS_PASS`의 실제 값은
**Drive 및 보관 파일과 분리된** 소유자의 암호 관리자에 저장한다. 세 암호가 같더라도 각 역할과
복구 위치를 따로 식별한다. `TAMER_UPLOAD_KEYSTORE_PATH`는 복원한 컴퓨터의 Git 밖 절대 경로이므로
백업의 고정 경로로 사용하지 않는다. 복원 후 그 경로, 메모의 alias·공개 인증서 지문과
별도 보관한 두 비밀번호를 환경 변수에 지정한다.

업로드 전 암호화된 파일의 열기·복호화를 확인하고, Drive에서 다시 받은 사본으로
원본 저장소 해시·공개 PEM 지문·alias를 대조해 독립 복원을 확인한다. 확인이 끝나기 전에는
로컬 원본이나 기존 `src/AeDeong.keystore`를 삭제하지 않는다. Drive 파일은 소유자 전용으로 두고
공유 링크를 만들지 않으며, 평문 키 저장소·암호·복구 메모를 Git·이슈·로그에 남기지 않는다.

## 기존 OAuth 연결의 필요성

소유자가 현재 **Fork를 사용 중**이라고 확인했다. Fork, 이번 Git 작업에 사용되는 기존 인증,
모바일 인증 및 보존 요청한 App Center 연결은 유지 대상이다.
다른 기존 개발 도구의 현재 사용 여부는 답변으로 확인되지 않았으므로 미사용이라고 단정하지 않는다.
기존 연결의 실제 넓은 권한과 사용 여부를 대조한 후, 필요 없는 연결만 소유자 범위에서 개별 정리한다.
이번 2단계에서 OAuth revoke, 신규 권한 부여 또는 운영 권한 변경은 하지 않았다.

## 비공개 에셋 스냅샷

현 manifest의 `private-restore` 항목은 **4,204개, 574,358,085바이트**다.
SDK는 현재 커밋에서 Git으로 복원하며 서비스 설정과 서명 재료는 에셋 사본에 포함하지 않는다.
기존 D: 원본을 읽기 전용으로 유지하고 C:에서 사본을 검증한 뒤,
worktree 밖의 고정된 소유자용 로컬 보관 영역에 최종 사본과 대응 manifest를 보관했다.
이 PC에서 C:와 D:는 서로 다른 물리 디스크지만 같은 컴퓨터 안에 있다.

`backup_private_assets.py`는 지정된 파일 전체의 원본 해시를 먼저 검사한 뒤 새 대상 폴더에만 복사한다.
완료 후 대상의 파일별 해시와 정확한 파일 목록을 검사하고 `private-assets-receipt.json`을 기록한다.
영수증은 현재 manifest의 정규 JSON SHA-256, 개수·바이트 수, SDK/서비스/서명 제외 방식을 담는다.
객체 키 정렬과 공백 없는 UTF-8 JSON으로 해시하므로 Git의 CRLF/LF 변환만으로 사본 검증이 실패하지 않는다.
manifest가 바뀌거나 파일이 누락·변조·추가되면 검증이 실패한다.
사본 생성 실패 시 일부 파일이 남을 수 있지만 완료 영수증이 없는 폴더를 유효한 백업으로 취급하지 않는다.
원본·기존 대상 폴더를 덮어쓰거나 자동 삭제하지 않으며 `--inventory`로 해시를 재생성하지 않는다.

```powershell
python tools/revival/backup_private_assets.py --source '<권한 있는 원본>' --destination '<새 비공개 사본 폴더>'
python tools/revival/backup_private_assets.py --destination '<비공개 사본 폴더>' --verify
python -m unittest discover -s tools/revival -p test_backup_private_assets.py
```

복구 환경에는 먼저 같은 manifest를 가진 Git 커밋과 SDK를 checkout한다.
그 환경에서 다음 명령의 source를 사본 폴더로 지정하면 기존 복원 절차를 그대로 사용한다.

```powershell
python tools/revival/restore_assets.py --source '<비공개 사본 폴더>'
python tools/revival/restore_assets.py --verify
```

현재 에셋 스냅샷은 **평문 로컬 사본**이다. Git ignore는 암호화나 접근 통제가 아니다.
공개 Git/LFS·PR 첨부·CI artifact·일반 공유 폴더에는 업로드하지 않는다.
구매 증빙·패키지별 라이선스와 버전은 여전히 미확인이며, 해시 일치는 재배포 권한을 부여하지 않는다.

## 보관과 단일 장애점

초기 검증용 사본은 worktree 안에 있지만 최종 로컬 사본은 worktree 밖의 고정 영역에 있다.
worktree 정리에 의존하는 문제는 분리했지만, PC 외부의 독립 백업은 아직 없다.

| 단계 | 구체적인 보관·검증 조건 |
| --- | --- |
| 초기 검증 사본 | D: 원본과 별도 디스크 C:의 ignored 작업 영역. 최종 보관본으로 사용하지 않음 |
| 고정 로컬 사본 | worktree/Git 밖의 소유자용 로컬 폴더에 새 사본 생성, receipt와 manifest 해시 재검증. source commit과 대응 manifest 함께 기록 |
| PC 외부 사본 | 소유자가 접근을 통제하는 암호화 외장매체 또는 승인된 비공개 보관소. 키/복구 수단은 별도 보관. 구매 에셋의 이용 권한 범위 내에서만 복사 |
| 정기 복원 확인 | 원본 경로가 없는 새 checkout에서 사본만 source로 사용해 복원·해시 검증. 새 SDK는 Git에서 복원하고 구형 원본으로 대체하지 않음 |
| 폐기·정리 | 새 사본의 독립 복구를 검증한 후 보존 대상을 결정. 원본이나 마지막 정상 사본을 자동 삭제하지 않음 |

동일 PC 사본은 계정 침해·랜섬웨어·도난·전원 사고를 함께 겪을 수 있다.
현재 상속 ACL에는 사용자·SYSTEM·Administrators와 앱 capability의 FullControl,
로컬 에이전트 그룹의 읽기 권한이 있으므로 사용자만 접근하는 보관소라고 보장하지 않는다.
PC 외부 보관 위치와 구매 증빙은 아직 지정·확인되지 않았다.
새 클라우드 서비스 연결, 외부 업로드, 원본 이동, 디스크 설정 변경은 하지 않았다.

## 검증 범위

새 백업 도구 테스트 8개와 복원 도구 테스트 5개를 실행했다.
원본 불일치·기존 대상 보존·경로 이탈·서비스/서명 재료 제외·누락/변조/추가 파일·receipt 불일치를 검사한다.
첫 깨끗한 clone(`4e8c039`)은 manifest가 `git-sdk`로 요구하는 Editor 설정 `.meta`가 Git에서 빠져 실패했다.
서비스 `.asset` 제외는 유지하고 정확한 `.meta` 1개의 ignore 예외와 원본 바이트·GUID만 복구했다.
전체 `git-sdk` 357개가 실제 Git index에 있는지 확인하는 회귀 검사를 추가했으며, 수정 전 실패·후 통과했다.
원본 `.meta`의 빈 importer 필드에 있는 trailing whitespace 3줄은 기존 해시 보존을 위해 유지했다.
이를 제외한 변경의 diff 검사와 해당 `.meta`의 manifest SHA-256/GUID 대조를 별도로 통과했다.

수정 커밋 `76d83b124818e0859e30f8e19249eeaec44f6e9b`의 clone에서는 C: 사본만 source로 지정해
복원(기존 파일 362개 확인, 누락 파일·중립 템플릿 4,201개 생성), 최종 해시 검사 4,561개 및
복원 테스트 5개가 통과했다. clone의 tracked 변경은 없고 Library를 만들지 않았다.
최종 도구 구현 `7c38704d03e850b56dea8cd7ed1cee9db48abfb7`의 별도 깨끗한 clone에서도
고정 보관본의 receipt·4,204개 사본 검사, 동일한 복원/4,561개 해시 검사와 백업 8개·복원 5개 테스트가 통과했다.
통합 담당의 별도 코드 리뷰에서 확정 P1/P2 지적은 없었다.
최종 사본 생성·검증 기록은 [2단계 검증 기록](security-phase2-validation.json)에 있다.
Unity Editor, APK/AAB 빌드, 실제 로그인·구매·광고 및 스토어 제출은 실행하지 않는다.
