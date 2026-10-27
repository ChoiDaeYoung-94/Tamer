# 암호 입력 한 번으로 키 확인과 빌드를 연결하는 도구

2026-10-27, [#445](https://github.com/ChoiDaeYoung-94/Tamer/issues/445)의 변경입니다. **연결 코드와 합성 검증을 준비했으며, 실제 운영 암호·키 사용·서명 AAB·출시는 아직 미검증입니다.** 런타임 게임 코드·광고 승인·기존 계정/저장/No Ads·키는 변경하지 않습니다.

## 해결한 공백과 실행 범위

기존 키 사전검사는 개인키로 난수 challenge를 확인한 뒤 암호를 지웠습니다. 실제 producer는 검토된 plan/source/signing 근거와 암호를 따로 요구하므로, 두 도구를 순서대로 쓰면 사용자가 암호를 다시 입력해야 했습니다.

새 [launcher](../../tools/revival/signing_launcher/README.ko.md)는 검토가 끝난 **approved adult AAB producer**를 대상으로 한 창에서 store/alias 두 암호를 받아 키 확인과 빌드를 이어갑니다. 일반 외부 키 메뉴·privacy-only APK·disabled candidate는 서로 다른 계약이며 자동으로 지원 범위에 넣지 않습니다.

1. exact source/plan·도구·원본 키/현재 인증서·신규 run과 기존 승인 조건을 입력 전에 확인합니다. 부족한 조건이 있으면 창을 열지 않습니다. 관측 자료나 형식상 true만으로 운영 승인을 만들지 않습니다.
2. 검토 파일의 잠근 동일 바이트를 해시 검사와 JSON 해석에 사용하고, 소스·runtime의 경로/파일 식별·ACL·해시를 고정합니다. 읽은 뒤 다른 내용을 다시 여는 경계를 닫았습니다.
3. 같은 입력을 소유 Python child의 stdin으로 전달합니다. 실제 Java worker가 JKS integrity·alias 개인키·현재 DER의 난수 challenge를 검증하고, 성공 뒤 문맥을 다시 확인해야만 exact plan에 귀속한 새 signing receipt를 만듭니다.
4. 두 암호는 소유 producer와 필요한 빌드 자손에만 전달합니다. 상위 launcher·사용자/시스템 환경·명령 인수·파일·로그·clipboard에 암호를 등록하지 않습니다. 읽기 전용 git/pwsh probe에는 제거합니다.
5. Windows가 child를 만들 때부터 private Job에 넣고, 종료·취소·timeout·마감에 소유 자손만 정리합니다. 검토되지 않은 새 실행·자동 재시도·자동 파일 원복은 하지 않습니다.

## 진단과 보호

실패 결과는 고정된 단계와 정규화한 예외 타입으로 남깁니다. 잘못된 키 입력·계약 거부·빌드 실패와 사용자 취소·시간 만료를 구분하며 raw 오류 메시지·경로·stack·네이티브 출력을 그대로 전달하지 않습니다.

`ownedChildStarted`는 연결 프로세스 시작, `producerInvoked`는 producer 함수 진입, `actualBuildExecuted`는 해당 완료 근거를 뜻합니다. 시작 전 거부는 false, 실행 중 종료로 확인하지 못한 값은 null입니다. callback이나 메타데이터 준비를 payload 서명/배포 검증으로 계산하지 않으며 `binaryVerified`와 `distributable`은 false입니다.

Job 종료는 설정 원복을 뜻하지 않습니다. journal·snapshot·actual delta를 보존하고 소유 Editor 종료와 정확한 변경 범위를 확인한 뒤 별도 복구를 검토해야 합니다. char[] 등 지울 수 있는 버퍼는 정리하지만 CLR/JVM/Python/OS의 모든 불변 사본을 완전히 지웠다고 주장하지 않습니다.

Python은 검토된 standalone runtime만 지원합니다. source-only 로더와 실행 전 startup 입력 검사를 사용하며 기존 bytecode·사용자 site·환경에 의해 다른 코드가 실행되지 않도록 범위를 고정합니다. 실제 runtime·새 plan/source 검토가 준비되지 않은 옛 15필드 binding은 이 도구에 재사용하지 않습니다.

## 변경 관련 검증

검증은 임시 합성 키/설정·전용 child를 대상으로 했습니다. 과거 helper 결과를 새 도구의 PASS로 옮기거나 변경 전·후 사례를 한 총계로 더하지 않습니다.

| 범위 | 기록한 결과 |
|---|---|
| 원자 Job·pipe·소유 자손 수명 | 17사례 PASS. 부모 종료/급사·생성 전 Job 거부·Unicode 전달·timeout/취소·별도 sentinel 보존·출력 제한 |
| 최종 변경 경계 | 3사례 PASS. 실패 phase/type, startup 입력 부재 변경 거부, 시간 만료/취소와 미시작 false 구분 |
| Python 연결 논리 | 6시험 PASS. framing·검토 source 로딩·stale plan/source/gate 거부·challenge/문맥 실패 시 receipt와 producer 미실행·귀속 순서·출력 폐기 |
| Editor 소유권 판별 | 합성 metadata 7사례 PASS. 다른 프로젝트/quoted 경로와 실제 Editor·알려진 CLI·소유 불명 상태 구분 |
| bootstrap 교체 방지 | 동일 바이트 해석·검토 source 유지·미검토 파일 교체 거부 확인 |
| 실제 Java worker 실패 연결 | invalid 합성 JKS의 예외 타입 보존, producer/receipt0, 소유 Job 관계 확인 |
| 유효 합성 JKS 성공 연결 | 실제 Java challenge→새 exact receipt→실 signing 계약을 검사하는 fake producer 1회 PASS. parent 환경 불변·실 Unity/실 승인 gate0 |

독립 읽기 검토에서 발견한 manifest 해시 확인 후 재개방, Job 배정 전 부모 종료 공백, 실패 진단 소실을 수정했습니다. 원 실패와 수정 근거를 보존했으며 독립 읽기 리뷰를 독립 실행 시험으로 표현하지 않습니다. 코드·시험별 정확한 source 해시와 로그는 비공개 실행 원장에 연결합니다.

## 실제 실행 전 남은 조건

최종 운영 자료·지역/연령·광고 파트너/신고와 승인 source/resource, 알려진 PlayFab 연결·선택 entry의 버전 입력·PGS, 현재 upload 인증서 역할, 새 actual build HEAD와 exact plan/source 검토를 고정해야 합니다. 해당 metadata를 만드는 실사용 생성기는 이 변경에 포함하지 않습니다.

이후 필요한 직접 입력은 준비된 로컬 창에서 받습니다. 실제 AAB를 만들면 같은 artifact의 서명·manifest/version·resource/native와 원본 복구를 검사하고, 사용자 수동 업로드 후 같은 Play 제공물·필요 기능/정책을 확인합니다. **현재 업로드할 새 운영 AAB는 없습니다.** [전체 복구 잔여](remaining-recovery-work.ko.md)를 유지합니다.
