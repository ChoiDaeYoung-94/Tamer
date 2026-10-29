# 후보27 업로드와 Play 서명 APK의 기본 검사 — 2026-10-29

**사용자 직접 AAB 초안 업로드와 같은 아티팩트의 Play 서명 범용 APK 기본 정적검사를 완료했습니다.** 초안 저장·검토 전송·배포·내부 트랙 재개는 진행하지 않았습니다. 설치·운영 기능·정책 심사와 모든 기기별 split 검증은 별도입니다.

## 실제 대상과 연결

| 항목 | 결과 |
| --- | --- |
| source / checkout | `eba77a66f75122f0c0c539ed0d5b006c73d28253`, `D:/codex/worktrees/tamer-working/Tamer` |
| 원본 AAB | [후보27 생성·검사](operating-candidate27-20261029.ko.md), 76,868,171 bytes |
| AAB SHA-256 | `961730db025bfec34ba2a8cca405bb2c60d646dcbf8fd2cdc64f627456e8af83` |
| Play 범용 APK | 77,283,195 bytes |
| APK SHA-256 | `670052ce2c55359d13dac89c3ece5fe966bbaeb450d8b5d0bab7c592091fa553` |
| package / version | `com.AeDeong.MonsterTamer`, 27 / 1.0.5 |
| Android / ABI | min25 / target36 / arm64-v8a, non-debug / non-test |
| 빌드와 읽기 도구 | 원본 Unity6000.3.25f1/e1dba0a9aba4·CLI1.0.0-beta.8. APK 검사는 같은 Unity 설치의 기존 Java와 SDK build-tools36.0.0 aapt2/apksigner·고정 native 검사 소스 사용 |

12:13~12:18(KST), 사용자가 운영 Monster Tamer의 일시중지 내부 테스트에서 새 초안에 지정 AAB 한 개를 직접 업로드했습니다. 같은 화면의27/1.0.5·min25/target36/ARM64 행과 상세의 출시0개를 읽었습니다. 빌드 직전 최대26 관측은 이전 단계 이력이며 **27은 이제 업로드된 코드**입니다. 다음 빌드가 필요해지면 변경 가능한 전체 사용 번호를 다시 확인합니다.

같은 아티팩트 상세의 ‘서명됨, 범용 APK’ 한 개를 내려받았습니다. 계정·내부 URL·다운로드 주소와 원시 로그는 비공개로 보존했습니다. 새 owner/SYSTEM 전용 폴더의 사본과 원다운로드를 모두 보존하고 복사 중 해시·파일 식별/크기·mtime 불변을 대조했습니다. 공개 Git/LFS에 바이너리·비공개 에셋을 올리지 않았습니다.

## 확인한 범위

- **앱 서명:** Android apksigner 검증에 성공했고 현재 Console의 **Play 앱 서명** 인증서와 일치했습니다. 원본 AAB의 업로드 서명 역할과 다릅니다. 실제 개인키를 다시 사용하거나 키 파일을 복제하지 않았습니다.
- **manifest:** package/code27/version1.0.5/min25/target36/ARM64와 non-debug/non-test를 확인했습니다.
- **설정 리소스:** APK 안의 named resource 이름·정수 길이 프레임과 실제 payload가 원본 AAB/plan에서 추출한 설정과 같았습니다. 전체 type tree 해석이나 실행 중 리소스 로드 성공을 뜻하지 않습니다.
- **네이티브:** ARM64 라이브러리6개 모두 ELF LOAD·ABI/ZIP 구조와 보수 RELRO 배치 검사를 통과했습니다. 이 APK의6개는 모두 압축형이므로 직접 mmap을 위한 비압축 ZIP16KB 정렬 요구의 적용 대상은 없습니다. manifest에 `extractNativeLibs`는 명시돼 있지 않았습니다. 설치 시 실제 압축 해제·파일 배치와16KB 실행을 검증하지 않았습니다.
- **추가 strict 진단:** `libc++_shared.so`, `libmain.so`, `libunity.so`의 RELRO end-modulo16384 세 항목이 미충족입니다. 원본 AAB의 진단 대상과 이름 집합은 같지만 이 사실만으로 모든 라이브러리 바이트 동일성·런타임 실패나 심사 거절을 판단하지 않습니다.
- **권한 차이:** 원본 AAB의 직접 `uses-permission`10개와 달리 Console/Play APK에는11개가 표시됐고 추가 항목은 `com.android.vending.CHECK_LICENSE`였습니다. 추가 경로와 실제 라이선스 검사·데이터 전송 동작은 이번 관측으로 확정하지 않았습니다. 권한 선언만으로 새 수집 유형을 추가하거나 수집 없음으로 판정하지 않습니다.

12:22~12:29(KST)의 다운로드·검사 차례에서 순차 Python/aapt2·Java1회만 사용했으며 실제 정적 처리 시간은1.157초였습니다. 전체 APK를 풀어 쓰거나 원본 AAB 검사·키 challenge·빌드·회귀 시험을 반복하지 않았습니다. APK 읽기 handle에서 writer/deleter를 차단했고 전체 해시·기본 정적 결과와 파일 metadata 불변을 연결했습니다. source HEAD/branch·작업본은 clean 상태로 보존했습니다.

## 13:08 KST 같은 아티팩트의 Console 표시

같은 후보27 상세의 메모리 페이지 크기는 **16KB 지원**으로 표시됐습니다. Console 자체의 정적 호환 표시이며 Google이 감지하지 못하는 라이브러리나 페이지 크기 가정 때문에 오류가 남을 수 있다는 안내도 함께 있었습니다. 이를 실제16KB 기기 설치/실행 성공이나 추가 strict 진단의 통과로 바꾸지 않습니다. 기존 실제16KB 명시 보류를 유지하고 별도 빌드/다운로드/시험을 재실행하지 않았습니다.

## 완료와 남은 일

| 구분 | 상태 |
| --- | --- |
| 사용자 키 입력·운영 AAB 생성/복구 | 완료, 같은 입력/빌드 반복 불필요 |
| 후보27 초안 업로드·같은 Play 범용 APK 기본 정적검사 | 완료 |
| 사용자 초안 | 원래 준비 화면·입력 보존. 저장/검토 전송/배포·트랙 재개0 |
| 기기별 모든 APK/split·실제16KB | 미검증. 실제16KB의 기존 명시 보류 유지 |
| 최종 광고/동의·계정/저장·구매/No Ads·삭제 | 같은 최종 후보에 필요한 최소 운영 검증은 남음. 기존 범위별 시험을 새 운영 실적으로 전용하지 않음 |
| 신고·공개 안내·심사 | [현재 신고 수정표](data-safety-submission-plan-20261029.ko.md)의 정합·필요한 저장/제출·심사 결과는 별도 |

이 기록의 `basicStaticChecksPassed=true`는 위 범위를 가리킵니다. 원래 producer/plan의 결과를 덮어쓰지 않으며 최종 `distributable/releaseReady=false`, `runtime16KBVerified=false`, `allSplitApksVerified=false`, `operatingFunctionsVerified=false`를 유지합니다. 기존5초 추가 조사·CI/App Center 활성화·새 서비스/결제·보호 우회는 보류 범위를 그대로 따릅니다.

**다음 우선순위:** 실제 신고/공개 안내의 남은 정합과 필요한 운영 검증의 구체 절차를 준비합니다. **현재 사용자 할 일:** 추가 조작 없이 미저장 Console 초안 탭을 유지해 주세요. 같은 업로드·암호·사진을 다시 요청하지 않습니다. **질문:** 없음. [전체 잔여 목록](remaining-recovery-work.ko.md)을 기준으로 마감합니다.

## 14:38 KST 데이터 보안 저장 후속

10월29일14:35(KST) 인간 사용자의 공식 저장 승인을 접수했고,14:38 승인 전 미리보기와 펼친 현재 미리보기의 SHA-256 동일성을 대조한 뒤 상위 **저장1회**를 완료했습니다. Console의 저장 성공·검토를 위해 게시 개요로 이동하라는 문구와 저장 후 내보낸 CSV에서 **수집10개·공유6개/상위 답안 일치**를 확인했습니다. 신고 CSV782행의 schema·응답을 대조했으며 유형 처리190행과 상위 비공백8답안/선택10유형이 준비값과 맞습니다. 현재는 검토 전송 준비 범위이며 **실제 심사전송·배포·트랙 재개0**입니다. 다운로드 응답 대기 후 추가 DOM 조회가 중단돼 게시 개요 자체는 따로 열지 않았고, 저장 성공 UI와 실제 저장 CSV를 근거로 구분합니다. [정확한 저장·한계](data-safety-saved-20261029.ko.md)를 따르며 운영 기능·정책 심사 통과나 최종 releaseReady로 확대하지 않습니다.

이 저장은 개인정보 신고 양식에 한정됩니다. 원래 후보27 내부 테스트 출시 초안의 출시명/노트·저장/다음 단계나 APK 검사·운영 기능 시험을 재실행한 결과가 아닙니다.

## 15:30 KST 실제 설치 확인 후속

10월29일15:30(KST), 기존 운영 패키지와 보존된 미설치 등록이 없음을 확인한 LD Tamer에 정확한 Play 서명 후보27 APK를 plain install1회로 설치했습니다. 설치된 base.apk SHA-256이 원본과 같고 code27/1.0.5/min25/target36/ARM64를 확인했습니다. 설치 후 게임 프로세스는 없었고 앱 실행·로그인/저장/광고·구매/삭제는 진행하지 않았습니다. 기존 Google 설정의 표시계정은 확인한 소유계정과 같지만 앱별 PGS 신원은 미검증입니다. 정상 HOME/에뮬레이터 종료·소유 PID/창 부재를 확인하고 설치 후보를 보존했습니다. [설치 근거와 심사 준비](candidate27-installed-20261029.ko.md)를 따르며 Play Store 배달·실제 운영 기능/16KB·심사 통과로 확대하지 않습니다.

Data safety 공식 저장 완료 상태는 [저장 근거](data-safety-saved-20261029.ko.md)를 따릅니다. 이 절의 설치는 새 키 입력/빌드/업로드 없이 같은 파일을 연결한 결과이며 사용자 내부 테스트 초안을 수정하거나 심사를 전송하지 않았습니다.
