"""Installed SDK login/reporting chain with server flags and inert transport."""
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
class LoginPrivacyServerFlagChecks(unittest.TestCase):
    def test_installed_login_chain_with_server_reporting_enabled(self):
        paths = ['Assets/Scripts/Login/Login.cs',
                 'Assets/ThirdParty/PlayFabSDK/Client/PlayFabDeviceUtil.cs',
                 'Assets/ThirdParty/PlayFabSDK/Entity/ScreenTimeTracker.cs',
                 'Assets/ThirdParty/PlayFabSDK/Shared/Internal/PlayFabHttp/PlayFabHTTP.cs']
        raw = {p: (ROOT / p).read_bytes() for p in paths}
        login, device, tracker, http = (raw[p].decode('utf-8-sig') for p in paths)
        methods = '\n'.join(block(http, marker) for marker in (
            'public static void InitializeScreenTimeTracker(',
            'private static IEnumerator SendScreenTimeEvents(',
            'public void OnApplicationFocus(', 'public void OnApplicationQuit('))
        source = r'''using System;
using System.Collections;
using System.Collections.Generic;
using PlayFab;
using ClientModels = PlayFab.ClientModels;
using PlayFab.Public;
using PlayFab.Internal;
using UnityEngine;
namespace UnityEngine {
 public class WaitForSeconds { public WaitForSeconds(float seconds) {} }
 public static class SystemInfo {
  public static string deviceType { get { State.modelReads++; return "fake-type"; } }
  public static string deviceModel { get { State.modelReads++; return "fake-model"; } }
  public static string operatingSystem { get { State.modelReads++; return "fake-os"; } }
 }
 public static class Debug { public static void Log(object x) {} public static void LogWarning(object x) {} }
}
namespace PlayFab.SharedModels { public class PlayFabResultCommon {} }
namespace PlayFab {
 public static class PlayFabSettings {
  public static bool DisableDeviceInfo,DisableFocusTimeCollection;
  public static PlayFabAuthenticationContext staticPlayer=new PlayFabAuthenticationContext();
 }
 public class PlayFabAuthenticationContext { public bool IsClientLoggedIn() { return true; } }
 public class PlayFabApiSettings {
  public bool DisableDeviceInfo { get=>PlayFabSettings.DisableDeviceInfo; set=>PlayFabSettings.DisableDeviceInfo=value; }
  public bool DisableFocusTimeCollection { get=>PlayFabSettings.DisableFocusTimeCollection; set=>PlayFabSettings.DisableFocusTimeCollection=value; }
 }
 public interface IPlayFabInstanceApi {}
 public class PlayFabError { public string GenerateErrorReport() { return "inert"; } }
 public class PlayFabClientInstanceAPI : IPlayFabInstanceApi {
  public void ReportDeviceInfo(ClientModels.DeviceInfoRequest r,object ok,Action<PlayFabError> error,PlayFabApiSettings s) { State.deviceRequests++; }
 }
 public static class PlayFabClientAPI {
  public static void ReportDeviceInfo(ClientModels.DeviceInfoRequest r,object ok,Action<PlayFabError> error,PlayFabApiSettings s) { State.deviceRequests++; }
 }
 public class PlayFabEventsInstanceAPI {
  public PlayFabEventsInstanceAPI(PlayFabAuthenticationContext c) {}
  public void WriteEvents(EventsModels.WriteEventsRequest r,Action<EventsModels.WriteEventsResponse> ok,Action<PlayFabError> fail) {
   State.focusRequests++; State.sentEvents+=r.Events.Count;
  }
 }
 namespace ClientModels {
  public class UserSettings { public bool NeedsAttribution,GatherDeviceInfo,GatherFocusInfo; }
  public class EntityKey { public string Id,Type; }
  public class EntityTokenResponse { public EntityKey Entity; }
  public class LoginResult : SharedModels.PlayFabResultCommon { public UserSettings SettingsForUser; public string PlayFabId; public EntityTokenResponse EntityToken; }
  public class RegisterPlayFabUserResult : SharedModels.PlayFabResultCommon { public UserSettings SettingsForUser; public string PlayFabId; public EntityTokenResponse EntityToken; }
  public class DeviceInfoRequest { public Dictionary<string,object> Info; }
 }
 namespace EventsModels {
  public class EntityKey { public string Id,Type; }
  public class EventContents { public string Name,EventNamespace; public EntityKey Entity; public DateTime OriginalTimestamp; public object Payload; }
  public class WriteEventsRequest { public List<EventContents> Events; }
  public class WriteEventsResponse {}
 }
}
namespace PlayFab.Internal {
 public class PlayFabDataGatherer { public PlayFabDataGatherer() { State.identifierReads++; } }
 public interface ISerializerPlugin { T DeserializeObject<T>(string s); string SerializeObject(object o); }
 public class Serializer : ISerializerPlugin {
  public T DeserializeObject<T>(string s) { return (T)(object)new Dictionary<string,object>(); }
  public string SerializeObject(object o) { return "inert"; }
 }
 public static class PluginContract { public const int PlayFab_Serializer=0; }
 public static class PluginManager { public static T GetPlugin<T>(int k) { return (T)(object)new Serializer(); } }
 public class CoroutineHost {
  public IEnumerator pending;
  public void StartCoroutine(IEnumerator routine) { pending=routine; State.routines++; routine.MoveNext(); }
 }
 public class PlayFabHttp {
  private static IScreenTimeTracker screenTimeTracker=new ScreenTimeTracker();
  private static CoroutineHost instance=new CoroutineHost();
  private const float delayBetweenBatches=1;
HTTP_METHODS
  public static void Reset() { screenTimeTracker=new ScreenTimeTracker(); instance=new CoroutineHost(); }
  public static void Tick() { if(instance.pending!=null) instance.pending.MoveNext(); }
 }
}
public static class State {
 public static int identifierReads,modelReads,deviceRequests,focusRequests,sentEvents,routines;
 public static void Reset() {
  identifierReads=modelReads=deviceRequests=focusRequests=sentEvents=routines=0;
  PlayFabSettings.DisableDeviceInfo=PlayFabSettings.DisableFocusTimeCollection=false;
  PlayFabHttp.Reset();
 }
 public static bool NoRequests=>deviceRequests==0 && focusRequests==0;
}
public class Probe {
LOGIN_DEFAULTS
 private static int cases;
 private static void Require(bool v) { if(!v) throw new Exception("case "+cases); cases++; }
 private static void Login(bool gather,bool entity,bool instanceApi=false,bool registration=false) {
  var settings=new ClientModels.UserSettings { GatherDeviceInfo=gather,GatherFocusInfo=gather,NeedsAttribution=true };
  var token=entity?new ClientModels.EntityTokenResponse { Entity=new ClientModels.EntityKey { Id="inert-entity",Type="title_player_account" } }:null;
  PlayFab.SharedModels.PlayFabResultCommon response=registration
   ?(PlayFab.SharedModels.PlayFabResultCommon)new ClientModels.RegisterPlayFabUserResult { SettingsForUser=settings,PlayFabId="inert-player",EntityToken=token }
   :new ClientModels.LoginResult { SettingsForUser=settings,PlayFabId="inert-player",EntityToken=token };
  PlayFabDeviceUtil.OnPlayFabLogin(response,new PlayFabApiSettings(),instanceApi?new PlayFabClientInstanceAPI():null);
 }
 public static int Main() {
  State.Reset(); Login(true,true);
  Require(State.identifierReads==1 && State.deviceRequests==1 && State.modelReads==3 && State.focusRequests==1 && State.sentEvents==2);
  new PlayFabHttp().OnApplicationFocus(false);
  Require(State.focusRequests==2 && State.sentEvents==3);
  State.Reset(); Login(true,true,true,true);
  Require(State.identifierReads==1 && State.deviceRequests==1 && State.focusRequests==1);
  State.Reset(); ApplyLoginPrivacyDefaults(); Login(true,true);
  Require(State.identifierReads==0 && State.NoRequests && PlayFabSettings.DisableDeviceInfo && PlayFabSettings.DisableFocusTimeCollection);
  Require(State.routines==1 && State.modelReads==3); // Local SDK queue construction still occurs; do not claim no collection.
  PlayFabHttp.Tick(); new PlayFabHttp().OnApplicationFocus(false); new PlayFabHttp().OnApplicationQuit();
  Require(State.NoRequests && State.sentEvents==0);
  Login(true,true,true,true);
  Require(State.identifierReads==0 && State.NoRequests && PlayFabSettings.DisableFocusTimeCollection);
  State.Reset(); ApplyLoginPrivacyDefaults(); Login(false,true);
  Require(State.identifierReads==0 && State.NoRequests && State.modelReads==0 && State.routines==0);
  State.Reset(); ApplyLoginPrivacyDefaults(); Login(true,false);
  Require(State.identifierReads==0 && State.NoRequests && State.modelReads==0 && State.routines==0);
  State.Reset(); ApplyLoginPrivacyDefaults(); PlayFabDeviceUtil.OnPlayFabLogin(new PlayFab.SharedModels.PlayFabResultCommon(),new PlayFabApiSettings(),null);
  Require(State.identifierReads==0 && State.NoRequests && State.modelReads==0 && State.routines==0);
  Console.WriteLine(cases); return 0;
 }
}'''.replace('HTTP_METHODS', methods).replace('LOGIN_DEFAULTS', block(login, 'private static void ApplyLoginPrivacyDefaults()'))
        output = ROOT / 'Logs/revival/login-privacy-server-flags' / uuid.uuid4().hex
        output.mkdir(parents=True)
        files = []
        for name, text in [('probe.cs', source), ('device.cs', device), ('tracker.cs', tracker)]:
            path = output / name
            path.write_bytes(text.encode('utf-8'))
            files.append(str(path))
        exe = output / 'probe.exe'
        command = [str(EDITOR / 'netcorerun/netcorerun.exe'), str(EDITOR / 'DotNetSdkRoslyn/csc.dll'),
                   '-nologo', '-langversion:9.0', '-define:UNITY_ANDROID', '-target:exe', '-out:' + str(exe)]
        command += ['-r:' + str(EDITOR / 'UnityReferenceAssemblies/unity-4.8-api' / name)
                    for name in ('mscorlib.dll', 'System.dll', 'System.Core.dll')]
        compiled = subprocess.run([*command, *files], capture_output=True, timeout=60)
        (output / 'compile.private.log').write_bytes(compiled.stdout + compiled.stderr)
        self.assertEqual(compiled.returncode, 0, 'Inspect private compilation evidence')
        executed = subprocess.run([str(exe)], capture_output=True, timeout=30)
        (output / 'execute.private.log').write_bytes(executed.stdout + executed.stderr)
        self.assertEqual(executed.returncode, 0, 'Inspect private execution evidence')
        self.assertEqual(executed.stdout.decode().strip(), '10')
        self.assertTrue(all((ROOT / p).read_bytes() == value for p, value in raw.items()))
        (output / 'result.private.json').write_text(json.dumps(dict(
            cases=10, sourceSha256={p: hashlib.sha256(value).hexdigest() for p, value in raw.items()},
            actualLoginResponseAndTrackerSource=True, identifierAndNetworkBoundariesInert=True,
            positiveUnprotectedControls=True, localSessionQueueStillConstructed=True,
            operatingSdkCalls=0, unityEditorLaunched=False, policyApproved=False), indent=2), encoding='utf-8')


if __name__ == '__main__':
    unittest.main()
