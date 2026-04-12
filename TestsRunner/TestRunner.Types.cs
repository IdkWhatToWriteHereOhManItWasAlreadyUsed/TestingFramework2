using System;
using System.Linq;
using System.Reflection;

namespace TestsRunner
{
    public partial class TestRunner
    {
        private class TestMethodInstance(Type testClass, MethodInfo method, object[] arguments)
        {
            public Type TestClass { get; } = testClass;
            public MethodInfo Method { get; } = method;
            public object? OwnerInstance { get; set; }
            public object[] Arguments { get; } = arguments;

            public string GetDisplayName()
            {
                if (Arguments.Length == 0)
                    return $"{TestClass.Name}.{Method.Name}";

                var argsStr = string.Join(", ", Arguments.Select(a => a?.ToString() ?? "null"));
                return $"{TestClass.Name}.{Method.Name}({argsStr})";
            }
        }

        private enum TestRunStatus
        {
            Passed,
            Failed,
            Error,
            Skipped,
            Timeout
        }

        private class TestResult(string name, TestRunStatus status, string? message, DateTime startTime, TimeSpan? duration = null)
        {
            public string Name { get; } = name;
            public TestRunStatus Status { get; } = status;
            public string? Message { get; } = message;
            public DateTime StartTime { get; } = startTime;
            public TimeSpan? Duration { get; } = duration ?? DateTime.Now - startTime;
        }

        private class TestRunResult(TestRunStatus status, string? message, TimeSpan duration)
        {
            public TestRunStatus Status { get; } = status;
            public string? Message { get; } = message;
            public TimeSpan Duration { get; } = duration;
        }
    }
}