# 제품 계정 삭제 화면 연결 및 검증

- 작업 checkout: `C:\Users\pc_17\.codex\worktrees\product-deletion-connection\Tamer`
- 작업 브랜치: `codex/product-deletion-connection`
- 검증 시작 시 HEAD/대상 기준 커밋: `8b8d211c25527e2fa872aa1e1cffc264771186d8` (`origin/main`에서 fast-forward)
- 도구 기준: Unity Editor `6000.0.81f1`, Unity CLI `1.0.0-beta.8`, Android min SDK 24 / target SDK 36 / ARM64 (`tools/revival/toolchain.json`)

일반 플레이어 화면은 기존 설정 → **Account & privacy** → 삭제 요청 화면으로 연결되어 있습니다. 캐릭터를 아직 만들지 않은 계정의 `SetCharacter` 화면에도 같은 설정 진입점을 추가했습니다. 해당 화면에서는 삭제 접수 중이거나 로그인 세션이 준비되지 않은 경우 캐릭터 저장 및 방향 조작을 시작하지 않습니다.

삭제 버튼을 눌러도 서버 요청은 전송되지 않으며, 현재 세션과 CloudScript 사전 확인이 끝난 뒤 별도 확인 버튼을 눌렀을 때만 전송됩니다. 사전 확인이 사용 가능한 삭제 서비스를 입증하지 못하면 화면에 비가용 상태를 표시하며 제출하지 않습니다. 접수 응답은 삭제 **완료**를 뜻하지 않습니다. 접수 뒤 자격 증명과 쓰기 권한을 끊고, 전체 화면 안내를 유지하며 **Exit game**으로 앱을 종료하도록 했습니다. 이전 게임 화면의 조작을 허용하지 않으며, 자동 재로그인도 하지 않습니다. PlayFab `AccountDeleted` 로그인 오류는 자동 재시도 대상에서 제외하고 지원 연락처를 표시합니다.

이번 변경은 제품 화면과 실패 상태 연결에 한정됩니다. 시험 타이틀의 PR #218 검증과 운영 Tamer 타이틀의 CloudScript 게시·`ServerDeletePlayer` 활성화는 서로 다릅니다. 운영 타이틀에서 실제 삭제가 가능한 것으로 간주하지 않습니다. 서버 접수 이후 전체 데이터 소거, 제공자 비동기 처리 완료, 스토어 검증, 실제 기기 삭제는 검증하지 않았습니다. 앱을 재시작한 뒤 사용자가 명시적으로 원래 계정 로그인을 시도할 가능성은 남아 있으며, 제공자의 비동기 삭제가 진행 중인 계정을 영구 차단하는 정책은 여기서 확정하지 않았습니다. 소유자별 `PlayerDataBackups` 정리와 No Ads 권한 증거 보존은 별도 출시 차단 이슈 [#220](https://github.com/ChoiDaeYoung-94/Tamer/issues/220)에서 다룹니다. 출처 불명·타인 소유 백업을 임의 삭제하지 않습니다.

검증 전 권한 있는 원본으로 `restore_assets.py`를 실행하여 해시 362개를 확인하고 비공개 에셋 4201개를 복원했습니다. 서비스 설정은 제외했습니다. 제품 계정으로 자동 로그인하거나 삭제 요청을 전송하지 않았습니다. 첫 EditMode 표적 실행은 Editor 테스트의 TextMesh Pro 직접 참조로, 두 번째는 Editor 테스트의 런타임 `AD` 직접 참조로 각각 컴파일 단계에서 중단됐습니다. 두 테스트 코드를 리플렉션 기반으로 수정하고, 사용자로부터 이번 한 번의 세 번째 실행을 명시 승인받았습니다.

2026-09-23 승인된 검증은 checkout `C:\Users\pc_17\.codex\worktrees\product-deletion-connection\Tamer`, 브랜치 `codex/product-deletion-connection`, 코드·문서 대상 커밋 `1c9ad07d8d4ca5a9e33cb29e6b449f249a933b40`의 깨끗한 작업 트리에서 수행했습니다. Unity Editor `6000.0.81f1`, Unity CLI `1.0.0-beta.8`로 변경된 두 테스트와 관련 접수 테스트를 포함한 EditMode 필터를 **한 번** 실행하여 **3/3 통과, 실패 0, 건너뜀 0**을 확인했습니다. 결과 파일 `.revival-local/product-deletion/editmode-third-approved.xml`의 SHA-256은 `129c6cada8338ace741c416a34f057e27114918b09624ccf621183b665727845`입니다. 이전 실패를 이 결과로 덮어쓰지 않습니다. Editor 종료 후 자동 변경된 ProjectSettings만 복원했으며 코드 변경은 없습니다. Android min SDK 24 / target SDK 36 / ARM64 기준은 유지했습니다. 새 APK를 빌드하지 않아 APK SHA-256은 없고, 기기·스토어·운영 타이틀 검증도 미실시입니다.
