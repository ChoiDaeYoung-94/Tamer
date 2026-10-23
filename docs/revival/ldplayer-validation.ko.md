# LDPlayer 게임 시험 대상 확인

LDPlayer는 이동·상점·장착·HUD·씬 왕복 등의 보조 검증 환경입니다. 실제 휴대폰의 GPU·발열·터치·스토어 전달·Google 구매/복원·광고 정책과 ARM64 16KB 검증을 대신하지 않습니다. ARM64 APK가 번역 계층으로 실행돼도 실제 ARM64 하드웨어 결과로 표시하지 않습니다.

## 읽기 전용 사전 확인

```powershell
python tools/revival/ldplayer_preflight.py `
  --ldplayer-dir 'D:/path/to/LDPlayer14' --name Tamer `
  --apk 'D:/path/to/Tamer-gameplay.apk'
```

기존 이름이 정확히 일치하는 인스턴스 하나를 선택합니다. `list2`의 시작 상태, ADB 열거의 예상 인덱스 transport, LD 자체 인덱스 명령의 serial 확인이 모두 일치한 뒤 Android 버전·ABI·페이지 크기를 읽습니다. 이름 중복·대상 부재·offline·다른 transport·무응답은 준비 완료로 처리하지 않습니다. 다른 인스턴스 이름/serial·계정값이나 도구 stderr 원문은 보고에 출력하지 않습니다.

- 종료0: **연결 준비만** 확인. 설치 APK나 게임 통과를 뜻하지 않음.
- 종료2: 대상 연결 대기. 설정을 자동으로 바꾸지 않음.
- 종료1: 입력/도구/응답 문제. 원인 확인 전 설치·실행으로 넘어가지 않음.

도구는 설치·실행·연결 재시작·ADB/root/보안 설정 변경·앱 데이터 삭제를 하지 않습니다. 로컬 APK 옵션은 파일 존재·크기·SHA256 확인만 하며 설치된 파일의 해시와는 구분합니다. 실제 기기 명령 전에는 대상과 앱 ID를 다시 대조합니다. 설치/실행 명령의 exit0·무출력만으로 성공을 선언하지 않고 Package Manager와 게임 상태를 확인합니다. 전체 ADB 서버 종료나 다른 인스턴스 설정 변경은 사용하지 않습니다.

## 2026-10-23 관측

기존 `Tamer` 인스턴스1/LD14.0.32.0을 식별했습니다. 처음에는 다른 인스턴스만 ADB에 있었고 Windows 입력 도구의 대상 캐시가 유지되지 않아 APK 선택도 확인하지 못했습니다. 이후 설정을 자동 변경하지 않은 상태에서 Tamer transport가 관측됐습니다. 새 사전 확인은 Android14/API34, ABI목록에 x86_64/arm64-v8a 포함, **페이지4096bytes**를 읽었습니다. 실제 기기16KB 완료로 기록하지 않습니다.

기존 다운로드 전달 사본은 없었지만 빌드 원본 APK는 보존돼 있었고 SHA256은 `eb92ab0a45f27d5f1bf8f967fda8409c6f4aac79028bd7698e686f31b3c55269`로10/22 전달물과 같았습니다. 명시 대상에 설치한 후 Package Manager에서 별도 gameplay ID·debug·version26/min25/target36·primaryCpuAbi arm64-v8a를 확인했습니다. 이는 번역 게스트의 설치 정보이며 네이티브 하드웨어 확인이 아닙니다.

**첫 실행은 실패1회**입니다. `GAMEPLAY_HARNESS FAIL synthetic data`와 `Server response could not be applied safely`를 관측했습니다. 동시에 Play Games 프로필 선택창이 나타났습니다. 계정을 선택하지 않고 해당 앱만 중지했으며 원본 로그/화면은 비공개로 보존합니다. 다른 인스턴스·계정·앱 데이터는 변경하지 않았습니다. 원인을 확정하기 전 재실행하지 않고, 합성 read 적용 예외의 안전한 진단과 native SDK 시작 경로를 확인합니다.10/22 실제 폰 Main 성공과 오늘 LD 실패를 서로 덮어쓰지 않습니다.

도구의 모의 검증은 `python -m unittest discover -s tools/revival -p test_ldplayer_preflight.py -v`로 수행하며 실제 게임 시험 횟수와 구분합니다.
