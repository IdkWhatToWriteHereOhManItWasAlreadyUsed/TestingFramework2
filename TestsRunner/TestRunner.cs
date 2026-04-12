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
        private readonly string _assemblyPath;
        private readonly List<TestResult> _results = [];
        private readonly Mutex _consoleMutex = new();
        private int _completedTests = 0;
        private int _totalTests = 0;
        private MyThreadPool _threadPool;

        public TestRunner(string assemblyPath)
        {
            _assemblyPath = assemblyPath;

            _threadPool = new MyThreadPool(
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

            SubscribeToThreadPoolEvents();
            _threadPool.Start();
        }
    }
}