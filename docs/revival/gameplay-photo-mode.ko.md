# 격리 게임 화면 촬영 모드 검증

2026-09-14, 구현 checkout `C:\Users\pc_17\.codex\worktrees\e5e5\Tamer`, 빌드 소스 `78b33c5f405724346326470851fb7a69ed523c38`.

## 빌드와 격리

`tools/revival/Run-GameplayHarness.ps1 -Variant photo`는 원래 Login/Main/Game/NextScene을 사용하는 별도 촬영 APK를 생성한다. `TAMER_GAMEPLAY_PHOTO`는 해당 빌드에만 추가한다. 기본 `development` 출력과 검증은 유지한다.

- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android build-tools `36.0.0`, min/target SDK `24/36`, ARM64.
- 패키지 `com.AeDeong.MonsterTamer.revival.gameplay`, 버전 `1.0.5` / `26`, Android debug 서명, `debuggable=false`.
- APK 80,761,976 bytes, SHA-256 `6a21be0373be50c4d428776040a588d259716ddb39baf45481e627acb1925485`.
- 병합 manifest의 INTERNET, ACCESS_NETWORK_STATE, BILLING, AD_ID 및 MobileAdsInitProvider 부재 검증 통과.
- 합성 계정·메모리 서버·별도 앱 저장소, 로그인/광고 차단과 IAP unavailable 가드를 유지한다. 실제 실행에서 `ISOLATION_OK`, `iapBlocked=1`, 자동 왕복 `ROUNDTRIP_OK errors=0`을 확인했다.
- 회사폰 SM-N986N / Android 13에서 대상 패키지 미설치를 새로 확인하고 신규 설치했다. 기존 앱·데이터·계정은 보존했다. 결제, 복원, 외부 계정 로그인은 실행하지 않았다.

## 촬영 조작과 결과

진단 화면의 Hide diagnostics 버튼은 OnGUI와 활성 진단 콘솔 Canvas의 렌더링만 숨긴다. 일반 HUD와 게임 로직, 로그 콜백은 유지한다. 화면 왼쪽 위 가로 8%·세로 5% 영역을 각 1초 이내 간격으로 세 번 탭하면 이전 표시 상태를 복원한다.

기기에서 숨김 및 세 번 탭 복귀를 두 차례 확인했다. 숨김 중 원래 Capture 버튼으로 동료 수 0→1, Home 확인창으로 마을 복귀, 게임 상점과 조이스틱 입력이 동작했다. 숨김 중에도 게임 관찰 로그가 계속 기록되며 해당 앱 로그에서 Unity Error/Exception/Assert를 발견하지 않았다.

`RuntimeInitialize.Awake`는 `DEVELOPMENT_BUILD`에서만 콘솔을 활성화한다. 따라서 이 nondevelopment 촬영에서는 콘솔이 처음부터 비활성 상태였으며, 활성 콘솔의 숨김 후 재표시는 실기기 미검증이다. OnGUI 복귀와 로그 지속 확인을 콘솔 재표시 검증으로 대체하지 않는다.

다음 1080×2316 PNG는 기기 스크린샷 원본이며 생성·보정·잘라내기를 하지 않았다. 진단 패널, 개발 워터마크, 시스템 바가 없고 일반 게임 HUD의 합성 닉네임은 유지한다. 원본은 비공개 로컬 증거 폴더에 보존하며 README용 선택본은 통합 담당자가 반영한다.

| 원본 | 내용 및 조건 | SHA-256 |
|---|---|---|
| village.png | 동료가 없는 마을 | `5420dba3cb855bbcb268268e3c1e812456a70e927b8b8f7d3850bbc1cd986e74` |
| combat.png | 전투/포획 직전. 기존 격리 보조의 Bat 생성·플레이어 무적·포획 roll 고정 사용 | `be99a88b8a28ba9ca00bb8339ade6b7115de8e916ef4a7de085cc6f3e332ac66` |
| ally-village.png | 합성 골드 100G로 원래 게임 상점에서 고용한 Bat의 마을 추종 | `eca08a14b7562842913800fadad3f73415b84f86098f2eb8099a206612c3d9c0` |

전투에서 포획한 첫 Bat는 후속 전투에서 사망했다. 마을 이미지의 Bat는 상점 고용으로 별도로 확보했으며 합성 골드는 1046→946, 동료 수는 0→1이었다. 이 촬영 자료는 일반 포획 확률이나 전투 밸런스 검증 결과가 아니다. 마을 포털 옆 검은 사각 등 원래 렌더링도 수정하지 않았다.

## 검증 범위

- 복원 에셋 해시 4,561개 확인, 복사/덮어쓰기 없음.
- Python APK variant 회귀 2개 통과: development/photo 뒤바뀜 거부 및 금지 권한/provider 거부.
- 촬영 APK IL2CPP 빌드와 APK 검증 통과.
- `Run-Baseline.ps1 -TestsOnly`: Editor Revival 테스트 395개 통과, 실패/건너뜀 0개.
- 운영 로그인·저장·광고·구매, 16KB 기기, strict RELRO, 스토어 배포는 검증하지 않았다. #92 기준 개발 APK 검증이나 #91 광고 정책/스토어 검증을 대체하지 않는다.
- 촬영 종료 후 대상 앱만 force-stop 했으며 설치와 데이터를 보존했다.
