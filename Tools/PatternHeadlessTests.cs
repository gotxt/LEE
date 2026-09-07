using System;
using System.Reflection;

// Runs only the scene-independent test fixture against real Unity managed assemblies.
// This does not replace Unity's asset import, serialization or PlayMode tests.
public static class PatternHeadlessTests
{
    public static int Main()
    {
        var assembly = Assembly.LoadFrom("Assembly-CSharp-Editor.dll");
        var type = assembly.GetType("NHN.TraceStrike.Tests.PatternRunnerTests", true);
        int passed = 0, failed = 0;
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.GetParameters().Length != 0) continue;
            try { method.Invoke(Activator.CreateInstance(type), null); Console.WriteLine("PASS " + method.Name); passed++; }
            catch (Exception e) { Console.WriteLine("FAIL " + method.Name + ": " + (e.InnerException ?? e)); failed++; }
        }
        Console.WriteLine("Passed=" + passed + " Failed=" + failed);
        return failed == 0 ? 0 : 1;
    }
}
