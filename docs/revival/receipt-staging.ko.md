# 영수증 서버 staging 실행 준비

이 구성은 별도 테스트 앱·PlayFab 타이틀 전용이다. 기본 Unity 구매 경로는 기존 상태이며 서버 검증은 명시적으로 주입한 경우에만 사용한다. 외부 서버 배포, Console 생성, 실제 인증정보 발급이나 구매를 수행하지 않았다. 삭제 서버 `server/privacy`와 진입점·DB를 공유하지 않는다.

## 실행 계약

검증 환경의 전이 의존성까지 재현하려면 `python -m pip install -r server/receipts/requirements.lock`을 사용한다. 잠금 기준은 Windows x64 CPython 3.14이며 배포 호스트 OS에서 설치·테스트를 다시 확인한다.

Python 가상환경에 `server/receipts/requirements.txt`를 설치한다. `staging.env.example`의 변수는 서비스 관리자가 환경으로 제공해야 한다. `.env` 자동 로딩은 없다. 키 내용 대신 기존 권한 있는 secret 파일의 절대 경로를 전달한다. 일반 서비스 계정에는 secret 읽기와 전용 DB 디렉터리 쓰기만 허용하고, 디렉터리/백업은 해당 계정만 접근하도록 OS 권한을 설정한다. Windows에서는 POSIX mode 대신 ACL을 확인한다.

`ENV=staging`, `.iaptest` 패키지, 운영과 다른 타이틀, 1~20개의 명시적 테스트 PlayFab 계정이 필수다. 운영 타이틀 ID는 관리자가 실제 inventory와 대조해야 한다. 문자열이 다르다는 검사만으로 원격 리소스의 격리가 증명되지는 않는다. `PUBLIC_ORIGIN`은 경로 없는 HTTPS origin이며 proxy가 전달하는 Host와 정확히 일치해야 한다.

저장소 루트에서 선택한 가상환경의 Python으로 실행한다.

```sh
python -m server.receipts check
python -m server.receipts migrate
python -m server.receipts check
python -m server.receipts serve
```

`check`는 설정 형식과 DB 준비 상태를 출력하며 `upstreamVerified`는 항상 false다. 형식이 유효하면 DB 미준비 상태도 exit 0이므로 JSON의 `databaseReady`를 확인한다. `serve`는 준비되지 않은 DB에서 시작하지 않는다. 마이그레이션은 명시적으로 실행하며 기존 v0 grants를 보존하고 알 수 없는 스키마/버전은 거부한다.

## HTTPS와 로그

Waitress는 동일 호스트 `127.0.0.1`에만 바인딩한다. `nginx.conf.example`은 Linux nginx의 `http` 블록용 템플릿이며 실제 도메인·인증서·포트를 선택한 뒤 `nginx -t`와 외부 HTTPS 검증이 필요하다. 이번 로컬 검증은 Waitress HTTP 소켓과 신뢰 프록시 헤더 계약을 시험했으며 nginx 실행이나 실제 TLS 인증서 검증은 포함하지 않는다.

프록시는 클라이언트의 전달 헤더를 덮어쓰고, HTTPS 검증 경로만 공개하며 IP별 속도/본문 크기를 제한한다. 프록시와 앱은 같은 신뢰 호스트에서 실행하고 다른 프로세스가 loopback 요청을 위조할 수 있다는 경계를 유지한다. 이 구성은 별도 컨테이너/원격 프록시 주소를 허용하지 않는다. 애플리케이션 warning/error는 고정 이벤트명만 출력하며 예외·인자·stack을 제거한다. 프록시 access/error 원문 로그도 끈다. 장애 진단은 상태 코드·건수·지연 같은 숫자로 수행하고 영수증, 세션 티켓, 키, URL 원문을 수집하지 않는다.

`GET http://127.0.0.1:8088/health/live`는 프로세스 응답, `/health/ready`는 DB 버전·스키마·무결성·쓰기 잠금 획득을 확인한다. readiness는 Google/PlayFab 자격증명·권한·가용성을 보증하지 않는다. 공개 proxy는 health를 노출하지 않는다.

## 백업과 복구

```sh
python -m server.receipts backup --destination /srv/tamer-backups/receipts-001.sqlite
python -m server.receipts restore --source /srv/tamer-backups/receipts-001.sqlite --destination /srv/tamer-restored/receipts.sqlite
```

디렉터리는 사전에 준비한다. SQLite backup API로 일관된 스냅샷을 만들고 무결성을 검사한다. 기존 목적지는 덮어쓰지 않는다. 복구는 새 디렉터리에 생성한 뒤 서비스를 정지하고 `DATA_DIR`을 그 디렉터리로 전환해 `check` 후 시작한다. 원본 DB를 지우지 않는다. 백업에는 계정 ID와 토큰 해시가 있어 원문 티켓이 없어도 접근 제한과 보존 정책이 필요하다. 서비스 전체 로그/secret 디렉터리를 백업 대상으로 묶지 않는다.

## 테스트 리소스와 클라이언트 연결 순서

1. 전용 `.iaptest` Play 앱, 별도 PlayFab 테스트 타이틀, HTTPS 호스트/도메인을 확정한다. 현재 확인한 Console 목록에서는 전용 Tamer 테스트 앱/타이틀을 찾지 못했다. 신규 생성과 배포는 별도 승인 범위다.
2. 관리자가 테스트 앱 범위로 Google API 서비스 계정 권한을 제한한다. 주문 확인에 필요한 Play 결제 API 권한만 검토하고 관리자·출시·다른 앱 권한은 부여하지 않는다. PlayFab 서버 키는 테스트 타이틀에만 둔다. 키를 클라이언트·저장소에 넣지 않는다.
3. 라이선스 테스터와 PlayFab allowlist 계정을 지정하고 테스트 No Ads 상품을 구성한다. allowlist 밖 계정, 테스트 결제가 아닌 구매, 패키지/상품/계정 바인딩 불일치는 서버가 거부한다.
4. 전용 앱 ID와 별도 저장 공간의 격리 로그인/구매 하네스에서 테스트 타이틀만 사용한다. `IapReceiptHttpVerifier`와 현재 세션 공급자를 `IAPManager`에 명시적으로 주입한다. 기본 시작 씬/운영 저장 경로를 바꾸지 않으며 현재 세션·저장 소유자가 바뀌면 지급하지 않는다.
5. 승인된 테스트 계정으로만 테스트 결제, 확인 응답 유실 후 재시도, 앱 재시작, 계정 교체를 실행한다. 서버는 같은 토큰을 같은 계정에 멱등 처리하고 다른 계정의 재사용을 거부한다. Unity의 내구성 있는 로컬 지급 성공 후 Confirm 순서를 유지한다. 환불/권한 회수·운영 전환은 이 staging 구현 범위 밖이다.

## 검증

```sh
python -m unittest discover -s server/receipts/tests -p "test_*.py" -v
python -m unittest discover -s tools/revival -p "test_*.py"
```

첫 명령은 runtime 의존성이 설치된 가상환경에서 실행한다. 두 번째 기존 도구 테스트는 별도 표준 Python으로 실행할 수 있다. 합성 제공자를 사용하는 실제 loopback 서버 요청/재시도/본문 제한/장애 응답과 CLI 마이그레이션·백업을 검증한다. 외부 API 호출과 실제 구매는 하지 않는다.

참고: [Waitress 프록시 설정](https://docs.pylonsproject.org/projects/waitress/en/latest/arguments.html), [nginx proxy](https://nginx.org/en/docs/http/ngx_http_proxy_module.html), [nginx 속도 제한](https://nginx.org/en/docs/http/ngx_http_limit_req_module.html), [Google Play Developer API 시작](https://developers.google.com/android-publisher/getting_started).
