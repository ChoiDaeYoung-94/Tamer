# 둥근모꼴 TTF의 작성자 배포물 대조 — 2026-11-07

현재 한국어 UI가 참조하는 `Assets/Fonts/DungGeunMo.ttf`는 작성자의 [둥근모꼴+Fixedsys 배포 페이지](https://cactus.tistory.com/193)에 연결된 TTF ZIP 안의 파일과 크기·SHA-256이 같습니다. 원본 글꼴을 변경하거나 새 글꼴을 설치하지 않았습니다.

| 항목 | 확인 결과 |
| --- | --- |
| 로컬 TTF | 7,354,268bytes · SHA-256 `3f184d949149f2e7e6a853593e25161fb1d3b6d3708ebf79ce78600788fdea30` |
| 작성자 페이지의 TTF ZIP | 1,749,715bytes · SHA-256 `e937a8334da98f6918658626c23b9379bfbdb7e47be0dc829534377ad76fc937` |
| ZIP 내부 | `DungGeunMo.ttf` 1개, 로컬과 바이트 동일 |
| 내부 식별 | DungGeunMo / Version1.301 / 제조 Kil Hyung-jin / 디자이너 Kim Jung-tae·Darien Gavin Valentine |
| 내부 저작권 표기 | Public Domain |
| SDF의 원본 참조 | 기존 `DungGeunMo SDF.asset`의 source-font GUID가 해당 TTF의 기존meta GUID5240a5385a84b0d40a49f0f39d154b5b와 같음 |

작성자는 이 결합 글꼴을 원본과 같은 퍼블릭 도메인으로 안내하며 재배포·수정·영리 이용을 허용한다고 명시합니다. 이 확인은 위 SHA의 TTF와 그 작성자 배포·사용권 표기의 연결에 한정합니다. [작성자 사용권 안내](https://cactus.tistory.com/193)

Neo둥근모의 파일·버전·라이선스를 이 TTF에 대신 적용하지 않았습니다. Avalon·EmojiOne·LiberationSans와 유료 에셋의 출처/권한은 기존 개별 기록에 따라 구분하며, 이 TTF의 확인을 전체 에셋 권한 완료로 합산하지 않습니다. 원본 `assets-manifest.json`, 비공개 복원 분류, 기존 GUID와 폰트·재질 파일은 바꾸지 않았습니다.

확인 방법은 TTF의 OpenType name 테이블 읽기와 작성자 TTF ZIP의 메모리 내부 대조입니다. 글꼴 설치·교체·압축파일 경로 추출·Unity/빌드·기기 실행은 하지 않았습니다. 원시 다운로드/메타데이터 근거는 비공개로 보존하고 공개 문서에는 식별 결과와 작성자 원문 링크만 남깁니다.
