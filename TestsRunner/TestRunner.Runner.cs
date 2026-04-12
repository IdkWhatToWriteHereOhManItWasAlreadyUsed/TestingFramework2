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

            var filteredInstances = ApplyFilter(testInstances);
            _totalTests = filteredInstances.Count;
            PrintInfo($"Всего тестов после фильтрации: {_totalTests}");

            if (_totalTests == 0)
            {
                PrintSummary();
                return;
            }

            PrintHeader($"ЗАПУСК ТЕСТОВ (ПАРАЛЛЕЛЬНО)", ConsoleColor.Green);
            PrintSeparator();


            var sortedInstances = filteredInstances.OrderBy(x => GetPriority(x.Method)).ToList();

            foreach (var instance in sortedInstances)
            {
                _threadPool.Enqueue(() => ExecuteTest(instance));
            }

            while (Interlocked.CompareExchange(ref _completedTests, 0, 0) < _totalTests)
            {
                Thread.Sleep(100);
            }

            PrintSummary();
        }
    }
}