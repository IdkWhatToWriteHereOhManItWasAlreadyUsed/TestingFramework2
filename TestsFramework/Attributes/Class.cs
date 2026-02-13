namespace TestsFramework.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class TestClassAttribute : Attribute
{
    public string? Category { get; set; }
    
    public TestClassAttribute() { }
    
    public TestClassAttribute(string category)
    {
        Category = category;
    }
}