# 복원 에셋의 출처 확인 범위 — 2026-10-28

원본 폴더·manifest·관련 README와 라이선스 표기로 출처 후보를 좁혔습니다. **패키지 이름·공개 저장소·부분 라이선스 확인은 구매 내역이나 모든 파일의 권한 확인을 뜻하지 않습니다.** 기존 `private-restore` 관리와 manifest의 원본 해시·GUID는 변경하지 않았고, 에셋을 공개 Git/LFS로 옮기지 않았습니다.

| 로컬 그룹 | 확인한 이름·출처 표기 | 남은 확인 |
| --- | --- | --- |
| `2D Casual UI` | 로컬 readme의 Mimu Studio 표기 | 정확 상품·취득 버전·주문/취득 주체 |
| `Animated Loading Icons` | 폴더명 확인 | 공급자·정확 상품·취득/권한 |
| `AxeyWorks` | 하위 `Low Poly v2` 표기 | 정식 상품/공급자·취득 버전·권한 |
| `Hovl Studio-Effects` | 하위 `Magic effects pack` 표기 | 정식 상품/취득 버전·권한 |
| `Monsters` | `Forest Bat`, `Magma Golem`, `RPGMonsterWave02PBR`, `Spider Toxin King Evolution Pack Cute series` | 단일 팩으로 합치지 않음. 각 공급자·취득 버전·권한 |
| `RPG Tiny Hero Duo`, `RPGPP_LT_MAP` | 각각의 폴더명 확인 | 정식 상품·공급자·취득/권한 |
| `Keystore Helper` | README의 `Android Keystore Helper`와 [megahard/KeystoreHelper](https://github.com/megahard/KeystoreHelper) 링크 | 연결된 공개 저장소의 [LICENSE](https://github.com/megahard/KeystoreHelper/blob/master/LICENSE)는 GitHub API에서 Apache-2.0으로 확인. 로컬 코드/버전과 그 저장소의 동일성은 미검증 |
| `TextMesh Pro` | 2016 User Guide 파일명과 아래 별도 폰트/스프라이트 표기 | 현재 TMP 전체·각 포함 에셋의 버전/권한을 한꺼번에 확정하지 않음 |
| `MiniJSON.cs` | 파일 선두의 Calvin Rien(2013) 저작권과 MIT 허가 문구 확인 | 해당 파일의 표기만 확인. 정확 취득 버전·변경 이력은 미확인이며 기존 비공개 분류 유지. 향후 관리 전환 시 저작권/허가문 보존 필요 |
| `SafeArea.cs` | 파일명 확인, 선두 허가 헤더는 확인하지 못함 | 원저작자·버전·권한. 공개되어 있는 유사 코드로 출처를 대체하지 않음 |
| Avalon 폰트 | manifest의 두 폰트 및 meta 항목 확인 | 별도 폰트 권한 미확인 유지. 다른 폰트의 라이선스로 대체하지 않음 |

TMP 하위 `LiberationSans - OFL.txt`에는 SIL Open Font License 1.1 표기가 있습니다. 해당 문서와 폰트 바이너리의 대조·완전성 검증은 하지 않았으며 전체 TMP나 Avalon의 허가로 확대하지 않습니다. `EmojiOne Attribution.txt`의 출처 표기도 개별 에셋의 라이선스 확인을 대신하지 않습니다.

구매·계정·버전의 권위 있는 증빙은 기존 [#92](https://github.com/ChoiDaeYoung-94/Tamer/issues/92)의 후속으로 남습니다. 확인 가능한 원구매 기록이 생기면 위 후보와 정확 파일·취득 주체를 대조합니다. 증빙 미확인만으로 새 구매를 요구하거나 원본을 삭제하지 않습니다. 복원·빌드 성공도 권한 확인으로 계산하지 않습니다.

개인 계정·구매 조회의 세부 근거와 원시 자료는 공개하지 않습니다. 이번 문서 점검에서 Unity·빌드·기기 시험은 하지 않았으며, 상세 복원/관리 원칙은 [의존 에셋 문서](dependencies.ko.md)를 따릅니다.

## 2026-10-29 제한 구매 증빙 후속

기존 운영 메일 계정 표시가 맞는지 확인하고 `2D Casual UI`, `Animated Loading Icons`, `RPG Tiny Hero Duo`, `Magic effects pack`과 구매 증빙 표현을 묶어1회 검색했습니다. 완료 UI는 일치 메일0건이며 본문 열기·첨부 다운로드·발신/초안·계정 변경은0입니다. 소유 검색 탭만 닫고 원래 메일과 Console 초안을 보존했습니다. 별도 value equality 확인은 false였으나 완료 DOM snapshot에는 계획한 검색문이 보였고, 그 불일치까지 비공개 관측에 남겼습니다. 동일 조건 재검색이나 다른 쿼리/페이지 순회는 하지 않았습니다. 이 계정/검색 범위의0건이며 다른 구매 계정·첨부에만 있는 상품명·무료 취득·정확 버전/파일 권한은 미확인입니다. 출시의 새 차단이나 새 구매 요구를 추가하지 않으며 #92는 미완료로 유지합니다. 개인 계정·주문·결제·무관 메일 원문은 공개하지 않았습니다.
