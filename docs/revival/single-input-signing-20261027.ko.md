# 암호 입력 한 번으로 키 확인과 빌드를 연결하는 도구

2026-10-27, [#445](https://github.com/ChoiDaeYoung-94/Tamer/issues/445)의 변경입니다. **연결 코드·합성 검증에 이어10월29일 단일 직접 입력→실키 challenge→운영 후보 AAB1회·기본 정적검사·복구를 확인했습니다. 최종 Play 제공물·운영 기능·정책·출시는 아직 미검증입니다.** 런타임 게임 코드·광고 승인·기존 계정/저장/No Ads·키는 변경하지 않습니다.

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

## 날짜별 준비와 실제 실행

10월28일에는 [성인 후보 소스·불변 private 계약](adult-candidate-source-20261028.ko.md)을 PR #466으로 통합하고 관련 Editor·순수 검증을 마쳤습니다. 현재 launcher→approved producer의 mode·preflight·영수증 경로를 읽기 대조한 범위에서 true 전환에 따른 추가 차단 결함은 발견하지 못했습니다. 고정 CLI의 Windows PE 버전 `1.0.0`을 Editor로 오인하던 판별도 수정했습니다. 이는 실제 metadata·키 사용·운영 빌드 통과가 아니며, 아래 남은 조건의 당시 이력을 대체하지 않습니다.

이후 실제 standalone Python `3.12.14`의 격리 플래그와 소스 선택 규칙에 따른732파일·37,294,817 bytes를 읽기 전용으로 관측하고 SHA-256을 기록했습니다. 전체 재해시·도구 교체·설정 변경·SDK 실행은 하지 않았습니다. 이 시점 관측은 현재 인증서/원키·실 plan·metadata 명령·native identity/SDDL/immutable lock·실키 challenge/producer 검증을 대신하지 않습니다. 실제 단계에서는 검토된 동일 파일과 실행 조건을 확인해야 합니다.

기존 producer의 `installed_tool_inventory`로 명시된 Unity 설치의 필수7파일·144,905,976 bytes에 대해서도 잠근 읽기 핸들의 파일 ID와 SHA-256을 관측했습니다. 앞뒤 경로 metadata는 같았고 원본·설정 쓰기나 SDK 실행은 없습니다. 이는 Android 도구 전체나 공식 배포 checksum의 검증이 아니며, 실제 plan과 실행 시점의 pin 검증은 별도로 필요합니다.

다음은 **PR473 수정 전 후보의 준비 이력**입니다. 10월28일18:29 actual build HEAD `fb13fa4`에서 기존 producer의 비실행 prepare_plan으로 보호7,062파일/복구 snapshot7,060개·private payload9개와 설치도구7파일을 연결했습니다. 원키·CLI는 ID/hash만 포함하고 복제하지 않았으며 actual Assets/journal/claim·암호 UI·SDK 실행은 없습니다. 독립 읽기 검토와 기존 시험/순수 컴파일의 입력 동일성을156개 소스에 연결한 exact6필드 source-review도 준비해 당시 require_source_review 검사가 수용했습니다. 이 계획은 아래 PR473 수정으로 실행 보류됐습니다. 새 시험이나 운영 승인/키 소유권·현재 cert·실제 metadata/native pin·AAB 성공 근거가 아니며 producerPrepared/binaryVerified/distributable=false, code27은 현재 Console 미확인·미예약입니다.

최종 운영 자료·지역/연령·광고 파트너/신고와 승인 source/resource, 현재 upload 인증서 역할, 새 actual build HEAD와 exact plan/source 검토를 고정해야 합니다. 10월27일 비공개 로컬 Title과 선택 approved entry의 명시 버전27을 준비했고 [개발 빌드](baseline-version27-20261027.ko.md)를 확인했지만, 운영 승인·actual plan·실키 사용의 통과 근거는 아닙니다.

이후 PR473의 광고 계정 수명 수정으로 위 fb13fa4 plan/source-review는 실행 보류·superseded했습니다. 원래 자료를 보존하며 새 소스에서 현재 Console/cert와 연결한 exact plan/source-review를 다시 준비해야 합니다. 옛 metadata나 새 예산으로 자동 전용하지 않습니다.

10월29일 source `3c44ab1`의 approved source 사전검사와 도구156파일 SHA 대조는 수용됐고, 실행 관련 추적 내용은 PR473 최종head와 같습니다. 새 plan/source-review·현재 cert·runtime/native pin·실키·AAB는 아직 준비하지 않았습니다. 일반 `python` 명령의3.15.1과 이전 서명 준비에서 관측한 standalone3.12.14는 다른 실행 파일이므로, 실제 준비 시 검토한 절대 runtime 경로와 실행 조건을 명시해야 합니다.

이후 같은 날 정상 Browser Use에서 현재 Console 최대code26·production24제공/26미승인과 upload 공개 인증서를 재확인했습니다. 다운로드한 현재 DER는 기존 검토 공개 DER와 byte 동일하며 개인키 challenge의 통과가 아닙니다. source27은 미예약이며 이후 새 exact plan/source-review와 실제 metadata를 아래 결과로 준비했습니다. 실키·운영 AAB는 남아 있습니다. [현재 관측과 한계](operating-readiness-20261026.ko.md)를 같은 후보 준비에 연결합니다.

10월29일10:17~10:23(KST), exact 후보 HEAD `eba77a66`에서 새 비실행 plan과 기존6필드 source-review를 준비하고 실제 `prepare_binding.py`의 native Collector를 통과했습니다. plan은 보호7,062파일·복구 snapshot7,060개·private payload9개/설치도구7파일을 연결했고 원키·CLI는 복제하지 않았습니다. 소스 검토는 도구156파일과 PR473 실행 내용의 동일성을 기존 검증에 연결했으며 새 시험을 실행하지 않았습니다. 실제 metadata는 runtime732파일을 포함한907개 pin의 파일 식별·ACL·SHA를 확인해 기존25필드 binding과6-source manifest를 비공개 생성했습니다. Collector가 종료하며 소유 핸들을 모두 닫았으므로 실행 시 다시 확인해야 합니다. `metadataPrepared=true`이며 암호 입력·개인키 challenge·claim/producer·Assets staging·SDK/운영 빌드는 미실행, `actualBuildExecuted/binaryVerified/distributable=false`입니다. 현재 Console 최대26과 인증서를 연결했지만 후보27은 미예약이며 새 운영 AAB는 없습니다. 옛 fb13fa4 plan/source-review는 보류·superseded로 보존하고 재사용하거나 예산을 초기화하지 않았습니다.

## 2026-10-29 실제 키·AAB 연결

10월29일10:55~11:10(KST), source `eba77a66`에서 단일 직접 입력→실제 기존 키 challenge→운영 후보27 AAB1회 생성→기본 정적검사→정확 복구를 마쳤습니다. payload822개 전부 현재 upload signer 일치, code27/1.0.5/min25/target36/ARM64·non-debug/non-test와 native6개 LOAD·보수 RELRO 배치를 확인했습니다. 추가 strict RELRO end-modulo 진단3건·실제16KB/Play 제공물·운영 기능/정책은 별도입니다. 원본9개/소유 staging9개와 보호7,062파일의 내용 복구·clean/소유 프로세스0·AAB 해시 보존을 확인했습니다. compiled resource는 receipt에 연결한 뒤 실제 AAB의 named resource 바이트를 독립 추출해 plan과 일치를 확인했습니다. 최종 manifest의 광고 metadata/권한도 읽었으며 runtime load·실제 통신은 미검증입니다. [정확한 파일·검증·한계](operating-candidate27-20261029.ko.md)를 따르며 최종 binaryVerified/distributable/releaseReady=false, 업로드·제출·새 운영 기능 실행은0입니다.

## 후속: 실행 없는 metadata 준비

`prepare_binding.py`는 검토가 끝난 기존 plan/source-review·현재 cert·키 소유권의 독립 근거를 받아 기존25필드 binding과6-source manifest를 조립합니다. 준비기 자체의 source와 exact helper·runtime·파일 식별/ACL/SHA를 확인하며 두 검토 bool은 기본 false입니다. 근거 파일 존재만으로 승인을 만들거나 옛15필드 binding을 변환하지 않습니다. [인수와 경계](../../tools/revival/signing_launcher/README.ko.md)의 placeholder로 실행하지 않습니다.

기존 private 폴더에 두 고정 파일을 CREATE_NEW로 저장합니다. 일부 생성 뒤 실패해도 덮어쓰기·삭제·자동 재시도를 하지 않고 근거를 보존합니다. 승인 source/resource의 false/null은 실제 key/runtime 전수 수집 전에 거절합니다. claim/attempt/signing receipt·암호창·Java challenge·producer·Unity·빌드와 환경/ACL/전역 설정 쓰기는 실행하지 않습니다. bridge 변경은 기존 exact immutable pin 집합 계산의 순수 함수 추출뿐입니다.

검토된6파일을 현재 통합 checkout에 적용한 뒤 준비기40개와 기존 bridge6개의 합성 검증을 각각1회 통과했습니다. source-only/실행 금지·환경 쓰기 trap을 유지했고 실제 운영 자료는 사용하지 않았습니다. 별도 사본에서 native 파일 helper의 identity/SDDL/SHA 동등성1회를 확인한 근거도 보존하며 현재 전체 launcher 재실행으로 계산하지 않습니다. 독립 검토에서 Windows slash 경로의 lock 조회 P2를 수정했고 최종6파일에서 추가 확정 미해결 P1/P2는 없습니다.

후속으로 실제 Windows의 새 합성 폴더에서 owner+SYSTEM ACL의 path/handle 긍정 수용, 실제 Collector bootstrap/save/pin·identity/SDDL/SHA·CREATE_NEW 충돌 시 원본 보존과 정상/예외 핸들 정리를1회26항목으로 확인했습니다. 전체 승인 gate·실제 plan/키/runtime inventory는 실행하지 않았고 합성 출력은 launcher binding으로 거부됩니다. 기존40+6 시험이나 실제 metadata 전체 성공에 합산하지 않습니다.

성공해도 `metadataPrepared=true`만 뜻하며 `actualBuildExecuted`·`binaryVerified`·`distributable`은 false입니다. 이 코드 통합 당시에는 실제 metadata 생성·키 입력·운영 AAB를 실행하지 않았습니다. 이후10월29일의 실제 metadata 준비는 위 날짜별 결과로 구분하며 키 입력·운영 AAB의10월29일 후속 성공은 위 실제 연결 결과로 구분합니다. 준비된 inventory도 실제 실행 시 exact 후보와 다시 대조해야 합니다.

10월29일 필요한 로컬 직접 입력을 접수해 실키 challenge와 같은 exact source의 운영 후보27 AAB 생성·기본 서명/manifest/version/native 검사·정확 원복을 마쳤습니다. **새 운영 AAB는 비공개로 준비돼 있지만 아직 업로드·제출하지 않았습니다.** binary resource 독립 추출은 완료했고 같은 Play 제공물·필요 기능/정책 검증은 별도이며, [전체 복구 잔여](remaining-recovery-work.ko.md)를 유지합니다.
