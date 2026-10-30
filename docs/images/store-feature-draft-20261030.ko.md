# 스토어 홍보 이미지 초안 출처 — 2026-10-30

[monster-tamer-feature-draft-20261030.png](monster-tamer-feature-draft-20261030.png)는 내장 `image_gen.imagegen`으로 편집했습니다. 별도 API/CLI 모델·Python 래스터 편집·수동 리사이즈는 사용하지 않았습니다. 기존 파일을 덮어쓰지 않았고 앱의 Assets에도 추가하지 않았습니다.

- 편집 대상: [10월27일 홍보 일러스트](monster-tamer-hero-20261027.png).
- 캐릭터·색상 참조: [새 기본 아이콘](../../Assets/Art/Branding/app-icon-facelift-20261029.png).
- 요청:1024×500 opaque 기능 그래픽. **실제 결과는1794×876 /8bit RGB PNG /2,055,558bytes**입니다. 원본 비율은 유사하지만 규격 완료로 계산하지 않습니다.
- 용도: 사용자 디자인 검토·제출용 내보내기 전 초안. 실제 플레이·새 콘텐츠 제공의 증거가 아닙니다.
- 시각 확인: 제목 철자, 금빛 머리/검은 타원형 눈/푸른 스카프/붉은 망토, 청록색 눈의 박쥐를 확인했습니다. 기존 큰 보라색 용·분홍 머리 마법사·성은 제거됐습니다. 풍경·캐릭터 표현과 외곽 여백은 최종 제공 경험에 맞춰 사용자 검토가 필요합니다.

## 전체 프롬프트

10월30일 후속으로 사용자가 일반 도구의 크기 변환을 선택했습니다. [1024×500 기술 규격 파일](store-exports-20261030/README.ko.md)은 원본을 보존한 System.Drawing 리사이즈이며 새 AI 생성/크롭은 없습니다. 위1794×876 원본의 생성 프롬프트/출처는 그대로입니다.

> Use case: precise-object-edit. Asset type: draft Google Play feature graphic for the existing game Monster Tamer, an illustrated promotional banner rather than a gameplay screenshot. Image 1 is the edit target, the old wide promotional illustration. Image 2 is the strict current character identity and palette reference. Rework Image 1 into a clean wide feature graphic, intended for exactly 1024 by 500 pixels, opaque edge to edge. Preserve only the exact game title text "MONSTER TAMER" from the old title treatment, with crisp readable gold-and-stone lettering, and simplify the rest. Remove the giant purple dragon, pink-haired witch, and elaborate castle scene; those features are not established in the current game. Show ONLY the recognizable low-poly golden-spiky-haired hero from Image 2 and the small dark bat companion with cyan oval eyes. Use the icon's simple black oval eyes, tan face, royal-blue scarf and red cape, angular geometric 3D shapes and friendly determined expression. Show a medium full-body adventure pose rather than duplicating the app icon's close-up face. Set them in a simple stylized forest meadow/path with distant low-poly trees and warm gold sunlight, forest-teal and fresh green with cream/gold accents. Keep the title and characters inside a generous central safe area, background only at outer edges. Balanced horizontal composition: title on the left, hero and bat on the right, readable at small display size, minimal fine detail. Do not add other characters, monsters, weapons, gameplay UI, phone mockups, badges, prices, awards, calls to action, age claims, watermarks, or additional words. This is a non-destructive draft; keep character identity consistent with Image 2.
