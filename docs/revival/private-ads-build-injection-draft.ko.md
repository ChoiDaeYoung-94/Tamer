# 비공개 광고 단위 빌드 씬 주입 초안 — 실행 차단

2026-09-29, 기준 커밋 `28a5493a405d9863ba433564c8ef5ff51f5504ac`.
구현 checkout은 `C:\Users\pc_17\.codex\worktrees\e5e5\Tamer`,
작업 브랜치는 `codex/private-ads-build-injection`이다. 아래 결과는 이 기준에
미커밋 도구 초안을 추가한 상태의 검증이며 최신 main 또는 Unity 검증 결과가 아니다.

## 현재 상태

`tools/revival/private_ads_build.py`의 `--execute`는 무조건 거절한다.
C# 계약 검증이 두 번 실패하여 해당 파서와 종속 실행 경로를 중단했다.
이 초안은 병합·실행 준비 완료 상태가 아니며, 파서 수정과 세 번째 검증은
총괄 담당자가 사용자 승인을 취합한 후에만 재개한다.

- 첫 번째: .NET 표준 출력의 UTF-8 한글을 Python 기본 cp949로 읽다가 실패했다.
  C# 입력 계약 결과는 확인하지 못했다.
- 두 번째: 출력 인코딩 수정 후 순수 C# 템플릿은 .NET으로 컴파일되었으나,
  `JsonReaderWriterFactory` 기반 파서가 후행 객체와 마지막 쉼표를 허용했다.
  Python의 엄격한 JSON 계약과 불일치하여 실패했다. 파서 교체/보강은 미완료다.
- 별도 Python 합성 lifecycle 9개는 첫 실행에서 통과했다. 소유 파일 정리,
  강제종료를 모사한 잔여 journal 차단, meta 변조·미소유 파일 보존,
  원본 변경 보존/스냅샷 유지, 부분 staging 실패 복구, journal 변조,
  경로 이탈, 설정 digest 변경 거절을 포함한다.
- 독립 읽기 보안 리뷰 후 실제 배타 생성에 성공한 파일/폴더 identity를 별도로
  기록하고, 삭제 직전 identity/해시 재대조 및 최종 branch 일치를 추가했다.
  동일 바이트 타인 파일/타인 빈 폴더 보존과 `--execute` 차단을 추가한 Python
  lifecycle **12개가 통과**했다. 이 실행에는 중단된 C# 계약 검증을 포함하지 않았다.

Unity Editor/CLI 실행, 실제 Assets hook 설치, APK/AAB 생성, 기기 접근,
서명 변경, 실제 광고 요청, Console 제출은 수행하지 않았다.
실제 Editor API 호환성·씬 콜백·최종 바이너리는 미검증이다.
프로젝트 버전 지침 충돌도 해결하지 않았으며 새 Editor 설치/전환은 없다.

## 구현 경계

템플릿 두 파일은 `tools/revival/private_ads_build/`에 있어 Unity가 자동 컴파일하지 않는다.
래퍼의 기본 동작은 명시한 절대 checkout/설정 경로, 예상 HEAD, Editor 버전,
clean Git 상태와 기존 비활성 승인 계약의 읽기 전용 검사다.
설정 값은 private 파일에 유지하고 출력/예외 메시지에는 넣지 않는다.

미완성 실행 초안은 자기 checkout의 Editor가 닫힌 상태에서만 소유 journal을
배타 생성한 뒤, 고정된 `Assets/Scripts/Editor/RevivalPrivateAdsPreparation` 경로와
meta를 설치하도록 작성했다. 다른 프로젝트의 Editor를 종료하거나 원본 checkout을
변경하지 않는다. 자식 환경에만 opt-in을 전달하고 CLI 출력/Editor 로그는 private
실행 폴더로 보낸다. `--allow-dirty-build`는 설치한 hook 때문에 필요하며 실행 직전
추가 변경을 재검사한다. 버전은 명시값과 ProjectVersion 일치를 요구하고 자동 설치하지 않는다.

씬·프리팹·코드·ProjectSettings·Packages 등의 원본 바이트/해시를 private 실행 폴더에
보관한다. hook은 build-scene Login의 manager 정확 한 개만 수정하며 원본 저장,
프리팹 적용, 설정/서명 변경 코드는 없다. 기존 rewarded/No Ads와 production/regional/
연령별 승인 플래그는 그대로 OFF다. AAB 설정과 서명 준비는 별도 승인된 소유 절차가
먼저 완료해야 한다. hook 자체는 해당 설정을 바꾸지 않는다.

`finally`는 자기 소유 해시와 일치하는 hook/meta만 제거한다. 원본 변경이나 예상 밖
파일이 생기면 덮어쓰지 않고 journal/스냅샷을 보존한다. 강제종료로 finally가 생략되면
다음 실행은 journal/hook 잔여물로 중단한다. 이는 동시 사용자 편집을 막는 OS 잠금이
아니므로 실제 실행 시 해당 checkout의 독점 사용도 필요하다.
특히 경로 검사와 open/unlink 사이 경합은 재대조만으로 완전히 없애지 못했다.
향후 활성화 전에는 Windows handle 기반 쓰기/교체 배제 등으로 이 간격을 닫거나,
독점 사용을 보장하지 못하면 자동 삭제를 보류하는 설계가 추가로 필요하다.
현재 코드는 이 OS 수준 경합 문제의 해결을 주장하지 않으며 실행 차단을 유지한다.
Git ignore는 OS ACL/백업 동기화 접근 제한을 제공하지 않는다.

## 수동 복구 절차

1. 해당 checkout의 Editor가 닫혔고 다른 담당자가 사용하지 않는지 확인한다.
2. `.revival-local/private-ads-build-active.json`의 checkout/HEAD/runId와 소유 목록을
   확인한다. JSON 손상·소유 불명 상태에서는 자동 정리를 실행하지 않는다.
3. journal의 owned 파일을 실제 파일 해시와 대조한다. 일치하는 자기 hook/meta만
   제거하고 폴더는 비었을 때만 제거한다. 해시 불일치·목록 밖 파일은 보존한다.
4. sources 원본 해시와 각 private snapshot을 현재 파일과 비교한다. 동시 변경의
   소유자를 확인하기 전에는 원본을 덮어쓰지 않는다. 복구 승인 시에만 개별 파일을 복원한다.
5. Git HEAD/브랜치/변경 상태와 원본 복구를 확인한 뒤 journal을 해당 run 폴더로
   보존 이동한다. 스냅샷·로그·Library·다른 worktree는 삭제하지 않는다.

## 결과 해석

hook 영수증의 `buildSceneValueMatched`와 `injectedManagers`는 콜백 결과다.
`configuredAndroidDefines`는 Android 설정의 define이며 실제 player 컴파일 define의
증거가 아니다. `compiledEditorGatesDisabled`도 Editor 어셈블리 범위다.
최종 AAB 직렬화 단위, merged manifest, player define/금지 harness 포함 여부,
player gate, 서명/ABI 검증은 별도 필요하다. 이 초안은 이를 구현했다고 주장하지 않으며
모든 결과에서 `binaryVerified=false`, `distributable=false`를 유지한다.

실제 산출물이 없어 APK/AAB SHA-256 및 Android 도구/기기 결과는 없다.
합성 검증 환경은 Python 3.14, .NET SDK 9.0.301이며 Unity 버전/CLI 버전 결과를
대체하지 않는다. 세 번째 C# 계약 검증 승인 전에는 해당 테스트를 이름을 바꾸거나
동등한 입력으로 다시 실행하지 않는다.

## 승인 후 파서 교체 최소안 (읽기 조사, 미실행)

프로젝트 lock에는 `com.unity.nuget.newtonsoft-json` **3.2.2**가 이미 있으며
현재 PackageCache의 Runtime Newtonsoft.Json DLL/XML 문서도 존재한다.
새 패키지 추가나 PlayFab SDK의 파서 변경은 필요하지 않다.
PlayFab `SimpleJson.TryDeserializeObject`는 ParseValue 이후 전체 소비를 확인하지
않고, ParseObject는 같은 키에 재대입하므로 이 계약의 대체로 사용하지 않는다.

교체안은 기존 Newtonsoft의 JObject 구조/타입 검사와
`JsonLoadSettings.DuplicatePropertyNameHandling = Error`를 사용하여 중첩을 포함한
decoded 키 중복을 거절하는 것이다. 루트의 실제 Boolean/String 타입을 확인하고
명시한 세 bool 값 및 checkout/동일 게시자 규칙을 유지한다.
Newtonsoft도 확장 JSON 문법을 받아들이므로 단독 교체만으로 엄격함을 주장하지 않는다.
그 앞에 깊이/입력 크기 제한을 둔 작은 JSON 문법 검사기를 두어 다음을 요구한다.

- 객체/배열의 쉼표 뒤에는 반드시 다음 멤버/값이 있고 최종 닫는 괄호 뒤에는
  JSON 공백(공백, 탭, CR, LF)과 EOF만 존재한다.
- 키/문자열은 이중 따옴표와 JSON escape만 허용하며 제어문자, 주석, 작은따옴표,
  bare name을 거절한다. 숫자는 JSON number 문법만 허용하고 NaN/Infinity는 거절한다.
- 문자열 해석과 객체 생성은 기존 Newtonsoft에 맡기고, 문법 검사에서 값이나
  식별자를 오류 문자열에 포함하지 않는다. Newtonsoft 파서 예외도 generic 처리한다.

Python도 `parse_constant` 거절을 명시하여 미사용 evidence 필드 안의
NaN/Infinity까지 동일하게 거절하도록 맞출 계획이다. 이 보강은 아직 적용/검증하지 않았다.
승인 요청 범위는 **교체 후 같은 C#↔Python 계약 검증 한 번**이며, 기존 반례에
객체/배열 trailing comma, 주석/작은따옴표, 숫자 확장 문법을 추가한다.
새 Unity 실행·AAB 빌드·기기 검증은 그 승인 범위에 포함하지 않는다.
