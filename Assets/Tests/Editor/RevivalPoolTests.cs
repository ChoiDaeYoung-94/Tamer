using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class RevivalPoolTests
{
    private object _manager;
    private GameObject _root;
    private GameObject _prefab;
    private Transform _borrowedRoot;

    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private object Call(string method, params object[] args)
    {
        try { return _manager.GetType().GetMethod(method).Invoke(_manager, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
    }

    private IDictionary Pools => (IDictionary)_manager.GetType().GetField("PoolDictionary").GetValue(_manager);

    private GameObject Pop() => (GameObject)Call("PopFromPool", _prefab.name, _borrowedRoot);

    [SetUp]
    public void SetUp()
    {
        // Construct only the plain pool manager; never initialize live services/managers.
        _manager = Activator.CreateInstance(RuntimeType("AD.PoolManager"));
        _root = new GameObject("Revival isolated pool roots");
        _prefab = new GameObject("Revival pool prefab " + Guid.NewGuid().ToString("N"));
        _prefab.SetActive(false);
        _borrowedRoot = new GameObject("Revival borrowed objects").transform;
        _borrowedRoot.SetParent(_root.transform);
        var poolRoot = new GameObject("Revival stored objects").transform;
        poolRoot.SetParent(_root.transform);
        _manager.GetType().GetField("RootGameObjects").SetValue(_manager, poolRoot);
        _manager.GetType().GetField("RootUI").SetValue(_manager, poolRoot);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var callback in _root.GetComponentsInChildren<RevivalPoolReturnOnDisable>(true))
            callback.ReturnToPool = null;
        UnityEngine.Object.DestroyImmediate(_root);
        UnityEngine.Object.DestroyImmediate(_prefab);
    }

    [Test]
    public void Revival_DuplicateReturnDoesNotLendTheSameObjectTwice()
    {
        Call("CreatePool", _prefab, true, 1);
        GameObject original = Pop();
        Call("PushToPool", original);
        Call("PushToPool", original);
        GameObject first = Pop();
        GameObject second = Pop();
        Assert.That(first, Is.SameAs(original));
        Assert.That(second, Is.Not.SameAs(first));
        Assert.That(first.activeSelf && second.activeSelf, Is.True);

        // Removing the stored marker on Pop must permit a later valid return.
        Call("PushToPool", first);
        Assert.That(second.activeSelf, Is.True);
        Assert.That(Pop(), Is.SameAs(first));
        Call("PushToPool", second);
        Assert.That(first.activeSelf, Is.True);
        Assert.That(Pop(), Is.SameAs(second));
    }

    [Test]
    public void Revival_OnDisableReturnCannotStoreTheObjectTwice()
    {
        Call("CreatePool", _prefab, true, 1);
        GameObject original = Pop();
        var callback = original.AddComponent<RevivalPoolReturnOnDisable>();
        int reentries = 0;
        callback.ReturnToPool = () =>
        {
            reentries++;
            Call("PushToPool", original);
        };
        Call("PushToPool", original);
        Assert.That(reentries, Is.EqualTo(1));
        Assert.That(Pop(), Is.SameAs(original));
        Assert.That(Pop(), Is.Not.SameAs(original));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Revival_DuplicatePoolCreationDoesNotAllocateRootsOrPrewarmObjects(bool gameObjectPool)
    {
        Call("CreatePool", _prefab, gameObjectPool, 2);
        object originalPool = Pools[_prefab.name];
        int objectsBefore = _root.GetComponentsInChildren<Transform>(true).Length;
        ExpectPoolError("Pool for " + _prefab.name + " already exists.");
        Call("CreatePool", _prefab, gameObjectPool, 3);
        Assert.That(Pools.Count, Is.EqualTo(1));
        Assert.That(Pools[_prefab.name], Is.SameAs(originalPool));
        Assert.That(_root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectsBefore));
        Assert.That(Pop(), Is.Not.SameAs(Pop()));
    }

    [Test]
    public void Revival_NullPrefabDoesNotCreatePoolObjects()
    {
        int objectsBefore = _root.GetComponentsInChildren<Transform>(true).Length;
        ExpectPoolError("Prefab is null when creating pool.");
        Call("CreatePool", null, true, 2);
        Assert.That(Pools.Count, Is.Zero);
        Assert.That(_root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectsBefore));
    }

    [Test]
    public void Revival_DisposeRemovesOwnedLoansAndRootsButPreservesExternalParent()
    {
        Call("CreatePool", _prefab, false, 2);
        GameObject borrowed = Pop();
        var external = new GameObject("Revival unrelated child");
        external.transform.SetParent(_borrowedRoot);
        object retainedPool = Pools[_prefab.name];
        Call("Dispose");
        Call("Dispose");
        Assert.That(borrowed == null, Is.True);
        Assert.That(external != null && _borrowedRoot != null, Is.True);
        Assert.That(Pools.Count, Is.Zero);
        Assert.That(retainedPool.GetType().GetMethod("PopFromPool").Invoke(retainedPool,
            new object[] { _borrowedRoot }), Is.Null);
        Assert.That(Pop(), Is.Null);
    }

    [Test]
    public void Revival_DestroyedStoredObjectIsSkippedOnNextLoan()
    {
        Call("CreatePool", _prefab, true, 1);
        GameObject first = Pop();
        Call("PushToPool", first);
        UnityEngine.Object.DestroyImmediate(first);
        GameObject replacement = Pop();
        Assert.That(replacement != null, Is.True);
        Assert.That(replacement.activeSelf, Is.True);
    }

    private static void ExpectPoolError(string message)
    {
        // Match DebugLogger's case-sensitive Conditional("Debug") symbol.
#if Debug
        LogAssert.Expect(LogType.Error,
            "<color=red>LogError</color> - PoolManager\n<color=cyan>" + message + "</color>");
#endif
    }
}

[ExecuteAlways]
public class RevivalPoolReturnOnDisable : MonoBehaviour
{
    public Action ReturnToPool;

    private void OnDisable() => ReturnToPool?.Invoke();
}
