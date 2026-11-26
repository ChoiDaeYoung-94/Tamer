# Unity Editor와 공유 ADB — 종료 부수효과 진단

2026-11-07 소스 `b0ca05022e3483973f986e1eb434f92813ece099`, Unity6000.3.25f1 / CLI1.0.0-beta.8 기준입니다. Android 빌드뿐 아니라 다른 프로젝트의 Win64 Editor 종료 후에도 공유 ADB 서버 단절을 관측했습니다. 명시 기기 명령0만으로 Editor와 기기 검증을 병행해도 된다고 판단하지 않습니다.

## 원인 경로

설치된 Android 확장 DLL을 수정 없이 읽었습니다. 종료의 `CleanupADB`는 `AndroidADBKillServerOnExit` 설정에 따라 `kill-server`를 호출합니다. 별도로 deployment target 조회에는 다른 SDK 경로의 `adb` 프로세스를 이름으로 찾아 종료하는 경로가 있고, `AndroidADBKillExternalInstance`로 제어됩니다. 두 설정의 현재 원값은 모두 DWORD1이었습니다.

따라서 ADB의 socket/port 환경변수를 다르게 지정하는 것만으로 Unity의 모든 종료 경로가 격리됐다고 주장하지 않습니다. DLL·SDK·JDK와 보안/인증 설정은 변경하지 않았습니다.

## 별도 실행 결과

다른 프로젝트의 Editor/기기 실제 반환과 새 범위를 확인한 뒤, 두 값의 정확한 원본을 보존하고 임시0으로 바꿨습니다. 하나의 no-build Editor에서 두 옵션이 false이고 선택한 LD 서버를 외부 프로세스로 인식함을 확인한 뒤 정상 종료했습니다. 소유 Editor/자식이 모두 끝난 다음 두 DWORD를 원래1로 복구했습니다.

| 확인 | 실제 결과 |
| --- | --- |
| 첫 시도 | C# 컴파일 성공, entry 인수 형식 불일치로 실행0. 미통과로 보존. 설정·소스 복구와 서버 존속은 확인 |
| 수정 시도 | 새 범위/metadata에서 명시적인 string 매개변수2개로 수정. 실제 C#·상하위 성공 응답과 CLI exit0 확인 |
| 공유 서버 | 시작 전후 PID·생성 시각·실행 파일 SHA·단일 loopback listener 일치 |
| 소유 실행 | Windows atomic Job의27프로세스 모두 종료, active0·강제 종료0·호스트 Editor0 |
| 원본 | 보호7,144파일 SHA·키/JDK·Git clean. 직렬화된 URP2개/ProjectSettings의3파일을 원본 바이트로 복구, Library 보존 |
| 공용 설정 | 두 ADB 옵션의 이름·형식·값을 정확 복구, SDK DLL SHA 불변 |
| 범위 종료 | 16:30:16KST 실제 전체 반환. APK/AAB 빌드0·기기 명령0 |

## 적용 한계

이번 결과는 **해당 no-build Editor 수명 범위에서의 서버 보존**입니다. 실제 Android 빌드의 모든 SDK client 호출이나 기기와 Editor의 병행을 검증한 결과가 아닙니다. 영구 옵션 변경을 적용하지 않았고, 기존 **Editor 수명 전체와 ADB 기기 검증 직렬화**는 유지합니다.

추가 범위에 임시 설정 보호를 적용하려면 최신 원값·소스/도구 pin·모든 공유 자원의 실제 상태·원본 복구를 다시 연결합니다. 소비된 시도/만료 범위를 재사용하지 않고, 공유 서버를 편의상 교체하지 않습니다. 원시 IL·레지스트리/프로세스·실행 증거는 비공개로 보존했습니다.
