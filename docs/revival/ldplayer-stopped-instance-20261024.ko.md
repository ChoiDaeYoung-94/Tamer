# 중지된 LD플레이어가 함께 있을 때의 준비 검사 — 2026-10-24

실제 `list2` 조회에서 요청한 `Tamer`는 index1·Android 시작 상태·양수 PID였으나, 다른 중지 인스턴스의 host/VM PID 두 값이 `-1`이라 전체 준비 검사가 `Invalid LDPlayer instance metadata`로 중단됐습니다. 이 단계에서는 앱 설치·실행·데이터 변경을 하지 않았습니다.

7필드/10필드의 중지 상태(`androidStarted=0`)에서 host/VM PID 위치의 정확한 `-1`만 부재 표시로 허용합니다. 실행 중의 음수 PID·다른 필드의 음수·`-2`·잘못된 시작 값은 계속 거절합니다. 중지 행을 버리지 않으므로 중복 index/정확한 이름 검사도 유지됩니다. 요청한 대상이 중지돼 있으면 guest 조회와 실행을 하지 않습니다.

관련 Python 검사10개가 통과했습니다(0.001초). 정상 Tamer와 중지된 다른 인스턴스의 공존, 대상 자체가 중지된 7/10필드, 잘못된 음수 상태 거절과 기존 transport/정확한 index 확인을 검증했습니다.

수정 후 실제 첫 조회는 목록을 정상 판독하고 정확한 Tamer/index1을 선택했으나 ADB가 offline이라 guest 조회 전 중단했습니다. 이후 총괄이 해당 전송(`emulator-5556`)만 명시적으로 재연결한 뒤, indexed console의 serial 확인과 Android14/API34·페이지4096bytes 조회를 마치고 `transportReady=true`를 확인했습니다. 전역 ADB 서버/다른 인스턴스 재시작·설정 변경·앱 설치/실행은 하지 않았습니다. offline 결과를 최초 성공으로 바꾸지 않았습니다. 이 도구 결과는 APK runtime·기기 호환성·16KB 검증을 대신하지 않습니다.
