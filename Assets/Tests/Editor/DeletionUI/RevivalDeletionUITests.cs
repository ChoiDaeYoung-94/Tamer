using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AD.Privacy;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class RevivalDeletionUITests
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    GameObject root, panel;
    Component view, presenter;
    Scene preview;
    static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    Button Button(string name) => (Button)Property(view, name + "Button");
    string Message => (string)Property(Property(view, "Message"), "text");
    void Render() => Call(presenter, "Render");
    void Click(string name) { Button(name).onClick.Invoke(); Render(); }

    [SetUp] public void Setup()
    {
        preview = EditorSceneManager.NewPreviewScene();
        root = new GameObject("Deletion UI fixture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(root, preview);
        panel = new GameObject("Privacy", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        panel.SetActive(false);
        view = panel.AddComponent(Find("AD.DeletionView"));
        Call(view, "Build", null, (Action)(() => panel.SetActive(false)));
        presenter = panel.AddComponent(Find("AD.DeletionPresenter"));
        Call(presenter, "Bind", view);
        panel.SetActive(true);
        Call(presenter, "OnEnable");
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(root);
        EditorSceneManager.ClosePreviewScene(preview);
    }
    DeletionFlow Configure(Fake gateway)
    {
        var session = new DeletionSession(new object(), "synthetic-account", "synthetic-session");
        var flow = new DeletionFlow(gateway, () => session);
        Call(presenter, "ConfigureSynthetic", flow);
        return flow;
    }

    [Test] public void Revival_DeletionUnavailableExplainsDisabledRequestAndNeverCompletes()
    {
        StringAssert.Contains("currently unavailable", Message);
        Assert.That(Button("Request").interactable, Is.False);
        Click("Request");
        Assert.That(Button("Confirm").gameObject.activeSelf, Is.False);
        StringAssert.DoesNotContain("completed", Message);
    }

    [UnityTest] public IEnumerator Revival_DeletionButtonsFollowReauthenticationConfirmationAndServerStatus()
    {
        var gateway = new Fake { PendingAuth = new TaskCompletionSource<DeletionAuthorization>() };
        var flow = Configure(gateway);
        Click("Request");
        StringAssert.Contains("Identity verification", Message);
        Click("Request");
        Assert.That(gateway.AuthCalls, Is.EqualTo(1));
        gateway.PendingAuth.SetResult(new DeletionAuthorization("synthetic-account", "test-proof"));
        yield return null; yield return null;
        Render();
        Assert.That(Button("Confirm").gameObject.activeSelf, Is.True);
        Click("Confirm");
        StringAssert.Contains("not complete", Message);
        gateway.Next = DeletionState.Processing;
        Click("Refresh");
        StringAssert.Contains("Completion has not been confirmed", Message);
        gateway.Next = DeletionState.Completed;
        Click("Refresh");
        Assert.That(flow.State, Is.EqualTo(DeletionState.Completed));
        StringAssert.Contains("Test deletion completed", Message);
        StringAssert.Contains("No real account data was deleted", Message);
    }

    [TestCase("Confirm")]
    [TestCase("Cancel")]
    public void Revival_DeletionRetryPreservesFailedAction(string action)
    {
        var gateway = new Fake(); Configure(gateway); Click("Request");
        gateway.Fail = true; Click(action);
        Assert.That(Button("Retry").gameObject.activeSelf, Is.True);
        gateway.Fail = false; Click("Retry");
        Assert.That(action == "Cancel" ? gateway.CancelCalls : gateway.ConfirmCalls, Is.EqualTo(2));
        Assert.That(gateway.RequestCalls, Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator Revival_DeletionCloseDiscardsLateResponseAndReopensUnavailable()
    {
        var gateway = new Fake { PendingAuth = new TaskCompletionSource<DeletionAuthorization>() };
        Configure(gateway); Click("Request");
        Call(presenter, "OnDisable");
        gateway.PendingAuth.SetResult(new DeletionAuthorization("synthetic-account", "test-proof"));
        yield return null; yield return null;
        Call(presenter, "OnEnable"); Render();
        Assert.That(gateway.RequestCalls, Is.Zero);
        StringAssert.Contains("currently unavailable", Message);
    }

    [Test] public void Revival_DeletionMissingCompletionEvidenceDoesNotShowSuccess()
    {
        var gateway = new Fake(); Configure(gateway); Click("Request"); Click("Confirm");
        gateway.Next = DeletionState.Completed; gateway.Evidence = null; Click("Refresh");
        StringAssert.DoesNotContain("Test deletion completed", Message);
        Assert.That(Button("Retry").gameObject.activeSelf, Is.True);
    }

    [Test] public void Revival_DeletionSettingsEntryIsIdempotentAndPreservesExistingChildren()
    {
        var manager = root.AddComponent(Find("AD.PopupManager"));
        var type = Find("AD.DeletionSettingsEntry");
        var method = type.GetMethod("Ensure");
        int original = root.transform.childCount;
        method.Invoke(null, new object[] { root, manager });
        method.Invoke(null, new object[] { root, manager });
        Assert.That(root.transform.childCount, Is.EqualTo(original + 2));
        Assert.That(panel, Is.Not.Null);
        var entry = root.transform.Find("AccountPrivacyEntry");
        Assert.That(((RectTransform)entry).sizeDelta.y, Is.GreaterThan(0));
        Assert.That(entry.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(root.GetComponent<GraphicRaycaster>(), Is.Not.Null);
    }

    sealed class Fake : IDeletionGateway
    {
        public bool IsAvailable => true;
        public bool IsSynthetic => true;
        public bool Fail;
        public int AuthCalls, RequestCalls, ConfirmCalls, CancelCalls;
        public string Evidence = "synthetic-only-evidence";
        public DeletionState Next = DeletionState.Processing;
        public TaskCompletionSource<DeletionAuthorization> PendingAuth;
        Task<DeletionSnapshot> Result(DeletionState state) => Fail
            ? Task.FromException<DeletionSnapshot>(new Exception("synthetic failure"))
            : Task.FromResult(new DeletionSnapshot("test-request", "test-policy", "title", state, "test-challenge", Evidence));
        public Task<DeletionAuthorization> ReauthenticateAsync(DeletionSession s, CancellationToken t)
        { AuthCalls++; return PendingAuth != null ? PendingAuth.Task : Task.FromResult(new DeletionAuthorization(s.AccountId, "test-proof")); }
        public Task<DeletionSnapshot> RequestAsync(DeletionAuthorization a, string key, CancellationToken t) { RequestCalls++; return Result(DeletionState.AwaitingConfirmation); }
        public Task<DeletionSnapshot> ConfirmAsync(DeletionAuthorization a, DeletionSnapshot r, CancellationToken t) { ConfirmCalls++; return Result(DeletionState.Queued); }
        public Task<DeletionSnapshot> StatusAsync(DeletionAuthorization a, string id, CancellationToken t) => Result(Next);
        public Task<DeletionSnapshot> CancelAsync(DeletionAuthorization a, string id, CancellationToken t) { CancelCalls++; return Result(DeletionState.Cancelled); }
    }
}
