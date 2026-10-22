# No Ads 저장 유지 확인 준비

2026년 10월 22일 시험 앱의 사용자 제공 화면에서 `Store: Ready`, `Receipt check: Verified`를 확인했습니다. 기존 구매 계정의 인증과 영수증 검증은 성공했지만, 화면에 저장된 No Ads 값이 없어 앱 재시작 후 보존 여부는 별도로 확인해야 합니다.

## 읽기 전용 관측

시험 패널에 다음 두 결과와 `Read saved No Ads (no Store call)` 버튼을 추가했습니다. 일반 게임 빌드에는 포함하지 않습니다.

- `Saved No Ads before reauthentication`: 기존 복구 프로필을 선택한 뒤 **세션 바인딩·동기화·저장 전에** 읽은 디스크 값입니다. 새 프로필은 `not checked`로 구분합니다.
- `No Ads local`: 인증된 현재 계정의 메모리·디스크 값과 저장 소유자 일치 여부입니다. 확인되지 않은 값은 `unavailable`로 표시합니다.

이 읽기는 구매·복원·서버 호출, 권한 지급, 파일 생성·수정·복구를 하지 않습니다. 정확한 `ProductNoAds` 토큰만 확인하고 다른 소유자·잘못된 JSON·연결 파일·누락·1MiB 초과 파일은 실패로 구분합니다. 계정 식별자·경로·JSON·예외 원문은 화면에 표시하지 않습니다. 읽기 결과를 새 권한 부여에 사용하지 않습니다.

## 검증과 한계

소스 커밋 `2427383fa211ae3bcba729ed473364f629e1a738`에서 `RevivalIapAccountRecoveryTests`를 Unity6000.3.25f1/Android 대상의 EditMode로 실행해 **25개 통과, 실패0, 건너뜀0**을 확인했습니다. 기존 계정 복구 시험과 새 읽기9개를 포함하며, 실제 Store/운영 계정 호출은 없습니다. 보호 파일53개와 Git clean을 확인했고 자동 직렬화 변경3개를 원내용으로 복구했습니다.

아래31번 첫 기기 결과에서 새 표시와 기존 저장값을 확인했습니다. 기존30번의 영수증 성공 화면과 구분하며, 추가 강제 종료·재시작 결과는 아직 확인하지 않았습니다. 이 변경은 아직 미병합인 복구 통합 후보에 의존하므로 해당 후보를 대상으로 한 별도 Draft PR로 관리합니다.

## 31번 시험 AAB 준비 완료

2026년10월22일 11:05:07~11:07:55 KST에 소스 `54ce975a67b65e4183ffa7b00820fc2f5b94d6d4`를 빌드하고 종료0을 확인했습니다. 코드 테스트 소스2427383 이후 차이는 위 검증 설명 문서뿐입니다.

- 시험 앱 전용 versionCode31/name1.0.5, Unity6000.3.25f1/min25/target36/ARM64/non-debug.
- 73,988,665bytes, SHA256 `1e49461d41791d26a05d669501e5cdae9a08e451ae773afceff02925d1d5f590`.
- bundletool·manifest·기존 시험 인증서/payload 서명·ELF LOAD6개·보수적 RELRO 배치 검사 통과. 과거 원시 strict RELRO 결과와 실제16KB·Play 전달 APK/split·폰 실행은 구분합니다.
- 복원 재개 define을 제외했고 생성 C++ getter=false를 확인했습니다. 실제 Store 실행 예산0이며 기존 원장을 초기화하지 않습니다.
- 빌드 후 원본51개·포트설정2개, GUID10개·임시 설정2개 부재·Git clean·Editor 종료를 확인했습니다. 새 키 생성·계정 연결/권한 변경·업로드는 하지 않았습니다.

로컬 전달 후 사용자가31번을 업로드하고 기존 앱을 업데이트·재인증했다고 알려주셨습니다. 첫 기기 관측 결과는 아래와 같으며 추가 강제 종료 후 유지 확인이 남아 `releaseReady=false`와 Draft를 유지합니다.

## 업데이트 후 첫 기기 관측

2026년10월22일 11:21에 사용자가 공유한 시험 앱 화면에서 다음 값을 확인했습니다.

```text
Saved No Ads before reauthentication: present
No Ads local: memory=present; disk=present; owner=matched
```

기존 복구 프로필의 No Ads가 세션 바인딩·동기화·저장 전에 이미 디스크에 있었고, 재인증 후 메모리·디스크도 일치했습니다. 새 저장값 읽기 표시가 실제 화면에 나타난 것도 확인했습니다. 앱 업데이트·프로필 재인증 후 기존 값이 남아 있다는 근거이며, 다음 강제 종료·재시작을 따로 실행한 근거는 아닙니다. 이 사진 자체에 설치 파일의 해시는 표시되지 않으므로 정확한 설치 해시까지 독립 확인했다고 표현하지 않습니다.

실제 Store 복원 성공은 이전30번 화면, 이후 저장값 보존은 이번31번 화면으로 구분합니다.31번의 실행 잠금을 유지하며 Store 복원을 다시 호출하거나 기존 원장을 초기화하지 않습니다.

## 데이터 삭제 후 관측과 변경된 안내

첫 저장 관측 후 사용자가 시험 앱 데이터·캐시를 삭제하고 기존 프로필 재인증을 다시 실행했다고 알려주셨습니다. 이어 공유한 화면에서 `Profile preparation stopped; all files preserved; restart requires reauthentication`, `Saved No Ads before reauthentication: not checked`, `No Ads local: unavailable (authenticate first)`, Store NotInitialized / Receipt NotStarted를 확인했습니다. 삭제 전 성공 관측은 유지하지만 이 결과를 강제 중지 후 저장 보존 성공이나 추가 영수증 실패로 계산하지 않습니다.

코드상 기존 프로필 재인증은 로컬 selection·ready·PlayerData 파일이 있어야 하며, 파일이 없는 경우 자동으로 새 프로필을 만들지 않고 중단합니다. 화면의 준비 중단은 이 보호 동작과 일치합니다. 예외 원문이나 실제 기기 파일을 수집한 결과는 아니므로 개별 파일의 부재를 직접 확인한 것으로 표현하지 않습니다.

현재 프로세스는 한 번 준비를 시도하면 재시도를 막습니다. 앱을 **강제 중지**한 후 다시 열고, 이전 계정 입력값을 사용하되 **Reauthenticate 체크를 해제**해 새 로컬 프로필 준비를 한 번 실행하는 것이 다음 단계입니다. PlayFab 로그인은 CreateAccount=false이며 새 서버 계정을 만드는 동작이 아닙니다. 추가 데이터 삭제·원장 삭제·새 결제·Store 복원을 요청하지 않습니다.

새 로컬 프로필은 스키마 기본값과 격리된 메모리 서버로 시작합니다. 기존 PlayFab No Ads 인벤토리는 준비 허용 조건으로 확인하지만 그 사실만으로 로컬 No Ads를 지급하지는 않습니다. 따라서 이 단계의 `not checked (new profile)` 또는 `memory=absent; disk=absent; owner=matched`는 새 기본 프로필의 관측이며 구매 권한 상실이나 복원 완료를 의미하지 않습니다. 다음 단계는 이 결과를 확인하고 로컬 권한 재반영 경로를 따로 정하는 것입니다. 기존 Store 단일 실행 성공·실패 이력과 실행 잠금은 유지합니다.

### 새 로컬 프로필 준비 결과

2026년10월22일11:38 사용자 제공 화면에서 `Existing account ready in a NEW blank local profile. STOP remains locked.`, `Saved No Ads before reauthentication: not checked (new profile)`, `No Ads local: memory=absent; disk=absent; owner=matched`를 확인했습니다. Store는 NotInitialized, Receipt check는 NotStarted입니다. 기존 서버 계정의 인증과 새 기본 로컬 프로필 준비는 완료됐으며, 로컬 No Ads 재반영과 이후 재시작 보존은 아직 완료되지 않았습니다.

이 결과는 인벤토리 소유 확인을 거쳐 새 프로필이 준비되는 실제 경로와 일치합니다. 새 프로필 준비를 No Ads 재지급이나 Store 복원 성공으로 계산하지 않습니다. 추가 인증·데이터 삭제·Store 버튼 반복 대신 현재 프로필을 보존하고 다음 복원 실행 경로를 준비합니다.31번의 Store 실행 잠금은 계속 유지합니다.

## 데이터 삭제 전 시험 절차 (과거 안내)

31번 시험 AAB를 준비할 때에는 복원 재개 define을 넣지 않고, 실제 산출물의 getter=false를 확인합니다. 기존 단일 실행 원장과 저장 파일을 유지하며 새 결제·복원 재실행은 하지 않습니다.

1. 사용자가 시험 앱 내부 테스트에 새 파일을 업로드·제공합니다.
2. 기존 시험 앱을 삭제하거나 데이터를 지우지 않고 업데이트합니다.
3. 기존 입력값을 사용하고 `Reauthenticate the previously committed recovery profile`을 체크해 인증합니다. 새 빈 프로필을 만들지 않습니다.
4. 재인증 전 디스크 `present`, 현재 `memory=present; disk=present; owner=matched`를 확인합니다.
5. 앱을 완전히 종료한 후 다시 열어 같은 기존 프로필로 재인증하고 같은 표시를 확인합니다. 이 두 단계에서 Store 호출 없이 보존을 검증합니다.

누락·소유자 불일치·읽기 실패가 나오면 원본을 보존해 진단합니다. 앱 삭제나 원장 초기화로 다시 시작하지 않습니다. 최종 게임·다른 계정·다른 기기·운영 출시·전체 개인정보·16KB 검증은 별도로 남아 있습니다.
