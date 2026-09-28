# 기존 개인정보처리방침 본문 보완 근거

2026-09-28, 사용자 승인 범위는 기존 정책 페이지·URL을 유지한 본문 보완과 AdMob 유럽·미국 메시지 초안 저장·미리보기다. 메시지 게시·운영 광고 활성화는 포함하지 않는다. 공개 방침은 [README 개인정보처리방침](../../README.md#개인정보처리방침)에 유지하고 내부 검증 근거는 이 문서에서 구분한다.

## 소스와 변경 범위

checkout `C:/Users/pc_17/.codex/worktrees/completion-state-refresh/Tamer`, branch `codex/privacy-policy-update`, 기반 main `f637f0b2a9dd5a1bbf5755175e31f70f048ec1ec`을 읽었다. clean·해당 Editor0 상태에서 문서만 수정했다. 정책 앞 README의 게임 소개·영상·이미지·스토어 URL·개발 내용, 개인정보처리방침 heading과 기존 지원 이메일을 보존한다. 앞부분의 과거 개발 기준 갱신은 이번 범위에 섞지 않는다.

| 확인한 구현 | 공개 방침에 반영한 내용과 제한 |
| --- | --- |
| `Login.cs`의 native GPGS 서버 인증코드, AndroidDeviceID/CustomID 로그인, 기기 OS·모델, 닉네임 쓰기 | 계정·기기 로그인·별칭을 구분한다. 합성 로그인 문자열을 실제 이용자 이메일, 캐릭터 Sex를 실제 성별로 설명하지 않는다. |
| `ServerManager.cs`의 PlayFab UserData 저장과 `DataManager` 진행·구매권한 | 계정·진행·No Ads가 기기 밖에 저장되는 경로를 설명한다. 현재 Private 쓰기가 과거 Public 데이터 전체 정리를 증명하지 않는다. |
| `IAPManager.cs`, `IapConsentDefaults.cs`, Unity IAP 5.4.3 | 구매·복원과 기존 구매 조회·SDK 이벤트를 구분한다. 선택 Ads/Analytics Denied 기본값을 진단·구매 기능을 포함한 모든 SDK 전송 중단으로 설명하지 않는다. |
| `LocalAgeChoice.cs`, `GoogleAdMobManager.cs`, `AgeTreatmentPolicy.cs`, `GoogleUmpConsentClient.cs` | 기기 내 연령 구간과 SDK의 연령 처리 신호·동의 상태를 구분한다. 준비 중인 새 기능을 제공 중인 앱 기능으로 단정하지 않는다. production/region gate는 false이며 성인 선택도 운영 허용이나 법적 동의 증거가 아니다. |
| `CloudScriptDeletionClient.cs`, `DeletionView.cs`, [공개 삭제 안내](../account-deletion.ko.md) | 접수와 전체 삭제 완료, 게임 계정과 타 서비스 계정·Google Play 구매 기록을 구분한다. 구매권한 보존을 이유로 삭제 계정 자료를 별도 보관한다고 표현하지 않는다. |

기존 [Data Safety 입력 초안](data-safety-draft.ko.md)의 기록상 실제 제공 버전 `24/1.0.3`과 승인되지 않은 `26/1.0.5`가 다르다. 새 소스의 managed 광고 차단을 제공 중인 전체 앱·native SDK 수집 중단으로 확대하지 않는다. 이 문서는 Console 답안 변경이나 모든 활성 트랙의 최종 합산 신고가 아니다.

## 공급자 공식 근거

- [PlayFab 플레이어 데이터](https://learn.microsoft.com/en-us/xbox/playfab/player-progression/player-data/): 게임 데이터 저장 범위. [PlayFab 삭제](https://learn.microsoft.com/en-us/xbox/playfab/data-analytics/privacy-compliance/gdpr-deleting-player-data): 계정·이벤트·개별 데이터 삭제가 다르며 접수 receipt는 완료 증거가 아니다.
- [Google Play Games](https://developer.android.com/games/pgs/data-collection): 인증 게임의 identity, 자동 진단·분석과 기능별 데이터를 구분한다. 미사용 친구·업적·Saved Games 기능을 임의 추가하지 않는다.
- [Google Mobile Ads Android](https://developers.google.com/admob/android/privacy/play-data-disclosure): IP, 식별자, 상호작용과 진단, 광고·분석·부정행위 방지 목적 및 TLS. 공식 표는 조회 시 최신25.5.0이고 후보25.4.0·과거 제공 SDK의 전체 실제 전송과 동일하다고 보장하지 않는다.
- [Unity IAP 5.4 이상](https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety), [동의 개요](https://docs.unity.com/en-us/iap/privacy-and-consent/overview): 구매·진단과 선택 목적을 구분한다. D2C 미사용 경로의 이메일·카드 전체 수집을 제품 사실로 추가하지 않는다.
- [Google UMP](https://developers.google.com/admob/unity/privacy): 동의 Update·필요한 form·Required privacy options와 실제 광고 요청을 구분한다. 메시지 초안 저장·미리보기는 게시 또는 실제 publisher 앱 실행 검증이 아니다.

AdMob 담당이 관측한 유럽 CMP 기본 미리보기의 파트너·정밀 위치·정당한 이익 문구는 이 게임이 GPS 권한을 요청하거나 모든 파트너에게 해당 정보를 실제 전송한다는 증거가 아니다. CMP 선택 문자열의 기기 저장·유효기간 안내도 게임 계정·진행 전체의 보관 기간으로 사용하지 않는다. 실제 적용 파트너·목적과 앱의 수집을 대조해야 한다.

기본 PlayFab HTTPS와 SDK의 공식 전송 암호화 설명은 최종 AAB의 모든 native endpoint·설정·인증서 동작 시험을 대신하지 않는다. 기기 내 JSON·설정 저장의 암호화와도 별개다. 공개 방침에 내부 빌드·검증 결과를 서비스 보장으로 제시하지 않는다.

## 남은 운영 확인

서버·처리자·백업·로그·메일 서비스의 실제 잔존 데이터/사유/기간, 삭제 요청의 실제 수신·소유 확인·처리 완료, 삭제 후 새 계정의 구매 복원 조건은 아직 확정되지 않았다. 기간이나 법적 근거·즉시 전체 삭제를 발명하지 않는다. 별도 보관용 게임 계정·진행 사본 없음 및 요청 이메일/확인 자료 처리 종료 후 삭제 원칙은 기존 승인된 [삭제·보관 방향](privacy-execution-readiness.ko.md)을 따른다.

최종 운영 확인은 총괄에 모아 전달한다. 이번 문서 보완만으로 법적 완결판·지역별 동의 게시·최종 Data Safety 제출·운영 삭제 검증 완료라고 주장하지 않는다. 원시 계정값·ID·이메일 요청 자료·설정·로그는 공개하지 않는다. 문서 관련 링크·보존 범위와 diff check만 확인하며 Unity·테스트·빌드·복원·운영 서비스 실행은 하지 않는다.
