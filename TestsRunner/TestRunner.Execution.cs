using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using TestsFramework.Assert;
using TestsFramework.Attributes;

namespace TestsRunner
{
    public partial class TestRunner
    {
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
    }
}