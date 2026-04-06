using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using TestsFramework.Assert;
using TestsFramework.Attributes;

namespace TestsFramework.Runner
{
    public class TestRunner
    {
        private readonly string _assemblyPath;
        private readonly int _maxParallelism;
        private readonly ConcurrentBag<TestResult> _results = [];
        private readonly Mutex _consoleMutex = new();
        private readonly Semaphore _testSemaphore;
        private int _completedTests = 0;

        public TestRunner(string assemblyPath, int maxDegreeOfParallelism)
        {
            _assemblyPath = assemblyPath;
            _maxParallelism = maxDegreeOfParallelism;
            _testSemaphore = new Semaphore(maxDegreeOfParallelism, maxDegreeOfParallelism);
        }

        public void RunAllTests()
        {
            PrintHeader($"ЗАГРУЗКА СБОРКИ: {_assemblyPath}", ConsoleColor.Cyan);

            var assembly = Assembly.LoadFrom(_assemblyPath);
            var testClasses = GetTestClasses(assembly);
            PrintInfo($"Найдено классов тестирования: {testClasses.Count}");

            var testMethods = GetAllTestMethods(testClasses);

            PrintHeader($"ЗАПУСК ТЕСТОВ", ConsoleColor.Green);
            PrintInfo($"Максимальная степень параллелизма: {_maxParallelism}");
            PrintSeparator();

            RunTestsInParallel(testMethods);
            PrintSummary();

            _testSemaphore.Dispose();
            _consoleMutex.Dispose();
        }

        private List<Type> GetTestClasses(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<TestClassAttribute>() != null)
                .ToList();
        }

        private List<(Type Class, MethodInfo Method)> GetAllTestMethods(List<Type> testClasses)
        {
            var methods = new List<(Type, MethodInfo)>();

            foreach (var testClass in testClasses)
            {
                var testMethods = testClass.GetMethods()
                    .Where(m => m.GetCustomAttribute<TestAttribute>() != null);

                methods.AddRange(testMethods.Select(m => (testClass, m)));
            }

            return methods;
        }

        private void RunTestsInParallel(List<(Type Class, MethodInfo Method)> testMethods)
        {
            var threads = new List<Thread>();
            var orderedTests = testMethods.OrderBy(x => GetPriority(x.Method));

            foreach (var (testClass, testMethod) in orderedTests)
            {
                _testSemaphore.WaitOne();

                var thread = new Thread(() => ExecuteTest(testClass, testMethod))
                {
                    IsBackground = true
                };
                thread.Start();
                threads.Add(thread);
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }
        }

        private void ExecuteTest(Type testClass, MethodInfo testMethod)
        {
            var testId = Interlocked.Increment(ref _completedTests);
            var startTime = DateTime.Now;
            var testName = $"{testClass.Name}.{testMethod.Name}";
            var maxTimeAttr = testMethod.GetCustomAttribute<MaxTimeAttribute>();

            try
            {
                PrintTestStart(testName, testId);

                if (TrySkipTest(testMethod, testName, testId))
                    return;

                var result = RunTestMethod(testClass, testMethod, maxTimeAttr, startTime);

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

                _results.Add(new TestResult(testName, result.Status, result.Message, startTime, result.Duration));
            }
            finally
            {
                CleanupTest(testClass);
                _testSemaphore.Release();
            }
        }

        private bool TrySkipTest(MethodInfo method, string testName, int testId)
        {
            var skipAttr = method.GetCustomAttribute<SkipAttribute>();
            if (skipAttr == null) return false;

            PrintTestSkipped(testName, skipAttr.Reason, testId);
            _results.Add(new TestResult(testName, TestRunStatus.Skipped, skipAttr.Reason, DateTime.Now));
            return true;
        }

        private TestRunResult RunTestMethod(Type testClass, MethodInfo method, MaxTimeAttribute? maxTimeAttr, DateTime startTime)
        {
            var setup = GetSetupMethod(testClass);
            var cleanup = GetCleanupMethod(testClass);
            Exception? caughtException = null;
            object? instance = null;

            var testThread = new Thread(() =>
            {
                try
                {
                    instance = Activator.CreateInstance(testClass);
                    setup?.Invoke(instance, null);
                    method.Invoke(instance, null);
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
                IsBackground = true
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

        private bool WaitForCompletion(Thread thread)
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

        private void CleanupTest(Type testClass)
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

        private MethodInfo? GetSetupMethod(Type testClass)
        {
            return testClass.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<SetupAttribute>() != null);
        }

        private MethodInfo? GetCleanupMethod(Type testClass)
        {
            return testClass.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<CleanupAttribute>() != null);
        }

        private int GetPriority(MethodInfo method)
        {
            return method.GetCustomAttribute<PriorityAttribute>()?.Level ?? int.MaxValue;
        }

        private void PrintSummary()
        {
            PrintSeparator('=');
            PrintHeader(" СВОДКА РЕЗУЛЬТАТОВ ТЕСТИРОВАНИЯ ", ConsoleColor.Magenta, true);
            PrintSeparator('-');

            var results = _results.ToList();
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
                var rateColor = successRate >= 80 ? ConsoleColor.Green : successRate >= 50 ? ConsoleColor.Yellow : ConsoleColor.Red;
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
                WriteTimestamp();
                WriteId(id);
                WriteLabel(" НАЧАТ:   ", ConsoleColor.Gray);
                WriteValue(name, ConsoleColor.DarkCyan);
                Console.WriteLine();
            });
        }

        private void PrintTestPassed(string name, int id, TimeSpan duration)
        {
            Locked(() =>
            {
                WriteTimestamp();
                WriteId(id);
                WriteLabel(" ПРОЙДЕН: ", ConsoleColor.Green);
                WriteValue($"{name} ", ConsoleColor.DarkGreen);
                WriteValue($"({duration.TotalMilliseconds:F0} мс)", ConsoleColor.DarkGray, true);
            });
        }

        private void PrintTestFailed(string name, string message, int id, TimeSpan duration)
        {
            Locked(() =>
            {
                WriteTimestamp();
                WriteId(id);
                WriteLabel(" ПРОВАЛ:  ", ConsoleColor.Red);
                WriteValue(name, ConsoleColor.DarkRed);
                WriteDetails(message, duration);
            });
        }

        private void PrintTestError(string name, string message, int id, TimeSpan duration)
        {
            Locked(() =>
            {
                WriteTimestamp();
                WriteId(id);
                WriteLabel(" ОШИБКА:  ", ConsoleColor.DarkRed);
                WriteValue(name, ConsoleColor.Red);
                WriteDetails(message, duration);
            });
        }

        private void PrintTestTimeout(string name, int timeoutMs, int id, TimeSpan duration)
        {
            Locked(() =>
            {
                WriteTimestamp();
                WriteId(id);
                WriteLabel(" ТАЙМАУТ: ", ConsoleColor.DarkYellow);
                WriteValue(name, ConsoleColor.Yellow);
                WriteDetails($"Превышен лимит: {timeoutMs} мс", duration);
            });
        }

        private void PrintTestSkipped(string name, string reason, int id)
        {
            Locked(() =>
            {
                WriteTimestamp();
                WriteId(id);
                WriteLabel(" ПРОПУЩЕН:", ConsoleColor.Yellow);
                WriteValue(name, ConsoleColor.DarkYellow);
                WriteLine($"      Причина: {reason}", ConsoleColor.DarkGray);
            });
        }

        private void WriteDetails(string message, TimeSpan duration)
        {
            WriteLine($"      Причина: {message}", ConsoleColor.DarkGray);
            WriteLine($"      Время: {duration.TotalMilliseconds:F0} мс", ConsoleColor.DarkGray);
        }

        private void WriteTimestamp()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
        }

        private void WriteId(int id)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"[{id:D3}] ");
        }

        private void WriteLabel(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
        }

        private void WriteValue(string text, ConsoleColor color, bool addNewLine = false)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            if (addNewLine) Console.WriteLine();
            Console.ResetColor();
        }

        private void WriteLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
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