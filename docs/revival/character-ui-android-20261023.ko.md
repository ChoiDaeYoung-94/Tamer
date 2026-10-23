# 원래 캐릭터 선택·저장·Main·재시작 — 2026-10-23

빈 합성 프로필에서 원래 `SetCharacter` 씬의 방향 버튼으로 여성 캐릭터를 선택하고 Play를 눌렀습니다. 원래 `CanvasSelectCharacter`의 저장·씬 전환을 통해 Main에 진입했고, 같은 앱의 새 프로세스에서도 선택과 모델이 유지됐습니다. 실제 가입/로그인·운영 PlayFab API는 호출하지 않았습니다.

| 단계 | 관측 |
| --- | --- |
| 빈 프로필 | 전용 폴더의 합성 Sex 빈 값, 원래 선택 화면 진입. 자동 선택·저장 없음 |
| 계정·개인정보 진입 | 선택 화면 하단 Account & privacy→설정→계정 패널을 직접 열고 Back/OK로 복귀. 이 오프라인 앱은 삭제 요청 불가 안내/비활성 버튼을 표시 |
| 방향 버튼 | 남성 모델에서 여성 모델로 원래 이동/표시 전환 |
| Play | 원래 SetData/getAllData/update→Main. `CHARACTER_UI_PASS phase=selection gender=Woman cloudGenderMatches=True modelMatches=True gold=20000 errors=0` |
| 강제 종료→새 실행 | PID10280→10595, 선택 화면을 다시 거치지 않고 Main. `phase=restart`, Woman/합성 서버 파일 로드/모델 일치/Gold20000/게임 오류0 |

Main HP100/도감·장비 빈 상태를 유지했고 화면의 여성 모델·Gold HUD도 확인했습니다. 개인정보 패널의 정책 링크·연령 변경·광고 설정 확인·삭제 요청·연락 버튼은 실행하지 않았습니다. 메뉴 접근과 오프라인 차단 관측을 실제 동의/삭제 접수·최종 정책 검증으로 확대하지 않습니다.

## 소스·산출물·보호

- checkout `D:/codex/worktrees/tamer-working/Tamer`, source `340c00640183b3e55158831a4ee254fe9a84c705`. 변경은 시험 전용 빌드/시작/관측·합성 저장 경로이며 원래 CanvasSelectCharacter UI나 저장·서버/씬 소유권 알고리즘을 바꾸지 않았습니다.
- `BuildCharacterUiAndroid`, APK 한정 `TAMER_CHARACTER_UI`·기존 offline define, 원래 Login/SetCharacter/Main/Game/NextScene5개를 빌드했습니다. 전역 시험 define은 거부합니다. 선택 화면의 수동 입력 대기는 총괄이 화면/로그와 종료 시각으로 관리했습니다.
- 앱 `com.AeDeong.MonsterTamer.revival.shoppurchase`의 독립 `RevivalCharacterManualUi` 폴더입니다. 이전 shop UI/P/I/실패 fixture를 보존하고 데이터 삭제 없이 update했습니다. 초기 Sex만 빈 값이며 Gold20000·Tutorial done은 선택 저장 경로를 분리하기 위한 fixture입니다. 튜토리얼 완료 절차를 시험한 것은 아닙니다.
- APK153908062bytes/SHA-256 `ee8a7acc7a1db447124472a2435b394036a47ae3b2c0e4781024da27fb101261`, 설치 base 바이트 일치. Unity6000.3.25f1/revision e1dba0a9aba4/CLI1.0.0-beta.8/min25/target36/ARM64/code26/debug 서명.
- 인터넷·결제·GMS광고ID 권한과 PGS/MobileAds 초기화 provider 부재·백업/기기전송 제외 확인. AdServices 권한 잔존을 전체 SDK 처리 부재로 확대하지 않습니다.
- 보호53개 사전 보존·빌드 후 읽기 검토·지정1회 복구·원바이트/Git clean/소유 Editor 종료 확인. 원시 화면·로그는 비공개 보존, 끝에 해당 앱만 종료했습니다. 운영 계정·기존 저장·No Ads·서명키·다른 LD 인스턴스 변경0.

읽기 검토·Android 컴파일·위 실제 버튼/화면·새 프로세스·diff check를 완료했습니다. 초기 화면 캡처는 원래 씬 전환 완료 직전이어서 대기 로그 후 선택 화면을 다시 관측했으며 실패/재실행으로 세지 않습니다. 이번 시나리오 실패0입니다. 기존 PR383의 Editor8/8은 해당 원소스의 별도 합성 근거로 유지하고 이번 결과를 그 원head 전체 재시험이라고 쓰지 않습니다.

LD Tamer index1/emulator-5556의 Android14/API34/x86_64 ARM64 번역·4KB 페이지 결과입니다. 실ARM64·16KB·Play 전달·실제 로그인/원격 저장·선택 중 계정 교체/네트워크 실패·운영 삭제·튜토리얼 전체·다른 캐릭터/해상도 전반은 미검증입니다. 통합 대상은 부모 gameplay 후보이며 main·운영 출시는 별도입니다.
