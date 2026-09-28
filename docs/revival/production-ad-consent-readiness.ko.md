# 운영 광고·동의 연결 준비 — 비활성 유지

2026-09-28, checkout `C:\Users\pc_17\.codex\worktrees\e5e5\Tamer`, 기반
`34f2aea5ec673168e0f17af17eb2f80a1feac9e2`에서 코드와 기존 AdMob 화면을 읽었다.
현재 기준은 Unity `6000.3.25f1` / CLI `1.0.0-beta.8` / min25·target36·ARM64다.
이 작업은 새 APK·운영 광고·Console 저장/게시를 실행하지 않았다.

## 실제 연결 공백과 변경

기존 `ProductionAdsEnabled=false`는 선언만 존재했고 실제 요청 조건에 사용되지 않았다.
로드 경로도 공식 샘플 단위만 선택했다. 따라서 두 검토 bool을 true로 바꾸는 것만으로
운영 광고가 연결되지 않으며, 샘플 광고를 운영 복구 완료로 판단할 수도 없다.

이 변경은 운영 환경 조건에서 `ProductionAdsEnabled`를 실제 참조한다. Android 일반
release만 후보이고 Editor·development·batch·명시적 테스트·격리 harness는 운영 환경에서
제외한다. 비공개 출시 빌드에서 설정할 `_productionRewardedAdUnit`은 빈값이 기본이며
tracked 씬/프리팹에는 운영 값을 쓰지 않았다. 테스트 환경은 이 필드를 무시하고 공식
샘플 단위만 사용한다. 운영 선택은 빈값·형식 오류·Google 샘플 게시자·iOS를 거절하며
두 환경이 동시에 허용되거나 모두 차단되면 단위를 반환하지 않는다. 구문 검사 자체는
실제 앱 소유권·광고 형식·서버 활성 상태를 확인하는 검사가 아니다.

UMP 시작 조건은 저장 연령·지역 검토·허용 환경을 따른다. 보상형 fullscreen 형식과
유효 광고 단위 조건은 광고 요청에만 적용하여, 동의 갱신을 광고 형식이나 설정 누락에
묶지 않는다. 이미 Required로 판정된 개인정보 옵션은 미성년 형식 차단/운영 단위 빈값과
무관하게 접근할 수 있고, 옵션 완료 후 자동 로드는 하지 않는다. Unknown·Declined,
미검토 지역과 일반 release 비활성은 계속 요청 전에 차단한다.

`ProductionAdsEnabled=false`, `RegionalConsentReviewed=false`, 현재 연령별 fullscreen
제한을 모두 유지한다. 아동/청소년 보호 설정은 기존 Child/Teen/G 및 별도 UMP TFUA를
그대로 사용한다. 기존 보상 완료→10분 버프·HP 회복, No Ads 권한·판매·복원 코드는
변경하지 않았다. 5초 닫기 추가 조사와 다른 보상/수익 모델 제안은 하지 않는다.

## 게시자 메시지와 정책 URL

AdMob 앱 설정과 tracked Android App ID의 일치를 확인했다. 운영 식별자·계정값·설정
수치는 공개 기록에 중복하지 않는다. 유럽/미국 메시지 탭에는 생성 안내가 표시됐으며
앱별 게시 메시지 근거를 확보하지 못했다. 계정의 대체/자동 메시지 동작도 따로 대조했으나
현재 앱의 실제 표시·문구·파트너·정책 URL 검증을 대신하지 않는다.

[Google 대체 메시지 안내](https://support.google.com/admob/answer/17198583)는 계정 설정에 따라
TC 문자열 없는 적격 광고 요청에 대체 폼을 시도할 수 있고, 새 앱 자동 게시도 설명한다.
따라서 생성 안내만으로 모든 운영 환경에서 폼이 없다고 단정하지 않는다.
[공식 UMP 연결](https://developers.google.com/admob/unity/privacy)은 프로젝트 App ID의 메시지,
실행별 Update, 필요한 폼 및 Required 옵션 진입점을 요구한다. 샘플 App ID의
[기존 성공 기록](ump-only-device-validation.ko.md)은 게시자의 실제 메시지 성공이 아니다.

앱의 독립 정책 버튼은 기존
`https://github.com/ChoiDaeYoung-94/Tamer#개인정보처리방침`을 연다.
공개 페이지 접근과 기존 정책 제목을 확인했지만, AdMob 메시지 안의 URL 설정은
확인하지 못했다. [출시 정책 초안](privacy-policy-release-draft.ko.md)의 실제 처리자·목적·보관
설명 확정과 게시가 남아 있다. 기존 URL을 보존하는 것과 새 정책 검토 완료는 구분한다.

## 승인 후 적용할 구체 묶음

| 순서 | 준비된 적용안 | 완료 근거 |
| --- | --- | --- |
| 배포·연령 계약 | Play 실제 배포 국가와 기존 구간의 Child/Teen/TFUA 조합 대조 | 국가별 범위·검토 근거를 소스 검토 계약에 기록 |
| 유럽 메시지 | 기존 앱만 연결, EEA·영국·스위스 범위, 최초 화면 거절 ON 및 동의/옵션 유지 | 실제 앱·언어·정책 URL·파트너·선택 화면을 검토 후 게시 승인 |
| 언어/정책 | 실제 지원 언어에 맞춘 기본/추가 언어와 기존 정책 URL | 공개 정책과 선택한 모든 언어 미리보기 대조 |
| 미국 메시지 | 미국 배포가 있으면 해당 지원 주의 판매/공유 거부 메시지와 UMP/GPP 경로 | 미국 주 폼·거부·개인정보 옵션 실제 결과; 전 지역 RDP를 임의 추가하지 않음 |
| 계정 공통 설정 | 파트너·적법한 이익·대체 메시지 관계 대조 | 다른 앱에도 미치는 변경 영향과 실제 표시를 승인 묶음에 포함 |
| 비공개 출시 설정 | 검토된 기존 운영 rewarded 단위 주입, 실제 manifest App ID·게시자/단위 대조 | 공개 코드/로그에 값 없이 연결 일치 검증, 설정 없으면 차단 |
| 동의만 기기 검증 | 슬롯 배정 후 최신 LTS에서 실제 게시자 UMP를 광고 초기화/load/show 없이 검증 | Update·거절·재진입·선택 변경·다음 실행·미성년/지역 결과 |
| 운영 활성화 | 위 계약과 출시 검토 후 별도 reviewed 변경 | 샘플 define/ID 없는 최종 APK/AAB; 현재 PR에서는 활성화하지 않음 |

메시지 생성 절차와 거절/지역/언어/정책 URL 항목은
[공식 유럽 메시지 작성 안내](https://support.google.com/admob/answer/10113207)를 따른다.
[미국 주 SDK 안내](https://developers.google.com/admob/unity/privacy/us-states)는 UMP가 GPP 신호를
쓸 수 있음을 설명한다. Child/Teen 보호와 별도 UMP TFUA는
[targeting](https://developers.google.com/admob/unity/targeting),
[GDPR TFUA 안내](https://developers.google.com/admob/unity/privacy/gdpr)로 대조했다.
TFUA 폼 생략은 동의 획득이 아니며, CanRequestAds는 개인화 동의와 동의어가 아니다.

운영 단위의 비공개 빌드 주입 절차, 실제 게시자 UMP 기기 구성 및 모든 Console 게시값은
아직 적용하지 않았다. 위 표는 승인할 구체 변경안이며 이미 적용됐다는 목록이 아니다.

## 최소 검증 상태

- 실제 `AdRequestPolicy.cs`와 기존 NUnit 정책 fixture를 .NET 9.0.6에 직접 링크하고
  reflection runner로 **27/27 통과**했다. 새 운영 단위 사례는 합성 식별자만 사용했다.
  runner는 로컬 ignored 경로이며 Unity runner 실행으로 표시하지 않는다.
- 첫 runner 구성에서 상대 경로가 한 단계 잘못되어 소스/참조를 못 찾았다(테스트 미실행).
  경로를 고친 다음 실행에서 위 fixture가 통과했으며 추가 재실행은 하지 않았다.
- 신규 검사: 모든 환경 조합에서 production false 차단, 운영 단위 누락/형식/샘플 거절,
  테스트에서 운영 값 무시, 운영 선택에서 샘플 대체 없음, 모호한 환경과 iOS 차단.
- Required 옵션과 늦은 보상 무효화 manager 테스트를 네 유효 연령으로 확장했다.
  Unity 컴파일/manager·consent fixture는 SDK 슬롯 이후 검증할 준비 상태다.
- 원시 Console 관찰은 `Logs/revival/production-consent-audit-private.ko.md`,
  .NET runner는 `Logs/revival/ad-production-policy-check/`에 비공개 보존한다.
- 새 Chrome 탭 0. 기존 AdMob 탭은 원래 광고 단위 목록으로 복귀해 보존했다.

미검증: 이 변경의 Unity/IL2CPP 실행, 실제 게시자 메시지·지역 법적 연령 계약,
운영 설정 주입/요청·동의 후 재시작, 네이티브 전체 트래픽, 최종 출시 AAB/스토어.
새 APK SHA-256은 해당 없음이다. 이후 검증은 실제 source head와 슬롯 결과를 별도 기록한다.
