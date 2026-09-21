# 삭제 접수 후 계정별 인벤토리 정리

2026-09-21 사용자 승인 범위: PR #203의 해당 계정 로컬 게임 자료를 정상 삭제 접수 이후 정리하고 재시작 접수 복구에도 연결한다. 공용 `AllyMonsters`/`playerEquippedItems`/`LocalItem` 원본, 다른 계정, No Ads, 소유 불명 자료, 보관기간 미확정 백업·구매 증거는 정리 범위에 넣지 않는다.

## 경계

현재 인증 계정의 게임 자료가 준비되면 인벤토리 파일에 무작위 세션 식별자를 원자적으로 저장한다. 같은 세션에서 값이 바뀌어도 식별자는 유지되며, 새 로그인 세션은 내용을 보존하고 식별자만 바꾼다. 삭제 요청의 로컬 복구 기록은 파일을 고르는 계정 해시와 그 세션 식별자를 캡처한다. 새 필드는 조회 권한 메시지 v2에 포함되어 서버에 등록한 검증자 및 기기 키 기반 최종 상태 서명과 결합된다. 두 필드가 없는 기존 기록의 v1 조회 형식은 유지한다.

서버 `receipt_recovery.py`는 등록한 불투명 검증자와 조회·ack 권한의 SHA-256을 대조하므로 서버 스키마나 HTTP 필드를 바꾸지 않는다. v2 기록을 만든 후 이전 클라이언트로 되돌리면 그 클라이언트는 새 HMAC 입력을 재현할 수 없어 복구를 지원하지 않는다. 기록·키를 임의 제거하는 downgrade 우회는 제공하지 않는다. 실제 배포 버전 간 업그레이드/다운그레이드 시험은 하지 않았다.

정상 accepted의 직접 정리와 로그인 전 receipt 정리 모두 파일의 계정·세션을 대조한 뒤 해당 JSON만 삭제한다. 재로그인으로 접수 상태를 복구해도 정리 대상 세션은 새 로그인 값이 아니라 원래 요청에 저장된 값을 사용한다. 새 활성 세션 또는 새 파일 세션과 다르면 보존하고 오류를 표시한다. 메타데이터 없는 옛 receipt에 인벤토리 파일이 남아 있으면 임의로 소유자를 추정하지 않는다.

삭제 대상 파일을 직접 읽고 File.Delete를 호출한다. 진짜 부재만 멱등 성공으로 처리하며, 잘못된 소유자·세션·형식, 읽기·공유 잠금·삭제 접근 오류를 File.Exists의 false로 숨기지 않는다. 디렉터리 자리를 파일이 차지한 경우도 오류다. 진행 파일 정리의 기존 File.Exists 선검사 역시 직접 읽기로 바꾸어 접근 실패를 전파한다.

로컬 정리가 예외를 내면 receipt의 CleanupApplied, 서버 ack, journal 제거, 키 삭제로 진행하지 않는다. 이미 저장한 accepted 증거로 다시 확인할 수 있고 서버 삭제 요청을 재전송하지 않는다. 인벤토리를 지운 직후 중단되었다면 다음 시도에서 그 파일의 부재를 인정하되, 동일 경로에 새 세션 자료가 생겼다면 이를 삭제하지 않는다.

활성 인벤토리 JSON 외 임시 실패 파일·백업·구매 증거의 보관기간이나 파기 정책은 이번 구현에서 확정하지 않는다. 로컬 해시 파일명은 암호화가 아니다. 실제 서버 최종 소거 완료, 운영 배포, 다른 기기 자료 삭제를 뜻하지 않는다.

## 검증 기록

실제 시험 소스 `1925d6be8ad76e7d3eef754caabe2a7343d66e0d`, checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`, branch `codex/inventory-deletion-cleanup`. Editor 기동 직전 HEAD는 `e5217d5fb00ea9f506b5f9a3664dea6e1fe5dfd2`였으며 시험 전 catch 경로의 오류 전파를 명시적으로 정리한 위 source로 재컴파일 completed/failed=false를 확인했다. 첫 시험 전 dirty는 결과 문서 2개와 Editor가 자동으로 비운 alias 설정뿐이었다. checkout/branch 전환은 없었다. 기동 전과 시험 전 preflight를 별도로 보존했다.

Unity `6000.0.81f1` / CLI `1.0.0-beta.8`, Android min24/target36/ARM64 유지. 신규 `Revival_InventoryDeletion` **10/10 통과, 0.35초**, 기존 v1 receipt 재시작 호환 단일 시험 **1/1 통과, 0.35초**. 모두 첫 실행, 실패·skip 0이며 같은 필터 반복이나 전체 회귀는 하지 않았다. 합성 자료·주입 transport로 소유자/세션 정리, 새 세션·legacy 보존, 잠금/잘못된 경로, 실제 receipt/key 유지 및 재시작, 메타데이터 변조 거절, 두 인증 흐름의 원본 정리 세션 전달을 확인했다. 비동기 test_status의 completed와 개별 결과를 확인했다.

비공개 로컬 원본:

- `Logs/revival/inventory-deletion-tests.json`, SHA-256 `57dea34d48fc57669e7fe54b370b5c144315325cb341e1f32df36c7e52c8ac17`
- `Logs/revival/inventory-deletion-v1-tests.json`, SHA-256 `9e85ff60df617bed26b5449db536c2ad8444a89310453d38ea2cb4c5594e451c`
- `inventory-deletion-preflight.json`, `inventory-deletion-test-preflight.json`, console/scenes/hierarchy 기록도 같은 디렉터리에 보존.

console 25개는 모두 Log이며 오류·경고 0. 정상 종료 후 해당 checkout Unity 프로세스 0을 확인하고 alias 원본을 복원했다. 운영 계정 삭제·서버·기기 호출과 APK 생성은 없어 새 APK SHA-256도 없다. Android 파일 삭제·실제 키 저장소·운영 endpoint 연결 및 실제 배포 업그레이드/다운그레이드는 이번 시험에 포함되지 않는다. 최종 문서 커밋은 위 소스 시험 이후 문서만 추가한다.
