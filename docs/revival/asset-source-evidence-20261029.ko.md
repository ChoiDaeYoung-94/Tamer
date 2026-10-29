# 복원 도구 개별 파일의 공개 원문 대조 — 2026-10-29

기존 #92의 출처 증빙 후속입니다. 실제 작업본 source `eba77a66f75122f0c0c539ed0d5b006c73d28253`에서 비공개 복원물4개를 읽어 고정 공개 원문과 바이트 단위로 대조했고, 각 SHA-256·크기는 기존 [복원 manifest](assets-manifest.json)의 항목과도 일치했습니다. 해당4개 파일의 동일성 근거이며 전체 유료 에셋의 취득/이용 권한 완료가 아닙니다.

| 대상 | 고정한 공개 원문 | 대조 결과와 범위 |
| --- | --- | --- |
| Keystore Helper 코드2개 | megahard/KeystoreHelper commit `39309c253fc5f7c5b9eab22a09831c8525a021eb`, tree `71ce888bb6087f754199c78a65d7d565c8b5b517` | `Editor/AndroidKeystoreLoader.cs`617bytes와 `Editor/KeystoreHelper.cs`2,916bytes가 각각 byte 동일 |
| Keystore Helper README | 같은 commit의 `Assets/Keystore Helper/README.pdf` | 115,350bytes byte 동일. 위 코드/문서3개 외 모든 포함 파일·취득 주체까지 확인한 결과는 아님 |
| MiniJSON | darktable/1411710 gist revision `d7c8c5fd25d86031e52883f4e69c31234b5e735c` | `MiniJSON.cs`18,161bytes byte 동일. 로컬/원문에 Calvin Rien(2013) 저작권·MIT 허가 문구가 포함돼 있음 |

Keystore Helper의 고정 원문 저장소 [LICENSE](https://github.com/megahard/KeystoreHelper/blob/39309c253fc5f7c5b9eab22a09831c8525a021eb/LICENSE)는 Apache-2.0이며 GitHub license 응답과 blob `8dada3edaf50dbc082c9a125058f25def75e625a`를 대조했습니다. [원문 파일 목록](https://github.com/megahard/KeystoreHelper/tree/39309c253fc5f7c5b9eab22a09831c8525a021eb/Assets/Keystore%20Helper)과 [MiniJSON 고정 revision](https://gist.github.com/darktable/1411710/d7c8c5fd25d86031e52883f4e69c31234b5e735c)을 근거로 보존합니다. 저작권/허가문을 제거하지 않았습니다.

## SafeArea는 공급자 후보만 연결

로컬 `SafeArea.cs`의 namespace Crystal와 주석의 forum569236 링크를 출처 단서로 읽었습니다. [공식 Unity Asset Store의 Safe Area Helper130488](https://assetstore.unity.com/packages/tools/gui/safe-area-helper-130488)은 Crystal Pug·현재1.0.8/2026-04-14·무료·Standard Unity Asset Store EULA/Extension Asset로 표시됐습니다. 이는 현재 상품 정보이며 로컬 파일의 정확 취득 버전·취득 주체·원문 byte 동일성이나 그 시점의 이용 허가를 증명하지 않습니다. 원문 forum 접근은 실패했고, 제3자 코드 재게시물은 허가 근거로 사용하지 않았습니다. 로컬 SafeArea의 기존 manifest SHA 일치만 확인하고 파일/권한은 미확인으로 유지합니다.

## 보존과 남은 범위

원문 대조는 기존 로컬 파일·meta/GUID·manifest를 변경하지 않는 읽기입니다. `private-restore`/`unverified-do-not-redistribute` 분류와 ignore 규칙은 그대로이며 소스나 README PDF를 공개 Git/LFS로 옮기지 않았습니다. 도구 코드를 실행하거나 실제 키/EditorPrefs·계정·비밀값을 읽지 않았고 Unity·SDK·기기/빌드 시험도 하지 않았습니다.

나머지 구매 에셋·Avalon 등의 취득 주체·정확 상품/버전·허가와 비공개 아카이브 운영 후속은 [출처 인덱스](asset-source-index-20261028.ko.md)·[의존 에셋 관리](dependencies.ko.md)에 남깁니다. 이 대조와 제한 메일 검색0건만으로 #92를 닫거나 새 구매/출시 차단을 추가하지 않습니다.
