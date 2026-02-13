namespace TestsFramework.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestAttribute : Attribute
{
    public string? Description { get; set; }
    
    public TestAttribute() { }
    
    public TestAttribute(string description)
    {
        Description = description;
    }
}

