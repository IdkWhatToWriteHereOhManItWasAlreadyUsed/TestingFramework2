using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using TestsFramework.Assert;
using TestsFramework.Attributes;
using MyThreading;

namespace TestsRunner
{
    public class TestRunner(string assemblyPath)
    {
        private readonly string _assemblyPath = assemblyPath;
        private readonly List<TestResult> _results = [];
        private readonly Mutex _consoleMutex = new();
        private int _completedTests = 0;
        private int _totalTests = 0;
        private MyThreadPool _threadPool;

        public void RunAllTests()
        {
            PrintHeader($"ЗАГРУЗКА СБОРКИ: {_assemblyPath}", ConsoleColor.Cyan);

            var assembly = Assembly.LoadFrom(_assemblyPath);
            var testClasses = GetTestClasses(assembly);
            PrintInfo($"Найдено классов тестирования: {testClasses.Count}");

            var testInstances = GetAllTestInstances(testClasses);
            _totalTests = testInstances.Count;
            PrintInfo($"Всего тестов: {_totalTests}");

            if (_totalTests == 0)
            {
                PrintSummary();
                return;
            }

            PrintHeader($"ЗАПУСК ТЕСТОВ (ПАРАЛЛЕЛЬНО)", ConsoleColor.Green);
            PrintSeparator();

            _threadPool = new MyThreadPool (
                minThreads: 2,
                maxThreads: Environment.ProcessorCount,
                idleTimeout: TimeSpan.FromSeconds(2),
                queueScaleThreshold: 5
            )
            {
                Log = msg =>
                {
                    Locked(() =>
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine(msg);
                        Console.ResetColor();
                    });
                }
            };

            _threadPool.Start();

            var sortedInstances = testInstances.OrderBy(x => GetPriority(x.Method)).ToList();

            foreach (var instance in sortedInstances)
            {
                _threadPool.Enqueue(() => ExecuteTest(instance));
            }

            while (Interlocked.CompareExchange(ref _completedTests, 0, 0) < _totalTests)
            {
                Thread.Sleep(100);
            }

            _threadPool.Stop();
            PrintSummary();
        }

        private void ExecuteTest(TestMethodInstance instance)
        {
            var testId = Interlocked.Increment(ref _completedTests);
            var startTime = DateTime.Now;
            var testName = instance.GetDisplayName();
            var maxTimeAttr = instance.Method.GetCustomAttribute<MaxTimeAttribute>();

            try
            {
                PrintTestStart(testName, testId);

                if (TrySkipTest(instance.Method, testName, testId))
                    return;

                var result = RunTestMethod(instance, maxTimeAttr, startTime);

                switch (result.Status)
                {
                    case TestRunStatus.Passed:
                        PrintTestPassed(testName, testId, result.Duration);
                        break;
                    case TestRunStatus.Failed:
                        PrintTestFailed(testName, result.Message, testId, result.Duration);
                        break;
                    case TestRunStatus.Error:
                        PrintTestError(testName, result.Message, testId, result.Duration);
                        break;
                    case TestRunStatus.Timeout:
                        PrintTestTimeout(testName, maxTimeAttr?.Milliseconds ?? 0, testId, result.Duration);
                        break;
                }

                lock (_results)
                {
                    _results.Add(new TestResult(testName, result.Status, result.Message, startTime, result.Duration));
                }
            }
            catch (Exception ex)
            {
                PrintTestError(testName, ex.Message, testId, DateTime.Now - startTime);
                lock (_results)
                {
                    _results.Add(new TestResult(testName, TestRunStatus.Error, ex.Message, startTime));
                }
            }
            finally
            {
                if (instance.OwnerInstance == null)
                {
                    CleanupTest(instance.TestClass);
                }
            }
        }

        private bool TrySkipTest(MethodInfo method, string testName, int testId)
        {
            var skipAttr = method.GetCustomAttribute<SkipAttribute>();
            if (skipAttr == null) return false;

            PrintTestSkipped(testName, skipAttr.Reason, testId);
            lock (_results)
            {
                _results.Add(new TestResult(testName, TestRunStatus.Skipped, skipAttr.Reason, DateTime.Now));
            }
            return true;
        }

        private TestRunResult RunTestMethod(TestMethodInstance instance, MaxTimeAttribute? maxTimeAttr, DateTime startTime)
        {
            var setup = GetSetupMethod(instance.TestClass);
            Exception? caughtException = null;

            var testThread = new Thread(() =>
            {
                try
                {
                    if (instance.OwnerInstance == null)
                    {
                        instance.OwnerInstance = Activator.CreateInstance(instance.TestClass);
                        setup?.Invoke(instance.OwnerInstance, null);
                    }

                    instance.Method.Invoke(instance.OwnerInstance, instance.Arguments);
                }
                catch (TargetInvocationException ex)
                {
                    caughtException = ex.InnerException;
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
            })
            {
                IsBackground = true,
                Name = $"TestThread-{instance.Method.Name}"
            };

            testThread.Start();

            bool completed = maxTimeAttr == null ? WaitForCompletion(testThread) : WaitWithTimeout(testThread, maxTimeAttr.Milliseconds);
            var duration = DateTime.Now - startTime;

            if (!completed)
            {
                ForceStopThread(testThread);
                return new TestRunResult(TestRunStatus.Timeout, $"Превышено время выполнения ({maxTimeAttr?.Milliseconds} мс)", duration);
            }

            if (caughtException is AssertFailedException assertEx)
                return new TestRunResult(TestRunStatus.Failed, assertEx.Message, duration);

            if (caughtException != null)
                return new TestRunResult(TestRunStatus.Error, caughtException.Message, duration);

            return new TestRunResult(TestRunStatus.Passed, null, duration);
        }

        private static bool WaitForCompletion(Thread thread)
        {
            thread.Join();
            return true;
        }

        private bool WaitWithTimeout(Thread thread, int milliseconds)
        {
            return thread.Join(milliseconds);
        }

        private void ForceStopThread(Thread thread)
        {
            thread.Interrupt();

            for (int i = 0; i < 10; i++)
            {
                if (thread.Join(100)) return;
                thread.Interrupt();
            }
        }

        private static void CleanupTest(Type testClass)
        {
            try
            {
                var cleanup = GetCleanupMethod(testClass);
                var instance = Activator.CreateInstance(testClass);
                cleanup?.Invoke(instance, null);

                if (instance is IDisposable disposable)
                    disposable.Dispose();
            }
            catch { }
        }

        private static List<Type> GetTestClasses(Assembly assembly)
        {
            return [.. assembly.GetTypes().Where(t => t.GetCustomAttribute<TestClassAttribute>() != null)];
        }

        private static List<TestMethodInstance> GetAllTestInstances(List<Type> testClasses)
        {
            var instances = new List<TestMethodInstance>();

            foreach (var testClass in testClasses)
            {
                var testMethods = testClass.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null);

                foreach (var method in testMethods)
                {
                    var argumentsAttrs = method.GetCustomAttributes<ArgumentsAttribute>().ToList();

                    if (argumentsAttrs.Count != 0)
                    {
                        foreach (var attr in argumentsAttrs)
                        {
                            instances.Add(new TestMethodInstance(testClass, method, attr.Values));
                        }
                    }
                    else
                    {
                        instances.Add(new TestMethodInstance(testClass, method, Array.Empty<object>()));
                    }
                }
            }

            return instances;
        }

        private static MethodInfo? GetSetupMethod(Type testClass)
        {
            return testClass.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<SetupAttribute>() != null);
        }

        private static MethodInfo? GetCleanupMethod(Type testClass)
        {
            return testClass.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<CleanupAttribute>() != null);
        }

        private static int GetPriority(MethodInfo method)
        {
            return method.GetCustomAttribute<PriorityAttribute>()?.Level ?? int.MaxValue;
        }

        private void PrintSummary()
        {
            PrintSeparator('=');
            PrintHeader(" СВОДКА РЕЗУЛЬТАТОВ ТЕСТИРОВАНИЯ ", ConsoleColor.Magenta, true);
            PrintSeparator('-');

            List<TestResult> results;
            lock (_results)
            {
                results = _results.ToList();
            }

            var passed = results.Count(r => r.Status == TestRunStatus.Passed);
            var failed = results.Count(r => r.Status == TestRunStatus.Failed);
            var errors = results.Count(r => r.Status == TestRunStatus.Error);
            var skipped = results.Count(r => r.Status == TestRunStatus.Skipped);
            var timeouts = results.Count(r => r.Status == TestRunStatus.Timeout);
            var total = results.Count;

            PrintLine();
            PrintStat("Всего тестов:", total, ConsoleColor.White);
            PrintStat("Пройдено:", passed, ConsoleColor.Green);
            PrintStat("Провалено:", failed, ConsoleColor.Red);
            PrintStat("Ошибок:", errors, ConsoleColor.DarkRed);
            PrintStat("Таймаут:", timeouts, ConsoleColor.DarkYellow);
            PrintStat("Пропущено:", skipped, ConsoleColor.Yellow);

            if (total > 0)
            {
                var successRate = (double)passed / total * 100;
                var rateColor = successRate >= 80 ? ConsoleColor.Green :
                               successRate >= 50 ? ConsoleColor.Yellow :
                               ConsoleColor.Red;
                PrintStat("Успешность:", $"{successRate:F1}%", rateColor);
            }

            PrintProblemDetails(results.Where(r => r.Status is TestRunStatus.Failed or TestRunStatus.Error or TestRunStatus.Timeout));
            PrintSeparator('=');
        }

        private void PrintProblemDetails(IEnumerable<TestResult> problemTests)
        {
            if (!problemTests.Any()) return;

            PrintLine();
            PrintHeader("Детали проблемных тестов:", ConsoleColor.Red);
            PrintSeparator('-');

            foreach (var result in problemTests)
            {
                var statusText = result.Status switch
                {
                    TestRunStatus.Failed => "ПРОВАЛ",
                    TestRunStatus.Error => "ОШИБКА",
                    TestRunStatus.Timeout => "ТАЙМАУТ",
                    _ => ""
                };

                var statusColor = result.Status == TestRunStatus.Failed ? ConsoleColor.Red :
                                 result.Status == TestRunStatus.Error ? ConsoleColor.DarkRed :
                                 ConsoleColor.DarkYellow;

                PrintLine($"  {result.Name,-60} [{statusText}]", statusColor);
                PrintLine($"      {result.Message}", ConsoleColor.DarkGray);

                if (result.Duration.HasValue)
                    PrintLine($"      Время: {result.Duration.Value.TotalMilliseconds:F0} мс", ConsoleColor.DarkGray);
            }
        }

        private void PrintTestStart(string name, int id)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}/{_totalTests}] ");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($" НАЧАТ:   ");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"{name}");
                Console.ResetColor();
            });
        }

        private void PrintTestPassed(string name, int id, TimeSpan duration)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}/{_totalTests}] ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($" ПРОЙДЕН: ");
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write($"{name} ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"({duration.TotalMilliseconds:F0} мс)");
                Console.ResetColor();
            });
        }

        private void PrintTestFailed(string name, string message, int id, TimeSpan duration)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}/{_totalTests}] ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($" ПРОВАЛ:  ");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"{name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      Причина: {message}");
                Console.WriteLine($"      Время: {duration.TotalMilliseconds:F0} мс");
                Console.ResetColor();
            });
        }

        private void PrintTestError(string name, string message, int id, TimeSpan duration)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}/{_totalTests}] ");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write($" ОШИБКА:  ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      Ошибка: {message}");
                Console.WriteLine($"      Время: {duration.TotalMilliseconds:F0} мс");
                Console.ResetColor();
            });
        }

        private void PrintTestTimeout(string name, int timeoutMs, int id, TimeSpan duration)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}/{_totalTests}] ");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write($" ТАЙМАУТ: ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      Превышен лимит: {timeoutMs} мс");
                Console.WriteLine($"      Время: {duration.TotalMilliseconds:F0} мс");
                Console.ResetColor();
            });
        }

        private void PrintTestSkipped(string name, string reason, int id)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}/{_totalTests}] ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($" ПРОПУЩЕН:");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"{name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      Причина: {reason}");
                Console.ResetColor();
            });
        }

        private void PrintHeader(string text, ConsoleColor color, bool centered = false)
        {
            Locked(() =>
            {
                Console.ForegroundColor = color;
                if (centered)
                {
                    var width = Console.WindowWidth > 0 ? Console.WindowWidth : 80;
                    var padding = Math.Max(0, (width - text.Length) / 2);
                    text = new string(' ', padding) + text + new string(' ', padding);
                }
                Console.WriteLine(text);
                Console.ResetColor();
            });
        }

        private void PrintInfo(string text)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($" {text}");
                Console.ResetColor();
            });
        }

        private void PrintLine(string text = "", ConsoleColor color = ConsoleColor.Gray)
        {
            Locked(() =>
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ResetColor();
            });
        }

        private void PrintStat(string label, object value, ConsoleColor color)
        {
            Locked(() =>
            {
                Console.Write(label.PadRight(20));
                Console.ForegroundColor = color;
                Console.WriteLine(value);
                Console.ResetColor();
            });
        }

        private void PrintSeparator(char symbol = '-')
        {
            Locked(() =>
            {
                var width = Console.WindowWidth > 0 ? Console.WindowWidth : 80;
                Console.WriteLine(new string(symbol, width));
            });
        }

        private void Locked(Action action)
        {
            _consoleMutex.WaitOne();
            try { action(); }
            finally { _consoleMutex.ReleaseMutex(); }
        }

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