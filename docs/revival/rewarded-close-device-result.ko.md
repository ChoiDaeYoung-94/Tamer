# 공식 보상형 샘플 실기기 닫기 측정 (2026-09-23)

이 기록은 별도 패키지의 Google 공식 샘플 광고 **한 회**에 한정된다. 운영 광고,
실제 이용자의 연령·지역, 두 게임 배치의 정책 준수를 증명하지 않는다. 사용자 승인을
받은 공용 테스트폰에서만 실행했으며 운영 계정·저장·구매·No Ads에는 접근하지 않았다.

## 환경과 증거

- 구현 checkout: `C:/Users/pc_17/.codex/worktrees/rewarded-close-review/Tamer`,
  측정 당시 HEAD `fca56fa6b92bb4b1f08e976aa9714e7c96aba19e`.
- Unity `6000.0.81f1`, Unity CLI `1.0.0-beta.8`, Android SDK build-tools `36.0.0`.
  삼성 SM-N986N, Android 13 공용 테스트폰. 실제 전화번호·기기 일련번호·UMP
  테스트 기기 해시는 기록하지 않는다.
- 설치 APK: `Build/revival/Tamer-ads-sample.apk`, 132,829,986바이트,
  SHA-256 `e46ec083667060e831a429e284c16acc2557924b86612f78e9543da87a05977c`.
  격리 패키지 `com.AeDeong.MonsterTamer.revival.ads`, 개발 빌드, debug 서명,
  공식 샘플 AdMob App ID와 보상형 단위, ARM64, min SDK 24/target SDK 36.
- 로컬 비공개 화면 녹화는 77.39초다. 영상과 해시, 스크린샷·원시 로그·실제
  UMP 테스트 기기 해시는 무시 대상 로컬 폴더에만 보관하고 저장소나 공개 PR에는
  올리지 않는다.

## 한 회의 관찰

격리 앱을 새로 설치한 뒤 합성 `Under13`/`EEA`와 이 공용 테스트폰의 UMP 테스트
기기 해시를 설정했다. 테스트 기기 해시가 31자리만 입력된 첫 시도는 SDK 호출 전에
차단됐다. 32자리로 바로잡은 뒤 첫 `Load sample`은 UMP Update/폼 요청과
`consent_allowed`까지 갔으나 Mobile Ads 초기화·광고 로드로 이어지지 않았다.
두 번째 `Load sample`에서 초기화와 공식 샘플 로드가 성공했다. 원인은
`LoadRewardedAd()`가 `Init()`을 호출하면서 먼저 `BeginConsent(false)`로
초기화를 점유하고, 이어지는 `BeginConsent(true)`가 거부되는 흐름이다.

이후 한 번만 `Show sample`을 눌렀다. 녹화 상대 시각 약 13.5초에 첫 네이티브
광고 픽셀(테스트 광고 표시), 약 21.5초에 실제 X가 처음 보였다. 프레임 판독
오차를 감안해도 간격은 약 8초이며, 첫 픽셀 뒤 5초인 약 18.5초에는 X가 없다.
따라서 **이 공식 샘플의 이 조건 한 회는 5초 닫기 조건을 충족하지 못했다.**
X를 한 번 눌러 광고가 닫히고 격리 앱으로 돌아왔다. 추가 확인 화면은 없었다.

SDK 콜백의 상대 경과는 Show→보상 약 7.85초, Show→닫힘 약 51.35초였다.
보상 1회, 완료 1회였고 Show 시 BGM `False`, 닫힌 뒤
BGM `True`로 복구됐다. opened 콜백은 첫 픽셀/X의 증거로 쓰지 않았다.

## 판정 범위와 후속 확인

이 결과는 샘플 요청·실제 X 터치·앱 콜백을 연결해 확인한다. 공식 샘플도 이 조건에서
5초 닫기를 보장하지 않았으므로 현재 제품 gate를 열거나 운영 광고 복구·스토어
준수 완료로 표시할 수 없다. 운영 소재·두 게임 배치·미보상 조기 닫기·16KB 기기·
UMP 실제 폼은 미검증이다. 기본 네이티브 LOAD/ZIP 정적 검사는 통과했으나
별도 GNU_RELRO 끝 정렬 검사는 실패한 상태다.

첫 `Load sample` 연결 결함의 코드 수정은 이 측정 뒤 진행했다. `Init()`의
씬 구독은 유지하되 `LoadRewardedAd()`에서 `Init()`의 UMP-only 요청을 거치지 않고
씬 구독 후 `BeginConsent(true)`로 진입한다. 수정 checkout의 광고 관리자
`RevivalAdManagerTests` EditMode 51/51 통과(실패·건너뜀 0, 결과 XML SHA-256
`3e77b29f4a6e131a920c97a9fd6ff06517e2600ab66960a9e6230f6e74a1712f`).
Unity `6000.0.81f1`/CLI `1.0.0-beta.8`로 격리 샘플 APK를 다시 빌드했고,
93,465,176바이트, SHA-256
`533b1f1ed7f7ffb3eeddc9ddb9a1edec08cade46381711fd8ccbdab2de240282`였다.
검증 스크립트가 격리 패키지·debug 서명·공식 샘플 App ID·ARM64·SDK 24/36을
확인했다. 새 APK를 같은 공용 테스트폰의 **격리 패키지에만** 재설치하고 그
패키지의 앱 데이터를 삭제해 이전 UMP 상태를 지운 뒤 실행했다. 합성
`Under13`/`EEA`와 같은 테스트 기기 해시를 입력했다. 입력 확정 전 한 번의
`Load`는 설정 단계에서 차단되어 SDK 호출이 없었다. 입력을 확정한 다음
**첫 유효한 `Load` 한 번**에서 UMP Update/허용 → Mobile Ads 초기화/콜백 →
샘플 `load_call`/`load_callback_ok`가 이어졌다. 광고 Show·X 계측은 반복하지
않았다. 따라서 코드 수정이 첫 유효 요청의 UMP-only 정지를 해결한 사실만
재검증했다. 운영 광고와 5초 닫기 판정은 여전히 미검증이다.

정책 기준은 [Google Play Families 정책](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en)의
아동 또는 연령 미상 이용자에게 표시되는 게임 방해 보상형·동의형 광고 5초 닫기
요구사항이다.

## 공급자·정책 지원 문의 초안 — 발송하지 않음

아래 문안은 후속 검토를 위한 초안이며 외부로 보내지 않았다. 운영 광고를 열거나
보상 구조를 바꾸기 전에 담당자가 문의 대상과 제품 설정을 검토해야 한다.

> We are reviewing rewarded ads in an Android game that may be used by children
> or users of unknown age. Google Play Families policy requires interruptive
> rewarded/opt-in ads in that context to be dismissible after five seconds.
> In one isolated development build using Google's official rewarded test unit,
> a real close X first appeared about eight seconds after the first native ad
> pixel on our shared test device. This is one sample observation, not a claim
> about production inventory.
>
> Which supported ad format, creative restrictions, inventory filters, and
> account or ad-unit settings can guarantee that a visible close control is
> available within five seconds for every eligible impression? Does that
> guarantee still hold with mediation or high-engagement rewarded settings?
> How should we configure UMP consent, child/under-age request treatment, and
> an existing No Ads entitlement alongside that approach? If no such
> guarantee is available, please state that explicitly and identify the
> supported compliant alternative for this audience.
