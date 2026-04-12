using System;
using System.Collections.Generic;
using System.Linq;

namespace TestsRunner
{
    public partial class TestRunner
    {
        private void PrintSummary()
        {
            PrintSeparator('=');
            PrintHeader(" СВОДКА РЕЗУЛЬТАТОВ ТЕСТИРОВАНИЯ ", ConsoleColor.Magenta, true);
            PrintSeparator('-');

            List<TestResult> results;
            lock (_results)
            {
                results = _results.ToList();
            }

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
                var rateColor = successRate >= 80 ? ConsoleColor.Green :
                               successRate >= 50 ? ConsoleColor.Yellow :
                               ConsoleColor.Red;
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
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}/{_totalTests}] ");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($" НАЧАТ:   ");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"{name}");
                Console.ResetColor();
            });
        }

        private void PrintTestPassed(string name, int id, TimeSpan duration)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}/{_totalTests}] ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($" ПРОЙДЕН: ");
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write($"{name} ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"({duration.TotalMilliseconds:F0} мс)");
                Console.ResetColor();
            });
        }

        private void PrintTestFailed(string name, string message, int id, TimeSpan duration)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}/{_totalTests}] ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($" ПРОВАЛ:  ");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"{name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      Причина: {message}");
                Console.WriteLine($"      Время: {duration.TotalMilliseconds:F0} мс");
                Console.ResetColor();
            });
        }

        private void PrintTestError(string name, string message, int id, TimeSpan duration)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}/{_totalTests}] ");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write($" ОШИБКА:  ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      Ошибка: {message}");
                Console.WriteLine($"      Время: {duration.TotalMilliseconds:F0} мс");
                Console.ResetColor();
            });
        }

        private void PrintTestTimeout(string name, int timeoutMs, int id, TimeSpan duration)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}/{_totalTests}] ");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write($" ТАЙМАУТ: ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      Превышен лимит: {timeoutMs} мс");
                Console.WriteLine($"      Время: {duration.TotalMilliseconds:F0} мс");
                Console.ResetColor();
            });
        }

        private void PrintTestSkipped(string name, string reason, int id)
        {
            Locked(() =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{id:D3}/{_totalTests}] ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($" ПРОПУЩЕН:");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"{name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      Причина: {reason}");
                Console.ResetColor();
            });
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
    }
}