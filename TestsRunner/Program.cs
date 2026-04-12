// TestsRunner/Program.cs
using System;
using TestsRunner;
using MyThreading;

static void RunTestsWithFilters()
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    string testAssemblyPath = @"C:\Users\dmitry\source\repos\TestingFramework2\Tests\bin\Debug\net10.0\Tests.dll";

    var runner = new TestRunner(testAssemblyPath);

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║           ТОЛЬКО ТЕСТЫ КАТЕГОРИИ 'WaitingTest'              ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.ResetColor();

    runner.SetFilter(TestRunner.ByCategory("WaitingTest"));
    runner.RunAllTests();

    Thread.Sleep(5000);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║           ТОЛЬКО ТЕСТЫ КАТЕГОРИИ 'Encryption'               ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.ResetColor();

    runner.SetFilter(TestRunner.ByCategory("Encryption"));
    runner.RunAllTests();

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║           ТОЛЬКО ТЕСТЫ С PRIORITY >= 3                       ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.ResetColor();

    runner.SetFilter(TestRunner.ByMinPriority(2));
    runner.RunAllTests();

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║     ТЕСТЫ С PRIORITY >= 2 И КАТЕГОРИЕЙ 'Encryption'          ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.ResetColor();

    var combinedFilter = TestRunner.And(
        TestRunner.ByMinPriority(2),
        TestRunner.ByCategory("Encryption")
    );
    runner.SetFilter(combinedFilter);
    runner.RunAllTests();

}

RunTestsWithFilters();
Console.ReadKey();