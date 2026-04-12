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
    public partial class TestRunner
    {
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

            _threadPool = new MyThreadPool(
                minThreads: 2,
                maxThreads: Environment.ProcessorCount,
                idleTimeout: TimeSpan.FromSeconds(2),
                queueScaleThreshold: 5
            );
            

            SubscribeToThreadPoolEvents();

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

            _threadPool.WaitForAllTasks();
            _threadPool.StopAndWait(3000);
            PrintSummary();
        }
    }
}