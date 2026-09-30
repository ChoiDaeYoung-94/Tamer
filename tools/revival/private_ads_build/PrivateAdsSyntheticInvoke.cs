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
        catch (TargetInvocationException error)
        {
            var allowed = new[] { "Synthetic diagnostic C01.", "Synthetic diagnostic C02.",
                "Synthetic diagnostic C03.", "Synthetic diagnostic C04.", "Synthetic diagnostic C05.",
                "Synthetic diagnostic S10.", "Synthetic diagnostic S20.", "Synthetic diagnostic S30.",
                "Synthetic diagnostic S40.", "Synthetic diagnostic S50.", "Synthetic diagnostic S90." };
            string message = error.InnerException == null ? null : error.InnerException.Message;
            throw new InvalidOperationException(allowed.Contains(message) ? message : "Synthetic diagnostic S00.");
        }
        catch { throw new InvalidOperationException("Synthetic diagnostic S00."); }
    }
}
