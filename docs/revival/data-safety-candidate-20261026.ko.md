# 최종 후보의 개인정보 신고 준비 — 2026-10-26

정상 계정·진행 저장과 SDK 경로를 대조한 **기술 초안**입니다. 현재 제공 중인24번과 향후 최종 후보의 처리를 합쳐 신고해야 하며, 이 문서가 Console 답안 저장·심사 제출·최종 제공물 검증은 아닙니다. 원래 정책·삭제 안내와 문의 주소는 유지합니다.

## 현재 확실한 방향

정상 Google/PlayFab 인증·닉네임·게임 진행에는 기기 밖 전송이 있으므로 `수집 없음`과 맞지 않습니다. 현재 export의 암호화 답변은 빈칸이며 NO로 해석하지 않습니다.782개 데이터 row에는 선택지·설명·조건부 항목도 있으므로 전체를 필수 미답이나 실제 수집 유형으로 세지 않습니다.

| 데이터 유형 | 근거가 있는 후보 처리 | 남은 적용 조건 |
| --- | --- | --- |
| 이름·사용자 ID | 닉네임/계정 신원과 PlayFab 소유 계정 저장 | 별칭을 실명 수집으로 표현하지 않음. 공급자별 공유 역할 대조 |
| 구매 내역 | 기존 구매 조회·거래/복원·No Ads와 IAP 처리 | 새 결제의 선택 가능성과 Main의 자동 기존 구매 조회를 구분. 시험32의 서버 Verified를 정상 운영 경로로 전용하지 않음 |
| 게임 활동 | 캐릭터·튜토리얼·진행 등 명시적 PlayFab 저장 | 로컬 도감/장비/아이템 전부를 서버에 보낸다고 확대하지 않음 |
| 기기/기타 ID·앱 상호작용 | PlayFab 장치/세션 수집 조건, Unity IAP 기본 identity/구매 경로 | 광고·분석 Denied를 SDK 전체 통신 OFF로 해석하지 않음. 실제 값과 로그인 응답/초기화 조건은 별도 |
| 대략적 위치 | PlayFab의 로그인 IP 기반 처리와 SDK 제공자 안내 | 서버 구성·최종 제공자 범위 연결. 정밀 위치/GPS 수집으로 바꾸지 않음 |
| 진단·앱 성능 | 제공자의 IAP/광고 공개표와 조건부 진단 경로 | 현재 엔진 진단 OFF와 별도 IAP 경로를 구분. 모든 성능 유형을 무조건 YES/NO로 확정하지 않음 |
| 이메일 | 정상 PGS 기본 경로에 추가 EMAIL scope 없음, IAP의 선택 이메일 기능과 문의 경로 구분 | Google 로그인이나 SDK 일반표만으로 자동 이메일 수집을 단정하지 않음 |

운영 타이틀의 데이터 수집 화면에서 **집중 시간 보고·장치 정보 보고·전체 IP 저장·새 수집 기능 자동 실행 ON**을 읽었습니다. source의 수집 비활성화 flag가 false라는 관측과 연결합니다. 오늘 최종 player가 전송한 실제 payload나 로그인 응답의 Gather flag를 읽은 것은 아닙니다. 전체 IP 저장을 요청 메모리에서만 처리 후 폐기한다고 가정하지 않으며, 공급자 보관 기간은 별도 확인 대상입니다. 설정은 변경하지 않았습니다.

## 공급자·광고·전송 범위

- PlayFab의 저장·인증 전송, IAP5.4.3의 기본/구매 경로, PGS와 광고 SDK의 처리 조건을 나눕니다. 처리자 공유 예외는 수집을 지우지 않으며, 공급자 역할이 확인되지 않은 행은 공유 NO로 임의 입력하지 않습니다.
- 운영 광고 source gate는 여전히 false/null입니다. GMA25.5의 공식 수집·공유 공개는 실제 광고 허용 후보에 적용할 조건부 행입니다. Google 샘플 실행이나 일반 파트너 수만으로 실제 운영 전송을 판정하지 않습니다. 현재fullscreen 후보는 성인 범위이고9+ 대상 설정 자체가 미성년 광고 승인도 아닙니다.
- 기본 PlayFab/IAP의 HTTPS 경로와 PGS·GMA의 전송 공개는 암호화 YES 방향 근거입니다. 최종 source/config·포함 SDK·manifest·실제 제공물에 같은 범위가 들어가는지 연결한 뒤 최종 답을 확정합니다. Photon의 범용 API 선언만으로 정상 Photon 연결·미암호화 전송을 추가하지 않습니다.
- No Ads는 광고 요청 차단 권한이며 로그인·저장·IAP·가능한 UMP 처리까지 모두 끄는 기능이 아닙니다. 기기에 저장한 연령 구간은 생년월일 업로드와 다르고, 연령 화면보다 앞선 인증·구매 초기화도 함께 고려합니다.

공식 근거: [Google Play 신고 정의](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en), [Unity IAP5.4+ 신고표](https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety), [IAP 역할·선택 필드](https://docs.unity.com/en-us/iap/privacy-and-consent/overview), [목적별 consent](https://docs.unity.com/en-us/cloud/developer-data-framework/end-user-consent), [PlayFab API 기능](https://learn.microsoft.com/en-us/xbox/playfab/live-service-management/gamemanager/api-feature-settings-in-the-playfab-game-manager), [PGS 처리](https://developer.android.com/games/pgs/data-collection), [GMA25.5 처리](https://developers.google.com/admob/android/privacy/play-data-disclosure), [UMP](https://developers.google.com/admob/unity/privacy).

## 최종 후보에 연결할 순서

1. 제공 중인24번의 SDK·기능과 최종 후보의 공급자/동의/수집 범위를 합쳐 실제 해당 유형을 확정합니다.
2. 유형별 필수/선택·목적·일시 처리·공유 역할과 전체 전송 암호화의 근거를 연결합니다. 수집되지 않은 것으로 확인하지 않은 유형을 NO로 메우지 않습니다.
3. 기존 공개 개인정보/삭제 안내와 Console 답안을 대조한 뒤 필요한 수정·게시/제출을 합니다. 삭제는 기존 문의 주소에서14일 이내 처리하는 운영 약속을 유지하며, 접수 수락을 모든 공급자의 삭제 완료로 표현하지 않습니다.
4. 정확한 최종 AAB/Play 제공물과 로그인·저장·구매/No Ads·연령/UMP 영향 검증을 연결합니다. 현재 새 운영 서명 AAB는 없습니다.

운영 입력의 빈 Title/legacy code1은 [별도 사전검사](operating-build-input-preflight.ko.md)가 검출합니다. 이 문서나 그 입력 검사로 광고 승인·키 사용·출시를 자동 허용하지 않습니다. [전체 잔여 목록](remaining-recovery-work.ko.md)을 유지합니다.
