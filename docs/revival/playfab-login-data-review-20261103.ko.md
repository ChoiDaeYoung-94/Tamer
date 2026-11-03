# 손님 로그인·PlayFab 데이터 처리 대조 — 2026-11-03

## 읽기 결과와 범위

11월3일11:11~11:13 KST에 새로 연결된 지정 작업 Chrome의 기존 PlayFab 운영 타이틀을 읽었습니다. 작업 창은 열린 시각과 메일·Drive·GitHub·Console·PlayFab·AdMob·Photon 탭 구성을 대조했고, 다른 창/새 탭·로그인·비밀 키 화면·설정 저장·실제 게임 인증/계정 생성은0입니다. 전날 탭 선택 timeout과 달라진 연결 상태에서 읽었으며 이전 실패를 성공으로 바꾸지 않습니다.

| 항목 | 화면에서 읽은 현재 상태 | 판정 범위 |
| --- | --- | --- |
| CustomID 신규 플레이어 생성 비활성화 | 꺼짐 | 이 기능의 생성 차단은 켜져 있지 않음. 실제 생성 성공은 미검증 |
| AndroidDeviceID 신규 플레이어 생성 비활성화 | 꺼짐 | 기존 기기 계정/식별자 이전이나 새 생성 실행을 승인한 결과가 아님 |
| 클라이언트 API 전체 차단 | 꺼짐 | 개별 접근 정책/런타임 응답과 별도 |
| Client API 정책 | 와일드카드 허용, CustomID/PGS 로그인·게임 데이터 조회/수정·기기정보 보고 허용 | 미저장 변경 없음/저장 비활성. 허용 상태가 실제 전송/성공의 증거는 아님 |
| 집중 시간·기기정보 보고 | 둘 다 켜짐 | 아래 클라이언트 차단과 서버 요청 설정을 구분 |
| 전체 사용자 IP 저장·새 수집 기능 자동 실행 | 둘 다 켜짐 | 기존 유지 결정 보존. 공급자 IP 처리/향후 SDK 동작·신고 대조가 남음 |

운영 식별자·정책 원문·세부 화면 기록은 비공개 근거에 보존합니다. 위 읽기를 위해 서버 설정이나 계정/데이터를 바꾸지 않았습니다.

## 공식 문서와 현재 구현

Microsoft의 [CustomID Client API](https://learn.microsoft.com/en-us/rest/api/playfab/client/authentication/login-with-custom-id?view=playfab-rest)는 같은 ID로 로그인하고 `CreateAccount`에 따라 신규 계정을 만들 수 있는 계약과 생성 차단 오류를 설명합니다. [익명 로그인 안내](https://learn.microsoft.com/en-us/xbox/playfab/identity/player-identity/platform-specific-authentication/anonymous-login)는 신규 타이틀의 기본 생성 제한과 서버 측 생성 방식을 설명합니다. 현재 타이틀의 UI 상태를 읽었다는 사실만으로 이 일반 지침이나 런타임 오류 확인을 대신하지 않습니다. 기존 계정 보존과 [사용자 승인 손님 보완](mixed-audience-login-20261102.ko.md)의 범위를 유지하며, 서버 신설/인증 방식 교체·보호 설정 완화를 자동 실행하지 않습니다.

설치 SDK는 로그인 응답의 `SettingsForUser.GatherDeviceInfo/GatherFocusInfo`를 읽습니다. 기기 보고는 `DisableDeviceInfo`가 켜져 있으면 gatherer를 만들기 전에 반환하고, 이용시간 전송 coroutine 및 focus/quit 전달은 `DisableFocusTimeCollection`을 확인합니다. 게임은 일반 Player의 네트워크 대기/인증 전에 두 값을 켜며 성인이어도 선택적 보고를 다시 켜지 않습니다. 기존 기기 계정의 인증 요청에 명시된 식별자/OS·모델, 신규 손님의 무작위 ID, Google/다른 SDK 처리는 구분합니다.

`InitializeScreenTimeTracker`는 보고 차단 여부와 별개로 기기 종류/모델/OS·세션 정보의 메모리 큐를 구성할 수 있습니다. 따라서 클라이언트 보완은 선택적 보고 차단이며 **기기 안에서 어떤 정보도 읽지 않는다거나 모든 SDK 수집/통신이 없다**는 뜻이 아닙니다.

설치된 `PlayFabDeviceUtil` 전체 로그인 응답 처리·`ScreenTimeTracker` 전체 소스와 HTTP 초기화/주기/focus/quit 메서드를 가짜 신원·기기·전송 경계에 연결한 **추가10조건**을 통과했습니다. 보고 허용 응답·정적/instance API·등록·반복 로그인·entity 없음/서버 gather 꺼짐/다른 응답과 차단 없는 양성 대조를 확인했습니다. 보호가 켜진 경로에서는 보고 요청0이었지만 로컬 모델/OS 조회와 큐 생성은 남는 점도 검사했습니다. 최초 probe의 `UnityEngine` namespace 누락 CS0246은 실행0으로 끝나 보존했고, 가짜 경계의 import만 수정한 재시도에서 통과했습니다. 제품/SDK 소스 변경·실제 운영 SDK 호출·Editor/Java/Gradle/기기 실행은0입니다. 실제 반환은02:28:59.668 UTC/11:28:59.668 KST이며, 이 가짜 경계를 최종 제공판의 실제 패킷/정책 승인 근거로 확대하지 않습니다.

Microsoft [IP 처리 안내](https://learn.microsoft.com/en-us/xbox/playfab/player-progression/player-data/ip-bans)는 전체 IP 저장을 끄면 마지막 옥텟이 가려지는 범위를 설명합니다. 현재 화면은 켜짐이므로 위 두 클라이언트 flag로 전체 IP 보관까지 중지했다고 안내하지 않습니다. 해당 문서는 이 게임의 실제 보관 기간·로그 기록량·모든 삭제 완료 증거가 아닙니다.

## 심사·공개 안내 반영 조건

- 현재 수집10/공유6의 공식 Data safety 답안을 소스 flag나 이번 읽기만으로 줄이지 않습니다. 사용자 ID·세션/기기·IP/대략적 위치·필수/선택·공급자 목적을 최종 제공판과 대조합니다.
- 새 손님 ID는 광고 ID/Android ID로 만들지 않지만 서버 인증에 전송되는 계정 식별자입니다. Google 선택형 진입과 광고 연령/개인정보 선택은 별개입니다.
- 신규 손님 전체 기능·서버 저장/재시작·기존 계정/No Ads·미성년/미상 광고 차단·성인 UMP·삭제는 실제 최종 제공판에서 확인합니다. 운영 계정을 이번 읽기에서 생성하지 않았습니다.
- [공개 방침 초안](privacy-policy-release-draft.ko.md)과 [심사 안내 초안](guest-review-instructions-draft-20261102.ko.md)은 미적용 상태를 유지합니다. 최종 심사 전송·프로덕션 출시는 사용자 마지막 검토 뒤 별도입니다.

업로드 인증서 다운로드 이벤트는 새 연결에서1회/8초 timeout이었지만, 후속 파일 확인에서 **이번 클릭 차례 안에 생성·기록된 실제 공개 DER**을 찾았습니다. X.509로 파싱했고 개인키가 없으며, 기존 후보30 서명 때 검토한 DER/인증서 SHA와 바이트까지 같았습니다. 원본과 현재 읽기 근거를 비공개 보존해 현재 업로드 인증서 대조를 완료했습니다. 이벤트 timeout과 실제 파일 완료를 구분하며 같은 조건 반복·clipboard 복사·키 재설정/업그레이드0입니다. 화면의 디지털 애셋 링크 지문을 업로드 키로 대신 사용하지 않았습니다. 후속 [후보31 서명 준비](candidate31-preparation-20261102.ko.md)에서 실제 바탕화면 JDK·소스 검토·metadata와 빈 암호창 표시를 확인했으나 입력 예산 종료로 키 검사·새 AAB는 아직0입니다.
