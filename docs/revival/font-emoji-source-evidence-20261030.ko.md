# EmojiOne·Avalon 출처 동일성 후속 — 2026-10-30

기존 #92의 복원 에셋 후속입니다. 실제 구현 작업본 `D:/codex/worktrees/tamer-working/Tamer@973ddea90346c6a731a96ccf04f29451ddce7458`의 파일을 읽었습니다. 이 문서는 별도 문서 작업본에서 main `5abdc330099cd3262bef72861d1e0c87c9b0b6c1`을 기준으로 작성했고 대상 manifest/기존 출처 문서는 두 소스에서 같았습니다. **파일 출처 동일성 확인이며 전체 이용·재배포 권한 완료가 아닙니다.**

## EmojiOne — 현재 해석된 Unity 패키지와4파일 일치

현재 승인 Unity6000.3.25f1 프로젝트의 lock은 `com.unity.ugui`2.0.0/builtin입니다. 해당 로컬 PackageCache의 `TMP Essential Resources.unitypackage`를 설치/해제하지 않고 읽었으며 archive SHA-256은 `58564ec64cef8a8cea3d0b8ffb5e6fecf4176d544a2f6a29a2b43a0a33256431`입니다.

| 복원물 | 바이트 수 | 패키지 내 같은 이름 파일과 대조 |
| --- | --- | --- |
| EmojiOne Attribution.txt | 185 | byte 동일·기존 manifest SHA/크기 일치 |
| EmojiOne.json | 3,966 | byte 동일·기존 manifest SHA/크기 일치 |
| EmojiOne.png | 112,319 | byte 동일·기존 manifest SHA/크기 일치 |
| Resources/Sprite Assets/EmojiOne.asset | 13,441 | byte 동일·기존 manifest SHA/크기 일치 |

이는 현재 설치에서 해석된 패키지 자료와의 일치이며 공개 업스트림 archive 전체의 공급 무결성을 새로 검증한 결과는 아닙니다. 동반 Attribution은 제공자 웹사이트에서 조건을 확인하도록 안내할 뿐, 그 파일 자체가 개별 emoji 버전/전체 허가문은 아닙니다. 제공자 링크 `emojione.com`은 이번 웹 도구에서 열리지 않았습니다. 제3자 재게시물로 허가를 대체하지 않았습니다.

동반 uGUI LICENSE는 Unity Companion License를 가리킵니다. [현재 공식 라이선스](https://unity.com/legal/licenses/unity-companion-license)의 제3자 권리 구분을 확인했으며, 이를 별도 EmojiOne 조건이 모두 해결됐다는 근거로 사용하지 않습니다. 기존 비공개 분류와 표시를 유지합니다.

## Avalon — 고정 PlayFab 공식 원문과4파일 일치

[공식 PlayFab UnityEditorExtensions](https://github.com/PlayFab/UnityEditorExtensions)의 archived 상태, commit `75e09bf93bd1f4ce7b887d304dae7f1449433084`와 tree `38f9ec12827c6e71cc669095700da9a2caedee18`을 확인했습니다. tree 응답은 truncated=false였습니다. [고정 Fonts 경로](https://github.com/PlayFab/UnityEditorExtensions/tree/75e09bf93bd1f4ce7b887d304dae7f1449433084/Source/Assets/PlayFabEditorExtensions/Editor/UI/Fonts)의 공개 blob을 메모리에서 읽어 로컬 복원물 및 기존 manifest와 대조했습니다. 새 폰트 사본은 저장하거나 공개하지 않았습니다.

| 파일 | 바이트 수 | 고정 blob | 결과 |
| --- | --- | --- | --- |
| Avalon Bold.ttf | 63,922 | `70cfe31d521228b780f5d889f2991f6d4d756653` | byte 동일·manifest 일치 |
| Avalon Bold.ttf.meta | 421 | `b92f5ae2a843f12c51c50d7f4dae14399c80aa9f` | byte 동일·manifest 일치 |
| Avalon.ttf | 51,288 | `a5e767b9e85b4169cf3a242d2f786172b912743d` | byte 동일·manifest 일치 |
| Avalon.ttf.meta | 421 | `1316245cbee9fcdaff099c0b9f995f733aa550b1` | byte 동일·manifest 일치 |

TTF 내부 이름 표기는 Regular의 Avalon/001.003·1992 Corel 저작권, Bold의 Avalon/Bold·URW T1 MAC/Fontographer 표기를 포함합니다. 공개 저장소의 Apache-2.0 표지만으로 두 폰트의 별도 권리·허가까지 확정하지 않았습니다. 실제 파일은 Editor/UI/Fonts에 있으며 이 경로 관찰을 최종 AAB 내부 부재 검사로 확대하지 않습니다.

## 보존·미검증

기존 에셋·meta/GUID·manifest·ignore·`private-restore`/`unverified-do-not-redistribute`를 변경하지 않았습니다. 원본 폰트·sprite PNG/JSON/asset·archive·원시 계정/구매/감사 로그는 공개 diff에 넣지 않았습니다. Editor·빌드·기기·키·설정/계정 조작은0입니다. 문서 링크·diff와 정확한 대상 파일/출처 SHA를 대조한 범위이며 실행 시험을 반복하지 않았습니다.

지난4개 도구 파일, Liberation 내부2파일과 이번8파일은 서로 다른 확인 범위입니다. 이를 전부 같은 종류의 업스트림/권한 완료 합계로 계산하지 않습니다. 유료 에셋의 취득 주체·정확 버전/권한, EmojiOne·Avalon 조건과 비공개 아카이브 운영은 [기존 출처 인덱스](asset-source-index-20261028.ko.md)·[관리 원칙](dependencies.ko.md)의 후속으로 유지하며 #92를 닫거나 새 구매/출시 차단을 추가하지 않습니다.
