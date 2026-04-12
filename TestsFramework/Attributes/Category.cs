using System;

namespace TestsFramework.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class CategoryAttribute : Attribute
    {
        public string Name { get; }

        public CategoryAttribute(string name)
        {
            Name = name;
        }
    }
}