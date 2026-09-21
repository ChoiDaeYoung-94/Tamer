# 기존 광고 검증 화면의 UMP 전용 모드

`RevivalAdHarnessBuild.BuildUmpSample`은 기존 `RevivalAdHarness.unity`를 사용해
`Build/revival/Tamer-ads-ump-sample.apk`를 만드는 개발용 빌드 진입점이다.
패키지는 `com.AeDeong.MonsterTamer.revival.ump`, App ID는 Google 공식 샘플 값이며
debug signing과 기존 빌더의 설정 복원 절차를 사용한다. 새 씬이나 운영 App ID 경로를 추가하지 않았다.

빌드별 `TAMER_UMP_ONLY_HARNESS` 정의가 시작 단계에서 광고 관리자 `Init`을 건너뛴다.
화면에는 광고 로드·표시·보상·예약 작업 버튼이 없으며 `Show`와 예약 작업 실행도 차단된다.
일반 빌드의 지역 검토 gate, 연령 저장값, No Ads 권한에는 영향을 주지 않는다.

## 수동 검증 흐름

이 변경에서는 APK 빌드·설치·네트워크 요청을 실행하지 않았다. 실제 기기 검증은 별도로 진행한다.

1. 검증용 연령 사례와 `EEA` / `RegulatedUSState` / `Other`를 선택한다.
2. 로컬 UMP 테스트 기기 해시 32자리 hex를 입력한다. 값은 화면에서 가려지고 저장·로그하지 않는다.
3. 필요하면 테스트 동의 상태를 로컬 초기화한다. 연령 미선택·응답 거부 상태에서는 초기화도 비활성이다.
4. 명시적 UMP 요청 버튼으로 `Update`와 필요한 폼 표시를 실행한다.
5. SDK가 개인정보 옵션을 요구하면 같은 화면에서 다시 연다.

연령 미선택·응답 거부 또는 잘못된 해시는 `Update` 전에 차단한다. 연령은 저장된 실제 사용자
선택과 별개인 합성 사례이며, TFUA 값은 기존 제안 매핑을 재사용한다. 지역별 연령 계약 승인으로
해석하지 않는다. 콜백은 Unity Update 큐에서 처리하고 네트워크 Update만 30초 만료를 적용한다.
열린 폼에는 시간 제한을 걸지 않는다. 사례 변경·화면 파괴 시 이전 gate를 폐기한다.

**샘플 App ID 결과는 이 게시자의 실제 메시지·파트너·정책 URL 검증 증거가 아니다.**
현재 콘솔 메시지 생성·저장·게시, 운영 App ID 사용 경로는 이 구현에 포함하지 않는다.

## 검증 기록

- checkout: `C:\Users\pc_17\.codex\worktrees\e5e5\Tamer`
- 검증 소스: `cb3973c22d6ae50e252b8fa23acd47900b561b50`
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, GMA Unity `11.5.0`, Android GMA `25.4.0`, UMP `4.0.0`
- Android 설정: min SDK 24 / target SDK 36 / ARM64. 새 APK SHA-256: 해당 없음(빌드 미실행).
- 2026-09-21 06:50:15 UTC, `RevivalAdManagerTests;RevivalAdConsentTests`: **59/59 통과**, 실패·건너뜀 0.
- 새 테스트 9개: 명시한 지역·해시 설정, 일반 클라이언트 debug 설정 없음, 잘못된 지역·해시 거절.
- 기존 fake 동의 처리와 관리자 gate 회귀 검증을 포함한다. 실제 SDK 네트워크 요청은 없다.
- 비공개 원본 XML: `Logs/revival/ump-only-final.xml`
- XML SHA-256: `cbd5f08fedc23d93ff09821ab0b4375505b8ff82f059fd8dd7da33b3f2f228cf`
- 앞선 `0d77098` 실행도 59/59 통과했다. 모드 분기 경고와 미선택 초기화 버튼을 정리한 뒤 위 소스를 재검증했다.
- 자신의 Editor 종료·프로젝트 설정 복원을 확인했다.

미검증: UMP 전용 APK 빌드/IL2CPP, 실제 기기 화면과 동의 재진입·재시작,
샘플/게시자 App ID 서버 응답, 실제 동의 메시지 문구·파트너 목록, 운영 정책·지역별 연령 계약.
