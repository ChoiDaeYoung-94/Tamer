# 게임플레이 후속 감사

전체 복구의 코드 감사에서 발견한 오류를 처리한다. 저장 키, enum 값, 기존 MonoBehaviour 이름과 serialized 필드, `.meta`/GUID를 보존한다. 운영 로그인·저장·구매·광고는 실행하지 않는다.

| 파일 | 문제와 처리 | 회귀 범위 |
| --- | --- | --- |
| Creatures/Player.cs | 이미 수집한 몬스터를 다시 처리하면 SavePrefs가 빈 문자열을 반환해 다음 저장의 기존 목록 접두사가 사라졌다. 변경 없는 추가/삭제는 기존 문자열을 반환한다. Gold도 다른 singleton이 아닌 조회한 Player의 값을 반환한다. | 중복 추가·없는 항목 삭제의 반환값/목록/쓰기 없음, 두 Player의 Gold 구분 |
| Creatures/Monster.cs | 죽음 또는 비활성 NavMesh에서 OR 조건 때문에 AI가 계속 실행됐다. 살아 있고 활성 NavMesh에 배치된 경우로 제한한다. 탐지 재시작은 이전 CTS를 취소·폐기한 뒤 새 소유자로 교체한다. | 죽음/비활성 상태에서 이동 접근 차단, 반복 탐지 시작과 Clear 취소 |
| Game/MonsterGenerator.cs | 그룹 반환이 active 목록 여러 항목을 삭제하는 동안 수동 index 보정이 다음 commander를 건너뛸 수 있었다. active/follower 목록 snapshot을 순회한다. | 소스 검토와 기존 생성 수/수명 회귀. 실제 화면 밖 그룹 회수 시각 검증은 별도 |
| Creatures/ShopMan.cs | 성공 문구가 구매 확인 상태를 대신하고 중복 확인이 다시 지급/차감할 수 있었다. 확인 가능 상태를 한 번 소비하고 당시 잔액·가격·이미 보유한 장비를 재검사한다. 실패 결과 닫기는 유지한다. SaveItem의 중복 추가도 막는다. | 확인 한 번 소비, 잔액 부족 시 재시도 차단. 실제 상점 화면은 별도 |
| Main/Item.cs, Main/IAPItem.cs | 등록한 ShopMan 목록에서 파괴된 항목이 남았다. 등록받은 ShopMan을 캡처해 파괴 시 제거한다. Item lock 표시도 현재 해금 상태를 반영한다. | 동일 목록 반복 해제, 실제 상점 레이아웃은 별도 |
| MiniMap/FogOfWar/FogOfWarRenderer.cs | 매 프레임 Texture2D 두 개를 생성하고 해제하지 않았다. 두 텍스처와 픽셀 버퍼를 재사용하며 종료 시 자체 texture/material/RenderTexture/plane을 정리한다. 임시 RT는 finally에서 반환한다. | 동일 texture 재사용, 변경된 셀의 실제 픽셀, 반복 종료 후 native texture 해제 |
| MiniMap/MiniMap.cs, MiniMapCanvas.cs | 구독 해제와 닫기에서 새 singleton을 조회하던 경계를 캡처한 소유자로 변경한다. 지도에서 시간 정지 후 씬이 닫히면 이전 timeScale을 복구한다. | publisher 교체·해제, 반복 pause/disable과 새 nonzero timeScale 보존 |
| Cameras/CameraManage.cs, Creatures/ShopMan.cs, Creatures/BuffingMan.cs, Game/Portal.cs, MiniMap/MiniMap.cs | 이전 객체 파괴가 새 singleton을 지우지 않도록 자기 소유일 때만 정리한다. | 이전/현재 객체 종료 순서 |

미니맵의 timeScale 처리는 자신이 설정한 0을 복구하는 제한된 수명 처리다. 여러 시스템이 서로 다른 0 pause를 중첩해서 소유하는 전역 pause 관리까지 보장하지 않는다. UI·카메라·실제 몬스터 이동과 상점의 Main/Game 왕복은 EditMode 회귀만으로 검증했다고 주장하지 않는다.

독립 소스 리뷰: `44a3caa`, `aa12c07`, `b4425fb`에서 확정 P1/P2 없음. `a57a3ca`는 Gold 소유 조회 한 줄과 그 회귀 추가다. 최종 통합 Unity 결과는 별도 구조화 검증 기록에 실행 소스와 함께 기록한다. 이 문서 작성 시점에는 후속 Unity 실행 대기다.
