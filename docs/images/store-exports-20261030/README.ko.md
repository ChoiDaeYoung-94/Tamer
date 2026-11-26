# Google Play 규격 내보내기 — 2026-10-30

사용자가 일반 이미지 도구로 **디자인을 유지하고 크기만 변환**하도록 직접 선택했습니다. System.Drawing의 HighQualityBicubic/TileFlipXY로 전체 원본을 리사이즈했으며 크롭·새 그리기·AI 재생성은 하지 않았습니다. 원본 파일은 해시가 그대로입니다. 앱의 Assets·adaptive icon/PlayerSettings와 AAB28도 변경하지 않았습니다.

| 파일 | 실제 형식·규격 | 크기 | SHA-256 |
| --- | --- | --- | --- |
| [Play 아이콘](monster-tamer-play-icon-512.png) | PNG512×512 /8bit RGBA·32bit | 581,518bytes | `45363a1523a27f44c6c21b1950a628a371f46d4837c1c88de0d1c69ba9b1c175` |
| [기능 그래픽](monster-tamer-feature-1024x500.png) | PNG1024×500 /8bit RGB·24bit·alpha 없음 | 1,279,103bytes | `3a1e51258c587c2f96de699ccad8b4f8f6084b313b480646e698b138c056bdc0` |

아이콘은1024KB 이하입니다. PNG header와 실제 재열기·시각 검토에서 규격/형식·제목/캐릭터·선명도를 확인했습니다. 기술 내보내기 완료이며 Console 업로드/저장·게시·최종 디자인/제공 경험의 승인은 별도입니다.

## 원본·제작 경로

- 아이콘: [1254px 기본 원본](../../../Assets/Art/Branding/app-icon-facelift-20261029.png), [AI 제작 도구·전체 프롬프트](../facelift-assets-20261029.ko.md).
- 기능 그래픽: [1794×876 홍보 초안](../monster-tamer-feature-draft-20261030.png), [참조·전체 프롬프트](../store-feature-draft-20261030.ko.md).

기능 그래픽은 **AI 홍보 일러스트이며 실제 게임 스크린샷이 아닙니다.** 이미지의 제작 출처는 원본과 같고 이번 작업은 크기·출력 픽셀 형식만 변환한 것입니다. 실제 게임 스크린샷은 최종 제공판에서 별도로 준비합니다. [스토어 소개·준비 범위](../../revival/store-listing-facelift-20261030.ko.md)

규격 근거: [Google Play 공식 미리보기 자료 안내](https://support.google.com/googleplay/android-developer/answer/9866151?hl=en). 게임 로그인·구매/No Ads·광고·정책/심사 검증을 이미지 규격 검사로 대신하지 않습니다.
