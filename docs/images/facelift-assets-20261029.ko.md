# 페이스리프트 아이콘 출처와 프롬프트 — 2026-10-29

내장 `image_gen.imagegen` 도구로 생성/편집했습니다. 별도 API/CLI 모델은 사용하지 않았습니다. 참조는 저장소의 기존 `Assets/Art/Images/icon.png`와 실제 게임 화면 `docs/images/gameplay-capture.png`입니다. 최종 프로젝트 에셋은 아래와 같습니다.

- [기본 아이콘](../../Assets/Art/Branding/app-icon-facelift-20261029.png)
- [Android 적응형 투명 전경](../../Assets/Art/Branding/app-icon-adaptive-foreground-20261029.png)
- [Android 적응형 배경](../../Assets/Art/Branding/app-icon-adaptive-background-20261029.png): Unity Editor API로 만든 단색 `RGB(18,39,36)` 1×1 텍스처입니다.

## 기본 아이콘 프롬프트

> Use case: stylized-concept. Asset type: final square Android game app icon for Monster Tamer, no text. Primary request: facelift the existing game's icon into a polished, highly readable stylized low-poly fantasy game icon. Image 1 is the current icon and primary character-identity reference; Image 2 is actual gameplay and an art-direction reference only. Preserve the recognizable hero: very spiky golden hair, simple black oval eyes, warm tan skin, royal-blue scarf, red cape, friendly determined expression. Use a clean three-quarter close-up portrait of the hero, with a small dark bat monster companion with cyan eyes peeking beside the shoulder to communicate monster taming. Keep the original geometric 3D fantasy character identity, tasteful crisp polygonal facets and restrained soft lighting, excellent silhouette at 48 pixels. Background: simple deep forest-teal with a subtle warm gold light behind the hair, minimal detail. Composition: face and hair fill most of a square canvas, all essential character features comfortably within a centered circular Android mask safe area, generous outer breathing room. Opaque edge-to-edge square artwork. No words, letters, logo, watermark, UI, frame, border, or pre-rounded corners. Do not add unrelated armor, weapons, realistic skin, oversized anime eyes, or a new character identity.

## 투명 전경 편집과 최종 여백 프롬프트

기본 아이콘에서 배경을 제거한 중간 후보의 여백이 부족해, 아래 두 편집을 거친 최종 전경을 선택했습니다. 둘 다 `transparent_background=true`로 요청했습니다.

> Use case: identity-preserve. Asset type: Android adaptive app icon foreground, transparent PNG cutout. Edit the referenced Monster Tamer icon into a clean transparent foreground layer. Preserve exactly this recognizable stylized low-poly gold-spiky-haired hero, tan face, black oval eyes, blue scarf, red cape, and small dark bat companion with cyan eyes. Keep the same polished geometric 3D visual style and warm lighting. Remove the entire forest background, all sparkles, glows outside the silhouette, and any scenery. Reframe/zoom out so the COMPLETE hair silhouette and complete bat including wing tips are visible with generous transparent padding; all main subject pixels should comfortably fit inside the CENTER 62 percent of a square canvas for Android adaptive icon mask safety. Hero bust may end in a clean natural chest silhouette; no hard rectangular lower crop. Friendly determined expression. Do not change character design, add text, a logo, border, backplate, rounded square, extra objects, or a fake checkerboard. True transparent background.

> Precise composition edit ONLY. The attached transparent character cutout is too large for an Android adaptive icon. Keep the artwork exactly the same, do not redraw or change it. Place the ENTIRE existing cutout much SMALLER at the center of a square transparent canvas: the combined hero and bat must be only FIFTY PERCENT of the total canvas width and FIFTY PERCENT of its height. This means a very large empty transparent margin, roughly one quarter of the canvas on EACH of the four sides. Think of a small centered sticker floating in a large invisible square, NOT a full-bleed portrait. Preserve the full hero silhouette, all hair spikes, the complete bat and every wing tip. Absolutely no background, checkerboard, shadow, text, border or new detail. The main purpose of this edit is substantial visible empty padding for Android's icon mask.

투명 픽셀 경계와 플랫폼 레이어 연결을 Editor 검사로 확인했습니다. 실제 Android launcher의 표시 결과와 사용자의 디자인 검토는 별도입니다. 이 문서의 UI 그림은 원래 UI를 Editor에서 렌더링한 것이며 AI로 게임 화면을 재생성한 이미지가 아닙니다.
