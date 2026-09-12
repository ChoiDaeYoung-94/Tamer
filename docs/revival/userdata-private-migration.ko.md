# 기존 Public UserData의 Private 이행 준비 (#105)

현재 상태(2026-09-12 KST): 기존 76개 타이틀 계정은 별도 승인 삭제 후
[일반 목록·세그먼트 0명](title-player-deletion-execution.ko.md)으로 확인됐다.
이 문서의 도구·절차는 실제 대상 재등장 시 재평가할 조건부 계약이며, 삭제된 76개의 이행 작업이 아니다.
대상 확보를 위한 로그인·재생성·추가 삭제·조회 반복은 수행하지 않는다.

2026-09-11, 기준 소스 main `4e8c039`. **운영 이행은 하지 않았다.**
도구는 로컬 JSON만 읽고 익명 집계만 출력한다. API 호출, 자격 증명 입력, 값 쓰기,
삭제, 실행 가능한 요청 파일 생성 기능이 없다. 성공 종료도 운영 실행 승인이 아니다.
후속 [실행 준비안](privacy-execution-readiness.ko.md)에 canary·쓰기 배제·중지/복구 기준과 운영 결정 목록을 정리했다.

## 바꿀 대상과 보존 계약

`ServerManager.CreateWriteRequest`의 Private 설정은 이후 쓰는 키에만 적용된다.
[UpdateUserData 공식 계약](https://learn.microsoft.com/en-us/rest/api/playfab/server/player-data-management/update-user-data?view=playfab-rest)은
같은 키의 값을 덮어쓰고 해당 요청의 키에 권한을 적용한다. 이 API의 요청 스키마에는
예상 DataVersion을 검사하는 CAS 인자가 없다. 따라서 조회한 값 재전송은 동시 저장을
잃게 할 수 있고, 조회를 두 번 해도 그 이후 경합을 막지 못한다.

초기 검토 목록은 `Gold`, `Power`, `AttackSpeed`, `MoveSpeed`, `Tutorial`, `AllyMonsters`,
`Sex`, `GooglePlay`, `GoogleAdMob`, `NickName`이다. `GoogleAdMob`은 보상 버프 시각/초기화
문자열로 BuffingMan·GoogleAdMobManager가 DataManager를 통해 저장한다.
알려진 키라는 사실만으로 운영 변경을 승인하지 않는다.
서버 기능·구버전이 공개 조회를 사용하는지 별도 확인한다. 모르는 키는 집계 후 별도 검토하며
일괄 Private 처리하지 않는다. `__TamerAccountOwner`는 로컬 귀속 필드이므로 서버 이행 대상이 아니다.

키 추가·삭제, 값 정규화, CSV 순서 변경, 숫자 변환, No Ads 권한 재계산을 하지 않는다.
`GooglePlay`의 기존 `ProductNoAds`와 미지의 과거 토큰도 **문자열 그대로** 보존한다.
로그인 방식·계정 식별자·로컬 파일·백업도 이 이행에서 바꾸지 않는다.

## 오프라인 도구 사용

운영 데이터로 시험하지 않았다. 아래 입력은 가상의 예시다. 권한 있는 운영자가 나중에
개인 저장 영역에 준비할 입력은 각 계정의 **필터 없는 전체 UserData 조회**여야 한다.
`Keys` 선택 조회나 `IfChangedFromDataVersion`으로 생략된 응답을 전체 응답으로 포장하면 안 된다.
다른 계정을 클라이언트 API로 조회한 Public-only 결과도 사용할 수 없다.
도구는 입력이 실제 전체 응답인지, 전체 사용자 모집단인지 증명하지 못한다.

```json
{
  "schema": 1,
  "title": "synthetic-title",
  "scope": "complete-userdata-per-listed-player",
  "players": [{
    "PlayFabId": "synthetic-player",
    "DataVersion": 7,
    "Data": {
      "Gold": {"Value": "00100", "Permission": "Public"},
      "GooglePlay": {"Value": "ProductNoAds", "Permission": "Private"}
    }
  }]
}
```

선택적 `LastUpdated` 문자열을 허용한다. 비교에서는 서버 수정 시각을 값과 혼동하지 않는다.
그 외 필드는 거부하므로 원시 응답의 티켓·인증·프로필을 통째로 넣지 않는다. 입력 최대 16 MiB,
중복 JSON 키·중복 계정·누락 권한·null 값·다른 title·다른 계정 집합은 오류다.
대규모 대상은 동일한 계정 집합의 제한된 청크로 나누고 누락/중복 모집단 관리는 별도로 한다.
개인 입력은 이미 gitignore된 `.revival-local/privacy/` 등에 보관하고 커밋하지 않는다.
출력에는 계정·title·키 이름·값·그 해시를 넣지 않는다. 집계도 운영 민감도를 검토한 뒤 공유한다.

```powershell
python tools/revival/audit_userdata_privacy.py .revival-local/privacy/before.json
python tools/revival/audit_userdata_privacy.py .revival-local/privacy/before.json --compare .revival-local/privacy/refreshed.json --phase preflight
python tools/revival/audit_userdata_privacy.py .revival-local/privacy/before.json --compare .revival-local/privacy/after.json --phase postflight
python -m unittest discover -s tools/revival -p "test_audit_userdata_privacy.py" -v
```

종료 코드: 0=입력 유효/요청한 비교 통과, 1=비교 불일치, 2=입력 오류.
Public 키가 있다는 이유만으로 audit가 실패하지 않는다. 보고서의 known/unknown 수를 읽는다.
preflight는 버전·키·값·권한이 같아야 통과하며, 값이 바뀌었다 되돌아와도 버전 변화를 잡는다.
postflight는 알려진 Public 키만 Private로 바뀌고, 모든 값·키·나머지 권한은 같아야 통과한다.
대상 키가 있으면 버전 증가, 없으면 버전 유지를 요구한다. 버전 wrap/재설정도 수동 검토로 남긴다.
postflight 통과는 모르는 Public 키까지 해결했다는 뜻이 아니다. after 입력을 audit로 다시 집계한다.

## 향후 운영 이행 순서 — 이번 도구에는 실행 기능 없음

1. 운영 담당자가 title·대상 모집단·공개 조회 사용처·변경 키를 확정하고 사전 집계를 검토한다.
   모든 쓰기 주체(구버전 앱, 현재 앱, CloudScript, 운영 도구)를 식별한다.
2. 대상의 쓰기를 서버에서 실제로 중지/직렬화할 수 있는 방식을 먼저 검증한다.
   단순 앱 업데이트 권고나 preflight 통과를 잠금으로 취급하지 않는다.
   안전하게 배제할 수 없다면 이행을 실행하지 않고 서버 저장 설계를 먼저 보완한다.
3. 제한된 합성 테스트 계정에서 재로그인·저장·No Ads·다른 계정의 공개 조회 비노출을 시험한다.
   운영 백업은 접근 통제·보관/삭제 기준을 별도로 정한 개인 영역에만 둔다.
4. 쓰기 중지 상태에서 전체 값을 새로 읽고 preflight를 수행한다. 미래 별도 실행기는 그 시점의
   값을 그대로 Private로 쓰고 `KeysToRemove`/null은 사용하지 않는다. 실패 후 오래된 요청을
   무조건 재전송하지 않고 계정 상태를 재조회한다. 서버 비밀은 앱·저장소에 넣지 않는다.
5. 같은 범위의 전체 재조회로 postflight와 after audit를 수행한다. 불일치면 중지하고 원인을
   조사한다. 오래된 백업으로 값을 되돌리거나 Public으로 복구하는 자동 rollback은 하지 않는다.
6. 구버전 재공개를 막는 운영 통제를 유지하고 재발 집계를 설계한다. 실제 적용 커밋,
   대상 수·키 수·실패 수·검증 결과만 공개 증거에 남긴다.

이행 완료 여부는 이 문서/단위 테스트로 확정할 수 없다. 앱의 Private 쓰기 변경과 운영 기존
데이터 정리는 별도 상태이며, 이번 PR은 후자의 준비만 완료한다.
