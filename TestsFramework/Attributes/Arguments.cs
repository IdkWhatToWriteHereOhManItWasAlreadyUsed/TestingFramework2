using System;

namespace TestsFramework.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ArgumentsAttribute(params object[] values) : Attribute
    {
        public object[] Values { get; } = values;
    }
}