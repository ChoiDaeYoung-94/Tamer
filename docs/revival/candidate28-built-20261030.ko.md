# 인트로·UI 통합 검토 후보28 생성과 기본 검사 — 2026-10-30

**Quad D 인트로·새 아이콘·UI를 포함한 운영 앱 ID의 AAB를 생성하고 기본 정적검사 및 원본 복구를 완료했습니다.** 최종 디자인/운영 검증과 정상 Play 제공·가족 정책/심사 승인은 남아 있습니다. 기존 후보27과 운영 설치/계정·저장·No Ads·키를 보존했습니다.

## 정확한 실행 입력과 결과

| 항목 | 실제 결과 |
| --- | --- |
| 작업본/브랜치 | `D:/codex/worktrees/tamer-working/Tamer` / `codex/ui-facelift-1029` |
| 실제 빌드 소스 | `f5d7ab42edb4386c9339be1f7be0af28bf267364` |
| 도구 | Unity `6000.3.25f1` / revision `e1dba0a9aba4`, CLI `1.0.0-beta.8`, Pipeline `0.6.0-exp.1` |
| 앱/버전 | `com.AeDeong.MonsterTamer` / code28 / 1.0.5 |
| Android | min25 / target36 / ARM64 / IL2CPP / nondebug·nontest |
| 실제 모드 | 기존 승인 `approved_adult_release`; 앱 타겟은 혼합 연령대이며 광고를 성인 경로로 제한하는 계약 |
| 서명/빌드 | 기존 원키와 현재 Console upload 공개 인증서의 실제 challenge 통과 후 Android AAB 빌드1회 성공 |
| 검토용 파일명 | `MonsterTamer-28-1.0.5-QuadD-Facelift.aab` |
| 파일 크기 | 76,134,501bytes |
| AAB SHA-256 | `ff14c83920f6123969eeabd8ed8eb6110fa6190b36a679ae561297cc19fd5d0d` |

빌드 직전 확인한 Console은 전체21번들·최대27/27비활성입니다. 현재 upload 공개 DER와 기존 검토본의 일치를 확인했습니다. 검토용 복사본은 생성 원본과 byte/SHA 동일하며 비공개 보존합니다. 실제 앱 키와 비밀값·원시 로그/검토 파일은 공개 저장소에 추가하지 않습니다.

## 실제 파일과 복구 검증

- bundletool validate·base manifest의 앱/버전/SDK/ABI·기존 upload 서명과 전체 payload 서명을 검사했습니다.
- 네이티브6개는 ELF LOAD와 보수적 RELRO 배치 기준을 통과했습니다. 원시 RELRO 끝나머지 진단3건을 별도로 보존하며 실제 native ARM64/16KB 실행이나 Play 제공 APK ZIP 정렬 완료로 표현하지 않습니다.
- `globalgamemanagers`의 길이 포함 씬 배열 프레임에서 **QuadDIntro→Login→Main→Game→SetCharacter→NextScene**의6개 원래 목록/순서가 일치했습니다. 전체 TypeTree 파싱 결과는 아닙니다.
- 공통 패키지 pin `55bf9e4aa48c2f95942f3c2c995f1d55414e97c0`의 인트로 영상88,032바이트와 실제 AAB의 영상 payload SHA가 일치했습니다. 패키지 launcher PNG192px를 추출했고 후보27의 같은 아이콘과 다른 것을 확인했습니다. [아이콘 원본·전체 프롬프트](../images/facelift-assets-20261029.ko.md)를 연결합니다.
- 승인 광고 설정을 named TextAsset 프레임에서 독립 추출해 원본 payload SHA와 일치를 확인했습니다. 실제 런타임 resource load/SDK 네트워크 관측은 별도입니다.
- 전역 시험 기호와 시험 씬 제외는 소스·빌드 계약으로 확인했습니다. manifest의 nondebug/nontest 검사와 연결하며 모든 미사용 시험 타입의 바이너리 부재를 주장하지 않습니다.
- 원본9개와 이번 소유 staging9개를 정확한 delta/기존 복구 계약으로 원복했습니다. 보호 입력7,112개 모두 원래 SHA와 일치하고 누락/새 보호 입력0, Git clean·활성 journal 없음·관측한 소유79프로세스/Editor0을 확인했습니다. 기존 Library와 과거 계획/실패 증거를 보존했습니다.

독립 SDK 메타데이터에서 관측한 주요 버전은 Billing9.0.0, Google ads25.5.0, PGS games-v2 22.0.0, UMP4.0.0, Unity IAP5.4.3·Authentication3.7.4·Core1.18.0입니다. 실제 AAB에 기록된 버전이며 SDK의 실시간 동작/정책 승인을 뜻하지 않습니다. GMA init provider 존재와 optimize 플래그 true, delay-app-measurement-init 명시값 없음도 관측했습니다. 모든 SDK의 시작 처리나 네트워크 수집이 차단됐다는 의미로 확대하지 않습니다.

## 남은 확인과 다음 순서

15:43 KST 추가 대조: [연령별 심사 동선·SDK 시작 경로](families-pre-review-20261030.ko.md)와 [스토어 자료 초안](store-listing-facelift-20261030.ko.md)을 준비했습니다. 이는 docs만의 후속이며 AAB/source는 같습니다. 정책 지원의 방향 안내를 앱 승인으로 계산하지 않고 성인 전 광고 ID/SDK 처리·심사 계정의 실제 No Ads 조건·모든 활성 release의26 제외 확인을 남깁니다. 사용자 요청으로 저장이 끝난 로그인 세부정보 탭만 닫았으며 자리 비움 중 사용자 조작을 호출하지 않습니다.

이번 실행은 실제 운영 파일의 컴파일·기본 정적검사입니다. 기존 격리 UI source83cfd19와 인트로/Login source998a808의 동작 근거를 [UI 기록](ui-facelift-20261029.ko.md)에 연결합니다. 이 새 후보의 실제 운영 로그인·서버 저장/기존 구매·No Ads, 정상 Play 제공/기기 인증·정책 통과는 미검증입니다. [운영 제공 제한](candidate27-first-launch-20261029.ko.md)을 유지하고 같은 조건의 설치/로그인을 반복하지 않습니다.

다음은 기존 수동 업로드 합의에 따른 내부 테스트 초안의 파일 업로드·분석 확인입니다. 총괄은28의 업로드 수락을 아직 확인하지 않았고 실제 심사 전송·배포·트랙 재개는 수행하지 않았습니다. 사용자 최종 UI 의견과 Store512px 아이콘/실제 스크린샷 등 자료도 남아 있습니다. PR492/493는 Draft/미병합이며 이 기록을 출시 승인으로 쓰지 않습니다.

**작업 상태:** 후보28 생성·기본 정적검사·정확 복구 완료, 운영/제공·심사·최종 디자인은 대기. **다음 작업:** 수동 업로드 후 Play 분석 대조→정상 제공/운영 기능·최종 UI 확인. **사용자 할 일:** 준비된 검토 파일의 Console 직접 업로드 및 완료 상태 공유. **질문:** 추가 질문 없음. 전체 잔여는 [잔여 계획](remaining-recovery-work.ko.md)을 따릅니다.
