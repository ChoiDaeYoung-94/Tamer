# 사용자 휴대폰의 Main·Game 왕복 사진 확인

2026-10-26 15:31 KST 사용자가 전달한 세 장의 원본 사진을 읽고 아래 결과를 확인했습니다. 새 빌드·반복 설치·기기 조작은 하지 않았습니다. 전달 후보는 [sourcec6b4ba1 일반 APK](gameplay-delivery-20261024.ko.md)이며, 후보와 사진의 연결은 전달 안내·사용자 대화 맥락에 근거합니다. 사진 자체에 설치 APK 해시는 표시되지 않습니다.

| 순서 | 화면에서 직접 읽은 결과 |
| --- | --- |
| Main 첫 화면 | `MAIN_READY`, `scene=Main`, HP100/Gold1000/동료0, reads2/writes0 |
| Game 진입 | `GAME_READY generator=present player=preserved`, `scene=Game`, reads3/writes0, 필드·미니맵·Main 복귀 버튼 |
| Main 복귀 | `ROUNDTRIP_OK count=1 reads=4 writes=0`, `scene=Main`, HP100/Gold1000/동료0, 마을과 Enter Game 버튼 |

세 장 모두 `OFFLINE TEST APP`·`synthetic account only`·`Development Build` 표기가 있고 하네스 `errors=0 totalErrors=0 playerConnectionDiagnostics=0`입니다. 우측 화면 로그 패널의 경고/오류도0이며 정보 메시지는8→21→49입니다. 마지막 성공 표식과 실제 마을/필드/마을 화면이 왕복 확인의 근거입니다. 연속 영상이나 전체 로그를 받았다는 뜻은 아닙니다.

원본 JPEG3개를 재인코딩하지 않고 비공개로 보존했으며 각 크기·SHA-256 일치를 독립 대조했습니다. 원시 첨부·Slack 식별자·개인 보관 경로는 공개하지 않습니다. 별도 읽기 검토에서도 같은 표식과 한계를 확인했습니다.

## 완료와 남은 범위

**사용자 휴대폰에서 보고한 오프라인 Main→Game→Main 시각 확인은 완료**입니다. 같은 확인을 다시 요청하지 않습니다. 사진의 파일 해시는 설치 APK 해시가 아니며, package/UID·기종·Android/ABI·page size·전체 Android 로그는 독립 계측하지 않았습니다. 화면의0을 장치 전체 오류0으로 확대하지 않습니다.

합성 reads/writes는 메모리 서버 카운터입니다. 운영 PlayFab 로그인·진행 저장·실제 구매/영구 No Ads·광고/UMP·삭제 접수·최종 서명 AAB·Play 제공물·실ARM64/16KB 결과를 검증하지 않습니다. 이전 [LD 정확 설치물·왕복](gameplay-roundtrip-20261024.ko.md)은 별도 근거로 유지하며 서로 다른 환경의 오류 숫자를 합산하지 않습니다.

다음은 운영 Console 현재값·서명/운영 자료 대조와 최종 후보 준비입니다. [전체 남은 작업](remaining-recovery-work.ko.md)을 따르며 정확한 전체 완료율은 세부 점수 근거 부재로 미산정입니다.
