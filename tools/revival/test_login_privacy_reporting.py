"""Actual login defaults and selected SDK reporting bodies with inert boundaries."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import unittest
import uuid
from test_guest_retry_flow import block

ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.27f2/Editor/Data')


@unittest.skipUnless(sys.platform == 'win32', 'Pinned Windows compiler required')
class LoginPrivacyReportingChecks(unittest.TestCase):
    def test_actual_reporting_bodies_respect_defaults_before_authentication(self):
        paths = ['Assets/Scripts/Login/Login.cs',
                 'Assets/ThirdParty/PlayFabSDK/Client/PlayFabDeviceUtil.cs',
                 'Assets/ThirdParty/PlayFabSDK/Shared/Internal/PlayFabHttp/PlayFabHTTP.cs']
        raw = {p: (ROOT / p).read_bytes() for p in paths}
        login, device, http = (raw[p].decode('utf-8-sig') for p in paths)
        route = block(login, 'private async UniTask RunLoginAsync(')
        self.assertLess(route.index('ApplyLoginPrivacyDefaults();'), route.index('WaitForNetworkAsync(token)'))
        self.assertLess(route.index('ApplyLoginPrivacyDefaults();'), route.index('LoginOnAndroidAsync(token)'))
        methods = block(login, 'private static void ApplyLoginPrivacyDefaults()')
        methods += '\n' + block(device, 'private static void SendDeviceInfoToPlayFab(')
        methods += '\n' + block(http, 'private static IEnumerator SendScreenTimeEvents(')
        source = r'''using System;
using System.Collections;
using System.Collections.Generic;
using PlayFab;
using ClientModels = PlayFab.ClientModels;
using UnityEngine;
namespace UnityEngine { class WaitForSeconds { public WaitForSeconds(float seconds) {} } }
namespace PlayFab {
 static class PlayFabSettings {
  public static bool dropDevice,dropFocus; private static bool device,focus;
  public static bool DisableDeviceInfo { get=>device; set { if(!dropDevice) device=value; } }
  public static bool DisableFocusTimeCollection { get=>focus; set { if(!dropFocus) focus=value; } }
 }
 class PlayFabApiSettings { public bool DisableDeviceInfo=>PlayFabSettings.DisableDeviceInfo; }
 interface IPlayFabInstanceApi {}
 class PlayFabError {}
 namespace ClientModels { class DeviceInfoRequest { public Dictionary<string,object> Info; } }
 class PlayFabClientInstanceAPI : IPlayFabInstanceApi {
  public void ReportDeviceInfo(ClientModels.DeviceInfoRequest r,object ok,Action<PlayFabError> error,PlayFabApiSettings s) { State.requests++; }
 }
 static class PlayFabClientAPI {
  public static void ReportDeviceInfo(ClientModels.DeviceInfoRequest r,object ok,Action<PlayFabError> error,PlayFabApiSettings s) { State.requests++; }
 }
}
static class State { public static int identifierReads,requests,focusSends; }
class PlayFabDataGatherer { public PlayFabDataGatherer() { State.identifierReads++; } }
interface ISerializerPlugin { T DeserializeObject<T>(string s); string SerializeObject(object o); }
class Serializer : ISerializerPlugin {
 public T DeserializeObject<T>(string s) { return (T)(object)new Dictionary<string,object>(); }
 public string SerializeObject(object o) { return "synthetic"; }
}
static class PluginContract { public const int PlayFab_Serializer=0; }
static class PluginManager { public static T GetPlugin<T>(int k) { return (T)(object)new Serializer(); } }
class ScreenTime { public void Send() { State.focusSends++; } }
class Probe {
 private static bool _gatherDeviceInfo;
 private static ScreenTime screenTimeTracker=new ScreenTime();
 private static void OnGatherFail(PlayFabError error) {}
METHODS
 private static int cases;
 private static void Require(bool value) { if(!value) throw new Exception("case " + cases); cases++; }
 private static void Reset() {
  PlayFabSettings.dropDevice=PlayFabSettings.dropFocus=false;
  PlayFabSettings.DisableDeviceInfo=PlayFabSettings.DisableFocusTimeCollection=false;
  State.identifierReads=State.requests=State.focusSends=0; _gatherDeviceInfo=true;
 }
 private static bool Rejected() { try { ApplyLoginPrivacyDefaults(); return false; } catch(InvalidOperationException) { return true; } }
 public static int Main() {
  var settings=new PlayFabApiSettings();
  Reset(); _gatherDeviceInfo=false; SendDeviceInfoToPlayFab(settings,null);
  Require(State.identifierReads==0 && State.requests==0);
  Reset(); SendDeviceInfoToPlayFab(settings,null);
  Require(State.identifierReads==1 && State.requests==1);
  Reset(); SendDeviceInfoToPlayFab(settings,new PlayFabClientInstanceAPI());
  Require(State.identifierReads==1 && State.requests==1);
  Reset(); ApplyLoginPrivacyDefaults(); SendDeviceInfoToPlayFab(settings,null);
  Require(State.identifierReads==0 && State.requests==0 && PlayFabSettings.DisableFocusTimeCollection);
  Reset(); ApplyLoginPrivacyDefaults(); ApplyLoginPrivacyDefaults(); SendDeviceInfoToPlayFab(settings,new PlayFabClientInstanceAPI());
  Require(State.identifierReads==0 && State.requests==0 && PlayFabSettings.DisableFocusTimeCollection);
  Reset(); var old=SendScreenTimeEvents(1); bool first=old.MoveNext(); PlayFabSettings.DisableFocusTimeCollection=true;
  Require(first && State.focusSends==1 && !old.MoveNext() && State.focusSends==1);
  Reset(); ApplyLoginPrivacyDefaults(); var guarded=SendScreenTimeEvents(1);
  Require(!guarded.MoveNext() && State.focusSends==0);
  Reset(); PlayFabSettings.dropDevice=true;
  Require(Rejected() && State.identifierReads==0 && State.requests==0);
  Reset(); PlayFabSettings.dropFocus=true;
  Require(Rejected() && State.identifierReads==0 && State.requests==0);
  Console.WriteLine(cases); return 0;
 }
}'''.replace('METHODS', methods)
        output = ROOT / 'Logs/revival/login-privacy-reporting-checks' / uuid.uuid4().hex
        output.mkdir(parents=True)
        file = output / 'probe.cs'
        file.write_bytes(source.encode('utf-8'))
        exe = output / 'probe.exe'
        command = [str(EDITOR / 'netcorerun/netcorerun.exe'), str(EDITOR / 'DotNetSdkRoslyn/csc.dll'),
                   '-nologo', '-langversion:9.0', '-define:UNITY_ANDROID', '-target:exe', '-out:' + str(exe)]
        command += ['-r:' + str(EDITOR / 'UnityReferenceAssemblies/unity-4.8-api' / name)
                    for name in ('mscorlib.dll', 'System.dll', 'System.Core.dll')]
        compiled = subprocess.run([*command, str(file)], capture_output=True, timeout=60)
        (output / 'compile.private.log').write_bytes(compiled.stdout + compiled.stderr)
        self.assertEqual(compiled.returncode, 0, 'Inspect private compilation evidence')
        executed = subprocess.run([str(exe)], capture_output=True, timeout=30)
        (output / 'execute.private.log').write_bytes(executed.stdout + executed.stderr)
        self.assertEqual(executed.returncode, 0, 'Inspect private execution evidence')
        self.assertEqual(executed.stdout.decode().strip(), '9')
        self.assertTrue(all((ROOT / p).read_bytes() == value for p, value in raw.items()))
        (output / 'result.private.json').write_text(json.dumps(dict(
            cases=9, sourceSha256={p: hashlib.sha256(value).hexdigest() for p, value in raw.items()},
            actualBodiesExtracted=True, identifierAndNetworkBoundariesInert=True,
            operatingSdkCalls=0, operatingIdentityChanged=False, unityEditorLaunched=False,
            policyApproved=False), indent=2), encoding='utf-8')


if __name__ == '__main__':
    unittest.main()
