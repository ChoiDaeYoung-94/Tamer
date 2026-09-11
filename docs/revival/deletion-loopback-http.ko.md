# 합성 삭제 C# → Python HTTP 계약

`SyntheticLoopbackDeletionGateway`는 Editor 또는 Development Build에만 컴파일되는 명시적 테스트 어댑터다. 기존 화면의 기본 `UnavailableDeletionGateway` 선택은 바꾸지 않는다. 고정 합성 계정 `synthetic-demo-account`와 합성 세션 `synthetic-demo-session`만 받으며, 실제 인증 정보 입력은 제공하지 않는다. 재인증 경계는 demo의 `available`·`synthetic` 설정 확인이다. 실제 사용자 재인증을 구현했다는 뜻이 아니다.

허용 원점은 `http://127.0.0.1:<port>/`뿐이다. DNS 이름, 외부 주소, 경로·사용자 정보·query·fragment를 거부하며 시스템 proxy와 자동 redirect를 끈다. 응답은 16KiB, 기본 timeout은 5초(최대 30초)로 제한한다. JSON의 snake_case 상태는 명시적으로 매핑하고 알 수 없는 상태·잘못된 범위·완료 증거 누락을 거부한다. HTTP 오류와 파싱/timeout 실패는 기존 flow의 재시도 상태로 전달되며 서버 원문 오류나 proof를 화면에 노출하지 않는다.

## 실행과 소유권

웹 demo는 `python -m server.privacy.local_demo`로 직접 실행한다. 임시 SQLite와 합성 provider만 사용하며 종료하면 임시 저장소가 정리된다. 서비스 시작 시 Unity 화면을 자동 연결하지 않는다.

UI 담당의 `DeletionPresenter.ConfigureSynthetic(flow)`에 전달할 때 호출자가 gateway를 소유한다. 아래 수명 범위는 화면 사용 전체를 감싸야 한다. `DeletionFlow.Dispose()`는 gateway를 해제하지 않으므로 테스트 실행자는 화면 종료 뒤 gateway도 반드시 해제한다.

```csharp
using (var gateway = new SyntheticLoopbackDeletionGateway(new Uri("http://127.0.0.1:8766/")))
{
    var session = new DeletionSession(new object(), SyntheticLoopbackDeletionGateway.AccountId,
        SyntheticLoopbackDeletionGateway.SessionKey);
    using (var flow = new DeletionFlow(gateway, () => session))
    {
        // UI에서는 이 범위를 화면 수명 동안 유지하고 ConfigureSynthetic(flow)로 주입한다.
        await flow.RequestAsync();
    }
}
```

## 검증 범위

`python -m server.privacy.contract_check`는 .NET SDK 9가 있는 로컬 PC에서 실행한다. 별도 NuGet 패키지 없이 실제 `DeletionFlow.cs`와 어댑터 소스를 링크해 컴파일하며, 무작위 loopback 포트의 실제 Python WSGI 서버에 HTTP 요청을 보낸다. 각 시나리오는 임시 DB와 서버를 새로 만들고 종료한다. 일반 Python unittest 122개와 별도 실행이다.

13개 시나리오: 완료 왕복, 취소, 확인 처리 후 응답 유실·동일 요청 재시도, 다른 합성 신원 차단, 비합성 config 차단, redirect 미추적, 알 수 없는 상태, 완료 증거 누락, timeout, 잘못된 JSON, 과대 응답, HTTP 오류, 취소 token. 완료 왕복은 실제 JSON challenge 회전과 동일 요청 ID 유지, queued→processing→completed 및 완료 증거를 확인한다. 서버 DB에서도 요청 한 건과 최종 상태를 대조한다.

이 결과는 C# 소스의 .NET 런타임 HTTP 계약 검증이다. Unity Editor 회귀 검사는 별도 기록하며, Unity 화면을 통한 실제 HTTP 클릭·Android/IL2CPP HTTP 실행·운영 인증·삭제·게시·배포는 수행하지 않았다. 완료 증거도 합성 표식이며 실제 데이터 삭제는 없다.
