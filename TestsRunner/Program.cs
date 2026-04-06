using System;
using TestsRunner;
using MyThreading;

static void RunTestsRunner()
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    string testAssemblyPath = @"C:\Users\dmitry\source\repos\TestingFramework2\Tests\bin\Debug\net10.0\Tests.dll";
    //string testAssemblyPath = Console.ReadLine();
    var runner = new TestRunner(testAssemblyPath);
    runner.RunAllTests();

    Console.WriteLine("\nНажмите любую клавишу для выхода...");
    Console.ReadKey();
}

static void TestMyThreadPool()
{

}


RunTestsRunner();
//TestMyThreadPool();