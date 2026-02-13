using System;
using TestsFramework.Runner;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        string testAssemblyPath = @"D:\Testing_framework\Tests\bin\Debug\net10.0\Tests.dll";
        //string testAssemblyPath = Console.ReadLine();
        var runner = new TestRunner(testAssemblyPath, 8);
        runner.RunAllTests();
        
        Console.WriteLine("\nНажмите любую клавишу для выхода...");
        Console.ReadKey();
    }
}