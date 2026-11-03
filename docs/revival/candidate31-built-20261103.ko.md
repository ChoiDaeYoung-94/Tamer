# 후보31 생성·기본 검사·정확 복구 — 2026-11-03

사용자가 기존 키 암호를 로컬 창에 직접 입력한 뒤 source `cf21b35b3f055f270614922ac3238036d690baed`의 후보31을 기존 업로드 키로 **1회 생성**했습니다. [손님 진입 준비](candidate31-preparation-20261102.ko.md), [실제 바탕화면 서명 준비](candidate31-signing-readiness-20261103.ko.md)를 연결합니다. 최종 운영 기능·정책 심사·프로덕션 출시 완료는 아닙니다.

| 항목 | 실제 결과 |
| --- | --- |
| 앱/버전 | com.AeDeong.MonsterTamer / code31 / 1.0.5 |
| 환경 | Unity6000.3.25f1/e1dba0a9aba4·CLI1.0.0-beta.8·Pipeline0.6.0-exp.1·min25/target36/ARM64 |
| AAB | 76,141,124 bytes |
| SHA-256 | `99bee02694430a53b842fce7f86522fd7c14e5ae5ced803a632516bdbf3a6e62` |
| 빌드/서명 | 기존키 검사 성공·실제 AAB1회·batch/producer exit0·현재 검토 업로드 인증서 일치·payload899개 전부 서명 확인 |
| 기본 검사 | bundletool validate·버전/manifest·nondebug/nontest·ARM64 native6 ELF LOAD/보수적RELRO 배치 통과·격리 손님UI 진단 코드/initializer 제외 |
| 원본 복구 | 실제 변경 원본9개 복구·소유 임시9개 제거·late writer0·보호7136파일 바이트/집합 불변·Git clean/upstream 동일·active journal 제거 |
| 키/도구 보존 | 원키·기존 AAB30 SHA·Library 보존, 양JDK421파일씩 바이트/집합·양환경 JDK2설정 불변, 공용 JDK 설정 쓰기0 |
| 실행 종료 | 관측 소유87개 identity의 잔존0·해당 Editor 종료·전역 강제 종료 명령0 |

이번 단독 실행은14:43:48.886 KST에 시작했고 실제 사용자 실행은14:48:13 KST, 기존키 검사 완료는14:49:32 KST입니다. producer 완료14:55:28 KST 뒤 실제 delta를 보존·검토하고14:56:40 KST에 복구했습니다. 기본 검사와 입력/JDK 보존을 대조한 뒤 **05:57:20.174 UTC/14:57:20.174 KST**에 실제 반환했습니다. 원래20분 새 실행/5분 복구 범위 안이며 자동 연장·다른 프로젝트 메시지0입니다.

실제 바탕화면의 JDK 두 설정은 원래 absent로 전후 같았습니다. 기존 실제 JDK getter/Hub Java SHA 가드와 build snapshot/callback 검사를 유지했습니다. source-review164개·metadata921 pin/runtime732파일을 연결했으며 암호 내용은 채팅이나 파일로 받지 않았습니다. 앞선 암호창 이전 환경 검사 실패와 입력 기한 종료 attempt는 이번 성공으로 지우거나 재사용하지 않았습니다.

## 실제 패키지 확인

QuadDIntro→Login→Main→Game→SetCharacter→NextScene의6개 씬 순서 프레임을 실제 패키지에서 대조했습니다. 공통 인트로 원본88,032B 영상과 동일 payload1개·후보27과 다른 새 launcher 아이콘을 확인했습니다. 최대192×192 패키지 아이콘과 그 해시를 비공개 보존했습니다. 이 씬 확인은 제한된 바이너리 프레임 검사이며 전체 Unity type tree 파싱이나 실제 영상 재생 검증은 아닙니다.

검토된 metadata39 읽기 방식으로 나이 입력 문구3개와 ‘Google로 계속 / Google 없이 시작 / 손님 선택 시 서버 저장용 게임 계정이 생성됩니다.’ 문구3개가 컴파일된 공개 문자열 영역에 있음을 확인했습니다. 기존 parser 교정 시험은 반복하지 않았습니다. UI 바인딩·실제 나이 제출·손님 생성/서버 저장·SDK 통신 성공의 근거로 확대하지 않습니다.

원본9개 중7개는 직렬화 공백/줄바꿈 또는 identity만 달랐습니다. 나머지는 resolver의 bundleId가 운영 패키지로 바뀐 것과 ProjectSettings의 alias가 비워진 것으로 분류했습니다. 실제 원문·archive·staging identity/SHA를 검토하고 기존7필드 recovery 계약으로 복구했습니다. 바이트/집합 복구와 원래 파일 identity 복구는 구분하며 후자는 주장하지 않습니다. 임시 디렉터리와 Library·원시 증거를 정리 목적으로 삭제하지 않았습니다.

## Console 전달과 남은 조건

15:00 KST 전후 기존 작업용 Chrome 창을 실제 창 ID·탭 마지막 열림·메일/Drive/GitHub/PlayFab/AdMob/Photon 구성으로 다시 확인했습니다. 전체 번들22개·최신29·코드31 검색 결과0개, 내부29 제공·프로덕션24 제공/26 승인되지 않은 출시를 읽었습니다. 내부 테스트 새 버전12의 업로드 양식을 열고 사용자 수동 업로드를 안내했습니다. 이 시점의 업로드·출시·검토 전송은 아직0이며, 완료 회신 뒤 분석 결과를 별도 확인합니다.

검토용 AAB 사본·checksum·안내는 비공개 Downloads 폴더에 준비했고 원본과 SHA/크기가 같습니다. 서명키·암호·원시 로그·비공개 snapshot은 전달 폴더나 공개 Git에 넣지 않았습니다. 기존 후보30은 손님 보완 전 원본으로 보존했습니다.

strict RELRO 끝주소 modulo 진단은 libc++_shared/libmain/libunity의3개에 남으며 기본 LOAD/보수적 배치6개 통과와 구분합니다. 이 진단만으로 실제 crash를 단정하지 않습니다. Play가 생성하는 APK의 ZIP 정렬·실제16KB/ARM64 시작과 정상 Play 설치는 아직 미검증입니다. 이전 LD의 새 ARM 네 변형 흰 화면도 원인 미확정입니다.

정상 제공 경로에서 기존/손님 계정 전체 게임·서버 저장/재시작·구매/No Ads 복원·미성년/미상/거절/성인 경로의 실제 광고/SDK 통신·삭제를 확인해야 합니다. 심사 계정 No Ads 권한·App access, 타겟/데이터 보안·공개 개인정보/삭제 안내·최종 UI/스토어와 모든 활성 출시의26 미포함도 남습니다. 실제 내부 제공·정책 심사 전송·프로덕션은 각 승인 범위를 구분합니다. [전체 잔여](remaining-recovery-work.ko.md)
