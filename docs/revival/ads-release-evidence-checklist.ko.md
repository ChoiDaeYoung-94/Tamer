# 실제 운영 광고 실행 직전 근거 체크리스트

2026-10-06 기준 main `68d14650a9934adafc50807508a47d468e766911`에서 기존 자료와 소스를
읽기 대조했다. PR #304·#305·#307의 producer/immutable 계약/consumer 연결은 **소스 준비**
근거다. 운영 승인 또는 실제 production 실행·개인정보·최종 artifact 검증 근거가 아니다.
이번 대조에는 Console 최신 조회·키 암호 확인·Editor·SDK·광고 실행이 없었다.

확정된 성인 전용 광고, 기존 9+ 대상, 배포국가 선택, No Ads 판매·기존 권한·복원 및 광고
완료 보상/buff 유지 결정은 다시 묻지 않는다. 5초 닫기 추가 조사 보류도 유지한다. 일반적인
작업 재개를 미완료 지역/운영 승인으로 간주하거나 선택의 확정을 실제 정책 충족으로 승격하지
않는다. 현재 국가 설정을 확인하는 일은 새로운 국가 선택 승인을 받는 일과 구분한다.

| 실행 직전 필요한 자료/계약 | 현재 실제 근거와 상태 | 충족에 필요한 구체 내용 |
| --- | --- | --- |
| inventory 원본 기록 → `inventorySha256` | 기존 비공개 6필드 기록의 inventory 확인 true, source App ID와 manifest 일치. activation/regional false | 실제 운영 앱/package/App ID/rewarded Unit, 같은 publisher, Console 앱·단위의 상태/관측일과 근거를 하나의 비공개 원본 기록으로 묶고 exact 바이트 해시를 검토. 현재 source 일치를 최신 Console 상태로 확대하지 않음 |
| 배포국가 계약 → `countryContractSha256` | 기존 배포 선택은 확정. 이번 최신 Console 국가·게시자 UMP 메시지 조회0 | 현재 선택된 국가 목록과 실제 Console 배포 목록, UMP 메시지의 대상·게시 상태·적용 범위/예외를 같은 기록에 대조. 정렬된 `countryCodes`는 그 목록과 일치해야 함. 두 글자 문법은 실제 ISO/지역 위치·동의 검증이 아님 |
| 지역·개인정보 검토 → `regionalReviewSha256` | 게시자 성인 EEA의 과거 격리 관측 존재. 새 runtime/privacy 승인 근거로 전환하지 않았으며 지역 gate false | 실제 게시자 메시지와 배포 범위, 오류/취소/철회 시 차단, 재시작/age 변경, 개인정보 선택 재진입 및 지역별 취급을 검토한 원본 기록과 그 한계·명시 승인. 과거 부분 동의 관측을 전체 거부 또는 개인 맞춤 동의로 쓰지 않음 |
| 성인 전용 적용 검토 → `adultReviewSha256` | adult-only 제품 선택·미성년 false source·합성 guard 근거 있음. 실제 기기 승인자료 없음 | 성인 자기신고·Unknown/Declined/미성년 차단, SDK request treatment/content rating, 기존 보상·No Ads 및 연령 변경 보호를 source/artifact/기기 범위별로 기록하고 적용 승인. 제품 선택 확정을 기술·정책 적합성 완료로 쓰지 않음 |
| 구체 운영 활성화 허용 → `activationReviewSha256` | 작업 계속 및 준비 소스는 승인. 운영 계약/flags 활성화 승인 기록은 없음 | 위 네 기록의 exact hashes와 대상 source baseline·앱·국가·운영 범위·알려진 미검증 항목을 식별하는 명시 승인 기록. 실제 gate/binding 변경과 단일 빌드 허용 범위를 분리. 새 서버를 승인 조건으로 만들지 않음 |
| 승인 resource 및 compiled binding | 현재 18필드 승인 resource 없음, `ApprovedRelease=null`, master/지역/성인 false | 승인된 다섯 원본 기록을 exact hash로 연결한 raw 18필드 resource와 전체 resource SHA, 검토 source baseline40자리, 일곱 literal binding. 별도 source review에서 gate 변경 범위를 확인. 자료를 자동으로 true로 만들거나 JSON/env/CLI로 compiled 승인을 대체하지 않음 |
| 기존 릴리스 서명 사용 근거 | 기존 key 파일 존재. 보존/백업 근거와 기존 암호·개인키 사용 가능성은 별개이며 이번 키 검사0 | 기존 키를 실제로 열고 private key/alias/인증서 사용 가능성을 확인한 허용된 단계와 현재 Google 업로드 인증서 대조. producer signing receipt의 key SHA·alias·certificate SHA·sourceHead·plan SHA·existingReleaseKeyVerified를 연결. 비밀번호는 실행 환경에만 두고 receipt/resource/공개 문서에 넣지 않음 |
| 정확한 단일 build run의 검토 계획 | mode별 소스 경로 준비. 실제 승인 production plan·launch marker 없음 | clean/closed checkout, source/tool/config SHA·설치 Editor/tool identity, owned staged 부재·output 부재, source review·서명 근거·명시 build 허용을 한 계획에 고정. 전체 Assets/meta/원본·기존 artifact 보호와 실제 delta/별도 검토 복구 범위 포함. 기존 source-ready receipt만으로 실행하지 않음 |
| 실제 AAB 및 runtime/privacy·스토어 검증 | production artifact 없음. binary/productionContract/distributable/runtime privacy 미검증 | 실제 callback/내부 복원/외부 복구, merged manifest·resource·serialized Unit·compile provenance·실제 인증서·16KB 등 해당 검사를 정확한 AAB SHA에 연결. 허용된 기기·스토어 검증과 정책/개인정보 대조는 별도 완료 근거로 남김 |

다섯 provenance 해시는 검토 기록을 식별할 뿐 전자서명·법적 승인·정책 적합성을 자동으로
증명하지 않는다. 원본 기록에는 관측 대상/시각, 관측 사실과 판단, 실제 source·artifact 또는
기기의 범위, 승인 주체와 허용 범위, 미검증/예외를 구분한다. 승인 결정이 없는 사실 기록에
승인 true를 임의 추가하지 않는다. 모든 운영 자료는 비공개 owner+SYSTEM 근거 경계 안에
보존하며 공개 커밋에는 실제 ID·서명 값·기기 hash·원로그를 넣지 않는다.

## 개인정보와 No Ads의 남은 실제 확인

요구사항과 현재 코드 관찰을 구분한다. No Ads는 현재 source에서 광고 init/load를 막고
성인의 기존 UMP/Required privacy 경로를 별도로 유지하며 즉시 보상 분기도 보존한다.
이는 실제 기기 동작 완료 근거가 아니다. 현재 adult-only production consumer는 미성년의
`CanBeginConsent`를 열지 않는다. 따라서 신규 미성년의 UMP 메시지/Required privacy 발견을
확인했다고 쓰면 안 된다. 게임의 개인정보 안내·계정 삭제 접근과 UMP native privacy entry는
별개로 대조하고, 성인→미성년/거절 변경 뒤의 캐시·withdrawal·접근 경로 역시 기기 검증 pending이다.
No Ads 기존 권한과 저장·구매 복원은 그대로 보존하며 테스트 조건을 만들려고 삭제하지 않는다.

## 현재 대조 결론과 다음 승인 경계

이번 로컬 읽기에서 inventory/source 일치, 기존 source-ready 원본, false gates/null binding,
resource 부재와 key 파일 존재만 확인했다. 실제 승인자료·최신 country/message 대조·key 실사용·
actual production artifact/runtime privacy는 미충족 또는 미검증이다. historical 준비 PASS를
최신 운영 검증으로 재실행하거나 확대하지 않았다. 새 테스트·compile·Editor/SDK/광고·승인
자료/Assets 수정은 0이다.

다음은 새로운 준비 코드를 만드는 단계가 아니라 위 원본 자료를 읽기 수집·대조하고, 부족한
외부 접근 또는 명시 승인 범위를 총괄 담당자가 한 번에 정리할 단계다. 자료가 준비되기 전에는
false/null과 production prelaunch 거절을 유지한다. 실제 승인 직전에 사용자에게는 식별된
원본 기록/해시·source 변경·단일 실행 범위와 남은 한계를 함께 제시하며, 확정된 제품 선택을
다시 묻지 않는다.
