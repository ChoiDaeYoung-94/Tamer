# 시험 앱29의 Play 제공·검증 범위 — 2026-10-18

**Monster Tamer IAP Test 29(1.0.5)은 10월18일14:07(KST) 내부 테스터에게 제공됐습니다.** 실제 폰 업데이트·계정 재인증·구매 복원·운영 출시 완료는 아니며 `releaseReady=false`입니다. [최신 복구 현황](current-recovery-status.ko.md)과 함께 읽습니다.

## 시험29와 Play 산출물

빌드 소스는 [Draft PR #378](https://github.com/ChoiDaeYoung-94/Tamer/pull/378)의 `ac6eec24ee9a50bfaef7364f0737818d51aa9c36`입니다. Unity `6000.3.25f1` / revision `e1dba0a9aba4`, CLI `1.0.0-beta.8`, 시험 앱 `com.AeDeong.MonsterTamer.iaptest`, versionCode29/name1.0.5, min25/target36/ARM64/non-debug 기준입니다. Play에서 다운로드한 원본 AAB는 검증·전달본과 같습니다.

| 산출물 | 크기 | SHA-256 |
|---|---:|---|
| 시험29 AAB·Play 보관 원본 | 73,997,176 bytes | `1072f2afa5b9a2b2b4d684d44d0620de9e04628367adc985e176820662f0696e` |
| Play 생성·서명 범용 APK | 78,643,333 bytes | `e032a05ea4aa6ae1241800d22c62eb40269af314dc7e6035d3c3c6b66d293296` |

범용 APK의 앱 서명 유효성과 기대 앱 서명 일치를 확인했으며 Google Source Stamp는 별도 역할로 구분했습니다. 최초 파서 검사는 이 두 역할을 구분하지 못해1회 실패했고 수정 후1회 성공했습니다. 실패 근거를 보존했고 해시로 같은 APK에 귀속된 native 검사 보고서를 재사용했습니다. 이 정정은 새 빌드나 새 native 검사 실행이 아닙니다.

6개 native 라이브러리의 전체 payload는 업로드 AAB와 각각 일치하며 기본 ELF LOAD·ZIP 검사는 PASS입니다. **별도 RELRO 끝 정렬3개는 미충족이고 strict 재실행은0입니다.** 실제16KB 환경 실행·기기별 Play split/ZIP 정렬·폰 UI는 미검증입니다. 정적 결과와 Console16KB 표시로 실제 실행 성공이나 정책 통과를 선언하지 않습니다.

## 계정 원인과 후속 코드의 구분

Store 실패 원인은 기존 영수증이 과거 시험 계정에 귀속되고 현재 설치별 로그인 계정이 다르기 때문으로 확인했습니다. 기존 계정·저장·No Ads 권한을 보존했으며 재구매·권한 이동·강제 삭제는0입니다.

PR378의 재인증·원본 보존·새 프로필·gate는 `ac6eec2`에서 합성85 PASS와 시험29 빌드까지 확인했습니다. 실제 폰의 기존 계정 재인증·단일 복원·No Ads 내구 저장/재시작은 미검증입니다. 다음 두 수정은 **시험29에 포함되지 않았습니다.**

| 후속 | 수정·실행 head | 보존된 최소 검증 |
|---|---|---|
| [Draft #381](https://github.com/ChoiDaeYoung-94/Tamer/pull/381), 중복·SDK 자동 retry·지연 주문 gate | 수정 `22aeb3ac06071d3025ac3dffe819d2384940ed14`, 검증 `1d7a2657a02d3938a776f00275e2367fcc26a2dc` | `RevivalIAPOrderTests` 28/28 PASS, 실패0, 1회, 10월18일14:18:58~14:19:32(KST). XML SHA-256 `b8560e37ad7e985c126965224c7c472ef73615f93b82886522b1748927dedbef` |
| [Draft #383](https://github.com/ChoiDaeYoung-94/Tamer/pull/383), 캐릭터 저장 후 원래 세션 귀속 | 수정·검증 `fb14df81a4ef62924e56f8e6710fc5a5b1263f2b` | `RevivalUILifecycleTests` 8/8 PASS, 실패0, 1회, 10월18일14:41:58~14:42:20(KST). XML SHA-256 `c15f15e22c1906c176925728f79dc2822af25c8c7307ae84c25392d55398341b` |

각 실행은 Unity6000.3.25f1의 Android buildTarget EditMode에서 관련 클래스1개만 실행했습니다. 실계정 API 호출0, 보호 설정70개 중 자동 변경3개 원바이트 복구 후 Git clean을 확인했습니다. #381 수정과 검증 head 사이의 `Assets/Packages/ProjectSettings` 차이는0입니다. #383의 새 UnityTest 2메서드 안16분기는16개의 독립 NUnit 결과로 세지 않습니다. 실제 Store·Android Player·폰 UI 검증으로 확대하지 않습니다.

현재 후보 `be6b5ee6f6b01c512dbf84b89354044207b1104f`는 `fb14df8`에 main의 검사 도구·문서3파일만 추가해 제품 소스 차이가0입니다. 새 AAB30은 아직 빌드·전달하지 않았습니다. 문서·도구 #379·#380·#382의 main 병합과 게임 코드 #363·#366·#368·#369·#374·#378·#381·#383의 Draft/미병합 상태는 별개입니다.

## 운영26·정책·중단 조건

운영26은 Console 원본 AAB/범용 APK와 기대 upload/app 서명,6native 전체 payload 동일성 및 LOAD/ZIP을 이미 확인했습니다. 단, header `6000.0.81f1`·min24/target36/ARM64인 과거 산출물이며 source commit은 미확정입니다. 현재 Unity6.3/min25 검증 후보가 아닙니다.

운영 Console은 가족 광고 형식 거부와 미제출2변경(운영26 전체 출시·9세 이상), Data Safety의2024-09-30 NO 신고를 읽기 확인했습니다. 제출·수정은0이며 실제 제공 버전·SDK 처리·공개 방침·Console 신고의 대조가 남습니다. Analytics flag는 변경0이고 legacy off만으로 IAP 데이터 처리 off를 확정하지 않습니다. 운영 광고 gate=false/binding=null, 기존 rewarded·영구 No Ads·미성년 광고 차단 및5초 닫기 조사 보류를 유지합니다.

실제 계정 재시도는 총괄이 승인 gate와 보존 조건에 따라 단일 실행을 관리합니다. 기존 영수증 재시도·strict RELRO·가입 UI와 제어 검사의 STOP을 유지하며 같은 시험2회 실패 후3차·유사조건 우회 시험을 하지 않습니다. 실제 폰·정책·16KB·최종 출시 검증과 시험 자원 정리·마지막 저장소 소문자 변경이 남습니다. CI/CD와 App Center는 보류/보존합니다. 이번 문서 작업으로 빌드·테스트·설치·구매·설정 변경을 실행하지 않았습니다.
