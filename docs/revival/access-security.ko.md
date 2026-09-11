# 저장소 접근 권한과 병합 운영

점검일: 2026-09-11. 관련: [로드맵 #90](https://github.com/ChoiDaeYoung-94/Tamer/issues/90),
[권한 감사 #94](https://github.com/ChoiDaeYoung-94/Tamer/issues/94).
공개 가능한 설정과 검증 요약은 [설정 기록](access-security-settings.json)에 있다.
계정별 앱·자격증명의 상세 목록과 원시 응답은 공개하지 않는다.

## 확인한 권한 경계

저장소는 개인 소유의 public 상태를 유지한다. 누구나 열람·clone·fork할 수 있고,
GitHub 계정으로 이슈·댓글·외부 PR을 제안할 수 있다. 이러한 공개 참여는 원본 저장소의
push·merge·release·관리 권한을 부여하지 않는다. 이번 작업은 외부 제안을 제한하지 않았다.

인증된 GitHub REST API에서 collaborator 목록은 소유자 1명(admin)만 반환했다.
대기 초대, deploy key, repository webhook, self-hosted runner는 각각 0개였다.
목록은 페이지네이션을 포함해 조회했다. 새 협업자·키·앱 권한은 추가하지 않았다.

이 결과는 **무권한 방문자에게 쓰기 권한이 없다는 확인**이다. 소유자가 이전에 승인한 앱과
OAuth 자격증명은 별도의 권한 경로다. 관리 권한을 가진 소유자 인증은 보호 규칙 자체를
수정할 수 있으므로, 브랜치 보호를 계정 탈취나 모든 기존 연동의 차단으로 해석하지 않는다.
GitHub의 [OAuth 권한 설명](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/scopes-for-oauth-apps)도
권한 범위가 소유자의 기존 권한 안에서 동작한다고 설명한다.

## 실제 적용한 설정

| 항목 | 적용 전 | 적용 후 |
| --- | --- | --- |
| 공개 범위 | public | public 유지 |
| main 보호 | protected=false, ruleset 없음 | active ruleset 22866324, protected=true |
| main 변경 | PR 필수 아님 | PR 필수 |
| 필수 승인 | 없음 | 0명; Code Owner·마지막 push의 타인 승인 요구 없음 |
| 리뷰 대화 | 필수 해결 규칙 없음 | 코드 리뷰 대화 해결 필요 |
| main 강제 push·삭제 | 보호 규칙 없음 | 금지 |
| 규칙 우회 | 보호 규칙 없음 | bypass actor 없음; 소유자도 current_user_can_bypass=never |
| 필수 CI·배포 검사 | 없음 | 없음; CI 보류 중 외부 검사로 병합을 잠그지 않음 |
| 병합 방식 | merge/squash/rebase | 모두 유지 |
| repository Actions | enabled=true | enabled=false |
| 기본 GITHUB_TOKEN | write | read |
| Actions의 PR 승인 | true | false |
| 외부 fork 기여자 실행 승인 | first_time_contributors | all_external_contributors |

[실제 main 규칙](https://github.com/ChoiDaeYoung-94/Tamer/rules/22866324)은
`refs/heads/main`에 적용된다. 다른 브랜치로 기본 브랜치를 바꾸는 경우 새 대상을 별도로 점검한다.
GitHub가 반환한 전체 규칙 파라미터는 설정 기록에 보존했다.

규칙 생성 후 `rulesets/22866324`, `rules/branches/main`, `branches/main`을 다시 조회해
활성 규칙과 보호 상태를 확인했다. Actions 설정도 별도 GET으로 재확인했으며,
당시 실행 중·대기 중 workflow run은 각각 0건이었다.
운영 main에 시험용 push·강제 push·삭제·시험 병합을 시도하지 않았다.

설정 의미와 REST API는 GitHub의
[사용 가능한 규칙](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets),
[규칙 REST API](https://docs.github.com/en/rest/repos/rules),
[Actions 권한 REST API](https://docs.github.com/en/rest/actions/permissions)를 기준으로 확인했다.

## 1인 운영과 통합 담당의 병합

1. 작업 브랜치에서 작은 변경을 커밋하고 `main` 대상 PR을 만든다. 공개할 파일을 명시적으로 stage하고 비밀·구매 에셋·원시 로그를 제외한다.
2. 통합 담당은 PR의 최신 head SHA, 의존 PR, 검증 결과, 미해결 리뷰 대화와 충돌을 확인한다. draft PR은 검증이 끝난 뒤 ready로 전환한다.
3. 코드 변경은 관련 로컬 테스트·빌드 결과를 검토하고, 마지막 검증 이후 head가 바뀌면 필요한 검증을 다시 수행한다. 문서 변경에는 내용·링크·diff 검사를 적용한다.
4. head SHA를 고정해 일반 PR merge API나 UI로 병합한다. 필수 승인은 0명이므로 소유자가 만든 PR도 다른 사람의 승인 없이 병합 가능하다. 기존 변경 요청·충돌·미해결 대화가 있다면 원인을 해결한다.
5. 응답의 merged 상태와 실제 main SHA를 확인하고 이슈에 결과를 기록한다. 보호 규칙 해제나 admin 우회를 일상 병합 방법으로 사용하지 않는다.

자동화는 기존에 승인된 소유자 인증 또는 PR 병합 권한이 있는 앱을 사용한다.
새 토큰을 만들거나 앱에 관리 권한을 추가할 필요가 없다. GitHub Actions의 PR 승인 권한을
끄는 것은 별도로 연결된 Codex 앱의 PR 작성·병합 권한을 제거하는 동작이 아니다.
실제 최종 병합은 통합 담당이 맡으며, 이번 감사에서 다른 작업의 PR을 병합하지 않았다.

## Actions와 기존 배포 구성

사용자가 CI/CD를 보류했으므로 저장소 Actions를 설정에서 비활성화했다.
기존 workflow·App Center·fastlane 구성, 저장된 비밀 및 서명키는 수정하거나 삭제하지 않았다.
workflow 파일의 `active` 표시와 저장소 전체 `enabled=false`는 별개이며,
현재 실행 가능 여부는 저장소 설정을 함께 확인해야 한다.

기본 토큰의 read 설정은 향후 workflow가 명시적으로 요청하는 쓰기 권한을 모두 금지하는
상한선이 아니다. 재개 전에 workflow의 `permissions`, 외부 코드 checkout·스크립트 실행,
`pull_request_target`, secret 전달, runner 격리와 Action 버전 고정을 다시 검토한다.
이번 작업에서는 runner 등록, dispatch, 재실행, 배포, 새 서비스 이행을 하지 않았다.

## 앱·토큰 감사와 남은 정비

GitHub 설치 API의 401/403은 해당 인증 유형으로 조회할 수 없다는 결과였다.
이를 앱이 없다는 증거로 삼지 않고, 소유자가 GitHub의 sudo 재인증을 완료한 뒤
로그인된 설정 화면에서 설치 앱 2개, 사용자 승인 GitHub 앱 1개, OAuth 앱 6개, classic PAT 0개,
fine-grained PAT 0개를 확인했다. 토큰 문자열·비밀번호는 출력하거나 저장하지 않았다.

승인된 Codex 앱은 코드·이슈·PR·Actions·workflow 쓰기 권한을 가지며 관리 권한은 없었다.
계정 전체에 설치된 기존 개발 도구와 OAuth 연결에는 넓은 권한이 남아 있다.
사용 목적과 다른 저장소에 미치는 영향을 확인하지 않고 일괄 해지하거나 범위를 축소하지 않았다.
App Center 관련 구성과 연결도 보존했다.

종료된 관리자 권한 앱은 공식 종료 사실을 확인한 뒤 복구 가능한 설치 일시중지로 차단했다.
브라우저 자동화가 확인창을 처리하지 못해 소유자가 확인을 완료했으며,
설정 화면의 2026-09-11 12:47 KST 중지 안내와 `Unsuspend` 상태를 직접 확인했다.
설치 해제나 기존 저장소 삭제는 하지 않았다.
근거: [GitHub Learning Lab 종료 공지](https://github.blog/changelog/2022-08-31-deprecating-learning-lab/).
기존 OAuth의 현재 사용 필요성과 불필요한 권한 정리도 별도 소유자 검토 대상이다.
따라서 이 기록만으로 모든 기존 자격증명이 최소 권한이라고 선언하지 않는다.

## 기존 Android 서명키

추적된 `src/AeDeong.keystore`는 기존 Android custom signing 설정에서 참조되며,
기준 main과 검토한 기준선 PR에서 같은 Git 객체다. `BuildScript`는 기존 서명 환경변수
또는 이미 입력된 Editor 값을 사용하며, 서명 입력이 없으면 실패한다.
키를 열거나 비밀번호를 조회하지 않았고 파일·alias·서명을 변경하지 않았다.

이 키가 Play의 upload key인지 app signing key인지, 또는 두 역할을 겸하는지는 미확인이다.
기준 개발 APK는 debug 서명을 쓰므로 운영키의 유효성이나 스토어 관계를 입증하지 않는다.

1. 기존 키와 복구 가능한 비공개 백업을 보존한다.
2. Play App Signing 가입 상태를 확인하고, 로컬의 공개 인증서와 Play Console의 upload/app signing 인증서를 소유자 환경에서 대조한다. 공개 문서에는 일치 여부만 기록한다.
3. upload key만 해당하면 비공개 업로드키와 공식 reset 절차를 검토한다. app signing key까지 해당하면 기존 설치 앱의 업데이트와 인증서 연동 서비스, 공식 키 업그레이드 경로를 먼저 검토한다.
4. 대체 서명 경로와 복구 절차를 검증한 후 추적 제외·보관 위치·공개 이력 대응을 별도 변경으로 결정한다. 현재 파일 삭제나 비밀번호 변경만으로 이미 공개된 이력의 노출이 해소되지는 않는다.

근거: [Android 앱 서명](https://developer.android.com/studio/publish/app-signing),
[Play App Signing과 업로드키 reset](https://support.google.com/googleplay/android-developer/answer/9842756?hl=en).
이번 작업은 기존 키의 삭제·회전·이력 재작성·스토어 변경을 수행하지 않았다.

## 변경 범위와 의존성

문서 브랜치의 기준은 `main c47c90217d45e8c6538c57a0924fa852abfbd736`이다.
[PR #95](https://github.com/ChoiDaeYoung-94/Tamer/pull/95)의
`c1613ab0fa7f6f3b8971e95410a12c52af5089b4`에서 `AGENTS.md`,
`tools/revival` 및 기준 검증 문서를 읽었다. 이 문서 PR은 #93/#95의 코드를 포함하거나
cherry-pick하지 않으므로 병합 선행 의존성은 없다. #95의 예비 권한 수치는 해당 작업 시점 기록이며,
이 감사의 실제 적용 결과가 더 최신이다.

앱 코드·CI 파일·에셋·GUID·서명키 변경이 없는 문서 작업이므로 Unity Editor·빌드는 실행하지 않았다.
다른 checkout의 미커밋 파일·브랜치·Editor·Library와 소유자의 원본은 변경하지 않았다.
빌드 성공·기기 검증·스토어 승인은 이 감사의 완료 기준에 포함하지 않는다.

설정은 서버에서 이미 적용되며 문서 PR 병합과 독립적이다. 되돌림이 필요하면 소유자 admin으로
위 규칙과 Actions 설정의 변경 전·후 기록을 대조해 필요한 항목만 수정한다.
감사 전의 무보호 main 또는 쓰기 토큰 상태로 일괄 복원하는 절차는 제공하지 않는다.
#94는 기존 OAuth 필요성 검토 및 서명 관계 확인을 완료할 때까지 자동 종료하지 않는다.
