# LTS SDK meta 변경과 비공개 복원 검증 분리

2026-09-28, e5e5 checkout의 광고 PR `60d8199`에서 Editor를 열기 전 복원 검사가
Git 추적 SDK `.meta` 해시 차이로 중단됐다. 전체 읽기 감사에서는 7개 차이만 발견했고
모두 LTS 전환 커밋과 현재 HEAD, SDK checkout의 바이트에 일치했다. GUID 변경·누락·
비공개 복원 항목의 충돌은 없었다. Editor·광고 fixture는 실행되지 않았다.

원본 `assets-manifest.json`은 기존 승인 원본을 검증하므로 재생성하거나 수정하지 않는다.
별도 [명시적 SDK meta 목록](sdk-meta-migrations.json)에 이 7개만 이전/새 SHA-256·바이트 수·
같은 GUID·전환 커밋 `c8b248c966729a28e3058e73aef0cef622cecb64`로 기록했다.
이 SDK 항목들은 Apache-2.0이며 비공개 에셋·서비스 설정의 값을 추가하지 않는다.

`restore_assets.py`는 기존 해시에 불일치할 때만 이 목록을 검토한다. `git-sdk` `.meta`이며
manifest의 이전 값과 정확히 연결되고 새 바이트/GUID가 일치해야 한다. 추가로 실제 파일,
현재 HEAD blob, index blob, 명시된 전환 커밋의 blob이 같고 전환 커밋이 HEAD의 조상이어야 한다.
tracked라는 이유만으로 허용하지 않는다. 미등록 차이, dirty/staged/untracked 파일,
GUID 변경과 잘못된 전환 근거는 기존 보존 오류로 중단한다.

비공개 복원은 원본 manifest의 엄격한 해시 검사를 그대로 사용한다. 모든 preflight가
끝나기 전에는 복사하지 않으며, 이미 있는 파일을 덮어쓰지 않는다. 출력에는 검토된
`sdkMetaMigrations` 수를 별도로 표시해 실제 원본 해시 일치와 구분한다.

별도 구현 브랜치 `codex/sdk-meta-migration-restore`, 기반
`cd89e9d64aca2cb3fcdc763e1dd021d18feac06f`에서 필요한 Python 검증을 1회 실행했다.
`test_restore_assets.py` **10/10 PASS**: 합성 7개 정상 migration, 미등록/dirty/staged/GUID/commit
거절, 비공개 충돌·preflight 보존, 기존 멱등 복원·경로 탈출·누락 SDK 보호를 확인했다.
실제 checkout의 `restore_assets.py --verify`도 **4561 검증 / 복사0 / migration7**로 완료했다.
광고 PR에서의 첫 preflight 중단 후 수정 도구로 실행한 한 번의 read-only 검사이며
실파일 덮어쓰기·원본 변경·manifest 재생성으로 실패를 숨기지 않았다.

Unity 기준 `6000.3.25f1`, CLI `1.0.0-beta.8`, Android min25/target36/ARM64는 변경하지 않았다.
이 도구 수정에서 Editor·APK·폰 실행은 없고 새 APK SHA-256은 해당 없음이다.
광고 PR #259와 별도 PR로 검토하며, 해당 Unity fixture는 SDK 슬롯 반환 후 별도로 수행한다.
