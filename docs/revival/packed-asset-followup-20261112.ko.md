# 기존 빌드 보고서로 좁힌 에셋 후속 — 2026-11-12

새 시험/빌드 없이 source `1d09455`의 완료된 LD APK에 대응하는 `LastBuild.buildreport`를 기존 Unity `binary2text` 도구로 읽었습니다. 입력 SHA 전후 동일·변환 exit0·보고서 outputPath와 APK SHA 일치를 확인했습니다. 소유 파일을 읽고 별도 텍스트만 썼으며 Editor/JVM/ADB를 추가 실행하지 않았습니다.

17개 PackedAssets 구간의8,120개 직렬화 객체·이름 있는3,126개 경로를 확인했습니다. 객체 수는 고유 에셋 수가 아니며, packedSize는 APK 압축/다운로드 크기와 다릅니다.

## 구형 후처리 프로필4개

| 원본 | 이번 LD APK 보고서 | 남는 해석 |
| --- | --- | --- |
| Hovl `Resources/Settings/PP low.asset` | resources.assets에4개 객체/합계176bytes | 활성6개 씬의 직렬화 참조는0이지만 Resources 포함은 실제로 확인됨. 모두 미사용/미포함이라고 선언하지 않음 |
| Forest Bat `Forest Creatures PPP.asset` | 해당 경로의 포함 객체0 | 이번 산출물의 미포함 근거이며 임의의 다른 빌드/외부 bundle까지 확대하지 않음 |
| Magma Golem `Magma.asset` | 포함 객체0 | 위와 같음 |
| Cute Series `Cute Series.asset` | 포함 객체0 | 위와 같음 |

별도 직렬화 역참조 조사에서도4개 모두 직접 참조와 활성 씬 참조가0이었습니다. Hovl의 Resources 경로는 동적 사용 가능성을 단순 참조0으로 배제할 수 없어 구분했습니다. 원본/meta/GUID와 복원 manifest를 보존했고, 구형 후처리 패키지 설치·원본 삭제·현재 값을 정답으로 한 manifest 재생성은 하지 않았습니다. 기존 main asset 로드 불가4개와 Hovl 누락 스크립트 경고는 알려진 제한입니다. 실제 사용/제외 처리가 끝난 것으로 닫지 않습니다.

## 권리 증빙의 확인 대상

복원 manifest의 `private-restore`와 겹치는 포함 경로168개를 별도 비공개 목록으로 추렸습니다. 이 중에는 스크립트/폰트/스프라이트도 있어168개가 유료 상품168개라는 뜻은 아닙니다. 원본 파일의 manifest SHA는168개 모두 일치했으나 이는 구매·이용 권한 증명이 아닙니다.

포함 그룹은2D Casual UI, Animated Loading Icons, AxeyWorks, Hovl 효과, Monsters, RPG Tiny Hero Duo, RPGPP_LT_MAP, 기존 TMP 자료·스프라이트와 일부 기존 스크립트입니다. 각 상품/취득 버전/취득 주체는 기존 [출처 인덱스](asset-source-index-20261028.ko.md)와 개별 증빙에 연결합니다. 공개 폰트는 [DungGeunMo의 별도 근거](dung-font-source-20261107.ko.md)를 유지합니다.

Avalon 이름의 경로와 로컬 meta GUID는 이번 PackedAssets에서 발견되지 않았습니다. 이를 Avalon 전체 권리 해결이나 모든 운영 산출물의 미포함으로 확대하지 않습니다. 사용권 미확인 자료와 원시 목록·로그를 공개 Git/LFS로 옮기지 않았습니다.

## 백업의 이번 완료와 남은 부분

현재 도달 가능한 Git 참조155개의 비공개 bundle을 만들고, 독립 mirror 복원·`git fsck --full --no-reflogs`·전체 ref/OID 일치를 확인했습니다. 이 Git 이력에는 과거 추적된 기존 키와 에셋이 포함될 수 있어 비공개로 다룹니다. 현재 미추적 비공개 에셋 스냅샷·새 업로드 키·관리 문서는 별도 보존 대상입니다. bundle이 이 모두를 포함한다고 표현하지 않습니다.

외부 백업 대상과 키/일반 자료 분리 안내는 지정 사용자 보관함에 정리했습니다. 기존 비공개 에셋 스냅샷4,204파일의 복원 성공과 새 Git bundle의 복원 성공을 구분하고, 이번 Google Drive 업로드/공유는0입니다. 전체 외부 복구/권리 증빙 완료는 남아 있습니다.
