using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// Minimal runner for the engine-free tests: every [Test] and [TestCase] in this assembly.
public static class Program
{
    public static int Main()
    {
        int passed = 0, failed = 0;
        foreach (var type in typeof(Program).Assembly.GetTypes())
        {
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var cases = m.GetCustomAttributes<TestCaseAttribute>().ToList();
                bool plain = m.GetCustomAttribute<TestAttribute>() != null;
                if (!plain && cases.Count == 0) continue;

                var argSets = cases.Count > 0
                    ? cases.Select(c => c.Arguments.Select((a, i) =>
                        Convert.ChangeType(a, m.GetParameters()[i].ParameterType)).ToArray()).ToList()
                    : new System.Collections.Generic.List<object[]> { new object[0] };

                foreach (var args in argSets)
                {
                    try
                    {
                        m.Invoke(Activator.CreateInstance(type), args);
                        passed++;
                    }
                    catch (TargetInvocationException e)
                    {
                        failed++;
                        string msg = e.InnerException.Message.Replace("\r", " ").Replace("\n", " ");
                        Console.WriteLine($"FAIL {m.Name}({string.Join(",", args)}): {msg}");
                    }
                }
            }
        }
        Console.WriteLine($"total={passed + failed} passed={passed} failed={failed}");
        return failed == 0 ? 0 : 1;
    }
}
