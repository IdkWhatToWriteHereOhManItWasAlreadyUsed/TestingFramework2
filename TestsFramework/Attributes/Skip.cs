namespace TestsFramework.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SkipAttribute(string reason) : Attribute
{
    public string Reason { get; set; } = reason;
}