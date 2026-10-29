# 운영 자료와 최종 후보 입력 대조 — 2026-10-26

이 문서는 실제 운영 화면의 읽기 관측과 source `9ce27677e2b32e259ea6e07310ce0c9337640784`의 정적 대조입니다. **새 운영 서명 AAB는 아직 없으며, 출시·신고·정책 통과나 운영 기능 실행 완료를 뜻하지 않습니다.** checkout은 `D:/codex/worktrees/tamer-working/Tamer`입니다. SDK 자체의 실행 범위는 [별도 검증](gma-116-20261026.ko.md)을 따릅니다.

## 2026-10-29 현재 Console·업로드 공개 인증서 재확인

사용자의 재확인 지시와 양 프로젝트의 실제 자원 반환·동의 뒤 정상 Chrome Browser Use로 운영 Monster Tamer를 읽었습니다. 전체 번들 두 페이지20개에서 최대versionCode26, production24의 Google Play 제공과26의 미승인을 확인했습니다. 소스27은 현재 관측보다 큰 후보값이며 예약·업로드·심사 수락된 번호는 아닙니다. 실제 빌드 직전 변경 가능한 번호/게시 상태는 필요한 범위로 다시 확인합니다.

현재 앱 무결성 메뉴는 Google Play로 보호됨→Play 스토어 보호→Play 앱 서명 관리로 이어집니다. 앱 서명 키와 업로드 키를 구분하고 업로드 공개 DER를 실제 다운로드했습니다. 다운로드 이벤트 대기는 시간 초과됐으나 해당 시각의 새678byte 파일 존재·인증서 파싱·SHA-256을 확인했고 기존 검토 upload DER 및 원본 키에서 추출해 둔 공개 DER와 byte 동일했습니다. 원래 다운로드와 비공개 owner+SYSTEM 근거 사본을 보존하며 지문/계정별 URL은 공개하지 않습니다. 개인키 본문·암호·새 challenge·운영 AAB 서명 검증은 실행하지 않았습니다.

정상 Browser Use 연결 확인을 이전 Windows URL 판독 중단의 해결로 표현하지 않습니다. 설정·확장/프로필·쿠키/CDP 우회, 새 탭·설정 저장/키 재설정·업로드/제출은 없었습니다. 이 읽기는 AdMob/UMP의 모든 지역·파트너와 Data safety 답안의 재검증이 아니며 수정 전fb13 plan/source-review는 계속 실행 보류입니다. 새 계획·source-review·실제 runtime/native pin·필요한 비밀 입력·운영 AAB·같은 Play 제공물 검증은 남아 있습니다.

## 2026-10-27 후속 준비 이력

아래 표와 빈 Title/legacy code1 설명은10월26일의 관측 이력입니다. 이후 비공개 로컬 Title을 준비하고 approved entry가 소비하는 ProjectSettings/개발 기준을27로 정합했습니다. [개발27의 실제 빌드·기본 정적검사·59파일 복구](baseline-version27-20261027.ko.md)와 [단일 입력/실행 없는 metadata 준비기](single-input-signing-20261027.ko.md)는 준비됐으며 새 운영 AAB는 여전히 없습니다. Console 최대26은 새 조회로 바꾸지 않습니다.

기존 inventory/country/adult·메시지 근거는 비공개 인덱스에서 정확한 원본 SHA로 연결했습니다. 현재 게시 메시지의 유효 파트너 집합과 지역·성인·활성화의 실제 검토/source binding은 미완료입니다. Other 유지 결정을 다시 묻거나 UI198/public199의 수치 일치를 새로운 승인 조건으로 만들지 않습니다. 운영 전환은 기존 근거의 의미와 적용 범위를 검토한 뒤 exact source/plan으로 이어갑니다.

현재 브라우저 제어 연결 미인식으로 필요한 후속 화면 읽기는 대기 중이며 설정·확장/앱 재시작으로 우회하지 않았습니다. [최신 현황](current-recovery-status.ko.md)과 아래 날짜별 관측을 구분합니다.

## 운영 Console

| 대상 | 실제 관측 | 해석·후속 |
| --- | --- | --- |
| 전체 번들 | 두 페이지의20개, 최대 versionCode26 | 다음 후보는 실제 빌드 직전 다시 조회한 최대값보다 커야 함.27은 현재 관측 기준의 최소 후보이지 이미 만든 파일 번호가 아님 |
| 프로덕션24/1.0.3 | 최신 production dashboard·release detail은 Google Play 제공/출시율100%,178개 국가 | 별도 bundle24 detail의 보관 처리/출시0 표시도 함께 보존. 한쪽 표시만으로 현재 전체 출시 중단을 단정하지 않음 |
| 프로덕션26/1.0.5 | 승인되지 않은 출시 | active bundle 표시는 심사 승인·제공 완료가 아님 |
| 공개 테스트 | 일시중지·비활성, 과거 v1.0.0.17 | 현재 테스터 제공으로 계산하지 않음 |
| 비공개 테스트 | 활성 Alpha에 버전 없음, 과거 두 트랙 일시중지 | 최근 별도 IAP Test 앱과 운영 앱의 옛 시험 트랙을 혼동하지 않음 |
| 내부 테스트 | 일시중지·비활성, 과거 v1.0.0_15 | 과거 row의 내부 테스터 제공 문구와 현재 중지 banner를 함께 기록. 트랙 재개/업로드 안 함 |
| 게시·정책·연령 | 미승인26과9세 이상·성인 포함 변경이 미제출. 기존 Families 닫기 문제 유지 |9+가 이미 모든 제공판에 반영됐다고 쓰지 않음. 사용자 결정인5초 추가조사 보류와 심사 통과를 구분 |

현재 Data safety export에는 수집 NO·가족정책 YES만 답변값이 있으며 전송 암호화는 **빈칸**입니다. 빈칸을 NO로 바꾸어 해석하지 않습니다.782개 row 중 선택지·조건부 설명도 포함되므로 나머지를 모두 필수 미답으로 세지 않습니다. Console 답안·검토 전송·게시 설정은 변경하지 않았습니다.

## AdMob·UMP

올바른 Monster Tamer 앱과 보상형 단위1개·미디에이션0·캠페인0, 앱 확인/준비 상태를 읽기 대조했습니다. 원래 source의 광고 App ID도 이 앱과 일치합니다. 실제 식별자·원시 내보내기는 공개하지 않습니다.

원래 메시지 iframe은 사용 가능한 내용을 표시하지 못했지만, DOM에 표시된 **같은 Google 서비스의 콘텐츠 주소**를 정상 브라우저 페이지로 열어 아래 내용을 확인했습니다. 인증·쿠키·보호 설정을 변경하거나 저장/게시하지 않았습니다. 이 방법이 과거 Windows helper의 URL 판별 오류를 수정했다는 뜻은 아닙니다.

| 범위 | 관측 | 남은 한계 |
| --- | --- | --- |
| 유럽 메시지 | Monster Tamer 이름의 연결 앱, 영어 메시지1개 게시됨, 최종 수정9/28 | 메시지 목록의 앱 이름 대조이며 message UI에서 별도 app ID까지 추출·대조한 것은 아님 |
| 미국 주 메시지 | 같은 앱 이름, 영어(en-US) 메시지1개 게시됨, 최종 수정9/29 | 게시 사실과 새 최종 player의 모든 지역/선택/No Ads 결합 검증은 별개 |
| 유럽 설정 | 일반 파트너 자동 포함198, 광고 소스 자동 추가OFF, 노출 범위 극대화ON. RTB 동의 추가 확인OFF, 적법한 이익/기본값ON, 광고 동의 모드·특수 기능2 OFF, 자체 목적0 | 현재 읽은 값이며 운영 source의 검토 gate를 자동으로 열지 않음 |
| 미국 설정 | 활동 중인 광고 파트너 사용339 | 유럽과 다른 범위의 명부이며 하나의 선택 수로 합치지 않음 |
| 내보내기·공개 목록 | 다운로드는 제공업체/정책 URL 전체 catalog1738행. 같은 날 Google의 공개 common JSON199항목(Google 포함) | 선택198·과거 preview210/고유209·전체 catalog·공개 common 목록을 동일 명부로 취급하지 않음. 유효 파트너 집합 차이는 미해결로 유지 |

Google은 common 목록을 갱신하며 제공업체에 여러 동의 framework ID가 있을 수 있습니다. [공식 관리 안내](https://support.google.com/admob/answer/9012903?hl=en)·[공개 common JSON](https://storage.googleapis.com/tcfac/commonly-used-providers.json)을 참고합니다. 이 사실만으로 현재 선택·미리보기 차이가 전부 해소됐다고 단정하지 않습니다. 광고·지역 승인 false/null과 source의 별도 연령 검토를 유지합니다. 현재 fullscreen 후보는 성인으로 제한돼 있으며9+ 대상 선택 자체가 아동·청소년 광고 승인은 아닙니다.

## 신고 대상과 실제 데이터 경로

| 범위 | 확인한 처리 | 신고에 반영할 한계 |
| --- | --- | --- |
| 계정·닉네임·진행 | 정상 Login/Main/Game 경로의 PlayFab 명시 전송·소유 계정 저장 | 후보의 수집 YES 방향 근거. SDK 이름만으로 이메일/연락처/사진/정밀 위치를 추가하지 않음 |
| 구매·No Ads | 정상 composition은 기존 store-trust 경로. Unity IAP5.4.3의 기본 identity·구매/진단 처리와 현재 Billing9 | 시험32의 PlayFab 서버 영수증 Verified를 일반 운영 서버 검증 주입·실행으로 확대하지 않음. No Ads나 Ads/Analytics Denied를 모든 SDK 처리 OFF로 쓰지 않음 |
| 광고·개인정보 선택 | GMA Unity11.6/Standard25.5·UMP4.0, 광고/연령·지역/source gate와 요청 조건 | 실제 sample의 native 실행·진단 통신은 격리 시험. 정상 운영의 실제 처리 실측으로 전용하지 않음 |
| 현재 전송 설정 | 비밀 제외 allowlist에서 PlayFab custom URL override·logger 없음, 기기정보/집중시간 수집 비활성화 flag는 꺼져 있음 | SDK 기본 HTTPS와 공급자 안내는 근거이지만 최종 artifact/config를 고정하기 전 전체 암호화·필수선택·공유/잔존을 일괄 확정하지 않음 |
| Photon | 후보 정상 게임의 연결 호출·클라이언트 사용 근거 미발견, PlayFab SDK의 범용 API 선언과 구분 | 브라우저에 Photon이 열려 있다는 이유만으로 후보의 수집·미암호화 제공자로 추가하지 않음 |

후속 읽기에서 운영 타이틀의 집중 시간·장치 정보 보고와 전체 IP 저장·새 데이터 수집 기능 자동 실행이 ON임을 확인했습니다. 이는 현재 source의 disable flag=false와 연결되는 설정 근거이며 실제 최종 player의 payload/로그인 응답은 아닙니다. [유형별 후보 신고표와 남은 조건](data-safety-candidate-20261026.ko.md)을 따릅니다. 설정 저장·계정 시험은 하지 않았습니다.

운영24의 실제 보관 AAB는 Unity6000.0.62f1/GMA Android23.2/UMP2.2/Billing8/IAP5.0.1입니다. 새 source의 Unity6000.3.25f1·IAP5.4.3·Authentication3.7.4·PlayFab SDK2.242.260805·PGS2.2.1/Games22 및 새 광고 SDK와 구분합니다. SDK 존재, 처리 조건, 실제 전송과 현재 제공판을 각각 연결해야 합니다. [Google 신고 정의](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en), [GMA 처리 안내](https://developers.google.com/admob/android/privacy/play-data-disclosure), [Unity IAP5.4+ 안내](https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety)를 기준으로 기술 초안을 정리했으며 기존 export를 그대로 새 신고로 제출하지 않습니다.

10월28일 공식 문서 대조에서는 [IAP5.4 미만 표](https://docs.unity.com/en-us/iap/privacy-and-consent/legacy-google-play-data-safety)의 개인정보/상위 No와 진단·기타 성능의 필수·일시 처리 상세 행을 함께 확인했습니다. 보관24의5.0.1에 현행5.4+의 기본 식별자·비일시 구매/진단·보관기간을 적용하지 않습니다. 기존 보관 버전 원장을 재사용한 문서 대조이며, 최신 실제 제공물의 동일성이나 payload를 새로 검증한 것은 아닙니다. [버전별 후보 적용 경계](data-safety-candidate-20261026.ko.md)를 따릅니다.

기존 [README 개인정보처리방침](../../README.md#개인정보처리방침)과 [앱 밖 삭제 문의](../account-deletion.ko.md)는 유지합니다. 기존 문의 메일로 운영자가14일 이내 처리하며 별도 삭제 서버·불필요 자체 보관을 추가하지 않습니다. 접수 수락·운영자 처리 목표·공급자의 전체 소거 완료는 별개입니다.

## 서명·빌드 입력에서 발견한 실제 공백

- 현재 활성 upload 공개 인증서는 기존 암호화 JKS의 공개 인증서와 일치하며 app signing 역할과 구분됩니다. 외부 비공개 복사본과 로컬 입력 도구의 수명/취소·소유 자손 정리, 합성 근거·독립 재검토를 준비했습니다. **실제 JKS 암호/개인키 challenge·운영 AAB 서명은 미실행**입니다. 인증서 값이나 키/암호를 공개하지 않습니다.
- 정상 운영 AAB 메뉴는 존재하지만 `ProjectSettings` code26을 그대로 쓰지 않습니다. 현재 legacy `BuildInfo`의 세 번째 값0을 증가시키므로 그대로 실행하면 **code1**을 계획합니다. Console 최대값을 기준으로 버전 입력·finish marker·존재하지 않는 출력 경로를 계획해야 합니다. [출력 선차단](../../Assets/Scripts/Editor/LegacyBuildOutputPlan.cs)은 보존합니다.
- 복원된 PlayFab 설정은 안전한 공백 TitleId이며 정상 AAB 메뉴에 운영값 자동 주입 scope가 없습니다. 현재 상태로 실제 서버 로그인을 할 수 있다는 증거가 아닙니다. 권위 있는 운영 연결값을 비공개 실행 scope에서 적용·검사·원복해야 합니다.
- PGS의 추적 설정과 runtime resource는 비공백·서로 일치합니다. 누락으로 분류하지 않으며 OAuth/실제 제공 인증서·최종 player 연결은 후속 확인입니다.
- 일반 메뉴, disabled preparation AAB, approved adult release, privacy-only APK는 서로 다른 entry/서명·source 검토 계약을 갖습니다. 외부 키와 승인 metadata가 모든 entry에 그대로 호환된다고 가정하지 않습니다. 최종 entry를 고정한 뒤 보호 입력·source/config/resource·서명·버전을 묶습니다.
- 기존 Drive 사본의 존재·내부 정합 이력과 현재 키의 실사용은 구분합니다. 현재 원본과 archive 각 역할을 연결하는 기존 metadata proof는 허용 위치에서 찾지 못했으며 과거 false 상태를 성공으로 바꾸지 않았습니다. 추가 백업/복호화가 필요하다고 아직 확정하지 않았습니다.

10월27일 [단일 입력 연결](single-input-signing-20261027.ko.md)을 구현하고 합성 검증·독립 소스 검토를 접수했습니다. 같은 입력으로 실제 Java challenge와 exact plan의 signing receipt를 연결하며, 소유 producer와 필요한 빌드 자손에만 암호를 전달합니다. 실제 운영 키·암호·AAB는 아직 미실행입니다. 현재 운영 승인과 새 actual build HEAD의 plan/source/runtime·인증서 근거를 준비한 뒤 입력창을 엽니다. 이전 15필드 metadata, 공개 인증서 일치·합성 challenge를 실제 AAB 서명 성공으로 계산하지 않으며 원본 JKS 계약과 일반 메뉴 외부 키 계약을 구분합니다.

현재는 **업로드할 새 운영 AAB가 없습니다.** 다음 순서는 정확 후보/운영 입력 준비→사용자의 필요한 비밀 입력→같은 source의 실제 AAB·서명/manifest/version/native/원복 검사→사용자 수동 업로드→Play 제공물·필요 기능/정책 확인입니다. 필요한 원격 상태만 빌드 직전 새로 대조하고 성공한 기기 시험·구매 복원을 기록 목적으로 반복하지 않습니다. [전체 잔여 계획](remaining-recovery-work.ko.md)과 실제16KB/5초 추가조사/CI·App Center의 명시 보류 범위를 유지합니다.

## 이후 준비한 입력 검사와 개발 진단

[별도 read-only 입력 검사](operating-build-input-preflight.ko.md)는 실제 빈 Title과 legacy 다음code1을 함께 검출하고 PGS 일치를 확인했습니다. 예상 거절을 준비 완료로 바꾸지 않습니다. [공통 개발 로그 조건](development-logging-20261026.ko.md)도 Editor/Development 심볼로 고쳐 C#6조건·관련 Unity7개를 확인했으며 새 운영 player 결과로 확대하지 않습니다.

계정 인가의 후속 읽기는 [최신 권한 범위](access-security.ko.md)에 반영했습니다. 초기403·재인증 대기를 현재 모든 계정 목록의 부재로 해석하지 않습니다. 실제 키 사용·새 운영AAB·광고 승인/동의·제공물/스토어 검증은 계속 별도입니다.
