# 개인정보처리방침·Data safety 기술 대조 (#105)

확인일: 2026-09-11. 추적: [#105](https://github.com/ChoiDaeYoung-94/Tamer/issues/105).
관련 변경: [광고 PR #104](https://github.com/ChoiDaeYoung-94/Tamer/pull/104),
[계정·저장 PR #100](https://github.com/ChoiDaeYoung-94/Tamer/pull/100),
[SDK #97](https://github.com/ChoiDaeYoung-94/Tamer/issues/97).

기존 Console의 **수집·공유 없음** 선언은 로그인 식별자와 계정에 연결한 서버 저장 경로에
다시 대조해야 한다. 이 문서는 공개 가능한 소스 근거와 신고 검토 항목을 정리한 기술 감사이며,
개정 개인정보처리방침이나 제출 가능한 확정 답안이 아니다. Console 선언, 저장소 구현,
SDK 제공자의 일반 설명, 실제 배포 앱의 동작을 구분한다.

## 조사 기준과 제한

| 근거 | 확인한 범위 |
| --- | --- |
| 감사 기준 | main `4e8c039`의 Login, DataManager, ServerManager, IAPManager, IapConsentDefaults, PlayFabDeviceUtil 및 패키지 선언을 재확인 |
| 광고·Console 기록 | [Families 정책 조사](families-ads-policy.ko.md), [광고 검증 기록](families-ads-validation.ko.md). Console 값은 담당자의 읽기 전용 확인 기록을 재사용. 2단계 광고 통신 관찰은 별도 후속 증거 |
| 통합 소스·산출물 | [통합 검증 기록](integration-validation.json)의 `f5b7e404d7bd2c797aed2c9a3c17416a2ef50ccf`, merged main `7cba8dbbcfb733b4e7b8fc8eb818cb330003504a`의 Assets/Packages/ProjectSettings/tools 일치 기록 |
| SDK 선언 버전 | Unity 6000.0.81f1, IAP 5.4.3, Services Core 1.18.0, GPGS Unity 2.2.1, GMA Unity 11.5.0, Android 표준 GMA 25.4.0, UMP 4.0.0 |

통합 담당의 격리 debug APK SHA-256은
`0d51f218491d90aa7e963b0e42b3ebf5606fc8e0d91f4b710d246fb8223eeb27`이다.
해당 기록에는 GPGS Android 22.0.0, PlayFab 2.242.260805, Billing 9.0.0,
EditMode 248/248와 Python 31/31 통과도 연결되어 있다. 이는 제공 중인 운영 APK나
실제 로그인·구매·SDK 통신을 검증한 결과가 아니다. 이 감사 작업 자체에서는 Editor 실행,
APK 빌드, 실제 계정·구매·광고 테스트, 네트워크 캡처를 수행하지 않았다.

## Console에서 확인된 선언과 비교

| 항목 | 기존 Console 기록 | 대조 결과·남은 확인 |
| --- | --- | --- |
| 위반 연결 번들 | `26 (1.0.5)`, 프로덕션 표시, target SDK 36, 첫 게시일 2026-08-13 | 검토·거절 통지에 연결된 번들이다. 변경사항 미게시 및 이전 버전 제공 안내가 있으므로 26을 현재 서비스 버전으로 단정하지 않는다. 실제 제공 트랙·버전은 미확인 |
| 타깃 연령 | 9~12세, 13~15세, 16~17세 | 아동 포함 선언을 확인했다. 실제 사용자 연령 분류, 지역별 동의 흐름과 같은 사실은 별도 확인 |
| 광고 | 광고 포함 | 새 광고 코드의 release API 호출 차단은 확인했다. 기존 서비스 버전의 광고·native SDK 시작 동작까지 차단되었다는 뜻은 아니다 |
| Data safety | 수집·공유 없음, 데이터가 암호화되지 않음, Families 준수 약속 | 각각 선언 값이다. 아래 데이터 경로, 처리 계약, 전체 전송 구간 및 Families 위반 상태에 재대조 필요 |
| 개인정보처리방침 URL | [저장소 루트](https://github.com/ChoiDaeYoung-94/Tamer) | README의 기존 정책은 식별·사용·기기 정보 수집 가능성과 제3자 제공을 설명한다. Console의 수집·공유 없음과 별도 정합성 검토 필요 |
| 콘텐츠 선언 목록의 최종 수정 | 2024-09-30 | 현재 코드·SDK 변경을 반영한 신고라는 근거가 되지 않는다 |

거절 연결 번들의 동작과 다음 제출 후보의 동작을 따로 기록해야 한다.
새 소스의 보호 조치를 과거 번들에 소급하지 않는다.
현재 세션에서는 기존 AdMob 관리 화면에 접근하지 못했으므로, 실제 mediation/custom event,
광고 공급자·소재, 앱·광고 단위 설정과 Privacy & messaging 구성도 미확인이다.

## 데이터 경로와 신고 검토 항목

아래 분류와 목적은 **신고 검토 후보**다. 호출 조건과 수신자를 최종 앱에서 확인한 뒤
필수·선택 여부, 수집·공유 예외 및 실제 목적을 확정한다.
[Play Data safety 정의](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en)는
SDK를 통한 기기 밖 전송도 포함하며, 별칭·사용자 ID·구매 내역·게임 활동·기기 ID를 구별한다.

| 소스·실행 조건 | 코드에서 확인한 데이터와 용도 | 신고 후보와 제한 |
| --- | --- | --- |
| `Login.LoginOnAndroidAsync` → GPGS 인증 | `Social.localUser.id`를 읽고 선택된 로그인 식별자를 PlayerPrefs에 캐시 | 사용자 ID, 계정 관리·앱 기능. 앱이 친구·업적·GPGS Saved Games를 실제 사용한다고 추정하지 않음 |
| `Login.LoginOrRegisterWithEmailAsync` | GPGS 식별자로 만든 합성 이메일 형태의 계정 키를 PlayFab 로그인·조건부 등록에 사용 | 사용자 ID 우선 검토. 합성 계정 키는 이용자의 실제 이메일을 읽었다는 증거가 아님. 자격 증명 설계의 보안 검토는 별도이며 값은 공개하지 않음 |
| `Login.LoginWithDeviceAsync` | 단말 식별자와 OS·모델을 쓰는 Android 로그인 또는 생성한 custom ID 로그인 | 기기·기타 ID 및 사용자 ID, 계정 관리·앱 기능. 각 로그인 방식이 실제 사용되는 조건을 확인 |
| `ResolveProfileAsync`, `UpdateDisplayNameAsync` | 이용자 입력 닉네임을 PlayFab DisplayName과 UserData `NickName`에 저장 | 이름/별칭, 계정 관리·앱 기능. 닉네임 단계가 필수인지 실제 로그인 UX와 대조 |
| `DataManager` → `ServerManager.WriteToPlayFab` | Gold, Power, AttackSpeed, MoveSpeed, Tutorial, AllyMonsters, 캐릭터 선택, No Ads 권한 등의 변경 키를 계정에 연결해 저장·조회 | 게임 활동·진행도, 구매 내역/권한, 앱 기능 후보. 서버에 지속 저장하는 경로를 일시 처리로 간주하지 않음 |
| `CanvasSelectCharacter`의 `Sex` | 선택한 캐릭터의 구분값 저장 | 이용자의 실제 성별을 수집한다는 근거로 사용하지 않음 |
| `BuffingMan`·`GoogleAdMobManager.ResetAdMob` → DataManager | `GoogleAdMob` 키에 버프 보상 시각 또는 초기화 문자열을 저장하고 cloud patch에 포함 | 게임 활동, 앱 기능 후보. SDK 자체 광고 추적 이벤트와 구별하며 No Ads 혜택 경로도 대조 |
| `IAPManager`의 상점 초기화·제품/구매 조회·구매·복원 | 플랫폼 상점 연결, 기존 주문·구매 권한 조회, No Ads 지급과 로컬 저장, 변경 키의 cloud 저장 예약 | 구매 내역, 앱 기능·권한 복원. 구매가 선택이라는 사실과 기존 구매 자동 조회의 조건을 구별. 앱이 카드정보를 직접 읽는다는 근거는 없음 |
| `GoogleAdMobManager`의 명시적 테스트 요청 | 아동 지향/미성년 동의/G 등급 설정 후 샘플 광고 API 호출. 일반 release의 managed 초기화·로드·표시는 차단, No Ads는 광고 없이 혜택 제공 | 관리 코드의 요청 차단만 확인. native provider 초기화, SDK 자동 통신·수집 전체의 부재를 보장하지 않음 |
| UMP 패키지 | 의존성은 있으나 조사한 앱 스크립트에서 ConsentInformation/ConsentForm/privacy options 호출은 찾지 못함 | 패키지 설치만으로 동의 획득 또는 실제 수집을 확정하지 않음. 재활성화 시 실제 연령·지역과 AdMob 메시지 설정을 확인 |

## SDK의 일반 설명과 실제 앱의 구분

- **GPGS:** [공식 disclosure](https://developer.android.com/games/pgs/data-collection)는 SDK의 자동 분석·진단
  수집과 사용 기능에 따라 달라지는 게임 데이터를 구분한다. 소스에서 로그인 ID를 사용하는 사실은 확인했지만,
  문서의 모든 선택 기능을 이 앱의 수집 항목으로 그대로 옮기지 않는다.
- **GMA:** [Android 표준 25.4.0 disclosure](https://developers.google.com/admob/android/privacy/play-data-disclosure)는
  기본 동작의 IP 주소, 상호작용, 진단, 기기·계정 식별자와 광고·분석·부정행위 방지 목적을 설명한다.
  이 목록은 SDK 제공자의 설명이다. 최종 manifest, 실제 시작 순서, 요청 차단 및 설정에 따른
  이 앱의 동작은 측정하지 않았다. 광고 API를 부르지 않는다는 이유로 자동 수집도 없다고 결론내리지 않는다.
  2단계 광고 담당의 25.4.0 ads-api AAR manifest/javap 검사 보고에서는
  `MobileAdsInitProvider.onCreate()`가 false를 반환하고, `attachInfo`의 `zzev` 위임은
  메타데이터·App ID 검사 경로이며 직접 initialize/load/network 호출은 보이지 않았다.
  따라서 provider 존재만으로 자동 전송이 발생했다고 단정하지도 않는다.
  다른 초기화 경로와 실제 통신은 별도이며 상세 산출물 근거는 광고 담당의 후속 기록에 연결한다.
- **IAP:** [5.4+ 개인정보 안내](https://docs.unity.com/en-us/iap/privacy-and-consent/overview)와
  [Data safety 안내](https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety)는
  D2C 기능도 포함하는 범위를 다룬다. 현재 소스는 플랫폼 상점 흐름이며 앱 스크립트의 D2C·Unity Authentication
  로그인 호출은 찾지 못했다. 문서의 모든 이메일·위치·ID 항목이 이 플랫폼 흐름에서 수집된다고 단정하지 않는다.
  반대로 미사용 선택 기능이 있다는 이유로 IAP의 모든 통신이 없다고 단정하지 않는다.
- **IAP 동의 기본값:** `IapConsentDefaults`는 BeforeSceneLoad와 스토어 생성 전 미지정 Ads/Analytics 목적을
  Denied로 설정하고 기존 지정 상태는 보존한다. IAP용 UGS 자동·직접 초기화도 제거했다.
  이는 모든 필수 구매 처리·진단·구매 이벤트 전송을 차단했다는 증거가 아니다.
  SDK 담당의 5.4.3 PackageCache(`com.unity.purchasing@c771cfd27c96`)를 읽기 전용으로 대조했다.
  `Runtime/Stores/Data/Insights/PurchaseEventEmitter.cs`는 구매 시작/성공/실패/지급 이벤트를 만들고,
  Unity 6000.0의 `ForwardGateway`는 Unity Insights HTTPS ingest로 protobuf POST한다.
  envelope에는 동의 상태·세션/설치 식별자·앱/엔진 버전·기기·상품/거래 필드가 있으며,
  Google 구매 payload 경로에는 OriginalJson/Signature도 있다. 필드별 실제 값 유무는 별도다.
  `PlayerData.cs`에서 일부 광고 식별자는 Ads Granted 및 설정 조건으로 제한하지만,
  이 emitter의 전송 함수 자체는 Denied에 따른 전체 차단을 하지 않는다.
  이는 소스 경로 확인이며 실제 구매 전송을 관찰한 결과가 아니다.
  [IAP 이행 기록](iap-v5.ko.md)과 함께 구매 내역·앱 활동·기기/기타 ID·진단 및 처리 목적을 대조한다.
- **PlayFab:** SDK의 `PlayFabDeviceUtil`은 로그인 응답의 `GatherDeviceInfo`/`GatherFocusInfo`와
  로컬 비활성화 설정을 함께 사용한다. 조사한 로컬 설정은 두 비활성화 값이 0이지만 서버 응답은 미확인이다.
  따라서 추가 장치 정보·화면시간 전송 여부는 확정하지 않는다. 로그인 요청에 들어가는 OS·모델과는 별도 경로다.

## UserData 접근 범위: 향후 Private 쓰기

데이터 변경 `0003c1d`의 `WriteToPlayFab`은 요청에 `UserDataPermission.Private`를 지정한다.
조회는 인증된 본인의 계정과 요청별 인증 컨텍스트를 확인하며, 조사한 앱 코드에서는
다른 플레이어 UserData 조회 의존성을 발견하지 못했다.

[PlayFab UpdateUserData 계약](https://learn.microsoft.com/en-us/rest/api/playfab/client/player-data-management/update-user-data?view=playfab-rest)에
따르면 접근 권한은 해당 요청으로 쓰는 키에 적용되고, 다른 플레이어 조회에는 Public 키만 반환된다.
따라서 **향후 쓰기를 Private로 바꾸어도 요청에 포함되지 않은 기존 Public 키는 그대로 남는다.**
기존 키의 일괄 마이그레이션을 수행한 것은 아니며, 구버전 클라이언트가 다시 Public으로 쓰는 상황도 남아 있다.
운영 키 목록·공개 의도, 서버측 사용처, 구버전 대응은 여전히 운영 확인이 필요하다.
[Private 이행 준비와 오프라인 도구](userdata-private-migration.ko.md)는 익명 집계,
사전 변경 감지, 사후 값·권한 보존 비교를 제공한다. 운영 쓰기 기능은 없으며
DataVersion 비교를 동시 쓰기 잠금/CAS로 오해하지 않도록 한계를 명시한다.
이 문서 작성 중 운영 데이터 조회·수정·삭제는 수행하지 않았다.

## 공유·전송 암호화·삭제·보관

| 항목 | 확인된 기술 근거 | 확정하지 않은 부분 |
| --- | --- | --- |
| 수집과 공유 | PlayFab에 계정 키·닉네임·진행도·권한을 보내는 소스 경로가 있음 | 서비스 제공자의 개발자 대행 처리는 공유 예외가 될 수 있으나 수집 자체가 없어지는 것은 아님. 계약과 실제 처리 목적 확인 없이 공유 없음 확정 불가 |
| 전송 암호화 | PlayFab SDK 기본 URL 구성은 HTTPS. GPGS는 HTTPS, GMA는 TLS, IAP disclosure는 SDK 데이터 전송 암호화를 설명 | 모든 endpoint·native SDK·최종 manifest·실제 트래픽 미검증. 앱 전체의 모든 수집 데이터가 암호화된다는 선언은 아직 확정 불가 |
| 로컬 저장 | 계정 귀속을 포함한 `PlayerData.json`, PlayerPrefs 및 복구 백업을 사용 | 로컬 파일 형식·보호와 전송 중 암호화는 별도 질문. 백업까지 포함한 접근·삭제 정책 확인 필요 |
| 계정 삭제 | `ServerManager.DeleteData`는 지정 UserData 키 제거 경로 | 앱의 계정 삭제 UI나 관리자 삭제 연동은 확인하지 못함. 특정 키 제거를 전체 계정 삭제로 신고하지 않음 |
| 보관 | 로컬 복구 백업을 보존하며 자동 정리는 하지 않는 정책 | PlayFab 서버·로그·백업 및 SDK별 실제 보관 대상/기간은 미확인. 공급자 문서의 일반 기간을 앱 전체 기간으로 복사하지 않음 |

공유 예외와 모든 사용자 데이터의 전송 암호화 판단 기준은
[Play Data safety 안내](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en)를 따른다.
SDK별 전송 보안 설명은 위 GPGS·GMA·IAP 공식 링크에 있으며, 서로 다른 SDK의 설명을 합쳐
최종 앱의 검증 결과로 간주하지 않는다.

[PlayFab 삭제 안내](https://learn.microsoft.com/en-us/xbox/playfab/data-analytics/privacy-compliance/gdpr-deleting-player-data)는
Master Player 계정 전체 삭제, 이벤트 데이터 삭제, 사용자 정의 데이터 삭제를 구분한다.
삭제 접수, 본인 확인, 관련 데이터 범위와 완료 확인이 필요하며,
GPGS 프로필 삭제가 PlayFab 계정·로컬 백업까지 삭제한다는 근거는 없다.
관련 저장소를 빠짐없이 찾는 범위는 [PlayFab 삭제·내보내기 안내](https://learn.microsoft.com/en-us/xbox/playfab/data-analytics/privacy-compliance/playfab-gdpr-deleting-and-exporting-player-data)와 대조한다.

[구체적 Data Safety 수정 초안](data-safety-draft.ko.md)에 유형별 답안 후보,
앱·웹 삭제 접수 경로의 누락과 구현 계약, 서버 영수증 검증 부재를 정리했다.
No Ads 지급 후 confirm은 영수증 진위 검증과 다르며, Private UserData도
클라이언트가 쓰는 권한 문자열을 서버 검증된 구매 증거로 바꾸지 않는다.

## #105에서 확정할 신고·정책 항목

| 항목 | 현재 판단 | 완료에 필요한 근거 |
| --- | --- | --- |
| 데이터 수집 여부 | 로그인·cloud save 경로를 근거로 수집 있음 방향의 재작성 검토가 필요 | 현재 제공 번들과 다음 제출 후보 각각의 활성 경로, 데이터 유형·수신자 확인 |
| 데이터 유형·목적 | 사용자 ID, 기기 ID, 닉네임, 진행도·구매 권한을 우선 대조 | SDK 자동 진단·위치·구매 이벤트는 최종 실행 조건과 실제 목적 확인 |
| 필수·선택 | 미확정 | 로그인·닉네임·상점 자동 조회와 기능 미사용 시의 동작 확인 |
| 공유·처리자 | 미확정 | PlayFab·Google·Unity·광고 공급자별 계약과 실제 처리 목적 확인 |
| 모든 전송 암호화 | 미확정 | 최종 산출물과 모든 활성 전송 경로의 보안 검증 |
| 삭제·보관 | 미확정 | 접수 창구, 본인 확인, 처리 범위·기간·담당자, 서버·SDK·로컬 백업 및 구매 증빙 보관 기준 확정 |
| 아동·동의 | 대상 연령에 아동 포함, 실제 처리 흐름 미검증 | 연령 미확인 사용자 처리, 지역별 동의, 메시지·공급자 설정과 Families 기기 검증 |
| 개인정보 본문 | 기존 README 정책 보존 상태 | 실제 운영 주체·연락 창구, 데이터/목적/수신자, 필수·선택 기능, 보관·삭제, 아동 처리, 전송 보안, 시행일 확정 후 개정 |

기존 정책 URL에서 개정 본문을 쉽게 찾을 수 있는지와 안정적인 별도 URL 사용 여부도 확인한다.
연락 창구·기간·수집 항목에 미확정 값을 채우거나 구현되지 않은 삭제 기능을 약속하지 않는다.
이 기술 대조를 완료한 상태와 Console 변경·제출, 스토어 승인, 실제 운영 데이터 이행 완료는 각각 별도로 기록한다.

공개 증거에는 소스 커밋·파일·API·검증 요약만 남긴다. 계정 식별자, 이메일 주소, Console 개인 URL,
원시 화면·응답·로그, 서비스 설정값과 사용자 데이터는 포함하지 않는다.
