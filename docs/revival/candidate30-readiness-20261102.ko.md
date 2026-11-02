# 후보30 재개 점검과 심사 전 연결 항목 — 2026-11-02

기존 [후보30 생성 결과](candidate30-built-20261030.ko.md)의 원본 AAB를 다시 읽어 76,123,986bytes와 SHA-256 `13e0a52c5ea3f244f00cce10a221ca642f350c4e5d7ea5a9472c6c8e74542c9e`가 일치함을 확인했습니다. 실제 빌드 소스는 `0c5d9047108b6dccbd6ae3f6229b910811c948b0`, 재개 시 문서 HEAD는 `f7f35643837feac8586235093158df39d3c65c62`입니다. 두 커밋 사이 Assets/Packages/ProjectSettings/tools 실행 입력의 차이는 없습니다. 새 빌드·설치·앱 실행을 하지 않았습니다.

## 실제 패키지의 SDK 연결

원본을 쓰기·삭제 공유 없는 읽기 핸들로 열어 AAB의 SDK 의존성 metadata 17,295bytes를 읽었습니다. 저장소의 패키지 버전만 보고 실제 빌드 버전으로 추정하지 않았습니다. 해석 형식은 [고정 bundletool1.18.3 schema](https://github.com/google/bundletool/blob/1.18.3/src/main/proto/app_dependencies.proto)와 대조했습니다.

| 포함된 구성 | AAB metadata의 버전 |
| --- | --- |
| Google Mobile Ads Android | 25.5.0 |
| Google UMP Android | 4.0.0 |
| Google Play Games Android v2 | 22.0.0 |
| Google Play Billing | 9.0.0 |
| Unity IAP | 5.4.3 |
| Unity Services Core / Authentication | 1.18.0 / 3.7.4 |

Maven87종·Unity67종의 metadata를 읽은 결과이며 모든 라이브러리의 동작을 검증했다는 뜻은 아닙니다. SDK·Editor·공유 도구 버전은 변경하지 않았습니다.

## 후보30의 지정 광고 provider 읽기

로컬 AAB29와 AAB30의 DEX3개는 각각 바이트/SHA가 동일했습니다. 그러나 Google이 제공한 APK29의 첫 DEX는 로컬 AAB의 첫 DEX와 달랐습니다. 나머지2개만 같다는 사실로 APK29의 분석 전체를 후보30에 전용하지 않았습니다. 차이의 원인은 이 비교만으로 확정하지 않습니다.

따라서 후보30의 첫 DEX를 공식 Android SDK build-tools36.0.0 `dexdump -d -n`으로 직접 읽었습니다. checksum/verification 생략 옵션 없이 종료0, 방문8,019클래스 중 `MobileAdsInitProvider`와 delegate `zzev` 두 클래스만 분석 대상으로 발췌했습니다. 두 `onCreate`는 정수0을 반환하고, `attachInfo`의 직접 경로는 metadata/앱 ID 형식 검사·옵션 로그·상위 provider 연결입니다. 두 클래스의 정규화한 직접 호출 목록은 기존 APK29의 지정 클래스 관측과 같았습니다. 읽은 목록에 직접적인 광고 ID 조회·`MobileAds.initialize`·네트워크 호출은 없었습니다.

첫 읽기는 DEX 문자열의 UTF-8 디코딩에서 실패했습니다. 부분 산출물을 보존하고 출력 디코더만 교정한 두 번째 읽기를 완료했습니다. 선택한 두 클래스의 발췌에는 치환 문자가 없습니다. 이 실패와 재시도는 게임·광고 실행 실패나 재빌드가 아닙니다.

이 결과는 지정 클래스의 정적 근거입니다. 간접 로그 함수·다른 provider/SDK·Google Play 서비스·IL2CPP·실제 기기 통신 전체를 검사한 것은 아닙니다. manifest의 AD_ID/AdServices 권한과 GMA provider를 임의 제거하거나, 존재만으로 성인 전 모든 데이터 처리 여부를 단정하지 않습니다. 기존 [IAP AdsIntent 거부](iap-advertising-intent-20261030.ko.md)와 로그인 기기 식별자 처리는 별개로 유지합니다.

## 정상 제공 후 확인할 최소 동선

| 경로 | 기대 동작 | 현재 남은 근거 |
| --- | --- | --- |
| 첫 실행·미입력 / 빈 값·잘못된 값 | 성인 기본값 없이 광고 경로 차단 | 실제 Play 제공30의 cold 시작과 SDK 처리 관측 |
| 18세 미만 / 응답하지 않음 / 연령 수정 중 | 광고 없이 마을·전투 이용, 광고 초기화·요청·표시 gate 차단 | 실제 제공물에서 사용자 직접 선택과 필요한 관측 |
| 18세 이상 | 필요한 개인정보 절차 뒤 명시 보상 포털 요청, 준비 뒤 재시도 | 지역 메시지·동의 변경·최초 cold·실제 보상 흐름 |
| 기존 No Ads 권한 | 영상 대신 기존 버프 경로, 권한 유지 | 기존 계정의 실제 권한과 재로그인/복원 연속성 |
| 심사 계정 | 저장을 유지하며 미성년/성인 경로에 접근 | No Ads 소유 여부 및 정확한 메뉴·버프 상태와 Console 안내 정합 |

유효 나이 입력·인증은 정해진 사용자 직접 조작 범위를 유지합니다. 운영 계정·저장·구매 권한을 지워 시험 조건을 만들지 않습니다. No Ads 계정의 영상 미표시를 성인 광고 재생 통과로 계산하지 않습니다. 같은 성공 시험을 목록 수만 늘리기 위해 반복하지 않습니다.

공식 [Families 정책](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en), [UMP 안내](https://developers.google.com/admob/unity/privacy), [요청 설정 순서](https://developers.google.com/admob/unity/targeting), [IAP5.4+ 개인정보 안내](https://docs.unity.com/en-us/iap/privacy-and-consent/overview)를11월2일 다시 읽었습니다. 광고 차단과 다른 SDK의 계정·구매·진단 데이터 처리는 구분하고, 신고와 공개 안내를 실제 제공판에 맞춰야 합니다. 지원 메일의 방향 확인은 앱 전체 정책 승인이 아닙니다.

## 현재 제공·작업 재개 조건

마지막 Console 읽기는10월30일입니다. 당시 내부29 제공·프로덕션24 제공/26 거절·30 미등록이며, 오늘 값으로 재확인한 것이 아닙니다. 전체 번들 번호·활성 release의26 포함/미포함·게시/신고 상태를 새로 읽은 뒤 기존 수동 업로드 합의에 따라30을 연결합니다. 자동 설치 확인·스토어 기기 검사를 우회하지 않습니다.

이번 정적 확인과 문서 보완은 Console 업로드·내부 제공·정책 심사 전송·프로덕션 출시가 아닙니다. 실제16KB 실행·최종 UI/스토어 검토와 [최초 복구 계획 전체 잔여](remaining-recovery-work.ko.md)는 유지합니다.
