# 최신 일반 오프라인 게임 APK 준비 — 2026-10-24

**최신 통합 소스로 일반 게임 APK를 만들고, 이전 파일에 남았던 AdServices3권한이 새 파일에서 제거된 것을 확인했습니다.** 이번 완료 범위는 빌드·실제 APK 정적 검사·기존 출력 보존·로컬 전달 패키지입니다. 새 파일의 설치/실행과 Drive 업로드는 하지 않았습니다.

| 항목 | 실제 결과 |
| --- | --- |
| source | `c6b4ba136f7f211c712b2c66d062eb8c9d710342` |
| 실행 | `RevivalGameplayBuild.BuildAndroid`,18:04:19~18:08:18 KST, exit0 |
| 도구 | Unity6000.3.25f1/e1dba0a9aba4, CLI1.0.0-beta.8, JDK17/Build Tools36.0.0 |
| 앱 | `com.AeDeong.MonsterTamer.revival.gameplay`,1.0.5/code26/debug, min25/target36/ARM64/IL2CPP |
| 새 APK | `Tamer-gameplay-c6b4ba1.apk`,110727684 bytes |
| SHA-256 | `cf666b84aaa91948b8d0d4b7ab172f60ca09bd2eee0dd11763744d00e04c1ffc` |
| 실제 manifest | INTERNET/ACCESS_NETWORK_STATE/BILLING/GMS AD_ID/AdServices3권한, 광고·PGS 초기화 provider2개 부재 |
| 실제 서명 | 기존 일반 APK와 같은 단일 debug 인증서 SHA-256 |
| 보호 | 현재 원본62개 before/after 원문·해시 대조, 변경 설정8개 복구, clean·Editor 종료 |
| 기존 출력 | 이전 APK 원래 경로·파일 identity·bytes 복구, 이전 로그/provenance·전달본 불변 |
| 전달 준비 | 새 APK·SHA256SUMS·한국어 설치/첫 확인 안내3파일, 독립 복사 후 해시 대조 |
| 소스/네이티브 | 소스 사전검사12/12, native6개 LOAD/ZIP·보수적 RELRO layout 통과 |

빌드 전 소스는 clean c6 기준으로 고정했습니다. 보호 실행기가 승인된 Editor Pipeline의 `m_AutoStart`를 일시 비활성화하므로 CLI provenance의 `source.dirty=true`를 보존합니다. 실제 delta8개를 확인한 뒤 해당62개 원본을 검증·복원했습니다. 이 임시 설정 변경을 숨기거나 실행 시작 전체가 clean이었다고 표현하지 않습니다.

이전 [10월23일 일반 후보](gameplay-delivery-20261023.ko.md)의 APK SHA `feb3a938abf7a0055032d942521cebb371b18824a32f09b857bcbc6a823ed371`에는 AdServices3권한이 실제로 남아 있었습니다. 최신 `OfflineManifest`의 제거 설정이 새 merged manifest에 반영됐습니다. 기존 파일은 먼저 독립 백업/readback 후 동일 볼륨의 새 보관 경로로 잠시 이동했습니다. 새 빌드 결과를 별도로 보존한 뒤 기존 파일을 원래 identity로 되돌렸으며 삭제/덮어쓰기는 하지 않았습니다.

남은 권한은 WAKE_LOCK/FOREGROUND_SERVICE/자기 패키지의 DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION3개입니다. PGS metadata는 남아 있어 모든 native provider나 SDK 설정이 없다는 주장은 하지 않습니다. 일반 변형에는 `allowBackup=false`/`fullBackupContent=false`/`dataExtractionRules` 속성이 없으며 전체 백업·기기 전송 차단을 보장하지 않습니다. 백업/복원 시험은 실행하지 않았습니다.

추가 raw RELRO 끝 정렬 진단3건은 미통과이고 `--strict-relro`는 요청하지 않았습니다. 다른 UMP 후보의 진단2건과 구분합니다. 정적 검사 성공은 실ARM64/16KB 실행이나 Play 제공물·정책 판정의 근거가 아닙니다. 빌드 종료 로그의 `StackAllocator m_LastAlloc` 진단도 보존했으며 전체 Editor 경고가0이었다고 주장하지 않습니다.

## 사용할 때의 범위

이 일반 변형은 실제 로그인·Store·광고 요청을 차단하고, 프로세스마다 새 합성 프로필로 원래 Main에서 대기합니다. Enter Game/Return to Main·조이스틱·전투·Capture를 사용할 수 있습니다. 오늘의 Login 안내/실제 UMP 재진입 검증에는 각각의 전용 후보를 사용했으며 이 일반 앱의 재빌드로 그 경로를 검증했다고 쓰지 않습니다.

기존 계정 진행도·No Ads 복원이나 재시작 저장을 시험하는 앱이 아닙니다. 설치 오류나 서명 충돌이 생겨도 기존 앱/데이터/캐시를 지우지 않고 오류를 확인합니다. 현재는 새 파일을 기기에 설치하지 않았으므로 [이전 후보의 LD 왕복](gameplay-delivery-20261023.ko.md)을 새 파일의 실행 결과로 합산하지 않습니다.

로컬 파일은 준비됐지만 Chrome 보류 때문에 Drive 전달은 대기입니다. 지금 사용자 휴대폰 조작이나 업로드가 필요한 단계는 아닙니다. 이 파일은 Play에 올리는 AAB나 IAP Test32·privacycold29가 아니며, 최종 운영 AAB는 여전히 없습니다. 운영 승인 false/null·`releaseReady=false`를 유지합니다.
