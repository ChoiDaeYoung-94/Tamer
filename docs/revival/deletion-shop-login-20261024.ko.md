# 삭제 접수 후 Android 재시도 문구·새 프로필 상점 관측 — 2026-10-24

로그인 중복 안내 수정(PR #420) 이후의 실제 Android 컴파일·UI 객체 상태를 확인하고, [이전 삭제 lifecycle](deletion-gameplay-lifecycle-20261024.ko.md)에서 미검증이던 C 프로필의 상점 내부 표시를 관측했습니다. 기존 v3 앱·데이터·APK는 보존하고 별도 v4 앱 ID와 출력 파일을 사용했습니다.

| 대상 | 정확한 근거 |
| --- | --- |
| checkout/source | `D:/codex/worktrees/tamer-working/Tamer` / `3f7fc64f495979adc09b869b59814749812697cd` |
| 도구 | Unity6000.3.25f1/revision e1dba0a9aba4, CLI1.0.0-beta.8, Android min25/target36/ARM64, IL2CPP |
| APK | `Build/revival/Tamer-deletiongameplay-v4.apk`,110752919bytes |
| SHA-256 | `8b4cd02416154dca106665f42349b064f4d52fdc97c89c5982ce053763836578` |
| 앱/서명 | `com.AeDeong.MonsterTamer.revival.deletiongameplayv4` / Android debug signing |
| 정적 격리 | INTERNET/네트워크 상태/BILLING/GMS AD_ID/AdServices3개 권한 및 PGS/AdMob 초기화 provider 부재. allowBackup=false, 실제APK cloud-backup/device-transfer 각각9domain 전체 제외 |
| 설치/환경 | LD플레이어 `Tamer`, Android14/API34/x86_64·ARM64 번역/4KB. 새 전용 package 설치, 설치 base APK SHA가 로컬 파일과 일치 |
| 최소 검사 | 같은 source의 RevivalGameplayIsolationTests37/37 PASS(0.131초). Android 빌드 exit0. 복구 뒤 출시 소스 사전검사12/12 PASS, releaseReady=false |
| 원본 보호 | Editor 검사와 Android 빌드 각각59개 before/after 보존·변경 대조·원본59개 byte 일치·clean/소유 Editor 종료. 시험2개/빌드8개 비의도 변경 복구. 다른 worktree·Library 삭제0 |

## 실제 실행

1. PID6629의 가상 A 프로필에서 원래 설정·계정/개인정보·삭제 요청·확인 uGUI 이벤트를 개발 명령으로 호출했습니다. 접수 안내·소유 로컬 정리·옛 callback/쓰기 차단을 확인했습니다. 원래 Exit 이벤트 후6629가 소멸했고 이 경계에는 force-stop을 사용하지 않았습니다.
2. 새 PID7090의 실제 Login 재시도 패널에서 안내 본문1개·Retry 캡션·기존 네트워크 문구 부재·loading 숨김·font30·TMP overflow=false를 관측했습니다. 본문은 기존 TMP_Contents이며 버튼의 자식이 아닙니다. 자동 인증·읽기·쓰기0을 확인했습니다.
3. 원래 Retry 이벤트를 명시적으로 호출한 뒤 가상 C 프로필의 Gold41/닉네임·빈 도감/장비/아이템·비활성 장비 모델·상점 보유0과 HUD 일치를 확인했습니다. C는 로컬에 준비한 합성 프로필이며 실제 PlayFab 신규 회원 생성이 아닙니다.
4. 개발 명령으로 실제 Player collider를 원래 ShopMan trigger handler에 전달하고 원래 카테고리 열기를 호출했습니다. 활성 상품4개가 모두 잠김·미장착이며 원래 `Item`의 이름/가격과 UI 문구가 정확히 일치했습니다. 구매는 실행하지 않았습니다. 다른 B inventory와 소유 불명 legacy 원본의 hash 보존도 확인했습니다.
5. 두 PID 원시 로그와 해당 전용 앱 fixture 파일을 비공개로 보존한 뒤 terminal 상태의 v4 앱만 중지했습니다. 데이터 삭제·앱 제거·다른 인스턴스 조작은 없었습니다.

각 PID의 Unity Error 로그3줄은 인터넷 권한을 제거한 개발 앱의 정확한 소켓 진단2종에 해당했습니다. 원시 로그를 보존하고 격리 정책의 인식 범위를 대조했으며, **예상 밖 Unity 오류0·Unity 경고0**으로 구분합니다. 소켓 진단까지 포함해 원시 Error가0이었다고 쓰지 않습니다. v4 제품 관측의 실패·재시도는 없었습니다. 앞선 v2/v3 설치·기준 시점 실패 이력은 그대로 보존합니다.

이는 프로그램을 통한 원래 uGUI/trigger 경로와 실제 활성 UI 객체·TMP 값 관측입니다. native 터치·Android 화면 픽셀의 시각 검수·실제 계정 인증/삭제·Store/No Ads 구매 복원·최종 운영 AAB·실ARM64/16KB·스토어 심사 통과로 확대하지 않습니다. 운영 광고·지역/연령·개인정보 승인 false/null과 CI/CD 보류는 유지합니다.
