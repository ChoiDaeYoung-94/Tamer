# 시험 앱 28번의 Play 제공·산출물 검증 — 2026-10-17

**Monster Tamer IAP Test 28(1.0.5)이 내부 테스터에게 제공됐습니다.** 실제 기기 업데이트·로그인·주문 진단·No Ads 복원과 운영 출시는 아직 완료하지 않았습니다.

## 빌드와 업로드의 동일성

빌드 소스는 `3d77ff6697c0385350a72acdd02caf4d25e5d5f7`이며 셰이더 수정 후보 [PR #374](https://github.com/ChoiDaeYoung-94/Tamer/pull/374)입니다. Unity6000.3.25f1/min25/target36/ARM64, 시험 앱 `com.AeDeong.MonsterTamer.iaptest`, non-debug 기준입니다.

사용자 수동 업로드 후 총괄이 신규28번과 차단 오류 부재를 확인하고 시험 앱의 내부 출시를 완료했습니다. Console에는 **내부 테스터에게 제공됨 / 2026-10-17 오후4:18**로 표시됐습니다. 운영 정책 심사 통과를 뜻하지 않습니다. Play에서 다시 다운로드한 원본 AAB는 로컬 검증·전달 파일과 크기·해시까지 일치합니다.

| 산출물 | 크기 | SHA-256 |
| --- | ---: | --- |
| Play 보관 원본 AAB | 73,955,742 bytes | `b637dc98c237c15bfb3db523ac54347267e6897b87679bffbcb39ba9ad0f96c8` |
| Play 생성·서명 범용 APK | 78,602,373 bytes | `c138a11c8a1dba4adc7493dd34a8f80f2a3067ee7ed850254cd813f81163d806` |

## Play 범용 APK 검사

aapt2·apksigner로 실제 다운로드한 APK의 시험 앱 ID, 버전28/name1.0.5, min25/target36, non-debug, ARM64 및 서명 유효성을 확인했습니다. 앱 서명 인증서는 Console에서 확인한 인증서와 일치하며, Google Source Stamp 인증서는 별도로 구분했습니다.

네이티브 라이브러리6개는 업로드 AAB의 동일 라이브러리와 각각 해시가 일치합니다. 모두 압축 저장되며 기본 ELF LOAD·ZIP 검사를 통과했습니다. **기기별 split APK 검증과 실제16KB 환경 실행은 미검증**입니다. 별도 RELRO 끝 정렬 조건3개는 미충족으로 기록했고, 기존 중단 상태인 strict RELRO 시험은 실행하지 않았습니다. 정적 조건 미충족만으로 실제 기기 충돌을 확정하지 않습니다.

Console의16KB 지원 표시를 실제 기기 실행 성공으로 계산하지 않습니다. 새 빌드·Unity 회귀 시험·설치·구매를 이 기록 때문에 반복하지 않았습니다.

## 진단 경고와 다음 작업

출시 전 경고는 가독화 파일과 네이티브 디버그 기호 미업로드2개였습니다. 생성된 Gradle 설정은 `minifyEnabled=false`이며, 존재하지 않는 R8 매핑을 만들거나 난독화 설정을 변경하지 않았습니다. 사전 난독화된 서드파티 코드의 매핑을 확보했다는 뜻도 아닙니다.

기존 빌드 자료에서 libil2cpp·libunity·libmain 기호를 실제 AAB의 ELF Build ID와 대조해 일치한3개만 ZIP으로 보존했습니다. 다른3개 라이브러리 기호, Console 업로드·수락 및 경고 해소는 아직 확인하지 않았습니다. 기호는 주소를 함수명으로 해석하는 진단 자료입니다. [Unity6.3 설명](https://docs.unity3d.com/6000.3/Documentation/Manual/android-symbols.html), [Google Play 안내](https://support.google.com/googleplay/android-developer/answer/9848633?hl=en).

다음은 기존 내부 테스트 참여 계정으로 앱을 **삭제하지 않고 업데이트**한 뒤 첫 화면의 빌드 표시와 오류 코드를 확인하는 일입니다. 현재 PC에 ADB 기기는 연결되어 있지 않아 기기 결과를 자동 확인하지 못했습니다. 진단 근거를 확보하기 전에 같은 구매·복원을 반복하지 않습니다.

실제 APK/AAB·기호·원시 로그는 비공개로 보존합니다. 이번 확인은 [#351 주문 복원](https://github.com/ChoiDaeYoung-94/Tamer/issues/351)의 후속 근거이며, 계정·저장·No Ads 권한·서명키·운영 앱과 프로덕션26/연령 변경은 보존했습니다. `releaseReady=false`를 유지합니다.
