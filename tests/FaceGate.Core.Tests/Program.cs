using System.Reflection;
using FaceGate.Core.Tests;

var failed = 0; var passed = 0;
foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Name.EndsWith("Tests")))
{
    var instance = Activator.CreateInstance(type)!;
    foreach (var m in type.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() is not null))
    {
        try { m.Invoke(instance, null); passed++; Console.WriteLine($"  PASS {type.Name}.{m.Name}"); }
        catch (TargetInvocationException ex)
        {
            failed++;
            Console.WriteLine($"  FAIL {type.Name}.{m.Name}: {ex.InnerException?.Message}");
        }
    }
}
Console.WriteLine($"\n{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

[AttributeUsage(AttributeTargets.Method)]
sealed class TestAttribute : Attribute { }
