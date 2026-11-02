# 후보30 생성·기본 검사·정확 복구 — 2026-10-30

사용자가 기존 키 암호를 직접 입력한 뒤 source`0c5d9047108b6dccbd6ae3f6229b910811c948b0`의 후보30을 기존 업로드 키로1회 생성했습니다. [직접 나이 입력](neutral-age-input-20261030.ko.md)을 포함하며 내부 제공29의 구간 버튼 화면과 다릅니다. 준비 입력과 Console 대조는 [후보30 준비](candidate30-preparation-20261030.ko.md)를 따릅니다.

| 항목 | 실제 결과 |
| --- | --- |
| 앱/버전 | com.AeDeong.MonsterTamer / code30 / 1.0.5 |
| 환경 | Unity6000.3.25f1/e1dba0a9aba4·CLI1.0.0-beta.8·min25/target36/ARM64 |
| AAB | 76,123,986 bytes |
| SHA-256 | `13e0a52c5ea3f244f00cce10a221ca642f350c4e5d7ea5a9472c6c8e74542c9e` |
| 빌드/기본 검사 | 실제 빌드1회 성공·AAB validate/기존 업로드 서명·버전/manifest·nondebug/nontest·native6 기본 배치 통과 |
| 원본 복구 | 원본9개 정확 복구·소유 임시9파일 제거·보호7113입력 SHA/집합 불변·키/기존29 AAB/Library 보존·Git clean |
| 실행 종료 | 관측 소유82개 identity의 잔존0·해당 Editor0, 강제 종료 없음 |

실제 패키지에서 QuadDIntro→Login→Main→Game→SetCharacter→NextScene 순서와 원본 영상88,032B·새 launcher 아이콘·승인 광고 리소스 payload 일치를 대조했습니다. IL2CPP metadata39의 공개 문자열 영역에는 새 입력 문구3개가 모두 들어 있습니다. 기존 오프라인 photo04/운영29의 양성3/3·음성0/3 대조로 읽기 검사를 교정했으며, 이 결과를 UI 실제 제출/SDK 통신 검증으로 확대하지 않습니다.

manifest에는 운영 광고/결제 권한과 GMA 초기 provider가 존재합니다. 앞선 SDK 시작 경로의 지정 메서드 관측과 최종 기기 실행·간접 호출 전체는 구분합니다. 미성년/미상/거절과 성인·No Ads 동작은 실제 제공물에서 확인해야 합니다. 기존 strict RELRO 진단3개도 보존하며 기본 LOAD/보수적 배치 통과를 실제16KB 기기 실행으로 계산하지 않습니다.

22:57 KST 검토용 사본과 checksum/안내·실제 아이콘 확인본을 비공개 Downloads 폴더에 준비했습니다. 원본 AAB와 바이트/SHA가 같으며 공개 저장소에는 바이너리·계정·키·원시 로그/스냅샷을 넣지 않았습니다. 이번 생성/검사는 Console 업로드·내부 제공·최종 심사 전송·프로덕션 출시가 아닙니다.

다음은 정상 Play 제공·기존 계정/서버 저장·기존 구매/No Ads·유효 나이 입력/개인정보·삭제와 정확한 심사 동선 확인입니다. 스토어 기기 검사는 유지하며 현재 LD 미인증/정상 제공 제한과 웹 설치창 로딩은 해결 완료로 쓰지 않습니다. 최종 사용자 UI·스토어/공개 정책 검토와 대체 출시의26 미포함 확인도 남습니다. [전체 잔여](remaining-recovery-work.ko.md)
