# 신규 손님 진입 구현·격리 검증 — 2026-11-02

사용자가 혼합 연령 대상을 유지하는 **신규 사용자 손님 플레이 구현·격리 검증**을 승인했습니다. 실제 운영 계정 생성·최종 심사·배포는 별도입니다. 검증한 checkout은 `D:/codex/worktrees/tamer-working/Tamer`, 브랜치는 `codex/mixed-audience-login-1102`, 최신 소스는 `9f9324eee24d89cd2feb69b4f58de1c64d65a986`입니다. Editor96개의 실행 head는 `fd3b365`이며 이후 차이는 검사용 Python 모형 보완뿐으로 Assets/Packages/ProjectSettings 차이가 없습니다. [설계와 공식 정책 입력](mixed-audience-login-20261102.ko.md), [PR500](https://github.com/ChoiDaeYoung-94/Tamer/pull/500), [이슈499](https://github.com/ChoiDaeYoung-94/Tamer/issues/499)를 연결합니다.

15:40 KST 후속: 기존 영문 loading 폰트를 새 한글 화면에 그대로 사용하지 않도록 기존 DungGeunMo 폰트를 LoginCanvas의 전용 입력에 Editor API로 연결했습니다. prefab의 의미 있는 변경은 참조1개이며 원래 GUID/구조를 보존했습니다. Editor가 덧붙인 빈 이름5곳의 공백은 엄격한 비교와 포맷 정리로 제거했습니다. 새 이용자의 Google 실패 문구도 다시 선택할 수 있다는 안내로 맞추되 기존 계정/pending의 복원 안내는 유지했습니다. 손님 선택에 진행·구매 권한 복구 제한을 명시했습니다.

## 바뀐 동작

- 정상 로드된 빈 최초 상태에서 `Google로 계속`과 `Google 없이 시작`을 같은 크기로 제공합니다. 손님도 서버 저장용 게임 계정을 사용하며 앱 삭제·기기 변경 후 복구 제한을 안내합니다.
- 손님을 직접 선택해야 무작위 CustomID와 `custom-pending`을 저장합니다. 저장·flush·read-back과 현재 소유 세대 검사를 마친 뒤 기존 CustomID 로그인으로 진행합니다. 저장이 불확실하면 같은 프로세스의 재시도나 Login 재생성으로 계정을 요청하지 못합니다.
- 기존 Google/Android/Custom 계정·진행/구매 흔적·불명확한 로드·삭제/receipt-only·기존 pending은 신규 손님 제안 대상에서 제외합니다. 로그인 실패를 자동 손님 생성으로 전환하지 않습니다. 재시작은 이미 저장한 같은 ID를 사용합니다.
- 선택은 소유 manager·계정 세대·삭제 epoch·화면 버전에 묶인 일회용입니다. 이전 화면의 늦은 콜백이 새 선택을 소비하지 못합니다. 로그인 실패 후 선택 재표시는 실행 종료 뒤에 수행합니다.
- Custom 경로에서 managed PGS 활성화와 Android 기기 ID 읽기를 건너뜁니다. 공식 `SUPPRESS_GAME_PROFILE_CREATION` 설정을 추가하되 native 자동 처리·모든 데이터 전송이 차단됐다고 판정하지 않습니다.
- SDK·운영 타이틀 보안 설정·기존 키·구매 권한·저장 형식·씬/프리팹은 바꾸지 않았습니다. 새 UI는 기존 LoginCanvas 아래에 구성하며 계정 생성 코드를 자체 소유하지 않습니다.

## 실제 검증과 복구

Unity `6000.3.25f1`/revision `e1dba0a9aba4`, 프로젝트 pinned CLI `1.0.0-beta.8`, Pipeline `0.6.0-exp.1`을 사용했습니다. Android 기준 min25/target36/ARM64는 유지했습니다. 공유 실행은 FPS/Paw 동의 후 직렬로 수행했습니다.

| 검사 | 최신 소스 결과 | 한계 |
| --- | --- | --- |
| `RevivalGuestEntryTests` | 33/33 | 가짜 설정/신원, 실제 PlayerPrefs·PlayFab 없음 |
| `RevivalGuestEntryViewTests` | 2/2 | 기존 한글 폰트의 prefab 연결·동일 크기/색상·콜백 교체·오류 표시. 실제 폰 렌더/입력은 별도 |
| `RevivalLoginContinuityTests` | 61/61 | 기존 계정 선택·생성 허용/콜백 등의 격리 경계. 운영 로그인 결과가 아님 |
| 관련 EditMode 합계 | 96/96, 컴파일 오류0 | 전체 회귀나 기존 모든 No Ads 시험을 재실행한 결과가 아님 |
| 실제 실패 처리 소스 추출 검사 | 22/22, 컴파일/실행 종료0 | 실제 StartLogin/RunLoginAsync/ShowRetry와 선택 조회·operation/guest 정책을 사용. UI·인증·저장은 가짜 경계, UniTask→Task 치환. 실제 SDK/계정·씬 실행 아님 |
| 새 화면 Editor 렌더 | 720×1280 일반·720×1560 실패 상태2장, 누락 글자0·넘침0·직접 시각 확인 | 실제 view/한글 폰트·1080 기준 폭을 임시 preview Canvas로 렌더. Android CanvasScaler·터치·safe area·저장/재시작 증거를 대신하지 않음 |
| Android release player C# | 68개 assembly 생성, main 존재, 오류0 | APK/AAB·IL2CPP/native/manifest merge·기기 실행이 아님 |
| 입력/키 복구 | 6,954파일 SHA·집합 일치, 원키 유지, Git clean, 소유 Editor0 | Library·원시 증거 보존, 다른 프로젝트 변경 없음 |
| GUID 참조 | 검사135파일, 미해결0 | 원래 에셋 검사의 범위대로 유지 |

Android 전용 `#if`의 실제 컴파일을 확인하기 위해 Unity [CompilePlayerScripts](https://docs.unity.com/ko-kr/engine/6000.3/script-reference/unityeditor/build/player/playerbuildinterface/compileplayerscripts)를 `ScriptCompilationOptions.None`으로 호출했습니다. 검사용 보조 코드 첫 시도는 `ReadOnlyCollection<string>`에 `Length`를 사용한 CS1061로 끝났고 제품 컴파일 호출은0회였습니다. 실패 원문을 보존하고 `Count`로 바로잡은 새 시도에서 통과했습니다. CLI의 바깥 종료0만 사용하지 않고 내부 명령의 성공과 실제 산출물을 대조했습니다.

초안 `5852b84`에서 첫95/95와 Android 컴파일을 확인한 뒤, 소스 검토에서 실패 처리 중에는 선택 gate가 아직 닫혀 있음을 발견해 `a42d5f7`로 복귀 시점을 수정했습니다. 이후 관련95개와 Android 컴파일을 확인했고, 위 폰트/문구·조회 보완 후96개·소스22개·최신 Android 컴파일을 실행했습니다. 변경 없는 성공 시험을 기록 목적으로 반복한 것은 아닙니다. 소스 추출 검사의 첫 시도는 가짜 모형의 `_firstEntry`/`AccountGeneration` 누락으로 컴파일에서 끝났고 실행0회였습니다. 모형만 보완한 새 시도에서22개를 통과했습니다. 실제 제품 검사 실패로 합산하지 않습니다.

렌더 원본은 PNG67,377B/SHA`83bbfdd41393572b53e77108cf190226de02ace9d93308bd32027e8b81618267`, PNG80,661B/SHA`87cb8a3765f8fb884071a3edab9be0bdabb162ead5c8e83030669911f6eb6b3d`입니다. 리사이즈/보정 없이 비공개 보존합니다. 렌더가 변경한 동적 폰트 데이터와 Editor 직렬화 URP 설정2개·ProjectSettings를 보존/원복했고 이후 보호6,954파일 SHA/집합·키·clean/Editor0을 대조했습니다. 자원 인수06:32:39.4874423 UTC/15:32:39.487 KST, 신규 실행 종료 후 실제 반환06:40:36.1000476 UTC/15:40:36.100 KST입니다. 실제 공유 사용/예약0으로 FPS/Paw에 통지했습니다.

## 아직 남은 완료 조건

1. Android의 실제 저장소와 입력·오류 후 복귀·앱 재시작을 분리된 시험 앱에서 확인합니다. Editor 화면/가짜 경계 통과를 실제 PlayerPrefs 저장이나 운영 SDK 증거로 확대하지 않습니다.
2. 운영 PlayFab의 신규 CustomID 생성 허용과 손님 전체 게임·서버 저장/복귀·삭제/구매 경계를 권한 있는 최종 제공판에서 확인합니다. 이번 시험은 실제 SDK 로그인·운영 계정·구매를 호출하지 않았습니다.
3. Console 현재 번호를 다시 읽고 새 서명 후보를 준비합니다. 후보30은 이 기능 이전의 별도 보존물이며 같은 번호/소진한 서명 계획을 재사용하지 않습니다.
4. [미저장 심사/데이터 안내안](guest-review-instructions-draft-20261102.ko.md)을 최종 제공판의 Google/손님 두 진입·연령 경로와 대조합니다. Data safety·공개 정책/삭제 안내 적용과 기존 심사 계정 No Ads 여부도 미확인입니다.
5. 기존 계정/저장·No Ads·연령/광고·개인정보·최종 UI/스토어, 모든 활성 출시의26 미포함과 실제 심사/프로덕션은 [전체 잔여](remaining-recovery-work.ko.md)대로 유지합니다. 이 격리 통과를 정책 승인이나 출시 완료로 표시하지 않습니다.
