# 최종 후보의 개인정보 신고 준비 — 2026-10-26

정상 계정·진행 저장과 SDK 경로를 대조한 **기술 초안**입니다. 2026-10-26 Console 읽기에서 제공 중이던24번과 향후 최종 후보의 처리를 합쳐 신고해야 합니다. 10월27일 소스·입력 준비 상태의 대조를 보완했으며, 이 문서가 Console 답안 저장·심사 제출·최종 제공물 검증은 아닙니다. 원래 정책·삭제 안내와 문의 주소는 유지합니다.

## 2026-10-29 현재 신고·실제 SDK 연결

10월29일 현재 Console의 수집/공유 No와 가족 정책 약속 Yes·미리보기 수집/공유 없음을 값을 바꾸지 않고 읽었습니다. 암호화 질문은 No 분기에 표시되지 않아 No로 해석하지 않습니다. 같은 후보27 AAB metadata에서 GMA25.5/UMP4/IAP5.4.3/Billing9/Core1.18/Auth3.7.4/Games22를 확인했습니다. [10월29일 수정표](data-safety-submission-plan-20261029.ko.md)는 이 불일치와 실제 SDK·제공24의 적용 범위, PlayFab 처리자 공유 예외 근거 및 남은 유형별 답안을 연결합니다. Console 저장/제출·업로드·실제 운영 기능/심사 완료는 별도입니다.

10월29일 후속으로 사용자 후보27 초안 업로드·[같은 Play 앱서명 APK 기본 검사](play-candidate27-static-20261029.ko.md)까지 연결했습니다. 현재27은 업로드된 코드이며 신고 저장/제출·운영 기능·정책 심사와 모든 기기별 split 검증은 별도입니다.

## 현재 확실한 방향

정상 Google/PlayFab 인증·닉네임·게임 진행에는 기기 밖 전송이 있으므로 `수집 없음`과 맞지 않습니다. 10월26일 읽은 export의 암호화 답변은 빈칸이며 NO로 해석하지 않습니다.782개 데이터 row에는 선택지·설명·조건부 항목도 있으므로 전체를 필수 미답이나 실제 수집 유형으로 세지 않습니다.

| 데이터 유형 | 근거가 있는 후보 처리 | 남은 적용 조건 |
| --- | --- | --- |
| 이름·사용자 ID | 닉네임/계정 신원과 PlayFab 소유 계정 저장 | 별칭을 실명 수집으로 표현하지 않음. 공급자별 공유 역할 대조 |
| 구매 내역 | 기존 구매 조회·거래/복원·No Ads와 IAP 처리 | 새 결제의 선택 가능성과 Main의 자동 기존 구매 조회를 구분. 시험32의 서버 Verified를 정상 운영 경로로 전용하지 않음 |
| 게임 활동 | 캐릭터 선택·튜토리얼·진행 등 명시적 PlayFab 저장은 기타 앱 활동 매핑 | 실제 성별이나 자유 서술 콘텐츠로 분류하지 않음. 로컬 도감/장비/아이템 전부를 서버에 보낸다고 확대하지 않음 |
| 기기/기타 ID·앱 상호작용 | PlayFab 자동 장치/세션 수집 조건, 선택된 복원 로그인 방식의 명시적 기기 ID·OS·모델 또는 CustomID 전송, Unity IAP 기본 identity/구매 경로 | 자동 장치 보고 설정과 직접 로그인 요청을 구분. 모든 이용자가 기기 로그인 방식을 쓰는 것은 아니며 광고·분석 Denied도 SDK 전체 통신 OFF가 아님. 실제 값과 로그인 응답/초기화 조건은 별도 |
| 대략적 위치 | PlayFab의 로그인 IP 기반 처리와 SDK 제공자 안내 | 서버 구성·최종 제공자 범위 연결. 정밀 위치/GPS 수집으로 바꾸지 않음 |
| 진단·앱 성능 | 제공자의 IAP/광고 공개표와 조건부 진단 경로 | 현재 엔진 진단 OFF와 별도 IAP 경로를 구분. 모든 성능 유형을 무조건 YES/NO로 확정하지 않음 |
| 이메일 | 정상 PGS 기본 경로에 추가 EMAIL scope 없음, IAP의 선택 이메일 기능과 문의 경로 구분 | Google 로그인이나 SDK 일반표만으로 자동 이메일 수집을 단정하지 않음 |

새 후보의 앱 내 삭제 기능은 이용자가 기능에 진입할 때 인증된 계정으로 사용 가능 구성을 조회하고, 확인 후 삭제 요청 ID와 확인 의사를 전송하는 경로가 있습니다. 로컬 기능 구성만으로 자동 제출하지 않으며 실제 제공판의 사용 가능 범위·접수/회신은 별도 검증입니다. 이를 이메일 수집·신규 결제·별도 서버 도입으로 분류하지 않습니다.

2026-10-26 운영 타이틀의 데이터 수집 화면에서 **집중 시간 보고·장치 정보 보고·전체 IP 저장·새 수집 기능 자동 실행 ON**을 읽었습니다. 당시 source의 수집 비활성화 flag가 false라는 관측과 연결합니다. 이 기록은 새 최종 player의 실제 payload나 로그인 응답의 Gather flag를 확인한 결과가 아닙니다. 전체 IP 저장을 요청 메모리에서만 처리 후 폐기한다고 가정하지 않으며, 공급자 보관 기간은 별도 확인 대상입니다. 설정은 변경하지 않았습니다.

## 공급자·광고·전송 범위

- PlayFab의 저장·인증 전송, IAP5.4.3의 기본/구매 경로, PGS와 광고 SDK의 처리 조건을 나눕니다. 처리자 공유 예외는 수집을 지우지 않으며, 공유 NO 근거가 없는 별도 서비스·처리 활동을 임의로 NO로 입력하지 않습니다.
- IAP5.4+의 [공식 SDK 신고표](https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety)는 해당 수집 행의 공유를 No로 명시합니다. 이 공개 근거를 유지하되 앱 전체·다른 SDK의 공유 No로 확대하지 않고, 이메일 등 선택 기능의 표를 실제 native 수집 결과로 바꾸지 않습니다. [역할·보관 안내](https://docs.unity.com/en-us/iap/privacy-and-consent/overview)는 처리 활동별 역할을 구분하므로 native Insights 기록별 역할·보관 범주를 별도로 연결합니다. 서비스 로그90일도 모든 native 데이터의 삭제 완료 기한이 아닙니다.
- 현재 성인 후보 소스의 production·regional·adult 승인은 true이며, 미성년3개 구간은 false·별도 개인정보 전용 환경/binding은 false/null로 유지합니다. 이는 실제 운영 SDK·제공물·동의 검증 완료가 아닙니다. GMA25.5의 공식 수집·공유 공개는 실제 광고 허용 후보에 적용할 조건부 행입니다. Google 샘플 실행이나 일반 파트너 수만으로 실제 운영 전송을 판정하지 않습니다. 현재fullscreen 후보는 성인 범위이고9+ 대상 설정 자체가 미성년 광고 승인도 아닙니다.
- 기본 PlayFab/IAP의 HTTPS 경로와 PGS·GMA의 전송 공개는 암호화 YES 방향 근거입니다. 최종 source/config·포함 SDK·manifest·실제 제공물에 같은 범위가 들어가는지 연결한 뒤 최종 답을 확정합니다. Photon의 범용 API 선언만으로 정상 Photon 연결·미암호화 전송을 추가하지 않습니다.
- 10월28일 source `abb8552`의 정상 앱 호출을 대조했습니다. 인증·진행 저장·앱 내 삭제는 PlayFab SDK 경로이며, [삭제 factory](../../Assets/Scripts/UI/CloudScriptDeletionClient.cs) 등록 자체가 자동 접수는 아닙니다. `Assets/Scripts`에는 Photon 연결 호출을 찾지 못했고 PlayFab의 범용 인증 토큰 API 선언을 실제 Photon 연결로 세지 않습니다.
- 같은 작업본의 로컬 runtime 설정을 비공개로 읽어 PlayFab 기본 URL 사용(HTTPS)·실시간 원격 로깅 OFF를 확인했습니다. 장치/집중 시간 수집 비활성화는 둘 다 false였습니다. URL·Title·비밀값은 공개하지 않으며 이 관측을 제공24의 설정이나 최종 player의 전체 전송 암호화·실제 수집 결과로 전용하지 않습니다.
- 직접 [영수증 HTTP 어댑터](../../Assets/Scripts/IapReceiptHttpVerifier.cs)는 HTTPS 입력 검사와 `redirectLimit=0`, [삭제 HTTP 어댑터](../../Assets/Scripts/Privacy/HttpDeletionGateway.cs)는 HTTPS origin 검사와 `AllowAutoRedirect=false`를 사용합니다. 두 어댑터의 일반 앱 조합 호출은 찾지 못했으며 기본 삭제 경로는 위 CloudScript입니다. 이 보호를 PlayFab SDK 전체에 확대하지 않습니다. SDK의 범용 URL override와 redirect 경계만으로 실제 평문 전송 결함을 확정하지도 않습니다.
- No Ads는 광고 요청 차단 권한이며 로그인·저장·IAP·가능한 UMP 처리까지 모두 끄는 기능이 아닙니다. 기기에 저장한 연령 구간은 생년월일 업로드와 다르고, 연령 화면보다 앞선 인증·구매 초기화도 함께 고려합니다.

공식 근거: [Google Play 신고 정의](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en), [Unity IAP5.4+ 신고표](https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety), [IAP 역할·선택 필드](https://docs.unity.com/en-us/iap/privacy-and-consent/overview), [목적별 consent](https://docs.unity.com/en-us/cloud/developer-data-framework/end-user-consent), [PlayFab API 기능](https://learn.microsoft.com/en-us/xbox/playfab/live-service-management/gamemanager/api-feature-settings-in-the-playfab-game-manager), [PGS 처리](https://developer.android.com/games/pgs/data-collection), [GMA25.5 처리](https://developers.google.com/admob/android/privacy/play-data-disclosure), [UMP](https://developers.google.com/admob/unity/privacy).

## 2026-10-29 실제 후보와 공급자 요청 경로

후보27의 광고 resource가 exact plan과 같고 최종 manifest에 GMA InitProvider·광고 ID/AdServices 권한이 포함됨을 확인했습니다. manifest의 측정 지연 metadata 없음·명시 cleartext/network-security 속성 없음은 선언 관측이며, 동의 전 실제 송신·전체 TLS·수집 없음 판정으로 쓰지 않습니다. 현재 제공24와 후보27의 합집합, 필수/선택·공유 역할·목적은 실제 기능 조건과 공식 버전 자료에 연결합니다.

10월29일 다시 읽은 [IAP5.4+ 역할별 안내](https://docs.unity.com/en-us/iap/privacy-and-consent/overview)는 처리자 범위의 IAP/거래 자료 요청을 개발자가 본인 확인 후 Unity IAP 지원 양식으로 전달하도록 설명합니다. Webshop 로그인/CodaPay 및 Unity 자체 재무·감사·보안·부정행위 방지 등 독립 처리 범위는 이용자가 Unity 지원 양식을 이용하는 경로와 구분합니다. 같은 거래도 처리 목적별 역할이 다를 수 있습니다. 이 안내를 실제 메일 접수·지원 요청 발송·자료 삭제 완료나 제공24의 IAP5.0.1에 소급하지 않습니다. 기존 운영자 문의 처리14일 목표를 공급자 전체 소거 기한으로 확대하지 않습니다.

연결 Gmail의 현재 계정이 운영/문의 계정과 달라 그 연결의 검색0건은 운영 메일의 통지·요청 부재 근거에서 제외했습니다. 본문/첨부 읽기·발신·계정 전환 없이 중단했으며 개인정보·계정 식별값은 이 문서에 남기지 않습니다. 현재 Console 답안·제공판 범위와 공개 안내를 직접 대조한 뒤 미확인 기술 조건만 정리합니다.

## 최종 후보에 연결할 순서

### 2026-10-28 버전별 적용 범위 보완

제공24의 **보관 AAB 메타데이터**는 Unity6000.0.62f1·GMA Android23.2/UMP2.2·Billing8·IAP5.0.1로 식별돼 있습니다. 이미 확보한 버전을 다시 미확인으로 돌리지 않되, 보관 파일과 최신 Play 제공물의 동일성·실제 초기화/전송은 별도로 연결합니다. 현재 후보 IAP5.4.3/Android GMA25.5의 공개표를 구버전에 소급하지 않습니다.

IAP5.0.1에는 [5.4 미만 안내](https://docs.unity.com/en-us/iap/privacy-and-consent/legacy-overview)와 [해당 Data safety 표](https://docs.unity.com/en-us/iap/privacy-and-consent/legacy-google-play-data-safety)를 적용합니다. 개인정보 없음·상위 수집/공유 No 안내와 함께, 상세 표의 진단·기타 앱 성능은 수집 Yes/공유 No·필수·일시 처리·앱 기능으로 설명된다는 점을 보존합니다. 이를 무통신으로 축약하거나5.4+ 기본 식별자·구매/진단·90일 보관 설명을 복사하지 않습니다. 현행 GMA 공개표도23.2의 같은 버전 처리 확정 자료는 아닙니다.

현재5.4.3의 [native 소스 경로 대조](iap-native-dataflow-20261028.ko.md)에서는 native Connect·별도 UGS 초기화·identity/cache·구매 Insights의 조건을 구분했습니다. pinned Core의 진단 API no-op을 별도 구매 전송까지 확대하지 않으며, 공식 SDK 표를 개별 실행의 실제 필드/수신 실적으로 바꾸지 않습니다.

1. 제공 중인24번의 SDK·기능과 최종 후보의 공급자/동의/수집 범위를 합쳐 실제 해당 유형을 확정합니다.
2. 유형별 필수/선택·목적·일시 처리·공유 역할과 전체 전송 암호화의 근거를 연결합니다. 현재 제공하는 어느 버전/지역/이용자라도 해당 유형이 필수이면 전체를 선택 수집으로 낮추지 않으며 일시 처리도 양식에 답합니다. 미배포 후보는 배포 준비 대상으로 구분하고, 수집되지 않은 것으로 확인하지 않은 유형을 NO로 메우지 않습니다. [Google의 합집합·선택 수집 정의](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en).
3. 기존 공개 개인정보/삭제 안내와 Console 답안을 대조한 뒤 필요한 수정·게시/제출을 합니다. 삭제는 기존 문의 주소에서14일 이내 처리하는 운영 약속을 유지하며, 접수 수락을 모든 공급자의 삭제 완료로 표현하지 않습니다.
4. 정확한 최종 AAB/Play 제공물과 로그인·저장·구매/No Ads·연령/UMP 영향 검증을 연결합니다. 10월29일 exact source `eba77a66`의 [운영 후보27 AAB](operating-candidate27-20261029.ko.md)를 생성·기본 검사/복구했고 named binary resource 독립 추출·최종 manifest 추가 대조까지 연결했습니다. Play 제공물·기능·실제 통신·신고 제출은 별도입니다.

2026-10-26 source `289d8ba1889aff153b86355ea259dd60afe68c48`의 [실제 입력 사전검사](operating-build-input-preflight.ko.md)는 당시 빈 Title과 legacy 다음 code1/관측 최대26 충돌을 검출했습니다. 10월27일에는 [비공개 로컬 Title 준비·개발 기준27 검증](baseline-version27-20261027.ko.md)을 진행했으며 legacy 계산 이력과 구분합니다. 이 입력 준비와 개발 APK 결과는 광고 승인·운영 키 사용·운영 서명 AAB 생성·출시를 자동 허용하지 않습니다. [전체 잔여 목록](remaining-recovery-work.ko.md)을 유지합니다.
