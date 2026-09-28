# UMP 전용 샘플 APK 실기기 최소 검증

2026-09-28, 기존 `BuildUmpSample`로 APK를 한 번 빌드하고 Samsung SM-N986N
(Android 13 / API 33)에서 샘플 동의 화면을 검증했다. 런타임 코드는 변경하지 않았다.
**게시자의 실제 메시지 검증이나 운영 광고·지역 정책 승인 결과가 아니다.**

## 빌드와 소스 범위

- checkout: `C:\Users\pc_17\.codex\worktrees\e5e5\Tamer`
- 실제 빌드 소스: `31fb40bc56ff10a74694125bf53e9469b4fa2e3a`
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, GMA Unity `11.5.0`, Android GMA `25.4.0`, UMP `4.0.0`
- Android min SDK 24 / target SDK 36 / ARM64, IL2CPP development APK
- 패키지 `com.AeDeong.MonsterTamer.revival.ump`, versionCode 26 / versionName 1.0.5
- 공식 샘플 App ID `ca-app-pub-3940256099942544~3347511713`
- APK 서명 검증 성공, 서명 DN `C=US, O=Android, CN=Android Debug`
- APK: `Build/revival/Tamer-ads-ump-sample.apk`, 93,464,611 bytes
- APK SHA-256: `1526869eab89f554c52534298f0ed19de9bcb80b4c4072bb04b44884754683bc`
- 빌드 완료와 `AD_HARNESS_IDENTITY_RESTORED` 확인. 빌드 전 clean 상태에서 발생한
  Editor 생성 설정 변경을 복원하고 런타임·설정 파일 변경 없음 확인.
- 에셋 복원 검사: 4,561개 검증, 복사 0, 서비스 설정 제외. GUID/.meta 보존.

병렬 Gradle을 끄고 worker 수를 2로 제한했다. 임시 template에는 heap 1536M을 넣었지만
Unity가 실행 명령에서 `-Xmx4096m`으로 덮어썼다. 따라서 실제 빌드는 heap 4096M이다.
임시 template과 프로젝트 설정은 종료 후 복원했다. OS와 다른 앱 설정은 변경하지 않았다.

당시 최신 main `47c1a3e`와의 차이는 삭제 온라인 시험 앱·관련 문서/스크립트였다.
UMP 빌더·동의 클라이언트·gate·씬·패키지 lock은 동일했다. 공용 gameplay 빌더의
새 패키지 분기도 `.revival.ump`에는 해당하지 않는다. 이는 정적 비교이며
`47c1a3e`를 실제 빌드/실행한 결과로 표시하지 않는다.

## 실제 동작

| 사례 | 관찰 | 범위 |
| --- | --- | --- |
| 시작 | `ump_only_boot no_ad_manager_init sample_app_identity` | 자동 managed 광고 초기화 없음 |
| Unknown | `ump_blocked unknown_or_declined_age` | 해당 클릭 UMP Update 0 |
| Declined | 같은 차단 이벤트 | 해당 클릭 UMP Update 0 |
| 빈 해시 Under13 | `ump_blocked invalid_test_configuration` | UMP Update 0 |
| 해시 bootstrap Under13 | TFUA=true → Update 성공 → 필요한 폼 API 완료, 화면 없음 | 기기 등록 전 요청이라 강제 EEA 증거 제외 |
| 등록 기기 Adult / EEA | TFUA=false → Update 성공 → Publisher Test Ads 폼 표시 | 실제 SDK 샘플 폼 |
| 최초 거절 | `Do not consent` 선택 → 완료, `can_request=True`, `ads_disabled=true` | 광고 동의 여부와 CanRequestAds를 동일시하지 않음 |
| 개인정보 옵션 재진입 | 버튼으로 샘플 폼 재표시 후 `Consent` 선택 → `ump_privacy_finished can_request=True` | 거절에서 동의로 선택 변경과 콜백 완료 관찰 |
| 등록 기기 Under13 / EEA | 로컬 동의 reset → TFUA=true → Update 성공 → 필요한 폼 API 즉시 완료, 화면/개인정보 옵션 버튼 없음 | 제안된 TFUA 매핑의 최소 SDK 동작 |

네이티브 UMP Update 총 3회(bootstrap 1, 성인 1, 등록 미성년 1), 개인정보 옵션 재진입 1회다.
폼이 표시된 동안 gate가 완료되지 않고 실제 선택 후 콜백이 완료되었다.
테스트 기기 해시는 첫 SDK 로그의 추천 값으로 얻었으며 공개 기록에 포함하지 않는다.
Unity 입력창에서는 뒤로가기 대신 확인/Enter로 값을 확정해야 했다. 첫 빈 값 차단 이후
입력을 고쳐 진행했으며 같은 실패 테스트를 반복하지 않았다.

TFUA=true는 harness trace와 클라이언트의 `TagForUnderAgeOfConsent` 전달 코드로 확인했다.
서버 수신 payload를 별도 캡처한 증거는 없다. 화면이 없다는 사실만으로 지역별 법적
동의 연령 계약이나 Families 요건 충족을 판단하지 않는다.

## 격리와 증거 한계

managed 광고 초기화/load/show는 이 모드의 코드 경로에서 0이며 수집한 harness 광고
이벤트도 없었다. UMP 요청은 네트워크를 사용했다. Android provider를 포함한 모든
네이티브 SDK 자동 동작·트래픽이 0이라는 검증은 수행하지 않았다.
`ProductionAdsEnabled=false`, `RegionalConsentReviewed=false`를 유지한다.
기존 보상형 버프·HP 회복·No Ads 권한·구매·복원에는 변경이 없다.

이번에 신규 설치한 정확한 `.revival.ump` 패키지만 확인 후 제거했다. 다른 시험 앱,
삭제 저널, 운영 로그인·저장·구매·광고는 건드리지 않았다. 자체 batch Editor는 종료했고
기기/Editor 쓰기 슬롯을 반환했다. 기존 fake 테스트 59/59 결과는 [기존 구현 기록](ump-only-harness.ko.md)에
있으며 이번 문서 변경을 위해 재실행하지 않았다.

원본 로그·화면·테스트 해시는 checkout의 ignored `Logs/revival/ump-device-*`에 비공개로
보존한다. 공개 문서에는 원시 로그, 기기 식별자, 테스트 해시를 포함하지 않는다.
브라우저 정리 시 현재 탭 중 본인이 생성했다고 확인 가능한 완료 탭은 없었다.
기존 AdMob 탭과 소유 불명 탭은 보존했다. 다른 담당자의 로그인·약관 대기 탭도 닫지 않았다.

미검증: 게시자의 실제 메시지/파트너/정책 URL, RegulatedUSState/Other, 13–15/16–17,
프로세스 재시작 후 동의 상태, 네트워크 실패/timeout, 운영 광고·스토어·5초 닫기 정책,
최신 main 전체 실행. [요약 JSON](ump-only-device-validation.json)은 같은 관찰 범위만 기록한다.
