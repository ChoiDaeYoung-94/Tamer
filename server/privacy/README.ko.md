# 삭제 API 단일 호스트 실행

`python -m server.privacy`는 기존 `session_confirmation.compose`와 `http_app.create_app`을 연결합니다. import만으로 환경 변수를 읽거나 소켓/외부 요청을 만들지 않습니다. 기본 비활성이고 실제 계정이나 PlayFab 자격 증명으로 검증하지 않았습니다.

## 실행 조건

- CPython **3.14.7**: `server/privacy/.python-version`과 CLI에서 정확한 patch를 고정합니다. [공식 릴리스](https://www.python.org/downloads/release/python-3147/).
- Waitress **3.0.2**: 외부 의존성은 이것 하나이며 wheel SHA-256을 고정했습니다. 저장소 루트에서 `python -m pip install --require-hashes -r server/privacy/requirements.txt`로 설치합니다. [공식 패키지](https://pypi.org/project/waitress/3.0.2/), [공식 실행 문서](https://docs.pylonsproject.org/projects/waitress/en/stable/runner.html).
- 단일 호스트, 단일 프로세스/WSGI 스레드, 로컬 영속 디스크와 같은 호스트의 TLS reverse proxy가 필요합니다. 서버는 **127.0.0.1**만 listen합니다. 프록시는 공개 HTTPS를 종료하고 원래 Host를 보존하며, 클라이언트의 전달 헤더를 버린 뒤 `X-Forwarded-Proto: https`를 직접 설정해야 합니다. 외부 HTTP를 HTTPS로 redirect하고 body/query/credential을 로그에 남기지 않습니다.
- 이는 호스트 중립 실행 구성입니다. 특정 관리형 호스트의 ingress가 컨테이너 외부에서 접근하는 경우 이 loopback 구성 그대로 사용할 수 없습니다. 호스트 선택 후 ingress 경계와 검증을 추가해야 합니다. Render 가입/설정/배포 자동화, Azure 자원 생성, CI/CD는 포함하지 않습니다.

## 환경 변수

아래 이름은 모두 `TAMER_DELETION_` 접두사를 붙입니다. 값은 호스트의 비공개 환경 설정으로 주입하고 Git·명령행 인자·로그에 넣지 않습니다. 실제 값을 담은 예제 파일은 만들지 않습니다.

| 이름 | 조건 |
| --- | --- |
| `TITLE_ID` | 명시적인 PlayFab title, 영문/숫자 3~32자 |
| `PLAYFAB_SECRET` | 해당 title의 서버 SecretKey. 빈 값/공백/제어 문자/비ASCII/4096자 초과 거부. 실제 권한 유효성은 오프라인에서 확인하지 않음 |
| `PUBLIC_ORIGIN` | `https://호스트/` 형식. 끝 `/` 필수, 사용자 정보·경로·query·fragment 금지 |
| `DATA_DIR` | 이미 존재하는 절대 디렉터리. symlink/우회 경로·알려진 임시 디렉터리 거부. DB 이름은 `deletion.sqlite` 고정 |
| `STORAGE_KIND` | `local-persistent` 명시. 이는 운영자의 저장소 선언이며 실제 마운트 영속성을 자동으로 증명하지 않음 |
| `POLICY_REVISION`, `SCOPE` | 비어 있지 않은 revision(최대 128자), scope는 `title`만 허용 |
| `RETENTION_PLAN` | 합의된 보관계획 문자열들의 비어 있지 않은 JSON 배열. 임의 기본 보관계획 없음 |
| `RECEIPT_TTL_SECONDS` | 합의된 영수증 유효기간을 초로 명시, 기존 프로토콜 범위 1~2592000. 기본값 없음; DB 보관/삭제 정책과 다른 값 |
| `PORT` | localhost 수신 포트 1024~65535 |
| `ENABLED` | 정확히 `true` 또는 `false`, 누락 시 `false` |
| `RETENTION_APPROVED`, `REJOIN_APPROVED`, `SESSION_CONFIRMATION_ENABLED` | 각각 정확히 `true`/`false`, 누락 시 `false`. 활성화하려면 모두 `true` 필요 |

활성/비활성 모두 필수 설정을 검증합니다. 비활성 서비스는 POST를 거부하고 upstream을 호출하지 않습니다. `/health/live`는 프로세스 응답만 뜻하며 DB·PlayFab 권한·삭제 서비스 활성화를 보증하지 않습니다. HTTPS와 Host를 검사한 `/v1/deletion/config`에서 활성 여부를 읽습니다.

## 최초 생성과 재시작

저장소 루트에서 위 설정을 주입한 **하나의 프로세스만** 실행합니다.

```text
python -m server.privacy init-db
python -m server.privacy check
python -m server.privacy serve
```

`init-db`는 최초 한 번만 실행합니다. 기존 파일은 비어 있거나 손상되어 있어도 덮어쓰지 않습니다. 실패한 생성의 부분 파일도 자동 삭제하지 않습니다. `check`/`serve`는 기존 DB가 없거나 SQLite quick_check가 실패하거나 기대하는 테이블·열 이름/순서가 다르면 중단합니다. 타입·제약조건까지 스키마 전체를 비교하는 검사는 아닙니다. 자동 migration·repair·파일 교체는 하지 않습니다. 기존 행과 추가 테이블은 보존합니다. 정상 CLI 시작 시 기존 테이블 정의는 그대로 유지됩니다.

같은 디렉터리의 `deletion.lock`에 OS 잠금을 잡아 중복 실행을 거부합니다. 잠금 파일은 종료 후에도 남고 정상 종료/프로세스 종료 시 잠금만 해제됩니다. 실행 중 DB·lock 파일·디렉터리 교체나 삭제를 하지 않습니다. 수평 확장, 네트워크 공유 파일시스템, 여러 title의 같은 DB 공유는 지원하지 않습니다. [SQLite 네트워크 파일 주의사항](https://www.sqlite.org/useovernet.html).

nonce/proof/삭제 요청/접수 상태/영수증 verifier를 같은 영속 DB에 유지해야 합니다. 배포나 재시작 때 DB를 새로 만들면 안 됩니다. DB 일관성을 보장하는 백업과 복구 검증은 실제 호스트에서 별도로 준비해야 하며 단순 파일 복사나 디스크 snapshot만으로 검증 완료로 간주하지 않습니다. [SQLite Backup API](https://www.sqlite.org/backup.html). 자동 보관기간 만료 삭제·최종 완료 worker는 추가하지 않았습니다.

## 검증과 한계

구현 기준 main: `27b3209927940635878bd0f4bd803f3caf5e5ad0`. 검증 소스: `3c9c2201caa48103f485b1027e0561b617dde1f9`에 들어간 코드·테스트와 동일한 커밋 직전 작업 내용입니다. checkout: `C:\Users\pc_17\.codex\worktrees\7299\Tamer`, branch: `codex/privacy-wsgi-bootstrap`. Windows x64의 독립 CPython 3.14.7 + Waitress 3.0.2에서 다음을 실행했습니다.

```text
python -m unittest tools.revival.test_deletion_runtime -v
```

추가 테스트 6/6 통과(0.472초): 기본 비활성, 활성 fake 접수 흐름, 설정 오류, DB 부재/손상/기대 테이블 누락 시 원본 보존, 앱 재구성 후 기존 DB/영수증 유지와 중복 submit 없음, HTTPS/Host·중복 프로세스 잠금, CLI/로그 secret 비노출. 첫 실행은 테스트에서 환경 변수를 비우면서 Windows 임시 경로 캐시가 cwd를 가리킨 fixture 문제로 5개 실패했고 fixture 수정 후 두 번째 실행에서 통과했습니다. 실제 upstream은 차단하고 fake transport만 사용했습니다. 출력은 작업 도구 응답으로 확인했고 별도 원시 로그 파일은 저장하지 않았습니다. 이후 변경은 이 검증 설명 보완뿐입니다.

Unity/Android/CLI 변경 및 실행 없음, APK 생성 없음. Linux 잠금 분기, 실제 TLS 프록시/소켓 배포, 재부팅·디스크 장애·백업 복구, 실 PlayFab 권한/정책 설정은 미검증입니다. Python과 WSGI 버전 pin은 검증 기준이며 이후 보안 patch 검토를 대체하지 않습니다.

`GET /privacy/deletion`은 여전히 미제공 안내 페이지입니다. 실제 외부 요청 페이지와 nativePGS 본인확인은 구현하지 않았습니다. 현재 어댑터는 단일 AndroidDevice 또는 CustomId 익명 계정만 수용합니다. 성공한 접수는 `accepted`이며 최종 삭제 완료로 바꾸지 않습니다.
