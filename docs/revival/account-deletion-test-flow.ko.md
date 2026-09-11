# 계정 삭제 요청: 합성 실행 구현 (#105)

기준 main `1e9f99cd837ca03e902b73dbcd228b27e1c57041`, 2026-09-11.
이 구현은 문서의 요청 흐름을 코드로 시험한다. **운영 삭제 서비스는 비활성이고 실제 계정·진행도·No Ads를 변경하지 않는다.**
운영 endpoint, 보관기간, 재가입 정책을 임의로 정하지 않았다.
[운영 결정 D1–D8](privacy-execution-readiness.ko.md)은 계속 미확정이며 그 결정을 대신하는 구현이 아니다.

## 구성과 책임

| 파일 | 실행 책임 | 운영 연결 경계 |
| --- | --- | --- |
| Assets/Scripts/Privacy/DeletionFlow.cs | 명시 재인증→요청 준비→확인→상태/취소, single-flight, 세션/소유 객체 변경과 Dispose 후 늦은 결과 거부 | 순수 C#; 파일 삭제·SDK·HTTP 호출 없음. 기본 UnavailableDeletionGateway는 인증/요청 자체를 시작하지 않음 |
| Assets/Scripts/Privacy/SyntheticDeletionGateway.cs | Editor/Development에서 synthetic- 계정의 메모리 요청·상태 대역, 대역 진행/완료·실패 주입 | 일반 release player에서 제외. 실제 인증, 사용자 데이터, 구매 권한을 읽거나 쓰지 않음 |
| server/privacy/deletion.py | SQLite 요청/중복 방지 키/확인 challenge/진행 상태/완료 증거 영속, 재인증 신선도·계정 귀속·정책 검사 | authenticate/provider/Policy를 주입. PlayFab 삭제 adapter와 비밀키 로딩이 없음 |
| server/privacy/http_app.py | 요청·확인·조회·취소 WSGI 경로, bounded JSON/중복 필드 거부, 오류 정보 제한 | listener/배포 없음. worker 실행은 기본 HTTP API에서 노출하지 않음 |
| server/privacy/local_demo.py, demo.html | 127.0.0.1 전용 합성 웹과 명시적 대역 진행 버튼 | 임시 DB, 고정 synthetic 계정·proof, 합성 정책만 사용. 실제 재인증의 증거가 아님 |

앱 설정의 진입/view/presenter와 그 버튼 회귀는 UI 담당의 별도 변경에서 이 core에 연결한다.
UI는 기본 미지원 설명과 비활성 실행 상태를 표시한다. 테스트 주입 시에도 완료를 **합성 완료**라고 표시해야 한다.
클라이언트의 Completed는 동일 요청/정책/범위와 비어 있지 않은 CompletionEvidence 응답이 있어야 수락한다.
이는 신뢰할 서버 adapter의 확인 결과를 표시하는 계약이며, 문자열 자체의 서명이나 PlayFab 삭제 완료를 검증하는 기능이 아니다.

## 재인증·확인·중복과 실패

현재 앱 세션은 owner 객체·계정 ID·sessionKey의 동일성을 검사한다. 재인증은 별도 gateway 메서드이며
계정 ID/캐시만으로 fresh proof를 만들지 않는다. 운영 adapter가 없다면 요청할 수 없다.
서버 authenticate는 신뢰 가능한 Principal(account, reauthenticated_at)을 반환해야 하며 클라이언트 body의
account/시간을 신뢰하지 않는다. 300초는 합성 흐름의 인증/확인 유효기간이며 데이터 보관기간이 아니다.

요청은 계정+clientKey로 중복 방지한다. 최초 응답 유실 시 같은 키로 확인 challenge를 다시 받는다.
challenge 원문은 DB에 저장하지 않고 해시와 만료 시각만 보관한다. 재발급하면 이전 challenge는 무효다.
확인 응답 유실 시 동일 요청으로 확인 또는 상태 조회를 반복하며 새 삭제 작업을 만들지 않는다.
다른 계정은 요청 ID를 알아도 조회·확인·취소할 수 없다.

`awaiting_confirmation → queued → processing → completed` 순서이며 확인/대기 중에는 취소할 수 있다.
worker가 processing을 영속 기록한 뒤에는 취소를 보장하지 않으며 취소 요청은 거절한다.
provider.reconcile(request_id, account, policy)는 같은 ID의 작업을 조회/재조정하는 **멱등 adapter 계약**이다.
응답 유실·예외는 processing으로 남는다. API 접수나 unknown 상태를 completed로 바꾸지 않는다.
완료 결과에는 provider의 명시적 completion_evidence가 필요하다. 재시작 후에도 같은 요청 ID를 사용한다.

클라이언트는 실패한 작업의 의도를 보존해 재시도한다. 재인증이 필요하면 ReauthenticateAsync를 사용한다.
확인 대기 상태의 재인증은 같은 clientKey의 challenge를 갱신하며, 제출된 요청은 상태만 다시 조회한다.
팝업 닫기나 프로세스 종료는 서버에 접수된 삭제 취소가 아니다. 현재 앱 core는 요청 ID를 디스크에 보존하지
않으므로 앱 재시작 후 접수 복구 UI는 후속 구현이다. 서버 DB 영속/멱등 시험과 이 제한을 구분한다.

## 정책 주입과 보존

Policy의 기본값은 enabled=false다. revision/scope와 retention_approved/rejoin_approved,
비어 있지 않은 retention_plan이 없으면 접수·확인·worker 실행을 차단한다.
정책 revision이나 범위가 바뀐 미완료 요청은 처리 전에 재검토해야 한다.
retention_plan은 provider에 전달하는 운영 검토 계약이며 자체적으로 파일/로그 보관 만료를 실행하지 않는다.
합성 예시는 합성 데이터에 대한 모의 처리만 표현하며 운영 보관기간의 권고안이 아니다.

SQLite에는 요청 ID, 계정, clientKey, 정책 revision/범위, 상태, challenge 해시/만료, 완료 증거가 남는다.
그 자체도 삭제·보관 대장의 대상이다. 운영 배포 전에 접근 통제·기산점·보관기간·만료 작업과
삭제 이후 상태를 조회할 제한된 인증 방법을 결정해야 한다. 현 schema는 신규 테스트 DB용이며
운영 DB migration/복원 또는 실제 계정 삭제 완료 이후 재인증을 구현했다고 주장하지 않는다.

기존 PlayerData.json·PlayerDataBackups·PlayerPrefs·No Ads는 이 기능에서 정리하지 않는다.
삭제 후 재가입/구매 복원 정책과 다기기·구버전 재업로드 통제가 미정이므로 실제 처리 adapter 활성화를 막는다.
신뢰 가능한 provider가 모든 대상 완료를 확인하기 전 로컬 데이터를 지우는 callback도 없다.

## 로컬 재현

```powershell
python -m unittest discover -s tools/revival -p 'test_deletion_server.py' -v
python -m server.privacy.local_demo
```

명시적으로 실행한 경우에만 [로컬 합성 페이지](http://127.0.0.1:8766/privacy/deletion)가 열린다.
재인증/요청 준비 → 의사 확인 → 대역 작업 접수 → 상태 조회 → 대역 완료 증거 제공을 누른다.
처리 중에는 완료가 아니라고 표시하며 마지막 문구도 실제 삭제가 없음을 표시한다.
종료는 Ctrl+C이며 임시 DB를 사용한다. 외부 인터페이스에 바인딩하거나 공개 배포하지 않는다.

서버 회귀는 WSGI→SQLite→SyntheticProvider를 소켓 없이 검증하고, 별도 실제 localhost 브라우저에서
같은 버튼/HTTP 흐름을 확인했다. C# core/UI는 합성 gateway로 검증한다. Unity 앱과 Python 서버를
HTTP로 연결한 검증은 아니며, 운영 HTTPS adapter는 endpoint/인증/삭제 정책 결정 후 별도 구현한다.
Unity 회귀와 정확한 소스는 [검증 기록](account-deletion-validation.json)에 기록한다.

## 공개 운영 정보 후보

기준 README의 기존 정책에서 운영자명 **AeDeong**, 일반 문의 **doeud1410@gmail.com**,
기존 정책 URL 후보 [저장소 README](https://github.com/ChoiDaeYoung-94/Tamer)를 확인했다.
기존 Console 읽기 기록도 저장소 루트 URL을 가리킨다. 새 Console 접근은 하지 않았다.
이는 이미 게시된 후보 정보이며 담당자/메일함의 현재 운영, 삭제 접수와 처리 기간을 확인한 것이 아니다.
동의 없이 메일을 보내거나 해당 주소를 작동하는 삭제 접수 endpoint로 연결하지 않았다.
