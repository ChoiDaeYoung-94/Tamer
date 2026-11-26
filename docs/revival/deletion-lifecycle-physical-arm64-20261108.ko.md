# 가상 계정 삭제·재시작·새 저장 분리의 실물 검증 — 2026-11-08

기존 오프라인 v4 시험은 예전 영어 접수 안내를 찾고 있어 현재 한국어 UI를 정상으로 표시해도 관측이 진행되지 않았습니다. `RevivalDeletionGameplayHarness.cs`의 관측 문자열1개만 ‘삭제 요청이 접수되었습니다.’로 맞췄습니다. 접수 횟수·정리·계정 owner/지연 콜백·차단창/버튼·다른 계정과 legacy 보존 조건은 그대로 유지합니다. 부모6104624 대비 해당 문구 외 소스 정규화 바이트가 같음을 확인했습니다.

## 실제 입력과 산출물

구현 checkout `D:/codex/worktrees/tamer-working/Tamer`, branch `codex/deletion-lifecycle-arm64-1108`, 실제 빌드/기기 소스 `db1ccb41eec1a0d4c03c467d606123dee4b182d2` / PR508(부모507)입니다. 원72c13cc 계획은 실행0으로 보존하고, 부모의 실물 UI 검증 문서를 반영한 새 계획으로 동결했습니다. Unity6000.3.25f1/e1dba0a9aba4·Android min25/target36·기본ARM64를 유지합니다.

기존 `RevivalGameplayBuild.BuildDeletionGameplayAndroid`를 문서화된 raw CLI로1회 실행했습니다. 임시 Editor소스0, 운영키0·별도 debug인증서입니다. APK 109403382바이트, SHA-256 `a55f5a325c9cb38dd08503d3d22d4fd7a482a42ff94a63bb6afa8ce64e366763`. 별도 ID `com.AeDeong.MonsterTamer.revival.deletiongameplayv4`, 원래 Login/Main/Game/NextScene4씬, 인터넷/결제/광고 식별자 권한과 자동 로그인/광고 provider 제거·백업/전송18제외·native정렬을 확인했습니다. 버전32는 격리 프로필 값으로 새 운영 번호가 아닙니다.

전체 Editor/소유 자식 종료·보호7172파일/원래 보호 집합·키/JDK·두 옵션 원본값·공유ADB49852를 확인해16:40:53.932061UTC 원17/20 기한 안에 빌드 자원을 반환했습니다. CLI0 뒤30초 동안 남은 소유 자식은 해당 제한 작업만 종료했습니다. Library/기존 자료를 보존했습니다.

## 실물 두 프로세스

SM-N986N/Android13/API33·ARM64·4096바이트 페이지 기기에 새 별도 앱을1회 설치했습니다. 실제 운영 앱31은 실행/로그인/데이터 삭제 없이 기존 stopped/notLaunched 사용자 상태를 보존했습니다. 이 시험의 연령값은 코드에 정의된 합성 Declined fixture이며 실제 나이 화면의 입력·확인·거절을 하지 않았습니다. PlayFab TitleId OFFLINE/로컬 gateway를 사용했습니다.

| 단계 | 관측한 내용 |
| --- | --- |
| 첫 가상A | 원래 Main/장착/소유 상품 확인 후 원 settings/privacy/request/confirm 이벤트로 접수1회·외부 gateway0·소유 진행/인벤토리/백업/보관본 정리 확인 |
| 정리 경계 | 다른 가상B 인벤토리와 legacy 키 보존·이전 write차단/대기 read 미전송/이전 login stale거부·플레이어 세션 비움 확인 |
| 실제 종료와 새PID | 원래 Exit 이벤트로 종료한 뒤 다른PID의 Login에서 자동 인증/읽기/쓰기0과 한국어 재시도 화면 확인 |
| 명시적 재시도C | 원 Retry 이벤트 뒤 합성 인증(native/SDK0)·새 골드41/닉네임FreshFixtureC·빈 수집/장착/인벤토리·이전 모델 꺼짐 확인 |
| 원래 상품 UI | 원래 상점 handler로 연 상품들이 잠김/미장착·한국어 가격 일치, 다른 가상B/legacy 보존을 확인 |

developer driver의 원 uGUI 이벤트 명령7개(settings/privacy/request/confirm/exit/retry/shop)를 사용했습니다. 손가락/네이티브 터치 커버리지로 표현하지 않습니다. own앱 종료/Home·운영 앱 상태·전체Editor0/두옵션 원본값/공유 서버 수명을 확인하고16:46:13.172348UTC 원6/9 안에 실제반환했습니다. 초기화/삭제/실제 인증/나이/구매/운영키0입니다. 현재 완료된 fixture의 저장과 원본 캡처는 비공개로 보존합니다.

## 로그와 미검증

위 생명주기 불변 조건은 통과했습니다. 전체 로그가0이라고 표현하지 않습니다. 각 개발 Player에는 인터넷 권한이 없는 격리 환경의 정확한 PlayerConnection 소켓 진단3줄이 있으며, 기존 격리 정책/48검사의 허용 앱·development·메시지/유형 경계와 구분합니다. 운영 앱/일반 예외는 이 예외 범위에 포함하지 않습니다.

별도로 DungGeunMo SDF의 Ellipsis 조회 경고1개와 자동 Truncate 전환을 관측했습니다. 원 TTF의 cmap12/글리프15135와 원 SDF의 U+2026/동일 글리프/atlas0은 실제로 존재하고 sourceFontData 포함/clear-on-build0입니다. 실행 중 조회 경고의 원인은 아직 미확정이며 원본 글꼴/atlas/GUID는 바꾸지 않았습니다. 문구와 버튼은 원래 화면에서 확인했고 경고를 기능 실패0과 섞어 지우지 않았습니다.

실제 운영 기존/손님 계정·서버 저장·PGS/PlayFab 인증·구매/No Ads 복원·실제 삭제 공급자/보관·연령별SDK/통신·실16KB/Android16·새서명/Play 제공·최종사용자 UI/스토어/정책/26제외·심사/프로덕션은 별도입니다. [전체 잔여](remaining-recovery-work.ko.md)
