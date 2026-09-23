# 계정 삭제 시 로컬 구매 권한 보관 사본 정리

삭제 접수가 명시적으로 확인된 뒤에는 해당 계정으로 소유가 확인된 현재 진행 파일과 `PlayerDataBackups` 생성 파일을 정리한다. `GooglePlay` 값을 `.deletion-entitlement-*` 파일로 새로 복사하지 않는다. 과거 버전이 만든 사본 중 현재 저장 파일 이름에 붙은 32자리 소문자 GUID 형식, `__TamerAccountOwner`·`GooglePlay` 두 필드, 삭제 대상 계정 소유가 모두 일치하는 것만 정리한다. 다른 계정·소유 불명·형식 불량·임의 이름·reparse 파일은 보존한다. 읽기/삭제 I/O 실패는 조용히 성공 처리하지 않고 기존 삭제 정리 실패 경로로 전달한다. 접수 불명확 상태의 journal은 재전송 방지를 위해 유지한다.

Google Play의 비소모성 구매 소유권이나 스토어 구매 기록을 취소하는 변경은 아니다. 기존 앱에는 `FetchPurchases`와 `RestoreTransactions`를 통한 구매 재조회 경로가 있다. 다만 삭제 후 새 게임 계정에서 No Ads가 실제로 복원되는지, 운영 영수증 검증 경로가 이를 허용하는지는 아직 확인하지 못했다. 별도 로컬 보관 사본을 없앤 것이 구매 복원을 보증하지 않는다. PlayFab title 삭제 API 범위 밖의 PlayStream 이벤트·publisher/master 연결 정보와 접근 불가 기기 데이터도 이 로컬 변경의 정리 대상이 아니다.

검증 소스는 `C:/Users/pc_17/.codex/worktrees/deletion-no-archive/Tamer`의 `codex/deletion-no-archive`, 기준 `origin/main` `74e4c61b07978742d8cebd38a5e8680ae15487e1`에 이 변경의 코드·테스트를 더한 상태다. Unity `6000.0.81f1`, CLI `1.0.0-beta.8`. 권한 있는 원본에서 비공개 에셋 verified 362/copied 4201, 설정·자격 증명 복사 제외. 관련 EditMode 13/13 통과, 실패·스킵 0. 결과 XML은 비공개 `.revival-local/deletion-no-archive/editmode.xml`, SHA-256 `9344b131985de3cac79d3fed051082da0862330a5bb17d40ec46b6caebad59bd`. Editor의 일시적 ProjectSettings 변경은 테스트 후 복원했고 작업 소스 외 차이는 없다. APK/AAB를 만들지 않아 APK SHA-256은 해당 없음. 운영 계정 로그인·삭제 요청·스토어 복원·실기기 검증은 수행하지 않았다.
