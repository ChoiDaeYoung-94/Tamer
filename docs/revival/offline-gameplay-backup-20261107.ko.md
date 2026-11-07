# 기본/PHOTO 격리 앱의 백업 차단

실물 숫자 키보드 확인용으로 기존 PHOTO APK를 검사하던 중, 해당 APK manifest에 `allowBackup`·`fullBackupContent`·`dataExtractionRules` 차단이 없음을 확인했습니다. 기존 파일과 LD의 시험 데이터는 보존하고 그 APK를 실물폰에 새로 설치하지 않았습니다.

`RevivalGameplayBuild`의 기본 gameplay/PHOTO 경로에도 기존 격리용 백업 manifest를 적용하고, 정확한 `.revival.gameplay` 앱 ID에 한해 Gradle callback이 cloud-backup/device-transfer 제외 XML을 기록하도록 보완했습니다. 운영 앱과 임의 접미사 ID는 callback 대상으로 추가하지 않습니다. 기존 운영 manifest 원본·실제 계정/저장·키와 기본 ARM64/min25/target36을 유지합니다.

정확 ID 경계와 실제 Gradle callback의 양쪽9개 제외 출력을 확인하는 검사를 추가했습니다. 현재는 구현/정적 검토 단계이며 실제 EditMode·APK 생성/패키지 검사·실물 빈 입력/숫자 키보드/가림 확인은 준비 중입니다. 나이 값 입력·확인·응답하지 않음 제출은 이 검증에 포함하지 않습니다.

부모 인트로/운영 준비 PR 위의 별도 변경입니다. 실제 검증과 원본/공용 설정 복구 근거를 추가하기 전 Draft로 유지합니다.
