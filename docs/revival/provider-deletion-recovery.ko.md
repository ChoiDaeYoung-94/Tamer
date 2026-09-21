# 일반 인증 삭제 흐름의 접수 복구 연결

`DeletionPresenter.ConfigureService`는 일반 공급자 재인증 gateway만 구성하여 보호된 접수 조회 키와 영속 복구 기록을 연결하지 않았다. 일반 경로가 항상 실패했던 것은 아니다. 접수 응답 유실 후 로그인 증거까지 사용할 수 없으면 같은 요청의 접수를 복구할 수 없었고, 접수 등록을 요구하는 서버 구성에서는 제출이 거절될 수 있었다.

일반 공급자 구성에도 명시적인 `titleId`를 받도록 하고, 기존 `ConfigureSessionService`와 계정·entity 바인딩, 복구 저장소, AndroidKeyStore 접수 클라이언트 및 로그인 전 복구 bootstrap을 공유한다. 기존 일반 구성 API의 호출처는 없었다. 기본 구성은 여전히 null이며 endpoint·title·인증 공급자를 자동 선택하지 않는다. 익명 세션 확인과 공급자 재인증의 증거 방식은 합치지 않는다.

일반 인증 흐름은 첫 요청 전 의도 키를 저장하고, 제출 전에 접수 조회 권한 등록·영속 확인과 제출 시작 기록을 완료한다. 제출 후에는 다시 확인을 눌러도 재제출하지 않는다. 재인증이 가능한 경우 기존 요청 상태를 읽고, 불가능한 경우 기기 보호 키를 사용해 같은 요청의 접수만 조회한다. 접수 확인은 최종 소거 완료 확인이 아니다. 실제 공급자 인증·서버 배포·운영 활성화는 이 변경에 포함하지 않는다.

## 검증

- checkout `C:/Users/pc_17/.codex/worktrees/7819/Tamer`, branch `codex/provider-deletion-recovery`, 기준 main `d631d8405eea5c8bee50048b625470c93a7d2ba4`, 검증 소스 `efb9d59ba0b2e6a08c3ade058a7e94af4150c913`. 실행 직전 미커밋 변경 없음·대상 Editor 없음 확인. 이후 변경은 이 문서뿐이다.
- Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, Android target/min24/target36 기준 유지. 에셋 4,561개 일치, 복사 0.
- EditMode 최소 7건을 1회 실행하여 7/7 통과, 실패·skip 0, CLI exit 0. 일반 인증 정상 접수와 응답 유실 후 재시작 복구, 등록 실패 시 제출 차단 3건, 일반/세션 구성과 manager 교체 검사 2건, 기존 세션 명시 확인·재시작 검사 2건이다. 새 검사는 이전 구현에서 누락된 receipt 등록을 필수로 확인한다. 실제 HTTP 대신 합성 handler, 임시 실제 파일 저장소 및 시험 키를 사용했다.
- 원본 `Logs/revival/provider-recovery-tests.xml` SHA-256 `c515431ac813beee15d2b94bf7cb896375be6fe229d0863361c1c1f08194e78b`. 같은 디렉터리의 `provider-recovery-preflight.json`, `provider-recovery-tests.log`는 비공개로 보존한다.
- 해당 Editor 종료와 ProjectSettings 스냅샷 복원을 확인했다. APK 생성·Android 기기 실행·실제 재인증·운영 HTTP·최종 main 전체 검증은 수행하지 않았으며 새 APK SHA-256은 없다. 이전 PR의 기기 증거를 이 변경의 기기 검증으로 간주하지 않는다.
