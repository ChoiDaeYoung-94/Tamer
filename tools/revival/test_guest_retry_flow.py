"""Run the actual login/failure/finally bodies with inert UI and authentication boundaries."""
from pathlib import Path
import argparse
import datetime
import hashlib
import json
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data')


def block(source, marker):
    start = source.index(marker)
    opening = source.index('{', start)
    depth, end = 1, opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence-dir', required=True, type=Path)
    args = parser.parse_args()
    out = args.evidence_dir
    if not out.is_absolute() or not out.resolve().is_relative_to((ROOT / 'Logs/revival').resolve()):
        raise ValueError('Use a new absolute evidence directory below this checkout Logs/revival')
    sys.path.insert(0, str(ROOT / 'tools/revival'))
    from private_ads_evidence import create_private_directory
    create_private_directory(out)
    login_path = ROOT / 'Assets/Scripts/Login/Login.cs'
    original = login_path.read_bytes()
    login = original.decode('utf-8-sig')
    markers = ('private void StartLogin()', 'private async UniTask RunLoginAsync(',
               'private void ShowRetry(')
    methods = '\n'.join(block(login, marker) for marker in markers)
    methods = methods.replace('UniTask<', 'Task<').replace('async UniTask ', 'async Task ')
    queries = []
    for marker in ('private bool CanOfferFirstEntry()', 'private bool HasEligibleFirstEntryState()'):
        start = login.index(marker)
        queries.append(login[start:login.index(';', start)+1])
    start = login.index('public static bool CanOfferGoogleSignup(')
    policy = login[start:login.index(';', start)+1]
    constants = []
    for name in ('PrefsKeyLoginMode', 'PrefsKeyGpgsId', 'PrefsKeyCustomId', 'PrefsKeyGoogleSignupPending'):
        start = login.index('private const string ' + name)
        constants.append(login[start:login.index(';', start)+1])
    helper_path = ROOT / 'Assets/Scripts/Login/LoginGuestEntry.cs'
    helper_source = helper_path.read_bytes()
    helper_copy = out / 'guest-entry-source.cs'
    helper_copy.write_bytes(helper_source)
    gate = block(login, 'internal sealed class LoginOperationGate')
    source = r'''using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
static class TaskRecorder { public static Task Last; public static void Forget(this Task task) { Last=task; } }
namespace AD {
class Box { public bool activeSelf; public void SetActive(bool value) { activeSelf=value; } }
class Text { public string text; }
class InitialAccountChoiceView { public Box gameObject=new Box(); }
class DataManager {
 public const string DeletionLoginPauseKey="pause";
 public bool ReceiptOnly,DeletionInProgress,Progress; private bool _known;
 public bool HasKnownAccount { get=>ReceiptOnly || _known; set { _known=value; } }
 public int Generation,Epoch; public void SuspendAccountSession() { Generation++; }
 public int AccountGeneration=>Generation;
 public int DeletionEpoch=>Epoch;
 public void UpdatePlayerData() { }
}
static class Managers { public static DataManager DataM; }
static class PlayerPrefs {
 public static Dictionary<string,string> Values=new Dictionary<string,string>();
 public static bool HasKey(string key)=>Values.ContainsKey(key);
 public static string GetString(string key,string fallback)=>Values.TryGetValue(key,out var value)?value:fallback;
 public static int GetInt(string key,int fallback)=>Values.TryGetValue(key,out var value)?int.Parse(value):fallback;
}
class LoginContinuityPolicy { POLICY }
class FakeSignupGate {
 public bool Offered;
 public bool Offer(string id,int generation,int epoch,bool eligible)=>Offered && eligible;
 public void Revoke() { }
}
GATE
class Probe {
 CONSTANTS
 private static LoginGuestIdentityPreparation GuestIdentities=new LoginGuestIdentityPreparation();
 private DataManager _dataOwner;
 private CancellationTokenSource _cts=new CancellationTokenSource();
 private bool _accountDeleted,_loginCaptured,_receiptRecoverySignIn;
 private int _loginGeneration,_loginDeletionEpoch;
 private string _pendingFirstEntryRetry,_signupCandidateIdentity,_loginFailureMessage,_diagnosticPhase;
 private LoginOperationGate _operations=new LoginOperationGate();
 private LoginGuestEntryGate _firstEntry=new LoginGuestEntryGate();
 private InitialAccountChoiceView _firstEntryView=new InitialAccountChoiceView();
 private FakeSignupGate _googleSignup=new FakeSignupGate();
 private Box _loading=new Box(),_retry=new Box(),_nicknamePanel=new Box(),_signupPanel=new Box();
 private Text _retryText=new Text();
 private int AuthCalls,ChoiceOffers,PrematureOffers;
 private bool Network=true,ThrowAuth,AuthSuccess;
 private TaskCompletionSource<bool> HeldNetwork;
 private void CaptureLoginSession() {
  _loginCaptured=true; _loginGeneration=_dataOwner.Generation; _loginDeletionEpoch=_dataOwner.Epoch;
 }
 private bool LoginCurrent()=>ReferenceEquals(_dataOwner,Managers.DataM) &&
  _loginGeneration==_dataOwner.Generation && _loginDeletionEpoch==_dataOwner.Epoch;
 private bool LoginCancelled(CancellationToken token)=>token.IsCancellationRequested || !LoginCurrent();
 private Task<bool> WaitForNetworkAsync(CancellationToken token) {
  token.ThrowIfCancellationRequested(); return HeldNetwork?.Task ?? Task.FromResult(Network);
 }
 private Task<bool> LoginOnAndroidAsync(CancellationToken token) {
  AuthCalls++; if(ThrowAuth) throw new InvalidOperationException("synthetic authentication failure");
  if(AuthSuccess) _dataOwner.HasKnownAccount=true;
  return Task.FromResult(AuthSuccess);
 }
 private Task<bool> LoginWithGoogleSignupAsync(CancellationToken token)=>LoginOnAndroidAsync(token);
 private Task<bool> WaitForServerAsync(CancellationToken token)=>Task.FromResult(true);
 private Task ResolveProfileAsync(CancellationToken token) { throw new InvalidOperationException("synthetic profile failure"); }
 private static string DescribeRequestException(Exception e)=>"Synthetic";
 private static void LogStep(string message) { }
 private void EnsureRetryMessage() { }
 private bool CanOfferGoogleSignup()=>_googleSignup.Offered;
 private void ShowGoogleSignupChoice() { _signupPanel.SetActive(true); }
 private void ShowDeletedAccountNotice() { }
 private void ShowReceiptOnlyBlocked() { }
 private void ShowLoading(string message) { _retry.SetActive(false); _firstEntryView?.gameObject.SetActive(false); }
 // UI presentation is inert; its request timing and actual eligibility queries are observable.
 private bool TryShowFirstEntryChoice(string message=null) {
  if(_operations.IsRunning) { PrematureOffers++; return false; }
  if(!CanOfferFirstEntry() || !LoginCurrent()) return false;
  ChoiceOffers++; _firstEntryView.gameObject.SetActive(true); _retry.SetActive(false); return true;
 }
 private bool HasLocalProgress()=>_dataOwner.Progress;
 QUERIES
 METHODS
 private static void Require(bool condition,string label) { if(!condition) throw new Exception(label); }
 private static Probe Fresh() {
  PlayerPrefs.Values.Clear(); Managers.DataM=new DataManager();
  Advertising.PrivatePrivacyReleaseContract.IsPrivatePrivacyTrial=false;
  GuestIdentities=new LoginGuestIdentityPreparation();
  return new Probe { _dataOwner=Managers.DataM };
 }
 private void Run() { StartLogin(); TaskRecorder.Last.GetAwaiter().GetResult(); }
 public static int Main() {
  int cases=0;
  var p=Fresh(); p.Run();
  Require(p.AuthCalls==1 && p.ChoiceOffers==1 && p.PrematureOffers==0,"auth failure returns choice only after finally");
  Require(p._pendingFirstEntryRetry==null && !p._operations.IsRunning && p._firstEntryView.gameObject.activeSelf,"retry consumed");cases++;
  p=Fresh();p.Network=false;p.Run();
  Require(p.AuthCalls==0 && p.ChoiceOffers==1 && p.PrematureOffers==0,"network failure returns choice");cases++;
  p=Fresh();p.ThrowAuth=true;p.Run();
  Require(p.AuthCalls==1 && p.ChoiceOffers==1 && p.PrematureOffers==0,"auth exception returns choice");cases++;
  p=Fresh();p._firstEntryView=null;p.Run();
  Require(p.AuthCalls==1 && p.ChoiceOffers==0 && p._retry.activeSelf,"legacy retry without first choice stays unchanged");cases++;
  foreach(var mode in new[]{"gpgs","custom","custom-pending"}) {
   p=Fresh();PlayerPrefs.Values[PrefsKeyLoginMode]=mode;p.Run();
   Require(p.ChoiceOffers==0 && p._retry.activeSelf,"saved mode cannot offer a new identity: "+mode);cases++;
  }
  p=Fresh();p._accountDeleted=true;p.RunDirectForDeleted();
  Require(p.ChoiceOffers==0 && p._retry.activeSelf,"deleted cannot offer a new identity");cases++;
  p=Fresh();p.AuthSuccess=true;p.Run();
  Require(p._dataOwner.HasKnownAccount && p.ChoiceOffers==0 && p._retry.activeSelf,"profile failure retains authenticated ownership");cases++;
  p=Fresh();p._googleSignup.Offered=true;p.Run();
  Require(p.ChoiceOffers==0 && p._signupPanel.activeSelf && p._pendingFirstEntryRetry==null,"Google signup UI takes precedence");cases++;
  p=Fresh();p.HeldNetwork=new TaskCompletionSource<bool>();p.StartLogin();
  Require(p._operations.IsRunning && p.ChoiceOffers==0 && p.AuthCalls==0,"held operation has no first-choice retry");
  p.HeldNetwork.SetResult(false);TaskRecorder.Last.GetAwaiter().GetResult();
  Require(p.ChoiceOffers==1 && !p._operations.IsRunning && p.PrematureOffers==0,"delayed failure resolves after operation");cases++;
  p=Fresh();p.HeldNetwork=new TaskCompletionSource<bool>();p.StartLogin();
  p._cts.Cancel();p.HeldNetwork.SetResult(true);TaskRecorder.Last.GetAwaiter().GetResult();
  Require(p.ChoiceOffers==0 && !p._operations.IsRunning,"cancelled operation cannot reopen choices");cases++;
  p=Fresh();p.HeldNetwork=new TaskCompletionSource<bool>();p.StartLogin();
  Managers.DataM=new DataManager();p.HeldNetwork.SetResult(true);TaskRecorder.Last.GetAwaiter().GetResult();
  Require(p.ChoiceOffers==0 && !p._operations.IsRunning,"old manager cannot reopen choices");cases++;
  p=Fresh();p.Run();p.Run();
  Require(p.ChoiceOffers==2 && p.PrematureOffers==0 && p._pendingFirstEntryRetry==null,"two explicit attempts do not retain old deferred state");cases++;
  foreach(var key in new[]{PrefsKeyCustomId,PrefsKeyGpgsId,PrefsKeyGoogleSignupPending}) {
   p=Fresh();PlayerPrefs.Values[key]="saved-state";p.Run();
   Require(p.ChoiceOffers==0 && p._retry.activeSelf,"saved identity/pending must not reopen first choices: "+key);cases++;
  }
  p=Fresh();PlayerPrefs.Values[DataManager.DeletionLoginPauseKey]="1";p.Run();
  Require(p.ChoiceOffers==0,"deletion pause must not reopen choices");cases++;
  p=Fresh();p._receiptRecoverySignIn=true;p.Run();Require(p.ChoiceOffers==0,"receipt recovery");cases++;
  p=Fresh();Advertising.PrivatePrivacyReleaseContract.IsPrivatePrivacyTrial=true;p.Run();
  Require(p.ChoiceOffers==0,"private trial must not reopen choices");cases++;
  p=Fresh();p.HeldNetwork=new TaskCompletionSource<bool>();p.StartLogin();
  p._dataOwner.DeletionInProgress=true;p.HeldNetwork.SetResult(false);TaskRecorder.Last.GetAwaiter().GetResult();
  Require(p.ChoiceOffers==0,"deletion started during operation");cases++;
  p=Fresh();p.HeldNetwork=new TaskCompletionSource<bool>();p.StartLogin();
  p._dataOwner.Progress=true;p.HeldNetwork.SetResult(false);TaskRecorder.Last.GetAwaiter().GetResult();
  Require(p.ChoiceOffers==0,"progress became known during operation");cases++;
  Console.WriteLine(cases);return 0;
 }
 private void RunDirectForDeleted() {
  CaptureLoginSession(); _operations.TryBeginLogin();
  RunLoginAsync(_cts.Token).GetAwaiter().GetResult();
 }
}
}
namespace AD.Advertising { static class PrivatePrivacyReleaseContract { public static bool IsPrivatePrivacyTrial; } }
'''.replace('GATE', gate).replace('METHODS', methods).replace('QUERIES','\n'.join(queries)).replace('POLICY',policy).replace('CONSTANTS','\n'.join(constants))
    file = out / 'probe.cs'
    file.write_text(source, encoding='utf-8')
    exe = out / 'probe.exe'
    compiler = EDITOR / 'DotNetSdkRoslyn/csc.dll'
    run = [str(EDITOR / 'netcorerun/netcorerun.exe'), str(compiler), '-nologo',
           '-langversion:9.0', '-define:UNITY_ANDROID', '-target:exe', '-out:' + str(exe)]
    run += ['-r:' + str(EDITOR / 'UnityReferenceAssemblies/unity-4.8-api' / name)
            for name in ('mscorlib.dll', 'System.dll', 'System.Core.dll')]
    compiled = subprocess.run([*run, str(file), str(helper_copy)], capture_output=True, cwd=out,
                              creationflags=subprocess.CREATE_NO_WINDOW, timeout=60)
    (out / 'compile.private.log').write_bytes(compiled.stdout + compiled.stderr)
    assert compiled.returncode == 0, 'Inspect private compilation evidence'
    executed = subprocess.run([str(exe)], capture_output=True, cwd=out,
                              creationflags=subprocess.CREATE_NO_WINDOW, timeout=30)
    (out / 'execute.private.log').write_bytes(executed.stdout + executed.stderr)
    assert executed.returncode == 0, 'Inspect private execution evidence'
    count = int(executed.stdout.decode().strip())
    assert count == 22, count
    assert login_path.read_bytes() == original, 'Source changed during extraction check'
    assert helper_path.read_bytes() == helper_source, 'Guest helper changed during extraction check'
    result = dict(atUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  checkout=str(ROOT),
                  head=subprocess.check_output(['git', '-C', str(ROOT), 'rev-parse', 'HEAD'],text=True).strip(),
                  branch=subprocess.check_output(['git', '-C', str(ROOT), 'branch', '--show-current'],text=True).strip(),
                  loginSourceSha256=hashlib.sha256(login_path.read_bytes()).hexdigest(),
                  compilerSha256=hashlib.sha256(compiler.read_bytes()).hexdigest(),
                  guestHelperSha256=hashlib.sha256(helper_source).hexdigest(),
                  cases=count,compileExitCode=compiled.returncode,executionExitCode=executed.returncode,
                  actualBodies=list(markers),actualOperationGate=True,
                  actualFirstEntryQueries=True,actualGuestPolicyAndIdentitySourceCompiled=True,
                  uniTaskReplacedWithTask=True,uiAndAuthInert=True,actualSdkOrAccountCalls=0,
                  appBuilds=0,deviceRuns=0,uiRendered=False)
    (out / 'result.private.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
