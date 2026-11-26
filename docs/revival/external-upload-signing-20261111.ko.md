# 외부 PKCS12 업로드 키와 경로 잠금 — 2026-11-11

실제 키를 Git 밖에서 보관하는 명시적 PKCS12 프로필을 추가합니다. 기존 JKS 프로필은 유지하며, 외부 키가 선택되지 않으면 자동으로 다른 형식이나 경로를 시도하지 않습니다. 이 문서는 구현·합성 검증 범위이며 실제 업로드 키 활성화·운영 서명·배포 완료 보고가 아닙니다.

## 계약과 입력 순서

- 기존 계획schema2/binding1/서명 영수증1/TKP1/JKS는 기존 원본 경로를 유지합니다. 외부 프로필은 계획schema3/binding2/서명 영수증2/TKP2/PKCS12로 구분합니다. `receiptModeVersion`은 광고 hook의 버전이므로 두 프로필 모두1입니다.
- 외부 경로는 canonical Windows 로컬 경로이며 checkout 밖의 명시된 private root 바로 아래 `.p12` 또는 `.pfx` 파일만 허용합니다. UNC·ADS·예약 장치명·모호한 경로와 다른 형식으로의 fallback을 거부합니다. 원본 프로젝트 키는 보존 inventory와 pin에서 제외하지 않습니다.
- 프로필에는 외부 파일 SHA, alias, 업로드 인증서 SHA와 별도의 Play 앱 서명 인증서 SHA, 현재 활성 근거의 경로/SHA를 명시합니다. 업로드 키와 Play 앱 서명 인증서는 달라야 합니다.
- 활성 근거는 현재 Console에서 확인한 `active` 상태와 두 인증서 지문을 대조합니다. 명시 UTC 시각·24시간 이내 관측·이미 유효해진 시각을 요구합니다. 처리 중·예약·미래·오래된 근거를 거부하고, 암호화된 키를 읽기 전에 검사하며 metadata 출력과 실제 실행 전후에도 다시 확인합니다.
- 활성 근거와 키에는 owner/SYSTEM private ACL, regular 단일 링크, ancestor reparse 거부, held-handle identity/SHA 및 비삭제 파일 잠금을 적용합니다. 해당 키를 checkout 또는 공개 증거에 복사하지 않습니다.
- Java worker는 PKCS12 형식을 명시하고 프로세스 안에서 JKS 호환 fallback을 차단합니다. 두 암호가 같아야 하며 정확한 alias/DER와 임의 challenge의 실제 서명을 검증합니다. stdin framing·출력/시간 제한·비공개 non-breakaway Job·소유 프로세스 회수는 유지합니다.
- 실제 challenge와 컨텍스트 재확인 뒤에만 영수증을 만들며, producer가 동일 프로필·경로/SHA·활성 근거와 암호 조건을 다시 검사합니다. 사용자 창은 새 키/alias/인증서를 구분해 표시하도록 준비했습니다. 실제 운영 비밀번호 입력은 직접 조작 범위입니다.

`currentUploadCertificateReviewed`, `existingKeyOwnershipReviewed`와 source-review의 독립 검토 항목은 실제 근거가 있을 때만 true로 기록합니다. 구현이나 합성 시험 통과가 이를 대신하지 않습니다.

## 부모 디렉터리 잠금 수정

최초 C# 합성 검사에서 빈 부모 폴더의 이름 변경이 허용되는 것을 확인했습니다. 별도 Win32 실험에서도 `FILE_READ_ATTRIBUTES`만 요청하거나 `READ_CONTROL`만 더한 핸들은 shareRead만 설정해도 이름 변경을 막지 못했습니다. 파일이 이미 잠긴 사례만 검사하면 빈 디렉터리의 이 차이를 놓칠 수 있습니다.

Python 공통 잠금, metadata bootstrap, C# launcher와 PowerShell bootstrap의 디렉터리 핸들에 `FILE_LIST_DIRECTORY`를 포함하도록 수정했습니다. 빈 폴더/상위 폴더 이름 변경은 sharing violation으로 막고, 정상적인 하위 파일 생성은 허용하는 회귀 검사를 추가했습니다. 다른 프로젝트 파일·공유 ADB/보호 설정은 변경하지 않았습니다.

## 검증 근거와 한계

별도 공개 소스 사본에서 다음 결과를 확인한 뒤 검토용 sparse checkout으로 소스를 옮겼습니다. 운영 키·비밀번호·비공개 에셋·Library는 이 검토본에 복사하지 않았습니다.

검토 checkout은 `D:/codex/worktrees/tamer-signing-profile-1111/Tamer`, 브랜치는 `codex/external-upload-signing-1111`, 기반 HEAD는 `53097aab553033c9ef291a889bf93d4623fc507f`입니다. Windows/Python3.12.14와 기존 Unity6000.3.25f1의 Eclipse Adoptium17.0.18+8을 사용했습니다. Unity Editor·CLI 빌드는 이 검사에서 실행하지 않았으며 새 APK/AAB와 그 SHA는 없습니다. 기존 프로젝트 기준은 CLI1.0.0-beta.8·Android min25/target36입니다. 검토본의 실행 소스는 검사한 사본과 전체 SHA를 대조했고, 문서만 후속 정리했습니다.

| 범위 | 결과 |
| --- | --- |
| Python 프로필·producer·Windows 잠금·binding·bridge | 108/108, 실패/skip0 |
| 실제 C# 프로필/빈 디렉터리 잠금 | 20/20 |
| 실제 합성 JKS/PKCS12 Java worker와 PKCS12 bridge challenge | 16/16 |
| 기존 native 소유권/취소/프로세스 회수 | 20/20 |
| 기존 JKS 실제 challenge→영수증 소비 연결 | 통과, producer는 합성 대역이며 Unity 호출0 |

첫 하드링크 거부 누락, 외부 증거 snapshot 재확인 오류, 합성 ACL/경로 fixture 오류와 빈 부모 폴더 잠금 실패는 원래 결과를 보존한 뒤 변경 조건별로 수정했습니다. 기존 실패를 새 결과로 덮어 통과 처리하지 않았습니다. 소유 Job3은 모두 종료했고 원본 소스/기존 키·JDK/공유 옵션·SDK 해시·공유 ADB의 신원과 새 연결 peer를 대조해 반환했습니다.

### 2026-11-12 Unity와 Android App Bundle 확인

Unity `6000.3.25f1`의 격리 작업본 `53097aab553033c9ef291a889bf93d4623fc507f`에서 검토본 `733e6952f20c643c529dc5ddaffc96df0afeaef8`과 바이트가 같은 템플릿 3개를 컴파일했습니다. 임시 PKCS12 파일로 경로·형식·해시 조건과 Android 빌드 설정 적용 및 복원 14/14를 확인했습니다. 설정 검사 실행에서는 Player 빌드가 0회였습니다.

별도 실행에서 임시 PKCS12 키와 빈 씬으로 Android App Bundle을 한 번 빌드했습니다. 번들 매니페스트는 격리 앱 ID, min25/target36, non-development였고 IL2CPP ARM64 라이브러리를 확인했습니다. 저장소의 Java 검증기로 내용 항목 875개를 전부 읽어 생성 당시 공개 인증서와 서명이 일치하는 것을 확인했습니다. 두 실행 모두 종료 후 원본 파일·Editor 설정과 소유 프로세스를 대조했습니다.

**남은 검증**에는 실제 운영 producer에서의 서명 영수증 소비, Console 활성 업로드 인증서 대조, 새 운영 키와 사용자 직접 암호 입력, 새 버전 AAB, 독립 소스 검토가 포함됩니다. 정책 심사·프로덕션 배포와 16KB 기기 실행은 각 단계에서 확인합니다.

기존 광고/연령/지역 승인과 ABI 계약, min25/target36/승인된 도구 pin, 원본 복구와 최종 심사·프로덕션 경계는 그대로 적용합니다.

## 작업 상태 / 다음 작업 / 사용자 할 일 / 질문

구현과 격리 Unity 설정·임시 키 AAB 서명 확인을 마쳤습니다. 다음은 활성 인증서 확인과 실제 운영 후보 계획입니다. 실제 키를 사용할 때는 현재 활성 인증서·독립 검토·사용자 직접 암호 입력 근거가 필요합니다. 지금 직접 하실 일과 질문은 없습니다.
