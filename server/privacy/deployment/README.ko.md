# 삭제 접수 연결 묶음 — 미배포

사용자 최종 범위는 **정상 API 접수 안내 → 로그아웃과 소유 확인된 해당 계정 로컬 정리 → 사용자 흐름 종료**다. 공급자 최종 소거 증거·완료 통지·지원 문의·완료 worker는 필수가 아니다. `accepted`를 `completed`로 바꾸지 않는다.

## 이번에 바로 재사용할 코드

`TitleDeletionProvider(..., components=None)`으로 접수 전용 구성을 만들고 기존 DeletionService의 신선한 principal 확인, request/confirm 후 서버가 도출한 DeletionTarget을 `bind_confirmed`한다. 이어 `submit_confirmed(request_id, authenticated_account, policy)`를 호출하면 완료 callback 없이 제출한다. 정상 접수는 `accepted`, 이미 불확실한 요청은 `submission_unknown`이며, 취소 요청은 거절한다. 첫 timeout은 기존 `Rejected(provider_unconfirmed)`로 전달되고 영속 상태는 unknown으로 남는다. 이 API는 신뢰된 서버 내부용이며 직접 클라이언트 입력으로 account/target을 만들지 않는다.

접수 전용 구성에서는 `DeletionService.advance`/provider `reconcile`을 호출하지 않는다. 기존 강한 완료 경로는 호환용으로 남지만 필수 실행 경로가 아니다. 현재 HTTP/UI는 아직 `submissionState`를 소비하지 않는다. 새 메서드만으로 배포 가능한 서버가 완성된 것은 아니다.

## 검토 가능한 파일과 자원

- `resources.review.json`: 신규 resource group 1, Linux Flex FC1 plan 1, HTTP Functions app 1, StorageV2 Standard_LRS 1(비공개 package container와 DeletionLedger table 포함), Standard Key Vault 1, app identity의 storage/vault 역할 목록. **ARM/Bicep 실행 파일이 아니며 자원을 생성하지 않는다.**
- `inputs.example.json`: 구독/tenant/지역/이름/runtime/월 예산/요청량, 고정 title/정책/보관/인증 issuer·audience/콜백 origin/unknown 예외절차, secret URI 목록. 실제 값은 비공개 파일에서만 채운다. null은 미확정이며 계정 키·토큰을 채워 Git에 넣지 않는다.
- `connection.review.json`: 기존 HTTP 다섯 경로와 필요한 변경, 정확한 접수 상태 투영, 최소 영속 필드, 로컬 정리 제한. 제출·접수 플래그 기본 false, 구현 전 환경변수가 동작한다고 가정하지 않는다.

[Azure 공식 Flex 예제](https://learn.microsoft.com/en-us/samples/azure/azure-quickstart-templates/function-app-flex-managed-identities/)를 기준으로 자원 유형을 대조했다. 예제의 별도 Insights/Log Analytics는 이번 최소 목록에 포함하지 않았다. 로그는 상태별 집계/오류 코드/시간만 허용하고 요청 body·인증 헤더·원시 계정 식별자는 남기지 않는다. 공개 HTTP endpoint는 자체 인증을 구현한 뒤에만 활성화한다. 신선한 재인증과 서버의 대상 도출은 아직 연결 전이다.

## 비용 입력과 생성 승인 범위

[Functions 가격표](https://azure.microsoft.com/en-us/pricing/details/functions/)의 Flex 무료 할당은 유료 consumption 구독 전체 월 250,000 실행/100,000 GB-s이며 Storage는 제외다. Always Ready는 0으로 계획한다. 비용식은 `max(0, 총실행-공유무료잔여)×실행단가 + max(0, 총GB-s-공유무료잔여)×시간단가 + 저장용량/트랜잭션 + 네트워크 + Key Vault 작업`이다. [Key Vault 가격](https://azure.microsoft.com/en-us/pricing/details/key-vault/)도 별도다. 지역 단가·구독 자격·실제 월 한도는 미확정이므로 0원 보장이나 월 확정액을 쓰지 않는다. timer polling은 제외했으므로 그 기본 실행 비용은 없다.

승인 요청은 미확정 입력을 채운 뒤 위 자원/역할/지역/비용 한도와 비운영 target, 기본 비활성 배포 산출물을 한 묶음으로 제시한다. 아직 생성·role assignment·secret provision·배포는 하지 않았다. 런타임/의존성 pin과 실제 Table adapter가 없으므로 지금 실행 가능한 배포 명령을 제공하지 않는다.

## B: 로그인/가입 정책 읽기 대조

[API 정책](https://learn.microsoft.com/en-us/xbox/playfab/live-service-management/gamemanager/api-access-page-doc)은 API 경로/호출자와 일부 조건을 제어한다. 삭제 대장의 계정별 상태나 요청의 CreateAccount 값에 따라 자동 판정하는 기능은 확인하지 못했다. 전체 Login API Deny는 정상 기존 로그인도 막을 수 있다. [Player encryption](https://learn.microsoft.com/en-us/xbox/playfab/identity/player-identity/encryption/player-encryption-services)의 서명/암호화는 클라이언트도 사용 가능하므로 이것만으로 서버 가입 허가가 강제된다고 보지 않는다. 공개키 암호화의 제외 필드에 CreateAccount도 포함된다.

앱에서 접수 중에는 현재 계정 로그인/자동 생성/중복 확인을 막고 요청 세대가 바뀐 늦은 callback을 무효화한다. 정상 accepted 뒤에는 완료 증거가 없다는 이유로 재가입을 영구 차단하지 않는다. 이후 별도 사용자 동작으로 새 진행도 가입을 시도하고 실제 AccountDeleted 등 응답에 맞춰 재시도 안내한다. unknown에서는 성공 안내나 데이터 정리를 하지 않고 동일 요청의 접수 여부를 복구한다. 구버전 직접 API 우회까지 해결됐다는 주장은 하지 않는다. 현재 API 설정 off 관측은 테스트 title에만 해당하고 운영 title 설정은 미확인이다.

## 연결 후 필요한 최소 시험 경로 — 아직 실행 전

1. 로컬 기존 합성 HTTP 경로: `python -m server.privacy.local_demo`는 localhost 전용이며 실제 provider 연결 검증이 아니다. 연결 코드 수정 시에만 기존 `python -m server.privacy.contract_check`의 관련 범위를 실행한다.
2. 승인된 비운영 환경에서 실제 fresh-auth proof로 request/confirm, 잘못된 owner·낡은 proof 거절, 기본 비활성 확인. 먼저 주입 transport로 accepted/timeout만 검사해 실제 삭제 없이 저장 adapter와 UI 연결을 확인한다.
3. 동일 confirm 동시 요청/응답 유실/서버 재시작에서 한 번만 제출되는지, accepted 재조회와 unknown 재조회가 구분되는지 확인한다. 저장 장애로 unknown 영속화 실패 시 외부 제출 0회여야 한다.
4. unknown일 때 로그아웃/로컬 삭제/접수 성공 안내 0회, accepted일 때 요청 계정·세대가 여전히 같을 때만 로그아웃/정리 1회. owner 없는 legacy·다른 계정·스토어 권한 증거 보존, 늦은 callback 무효화와 No Ads 명시 복원 경계 확인.
5. 별도 승인된 폐기 가능 타이틀 계정에서만 실제 삭제 접수 확인. 재가입은 별도 명시 동작과 실제 API 오류 안내를 검증하며 완료 증거 수집으로 확대하지 않는다. unknown 복구 경로는 임의 재제출이 아닌 별도 예외 절차로 검토한다.

2026-09-21 checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`, 기준 `9cc1af0bc3a0ee42fe388953a7ce97a79cbdec70`. provider 변경 관련 Python 테스트 최초 **15/15 통과**, 실패0/재시도0(기존12+접수전용3). 배포/HTTP/Unity 연결과 위 단계2–5는 미검증이다. Unity6000.0.81f1/CLI1.0.0-beta.8/Android min24 target36 ARM64는 변경 없고 Editor·기기·APK 실행/생성 없음, 새 APK SHA 없음.

검증한 provider 소스 커밋: `10f8e2e` (이후 연결 명세/문서 변경만 추가). JSON 3개 구문 검사와 staged diff 검사 통과. 실제 SDK/API 삭제 호출은 테스트에 포함하지 않았다.
