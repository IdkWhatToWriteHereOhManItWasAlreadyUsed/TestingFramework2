using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TestsFramework.Attributes;

namespace TestsRunner
{
    public partial class TestRunner
    {
        private static List<Type> GetTestClasses(Assembly assembly)
        {
            return [.. assembly.GetTypes().Where(t => t.GetCustomAttribute<TestClassAttribute>() != null)];
        }

        private static List<TestMethodInstance> GetAllTestInstances(List<Type> testClasses)
        {
            var instances = new List<TestMethodInstance>();

            foreach (var testClass in testClasses)
            {
                var testMethods = testClass.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null);

                foreach (var method in testMethods)
                {
                    var rangeAttrs = method.GetCustomAttributes<IntegerRangesArgs>().ToList();

                    if (rangeAttrs.Count > 0)
                    {
                        foreach (var rangeAttr in rangeAttrs)
                        {
                            foreach (var combination in rangeAttr.GetAllCombinations())
                            {
                                instances.Add(new TestMethodInstance(testClass, method, combination));
                            }
                        }
                    }
                    else
                    {
                        var argumentsAttrs = method.GetCustomAttributes<ArgumentsAttribute>().ToList();

                        if (argumentsAttrs.Count != 0)
                        {
                            foreach (var attr in argumentsAttrs)
                            {
                                instances.Add(new TestMethodInstance(testClass, method, attr.Values));
                            }
                        }
                        else
                        {
                            instances.Add(new TestMethodInstance(testClass, method, Array.Empty<object>()));
                        }
                    }
                }
            }

            return instances;
        }

        private static MethodInfo? GetSetupMethod(Type testClass)
        {
            return testClass.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<SetupAttribute>() != null);
        }

        private static MethodInfo? GetCleanupMethod(Type testClass)
        {
            return testClass.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<CleanupAttribute>() != null);
        }

        private static int GetPriority(MethodInfo method)
        {
            return method.GetCustomAttribute<PriorityAttribute>()?.Level ?? int.MaxValue;
        }
    }
}