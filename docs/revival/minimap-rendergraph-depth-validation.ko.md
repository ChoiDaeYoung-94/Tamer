# 미니맵 Render Graph 출력 깊이 수정 검증

Game에서 반복된 출력 RenderTexture 깊이 경고를 기존 미니맵 RenderTexture의 깊이 형식을 `D24_UNorm_S8_UInt`로 지정해 해결했습니다. 실제 Unity API 저장/재로드, 비Play 실제 Game 카메라 렌더1회, 새 APK 빌드1회와 Android Game 경고 전용 관측1회가 통과했습니다. [이전 자연 전투 관측의 수동복귀 실패](lts-gameplay-combat-observation.ko.md)는 재시험하지 않았고 이번 결과로 완료 처리하지 않습니다.

## 수정 근거와 실제 source

설치된 URP17.3 `UniversalRendererRenderGraph.ImportBackBuffers`는 camera output의 depthStencilFormat이 None일 때 기본 깊이 형식으로 fallback하며 해당 경고를 발생시킵니다. Game의 `Camera_MiniMap`과 canvas RawImage가 함께 참조하는 `Assets/Scripts/MiniMap/minimapRenderTexture.renderTexture`의 깊이가0이었습니다. [Unity6000.3 공식 API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RenderTexture-depthStencilFormat.html)는 플랫폼에서 선택 형식을 지원하지 않을 때 호환 가능한 더 큰 형식으로 대체할 수 있음을 설명합니다. 기존 compatible format 설정을 유지했습니다. 경고를 숨기거나 compatibility mode를 켜지 않았습니다.

checkout `C:/Users/pc_17/.codex/worktrees/unity-lts-transition/Tamer`, branch `codex/minimap-rendergraph-depth`, 수정 Editor source는 `b963e366ceabbbd4a94a2a0ae52e08f9a5455bff`, APK 빌드 source는 `c5abefbdd5e1b1ffb1e7ac969cf422efca0f694c`입니다. 각각 실행 직전 절대경로/branch/HEAD/target/dirty0/해당 Editor0를 기록했습니다. 최신 main을 조회하고 해당 clean checkout만 fast-forward했습니다. 문서 후속 커밋과 이후 main을 이 APK source로 표현하지 않습니다.

Editor를 열기 전 [사용자가 승인한 비공개 메타 복원3차](private-meta-restoration-approval.ko.md)는 검토 source4f9a13b에서 verified4561/copied0/sdk migration7/private migration1로 통과했고, 도구 병합 뒤 실제 코드가 동일한 것을 확인해 복원 검사를 반복하지 않았습니다. 원본/current/비공개 보존 메타·원본manifest를 덮어쓰지 않았습니다. 다른 checkout의 복원 결과로 확대하지 않습니다.

Unity6000.3.25f1/revision e1dba0a9aba4·CLI1.0.0-beta.8·Pipeline0.6.0-exp.1·JDK17.0.18+8·NDK27.2.12479018·Gradle9.3.1·build tools36.0.0을 사용했습니다. 설정·설치·SDK 버전 변경은 없습니다.

## Editor API와 카메라 검증

그래픽 기능을 켠 resident batch Editor를 해당 checkout에만 열었습니다. 첫 status는 컴파일/domain reload 준비 중 Pipeline 미등록이었고, 완료 뒤 editor_status ready/compiling false/stopped를 확인했습니다. set_autotick 후 기존 asset GUID·300×300·MSAA1·depthNone을 확인하는 private run_script로 깊이를 지정해 SaveAssetIfDirty/ImportAsset/재로드했습니다. 실제 결과는 D24_UNorm_S8_UInt·R8G8B8A8_UNorm·300×300·MSAA1이고 `.meta` 전체 바이트도 그대로입니다. 최종 제품 diff는 `m_DepthStencilFormat: 0`→`92` 한 줄입니다.

별도 private smoke는 비Play 상태에서 Game을 additive로 열어 기존 target을 가진 카메라1개와 Render Graph 활성/URP 요청 지원을 확인했습니다. 실제 `Camera_MiniMap`에 `RenderPipeline.SubmitRenderRequest` SingleCameraRequest1회를 보내 endCameraRendering1·대상 깊이 경고0·scope Error/Exception/Assert0·실제 깊이 D24를 확인했습니다. 그래픽 API는 Direct3D11입니다. Game scene을 저장하지 않고 닫았고 제품 Start/운영 로그인/HP 변경/spawn은 실행하지 않았습니다. 이것은 Android 실제 gameplay 프레임 검사와 별개입니다.

Console25개는 Log이며 일부 TMP 폰트 serialization 안내 로그를 포함합니다. 전체 에셋/폰트 회귀를 검증한 결과는 아닙니다. EditorApplication.Exit(0) 요청은 서버 종료로 invalid response를 반환했으나 실제 owned PID0와 Server shutdown을 확인했습니다. 원래 서명/광고/manifest/settings snapshot을 복원했습니다. Low/Medium URP가 자동 생성한 previousVersion12→13/공백 diff는 비공개로 보존하고 원래 snapshot으로 되돌렸습니다. 이 부수적인 import 기록을 제품 설정 변경으로 포함하지 않았습니다.

## 새 APK와 단일 실제 Game 관측

Run-GameplayHarness development 빌드1회가 성공했습니다. 새 APK는133,569,033바이트, SHA-256 `5beb09f228efea4556dfd7ac3680f66df62a5b20ac1866e599d2ed54d1379c3e`입니다. 실제 별도 앱ID `com.AeDeong.MonsterTamer.revival.gameplay`·debug 서명/debuggable·min25/target36/ARM64/version1.0.5/code26, INTERNET/ACCESS_NETWORK_STATE/BILLING/AD_ID·MobileAdsInitProvider 제거 검사가 통과했습니다. 기본 development 변형의 백업/전송 제외 검사는 수행하지 않았습니다. 이전 전투 APK와 verifier/log는 비공개 previous-gameplay 폴더에 보존했습니다.

빌드 중 ProjectSettings/SceneTemplateSettings/AndroidResolverDependencies·GoogleMobileAdsSettings·AndroidManifest2개를 격리 앱 설정으로 전환했으며 clean source에서 wrapper가 보존한 임시 변경입니다. source/임시 파일 목록·목적·snapshot 원복을 private provenance에 기록했습니다. 완료 후 owned Editor0, wrapper 설정 복원, 자동 URP4개 trailing whitespace만의 diff 보존/정리 후 dirty0를 확인했습니다. 불명의 사용자 변경을 원복하지 않았습니다.

Android13/API33/PAGE_SIZE4096 물리 기기의 기존 패키지 부재 확인 후 fresh 앱만 설치했습니다. 완료 PNG에서 실제 연령창의 응답하지 않음과 Main/Enter Game 버튼을 확인했고 own 앱ID/설치 경로SHA/새 APK SHA/nonce에 묶은 phase 신호로 입력2회만 수행했습니다. 취소 가능한300초 준비 대기와 runtime 경고 관측 구간을 분리했습니다. Game 진입 완료 뒤 자동 bounded 관측을 수행해 불필요한 모델 화면 대기로 실행 시간을 늘리지 않았습니다.

| 경계 | 이번 새 APK에서 확인한 결과 |
| --- | --- |
| 준비/실행 | setup 완료, runtime 관측11.185초, 별도 failure 없음 |
| Game 유지 | 구간 중5개 시각의 마지막 scene 관측과 종료 관측이 모두 Game, HP100/Gold1000/read3/write0 |
| 실제 프레임 | 시작·종료 PNG에 캐릭터/HUD/미니맵/적 표시, 적 위치 변화; Game 읽기 관측27개 |
| 대상 경고 | own PID 보존 로그의 출력 깊이 경고0, W Unity 줄0 |
| 오류 범위 | fatal/harness FAIL 없음, 구독 전 PlayerConnection E Unity3줄은 계속 존재 |
| 네이티브/정리 | Unity/IL2CPP maps, translation 없음, fresh own 앱 제거, Editor0/dirty0 확인 후 Ads 슬롯 반환 |

시작·종료 미니맵은 실제 캐릭터와 시야/환경을 표시했고 검은 화면/magenta/Game Over는 관측되지 않았습니다. Game이 끝나거나 다른 씬으로 이동한 결과를 경고0으로 판정한 것이 아닙니다. overlay errors0과 전체 로그 오류0은 구분하며 PlayerConnection Socket blocking 실패2+multicast setup 실패1을 숨기지 않습니다. runtime GPU 깊이 형식 자체를 폰에서 별도 introspection한 결과는 아니며 Editor 실제 D24/빌드 에셋과 폰의 렌더·대상 경고0을 각각 기록합니다.

추가 빌드/기기 재실행/복원 재검사0, 기존 생존 수동복귀 criterion 재시험0, 인벤토리 restart 반복0입니다. 운영 계정/결제/광고/저장·기기 네트워크 설정·HP/무적/spawn 조작0입니다. 전체 gameplay·장시간 성능·생존 이동/처치·실제 ARM64 16KB·strict RELRO 재검사·최종 출시 AAB/스토어 검증은 이 결과의 범위가 아닙니다.

[공개 결과와 증거 해시](minimap-rendergraph-depth-validation.json)를 참조하십시오. raw 화면/own PID 로그/maps/operator/승인 ledger는 ignored 비공개 Logs에만 있으며 공개 원시 에셋·계정·키·기기 식별자를 추가하지 않았습니다. 로컬 worktree/Library/비공개 증거를 유지합니다.
