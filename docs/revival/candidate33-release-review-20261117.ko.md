# 후보33 실제 패키지·정책·에셋 검토 — 2026-11-17

실제 운영 AAB의 빌드 소스는 81431d5입니다. Unity 6000.3.27f2·ARM64, 코드33/1.0.5/min25/target36이며 AAB는 76,155,877바이트입니다. SHA-256은 7875b2b340832fe8fc2ccf7a6e112684d984bf834ce1f349c6cf470523aafbbf 입니다. 운영 Play 제공과 실제 계정·기기 검증은 다음 단계입니다.

## 실제 산출물과 패키징

- 새 업로드 키로 payload 897개의 서명과 bundletool 검사를 통과했습니다. 기본 네이티브 라이브러리 6개의 LOAD 정렬·보수적 RELRO 배치 및 시험 UI 진단 코드 제외 검사를 확인했습니다.
- 실제 AAB의 BundleConfig는 uncompressNativeLibraries.alignment를 PAGE_ALIGNMENT_16K로 설정합니다.
- 이 AAB에서 합성 ARM64/API36 사양으로 로컬 debug 재서명 split APK 2개를 생성했습니다. 각 APK의 서명과 zipalign 16KB 검사, 네이티브 6개 바이트의 원 AAB 일치 및 LOAD/RELRO 배치 검사를 통과했습니다. 원 AAB 해시는 전후 같습니다. 기기 설치는 수행하지 않았습니다.
- 실제 Play 생성 APK, 16KB 기기 실행, libc++_shared.so·libmain.so의 end-modulo 추가 진단과 운영 경로는 후속입니다. 로컬 split의 서명은 Play 앱 서명과 구분합니다.
- libil2cpp·libunity·Burst의 공개 기호 3개가 실제 AAB의 Build ID와 일치했고 ZIP 엔트리 해시도 확인했습니다.

## 후보33의 개인정보·연령 처리

| 처리 | 현재 소스와 AAB에서 확인한 범위 | 운영에서 확인할 부분 |
| --- | --- | --- |
| 중립 연령 입력 | AgeChoicePresenter는 빈 숫자 입력과 응답 거절을 제공하고 LocalAgeChoice는 연령 그룹을 저장합니다. | 최종 제공판 입력·분기 |
| 광고 진입 | AgeTreatmentPolicy의 Adult 조건과 GoogleAdMobManager의 계정·동의·연령 조건을 초기화/요청 앞에서 확인합니다. | 연령별 실제 SDK 통신·식별자와 성인 선택 변경 |
| 선택 보고 | 로그인 전 PlayFab 선택 보고 OFF, IAP AdsIntent Denied, Unity Analytics 자동 시작 OFF를 확인합니다. | 필수 인증/구매 처리와 선택 보고 범위 |
| Google 로그인 | AAB에 PGS 자동 InitProvider가 없고 명시 Google 진입에서 Activate합니다. | 기존 계정 진행·구매 복원 |
| 손님 플레이 | 명시 선택한 신규 손님의 무작위 로컬 식별자를 CustomID 계정으로 연결합니다. | 실제 서버 저장·재접속 |
| 삭제 | 미리보기 때 Live availability를 확인하고 확인한 revision으로 요청합니다. | 운영 gate·소유 확인·공급자 후속 처리 |

성인 광고를 초기화한 후 미성년 또는 응답 거절로 바꾸는 동선은 관리 상태와 광고 객체 무효화 뒤 native SDK 처리까지 운영에서 관찰합니다. 현재 소스의 객체 해제와 native SDK 종료를 같은 결과로 해석하지 않습니다.

기존 [개인정보 감사](privacy-data-safety-audit.ko.md)의 UMP 미사용·자동 Analytics·PlayFab 보고 flag 설명은 해당 날짜의 이력입니다. 후보33의 현재 설명은 위 표와 현재 소스를 기준으로 합니다. [심사 접근 초안](guest-review-instructions-draft-20261102.ko.md)과 Data safety의 사용자 ID 신고 범위를 실제 손님 경로·계정과 최종 대조합니다.

## 이번 AAB의 에셋 포함 근거

같은 빌드의 LastBuild.buildreport를 공식 binary2text 도구로 읽었습니다. 보고서 outputPath 두 곳과 AndroidAppBundle 크기가 실제 AAB에 일치하며 입력 보고서 SHA-256은 전후 같습니다.

PackedAssets는 263구간·객체7,966개이며 이름 있는 고유 경로3,128개입니다. 객체 수와 구간 수는 고유 상품 수가 아닙니다.

| 구형 후처리 원본 | 이번 AAB 보고서의 포함 결과 |
| --- | --- |
| PP low.asset | 4개 객체·176바이트 |
| Forest Creatures PPP.asset | 0개 |
| Magma.asset | 0개 |
| Cute Series.asset | 0개 |

PP low의 이번 보고서 구간은 GUID 형태이므로 이전 LD 보고서의 resources.assets 배치를 그대로 적용하지 않습니다. [이전 포함 분석](packed-asset-followup-20261112.ko.md)의 범위를 이 결과로 갱신합니다. 원본과 GUID를 보존했습니다.

private-restore와 겹치는 포함 경로168개의 원본이 복원 manifest SHA와 일치합니다. Avalon 이름과 두 로컬 GUID는 이 보고서에서 발견되지 않았습니다. 별도 권리 증빙과 외부 백업은 후속이며 원시 포함 목록은 비공개로 보존합니다.

## 다음 작업

최신 Console의 코드33 등록·활성 인증서 대조, 수동 AAB/기호 업로드와 분석, 정상 Play 보존 업데이트의 계정/저장·No Ads·연령별 SDK·정책 확인을 진행합니다. 거절26 제외와 사용자 최종 검토 뒤 정책 심사·프로덕션 단계로 이어갑니다. 기존 복구1~29, 30제외, 31출시필요시 범위를 유지합니다.
