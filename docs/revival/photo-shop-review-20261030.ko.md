# 최신 상점 화면 촬영 보조와 원본3개 — 2026-10-30

원래 상점 패널을 촬영 앱에서 직접 열 수 있는 버튼을 추가해 장비·동료·No Ads 탭의 원본 화면을 확보했습니다. 앞선 마을/설정/전투3개와 합쳐 검토용 원본은6개입니다. **상품 클릭/구매·운영 계정/저장·기존 No Ads 검증이나 최종 디자인 승인은 아닙니다.**

## 변경과 입력

`codex/photo-shop-review-1030`, 빌드 source`40ca9f53c75f038d9feed2ced3b0dfe7eb802574`입니다. `RevivalGameplayHarness`의 기존 `TAMER_GAMEPLAY_PHOTO` 조건 안에서 원래 ShopMan의 패널 참조/씬 소유권을 확인한 뒤 기존 근접 진입과 같은 패널 활성화만 수행합니다. 성공하면 진단 렌더링을 숨깁니다. 플레이어 위치·상품/가격·재화/구매 상태는 변경하지 않습니다. 전체 harness의 기존 운영 조건부 제외도 유지합니다.

긴 터치 드래그의 이동을 현재 Windows 조작 도구로 충분히 확인하지 못해 촬영 진입을 분리한 것입니다. **정상 NPC 접근 동선의 검증으로 계산하지 않습니다.** 기존 보조의 몬스터 생성·무적·포획 확률 고정도 사용하지 않았습니다.

| 항목 | 실제 결과 |
| --- | --- |
| 환경 | Unity6000.3.25f1/e1dba0a9aba4·CLI1.0.0-beta.8·Pipeline0.6.0-exp.1·min25/target36/ARM64 |
| 별도 앱 | com.AeDeong.MonsterTamer.revival.gameplay /29/1.0.5·Android debug 서명·debuggable=false |
| APK | 82,275,992bytes / SHA-256 `746764180082b61018221b35452f8e85815fecb9a779c6759e83fa97eeb8a236` |
| 검사 | 빌드1회 성공·컴파일 오류0·INTERNET/ACCESS_NETWORK_STATE/BILLING/AD_ID·GMA init provider 부재 |
| 복구 | 설정6개·보호6946파일 SHA/집합 일치·clean·원키 불변·관측 소유13프로세스 종료/Editor0 |

이전 촬영 APK/원본3개를 보존하고 현재 LD Tamer의 같은 촬영 패키지만 `install -r`로 한 번 갱신했습니다. 기존 보호6패키지의 버전/설치 경로·운영 앱 미실행, 데이터 초기화/자동 권한 부여 없음과 기존 사용자 연령 선택 뒤 추가 질문 없이 진입한 상태를 확인했습니다. 첫 ADB 사전 조회는 빌드 뒤 연결 부재로 중단돼 설치0이었고, 공식 LD instance1 연결 후 새 조건으로 설치1회가 성공했습니다. 다른 인스턴스·Play 보호 설정은 변경하지 않았습니다.

## 원본 PNG

| 원본 | 720×1280 크기 | SHA-256 |
| --- | --- | --- |
| shop.png | 120,170bytes | `fbdb82e94f9f9886e000a6a4e51b39ae511dea152181d5fc6f2fd0c80ec29ebb` |
| shop-allies.png | 166,669bytes | `1c347e390b804bf8e23605e6c5a086c50850243d3fb58c6aa1e2e9d97ee0155e` |
| shop-noads.png | 82,312bytes | `1e2d307a034b9b8532e76944b33190068f66338035036f8042e6657405f256d6` |

Android `screencap -p` 원본 바이트를 저장하고 시각 상태·검토 폴더 복사 SHA를 확인했습니다. 자르기·보정·크기 변환·AI 재생성은 없습니다. 장비/동료/No Ads 탭과 원래 닫기 버튼을 사용했고 상품 카드는 누르지 않았습니다. No Ads의 `Price shown in Google Play`는 오프라인 대체 안내이며 실제 Play 가격을 확인한 결과가 아닙니다. 합성 계정이며 화면/바이너리/로그는 비공개 보존하고 스토어에는 등록하지 않았습니다.

촬영 뒤 해당 앱만 종료하고 설치/자료·LD 인스턴스·다른 앱·소리 설정을 보존했습니다. 운영 AAB29/Google 서명 APK29·내부 임시보관함은 그대로이며 심사/배포/트랙 재개0입니다. 다음은 최종 사용자 UI 검토와 정상 Play 제공/운영 기능입니다. 사용자 즉시 할 일·질문은 없습니다. [앞선3화면과 검사](current-ui-photo-20261030.ko.md) · [전체 잔여](remaining-recovery-work.ko.md)
