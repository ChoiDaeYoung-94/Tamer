"""Exercise actual signup/guard/callback source with inert SDK and persistence boundaries."""
from pathlib import Path
import hashlib
import json
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data')


def block(source, marker):
    start = source.index(marker)
    opening = source.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def main():
    login = (ROOT / 'Assets/Scripts/Login/Login.cs').read_text(encoding='utf-8-sig')
    members = [block(login, marker) for marker in (
        'private async UniTask<bool> LoginWithGoogleSignupAsync(',
        'private static LoginWithGooglePlayGamesServicesRequest CreateGoogleSignupRequest(',
        'private static LoginWithGooglePlayGamesServicesRequest CreateGoogleLoginRequest(',
        'private static bool PrivateGoogleSignupApproved()',
        'private struct ApiResult<T>',
        'private async UniTask<ApiResult<T>> CallAsync<T>(')]
    begin = login.index('private bool CanOfferGoogleSignup(')
    members.append(login[begin:login.index(';', begin) + 1])
    begin = login.index('private static string GoogleSignupBinding(')
    members.append(login[begin:login.index(';', begin) + 1])
    policy_begin = login.index('public static bool CanOfferGoogleSignup(')
    policy = login[policy_begin:login.index(';', policy_begin) + 1]
    policy += '\n' + login[login.index('public static bool MatchesKnownPlayFabAccount('):
        login.index(';', login.index('public static bool MatchesKnownPlayFabAccount(')) + 1]
    gates = '\n'.join(block(login, 'internal sealed class ' + name)
                      for name in ('LoginGoogleSignupGate', 'LoginCallbackGate'))
    sdk_enum = block((ROOT / 'Assets/ThirdParty/PlayFabSDK/Shared/Internal/PlayFabErrors.cs')
                     .read_text(encoding='utf-8-sig'), 'public enum PlayFabErrorCode')
    source = r'''using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
SDK_ENUM
class PlayFabError { public PlayFabErrorCode Error; }
class PlayFabAuthenticationContext { }
class LoginResult { public string PlayFabId; public bool NewlyCreated; public PlayFabAuthenticationContext AuthenticationContext; }
class LoginWithGooglePlayGamesServicesRequest { public bool? CreateAccount; public string ServerAuthCode; public PlayFabAuthenticationContext AuthenticationContext; }
class DataManager { public const string DeletionLoginPauseKey="pause"; public bool HasKnownAccount, DeletionInProgress, Progress; public string PlayFabId=""; }
class PlayFabSettings { public static string TitleId="test-title"; }
class Application { public static string identifier="test-package"; }
class TextAsset { public string text; }
class Resources { public static TextAsset Grant; public static T Load<T>(string name) where T:class { return Grant as T; } }
namespace AD.Advertising { class PrivatePrivacyReleaseContract { public static bool IsPrivatePrivacyTrial; } }
class PlayerPrefs {
 public static Dictionary<string,string> Values=new Dictionary<string,string>();
 public static bool HasKey(string key)=>Values.ContainsKey(key);
 public static string GetString(string key,string fallback)=>HasKey(key)?Values[key]:fallback;
 public static int GetInt(string key,int fallback)=>HasKey(key)?int.Parse(Values[key]):fallback;
 public static void SetString(string key,string value) { Values[key]=value; }
 public static void Save() { }
}
class CloudScriptDeletionClient { public const string SupportEmail="support.invalid"; }
class PlayGamesPlatform {
 public static PlayGamesPlatform Instance=new PlayGamesPlatform(); public static string Identity="profile-one";
 public bool IsAuthenticated()=>true; public string GetUserId()=>Identity;
}
class PlayFabClientAPI {
 public static int Calls; public static Action<LoginResult> Late;
 public static void LoginWithGooglePlayGamesServices(LoginWithGooglePlayGamesServicesRequest request,Action<LoginResult> ok,Action<PlayFabError> fail) {
  Calls++; if(request.CreateAccount!=true || request.ServerAuthCode!="fresh-code" || !PlayerPrefs.HasKey("pending")) throw new Exception("guard");
  Late=ok;
  if(Probe.Case==9) return;
  if(Probe.Case==10) { fail(new PlayFabError{Error=PlayFabErrorCode.AccountDeleted}); return; }
  if(Probe.Case==11) PlayGamesPlatform.Identity="profile-other";
  if(Probe.Case==12) Probe.Current.Cancelled=true;
  if(Probe.Case==13) { ok(null); return; }
  var response=new LoginResult{PlayFabId="existing-or-new",NewlyCreated=Probe.Case!=14,AuthenticationContext=new PlayFabAuthenticationContext()};
  ok(response); ok(new LoginResult{PlayFabId="duplicate",NewlyCreated=true,AuthenticationContext=new PlayFabAuthenticationContext()});
 }
}
class LoginContinuityPolicy { POLICY }
GATES
class Probe {
 public static int Case; public static Probe Current;
 private const string PrefsKeyLoginMode="mode", PrefsKeyGpgsId="cached", PrefsKeyGoogleSignupPending="pending", LoginModeGpgs="gpgs";
 private DataManager _dataOwner=new DataManager(); private bool _accountDeleted, _receiptRecoverySignIn;
 private int _loginGeneration=1, _loginDeletionEpoch=2; private string _selectedGpgsId,_loginFailureMessage,_diagnosticPhase;
 private LoginGoogleSignupGate _googleSignup=new LoginGoogleSignupGate();
 private TimeSpan ApiTimeout=TimeSpan.FromSeconds(20);
 public bool Cancelled; private int FreshCalls,Adoptions; private bool AdoptedNew;
 private bool LoginCurrent()=>!Cancelled;
 private bool LoginCancelled(CancellationToken token)=>Cancelled || token.IsCancellationRequested;
 private bool HasLocalProgress()=>_dataOwner.Progress;
 private Task<string> RequestGoogleServerCodeAsync(CancellationToken token) {
  FreshCalls++;
  if(Case==6) PlayGamesPlatform.Identity="profile-other";
  if(Case==7) _dataOwner.Progress=true;
  if(Case==8) return Task.FromResult(" ");
  return Task.FromResult("fresh-code");
 }
 private Task<bool> WaitUntilAsync(Func<bool> ready,TimeSpan timeout,CancellationToken token)=>Task.FromResult(ready());
 private static void LogStep(string value) { }
 private static string DescribeRequestException(Exception e)=>"Exception";
 private static string DescribeGoogleLoginOutcome(bool timeout,PlayFabError error,bool hasResult,bool newlyCreated)=>"Outcome";
 private void NoteLoginError(PlayFabError error) { if(error!=null && error.Error==PlayFabErrorCode.AccountDeleted) { _accountDeleted=true; _loginFailureMessage="deleted"; } }
 private void OnLoggedIn(string id,bool newlyCreated,string method,PlayFabAuthenticationContext context,string mode) { Adoptions++;AdoptedNew=newlyCreated; }
 MEMBERS
 static void Require(bool condition,string label) { if(!condition) throw new Exception(label); }
 public static int Main() {
  int count=0;
  // Every continuity, deletion, receipt, pending and private-scope veto is independently exercised.
  Require(LoginContinuityPolicy.CanOfferGoogleSignup(false,false,"","",false,false,false,false,true),"eligible"); count++;
  for(int i=0;i<9;i++) {
   Require(!LoginContinuityPolicy.CanOfferGoogleSignup(i==0,i==1,i==2?"gpgs":"",i==3?"cached":"",i==4,i==5,i==6,i==7,i!=8),"veto");count++;
  }
  var gate=new LoginGoogleSignupGate();
  Require(!gate.TryConsent("profile-one",1,2),"consent without missing-account offer");count++;
  Require(gate.Offer("profile-one",1,2,true),"offer");
  Require(!gate.TryConsent("profile-other",1,2) && !gate.TryConsent("profile-one",2,2) && !gate.TryConsent("profile-one",1,3),"binding");count++;
  Require(gate.TryConsent("profile-one",1,2) && gate.TryConsume("profile-one",1,2,true),"consume");
  Require(!gate.TryConsume("profile-one",1,2,true) && !gate.Offer("profile-one",1,2,true),"one shot");count++;
  gate=new LoginGoogleSignupGate();gate.Offer("profile-one",1,2,true);gate.TryConsent("profile-one",1,2);gate.Revoke();
  Require(!gate.TryConsume("profile-one",1,2,true),"revoke");count++;
  for(Case=0;Case<=14;Case++) {
   PlayerPrefs.Values.Clear();Resources.Grant=null;AD.Advertising.PrivatePrivacyReleaseContract.IsPrivatePrivacyTrial=false;
   PlayGamesPlatform.Identity="profile-one";PlayFabClientAPI.Calls=0;PlayFabClientAPI.Late=null;
   var p=Current=new Probe();p._googleSignup.Offer("profile-one",1,2,true);p._googleSignup.TryConsent("profile-one",1,2);
   if(Case==1) p._dataOwner.HasKnownAccount=true;
   if(Case==2) PlayerPrefs.SetString("pending","test-title\nprofile-one");
   if(Case==3) { AD.Advertising.PrivatePrivacyReleaseContract.IsPrivatePrivacyTrial=true; }
   if(Case==4) { AD.Advertising.PrivatePrivacyReleaseContract.IsPrivatePrivacyTrial=true;Resources.Grant=new TextAsset{text="explicit-google-signup-v1\nwrong-title\ntest-package\n"}; }
   if(Case==5) { AD.Advertising.PrivatePrivacyReleaseContract.IsPrivatePrivacyTrial=true;Resources.Grant=new TextAsset{text="explicit-google-signup-v1\ntest-title\ntest-package\n"}; }
   bool result=p.LoginWithGoogleSignupAsync(CancellationToken.None).GetAwaiter().GetResult();
   bool success=Case==0 || Case==5 || Case==14;
   Require(result==success,"signup result "+Case);
   Require(p.Adoptions==(success?1:0),"adoption "+Case);
   Require(PlayFabClientAPI.Calls==((Case==0 || Case>=5 && Case!=6 && Case!=7 && Case!=8)?1:0),"exchange count "+Case);
   if(success) Require(p.AdoptedNew==(Case!=14),"new/existing response");
   if(Case==9) { PlayFabClientAPI.Late(new LoginResult{PlayFabId="late",NewlyCreated=true});Require(p.Adoptions==0 && PlayerPrefs.HasKey("pending"),"late callback/pending"); }
   if(Case==10) Require(p._accountDeleted && p._loginFailureMessage=="deleted","deletion message");
   if(PlayFabClientAPI.Calls>0) Require(PlayerPrefs.GetString("pending","")=="test-title\nprofile-one","identity-bound pending");
   Require(CreateGoogleLoginRequest("ordinary-code").CreateAccount==false,"ordinary login cannot create");
   count++;
  }
  Console.WriteLine(count);return 0;
 }
}'''
    source = source.replace('SDK_ENUM', sdk_enum).replace('POLICY', policy).replace('GATES', gates)
    extracted = '\n'.join(members).replace('UniTask<', 'Task<').replace('UniTask.CompletedTask', 'Task.CompletedTask')
    source = source.replace('MEMBERS', extracted)
    out = ROOT / 'Logs/revival/explicit-google-signup-checks' / uuid.uuid4().hex
    out.mkdir(parents=True)
    file = out / 'probe.cs'
    file.write_text(source)
    exe = out / 'probe.exe'
    args = [str(EDITOR / 'netcorerun/netcorerun.exe'), str(EDITOR / 'DotNetSdkRoslyn/csc.dll'),
            '-nologo', '-langversion:9.0', '-define:UNITY_ANDROID', '-target:exe', '-out:' + str(exe)]
    args += ['-r:' + str(EDITOR / 'UnityReferenceAssemblies/unity-4.8-api' / name)
             for name in ('mscorlib.dll', 'System.dll', 'System.Core.dll')]
    compiled = subprocess.run([*args, str(file)], capture_output=True, cwd=out)
    (out / 'compile.log').write_bytes(compiled.stdout + compiled.stderr)
    assert compiled.returncode == 0, 'Compile failed; inspect private compile.log'
    executed = subprocess.run([str(exe)], capture_output=True, cwd=out)
    (out / 'execute.log').write_bytes(executed.stdout + executed.stderr)
    assert executed.returncode == 0, 'Probe failed; inspect private execute.log'
    assert executed.stdout.decode().strip() == '29'
    report = dict(sourceSha256=hashlib.sha256((ROOT / 'Assets/Scripts/Login/Login.cs').read_bytes()).hexdigest(),
                  cases=29, actualSignupAndCallbackBodies=True, uniTaskReplacedWithTask=True,
                  sdkAndPersistenceInert=True, uiRendered=False, realSessionAdoptionExecuted=False,
                  realSdkExecutions=0, builds=0, deviceRuns=0)
    (out / 'result.json').write_text(json.dumps(report, indent=2))
    print(json.dumps(dict(output=str(out), **report)))


if __name__ == '__main__':
    main()
