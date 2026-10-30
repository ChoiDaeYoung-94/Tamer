# 광고 동의 보완 통합 후보29 생성과 기본 검사 — 2026-10-30

**Quad D 인트로·새 아이콘/주요 UI와 IAP 광고 식별자 동의 보완을 포함한 검토 AAB29를 생성하고 기본 정적검사 및 원본 복구를 완료했습니다.** 실제 운영 로그인/저장·기존 구매/No Ads, 정상 Play 제공과 정책/심사·최종 디자인 검토는 남아 있습니다.

## 정확한 실행 입력

| 항목 | 실제 결과 |
| --- | --- |
| checkout/branch | `D:/codex/worktrees/tamer-working/Tamer` / `codex/iap-ads-consent-1030` |
| 실제 빌드 소스 | `6bb7b37c53a3be764e032b1e1c880b1af752da3c` |
| 도구 | Unity6000.3.25f1 / e1dba0a9aba4 / CLI1.0.0-beta.8 / Pipeline0.6.0-exp.1 |
| 앱/버전 | com.AeDeong.MonsterTamer / code29 / 1.0.5 |
| Android | min25 / target36 / ARM64 / IL2CPP / nondebug·nontest |
| 모드 | 기존 approved_adult_release: 혼합 연령 앱의 성인 광고 경로 계약 |
| 서명/빌드 | 기존 키·현재 upload 공개 인증서 challenge 통과 후 AAB1회 성공 |
| 검토 파일 | MonsterTamer-29-1.0.5-QuadD-Facelift.aab |
| 크기 | 76,133,522bytes |
| SHA-256 | `eecea981f569fcccd2609fa20124ea2faf45c46bd7d4a19855eb4c7b03b2c663` |

Console 전체21번들/최대27·28/29부재와 새로 다운로드한 현재 공개 upload DER를 확인한 뒤 별도29 plan/source-review/native metadata를 준비했습니다. 첫 직접 입력은 challenge IOException1/실제 빌드0이었고 원인은 미확정입니다. 실패 자료를 보존한 별도 새 직접 입력이 키 확인을 통과했으며 실제 AAB 빌드 성공1회·빌드 실패0입니다. 기존28 plan/receipt와 암호를 재사용하지 않았습니다. 원키·CLI는 복제하지 않고 이전27/28 및 키/계정·저장/No Ads·Library를 보존했습니다.

## 변경과 검증 범위

- [IAP 보완](iap-advertising-intent-20261030.ko.md)은 별도 opt-in 없는 IAP AdsIntent를 항상Denied로 정합니다. 명시 Analytics 선택과 기존 No Ads 상품/권한·구매/복원·GMA 연령/UMP는 유지합니다. 실제 사용자 Granted나 ID 전송을 관측했다는 뜻은 아닙니다.
- 실제 Editor 시험 소스54873ac의6/6·오류0·보호6946파일 정확 복구를 연결했습니다. 이번 빌드 소스의 Assets/Packages는 그 시험 소스와 동일하고 실행 설정은 code28→29만 변경했습니다. 소스12항목/후보 검사기19/19도 통과했으며 같은 성공 시험을 반복하지 않았습니다.
- 실제 AAB의 bundletool validate·업로드/전체 payload 서명·manifest/버전명·앱ID·SDK·ABI·nondebug/nontest를 확인했습니다. 빌드 callback의 정확한 source/config/resource 계약을 대조했습니다.
- 네이티브6개는 ELF LOAD·보수적 RELRO 배치 기준을 통과했습니다. 원시 RELRO 끝나머지 진단3건은 별도이며 실제 ARM64/16KB 실행·Play 제공 APK ZIP 정렬 통과를 뜻하지 않습니다.
- 길이 포함 씬 배열 프레임에서 QuadDIntro→Login→Main→Game→SetCharacter→NextScene 순서와 원본 패키지 영상88,032B의 SHA 일치, 새 launcher 아이콘과 승인 광고 설정 named TextAsset의 독립 추출/바이트 일치를 확인했습니다. 전체 TypeTree 파싱이나 실제 resource load/SDK 네트워크 검증은 아닙니다.
- 원본9개/소유 staging9개를 검토한 delta로 정확 복구했습니다. 보호7112개 전체 SHA/파일 집합·Git clean·활성 journal 없음, 관측 소유16프로세스와 Editor0을 확인했습니다. 프로세스 이름만으로 다른 Editor/SDK/VM을 종료하지 않았습니다.
- 검토용 복사본은 원본과 byte/SHA 동일하고 원시 로그·설정·키/계정·AAB는 공개 저장소에 올리지 않습니다. 주요 SDK 메타데이터와 GMA provider/manifest 관측은 기본 정적 범위이며 모든 SDK 초기 처리/기기 통신 차단의 증명이 아닙니다.

## 제출 전 남은 조건

Google 지원 답변은 중립 연령 화면·아동/연령 미상/거부 사용자 무광고 방향을 허용한다는 안내입니다. 성인 확인 전 광고 SDK/AD_ID·전송 차단의 최종 동작, 대상 연령 신고, 심사 계정으로 두 경로 접근 및 모든 활성 release에서 거절26의 Not included 대조가 남아 있습니다. 심사 계정 No Ads 권한은 사용자 답변대로 미확인이고 기존 credential 공식 저장 이력과 실제 접근 검증을 구분합니다.

다음은 후보29 수동 업로드·Console 분석, 정상 Play 제공 조건·기존 로그인/서버저장·구매/No Ads·연령/개인정보/삭제 경로와 사용자 최종 UI/스토어 검토입니다. [512px 아이콘과1024×500 대표 이미지](../images/store-exports-20261030/README.ko.md)는 원본을 보존한 일반 도구의 크기 변환 완료본이며 실제 게임 스크린샷과 구분합니다. Console 업로드/공식 저장·실제 심사 전송·배포·트랙 재개는 수행하지 않았습니다. PR492/493/495는 Draft로 유지합니다.

작업 상태: AAB 생성·기본 검사·정확 복구 완료, 운영/제공·심사·최종 디자인 미검증. 다음 작업: 사용자 수동 업로드 후 분석 대조·운영 기능 검증. 사용자 할 일: 준비한 검토본 직접 업로드와 필요한 인증. 추가 질문: 없음. [전체 잔여와 명시 보류](remaining-recovery-work.ko.md)를 유지합니다.
