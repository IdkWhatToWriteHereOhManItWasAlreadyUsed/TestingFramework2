using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TestsFramework.Attributes;

namespace TestsRunner
{
    public partial class TestRunner
    {
        public delegate bool TestFilter(MethodInfo method);

        private TestFilter? _currentFilter;

        public void SetFilter(TestFilter filter)
        {
            _currentFilter = filter;
            PrintInfo($"Установлен фильтр тестов");
        }

        public void ClearFilter()
        {
            _currentFilter = null;
            PrintInfo($"Фильтр сброшен");
        }

        private List<TestMethodInstance> ApplyFilter(List<TestMethodInstance> instances)
        {
            if (_currentFilter == null) return instances;

            var filtered = instances.Where(inst => _currentFilter(inst.Method)).ToList();
            var skipped = instances.Count - filtered.Count;

            if (skipped > 0)
            {
                Locked(() =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n🔍 Фильтр отсеял {skipped} тестов, запускается {filtered.Count}\n");
                    Console.ResetColor();
                });
            }

            return filtered;
        }

        public static TestFilter ByCategory(string category)
        {
            return method =>
            {
                var methodCat = method.GetCustomAttribute<CategoryAttribute>();
                if (methodCat != null && methodCat.Name == category)
                    return true;

                var classCat = method.DeclaringType?.GetCustomAttribute<CategoryAttribute>();
                return classCat != null && classCat.Name == category;
            };
        }

        public static TestFilter ByMinPriority(int minPriority)
        {
            return method =>
            {
                var priority = method.GetCustomAttribute<PriorityAttribute>();
                return priority != null && priority.Level >= minPriority;
            };
        }

        public static TestFilter ByAuthor(string author)
        {
            return method =>
            {
                var authorAttr = method.GetCustomAttribute<AuthorAttribute>();
                return authorAttr != null && authorAttr.Name == author;
            };
        }

        public static TestFilter ByNameContains(string substring)
        {
            return method => method.Name.Contains(substring, StringComparison.OrdinalIgnoreCase);
        }

        public static TestFilter ExcludeSkipped()
        {
            return method => method.GetCustomAttribute<SkipAttribute>() == null;
        }

        public static TestFilter And(TestFilter first, TestFilter second)
        {
            return method => first(method) && second(method);
        }

        public static TestFilter Or(TestFilter first, TestFilter second)
        {
            return method => first(method) || second(method);
        }
    }
}