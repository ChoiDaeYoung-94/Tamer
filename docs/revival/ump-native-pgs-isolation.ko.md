# UMP 개인정보 시험의 네이티브 Play Games 초기화 분리

`RevivalAdHarnessBuild.BuildUmpPrivacyAgeIsolated`는 기존 publisher 개인정보 시험과 같은 패키지·디버그 서명·단일 검증 씬을 사용하는 별도 APK를 생성합니다. 이 진입점에서만 최상위 manifest에 정확한 `PlayGamesInitProvider`와 `com.google.android.gms.games.APP_ID`의 제거 규칙을 임시 적용합니다. 기존 빌드 진입점, 운영 광고 승인, 정상 PGS·IAP 경로와 플러그인은 변경하지 않습니다.

Play Games SDK는 [provider를 통한 시작 시 자동 초기화](https://developers.google.com/android/reference/com/google/android/gms/games/PlayGamesSdk)를 지원합니다. Managers가 없는 씬만으로 이 네이티브 경로를 분리할 수 없었습니다. [manifest 병합 제거 규칙](https://developer.android.com/build/manage-manifests#node_markers)을 시험 빌드에 한정해 적용하며, 플러그인의 명시적 `PlayGamesSdk.initialize` 호출까지 모두 제거했다는 의미는 아닙니다.

원 manifest·메타파일은 삭제 공유 없는 읽기 핸들로 원래 파일 객체를 유지합니다. 짧은 쓰기 핸들은 다른 쓰기를 차단하고 파일 식별 정보와 예상 바이트를 다시 확인한 뒤 닫습니다. Unity import는 그 뒤 수행하고, 실패 시에도 원 manifest 바이트와 메타파일·GUID를 확인합니다. 동시 변경을 발견하면 덮어쓰지 않고 외부 복구가 필요하다는 실패를 남깁니다. 빌드 실패와 복구 실패는 별도 기록하며, 복구 실패를 성공으로 처리하지 않습니다.

## 빌드와 정적 검증

실제 APK의 소스는 `29ce05b4d81d267eb27645259098dd70eecc1481`입니다. 이후 문서 커밋에서 다시 빌드하지 않았습니다. 출력은 `Build/revival/Tamer-ads-ump-publisher-privacy-age-pgs-isolated.apk`이며 기존 APK는 보존했습니다.

검증 checkout은 `ChoiDaeYoung-94/Tamer`의 `codex/ump-native-pgs-isolation` 브랜치입니다. Unity `6000.3.25f1`(revision `e1dba0a9aba4`), Unity CLI `1.0.0-beta.8`을 사용했습니다. Unity AndroidPlayer의 설치 파일에서 SDK platform API `36`(revision 2), SDK Build Tools `36.0.0`, NDK `27.2.12479018`, OpenJDK `17.0.18`을 확인했습니다. 실제 시험 기기는 Android `13`/API `33`, ARM64 지원, 메모리 페이지 `4096`바이트입니다. 이 4KB 기기 시험으로 16KB 환경을 검증했다고 주장하지 않습니다.

최종 APK는 `141601843`바이트이며 SHA-256은 `ff53c7494027e1ef822b9f2c9d842b7926e03aa98b201eb70be4a370973ba952`입니다. 도구체인 설치 버전 확인과 캐시된 빌드 산출물의 생성 이력은 구분합니다.

첫 격리 빌드는 장기 읽기·쓰기 핸들과 Unity 읽기 공유의 충돌로 실패했습니다. 원자료를 보존하고 설정을 복구한 뒤 읽기 핸들과 짧은 쓰기 핸들을 분리한 두 번째 격리 빌드가 성공했습니다. 이전 일반 빌드·컴파일·기기 시험의 실패 이력도 유지했습니다.

최종 APK에서 PGS provider·앱 ID 부재, AdMob 초기화 provider·publisher 앱 ID 유지, UMP DEX 타입 유지, 기존 디버그 인증서 일치, 최소 API 25·대상 API 36·ARM64를 확인했습니다. UMP DEX 타입의 존재는 SDK 실행 결과를 대체하지 않습니다. 컴파일 설정 파일에는 네 가지 시험 정의·Unity Core 참조·검증 소스와 Editor 정의 부재가 확인됐지만, 캐시된 파일이므로 이번 빌드에서 새로 생성됐다고 주장하지 않습니다.

정적 검증의 기존 APK 예상 해시 리터럴에 두 글자가 빠진 전사 오류가 있었습니다. 최초 실패 기록을 보존하고, 원래 보호 스냅샷과 현재 APK의 전체 바이트 해시·파일 식별 정보 일치로 별도 정정했습니다. 통과한 manifest·서명·DEX 검사는 반복하지 않았습니다. 빌드 후 설정 변경과 생성 파일은 바이트를 보관한 뒤 정확한 대상으로만 복구했고, 전체 보호 파일과 원 manifest·메타파일을 확인했습니다.

## 실제 Android 관측

기존 시험 휴대폰에서 user0 앱을 데이터 보존 업데이트했습니다. 업데이트 전후 관측 가능한 환경설정 4개의 내용·파일 집합·식별 정보가 같았습니다. 정상 프로세스 재시작은 저장된 SDK 동의를 초기화하지 않습니다. 기기 교체 관측과 과거 시험 기록은 보존했습니다.

| 단계 | 관측 결과 |
| --- | --- |
| 등록·정상 재시작 | 새 APK 실행 2회, 현재 SDK 원문 시험 해시 등록 Update 1회. 자동 동의 요청과 외부 인증 화면 재등장 없음 |
| EEA 초기 요청 | 성인 시험 선택, EEA Update 1회·초기 네이티브 폼 1회. 실제 `Do not consent` 후 완료와 `Required=true` 확인 |
| 열린 개인정보 창에서 연령 거절 | 개인정보 창 첫 진입의 실제 픽셀과 처리 중 로그를 함께 관측. 2초 후 공유 helper로 `Declined` 전환 시 `busy_before=true`, `busy_after=true`, `Required=true`, `can_request=false` |
| 첫 개인정보 창 완료 | 비동의 선택 후 실제 콜백으로 처리 중 상태 해소, `Required=true`·앱 요청 차단 유지 |
| 명시적 재진입 | 추가 Update 없이 개인정보 창 두 번째 진입. 관리 화면에서 관측한 동의 토글 2개가 꺼져 있었으며 기존 선택을 확정한 뒤 콜백 `can_request=false`·`Required=true` 유지 |

해시 구성 첫 시도는 키보드를 뒤로 닫은 후 빈 입력란이 관측되어 요청을 보내지 않았습니다. 그 미완료 기록을 유지하고 두 번째 시도에서 현재 키보드의 실제 확인 버튼으로 확정했습니다. 원 SDK 값의 대소문자를 그대로 입력했고 마스킹된 입력란을 확인한 뒤에만 EEA 요청을 진행했습니다. 마스킹 화면만으로 값 전체가 일치한다고 주장하지 않습니다.

초기 `Do not consent` 후 SDK의 광고 요청 가능 값은 `true`였습니다. 이후 연령 거절로 차단한 앱 게이트의 `false`와 구분합니다. 관리 화면에서 모든 목적·공급자·정당한 이익을 철회한 시험은 아닙니다. 동의 승인·운영 로그인·구매, 관리 코드의 `MobileAds.Initialize`·광고 로드·표시, clear·uninstall·SDK reset과 추가 성공 재시험은 수행하지 않았습니다. 유지된 네이티브 AdMob provider의 자동 동작까지 없다고 주장하지 않습니다.

이 관측은 UMP 전용 씬의 기존 개인정보 게이트 보존·처리 중 상태·명시적 재진입에 한정됩니다. 운영 Managers/LocalAgeChoice/게임 UI, 연령 거절 후 cold restart 복구, 전체 지역별 동의 정책, No Ads 전체 경로, 미국의 중단된 시험을 검증하지 않았습니다. 기존 Google 인증 화면의 인과관계나 모든 네이티브 통신이 없음을 입증한 것도 아닙니다. 원 SDK 해시·publisher ID·기기 식별자·서명 원문·계정 정보는 비공개 근거에만 보관합니다.
