# 원격 소스·비공개 에셋 복원과 Editor 통합 검증

2026-11-11, 검증 소스는 `70e1c9aea04b67c19a88421c39af9bc1aea3fe9e`입니다. 게임 코드 변경은08611bd이며 이후 차이는 인트로 검증 문서입니다. 기존 구현 작업본과 별도로 GitHub에서 소스를 다시 받은 작업본을 사용했습니다. 기존 작업본의 Git 객체 저장소나 Library를 연결·복사하지 않았습니다.

## 확인한 범위

| 항목 | 결과 |
| --- | --- |
| 원격 소스 | blobless sparse clone·정확 HEAD·로컬 object alternates 없음 |
| 비공개 에셋 | 4,204파일·574,358,085bytes, 원본 manifest SHA와 GUID 보존 |
| 복원 도구 재검사 | verified4,561/copied0·SDK meta migration7/private migration0 |
| 서비스·서명 자료 | 서비스 설정은 빈 템플릿, 서명 파일과 알려진 키 객체는 복원 대상에서 제외 |
| 실제 Unity 에셋 DB | 비공개 GUID2,186개 일치·main asset2,014개 로드·아래 구형 설정4개는 로드 불가 |
| 활성 여섯 씬의 정적 의존성 | 421개·읽을 수 없는 main asset0 |
| 새 작업본의 일반 Android 컴파일 | 68어셈블리·DLL/PDB136개·추가 시험 define0 |
| 컴파일 결과의 메타데이터 검사 | UI·격리 코드 제외18항목 통과, 게임 어셈블리 실행0 |
| YAML GUID 검사 | 135파일·미해결0 |
| 전체 `Revival` EditMode 검사 | 한 실행에서1,117/1,117·실패0·skip0·inconclusive0,57fixture |

Unity6000.3.25f1/revision e1dba0a9aba4, CLI1.0.0-beta.8, Android min25/target36/ARM64 기준입니다. 복원 작업본의 Library는 새로 생성했으며 호스트의 전역 UPM 캐시는 재사용할 수 있는 환경입니다. 전체 EditMode1,117개에는 `RevivalQuadDIntroTests`21개와 `RevivalQuadDLoginIsolationTests`8개가 포함됩니다. 앞선 부분 검사 수를 더해 새로운 전체 검사 수로 계산하지 않습니다.

## 첫 불러오기에서 발견한 한계

첫 cold import 뒤 전체 비공개 파일을 모두 Unity 오브젝트로 읽으려던 검사는 구형 이펙트의 `PP low.asset`에서 실패했습니다. 실패 기록과 생성된 Library를 보존했습니다. 같은 Library의 후속 진단에서 전체 목록을 읽고 현재 게임 씬의 의존성과 일반 Android 컴파일을 확인했습니다. 이 후속 결과를 첫 cold 검사 전체 통과나 새 Library 재생성으로 표현하지 않습니다.

읽히지 않은 네 파일은 Hovl 이펙트·Forest Bat 데모·Magma Golem·Cute Series 데모의 구형 후처리 설정입니다. 저장된 파일/SHA/GUID는 복원됐지만 main asset 로드는 실패합니다. 원본을 삭제하거나 manifest를 새 값으로 바꾸지 않았고, 구형 후처리 패키지를 임의 추가하지 않았습니다. 활성 여섯 씬의 정적 의존성에는 누락이 없었지만 `Resources` 등에서 문자열로 동적 로드하는 모든 경로까지 검증한 결과는 아닙니다. 구형 데모까지 사용하는 복구에는 이 네 설정의 추가 검토가 필요합니다.

## 통합 검사와 원본 복구

10월23일 이후 로그인·손님 진입·구매/저장·UI·인트로 변경을 개별 검증한 조합을 확인하기 위해 전체 `Revival` EditMode를 한 번 실행했습니다. 운영 계정 로그인·서버 저장·실제 구매/광고·Play Mode·Player 빌드·기기 명령은 실행하지 않았습니다. 기존 서명 사전검사 사례는 임시 합성 인증서와 고정 JDK의 keytool만 사용했습니다. 운영 서명키는 새 작업본에 존재하지 않습니다.

과거 Pipeline 외부 요청 간섭 조건을 재사용하지 않도록 이 독립 batch의 Pipeline AutoStart만 임시로 비활성화했고 종료 후 원바이트로 복구했습니다. 제품 assert나 로그 검사 조건은 완화하지 않았습니다. 실제 XML1,117개 고유 실행명과 각 결과·전체 집계·CLI exit0을 대조했습니다.

Unity가 PSD importer 메타데이터와 설정의 직렬화/줄바꿈을 바꾼 자료는 별도로 보존하고 기존 GUID·원바이트로 돌렸습니다. 처음 생성된 PlayFab Editor 설정과 통합 검사에서 생성된 Play Games 설정도 소유·타입·사전 부재·SHA를 확인해 비공개 증거 폴더에 보존했습니다. 기존 서비스 설정이나 다른 작업본의 파일은 이동하지 않았습니다.

통합 NUnit1,117개 자체는 한 번에 통과했습니다. 최초 scope 집계는 새 Play Games 설정2개의 사전 부재 때문에 복구 확인에 실패했습니다. 그 원실패를 보존하고 해당 두 파일을 Assets 밖에 보존한 뒤 원본 파일 구성을 다시 대조했습니다. 이 정리 때문에 NUnit·컴파일·Editor를 반복하지 않았습니다. 보호 입력은 복원 작업본6,954개와 기존 작업본/키6,961개 모두 원바이트입니다. 공유 ADB 신원·JDK·EditorPrefs 원래 값과 전체 소유 프로세스 종료를 확인했으며 실제 실행 자원 반환은05:27:12UTC, 이후 파일 보존 정리를 별도로 완료했습니다.

## 백업과 출시 검증의 구분

원격에서 필요한 소스를 다시 받는 절차는 확인했습니다. 그러나 [Git의 blobless clone](https://git-scm.com/docs/git-clone.html)은 필요한 blob을 나중에 가져오므로 전체 Git 이력을 독립된 오프라인 백업으로 보관한 결과는 아닙니다. 비공개 에셋은 같은 PC의 보존 snapshot에서 복원했으며 Google Drive 등 외부 사본의 복구·업로드는 하지 않았습니다. Library를 백업 원본 대신 사용하지 않습니다.

필수 보관 자료는 원래 서명/제출 자료, 비공개 에셋 snapshot과 receipt, 기존 암호화 키 복구 자료입니다. 최신 시험 APK·화면은 검토 자료이며 원본의 대체물이 아닙니다. 개인 설정·키·에셋 원본·XML/원시 로그를 공개 Git에 넣지 않았습니다.

이 결과는 실제 Google/손님 서버 계정·기존 저장/No Ads·연령별 SDK 전송/동의/광고·운영 AAB/정상 Play 전달·실제16KB·최종 UI/스토어/정책 심사/프로덕션을 검증하지 않습니다. [전체 잔여 작업](remaining-recovery-work.ko.md)을 유지하며 부모 인트로/UI 변경의 운영 통합 조건도 남아 있습니다.
