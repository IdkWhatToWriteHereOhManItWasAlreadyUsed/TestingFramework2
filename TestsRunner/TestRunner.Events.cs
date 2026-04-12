// TestRunner.Events.cs - новый файл
using System;
using MyThreading;

namespace TestsRunner
{
    public partial class TestRunner
    {
        private void SubscribeToThreadPoolEvents()
        {
            _threadPool.PoolStarted += OnPoolStarted;
            _threadPool.PoolStopped += OnPoolStopped;
            _threadPool.WorkerCreated += OnWorkerCreated;
            _threadPool.WorkerTerminated += OnWorkerTerminated;
            _threadPool.WorkerIdleTimeout += OnWorkerIdleTimeout;
            _threadPool.TaskStarted += OnTaskStarted;
            _threadPool.TaskCompleted += OnTaskCompleted;
            _threadPool.TaskFailed += OnTaskFailed;
            _threadPool.ScalingUp += OnScalingUp;
        }

        private void OnPoolStarted(object? sender, ThreadPoolEventArgs e)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"[ПУЛ] ");
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"{e.EventType} | Потоков: {e.ActiveWorkers}, Очередь: {e.QueueSize}");
                Console.ResetColor();
            });
        }

        private void OnPoolStopped(object? sender, ThreadPoolEventArgs e)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"[ПУЛ] ");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"{e.EventType}");
                Console.ResetColor();
            });
        }

        private void OnWorkerCreated(object? sender, ThreadPoolEventArgs e)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write($"[ПУЛ] ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Worker #{e.WorkerId} создан");
                Console.ResetColor();
            });
        }

        private void OnWorkerTerminated(object? sender, ThreadPoolEventArgs e)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"[ПУЛ] ");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"Worker #{e.WorkerId} завершен");
                Console.ResetColor();
            });
        }

        private void OnWorkerIdleTimeout(object? sender, ThreadPoolEventArgs e)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write($"[ПУЛ] ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"Worker #{e.WorkerId} таймаут простоя");
                Console.ResetColor();
            });
        }

        private void OnTaskStarted(object? sender, ThreadPoolEventArgs e)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[ПУЛ] ");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"Задача стартовала на Worker #{e.WorkerId}");
                Console.ResetColor();
            });
        }

        private void OnTaskCompleted(object? sender, ThreadPoolEventArgs e)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"[ПУЛ] ");
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"Задача завершена на Worker #{e.WorkerId}");
                Console.ResetColor();
            });
        }

        private void OnTaskFailed(object? sender, ThreadPoolEventArgs e)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"[ПУЛ] ");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Задача провалилась на Worker #{e.WorkerId}: {e.Message}");
                Console.ResetColor();
            });
        }

        private void OnScalingUp(object? sender, ThreadPoolEventArgs e)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"[ПУЛ] ");
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"{e.Message}");
                Console.ResetColor();
            });
        }
    }
}