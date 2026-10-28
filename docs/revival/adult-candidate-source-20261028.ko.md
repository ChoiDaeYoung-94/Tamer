# 성인 전용 후보의 소스 연결 — 2026-10-28

성인 전용 후보 생성 범위의 소스·불변 private 계약을 연결합니다. **코드 연결·후보 생성·실제 광고/개인정보·스토어 검증은 별개의 결과**입니다. 공개 저장소에 실제 운영 광고 단위나 비공개 승인 원본을 넣지 않습니다.

## 변경 범위

- `ProductionAdsEnabled`, `RegionalConsentReviewed`, `AdultConsentReviewed`를 검토된 후보 범위에 맞게 열고, `ApprovedRelease`에 private resource 전체 SHA·검토 baseline·다섯 근거 SHA를 고정했습니다. 관측 원본과 기술 검토·구체 적용 기록은 비공개로 보존합니다. 검토 baseline과 최종 build HEAD는 구분합니다.
- 실행에는 정확한 private resource·Android package/App ID/직렬화 Unit 일치와 정상 Android release 환경이 필요합니다. resource 누락·불일치·파싱 오류는 거절합니다. JSON·환경변수·PlayerPrefs만으로 compiled 승인을 만들 수 없습니다.
- 미성년3·Unknown·Declined·연령 수정 중·Editor/development/batch/명시 시험을 차단합니다. 성인 자기신고와 소스 승인만으로 UMP 동의나 광고 요청을 허용하지 않습니다. 기존 Update→Gather·요청 직전 재확인·No Ads·원래 광고 완료 보상과 소유권 보호를 유지합니다.
- 별도 개인정보 전용 환경 gate는 false, `ApprovedPrivacy`는 null로 유지합니다. 다만 성인 일반 광고 경로의 UMP 시작까지 차단하는 값은 아니며, 두 경로를 혼동하지 않습니다.
- Editor 시험은 저장된 연령을 읽거나 바꾸는 대신 메모리 Unknown과 기존 Changed 구독을 사용합니다. 순수 환경 조합·성인/비성인·차단 context와 합성 resource 거절을 확인하며, 실제 SDK/JNI를 호출해 시험하지 않습니다.

## 실제 빌드와 남은 경계

### 이번 변경의 검증

검증 checkout은 `D:/codex/worktrees/tamer-working/Tamer`, Unity는 `6000.3.25f1`/`e1dba0a9aba4`, CLI는 고정된 `1.0.0-beta.8`입니다. Android target 설정을 사용했으며 실제 APK/AAB를 생성하지 않아 제공물 SHA-256·서명·ARM64/16KB 결과는 없습니다.

| 범위 | 실제 결과와 대상 |
| --- | --- |
| 일반 Editor의 정책·연령·광고 관리자·연령 UI 4클래스 | `a58cae4`에서 114개 중113통과·실패0·배치 전용1건 건너뜀 |
| 건너뛴 배치 전용 광고 진입 차단 | `4b43b18`에서 별도1/1통과. 앞 실행과 환경을 구분 |
| 개인정보 순수 계약·재시작·context·native context | `4b43b18`에서 4모드 통과. 계약12검사이며 실제 SDK/JNI·개인정보 runtime 검증은 아님 |
| Editor 소유권 판별 | 실제 고정 CLI의 Windows PE 버전이 `1.0.0`인 경우를 재현·수정하고 합성13사례 통과. 정확한 CLI 설명과 알려진 버전을 함께 요구하며 알 수 없는 프로세스는 거절 |

앞선 순수 광고 계약21검사·consumer 차단 검사·Python17검사는 당시 작업본의 입력 해시로 연결합니다. 이후 변경되지 않은 입력의 성공 검사를 다시 실행한 것으로 적지 않습니다. 두 검토 단계와 소유권 수정의 독립 검토에서 남은 소스 차단 결함은 없었으며, 실제 광고·동의·제공물의 검증을 대신하지 않습니다.

검증 후 소유 Editor를 정상 종료하고 보호 설정38파일의 원래 바이트 해시를 대조했습니다. Editor가 기록한 동적 폰트 캐시는 비공개 보존 후 Git의 필터를 적용한 추적 원본으로 복원했습니다. 폰트의 검증 전 바이트 해시는 수집하지 않았으므로 그 파일까지 원래 바이트가 확인됐다고 주장하지 않습니다. 검증이 만든 설정·에셋 변경은 이 PR에 포함하지 않습니다.

기존18필드·일곱 literal·다섯 provenance·단일 exact plan 계약은 유지합니다. 현재 Console 최대번호·업로드 인증서, 실제 source/tool/runtime·키 challenge·실행 허용과 복구 범위가 고정되기 전에는 운영 빌드를 시작하지 않습니다. 일반적인 작업 재개 지시를 runtime·정책·심사 완료로 바꾸지 않습니다.

파트너 선택은 [동적 관리 검토](ads-release-evidence-checklist.ko.md)를 적용합니다. 전체 업체 명단의 수동 추출만을 유일한 후보 생성 조건으로 추가하지 않으며, 실제 유효 업체·메시지 전달·재동의/철회·지역별 동작의 미검증은 남깁니다. 국가 선언은 runtime 위치 판정이 아닙니다.

최종 AAB의 resource·직렬화 Unit·manifest·서명·native·복구와 같은 Play 제공물의 cold UMP·거절/변경·No Ads·로그인/저장/삭제·심사는 이후 필요한 범위로 확인합니다. 실제16KB 미검증·추가 strict RELRO2 미충족,5초 추가 조사와 CI 활성화 보류는 유지합니다. 최신 결과와 다음 단계는 [복구 현황](current-recovery-status.ko.md), 전체 잔여는 [잔여 목록](remaining-recovery-work.ko.md)을 따릅니다.
