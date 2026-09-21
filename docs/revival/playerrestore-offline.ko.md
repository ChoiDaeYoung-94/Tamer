# 여성·저장 동료 원본 씬 검증 준비

2026-09-21, 기존 gameplay 빌더·하네스에 `TAMER_PLAYER_RESTORE` 모드를 추가했다. 별도 하네스나 씬을 만들지 않고 원본 Login/Main/Game/NextScene을 사용한다. 2026-09-21 실제 기기 최초 설치 검증도 통과했다. 아래 기기 결과와 증거 manifest를 참조한다.

## 격리 경계

- 별도 패키지 `com.AeDeong.MonsterTamer.revival.playerrestore`. 기존 gameplay/cloud/운영 앱을 업데이트하거나 삭제하지 않는다.
- manifest에서 INTERNET/ACCESS_NETWORK_STATE/BILLING/AD_ID 및 광고 초기화 provider 제거. 기존 gameplay의 로그인·구매·광고 차단과 메모리 transport를 재사용한다.
- allowBackup=false, fullBackupContent=false. 빌드 후처리에서 cloud-backup/device-transfer 양쪽의 9개 저장 도메인 전체를 제외하는 dataExtractionRules를 생성한다.
- Boot와 메모리 서버 생성 전에 정확한 패키지·비Editor 조건을 확인한다. 세 legacy PlayerPrefs 키의 HasKey가 참이면 값을 읽거나 삭제하지 않고 중단한다. 기존 일반 하네스의 패키지 허용 범위는 확대하지 않는다.
- 저장 파일은 전용 앱 persistentDataPath 아래 기존 GUID 경로를 사용한다. seed는 기존 2단계 fixture의 Woman/GameplayFixtureB/Gold235/Power12/AttackSpeed0.7/MoveSpeed3.4/Bat,Magma이며 광고·구매 권한은 없다.
- Main 준비 후 원본 Player/UI/동료 상태를 검사하고 자동 전투 왕복을 생략한다. 캡처 보조는 기존 gameplay 패키지에만 허용되므로 이 모드에서는 사용할 수 없다.

## 빌드와 최소 검증

- checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`, APK 소스 `169ab82f20cb238730b74b3beede1436bd57d7f1`
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android debug ARM64 / min SDK24 / target SDK36
- `RevivalGameplayIsolationTests` 12/12 통과(신규 격리7개 포함), Python APK 검사기 3/3 통과
- `Build/revival/Tamer-playerrestore.apk`, 105468683 bytes
- APK SHA-256 `609343129cd3609a6ea9e33cb8317d1fd2070289b4158b2f59cb36a1dc47a8ba`
- 최종 APK의 패키지·debug 서명·권한 제거·백업 차단·복원 제외 규칙 검사 통과. 최초 APK 검사는 aapt2의 false 출력 형식 미지원으로 1회 실패했으며 검사기를 수정한 2차 검사에서 통과했다. 3차 재시도 없음
- LOAD/ZIP 통과. 별도 RELRO 끝 정렬 5개 실패이며 16 KB 런타임 성공으로 취급하지 않는다
- 본인 Editor 종료/PID 없음 확인, build 설정 및 incidental 렌더러 설정 복원. 에셋 GUID 변경 없음

비공개 근거는 `Logs/revival/`에 보존한다.

| 파일 | SHA-256 |
| --- | --- |
| playerrestore-tests.json | e01f04109609c1808a1c9a884f49e61ed9759e0552d3746275924709eb23f998 |
| playerrestore-build.provenance.json | 4c22fa81119cf1709d2665e6124345e6cd5441aa200daccfbbdc6fb46c3375d8 |
| playerrestore-apk-verification.json | d80aa1ef0e779bd07d62661e340030743dddb49c5ae9c41cd181dfbbfcdede4b |
| playerrestore-native.json | f75179d7f7fc080399f5a3582eeeece0413f195c18e5996b09ac1c9238aefaf5 |

## 실제 기기 검증 결과

2026-09-21, SM-N986N / Android 13 / user 0에서 위 APK의 해시와 정확한 패키지 부재를 확인한 뒤 최초 설치했다. 실행 1회, 실패 0회, 재시도 0회로 `PLAYER_RESTORE_PASS`를 확인했다. 검증 checkout 커밋은 `da6c76b8155ca2a78a3a39e8d242a4fd8ff81381`이며 APK는 재빌드하지 않았다.

- 원본 Main에서 여성 Player prefab, 8키 로컬 값, 실제 Power 12 / AttackSpeed 0.7 / MoveSpeed 3.4 / Gold 235, 빈 장비·수집 목록 검사를 통과했다.
- Bat/Magma 두 동료의 역할·부모·활성 상태와 NavMesh 연결 2/2를 확인했다. 화면에서는 Player와 Bat가 겹쳐 있으므로 개체 판정은 런타임 검사와 함께 해석한다.
- 실제 HUD의 GameplayFixtureB, Gold - 235, 동료 2 / 10, HP 100 / 100을 화면에서 확인했다. overlay는 Main / reads=2 / writes=0 / errors=0이며 로그인·광고 차단과 메모리 서버 격리 마커도 확인했다.
- 전용 앱의 PlayerData.json을 비공개 증거로 보존했다. 8개 fixture 값이 일치하고 pending journal은 비어 있다. 여기서 writes=0은 하네스의 서버 쓰기 계수이며 로컬 파일 쓰기가 없다는 뜻은 아니다.
- 해당 앱만 정상 명령으로 force-stop하고 PID가 없음을 확인했다. 설치와 앱 데이터를 유지했으며 다른 앱 데이터나 계정은 변경하지 않았다. 폰 사용 슬롯을 Integration에 반환했다.

비공개 원시 로그·스크린샷·저장 파일·종료 기록은 `Logs/revival/playerrestore-device/`에 유지한다. 파일별 SHA-256과 환경·결과는 [증거 manifest](playerrestore-device-validation.json)에 기록했다. 공개 문서에 기기 일련번호나 계정 인증값은 포함하지 않는다.

이번 결과는 오프라인 fixture를 원본 Main에 적용한 첫 실행 검사다. 이 APK의 프로세스 재시작 복원·실제 Cloud·전투·legacy PlayerPrefs 이관은 검증하지 않았다. Tutorial=done은 원본 TODO 분기를 건너뛰는 값으로, 튜토리얼 기능 완료를 의미하지 않는다. 운영 서비스·구매·광고·스토어·16 KB 런타임 또는 #91/#92 전체 완료를 주장하지 않는다. 빌드 단계의 RELRO 끝 정렬 5개 실패는 그대로 남아 있다.
