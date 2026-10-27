# README 이미지 출처

## 홍보 일러스트

`monster-tamer-hero-20261027.png`는 2026-10-27 내장 `image_gen` 도구로 새로 생성한 README 소개용 일러스트입니다. 공개 게임 소개와 기존 타이틀의 캐릭터 구성을 바탕으로 제작했으며, 실제 플레이 캡처·구매 에셋 원본·출시 화면이 아닙니다. 캐릭터 의상·배경·용의 표현은 실제 제공판과 다를 수 있습니다. 기존 게임 화면은 아래 촬영 근거와 함께 별도로 유지합니다.

- 생성 방식: 내장 image generation, 신규 생성1회·후속 이미지 편집0회. 외부 CLI/API 키를 사용하지 않았습니다.
- 파일: 1774×887 PNG, 원본 생성 바이트 그대로 복사. SHA-256 `47a9d4d974055b564f18b51cc258b5da97480f9efe6fef4a8b7e86c84b2fb784`.
- [사용한 전체 제작 프롬프트](monster-tamer-hero-20261027.prompt.txt). 이 파일은 제작 이력이며 프로젝트 실행 지침이 아닙니다.
- 확인 범위: 이미지의 제목 철자·캐릭터·가로 구도·UI/스토어 배지 부재를 시각 확인했습니다. GitHub 자체의 모바일 렌더는 이 추가 작업에서 확인하지 않았습니다.

## 실제 게임 화면

아래 gameplay 이미지3개는 2026-09-14 사용자 요청에 따라 격리 게임을 직접 실행해 촬영한 화면입니다. 생성 이미지나 구매 에셋 원본이 아닙니다. 원본 PNG를 픽셀 편집 없이 보존하며 README 표시 폭만 지정했습니다. 이3개 파일은1080×2316이며 게임 HUD와 테스트 환경 표시가 포함됩니다. 이후 추가한 검증 이미지는 각 항목의 해상도·출처를 따릅니다.

촬영 소스는 `78b33c5f405724346326470851fb7a69ed523c38`, Unity 6000.0.81f1 photo 변형이며 APK SHA-256은 `6a21be0373be50c4d428776040a588d259716ddb39baf45481e627acb1925485`입니다. 회사 테스트폰의 별도 오프라인 게임 패키지에서 합성 계정을 사용했습니다. 원래 게임 HUD를 유지했으며 포획 화면에는 OfflineHarness 표시도 보입니다. 계정·업무 데이터·결제 화면은 포함하지 않습니다.

- gameplay-village.png: 동료가 없는 마을 화면.
- gameplay-capture.png: 포획 버튼이 표시된 필드 화면. 테스트 Bat 생성·무적·포획 가능 여부 고정으로 준비했습니다. 자연 확률이나 밸런스 검증이 아닙니다.
- gameplay-ally.png: 합성 골드 100을 사용해 원래 게임 상점에서 고용한 Bat가 따라오는 마을 화면. 앞 장면의 포획 결과가 유지된 것처럼 해석하지 않습니다.

| 파일 | SHA-256 |
| --- | --- |
| gameplay-ally.png | `eca08a14b7562842913800fadad3f73415b84f86098f2eb8099a206612c3d9c0` |
| gameplay-capture.png | `be99a88b8a28ba9ca00bb8339ade6b7115de8e916ef4a7de085cc6f3e332ac66` |
| gameplay-village.png | `5420dba3cb855bbcb268268e3c1e812456a70e927b8b8f7d3850bbc1cd986e74` |

기존 플레이 영상은 README의 원래 GitHub 첨부 주소를 보존했고 브라우저에서 로딩 이후 상점 장면까지 재생을 확인했습니다. README의 로컬 Markdown 미리보기를 데스크톱과 390px iframe 폭에서 렌더해 이미지·텍스트 배치를 확인했습니다. GitHub 자체의 모바일 렌더와 동일하다는 주장은 아닙니다.

공개 Google Play 링크는 기존 운영 주소입니다. 웹 조회에 페이지가 표시됐지만 해당 기기의 실제 설치 가능 여부와 복구 빌드 공개 배포는 확인하지 않았으므로 직접 APK 다운로드나 최신 배포 완료로 안내하지 않습니다. 기존 개인정보처리방침 접미부는 변경 전과 문자열 전체 일치를 확인했습니다.

- `privacy-product-ui-failed-20261024.png`: source896b163의 별도 오프라인 앱에서 원래 계정·개인정보 화면을 직접 캡처했습니다. 가짜 SDK adapter로 오류 안내와 실제 터치를 검증한 자료이며, 실제 운영 UMP/계정/스토어 검증으로 표현하지 않습니다. 원시 로그·식별자·계정 입력은 포함하지 않습니다.

- `privacy-product-real-ump-native-20261024.png`와 `privacy-product-real-ump-closed-20261024.png`: source0e3e802/debug APK SHA256 `18584f3e274db295ec3b2540e1813f36f634ed0c00ad3d02349d4f3294f572a7`의 LD Tamer 실제 캡처 원본입니다(720×1280). 개발용 합성 Adult 준비 이후 원래 게임 버튼으로 연 실제 UMP 창과 Do not consent 선택 후 원래 화면입니다. 합성 No Ads/Declined fixture이며 운영 최초 cold·영수증·정책 판정 자료는 아닙니다. 게임/SDK 앱 이름과 개발 로그 배지를 보존했고 계정 입력·등록 식별자·원시 로그는 포함하지 않습니다. [검증 조건](../revival/privacy-product-real-ump-20261024.ko.md).

- `gameplay-main-20261024.png`: sourcec6b4ba1/debug APK SHA256 `cf666b84aaa91948b8d0d4b7ab172f60ca09bd2eee0dd11763744d00e04c1ffc`의 실제 LD Tamer 첫 Main 원본입니다(720×1280). 합성 프로필·오프라인/IAP 차단 시험이며 디버그 overlay를 보존했습니다. 표시 카운터0과 별도의 전체 PID 로그 E73/W7을 구분하며 운영 계정·실폰/16KB·새 파일의 Game 왕복 검증으로 표현하지 않습니다. [검증 조건](../revival/gameplay-delivery-20261024.ko.md).

- `gameplay-roundtrip-20261024.png`: 위와 같은 정확한 APK의22:20 LD Tamer 왕복 완료 원본입니다(720×1280). PID3015의 화면 카운터0과 별도 전체 로그 E91/W7을 구분합니다. 합성 오프라인 씬 왕복이며 실제 폰·운영 저장/구매·16KB 검증은 아닙니다. [중단된 첫 시도와 성공 범위](../revival/gameplay-roundtrip-20261024.ko.md).
