# 최신 복구 상태와 남은 순서 — 2026-10-18

**시험용 29번이 10월 18일 14:07(KST) 내부 테스터에게 제공됐습니다. Play 원본 AAB의 동일성과 범용 APK의 앱 서명·manifest·ARM64 payload·기본 LOAD/ZIP 검사를 확인했습니다. 실제 기기 업데이트·계정 재인증·구매 복원과 운영 출시는 미검증입니다.** `releaseReady=false`를 유지합니다. [29번 Play 제공·검증 근거](iap-v29-play-verification.ko.md)

공개 문서의 main 대조 기준은 `13e1d5b0b06dfd273a4b1ce7fb0b16f7ce1ff79a`입니다. 시험29의 빌드 소스는 계정 복구 [초안 PR #378](https://github.com/ChoiDaeYoung-94/Tamer/pull/378)의 `ac6eec24ee9a50bfaef7364f0737818d51aa9c36`입니다. 후속 후보 `be6b5ee6f6b01c512dbf84b89354044207b1104f`는 씬 저장 후속 `fb14df8`에 main의 검사 도구·문서 3파일만 추가했으며 `Assets/Packages/ProjectSettings` 차이는 없습니다. **후속 수정 #381·#383은 시험29에 포함되지 않았고, 새 AAB30은 아직 빌드·전달하지 않았습니다.**

| 항목 | 확인된 결과 | 남은 작업·제한 |
|---|---|---|
| Unity·Android | Unity `6000.3.25f1` / revision `e1dba0a9aba4`, min25/target36/ARM64, CLI `1.0.0-beta.8` 기준 | main 문서·도구 반영과 게임 코드 통합·실행 검증을 구분합니다. 이전 Unity의 결과를 현재 후보로 전용하지 않습니다. |
| 통합 코드·병합 상태 | 문서·도구 [#379](https://github.com/ChoiDaeYoung-94/Tamer/pull/379)·[#380](https://github.com/ChoiDaeYoung-94/Tamer/pull/380)·[#382](https://github.com/ChoiDaeYoung-94/Tamer/pull/382)는 main에 Squash 병합 | 게임 코드 #363·#366·#368·#369·#374·#378·#381·#383은 모두 Draft/미병합입니다. 실제 상점·장비/HUD·전투·씬·삭제 접수 UI 확인이 남습니다. |
| 기존 통합 회귀 | 최초 9클래스 250개 중 238통과/12실패, 수정 `9877574`의 영향 3클래스 139/139통과/skip0, 최초 실패12개 포함 | 다른 클래스의 과거 통과를 최신 head 전체 재실행으로 세지 않습니다. |
| GLES3 셰이더 | Core17.3.0의 GLES3 FSR 경로를 기존 FP32 구현으로 전환한 로컬 패키지. source `4dac486` 개발 APK에서 FinalPost 48변형 새 컴파일/캐시0/오류0 | 실제 기기 표시·FP32 비용/정밀도·다른 그래픽 API 미검증입니다. 패키지 전체 업그레이드 결과가 아닙니다. |
| 시험 AAB29·Play APK | source `ac6eec2`, AAB 73,997,176bytes와 Play 원본 SHA-256 동일. 범용 APK 78,643,333bytes, 시험ID/29/min25target36/ARM64/non-debug, 앱 서명과 Source Stamp 역할 분리, 6native 전체 payload 일치·LOAD/ZIP PASS | 최초 Source Stamp 파서 실패1회 후 역할 분리 수정으로1회 성공. 해시가 귀속된 native 보고서를 재사용했으며 새 빌드가 아닙니다. 실제 기기·split은 미검증입니다. |
| Store 실패 원인·권한 보존 | 기존 영수증은 과거 시험 계정에 귀속되고 현재 설치별 로그인 계정과 달랐음을 확인했습니다. 기존 계정·저장·No Ads 권한을 보존했고 재구매·권한 이동·강제 삭제는0입니다. | PR378의 재인증·원본 보존·새 프로필·gate는 `ac6eec2`에서 합성85 PASS 및 시험29 빌드까지 확인했습니다. 실제 폰 재인증·단일 복원·No Ads 내구 저장/재시작은 미검증입니다. |
| IAP 후속 #381 | 중복·SDK 자동 retry·지연 주문 gate 수정 `22aeb3a`. 검증 head `1d7a265`에서 `RevivalIAPOrderTests` 28/28 PASS, 14:18:58~14:19:32(KST),1회/실패0 | #378 이후 수정이며 시험29에는 없습니다. Android buildTarget의 EditMode 합성이며 실제 Store·계정·Player 결과가 아닙니다. |
| 캐릭터 저장 후속 #383 | 저장 후 원래 세션 귀속 수정·검증 head `fb14df8`에서 `RevivalUILifecycleTests` 8/8 PASS,14:41:58~14:42:20(KST),1회/실패0 | 새 UnityTest 2메서드 안16분기는16개 독립 NUnit 결과가 아닙니다. 실제 폰 저장·씬·BGM·UI는 미검증입니다. 두 후속 실행 모두 Unity6000.3.25f1/실계정API0, 보호 설정70개 중 변경3개 원바이트 복구/Git clean입니다. |
| 서명·JDK·도구 | 기존 시험키·현재 사용자 DPAPI·embedded JDK 복구 이력 유지. #379 도구 회귀7개, #382 합성11개와 운영26 실제 SDK 정적 검사1회 확인 | 새 키 생성·재설정 없음. 도구 검증을 정책·운영 출시 승인으로 확대하지 않습니다. 이전 PowerShell5.1 실행정책 차단 이력도 미검증으로 보존합니다. |
| 호스트·중단 이력 | BIOS M.H0/Intel Default Settings/microcode0x137 확인 뒤 최소 검증·APK·AAB 실행 이력 보존 | [#358](https://github.com/ChoiDaeYoung-94/Tamer/issues/358)의 장기 안정성·기존 재부팅 원인은 미확정입니다. 제어 검사 `editor_status` 2회 timeout 경로와 의존 live 작업은 STOP이며 충돌 원인을 확정하지 않습니다. |
| 16KB·RELRO | 시험29의 6native ELF LOAD 및 Play 범용 APK 기본 LOAD/ZIP PASS | 별도 RELRO 끝 정렬3개 미충족, strict 재실행0. 실제16KB 실행·기기별 Play split/ZIP 정렬은 미검증입니다. Console 표시·기본 정적 PASS로 대체하지 않습니다. |
| 운영26 원본·출처 | Console 원본 AAB/범용 APK 보존, 기대 upload/app 서명·6native 동일성·LOAD/ZIP 확인. header는 `6000.0.81f1`, min24/target36/ARM64 | source commit 미확정인 과거 산출물입니다. 현재 Unity6.3/min25 후보가 아니며 실제 기기·split·16KB·정책 통과도 미검증입니다. |
| 운영 Console·정책 | 읽기 확인: 가족 광고 형식 거부, 미제출2변경(운영26 전체 출시·9세 이상), Data Safety의 2024-09-30 NO 신고. 제출·수정0 | 실제 제공 버전/SDK 처리·Data Safety/공개 방침/Console 신고 대조가 남습니다. 연령만 제출하는 것으로 오해해 일괄 전송하지 않습니다. |
| 광고·Analytics·삭제 | 기존 rewarded·영구 No Ads,9세 이상 성인 포함·미성년 광고 차단·PlayFab 수집 유지. 앱 밖 삭제는 기존 문의 메일로 운영자 직접2주 이내 처리 목표 | 운영 광고 승인 gate=false/binding=null, 지역·거절/철회·연령·복원 확인이 남습니다. Analytics flag 변경0이며 legacy off만으로 IAP 데이터 처리 off를 확정할 수 없습니다. 5초 닫기 조사는 보류, 공급자 로그·백업 전부 소거 보증도 아닙니다. |
| 출시·정리 | 기존 게임 소개·영상·이미지·Play 링크·개인정보방침·외부 삭제 안내 유지 | 가입 UI 시각검증9차 STOP, 실기기·최종 출시 AAB·정책 심사·결산·시험 자원 정리·마지막 `Tamer`→`tamer` 변경이 남습니다. CI/CD·App Center는 보류/보존합니다. |

다음 우선순위는 **총괄의 실행 소유권·실제 승인 gate에 따른 시험29의 폰 업데이트·기존 계정 재인증·단일 복원 확인 → 후속 코드가 포함된 정확한 후보의 기능 확인 → 정책·최종 출시 검증**입니다. 아직 없는 AAB30의 제공이나 후속 코드를29에서 검증했다고 기록하지 않습니다.

같은 시험2회 실패 시 해당 경로와 종속 작업을 중단하고 승인 전3차·유사조건 우회 시험을 하지 않습니다. 기존 영수증 재시도·strict RELRO·가입 UI 중단 이력을 유지하며 변경 없는 회귀·빌드를 기록만을 위해 반복하지 않습니다. 완료 채팅은 인계 접수 뒤 정리하고 총괄은 유지합니다. 과거75.x%는 당시 추정값이며 고정 산정표 없이 PR 수·문서량으로 새 완료율을 만들지 않습니다.

<details>
<summary>2026-10-17 당시 기록 — 아래의 현재·대기 표현은 당시 시점입니다</summary>

## 2026-10-17 기록

**시험용28번이 업로드되어 내부 테스터에게 제공됐습니다. Play가 생성한 범용 APK도 서명·manifest·ARM64 LOAD/ZIP 검사를 통과했습니다. 실제 기기 업데이트·구매 복원·운영 출시는 아직 완료하지 않았습니다.** `releaseReady=false`를 유지합니다. [28번 Play 제공·산출물 검증 근거](iap-v28-play-verification.ko.md)

현재 main 대조 기준은 `ad030c58ff492f8683f755063cd07f671b4eb7b7`입니다. 게임 코드 통합 [초안 PR #369](https://github.com/ChoiDaeYoung-94/Tamer/pull/369)의 head는 `a2e17a335696a01a6d0365caeebf8bbded7a053f`이며, 그 위의 셰이더 수정 [초안 PR #374](https://github.com/ChoiDaeYoung-94/Tamer/pull/374) 후보 `3d77ff6697c0385350a72acdd02caf4d25e5d5f7`에서 이번 AAB를 만들었습니다. 이 코드 후보들은 아직 main에 병합하지 않았습니다.

| 항목 | 완료 근거 | 남은 작업·제한 |
|---|---|---|
| Unity·Android | Unity6000.3.25f1/revision e1dba0a9aba4, min25/target36/ARM64 | 이전 Unity 결과와 현재 후보의 검증을 구분합니다. |
| 통합 코드 | 최초9클래스250개 중238통과/12실패. 수정9877574에서 영향3클래스139/139통과/skip0, 최초실패12개 모두 포함 | 나머지 클래스의 과거 통과를 새 head 전체 재실행으로 표현하지 않습니다. 실제 상점·장비/HUD·전투·씬·삭제 접수 UI가 남습니다. |
| GLES3 셰이더 | Core17.3.0의 GLES3 FSR 경로를 기존FP32 구현으로 전환한 로컬 패키지. 개발APK 빌드source4dac486에서 FinalPost48변형 새 컴파일/캐시0/오류0 | 실제 기기 표시·FP32비용/정밀도·다른그래픽API 미검증. 패키지 버전 전체 업그레이드가 아닙니다. 공개 근거는 PR374에 있습니다. |
| 시험 AAB28 | source3d77ff6/빌드성공/셰이더오류0. 73,955,742bytes/SHA-256 `b637dc98c237c15bfb3db523ac54347267e6897b87679bffbcb39ba9ad0f96c8`. Play 원본 다운로드와 동일성 확인, 10월17일16:18 내부 제공 완료 | 시험 앱ID/non-debug/min25target36/ARM64/전용시험서명/payload601개/ELF LOAD6개 확인. Play 범용APK 서명·LOAD/ZIP·native원본일치도 확인. 실제 기기 업데이트·로그인·주문 진단은 남습니다. |
| 원본 복구 | 새 보호manifest·실제delta 독립 검토 후 변경된 원본5개/보조6개를 사전 snapshot 원바이트로 복구. 원본9개+보조42개 전체 확인, 임시2개 제거, Git clean | 빌드가 교체한 파일ID8개는 과거ID로 복원한 것이 아닙니다. snapshot 파일 자체의 과거ID 동일성은 별도로 증명하지 않았습니다. |
| Store 구매·복원 | v27의 Play 설치·격리 시험 로그인 이력, PR352 진단 코드 포함 | 새v28 설치 후 실제 오류 코드·주문 귀속·No Ads 지급·구매 확정 확인. 이전 WaitingForVerification의 원인을 단정하지 않습니다. |
| 서명·JDK·도구 | 기존 시험키/현재사용자DPAPI·embeddedJDK 복구. 이번 Console 업로드 인증서와 기존DER 일치. [PR375](https://github.com/ChoiDaeYoung-94/Tamer/pull/375) Editor/CLI 판별 수정 병합, PowerShell7 테스트5개 통과 | 키 생성/재설정 없음. 운영 서명 결과와 구분. PowerShell5.1은 실행정책으로 시작 차단되어 미검증이며 우회하지 않았습니다. |
| 호스트 | BIOS M.H0/Intel Default Settings/microcode0x137 확인 후 최소검증·개발APK·시험AAB 실행. 이번 AAB 구간 WHEA/컴파일러 충돌 관측0 | [#358](https://github.com/ChoiDaeYoung-94/Tamer/issues/358) 장기안정성·기존 재부팅 원인은 미확정. 단일 빌드 성공으로 하드웨어 문제 해결을 선언하지 않습니다. |
| 16KB·RELRO | 이번 AAB6개 ELF LOAD와 Play 생성 범용APK의 기본LOAD/ZIP 통과 | 범용APK의 RELRO 끝 정렬3개 미충족, strict 중단시험 재개0. 실제16KB 실행·기기별Play split/ZIP정렬은 미검증이며 범용APK 결과와 구분합니다. |
| 광고·정책·삭제 | 기존 rewarded·영구 No Ads 유지,9세 이상 성인 포함, 미성년 광고 차단, PlayFab 수집 유지. 앱 밖 삭제는 기존문의메일로 운영자 직접2주 이내 처리 목표 | 운영광고 승인게이트false/bindingnull,지역·거절/철회·연령·복원 확인, 실제제공버전과DataSafety/공개방침/Console신고 대조. 5초닫기 조사는 보류이며 정책충족 선언 아님. 공급자 로그/백업 전부소거 보증 아님. |
| 운영 Console | 이번읽기에서도 가족광고형식 거부와 미제출2개(프로덕션26전체출시+9세이상 변경)를 확인 | 운영26 원본AAB/source/hash/서명 귀속 미확정. 연령만 제출하는 것으로 오해해 일괄 전송하지 않습니다. |
| 출시·정리 | README 게임소개·실제화면·기존영상·Play링크·개인정보방침 유지 | 가입UI 시각검증9차STOP,실기기/최종출시AAB·정책심사·결산·시험자원정리·마지막 Tamer→tamer 변경. CI/CD·App Center는 보류/보존합니다. |

다음 순서는 **시험28의 실제 기기 Play 업데이트·첫 화면 확인 → 주문 진단 → 기능·정책·최종 출시 검증**입니다. 같은 시험2회 실패 시3차와 유사조건 우회시험을 하지 않으며, 변경 없는 회귀·빌드를 기록만을 위해 반복하지 않습니다. 완료 채팅은 인계 후 정리하고 총괄은 유지합니다. 이전75.x%는 과거 추정이며 고정 산정표 없이 PR 수나 문서량으로 새 완료율을 만들지 않습니다.

</details>

<details>
<summary>2026-10-16 당시 기록 — 아래의 현재·대기 표현은 당시 시점입니다</summary>

## 2026-10-16 기록

10월 15일 실패한 진단 빌드의 소스 기준은 `f7a051b48ca7f0fcd6c957ead29ebd6ebedaaf31`입니다. [PR #352](https://github.com/ChoiDaeYoung-94/Tamer/pull/352)의 구매 검증 진단 수정과 [PR #354](https://github.com/ChoiDaeYoung-94/Tamer/pull/354)의 기존 시험 서명 백업 복구 도구는 병합된 상태입니다. 새 PC에서 실제 시험 서명 복구와 JDK 복구를 완료했지만, 신규 진단 AAB 빌드는 컴파일러 충돌로 실패했습니다. 10월 16일에는 [PR #362](https://github.com/ChoiDaeYoung-94/Tamer/pull/362)로 정책·인벤토리 문서 6개의 과거 관측과 현재 구현을 구분했습니다. 이 문서 정리까지 병합한 소스 기준은 `1e34718f25e3814742cee4c5c332b6551c68c55a`이며, 새 빌드·기기·정책 검증 결과는 아닙니다.

**현재 대조 기준:** main `7f05f39326c7ffb2bf2bfd25653ed202e8ba12d5`, 검증 전 통합 후보 [초안 PR #369](https://github.com/ChoiDaeYoung-94/Tamer/pull/369) `4a99da7c4275a240e5bed84448eeda083c45204b`입니다. 위 빌드·초기 문서 기준은 당시 이력입니다. 통합 후보에는 동료 구매 [#363](https://github.com/ChoiDaeYoung-94/Tamer/pull/363) `6462d7f`, 장비 구매·삭제 접수 복구 경계 [#366](https://github.com/ChoiDaeYoung-94/Tamer/pull/366) `64d1fa2`, 전투·씬 요청 수명 [#368](https://github.com/ChoiDaeYoung-94/Tamer/pull/368) `fe8c54b`를 함께 보존했습니다. 이 네 코드 PR은 모두 미병합이며 컴파일·실행 검증 전입니다. 충돌 부분의 회귀 소스 합집합과 서비스 파일 대조는 읽기 검토이며 실행 PASS가 아닙니다. 별도 도구 수정 [PR #365](https://github.com/ChoiDaeYoung-94/Tamer/pull/365)는 병합해 기본 검증 스크립트에 Python 실행 파일 지정 옵션을 추가했습니다.

[PR #370](https://github.com/ChoiDaeYoung-94/Tamer/pull/370)으로 필요한 검증·필수 검사 통과 후 자동 Squash merge와 완료 정리 기준을 지침에 반영했습니다. 과거 로컬 브랜치 56개와 완료 문서 브랜치 1개를 정리했으며 검증 전·사용 중·미반영 자료는 보존했습니다. 지침·브랜치 정리를 출시 완료나 실행 보류 해제로 해석하지 않습니다.

코드 통합과 현재 설치 앱의 동작·실제 스토어 검증은 별도로 기록하며, 아래 날짜별 문서의 미실행·실패 기록은 당시 이력으로 보존합니다. BIOS 점검을 기다리는 동안에도 실행과 독립적인 코드 감사·필요한 수정·문서 준비는 계속합니다.

| 항목 | 현재 확인 범위 | 남은 작업·제한 |
|---|---|---|
| Unity·Android 기준 | Unity `6000.3.25f1`, revision `e1dba0a9aba4`, Android min25·target36·ARM64 | 이전 Unity의 PASS를 현재 최종 후보의 검증으로 대체하지 않습니다. |
| 시험 앱 v27 | 내부 테스트 출시, 실제 Play 설치·격리 시험 로그인 확인 | 운영 Google 로그인·게임 저장·전체 게임 진입·구매 복원 완료의 근거는 아닙니다. |
| Store 주문 검증 | 시험 화면의 `WaitingForVerification` 관측 | 실제 오류 코드·주문 귀속·No Ads 지급·구매 확정은 미확인입니다. 원인을 `ReceiptAlreadyUsed`로 단정하지 않습니다. |
| 구매 진단 코드 | PR #352 병합. `6c413127` 관련 Win64 EditMode 44/44 PASS, 후속 `61798607` 변경 회귀 2/2 PASS 및 독립 읽기 검토 완료 | 최신 수정은 아직 설치 앱에 반영되지 않았습니다. 첫 컴파일 AccessViolation 이후 프로세스 한정 `DOTNET_PROCESSOR_COUNT=1`로 통과했으며 환경 근본 원인 해소를 주장하지 않습니다. |
| 기존 시험 키 복구 | PR #354 병합 후 실제 백업 복구·현재 Windows DPAPI·기존 키의 일회성 challenge 서명/공개키 대조·현재 Console 업로드 인증서 일치 확인. [#353](https://github.com/ChoiDaeYoung-94/Tamer/issues/353) 완료 | 원 metadata·receipt·키를 보존했고 새 키를 만들지 않았습니다. 시험 키의 결과이며 운영 서명키나 새 AAB의 서명·업로드 완료를 뜻하지 않습니다. |
| 새 PC Android 환경 | 공식 고정 JDK ZIP 해시를 검증해 누락된 embedded OpenJDK만 복구. 실제 Java 17.0.18+8 실행·SDK·NDK·API36 파일 확인 | 과거 Android 부모 모듈 설치 실패 이력은 보존합니다. 이후 단일 JDK 복구는 성공했으며 현재 관리자 승인·백업 암호 재입력 대기는 없습니다. |
| 신규 진단 v28·호스트 | 10월 15일 Console의 최신 업로드/내부 배포 v27 확인 후 v28 빌드 1회 실행. 10월 16일에도 BIOS M.50·microcode 0x11D 확인 | clang frontend·ShaderCompiler 충돌로 실패, AAB 생성·검증·업로드 없음. 같은 시간대 프로세서 하드웨어 교정 오류도 관측해 추가 컴파일·실행 검증은 보류합니다. 원인/재개 조건은 [#358](https://github.com/ChoiDaeYoung-94/Tamer/issues/358)이며 CPU 고장이나 단독 원인으로 확정하지 않습니다. BIOS·펌웨어 변경은 아직 없습니다. |
| 실패 빌드 정리 | 독립 검토한 변경분만 복구하고 시험용 생성 설정 정리. 에셋 4,561개 사후 검사 통과, 복사 0개 | 보호한 9개 원본은 사전 바이트로, 별도 자동 변경 9개는 고정 HEAD의 필터 표현으로 복구했습니다. 후자는 사전 원바이트 동일성 증명과 구분합니다. 실패 자료·Library·기존 키는 보존합니다. |
| 인게임 Gold 구매 | [#361](https://github.com/ChoiDaeYoung-94/Tamer/issues/361) 추적. 초안 PR #363은 동료 구매의 Gold·목록·pending revision을 함께 저장합니다. 후속 초안 PR #366은 Gold·거래 기록 확정 → 장비 파일 반영 → 완료 기록의 복구 경로를 추가했습니다. | 소스 작성·독립 읽기와 지적 보완까지 완료했으나 컴파일·테스트·실제 상점 UI는 미검증입니다. 로컬 여러 파일 복구 후보이며 원격/다기기 거래 원자성·재설치 복원을 보장하지 않습니다. Play No Ads 구매 복원과 다른 작업입니다. |
| 삭제 접수 전용 복구 후보 | #366은 소유자·접수 기록을 확인할 수 있지만 장비 거래 본문이 손상된 경우 일반 게임 서비스를 시작하지 않고 기존 삭제 접수 복구만 허용하는 ReceiptOnly 경로를 포함합니다. | 소스 검토만 완료했습니다. 실제 시작 화면·로그인 차단·기기 저장 정리와 공급자 삭제 완료는 미검증입니다. 새로운 삭제 서비스나 전체 자료 소거 보장이 아닙니다. |
| 전투·씬 요청 수명 후보 | #368은 선택된 공격 대상의 세션·활성 수명·진영과 씬 요청 시점의 목적지·계정 세대를 고정합니다. #369에서 구매 회귀와 통합한 소스를 검토했습니다. | 정상 피해·시체 도감 저장·오래된 씬 요청 재소비 등의 회귀는 작성 상태입니다. 실제 AnimationEvent·풀·씬·BGM 및 이미 시작한 native scene load의 취소 한계는 실행 미검증입니다. |
| 가입 UI | 승인된 8차에서 예외 해소 후 전체 검정 캡처로 시각 검증 FAIL, 원본 복구 완료 | 9차와 실질적으로 같은 우회 시험은 STOP입니다. 근본 원인은 미확정입니다. |
| 16KB·RELRO | Play 16KB 지원 표시·기본 정적 LOAD·범용 APK 서명/ZIP 정렬 검사 이력 | 실제 16KB 환경 실행·기기별 split은 미검증이고 과거 산출물의 strict RELRO 실패 이력이 남아 있습니다. 신규 v28은 AAB가 없어 재검사 결과가 없습니다. |
| 광고·연령·No Ads | 9세 이상 성인 포함, 미성년 광고 차단, 기존 rewarded 보상과 영구 No Ads 상품·권한 유지 | 운영 광고/동의 승인 게이트는 false이고 binding은 null입니다. 지역·거절/철회·연령 변경·복원 실기기 확인이 남습니다. 5초 닫기 추가 조사는 보류이며 정책 충족을 선언하지 않습니다. |
| 개인정보·Data Safety | PlayFab 현 수집 유지, 기술 대조·신고 후보 준비 | 실제 제공 버전/SDK 처리·공개 방침·Console 신고/심사 결과의 최종 대조가 남습니다. No Ads가 모든 SDK 수집 중단을 뜻하지 않습니다. |
| 삭제 문의 | 기존 문의 메일로 운영자 직접 처리, 2주 이내 처리 목표, 종료 후 요청 자료 삭제 원칙 | 소유 확인·공급자 로그/백업 잔존 범위와 삭제 후 구매 복원은 미확정입니다. 2주를 모든 공급자 자료 소거 보증으로 쓰지 않습니다. |
| 최종 정리 | 기존 README 게임 소개·영상·Play 링크·방침 및 과거 검증 기록 유지, CI/CD 보류 | 최종 후보·기능·정책 확인 후 결산 문서, 시험 자원·브랜치 정리, 마지막 저장소 `Tamer` → `tamer` 변경이 남습니다. |

## 다음 작업 순서

호스트 점검은 실행 검증의 선행 조건입니다. 그와 독립적인 코드 설계·문서 대조까지 모두 멈추지는 않습니다. 현재 두 갈래로 진행합니다.

1. **현장 호스트 점검을 준비합니다.** JDK와 기존 시험 서명 복구는 완료했습니다. 사용자가 집 PC 앞에서 진행할 BIOS 점검 안내는 별도 담당 채팅에 남기고, 정확한 보드·공식 패키지·BitLocker 준비 여부를 확인한 뒤 실제 작업합니다. 원격 접속만 가능한 동안 자동 펌웨어 변경·전압/클럭 변경·재부팅·반복 빌드는 하지 않습니다. 관련 [Intel 공식 안내](https://www.intel.com/content/www/us/en/support/articles/000102331/processors.html)는 최신 BIOS와 Intel Default Settings를 권고하며, 해당 일반 안내만으로 이 PC의 고장 여부를 판정하지 않습니다. 실패 자료·Library·원본은 보존합니다.
2. **호스트와 독립적인 저장·정책 작업을 진행합니다.** 구매·삭제 접수 복구·전투·씬 요청 후보는 #369에 모았으며 이미 끝난 소스 검토를 반복하지 않습니다. 보유 장비 직접 착용처럼 구매와 다른 아직 확인하지 않은 경로만 좁게 점검하고, 확정 가능한 결함은 작은 수정으로 나눕니다. 모든 코드 초안은 필요한 검증 전까지 병합하지 않습니다. [광고 근거표](ads-release-evidence-checklist.ko.md), [Data Safety 후보](data-safety-draft.ko.md), README 방침과 [외부 삭제 안내](../account-deletion.ko.md)의 기존 대조 결과를 활용합니다. 기존 rewarded·영구 No Ads·직접 메일 처리 선택을 유지하며 10월 16일의 새 Tamer Console 관측·저장·신고 완료를 주장하지 않습니다.
3. **호스트 확인 후 통합 후보의 최소 코드 검증과 Store 진단을 재개합니다.** 정확한 검증 head를 고정한 뒤 관련 9개 Editor 테스트 클래스를 한 번에 묶고, 같은 회귀를 기능별·전체 runner로 연속 반복하지 않습니다. 컴파일·발견된 필수 사례·실패·skip·원본 복원을 함께 대조하며 실제 상점·전투·씬 UI는 별도 확인합니다. 이후 새로운 빌드의 소스·그때의 Console 최대 버전·업로드 인증서를 다시 결부해 제한된 재시도를 결정합니다. 성공한 후보의 서명·manifest·네이티브 검사와 Play 업데이트 이후 기기 관측은 각각 별도입니다. 기존 설치 앱에서 같은 구매/복원 반복을 요청하지 않으며, 검증 없는 권한 지급·구매 확정·주문 폐기를 하지 않습니다. 실제 복원은 [#351](https://github.com/ChoiDaeYoung-94/Tamer/issues/351), 빌드 차단은 [#358](https://github.com/ChoiDaeYoung-94/Tamer/issues/358)에서 추적합니다.
4. **운영 기능·정책과 최종 출시를 검증하고 정리합니다.** 정책 초안을 실제 제공 버전·지역·처리자·잔존 자료 및 Console 신고에 대조합니다. 최종 서명 AAB·전달 APK/split·실기기·정책/스토어 결과와 16KB/RELRO 미검증을 같은 후보에 연결합니다. 완료 전 `releaseReady=false`를 유지합니다. 이후 시험 자원을 정리하고 저장소 소문자 변경과 링크 도달성을 확인합니다. 기존 계정·저장·No Ads·서명키·GUID·비공개 근거와 보류 중인 CI/CD 구성을 보존합니다.

동일 시험이 두 번 실패하면 해당 시험과 종속 작업을 중단하고 결정 전에 세 번째나 유사 조건의 우회 시험을 실행하지 않습니다. 진행률의 과거 75.x%는 날짜가 있는 추정값입니다. 고정된 세부 완료 점수 없이 병합 PR 수·문서량·실패 시도를 새 정확한 완료율로 환산하지 않습니다.

</details>
