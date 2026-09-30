# Data Safety 변경안 — 미제출 검토 초안

2026-09-30. 이 문서는 Console 입력 준비용이며 저장·제출·게시하지 않았다. 개인정보처리방침 본문 후보는 [출시 수정안](privacy-policy-release-draft.ko.md)에 있다. 과거 초안은 Git 이력에서 확인할 수 있으며 당시 미완료 상태를 현재 구현 상태로 재사용하지 않는다.

## 기준과 현재 신고의 차이

- 구현 검토 기준: `85892a9ec299a36b011c6d8045b2342e91ba93cb`. 문서 브랜치 기준 main: `e62f86a5fff68db975cc99681deedaf45005cb61`. 두 커밋 사이 변경은 RELRO 검사·문서이며 아래 제품 데이터 경로와 SDK 선언은 같다.
- 통합 담당자의 2026-09-30 Console 읽기 보고: Data Safety는 2024-09-30의 수집 NO·전송 암호화되지 않음·Families YES를 유지한다. 현재 전체 제공은 versionCode 24 / 1.0.3, 최대 26은 미승인이다. 대상 연령 9~12·13~15·16~17·18+ 변경은 검토 전송 준비 상태다. 새 9+ 방향을 현재 심사·제공 완료로 쓰지 않는다.
- 정책 URL은 [기존 GitHub 루트](https://github.com/ChoiDaeYoung-94/Tamer)와 [개인정보처리방침 앵커](https://github.com/ChoiDaeYoung-94/Tamer#개인정보처리방침)를 유지한다. 외부 [삭제 안내](../account-deletion.ko.md)는 있으나 Console의 삭제 URL 등록 여부는 현재 분기 화면에서 확인하지 못했다. 미등록으로 단정하지 않는다.
- 소스 선언은 Unity IAP 5.4.3, Authentication 3.7.4, GMA Unity 11.5.0 / Android 25.4.0, UMP 4.0.0이다. 최종 출시 AAB의 해석된 의존성과 운영 24의 SDK 구성은 별도 증거가 필요하다. 합성 서명 AAB·시험 앱·새 소스를 운영 24의 메타데이터로 사용하지 않는다.

소스에는 로그인 및 PlayFab 저장을 위해 기기 밖으로 보내는 데이터가 있으므로 이 구현의 답안 후보는 **수집 YES**다. 실제 신고는 제공 중인 버전들과 사용 SDK를 함께 대조한다. 로컬 전용 정보는 전송이 없는 범위에서 수집 신고 대상과 구분한다. 서비스 제공자 예외는 공유 항목에 관한 것으로 수집 신고를 없애지 않는다. [Google Play 정의](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en).

## 데이터 유형별 답안 후보

`확정`은 아래 소스 경로의 사실이며 운영 24까지 검증했다는 뜻이 아니다. `후보`와 `확인 필요`는 Console 최종 선택 전 남은 대조다. 목적의 영문 괄호는 Console 선택값이다.

| 유형 | 실제 정보·경로 | 수집 / 일시적 처리 | 목적 후보 | 필수·선택 / 공유 판단 |
| --- | --- | --- | --- | --- |
| 개인정보: 사용자 ID | PlayFab ID, PGS 인증 코드·로그인 연결 식별 정보, 기기/custom 계정 식별자. `Login.cs` → PlayFab | 확정 YES / 저장 계정 정보는 NO | 앱 기능, 계정 관리 | 현재 계정 기반 게임 경로에는 필요. 로그인 방식 선택과 데이터 수집 선택을 혼동하지 않는다. PlayFab 서비스 제공자 해당 범위의 공유 예외는 계약 대조 필요 |
| 기기 또는 기타 ID | `deviceUniqueIdentifier`를 AndroidDeviceId로 사용하거나 기존 custom ID 경로를 선택. OS·모델 포함 | 확정 YES / 계정 연결 값은 NO | 앱 기능, 계정 관리 | 해당 로그인 방식에 필요. 모든 이용자가 이 유형 수집 없이 사용할 수 있는지와 SDK 중복 수집을 확인하기 전 전체 선택으로 신고하지 않는다 |
| 개인정보: 이름 | 이용자가 입력한 게임 닉네임, PlayFab UserData `NickName` | 확정 YES / NO | 앱 기능, 계정 관리 | 캐릭터 생성·게임 기능에 필요. 실명 수집으로 표현하지 않는다. 공유 예외 조건은 위와 같음 |
| 앱 활동: 기타 사용자 제작 콘텐츠·기타 활동 | `Sex`는 캐릭터 설정이며 실제 성별이 아니다. `Tutorial`, `Gold`, `Power`, `AttackSpeed`, `MoveSpeed`, `AllyMonsters` 등 게임 진행 및 동료 상태를 PlayFab에 저장 | 확정 YES / NO | 앱 기능 | 게임 저장에 필요. 캐릭터 설정은 사용자 제작 콘텐츠, 진행은 기타 활동 후보로 분리해 Console 유형 대조 |
| 금융 정보: 구매 내역 | Google Play 구매·재조회, No Ads 상품·거래/권한 정보, PlayFab `GooglePlay` 권한 값. 일반 경로에 운영 서버 영수증 검증기 주입은 확인되지 않음 | 구매·권한 처리 YES / 저장 권한은 NO | 앱 기능; SDK 분석·사기 방지는 서비스별 대조 | 구매 선택 자체와 시작·스토어 연결 때의 필수 SDK 수집을 분리. 카드·은행 전체 번호를 게임이 받는다고 쓰지 않음 |
| 대략적 위치 | 광고 SDK의 IP 기반 위치, IAP의 국가 정보 후보. 게임의 GPS 위치 수집 근거는 없음 | SDK 후보 YES / SDK·항목별 확인 | 광고 또는 마케팅, 분석, 사기 방지 / IAP 앱 기능 | 광고 ID 선택과 별개로 IP 수집의 전 사용자 선택 여부 확인. IAP 자료의 일시 처리 값을 광고 경로에 확대하지 않음 |
| 앱 활동: 앱 상호작용 | 광고 SDK의 실행·클릭·영상 시청 정보 후보 | SDK 후보 YES / 확인 필요 | 광고 또는 마케팅, 분석, 사기 방지 | 실제 제공 버전·광고 요청·동의 설정 대조. 광고 버튼을 누르지 않는 것만으로 시작 시 SDK 수집까지 선택이라고 보장하지 않음 |
| 앱 정보 및 성능: 비정상 종료 로그·진단·기타 성능 | IAP와 광고 SDK의 진단·성능 자료 후보 | SDK 후보 YES / 공급자 표 대조 | 앱 기능, 분석; 광고 경로는 사기 방지 추가 대조 | IAP 공식 표는 필수·비일시적 수집으로 안내. 앱의 동의 기본값이 실제 항목을 줄이는 범위를 확인 |
| 기기 또는 기타 ID / 사용자 ID: SDK | Unity 설치·플레이어·세션 식별자, 광고/app set/해당되는 계정 식별자 후보 | SDK 후보 YES / 식별자별 확인 | 앱 기능, 분석 / 광고 또는 마케팅, 사기 방지 | 개발자 자체 식별자와 함께 유형별 합산. 광고 ID의 선택 가능성을 모든 식별자에 확대하지 않음 |
| 이메일 주소·이메일 내용 / 기타 사용자 제작 콘텐츠 | 지원·삭제 요청 시 이용자가 보내는 회신 주소와 최소 확인 자료 | 지원 창구에서 처리. 앱 외 이메일과 앱에서 전송하는 정보의 신고 범위를 구분 | 개발자 커뮤니케이션, 계정 관리 | 문의는 선택, 삭제 소유 확인은 필요한 범위. Unity IAP 전체 표의 이메일 YES를 이 Google Play 경로의 실제 이메일 전송으로 단정하지 않음 |

장비 소유 목록·장착 슬롯·로컬 수집 상태는 `DataManager`의 계정에 귀속된 `.inventory` 경로에 저장하며 현재 이 파일의 클라우드 업로드 경로는 없다. 서버 `AllyMonsters`와 별개다. 연령 선택은 생년월일을 입력받지 않는 로컬 연령 그룹이다. SDK에 적용하는 아동·연령 처리 신호는 별도 처리로 검토한다. 로컬 전용 파일·PlayerPrefs를 그 자체로 서버 수집이라 신고하지 않는다.

### 공급자 공식 자료와 적용 한계

- [Unity IAP 5.4 이상 Data Safety 표](https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety)는 식별자·구매·진단·성능 등의 수집, SDK 전송 암호화 YES, 공유 NO를 안내한다. 대략적 위치·이메일의 일시 처리 표기도 있다. 전체 제품 표이므로 사용하지 않는 Webshop/결제 공급자 항목까지 앱에 있다고 복사하지 않는다. 기본 데이터와 선택 데이터의 구분은 [IAP 개인정보 안내](https://docs.unity.com/en-us/iap/privacy-and-consent/overview)와 실제 호출·설정을 함께 대조한다.
- [AdMob Android 공식 자료](https://developers.google.com/admob/android/privacy/play-data-disclosure)는 IP·상호작용·진단·식별자의 자동 수집 및 공유와 TLS를 안내한다. **2026-09-30 열람 페이지는 25.5.0 기준**이므로 선언 25.4.0의 상세 표로 확정하지 않는다. 현재 앱의 운영 광고 gate OFF는 관리 코드의 요청 제한이며 SDK의 모든 native 전송 0을 입증하지 않는다. 운영 24에 소급하지 않는다.
- [PGS 공식 자료](https://developer.android.com/games/pgs/data-collection?hl=en)는 게임 계정 신원과 분석·진단의 자동 수집, 기능별 추가 수집과 HTTPS를 안내한다. 업적·친구·Saved Games 등의 예시를 사용 확인 없이 추가하지 않는다. 일반 core Play services의 수집 없음 안내를 PGS나 AdMob 전체에 적용하지 않는다.

## 공유·보호·삭제 답안

| Console 항목 | 작성 후보 / 확정 조건 |
| --- | --- |
| 제3자 공유 | AdMob 공식 공유 자료를 반영할 후보다. PlayFab·Unity·Google을 이름만으로 모두 공유 YES 또는 모두 예외 NO로 묶지 않는다. PlayFab 위탁 처리, Unity 처리자/독립 처리 활동, PGS·Play 이용자 요청, 광고 파트너의 실제 전달·목적·계약을 구분해 유형별 선택 |
| 서비스 제공자 예외 | 개발자의 지시로 대신 처리하는 범위에만 적용. 해당 계약과 처리 활동 확인 필요. 독립 목적의 분석·광고를 단순 위탁으로 가정하지 않음 |
| 전송 중 암호화 | PlayFab 기본 HTTPS, SDK 공식 HTTPS/TLS 안내는 있음. 최종 빌드 설정·전체 엔드포인트·제공 24 대조 후 전체 YES 여부 결정. 암호화 저장·종단 간 암호화와 구분. 소스 검토만으로 기존 Console NO를 자동 변경하지 않음 |
| 삭제 요청 수단 | 기존 이메일과 공개 웹 안내는 준비됨. 앱 자동 접수 경로는 구현·시험 근거와 실제 제공 버전을 구분. Console 삭제 URL 등록·메일 수신/확인/담당 처리 운영 확인 필요 |
| Families | 현재 Console YES는 관측 사실. 9+ 선택·MaxAdContentRating G·시험 광고 성공만으로 아동 데이터/광고/동의 전 항목 적합성을 확정하지 않음 |

Unity는 활동별로 처리자 또는 독립 처리자 역할이 달라진다고 안내한다. Google Play의 공유 예외 판단은 법적 역할 명칭만으로 자동 확정하지 않는다. 실제 수신자 목록과 서비스별 처리 범위가 필요하다. AdMob Console의 선택된 파트너 수는 실제 전송 업체 수나 설치된 어댑터 수가 아니다. [Unity 역할 안내](https://docs.unity.com/en-us/iap/privacy-and-consent/overview), [Google 공유 정의](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en).

## 삭제·보관에 이미 반영할 사실

정상 접수가 확인되면 앱은 로그아웃하며 소유가 확인된 현재 진행·생성 백업·귀속 inventory와 형식/소유가 확인된 과거 권한 사본을 정리한다. 삭제 때 새 `.deletion-entitlement-*` 사본을 만들지 않는다. 소유 불명·타인·형식 불량 파일 및 접근 불가 기기의 파일까지 지웠다고 안내하지 않는다. 새 계정에서 기존 No Ads가 실제 복원되는지는 미검증이다.

CloudScript의 Server/DeletePlayer는 title 데이터 삭제를 비동기 접수한다. PlayStream 이벤트·publisher 계정·연결 등의 범위는 별도로 확인한다. 접수 응답은 전체 소거 완료가 아니다. [PlayFab API 범위](https://learn.microsoft.com/en-us/rest/api/playfab/server/account-management/delete-player?view=playfab-rest).

운영자는 삭제 후 자체 계정·진행 보관용 사본을 만들지 않으며 이메일 원문·소유 확인 자료는 처리 종료 후 삭제하고 별도로 보관하지 않는 원칙이 승인됐다. 이 원칙을 다시 결정 요청하지 않는다. 공급자 로그·백업의 실제 잔존과 삭제 절차·기간, 실제 메일 정리·회신 가능성은 별도 확인 대상이다. [기존 운영 절차](account-deletion-email-draft.ko.md).

## 남은 확인과 제출 순서

1. 실제 제공 24 및 신고에 포함할 다른 버전의 SDK·로그인·광고·결제·전송 설정 증거를 확보해 위 유형을 합친다. 새 소스·합성 AAB로 대체하지 않는다.
2. 유형별 필수/선택과 일시 처리를 확정한다. 특히 IAP 기본 수집·동의 거절의 효과, 광고 시작 수집, 이메일·국가 정보 실제 경로를 확인한다.
3. 실제 광고 수신자·계약과 서비스 제공자 예외, 전송 암호화 전체 범위, 아동 대상 지역별 SDK/동의 설정을 대조한다.
4. 담당자의 메일 수신·소유 확인·지연/처리 상태 회신 방식 및 공급자 잔존 데이터의 항목·목적·기간·요청 범위를 확인한다. 임의의 30일·영구 보관 숫자를 만들지 않는다.
5. 공개 정책 후보의 미확정란을 채우고 실제 제공 내용과 Console 입력을 함께 검토한 뒤 승인된 별도 실행으로 게시·제출한다.

## 이번 문서 검증 범위

두 초안만 변경한다. 소스 경로·상대 링크·기존 정책 URL과 승인 원칙·차이 검사를 수행하며 Unity·기기·구매·삭제·메일·Console 쓰기는 없다. 기존 LTS 114/114는 임시/fake Editor 시험이며 원복 독립 검토가 완료됐다는 별도 보고다. 운영 24·실기기 UI·공급자 전체 소거 결과로 사용하지 않는다. 새 APK/AAB와 해시는 없다.

근거 소스: [로그인](../../Assets/Scripts/Login/Login.cs), [서버 저장](../../Assets/Scripts/Managers/ServerManager.cs), [로컬 저장·삭제](../../Assets/Scripts/Managers/DataManager.cs), [IAP](../../Assets/Scripts/Managers/IAPManager.cs), [동의 기본값](../../Assets/Scripts/Managers/IapConsentDefaults.cs), [광고 gate](../../Assets/Scripts/Advertising/AdRequestPolicy.cs), [삭제 CloudScript](../../server/cloudscript/account-deletion.js), [패키지 선언](../../Packages/manifest.json).
