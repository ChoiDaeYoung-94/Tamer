// External run_script entry. Avoids defining another callback type in an ephemeral assembly.
using System;
using System.Linq;
using System.Reflection;

public static class PrivateAdsSyntheticInvoke
{
    public static object Run()
    {
        var types = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
            .Select(assembly => assembly.GetType("PrivateAdsSyntheticBuild", false)).Where(type => type != null).ToArray();
        if (types.Length != 1) throw new InvalidOperationException("Synthetic runner registration rejected.");
        try { return types[0].GetMethod("Run", BindingFlags.Static | BindingFlags.Public).Invoke(null, null); }
        catch { throw new InvalidOperationException("Synthetic invocation rejected; inspect private recovery evidence."); }
    }
}
