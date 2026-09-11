# 서명 역할 확인과 비공개 에셋 복구

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
4. 기존 빌드는 `BuildScript`의 고정 `src/AeDeong.keystore` 경로와 alias를 사용한다. 후속 구현에서는 외부 경로/alias를 명시적으로 입력받고 누락·잘못된 인증서에 실패하도록 변경한다. 기존 비밀번호 환경변수는 유지하되 값은 로그·코드·커밋에 남기지 않는다.
5. 새 업로드 인증서가 활성화된 뒤 로컬 서명과 허용된 비운영 검증을 수행한다. 별도 제출 승인 전에는 AAB 업로드·트랙 배포를 하지 않는다. reset 활성화 후에는 과거 업로드키로 되돌려도 업로드가 가능하다고 가정하지 않는다.
6. 새 경로로 빌드·복구할 수 있음을 확인한 뒤 추적 제외와 공개 이력 대응 범위를 통합 담당이 정한다. 이력 재작성은 열린 작업·fork·clone에 영향을 주므로 별도 계획과 조율 없이 실행하지 않는다.

업로드키 reset은 Google이 관리하는 앱 서명키를 변경하지 않는다.
따라서 Play가 배포하는 APK의 인증서와 기존 설치 앱의 업데이트 연속성은 업로드키 변경과 구분한다.
반면 로컬에서 새 업로드키로 직접 서명한 빌드를 사용하는 로그인/API 등록은 새 SHA 지문을 필요로 할 수 있다.
GPGS·OAuth·App Links 등 인증서에 묶인 연동은 실제 배포 인증서별로 대조해야 한다.
근거: [Android 앱 서명](https://developer.android.com/studio/publish/app-signing),
[Google Play 앱 서명 관리](https://support.google.com/googleplay/android-developer/answer/9842756?hl=en).

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
