# 광고 보상 후 복귀 검증과 운영 설정 읽기 감사

2026-09-14, `codex/families-ad-followup`, 빌드 소스 `731d9cfa3fe66960b2d8096b04295e4b220a16b6`, 실제 checkout `C:/Users/pc_17/.codex/worktrees/e5e5/Tamer`. PR155까지 포함한 main에서 실행했다. 런타임 코드는 변경하지 않았다. 이전 [UMP 검증](families-consent-gate.ko.md)에 없던 **earned 이후 Home 및 기존 task 복귀** 경계를 실제 공식 sample 광고로 검증했다.

## 격리 APK와 실행 조건

Unity `6000.0.81f1`, CLI `1.0.0-beta.8`, GMA Unity `11.5.0`/표준 Android `25.4.0`, UMP `4.0.0`, Android Build Tools `36.0.0`. 새 개인 기기는 Android16/API36, ARM64, 페이지4096 bytes이며 16KB 기기 증거가 아니다. 현재 사용자 프로필에서 두 테스트 패키지가 없는 것을 확인한 뒤 설치했다. 기존 앱·데이터·계정·네트워크 설정은 보존했다.

| 산출물 | bytes | SHA-256 |
|---|---:|---|
| Tamer-ads-sample.apk | 93,327,744 | `dee64dee54daf616956bfccfb706f959e03e3074572d9df8fe52ca6e7673e79b` |
| Tamer-ads-control.apk | 63,665,261 | `cbfc11cbe1dfee41eed6cad606caca8571139a83714ab057c481e94e750984bb` |

`Run-AdHarness.ps1 -Variant sample/control`을 직렬 실행했다. 두 APK는 debug 서명, 별도 `.revival.ads`/`.revival.adscontrol` package, min24/target36, version1.0.5/code26, ARM64다. merged manifest의 공식 sample App ID를 verifier로 확인했다. sample만 debuggable이며 control은 일반 release 요청 정책을 실행한다. 두 APK 모두 MobileAdsInitProvider와 GMS AD_ID 권한이 남아 있다. managed 호출 부재를 SDK 전체 통신·정보 접근 부재로 확대하지 않는다.

실행 씬은 계정·저장·결제가 없는 기존 allowlist harness다. 운영 광고 단위는 요청하지 않았다. [Google 공식 안내](https://developers.google.com/admob/android/test-ads)에 따르면 demo 광고 단위는 게시자 계정과 연결되지 않고 Google 광고만 제공하므로 운영 미디에이션 검증을 대신하지 않는다.

## sample: 보상 콜백 후 Home/복귀

12:07–12:11 KST의 동일 프로세스/owner1/request1을 관찰했다. 다음 수치는 하네스 Stopwatch 기반 **단조 시간, 초 단위**다. callback_monotonic 값은 callback 진입 시각이며 화면에 픽셀이 나타난 시각이 아니다.

| 순서 | 시간 | 결과 |
|---|---:|---|
| 부팅 | 48393.240 | can_request=true, managed_sdk_idle=true, 보상0/완료0 |
| 명시적 Load | 48424.267 | consent Update 호출 |
| 동의 요청 허용 | 48424.863 | update→필요 폼 처리→allowed. 실제 동의 폼은 나타나지 않음 |
| 별도 광고 요청 태그/Initialize | 48424.876 | 아동/동의 연령 미만 true, 최대 등급 G 코드 경로 |
| Load 성공 | 48427.523 | 공식 sample 로드 완료 |
| Show/opened | 48508.460 / 48508.499 | 수락1회, BGM false |
| earned | 48516.040 | 보상 자격 callback 도착. 아직 closed/보상 지급/완료 없음 |
| Home pause | 48517.161 | 자동 관찰기가 earned 로그를 확인한 후 Home 입력. BGM false |
| 기존 task 복귀 | 48603.337 | 최근 앱 화면에서 해당 광고 task를 직접 선택. 약86.176초 background. native 광고가 그대로 표시되며 ‘테스트 광고’와 보상 획득 표시를 확인 |
| 원래 X 닫기 | 48620.411 | native closed callback |
| 보상/완료 | 48620.420 / 48620.422 | reward count1, finished1, Rewarded |

닫기 후 화면은 보상1/완료1/BGM playing=true였다. native 광고 표시 중에는 원래 BGM sample38807이 정지됐고 닫힌 뒤 sample39043을 관찰했다. 이는 AudioSource 상태 확인이며 청각적 청취 검증은 아니다. Home 입력으로 자동 닫혔다고 기록하지 않는다. 가짜 earned/closed callback을 주입하지 않았고 설치 광고 버튼은 누르지 않았다. 이번 프로세스의 수집 로그에서 Unity Error와 게임 C# 예외는 관찰하지 않았다.

## control: 일반 release 정책 차단

control 부팅은 48669.741에 can_request=false/managed_sdk_idle=true, 명시적 Load48703.663, Show48703.731, PolicyBlocked 완료48703.733이었다. 보상0/완료1/BGM true를 확인했다. UMP Update, SDK Initialize, 광고 Load trace는 없었다. `show_accepted=true`는 요청 완료 callback을 수락했다는 API 결과이며 광고 표시 성공이 아니다.

## AdMob 읽기 감사

승인된 기존 Chrome 세션에서 Monster Tamer의 다음 UI 값을 읽었다. 스위치·체크박스·광고 단위·메시지를 변경하거나 저장하지 않았다. 고참여 설정은 펼쳐 값을 읽은 후 취소했다. 운영 ID·계정·수익 정보는 공개 기록에서 제외한다.

| 화면 | 현재 관찰 |
|---|---|
| 앱 설정 → 참여도가 높은 광고 | **켜짐** |
| 미디에이션 그룹 | AdMob(기본) 1행만 표시 |
| 개인정보 및 메시지 홈 | 유럽/미국/IDFA 모두 새 메시지 만들기 표시 |
| 유럽 규정 → 메시지 | 메시지 생성 3단계 안내. 게시된 메시지 목록을 확인하지 못함 |
| 유럽 규정 → 설정 | 일반 광고 파트너198개 자동 포함, 광고 소스의 파트너 자동 추가 꺼짐 |
| 메시지 노출 범위 극대화 | **켜짐**. UI는 대체 동의 메시지 시도와 새 앱 자동 생성 메시지를 설명 |
| RTB 파트너 소재의 동의 추가 확인 / 광고 목적 동의 모드 | 둘 다 꺼짐 |
| 적법한 이익 포함 / 기본 사용 | 둘 다 켜짐 |

메시지 생성 안내만으로 운영 사용자에게 동의 UI가 절대 나오지 않는다고 단정하지 않는다. 대체 메시지의 실제 발동·앱 적용·지역별 노출은 이번 감사에서 확인하지 않았다. 위 값은 설정 관찰이며 법적 적합성 판정이 아니다. sample App ID와 TFUA=true인 이번 기기 실행도 운영 계정의 게시 메시지·성인/EEA 동의 UI 검증을 대신하지 않는다. [UMP 안내](https://developers.google.com/admob/unity/privacy/gdpr)는 TFUA=true가 해당 사용자에게 동의를 요청하지 않도록 한다고 설명한다.

## 5초 종료 요건의 구체적 해결 경로

[Families 정책](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en)은 아동 또는 연령 미상 사용자에게 정상 이용을 방해하는 보상형/선택형 광고도 5초 후 닫을 수 있어야 한다고 명시한다. 이번 검증은 보상과 앱 복귀 처리 검증이며 native 최초 표시→X 최초 표시 영상을 새로 측정하지 않았다. 이전4.980722–5.029033초 오차 구간의 판정 불가를 그대로 유지한다.

1. **현재 적용 가능한 차단 경로는 이미 코드에 있다.** 일반 release는 광고 요청을 막고, 광고 버프/회복 사용 불가를 안내한다. 기존 No Ads 권한의 무광고 보상 경로는 보존한다. 이번 control 실행으로 managed 차단을 재확인했다. 실제 배포 트랙·기존 제공 번들의 위반 해소는 별도 Console 검증이 필요하다.
2. **수익화를 복구하려면 고참여 설정만 끄는 것으로 완료 처리하지 않는다.** 변경 위치는 AdMob → Monster Tamer → 앱 설정 → 참여도가 높은 광고다. 끄면 표준 광고 단위의 참여형 형식과 종료 단계가 줄 수 있으며 수익에 영향을 줄 수 있다. [공식 설정 표](https://support.google.com/admob/answer/15525707?hl=en)는 꺼짐에서도 보상형 reward skip time을 최대30초로 설명한다. 이는 조기 종료 X와 보상 취득 시각을 별개로 검증해야 한다는 뜻이며, 켜짐 자체를 현재 위반 원인으로 확정하지 않는다. 이번에는 변경하지 않았다.
3. **혼합 연령 요청 처리와 공급원 근거를 연결한다.** 현재 sample의 TFCD=true/G/TFUA=true를 운영 이용자의 실제 연령 판정으로 간주하지 않는다. [AdMob Families 안내](https://support.google.com/admob/answer/6223431?hl=en)는 혼합 연령 앱에서 아동 요청에 child-directed 처리와 최대 등급 설정을 요구한다. 운영 활성화 전에 연령 미상 포함 처리 방침, 요청 전 태그, 실제 공급 네트워크와 해당 버전, 실제 닫기 가능성을 함께 검토해야 한다.
4. **5초 종료를 앱 타이머의 `Destroy()`로 대체하지 않는다.** SDK 광고 객체 정리와 native 종료 UI 보장은 다르다. 공급원/소재의 종료 동작이 확인되지 않으면 release 차단을 유지한다. 필요하면 비공개 지원 문의로 위반 번들·배치·요청 태그·소재 식별 근거와 5초 후 종료 가능성 확인을 요청할 수 있으나, 문의 전송과 운영 소재 수집은 이번에 하지 않았다.
5. **UMP 운영 전환은 별도 변경안이다.** 지역별 메시지/파트너 범위 및 현재 켜진 대체 메시지 설정의 영향을 결정하고, 실제 시작 시 갱신·필요 폼·접근 가능한 privacy options UI를 운영 흐름에 연결해야 한다. 이는 현재 test-only 첫 명시적 요청 방식과 다르며, 게시/설정 변경 또는 운영 ID를 사용한 검증은 검토 후 별도 승인 범위다.

## 미검증 및 정리

native 표시 실패 callback, 실제 성인/EEA 동의·privacy options 화면, 운영 광고·미디에이션 소재, 자연 네트워크 오류, 16KB 실기기와 스토어 Families 해소는 미검증이다. 실패를 만들기 위해 개인폰의 네트워크를 변경하거나 native callback을 주입하지 않았다. 새 코드 변경이 없어 Editor 회귀를 새 실행했다고 기재하지 않는다. 이번 새 근거는 두 APK 빌드·정적 검증과 실제 기기/설정 관찰이다.

두 앱을 force-stop하고 Home으로 돌아갔다. 기존 앱과 이번 테스트 앱의 데이터는 삭제하지 않았다. phone/Chrome/heavy 슬롯은 통합 담당에 반환했다. 이 checkout Editor0과 빌드 부수 재직렬화/줄바꿈 복원을 확인했다. 원시 로그·화면·기기 식별자는 ignored 비공개 로컬에 보관한다. #91 전체 완료 또는 운영 광고 재활성화를 선언하지 않는다.
