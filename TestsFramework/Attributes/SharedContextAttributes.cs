namespace TestsFramework.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SetupAttribute : Attribute
{
    
}


[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class CleanupAttribute : Attribute
{
    
}


[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class SharedContextAttribute(string contextName) : Attribute
{
    public string ContextName { get; set; } = contextName;
}