using System;

namespace TestsFramework.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class AuthorAttribute : Attribute
    {
        public string Name { get; }

        public AuthorAttribute(string name)
        {
            Name = name;
        }
    }
}