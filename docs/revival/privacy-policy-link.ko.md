# 앱 내 개인정보처리방침 연결

2026-09-21, checkout `C:/Users/pc_17/.codex/worktrees/7299/Tamer`, 기준 main `d631d8405eea5c8bee50048b625470c93a7d2ba4` 위 `DeletionSettingsEntry.cs` 3줄 변경으로 확인했다.

설정 → Account & privacy 상단에 `개인정보처리방침` 버튼을 추가했다. 기존 공개 URL `https://github.com/ChoiDaeYoung-94/Tamer#개인정보처리방침`을 `Application.OpenURL`로 연다. 연령 선택·UMP 상태·삭제 서비스 구성과 무관한 버튼이며 기존 Ensure 중복 방지를 따른다. 정책 본문·URL·광고/서버 활성화·meta/GUID는 변경하지 않았다.

Unity 6000.0.81f1 / CLI 1.0.0-beta.8에서 실제 설정 builder로 만든 격리 preview scene UI를 1080×1920으로 렌더하여 한글 표시·배치·활성 버튼을 확인했다. Ensure 2회에 진입점 1개, 실제 정책 버튼 onClick 1회 호출 성공. 검증 스크립트 컴파일·실행 성공, 같은 확인 재시도 0회. Play mode나 운영 로그인·삭제·광고 호출은 하지 않았다. Edit mode preview는 제품의 전체 런타임 상태 표시를 검증한 것이 아니다. 접근 가능한 브라우저 목록에서 목적지 새 탭은 확인하지 못해 OS 브라우저 페이지 표시와 Android 외부 브라우저 전환은 미검증으로 남긴다.

비공개 로컬 증거:

- `Logs/revival/privacy-policy-link-check.json`: SHA-256 `d31c3e9d5215d466e76aecae8b0c048a3b1b89742cce6cb22575070fa88aa501`
- `Logs/revival/privacy-policy-link-ui.png`: SHA-256 `c16aae0b7f76aad67f939dc95c627baabf4d05376ff74e5e0ffb2ac8b1540609`
- `Logs/revival/PrivacyLinkCheck.cs`, `privacy-policy-link-console.json`, `privacy-policy-link-hierarchy.json` 보존.

해당 Editor PID 72768만 정상 종료하고 자동으로 비워진 Android alias 설정을 원래 값으로 복원했다. 새 APK·기기 시험·전체 테스트 반복은 없으며 APK SHA-256도 새로 생성하지 않았다. Android 기준 min24/target36/ARM64는 변경하지 않았다. 삭제 및 데이터 보관 정책의 미확정 사항은 [출시 초안](privacy-policy-release-draft.ko.md)에 남아 있다.
