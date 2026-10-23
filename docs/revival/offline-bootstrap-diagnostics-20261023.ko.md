# LD 오프라인 게임 초기화 진단 — 2026-10-23

## 현재 판정

기존 LDPlayer14의 Tamer 인스턴스에서 gameplay 첫 초기화가2회 실패해 해당 시험과 종속 게임 조작을 중단했습니다. 두 번째 진단에서 `bind-inventory / FileNotFoundException`으로 범위를 좁혔지만 정확한 파일 연산 원인은 아직 미확정입니다. 실패를 무시하거나 저장 보호를 완화해 Main으로 진행하지 않습니다. 세 번째 실행은 사용자 승인 전 시작하지 않습니다.

동시에 나타났던 Play Games 선택창은 offline manifest의 native 자동 초기화 제공자 제거 후 두 번째 실행 사진에서 나타나지 않았습니다. APK의 provider 부재도 검사했습니다. 이것을 합성 데이터 실패의 원인 해결이나 모든 SDK 통신 중단으로 표현하지 않습니다.

## 기준과 실제 관측

- checkout: `D:/codex/worktrees/tamer-working/Tamer`, 작업 브랜치 `codex/offline-bootstrap-diagnostics-1023`.
- Unity6000.3.25f1/e1dba0a9aba4, CLI1.0.0-beta.8, 별도 `com.AeDeong.MonsterTamer.revival.gameplay`, debug/min25/target36/ARM64/version26.
- LD14.0.32.0/Android14/API34, x86_64 게스트와 ARM64 번역 ABI, 페이지4096bytes입니다. 드라이버가 표시한 Adreno 문자열을 실제 Qualcomm 기기나 ARM64 하드웨어 검증으로 해석하지 않습니다.
- 같은10/22 APK의 개인폰 MainReady/HP100/Gold1000/오류0 사진은 과거 유효 근거로 보존합니다. 오늘 LD 실패로 그 기록을 삭제하거나, 반대로 폰 성공을 LD 성공으로 복사하지 않습니다.

| 실제 LD 시도 | APK 소스·SHA256 | 관측 |
| --- | --- | --- |
| 1 | f30a9e6 / eb92ab0a45f27d5f1bf8f967fda8409c6f4aac79028bd7698e686f31b3c55269 | 합성 read 적용 경고·FAIL synthetic data. Play Games 선택창도 관측. 계정 선택 없이 중지 |
| 2 | e612312 / 57fc48f7f1d87549eb17f190965e21b43cea74edca3bc6d245851009b142f4fc | bind-inventory/FileNotFoundException·FAIL synthetic data. provider 부재/선택창 미관측. 중지 후 프로세스 부재 확인 |

두 시도 모두 설치 성공과 Package Manager 정보를 확인하고 설치된 base.apk 바이트 해시를 원본과 대조했습니다. 게임 ready 실패이며 설치 성공을 기능 통과로 세지 않습니다. 원본 로그/계정이 표시된 첫 사진과 앱 소유 디렉터리 목록은 비공개로 보존합니다.

소유 앱 저장소의 읽기 관측에서 PlayerData.json2개(각351bytes), inventory pending2개(각165bytes), 최종 inventory JSON0개가 있습니다. 임시 파일의 바이트 존재는 flush/원자 교체 완료를 보증하지 않습니다. 권한 오류를 데이터 부재로 처리하거나 원본·pending 파일을 삭제하지 않았습니다.

## 변경과 한계

1. `OfflineManifest`에서 MobileAds와 PlayGamesInitProvider를 명시 제거합니다. 기존 매니페스트에 같은 provider가 있어도 제거 지시를 하나로 만들고 무관한 provider/활동은 보존합니다. native provider는 Unity의 Login.Start guard보다 먼저 실행될 수 있으므로 별도 경계가 필요합니다. 일반 운영 manifest 원문과 로그인 흐름은 변경하지 않습니다.
2. 합성 read 적용에 고정 단계와 예외 종류만 기록하고 원래 예외를 다시 전달합니다. 실패를 성공으로 바꾸지 않습니다. DataManager checkpoint는 UNITY_EDITOR/TAMER_GAMEPLAY_HARNESS 조건부 호출이며 일반 player에는 호출을 남기지 않습니다.
3. 두 번째 실패 후9d8be01에서 인벤토리의 경로 키·read·directory·serialize·pending open/write/flush·owner 확인·move/replace checkpoint와 숫자 HResult를 추가했습니다. 예외 메시지/파일명/경로/계정/응답 본문은 출력하지 않습니다. 이는 추가 진단이며 정확한 I/O 원인 수정이라고 주장하지 않습니다.

## 최소 검증과 보존

- e612312의 `RevivalGameplayIsolationTests`21/21 PASS(실패/skip0), 새 native 제거/기존 provider 보존과 read 실패의 닫힌 상태 진단 포함.
- 9d8be01의 상세 checkpoint/HResult 변경 후 같은 집중 범위21/21 PASS(실패/skip0). 두 실행을42개 고유 테스트라고 합산하지 않습니다.
- 각 실행 전 원본51개+포트2개를 보존하고, 원본/캡처51·현재53·GUID10·범위 밖 변경0을 읽기 대조했습니다. 검토한 복구를 각 실행당 한 번 수행해 원바이트·임시IAP2개 부재·clean·해당Editor 종료를 확인합니다. 다른 Editor/Library/계정/키는 정리하지 않습니다.
- e612312 APK빌드15:18:20~15:20:56KST 종료0,153859068bytes,위 두 번째 해시, provider2개/인터넷/결제/GMS광고ID 부재·debug서명 확인. 이 빌드는 두 번째 실제 LD 실패까지 관측한 산출물입니다.
- 후속9d8be01 상세 APK는 별도 빌드·정적 검사 근거를 아래에 추가하며 **설치/세 번째 실행 전 대기**합니다.

## 다음 순서

상세 후보의 고정 해시와 복구 완료를 확인한 뒤 사용자의 세 번째 초기화 진단 실행 승인을 받습니다. 승인되면 명시 Tamer 인스턴스/별도 앱만 업데이트하고 정확한 단계·오류 코드로 원인을 확인합니다. 원인 없이 재실행을 반복하거나 파일 보존/소유 확인을 생략하지 않습니다. 이동·상점·구매/장착·전투·씬 왕복은 초기화 성공 후 별도로 관측합니다. 실제 폰·운영 로그인·구매/광고·16KB·스토어·기존8Draft/main 출시 게이트는 여전히 별도입니다.

관련 [이슈398](https://github.com/ChoiDaeYoung-94/Tamer/issues/398), [진단PR399](https://github.com/ChoiDaeYoung-94/Tamer/pull/399)는 원인·기기 검증 전 OPEN/Draft입니다. 연결 도구[PR397](https://github.com/ChoiDaeYoung-94/Tamer/pull/397)의 부모 후보 Squash는 게임 실행 통과나 main 출시 병합이 아닙니다.

## 상세 후보 준비 완료 — 세 번째 기기 실행은 미실행

소스 `9d8be013cf696434d7a975c567660fb7564ea0d1`, 빌드15:26:13~15:28:09KST 종료0입니다. APK153846064bytes/SHA256 `1242837aa7bdb04bff076af8b243d5464bd8ad425217477c3cfb9f143b7a8b31`, debug 서명/별도 패키지/min25target36/ARM64 및 PlayGames·MobileAdsInitProvider/INTERNET/BILLING/GMS광고ID 부재를 확인했습니다. AdServices 권한은 남아 전체 수집 OFF로 표현하지 않습니다. C#·셰이더 오류 표식0은 빌드 범위이고 런타임 통과가 아닙니다. 실제16KB/strict/Play 전달은 실행하지 않았습니다.

빌드 전 기존 gameplay 출력3개를 보관했고, 원본/캡처51·현재53·GUID10·범위밖0을 읽기 대조한 복구1회 후 보호53개 원바이트·clean·Editor 종료를 확인했습니다. 개인 전달 후보는 `Build/revival/delivery-20261023/Tamer-gameplay-9d8be01.apk`와 SHA256SUMS.txt/실행 대기 안내입니다. 바이트 대조된 사본이며 Drive 업로드·추가 설치·세 번째 LD 실행은 하지 않았습니다. 기존 각 산출물/실패·파일 증거를 보존합니다.
