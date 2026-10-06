# 연령 변경 뒤 기존 UMP 개인정보 선택 접근 보존

2026-10-06 main `7f98963057ba62995122b6d0515499ef28cb7f14`에서 소스 경로를 확인했다.
기존 `LocalAgeChoice`는 edit 시작과 선택/저장 시 `Changed`를 발생시키며 manager는
그때 기존 consent gate를 Dispose/null로 바꿨다. 개인정보 옵션 표시는 `HasConsentAge`와
그 gate를 요구하므로 성인→미성년/미선택/거절로 변경하면 이미 발견한 Required 옵션에
접근할 owner를 잃을 수 있었다. 이 발견은 소스 경로이며 실제 기기 재현 결과가 아니다.

이번 수정은 기존 consent owner를 **privacy-only**로 보존한다. 광고 허용은 영구 false로
내리고 새 Request/Update/Gather를 시작하지 않는다. Updating의 늦은 callback은 version으로
폐기해 form을 자동으로 열지 않는다. 이미 Gathering/Privacy native form이 열린 경우에는
stage/version과 busy ownership을 실제 native completion까지 유지한다. 연령을 직접 바꿔도
form을 임의 종료/expire하지 않는다. 이전 pending completion은 전달하지 않으며 form 완료가
광고 eligibility·init/load를 되살리지 않는다. 이후 사용자가 명시적으로 기존 Required 옵션을
열면 completion은 false로 끝나고 광고를 자동 load하지 않는다.

manager는 active consent와 기존 privacy fallback을 구분한다. 개인정보 entry는 현재 age의
광고 허용 대신 **이미 존재하는 gate의 현재 Required 상태**를 따른다. 새로운 미성년 UMP
client·Update·message discovery를 자동으로 만들지 않는다. 성인 재진입은 기존 policy를
통과할 때 새 treatment/active gate를 만들지만, 새 active가 Required를 아직 제공하지 않으면
이전 Required fallback을 지우지 않는다. 새로운 Required owner를 제공할 때 bounded 교체하고,
열린 native owner는 완료까지 유지한다. edit/Select의 두 Changed, 저장 실패, 직접 age 변경에도
동일한 `SuspendAndRetainPrivacy` 규칙을 사용한다. manager 종료 시 두 owner를 Dispose한다.

No Ads 기존 권한·즉시 보상/buff·운영 gate와 readonly null binding은 변경하지 않았다.
광고는 계속 성인 전용이며 기존 UMP 허용/consumer 조건과 init/load 차단을 유지한다.
게임의 개인정보 안내·삭제 접근과 native UMP 옵션은 별개다. 신규 미성년의 Required
discovery나 모든 연령/지역의 실제 개인정보 적합성을 이번 수정으로 검증했다고 쓰지 않는다.

변경 관련 새 순수 fixture 10개가 첫 실행 PASS였다. Ready/Updating/Gathering/Privacy
suspension, 늦은 callback과 busy 해제, 명시 옵션 completion/ad false, Idle 신규 호출0,
Dispose, LocalAge edit/Select·저장 실패, 기존 Required→새 nonRequired→double age edit와
두 native owner 보존을 실제 gate/공유 helper로 확인했다. SDK client는 합성 구현이다.
manager/변경 gate/실제 Managers UMP client를 같은 source assembly에 포함한 Unity 참조
순수 컴파일도 첫 실행 exit0였다. 컴파일한 runtime assembly를 로드하지 않았고 기존
통과 회귀는 반복하지 않았다. 앞선 별도 consumer 컴파일2FAIL→명시 승인3PASS 이력은
그대로 보존하며 새 검증의 성공으로 과거 실패를 지우지 않는다.

실제 Editor/build·native SDK·form·광고·운영 승인 true 자료·resource/Unit 변경은 0이다.
따라서 이번 완료 범위는 소스 수정과 순수 ownership 검사다. 실제 기기에서 연령 변경,
open form 중 직접 변경, withdrawal·재시작·지역별 옵션·No Ads 동작을 확인하는 후속
검증은 미완료이며 `runtimePrivacyVerified/binaryVerified/distributable=false`를 유지한다.
