# 격리 debug AAB와 16KB 실행 환경 검증

추적: [#97](https://github.com/ChoiDaeYoung-94/Tamer/issues/97). 기준 main은 `4e8c039`이며 Unity `6000.0.81f1`를 유지한다. 운영 앱 ID·키·계정·결제·광고 요청·CI·업로드는 사용하지 않았다. AAB/APKS/APK와 원시 로그에는 비공개 게임 자산이 있으므로 저장소에 올리지 않는다.

## AAB와 split 결과

`RevivalBuild.BuildAndroidDevelopmentBundle`은 기존 격리 APK 빌드와 같은 검사·씬·Development 옵션을 사용하고 출력 확장자와 결과 파일만 분리한다. 원래 앱 ID, custom-keystore 여부, alias, scripting backend와 bundle 설정을 finally에서 복원한다. CLI가 Editor 실행 전에 alias를 비우는 현상은 이 함수보다 먼저 일어나므로 Editor 종료 후 추적 파일과 비교하여 원래 alias를 복원했다.

- debug AAB 빌드 성공, errors 0; 앱 ID `com.AeDeong.MonsterTamer.revival`, 버전 1.0.5/code26, min24/target36, ARM64 전용.
- AAB 크기 `91,737,845` bytes, SHA-256 `5754ee67ae9d11007cfd0d689088306a063b48cd858c8998ee89cd005e29e77b`.
- bundletool `1.18.3` 공식 SHA-256 `a099cfa1543f55593bc2ed16a70a7c67fe54b1747bb7301f37fdfd6d91028e29`를 다운로드 후 대조했다.
- bundletool validate 통과, BundleConfig의 `PAGE_ALIGNMENT_16K` 요청 확인.
- 합성 ARM64/API36/density420/en device spec으로 `base-master.apk`와 `base-arm64_v8a.apk`를 생성했다. 실제 기기의 device spec은 아니다.
- 이번 실행에서 만든 전용 debug 키의 인증서 SHA-256과 각 split signer 지문이 같다. 두 split의 SDK `zipalign -c -P 16 4`가 통과했다.
- native split에는 ARM64 `.so` 6개가 있고 LOAD/ZIP 검사가 통과했다. 6개 모두 압축돼 직접 mmap용 ZIP 정렬은 N/A다. 별도 RELRO 끝 modulo 검사는 5개 실패했다. 이는 실제 16KB 실행 성공을 뜻하지 않는다.

정확한 AAB/APKS/split 해시와 실행 환경 결과는 [검증 JSON](aab-16kb-validation.json)에 기록한다. 기존 APK와 이번 AAB의 빌드 산출물은 서로 다른 파일이며 기존 APK 증거로 AAB 실행 성공을 대신하지 않는다.

## 서명 판정

AGP가 `META-INF/MANIFEST.MF`를 뒤에 넣는 AAB에서는 jarsigner가 `jar verified`와 함께 JarInputStream 순서 경고를 출력했다. 순차 스트림 reader가 앞서 읽은 payload의 서명 메타데이터를 아직 보지 못한다는 경고이며, 경고를 숨기지 않고 결과에 `jarInputStreamOrderingWarning=true`로 남겼다.

`VerifyAabSignature.java`는 검증 기능을 켠 JarFile로 모든 항목을 끝까지 읽고 601개 payload 전부의 서명 coverage, 동일한 debug 인증서, 인증서 유효 기간, 중복 ZIP 항목 부재를 확인했다. 이 판정은 JarFile 방식의 검증이며 JarInputStream 순차 검증 성공이나 신뢰 CA/스토어 승인 주장이 아니다. self-signed debug 인증서 경고와 unsigned payload를 구분한다.

합성 바이트와 실행 중 생성한 임시 키로 정상 서명 통과, 서명된 payload 변조 거부, unsigned payload 추가 거부를 실제 실행했다. 운영 ID/버전/API/debug 설정 거부, runtime hash/provenance와 network 실패 거부 및 병합된 개인정보 감사 테스트까지 Python 59개가 통과했다. 실제 게임 자산이나 키를 테스트 fixture로 추가하지 않았다.

## 호스트와 실행 범위

Windows x64 / Intel Core i7-14700KF, BIOS 가상화와 SLAT는 사용 가능했다. 초기 여유 RAM 약 13GiB, 디스크 약 374GiB를 확인하고 heavy slot을 조율했다. Windows hypervisor는 활성화되지 않았고 AEHD/GVM 서비스도 없었다. 설치 후 `emulator -accel-check`는 코드 6과 hypervisor driver 미설치를 보고했다.

별도 로컬 SDK에 Emulator `37.1.11` 및 `system-images;android-36;google_apis_ps16k;x86_64` revision 7을 설치하고 `Tamer_16KB_API36` AVD를 생성했다. 기존 Unity SDK·다른 AVD·Editor·Windows 기능은 변경하지 않았다. 요청 RAM은 2048MB였으나 에뮬레이터가 2560MB로 올렸다는 로그도 기록했다.

첫 software 부팅은 600초 동안 adb offline 상태로 부팅하지 못했다. 종료 console도 응답하지 않아 실행 시 기록한 launcher의 경로와 PID를 확인한 뒤 해당 프로세스 트리만 종료했다. 이때 발견된 timeout cleanup 문제는 probe 도구에 보완했다. Vulkan을 끄고 kernel 로그를 켠 두 번째 시도는 5.03초 뒤 `0xC0000005`로 종료됐다. 정확한 crash 원인을 하이퍼바이저 부재로 단정하지 않는다. 두 시도 후 own emulator/qemu는 0개이고 heavy slot을 반환했다.

**PAGE_SIZE, device/process ABI, 번역 여부, linker compatibility, 앱 설치·실행·화면은 관측하지 못했다.** APKS 해시와 provenance를 지정해 runtime 도구를 호출했지만 지정 AVD가 없어 설치 전에 거부됐다. runtime guard는 4개 합성 회귀와 AVD 없는 경우의 설치 전 거부까지만 검증했으며, 실제 기기에서의 설치·오프라인 차단·실행 경로는 미검증이다.

이미지 이름의 16KB 표기를 실제 `adb shell getconf PAGE_SIZE=16384` 관측으로 대체하지 않는다. x86_64 이미지에서 ARM64 번역으로 실행하는 경우에도 실제 ARM64 16KB 프로세스와 구분해야 한다. smoke 첫 씬의 성공은 로그인·구매·광고·게임 진행 검증이 아니다.

## 재현

구매 자산 복원과 고정 Unity CLI 준비는 [SDK 재현 절차](sdk-audit.ko.md)를 따른다. 해당 프로젝트 Editor가 없는 상태에서 실행한다.

```powershell
tools/.local/unity-cli/1.0.0-beta.8/unity.exe build . --editor-path 'C:\Program Files\Unity\Hub\Editor\6000.0.81f1\Editor\Unity.exe' --target Android --execute-method RevivalBuild.BuildAndroidDevelopmentBundle --log-file Logs/revival/android-aab-build.log --no-tail --non-interactive
python tools/revival/install_bundletool.py
python tools/revival/verify_bundle.py --output Build/revival/bundle-check-new
python -m unittest discover -s tools/revival -p 'test_*.py'
```

`verify_bundle.py`는 기존 출력 폴더를 덮어쓰지 않는다. 결과 폴더에는 새 debug keystore와 구매 자산이 포함된 split이 있으므로 비공개로 보관한다.

AVD 재개는 해당 로컬 SDK와 AVD가 준비된 호스트에서만 실행한다. 기본 가속 설정은 auto이며 가속 없이 진단할 때만 `--accel off`를 명시한다.

```powershell
python tools/revival/run_16kb_emulator.py --timeout 600
# 부팅 및 실제 16384 확인 후, 별도 실행 관측:
python tools/revival/verify_emulator_runtime.py --apks Build/revival/bundle-check-2/Tamer-development.apks --expected-sha256 ca19eb7cb6fdfc3faacb1640f098ef57f86a93bdec62499d28ab12cfe6ece556 --provenance docs/revival/aab-16kb-validation.json --output Build/revival/runtime-splits-new
```

위 runtime 명령의 해시는 이번에 생성한 APKS용이다. 새 AAB/APKS를 만들면 검토된 빌드 결과에서 새 hash와 provenance를 준비해야 하며 위 값을 재사용하지 않는다. provenance JSON에는 `artifactSha256`, `applicationId`, `entryScene`, `buildCodeCommit`이 필요하다. 첫 씬은 `Assets/Tests/Scenes/RevivalSmoke.unity`여야 하며 도구는 실제 파일 SHA와 사용자 지정 SHA까지 모두 대조한다. APK는 같은 조건으로 `--apk`를 사용할 수 있다.

runtime 도구는 지정 격리 AVD를 변경하는 도구다. AVD 이름/serial과 debug 앱 ID를 검사하고 guest Wi-Fi/data 명령 성공 및 UP 인터페이스가 loopback뿐임을 설치 전과 launch 직전에 관측한다. 실패·알 수 없는 출력·다른 UP 인터페이스가 있으면 launch를 거부한다. 이후 설치·실행·프로세스·로그·화면을 관측한다. APK와 새 키의 APKS를 같은 AVD에 순서대로 설치하면 인증서 불일치로 설치가 거부될 수 있다. 그 경우 기존 격리 앱의 생성 이력을 확인하고 그 앱만 제거한 뒤 재시도해야 한다. 도구는 다른 인증서의 앱이나 데이터를 자동 삭제하지 않는다.

Windows에서 권장되는 WHPX 활성화는 관리자 권한과 재부팅이 필요할 수 있다. 다른 작업 중인 호스트에서 자동 재부팅하거나 기존 Unity 6.3 UAC 거절을 우회하지 않았다. [Android 가속 설정](https://developer.android.com/studio/run/emulator-acceleration), [16KB 실행 검증](https://developer.android.com/guide/practices/page-sizes), [bundletool](https://developer.android.com/tools/bundletool), [고정 bundletool 배포](https://github.com/google/bundletool/releases/tag/1.18.3), [Unity 6.3 재개 인계](unity63-handoff.ko.md).
