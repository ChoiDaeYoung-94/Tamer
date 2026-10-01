# 격리 publisher UMP 등록 요청 준비

## 소스 범위

`RevivalAdHarness`의 `TAMER_UMP_PUBLISHER_HARNESS` 분기에만 명시적 등록 버튼을 추가한다.
Android 개발 빌드, 격리 패키지 `com.AeDeong.MonsterTamer.revival.umppublisher`, 기존
`Assets/Tests/Scenes/RevivalAdHarness.unity`, Managers 부재, 운영 광고·지역 검토 gate false를
모두 만족해야 실행한다. 기본 `GoogleUmpConsentClient()`에서 `Update(true, callback)`을
한 번만 직접 호출한다. debug geography·test-device hash 설정은 전달하지 않는다.

프로세스 수명의 static latch로 객체 재생성 이후에도 재요청을 막고, 시도 이후 기존
동의 UI도 잠근다. 콜백은 기존 큐로 메인 스레드에서 처리하며 객체 소멸·늦은 콜백은
무시한다. 30초 초과·SDK 실패·예외는 중단 상태로 남긴다. 성공 콜백도 해시 확인을
기다리는 중단 상태이며 자동으로 다음 테스트에 진입하지 않는다.

Gather·폼·privacy options·Reset·managed Mobile Ads initialize/load/show 호출이나
운영 gate 변경은 이 등록 경로에 없다. 실제 UMP 네트워크 요청이므로 native SDK 수집
또는 네트워크 0으로 표현하지 않는다. TFUA=true는 기술적 요청값이며 법적 연령 승인이나
성인 EEA 동의 성공 근거가 아니다. SDK가 등록 해시를 출력한다는 보장도 없다.

## 단계별 실행 계획 — 별도 지시 전 실행 보류

아래는 실행 전 계획이다. 후속 지시로 진행한 빌드·교체 설치·등록 첫 시도의 실제 결과는
문서 끝에 구분해 기록한다.

1. 변경 커밋과 clean checkout을 고정하고, 해당 checkout Editor 부재를 확인한다.
   기본 검증은 Unity 번들 Roslyn과 기존 publisher player RSP의 참조·소스로 수행하는
   순수 컴파일 1회다. 출력만 ignored 증거 폴더에 저장한다. Editor·assembly load·빌드·기기
   실행은 포함하지 않는다. 컴파일 결과와 exact commit을 독립 검토에 전달한다.
2. 빌드 지시 후에는 기존 `BuildUmpPublisher`와 이전 검토 runner를 재사용해 exact
   commit/branch·CLI SHA·Unity 6000.3.25f1/e1dba0a9aba4·private config·전체 Assets와
   ProjectSettings·meta/GUID·서명키를 고정한 새 manifest를 준비하고 먼저 검토받는다.
   기존 APK SHA `06ae4ff09fb515bde71813b05200b63928e19915392145c7716335d01b4fb21e`를
   same-handle 검증한 비공개 사본으로 보존한 뒤, 별도 승인된 정확한 출력 파일 처리만
   수행한다. 경로를 바꿔 기존 APK를 덮어쓰거나 보존 여부를 추정하지 않는다.
   BuildPlayer 출력은 기존 `Build/revival/Tamer-ads-ump-publisher.apk`로 고정돼 있다.
   빌드는 1회, 같은 격리 패키지/debug signing/min25/target36/ARM64/네 가지 harness define을
   사용한다. 빌드 종료 후 원래 identity와 변경 파일의 정확한 복원 여부를 검토한다.
   Library·원본 D checkout·다른 Editor는 보존한다. 16KB 전체 충족 주장은 하지 않는다.
3. 설치는 아직 승인되지 않았다. 현재 기기에 이미 설치된 격리 앱과 데이터를 보존해야
   하므로 기존의 신규 설치 runner를 재실행할 수 없다. 새 APK SHA·package·debug cert와
   현재 설치본의 package/cert/version/source 근거, 단일 기기·owner profile을 읽기로 확인하고
   설치 계획을 다시 검토한다. 같은 격리 패키지의 데이터 보존 교체 설치 `adb install -r`
   1회는 명시적 별도 지시가 있어야 한다. clear/uninstall/downgrade 우회는 하지 않는다.
   확인 불가·불일치 시 중단한다. 기존 install 1회와 최초 PID 확인 실패 1회는 보존한다.
4. 기기 등록 요청도 별도 지시가 필요하다. 명시적 버튼 탭 1회만 허용하고 같은 앱의
   정확한 단일 PID를 제한 시간 내 읽어 확인한다. 즉시 PID가 없었던 기존 실패를 성공으로
   바꾸지 않는다. 기동/등록 시작 전후 로그 범위를 비공개로 보존하고 이번 요청의 SDK
   등록 메시지에서 정확히 단일 원문 해시를 확인한다. 0건·다중·오류·timeout은 중단하며
   재bootstrap·fake hash·대소문자 변환은 하지 않는다. 이후 입력은 원문과 Ordinal로 대조한다.
   등록 요청 성공만으로 forced EEA 테스트를 실행하지 않는다.

## 보존할 이력과 현재 검증 한계

기존 Adult US 3차 성공 기록과 이전 두 실패·원문 해시 변환 오류는
`publisher-ump-harness-preparation.ko.md`에 그대로 남긴다. 현재 빌드의 올바른 해시를
사용한 새 EEA 관측은 아직 미실시다. 기존 신규 설치는 성공했으나 최초 PID 확인에서
runner가 실패했고, 후속 읽기에서 실제 시작을 확인했다. 이 둘을 구분해 보존한다.
이번 준비는 Console 설정·운영 광고·로그인·구매·저장·계정·국가 범위 변경을 포함하지 않는다.
원시 로그·기기 식별값·publisher 설정·SDK 해시는 ignored 폴더 밖에 공개하지 않는다.

## 2026-10-01 실제 빌드·교체 설치와 등록 첫 시도

실행 소스는 `9d215537a77ecc949f4d5e60724b2305609cdaeb`(PR #293), Unity
`6000.3.25f1`/revision `e1dba0a9aba4`/CLI `1.0.0-beta.8`, min25·target36·ARM64·
IL2CPP·debug signing·version1.0.5/code26·기존 격리 패키지와 harness 씬이다.
앞선 순수 Roslyn 컴파일 1회 PASS와 실제 등록 런타임 결과는 별개다.

기존 `BuildUmpPublisher` **1회 exit0**로 새 APK 116,380,113 bytes를 생성했다.
SHA-256은 `4432efd1cf0018d65c7cc610891486b5b1944490c323514549813b7c0ee6d00c`다.
player의 네 harness define와 `UNITY_EDITOR` 부재, 실제 게시자 App ID 일치 및 기존
debug certificate 일치를 확인했다. APK 검사 출력 파싱 실패 1회는 보존했고, 외부 명령을
재실행하지 않은 기존 출력 재파싱 2차가 PASS였다. 최종 독립 검토도 통과했다.
변경 파일 8개 복원·신규 파일 2개 비공개 보존 후 정리, 보호 파일 6,861개·Assets 6,827개·
meta 3,603개의 내용 대조와 Git clean/Editor 종료를 확인했다. 기존 APK의 보존본과 새 APK의
원본·보존본은 유지했다. 씬 내용·meta/GUID는 일치하나 빌더 저장으로 파일 identity가
변경됐으므로 원래 파일 identity까지 복원됐다고 주장하지 않는다.

데이터 보존 교체 설치 `adb install -r` **1회**와 앱 시작 **1회** 후, 설치된 APK가
새 APK와 바이트·해시로 일치하고 해당 앱 PID의 실제 부팅 로그가 있음을 확인했다.
clear·uninstall·reset·downgrade는 하지 않았다. 앱 데이터 내용을 검사하지 않았으므로
이 설치 방식만으로 기존 데이터 불변을 보증하지 않는다. 앞선 최초 PID 확인 실패 1회와
이후 부팅 확인, 이번 등록 실패는 서로 다른 결과로 보존한다.

명시적 등록 버튼 **1회**에서 managed 등록 시작 1건 뒤 catch 예외 중단 1건이 기록됐다.
완료 콜백·실패 콜백·timeout·SDK 등록 해시는 모두 0건이었다. **등록 첫 시도는 실패**이며,
실제 네이티브 SDK Update 도달과 네트워크 발생 여부는 미확정이다. 현재 catch가 예외
type/message를 남기지 않아 원인은 확정하지 못했고 정적 조사 중이다. 자동 재시도·추가
빌드/SDK 실행·EEA 관측은 하지 않았다. 가짜 해시나 반복 bootstrap으로 진행하지 않는다.
운영 gate는 false로 유지하며 운영 로그인·저장·구매·managed 광고 초기화/로드/표시는 0회다.
과거 성인 미국 3차 성공은 유지하고, 새 EEA/연령·전체 16KB·최종 AAB 검증은 미완료다.
원시 로그·기기 식별값·SDK 해시·비공개 구성과 경로는 공개하지 않는다.

### 정적 조사에서 확인한 null 설정 결함과 수정 준비

실행 소스의 기본 `GoogleUmpConsentClient()`는 `_debugSettings`를 null로 유지하면서
harness define 경로의 `Update`에서 SDK의 기본 `ConsentDebugSettings` 객체를 null로
덮어쓴다. 로컬 UMP 11.5.0.0 DLL의 IL과 [공식 Android 변환 소스](https://github.com/googleads/googleads-mobile-unity/blob/v11.5.0/source/plugin/Assets/GoogleMobileAds/Ump/Platforms/Android/Utils.cs)를
대조하면 이 설정의 `DebugGeography`를 null 검사 없이 읽는다. 이는 확정된 정적 결함이다.
다만 실제 첫 시도는 상세 예외를 기록하지 않았으므로 이번 예외가 이 지점에서 발생했는지는
미확정이다. 더 앞선 factory·JNI 초기화 등 다른 지점의 예외 가능성을 배제하지 않는다.

기본 요청에서는 SDK 기본 debug 설정 객체를 보존하고, caller의 강제 geography·test-device
hash 설정을 넣지 않도록 수정 준비 중이다. 이를 SDK debug 객체 자체가 null이거나 전혀
전달되지 않는다는 뜻으로 확대하지 않는다. 예외 type만 기록하는 최소 진단도 준비하며,
새 APK·실제 SDK 등록 재요청·수정 후 런타임 원인 검증은 아직 수행하지 않았다.
