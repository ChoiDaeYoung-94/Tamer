# 개인폰의 전용 progress Cloud 검증

2026-09-14 개인폰 SM-S938N(Android 16, 현재 사용자 0, 페이지 크기 4096)에서
별도 progress 앱의 실제 PlayFab 저장·읽기 및 재시작 후 재인증 복원을 확인했다.
사용자가 새 전용 계정을 생성한 뒤 진행했으며 기존 IAP 계정과 개인 앱은 조작하지 않았다.

## 대상과 근거

- 작업 checkout: `C:/Users/pc_17/.codex/worktrees/7299/Tamer`
- 문서 기준 main: `355640104b5c0fd0f1b1aacbdc8e8972f789a3b6`
- 실행 APK 소스: `e712627e47163760c22a1d32251d455c4e914344`
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android min SDK 24 / target SDK 36, debug ARM64
- 패키지: `com.AeDeong.MonsterTamer.revival.progress`
- APK SHA-256: `8a09b37cd5cedba53a3b42eda54d1e02d37531b65dda0844f6831bd2abe91ebd`
- 빌드·기준 Editor 400/400 결과: [기존 검증 기록](progress-only-validation.json)
- 단계별 화면의 해시·촬영 시각: [실기기 검증 기록](progress-cloud-device-validation.json)

## 실행 결과

1. 새 전용 계정의 생성 및 CustomID 연결 상태를 PlayFab 개요에서 확인했다.
2. 개인폰에 해당 패키지가 없음을 확인한 뒤 기존 검증 APK를 신규 설치했다.
3. 승인된 기존 전용 자격 증명으로 앱에서 로그인했다. 새 계정 생성이나 자격 증명 변경은 하지 않았다.
4. 초기 화면 `TEST PLAYFAB — dedicated key only` / `No dedicated test progress saved`를 확인했다.
5. 합성 step 1 저장 후 읽기, step 2 저장 후 읽기가 각각 완료되었다.
6. `Reconnect / read current mode` 실행 후에도 step 2를 읽었다.
7. 해당 앱만 force-stop하고 PID가 없음을 확인했다. 재실행 후 `No progress session`과 빈 입력 칸을 확인했다.
8. 같은 전용 계정으로 재인증한 뒤 추가 저장 없이 `Test cloud progress read: step 2`를 확인했다.
9. PlayFab 플레이어 데이터 화면에서 단일 키 `RevivalProgressProbeV1`, 값 `v1:2`, 권한 `비공개`를 대조했다.

마지막에는 해당 앱만 종료했다. 설치된 앱과 서버 합성 값은 보존했고 폰·Chrome 슬롯을 반환했다.
추가 사용자 입력 없이 이 범위의 개인폰 검증을 마쳤다.
화면, 프로세스 확인 기록, 계정 식별자 및 인증용 수동 파일은 비공개 로컬에 보존한다.
공개 기록에는 인증 값·계정 ID·기기 일련번호·원시 로그·화면을 포함하지 않는다.

## 검증 범위

이 결과는 별도 `RevivalProgressProbe`의 단일 합성 키에 대한 실제 Cloud 왕복 및 재인증 복원 근거다.
현재 APK는 PR #166 이전 소스이므로 PR #166의 `DataManager` pending/revision 저널 Android 검증이 아니다.
전체 게임 진행도, No Ads·구매 복원, 운영 계정, 오프라인 변경, 쓰기 도중 강제 종료,
16 KB 페이지 환경은 검증하지 않았다. 이번 작업에서 코드 변경·APK 재빌드·Editor 테스트 재실행은 하지 않았다.
