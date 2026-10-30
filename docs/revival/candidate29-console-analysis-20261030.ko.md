# 후보29 Console 분석·초안 저장과 Play APK 기본 검사 — 2026-10-30

19:06 KST 후속: 사용자 UI 검토·기존 내부 테스터 제공 승인을 받아18:56 KST29 출시와 내부 트랙 재개를 완료했습니다. 최종 활성/29 제공 표시를 확인했으며 프로덕션·최종 심사 전송은 별도입니다. [최신 제공 결과](internal29-live-20261030.ko.md)

## 초안 저장과 기본 검사 이력

사용자가 후보29 AAB를 직접 업로드했고, 기존 내부 테스트 초안에 한국어 출시 노트를 넣어 **임시보관함에 저장**했습니다. 저장 성공 표시·버튼 비활성·다시 연 노트·개요의 임시29를 확인했습니다. **내부 트랙은 일시중지이며 심사 전송·배포·트랙 재개는 하지 않았습니다.**

## 입력과 Google 제공물

| 항목 | 확인 결과 |
| --- | --- |
| AAB 소스 | `6bb7b37c53a3be764e032b1e1c880b1af752da3c` / `codex/iap-ads-consent-1030` |
| AAB | code29 / 1.0.5 / 76,133,522bytes / SHA-256 `eecea981f569fcccd2609fa20124ea2faf45c46bd7d4a19855eb4c7b03b2c663` |
| Console | min25 / target36 / ARM64 / 지원 기기13,391 / 16KB 지원 표시 / DEX 수준 낮음·18.4MB |
| Google 범용 APK | 76,545,214bytes / SHA-256 `5e10b8632d56fb4b9aa20bac58ae3501d87b13d79f5ea3b37f4a9ef0811bd44b` |
| APK 기본 검사 | 실제 앱 서명·앱ID/버전·SDK/ABI·nondebug/nontest·승인 광고 리소스 일치 |
| 네이티브 | 6개 ELF LOAD·ZIP·보수적 RELRO 배치 통과. 원시 strict RELRO 진단3건은 별도 |

APK는 한 번 다운로드해 비공개 복사본의 크기·SHA를 다시 확인했습니다. 앱 서명과 AAB 업로드 서명을 구분하고 현재 Console 인증서와 대조했습니다. `CHECK_LICENSE`도 존재합니다. **이 APK의 설치/실행은 하지 않았으며 Console의16KB 표시는 실제 ARM64/16KB 실행 결과가 아닙니다.** 정상 Play 설치·운영 로그인/저장·No Ads·기기 통신은 미검증입니다. 29는 이제 사용된 번호이므로 변경된 운영 바이너리는 최신 번호/트랙을 확인한 새 계획을 사용합니다.

## 미리보기 경고3개

1. **지원 기기7,592개 감소:** 이전 내부 테스트15와의 비교입니다. 휴대전화5,241·태블릿2,320·TV4·Chromebook27 감소가 표시됐습니다. 프로덕션24와의 차이로 해석하지 않습니다. 승인된 min25/ARM64 전환과 연결하되 모든 기기별 제외 원인을 조사한 것은 아닙니다.
2. **가독화 파일 없음:** 소스의 Android release/debug minify는0입니다. 없는 R8 mapping을 만들거나 경고만을 위해 R8을 활성화하지 않았습니다.
3. **네이티브 기호 없음:** 빌드 시 남은 `libmain`·`libunity`·`libil2cpp` 기호의 GNU Build ID를 AAB29/Google APK29에 각각 대조했습니다. `.symtab`과 ZIP 원본 바이트 일치를 확인해36,851,878bytes ZIP을 비공개 준비했습니다. **전체6개 중3개이며 업로드/경고 해소·실제 크래시 해석은 미실행**입니다. public symbols는 형식 이름으로 공개 배포 허가를 뜻하지 않습니다.

미리보기의 `저장 및 출시`는 즉시 게시 안내가 있어 누르지 않았습니다. 기호의 형식은 [Unity6000.3 문서](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/developing/testing-and-debugging/symbols)와 [Android 문서](https://developer.android.com/build/include-native-symbols)에 연결하며 로컬 준비와 업로드를 구분합니다.

## 트랙 관측과 남은 조건

프로덕션은 제공1.0.3/code24·거절26·이전25, 공개 테스트는 일시중지/마지막17, 비공개 Alpha는 활성이나 출시 없음·IAP_Test와 스토어는 일시중지입니다. 내부 테스트는 기존15와 임시29·일시중지이며 새 초안에서15가 미포함입니다. 읽기 관측만 수행했고 트랙을 변경하지 않았습니다.

최종 심사 전 **전체 활성 release에서26 미포함**을 대조해야 합니다. 거절 상태나 내부15 미포함으로 충족됐다고 쓰지 않습니다. 대상 연령·공개 방침/삭제 안내·심사 계정의 실제 접근/No Ads 조건과 두 연령 경로 안내도 남습니다. 계정 비밀값은 읽거나 복제하지 않았습니다.

작업 상태: 업로드/분석·내부 초안 저장·APK 기본 검사 완료, 운영/정책·디자인 미검증. 다음: 정상 Play 제공·계정/저장·기존 구매/No Ads와 연령별 경로, 최종 UI/스토어 검토. 사용자 즉시 조치·질문: 없음. [생성 근거](candidate29-built-20261030.ko.md) · [전체 잔여](remaining-recovery-work.ko.md)
