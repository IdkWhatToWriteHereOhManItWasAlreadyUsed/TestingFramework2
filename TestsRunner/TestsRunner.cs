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
        private readonly ConcurrentBag<TestResult> _results = new();
        private readonly Mutex _consoleMutex = new();
        private readonly Semaphore _testSemaphore;
        private readonly int _maxDegreeOfParallelism;
        private int _completedTests = 0;

        public TestRunner(string assemblyPath, int maxDegreeOfParallelism)
        {
            _assemblyPath = assemblyPath;
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
            _testSemaphore = new Semaphore(maxDegreeOfParallelism, maxDegreeOfParallelism);
        }

        public void RunAllTests()
        {
            PrintHeader($"ЗАГРУЗКА СБОРКИ: {_assemblyPath}", ConsoleColor.Cyan);

            var assembly = Assembly.LoadFrom(_assemblyPath);
            var testClasses = GetTestClasses(assembly);
            PrintInfo($"Найдено классов тестирования: {testClasses.Count}");

            var allTestMethods = new List<(Type TestClass, MethodInfo Method)>();
            foreach (var testClass in testClasses)
            {
                var methods = GetTestMethods(testClass);
                allTestMethods.AddRange(methods.Select(m => (testClass, m)));
            }

            PrintHeader($"ЗАПУСК ТЕСТОВ", ConsoleColor.Green);
            PrintLine($"Максимальная степень параллелизма: {_maxDegreeOfParallelism}");
            PrintSeparator();

            var threads = new List<Thread>();
            foreach (var (testClass, testMethod) in allTestMethods.OrderBy(x => GetPriority(x.Method)))
            {
                _testSemaphore.WaitOne();
                var thread = new Thread(() => ExecuteTest(testClass, testMethod));
                thread.IsBackground = true;
                thread.Start();
                threads.Add(thread);
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            PrintSummary();

            _testSemaphore.Dispose();
            _consoleMutex.Dispose();
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

                var skipAttr = testMethod.GetCustomAttribute<SkipAttribute>();
                if (skipAttr != null)
                {
                    PrintTestSkipped(testName, skipAttr.Reason, testId);
                    _results.Add(new TestResult(testName, TestStatus.Skipped, skipAttr.Reason, startTime));
                    return;
                }

                var setupMethod = GetSetupMethod(testClass);
                var cleanupMethod = GetCleanupMethod(testClass);

                Exception? testException = null;
                object? testInstance = null;

                var testThread = new Thread(() =>
                {
                    try
                    {
                        testInstance = Activator.CreateInstance(testClass);
                        setupMethod?.Invoke(testInstance, null);
                        testMethod.Invoke(testInstance, null);
                    }
                    catch (ThreadInterruptedException)
                    {
                        return;
                    }
                    catch (TargetInvocationException ex)
                    {
                        testException = ex.InnerException;
                    }
                    catch (Exception ex)
                    {
                        testException = ex;
                    }
                });

                testThread.IsBackground = true;
                testThread.Start();

                bool completed = true;
                if (maxTimeAttr != null)
                {
                    completed = testThread.Join(maxTimeAttr.Milliseconds);
                }
                else
                {
                    testThread.Join();
                }

                var duration = DateTime.Now - startTime;

                if (!completed)
                {
                    testThread.Interrupt();

                    var gaveUp = false;
                    for (int i = 0; i < 10; i++)
                    {
                        if (testThread.Join(100))
                        {
                            gaveUp = true;
                            break;
                        }
                        testThread.Interrupt();
                    }

                    if (!gaveUp)
                    {
                        testThread.Interrupt();
                    }

                    PrintTestTimeout(testName, maxTimeAttr?.Milliseconds ?? 0, testId, duration);
                    _results.Add(new TestResult(testName, TestStatus.Timeout,
                        $"Превышено время выполнения ({maxTimeAttr?.Milliseconds} мс)", startTime, duration));
                    return;
                }

                if (testException != null)
                {
                    if (testException is AssertFailedException assertEx)
                    {
                        PrintTestFailed(testName, assertEx.Message, testId, duration);
                        _results.Add(new TestResult(testName, TestStatus.Failed, assertEx.Message, startTime, duration));
                    }
                    else
                    {
                        PrintTestError(testName, testException.Message, testId, duration);
                        _results.Add(new TestResult(testName, TestStatus.Error, testException.Message, startTime, duration));
                    }
                    return;
                }

                PrintTestPassed(testName, testId, duration);
                _results.Add(new TestResult(testName, TestStatus.Passed, null, startTime, duration));
            }
            finally
            {
                try
                {
                    var cleanupMethod = GetCleanupMethod(testClass);
                    var instance = Activator.CreateInstance(testClass);
                    cleanupMethod?.Invoke(instance, null);
                    if (instance is IDisposable disp)
                        disp.Dispose();
                }
                catch { }

                _testSemaphore.Release();
            }
        }

        private List<Type> GetTestClasses(Assembly assembly) =>
            assembly.GetTypes().Where(t => t.GetCustomAttribute<TestClassAttribute>() != null).ToList();

        private List<MethodInfo> GetTestMethods(Type testClass) =>
            testClass.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null).ToList();

        private MethodInfo? GetSetupMethod(Type testClass) =>
            testClass.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<SetupAttribute>() != null);

        private MethodInfo? GetCleanupMethod(Type testClass) =>
            testClass.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<CleanupAttribute>() != null);

        private int GetPriority(MethodInfo method) =>
            method.GetCustomAttribute<PriorityAttribute>()?.Level ?? int.MaxValue;

        private void PrintSummary()
        {
            PrintSeparator('=');
            PrintHeader(" СВОДКА РЕЗУЛЬТАТОВ ТЕСТИРОВАНИЯ ", ConsoleColor.Magenta, true);
            PrintSeparator('-');

            var results = _results.ToList();
            var passed = results.Count(r => r.Status == TestStatus.Passed);
            var failed = results.Count(r => r.Status == TestStatus.Failed);
            var error = results.Count(r => r.Status == TestStatus.Error);
            var skipped = results.Count(r => r.Status == TestStatus.Skipped);
            var timeout = results.Count(r => r.Status == TestStatus.Timeout);
            var total = results.Count;

            PrintLine();
            PrintStat("Всего тестов:", total, ConsoleColor.White);
            PrintStat("Пройдено:", passed, ConsoleColor.Green);
            PrintStat("Провалено:", failed, ConsoleColor.Red);
            PrintStat("Ошибок:", error, ConsoleColor.DarkRed);
            PrintStat("Таймаут:", timeout, ConsoleColor.DarkYellow);
            PrintStat("Пропущено:", skipped, ConsoleColor.Yellow);

            if (total > 0)
            {
                double rate = (double)passed / total * 100;
                PrintStat("Успешность:", $"{rate:F1}%",
                    rate >= 80 ? ConsoleColor.Green : rate >= 50 ? ConsoleColor.Yellow : ConsoleColor.Red);
            }

            if (failed + error + timeout > 0)
            {
                PrintLine();
                PrintHeader("Детали проблемных тестов:", ConsoleColor.Red);
                PrintSeparator('-');

                foreach (var r in results.Where(r => r.Status is TestStatus.Failed or TestStatus.Error or TestStatus.Timeout))
                {
                    var status = r.Status switch
                    {
                        TestStatus.Failed => "ПРОВАЛ",
                        TestStatus.Error => "ОШИБКА",
                        TestStatus.Timeout => "ТАЙМАУТ",
                        _ => ""
                    };
                    var color = r.Status == TestStatus.Failed ? ConsoleColor.Red :
                               r.Status == TestStatus.Error ? ConsoleColor.DarkRed : ConsoleColor.DarkYellow;

                    PrintLine($"  {r.Name,-60} [{status}]");
                    PrintLine($"      {r.Message}", ConsoleColor.DarkGray);
                    if (r.Duration.HasValue)
                        PrintLine($"      Время: {r.Duration.Value.TotalMilliseconds:F0} мс", ConsoleColor.DarkGray);
                }
            }
            PrintSeparator('=');
        }

        private void PrintTestStart(string name, int id) =>
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}] ");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($" НАЧАТ:   ");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"{name}");
                Console.ResetColor();
            });

        private void PrintTestPassed(string name, int id, TimeSpan dur) =>
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}] ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($" ПРОЙДЕН: ");
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write($"{name} ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"({dur.TotalMilliseconds:F0} мс)");
                Console.ResetColor();
            });

        private void PrintTestFailed(string name, string msg, int id, TimeSpan dur) =>
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}] ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($" ПРОВАЛ:  ");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"{name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      Причина: {msg}");
                Console.WriteLine($"      Время: {dur.TotalMilliseconds:F0} мс");
                Console.ResetColor();
            });

        private void PrintTestError(string name, string msg, int id, TimeSpan dur) =>
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}] ");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write($" ОШИБКА:  ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      Ошибка: {msg}");
                Console.WriteLine($"      Время: {dur.TotalMilliseconds:F0} мс");
                Console.ResetColor();
            });

        private void PrintTestTimeout(string name, int timeout, int id, TimeSpan dur) =>
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}] ");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write($" ТАЙМАУТ: ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      Превышен лимит: {timeout} мс");
                Console.ResetColor();
            });

        private void PrintTestSkipped(string name, string reason, int id) =>
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}] ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($" ПРОПУЩЕН:");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"{name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      Причина: {reason}");
                Console.ResetColor();
            });

        private void PrintHeader(string text, ConsoleColor color, bool centered = false) =>
            Locked(() =>
            {
                Console.ForegroundColor = color;
                if (centered)
                {
                    int width = Console.WindowWidth > 0 ? Console.WindowWidth : 80;
                    int pad = Math.Max(0, (width - text.Length) / 2);
                    text = new string(' ', pad) + text + new string(' ', pad);
                }
                Console.WriteLine(text);
                Console.ResetColor();
            });

        private void PrintInfo(string text) =>
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($" {text}");
                Console.ResetColor();
            });

        private void PrintLine(string text = "", ConsoleColor color = ConsoleColor.Gray) =>
            Locked(() =>
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ResetColor();
            });

        private void PrintStat(string label, object value, ConsoleColor color) =>
            Locked(() =>
            {
                Console.Write(label.PadRight(20));
                Console.ForegroundColor = color;
                Console.WriteLine(value);
                Console.ResetColor();
            });

        private void PrintSeparator(char symbol = '-') =>
            Locked(() =>
            {
                int width = Console.WindowWidth > 0 ? Console.WindowWidth : 80;
                Console.WriteLine(new string(symbol, width));
            });

        private void Locked(Action action)
        {
            _consoleMutex.WaitOne();
            try { action(); }
            finally { _consoleMutex.ReleaseMutex(); }
        }

        private enum TestStatus { Passed, Failed, Error, Skipped, Timeout }

        private class TestResult
        {
            public string Name { get; }
            public TestStatus Status { get; }
            public string? Message { get; }
            public DateTime StartTime { get; }
            public TimeSpan? Duration { get; }
            public TestResult(string name, TestStatus status, string? msg, DateTime start, TimeSpan? dur = null)
            {
                Name = name; Status = status; Message = msg; StartTime = start; Duration = dur ?? DateTime.Now - start;
            }
        }
    }
}