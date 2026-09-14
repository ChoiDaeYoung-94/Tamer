# 테스트 타이틀 전용 진행도 검증 경로

2026-09-14, 기준 main `188e98d` (#159). 구현 checkout은 `C:/Users/pc_17/.codex/worktrees/7299/Tamer`다.

## 구현과 경계

기존 IAPStore 앱은 실제 테스트 PlayFab 로그인·영수증·인벤토리를 사용하지만 게임의 `Managers.ServerM`은 메모리 대역이다. 이 변경은 해당 대역과 `DataManager` 저장을 교체하지 않는다. 별도 `RevivalProgressProbe`가 인증된 기존 세션을 받아 테스트 타이틀 `12B656`의 `RevivalProgressProbeV1` 키만 읽고 쓴다. 값은 합성 진행 단계 `v1:1` 또는 `v1:2`이며 게임 보상·재화가 아니다.

운영 코드와 공유하는 부분은 `ServerManager`의 직렬 큐·제한 재시도·시간 제한·계정 경계·Dispose 처리와 Private 쓰기 요청 생성기다. 다른 부분은 instance PlayFab 인증, 단일 키 allowlist, 게임 저장에 적용하지 않는 응답 처리다. **실제 게임 DataManager의 전체 진행도 저장·복원 검증 완료가 아니다.**

- 허용 타이틀과 앱 패키지가 정확히 일치해야 연결된다. 전역 PlayFab 설정·인증을 변경하지 않는다.
- 현재 세션 객체와 DataManager 소유자가 유지되는 동안만 요청한다. 세션 교체·소유자 교체·종료 후 응답은 적용하지 않는다.
- 첫 읽기 성공 후에만 합성 값을 쓸 수 있다. 잘못된 전용 키 값은 덮어쓰지 않는다. 동시에 두 쓰기를 시작하지 않는다.
- 실제 SDK 요청은 전용 키 조회와 단일 키 Private patch뿐이다. 삭제 키·전체 데이터 교체·인벤토리 변경은 없다. `GooglePlay`, `__TamerAccountOwner`, 일반 진행도 키를 쓰지 않는다.
- 계정 ID·세션 티켓·설치 identity는 패널이나 로그에 표시하지 않는다. 파일과 구매 권한은 그대로 둔다.

## 수동 실측 순서와 아직 하지 않은 작업

**기기 교체에 따른 보류:** 아래 순서는 기존 개인폰 IAP 앱의 별도 재연결 승인 이후에만 검토한다. 회사 테스트폰의 일반 검증 승인은 받았지만 이 IAP 세션 기반 경로는 적용하지 않는다. 회사폰에서 결제·구매 복원·구매 계정 추가/전환·라이선스 테스터 등록을 하지 않는다. 이전 기기의 serial·CustomID·Play 계정을 재사용하거나 동일 계정으로 추정하지 않는다. 회사폰 합성 저장 시험은 구매 기능 없는 별도 격리 구성의 검토가 필요하다. 이번 변경에서는 어떤 폰도 조회·조작하지 않았다.

통합 담당자가 기존 Store 서명과 설치를 유지하는 Play 테스트 업데이트 경로를 조율한 뒤 수행한다. 코드 준비만으로 현재 폰에 기능이 추가되지는 않는다. debug APK 덮어쓰기, 삭제/재설치, 앱 데이터 초기화, 설치 identity 교체는 사용하지 않는다. 새 설치 ID 연결이 필요하면 사용자가 승인된 수동 경로에서 처리하며 자동 전달 거절을 우회하지 않는다.

1. 현재 테스트 앱에서 기존 연결 계정으로 로그인한다. 이 기능은 로그인·계정 생성을 자동 실행하지 않는다.
2. `Reconnect probe / read cloud test progress`로 읽는다. 없는 키는 없는 상태로 표시하며 자동 생성하지 않는다.
3. `Save synthetic test step 1 / read back` 후 조회된 단계 1을 확인한다. 완료 전 단계 2 쓰기는 비활성이다.
4. 단계 2를 쓰고 재조회한다. probe를 재연결해 같은 값을 읽는다.
5. 앱을 정상 종료·재실행하고 기존 계정 로그인 후 다시 읽어 단계 2를 확인한다. 이 시점이 프로세스 간 실제 서버 복원 근거다.
6. 기존 No Ads와 게임 저장이 유지되는지 별도 집계로 확인한다. 다른 계정 준비가 없으면 계정 전환의 서버 실측은 미검증으로 남긴다. 새 계정을 임의 생성하지 않는다.

현재 테스트는 합성 delegate 및 실제 SDK의 전송 직전 가로채기다. 서버 응답도 대역이므로 실제 PlayFab 지속 저장의 증거가 아니다. 실제 네트워크·기기 조작·Store 업데이트·계정 전환 실측은 이번 PR에서 수행하지 않았다. 결과와 버전은 별도 검증 JSON에 기록한다.

[PlayFab GetUserData](https://learn.microsoft.com/en-us/rest/api/playfab/client/player-data-management/get-user-data?view=playfab-rest)와 [UpdateUserData](https://learn.microsoft.com/en-us/rest/api/playfab/client/player-data-management/update-user-data?view=playfab-rest)를 2026-09-14 확인했다. 키 제한 읽기와 지정 데이터 patch를 사용하며 권한은 명시적으로 Private다.
