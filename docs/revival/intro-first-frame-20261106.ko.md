# 인트로 첫 영상 전 흰 화면 보완 — 2026-11-06

후보32 사용자 시험에서 인트로 전에 흰 화면이 보인다는 피드백을 받았습니다. 다른 영역은 큰 문제가 없다는 총평이며 실제 계정/서버 저장·구매/광고/삭제 검증 통과로 확대하지 않습니다. 사용자 기기/설치물·지속 시간은 아직 계측으로 확정하지 않았습니다.

## 원인 경로와 변경

기존 `QuadDIntroCanvasOutput`은 Awake에서 흰 RawImage를 켜고 디코더 texture가 생길 때까지 기다렸습니다. 현재 UGUI는 texture가 없을 때 흰 기본 texture를 그리므로 준비 중 빈 흰 사각형을 노출할 수 있습니다. 앱의 splash background도 흰색입니다. Android 시작 창/Unity 시작/영상 준비 구간의 실제 노출 시간은 별도 관측합니다.

출력 이미지를 처음에 숨기고 해당 VideoPlayer의 첫 정상 frame-ready 알림과 실제 texture가 모두 있을 때 표시하도록 보완했습니다. 준비 배경은 영상과 같은 어두운 색(16,21,28)이고 입력을 가로채지 않습니다. 영상 tint/원본 영상·음향·재생 길이·준비8초 상한·오류 시 Login 진행은 기존 공통 패키지에 유지합니다. 첫 프레임 확인 뒤 반복 알림을 끄고 완료 후 늦은 프레임이 화면을 다시 켜지 못하게 합니다.

Editor API로 같은 시작 배경을 적용했습니다. Play/빌드 중이거나 첫 씬이 기존 Intro가 아니면 도구가 거부하며 씬/로그인·SDK·키 설정을 바꾸지 않습니다. 패키지 캐시와 씬 YAML을 직접 고치지 않습니다. 보존된 Android 생성 리소스에도 기존 시작 배경 `#FFFFFFFF`가 있었으며 새 APK에서는 실제 Android 리소스/첫 화면을 별도로 대조합니다.

## 검증 상태

14:46 후속: `b910acf`의 x86 시험 APK 생성은 성공했습니다. 실제 파일은101,348,896bytes/SHA256 `d21a15309491ce077c39b9de9a6c3c75e910ed65e77659bb3d3d9145841a93dc`이며 BuildReport totalSize와 구분합니다. 앱ID/명칭·debug 서명/운영·Play 인증서와 구분·min25/target36·x86_64·오프라인 권한/두 초기 provider 제거·실제 시작 리소스#FF10151C·native6개 LOAD/ZIP/보수적RELRO·원영상88032bytes1회/observer표기를 확인했습니다. raw RELRO끝 진단3개와 실제16KB/운영 실행은 별도입니다.

백업 차단 두 속성이 없다는 정적 gate에서 설치를 보류했습니다. 실제 기기 설치0이며 첫 APK/실패 검사를 보존했습니다. 시험 manifest가 기존 백업 차단/extraction 규칙을 사용하도록 하고 생성 callback의 exact 허용 목록에 Intro 두 ID만 추가했습니다. 운영 ID/임의 접미사는 제외하며 새2사례 검사·캐시 재패키징은 대기입니다. 영상/게임 진입·계정/키 설정의 새 제품 변경은 없습니다. 보호7137개/키/JDK·clean·소유Job0 복구 후05:46:27.672066UTC에 실제 자원을 반환했습니다. 아래는 이전 시도의 기록입니다.

`8ee2b48`에서 단일 CPU1 Editor/CLI 실행1회를 마쳤습니다. 기존10개는 통과했고 새3개는 시험 코드의 UI assembly 이름 지정 오류(TypeLoadException)로 실패했습니다. CLI0/중첩 run_script 실패/실제 NUnit10통과3실패를 구분하며 통과로 처리하지 않았습니다. 설치된 어셈블리에서 UI type을 찾는 기존 프로젝트 방식으로 검사 참조를 고쳤습니다. 다음 차례에는 실패3개와 추가된 x86 격리/패키징 경계2개, 총5개만 재검증하며 기존 성공 전체 검사를 반복하지 않습니다.

Editor 저장 때 URP low/medium 두 파일의 previous-version12→13 및 공백 직렬화가 함께 바뀌었습니다. 최초 복구 gate는 적용 전에 중단했습니다. 정확한 버전/공백 변경과 ProjectSettings의 배경·alias/공백만 검토한 뒤 의도한 배경1필드 외 보호7136개 바이트·원alias/키/JDK 설정을 복구했습니다. Library는 보존했고 Editor0 확인 뒤03:55:09.749885UTC/12:55:09KST에 실제 자원을 반환했습니다. 원래 파일 identity 복원은 주장하지 않습니다.

수정한 `dffac54`에서 새 관련5개(첫 프레임/실제 texture/완료 후 재표시 차단3개·x86 격리/패키징2개)를 실행해5통과·실패/건너뜀0을 확인했습니다. 중첩 run_script 성공과 실제 NUnit 결과를 함께 대조했습니다. 이전10개 통과와 대상 소스/범위가 다르므로 같은 소스15/15로 합산하지 않습니다. 소유 Editor 종료 뒤 보호7137개 바이트·키/JDK·소스 clean을 확인해04:17:11.403500UTC에 검사 단계를 반환했습니다.

실제 촬영을 위한 별도 x86_64 영상 시험 앱은 한 Intro 씬·Login 불가·debug signing·오프라인 manifest 경계입니다. observer는 Awake/준비/재생/첫 디코드/표시 시각과 디코드 전 노출 횟수·첫 화면을 기록합니다. 같은 `dffac54`에서 빌드1회를 시도했으나 run_script840초 상한에서 네이티브 C++ 컴파일 중 Timeout이 발생했습니다. 외부 CLI0과 내부 실패를 구분했으며 APK 생성0·설치/기기 검증0입니다. 컴파일 진행 표시의 분모가 변해 진행률로 계산하지 않았습니다.

CLI/Editor가 먼저 끝난 뒤 남은 소유 컴파일러는 자연 종료했습니다. 생성 시각을 대조해 빌드보다 먼저 실행된 원격 데스크톱 프로세스의 PID 재사용 오분류를 제외했고 해당 프로세스를 조작하지 않았습니다. 변경10개(격리 앱 설정·오프라인 manifest/광고 시험 ID·ABI 패키징·의존성/URP 직렬화)를 각각 검토하고 보호7137개 원본 바이트·키/JDK·clean·소유 프로세스0을 확인했습니다. Library/부분 컴파일 캐시는 보존했고04:36:00.010231UTC/13:36:00KST에 전체 공유 자원을 실제 반환했습니다. 성공 APK가 없어 제안했던 후속 LD 차례는 실행하지 않았습니다.

후속으로 캐시 보존·CPU8/private Windows Job·새 실행 상한을 준비했습니다. 14:00 차례에서는 Editor의 IL 후처리 실행기가 `msquic.dll` 접근 위반(c0000005)으로 종료해 Android 빌드 요청 전 대기했습니다. Windows Application 이벤트와 Library의 실행기 PID를 대조했습니다. DOTNET processor count를8→1로 바꾼 새 Editor에서도 같은 모듈/예외가 재현돼 이 변경만으로 해소되지 않았습니다. 각 Editor의 Android 요청0·APK0이며 최초 네이티브 컴파일 시간 초과1회와 구분합니다.

정상 종료 창/Pipeline이 없는 배치 대기 상태에서 해당 소유 Editor/CLI 신원을 대조해 종료하고 private Job 자식 잔존0을 확인했습니다. 두 후속 시도는 보호7137개 바이트 변경0·키/JDK·clean을 확인했습니다. Library/실패 이벤트는 보존했고 원래14:30 상한을 연장하지 않은 채05:10:56.355443UTC/14:10:56KST에 전체 자원을 조기 반환했습니다. CPU-affinity 제한 방식의 영향은 아직 가설이며 전체 게임이나 Unity 버전 전체의 오류로 확정하지 않습니다.

양측 총괄이 독립 실행의 비충돌을 확인한 뒤14:17에 같은 설치 DLL의 초기화/종료만 호출하는3사례를 비교했습니다. 강제 Job affinity8에서 같은 c0000005가 재현됐고, 초기 process affinity8 및 Job CPU-rate25%는 Open/Close 정상 완료·exit0이었습니다. 개별 process 방식에서는 vendor 초기화가 mask를8개→전체32개로 넓혔습니다. 새 실행 장치의 강제 Job affinity와 vendor 초기화의 충돌을 재현으로 구분했으며 실제 Editor 재시작 해소는 아직 미검증입니다.

이 진단의 Editor/빌드·listener/connection API·공용 설정 변경은0입니다. 각 Job active0·동일 DLL SHA불변과 도구 불변을 확인해05:17:01.485376UTC/14:17:01KST에 실제 반환했습니다. [.NET6.0.18 원문](https://github.com/dotnet/runtime/blob/v6.0.18/src/libraries/System.Net.Quic/src/System/Net/Quic/Implementations/MsQuic/Internal/MsQuicApi.cs)의 API version1과 [MsQuic 공식 API](https://microsoft.github.io/msquic/msquicdocs/docs/api/MsQuicOpenVersion.html)의 Open/Close 짝을 사용했습니다. 다음 전체 실행은 atomic Job/kill-on-close를 유지하면서 강제 affinity 대신 CPU-rate25%를 적용하는 조건으로 새 차례를 잡습니다.

진단 후 다른 프로젝트와 새 차례를 맞춰 격리 Android APK/실제 LD cold·warm 첫 영상 관측을 진행합니다. 기존 앱/데이터·운영 인증/계정 생성·서명키는 유지합니다. 수정본 제공 전 실제 APK SHA/설정 복구·소유 실행 종료를 기록합니다. 실제 Android 시작 창과 실물 ARM64/16KB는 각각 별도 검증입니다.

## Console 인수인계 재확인

11월6일 기존 작업 Chrome에서 제출 대기4개(프로덕션26 전체 출시·9세 이상 타깃·데이터 보안·판매자 상태), 정책 위반 세부정보의 대상26/5초 후 닫을 수 없는 광고를 직접 읽었습니다. 모든4개가 거절됐다고 판단하지 않습니다. 실제 프로덕션 제공24·내부31, 전체 번들23개/최고31·32 검색0개도 확인했습니다. 저장·업로드·전송·삭제·새 탭은0이며 사용자 탭을 게시 개요로 돌려놓았습니다.

새 최종 후보에서26을 모든 활성 출시의 Not included로 제외하고 공식 신고/심사 접근 안내와 대조해야 합니다. 현재4개 일괄 제출은 준비되지 않았습니다. 후보32 AAB/로컬 시험 APK는 보존하고 수정 후 번호는 당시 Console 상태와 대조합니다. 실제 내부 제공·정책 심사·프로덕션은 기존 별도 승인 범위입니다.

[전체 잔여](remaining-recovery-work.ko.md)와 [운영 검증 순서](candidate32-operating-validation-20261103.ko.md)를 유지합니다. 이번 작업은19:00KST 또는 계정 주간30% 초과 중 먼저 오는 때 정상 복구/기록/상세 보고하며, 실제 시간/사용률과 실행 검증을 분리합니다.
