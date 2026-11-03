# 내부 두 ABI 후보 도구 — 구현·합성 검사

`codex/internal-dual-abi-1103`은 후보31 기록 `166fe46`에서 분리한 다음 내부 후보 준비 브랜치입니다. [승인 범위](candidate32-abi-validation-plan-20261103.ko.md)의 ARM64+x86_64를 명시 프로필 `internal_dual64`로 결합합니다. 새 Android 후보의 실행 성공이나 출시 가능 판정은 아닙니다.

## 동작

- 기본 프로필 `arm64`와 소스 ProjectSettings/도구 기준의 ARM64·min25/target36·Unity6000.3.25f1은 유지합니다. 두 ABI는 빌드 범위 안에서만 설정하고 기존 finally 복구를 거칩니다.
- 계획에 `abiProfileVersion: 1`, 프로필명, 순서까지 정규화한 `expectedAbis`를 포함합니다. 누락·잘못된 타입·ABI 단독/중복/역순은 거부합니다. 예전 계획은 새 도구로 실행할 수 없으며 새 HEAD/전체 소스맵에 결합해 다시 준비해야 합니다. 예전 영수증의 읽기 검증은 실행 승인과 별개입니다.
- `internal_dual64`는 기존 승인 광고 계약과 서명 launcher의 `approved_adult_release` 및 격리 `disabled_candidate` 경로에 허용합니다. `privacy_only_adult`에는 허용하지 않습니다. 프로필명은 내부 검증 의도이며 트랙 배포 권한의 기술적 제한은 아닙니다. `binaryVerified`·`distributable`은 false이며 내부 제공·정책 심사·프로덕션의 별도 승인도 유지합니다.
- producer는 상속 전용 환경값을 제거하고 계획의 버전/프로필/ABI 집합을 전달합니다. C# 훅은 키 접근 전에 프로필을 해석하고 snapshot·각 callback에서 환경/현재 architecture를 재검사합니다. 영수증도 정확한 계약과 일치해야 합니다.
- AAB 검사는 기본 ARM64만, 명시 프로필에서만 ARM64+x86_64를 허용합니다. 각 ELF의64비트·리틀엔디언·machine183/62, 정확한 ABI 집합과 모듈/라이브러리명 대칭을 요구합니다. 누락·다른 ABI·경로/ELF 위장·비대칭을 거부합니다. 기존 서명·게스트 fixture 제외·LOAD/보수적 RELRO 검사를 유지합니다.
- JDK D14/키 가드·정확 pin·전체 소스맵·비공개 증거·설정/원본 복구와 기존31 AAB/Play 분할APK/계정·저장·No Ads를 유지합니다. 로더에 새 helper는 추가하지 않았습니다.

## 실제 검증

- producer·AAB 검사·승인/개인정보 경로 관련62개 통과. 전체 C# 템플릿도 실제 고정 Unity API로 컴파일했으며 Editor·Android 빌드는 실행하지 않았습니다.
- 서명 bridge 합성6개, metadata binding 합성42개 통과. 승인 광고 모드의 두 ABI 계획 연결과 잘못된 계약 거부를 포함합니다.
- 템플릿의 실제 ABI guard 본문을 불활성 타입으로 컴파일·실행한1검사/16사례 통과. 기본/두 ABI, 개인정보 모드 거부, 누락/버전/프로필/순서/중복, snapshot 환경·architecture·목록 변경을 확인했습니다. 실제 Android enum/Editor 동작 실행과는 구분합니다.
- 처음 잘못된 discovery 호출은 필수 `--evidence`가 없어38 setup 오류였으며 제품 실행은 없었습니다. 올바른 진입점 재시도에서 새 계약이 빠진 합성 계획6오류·그 여파1실패 및 비공개 부모 ACL을 상속한 broad-ACL fixture1실패를 확인했습니다. 합성 계획에 계약을 추가하고 원래 광범위 ACL의 합성 경로에서42개 전부 통과했습니다. 실제 보호 설정은 완화하지 않았습니다.
- 실제 키 접근·서명·Unity Editor/Android 실행·운영 SDK 호출·기기 조작0회입니다. 합성 성공을 운영 계정/저장/구매·광고 성공으로 해석하지 않습니다.

## 남은 단계

1. 최종 커밋의 승인된 일회 독립 읽기 검토와 Console 미사용 버전 번호 재확인.
2. 새 HEAD/전체 도구 소스맵의 준비·격리 검증·서명 연결. 소비된 후보31 계획/검토는 재사용하지 않습니다.
3. 사용자 기존 키 암호 직접 입력으로 AAB1회, 외부 복구·서명/두 ABI native/리소스/fixture 제외 검사 후 기존 수동 업로드 절차.
4. 새 내부 제공 별도 승인 후 실제 Play 전달 x86_64/서명/UID/installer·첫 화면 확인. 사용자 인증/나이 직접 조작 뒤 계정·저장·No Ads·연령별 광고/SDK·삭제 검증.
5. 실물 ARM64 시작/16KB·최종 사용자 UI 개선/검토·최종 정책 심사/프로덕션은 계속 별도입니다. [전체 잔여](remaining-recovery-work.ko.md)
