# 원래 게임 씬의 오프라인 격리 검증

## 구현과 운영 경계

`TAMER_GAMEPLAY_HARNESS`는 `RevivalGameplayBuild.BuildAndroid`의 추가 빌드 심볼로만 사용한다.
일반 플레이어에는 하네스 타입을 넣지 않으며 전역 심볼 설정도 거부한다. 원래 Login/Main/Game/NextScene을
수정하거나 복제하지 않고 빌드한다. 첫 씬의 Managers/Player 생성과 기존 씬 전환 코드를 실제 실행한다.

- Login의 Awake/Start를 빌드 조건으로 차단해 GPGS 활성화 및 로그인 작업을 시작하지 않는다.
- IAP 초기화는 Unavailable로 끝내고 광고 요청 정책은 항상 false로 둔다. ShopMan의 기존 IAP 초기화 호출도 차단한다.
- Managers는 기존 ServerManager의 6개 delegate 생성자에 메모리 전송을 주입한다. 합성 계정 외 요청은 거부한다.
- DataManager는 별도 앱의 `RevivalGameplay/<새 GUID>/PlayerData.json`에만 저장한다. 기존 PlayerData 경로를 읽지 않는다.
- PlayerPrefs는 별도 Android 패키지의 저장소를 사용한다. 하네스는 Editor Play에서 실행하지 않으며 실제 계정·No Ads를 가져오지 않는다.
- 최종 manifest에서 INTERNET, ACCESS_NETWORK_STATE, BILLING, AD_ID와 MobileAdsInitProvider가 없어야 verifier가 통과한다.
  SDK 요청 코드의 차단과 Android 네트워크 권한 제거를 각각 확인한다.

빌드 wrapper는 Editor 시작 전 설정을 비공개로 보관하고 Editor 종료 후 원본 바이트를 복원한다.
원래 앱 ID, 서명 설정, Android/AdMob manifest, 서비스 설정을 운영에 적용하지 않는다.

## 실행 결과

실기기 APK는 `f2ab1d9`의 소스다. UMP PR128을 합친 뒤의 통합 Editor 검증은 `e66acbcc745d8489339e6661393a1722d9aad1b8`이다.
후자를 새 APK 실행으로 표기하지 않는다. 정확한 SHA와 환경은 [검증 JSON](gameplay-harness-validation.json)을 따른다.

- 최초 Editor 320/320: 기존 317개와 신규 격리 3개. 저장 경로 분리, 메모리 응답 복사·다른 계정 거부, manifest 제거 지시를 검증했다.
- UMP 통합 후 Editor **335/335**, 실패/skip 0. Unity 6000.0.81f1, 에셋 4561개 검증, 종료 후 자체 Editor 0과 설정 복원 확인.
- APK **104,528,624 bytes**, 별도 `com.AeDeong.MonsterTamer.revival.gameplay`, debug 서명,
  ARM64, min24/target36, 1.0.5/code26. 최종 APK의 네트워크·결제·광고 ID 권한과 광고 자동 초기화 제공자 부재 검증 통과.
- Android 13/API33, ARM64, PAGE_SIZE=4096 실기기에서 최초 격리 데이터 준비와 로그인 차단을 확인했다.
- 원래 Main → Game → Main을 **2회** 실행했다. 매니저/플레이어 owner 유지, Game의 생성기 존재,
  실제 몬스터·미니맵 표시, 로비 복귀와 HP 100을 확인했다. 최종 errors=0, 메모리 read=6, write=0이었다.
- Home 후 기존 task를 전면으로 복귀시켰으며 같은 왕복 횟수와 오류 0, 로비 화면을 확인했다.
- 테스트 앱 제거 Success와 pm path 빈값을 확인했다. 지정한 원격 영상/스크린샷도 제거했다.
  원시 로그·영상·스크린샷은 비공개 로컬에만 남긴다.

## 파일별 판단과 한계

RevivalGameplayIsolation은 합성 서버와 고유 저장 경로, RevivalGameplayHarness는 원래 씬 왕복 관찰,
RevivalGameplayBuild는 별도 APK 설정과 manifest 제거, RevivalGameplayIsolationTests는 격리 경계 회귀를 담당한다.
서비스 담당이 빌드 전 소스를 독립 검토했고 확정 P1/P2는 없었다. 최종 APK 검사는 실제 산출물에서 수행했다.

실제 로컬 파일 생성과 씬 왕복은 검증했지만 이번 기기 실행에서 메모리 서버 write는 0이다.
진행도 변경·장비 구매·포획·사망 후 복원 또는 클라우드 쓰기 왕복을 수행했다고 주장하지 않는다.
원래 서버의 지연/실패, 운영 로그인, 재설치 계정 복원, 영수증 진위/구매 복원, 운영 광고·정책,
16KB 환경과 출시 AAB는 별도 작업이다. 단일 남성 캐릭터·현재 기기 화면과 관찰한 전투 구간만 확인했다.
테스트 앱은 검증 뒤 제거했으므로 재실행하려면 검증된 APK를 다시 설치해야 한다.
