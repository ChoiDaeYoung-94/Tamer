# 페이스리프트 스토어 자료 검토안 — 2026-10-30

새 UI·아이콘에 맞춘 소개 문구와 홍보 이미지 초안입니다. Console에 입력·저장·게시하지 않았습니다. 실제 앱 화면, 사용자 디자인 검토와 최종 제공 범위를 대조한 뒤 확정합니다. 기준 구현은 통합 후보28의 source `f5d7ab42edb4386c9339be1f7be0af28bf267364`입니다.

## 한국어 문구

앱 이름은 기존 **Monster Tamer**를 유지합니다.

짧은 설명:

> 몬스터를 포획해 동료로 모으고, 장비를 갖춰 함께 싸우는 3D 모험 게임

전체 설명:

> Monster Tamer에서 몬스터와 함께 싸우는 사냥꾼이 되어 보세요.
>
> 필드를 이동하며 몬스터와 전투하고, 포획할 수 있는 몬스터를 동료로 맞이하세요. 모은 동료와 함께 다음 전투를 준비할 수 있습니다.
>
> 마을에서는 게임 속 골드로 장비와 동료를 구입하고, 캐릭터와 장비를 관리하세요. 사냥터 안내판에서 전투 지역으로 이동할 수 있습니다.
>
> 주요 특징
> - 조이스틱으로 이동하는 3D 전투
> - 몬스터 포획과 동료 전투
> - 게임 속 골드로 이용하는 장비·동료 상점
> - 캐릭터 선택과 장비 관리
>
> Google Play Games 로그인을 사용합니다. 앱 내 구매와 광고가 포함됩니다. 광고 제공 여부는 연령 선택, 개인정보 선택과 구매 권한 등에 따라 달라집니다.

## 영어 문구

짧은 설명:

> Capture monster allies, equip your hero, and fight together in a 3D adventure.

전체 설명:

> Become a hunter who fights alongside monsters in Monster Tamer.
>
> Move through the field, battle monsters, and capture eligible monsters as allies. Prepare for the next fight with the companions you collect.
>
> Back in the village, spend in-game gold on equipment and allies, and manage your character and gear. Use the hunting-ground sign to enter combat.
>
> Features
> - Joystick movement and 3D combat
> - Monster capture and ally battles
> - Equipment and ally shops using in-game gold
> - Character selection and equipment management
>
> Uses Google Play Games sign-in. Includes in-app purchases and ads. Ad availability depends on age choices, privacy choices, and purchase entitlements.

한국어 번역이 게임 전체에 제공된다는 뜻은 아닙니다. 멀티플레이, 무조건적인 포획·영구 동료 보장, 오프라인 운영, 모든 기기 간 장비 복원, 무료/광고 없음, 새 콘텐츠 수량·랭킹·수상·가격 할인은 약속하지 않습니다. 심사 접근 안내는 이 홍보 문구와 별도로 유지합니다.

## 이미지와 실제 화면

![새 아이콘의 주인공과 박쥐를 사용한 AI 홍보 일러스트 초안](../images/monster-tamer-feature-draft-20261030.png)

위 이미지는 **AI 홍보 일러스트이며 게임 스크린샷이 아닙니다.** 원본은 RGB PNG `1794×876` / `2,055,558 bytes`로, Play 기능 그래픽의 정확한 `1024×500` 제출 규격을 아직 충족하지 않습니다. 제목·캐릭터·색상 검토용이며 그대로 업로드하지 않습니다. 기존 아이콘의 얼굴 확대 구도를 반복하지 않고 주인공의 모험 동작과 박쥐를 사용했습니다. 실제 캐릭터·풍경의 표현 일치와 중앙 안전 영역은 최종 디자인 검토에 포함합니다. [출처·전체 프롬프트](../images/store-feature-draft-20261030.ko.md)

대체 텍스트 후보:

> 금빛 머리의 사냥꾼과 박쥐 동료가 숲길을 달리는 Monster Tamer 홍보 일러스트

> Monster Tamer illustration of a golden-haired hunter and bat companion running along a forest path.

| 제출 자료 | 현재 상태 | 남은 조건 |
| --- | --- | --- |
| Play 아이콘 | 새 기본 원본1254×1254 /1,665,432bytes, 실제 후보28 launcher192px 별도 확인 | 동일 원본의512×512·32bit PNG·1024KB 이하 제출 파일과 작은 크기 가독성 확인 |
| 기능 그래픽 | 위1794×876 RGB 초안 생성·시각 확인 | 사용자 디자인 검토와 정확1024×500 opaque JPEG/24bit PNG 내보내기 |
| 스크린샷 | 기존 자료는 구UI 또는 시험 표시 포함 | 최종 후보의 실제 주요 화면을 진단 표시 없이 촬영·검토 |
| 소개 문구 | 위 한국어·영어 초안과 소스 기능 대조 | 최종 제공판·언어·실제 로그인/광고 조건 및 사용자 의견 반영 |

실제 스크린샷은 마을/상점, 전투·포획, 동료와 장비 관리 순으로 준비합니다. 장식용 합성 화면이나 진단 표시를 지운 재구성 이미지를 실제 플레이 캡처로 제출하지 않습니다. 최종 후보·언어·기기/화면 크기·촬영 조건을 기록하고 운영 저장·계정을 유지합니다.

## 근거와 확인 범위

문서에서 실제 입력 문자열만 추출해 공백·줄바꿈을 포함한 길이를 확인했습니다. 한국어/영어 짧은 설명40/78자, 전체 설명370/677자, 대체 텍스트48/99자입니다. 앱 이름은13자로 유지합니다. 이는 길이·소스 기능 대조이며 Console 입력/승인 결과는 아닙니다.

`Player.cs`/`Monster.cs`의 이동·포획·동료 경로, `ShopMan.cs`의 골드/장비·동료 구매와 기존 격리 실제 UI 근거를 대조했습니다. 설명에 나오는 기능을 운영 후보에서 새로 전부 검증했다는 뜻은 아닙니다. [운영 후보28의 범위](candidate28-built-20261030.ko.md), [격리 UI 근거](ui-facelift-20261029.ko.md)를 구분합니다.

[Google Play 공식 미리보기 자료 안내](https://support.google.com/googleplay/android-developer/answer/9866151?hl=en)와 [앱 상세정보 입력 안내](https://support.google.com/googleplay/android-developer/answer/9859152?hl=en)를10월30일 확인했습니다. 짧은 설명80자, 전체 설명4000자 이내와 이미지 규격을 최종 입력 전에 다시 대조합니다. 문서/이미지 준비만으로 스토어 신고·정책·심사 통과를 의미하지 않습니다. 앱 실행 입력은 변경하지 않았으므로 이 자료 준비를 위한 재빌드는 하지 않습니다.
