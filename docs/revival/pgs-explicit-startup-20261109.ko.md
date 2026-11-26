# PGS 자동 시작 provider 제거

managed PlayGamesPlatform.Activate 지연만으로 앱 시작의 native PGS 초기화를 막았다고 표현할 수 없었다. 보존 서명 후보32의 실제3DEX와 PGS22 AAR에서 provider 및 lifecycle 초기화 경로를 확인했다. main AndroidManifest에 PlayGamesInitProvider의 tools:node=remove1개만 추가해 자동 provider를 제거한다. 기존 SUPPRESS_GAME_PROFILE_CREATION=true와 Unity activity/metadata·GUID/meta는 보존한다.

현재 Unity 플러그인의 AndroidClient 생성자는 이미 PlayGamesSdk.initialize(Activity)를 호출한다. 기존 Google 경로의 Activate가 이 클라이언트를 생성하므로 새 JNI나 중복 초기화를 추가하지 않는다. PlayFab/Google 계정 ID·연결·로그인 선택/복원·저장·No Ads·SDK 버전과 min25/target36/Unity6000.3.25f1을 변경하지 않는다. Google 선택과 광고의 성인 확인은 별개다.

## 실제 병합 대조

AGP9.0.0/manifest-merger32.0.0/Gradle9.3.1과 고정 JDK, 실제 PGS22.0.0 AAR 및 현재 Unity 플러그인 manifest로 원본/정확한 제거/잘못된 이름3개를 대조했다. 최종 tools 지시 정리 후 provider 수는1/0/1이었고, 병합 결과의 min25/target36·기존 APP_ID 값·프로필 생성 억제 설정은 보존됐다. 앱 빌드/설치·PGS runtime 초기화·실제 로그인/네트워크는0회다.

원시 library 표본의 옛 target 호환 처리를 포함한 결과이며 전체 Gradle 모듈·의존성을 거친 최종 앱의 권한 대조로 쓰지 않는다. 다음 서명 앱에서 실제 manifest/provider/권한을 다시 검증해야 한다.

첫 도구 준비는 main uses-sdk에 버전을 넣어 AGP9가 거부했고, 진단 실행에서 원인을 확인했다. 빌드 설정과 같은 API override로25/36을 전달한 후에는 중간 출력의 tools 제거 marker를 provider로 세어 실패했다. 최종 tools 정리 단계까지 요청한 별도 네 번째 실행에서3대조를 통과했다. 앞선3실패 기록과각 정상 Job 종료/원본 복구는 보존하며 원래 실행을 성공으로 바꾸지 않는다.

## 남은 확인

실제 Google 재인증·기존 계정 복원과 미성년/미상/거부/성인의 SDK·식별자·네트워크, 심사 계정 No Ads 여부 및 최신 서명 Play 제공물 검증은 미완료다. provider 제거만으로 PGS의 신고 데이터 유형이나 모든 SDK 수집을 없앴다고 계산하지 않는다. 최종 정책 신고·심사 전송·프로덕션은 기존 별도 검토 절차를 유지한다.

공식 근거:
- [PlayGamesSdk 자동 및 명시 초기화](https://developers.google.com/android/reference/com/google/android/gms/games/PlayGamesSdk)
- [PGS 데이터 처리 안내](https://developer.android.com/games/pgs/data-collection) — 최신 SDK 안내와 실제22 코드/패키지 대조를 구분
- [Families 데이터 지침](https://support.google.com/googleplay/android-developer/answer/11043825?hl=en)
